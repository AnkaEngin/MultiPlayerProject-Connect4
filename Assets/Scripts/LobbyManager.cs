using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

// Handles all lobby and relay networking so the UI scripts dont have to
public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private Lobby currentLobby;
    private float heartbeatTimer;
    private const float heartbeatDelay = 15f;
    private const string relayCodeKey = "relayCode";

    // Events for the menu UI to listen to
    public event Action OnLobbyCreated;
    public event Action OnJoinedLobby;
    public event Action<string> OnLobbyError;

    private async void Start()
    {
        await SetupServices();
    }

    private void Update() // Keeps the lobby alive with heartbeat pings
    {
        if (currentLobby == null) return;
        heartbeatTimer += Time.deltaTime;
        if (heartbeatTimer >= heartbeatDelay)
        {
            heartbeatTimer = 0f;
            _ = LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
        }
    }

    private async Task SetupServices() // Signs in anonymously so each player gets a unique ID
    {
        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[LobbyManager] Signed in anonymously. " +
                           $"PlayerID = {AuthenticationService.Instance.PlayerId}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LobbyManager] Service initialization failed: {e.Message}");
            OnLobbyError?.Invoke("Could not connect to Unity Services: " + e.Message);
        }
    }

    public async void CreateLobby(string lobbyName) // Allocates relay, creates lobby, starts host
    {
        try
        {
            // Only need 1 connection since its just host + 1 player
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
            string joinCode       = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log($"[LobbyManager] Relay allocation created. Join code: {joinCode}");

            // Store the relay join code in lobby public data so the joiner can read it
            var options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    {
                        relayCodeKey,
                        new DataObject(DataObject.VisibilityOptions.Public, joinCode)
                    }
                }
            };

            currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers: 2, options);
            Debug.Log($"[LobbyManager] Lobby '{currentLobby.Name}' created (id={currentLobby.Id})");

            // Configure transport with relay alocation data
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData);

            NetworkManager.Singleton.StartHost();
            OnLobbyCreated?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[LobbyManager] CreateLobby failed: {e.Message}");
            OnLobbyError?.Invoke("Failed to create lobby: " + e.Message);
        }
    }

    public async void JoinLobbyByName(string lobbyName) // Finds lobby by name, joins relay, starts client
    {
        try
        {
            var queryOptions = new QueryLobbiesOptions
            {
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(
                        QueryFilter.FieldOptions.Name,
                        lobbyName,
                        QueryFilter.OpOptions.EQ)
                }
            };

            QueryResponse result = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);

            if (result.Results.Count == 0)
            {
                OnLobbyError?.Invoke($"No lobby named '{lobbyName}' found. " +
                                      "Check the name and try again.");
                return;
            }

            Lobby lobby = await LobbyService.Instance.JoinLobbyByIdAsync(result.Results[0].Id);
            Debug.Log($"[LobbyManager] Joined lobby '{lobby.Name}'");

            if (!lobby.Data.TryGetValue(relayCodeKey, out DataObject codeData))
            {
                OnLobbyError?.Invoke("Lobby has no relay code yet. Host may still be setting up.");
                return;
            }

            // Join the relay using the code from the lobby
            JoinAllocation joinAlloc = await RelayService.Instance.JoinAllocationAsync(codeData.Value);
            Debug.Log($"[LobbyManager] Joined Relay allocation.");

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetClientRelayData(
                joinAlloc.RelayServer.IpV4,
                (ushort)joinAlloc.RelayServer.Port,
                joinAlloc.AllocationIdBytes,
                joinAlloc.Key,
                joinAlloc.ConnectionData,
                joinAlloc.HostConnectionData);

            NetworkManager.Singleton.StartClient();
            OnJoinedLobby?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[LobbyManager] JoinLobbyByName failed: {e.Message}");
            OnLobbyError?.Invoke("Failed to join lobby: " + e.Message);
        }
    }
}
