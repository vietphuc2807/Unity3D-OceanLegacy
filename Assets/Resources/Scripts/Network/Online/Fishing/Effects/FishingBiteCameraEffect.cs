using System.Collections;
using UnityEngine;
using Fusion;

public class FishingBiteCameraEffect : MonoBehaviour
{
    public static FishingBiteCameraEffect Instance { get; private set; }

    private enum Phase { Idle, Biting, Reeling }
    private Phase _phase = Phase.Idle;

    [Header("FOV chung")]
    [SerializeField] private float _fovSmoothTime = 0.35f;

    [Header("Bite FOV")]
    [SerializeField] private float _biteFovNarrowAmount = 4f;

    [Header("Bite Shake (giật 1 nhịp lúc cá cắn)")]
    [SerializeField] private float _biteShakeIntensity = 0.04f;
    [SerializeField] private float _biteShakeDuration = 0.3f;
    [SerializeField] private float _biteShakeFrequency = 25f;

    [Header("Bite Zoom Punch (giật zoom vào lúc cá cắn, cảm giác tập trung)")]
    [Tooltip("FOV giảm THÊM (cộng dồn vào Bite Fov Narrow Amount) tại đỉnh punch — số càng lớn zoom vào càng sâu.")]
    [SerializeField] private float _biteZoomPunchAmount = 6f;
    [Tooltip("Thời gian (giây) camera zoom giật vào tới đỉnh punch.")]
    [SerializeField] private float _biteZoomPunchInDuration = 0.12f;
    [Tooltip("Thời gian (giây) giữ camera ở đỉnh punch trước khi nhả ra.")]
    [SerializeField] private float _biteZoomPunchHoldDuration = 0.08f;
    [Tooltip("Đường cong ease cho pha zoom vào (0→1 theo thời gian, 0→1 theo độ zoom).")]
    [SerializeField] private AnimationCurve _biteZoomPunchInCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Reel FOV (siết dần theo tiến độ kéo cá)")]
    [SerializeField] private float _reelFovAtLowProgress = 5f;
    [SerializeField] private float _reelFovAtHighProgress = 2f;

    [Header("Reel Shake (rung liên tục theo cường độ giằng co)")]
    [SerializeField] private float _reelShakeAmount = 0.035f;
    [SerializeField] private float _reelShakeSpeed = 16f;
    [SerializeField] private float _reelShakeIntensitySmoothTime = 0.15f;

    [Header("Sudden Pull Kick (giật camera có HƯỚNG theo lực kéo của cá)")]
    [Tooltip("pull01 phải vượt ngưỡng này mới kích hoạt kick.")]
    [SerializeField] private float _kickPullThreshold = 0.85f;
    [SerializeField] private float _kickCooldown = 0.5f;
    [Tooltip("Độ lớn impulse — không phải biên độ cuối, biên độ thực tế phụ thuộc spring K/damping bên dưới.")]
    [SerializeField] private float _kickImpulseStrength = 2.6f;
    [SerializeField] private float _kickSpringK = 60f;
    [SerializeField] private float _kickSpringDamping = 9f;
    [Tooltip("Trộn bao nhiêu % hướng thật (theo lực kéo) so với random — 1 = luôn theo hướng thật, 0 = luôn random.")]
    [SerializeField] [Range(0f, 1f)] private float _kickDirectionality = 0.75f;

    [Header("Reel Roll (nghiêng nhẹ camera — dutch angle theo tension)")]
    [Tooltip("Góc nghiêng tối đa (độ) khi cường độ giằng co = 1.")]
    [SerializeField] private float _reelMaxRoll = 2.5f;
    [SerializeField] private float _rollNoiseSpeed = 0.6f;
    [SerializeField] private float _rollSmoothTime = 0.3f;
    [Tooltip("Roll cộng thêm tức thời mỗi lần kick xảy ra, cùng hướng với kick — tăng cảm giác 'giật nghiêng người'.")]
    [SerializeField] private float _kickRollKick = 4f;

    [Header("Win / Lose")]
    [SerializeField] private float _winFovPunch = 3f;
    [SerializeField] private float _winPunchDuration = 0.3f;
    [SerializeField] private float _loseShakeIntensity = 0.09f;
    [SerializeField] private float _loseShakeDuration = 0.35f;

    private Camera _cam;
    private float _defaultFov;
    private float _fovVelocity;
    private float? _fovOverride;

    private Coroutine _biteShakeRoutine;
    private Vector3 _biteShakeOffset;

