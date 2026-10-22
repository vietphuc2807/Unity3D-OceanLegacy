using Fusion;
using UnityEngine;

public class FishingCastController : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHeldItem _heldItem;
    [SerializeField] private Animator _animator;
    [SerializeField] private FishingCastCameraEffect _castCameraEffect;
    [SerializeField] private PlayerInventory _inventory;

    [Tooltip("Danh sach ca co the cau duoc. PHAI keo CUNG 1 asset " +
             "FishCatalog nay cho MOI player/prefab trong game - index " +
             "trong list duoc gui qua RPC de dong bo client khac nen thu " +
             "tu list phai khop nhau tuyet doi.")]
    [SerializeField] private FishCatalog _fishCatalog;

    [Header("Bait")]
    [Tooltip("Cung 1 BaitCatalog asset dung chung toan game, de tra BaitData tu BaitId da chon trong Bait Bag.")]
    [SerializeField] private BaitCatalog _baitCatalog;

    [Tooltip("Keo component HookBaitVisual cua player nay vao day, dung de " +
             "an/hien lai model moi 3D gan tren luoi cau dung luc moi thuc " +
             "su bi tieu hao.")]
    [SerializeField] private HookBaitVisual _hookBaitVisual;

    // ============================================================
    // CAST LINE SOUND (2D - CHI nguoi quang nghe)
    // ============================================================

    [Header("Cast Line Sound (2D - CHI nguoi quang nghe)")]
    [Tooltip("Keo THANG file cast the line vao day. Chi phat tren may " +
             "nguoi dang quang (HasInputAuthority), nguoi choi khac KHONG nghe.")]
    [SerializeField] private AudioClip _castLineClip;

    [Range(0f, 1f)]
    [SerializeField] private float _castLineVolume = 1f;

    [Tooltip("BAT: am luong nhan them voi thanh SFX trong Settings.")]
    [SerializeField] private bool _castLineFollowSfxVolume = true;

    [Tooltip("Thoi gian fade out (giay) luc phao cham nuoc. 0 = tat ngay.")]
    [SerializeField] private float _castLineFadeOutSeconds = 0.08f;

    private AudioSource _castLineSource;
    private bool _castLineFadingOut;
    private float _castLineBaseVolume;

    // ============================================================
    // PENDING BAIT
    // ============================================================

    private string _pendingBaitId;
    private BaitData _pendingBaitData;
    private bool _pendingHasBait;

    // ============================================================
    // CHARGE SETTINGS
    // ============================================================

    [Header("Charge Settings")]
    [SerializeField] private float _maxChargeTime = 1.5f;
    [SerializeField] private float _minCastDistance = 5f;
    [SerializeField] private float _maxCastDistance = 25f;

    // ============================================================
    // CHARGE ANIMATION
    // ============================================================

    [Header("Charge Animation")]
    [SerializeField] private string _castStateName = "Fishing-Cast";
    [SerializeField] private int _castLayerIndex = 0;

    [Range(0f, 1f)]
    [SerializeField] private float _chargeHoldNormalizedTime = 0.354f;

    [SerializeField] private string _idleStateName = "Fishing-BlendTree";
    [SerializeField] private float _chargeBlendDuration = 0.15f;

    // ============================================================
    // RELEASE TIMING
    // ============================================================

    [Header("Release Timing")]
    [SerializeField] private float _castClipDurationSeconds = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float _releaseNormalizedTime = 0.75f;

    // ============================================================
    // NETWORK SYNC
    // ============================================================

    [Header("Network Sync")]
    [SerializeField] private float _chargeSyncInterval = 0.06f;

    [Tooltip("Tan suat (giay) gui RPC dong bo tien do keo ca cho cac client khac trong luc CatchFishMinigame dang active.")]
    [SerializeField] private float _reelSyncInterval = 0.06f;

    // ============================================================
    // FISH STRENGTH
    // ============================================================

    [Header("Fish Strength (do manh ca to/nho)")]

    [Tooltip("Can nang (KG) lam moc THAP NHAT khi quy doi ra strength01 = 0.")]
    [SerializeField] private float _minWeightForStrength = 0.3f;

    [Tooltip("Can nang (KG) lam moc CAO NHAT khi quy doi ra strength01 = 1.")]
    [SerializeField] private float _maxWeightForStrength = 12f;

    private float _pendingFishStrength01 = 0.5f;
    private float _pendingRarityDifficulty01;

    private FishData _pendingCaughtFish;
    private float _pendingSizeMultiplier = 1f;

    // ============================================================
    // STATE
    // ============================================================

    private readonly int _castStateHash =
        Animator.StringToHash("Fishing-Cast");

    private int _idleStateHash;

    private FishingLine _currentFishingLine;

    private bool _isHoldingFishingRod;
    private bool _isCharging;

    private float _chargeStartTime;
    private float _lastChargeSyncTime;
    private float _lastReelSyncTime;

    private bool _remoteWasReeling;

    [Tooltip("CHAN SPAM: bat true ngay khi ReleaseCast() thuc hien 1 cu quang " +
             "va chi tat khi cu quang that su xong.")]
    private bool _castResolving;

    // ============================================================
    // CAUGHT FISH PICKUP
    // ============================================================

    [Header("Caught Fish Pickup")]
    [SerializeField] private float _caughtFishPickupDelay = 0.8f;

    private float _caughtFishPickupUnlockTime;

    private Coroutine _pendingCastCoroutine;

    // ============================================================
    // REMOTE CHARGE VISUAL
    // ============================================================

    private bool _wasNetworkedCharging;
    private float _remoteChargeStartTime;

    private float _remotePowerPrev;
    private float _remotePowerTarget;
    private float _remotePowerChangeTime;
    private float _lastSeenNetworkedPower;

    // ============================================================
    // NETWORKED
    // ============================================================

    [Networked]
    private NetworkBool NetworkedIsCharging { get; set; }

    [Networked]
    private float NetworkedChargePower { get; set; }

    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        _idleStateHash =
            Animator.StringToHash(_idleStateName);
    }

    public override void Spawned()
    {
        // Chi nguoi dang dieu khien player nay moi can AudioSource tieng
        // cast the line (2D) - may cua nguoi khac KHONG tao nen khong the
        // phat tieng nay.
        if (Object.HasInputAuthority)
            SetupCastLineSource();

        if (_heldItem != null)
            _heldItem.OnHeldItemChanged += HandleHeldItemChanged;

        _currentFishingLine =
            GetComponentInChildren<FishingLine>(true);

        _isHoldingFishingRod =
            _currentFishingLine != null;

        if (_currentFishingLine != null)
        {
            _currentFishingLine.gameObject.SetActive(true);

            if (Object.HasInputAuthority)
            {
                _currentFishingLine.OnBiteStarted -=
                    HandleBiteStarted;

                _currentFishingLine.OnCastMissedWater -=
                    HandleCastMissedWater;

                _currentFishingLine.OnFloatLanded -=
                    HandleFloatLanded;

                _currentFishingLine.OnBiteStarted +=
                    HandleBiteStarted;

                _currentFishingLine.OnCastMissedWater +=
                    HandleCastMissedWater;

                _currentFishingLine.OnFloatLanded +=
                    HandleFloatLanded;
            }
        }

        Debug.Log(
            $"[FishingCastController] Spawned | " +
            $"HasInputAuthority={Object.HasInputAuthority} | " +
            $"HoldingRod={_isHoldingFishingRod} | " +
            $"FishingLine={(_currentFishingLine != null ? "FOUND" : "NULL")}"
        );
    }

    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        StopCastLineSound(immediate: true);

        if (_heldItem != null)
            _heldItem.OnHeldItemChanged -=
                HandleHeldItemChanged;

        if (_pendingCastCoroutine != null)
        {
            StopCoroutine(_pendingCastCoroutine);
            _pendingCastCoroutine = null;
        }

        if (Object.HasInputAuthority &&
            _currentFishingLine != null)
        {
            _currentFishingLine.OnBiteStarted -=
                HandleBiteStarted;

            _currentFishingLine.OnCastMissedWater -=
                HandleCastMissedWater;

            _currentFishingLine.OnFloatLanded -=
                HandleFloatLanded;

            FishingBiteVignetteEffect.Instance?.HideBite();
        }
    }

    // ============================================================
    // CAST LINE SOUND
    // ============================================================

    private void SetupCastLineSource()
    {
        if (_castLineClip == null)
            return;

        _castLineSource = gameObject.AddComponent<AudioSource>();
        _castLineSource.clip = _castLineClip;
        _castLineSource.playOnAwake = false;
        _castLineSource.loop = false;
        _castLineSource.spatialBlend = 0f; // 2D
    }

    private void StartCastLineSound()
    {
        if (_castLineSource == null)
            return;

        float volume = _castLineVolume;

        if (_castLineFollowSfxVolume && SoundManager.Instance != null)
            volume *= SoundManager.Instance.GetSFXVolume();

        _castLineBaseVolume = volume;
        _castLineFadingOut = false;

        _castLineSource.volume = volume;
        _castLineSource.Stop();
        _castLineSource.Play();
    }

    private void StopCastLineSound(bool immediate = false)
    {
        if (_castLineSource == null || !_castLineSource.isPlaying)
            return;

        if (immediate || _castLineFadeOutSeconds <= 0f)
        {
            _castLineSource.Stop();
            _castLineFadingOut = false;
            return;
        }

        _castLineFadingOut = true;
    }

    private void UpdateCastLineFade()
    {
        if (!_castLineFadingOut || _castLineSource == null)
            return;

        float step =
            _castLineBaseVolume *
            Time.unscaledDeltaTime /
            Mathf.Max(0.0001f, _castLineFadeOutSeconds);

        _castLineSource.volume =
            Mathf.MoveTowards(_castLineSource.volume, 0f, step);

        if (_castLineSource.volume <= 0.001f)
        {
            _castLineSource.Stop();
            _castLineFadingOut = false;
        }
    }

    // ============================================================
    // NO BAIT CHECK
    // ============================================================

    private bool HasUsableSelectedBait()
    {
        string selectedBaitId =
            BaitBagUI.SelectedBaitId;

        if (string.IsNullOrEmpty(selectedBaitId))
            return false;

        if (BaitBag.Instance == null)
            return false;

        return BaitBag.Instance.GetCount(
            selectedBaitId
        ) > 0;
    }

    // ============================================================
    // HELD ITEM
    // ============================================================

    private void HandleHeldItemChanged(ItemData item)
    {
        bool wasHoldingRod =
            _isHoldingFishingRod;

        _isHoldingFishingRod =
            item != null &&
            item.IsFishingRod;

        FishingLine previousLine =
            _currentFishingLine;

        if (Object.HasInputAuthority &&
            previousLine != null)
        {
            previousLine.OnBiteStarted -=
                HandleBiteStarted;

            previousLine.OnCastMissedWater -=
                HandleCastMissedWater;

            previousLine.OnFloatLanded -=
                HandleFloatLanded;
        }

        _currentFishingLine =
            _isHoldingFishingRod
                ? GetComponentInChildren<FishingLine>(true)
                : null;

        if (Object.HasInputAuthority &&
            _currentFishingLine != null)
        {
            _currentFishingLine.OnBiteStarted +=
                HandleBiteStarted;

            _currentFishingLine.OnCastMissedWater +=
                HandleCastMissedWater;

            _currentFishingLine.OnFloatLanded +=
                HandleFloatLanded;
        }

        CancelCharge();

        _castResolving = false;
        _remoteWasReeling = false;

        // Doi/cat can cau giua chung: tat tieng cast the line ngay.
        StopCastLineSound(immediate: true);

        if (wasHoldingRod &&
            !_isHoldingFishingRod)
        {
            if (_pendingCastCoroutine != null)
            {
                StopCoroutine(
                    _pendingCastCoroutine
                );

                _pendingCastCoroutine = null;
            }

            if (Object.HasInputAuthority)
            {
                HookFishMinigame.Instance?.CancelMinigame();

                CatchFishMinigame.Instance?.CancelMinigame();

                FishingBiteVignetteEffect.Instance?.HideBite();

                FishingBiteCameraEffect.Instance?.ResetCamera();
            }

            previousLine?.ResetRodBend();
            previousLine?.ForceImmediateIdle();

            if (previousLine != null)
                previousLine.gameObject.SetActive(false);
        }

        if (!wasHoldingRod &&
            _isHoldingFishingRod &&
            _currentFishingLine != null)
        {
            _currentFishingLine.gameObject.SetActive(true);
        }

        if (wasHoldingRod !=
            _isHoldingFishingRod)
        {
            _animator?.Play(
                _idleStateHash,
                _castLayerIndex,
                0f
            );
        }
    }

    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        // Fade out tieng cast the line (chi co tac dung tren may nguoi quang).
        UpdateCastLineFade();

        if (!Object.HasInputAuthority)
        {
            HandleRemoteChargeVisual();
            return;
        }

        if (!_isHoldingFishingRod)
            return;

        if (_currentFishingLine == null)
        {
            _currentFishingLine =
                GetComponentInChildren<FishingLine>(true);

            if (_currentFishingLine == null)
                return;

            _currentFishingLine.gameObject.SetActive(true);

            _currentFishingLine.OnBiteStarted -=
                HandleBiteStarted;

            _currentFishingLine.OnCastMissedWater -=
                HandleCastMissedWater;

            _currentFishingLine.OnFloatLanded -=
                HandleFloatLanded;

            _currentFishingLine.OnBiteStarted +=
                HandleBiteStarted;

            _currentFishingLine.OnCastMissedWater +=
                HandleCastMissedWater;

            _currentFishingLine.OnFloatLanded +=
                HandleFloatLanded;
        }

        if (LobbyChatUI.IsTyping ||
            PlayerCursor.IsUIOpen)
        {
            CancelCharge();
            return;
        }

        // ========================================================
        // LEFT MOUSE DOWN
        // ========================================================

        if (Input.GetMouseButtonDown(0))
        {
            if (Time.time <
                _caughtFishPickupUnlockTime)
            {
                return;
            }

            if (_inventory != null &&
                _inventory.HasPendingCaughtFish)
            {
                _inventory.TryConsumePendingCaughtFish();
                return;
            }

            if (!_currentFishingLine.IsInWater &&
                !_currentFishingLine.IsCasting &&
                !_castResolving)
            {
                StartCharge();
            }
        }

        // ========================================================
        // LEFT MOUSE UP
        // ========================================================

        else if (Input.GetMouseButtonUp(0))
        {
            if (_isCharging)
                ReleaseCast();
        }

        // ========================================================
        // CHARGING
        // ========================================================

        if (_isCharging)
        {
            float power =
                Mathf.Clamp01(
                    (Time.time -
                     _chargeStartTime) /
                    _maxChargeTime
                );

            FishingCastUI.Instance?.SetFill(
                power
            );

            float elapsed =
                Time.time -
                _chargeStartTime;

            if (elapsed <
                _chargeBlendDuration)
                return;

            float scrubTime =
                power *
                _chargeHoldNormalizedTime;

            _animator?.Play(
                _castStateHash,
                _castLayerIndex,
                scrubTime
            );

            _castCameraEffect?.UpdateChargeTilt(
                power
            );

            if (Time.time -
                _lastChargeSyncTime >=
                _chargeSyncInterval)
            {
                _lastChargeSyncTime =
                    Time.time;

                RPC_ReportChargePower(
                    power
                );
            }
        }

        // ========================================================
        // REEL SYNC
        // ========================================================

        if (CatchFishMinigame.Instance != null &&
            CatchFishMinigame.Instance.IsActive)
        {
            if (Time.time -
                _lastReelSyncTime >=
                _reelSyncInterval)
            {
                _lastReelSyncTime =
                    Time.time;

                RPC_ReportReelProgress(
                    CatchFishMinigame.Instance.VisualProgress,
                    CatchFishMinigame.Instance.StrugglePull01,
                    true
                );
            }
        }
    }

    // ============================================================
    // REMOTE CHARGE
    // ============================================================

    private void HandleRemoteChargeVisual()
    {
        if (NetworkedIsCharging)
        {
            if (!_wasNetworkedCharging)
            {
                _remoteChargeStartTime =
                    Time.time;

                _remotePowerPrev = 0f;
                _remotePowerTarget = 0f;
                _remotePowerChangeTime =
                    Time.time;

                _lastSeenNetworkedPower = 0f;
            }

            if (!Mathf.Approximately(
                    NetworkedChargePower,
                    _lastSeenNetworkedPower))
            {
                _remotePowerPrev =
                    GetCurrentInterpolatedPower();

                _remotePowerTarget =
                    NetworkedChargePower;

                _remotePowerChangeTime =
                    Time.time;

                _lastSeenNetworkedPower =
                    NetworkedChargePower;
            }

            float elapsed =
                Time.time -
                _remoteChargeStartTime;

            if (elapsed >=
                    _chargeBlendDuration &&
                _animator != null)
            {
                float smoothedPower =
                    GetCurrentInterpolatedPower();

                float remoteScrub =
                    smoothedPower *
                    _chargeHoldNormalizedTime;

                _animator.Play(
                    _castStateHash,
                    _castLayerIndex,
                    remoteScrub
                );
            }
        }

        _wasNetworkedCharging =
            NetworkedIsCharging;
    }

    private float GetCurrentInterpolatedPower()
    {
        if (_chargeSyncInterval <= 0f)
            return _remotePowerTarget;

        float t =
            Mathf.Clamp01(
                (Time.time -
                 _remotePowerChangeTime) /
                _chargeSyncInterval
            );

        return Mathf.Lerp(
            _remotePowerPrev,
            _remotePowerTarget,
            t
        );
    }

    // ============================================================
    // START CHARGE
    // ============================================================

    private void StartCharge()
    {
        // ========================================================
        // NGAY DA HET (dang cho nguoi ve nha / cho panel Day) ->
        // KHONG cho bat dau luot cau MOI. Check nay CHI nam o diem
        // khoi tao charge (lan tha can TIEP THEO) - luot cau dang
        // do (dang charge/bite/hook/reel) khong he bi anh huong vi
        // cac ham xu ly minigame phia sau (ReleaseCast, HandleBite
        // Started, OnHookMinigameResult, OnCatchMinigameResult...)
        // khong kiem tra co nay.
        // ========================================================

        if (GameDayManager.Instance != null &&
            GameDayManager.Instance.IsDayOver)
        {
            return;
        }

        // ========================================================
        // KHONG CO MOI -> KHONG CHO CAU
        // ========================================================

        if (!HasUsableSelectedBait())
        {
            return;
        }

        _isCharging = true;

        _chargeStartTime =
            Time.time;

        _lastChargeSyncTime = 0f;

        FishingCastUI.Instance?.Show();

        _currentFishingLine?.SetRigidTracking(
            true
        );

        _animator?.CrossFade(
            _castStateHash,
            _chargeBlendDuration,
            _castLayerIndex,
            0f
        );

        RPC_ReportChargingState(true);

        RPC_ReportChargePower(0f);

        RPC_OnChargeStarted();

        RPC_ShowHookBaitVisual();
    }

    // ============================================================
    // CANCEL CHARGE
    // ============================================================

    private void CancelCharge()
    {
        if (!_isCharging)
            return;

        _isCharging = false;

        FishingCastUI.Instance?.Hide();

        _currentFishingLine?.SetRigidTracking(
            false
        );

        _animator?.CrossFade(
            _idleStateHash,
            _chargeBlendDuration,
            _castLayerIndex,
            0f
        );

        RPC_ReportChargingState(false);

        RPC_CancelChargeVisual();
    }

    // ============================================================
    // RELEASE CAST
    // ============================================================

    private void ReleaseCast()
    {
        float power =
            Mathf.Clamp01(
                (Time.time -
                 _chargeStartTime) /
                _maxChargeTime
            );

        _isCharging = false;

        FishingCastUI.Instance?.Hide();

        RPC_ReportChargingState(false);

        _pendingBaitId =
            BaitBagUI.SelectedBaitId;

        _pendingBaitData = null;
        _pendingHasBait = false;

        if (!string.IsNullOrEmpty(
                _pendingBaitId) &&
            BaitBag.Instance != null &&
            BaitBag.Instance.GetCount(
                _pendingBaitId
            ) > 0)
        {
            _pendingHasBait = true;

            _pendingBaitData =
                _baitCatalog != null
                    ? _baitCatalog.GetById(
                        _pendingBaitId)
                    : null;
        }

        if (!_pendingHasBait)
        {
            _castResolving = false;

            _animator?.Play(
                _idleStateHash,
                _castLayerIndex,
                0f
            );

            return;
        }

        _castResolving = true;

        Camera cam =
            Camera.main;

        if (cam == null ||
            _currentFishingLine == null)
        {
            _castResolving = false;
            return;
        }

        Vector3 forward =
            cam.transform.forward;

        Vector3 horizontalDir =
            new Vector3(
                forward.x,
                0f,
                forward.z
            ).normalized;

        float verticalFactor =
            Mathf.Clamp01(
                Vector3.Dot(
                    forward,
                    Vector3.up
                ) * 0.5f +
                0.5f
            );

        float distanceMultiplier =
            Mathf.Lerp(
                0.5f,
                1.5f,
                verticalFactor
            );

        float baseDistance =
            Mathf.Lerp(
                _minCastDistance,
                _maxCastDistance,
                power
            );

        float finalDistance =
            Mathf.Clamp(
                baseDistance *
                distanceMultiplier,
                _minCastDistance,
                _maxCastDistance
            );

        float startNormalized =
            power *
            _chargeHoldNormalizedTime;

        float releaseNormalized =
            Mathf.Max(
                startNormalized,
                _releaseNormalizedTime
            );

        float deltaNormalized =
            releaseNormalized -
            startNormalized;

        float dynamicDelay =
            deltaNormalized *
            _castClipDurationSeconds;

        _animator?.Play(
            _castStateHash,
            _castLayerIndex,
            startNormalized
        );

        StartCoroutine(
            TriggerCastCameraEffect(
                power,
                dynamicDelay
            )
        );

        RPC_PerformCast(
            horizontalDir,
            finalDistance,
            dynamicDelay,
            startNormalized,
            _pendingHasBait
        );
    }

    private System.Collections.IEnumerator
        TriggerCastCameraEffect(
            float power,
            float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        _castCameraEffect?.ReleaseCast(
            power
        );
    }

    // ============================================================
    // BITE
    // ============================================================

    private void HandleBiteStarted()
    {
        FishingBiteVignetteEffect.Instance?.ShowBite();

        FishingBiteCameraEffect.Instance?.TriggerBite();

        _pendingCaughtFish = null;
        _pendingSizeMultiplier = 1f;
        _pendingFishStrength01 = 0.5f;
        _pendingRarityDifficulty01 = 0f;

        if (_fishCatalog != null)
        {
            FishData picked =
                _fishCatalog.GetRandomFish(
                    _pendingBaitData
                );

            if (picked != null)
            {
                _pendingCaughtFish =
                    picked;

                _pendingSizeMultiplier =
                    picked.GetRandomSizeMultiplier(
                        _pendingBaitData != null
                            ? _pendingBaitData.SizeBiasPercent / 100f
                            : 0f
                    );

                float weightKg =
                    picked.GetWeightKg(
                        _pendingSizeMultiplier
                    );

                _pendingFishStrength01 =
                    Mathf.InverseLerp(
                        _minWeightForStrength,
                        _maxWeightForStrength,
                        weightKg
                    );

                _pendingRarityDifficulty01 =
                    picked.GetRarityDifficulty01();
            }
        }

        HookFishMinigame.Instance?.StartMinigame(
            OnHookMinigameResult,
            _pendingRarityDifficulty01
        );
    }

    // ============================================================
    // BAIT CONSUME (dung chung cho ca Hook fail va Catch xong)
    // ============================================================

    private void ConsumePendingBaitIfAny()
    {
        if (_pendingHasBait &&
            !string.IsNullOrEmpty(
                _pendingBaitId))
        {
            BaitBag.Instance?.UseBait(
                _pendingBaitId
            );

            _pendingHasBait = false;

            RPC_HideHookBaitVisual();
        }
    }

    // ============================================================
    // HOOK MINIGAME
    // ============================================================

    private void OnHookMinigameResult(
        bool success)
    {
        FishingBiteVignetteEffect.Instance?.HideBite();

        FishingBiteCameraEffect.Instance?.ResetCamera();

        if (!success)
        {
            _pendingCaughtFish = null;

            ConsumePendingBaitIfAny();

            RPC_ResolveBite(
                false,
                -1,
                1f
            );

            return;
        }

        RPC_ReportFishStrength(
            _pendingFishStrength01
        );

        _currentFishingLine?.SetFishStrength(
            _pendingFishStrength01
        );

        _currentFishingLine?.TriggerRodHookSnap();

        CatchFishMinigame.Instance?.StartMinigame(
            OnCatchMinigameResult,
            _currentFishingLine,
            _pendingFishStrength01,
            _pendingRarityDifficulty01
        );
    }

    // ============================================================
    // CATCH MINIGAME
    // ============================================================

    private void OnCatchMinigameResult(
        bool success)
    {
        if (success)
        {
            _caughtFishPickupUnlockTime =
                Time.time +
                _caughtFishPickupDelay;
        }

        int fishIndex = -1;

        float sizeMultiplier =
            _pendingSizeMultiplier;

        if (success &&
            _pendingCaughtFish != null &&
            _fishCatalog != null)
        {
            fishIndex =
                _fishCatalog.IndexOf(
                    _pendingCaughtFish
                );
        }

        _pendingCaughtFish = null;

        ConsumePendingBaitIfAny();

        RPC_ResolveBite(
            success,
            fishIndex,
            sizeMultiplier
        );
    }

    // ============================================================
    // CAST MISSED
    // ============================================================

    private void HandleCastMissedWater()
    {
        // Quang truot nuoc: tat tieng cast the line.
        StopCastLineSound();

        _castResolving = false;

        _pendingBaitId = null;
        _pendingBaitData = null;
        _pendingHasBait = false;

        _animator?.Play(
            _idleStateHash,
            _castLayerIndex,
            0f
        );
    }

    // ============================================================
    // FLOAT LANDED
    // ============================================================

    private void HandleFloatLanded(
        Vector3 landingPoint)
    {
        // Phao cham nuoc: tat tieng cast the line (fade out nhanh).
        StopCastLineSound();

        _castResolving = false;
    }

    // ============================================================
    // FISH STRENGTH RPC
    // ============================================================

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.All
    )]
    private void RPC_ReportFishStrength(
        float strength01)
    {
        if (Object.HasInputAuthority)
            return;

        if (_currentFishingLine == null)
        {
            _currentFishingLine =
                GetComponentInChildren<FishingLine>(
                    true
                );
        }

        _currentFishingLine?.SetFishStrength(
            strength01
        );
    }

    // ============================================================
    // CHARGE RPC
    // ============================================================

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority
    )]
    private void RPC_ReportChargingState(
        bool charging)
    {
        NetworkedIsCharging =
            charging;
    }

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority
    )]
    private void RPC_ReportChargePower(
        float power)
    {
        NetworkedChargePower =
            power;
    }

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.All
    )]
    private void RPC_OnChargeStarted()
    {
        if (Object.HasInputAuthority)
            return;

        _animator?.CrossFade(
            _castStateHash,
            _chargeBlendDuration,
            _castLayerIndex,
            0f
        );
    }

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.All
    )]
    private void RPC_CancelChargeVisual()
    {
        if (Object.HasInputAuthority)
            return;

        _animator?.CrossFade(
            _idleStateHash,
            _chargeBlendDuration,
            _castLayerIndex,
            0f
        );
    }

    // ============================================================
    // PERFORM CAST RPC
    // ============================================================

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.All
    )]
    private void RPC_PerformCast(
        Vector3 direction,
        float distance,
        float delay,
        float startNormalized,
        NetworkBool hasBait)
    {
        if (_currentFishingLine == null)
        {
            _currentFishingLine =
                GetComponentInChildren<FishingLine>(
                    true
                );
        }

        FishingLine targetLine =
            _currentFishingLine;

        targetLine?.ResetToIdle();

        _animator?.Play(
            _castStateHash,
            _castLayerIndex,
            startNormalized
        );

        if (_pendingCastCoroutine != null)
        {
            StopCoroutine(
                _pendingCastCoroutine
            );
        }

        _pendingCastCoroutine =
            StartCoroutine(
                CastAfterDelay(
                    targetLine,
                    direction,
                    distance,
                    delay,
                    hasBait
                )
            );
    }

    // ============================================================
    // REEL PROGRESS RPC
    // ============================================================

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.All
    )]
    private void RPC_ReportReelProgress(
        float progress,
        float strugglePull01,
        NetworkBool isReeling)
    {
        if (Object.HasInputAuthority)
            return;

        if (_currentFishingLine == null)
        {
            _currentFishingLine =
                GetComponentInChildren<FishingLine>(
                    true
                );
        }

        if (isReeling &&
            !_remoteWasReeling)
        {
            _currentFishingLine?.StartRodStruggle();
        }

        _remoteWasReeling =
            isReeling;

        _currentFishingLine?.SetReeling(
            isReeling
        );

        _currentFishingLine?.SetReelProgress(
            progress
        );

        _currentFishingLine?.SetRodBend(
            progress
        );

        _currentFishingLine?.SetRodStrugglePull(
            strugglePull01
        );
    }

    // ============================================================
    // RESOLVE BITE
    // ============================================================

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.All
    )]
    private void RPC_ResolveBite(
        NetworkBool success,
        int fishIndex,
        float sizeMultiplier)
    {
        _currentFishingLine?.StopBiting();

        _remoteWasReeling = false;

        _currentFishingLine?.SetReelProgress(
            success ? 1f : 0f
        );

        _currentFishingLine?.ForceSnapReelDistance();

        _currentFishingLine?.SetReeling(false);

        _currentFishingLine?.SetReelProgress(0f);

        _currentFishingLine?.ResetRodBend();

        if (success &&
            fishIndex >= 0 &&
            _fishCatalog != null)
        {
            FishData fish =
                _fishCatalog.GetByIndex(
                    fishIndex
                );

            _currentFishingLine?.AttachCaughtFish(
                fish,
                sizeMultiplier
            );

            if (Object.HasInputAuthority &&
                fish != null &&
                fish.InventoryItem != null &&
                _inventory != null)
            {
                FishingLine lineRef =
                    _currentFishingLine;

                _inventory.SetPendingCaughtFish(
                    fish.InventoryItem,
                    sizeMultiplier,
                    () =>
                    {
                        lineRef?.ClearCaughtFish();

                        RPC_ClearCaughtFishOnPickup();
                    }
                );
            }
        }

        _currentFishingLine?.ResetToIdle();

        _animator?.Play(
            _idleStateHash,
            _castLayerIndex,
            0f
        );
    }

    // ============================================================
    // CLEAR CAUGHT FISH
    // ============================================================

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.All
    )]
    private void RPC_ClearCaughtFishOnPickup()
    {
        if (Object.HasInputAuthority)
            return;

        if (_currentFishingLine == null)
        {
            _currentFishingLine =
                GetComponentInChildren<FishingLine>(
                    true
                );
        }

        _currentFishingLine?.ClearCaughtFish();
    }

    // ============================================================
    // HOOK BAIT VISUAL
    // ============================================================

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.All
    )]
    private void RPC_HideHookBaitVisual()
    {
        _hookBaitVisual?.HideForRound();
    }

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.All
    )]
    private void RPC_ShowHookBaitVisual()
    {
        _hookBaitVisual?.ShowForNewRound();
    }

    // ============================================================
    // CAST AFTER DELAY
    // ============================================================

    private System.Collections.IEnumerator CastAfterDelay(
        FishingLine targetLine,
        Vector3 direction,
        float distance,
        float delay,
        bool hasBait)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(
                delay
            );

        targetLine?.SetBaited(
            hasBait
        );

        targetLine?.StartCast(
            direction,
            distance
        );

        // Phao vua roi khoi can: bat dau tieng cast the line (2D). CHI
        // nguoi quang (HasInputAuthority) nghe - may cua nguoi khac
        // khong tao AudioSource nen khong the phat. Tieng se dung khi
        // phao cham nuoc (HandleFloatLanded) hoac quang truot nuoc
        // (HandleCastMissedWater).
        if (Object.HasInputAuthority)
            StartCastLineSound();

        _pendingCastCoroutine = null;
    }
}