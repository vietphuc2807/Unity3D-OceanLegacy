using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(200)]
public class FishingLine : MonoBehaviour
{
    private enum LineState
    {
        Idle,
        Casting,
        InWater,
        ReelingIn
    }

    [Header("References")]
    [SerializeField] private Transform _rodTip;
    [SerializeField] private Transform _floatModel;
    [SerializeField] private Transform _hookModel;
    [SerializeField] private Transform _hookAttachPoint;
    [SerializeField] private Transform _lineAttachPoint;
    [SerializeField] private LineRenderer _lineRenderer;

    [Header("Rod Bend Reference")]
    [SerializeField] private FishingRodBendEffect _rodBendEffect;

    [Header("Line Settings")]
    [SerializeField] private float _hangLength = 0.5f;
    [SerializeField] private float _floatRatio = 0.75f;

    [Header("Float Settings")]
    [SerializeField] private float _floatYOffset = 0f;
    [SerializeField] private Vector3 _floatRotationOffset = Vector3.zero;

    [Header("Hook Settings")]
    [SerializeField] private Vector3 _hookPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 _hookRotationOffset = new Vector3(90f, 0f, 0f);

    [Header("Caught Fish Display")]
    [SerializeField] private List<DebugFishToggle> _debugFishToggles =
        new List<DebugFishToggle>();

    [System.Serializable]
    private class DebugFishToggle
    {
        public FishData Fish;
        public bool AttachThis;
        public bool MinSize;
        public bool MaxSize;
    }

    private FishData _lastToggledDebugFish;
    private float _lastDebugSizeMultiplier = -1f;

    private FishData _pendingDebugFish;
    private float _pendingDebugSizeMultiplier = 1f;
    private bool _hasPendingDebugChange;

    [Header("Caught Fish Pull On Line")]
    [SerializeField] private float _caughtFishPullMultiplier = 1f;
    [SerializeField] private float _caughtFishPullSmoothTime = 0.07f;

    [Header("Sway Settings")]
    [SerializeField] private float _floatSmoothTime = 0.15f;
    [SerializeField] private float _hookSmoothTime = 0.2f;
    [SerializeField] private float _hookRotationSmoothSpeed = 8f;

    [Header("Rod Sway")]
    [SerializeField] private float _positionSwayAmount = 0.08f;
    [SerializeField] private float _swaySmoothTime = 0.12f;
    [SerializeField] private float _maxSway = 0.15f;

    [Header("Cast Settings")]
    [SerializeField] private float _castDuration = 0.8f;
    [SerializeField] private float _castArcHeight = 2.5f;
    [SerializeField] private LayerMask _waterLayerMask;
    [SerializeField] private LayerMask _groundLayerMask;
    [SerializeField] private float _fallbackWaterY = 0f;
    [SerializeField] private float _extraUnderwaterDepth = 0f;

    [Header("Cast Duration By Distance")]
    [Tooltip("BAT: thoi gian bay cua phao phu thuoc khoang cach quang " +
             "(nhe = bay ngan, manh = bay lau). TAT: luon dung _castDuration co dinh.")]
    [SerializeField] private bool _scaleCastDurationByDistance = true;

    [Tooltip("Thoi gian bay (giay) khi quang o khoang cach NHO NHAT.")]
    [SerializeField] private float _castDurationMin = 0.5f;

    [Tooltip("Thoi gian bay (giay) khi quang o khoang cach LON NHAT.")]
    [SerializeField] private float _castDurationMax = 1.1f;

    [Tooltip("Khoang cach ung voi Duration Min (nen = Min Cast Distance cua FishingCastController).")]
    [SerializeField] private float _durationMinDistance = 5f;

    [Tooltip("Khoang cach ung voi Duration Max (nen = Max Cast Distance cua FishingCastController).")]
    [SerializeField] private float _durationMaxDistance = 25f;

    [Header("Splash Sound (3D)")]
    [Tooltip("Keo THANG file splash vao day. Phat 3D tai diem phao cham " +
             "nuoc, moi nguoi choi dung gan do deu nghe.")]
    [SerializeField] private AudioClip _splashClip;

    [Range(0f, 1f)]
    [SerializeField] private float _splashVolume = 1f;

    [Tooltip("BAT: am luong nhan them voi thanh SFX trong Settings.")]
    [SerializeField] private bool _splashFollowSfxVolume = true;

    [Tooltip("Trong khoang nay tieng to toi da.")]
    [SerializeField] private float _splashMinDistance = 2f;

    [Tooltip("Ngoai khoang nay khong nghe thay gi.")]
    [SerializeField] private float _splashMaxDistance = 25f;

    [Header("Water Current")]
    [SerializeField] private float _currentSwaySpeed = 0.8f;
    [SerializeField] private float _currentSwayAmount = 0.12f;
    [SerializeField] private float _currentSwaySmooth = 0.3f;

    [Header("Landing Bob")]
    [SerializeField] private float _landingImpactSpeed = 0.12f;
    [SerializeField] private float _landingSpringK = 90f;
    [SerializeField] private float _landingSpringDamping = 7f;
    [SerializeField] private float _landingSettleThreshold = 0.002f;

    [Header("Bite Settings")]
    [SerializeField] private float _biteDelayMin = 2f;
    [SerializeField] private float _biteDelayMax = 6f;
    [SerializeField] private float _biteBobAmount = 0.02f;
    [SerializeField] private float _biteBobSpeed = 1.4f;

    [Header("Reel Pull")]
    [SerializeField] private float _minReelDistance = 1.2f;
    [SerializeField] private float _reelDistanceSmoothTime = 0.18f;

    [Header("Reel Distance Scaling")]
    [SerializeField] private float _minFarDistance = 3f;
    [SerializeField] private float _farPullExtraDistance = 3f;

    [Header("Diagonal Drift + Wobble")]
    [SerializeField] private float _escapeAngleAtLowProgress = 32f;
    [SerializeField] private float _escapeAngleAtHighProgress = 8f;
    [SerializeField] private float _escapeAngleSmoothTime = 0.5f;
    [SerializeField] private float _escapeWobbleAmount = 10f;
    [SerializeField] private float _escapeWobbleSpeed = 1.6f;
    [SerializeField] private bool _fadeWobbleWithProgress = true;

    [Header("Pull-Under Depth")]
    [SerializeField] private float _maxPullUnderDepth = 0.25f;
    [SerializeField] private float _pullUnderSmoothTime = 0.22f;
    [SerializeField] private float _decreasingPullMultiplier = 1.6f;

    private Vector3 _currentSwayOffset;
    private Vector3 _currentSwayVelocity;
    private float _swayTimeOffset;

    private Quaternion _lastRodRotation;

    private Vector3 _swayOffset;
    private Vector3 _swayVelocity;

    private Vector3 _floatVelocity;
    private Vector3 _hookVelocity;

    private Vector3 _floatPosLastFrameForVelocity;
    private Vector3 _hookPosLastFrameForVelocity;

    private Vector3 _currentFloatPos;
    private Vector3 _currentHookPos;
    private Vector3 _currentLineAttachPos;

    private Quaternion _currentHookRotation;

    private bool _hasFrozenFishHookRotation;
    private Quaternion _frozenFishHookRotation;

    private bool _needSnap;
    private bool _isReady;

    private LineState _state = LineState.Idle;

    private Transform _floatOriginalParent;
    private Transform _hookOriginalParent;

    private bool _isDetached;

    private Vector3 _floatOriginalLocalPosition;
    private Quaternion _floatOriginalLocalRotation;
    private Vector3 _floatOriginalLocalScale;

    private Vector3 _hookOriginalLocalPosition;
    private Quaternion _hookOriginalLocalRotation;
    private Vector3 _hookOriginalLocalScale;

    private Vector3 _hookBasePos;

    private bool _rigidTracking;
    private float _inWaterLineLength;

    private bool _isLandingBob;
    private float _landingSurfaceY;
    private float _landingOffsetY;
    private float _landingVelY;

