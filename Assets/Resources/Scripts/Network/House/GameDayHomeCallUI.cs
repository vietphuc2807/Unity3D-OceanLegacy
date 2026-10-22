using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Hiện text kêu gọi TẤT CẢ player về nhà khi hết giờ trong ngày.
///
/// 3 trạng thái message:
/// - Còn thiếu người: "Round everyone up, it's home time! 1/4"
/// - Đủ người nhưng cửa còn mở: "Close the door to end the day!"
/// - Đã đóng cửa (khoảnh khắc trước khi UI tự ẩn do EvaluateDayEnd
///   chạy ngay lập tức): "Everyone's home! Wrapping up the day..."
///
/// Hiệu ứng SHOW: trượt từ trên xuống + fade in, chữ lóe sáng trắng rồi
/// về màu gốc. KHÔNG scale to/nhỏ (giữ kích thước cố định, tránh nhìn
/// giật/xấu).
///
/// Hiệu ứng HIDE: trượt lên nhẹ + fade out.
///
/// Mỗi khi số người về nhà tăng, hoặc trạng thái chờ-đóng-cửa bắt đầu
/// -> chữ chớp sáng nhẹ (không scale).
/// </summary>
public class GameDayHomeCallUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject _root;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TextMeshProUGUI _messageText;

    [Header("Message")]
    [Tooltip("{0} = số người đã về nhà, {1} = tổng số người chơi.")]
    [SerializeField]
    private string _messageFormat =
        "Round everyone up, it's home time! {0}/{1}";

    [Tooltip("Hiện khi ĐỦ người trong nhà nhưng cửa VẪN ĐANG MỞ.")]
    [SerializeField]
    private string _waitingForDoorMessage =
        "Everyone's back — close the door to end the day!";

    [SerializeField]
    private string _allHomeMessageFormat =
        "Everyone's home! Wrapping up the day...";

    [Header("Show / Hide Animation")]
    [SerializeField] private float _showDuration = 0.45f;
    [SerializeField] private float _hideDuration = 0.3f;
    [SerializeField] private Ease _showEase = Ease.OutCubic;
    [SerializeField] private Ease _hideEase = Ease.InCubic;

    [Header("Slide")]
    [Tooltip("Offset (theo local position) lúc bắt đầu, panel sẽ trượt từ vị trí này về vị trí gốc.")]
    [SerializeField] private Vector2 _slideOffset = new Vector2(0f, 30f);

    [Header("Text Flash")]
    [Tooltip("Màu lóe lên lúc panel vừa hiện hoặc lúc nội dung message thay đổi.")]
    [SerializeField] private Color _flashColor = Color.white;
    [SerializeField] private float _flashDuration = 0.3f;

    private RectTransform _rootRect;
    private Vector2 _originalAnchoredPos;
    private Color _textOriginalColor;

    private bool _isShown;
    private int _lastCount = -1;
    private bool _lastWaitingForDoor;

    private Sequence _showHideSeq;
    private Tween _flashTween;

    private void Awake()
    {
        if (_root == null) _root = gameObject;
        _rootRect = _root.transform as RectTransform;

        if (_canvasGroup == null) _canvasGroup = _root.GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = _root.AddComponent<CanvasGroup>();

        if (_messageText != null)
            _textOriginalColor = _messageText.color;

        if (_rootRect != null)
            _originalAnchoredPos = _rootRect.anchoredPosition;

        SetInstant(false);
    }

    private void OnDisable() => KillTweens();

    private void Update()
    {
        if (GameDayManager.Instance == null)
        {
            if (_isShown) Hide();
            return;
        }

        bool shouldShow = GameDayManager.Instance.IsWaitingForPlayersHome;

        if (shouldShow && !_isShown) Show();
        else if (!shouldShow && _isShown) Hide();

        if (shouldShow) UpdateMessage();
    }


    private void Show()
    {
        _isShown = true;
        _lastCount = -1;
        _lastWaitingForDoor = false;

        KillShowHideTween();
        _root.SetActive(true);


        if (_rootRect != null)
            _rootRect.anchoredPosition = _originalAnchoredPos + _slideOffset;

        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;

        if (_messageText != null)
            _messageText.color = _flashColor;


        Sequence seq = DOTween.Sequence();

        seq.Join(_canvasGroup.DOFade(1f, _showDuration).SetEase(Ease.OutQuad));

        if (_rootRect != null)
        {
            seq.Join(_rootRect.DOAnchorPos(_originalAnchoredPos, _showDuration)
                .SetEase(_showEase));
        }

        if (_messageText != null)
        {
            seq.Join(_messageText.DOColor(_textOriginalColor, _flashDuration)
                .SetDelay(_showDuration * 0.2f));
        }

        seq.SetUpdate(true);
        seq.OnComplete(() => _canvasGroup.blocksRaycasts = true);

        _showHideSeq = seq;
    }


    private void Hide()
    {
        _isShown = false;
        KillShowHideTween();

        Sequence seq = DOTween.Sequence();
        seq.Join(_canvasGroup.DOFade(0f, _hideDuration).SetEase(Ease.InQuad));

        if (_rootRect != null)
        {
            seq.Join(_rootRect.DOAnchorPos(_originalAnchoredPos - _slideOffset * 0.4f, _hideDuration)
                .SetEase(_hideEase));
        }

        seq.SetUpdate(true);
        seq.OnComplete(() =>
        {
            _root.SetActive(false);

            if (_rootRect != null)
                _rootRect.anchoredPosition = _originalAnchoredPos;
        });

        _showHideSeq = seq;
    }


    private void UpdateMessage()
    {
        if (_messageText == null) return;

        var mgr = GameDayManager.Instance;

        int current = mgr.PlayersInsideHouseCount;
        var runner = mgr.Runner;
        int total = runner != null ? runner.ActivePlayers.Count() : Mathf.Max(current, 1);

        bool waitingForDoor = mgr.IsWaitingForDoorClose;

        string newText;

        if (waitingForDoor)
        {
            newText = _waitingForDoorMessage;
        }
        else if (current >= total && total > 0)
        {
            // Trường hợp hiếm gặp (đúng frame door vừa đóng nhưng
            // UI chưa kịp ẩn) — vẫn xử lý để không hiện text sai.
            newText = _allHomeMessageFormat;
        }
        else
        {
            newText = string.Format(_messageFormat, current, total);
        }

        _messageText.text = newText;

        bool contentChanged =
            (_lastCount != -1 && current != _lastCount) ||
            (waitingForDoor && !_lastWaitingForDoor);

        if (contentChanged)
            PlayCountFlash();

        _lastCount = current;
        _lastWaitingForDoor = waitingForDoor;
    }

    private void PlayCountFlash()
    {
        if (_messageText == null) return;

        if (_flashTween != null && _flashTween.IsActive())
            _flashTween.Kill();

        _messageText.color = _flashColor;

        _flashTween = _messageText
            .DOColor(_textOriginalColor, _flashDuration)
            .SetUpdate(true);
    }


    private void SetInstant(bool visible)
    {
        _isShown = visible;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
        }

        if (_rootRect != null)
            _rootRect.anchoredPosition = _originalAnchoredPos;

        if (_messageText != null)
            _messageText.color = _textOriginalColor;

        _root.SetActive(visible);
    }

    private void KillShowHideTween()
    {
        if (_showHideSeq != null && _showHideSeq.IsActive())
            _showHideSeq.Kill();
    }

    private void KillTweens()
    {
        KillShowHideTween();

        if (_flashTween != null && _flashTween.IsActive())
            _flashTween.Kill();
    }
}