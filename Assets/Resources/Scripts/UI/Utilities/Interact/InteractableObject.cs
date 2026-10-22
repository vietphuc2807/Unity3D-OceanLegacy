using UnityEngine;

public class InteractableObject : MonoBehaviour
{
    /// <summary>Object dang mo tuong tac (null neu khong co).</summary>
    public static InteractableObject Active { get; private set; }

    [Header("UI")]
    [SerializeField] private PanelEffect _serverPanel;

    [Header("Player")]
    [SerializeField] protected PlayerMovement _playerMovement;
    [SerializeField] protected PlayerLook _playerLook;

    private bool _isClosing;

    private void OnDestroy()
    {
        if (Active == this)
            Active = null;
    }

    private void Update()
    {
        if (Active != this || _isClosing)
            return;

        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        if (IsOverlayPanelBlockingEsc())
            return;

        _isClosing = true;
        Close();
    }

    public virtual void Interact()
    {
        if (Active != null)
            return;

        _serverPanel.Show();

        FreezePlayer();
    }

    public virtual void Close()
    {
        _serverPanel.Hide(UnfreezePlayer);
    }

    protected void FreezePlayer()
    {
        Active = this;
        _isClosing = false;

        _playerMovement.StopMovementAnimation();

        _playerMovement.enabled = false;
        _playerLook.enabled = false;

        PlayerCursor.IsUIOpen = true;
        PlayerCursor.UnlockCursor();
    }

    protected void UnfreezePlayer()
    {
        if (Active == this)
            Active = null;

        _isClosing = false;

        _playerMovement.enabled = true;
        _playerLook.enabled = true;

        PlayerCursor.IsUIOpen = false;
        PlayerCursor.LockCursor();
    }

    protected static bool IsOverlayPanelBlockingEsc()
    {
        int frame = Time.frameCount;

        return SettingsUI.IsOpen
            || ExitMuseumUI.IsOpen
            || MuseumLeaderboardUI.IsOpen
            || SettingsUI.EscClosedFrame == frame
            || ExitMuseumUI.EscClosedFrame == frame
            || MuseumLeaderboardUI.EscClosedFrame == frame;
    }
}