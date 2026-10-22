using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class GameDayIntroPanel : MonoBehaviour
{
    public static GameDayIntroPanel Instance { get; private set; }

    public static event Action OnDayPanelFullyBlack;

    public static event Action OnDayIntroFinished;

    [Header("References")]

    [SerializeField]
    private CanvasGroup _canvasGroup;

    [SerializeField]
    private TextMeshProUGUI _dayText;

    [Header("Timing - Che do TUC THI")]

    [SerializeField]
    private float _textFadeInTime = 0.5f;

    [SerializeField]
    private float _holdSeconds = 2f;

    [SerializeField]
    private float _textFadeOutTime = 0.5f;

    [SerializeField]
    private float _fadeOutTime = 0.6f;

    [Header("Timing - Che do FADE TOAN BO")]

    [SerializeField]
    private float _wholePanelFadeInTime = 0.5f;

    [SerializeField]
    private float _wholePanelHoldSeconds = 1.5f;

    [SerializeField]
    private float _wholePanelFadeOutTime = 0.5f;

    private Coroutine _routine;

    private bool _isVisible;


    public bool IsVisible => _isVisible;

    public bool IsFullyBlack { get; private set; }


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            transform.SetParent(null);

            DontDestroyOnLoad(gameObject);


            GameplayLoadingOverlay.OnWaitingOverlayHidden +=
                HandleWaitingOverlayHidden;


            SetInstant(false);
        }
        else
        {
            Destroy(gameObject);
        }
    }


    private void OnDestroy()
    {
        if (Instance == this)
        {
            GameplayLoadingOverlay.OnWaitingOverlayHidden -=
                HandleWaitingOverlayHidden;


            Instance = null;
        }
    }


    private void HandleWaitingOverlayHidden()
    {
        Show(false);
    }


    public void Show()
    {
        Show(false);
    }

    public void Show(bool fadeInBackground)
    {
        int fallbackDay =
            GameDayManager.Instance != null
                ? GameDayManager.Instance.PendingDisplayDay
                : 1;

        Show(fadeInBackground, fallbackDay);
    }

    public void Show(bool fadeInBackground, int dayToShow)
    {
        if (_isVisible)
            return;


        _isVisible = true;

        IsFullyBlack = false;



        if (_dayText != null)
        {
            _dayText.text =
                $"DAY {dayToShow}";
        }


        gameObject.SetActive(true);


        if (_routine != null)
        {
            StopCoroutine(_routine);
        }


        if (fadeInBackground)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;

                _canvasGroup.blocksRaycasts = true;
            }


            SetTextAlpha(
                _dayText,
                1f
            );


            _routine =
                StartCoroutine(
                    WholePanelFadeInHoldFadeOutRoutine()
                );
        }


        else
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;

                _canvasGroup.blocksRaycasts = true;
            }


            SetTextAlpha(
                _dayText,
                0f
            );


            _routine =
                StartCoroutine(
                    TextFadeInThenHoldThenFadeOutRoutine()
                );
        }
    }


    private IEnumerator TextFadeInThenHoldThenFadeOutRoutine()
    {
        yield return FadeTextInRoutine();


        if (_holdSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(
                _holdSeconds
            );
        }


        yield return FadeTextOutRoutine();


        yield return FadeOutRoutine();


        _routine = null;
    }


    private IEnumerator FadeTextInRoutine()
    {
        if (_dayText == null)
            yield break;


        float t = 0f;


        while (t < _textFadeInTime)
        {
            t += Time.unscaledDeltaTime;


            float ratio =
                _textFadeInTime <= 0f
                    ? 1f
                    : t / _textFadeInTime;


            SetTextAlpha(
                _dayText,
                Mathf.Lerp(
                    0f,
                    1f,
                    ratio
                )
            );


            yield return null;
        }


        SetTextAlpha(
            _dayText,
            1f
        );
    }


    private IEnumerator FadeTextOutRoutine()
    {
        if (_dayText == null)
            yield break;


        float start =
            _dayText.color.a;


        float t = 0f;


        while (t < _textFadeOutTime)
        {
            t += Time.unscaledDeltaTime;


            float ratio =
                _textFadeOutTime <= 0f
                    ? 1f
                    : t / _textFadeOutTime;


            SetTextAlpha(
                _dayText,
                Mathf.Lerp(
                    start,
                    0f,
                    ratio
                )
            );


            yield return null;
        }


        SetTextAlpha(
            _dayText,
            0f
        );
    }


    // ============================================================
    // PANEL FADE OUT
    // ============================================================

    private IEnumerator FadeOutRoutine()
    {
        if (_canvasGroup == null)
        {
            FinishHide();

            yield break;
        }


        _canvasGroup.blocksRaycasts = false;


        float start =
            _canvasGroup.alpha;


        float t = 0f;


        while (t < _fadeOutTime)
        {
            t += Time.unscaledDeltaTime;


            float ratio =
                _fadeOutTime <= 0f
                    ? 1f
                    : t / _fadeOutTime;


            _canvasGroup.alpha =
                Mathf.Lerp(
                    start,
                    0f,
                    ratio
                );


            yield return null;
        }


        _canvasGroup.alpha = 0f;


        FinishHide();
    }


    // ============================================================
    // WHOLE PANEL
    // ============================================================

    private IEnumerator WholePanelFadeInHoldFadeOutRoutine()
    {
        // ========================================================
        // 1. FADE IN
        // ========================================================

        yield return FadeCanvasGroupRoutine(
            0f,
            1f,
            _wholePanelFadeInTime
        );


        // ========================================================
        // 2. PANEL DA DEN HOAN TOAN
        // ========================================================

        IsFullyBlack = true;


        // ========================================================
        // COMMIT DAY
        // ========================================================
        //
        // GameDayManager se:
        //
        // CurrentDay = PendingDay
        // MoneyTarget = PendingTarget
        // Reset Timer
        //
        // Tat ca xay ra khi man hinh dang den.
        //

        OnDayPanelFullyBlack?.Invoke();


        // ========================================================
        // 3. HOLD BLACK
        // ========================================================

        float hold =
            _wholePanelHoldSeconds > 0f
                ? _wholePanelHoldSeconds
                : _holdSeconds;


        if (hold > 0f)
        {
            yield return new WaitForSecondsRealtime(
                hold
            );
        }


        // ========================================================
        // 4. FADE OUT
        // ========================================================

        if (_canvasGroup != null)
        {
            _canvasGroup.blocksRaycasts = false;
        }


        yield return FadeCanvasGroupRoutine(
            1f,
            0f,
            _wholePanelFadeOutTime
        );


        // ========================================================
        // 5. FINISH
        // ========================================================

        IsFullyBlack = false;


        FinishHide();


        _routine = null;
    }


    // ============================================================
    // CANVAS FADE
    // ============================================================

    private IEnumerator FadeCanvasGroupRoutine(
        float from,
        float to,
        float duration)
    {
        if (_canvasGroup == null)
            yield break;


        _canvasGroup.alpha = from;


        float t = 0f;


        while (t < duration)
        {
            t += Time.unscaledDeltaTime;


            float ratio =
                duration <= 0f
                    ? 1f
                    : t / duration;


            _canvasGroup.alpha =
                Mathf.Lerp(
                    from,
                    to,
                    ratio
                );


            yield return null;
        }


        _canvasGroup.alpha = to;
    }


    // ============================================================
    // FINISH
    // ============================================================

    private void FinishHide()
    {
        gameObject.SetActive(false);

        _isVisible = false;


        // ========================================================
        // QUAN TRONG
        // ========================================================
        //
        // Event nay duoc goi SAU KHI:
        //
        // DAY panel da fade out hoan toan.
        //
        // GameDayManager se broadcast:
        //
        // "Day 2 begins..."
        //
        // o day.
        //

        OnDayIntroFinished?.Invoke();
    }


    // ============================================================
    // SET INSTANT
    // ============================================================

    private void SetInstant(bool visible)
    {
        IsFullyBlack = false;


        if (_canvasGroup != null)
        {
            _canvasGroup.alpha =
                visible
                    ? 1f
                    : 0f;


            _canvasGroup.blocksRaycasts =
                visible;
        }


        gameObject.SetActive(visible);
    }


    // ============================================================
    // TEXT ALPHA
    // ============================================================

    private static void SetTextAlpha(
        TextMeshProUGUI text,
        float alpha)
    {
        if (text == null)
            return;


        Color c =
            text.color;


        c.a = alpha;


        text.color = c;
    }
}