    private Coroutine _biteWaitCoroutine;
    private bool _isBiting;
    private float _biteBobTimeOffset;

    // AudioSource 3D cho tieng splash - nam tren 1 GameObject RIENG (khong
    // phai con cua can cau) de dat dung diem phao cham nuoc.
    private AudioSource _splashSource;

    // Mặc định true để không phá vỡ hành vi cũ nếu chỗ nào đó chưa gọi
    // SetBaited() (vd test nhanh trong Editor). FishingCastController sẽ
    // gọi SetBaited(false) trước khi StartCast() nếu người chơi không có
    // mồi trong Bait Bag lúc quăng cần — khi đó phao vẫn quăng và nằm dưới
    // nước bình thường, chỉ là BiteWaitRoutine() sẽ không được khởi động
    // nên sẽ không bao giờ có cá cắn câu cho tới lần quăng tiếp theo.
    private bool _hasBait = true;

    public bool HasBait => _hasBait;

    public void SetBaited(bool hasBait)
    {
        _hasBait = hasBait;
    }

    // ============================================================
    // CAUGHT FISH
    // ============================================================

    private GameObject _caughtFishInstance;

    private CaughtFishStruggleEffect _caughtFishStruggleRef;

    // NEW:
    // Giữ reference tới script deform bone của cá.
    private FishRigStruggleEffect _caughtFishRigStruggleRef;

    private Vector3 _fishPullOffset;
    private Vector3 _fishPullVelocity;

    // ============================================================
    // REEL
    // ============================================================

    private float _reelProgress;
    private float _reelDistVelocity;

    private float _pullUnderDepth;
    private float _pullUnderVelocity;
    private float _prevReelProgress;

    private bool _isReeling;

    private bool _pendingForceSnapDistance;
    private float _pendingSnapProgress;

    private Vector3 _baseCastDirXZ = Vector3.forward;

    private float _currentEscapeAngle;
    private float _escapeAngleVelocity;

    private float _wobbleTimeOffset;

    // ============================================================
    // FISH STRENGTH
    // ============================================================

    private float _fishStrength01 = 0.5f;

    private float _maxPullUnderDepthBase;
    private float _decreasingPullMultiplierBase;

    private float _escapeAngleAtLowProgressBase;
    private float _escapeAngleAtHighProgressBase;
    private float _escapeWobbleAmountBase;

    // ============================================================
    // PUBLIC
    // ============================================================

    public Transform HookModel => _hookModel;

    public Transform HookAttachPoint =>
        _hookAttachPoint != null
            ? _hookAttachPoint
            : _hookModel;

    public Transform LineAttachPoint =>
        _lineAttachPoint != null
            ? _lineAttachPoint
            : _hookModel;

    public bool IsInWater =>
        _state == LineState.InWater ||
        _state == LineState.ReelingIn;

    public bool IsCasting =>
        _state == LineState.Casting;

    public bool IsBiting =>
        _isBiting;

    public Vector3 CurrentFloatPos =>
        _currentFloatPos;

    // FIX: cho phép các script bên ngoài (vd HookBaitVisual) biết hiện có
    // đang gắn 1 con cá bắt được trên lưỡi câu hay không, để tránh spawn
    // model mồi đè lên con cá đó.
    public bool HasCaughtFish => _caughtFishInstance != null;

    public event System.Action<Vector3> OnFloatLanded;
    public event System.Action OnBiteStarted;
    public event System.Action OnCastMissedWater;

    // FIX: bắn ra đúng lúc con cá đang gắn trên lưỡi câu bị gỡ bỏ THẬT SỰ
    // (không bắn khi AttachCaughtFish() tự thay cá cũ bằng cá mới, để
    // tránh nháy mồi 1 frame giữa lúc đổi cá). HookBaitVisual lắng nghe
    // event này để tự hiện lại đúng mồi đang chọn ngay khi hook trống.
    public event System.Action OnCaughtFishCleared;

    // ============================================================
    // AWAKE
    // ============================================================

    private void Awake()
    {
        SetupLineRenderer();

        if (_rodTip != null)
            _lastRodRotation = _rodTip.rotation;

        if (_floatModel != null)
        {
            _floatOriginalParent = _floatModel.parent;

            _floatOriginalLocalPosition =
                _floatModel.localPosition;

            _floatOriginalLocalRotation =
                _floatModel.localRotation;

            _floatOriginalLocalScale =
                _floatModel.localScale;
        }

        if (_hookModel != null)
        {
            _hookOriginalParent = _hookModel.parent;

            _hookOriginalLocalPosition =
                _hookModel.localPosition;

            _hookOriginalLocalRotation =
                _hookModel.localRotation;

            _hookOriginalLocalScale =
                _hookModel.localScale;
        }

        _swayTimeOffset =
            Random.Range(0f, 100f);

        _wobbleTimeOffset =
            Random.Range(0f, 100f);

        _maxPullUnderDepthBase =
            _maxPullUnderDepth;

        _decreasingPullMultiplierBase =
            _decreasingPullMultiplier;

        _escapeAngleAtLowProgressBase =
            _escapeAngleAtLowProgress;

        _escapeAngleAtHighProgressBase =
            _escapeAngleAtHighProgress;

        _escapeWobbleAmountBase =
            _escapeWobbleAmount;

        SetupSplashAudio();
    }

    // ============================================================
    // DESTROY
    // ============================================================

    private void OnDestroy()
    {
        // GameObject chua AudioSource splash la object rieng (khong phai
        // con cua can cau) nen phai tu huy.
        if (_splashSource != null)
            Destroy(_splashSource.gameObject);

        if (_isDetached)
        {
            if (_floatModel != null)
                Destroy(_floatModel.gameObject);

            if (_hookModel != null)
                Destroy(_hookModel.gameObject);
        }
    }

    // ============================================================
    // ENABLE
    // ============================================================

    private void OnEnable()
    {
        ReattachModels();

        _needSnap = false;
        _isReady = false;

        _state = LineState.Idle;

        _swayOffset = Vector3.zero;
        _swayVelocity = Vector3.zero;

        _currentSwayOffset = Vector3.zero;
        _currentSwayVelocity = Vector3.zero;

        _rigidTracking = false;

        _isLandingBob = false;
        _landingOffsetY = 0f;
        _landingVelY = 0f;

        _isBiting = false;
        _biteWaitCoroutine = null;

        _reelProgress = 0f;
        _reelDistVelocity = 0f;

        _pullUnderDepth = 0f;
        _pullUnderVelocity = 0f;

        _prevReelProgress = 0f;

        _isReeling = false;

        _pendingForceSnapDistance = false;
        _pendingSnapProgress = 0f;

        _currentEscapeAngle = 0f;
        _escapeAngleVelocity = 0f;

        _rodBendEffect?.ClearBendTarget();

        if (_lineRenderer != null)
            _lineRenderer.positionCount = 3;

        StopAllCoroutines();

        // ========================================================
        // GIỮ CÁ ĐANG GẮN TRÊN LƯỠI
        // ========================================================

        if (_caughtFishInstance != null)
        {
            SetVisualsActive(true);

            SnapToTarget();

            // Nếu cá đã tồn tại từ trước, đảm bảo rig vẫn hoạt động.
            if (_caughtFishRigStruggleRef != null)
            {
                _caughtFishRigStruggleRef.enabled = true;

                _caughtFishRigStruggleRef.SetFishStrength(
                    _fishStrength01
                );

                _caughtFishRigStruggleRef.SetReeling(
                    _isReeling
                );
            }
        }
        else
        {
            SetVisualsActive(false);

            StartCoroutine(
                DelayedSnap()
            );
        }
    }

    private IEnumerator DelayedSnap()
    {
        yield return null;
        yield return null;

        _needSnap = true;
    }

    // ============================================================
    // VALIDATE
    // ============================================================

