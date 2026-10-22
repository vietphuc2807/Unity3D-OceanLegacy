using UnityEngine;
using UnityEngine.SceneManagement;

public class ExitMuseumUI : MonoBehaviour
{
    /// <summary>
    /// True khi panel Exit dang mo (cho toi khi animation Hide xong).
    /// Script khac (VD AquariumInteractableObject) dung de bo qua Esc.
    /// </summary>
    public static bool IsOpen { get; private set; }

    /// <summary>
    /// Frame ma Esc vua duoc panel nay dung de DONG chinh no. Dung de
    /// script khac bo qua Esc trong CUNG frame do (phong khi thu tu
    /// Update() khac nhau).
    /// </summary>
    public static int EscClosedFrame { get; private set; } = -1;

    [Header("UI")]
    [SerializeField] private PanelEffect _returnToMenuPanel;

    [Header("Player")]
    [SerializeField] private PlayerMovement _playerMovement;
    [SerializeField] private PlayerLook _playerLook;

    [Header("Scene")]
    [SerializeField] private string _menuScene = "S-Menu";

    private bool _isPanelOpen;
    private bool _isClosing;

    // Trang thai TRUOC khi mo panel Exit - de khoi phuc dung khi dong
    // (VD dang o AquariumView thi van giu frozen + IsUIOpen = true).
    private bool _prevMovementEnabled;
    private bool _prevLookEnabled;
    private bool _prevUIOpen;

    private void OnDestroy()
    {
        IsOpen = false;
    }

    private void Update()
    {
        if (!_isPanelOpen)
        {
            // Settings hoac Leaderboard dang mo -> khong cho mo Exit chong len.
            if (SettingsUI.IsOpen || MuseumLeaderboardUI.IsOpen)
                return;

            if (Input.GetKeyDown(KeyCode.X))
                OpenPanel();

            return;
        }

        // Panel dang mo: Esc de dong.
        if (Input.GetKeyDown(KeyCode.Escape) && !_isClosing)
        {
            EscClosedFrame = Time.frameCount;
            ClosePanel();
        }
    }

    public void OpenPanel()
    {
        if (_isPanelOpen)
            return;

        _isPanelOpen = true;
        _isClosing = false;
        IsOpen = true;

        _prevMovementEnabled = _playerMovement.enabled;
        _prevLookEnabled = _playerLook.enabled;
        _prevUIOpen = PlayerCursor.IsUIOpen;

        _returnToMenuPanel.Show();

        _playerMovement.enabled = false;
        _playerLook.enabled = false;

        PlayerCursor.IsUIOpen = true;
        PlayerCursor.UnlockCursor();
    }

    public void ClosePanel()
    {
        if (!_isPanelOpen || _isClosing)
            return;

        _isClosing = true;

        _returnToMenuPanel.Hide(() =>
        {
            _isPanelOpen = false;
            _isClosing = false;
            IsOpen = false;

            _playerMovement.enabled = _prevMovementEnabled;
            _playerLook.enabled = _prevLookEnabled;
            PlayerCursor.IsUIOpen = _prevUIOpen;

            if (_prevUIOpen)
                PlayerCursor.UnlockCursor();
            else
                PlayerCursor.LockCursor();
        });
    }

    public void ReturnToMenu()
    {
        _returnToMenuPanel.Hide(() =>
        {
            IsOpen = false;
            PlayerCursor.IsUIOpen = false;
            SceneManager.LoadScene(_menuScene);
        });
    }
}