using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Fusion;

public class FishingBiteVignetteEffect : MonoBehaviour
{
    public static FishingBiteVignetteEffect Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Volume _postProcessVolume;

    [Header("Vignette")]
    [SerializeField] private float _defaultIntensityFallback = 0.2f;
    [SerializeField] private float _targetIntensity = 0.55f;
    [SerializeField] private float _fadeInDuration = 0.6f;
    [SerializeField] private float _fadeOutDuration = 0.8f;
    [SerializeField] private AnimationCurve _fadeCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Vignette _vignette;
    private float _defaultIntensity;

    private Coroutine _activeRoutine;

    private void Awake()
    {
        StartCoroutine(WaitForNetworkAuthorityThenInitialize());
    }

    private IEnumerator WaitForNetworkAuthorityThenInitialize()
    {
        var networkObj = GetComponentInParent<NetworkObject>();

        if (networkObj == null)
        {
            Instance = this;
            InitializeVolume();
            yield break;
        }

        while (!networkObj.IsValid)
            yield return null;

        if (!networkObj.HasInputAuthority)
        {
            enabled = false;
            yield break;
        }

        Instance = this;
        InitializeVolume();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void InitializeVolume()
    {
        if (_postProcessVolume == null)
            _postProcessVolume = FindFirstObjectByType<Volume>();

        if (_postProcessVolume == null)
        {
            _defaultIntensity = _defaultIntensityFallback;
            return;
        }

        // Chỉ lấy Vignette.
        // Không còn Chromatic Aberration.
        _postProcessVolume.profile.TryGet(out _vignette);

        if (_vignette != null)
        {
            _defaultIntensity = _vignette.intensity.value;

            if (_defaultIntensity <= 0f)
                _defaultIntensity = _defaultIntensityFallback;
        }
        else
        {
            _defaultIntensity = _defaultIntensityFallback;
        }
    }

    public void ShowBite()
    {
        StartRoutine(ShowRoutine());
    }

    public void HideBite()
    {
        StartRoutine(HideRoutine());
    }

    private void StartRoutine(IEnumerator routine)
    {
        if (_activeRoutine != null)
            StopCoroutine(_activeRoutine);

        _activeRoutine = StartCoroutine(routine);
    }

    private IEnumerator ShowRoutine()
    {
        if (_vignette == null)
            yield break;

        float startV = _vignette.intensity.value;

        yield return FadeTo(
            _targetIntensity,
            _fadeInDuration,
            startV
        );
    }

    private IEnumerator HideRoutine()
    {
        if (_vignette == null)
            yield break;

        float startV = _vignette.intensity.value;

        yield return FadeTo(
            _defaultIntensity,
            _fadeOutDuration,
            startV
        );
    }

    private IEnumerator FadeTo(
        float targetV,
        float duration,
        float startV)
    {
        if (duration <= 0f)
        {
            _vignette.intensity.Override(targetV);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / duration
            );

            float eased = _fadeCurve.Evaluate(t);

            _vignette.intensity.Override(
                Mathf.Lerp(
                    startV,
                    targetV,
                    eased
                )
            );

            yield return null;
        }

        _vignette.intensity.Override(targetV);
    }
}