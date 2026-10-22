using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class NotiFeedItem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI _playerNameText;
    [SerializeField] private TextMeshProUGUI _moneyText;
    [SerializeField] private Image _borderImage;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Colors")]
    [SerializeField] private Color _positiveColor =
        new Color(0.4392f, 1f, 0.4392f);

    [SerializeField] private Color _negativeColor =
        new Color(1f, 0.2941f, 0.2941f);

    [Header("Intro Animation")]
    [SerializeField] private float _introDuration = 0.38f;
    [SerializeField] private float _introOffsetX = 80f;
    [SerializeField] private float _introStartScale = 0.82f;

    [Header("Exit Animation")]
    [SerializeField] private float _exitDuration = 0.28f;
    [SerializeField] private float _exitOffsetX = 60f;
    [SerializeField] private float _exitEndScale = 0.88f;

    [Header("Money Count")]
    [SerializeField] private MoneyCountSequenceBuilder.Settings _countSettings =
        MoneyCountSequenceBuilder.Settings.Default;

    [Header("Money Number Animation")]
    [SerializeField] private bool _animateNumberOnCount = true;
    [SerializeField] private float _numberPunchScale = 1.035f;
    [SerializeField] private float _numberPunchDuration = 0.08f;

    private RectTransform _rectTransform;

    private Sequence _countSequence;
    private Sequence _introSequence;
    private Sequence _exitSequence;

    private Tween _numberScaleTween;

    private Vector2 _normalAnchoredPosition;
    private Vector3 _normalScale;

    public RectTransform RectTransform => _rectTransform;

    private void Awake()
    {
        _rectTransform = transform as RectTransform;

        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();

        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _normalAnchoredPosition = _rectTransform.anchoredPosition;
        _normalScale = Vector3.one;
    }

    // ============================================================
    // SETUP
    // ============================================================

    public void Setup(string playerName, int amount)
    {
        KillTweens();

        if (_rectTransform == null)
            _rectTransform = transform as RectTransform;

        _normalAnchoredPosition = _rectTransform.anchoredPosition;

        // --------------------------------------------------------
        // PLAYER NAME
        // --------------------------------------------------------

        if (_playerNameText != null)
        {
            _playerNameText.text = playerName;
        }

        // --------------------------------------------------------
        // POSITIVE / NEGATIVE
        // --------------------------------------------------------

        bool isNegative = amount < 0;

        if (_borderImage != null)
        {
            _borderImage.gameObject.SetActive(isNegative);
        }

        if (_moneyText != null)
        {
            _moneyText.color =
                isNegative
                    ? _negativeColor
                    : _positiveColor;
        }

        // --------------------------------------------------------
        // INITIAL STATE
        // --------------------------------------------------------

        _canvasGroup.alpha = 0f;

        _rectTransform.localScale =
            Vector3.one * _introStartScale;

        _rectTransform.anchoredPosition =
            _normalAnchoredPosition +
            Vector2.right * _introOffsetX;

        // --------------------------------------------------------
        // INITIAL MONEY
        // --------------------------------------------------------

        UpdateMoneyText(0, amount);

        // --------------------------------------------------------
        // INTRO
        // --------------------------------------------------------

        _introSequence = DOTween.Sequence();

        _introSequence.Join(
            _canvasGroup.DOFade(
                1f,
                _introDuration * 0.65f
            )
        );

        _introSequence.Join(
            _rectTransform.DOAnchorPos(
                _normalAnchoredPosition,
                _introDuration
            )
            .SetEase(Ease.OutCubic)
        );

        _introSequence.Join(
            _rectTransform.DOScale(
                Vector3.one,
                _introDuration
            )
            .SetEase(Ease.OutBack, 1.25f)
        );

        // --------------------------------------------------------
        // COUNT MONEY
        // --------------------------------------------------------

        _countSequence =
            MoneyCountSequenceBuilder.Build(
                0,
                amount,
                _countSettings,
                value =>
                {
                    UpdateMoneyText(
                        value,
                        amount
                    );

                    if (_animateNumberOnCount)
                        PlayNumberPunch();
                }
            );

        _countSequence.Play();

        _introSequence.Play();
    }


    // ============================================================
    // MONEY TEXT
    // ============================================================

    private void UpdateMoneyText(
    int currentValue,
    int fullAmount)
    {
        if (_moneyText == null)
            return;

        string sign =
            fullAmount < 0
                ? "- "
                : "+ ";

        _moneyText.text =
            $"{sign}{Mathf.Abs(currentValue):N0}$";
    }


    // ============================================================
    // NUMBER PUNCH
    // ============================================================

    private void PlayNumberPunch()
    {
        if (_moneyText == null)
            return;

        Transform target =
            _moneyText.transform;

        if (_numberScaleTween != null &&
            _numberScaleTween.IsActive())
        {
            _numberScaleTween.Kill();
        }

        target.localScale =
            Vector3.one;

        _numberScaleTween =
            target
                .DOPunchScale(
                    Vector3.one *
                    (_numberPunchScale - 1f),
                    _numberPunchDuration,
                    1,
                    0.7f
                )
                .SetEase(Ease.OutQuad);
    }


    // ============================================================
    // EXIT
    // ============================================================

    public void FadeOutAndDisable(
        float duration,
        Action onComplete)
    {
        KillTweens();

        Vector2 startPosition =
            _rectTransform.anchoredPosition;

        Vector2 endPosition =
            startPosition +
            Vector2.right * _exitOffsetX;

        _exitSequence =
            DOTween.Sequence();

        _exitSequence.Join(
            _canvasGroup
                .DOFade(
                    0f,
                    duration
                )
                .SetEase(Ease.InQuad)
        );

        _exitSequence.Join(
            _rectTransform
                .DOAnchorPos(
                    endPosition,
                    duration
                )
                .SetEase(Ease.InCubic)
        );

        _exitSequence.Join(
            _rectTransform
                .DOScale(
                    Vector3.one * _exitEndScale,
                    duration
                )
                .SetEase(Ease.InQuad)
        );

        _exitSequence.OnComplete(() =>
        {
            onComplete?.Invoke();
        });

        _exitSequence.Play();
    }


    // ============================================================
    // PREPARE FOR STACK MOVEMENT
    // ============================================================

    public void SetImmediatePosition(
        float y)
    {
        if (_rectTransform == null)
            return;

        _rectTransform.DOKill();

        _rectTransform.anchoredPosition =
            new Vector2(
                _normalAnchoredPosition.x,
                y
            );

        _normalAnchoredPosition =
            _rectTransform.anchoredPosition;
    }


    public void AnimateToPosition(
        float y,
        float duration,
        Ease ease)
    {
        if (_rectTransform == null)
            return;

        _normalAnchoredPosition =
            new Vector2(
                _normalAnchoredPosition.x,
                y
            );

        _rectTransform
            .DOAnchorPosY(
                y,
                duration
            )
            .SetEase(ease);
    }


    // ============================================================
    // KILL TWEENS
    // ============================================================

    private void KillTweens()
    {
        if (_countSequence != null &&
            _countSequence.IsActive())
        {
            _countSequence.Kill();
        }

        if (_introSequence != null &&
            _introSequence.IsActive())
        {
            _introSequence.Kill();
        }

        if (_exitSequence != null &&
            _exitSequence.IsActive())
        {
            _exitSequence.Kill();
        }

        if (_numberScaleTween != null &&
            _numberScaleTween.IsActive())
        {
            _numberScaleTween.Kill();
        }

        if (_rectTransform != null)
        {
            _rectTransform.DOKill();
        }
    }


    // ============================================================
    // DESTROY
    // ============================================================

    private void OnDestroy()
    {
        KillTweens();
    }
}