    private void OnValidate()
    {
        if (!Application.isPlaying ||
            _debugFishToggles == null)
            return;

        DebugFishToggle activeEntry = null;

        foreach (var entry in _debugFishToggles)
        {
            if (entry != null &&
                entry.Fish != null &&
                entry.AttachThis)
            {
                activeEntry = entry;
                break;
            }
        }

        if (activeEntry == null)
        {
            if (_lastToggledDebugFish != null)
            {
                _pendingDebugFish = null;
                _pendingDebugSizeMultiplier = 1f;
                _hasPendingDebugChange = true;
            }

            return;
        }

        foreach (var entry in _debugFishToggles)
        {
            if (entry != null &&
                entry != activeEntry)
            {
                entry.AttachThis = false;
            }
        }

        float sizeMultiplier =
            activeEntry.MinSize
                ? 1f
                : activeEntry.Fish.MaxSizeMultiplier;

        if (activeEntry.MaxSize)
            sizeMultiplier =
                activeEntry.Fish.MaxSizeMultiplier;

        bool fishChanged =
            activeEntry.Fish !=
            _lastToggledDebugFish;

        bool sizeChanged =
            !Mathf.Approximately(
                sizeMultiplier,
                _lastDebugSizeMultiplier
            );

        if (fishChanged ||
            sizeChanged)
        {
            _pendingDebugFish =
                activeEntry.Fish;

            _pendingDebugSizeMultiplier =
                sizeMultiplier;

            _hasPendingDebugChange =
                true;
        }
    }

    // ============================================================
    // LATE UPDATE
    // ============================================================

    private void LateUpdate()
    {
        // ========================================================
        // DEBUG FISH
        // ========================================================

        if (_hasPendingDebugChange)
        {
            _hasPendingDebugChange = false;

            _lastToggledDebugFish =
                _pendingDebugFish;

            if (_pendingDebugFish != null)
            {
                _lastDebugSizeMultiplier =
                    _pendingDebugSizeMultiplier;

                AttachCaughtFish(
                    _pendingDebugFish,
                    _pendingDebugSizeMultiplier
                );
            }
            else
            {
                _lastDebugSizeMultiplier = -1f;

                ClearCaughtFish();
            }
        }

        // ========================================================
        // INVALID STATE
        // ========================================================

        if (HasInvalidState())
        {
            Debug.LogWarning(
                $"[FishingLine] Phát hiện vị trí không hợp lệ (NaN/Infinity), tự reset về Idle. float={_currentFloatPos} hook={_currentHookPos}",
                this
            );

            ForceResetToIdle();

            return;
        }

        UpdateRodSway();

        switch (_state)
        {
            case LineState.Idle:

                UpdateHangingLine();

                break;

            case LineState.InWater:

                UpdateInWaterLine();

                break;

            case LineState.ReelingIn:

                // ReelInRoutine tự xử lý vị trí.
                break;
        }

        UpdateLineStart();
    }

    // ============================================================
    // INVALID
    // ============================================================

    private bool HasInvalidState()
    {
        return IsInvalid(_currentFloatPos) ||
               IsInvalid(_currentHookPos) ||
               IsInvalid(_currentLineAttachPos);
    }

    private static bool IsInvalid(Vector3 v)
    {
        return float.IsNaN(v.x) ||
               float.IsNaN(v.y) ||
               float.IsNaN(v.z) ||
               float.IsInfinity(v.x) ||
               float.IsInfinity(v.y) ||
               float.IsInfinity(v.z);
    }

    // ============================================================
    // RESET
    // ============================================================

    public void ResetToIdle()
    {
        StopAllCoroutines();

        if (_state == LineState.InWater)
        {
            StartCoroutine(
                ReelInRoutine()
            );

            return;
        }

        ForceResetToIdle();
    }

    public void ForceImmediateIdle()
    {
        StopAllCoroutines();

        ForceResetToIdle();
    }

    // ============================================================
    // REEL IN
    // ============================================================

    private IEnumerator ReelInRoutine()
    {
        _isBiting = false;
        _biteWaitCoroutine = null;
        _isLandingBob = false;

        _state =
            LineState.ReelingIn;

        float smoothTime = 0.22f;
        float maxDuration = 0.6f;

        float t = 0f;

        while (t < maxDuration)
        {
            t += Time.deltaTime;

            Vector3 tipTarget =
                _rodTip != null
                    ? _rodTip.position
                    : transform.position;

            Vector3 hookEndTarget =
                tipTarget +
                Vector3.down *
                _hangLength +
                _hookPositionOffset;

            Vector3 floatEndTarget =
                Vector3.Lerp(
                    tipTarget,
                    tipTarget +
                    Vector3.down *
                    _hangLength,
                    _floatRatio
                ) +
                Vector3.up *
                _floatYOffset;

            _currentFloatPos =
                Vector3.SmoothDamp(
                    _currentFloatPos,
                    floatEndTarget,
                    ref _floatVelocity,
                    smoothTime
                );

            _currentHookPos =
                Vector3.SmoothDamp(
                    _currentHookPos,
                    hookEndTarget,
                    ref _hookVelocity,
                    smoothTime
                );

            _currentLineAttachPos =
                _currentHookPos;

            _rodBendEffect?.SetBendTargetPosition(
                _currentHookPos
            );

            if (_floatModel != null)
                _floatModel.position =
                    _currentFloatPos;

            if (_hookModel != null)
                _hookModel.position =
                    _currentHookPos;

            DrawLine();

            bool settled =
                (_currentFloatPos -
                 floatEndTarget).sqrMagnitude < 0.0004f &&

                (_currentHookPos -
                 hookEndTarget).sqrMagnitude < 0.0004f &&

                _floatVelocity.sqrMagnitude < 0.01f &&

                _hookVelocity.sqrMagnitude < 0.01f;

            if (settled)
                break;

            yield return null;
        }

        ForceResetToIdle();
    }

    // ============================================================
    // FORCE RESET
    // ============================================================

    private void ForceResetToIdle()
    {
        StopAllCoroutines();

        SetVisualsActive(false);

        ReattachModels();

        _needSnap = true;

        _state =
            LineState.Idle;

        _isLandingBob = false;
        _landingOffsetY = 0f;
        _landingVelY = 0f;

        _isBiting = false;
        _biteWaitCoroutine = null;

        _reelProgress = 0f;
        _reelDistVelocity = 0f;

        _pullUnderDepth = 0f;
        _pullUnderVelocity = 0f;

        _prevReelProgress = 0f;

        _isReeling = false;

        _pendingForceSnapDistance = false;
        _pendingSnapProgress = 0f;

        _currentEscapeAngle = 0f;
        _escapeAngleVelocity = 0f;

        // Cá vẫn còn nếu chỉ đang reset line.
        if (_caughtFishRigStruggleRef != null)
        {
            _caughtFishRigStruggleRef.SetReeling(false);
        }

        _rodBendEffect?.ClearBendTarget();

        _currentFloatPos = Vector3.zero;
        _currentHookPos = Vector3.zero;
        _currentLineAttachPos = Vector3.zero;

        _floatVelocity = Vector3.zero;
        _hookVelocity = Vector3.zero;

        if (_lineRenderer != null)
            _lineRenderer.positionCount = 3;
    }

    // ============================================================
    // GENERAL API
    // ============================================================

    public void SetRigidTracking(bool enabled)
    {
        _rigidTracking = enabled;
    }

    public void StopBiting()
    {
        _isBiting = false;

        if (_biteWaitCoroutine != null)
        {
            StopCoroutine(
                _biteWaitCoroutine
            );

            _biteWaitCoroutine = null;
        }
    }

    public void SetReelProgress(float progress01)
    {
        _reelProgress =
            Mathf.Clamp01(progress01);
    }

    // ============================================================
    // REELING
    // ============================================================

    public void SetReeling(bool active)
    {
        _isReeling = active;

        _prevReelProgress =
            _reelProgress;

        // NEW:
        // Truyền trạng thái đang reel cho cá.
        if (_caughtFishRigStruggleRef != null)
        {
            _caughtFishRigStruggleRef.SetReeling(
                active
            );
        }
    }

    public void ForceSnapReelDistance()
    {
        _pendingForceSnapDistance = true;

        _pendingSnapProgress =
            _reelProgress;
    }

