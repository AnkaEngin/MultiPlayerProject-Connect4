using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Menu UI that lets the player create or join a lobby
public class NetworkManagerUI : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private TMP_InputField lobbyInput;

    [Header("Buttons")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;

    [Header("Status")]
    [SerializeField] private TMP_Text statusText;

    [Header("Panels")]
    [SerializeField] private GameObject menuPanel;

    private void Start()
    {
        if (LobbyManager.Instance == null)
        {
            Debug.LogError("[NetworkManagerUI] LobbyManager.Instance is null! " +
                           "Add a LobbyManager GameObject to the scene.");
            SetStatus("Setup error — check console.");
            hostButton.interactable = false;
            joinButton.interactable = false;
            return;
        }

        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(OnJoinClicked);

        LobbyManager.Instance.OnLobbyCreated += OnLobbyMade;
        LobbyManager.Instance.OnJoinedLobby  += OnJoinedLobby;
        LobbyManager.Instance.OnLobbyError   += OnError;

        SetStatus("");
    }

    private void OnDestroy()
    {
        if (LobbyManager.Instance == null) return;
        LobbyManager.Instance.OnLobbyCreated -= OnLobbyMade;
        LobbyManager.Instance.OnJoinedLobby  -= OnJoinedLobby;
        LobbyManager.Instance.OnLobbyError   -= OnError;
    }

    private void OnHostClicked()
    {
        string name = GetLobbyName();
        if (name == null) return;

        SetButtonsEnabled(false);
        SetStatus("Creating lobby...");
        LobbyManager.Instance.CreateLobby(name);
    }

    private void OnJoinClicked()
    {
        string name = GetLobbyName();
        if (name == null) return;

        SetButtonsEnabled(false);
        SetStatus("Searching for lobby...");
        LobbyManager.Instance.JoinLobbyByName(name);
    }

    private void OnLobbyMade() // Hides menu and waits for oponnent
    {
        menuPanel.SetActive(false);
        SetStatus("Waiting for opponent...");
    }

    private void OnJoinedLobby()
    {
        menuPanel.SetActive(false);
        SetStatus("Connected! Starting game...");
    }

    private void OnError(string message) // Re-enables buttons so player can try again
    {
        SetButtonsEnabled(true);
        SetStatus($"Error: {message}");
    }

    private string GetLobbyName()
    {
        string name = lobbyInput.text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            SetStatus("Please enter a session name.");
            return null;
        }
        return name;
    }

    private void SetButtonsEnabled(bool value)
    {
        hostButton.interactable = value;
        joinButton.interactable = value;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}
