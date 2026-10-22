using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class CatchFishMinigame : MonoBehaviour
{
    public static CatchFishMinigame Instance { get; private set; }

    [Header("References")]
    [Tooltip("Panel hiệu ứng hiện/ẩn UI minigame (kéo vào giống HookFishMinigame nếu có).")]
    [SerializeField] private PanelEffect _panelEffect;
    [SerializeField] private RectTransform _bar;
    [SerializeField] private RectTransform _fish;
    [SerializeField] private RectTransform _controlZone;
    [SerializeField] private Image _controlZoneImage;
    [SerializeField] private Image _progressFill;

    [Header("Tutorial Text")]
    [Tooltip("Kéo T-Message vào đây. Nội dung chữ lấy sẵn từ Text, code chỉ điều khiển hiệu ứng fade.")]
    [SerializeField] private TMP_Text _tutorialText;

    [Tooltip("Thời gian chữ mờ dần hiện lên.")]
    [SerializeField] private float _fadeInDuration = 0.25f;

    [Tooltip("Thời gian giữ chữ hiển thị rõ.")]
    [SerializeField] private float _visibleDuration = 2f;

    [Tooltip("Thời gian chữ mờ dần biến mất.")]
    [SerializeField] private float _fadeOutDuration = 0.6f;

    [Header("Reel Loop Sound (2D - tiếng cuộn cước)")]
    [Tooltip("Kéo THẲNG file fishingreel vào đây. Tự loop trong lúc kéo cá, " +
             "tắt khi kéo xong / cá thoát / bị hủy. Để trống = không phát.")]
    [SerializeField] private AudioClip _reelLoopClip;

    [Range(0f, 1f)]
    [SerializeField] private float _reelLoopVolume = 1f;

    [Tooltip("BẬT: âm lượng nhân thêm với thanh SFX trong Settings.")]
    [SerializeField] private bool _reelFollowSfxVolume = true;

    [Header("Control Zone (Dễ điều khiển)")]
    [SerializeField] private float _riseForce = 3500f;
    [SerializeField] private float _gravity = 2800f;
    [SerializeField] private float _maxSpeed = 260f;

    [Header("Fish (Mượt + khó đoán)")]
    [SerializeField] private float _slowMin = 35f;
    [SerializeField] private float _slowMax = 50f;

    [SerializeField] private float _normalMin = 60f;
    [SerializeField] private float _normalMax = 90f;

    [SerializeField] private float _dartMin = 110f;
    [SerializeField] private float _dartMax = 145f;

    [SerializeField] private float _speedSmooth = 4f;

    [Header("Fish Swim Animation (chỉ hiệu ứng hình ảnh, KHÔNG ảnh hưởng logic)")]
    [Tooltip("Góc xoay (độ) của cá khi đang bơi LÊN (_fishDir = 1). Giá trị mặc định khớp với " +
             "rotation Z = 90 đang set sẵn trên I-Fish. Nếu thấy cá bơi lộn đầu, thử đổi dấu " +
             "(vd 90 -> -90).")]
    [SerializeField] private float _facingRotationUp = 90f;
    [Tooltip("Góc xoay (độ) của cá khi đang bơi XUỐNG (_fishDir = -1).")]
    [SerializeField] private float _facingRotationDown = -90f;
    [Tooltip("Tốc độ xoay đổi hướng (số càng lớn thì cá quay đầu càng nhanh khi đổi chiều bơi).")]
    [SerializeField] private float _facingTurnSmooth = 6f;

    [Tooltip("Pivot theo trục ngang (0..1) của I-Fish, tính theo hướng sprite GỐC (trước khi bị " +
             "xoay 90°) — tức trục 'trái-phải' của ảnh cá lúc chưa xoay. Dời pivot này về phía " +
             "ĐẦU cá (vd 0.8 nếu đầu nằm bên phải sprite gốc, 0.2 nếu đầu nằm bên trái) để khi " +
             "lắc đuôi, đầu cá gần như đứng yên và chỉ có phần đuôi vẫy qua lại — giống cá bơi " +
             "thật hơn nhiều so với lắc nguyên cả thân quanh tâm (0.5). Set trên Inspector rồi " +
             "bấm Play để xem đầu cá có đứng yên đúng chỗ không, sai chiều thì đổi ngược lại " +
             "(vd 0.8 -> 0.2).")]
    [Range(0f, 1f)]
    [SerializeField] private float _headPivotX = 0.75f;

    [Tooltip("Biên độ lắc đuôi (độ) khi cá đang bơi CHẬM. Vì giờ xoay quanh đầu thay vì quanh " +
             "tâm nên có thể để số nhỏ hơn trước mà đuôi vẫn vẫy rõ.")]
    [SerializeField] private float _wiggleAmountSlow = 10f;
    [Tooltip("Biên độ lắc đuôi (độ) khi cá đang bơi NHANH (dart) — vọt mạnh, đuôi quẫy dữ hơn.")]
    [SerializeField] private float _wiggleAmountFast = 24f;
    [Tooltip("Tốc độ lắc đuôi (chu kỳ/giây) khi cá đang bơi CHẬM.")]
    [SerializeField] private float _wiggleSpeedSlow = 2.2f;
    [Tooltip("Tốc độ lắc đuôi (chu kỳ/giây) khi cá đang bơi NHANH.")]
    [SerializeField] private float _wiggleSpeedFast = 6f;

    [Tooltip("Biên độ co giãn thân cá (squash & stretch) theo nhịp lắc đuôi — để rất nhẹ, chỉ đủ " +
             "gợi cảm giác cơ bụng co lại khi quẫy, không nên để cao kẻo lấn át chuyển động lắc " +
             "đuôi chính, nhìn dễ bị rối/giả. Để 0 nếu không muốn hiệu ứng này.")]
    [SerializeField] private float _squashAmount = 0.035f;

    [Header("Progress")]
    [SerializeField] private float _gainRate = 0.32f;
    [SerializeField] private float _loseRate = 0.20f;

    [SerializeField] private float _visualProgressSmoothTime = 0.28f;

    [Header("Lose Condition")]
    [SerializeField] private float _emptyLoseDelay = 1.5f;

    [Header("Zone Colors")]
    // Đã bỏ _badColor — gộp mức "lệch nhiều nhưng vẫn trong zone" vào
    // chung _midColor để bớt số màu chuyển đổi (trước đây 4 mức: Good/Mid/
    // Bad/Outside, giờ còn 3 mức: Good/Mid/Outside), xem UpdateProgress().
    [SerializeField] private Color _goodColor = new Color(0.13f, 0.77f, 0.37f);
    [SerializeField] private Color _midColor = new Color(0.96f, 0.62f, 0.07f);
    [SerializeField] private Color _outsideColor = new Color(0.50f, 0.11f, 0.11f);

    [Header("Fish Strength Scaling")]
    [Tooltip("Hệ số nhân tốc độ bơi khi cá strength = 0 (cá nhỏ, bơi ù lì hơn).")]
    [SerializeField] private float _speedScaleAtMinStrength = 0.7f;
    [Tooltip("Hệ số nhân tốc độ bơi khi cá strength = 1 (cá lớn, bơi/dart mạnh hơn hẳn).")]
    [SerializeField] private float _speedScaleAtMaxStrength = 1.35f;

    [Header("Control Zone Difficulty by Rarity (cá càng hiếm, control zone càng nhỏ)")]
    [Tooltip("Chiều cao control zone khi cá Common (rarityDifficulty01 = 0).")]
    [SerializeField] private float _controlZoneHeightAtMinRarity = 90f;
    [Tooltip("Chiều cao control zone khi cá Legendary (rarityDifficulty01 = 1) — nhỏ hơn để khó hơn.")]
    [SerializeField] private float _controlZoneHeightAtMaxRarity = 55f;

    private float _rarityDifficulty01;

    private float _fishStrength01 = 0.5f;

    private float _zoneVelocity;
    private float _halfBar;

    private float _fishTimer;
    private int _fishDir;

    private float _currentFishSpeed;
    private float _targetFishSpeed;

    // Dùng cho hiệu ứng xoay mặt + lắc đuôi của cá (visual only).
    // _currentFacingZ CHỈ lưu phần "hướng bơi" (không bao gồm wiggle) — bắt
    // buộc phải tách riêng thế này, KHÔNG được đọc lại từ _fish.localEulerAngles.z
    // mỗi frame, vì góc hiển thị cuối cùng luôn = facing + wiggle; nếu đọc
    // ngược lại từ transform thì wiggle của frame trước sẽ lẫn vào làm target
    // xoay hướng bị nhiễu theo tần số wiggle → cá lắc giật cục ngẫu nhiên
    // thay vì xoay hướng mượt + lắc đuôi đều đặn.
    private float _currentFacingZ;
    private float _fishRotationVelocity;
    private float _wigglePhaseOffset;

    private float _progress;
    private float _emptyTimer;

    private float _visualProgress;
    private float _visualProgressVelocity;

    private bool _isActive;

    private Action<bool> _onResult;
    private Coroutine _tutorialFadeCoroutine;

    private FishingLine _targetLine;

    // AudioSource RIÊNG (2D, loop) cho tiếng cuộn cước.
    private AudioSource _reelSource;

    // Giá trị pull01 (0..1, mức cá đang kéo lại mạnh cỡ nào) tính lại mỗi
    // frame trong Update() — lưu lại để FishingCastController đọc và gửi
    // kèm trong RPC_ReportReelProgress cho các client khác, thay vì chỉ
    // gửi progress suông (nếu không gửi kèm, cần câu bên client khác vẫn
    // rung nhưng luôn ở cường độ mặc định = 1, không phản ánh đúng lúc cá
    // đang giằng co mạnh hay yếu).
    private float _lastStrugglePull01;

    public bool IsActive => _isActive;
    public float VisualProgress => _visualProgress;
    public float StrugglePull01 => _lastStrugglePull01;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (_tutorialText != null)
            _tutorialText.alpha = 0f;

        // Dời pivot của cá về phía đầu (theo trục ngang của sprite gốc) để
        // UpdateFishVisual() xoay quanh đầu thay vì quanh tâm — xem tooltip
        // của _headPivotX phía trên để biết cách chỉnh nếu bị sai chiều.
        if (_fish != null)
            _fish.pivot = new Vector2(_headPivotX, _fish.pivot.y);

        SetupReelAudio();
    }

    private void OnDisable()
    {
        // Phòng trường hợp object bị tắt giữa lúc đang kéo cá.
        StopReelSound();
    }

    private void Update()
    {
        if (!_isActive)
            return;

        UpdateReelVolume();

        UpdateControlZone();
        UpdateFish();
        UpdateFishVisual();
        UpdateProgress();

        _visualProgress = Mathf.SmoothDamp(
            _visualProgress, _progress, ref _visualProgressVelocity,
            Mathf.Max(0.01f, _visualProgressSmoothTime)
        );

        _targetLine?.SetRodBend(_visualProgress);
        _targetLine?.SetReelProgress(_visualProgress);

        float pull01 = Mathf.Clamp01(-_visualProgressVelocity / Mathf.Max(0.0001f, _loseRate));
        _lastStrugglePull01 = pull01;
        _targetLine?.SetRodStrugglePull(pull01);

        float fishOffsetY = _fish.anchoredPosition.y - _controlZone.anchoredPosition.y;
        Vector2 pullDir = new Vector2(0f, Mathf.Sign(fishOffsetY));

        FishingBiteCameraEffect.Instance?.UpdateReel(pull01, _visualProgress, pullDir);
    }

    // ============================================================
    // REEL LOOP SOUND
    // ============================================================

    private void SetupReelAudio()
    {
        if (_reelLoopClip == null)
            return;

        _reelSource = gameObject.AddComponent<AudioSource>();
        _reelSource.clip = _reelLoopClip;
        _reelSource.playOnAwake = false;
        _reelSource.loop = true;      // hết clip thì tự lặp lại
        _reelSource.spatialBlend = 0f; // 2D
    }

    private float GetReelVolume()
    {
        float volume = _reelLoopVolume;

        if (_reelFollowSfxVolume && SoundManager.Instance != null)
            volume *= SoundManager.Instance.GetSFXVolume();

        return volume;
    }

    private void StartReelSound()
    {
        if (_reelSource == null)
            return;

        _reelSource.volume = GetReelVolume();
        _reelSource.Stop();
        _reelSource.Play();
    }

    private void StopReelSound()
    {
        if (_reelSource != null && _reelSource.isPlaying)
            _reelSource.Stop();
    }

    // Cập nhật mỗi frame để kéo thanh SFX trong Settings lúc đang kéo cá
    // thì tiếng loop cũng đổi theo.
    private void UpdateReelVolume()
    {
        if (_reelSource != null && _reelSource.isPlaying)
            _reelSource.volume = GetReelVolume();
    }

    public void StartMinigame(Action<bool> onResult, FishingLine targetLine, float fishStrength01 = 0.5f, float rarityDifficulty01 = 0f)
    {
        _onResult = onResult;
        _targetLine = targetLine;
        _fishStrength01 = Mathf.Clamp01(fishStrength01);
        _rarityDifficulty01 = Mathf.Clamp01(rarityDifficulty01);

        _panelEffect?.Show();
        PlayTutorialFade();

        _isActive = true;

        // Bắt đầu kéo cá: loop tiếng cuộn cước (chạy cùng lúc với clicksuccess
        // vì minigame này được gọi ngay khi click trúng ở HookFishMinigame).
        StartReelSound();

        _halfBar = _bar.rect.height * 0.5f;
        _progress = 0f;
        _emptyTimer = 0f;
        _visualProgress = 0f;
        _visualProgressVelocity = 0f;
        _lastStrugglePull01 = 0f;

        if (_progressFill != null)
            _progressFill.fillAmount = 0f;

        _controlZone.anchoredPosition = Vector2.zero;
        _fish.anchoredPosition = Vector2.zero;
        _zoneVelocity = 0f;

        // FIX RARITY: chiều cao control zone giờ phụ thuộc rarity thay vì
        // luôn cố định 90f — cá càng hiếm, zone càng nhỏ, càng khó giữ.
        float controlZoneHeight = Mathf.Lerp(
            _controlZoneHeightAtMinRarity,
            _controlZoneHeightAtMaxRarity,
            _rarityDifficulty01
        );
        _controlZone.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, controlZoneHeight);

        // FIX: I-ControlZone (ảnh hiển thị, con của Rect-ControlZone) có
        // height RIÊNG, cố định sẵn trong Editor, KHÔNG tự co giãn theo cha
        // vì anchors của nó là center-only (min=max=0.5,0.5) chứ không
        // stretch theo cha. Nếu chỉ resize Rect-ControlZone (hitbox logic)
        // mà không resize luôn ảnh hiển thị này theo cùng 1 giá trị, ảnh sẽ
        // giữ nguyên size cũ (68) bất kể hitbox đã to/nhỏ cỡ nào → nhìn như
        // control zone bị lệch/tràn ra ngoài giới hạn khi rarity thay đổi.
        // Luôn resize CẢ HAI cùng lúc, cùng 1 giá trị controlZoneHeight, để
        // ảnh hiển thị khớp chính xác với hitbox logic.
        if (_controlZoneImage != null)
            _controlZoneImage.rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical, controlZoneHeight
            );

        _fishDir = UnityEngine.Random.value > 0.5f ? 1 : -1;
        _fishTimer = 0.5f;
        _currentFishSpeed = 45f;
        _targetFishSpeed = 45f;

        _wigglePhaseOffset = UnityEngine.Random.Range(0f, 100f);
        _fishRotationVelocity = 0f;
        _currentFacingZ = _fishDir >= 0 ? _facingRotationUp : _facingRotationDown;
        _fish.localRotation = Quaternion.Euler(0f, 0f, _currentFacingZ);
        _fish.localScale = Vector3.one;

        if (_controlZoneImage != null)
            _controlZoneImage.color = _midColor;

        _targetLine?.StartRodStruggle();
        FishingBiteCameraEffect.Instance?.StartReelCamera();
        _targetLine?.SetReeling(true);
        _targetLine?.SetReelProgress(0f);
    }

    public void CancelMinigame()
    {
        if (!_isActive)
            return;

        _isActive = false;
        _onResult = null;

        // Hủy giữa chừng (đổi item...): tắt tiếng cuộn cước.
        StopReelSound();

        StopTutorialFade();

        _panelEffect?.Hide();

        _targetLine?.ResetRodBend();

        FishingBiteCameraEffect.Instance?.EndReelCamera(null);

        _targetLine?.SetReeling(false);
        _targetLine?.SetReelProgress(0f);
        _targetLine = null;

        _visualProgress = 0f;
        _visualProgressVelocity = 0f;
        _lastStrugglePull01 = 0f;

        _fish.localRotation = Quaternion.identity;
        _fish.localScale = Vector3.one;
    }

    private void UpdateControlZone()
    {
        bool hold = Input.GetMouseButton(0);

        if (hold)
            _zoneVelocity = Mathf.MoveTowards(_zoneVelocity, _maxSpeed, _riseForce * Time.deltaTime);
        else
            _zoneVelocity = Mathf.MoveTowards(_zoneVelocity, -_maxSpeed, _gravity * Time.deltaTime);

        Vector2 pos = _controlZone.anchoredPosition;
        pos.y += _zoneVelocity * Time.deltaTime;

        float halfZone = _controlZone.rect.height * 0.5f;

        pos.y = Mathf.Clamp(pos.y, -_halfBar + halfZone, _halfBar - halfZone);

        _controlZone.anchoredPosition = pos;
    }

    private void UpdateFish()
    {
        _fishTimer -= Time.deltaTime;

        if (_fishTimer <= 0f)
        {
            _fishDir = UnityEngine.Random.value > 0.5f ? 1 : -1;

            float roll = UnityEngine.Random.value;

            float speedScale = Mathf.Lerp(_speedScaleAtMinStrength, _speedScaleAtMaxStrength, _fishStrength01);

            if (roll < 0.55f)
            {
                _targetFishSpeed = UnityEngine.Random.Range(_slowMin, _slowMax) * speedScale;
                _fishTimer = UnityEngine.Random.Range(0.9f, 1.5f);
            }
            else if (roll < 0.90f)
            {
                _targetFishSpeed = UnityEngine.Random.Range(_normalMin, _normalMax) * speedScale;
                _fishTimer = UnityEngine.Random.Range(0.5f, 1.0f);
            }
            else
            {
                _targetFishSpeed = UnityEngine.Random.Range(_dartMin, _dartMax) * speedScale;
                _fishTimer = UnityEngine.Random.Range(0.25f, 0.45f);
            }
        }

        _currentFishSpeed = Mathf.Lerp(
            _currentFishSpeed,
            _targetFishSpeed,
            _speedSmooth * Time.deltaTime
        );

        Vector2 pos = _fish.anchoredPosition;

        pos.y += _fishDir * _currentFishSpeed * Time.deltaTime;

        pos.y += Mathf.Sin(Time.time * 8f) * 0.15f;

        float halfFish = _fish.rect.height * 0.5f;

        if (pos.y >= _halfBar - halfFish)
        {
            pos.y = _halfBar - halfFish;
            _fishDir = -1;
            _targetFishSpeed = UnityEngine.Random.Range(70f, 120f);
        }
        else if (pos.y <= -_halfBar + halfFish)
        {
            pos.y = -_halfBar + halfFish;
            _fishDir = 1;
            _targetFishSpeed = UnityEngine.Random.Range(70f, 120f);
        }

        _fish.anchoredPosition = pos;
    }

    /// Hiệu ứng bơi thuần hình ảnh, tách riêng khỏi UpdateFish() (không đụng
    /// tới anchoredPosition/logic tính điểm): xoay mặt cá theo hướng đang
    /// bơi lên/xuống, cộng thêm nhịp lắc đuôi (wiggle) và co giãn thân nhẹ
    /// (squash & stretch) — cả 2 đều "hăng" hơn khi cá đang bơi nhanh/dart,
    /// êm hơn khi bơi chậm, để cảm giác khớp với tốc độ thật của cá.
    private void UpdateFishVisual()
    {
        float speedT = Mathf.InverseLerp(_slowMin, _dartMax, _currentFishSpeed);

        // --- Xoay mặt theo hướng bơi (mượt, không giật cục khi đổi chiều) ---
        // QUAN TRỌNG: SmoothDampAngle đuổi theo _currentFacingZ (field lưu
        // riêng), KHÔNG đọc lại _fish.localEulerAngles.z — vì giá trị đó đã
        // bị cộng thêm wiggle ở frame trước, đọc ngược lại sẽ làm nhiễu
        // target xoay hướng theo đúng tần số wiggle, gây lắc giật ngẫu nhiên.
        float targetFacing = _fishDir >= 0 ? _facingRotationUp : _facingRotationDown;
        _currentFacingZ = Mathf.SmoothDampAngle(
            _currentFacingZ, targetFacing, ref _fishRotationVelocity,
            1f / Mathf.Max(0.01f, _facingTurnSmooth)
        );

        // --- Lắc đuôi: cộng thêm dao động hình sin vào góc xoay (quanh pivot
        // gần đầu cá nên chỉ phần đuôi swing, đầu gần như đứng yên) ---
        float wiggleAmount = Mathf.Lerp(_wiggleAmountSlow, _wiggleAmountFast, speedT);
        float wiggleSpeed = Mathf.Lerp(_wiggleSpeedSlow, _wiggleSpeedFast, speedT);
        float wigglePhase = (Time.time + _wigglePhaseOffset) * wiggleSpeed * Mathf.PI * 2f;
        float wiggle = Mathf.Sin(wigglePhase) * wiggleAmount;

        _fish.localRotation = Quaternion.Euler(0f, 0f, _currentFacingZ + wiggle);

        // --- Squash & stretch nhẹ theo cùng nhịp lắc, mô phỏng lực đẩy nước ---
        float squashPhase = Mathf.Sin(wigglePhase);
        float scaleX = 1f + squashPhase * _squashAmount;
        float scaleY = 1f - squashPhase * _squashAmount;
        _fish.localScale = new Vector3(scaleX, scaleY, 1f);
    }

    private void UpdateProgress()
    {
        float fishY = _fish.anchoredPosition.y;
        float zoneY = _controlZone.anchoredPosition.y;

        float halfZone = _controlZone.rect.height * 0.5f;

        float distance = Mathf.Abs(fishY - zoneY);

        bool inside = distance <= halfZone;

        if (inside)
        {
            float centered = 1f - (distance / halfZone);

            float multiplier = Mathf.Lerp(0.2f, 1f, centered);

            // Level cần câu (TeamRodUpgrade, dùng chung cho cả team) càng
            // cao thì thanh tiến độ tăng càng nhanh.
            _progress += _gainRate * multiplier * GetRodGainMultiplier() * Time.deltaTime;

            // Chỉ còn 2 mức màu trong lúc ở trong zone: Good (căn giữa tốt)
            // và Mid (còn trong zone nhưng lệch) — đã bỏ mức Bad riêng để
            // giảm số lần đổi màu, đỡ rối mắt người chơi.
            if (_controlZoneImage != null)
                _controlZoneImage.color = centered > 0.5f ? _goodColor : _midColor;
        }
        else
        {
            _progress -= _loseRate * Time.deltaTime;

            if (_controlZoneImage != null)
                _controlZoneImage.color = _outsideColor;
        }

        _progress = Mathf.Clamp01(_progress);

        if (_progressFill != null)
            _progressFill.fillAmount = _progress;

        if (_progress >= 1f)
        {
            Finish(true);
            return;
        }

        if (_progress <= 0f)
        {
            _emptyTimer += Time.deltaTime;

            if (_emptyTimer >= _emptyLoseDelay)
            {
                Finish(false);
            }
        }
        else
        {
            _emptyTimer = 0f;
        }
    }

    /// <summary>
    /// Hệ số tăng tốc thanh tiến độ theo Level cần câu hiện tại của team
    /// (TeamRodUpgrade). Level càng cao (đã nâng cấp) thì kéo cá càng nhanh.
    /// Trả về 1 (không ảnh hưởng) nếu TeamRodUpgrade chưa sẵn sàng.
    /// </summary>
    private float GetRodGainMultiplier()
    {
        return TeamRodUpgrade.Instance != null
            ? TeamRodUpgrade.Instance.GetProgressGainMultiplier()
            : 1f;
    }

    private void Finish(bool success)
    {
        _isActive = false;

        // Kéo cá xong (thắng hoặc cá thoát): tắt tiếng cuộn cước.
        StopReelSound();

        StopTutorialFade();

        _panelEffect?.Hide();

        _targetLine?.ResetRodBend();

        FishingBiteCameraEffect.Instance?.EndReelCamera(success);

        // FIX: KHÔNG gọi ForceSnapReelDistance()/SetReeling(false)/
        // SetReelProgress(0f) trực tiếp lên _targetLine ở đây nữa.
        // callback?.Invoke(success) bên dưới sẽ trigger
        // FishingCastController.RPC_ResolveBite(success), nơi này sẽ TỰ
        // gọi lại đúng 3 hàm đó với tiến độ ĐÃ ĐƯỢC ÉP CHÍNH XÁC (1 khi
        // thắng / 0 khi thua) — xem RPC_ResolveBite.
        //
        // TRƯỚC ĐÂY: gọi ForceSnapReelDistance() ở đây rồi SetReelProgress(0f)
        // ngay sau đó → khi RPC_ResolveBite gọi ForceSnapReelDistance() LẦN
        // NỮA, nó chụp lại _reelProgress lúc này ĐÃ BỊ GHI ĐÈ VỀ 0 (do dòng
        // SetReelProgress(0f) vừa rồi) → _pendingSnapProgress = 0 → phao bị
        // snap về farDistance (xa) thay vì closeDistance (gần), KHÔNG LIÊN
        // QUAN gì tới giá trị Min Reel Distance đang set — đây chính là lý
        // do dù để Min Reel Distance = 0 vẫn thấy dây câu không kéo gần lại
        // được lúc thắng.
        _targetLine = null;

        _visualProgress = 0f;
        _visualProgressVelocity = 0f;
        _lastStrugglePull01 = 0f;

        _fish.localRotation = Quaternion.identity;
        _fish.localScale = Vector3.one;

        var callback = _onResult;
        _onResult = null;
        callback?.Invoke(success);
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

        if (_tutorialText != null)
            _tutorialText.alpha = 0f;
    }

    private IEnumerator TutorialFadeRoutine()
    {
        // Fade In: 0 → 1
        float t = 0f;
        float fadeInDuration = Mathf.Max(0.01f, _fadeInDuration);

        while (t < fadeInDuration)
        {
            t += Time.deltaTime;

            _tutorialText.alpha =
                Mathf.Clamp01(t / fadeInDuration);

            yield return null;
        }

        _tutorialText.alpha = 1f;

        // Giữ chữ
        if (_visibleDuration > 0f)
            yield return new WaitForSeconds(_visibleDuration);

        // Fade Out: 1 → 0
        t = 0f;
        float fadeOutDuration = Mathf.Max(0.01f, _fadeOutDuration);

        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;

            _tutorialText.alpha =
                1f - Mathf.Clamp01(t / fadeOutDuration);

            yield return null;
        }

        _tutorialText.alpha = 0f;
        _tutorialFadeCoroutine = null;
    }
}