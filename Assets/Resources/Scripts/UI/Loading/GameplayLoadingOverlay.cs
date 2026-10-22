using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Panel "Waiting for players..." lúc chuyển từ Lobby sang Gameplay.
///
/// HÀNH VI (theo yêu cầu):
///   - Show(): hiện TỨC THÌ, không fade-in — panel đen + text lên full ngay
///     khung hình đầu tiên.
///   - Panel luôn đứng tối thiểu _minDisplaySeconds giây, dù máy mạnh load
///     xong tức khắc cũng không tắt sớm hơn mốc này (tránh chớp nháy giật).
///   - Khi GameDayManager.OnRunStarted bắn ra (đủ người chơi đã thật sự
///     vào Gameplay) -> chờ cho đủ _minDisplaySeconds (nếu load nhanh hơn
///     mốc đó) rồi mới fade-out mờ dần và tắt hẳn. Nếu load LÂU hơn mốc đó
///     thì ẩn NGAY khi vừa nhận tín hiệu sẵn sàng, không chờ thêm.
///   - Có guard chặn double-trigger: nếu panel đang hiện, gọi Show() lần
///     nữa sẽ bị bỏ qua — không restart hiệu ứng, không reset mốc thời
///     gian tối thiểu. Đây là fix cho lỗi "chạy lại hiệu ứng 2 lần".
///
/// FIX (bug: Host Game lần 2 từ Museum bị hiện overlay "Waiting for
/// players..." ngay dù chưa vào Lobby / chưa bấm Start):
///
/// Nguyên nhân gốc nằm ở FusionRoomManager.OnSceneLoadStart(), Show() bị
/// gọi cho MỌI lần load scene (kể cả lần load S-Lobby đầu tiên lúc
/// CreateRoom/JoinRoom), không chỉ riêng lúc StartGameplay(). Vì overlay
/// chỉ tự ẩn khi GameDayManager.OnRunStarted bắn ra (chỉ xảy ra sau khi
/// bấm Start Game), nếu người chơi bấm Back rời Lobby TRƯỚC khi bấm Start,
/// _isVisible bị kẹt ở true mãi (do là DontDestroyOnLoad singleton, sống
/// xuyên scene). Lần Host Game kế tiếp, guard "if (_isVisible) return;"
/// trong Show() nuốt luôn lệnh Show() mới -> nhìn như panel "tự hiện ra"
/// dù thực chất nó chưa từng tắt từ lần trước.
///
/// Cách sửa gồm 2 phần:
///   1. FusionRoomManager chỉ gọi Show() khi lần LoadScene đó thực sự là
///      Lobby -> Gameplay (xem cờ _isLobbyToGameplayTransition ở đó).
///   2. Thêm ForceHideImmediate() ở đây làm lưới an toàn thứ hai — gọi
///      ngay khi người chơi chủ động rời phòng (Back) hoặc bị host kick,
///      để state không bao giờ mang state cũ sang lần chơi kế tiếp, bất
///      kể nguyên nhân gây kẹt là gì.
///
/// FIX 2 (bug: client load chậm bị kẹt mãi ở "Waiting for players..."):
///
/// Trước đây Show() luôn set _readySignalReceived = false ngay khi chạy.
/// Nếu OnRunStarted (bắn qua RPC_NotifyRunStart, tới TẤT CẢ client cùng
/// lúc) tới sớm hơn lúc scene của 1 client chậm load xong (tức sớm hơn
/// lúc Show() của client đó được gọi), thì HandleRunStarted() ghi nhận
/// _readySignalReceived = true trước, nhưng ngay sau đó Show() chạy và
/// GHI ĐÈ nó về false — tín hiệu "đã sẵn sàng" bị mất trắng vĩnh viễn,
/// và vì OnRunStarted chỉ bắn đúng 1 lần cho mỗi lượt run, panel không
/// bao giờ nhận được tín hiệu hide nữa -> kẹt "Waiting..." mãi mãi.
///
/// Cách sửa: Show() không còn xóa _readySignalReceived (việc reset dời
/// sang FinishHide(), đúng lúc panel THỰC SỰ tắt xong của lượt trước).
/// Nếu Show() phát hiện tín hiệu ready đã tới từ trước, nó tự
/// ScheduleHideIfNeeded() ngay, thay vì chờ một OnRunStarted không bao
/// giờ tới nữa.
/// </summary>
public class GameplayLoadingOverlay : MonoBehaviour
{
    public static GameplayLoadingOverlay Instance { get; private set; }

    /// <summary>
    /// Bắn ra CỤC BỘ (không qua RPC) đúng lúc panel "Waiting for players..."
    /// này đã ẩn HẲN (hết fade-out). GameDayIntroPanel lắng nghe event này
    /// để biết chính xác khi nào được phép hiện panel "Day 1" tiếp theo —
    /// đảm bảo 2 panel không bao giờ chồng lên nhau.
    /// </summary>
    public static event System.Action OnWaitingOverlayHidden;