    // ============================================================
    // ROD BEND
    // ============================================================

    public void SetRodBend(float value01)
    {
        _rodBendEffect?.SetBend(
            value01
        );
    }

    // ============================================================
    // FISH STRENGTH
    // ============================================================

    public void SetFishStrength(float strength01)
    {
        _fishStrength01 =
            Mathf.Clamp01(
                strength01
            );

        _maxPullUnderDepth =
            _maxPullUnderDepthBase *
            Mathf.Lerp(
                0.5f,
                1.6f,
                _fishStrength01
            );

        _decreasingPullMultiplier =
            _decreasingPullMultiplierBase *
            Mathf.Lerp(
                0.5f,
                1.8f,
                _fishStrength01
            );

        _escapeAngleAtLowProgress =
            _escapeAngleAtLowProgressBase *
            Mathf.Lerp(
                0.6f,
                1.4f,
                _fishStrength01
            );

        _escapeAngleAtHighProgress =
            _escapeAngleAtHighProgressBase *
            Mathf.Lerp(
                0.6f,
                1.4f,
                _fishStrength01
            );

        _escapeWobbleAmount =
            _escapeWobbleAmountBase *
            Mathf.Lerp(
                0.5f,
                1.6f,
                _fishStrength01
            );

        _rodBendEffect?.SetStrengthScale(
            _fishStrength01
        );

        // NEW:
        // Truyền strength sang FishRigStruggleEffect.
        if (_caughtFishRigStruggleRef != null)
        {
            _caughtFishRigStruggleRef.SetFishStrength(
                _fishStrength01
            );
        }
    }

    public void ResetRodBend()
    {
        _rodBendEffect?.ResetBend();

        SetFishStrength(
            0.5f
        );
    }

    // ============================================================
    // ROD STRUGGLE
    // ============================================================

    public void TriggerRodHookSnap()
    {
        _rodBendEffect?.TriggerHookSnap();
    }

    public void StartRodStruggle()
    {
        _rodBendEffect?.StartStruggle();
    }

    public void StopRodStruggle()
    {
        _rodBendEffect?.StopStruggle();
    }

    public void SetRodStrugglePull(float pull01)
    {
        _rodBendEffect?.SetStrugglePull(
            pull01
        );
    }

    // ============================================================
    // ATTACH CAUGHT FISH
    // ============================================================

    public void AttachCaughtFish(
        FishData fishData,
        float sizeMultiplier)
    {
        // FIX: dùng bản nội bộ, KHÔNG bắn OnCaughtFishCleared ở đây — đây
        // là lúc thay cá cũ bằng cá mới (debug toggle chẳng hạn), hook vẫn
        // sẽ có cá ngay sau khi hàm này chạy xong nên không cần (và không
        // nên) báo cho HookBaitVisual là "hook đã trống".
        ClearCaughtFishInternal(notify: false);

        if (fishData == null ||
            fishData.WorldPrefab == null)
        {
            return;
        }

        Transform anchor =
            HookAttachPoint;

        if (anchor == null)
            return;

        // ========================================================
        // CREATE PIVOT
        // ========================================================

        GameObject pivotObj =
            new GameObject(
                "CaughtFishPivot"
            );

        pivotObj.transform.SetParent(
            transform,
            false
        );

        pivotObj.transform.SetPositionAndRotation(
            anchor.position,
            anchor.rotation
        );

        Debug.Log(
            $"[Fish] pivot parent lossyScale = {transform.lossyScale}",
            this
        );

        // ========================================================
        // FOLLOW ANCHOR
        // ========================================================

        var followAnchor =
            pivotObj.AddComponent<
                CaughtFishFollowAnchor
            >();

        followAnchor.Anchor =
            anchor;

        _caughtFishInstance =
            pivotObj;

        // ========================================================
        // INSTANTIATE FISH
        // ========================================================

        GameObject fishObj =
            Instantiate(
                fishData.WorldPrefab,
                pivotObj.transform
            );

        fishObj.transform.localPosition =
            Vector3.zero;

        fishObj.transform.localRotation =
            Quaternion.Euler(
                fishData.HookRotationOffset
            );

        fishObj.transform.localScale =
            fishData.BaseScale *
            sizeMultiplier;

        // ========================================================
        // MOUTH ALIGN
        // ========================================================

        FishMouthPoint mouth =
            fishObj.GetComponentInChildren<
                FishMouthPoint
            >();

        if (mouth != null)
        {
            Vector3 worldDelta =
                pivotObj.transform.position -
                mouth.transform.position;

            fishObj.transform.position +=
                worldDelta;
        }
        else
        {
            Debug.LogWarning(
                $"[FishingLine] Prefab '{fishData.WorldPrefab.name}' chưa có FishMouthPoint.",
                this
            );
        }

        // ========================================================
        // HOOK POSITION OFFSET
        // ========================================================

        if (fishData.HookPositionOffset != Vector3.zero)
        {
            fishObj.transform.position +=
                anchor.TransformDirection(
                    fishData.HookPositionOffset
                );
        }

        // ========================================================
        // DISABLE FISH COMPONENTS
        // ========================================================
        //
        // QUAN TRỌNG:
        //
        // KHÔNG disable FishRigStruggleEffect.
        //
        // Trước đây:
        //
        // foreach MonoBehaviour
        //     behaviour.enabled = false;
        //
        // đã tắt luôn FishRigStruggleEffect.
        //
        // Bây giờ chỉ disable các behaviour khác.
        // ========================================================

        foreach (
            var behaviour in
            fishObj.GetComponentsInChildren<
                MonoBehaviour
            >(true)
        )
        {
            if (behaviour == null)
                continue;

            if (behaviour is FishRigStruggleEffect)
                continue;

            behaviour.enabled = false;
        }

        // ========================================================
        // DISABLE ANIMATOR
        // ========================================================

        foreach (
            var fishAnimator in
            fishObj.GetComponentsInChildren<
                Animator
            >(true)
        )
        {
            if (fishAnimator != null)
                fishAnimator.enabled = false;
        }

        // ========================================================
        // CAUGHT FISH WHOLE-BODY EFFECT
        // ========================================================

        _caughtFishStruggleRef =
            pivotObj.AddComponent<
                CaughtFishStruggleEffect
            >();

        float size01 =
            Mathf.InverseLerp(
                1f,
                Mathf.Max(
                    1.0001f,
                    fishData.MaxSizeMultiplier
                ),
                sizeMultiplier
            );

        _caughtFishStruggleRef.SetSizeFactor01(
            size01
        );

        // ========================================================
        // FISH RIG STRUGGLE
        // ========================================================

        _caughtFishRigStruggleRef =
            fishObj.GetComponentInChildren<
                FishRigStruggleEffect
            >(true);

        if (_caughtFishRigStruggleRef != null)
        {
            // Đảm bảo component được bật.
            _caughtFishRigStruggleRef.enabled =
                true;

            // Truyền size.
            //
            // Nếu FishRigStruggleEffect của bạn chưa có
            // SetSizeFactor01(), bỏ block này.
            //
            // Hiện tại để tránh compile error nếu script
            // của bạn chưa có API này, ta chỉ truyền strength.
            _caughtFishRigStruggleRef.SetFishStrength(
                _fishStrength01
            );

            _caughtFishRigStruggleRef.SetReeling(
                _isReeling
            );

            _caughtFishRigStruggleRef.StartStruggle();

            Debug.Log(
                $"[FishingLine] FishRigStruggleEffect activated on '{fishObj.name}'.",
                fishObj
            );
        }
        else
        {
            Debug.LogWarning(
                $"[FishingLine] Không tìm thấy FishRigStruggleEffect trong prefab '{fishObj.name}'.",
                fishObj
            );
        }

        // ========================================================
        // RESET PULL
        // ========================================================

        _fishPullOffset =
            Vector3.zero;

        _fishPullVelocity =
            Vector3.zero;

        _hasFrozenFishHookRotation =
            false;
    }

    // ============================================================
    // CLEAR CAUGHT FISH
    // ============================================================

