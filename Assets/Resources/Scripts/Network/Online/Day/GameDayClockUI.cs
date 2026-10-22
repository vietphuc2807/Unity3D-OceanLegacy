using TMPro;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// UI hiển thị Day và Game Time.
///
/// Trong lúc Day Panel đang chuyển cảnh (đang fade in, CHƯA đen hoàn
/// toàn): đóng băng UI + tắt nháy đỏ ngay lập tức.
///
/// Ngay khi panel ĐEN HOÀN TOÀN: UI cập nhật lại NGAY, trước khi panel
/// fade out lộ ra.
///
/// NHÁY ĐỎ (urgent warning):
/// Không còn dùng kiểu DOColor loop Yoyo LIÊN TỤC (mượt nhưng nhìn hơi
/// "rung" nếu để lâu). Thay bằng kiểu CHỚP NGẮN RỒI NGHỈ (giống nhịp
/// tim): chớp nhanh sang màu đỏ, giữ 1 nhịp, mờ dần về màu thường, rồi
/// NGHỈ 1 khoảng trước khi chớp tiếp — lặp vô hạn trong khoảng
/// _urgentStartHour (GameDayManager) cho tới hết ngày.
/// </summary>
public class GameDayClockUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI _dayText;
    [SerializeField] private TextMeshProUGUI _timeText;


    [Header("Day Change Effect")]
    [SerializeField] private float _dayPunchScale = 0.3f;
    [SerializeField] private float _dayPunchDuration = 0.4f;


    [Header("Urgent Time Warning - Blink Pattern")]
    [SerializeField] private Color _normalTimeColor = Color.white;
    [SerializeField] private Color _urgentTimeColor =
        new Color(1f, 0.3f, 0.3f);

    [Tooltip("Thời gian chớp NHANH sang màu đỏ.")]
    [SerializeField] private float _flashInDuration = 0.15f;

    [Tooltip("Giữ nguyên màu đỏ trong khoảng này trước khi mờ dần.")]
    [SerializeField] private float _flashHoldDuration = 0.1f;

    [Tooltip("Thời gian mờ dần TỪ đỏ VỀ màu thường.")]
    [SerializeField] private float _flashOutDuration = 0.4f;

    [Tooltip("Khoảng NGHỈ giữa 2 lần chớp — càng lớn càng 'lâu lâu " +
             "nháy 1 cái', càng nhỏ càng gần với nháy liên tục.")]
    [SerializeField] private float _pauseBetweenFlashes = 1.1f;


    private RectTransform _dayRect;

    private Vector3 _dayOriginalScale;

    private int _lastDay = -1;

    private Tween _dayPunchTween;

    private Sequence _urgentFlashSeq;

    private bool _isPulsing;

    private bool _isFrozenForTransition;


    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        if (_dayText != null)
        {
            _dayRect =
                _dayText.transform as RectTransform;


            _dayOriginalScale =
                _dayRect != null
                    ? _dayRect.localScale
                    : _dayText.transform.localScale;
        }


        if (_timeText != null)
        {
            _normalTimeColor =
                _timeText.color;
        }
    }


    private void OnDisable()
    {
        KillTweens();

        _isPulsing = false;

        _isFrozenForTransition = false;
    }


    private void Update()
    {
        if (GameDayManager.Instance == null)
            return;


        bool isTransitioning =
            GameDayIntroPanel.Instance != null &&
            GameDayIntroPanel.Instance.IsVisible &&
            !GameDayIntroPanel.Instance.IsFullyBlack;


        if (isTransitioning)
        {
            if (!_isFrozenForTransition)
            {
                _isFrozenForTransition = true;

                StopUrgentPulse();

                _isPulsing = false;
            }


            return;
        }


        _isFrozenForTransition = false;


        UpdateDayText();

        UpdateTimeText();
    }


    // ============================================================
    // DAY
    // ============================================================

    private void UpdateDayText()
    {
        int day =
            GameDayManager.Instance.CurrentDay;


        if (_dayText != null)
        {
            _dayText.text =
                $"Day {day}";
        }


        if (_lastDay == -1)
        {
            _lastDay = day;
            return;
        }


        if (day != _lastDay)
        {
            _lastDay = day;

            PlayDayPunch();
        }
    }


    private void PlayDayPunch()
    {
        if (_dayRect == null)
            return;


        if (_dayPunchTween != null &&
            _dayPunchTween.IsActive())
        {
            _dayPunchTween.Kill();
        }


        _dayRect.localScale =
            _dayOriginalScale;


        _dayPunchTween =
            _dayRect.DOPunchScale(
                Vector3.one * _dayPunchScale,
                _dayPunchDuration,
                8,
                0.9f
            );
    }


    // ============================================================
    // TIME
    // ============================================================

    private void UpdateTimeText()
    {
        if (_timeText == null)
            return;


        _timeText.text =
            GameDayManager.Instance.GetGameClockText();


        bool urgent =
            GameDayManager.Instance.IsUrgent;


        if (urgent && !_isPulsing)
        {
            _isPulsing = true;

            StartUrgentPulse();
        }
        else if (!urgent && _isPulsing)
        {
            _isPulsing = false;

            StopUrgentPulse();
        }
    }


    // ============================================================
    // URGENT PULSE - CHỚP NGẮN RỒI NGHỈ (không liên tục)
    // ============================================================

    private void StartUrgentPulse()
    {
        if (_timeText == null)
            return;

        if (_urgentFlashSeq != null &&
            _urgentFlashSeq.IsActive())
        {
            _urgentFlashSeq.Kill();
        }


        _timeText.color =
            _normalTimeColor;


        Sequence seq = DOTween.Sequence();

        // 1. Chớp nhanh sang đỏ.
        seq.Append(
            _timeText
                .DOColor(_urgentTimeColor, _flashInDuration)
                .SetEase(Ease.OutQuad)
        );

        // 2. Giữ nguyên đỏ 1 nhịp ngắn.
        if (_flashHoldDuration > 0f)
        {
            seq.AppendInterval(_flashHoldDuration);
        }

        // 3. Mờ dần về màu thường.
        seq.Append(
            _timeText
                .DOColor(_normalTimeColor, _flashOutDuration)
                .SetEase(Ease.InQuad)
        );

        // 4. Nghỉ 1 khoảng trước khi chớp lại.
        if (_pauseBetweenFlashes > 0f)
        {
            seq.AppendInterval(_pauseBetweenFlashes);
        }

        seq.SetLoops(-1, LoopType.Restart);

        seq.SetUpdate(true);

        _urgentFlashSeq = seq;
    }


    private void StopUrgentPulse()
    {
        if (_urgentFlashSeq != null &&
            _urgentFlashSeq.IsActive())
        {
            _urgentFlashSeq.Kill();
        }

        _urgentFlashSeq = null;

        if (_timeText != null)
        {
            _timeText.color =
                _normalTimeColor;
        }
    }


    // ============================================================
    // KILL
    // ============================================================

    private void KillTweens()
    {
        if (_dayPunchTween != null &&
            _dayPunchTween.IsActive())
        {
            _dayPunchTween.Kill();
        }

        if (_urgentFlashSeq != null &&
            _urgentFlashSeq.IsActive())
        {
            _urgentFlashSeq.Kill();
        }
    }
}