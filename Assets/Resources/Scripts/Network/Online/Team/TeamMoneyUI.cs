using TMPro;
using UnityEngine;
using DG.Tweening;

public class TeamMoneyUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI _moneyText;

    [Header("Counting")]
    [Tooltip("Khoảng thời gian giữa mỗi bước khi số tiền thay đổi ít.")]
    [SerializeField] private float _slowStepTime = 0.035f;

    [Tooltip("Khoảng thời gian giữa các bước khi đang tăng tốc.")]
    [SerializeField] private float _fastStepTime = 0.012f;

    [Tooltip("Khoảng cách tiền mà bắt đầu tăng tốc.")]
    [SerializeField] private int _accelerationStart = 50;

    [Tooltip("Khoảng cách tiền mà đạt tốc độ tối đa.")]
    [SerializeField] private int _accelerationEnd = 500;

    [Tooltip("Khoảng cách cuối cùng sẽ đếm chậm lại.")]
    [SerializeField] private int _slowDownDistance = 30;

    [Tooltip("Số tiền tối đa được hiển thị mỗi bước khi đang tăng tốc.")]
    [SerializeField] private int _maxStep = 10;

    [Header("Large Amount")]
    [Tooltip("Tổng thời gian tối thiểu cho những thay đổi tiền rất lớn.")]
    [SerializeField] private float _largeChangeDuration = 1.2f;

    [Tooltip("Khoảng cách được xem là thay đổi rất lớn.")]
    [SerializeField] private int _largeChangeDistance = 1000;

    [Header("Number Punch")]
    [SerializeField] private bool _punchEveryStep = true;

    [SerializeField] private float _numberPunchScale = 1.02f;

    [SerializeField] private float _numberPunchDuration = 0.045f;

    [Header("Final Impact")]
    [SerializeField] private float _impactScale = 1.15f;

    [SerializeField] private float _impactUpDuration = 0.12f;

    [SerializeField] private float _impactDownDuration = 0.08f;

    [SerializeField] private float _impactReturnDuration = 0.16f;

    private RectTransform _rectTransform;

    private Vector3 _originalScale;

    private int _displayedMoney;

    private int _targetMoney;

    private bool _initialized;

    private Sequence _countSequence;

    private Sequence _impactSequence;

    private Tween _scaleTween;

    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        _rectTransform = transform as RectTransform;

        if (_rectTransform != null)
        {
            _originalScale =
                _rectTransform.localScale;
        }
        else
        {
            _originalScale =
                transform.localScale;
        }
    }

    private void OnEnable()
    {
        RefreshInitialMoney();
    }

    private void OnDisable()
    {
        KillAnimations();
    }

    private void OnDestroy()
    {
        KillAnimations();
    }

    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        if (TeamWallet.Instance == null)
            return;

        int currentMoney =
            TeamWallet.Instance.Money;

        // --------------------------------------------------------
        // INITIAL
        // --------------------------------------------------------

        if (!_initialized)
        {
            _initialized = true;

            _displayedMoney =
                currentMoney;

            _targetMoney =
                currentMoney;

            UpdateMoneyText(
                _displayedMoney
            );

            return;
        }

        // --------------------------------------------------------
        // NO CHANGE
        // --------------------------------------------------------

        if (_targetMoney == currentMoney)
            return;

        // --------------------------------------------------------
        // MONEY CHANGED
        // --------------------------------------------------------

        AnimateMoneyChange(
            currentMoney
        );
    }

    // ============================================================
    // INITIAL MONEY
    // ============================================================

    private void RefreshInitialMoney()
    {
        KillAnimations();

        _initialized = false;

        _displayedMoney = 0;

        _targetMoney = 0;

        ResetScale();

        if (_moneyText != null)
            _moneyText.text = "$0";

        if (TeamWallet.Instance == null)
            return;

        int currentMoney =
            TeamWallet.Instance.Money;

        _initialized = true;

        _displayedMoney =
            currentMoney;

        _targetMoney =
            currentMoney;

        UpdateMoneyText(
            _displayedMoney
        );
    }

    // ============================================================
    // MONEY CHANGE
    // ============================================================

    private void AnimateMoneyChange(
        int newMoney)
    {
        int startMoney =
            _displayedMoney;

        _targetMoney =
            newMoney;

        // --------------------------------------------------------
        // KILL COUNT ANIMATION
        // --------------------------------------------------------

        if (_countSequence != null &&
            _countSequence.IsActive())
        {
            _countSequence.Kill();
        }

        // --------------------------------------------------------
        // KILL FINAL IMPACT
        // --------------------------------------------------------

        if (_impactSequence != null &&
            _impactSequence.IsActive())
        {
            _impactSequence.Kill();
        }

        // --------------------------------------------------------
        // SCALE TWEEN
        // --------------------------------------------------------

        if (_scaleTween != null &&
            _scaleTween.IsActive())
        {
            _scaleTween.Kill();
        }

        ResetScale();

        // --------------------------------------------------------
        // ALREADY AT TARGET
        // --------------------------------------------------------

        if (startMoney == newMoney)
        {
            PlayImpactAnimation();
            return;
        }

        // --------------------------------------------------------
        // CREATE ADAPTIVE COUNT
        // --------------------------------------------------------

        _countSequence =
            DOTween.Sequence();

        int difference =
            Mathf.Abs(
                newMoney - startMoney
            );

        int direction =
            newMoney > startMoney
                ? 1
                : -1;

        // --------------------------------------------------------
        // SMALL CHANGE
        //
        // Ví dụ:
        //
        // 100 → 90
        //
        // sẽ là:
        //
        // 100
        // 99
        // 98
        // ...
        // 90
        // --------------------------------------------------------

        if (difference <= _accelerationStart)
        {
            AddSlowCount(
                startMoney,
                newMoney,
                direction,
                difference
            );
        }

        // --------------------------------------------------------
        // MEDIUM CHANGE
        //
        // Ví dụ:
        //
        // 1000 → 500
        //
        // đầu chậm
        // giữa nhanh
        // cuối chậm
        // --------------------------------------------------------

        else if (difference <
                 _largeChangeDistance)
        {
            AddAdaptiveCount(
                startMoney,
                newMoney,
                direction,
                difference
            );
        }

        // --------------------------------------------------------
        // LARGE CHANGE
        //
        // Ví dụ:
        //
        // 5000 → 1000
        //
        // Không chạy 4000 callback.
        // Tự động tăng step.
        // --------------------------------------------------------

        else
        {
            AddLargeCount(
                startMoney,
                newMoney,
                direction,
                difference
            );
        }

        // --------------------------------------------------------
        // COMPLETE
        // --------------------------------------------------------

        _countSequence.OnComplete(() =>
        {
            _displayedMoney =
                newMoney;

            UpdateMoneyText(
                _displayedMoney
            );

            PlayImpactAnimation();
        });

        _countSequence.Play();
    }

    // ============================================================
    // SMALL COUNT
    // ============================================================

    private void AddSlowCount(
        int startMoney,
        int targetMoney,
        int direction,
        int difference)
    {
        for (int i = 1;
             i <= difference;
             i++)
        {
            int value =
                startMoney +
                (direction * i);

            AddCountStep(
                value,
                _slowStepTime
            );
        }
    }

    // ============================================================
    // MEDIUM COUNT
    // ============================================================

    private void AddAdaptiveCount(
        int startMoney,
        int targetMoney,
        int direction,
        int difference)
    {
        int current =
            startMoney;

        while (current != targetMoney)
        {
            int distanceRemaining =
                Mathf.Abs(
                    targetMoney - current
                );

            // ----------------------------------------------------
            // GẦN ĐÍCH
            //
            // Quay về từng số một.
            // ----------------------------------------------------

            if (distanceRemaining <=
                _slowDownDistance)
            {
                current += direction;

                AddCountStep(
                    current,
                    _slowStepTime
                );

                continue;
            }

            // ----------------------------------------------------
            // TÍNH TỐC ĐỘ
            // ----------------------------------------------------

            float acceleration =
                Mathf.InverseLerp(
                    _accelerationStart,
                    _accelerationEnd,
                    Mathf.Abs(
                        current - startMoney
                    )
                );

            int step =
                Mathf.RoundToInt(
                    Mathf.Lerp(
                        1f,
                        _maxStep,
                        acceleration
                    )
                );

            step =
                Mathf.Max(
                    1,
                    step
                );

            // ----------------------------------------------------
            // KHÔNG ĐƯỢC VƯỢT TARGET
            // ----------------------------------------------------

            step =
                Mathf.Min(
                    step,
                    distanceRemaining
                );

            current +=
                direction * step;

            float stepTime =
                Mathf.Lerp(
                    _slowStepTime,
                    _fastStepTime,
                    acceleration
                );

            AddCountStep(
                current,
                stepTime
            );
        }
    }

    // ============================================================
    // LARGE COUNT
    // ============================================================

    private void AddLargeCount(
        int startMoney,
        int targetMoney,
        int direction,
        int difference)
    {
        int current =
            startMoney;

        // --------------------------------------------------------
        // ƯỚC TÍNH SỐ BƯỚC
        // --------------------------------------------------------

        int desiredSteps =
            Mathf.RoundToInt(
                _largeChangeDuration /
                _fastStepTime
            );

        desiredSteps =
            Mathf.Max(
                20,
                desiredSteps
            );

        // --------------------------------------------------------
        // TÍNH STEP
        // --------------------------------------------------------

        int step =
            Mathf.CeilToInt(
                (float)difference /
                desiredSteps
            );

        step =
            Mathf.Max(
                1,
                step
            );

        // --------------------------------------------------------
        // COUNT
        // --------------------------------------------------------

        while (current != targetMoney)
        {
            int distanceRemaining =
                Mathf.Abs(
                    targetMoney - current
                );

            // ----------------------------------------------------
            // GẦN ĐÍCH
            //
            // Chậm lại và từng số một.
            // ----------------------------------------------------

            if (distanceRemaining <=
                _slowDownDistance)
            {
                current += direction;

                AddCountStep(
                    current,
                    _slowStepTime
                );

                continue;
            }

            // ----------------------------------------------------
            // ADAPTIVE STEP
            //
            // Càng xa target → bước càng lớn.
            // Càng gần target → bước càng nhỏ.
            // ----------------------------------------------------

            float normalizedDistance =
                Mathf.InverseLerp(
                    _slowDownDistance,
                    difference,
                    distanceRemaining
                );

            int currentStep =
                Mathf.RoundToInt(
                    Mathf.Lerp(
                        1f,
                        step,
                        normalizedDistance
                    )
                );

            currentStep =
                Mathf.Max(
                    1,
                    currentStep
                );

            currentStep =
                Mathf.Min(
                    currentStep,
                    distanceRemaining
                );

            current +=
                direction * currentStep;

            // ----------------------------------------------------
            // TỐC ĐỘ
            // ----------------------------------------------------

            float stepTime =
                Mathf.Lerp(
                    _slowStepTime,
                    _fastStepTime,
                    normalizedDistance
                );

            AddCountStep(
                current,
                stepTime
            );
        }
    }

    // ============================================================
    // ADD COUNT STEP
    // ============================================================

    private void AddCountStep(
        int value,
        float duration)
    {
        _countSequence.AppendInterval(
            duration
        );

        _countSequence.AppendCallback(
            () =>
            {
                _displayedMoney =
                    value;

                UpdateMoneyText(
                    value
                );

                if (_punchEveryStep)
                {
                    PlayNumberPunch();
                }
            }
        );
    }

    // ============================================================
    // NUMBER PUNCH
    // ============================================================

    private void PlayNumberPunch()
    {
        if (_rectTransform == null)
            return;

        if (_scaleTween != null &&
            _scaleTween.IsActive())
        {
            _scaleTween.Kill();
        }

        _scaleTween =
            _rectTransform
                .DOPunchScale(
                    Vector3.one *
                    (_numberPunchScale - 1f),
                    _numberPunchDuration,
                    2,
                    0.5f
                )
                .SetEase(
                    Ease.OutQuad
                );
    }

    // ============================================================
    // FINAL IMPACT
    // ============================================================

    private void PlayImpactAnimation()
    {
        if (_rectTransform == null)
            return;

        if (_impactSequence != null &&
            _impactSequence.IsActive())
        {
            _impactSequence.Kill();
        }

        if (_scaleTween != null &&
            _scaleTween.IsActive())
        {
            _scaleTween.Kill();
        }

        ResetScale();

        Vector3 normalScale =
            _originalScale;

        Vector3 bigScale =
            normalScale *
            _impactScale;

        Vector3 smallScale =
            normalScale *
            0.97f;

        _impactSequence =
            DOTween.Sequence();

        // --------------------------------------------------------
        // 1. PHÓNG TO
        // --------------------------------------------------------

        _impactSequence.Append(
            _rectTransform
                .DOScale(
                    bigScale,
                    _impactUpDuration
                )
                .SetEase(
                    Ease.OutBack
                )
        );

        // --------------------------------------------------------
        // 2. THU NHỎ
        // --------------------------------------------------------

        _impactSequence.Append(
            _rectTransform
                .DOScale(
                    smallScale,
                    _impactDownDuration
                )
                .SetEase(
                    Ease.InOutQuad
                )
        );

        // --------------------------------------------------------
        // 3. VỀ SCALE BAN ĐẦU
        // --------------------------------------------------------

        _impactSequence.Append(
            _rectTransform
                .DOScale(
                    normalScale,
                    _impactReturnDuration
                )
                .SetEase(
                    Ease.OutBack
                )
        );

        _impactSequence.Play();
    }

    // ============================================================
    // UPDATE TEXT
    // ============================================================

    private void UpdateMoneyText(
        int money)
    {
        if (_moneyText == null)
            return;

        _moneyText.text =
            $"${money}";
    }

    // ============================================================
    // RESET SCALE
    // ============================================================

    private void ResetScale()
    {
        if (_rectTransform != null)
        {
            _rectTransform.localScale =
                _originalScale;
        }
        else
        {
            transform.localScale =
                _originalScale;
        }
    }

    // ============================================================
    // KILL ANIMATIONS
    // ============================================================

    private void KillAnimations()
    {
        if (_countSequence != null &&
            _countSequence.IsActive())
        {
            _countSequence.Kill();
        }

        if (_impactSequence != null &&
            _impactSequence.IsActive())
        {
            _impactSequence.Kill();
        }

        if (_scaleTween != null &&
            _scaleTween.IsActive())
        {
            _scaleTween.Kill();
        }

        if (_rectTransform != null)
            _rectTransform.DOKill();

        ResetScale();
    }
}