    // FIX: giữ nguyên chữ ký public cũ để không phá vỡ chỗ gọi khác (vd
    // FishingCastController, debug toggle trong LateUpdate) — chỉ đổi thân
    // hàm thành gọi bản nội bộ có bắn event, vì đây luôn là trường hợp
    // "hook thực sự trống lại" (không phải đang thay cá cũ bằng cá mới).
    public void ClearCaughtFish()
    {
        ClearCaughtFishInternal(notify: true);
    }

    // FIX: bản nội bộ dùng chung cho cả ClearCaughtFish() (public, luôn
    // báo event) và AttachCaughtFish() (thay cá cũ bằng cá mới, không báo
    // event để tránh HookBaitVisual nháy mồi lên 1 frame rồi lại tắt ngay
    // khi cá mới được gắn vào).
    private void ClearCaughtFishInternal(bool notify)
    {
        // Stop rig trước khi destroy.
        if (_caughtFishRigStruggleRef != null)
        {
            _caughtFishRigStruggleRef.StopStruggle();

            _caughtFishRigStruggleRef =
                null;
        }

        if (_caughtFishInstance != null)
        {
            Destroy(
                _caughtFishInstance
            );

            _caughtFishInstance =
                null;
        }

        _caughtFishStruggleRef =
            null;

        _fishPullOffset =
            Vector3.zero;

        _fishPullVelocity =
            Vector3.zero;

        _hasFrozenFishHookRotation =
            false;

        if (notify)
            OnCaughtFishCleared?.Invoke();
    }

    // ============================================================
    // SPLASH SOUND (3D)
    // ============================================================

    private void SetupSplashAudio()
    {
        if (_splashClip == null)
            return;

        // Object RIENG (khong la con cua can cau) de co the dat dung diem
        // phao cham nuoc va khong bi di chuyen theo nguoi choi.
        GameObject go = new GameObject("FishingSplashAudio");

        _splashSource = go.AddComponent<AudioSource>();
        _splashSource.playOnAwake = false;
        _splashSource.loop = false;

        // 3D: cang xa cang nho, vuot _splashMaxDistance thi im han.
        _splashSource.spatialBlend = 1f;
        _splashSource.rolloffMode = AudioRolloffMode.Linear;
        _splashSource.minDistance = _splashMinDistance;
        _splashSource.maxDistance = _splashMaxDistance;
        _splashSource.dopplerLevel = 0f;
    }

    private void PlaySplashAt(Vector3 worldPos)
    {
        if (_splashSource == null || _splashClip == null)
            return;

        float volume = _splashVolume;

        if (_splashFollowSfxVolume && SoundManager.Instance != null)
            volume *= SoundManager.Instance.GetSFXVolume();

        _splashSource.transform.position = worldPos;
        _splashSource.PlayOneShot(_splashClip, volume);
    }

    // ============================================================
    // CAST DURATION
    // ============================================================

    private float ComputeCastDuration(float distance)
    {
        if (!_scaleCastDurationByDistance)
            return _castDuration;

        float t =
            Mathf.InverseLerp(
                _durationMinDistance,
                _durationMaxDistance,
                distance
            );

        return Mathf.Lerp(
            _castDurationMin,
            _castDurationMax,
            t
        );
    }

    // ============================================================
    // START CAST
    // ============================================================

    public void StartCast(
        Vector3 horizontalDirection,
        float distance)
    {
        if (_rodTip == null)
            return;

        StopAllCoroutines();

        ReattachModels();

        ClearCaughtFish();

        Vector3 tip =
            _rodTip.position;

        Vector3 flatDir =
            horizontalDirection.sqrMagnitude >
            0.0001f

                ? new Vector3(
                    horizontalDirection.x,
                    0f,
                    horizontalDirection.z
                ).normalized

                : transform.forward;

        Vector3 landingXZ =
            tip +
            flatDir *
            distance;

        bool isWater =
            FindWaterHeight(
                landingXZ,
                out float waterY
            );

        Vector3 landingPoint =
            new Vector3(
                landingXZ.x,
                waterY +
                _floatYOffset,
                landingXZ.z
            );

        StartCoroutine(
            CastRoutine(
                landingPoint,
                isWater,
                ComputeCastDuration(distance)
            )
        );
    }

    // ============================================================
    // WATER HEIGHT
    // ============================================================

    private bool FindWaterHeight(
        Vector3 xzPosition,
        out float waterY)
    {
        Vector3 rayStart =
            new Vector3(
                xzPosition.x,
                xzPosition.y + 100f,
                xzPosition.z
            );

        bool hitWater =
            Physics.Raycast(
                rayStart,
                Vector3.down,
                out RaycastHit waterHit,
                500f,
                _waterLayerMask
            );

        bool hitGround =
            Physics.Raycast(
                rayStart,
                Vector3.down,
                out RaycastHit groundHit,
                500f,
                _groundLayerMask
            );

        if (
            hitWater &&
            (
                !hitGround ||
                waterHit.distance <=
                groundHit.distance
            )
        )
        {
            waterY =
                waterHit.point.y;

            return true;
        }

        waterY =
            _fallbackWaterY;

        return false;
    }

    // ============================================================
    // CAST ROUTINE
    // ============================================================

    private IEnumerator CastRoutine(
        Vector3 landingPoint,
        bool isWater,
        float duration)
    {
        // duration = thoi gian bay cua phao (phu thuoc khoang cach quang
        // neu _scaleCastDurationByDistance bat).
        duration =
            Mathf.Max(
                0.01f,
                duration
            );

        _state =
            LineState.Casting;

        if (_lineRenderer != null)
            _lineRenderer.positionCount = 3;

        SetVisualsActive(true);

        Vector3 startPos =
            _rodTip != null
                ? _rodTip.position
                : transform.position;

        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;

            float p =
                Mathf.Clamp01(
                    t /
                    duration
                );

            Vector3 flat =
                Vector3.Lerp(
                    startPos,
                    landingPoint,
                    p
                );

            float arc =
                _castArcHeight *
                4f *
                p *
                (1f - p);

            Vector3 pos =
                flat +
                Vector3.up *
                arc;

            _currentFloatPos =
                pos;

            _currentHookPos =
                pos +
                Vector3.down *
                0.15f;

            _currentLineAttachPos =
                _currentHookPos;

            _rodBendEffect?.SetBendTargetPosition(
                _currentHookPos
            );

            ApplyPositionsDirect();

            DrawLine();

            yield return null;
        }

        if (!isWater)
        {
            ForceResetToIdle();

            OnCastMissedWater?.Invoke();

            yield break;
        }

        _currentFloatPos =
            landingPoint;

        _landingSurfaceY =
            landingPoint.y;

        _isLandingBob = true;
        _landingOffsetY = 0f;

        _landingVelY =
            -_landingImpactSpeed;

        float idleSegmentLength =
            _hangLength *
            (1f - _floatRatio);

        float underwaterDepth =
            idleSegmentLength +
            _extraUnderwaterDepth;

        _hookBasePos =
            new Vector3(
                landingPoint.x,
                landingPoint.y -
                underwaterDepth,
                landingPoint.z
            );

        _currentHookPos =
            _hookBasePos;

        _currentSwayOffset =
            Vector3.zero;

        _reelProgress = 0f;
        _reelDistVelocity = 0f;

        _pullUnderDepth = 0f;
        _pullUnderVelocity = 0f;

        _prevReelProgress = 0f;

        _isReeling = false;

        _pendingForceSnapDistance = false;
        _pendingSnapProgress = 0f;

        _currentEscapeAngle = 0f;
        _escapeAngleVelocity = 0f;

        _wobbleTimeOffset =
            Random.Range(
                0f,
                100f
            );

        _rodBendEffect?.SetBendTargetPosition(
            _currentHookPos
        );

        ApplyPositionsDirect();

        DrawLine();

        DetachModels();

