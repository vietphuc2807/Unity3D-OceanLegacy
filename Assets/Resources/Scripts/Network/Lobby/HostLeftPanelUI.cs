using TMPro;
using UnityEngine;

public class HostLeftPanelUI : MonoBehaviour
{
    [SerializeField] private PanelEffect _panelEffect;

    [Header("Player")]
    [SerializeField] private PlayerMovement _playerMovement;
    [SerializeField] private PlayerLook _playerLook;

    private void Awake()
    {
        _panelEffect.Hide();
    }

    private void Start()
    {
        if (!HostLeftNotice.ShouldShow)
            return;

        HostLeftNotice.ShouldShow = false;

        ShowPanel();
    }

    private void ShowPanel()
    {
        _panelEffect.Show();

        if (_playerMovement != null) _playerMovement.enabled = false;
        if (_playerLook != null) _playerLook.enabled = false;

        PlayerCursor.IsUIOpen = true;
        PlayerCursor.UnlockCursor();
    }

    public void ClosePanel()
    {
        _panelEffect.Hide(() =>
        {
            if (_playerMovement != null) _playerMovement.enabled = true;
            if (_playerLook != null) _playerLook.enabled = true;

            PlayerCursor.IsUIOpen = false;
            PlayerCursor.LockCursor();
        });
    }
}