    [Header("References")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TextMeshProUGUI _waitingText;

    [Header("Minimum Display Time")]
    [Tooltip("Panel luôn hiện tối thiểu chừng này giây kể từ lúc Show() " +
             "được gọi, bất kể load nhanh cỡ nào. Nếu load lâu hơn số này " +
             "thì panel ẩn NGAY khi thật sự sẵn sàng, không chờ thêm.")]
    [SerializeField] private float _minDisplaySeconds = 3f;

    [Header("Text Fade Out")]
    [Tooltip("Thời gian mờ dần CHỈ riêng chữ 'Waiting for players...' " +
             "trước khi panel tắt hẳn. Nền panel (canvasGroup) vẫn giữ " +
             "alpha = 1 (đen đặc) suốt lúc này — chỉ chữ biến mất, không " +
             "lộ scene phía sau — để bàn giao liên tục cho panel Day 1.")]
    [SerializeField] private float _textFadeOutTime = 0.5f;

    [Header("Dots Animation")]
    [SerializeField] private string _baseText = "Waiting for players";
    [SerializeField] private float _dotInterval = 0.4f;
    [SerializeField] private int _maxDots = 3;

    private Coroutine _dotsCoroutine;
    private Coroutine _hideCoroutine;

    private bool _isVisible;
    private bool _readySignalReceived;
    private float _shownAtUnscaledTime;

    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            SetInstant(false);
        }
        else
        {
            // FIX: nếu lỡ có 2 object cùng gắn script này (VD 1 bản còn sót
            // trong scene khác), bản dư sẽ tự huỷ ở đây thay vì chạy song
            // song gây double hiệu ứng.
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        GameDayManager.OnRunStarted += HandleRunStarted;
    }

    private void OnDisable()
    {
        GameDayManager.OnRunStarted -= HandleRunStarted;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void HandleRunStarted()
    {
        _readySignalReceived = true;

        if (!_isVisible)
            return;

        ScheduleHideIfNeeded();
    }

    // ============================================================
    // SHOW — tức thì, không fade
    // ============================================================

    public void Show()
    {
        // FIX: chặn double-trigger. Panel đang hiện rồi thì bỏ qua hẳn,
        // không reset mốc _shownAtUnscaledTime, không restart dots.
        if (_isVisible)
            return;

        _isVisible = true;
        _shownAtUnscaledTime = Time.unscaledTime;

        // FIX: KHÔNG reset _readySignalReceived = false ở đây nữa (dời
        // sang FinishHide()). Nếu để ở đây, tín hiệu ready tới SỚM HƠN
        // lúc Show() này chạy (client load chậm, RPC_NotifyRunStart tới
        // trước khi scene load xong) sẽ bị xóa mất, và vì OnRunStarted
        // chỉ bắn 1 lần, panel sẽ kẹt "Waiting..." vĩnh viễn.

        if (_hideCoroutine != null)
        {
            StopCoroutine(_hideCoroutine);
            _hideCoroutine = null;
        }

        gameObject.SetActive(true);

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
        }

        // Reset alpha của chữ về 1 — phòng trường hợp lượt chạy trước đó
        // chữ đã bị mờ (FadeTextOutRoutine) rồi mới Show() lại.
        SetTextAlpha(_waitingText, 1f);

        if (_dotsCoroutine != null)
            StopCoroutine(_dotsCoroutine);
        _dotsCoroutine = StartCoroutine(DotsRoutine());

        // FIX: nếu tín hiệu ready đã tới TỪ TRƯỚC lúc Show() này chạy
        // (kẹt tạm trong _readySignalReceived vì lúc đó _isVisible còn
        // false), tự lên lịch ẩn ngay bây giờ — đừng chờ một
        // OnRunStarted khác sẽ không bao giờ bắn lại nữa trong lượt này.
        if (_readySignalReceived)
        {
            ScheduleHideIfNeeded();
        }
    }

    // ============================================================
    // HIDE — chờ đủ thời gian tối thiểu rồi tắt TỨC THÌ (không fade)
    // ============================================================
    //
    // FIX: panel này KHÔNG còn tự fade-out nữa. Ngay khi panel "Day 1"
    // (GameDayIntroPanel) đã được thiết kế để hiện lên NGAY LÚC panel này
    // tắt — nếu panel này fade-out (mờ dần lộ scene phía sau), người chơi
    // sẽ thấy 1 khoảng scene lộ ra giữa lúc chuyển từ "Waiting..." sang
    // "Day 1", thay vì đen liên tục. Nên giờ tắt TỨC THÌ, để panel Day 1
    // (đã đứng sẵn màu đen) tiếp quản mà không có khoảng hở nào. Việc mờ
    // dần thật sự CHỈ xảy ra 1 lần duy nhất, ở cuối cùng khi
    // GameDayIntroPanel tự fade-out.
    // ============================================================