        // Phao cham nuoc: phat tieng splash 3D tai dung diem phao roi.
        // CastRoutine chay tren MOI may (RPC_PerformCast -> All) nen moi
        // nguoi tu phat tai may minh, to nho theo khoang cach 3D.
        PlaySplashAt(landingPoint);

        OnFloatLanded?.Invoke(
            landingPoint
        );

        Vector3 tipXZ =
            new Vector3(
                _rodTip.position.x,
                _currentFloatPos.y,
                _rodTip.position.z
            );

        Vector3 floatXZ =
            new Vector3(
                _currentFloatPos.x,
                _currentFloatPos.y,
                _currentFloatPos.z
            );

        _inWaterLineLength =
            Vector3.Distance(
                tipXZ,
                floatXZ
            );

        Vector3 baseDirXZ =
            floatXZ -
            tipXZ;

        _baseCastDirXZ =
            baseDirXZ.sqrMagnitude >
            0.0001f

                ? baseDirXZ.normalized

                : (
                    new Vector3(
                        _rodTip.forward.x,
                        0f,
                        _rodTip.forward.z
                    ).sqrMagnitude >
                    0.0001f

                    ? new Vector3(
                        _rodTip.forward.x,
                        0f,
                        _rodTip.forward.z
                    ).normalized

                    : Vector3.forward
                );

        _state =
            LineState.InWater;

        _isBiting = false;

