using TMPro;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Hiển thị Money Target.
///
/// Trong lúc Day Panel đang transition:
///     UI giữ nguyên trạng thái cũ.
///
/// Khi panel đã tắt:
///     UI nhận Day / Target / Money mới bình thường.
/// </summary>
public class MoneyTargetUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI _targetText;


    [Header("Colors")]
    [SerializeField] private Color _neutralColor =
        Color.white;

    [SerializeField] private Color _reachedColor =
        new Color(0.45f, 1f, 0.45f);

    [SerializeField] private Color _increaseFlashColor =
        new Color(0.55f, 1f, 0.6f);

    [SerializeField] private Color _decreaseFlashColor =
        new Color(1f, 0.45f, 0.45f);

    [SerializeField] private Color _newDayFlashColor =
        new Color(1f, 0.85f, 0.3f);


    [Header("Punch")]
    [SerializeField] private float _punchScale = 0.25f;

    [SerializeField] private float _punchDuration = 0.35f;

    [SerializeField] private float _colorFlashDuration = 0.5f;

    [SerializeField] private float _newDayPunchScale = 0.4f;


    private RectTransform _rectTransform;

    private Vector3 _originalScale;

    private int _lastMoney;

    private int _lastTarget;

    private int _lastDay;

    private bool _initialized;

    private Sequence _flashSequence;


    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        _rectTransform =
            transform as RectTransform;


        _originalScale =
            _rectTransform != null
                ? _rectTransform.localScale
                : transform.localScale;
    }


    private void OnEnable()
    {
        _initialized = false;
    }


    private void OnDisable()
    {
        KillFlash();
    }


    private void OnDestroy()
    {
        KillFlash();
    }


    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        if (TeamWallet.Instance == null ||
            GameDayManager.Instance == null)
        {
            return;
        }


        // ========================================================
        // DAY PANEL ĐANG CHẠY
        // ========================================================
        //
        // Không cho UI nhảy trước khi màn hình đen.
        //

        if (GameDayIntroPanel.Instance != null &&
            GameDayIntroPanel.Instance.IsVisible)
        {
            return;
        }


        int money =
            TeamWallet.Instance.Money;


        int target =
            GameDayManager.Instance.MoneyTarget;


        int day =
            GameDayManager.Instance.CurrentDay;


        // ========================================================
        // INITIAL
        // ========================================================

        if (!_initialized)
        {
            _initialized = true;

            _lastMoney = money;

            _lastTarget = target;

            _lastDay = day;

            RefreshText(
                money,
                target
            );

            return;
        }


        // ========================================================
        // DAY CHANGE
        // ========================================================

        if (day != _lastDay ||
            target != _lastTarget)
        {
            _lastDay = day;

            _lastTarget = target;

            _lastMoney = money;


            RefreshText(
                money,
                target
            );


            PlayFlash(
                _newDayFlashColor,
                _newDayPunchScale
            );


            return;
        }


        // ========================================================
        // MONEY CHANGE
        // ========================================================

        if (money != _lastMoney)
        {
            bool increased =
                money > _lastMoney;


            _lastMoney = money;


            RefreshText(
                money,
                target
            );


            PlayFlash(
                increased
                    ? _increaseFlashColor
                    : _decreaseFlashColor,
                _punchScale
            );
        }
    }


    // ============================================================
    // TEXT
    // ============================================================

    private void RefreshText(
        int money,
        int target)
    {
        if (_targetText == null)
            return;


        _targetText.text =
            $"/ ${target}";


        _targetText.color =
            money >= target
                ? _reachedColor
                : _neutralColor;
    }


    // ============================================================
    // FLASH
    // ============================================================

    private void PlayFlash(
        Color flashColor,
        float punchScale)
    {
        if (_targetText == null ||
            _rectTransform == null)
        {
            return;
        }


        KillFlash();


        Color baseColor =
            _targetText.color;


        _rectTransform.localScale =
            _originalScale;


        _flashSequence =
            DOTween.Sequence();


        _flashSequence.Join(
            _rectTransform.DOPunchScale(
                Vector3.one * punchScale,
                _punchDuration,
                6,
                0.8f
            )
        );


        _flashSequence.Join(
            _targetText.DOColor(
                flashColor,
                _colorFlashDuration * 0.4f
            )
        );


        _flashSequence.Append(
            _targetText.DOColor(
                baseColor,
                _colorFlashDuration * 0.6f
            )
        );


        _flashSequence.Play();
    }


    // ============================================================
    // KILL
    // ============================================================

    private void KillFlash()
    {
        if (_flashSequence != null &&
            _flashSequence.IsActive())
        {
            _flashSequence.Kill();
        }


        if (_rectTransform != null)
        {
            _rectTransform.DOKill();

            _rectTransform.localScale =
                _originalScale;
        }
    }
}