    private void ScheduleHideIfNeeded()
    {
        if (_hideCoroutine != null)
            return; // đã lên lịch ẩn rồi

        _hideCoroutine = StartCoroutine(HideAfterMinDisplayRoutine());
    }

    private IEnumerator HideAfterMinDisplayRoutine()
    {
        float elapsed = Time.unscaledTime - _shownAtUnscaledTime;
        float remaining = _minDisplaySeconds - elapsed;

        if (remaining > 0f)
            yield return new WaitForSecondsRealtime(remaining);

        if (_dotsCoroutine != null)
        {
            StopCoroutine(_dotsCoroutine);
            _dotsCoroutine = null;
        }

        // Mờ dần CHỈ chữ — nền panel vẫn alpha = 1 suốt lúc này.
        yield return FadeTextOutRoutine();

        // Sau khi chữ đã mờ hẳn, tắt tức thì cả panel (nền đen biến mất
        // trong ĐÚNG lúc panel Day 1 xuất hiện, nên người chơi không thấy
        // khoảng hở nào, chỉ thấy chữ đổi).
        FinishHide();

        _hideCoroutine = null;
    }

    private IEnumerator FadeTextOutRoutine()
    {
        if (_waitingText == null)
            yield break;

        float start = _waitingText.color.a;
        float t = 0f;

        while (t < _textFadeOutTime)
        {
            t += Time.deltaTime;
            float ratio = _textFadeOutTime <= 0f ? 1f : t / _textFadeOutTime;
            SetTextAlpha(_waitingText, Mathf.Lerp(start, 0f, ratio));
            yield return null;
        }

        SetTextAlpha(_waitingText, 0f);
    }

    private void FinishHide()
    {
        gameObject.SetActive(false);
        _isVisible = false;

        // FIX: reset tín hiệu ready dời VỀ ĐÂY thay vì trong Show(). Đây
        // mới đúng là lúc lượt "waiting" hiện tại đã hoàn tất trọn vẹn —
        // reset ở đây đảm bảo lượt Show() KẾ TIẾP (lần Host Game sau)
        // luôn bắt đầu sạch, mà không xóa mất tín hiệu ready có thể đến
        // sớm hơn Show() ở lượt đang chạy.
        _readySignalReceived = false;

        // FIX: báo cho GameDayIntroPanel (panel "Day 1") biết là panel này
        // đã tắt hẳn — đó mới là lúc panel "Day 1" được phép hiện lên.
        OnWaitingOverlayHidden?.Invoke();
    }

    // ============================================================
    // FORCE HIDE — reset cứng, KHÔNG bắn OnWaitingOverlayHidden
    // ============================================================
    //
    // Dùng khi rời phòng / bị kick / quay về Museum GIỮA CHỪNG (chưa bấm
    // Start Game, chưa thật sự vào Gameplay). Khác với FinishHide():
    //   - Dừng hết coroutine đang chạy dở (dots, hide-after-min-display).
    //   - Reset toàn bộ state (_isVisible, _readySignalReceived) ngay lập
    //     tức, không chờ min display time, không fade chữ.
    //   - KHÔNG bắn OnWaitingOverlayHidden, vì đây không phải luồng bàn
    //     giao bình thường sang GameDayIntroPanel (panel Day 1 không nên
    //     tự hiện lên chỉ vì người chơi bấm Back thoát Lobby).
    //
    // Gọi hàm này ở FusionRoomManager.LeaveRoom() và HandleHostLeft(), để
    // đảm bảo overlay (DontDestroyOnLoad, sống xuyên scene) không bao giờ
    // mang state "đang hiện" sót lại sang lần Host/Join phòng kế tiếp.
    // ============================================================

    public void ForceHideImmediate()
    {
        if (_hideCoroutine != null)
        {
            StopCoroutine(_hideCoroutine);
            _hideCoroutine = null;
        }

        if (_dotsCoroutine != null)
        {
            StopCoroutine(_dotsCoroutine);
            _dotsCoroutine = null;
        }

        _isVisible = false;
        _readySignalReceived = false;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }

        // Trả chữ về alpha = 1 để lần Show() kế tiếp không bị thừa hưởng
        // trạng thái mờ dở từ lượt trước.
        SetTextAlpha(_waitingText, 1f);

        gameObject.SetActive(false);
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private void SetInstant(bool visible)
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
        }

        gameObject.SetActive(visible);
    }

    private static void SetTextAlpha(TextMeshProUGUI text, float alpha)
    {
        if (text == null)
            return;

        Color c = text.color;
        c.a = alpha;
        text.color = c;
    }

    private IEnumerator DotsRoutine()
    {
        int dotCount = 1;

        while (true)
        {
            if (_waitingText != null)
                _waitingText.text = _baseText + new string('.', dotCount);

            yield return new WaitForSeconds(_dotInterval);

            dotCount = dotCount % _maxDots + 1;
        }
    }
}