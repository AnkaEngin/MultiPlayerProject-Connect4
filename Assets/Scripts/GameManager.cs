using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Server-authoritative game logic, placed in the scene as an in-scene NetworkObject
public class GameManager : NetworkBehaviour
{
    public const int Cols = 7;
    public const int Rows = 6;
    public const string EmptyBoard = "000000000000000000000000000000000000000000";

    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    [SerializeField] private GameObject playerPrefab;

    // Board is a 42 char string, index = row * 7 + col. 0=empty 1=P1 2=P2
    public NetworkVariable<FixedString64Bytes> BoardState =
        new NetworkVariable<FixedString64Bytes>(
            new FixedString64Bytes(EmptyBoard),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public NetworkVariable<int> CurrentTurn =
        new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    // 0 = waiting, 1 = playing, 2 = game over
    public NetworkVariable<int> GamePhase =
        new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public NetworkVariable<int> ScoreP1 =
        new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public NetworkVariable<int> ScoreP2 =
        new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    // Maps client IDs to player index (0 or 1), server only
    private readonly Dictionary<ulong, int> playerMap = new Dictionary<ulong, int>();
    private int rematchCount = 0;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientConnectedCallback  += OnPlayerJoined;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnPlayerLeft;

        // Register the host manually since the callback fires after OnNetworkSpawn
        OnPlayerJoined(NetworkManager.Singleton.LocalClientId);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;
        NetworkManager.Singleton.OnClientConnectedCallback  -= OnPlayerJoined;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnPlayerLeft;
    }

    private void OnPlayerJoined(ulong clientId) // Assigns player index and spawns their object
    {
        if (playerMap.ContainsKey(clientId)) return; // dedup guard for host
        if (playerMap.Count >= 2) return;

        int index = playerMap.Count;
        playerMap[clientId] = index;
        Debug.Log($"[GameManager] Client {clientId} → Player {index + 1}");

        // SpawnAsPlayerObject makes the client the owner so they can send RPCs
        GameObject obj = Instantiate(playerPrefab);
        NetworkObject netObj = obj.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId, destroyWithScene: true);

        PlayerInputHandler handler = obj.GetComponent<PlayerInputHandler>();
        handler.PlayerIndex.Value = index;

        if (playerMap.Count == 2)
            BeginGame();
    }

    private void OnPlayerLeft(ulong clientId) // Notifes remaining clients about disconnect
    {
        if (!playerMap.ContainsKey(clientId)) return;

        int idx = playerMap[clientId];
        Debug.Log($"[GameManager] Player {idx + 1} (client {clientId}) disconnected.");
        playerMap.Remove(clientId);

        if (GamePhase.Value == 1 || GamePhase.Value == 2)
        {
            GamePhase.Value = 0;
            SendDisconnectRpc();
        }
    }

    private void BeginGame()
    {
        BoardState.Value  = new FixedString64Bytes(EmptyBoard);
        CurrentTurn.Value = 0;
        GamePhase.Value   = 1;
        rematchCount      = 0;
        Debug.Log("[GameManager] Game started!");
    }

    public void ProcessMove(int column, int playerIndex) // Called by PlayerInputHandler RPCs
    {
        if (!IsServer) return;
        if (GamePhase.Value != 1) return;
        if (playerIndex != CurrentTurn.Value) return;
        if (column < 0 || column >= Cols) return;

        int row = FindOpenRow(BoardState.Value.ToString(), column);
        if (row < 0) return;

        char piece = playerIndex == 0 ? '1' : '2';
        string newBoard = PlacePiece(BoardState.Value.ToString(), row, column, piece);
        BoardState.Value = new FixedString64Bytes(newBoard);

        // Tell clients to play the drop animation
        SendPiecePlacedRpc(row, column, playerIndex);

        if (CheckWin(newBoard, row, column, piece))
        {
            if (playerIndex == 0) ScoreP1.Value++;
            else                  ScoreP2.Value++;

            GamePhase.Value = 2;
            SendGameOverRpc(playerIndex, false);
            return;
        }

        if (IsFull(newBoard))
        {
            GamePhase.Value = 2;
            SendGameOverRpc(-1, true);
            return;
        }

        CurrentTurn.Value = 1 - CurrentTurn.Value;
    }

    public void HandleRematchVote() // Called by PlayerInputHandler rematch RPC
    {
        if (!IsServer) return;
        if (GamePhase.Value != 2) return;

        rematchCount++;
        Debug.Log($"[GameManager] Rematch votes: {rematchCount}/2");

        if (rematchCount >= 2)
        {
            BeginGame();
            SendRematchRpc();
        }
    }

    // RPCs that broadcast events to all clients
    [Rpc(SendTo.ClientsAndHost)]
    private void SendPiecePlacedRpc(int row, int col, int playerIndex)
    {
        BoardRenderer.Instance?.OnPiecePlaced(row, col, playerIndex);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SendGameOverRpc(int winnerIndex, bool isDraw)
    {
        UIManager.Instance?.ShowGameOver(winnerIndex, isDraw);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SendRematchRpc()
    {
        UIManager.Instance?.HideGameOver();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SendDisconnectRpc()
    {
        UIManager.Instance?.ShowDisconnectMessage();
    }

    // Board logic helpers

    private static string PlacePiece(string board, int row, int col, char piece)
    {
        char[] arr = board.ToCharArray();
        arr[row * Cols + col] = piece;
        return new string(arr);
    }

    public static int FindOpenRow(string board, int col) // Scans bottom up for empty row
    {
        for (int row = Rows - 1; row >= 0; row--)
        {
            if (board[row * Cols + col] == '0')
                return row;
        }
        return -1;
    }

    private static bool CheckWin(string board, int row, int col, char piece) // Checks all 4 directions
    {
        return CountDir(board, row, col, piece, 0,  1) >= 4
            || CountDir(board, row, col, piece, 1,  0) >= 4
            || CountDir(board, row, col, piece, 1,  1) >= 4
            || CountDir(board, row, col, piece, 1, -1) >= 4;
    }

    private static int CountDir(string board, int row, int col, char piece, int dr, int dc) // Counts consecutive peices in both directions
    {
        int count = 1;

        for (int i = 1; i < 4; i++)
        {
            int r = row + dr * i, c = col + dc * i;
            if (r < 0 || r >= Rows || c < 0 || c >= Cols) break;
            if (board[r * Cols + c] != piece) break;
            count++;
        }
        for (int i = 1; i < 4; i++)
        {
            int r = row - dr * i, c = col - dc * i;
            if (r < 0 || r >= Rows || c < 0 || c >= Cols) break;
            if (board[r * Cols + c] != piece) break;
            count++;
        }

        return count;
    }

    private static bool IsFull(string board) => !board.Contains('0');
}
