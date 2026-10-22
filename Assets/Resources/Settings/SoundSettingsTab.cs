using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SoundSettingsTab : MonoBehaviour
{
    // PHAI TRUNG key + default voi SoundManager.LoadVolume().
    private const string KeyMaster = "MasterVolume";
    private const string KeyMusic = "MusicVolume";
    private const string KeySfx = "SFXVolume";

    private const float DefaultMaster = 1f;
    private const float DefaultMusic = 0.5f;
    private const float DefaultSfx = 0.5f;

    [Header("Master")]
    [SerializeField] private Slider _masterSlider;
    [SerializeField] private TextMeshProUGUI _masterValueText;

    [Header("Music")]
    [SerializeField] private Slider _musicSlider;
    [SerializeField] private TextMeshProUGUI _musicValueText;

    [Header("SFX")]
    [SerializeField] private Slider _sfxSlider;
    [SerializeField] private TextMeshProUGUI _sfxValueText;

    [Header("Slider Sound")]
    [Tooltip("Ten clip trong SoundManager > Sfx Clips (de trong = khong phat tieng).")]
    [SerializeField] private string _sliderSfxName = "Slider";

    [Tooltip("Khoang cach toi thieu (giay) giua 2 tieng - keo nhanh van khong bi on/chong tieng.")]
    [SerializeField] private float _minTickInterval = 0.06f;

    private float _lastTickTime = -999f;

    private void Awake()
    {
        SetupSlider(_masterSlider);
        SetupSlider(_musicSlider);
        SetupSlider(_sfxSlider);

        _masterSlider.onValueChanged.AddListener(OnMasterChanged);
        _musicSlider.onValueChanged.AddListener(OnMusicChanged);
        _sfxSlider.onValueChanged.AddListener(OnSfxChanged);
    }

    private void OnEnable()
    {
        Refresh();
    }

    private static void SetupSlider(Slider s)
    {
        s.minValue = 0f;
        s.maxValue = 100f;
        s.wholeNumbers = true;
    }

    // Luon doc tu PlayerPrefs (SoundManager cung luu vao day) nen
    // khong phu thuoc SoundManager.Instance co ton tai hay chua.
    private void Refresh()
    {
        SetSliderSilently(_masterSlider, _masterValueText, PlayerPrefs.GetFloat(KeyMaster, DefaultMaster));
        SetSliderSilently(_musicSlider, _musicValueText, PlayerPrefs.GetFloat(KeyMusic, DefaultMusic));
        SetSliderSilently(_sfxSlider, _sfxValueText, PlayerPrefs.GetFloat(KeySfx, DefaultSfx));
    }

    // SetValueWithoutNotify -> KHONG goi onValueChanged -> mo tab khong phat tieng.
    private static void SetSliderSilently(Slider slider, TextMeshProUGUI text, float volume01)
    {
        int v = Mathf.RoundToInt(volume01 * 100f);
        slider.SetValueWithoutNotify(v);
        text.text = v.ToString();
    }

    private void OnMasterChanged(float v)
    {
        _masterValueText.text = Mathf.RoundToInt(v).ToString();
        float vol = v / 100f;

        if (SoundManager.Instance != null) SoundManager.Instance.SetMasterVolume(vol);
        else Save(KeyMaster, vol);

        PlaySliderTick();
    }

    private void OnMusicChanged(float v)
    {
        _musicValueText.text = Mathf.RoundToInt(v).ToString();
        float vol = v / 100f;

        if (SoundManager.Instance != null) SoundManager.Instance.SetMusicVolume(vol);
        else Save(KeyMusic, vol);

        PlaySliderTick();
    }

    private void OnSfxChanged(float v)
    {
        _sfxValueText.text = Mathf.RoundToInt(v).ToString();
        float vol = v / 100f;

        if (SoundManager.Instance != null) SoundManager.Instance.SetSFXVolume(vol);
        else Save(KeySfx, vol);

        // Goi SAU khi set volume -> tieng tick nghe dung muc am luong moi.
        PlaySliderTick();
    }

    // Phat tieng "Slider" khi gia tri doi, co gioi han tan suat.
    private void PlaySliderTick()
    {
        if (string.IsNullOrEmpty(_sliderSfxName) || SoundManager.Instance == null)
            return;

        if (Time.unscaledTime - _lastTickTime < _minTickInterval)
            return;

        _lastTickTime = Time.unscaledTime;
        SoundManager.Instance.PlaySFX(_sliderSfxName);
    }

    // Du phong khi scene hien tai chua co SoundManager.
    private static void Save(string key, float volume01)
    {
        Debug.LogWarning("SoundManager.Instance = null, luu thang vao PlayerPrefs: " + key);
        PlayerPrefs.SetFloat(key, Mathf.Clamp01(volume01));
        PlayerPrefs.Save();
    }
}