        // Không có mồi -> phao vẫn nằm dưới nước bình thường nhưng không
        // bao giờ có cá cắn câu (không khởi động BiteWaitRoutine).
        if (_hasBait)
        {
            _biteWaitCoroutine =
                StartCoroutine(
                    BiteWaitRoutine()
                );
        }
    }

    // ============================================================
    // LANDING BOB
    // ============================================================

    private float StepLandingBob()
    {
        float dt =
            Time.deltaTime;

        float acc =
            -_landingSpringK *
            _landingOffsetY -

            _landingSpringDamping *
            _landingVelY;

        _landingVelY +=
            acc *
            dt;

        _landingOffsetY +=
            _landingVelY *
            dt;

        if (
            Mathf.Abs(
                _landingOffsetY
            ) <
            _landingSettleThreshold &&

            Mathf.Abs(
                _landingVelY
            ) <
            _landingSettleThreshold
        )
        {
            _landingOffsetY = 0f;
            _landingVelY = 0f;

            _isLandingBob = false;

            return 0f;
        }

        return _landingOffsetY;
    }

    // ============================================================
    // BITE
    // ============================================================

    private IEnumerator BiteWaitRoutine()
    {
        float delay =
            Random.Range(
                _biteDelayMin,
                _biteDelayMax
            );

        yield return new WaitForSeconds(
            delay
        );

        _isBiting = true;

        _biteWaitCoroutine =
            null;

        _biteBobTimeOffset =
            Random.Range(
                0f,
                100f
            );

        OnBiteStarted?.Invoke();
    }

    // ============================================================
    // DETACH
    // ============================================================

    private void DetachModels()
    {
        if (_isDetached)
            return;

        if (_floatModel != null)
        {
            _floatModel.SetParent(
                null,
                true
            );
        }

        if (_hookModel != null)
        {
            _hookModel.SetParent(
                null,
                true
            );
        }

        _isDetached = true;
    }

    // ============================================================
    // REATTACH
    // ============================================================

    private void ReattachModels()
    {
        if (!_isDetached)
            return;

        if (
            _floatModel != null &&
            _floatOriginalParent != null
        )
        {
            _floatModel.SetParent(
                _floatOriginalParent,
                false
            );

            _floatModel.localPosition =
                _floatOriginalLocalPosition;

            _floatModel.localRotation =
                _floatOriginalLocalRotation;

            _floatModel.localScale =
                _floatOriginalLocalScale;
        }

        if (
            _hookModel != null &&
            _hookOriginalParent != null
        )
        {
            _hookModel.SetParent(
                _hookOriginalParent,
                false
            );

            _hookModel.localPosition =
                _hookOriginalLocalPosition;

            _hookModel.localRotation =
                _hookOriginalLocalRotation;

            _hookModel.localScale =
                _hookOriginalLocalScale;
        }

        _isDetached = false;
    }

    // ============================================================
    // APPLY POSITIONS
    // ============================================================

    private void ApplyPositionsDirect()
    {
        if (_floatModel != null)
        {
            _floatModel.position =
                _currentFloatPos;

            _floatModel.rotation =
                Quaternion.Euler(
                    _floatRotationOffset
                );
        }

        if (_hookModel != null)
        {
            _hookModel.position =
                _currentHookPos;
        }
    }

    // ============================================================
    // CAUGHT FISH PULL
    // ============================================================

    private Vector3 UpdateCaughtFishPull()
    {
        if (_caughtFishStruggleRef == null)
        {
            _fishPullOffset =
                Vector3.zero;

            _fishPullVelocity =
                Vector3.zero;

            return Vector3.zero;
        }

        Vector3 targetPull =
            _caughtFishStruggleRef.CurrentTugWorld *
            _caughtFishPullMultiplier;

        _fishPullOffset =
            Vector3.SmoothDamp(
                _fishPullOffset,
                targetPull,
                ref _fishPullVelocity,
                Mathf.Max(
                    0.01f,
                    _caughtFishPullSmoothTime
                )
            );

        return _fishPullOffset;
    }

    // ============================================================
    // IN WATER
    // ============================================================

    private void UpdateInWaterLine()
    {
        if (
            _rodTip == null ||
            _lineRenderer == null
        )
        {
            return;
        }

        float bobOffset = 0f;

        if (_isLandingBob)
            bobOffset =
                StepLandingBob();

        float biteOffset = 0f;

        if (_isBiting)
        {
            float bt =
                (
                    Time.time +
                    _biteBobTimeOffset
                ) *
                _biteBobSpeed;

            biteOffset =
                (
                    Mathf.PerlinNoise(
                        bt,
                        0f
                    ) -
                    0.5f
                ) *
                2f *
                _biteBobAmount;
        }

        float progressDelta =
            _reelProgress -
            _prevReelProgress;

        _prevReelProgress =
            _reelProgress;

        float targetPullDepth = 0f;

        if (_isReeling)
        {
            float baseDepth =
                Mathf.Lerp(
                    _maxPullUnderDepth,
                    0f,
                    _reelProgress
                );

            float extraPull =
                progressDelta < 0f

                    ? -progressDelta *
                      _decreasingPullMultiplier

                    : 0f;

            targetPullDepth =
                Mathf.Clamp(
                    baseDepth +
                    extraPull,
                    0f,
                    _maxPullUnderDepth *
                    1.5f
                );
        }

        _pullUnderDepth =
            Mathf.SmoothDamp(
                _pullUnderDepth,
                targetPullDepth,
                ref _pullUnderVelocity,
                Mathf.Max(
                    0.01f,
                    _pullUnderSmoothTime
                )
            );

        _currentFloatPos.y =
            _landingSurfaceY +
            bobOffset +
            biteOffset -
            _pullUnderDepth;

        float floatWaterY =
            _currentFloatPos.y;

        if (
            _isReeling ||
            _pendingForceSnapDistance
        )
        {
            Vector3 rodTipXZ =
                new Vector3(
                    _rodTip.position.x,
                    floatWaterY,
                    _rodTip.position.z
                );

            float startDistance =
                Mathf.Max(
                    _inWaterLineLength,
                    _minFarDistance
                );

            float farDistance =
                startDistance +
                Mathf.Max(
                    0f,
                    _farPullExtraDistance
                );

            float closeDistance =
                _minReelDistance;

            float progressForDistance =
                _pendingForceSnapDistance
                    ? _pendingSnapProgress
                    : _reelProgress;

            float targetDistXZ =
                Mathf.Lerp(
                    farDistance,
                    closeDistance,
                    progressForDistance
                );

            float currentDistXZ =
                Vector3.Distance(
                    rodTipXZ,
                    new Vector3(
                        _currentFloatPos.x,
                        floatWaterY,
                        _currentFloatPos.z
                    )
                );

            float smoothedDist;

            if (_pendingForceSnapDistance)
            {
                smoothedDist =
                    targetDistXZ;

                _reelDistVelocity = 0f;

                _pendingForceSnapDistance =
                    false;
            }
            else
            {
                smoothedDist =
                    Mathf.SmoothDamp(
                        currentDistXZ,
                        targetDistXZ,
                        ref _reelDistVelocity,
                        Mathf.Max(
                            0.01f,
                            _reelDistanceSmoothTime
                        )
                    );
            }

            float targetAngle =
                Mathf.Lerp(
                    _escapeAngleAtLowProgress,
                    _escapeAngleAtHighProgress,
                    progressForDistance
                );

            _currentEscapeAngle =
                Mathf.SmoothDamp(
                    _currentEscapeAngle,
                    targetAngle,
                    ref _escapeAngleVelocity,
                    Mathf.Max(
                        0.01f,
                        _escapeAngleSmoothTime
                    )
                );

            float wobbleScale =
                _fadeWobbleWithProgress

                    ? Mathf.Lerp(
                        1f,
                        0.35f,
                        progressForDistance
                    )

                    : 1f;

            float wobbleNoise =
                Mathf.PerlinNoise(
                    Time.time *
                    _escapeWobbleSpeed +
                    _wobbleTimeOffset,
                    0f
                );

            float wobbleAngle =
                (
                    wobbleNoise -
                    0.5f
                ) *
                2f *
                _escapeWobbleAmount *
                wobbleScale;

            float finalAngle =
                _currentEscapeAngle +
                wobbleAngle;

            Vector3 fwd =
                _baseCastDirXZ;

            Vector3 left =
                Vector3.Cross(
                    fwd,
                    Vector3.up
                ).normalized;

            float rad =
                finalAngle *
                Mathf.Deg2Rad;

            Vector3 escapeDir =
                (
                    fwd *
                    Mathf.Cos(rad) +
                    left *
                    Mathf.Sin(rad)
                ).normalized;

            Vector3 newXZ =
                rodTipXZ +
                escapeDir *
                smoothedDist;

            _currentFloatPos =
                new Vector3(
                    newXZ.x,
                    floatWaterY,
                    newXZ.z
                );
        }

        // ========================================================
        // CURRENT SWAY
        // ========================================================

        float time =
            Time.time *
            _currentSwaySpeed +
            _swayTimeOffset;

        Vector3 targetSway =
            new Vector3(
                (
                    Mathf.PerlinNoise(
                        time,
                        0f
                    ) -
                    0.5f
                ) *
                2f *
                _currentSwayAmount,

                0f,

                (
                    Mathf.PerlinNoise(
                        0f,
                        time + 5f
                    ) -
                    0.5f
                ) *
                2f *
                _currentSwayAmount
            );

        _currentSwayOffset =
            Vector3.SmoothDamp(
                _currentSwayOffset,
                targetSway,
                ref _currentSwayVelocity,
                _currentSwaySmooth
            );

        // ========================================================
        // HOOK
        // ========================================================

        _hookBasePos =
            new Vector3(
                _currentFloatPos.x,
                _hookBasePos.y,
                _currentFloatPos.z
            );

        _currentHookPos =
            _hookBasePos +
            _currentSwayOffset;

        if (_hookModel != null)
        {
            _hookModel.position =
                _currentHookPos;

            if (
                _currentSwayOffset.sqrMagnitude >
                0.0001f
            )
            {
                Vector3 swayDir =
                    _currentSwayOffset.normalized;

                Quaternion targetRot =
                    Quaternion.LookRotation(
                        Vector3.down +
                        swayDir * 0.5f,
                        Vector3.forward
                    ) *
                    Quaternion.Euler(
                        _hookRotationOffset
                    );

                _hookModel.rotation =
                    Quaternion.Slerp(
                        _hookModel.rotation,
                        targetRot,
                        Time.deltaTime *
                        3f
                    );
            }
        }

        // ========================================================
        // LINE ATTACH
        // ========================================================

        if (_lineAttachPoint != null)
        {
            _currentLineAttachPos =
                _lineAttachPoint.position;
        }
        else
        {
            _currentLineAttachPos =
                _currentHookPos;
        }

        _rodBendEffect?.SetBendTargetPosition(
            _currentLineAttachPos
        );

        // ========================================================
        // FLOAT
        // ========================================================

        if (_floatModel != null)
        {
            _floatModel.position =
                _currentFloatPos;
        }

        // ========================================================
        // VELOCITY BRIDGE
        // ========================================================

        if (Time.deltaTime > 0.0001f)
        {
            _floatVelocity =
                (
                    _currentFloatPos -
                    _floatPosLastFrameForVelocity
                ) /
                Time.deltaTime;

            _hookVelocity =
                (
                    _currentHookPos -
                    _hookPosLastFrameForVelocity
                ) /
                Time.deltaTime;
        }

        _floatPosLastFrameForVelocity =
            _currentFloatPos;

        _hookPosLastFrameForVelocity =
            _currentHookPos;

        DrawLine();
    }

    // ============================================================
    // DRAW LINE
    // ============================================================

    private void DrawLine()
    {
        if (
            _lineRenderer == null ||
            _rodTip == null
        )
        {
            return;
        }

        if (_state == LineState.InWater)
        {
            _lineRenderer.positionCount = 2;

            _lineRenderer.SetPosition(
                0,
                _rodTip.position
            );

            _lineRenderer.SetPosition(
                1,
                _currentFloatPos
            );
        }
        else
        {
            _lineRenderer.positionCount = 3;

            _lineRenderer.SetPosition(
                0,
                _rodTip.position
            );

            _lineRenderer.SetPosition(
                1,
                _currentFloatPos
            );

            _lineRenderer.SetPosition(
                2,
                _currentLineAttachPos
            );
        }
    }

    // ============================================================
    // LINE START
    // ============================================================

    private void UpdateLineStart()
    {
        if (
            _lineRenderer == null ||
            _rodTip == null
        )
        {
            return;
        }

        if (_lineRenderer.positionCount <= 0)
            return;

        _lineRenderer.SetPosition(
            0,
            _rodTip.position
        );
    }

    // ============================================================
    // VISUALS
    // ============================================================

    private void SetVisualsActive(bool active)
    {
        if (_lineRenderer != null)
        {
            _lineRenderer.enabled =
                active;
        }

        if (_floatModel != null)
        {
            _floatModel.gameObject.SetActive(
                active
            );
        }

        if (_hookModel != null)
        {
            _hookModel.gameObject.SetActive(
                active
            );
        }
    }

    // ============================================================
    // LINE RENDERER
    // ============================================================

    private void SetupLineRenderer()
    {
        if (_lineRenderer == null)
            return;

        _lineRenderer.positionCount = 3;

        _lineRenderer.startWidth = 0.01f;
        _lineRenderer.endWidth = 0.01f;

        _lineRenderer.useWorldSpace = true;

        _lineRenderer.numCapVertices = 4;
        _lineRenderer.numCornerVertices = 4;

        _lineRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;

        _lineRenderer.receiveShadows =
            false;

        _lineRenderer.alignment =
            LineAlignment.TransformZ;

        _lineRenderer.textureMode =
            LineTextureMode.Stretch;

        var mat =
            new Material(
                Shader.Find(
                    "Sprites/Default"
                )
            );

        mat.color =
            new Color(
                0.9f,
                0.88f,
                0.75f,
                1f
            );

        _lineRenderer.material =
            mat;
    }

    // ============================================================
    // SNAP
    // ============================================================

    private void SnapToTarget()
    {
        if (_rodTip == null)
            return;

        if (_lineRenderer != null)
            _lineRenderer.positionCount = 3;

        Vector3 tip =
            _rodTip.position;

        Vector3 hookTarget =
            tip +
            Vector3.down *
            _hangLength +
            _hookPositionOffset;

        Vector3 floatTarget =
            Vector3.Lerp(
                tip,
                tip +
                Vector3.down *
                _hangLength,
                _floatRatio
            ) +
            Vector3.up *
            _floatYOffset;

        _currentFloatPos =
            floatTarget;

        _currentHookPos =
            hookTarget;

        _floatVelocity =
            Vector3.zero;

        _hookVelocity =
            Vector3.zero;

        Vector3 dir =
            _currentHookPos -
            _currentFloatPos;

        if (dir.sqrMagnitude >
            0.0001f)
        {
            Vector3 upHint =
                Mathf.Abs(
                    Vector3.Dot(
                        dir.normalized,
                        Vector3.up
                    )
                ) > 0.95f

                    ? Vector3.forward
                    : Vector3.up;

            _currentHookRotation =
                Quaternion.LookRotation(
                    dir.normalized,
                    upHint
                ) *
                Quaternion.Euler(
                    _hookRotationOffset
                );
        }

        _currentLineAttachPos =
            _currentHookPos;

        if (
            _hookModel != null &&
            _lineAttachPoint != null
        )
        {
            _hookModel.rotation =
                _currentHookRotation;

            Vector3 localAttachPoint =
                _hookModel.InverseTransformPoint(
                    _lineAttachPoint.position
                );

            _hookModel.position =
                _currentHookPos -
                _hookModel.TransformVector(
                    localAttachPoint
                );

            _currentLineAttachPos =
                _lineAttachPoint.position;
        }

        _isReady = true;

        SetVisualsActive(true);
    }

    // ============================================================
    // HANGING LINE
    // ============================================================

    private void UpdateHangingLine()
    {
        if (_rodTip == null)
            return;

        if (_needSnap)
        {
            SnapToTarget();

            _needSnap = false;
        }

        if (!_isReady)
            return;

        if (
            _lineRenderer != null &&
            _lineRenderer.positionCount < 3
        )
        {
            _lineRenderer.positionCount = 3;
        }

        Vector3 tip =
            _rodTip.position;

        Vector3 fishPullOffset =
            UpdateCaughtFishPull();

        Vector3 hookTarget =
            tip +
            Vector3.down *
            _hangLength +
            _hookPositionOffset +
            _swayOffset +
            fishPullOffset;

        Vector3 floatTarget =
            Vector3.Lerp(
                tip,
                tip +
                Vector3.down *
                _hangLength,
                _floatRatio
            ) +
            Vector3.up *
            _floatYOffset +
            _swayOffset *
            _floatRatio +
            fishPullOffset *
            _floatRatio;

        _currentFloatPos =
            Vector3.SmoothDamp(
                _currentFloatPos,
                floatTarget,
                ref _floatVelocity,
                _floatSmoothTime
            );

        _currentHookPos =
            Vector3.SmoothDamp(
                _currentHookPos,
                hookTarget,
                ref _hookVelocity,
                _hookSmoothTime
            );

        if (_caughtFishStruggleRef != null)
        {
            _rodBendEffect?.SetBendTargetPosition(
                _currentHookPos
            );
        }

        // ========================================================
        // FLOAT
        // ========================================================

        if (
            _floatModel != null &&
            _floatModel.parent != null
        )
        {
            _floatModel.localPosition =
                _floatModel.parent.InverseTransformPoint(
                    _currentFloatPos
                );

            _floatModel.localRotation =
                Quaternion.Inverse(
                    _floatModel.parent.rotation
                ) *
                Quaternion.Euler(
                    _floatRotationOffset
                );
        }

        // ========================================================
        // HOOK
        // ========================================================

        if (_hookModel != null)
        {
            if (_caughtFishStruggleRef != null)
            {
                if (!_hasFrozenFishHookRotation)
                {
                    Vector3 dirInit =
                        _currentHookPos -
                        _currentFloatPos;

                    if (
                        dirInit.sqrMagnitude >
                        0.0001f
                    )
                    {
                        Vector3 upHintInit =
                            Mathf.Abs(
                                Vector3.Dot(
                                    dirInit.normalized,
                                    Vector3.up
                                )
                            ) > 0.95f

                                ? Vector3.forward
                                : Vector3.up;

                        _currentHookRotation =
                            Quaternion.LookRotation(
                                dirInit.normalized,
                                upHintInit
                            ) *
                            Quaternion.Euler(
                                _hookRotationOffset
                            );
                    }

                    _frozenFishHookRotation =
                        _currentHookRotation;

                    _hasFrozenFishHookRotation =
                        true;
                }

                _currentHookRotation =
                    _frozenFishHookRotation;

                _hookModel.rotation =
                    _currentHookRotation;

                if (_lineAttachPoint != null)
                {
                    Vector3 localAttachPoint =
                        _hookModel.InverseTransformPoint(
                            _lineAttachPoint.position
                        );

                    _hookModel.position =
                        _currentHookPos -
                        _hookModel.TransformVector(
                            localAttachPoint
                        );

                    _currentLineAttachPos =
                        _lineAttachPoint.position;
                }
                else
                {
                    _hookModel.position =
                        _currentHookPos;

                    _currentLineAttachPos =
                        _currentHookPos;
                }
            }
            else
            {
                Vector3 dir =
                    _currentHookPos -
                    _currentFloatPos;

                if (
                    dir.sqrMagnitude >
                    0.0001f
                )
                {
                    Vector3 upHint =
                        Mathf.Abs(
                            Vector3.Dot(
                                dir.normalized,
                                Vector3.up
                            )
                        ) > 0.95f

                            ? Vector3.forward
                            : Vector3.up;

                    Quaternion targetWorldRotation =
                        Quaternion.LookRotation(
                            dir.normalized,
                            upHint
                        ) *
                        Quaternion.Euler(
                            _hookRotationOffset
                        );

                    _currentHookRotation =
                        Quaternion.Slerp(
                            _currentHookRotation,
                            targetWorldRotation,
                            1f -
                            Mathf.Exp(
                                -_hookRotationSmoothSpeed *
                                Time.deltaTime
                            )
                        );

                    _hookModel.rotation =
                        _currentHookRotation;

                    if (_lineAttachPoint != null)
                    {
                        Vector3 localAttachPoint =
                            _hookModel.InverseTransformPoint(
                                _lineAttachPoint.position
                            );

                        _hookModel.position =
                            _currentHookPos -
                            _hookModel.TransformVector(
                                localAttachPoint
                            );

                        _currentLineAttachPos =
                            _lineAttachPoint.position;
                    }
                    else
                    {
                        _hookModel.position =
                            _currentHookPos;

                        _currentLineAttachPos =
                            _currentHookPos;
                    }
                }
            }
        }

        // ========================================================
        // LINE
        // ========================================================

        if (_lineRenderer != null)
        {
            _lineRenderer.SetPosition(
                0,
                tip
            );

            _lineRenderer.SetPosition(
                1,
                _currentFloatPos
            );

            _lineRenderer.SetPosition(
                2,
                _currentLineAttachPos
            );
        }
    }

    // ============================================================
    // ROD SWAY
    // ============================================================

    private void UpdateRodSway()
    {
        if (_rodTip == null)
            return;

        Quaternion currentRotation =
            _rodTip.rotation;

        Quaternion deltaRotation =
            currentRotation *
            Quaternion.Inverse(
                _lastRodRotation
            );

        deltaRotation.ToAngleAxis(
            out float angle,
            out Vector3 axis
        );

        if (angle > 180f)
            angle -= 360f;

        if (
            Mathf.Abs(angle) > 0.001f &&
            axis.sqrMagnitude > 0.001f
        )
        {
            Vector3 swayDirection =
                Vector3.Cross(
                    axis.normalized,
                    Vector3.down
                );

            float swayStrength =
                Mathf.Clamp(
                    angle *
                    Mathf.Deg2Rad *
                    _positionSwayAmount,
                    -_maxSway,
                    _maxSway
                );

            _swayOffset +=
                swayDirection *
                swayStrength;
        }

        _swayOffset =
            Vector3.SmoothDamp(
                _swayOffset,
                Vector3.zero,
                ref _swayVelocity,
                _swaySmoothTime
            );

        _lastRodRotation =
            currentRotation;
    }
}