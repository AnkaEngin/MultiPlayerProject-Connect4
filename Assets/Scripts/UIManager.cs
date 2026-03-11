using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// Controls all in-game UI panels and listens to network variable changes
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    [Header("HUD")]
    [SerializeField] private TMP_Text turnText;
    [SerializeField] private TMP_Text scoreText;

    [Header("Panels")]
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private GameObject waitingPanel;

    [Header("Game Over")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text gameOverText;
    [SerializeField] private Button rematchButton;

    [Header("Disconnect")]
    [SerializeField] private TMP_Text disconnectText;

    private void Start()
    {
        rematchButton.onClick.AddListener(OnRematchClicked);

        // Hide all gameplay panels at start
        if (hudPanel      != null) hudPanel.SetActive(false);
        if (waitingPanel  != null) waitingPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (disconnectText != null) disconnectText.gameObject.SetActive(false);

        StartCoroutine(WaitForGameManager());
    }

    private void OnDestroy()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.CurrentTurn.OnValueChanged -= OnTurnChanged;
        GameManager.Instance.ScoreP1.OnValueChanged     -= OnScoreChanged;
        GameManager.Instance.ScoreP2.OnValueChanged     -= OnScoreChanged;
        GameManager.Instance.GamePhase.OnValueChanged   -= OnPhaseChanged;
    }

    private System.Collections.IEnumerator WaitForGameManager() // Waits for GameManager to be spawned before subscribing
    {
        yield return new WaitUntil(
            () => GameManager.Instance != null && GameManager.Instance.IsSpawned);

        GameManager.Instance.CurrentTurn.OnValueChanged += OnTurnChanged;
        GameManager.Instance.ScoreP1.OnValueChanged     += OnScoreChanged;
        GameManager.Instance.ScoreP2.OnValueChanged     += OnScoreChanged;
        GameManager.Instance.GamePhase.OnValueChanged   += OnPhaseChanged;

        // Render current state immediatly in case we joined mid game
        UpdateTurnText(GameManager.Instance.CurrentTurn.Value);
        UpdateScoreText();
        OnPhaseChanged(0, GameManager.Instance.GamePhase.Value);
    }

    private void OnTurnChanged(int prev, int curr) => UpdateTurnText(curr);

    private void OnScoreChanged(int prev, int curr) => UpdateScoreText();

    private void OnPhaseChanged(int prev, int curr) // Shows/hides panels based on game phase
    {
        if (hudPanel     != null) hudPanel.SetActive(curr == 1);
        if (waitingPanel != null) waitingPanel.SetActive(curr == 0);
    }

    public void ShowGameOver(int winnerIndex, bool isDraw) // Called by GameManager ClientRpc
    {
        if (gameOverPanel == null) return;

        gameOverPanel.SetActive(true);
        rematchButton.interactable = true;

        if (isDraw)
        {
            gameOverText.text = "It's a Draw!";
        }
        else
        {
            string color      = winnerIndex == 0 ? "Red" : "Yellow";
            gameOverText.text = $"Player {winnerIndex + 1} ({color}) Wins!";
        }
    }

    public void HideGameOver() // Called when rematch starts
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        rematchButton.interactable = true;
    }

    public void ShowDisconnectMessage()
    {
        if (disconnectText == null) return;
        disconnectText.gameObject.SetActive(true);
        disconnectText.text = "Opponent disconnected.";
    }

    private void OnRematchClicked() // Routes through client-owned PlayerInputHandler since GameManager is server-owend
    {
        rematchButton.interactable = false;

        NetworkObject localPlayer =
            NetworkManager.Singleton?.SpawnManager?.GetLocalPlayerObject();

        if (localPlayer == null)
        {
            Debug.LogWarning("[UIManager] Could not find local player object for rematch.");
            rematchButton.interactable = true;
            return;
        }

        localPlayer.GetComponent<PlayerInputHandler>()?.RequestRematchServerRpc();
    }

    private void UpdateTurnText(int currentTurn)
    {
        if (turnText == null) return;
        string color  = currentTurn == 0 ? "Red" : "Yellow";
        turnText.text = $"Player {currentTurn + 1} ({color})'s Turn";
    }

    private void UpdateScoreText()
    {
        if (scoreText == null || GameManager.Instance == null) return;
        scoreText.text =
            $"Red: {GameManager.Instance.ScoreP1.Value}  |  " +
            $"Yellow: {GameManager.Instance.ScoreP2.Value}";
    }
}
