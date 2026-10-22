using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LobbyChatUI : MonoBehaviour
{
    public static LobbyChatUI Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private CanvasGroup _chatCanvasGroup;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private RectTransform _content;

    [Header("Message Prefab")]
    [SerializeField] private GameObject _messagePrefab;

    [Header("Input")]
    [SerializeField] private TMP_InputField _inputField;

    [Header("Settings")]
    [SerializeField] private int _maxMessages = 30;
    [SerializeField] private float _visibleDuration = 6f;
    [SerializeField] private float _fadeDuration = 0.5f;

    [Header("System Message")]
    [Tooltip(
        "Mẫu câu khi có người vào lobby. " +
        "{0} sẽ được thay bằng tên người chơi."
    )]
    [SerializeField]
    private string _joinedLobbyFormat =
        "{0} has joined the lobby.";

    [Tooltip(
        "Mẫu câu khi có người rời lobby. " +
        "{0} sẽ được thay bằng tên người chơi."
    )]
    [SerializeField]
    private string _leftLobbyFormat =
        "{0} has left the lobby.";

    [Tooltip(
        "Màu chữ cho thông báo hệ thống " +
        "(người vào/rời lobby, bắt cá...), dạng hex."
    )]
    [SerializeField]
    private string _systemMessageColorHex =
        "#FFD700";

    [Tooltip(
        "Mẫu câu khi có người vào scene gameplay. " +
        "{0} sẽ được thay bằng tên người chơi."
    )]
    [SerializeField]
    private string _joinedGameFormat =
        "{0} has joined the game.";

    [Tooltip(
        "Mẫu câu khi có người rời scene gameplay. " +
        "{0} sẽ được thay bằng tên người chơi."
    )]
    [SerializeField]
    private string _leftGameFormat =
        "{0} has left the game.";

    [Header("Fish Catch Message")]
    [Tooltip(
        "Mẫu thông báo khi có player bắt được cá. " +
        "{0} = tên player, {1} = tên cá, {2} = cân nặng."
    )]
    [SerializeField]
    private string _fishCatchFormat =
        "{0} ~ {1} [{2} Kg]";

    [Header("Day Event Message")]
    [Tooltip(
        "Màu chữ riêng cho các thông báo liên quan tới ngày chơi " +
        "(ngày mới, mục tiêu mới, quay về Day 1...), dạng hex. " +
        "Để khác với _systemMessageColorHex nhằm phân biệt với các " +
        "thông báo bắt cá / vào-rời game."
    )]
    [SerializeField]
    private string _dayEventColorHex =
        "#7EC8FF";


    private readonly Queue<GameObject> _messages = new();

    private bool _isTyping;

    private Coroutine _hideRoutine;

    // Frame vừa đóng typing. Dùng để KHÔNG mở lại chat ngay trong cùng frame
    // khi Enter vừa dùng để GỬI (onEndEdit đóng typing xong, rồi Update của
    // script này chạy sau và lại thấy Enter -> mở lại).
    private int _closedFrame = -1;

    public static bool IsTyping { get; private set; }

    /// <summary>Frame mà Esc vừa dùng để THOÁT nhập chat (SettingsUI dùng để không mở Settings).</summary>
    public static int EscClosedFrame { get; private set; } = -1;


    // ============================================================
    // AWAKE
    // ============================================================

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }


    // ============================================================
    // START
    // ============================================================

    private void Start()
    {
        SetChatVisible(false, true);

        ShowPlaceholderIfEmpty();

        if (_inputField != null)
        {
            _inputField.onSelect.AddListener(
                _ => HidePlaceholder()
            );

            _inputField.onDeselect.AddListener(
                _ => ShowPlaceholderIfEmpty()
            );

            _inputField.onEndEdit.AddListener(
                OnSubmit
            );
        }
    }


    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        // --------------------------------------------------------
        // ENTER → MỞ CHAT
        // --------------------------------------------------------

        if (Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            // Không mở chat khi:
            // - đang gõ rồi
            // - Enter này vừa dùng để gửi/đóng chat (cùng frame)
            // - Settings đang mở (VD gõ Enter xuống dòng ở tab Bug)
            if (!_isTyping &&
                _closedFrame != Time.frameCount &&
                !SettingsUI.IsOpen)
            {
                OpenChat();
            }
        }


        // --------------------------------------------------------
        // ESC → THOÁT CHAT
        // --------------------------------------------------------

        if (_isTyping &&
            Input.GetKeyDown(KeyCode.Escape))
        {
            // Báo cho SettingsUI biết Esc này đã dùng để thoát chat.
            EscClosedFrame = Time.frameCount;

            CloseTyping();
        }


        // --------------------------------------------------------
        // CLICK RA NGOÀI → THOÁT CHAT
        // --------------------------------------------------------

        if (_isTyping &&
            Input.GetMouseButtonDown(0))
        {
            if (_inputField != null &&
                !_inputField.isFocused)
            {
                CloseTyping();
            }
        }


        // --------------------------------------------------------
        // KHÔNG ĐANG GÕ MÀ Ô NHẬP VẪN FOCUS -> TẮT LẠI
        // (phòng khi Unity UI tự kích hoạt lại ô nhập sau khi gửi)
        // --------------------------------------------------------

        if (!_isTyping &&
            _inputField != null &&
            _inputField.isFocused)
        {
            _inputField.DeactivateInputField();
            ClearInputSelection();
        }


        // --------------------------------------------------------
        // LUÔN ẨN PLACEHOLDER KHI ĐANG GÕ
        // --------------------------------------------------------

        if (_isTyping)
            HidePlaceholder();


        // --------------------------------------------------------
        // SCROLL CHAT
        // --------------------------------------------------------

        HandleScrollInput();
    }


    // ============================================================
    // HANDLE SCROLL INPUT
    // ============================================================

    private void HandleScrollInput()
    {
        if (_scrollRect == null)
            return;

        float scroll =
            Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Approximately(scroll, 0f))
            return;

        // Chỉ scroll khi chat đang hiển thị.
        // Không bật chat lên khi lăn chuột.

        if (_chatCanvasGroup == null ||
            _chatCanvasGroup.alpha < 0.1f)
        {
            return;
        }

        _scrollRect.verticalNormalizedPosition =
            Mathf.Clamp01(
                _scrollRect.verticalNormalizedPosition +
                scroll * 1f
            );
    }


    // ============================================================
    // OPEN CHAT
    // ============================================================

    private void OpenChat()
    {
        if (_inputField == null)
            return;

        _isTyping = true;
        IsTyping = true;

        SetChatVisible(true);

        _inputField.ActivateInputField();
        _inputField.Select();

        HidePlaceholder();
    }


    // ============================================================
    // CLOSE TYPING
    // ============================================================

    private void CloseTyping()
    {
        _isTyping = false;
        IsTyping = false;
        _closedFrame = Time.frameCount;

        if (_inputField != null)
        {
            _inputField.text = "";
            _inputField.DeactivateInputField();
        }

        ClearInputSelection();

        ShowPlaceholderIfEmpty();

        StartHideTimer();
    }


    // ============================================================
    // CLEAR INPUT SELECTION
    // ============================================================

    // TMP_InputField vẫn là object đang được EventSystem "chọn" sau khi gửi.
    // Lần Enter kế tiếp (hoặc chính Enter vừa gửi) EventSystem gửi sự kiện
    // Submit tới nó -> TMP TỰ KÍCH HOẠT LẠI ô nhập => bị "gõ chat tiếp".
    // Bỏ chọn ra khỏi EventSystem để Enter không bật lại ô nhập.
    private void ClearInputSelection()
    {
        if (_inputField == null || EventSystem.current == null)
            return;

        if (EventSystem.current.currentSelectedGameObject == _inputField.gameObject)
            EventSystem.current.SetSelectedGameObject(null);
    }


    // ============================================================
    // PLACEHOLDER
    // ============================================================

    private void HidePlaceholder()
    {
        if (_inputField != null &&
            _inputField.placeholder != null)
        {
            _inputField.placeholder.gameObject.SetActive(false);
        }
    }


    private void ShowPlaceholderIfEmpty()
    {
        if (_inputField != null &&
            _inputField.placeholder != null)
        {
            bool show =
                string.IsNullOrEmpty(
                    _inputField.text
                ) &&
                !_isTyping;

            _inputField.placeholder.gameObject.SetActive(
                show
            );
        }
    }


    // ============================================================
    // CHAT SENDER
    // ============================================================

    private IChatSender _chatSender;


    public void SetSender(IChatSender sender)
    {
        _chatSender = sender;
    }


    // ============================================================
    // SUBMIT CHAT
    // ============================================================

    // Enter khi đang gõ: gửi tin (nếu có chữ) rồi THOÁT nhập luôn.
    // Enter khi ô trống: chỉ thoát nhập.
    private void OnSubmit(string text)
    {
        if (!_isTyping)
            return;

        if (!Input.GetKeyDown(KeyCode.Return) &&
            !Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            CloseTyping();
            return;
        }

        _chatSender?.SendChatMessage(
            text.Trim()
        );

        _inputField.text = "";

        CloseTyping();
    }


    // ============================================================
    // NORMAL CHAT MESSAGE
    // ============================================================

    /// <summary>
    /// Tin nhắn chat bình thường:
    ///
    /// PlayerName: nội dung
    /// </summary>
    public void AddMessage(
        string playerName,
        string message)
    {
        AddRawEntry(
            $"<b>{playerName}</b>: {message}"
        );
    }


    // ============================================================
    // SERVER MESSAGE
    // ============================================================

    /// <summary>
    /// Tin nhắn hệ thống/server.
    ///
    /// Dùng cho các thông báo đặc biệt như:
    ///
    /// SERVER: Phuc ~ Largemouth Bass [4.25 Kg]
    ///
    /// SERVER sẽ dùng màu system message.
    /// </summary>
    public void AddServerMessage(
        string message)
    {
        string content =
            $"<b>SERVER:</b> {message}";

        AddRawEntry(
            $"<color={_systemMessageColorHex}>{content}</color>"
        );
    }


    // ============================================================
    // DAY EVENT MESSAGE
    // ============================================================

    /// <summary>
    /// Tin nhắn cho các sự kiện liên quan tới ngày chơi: bắt đầu ngày
    /// mới, mục tiêu tiền thuê mới, quay về Day 1 khi thất bại...
    ///
    /// KHÔNG có prefix "SERVER:" và dùng màu riêng
    /// (_dayEventColorHex) để không bị lẫn với thông báo bắt cá /
    /// vào-rời game (vốn dùng AddServerMessage / màu system).
    ///
    /// Ví dụ:
    ///
    /// Day 1 has begun! Grab your rod, the rent's waiting to be paid!
    /// Day 2 begins. New target: $120
    /// Back to Day 1. Target: $100
    /// </summary>
    public void AddDayEventMessage(
        string message)
    {
        AddRawEntry(
            $"<color={_dayEventColorHex}><b>{message}</b></color>"
        );
    }


    // ============================================================
    // FISH CAUGHT MESSAGE
    // ============================================================

    /// <summary>
    /// Hiển thị thông báo khi player bắt được cá.
    ///
    /// Kết quả:
    ///
    /// SERVER: PlayerName ~ FishName [4.25 Kg]
    ///
    /// Ví dụ:
    ///
    /// SERVER: Phuc ~ Largemouth Bass [4.25 Kg]
    /// </summary>
    public void AddFishCaughtMessage(
        string playerName,
        string fishName,
        float weightKg)
    {
        string weightText =
            FormatFishWeight(weightKg);

        string content =
            string.Format(
                _fishCatchFormat,
                playerName,
                fishName,
                weightText
            );

        AddServerMessage(
            content
        );
    }


    // ============================================================
    // FORMAT FISH WEIGHT
    // ============================================================

    private static string FormatFishWeight(
        float weightKg)
    {
        if (weightKg >= 10f)
        {
            return weightKg.ToString(
                "0.0",
                System.Globalization.CultureInfo.InvariantCulture
            );
        }

        return weightKg.ToString(
            "0.##",
            System.Globalization.CultureInfo.InvariantCulture
        );
    }


    // ============================================================
    // PLAYER JOIN LOBBY
    // ============================================================

    /// <summary>
    /// Thông báo hệ thống khi có người vào lobby.
    ///
    /// Ví dụ:
    /// Phuc has joined the lobby.
    /// </summary>
    public void AddPlayerJoinedMessage(
        string playerName)
    {
        string content =
            string.Format(
                _joinedLobbyFormat,
                playerName
            );

        AddRawEntry(
            $"<color={_systemMessageColorHex}>{content}</color>"
        );
    }


    // ============================================================
    // PLAYER LEFT LOBBY
    // ============================================================

    public void AddPlayerLeftMessage(
        string playerName)
    {
        string content =
            string.Format(
                _leftLobbyFormat,
                playerName
            );

        AddRawEntry(
            $"<color={_systemMessageColorHex}>{content}</color>"
        );
    }


    // ============================================================
    // PLAYER JOIN GAME
    // ============================================================

    /// <summary>
    /// Thông báo khi player vào scene gameplay.
    ///
    /// Ví dụ:
    /// Phuc has joined the game.
    /// </summary>
    public void AddPlayerJoinedGameMessage(
        string playerName)
    {
        string content =
            string.Format(
                _joinedGameFormat,
                playerName
            );

        AddRawEntry(
            $"<color={_systemMessageColorHex}>{content}</color>"
        );
    }


    // ============================================================
    // PLAYER LEFT GAME
    // ============================================================

    public void AddPlayerLeftGameMessage(
        string playerName)
    {
        string content =
            string.Format(
                _leftGameFormat,
                playerName
            );

        AddRawEntry(
            $"<color={_systemMessageColorHex}>{content}</color>"
        );
    }


    // ============================================================
    // ADD RAW ENTRY
    // ============================================================

    /// <summary>
    /// Dùng chung cho:
    ///
    /// - Chat bình thường
    /// - System message
    /// - Server message
    /// - Day event message
    /// - Fish caught message
    ///
    /// Không cần tạo UI riêng cho từng loại.
    /// </summary>
    private void AddRawEntry(
        string richText)
    {
        if (_messagePrefab == null ||
            _content == null)
        {
            return;
        }


        // --------------------------------------------------------
        // HIỆN CHAT
        // --------------------------------------------------------

        SetChatVisible(true);


        // --------------------------------------------------------
        // TẠO MESSAGE
        // --------------------------------------------------------

        GameObject obj =
            Instantiate(
                _messagePrefab,
                _content
            );


        TMP_Text text =
            obj.GetComponent<TMP_Text>();


        if (text != null)
        {
            text.text = richText;
        }


        // --------------------------------------------------------
        // THÊM VÀO QUEUE
        // --------------------------------------------------------

        _messages.Enqueue(
            obj
        );


        // --------------------------------------------------------
        // GIỚI HẠN SỐ MESSAGE
        // --------------------------------------------------------

        if (_messages.Count > _maxMessages)
        {
            GameObject oldest =
                _messages.Dequeue();

            if (oldest != null)
                Destroy(oldest);
        }


        // --------------------------------------------------------
        // UPDATE CANVAS
        // --------------------------------------------------------

        Canvas.ForceUpdateCanvases();


        // --------------------------------------------------------
        // SCROLL XUỐNG CUỐI
        // --------------------------------------------------------

        if (_scrollRect != null)
        {
            _scrollRect.verticalNormalizedPosition = 0f;
        }


        // --------------------------------------------------------
        // RESET HIDE TIMER
        // --------------------------------------------------------

        StartHideTimer();
    }


    // ============================================================
    // START HIDE TIMER
    // ============================================================

    private void StartHideTimer()
    {
        if (_isTyping)
            return;

        if (_hideRoutine != null)
        {
            StopCoroutine(
                _hideRoutine
            );
        }

        _hideRoutine =
            StartCoroutine(
                HideAfterDelay()
            );
    }


    // ============================================================
    // HIDE AFTER DELAY
    // ============================================================

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(
            _visibleDuration
        );


        float t = 0f;

        float start =
            _chatCanvasGroup != null
                ? _chatCanvasGroup.alpha
                : 0f;


        float duration =
            Mathf.Max(
                0.01f,
                _fadeDuration
            );


        while (t < duration)
        {
            t += Time.deltaTime;

            if (_chatCanvasGroup != null)
            {
                _chatCanvasGroup.alpha =
                    Mathf.Lerp(
                        start,
                        0f,
                        t / duration
                    );
            }

            yield return null;
        }


        SetChatVisible(
            false,
            true
        );

        _hideRoutine = null;
    }


    // ============================================================
    // SET CHAT VISIBLE
    // ============================================================

    private void SetChatVisible(
        bool visible,
        bool instant = false)
    {
        if (_chatCanvasGroup == null)
            return;


        // --------------------------------------------------------
        // STOP HIDE COROUTINE KHI CHAT ĐƯỢC HIỆN
        // --------------------------------------------------------

        if (_hideRoutine != null &&
            visible)
        {
            StopCoroutine(
                _hideRoutine
            );

            _hideRoutine = null;
        }


        // --------------------------------------------------------
        // ENABLE GAMEOBJECT
        // --------------------------------------------------------

        _chatCanvasGroup.gameObject.SetActive(
            true
        );


        // --------------------------------------------------------
        // ALPHA
        // --------------------------------------------------------

        if (instant)
        {
            _chatCanvasGroup.alpha =
                visible
                    ? 1f
                    : 0f;
        }
        else
        {
            _chatCanvasGroup.alpha = 1f;
        }


        // --------------------------------------------------------
        // RAYCAST / INTERACTABLE
        // --------------------------------------------------------

        if (!visible && instant)
        {
            _chatCanvasGroup.blocksRaycasts = false;
            _chatCanvasGroup.interactable = false;

            _chatCanvasGroup.gameObject.SetActive(
                false
            );
        }
        else
        {
            _chatCanvasGroup.blocksRaycasts = true;
            _chatCanvasGroup.interactable = true;
        }
    }
}