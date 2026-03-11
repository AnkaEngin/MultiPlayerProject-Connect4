using Unity.Collections;
using UnityEngine;

// Renders the board grid and handles drop animations when peices are placed
public class BoardRenderer : MonoBehaviour
{
    public static BoardRenderer Instance { get; private set; }

    [Header("Token Sprites")]
    [SerializeField] private Sprite emptySprite;
    [SerializeField] private Sprite redSprite;
    [SerializeField] private Sprite yellowSprite;

    [Header("Board Frame")]
    [SerializeField] private Sprite boardFrameSprite;

    private SpriteRenderer[,] cells;
    private TokenAnimator animator;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        animator = GetComponent<TokenAnimator>();
        StartCoroutine(WaitForGameManager());
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.BoardState.OnValueChanged -= OnBoardChanged;
    }

    private System.Collections.IEnumerator WaitForGameManager() // Waits for GameManager to be spawned before subscribing
    {
        yield return new WaitUntil(
            () => GameManager.Instance != null && GameManager.Instance.IsSpawned);

        CreateGrid();
        GameManager.Instance.BoardState.OnValueChanged += OnBoardChanged;
        UpdateBoard(GameManager.Instance.BoardState.Value.ToString());
    }

    private void CreateGrid() // Creates sprite renderers for each cell
    {
        cells = new SpriteRenderer[GameManager.Rows, GameManager.Cols];

        for (int row = 0; row < GameManager.Rows; row++)
        {
            for (int col = 0; col < GameManager.Cols; col++)
            {
                Vector3 pos = GetCellPos(row, col);

                // Token layer
                GameObject tokenObj = new GameObject($"Token_{row}_{col}");
                tokenObj.transform.SetParent(transform, worldPositionStays: false);
                tokenObj.transform.localPosition = pos;

                SpriteRenderer sr = tokenObj.AddComponent<SpriteRenderer>();
                sr.sprite         = emptySprite;
                sr.sortingOrder   = 0;
                cells[row, col]   = sr;

                // Frame overlay on top of tokens
                if (boardFrameSprite != null)
                {
                    GameObject frameObj = new GameObject($"Frame_{row}_{col}");
                    frameObj.transform.SetParent(transform, worldPositionStays: false);
                    frameObj.transform.localPosition = pos;

                    SpriteRenderer frameSR = frameObj.AddComponent<SpriteRenderer>();
                    frameSR.sprite         = boardFrameSprite;
                    frameSR.sortingOrder   = 2;
                }
            }
        }
    }

    private void OnBoardChanged(FixedString64Bytes previous, FixedString64Bytes current)
    {
        UpdateBoard(current.ToString());
    }

    public void UpdateBoard(string board) // Updates all cell sprites from the board string
    {
        if (cells == null || board.Length < GameManager.Rows * GameManager.Cols)
            return;

        for (int row = 0; row < GameManager.Rows; row++)
            for (int col = 0; col < GameManager.Cols; col++)
                cells[row, col].sprite = CharToSprite(board[row * GameManager.Cols + col]);
    }

    public void OnPiecePlaced(int row, int col, int playerIndex) // Triggers the drop animaton
    {
        if (cells == null) return;

        Sprite  sprite    = playerIndex == 0 ? redSprite : yellowSprite;
        Vector3 targetPos = GetCellPos(row, col);

        // Start above the top row
        Vector3 startPos = new Vector3(
            targetPos.x,
            (GameManager.Rows - 1) / 2f + 2f,
            targetPos.z);

        SpriteRenderer tokenSR = cells[row, col];

        if (animator != null)
            animator.PlayDrop(tokenSR, sprite, startPos, targetPos);
        else
            tokenSR.sprite = sprite;
    }

    private static Vector3 GetCellPos(int row, int col) // Converts board index to world position
    {
        float x = col - (GameManager.Cols - 1) / 2f;
        float y = (GameManager.Rows - 1) / 2f - row;
        return new Vector3(x, y, 0f);
    }

    private Sprite CharToSprite(char c) => c switch
    {
        '1' => redSprite,
        '2' => yellowSprite,
        _   => emptySprite,
    };
}
