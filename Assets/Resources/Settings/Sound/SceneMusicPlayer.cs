using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dat script nay len 1 object o GOC cua MOI SCENE (VD "M-SceneMusic")
/// de TU DONG quyet dinh scene do nen phat nhac gi khi vua load xong.
///
/// 2 PHAN, dung doc lap hoac cung luc:
///
/// 1) NHAC NEN CHINH (_musicClipName): van qua SoundManager.PlayMusic()
///    nhu cu - de TRONG thi dung nhac dang phat tu scene truoc.
///
/// 2) CAC LOP AM THANH PHU (_ambienceLayers): moi lop la 1 AudioClip keo
///    TRUC TIEP vao Inspector, phat LOOP tren AudioSource rieng cua
///    script nay, co fade in. Cac AudioSource nay nam tren object cua
///    scene (KHONG DontDestroyOnLoad) nen TU HUY khi roi scene.
///
/// DOC LAP VOI VOLUME CUA SOUNDMANAGER:
/// - Script nay KHONG sua volume cua musicSource / sfxSource / bat ky
///   AudioSource nao khac - chi dieu khien cac AudioSource do CHINH NO
///   tao ra.
/// - Mac dinh volume cac lop phu CHI phu thuoc vao 'Volume' cua tung lop
///   (khong phu thuoc thanh Music trong Settings). Bat _followMusicVolume
///   neu muon chung.
/// - Master volume cua SoundManager ap qua AudioListener.volume nen tac
///   dong len MOI am thanh trong game (rieng cua Unity, khong phai do
///   script nay). Bat _ignoreMasterVolume neu muon cac lop phu KHONG bi
///   anh huong boi thanh Master.
/// </summary>
public class SceneMusicPlayer : MonoBehaviour
{
    [System.Serializable]
    public class AmbienceLayer
    {
        public string label = "Layer";

        [Tooltip("Keo thang file am thanh (nen la WAV, loop mem).")]
        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume = 0.4f;

        [Tooltip("Thoi gian fade in tu 0 -> volume (giay).")]
        public float fadeInSeconds = 2f;
    }

    private class RuntimeLayer
    {
        public AudioSource source;
        public float baseVolume;
        public float fade;
    }

    [Header("Nhac nen chinh (qua SoundManager)")]
    [Tooltip(
        "Ten clip nhac nen cho SCENE NAY - PHAI trung voi ten AudioClip " +
        "da keo vao SoundManager > Music Clips. De TRONG neu scene nay " +
        "khong dung nhac chinh (se dung nhac dang phat tu scene truoc)."
    )]
    [SerializeField] private string _musicClipName;

    [Header("Cac lop am thanh phu (phat cung luc, loop)")]
    [SerializeField] private List<AmbienceLayer> _ambienceLayers = new();

    [Header("Lien ket voi Settings (mac dinh TAT = doc lap)")]
    [Tooltip("BAT: volume cac lop phu duoc nhan them voi thanh Music trong Settings.")]
    [SerializeField] private bool _followMusicVolume = false;

    [Tooltip("BAT: cac lop phu BO QUA thanh Master (AudioListener.volume). " +
             "Luu y: khi do keo Master ve 0 cung KHONG tat duoc cac lop nay.")]
    [SerializeField] private bool _ignoreMasterVolume = false;

    private readonly List<RuntimeLayer> _runtimeLayers = new();

    private void Start()
    {
        StartMainMusic();
        StartAmbienceLayers();
    }

    private void Update()
    {
        if (_runtimeLayers.Count == 0)
            return;

        float musicFactor =
            _followMusicVolume && SoundManager.Instance != null
                ? SoundManager.Instance.GetMusicVolume()
                : 1f;

        foreach (RuntimeLayer layer in _runtimeLayers)
        {
            if (layer.source == null)
                continue;

            layer.source.volume = layer.baseVolume * layer.fade * musicFactor;
        }
    }

    // ============================================================
    // NHAC NEN CHINH
    // ============================================================

    private void StartMainMusic()
    {
        if (SoundManager.Instance == null)
        {
            Debug.LogWarning(
                "[SceneMusicPlayer] SoundManager.Instance dang null - " +
                "bo qua phat/dung nhac chinh cho scene nay."
            );
            return;
        }

        if (string.IsNullOrEmpty(_musicClipName))
        {
            // De trong = scene nay khong dung nhac chinh -> dung nhac cu
            // (VD nhac Menu) de khong phat nham vao scene nay.
            SoundManager.Instance.StopMusic();
            return;
        }

        SoundManager.Instance.PlayMusic(_musicClipName);
    }

    // ============================================================
    // CAC LOP PHU
    // ============================================================

    private void StartAmbienceLayers()
    {
        foreach (AmbienceLayer layer in _ambienceLayers)
        {
            if (layer == null || layer.clip == null)
                continue;

            AudioSource source = gameObject.AddComponent<AudioSource>();

            source.clip = layer.clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D
            source.volume = 0f;
            source.ignoreListenerVolume = _ignoreMasterVolume;
            source.Play();

            RuntimeLayer runtime = new RuntimeLayer
            {
                source = source,
                baseVolume = layer.volume,
                fade = layer.fadeInSeconds <= 0f ? 1f : 0f,
            };

            _runtimeLayers.Add(runtime);

            if (layer.fadeInSeconds > 0f)
                StartCoroutine(FadeIn(runtime, layer.fadeInSeconds));
        }
    }

    private IEnumerator FadeIn(RuntimeLayer layer, float duration)
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;

            layer.fade = Mathf.Clamp01(t / duration);

            yield return null;
        }

        layer.fade = 1f;
    }
}