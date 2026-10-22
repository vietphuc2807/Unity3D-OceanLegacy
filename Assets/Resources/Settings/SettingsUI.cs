using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    /// <summary>True khi panel Settings dang mo (cho toi khi Hide xong).</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>Frame ma Esc vua dung de DONG Settings.</summary>
    public static int EscClosedFrame { get; private set; } = -1;

    [Serializable]
    private class Tab
    {
        public string Title;            // VD "SOUND"
        public Button Button;           // nut tab
        public GameObject Content;      // noi dung tab (de trong neu chua lam)

        [Tooltip("Sprite (khung + icon) khi tab DANG BAT - xanh toi.")]
        public Sprite ActiveSprite;

        [Tooltip("Sprite (khung + icon) khi tab DANG TAT - sang.")]
        public Sprite InactiveSprite;
    }

    [Header("UI")]
    [SerializeField] private PanelEffect _panel;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private Button _backButton;

    [Header("Tabs (thu tu: Sound, Graphics, Bug, Exit)")]
    [SerializeField] private Tab[] _tabs;

    [Header("Che do theo scene")]
    [Tooltip("BAT o scene Museum (Esc mo Settings). TAT o scene Menu (mo bang nut Options).")]
    [SerializeField] private bool _openWithEsc = true;

    [Tooltip("BAT o scene Gameplay (khoa/mo chuot, dung nhan vat). TAT o scene Menu.")]
    [SerializeField] private bool _manageCursor = true;

    [Header("Player (tuy chon - de trong o scene khong co nhan vat)")]
    [SerializeField] private PlayerMovement _playerMovement;
    [SerializeField] private PlayerLook _playerLook;

    private bool _isPanelOpen;
    private bool _isClosing;

    private bool _prevMovementEnabled;
    private bool _prevLookEnabled;
    private bool _prevUIOpen;

    private void Awake()
    {
        for (int i = 0; i < _tabs.Length; i++)
        {
            int index = i;

            if (_tabs[i].Button != null)
                _tabs[i].Button.onClick.AddListener(() => SelectTab(index));
        }

        if (_backButton != null)
            _backButton.onClick.AddListener(ClosePanel);
    }

    private void Start()
    {
        SelectTab(0);
    }

    private void OnDestroy()
    {
        IsOpen = false;
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        // Panel dang mo: Esc de dong (o ca Menu lan Museum).
        if (_isPanelOpen)
        {
            if (_isClosing) return;

            EscClosedFrame = Time.frameCount;
            ClosePanel();
            return;
        }

        // Scene Menu: chi mo bang nut Options, khong mo bang Esc.
        if (!_openWithEsc) return;

        // Dang co UI khac -> de UI do tu xu ly Esc, khong mo Settings.
        if (PlayerCursor.IsUIOpen) return;
        if (ExitMuseumUI.IsOpen) return;
        if (ExitMuseumUI.EscClosedFrame == Time.frameCount) return;
        if (MuseumLeaderboardUI.IsOpen) return;
        if (MuseumLeaderboardUI.EscClosedFrame == Time.frameCount) return;

        OpenPanel();
    }

    // ============================================================
    // OPEN / CLOSE
    // ============================================================

    /// <summary>Gan vao OnClick cua nut Options o scene Menu.</summary>
    public void OpenPanel()
    {
        if (_isPanelOpen) return;

        _isPanelOpen = true;
        _isClosing = false;
        IsOpen = true;

        if (_playerMovement != null) _prevMovementEnabled = _playerMovement.enabled;
        if (_playerLook != null) _prevLookEnabled = _playerLook.enabled;

        if (_manageCursor)
            _prevUIOpen = PlayerCursor.IsUIOpen;

        _panel.Show();

        if (_playerMovement != null) _playerMovement.enabled = false;
        if (_playerLook != null) _playerLook.enabled = false;

        if (_manageCursor)
        {
            PlayerCursor.IsUIOpen = true;
            PlayerCursor.UnlockCursor();
        }
    }

    public void ClosePanel()
    {
        if (!_isPanelOpen || _isClosing) return;

        _isClosing = true;

        _panel.Hide(() =>
        {
            _isPanelOpen = false;
            _isClosing = false;
            IsOpen = false;

            if (_playerMovement != null) _playerMovement.enabled = _prevMovementEnabled;
            if (_playerLook != null) _playerLook.enabled = _prevLookEnabled;

            if (_manageCursor)
            {
                PlayerCursor.IsUIOpen = _prevUIOpen;

                if (_prevUIOpen)
                    PlayerCursor.UnlockCursor();
                else
                    PlayerCursor.LockCursor();
            }
        });
    }

    // ============================================================
    // TABS
    // ============================================================

    public void SelectTab(int index)
    {
        if (index < 0 || index >= _tabs.Length) return;

        for (int i = 0; i < _tabs.Length; i++)
        {
            Tab tab = _tabs[i];
            bool active = i == index;

            if (tab.Content != null)
                tab.Content.SetActive(active);

            // Moi tab co cap sprite RIENG (khung + icon dinh lien).
            if (tab.Button != null &&
                tab.Button.targetGraphic is Image img)
            {
                Sprite sprite = active ? tab.ActiveSprite : tab.InactiveSprite;

                if (sprite != null)
                    img.sprite = sprite;
            }
        }

        if (_titleText != null)
            _titleText.text = _tabs[index].Title;
    }
}