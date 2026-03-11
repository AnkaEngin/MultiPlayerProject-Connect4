using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// Attached to the player prefab, client-owned so it can send RPCs to the server
public class PlayerInputHandler : NetworkBehaviour
{
    // Server sets this after spawning, -1 means not assigned yet
    public NetworkVariable<int> PlayerIndex = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private void Update() // Detects mouse clicks and sends moves to server
    {
        if (!IsOwner) return;
        if (PlayerIndex.Value < 0) return;

        if (GameManager.Instance == null) return;
        if (!GameManager.Instance.IsSpawned) return;
        if (GameManager.Instance.GamePhase.Value != 1) return;
        if (GameManager.Instance.CurrentTurn.Value != PlayerIndex.Value) return;

        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        int col = GetClickedColumn();
        if (col < 0 || col >= GameManager.Cols) return;

        // Quick client side check if column is full, server validates again anyway
        string board = GameManager.Instance.BoardState.Value.ToString();
        if (GameManager.FindOpenRow(board, col) < 0) return;

        SubmitMoveServerRpc(col);
    }

    [Rpc(SendTo.Server)]
    private void SubmitMoveServerRpc(int column, RpcParams rpcParams = default) // Sends move to server with ownership check
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        GameManager.Instance?.ProcessMove(column, PlayerIndex.Value);
    }

    [Rpc(SendTo.Server)]
    public void RequestRematchServerRpc(RpcParams rpcParams = default) // Sends rematch vote to server
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        GameManager.Instance?.HandleRematchVote();
    }

    private int GetClickedColumn() // Converts mouse screen pos to board column index
    {
        if (Camera.main == null) return -1;

        // z needs to be the camera distance for 2D orthographic projecton
        Vector2 screenPos = Mouse.current.position.ReadValue();
        Vector3 mousePos  = new Vector3(screenPos.x, screenPos.y, Mathf.Abs(Camera.main.transform.position.z));
        Vector3 worldPos  = Camera.main.ScreenToWorldPoint(mousePos);

        return Mathf.FloorToInt(worldPos.x + GameManager.Cols / 2f);
    }
}
