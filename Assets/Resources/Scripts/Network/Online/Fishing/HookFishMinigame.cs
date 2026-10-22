using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class HookFishMinigame : MonoBehaviour
{
    public static HookFishMinigame Instance { get; private set; }

    [Header("References")]
    [SerializeField] private PanelEffect _panelEffect;
    [SerializeField] private RectTransform _bar;
    [SerializeField] private RectTransform _needle;
    [SerializeField] private RectTransform _targetZone;

    [Header("Tutorial Text")]
    [Tooltip("Kéo T-Message vào đây. Nội dung chữ lấy sẵn từ text hiện có trên component này (không bị code ghi đè), chỉ animate hiệu ứng hiện/tắt (fade).")]
    [SerializeField] private TMP_Text _tutorialText;
    [Tooltip("Thời gian (giây) để chữ mờ dần hiện lên khi bắt đầu minigame.")]
    [SerializeField] private float _fadeInDuration = 0.25f;
    [Tooltip("Thời gian (giây) giữ chữ hiển thị rõ trước khi bắt đầu mờ dần tắt.")]
    [SerializeField] private float _visibleDuration = 2f;
    [Tooltip("Thời gian (giây) để chữ mờ dần biến mất sau khi hết Visible Duration.")]
    [SerializeField] private float _fadeOutDuration = 0.6f;

    [Header("Movement")]
    [Tooltip("Tốc độ di chuyển của thanh đỏ được random trong khoảng này mỗi lần bắt đầu minigame, để độ khó thay đổi qua từng lần cá cắn câu.")]
    [SerializeField] private float _moveSpeedMin = 200f;
    [SerializeField] private float _moveSpeedMax = 320f;

    [Header("Target")]
    [Tooltip("Bề rộng khối xanh (vùng trúng) được random trong khoảng này.")]
    [SerializeField] private float _targetWidthMin = 30f;
    [SerializeField] private float _targetWidthMax = 55f;

    [Header("Timing")]
    [Tooltip("Nếu người chơi không click trong khoảng thời gian này thì tự tính là trượt (không bắt buộc, đặt 0 để tắt).")]
    [SerializeField] private float _timeoutSeconds = 4f;

    [Header("Target Difficulty by Rarity (cá càng hiếm, khối xanh càng nhỏ)")]
    [Tooltip("Hệ số nhân bề rộng khối xanh khi cá Common (rarityDifficulty01 = 0). Để 1 = giữ nguyên Target Width Min/Max phía trên.")]
    [SerializeField] private float _targetWidthMultiplierAtMinRarity = 1f;
    [Tooltip("Hệ số nhân bề rộng khối xanh khi cá Legendary (rarityDifficulty01 = 1) — nhỏ hơn 1 để khó hơn.")]
    [SerializeField] private float _targetWidthMultiplierAtMaxRarity = 0.55f;

    [Header("Sound (2D - kéo THẲNG file vào, mỗi tiếng có volume riêng)")]
    [Tooltip("Phát khi minigame hiện lên (fisheat). Sẽ bị dừng ngay khi click hoặc hết giờ.")]
    [SerializeField] private AudioClip _startClip;
    [Range(0f, 1f)]
    [SerializeField] private float _startVolume = 1f;

    [Tooltip("Phát khi click trúng (clicksuccess).")]
    [SerializeField] private AudioClip _successClip;
    [Range(0f, 1f)]
    [SerializeField] private float _successVolume = 1f;

    [Tooltip("Phát khi click trượt hoặc hết giờ không chơi (slack).")]
    [SerializeField] private AudioClip _missClip;
    [Range(0f, 1f)]
    [SerializeField] private float _missVolume = 1f;

    [Tooltip("BẬT: âm lượng nhân thêm với thanh SFX trong Settings.")]
    [SerializeField] private bool _followSfxVolume = true;

    private AudioSource _sfxSource;   // success / miss (PlayOneShot)
    private AudioSource _startSource; // riêng cho fisheat để còn dừng được

    private float _rarityDifficulty01;

    private bool _isActive;
    private int _direction = 1;
    private float _moveSpeed;

    private float _leftLimit;
    private float _rightLimit;

    private Action<bool> _onResult;
    private Coroutine _timeoutCoroutine;
    private Coroutine _tutorialFadeCoroutine;

    // Cờ public để FishingCastController biết đang có minigame → không nhận input.
    public bool IsActive => _isActive;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.loop = false;
        _sfxSource.spatialBlend = 0f; // 2D

        _startSource = gameObject.AddComponent<AudioSource>();
        _startSource.playOnAwake = false;
        _startSource.loop = false;
        _startSource.spatialBlend = 0f; // 2D

        if (_tutorialText != null)
            _tutorialText.alpha = 0f;
    }

    private void OnDisable()
    {
        // Phòng trường hợp object bị tắt giữa lúc minigame đang chạy.
        StopStartSound();
    }

    private void Update()
    {
        if (!_isActive)
            return;

        MoveNeedle();

        if (Input.GetMouseButtonDown(0))
        {
            CheckHit();
        }
    }

    /// Bắt đầu minigame giật cần. onResult trả về true nếu người chơi click
    /// đúng lúc thanh đỏ nằm trong khối xanh, false nếu trượt hoặc hết giờ.
    public void StartMinigame(Action<bool> onResult, float rarityDifficulty01 = 0f)
    {
        _onResult = onResult;
        _rarityDifficulty01 = Mathf.Clamp01(rarityDifficulty01);

        _panelEffect?.Show();
        PlayTutorialFade();

        // Minigame vừa hiện lên: tiếng cá rỉa mồi.
        PlayStartSound();

        CalculateLimits();
        ResetNeedle();
        RandomizeTargetZone();

        _isActive = true;

        if (_timeoutCoroutine != null)
            StopCoroutine(_timeoutCoroutine);

        if (_timeoutSeconds > 0f)
            _timeoutCoroutine = StartCoroutine(TimeoutRoutine());
    }

    /// Hủy minigame đang diễn ra mà KHÔNG báo kết quả (dùng khi người chơi
    /// đổi item / rời cần câu giữa lúc minigame đang mở) — tránh callback
    /// bắn về một FishingLine không còn liên quan nữa.
    public void CancelMinigame()
    {
        if (!_isActive)
            return;

        _isActive = false;
        _onResult = null;

        StopStartSound();

        if (_timeoutCoroutine != null)
        {
            StopCoroutine(_timeoutCoroutine);
            _timeoutCoroutine = null;
        }

        StopTutorialFade();
        _panelEffect?.Hide();
    }

    // ---------- Sound ----------

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null || _sfxSource == null)
            return;

        float v = volume;

        if (_followSfxVolume && SoundManager.Instance != null)
            v *= SoundManager.Instance.GetSFXVolume();

        _sfxSource.PlayOneShot(clip, v);
    }

    private void PlayStartSound()
    {
        if (_startClip == null || _startSource == null)
            return;

        float v = _startVolume;

        if (_followSfxVolume && SoundManager.Instance != null)
            v *= SoundManager.Instance.GetSFXVolume();

        _startSource.clip = _startClip;
        _startSource.volume = v;
        _startSource.Stop();
        _startSource.Play();
    }

    private void StopStartSound()
    {
        if (_startSource != null && _startSource.isPlaying)
            _startSource.Stop();
    }

    // ---------- Setup ----------

    private void CalculateLimits()
    {
        float halfBar    = _bar.rect.width    * 0.5f;
        float halfNeedle = _needle.rect.width * 0.5f;

        _leftLimit  = -halfBar + halfNeedle;
        _rightLimit =  halfBar - halfNeedle;

        _moveSpeed = UnityEngine.Random.Range(_moveSpeedMin, _moveSpeedMax);
    }

    private void ResetNeedle()
    {
        _needle.anchoredPosition = new Vector2(_leftLimit, 0f);
        _direction = 1;
    }

    private void RandomizeTargetZone()
    {
        float rarityWidthMultiplier = Mathf.Lerp(
            _targetWidthMultiplierAtMinRarity,
            _targetWidthMultiplierAtMaxRarity,
            _rarityDifficulty01
        );

        float targetWidth = UnityEngine.Random.Range(_targetWidthMin, _targetWidthMax) * rarityWidthMultiplier;
        float halfTarget  = targetWidth * 0.5f;

        float targetX = UnityEngine.Random.Range(
            _leftLimit  + halfTarget,
            _rightLimit - halfTarget
        );

        _targetZone.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
        _targetZone.anchoredPosition = new Vector2(targetX, 0f);
    }

    // ---------- Tutorial fade effect ----------

    private void PlayTutorialFade()
    {
        if (_tutorialText == null)
            return;

        StopTutorialFade();
        _tutorialFadeCoroutine = StartCoroutine(TutorialFadeRoutine());
    }

    private void StopTutorialFade()
    {
        if (_tutorialFadeCoroutine != null)
        {
            StopCoroutine(_tutorialFadeCoroutine);
            _tutorialFadeCoroutine = null;
        }

        // Đảm bảo lần Hide/Cancel giữa chừng không để lại chữ hiện dở dang —
        // lần Show() kế tiếp sẽ luôn bắt đầu lại từ đầu (mờ hẳn).
        if (_tutorialText != null)
            _tutorialText.alpha = 0f;
    }

    private IEnumerator TutorialFadeRoutine()
    {
        // Fade in: 0 → 1
        float t = 0f;
        float fadeInDuration = Mathf.Max(0.01f, _fadeInDuration);

        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            _tutorialText.alpha = Mathf.Clamp01(t / fadeInDuration);
            yield return null;
        }

        _tutorialText.alpha = 1f;

        // Giữ chữ hiển thị rõ trong khoảng _visibleDuration
        if (_visibleDuration > 0f)
            yield return new WaitForSeconds(_visibleDuration);

        // Fade out: 1 → 0
        t = 0f;
        float fadeOutDuration = Mathf.Max(0.01f, _fadeOutDuration);

        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            _tutorialText.alpha = 1f - Mathf.Clamp01(t / fadeOutDuration);
            yield return null;
        }

        _tutorialText.alpha = 0f;
        _tutorialFadeCoroutine = null;
    }

    // ---------- Update loop ----------

    private void MoveNeedle()
    {
        Vector2 pos = _needle.anchoredPosition;

        pos.x += _direction * _moveSpeed * Time.deltaTime;

        if (pos.x >= _rightLimit)
        {
            pos.x      = _rightLimit;
            _direction = -1;
        }
        else if (pos.x <= _leftLimit)
        {
            pos.x      = _leftLimit;
            _direction = 1;
        }

        _needle.anchoredPosition = pos;
    }

    private void CheckHit()
    {
        // Click rồi thì fisheat phải tắt ngay, nhường chỗ cho slack / clicksuccess.
        StopStartSound();

        bool success = IsNeedleInTarget();

        // Trúng: clicksuccess. Trượt: slack.
        // (Tiếng fishingreel loop do CatchFishMinigame tự phát khi nó bắt đầu.)
        if (success)
            PlaySfx(_successClip, _successVolume);
        else
            PlaySfx(_missClip, _missVolume);

        FinishMinigame(success);
    }

    private IEnumerator TimeoutRoutine()
    {
        yield return new WaitForSeconds(_timeoutSeconds);

        if (_isActive)
        {
            // Không chơi, hết giờ: tắt fisheat rồi phát slack.
            StopStartSound();
            PlaySfx(_missClip, _missVolume);
            FinishMinigame(false);
        }
    }

    private void FinishMinigame(bool success)
    {
        _isActive = false;

        if (_timeoutCoroutine != null)
        {
            StopCoroutine(_timeoutCoroutine);
            _timeoutCoroutine = null;
        }

        StopTutorialFade();
        _panelEffect?.Hide();

        // Lấy callback ra biến local rồi clear field trước khi Invoke —
        // phòng trường hợp callback lại gọi ngược vào StartMinigame/CancelMinigame.
        var callback = _onResult;
        _onResult = null;
        callback?.Invoke(success);
    }

    private bool IsNeedleInTarget()
    {
        float needleX      = _needle.anchoredPosition.x;
        float targetCenter = _targetZone.anchoredPosition.x;
        float targetHalf   = _targetZone.rect.width * 0.5f;

        return needleX >= targetCenter - targetHalf &&
               needleX <= targetCenter + targetHalf;
    }
}