using UnityEngine;
using UnityEngine.UI;

public class MicToggleUI : MonoBehaviour
{
    public static MicToggleUI Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private Button _micButton;
    [SerializeField] private Image _micIcon;

    [Header("Icons")]
    [SerializeField] private Sprite _micOnSprite;
    [SerializeField] private Sprite _micOffSprite;

    [Header("Settings")]
    [SerializeField] private KeyCode _hotkey = KeyCode.V;

    private VoiceController _voiceController;
    private bool _isMicOn;
    public bool IsMicOn => _isMicOn;

    public static event System.Action<bool> OnMicStateChanged;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        SetMicIcon(false);

        if (_micButton != null)
            _micButton.onClick.AddListener(ToggleMic);
    }

    private void Update()
    {
        if (LobbyChatUI.IsTyping)
            return;

        if (Input.GetKeyDown(_hotkey))
            ToggleMic();
    }

    public void SetVoiceController(VoiceController controller)
    {
        _voiceController = controller;
    }

    public void ToggleMic()
    {
        if (_voiceController == null)
        {
            Debug.LogWarning("Chưa có VoiceController — Player local chưa spawn xong?");
            return;
        }

        _isMicOn = !_isMicOn;
        _voiceController.SetMicActive(_isMicOn);

        SetMicIcon(_isMicOn);
        OnMicStateChanged?.Invoke(_isMicOn);
    }

    private void SetMicIcon(bool on)
    {
        if (_micIcon == null)
            return;

        _micIcon.sprite = on ? _micOnSprite : _micOffSprite;
    }

    public void SyncIcon(bool on)
    {
        _isMicOn = on;
        SetMicIcon(on);
    }
}