    private Coroutine _biteZoomPunchRoutine;

    private float _pull01;
    private float _reelProgress;
    private float _currentReelIntensity;
    private float _reelIntensityVelocity;
    private float _reelNoiseSeed;

    // Kick position (spring)
    private Vector3 _kickOffset;
    private Vector3 _kickVelocity;
    private float _lastKickTime = -999f;

    // Roll (dutch angle)
    private float _currentRoll;
    private float _rollVelocity;
    private float _kickRollOffset;      // cộng thêm tức thời lúc kick, tự SmoothDamp về 0
    private float _kickRollVelocity;

    private Coroutine _resultRoutine;

    // ============================================================
    // AWAKE / NETWORK AUTHORITY INIT
    // ============================================================
    //
    // Component này gắn theo TỪNG PLAYER PREFAB (kể cả remote player), nên
    // chỉ player có HasInputAuthority (local player) mới được giữ Instance —
    // tránh remote player ghi đè Instance toàn cục của local player.

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
            InitializeCamera();
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
        InitializeCamera();
    }

    private void InitializeCamera()
    {
        _cam = GetComponentInChildren<Camera>();
        if (_cam != null) _defaultFov = _cam.fieldOfView;
        _reelNoiseSeed = Random.Range(0f, 1000f);
    }

    // ============================================================
    // DESTROY
    // ============================================================
    //
    // Clear Instance khi object bị destroy (vd client rời game) — nếu
    // không, Instance sẽ trỏ tới object đã chết (fake null), mọi lời gọi
    // Instance?.TriggerBite() sau đó vẫn chạy vào thân hàm và
    // StartCoroutine() bên trong sẽ ném MissingReferenceException, làm
    // gãy giữa chừng caller (HandleBiteStarted trong FishingCastController).

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void LateUpdate()
    {
        if (_cam == null) return;

        // FIX: đang ở góc inspect cá (FishInspectView đã tách camera ra
        // khỏi CameraHolder và tự điều khiển transform/FOV riêng) — nhường
        // toàn quyền cho FishInspectView, không ghi đè localPosition/
        // localRotation/fieldOfView của camera nữa. Nếu không guard chỗ
        // này, LateUpdate ở đây chạy sau Update() của FishInspectView
        // trong cùng frame và sẽ kéo camera về lại offset bite/reel (0 ở
        // Phase.Idle), khiến góc inspect không hiển thị được dù
        // FishInspectView đã set đúng giá trị.
        if (NetPlayerLook.IsInspectingFish) return;

        // --- Reel shake liên tục ---
        Vector3 reelOffset = Vector3.zero;
        if (_phase == Phase.Reeling)
        {
            _currentReelIntensity = Mathf.SmoothDamp(
                _currentReelIntensity, _pull01, ref _reelIntensityVelocity,
                Mathf.Max(0.01f, _reelShakeIntensitySmoothTime));

            float nx = (Mathf.PerlinNoise(Time.time * _reelShakeSpeed + _reelNoiseSeed, 0f) - 0.5f) * 2f;
            float ny = (Mathf.PerlinNoise(0f, Time.time * _reelShakeSpeed + _reelNoiseSeed) - 0.5f) * 2f;

            reelOffset = new Vector3(nx, ny, 0f) * _reelShakeAmount * _currentReelIntensity;
        }
        else
        {
            _currentReelIntensity = Mathf.SmoothDamp(
                _currentReelIntensity, 0f, ref _reelIntensityVelocity,
                Mathf.Max(0.01f, _reelShakeIntensitySmoothTime));
        }

        // --- Kick spring (vị trí) ---
        Vector3 kickAcc = -_kickSpringK * _kickOffset - _kickSpringDamping * _kickVelocity;
        _kickVelocity += kickAcc * Time.deltaTime;
        _kickOffset += _kickVelocity * Time.deltaTime;

        _cam.transform.localPosition = _biteShakeOffset + reelOffset + _kickOffset;

        // --- Roll (dutch angle) ---
        float targetRoll = 0f;
        if (_phase == Phase.Reeling)
        {
            float rollNoise = (Mathf.PerlinNoise(Time.time * _rollNoiseSpeed, _reelNoiseSeed + 50f) - 0.5f) * 2f;
            targetRoll = rollNoise * _reelMaxRoll * _currentReelIntensity;
        }
        _currentRoll = Mathf.SmoothDamp(_currentRoll, targetRoll, ref _rollVelocity, Mathf.Max(0.01f, _rollSmoothTime));

        // Roll kick riêng — nảy lên rồi tự tắt (spring nhẹ, không cần chính xác)
        _kickRollOffset = Mathf.SmoothDamp(_kickRollOffset, 0f, ref _kickRollVelocity, 0.25f);

        _cam.transform.localRotation = Quaternion.Euler(0f, 0f, _currentRoll + _kickRollOffset);

        // --- FOV ---
        float targetFov = _defaultFov;
        switch (_phase)
        {
            case Phase.Biting:
                targetFov = _defaultFov - _biteFovNarrowAmount;
                break;
            case Phase.Reeling:
                float reelNarrow = Mathf.Lerp(_reelFovAtLowProgress, _reelFovAtHighProgress, _reelProgress);
                targetFov = _defaultFov - reelNarrow;
                break;
        }

        // _fovOverride (khi != null) đè lên targetFov thường — dùng cho cả
        // Win Punch, Lose Shake (không đổi FOV nên không dùng ở đó) và giờ
        // là Bite Zoom Punch. SmoothDamp bên dưới vẫn chạy như cũ nên khi
        // override được thả ra (null), FOV sẽ tự mượt về lại targetFov
        // của phase hiện tại — không bị giật.
        if (_fovOverride.HasValue)
            targetFov = _fovOverride.Value;

        _cam.fieldOfView = Mathf.SmoothDamp(_cam.fieldOfView, targetFov, ref _fovVelocity, Mathf.Max(0.01f, _fovSmoothTime));
    }

    // ============== Bite phase ==============

    public void TriggerBite()
    {
        _phase = Phase.Biting;

        if (_biteShakeRoutine != null) StopCoroutine(_biteShakeRoutine);
        _biteShakeRoutine = StartCoroutine(BiteShakeRoutine());

        if (_biteZoomPunchRoutine != null) StopCoroutine(_biteZoomPunchRoutine);
        _biteZoomPunchRoutine = StartCoroutine(BiteZoomPunchRoutine());
    }

    public void ResetCamera()
    {
        if (_phase == Phase.Biting)
            _phase = Phase.Idle;

        // Cá thoát/hết giờ giữa lúc đang zoom punch (vd người chơi trượt
        // minigame Hook rất nhanh) -> dừng punch, thả FOV override ngay để
        // không bị kẹt zoom sâu khi đã về Idle.
        if (_biteZoomPunchRoutine != null)
        {
            StopCoroutine(_biteZoomPunchRoutine);
            _biteZoomPunchRoutine = null;
            _fovOverride = null;
        }
    }

    private System.Collections.IEnumerator BiteShakeRoutine()
    {
        float elapsed = 0f;
        while (elapsed < _biteShakeDuration)
        {
            elapsed += Time.deltaTime;
            float dampen = 1f - elapsed / _biteShakeDuration;
            float x = Mathf.Sin(elapsed * _biteShakeFrequency * 1.3f) * _biteShakeIntensity * dampen;
            float y = Mathf.Sin(elapsed * _biteShakeFrequency) * _biteShakeIntensity * dampen * 0.6f;
            _biteShakeOffset = new Vector3(x, y, 0f);
            yield return null;
        }
        _biteShakeOffset = Vector3.zero;
        _biteShakeRoutine = null;
    }

    /// Giật zoom vào nhanh (FOV giảm sâu hơn mức Bite Fov Narrow bình
    /// thường) rồi giữ 1 nhịp ngắn, sau đó THẢ _fovOverride ra (về null) để
    /// hệ thống FOV thường ở LateUpdate tự SmoothDamp êm về lại mức
    /// _defaultFov - _biteFovNarrowAmount của Phase.Biting — tạo cảm giác
    /// "giật mình tập trung" ngay lúc cá cắn thay vì chỉ zoom từ từ đều đều.
    private IEnumerator BiteZoomPunchRoutine()
    {
        float startFov = _cam != null ? _cam.fieldOfView : _defaultFov;
        float punchTargetFov = _defaultFov - _biteFovNarrowAmount - _biteZoomPunchAmount;

        // --- Zoom vào (in) ---
        float t = 0f;
        float inDuration = Mathf.Max(0.01f, _biteZoomPunchInDuration);

        while (t < inDuration)
        {
            t += Time.deltaTime;

            float p = Mathf.Clamp01(t / inDuration);
            float eased = _biteZoomPunchInCurve.Evaluate(p);

            _fovOverride = Mathf.Lerp(startFov, punchTargetFov, eased);

            yield return null;
        }

        _fovOverride = punchTargetFov;

        // --- Giữ đỉnh punch ---
        if (_biteZoomPunchHoldDuration > 0f)
            yield return new WaitForSeconds(_biteZoomPunchHoldDuration);

        // --- Thả ra, để LateUpdate tự SmoothDamp về targetFov của phase hiện tại ---
        _fovOverride = null;
        _biteZoomPunchRoutine = null;
    }

    // ============== Reel phase ==============

    public void StartReelCamera()
    {
        _phase = Phase.Reeling;
        _pull01 = 0f;
        _reelProgress = 0f;
        _kickOffset = Vector3.zero;
        _kickVelocity = Vector3.zero;
        _currentRoll = 0f;
        _rollVelocity = 0f;
        _kickRollOffset = 0f;
        _kickRollVelocity = 0f;

        // Đảm bảo punch zoom lúc bite (nếu còn đang chạy dở khi hook thành
        // công quá nhanh) không đè lên FOV của Reel phase.
        if (_biteZoomPunchRoutine != null)
        {
            StopCoroutine(_biteZoomPunchRoutine);
            _biteZoomPunchRoutine = null;
        }

        _fovOverride = null;
    }

    /// pullDirectionScreenSpace: hướng (XY, screen/local space) mà cá đang
    /// kéo dây đi — ví dụ (0, 1) nếu cá đang kéo lên trên, (0, -1) nếu kéo
    /// xuống. Không cần chuẩn hoá, hàm tự normalize. Truyền Vector2.zero
    /// nếu không có hướng cụ thể (sẽ dùng random).
    public void UpdateReel(float pull01, float progress01, Vector2 pullDirectionScreenSpace)
    {
        _pull01 = Mathf.Clamp01(pull01);
        _reelProgress = Mathf.Clamp01(progress01);

        if (_pull01 >= _kickPullThreshold && Time.time - _lastKickTime >= _kickCooldown)
        {
            _lastKickTime = Time.time;

            Vector2 realDir = pullDirectionScreenSpace.sqrMagnitude > 0.01f
                ? pullDirectionScreenSpace.normalized
                : Vector2.zero;
            Vector2 randomDir = Random.insideUnitCircle.normalized;

            Vector2 finalDir = realDir.sqrMagnitude > 0.01f
                ? Vector2.Lerp(randomDir, realDir, _kickDirectionality).normalized
                : randomDir;

            _kickVelocity += new Vector3(finalDir.x, finalDir.y, 0f) * (_kickImpulseStrength * 10f);

            // Roll giật theo trục X của hướng kick — kéo trái thì nghiêng 1
            // chiều, kéo phải thì nghiêng chiều ngược lại, tăng cảm giác
            // "bị lôi đi" chứ không chỉ rung vị trí đơn thuần.
            _kickRollOffset += -finalDir.x * _kickRollKick;
            _kickRollVelocity = 0f;
        }
    }

    public void EndReelCamera(bool? success)
    {
        _phase = Phase.Idle;

        if (_resultRoutine != null) StopCoroutine(_resultRoutine);

        if (success == true)
            _resultRoutine = StartCoroutine(WinPunchRoutine());
        else if (success == false)
            _resultRoutine = StartCoroutine(LoseShakeRoutine());
    }

    private System.Collections.IEnumerator WinPunchRoutine()
    {
        float elapsed = 0f;
        while (elapsed < _winPunchDuration)
        {
            elapsed += Time.deltaTime;
            float curve = Mathf.Sin(Mathf.Clamp01(elapsed / _winPunchDuration) * Mathf.PI);
            _fovOverride = _defaultFov - _winFovPunch * curve;
            yield return null;
        }
        _fovOverride = null;
        _resultRoutine = null;
    }

    private System.Collections.IEnumerator LoseShakeRoutine()
    {
        float elapsed = 0f;
        while (elapsed < _loseShakeDuration)
        {
            elapsed += Time.deltaTime;
            float dampen = 1f - elapsed / _loseShakeDuration;
            float x = Mathf.Sin(elapsed * 30f) * _loseShakeIntensity * dampen;
            float y = Mathf.Sin(elapsed * 22f + 1f) * _loseShakeIntensity * dampen * 0.7f;
            _biteShakeOffset = new Vector3(x, y, 0f);
            yield return null;
        }
        _biteShakeOffset = Vector3.zero;
        _resultRoutine = null;
    }
}