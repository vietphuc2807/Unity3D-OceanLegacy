using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[NetworkBehaviourWeaved(NetworkPlayerCCData.WORDS)]
public sealed unsafe class NetPlayerMovementFusion
    : NetworkTRSP,
      INetworkTRSPTeleport,
      IBeforeAllTicks,
      IAfterAllTicks,
      IBeforeCopyPreviousState
{
    new ref NetworkPlayerCCData Data => ref ReinterpretState<NetworkPlayerCCData>();

    [Header("Data")]
    [SerializeField] private PlayerMovementData _movementData;
    [SerializeField] private PlayerLookData _lookData;

    [Header("Animation")]
    [SerializeField] private Animator _animator;
    [SerializeField] private float _animationSmoothTime = 0.1f;

    [Header("Held Item")]
    [SerializeField] private PlayerHeldItem _heldItem;

    [Header("Sound")]
    [SerializeField] private PlayerFootstepSound _footstepSound;
    [Tooltip("Phai o tren khong it nhat chung nay (giay) thi moi phat tieng tiep dat.")]
    [SerializeField] private float _minAirTimeForLandSound = 0.15f;
    [Tooltip("Tat: chi phat tieng nhay/tiep dat cua CHINH MINH. Bat: phat ca cua nguoi choi khac.")]
    [SerializeField] private bool _playJumpLandForRemotePlayers = false;

    bool _wasGroundedRender = true;
    float _airTime;

    [Header("Fishing In-Water Movement")]
    [SerializeField] private float _inWaterSpeedMultiplier = 0.5f;
    [SerializeField] private float _inWaterAnimSpeedMultiplier = 0.5f;

    [Header("Fishing In-Water Camera/Yaw Limit")]
    [Tooltip("Hệ số nhân tốc độ xoay ngang (yaw) khi phao đang ở trong nước. " +
             "Nhỏ hơn 1 sẽ xoay chậm lại. Đặt = 1 nếu chỉ muốn giới hạn góc.")]
    [SerializeField] private float _inWaterYawSpeedMultiplier = 0.4f;
    [Tooltip("Góc xoay ngang tối đa (độ) lệch mỗi bên so với hướng lúc phao " +
             "vừa chạm nước. Vd 70 nghĩa là tối đa 70° trái HOẶC phải.")]
    [SerializeField] private float _inWaterMaxYawAngle = 70f;

    Tick _initial;
    CharacterController _controller;

    float _renderSpeed;
    float _renderSpeedVelocity;
    int _lastRenderedJumpCount;
    bool _isHoldingFishingRod;
    FishingLine _currentFishingLine;

    static readonly int SpeedHash    = Animator.StringToHash("Speed");
    static readonly int JumpHash     = Animator.StringToHash("Jump");
    static readonly int GroundedHash = Animator.StringToHash("Grounded");
    static readonly int FishingHash  = Animator.StringToHash("IsFishingRod");

    bool ShouldPlayJumpLandSound =>
    _footstepSound != null &&
    (Object.HasInputAuthority || _playJumpLandForRemotePlayers);

    void HandleLandSound()
    {
        bool grounded = Data.Grounded;

        if (!grounded)
        {
            _airTime += Time.deltaTime;
        }
        else
        {
            if (!_wasGroundedRender &&
                _airTime >= _minAirTimeForLandSound &&
                ShouldPlayJumpLandSound)
            {
                _footstepSound.Land();
            }

            _airTime = 0f;
        }

        _wasGroundedRender = grounded;
    }

    public Vector3 Velocity
    {
        get => Data.Velocity;
        set => Data.Velocity = value;
    }

    public float CurrentSpeed => new Vector2(Velocity.x, Velocity.z).magnitude;

    public bool Grounded
    {
        get => Data.Grounded;
        set => Data.Grounded = value;
    }

    /// Chỉ dùng để LẤY INPUT ở client local (NetworkInputProvider).
    /// KHÔNG dùng trong FixedUpdateNetwork/Move/ApplyYawRotation;
    /// trong simulation phải dùng input.LineInWater.
    public bool IsInWaterFishingLocal =>
        _currentFishingLine != null && _currentFishingLine.IsInWater;

    public void Teleport(Vector3? position = null, Quaternion? rotation = null)
    {
        _controller.enabled = false;
        NetworkTRSP.Teleport(this, transform, position, rotation);
        _controller.enabled = true;
    }

    public override void Spawned()
    {
        _initial = default;

        TryGetComponent(out _controller);

        _controller.enabled = false;
        _controller.enabled = true;

        _lastRenderedJumpCount = Data.JumpCount;

        if (_heldItem != null)
            _heldItem.OnHeldItemChanged += HandleHeldItemChanged;

        _wasGroundedRender = Data.Grounded;
        _airTime = 0f;

        if (_footstepSound == null && _animator != null)
            _footstepSound = _animator.GetComponent<PlayerFootstepSound>();

        CopyToBuffer();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_heldItem != null)
            _heldItem.OnHeldItemChanged -= HandleHeldItemChanged;
    }

    void HandleHeldItemChanged(ItemData item)
    {
        _isHoldingFishingRod = item != null && item.IsFishingRod;

        _currentFishingLine = _isHoldingFishingRod
            ? GetComponentInChildren<FishingLine>(true)
            : null;

        // Không cần reset gì thêm: khi đổi item, _currentFishingLine thành
        // null nên IsInWaterFishingLocal = false, input.LineInWater = false
        // và Data.WasInWater tự về false ở tick kế tiếp.
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority && !Object.HasInputAuthority)
            return;

        if (!GetInput(out NetworkInputData input))
            return;

        // Typing hoặc mở UI: không xoay, không nhảy, không đi ngang,
        // nhưng VẪN chạy Move() để gravity kéo nhân vật xuống đất
        // (tránh bị đứng lơ lửng khi nhảy rồi mở túi mồi / gõ chat).
        // NetworkInputProvider đã zero sẵn MoveX/MoveZ/Run/Jump/Yaw
        // khi typing || uiOpen nên Move() sẽ không đi ngang.
        if (input.IsTyping || input.IsUIOpen)
        {
            Move(input);
            return;
        }

        ApplyYawRotation(input);

        // Move() chạy trước (cập nhật Data.Grounded sau _controller.Move()),
        // sau đó HandleJump() dùng Data.Grounded đó.
        Move(input);
        HandleJump(input);
    }

    /// Xoay ngang (yaw) theo input chuột.
    /// Khi phao ở trong nước (input.LineInWater), xoay bị làm chậm và giới
    /// hạn trong ±_inWaterMaxYawAngle quanh hướng lúc phao vừa chạm nước.
    /// State (anchor, offset, cờ) nằm trong networked Data và cờ trạng thái
    /// đến từ input, nên client predict/resimulate và host cho cùng kết quả.
    void ApplyYawRotation(NetworkInputData input)
    {
        bool inWater = input.LineInWater;

        if (inWater && !Data.WasInWater)
        {
            Data.WaterAnchorYaw = transform.eulerAngles.y;
            Data.WaterYawOffset = 0f;
        }

        Data.WasInWater = inWater;

        float rawYawDelta = input.YawDelta * _lookData.MouseSensitivity;

        if (inWater)
        {
            float offset = Mathf.Clamp(
                Data.WaterYawOffset + rawYawDelta * _inWaterYawSpeedMultiplier,
                -_inWaterMaxYawAngle,
                _inWaterMaxYawAngle
            );

            Data.WaterYawOffset = offset;

            transform.rotation = Quaternion.Euler(0f, Data.WaterAnchorYaw + offset, 0f);
        }
        else
        {
            transform.Rotate(Vector3.up * rawYawDelta);
        }
    }

    void Move(NetworkInputData input)
    {
        float   deltaTime = Runner.DeltaTime;
        Vector3 v         = Data.Velocity;

        Vector3 direction =
            transform.right   * input.MoveX +
            transform.forward * input.MoveZ;
        direction = direction.normalized;

        // Dùng Data.Grounded (networked, từ tick trước) để reset v.y.
        if (Data.Grounded && v.y < 0f)
            v.y = -2f;

        v.y += _movementData.Gravity * deltaTime;

        // Lấy từ input thay vì đọc trực tiếp FishingLine (không networked).
        bool isInWaterFishing = input.LineInWater;
        bool effectiveRunHeld = input.RunHeld && !isInWaterFishing;

        float moveSpeed = effectiveRunHeld ? _movementData.RunSpeed : _movementData.WalkSpeed;
        if (isInWaterFishing)
            moveSpeed *= _inWaterSpeedMultiplier;

        Vector3 horizontal = direction * moveSpeed;
        v.x = horizontal.x;
        v.z = horizontal.z;

        _controller.Move(v * deltaTime);

        // Cập nhật Data.Grounded SAU _controller.Move().
        Data.Grounded = _controller.isGrounded;
        Data.Velocity = v;

        bool isMoving            = direction.sqrMagnitude > 0.01f;
        Data.TargetSpeed         = isMoving ? (effectiveRunHeld ? 1f : 0.5f) : 0f;
        Data.AnimSpeedMultiplier = isInWaterFishing ? _inWaterAnimSpeedMultiplier : 1f;
    }

    void HandleJump(NetworkInputData input)
    {
        // Không guard bằng Runner.IsForward: logic này đổi networked state
        // nên phải chạy giống hệt ở forward tick và resimulation tick.
        // input.JumpPressed chỉ true đúng 1 tick nên an toàn.
        if (Data.Grounded && input.JumpPressed)
        {
            var v = Data.Velocity;
            v.y           = Mathf.Sqrt(_movementData.JumpHeight * -2f * _movementData.Gravity);
            Data.Velocity = v;

            Data.Grounded = false;

            Data.JumpCount++;
        }
    }

    public override void Render()
    {
        NetworkTRSP.Render(
            this,
            transform,
            false,
            false,
            false,
            ref _initial
        );

        if (_animator == null)
            return;

        HandleLandSound();

        if (PlayerCursor.IsUIOpen)
        {
            _renderSpeed         = 0f;
            _renderSpeedVelocity = 0f;

            _animator.speed = 1f;
            _animator.SetFloat(SpeedHash,   0f);
            _animator.SetBool(GroundedHash, Data.Grounded);
            _animator.SetBool(FishingHash,  _isHoldingFishingRod);
            return;
        }

        _renderSpeed = Mathf.SmoothDamp(
            _renderSpeed,
            Data.TargetSpeed,
            ref _renderSpeedVelocity,
            _animationSmoothTime
        );

        // Render chỉ phục vụ hiển thị nên đọc FishingLine local là ổn.
        bool isCastingOrIdle = _currentFishingLine == null || !_currentFishingLine.IsInWater;
        _animator.speed = Mathf.Max(0.01f, isCastingOrIdle ? 1f : Data.AnimSpeedMultiplier);
        _animator.SetFloat(SpeedHash,   _renderSpeed);
        _animator.SetBool(GroundedHash, Data.Grounded);
        _animator.SetBool(FishingHash,  _isHoldingFishingRod);

        if (Data.JumpCount != _lastRenderedJumpCount)
        {
            _lastRenderedJumpCount = Data.JumpCount;
            _animator.SetTrigger(JumpHash);

            if (ShouldPlayJumpLandSound) _footstepSound.Jump();
        }
    }

    void IBeforeAllTicks.BeforeAllTicks(bool resimulation, int tickCount)
    {
        CopyToEngine();
    }

    void IAfterAllTicks.AfterAllTicks(bool resimulation, int tickCount)
    {
        CopyToBuffer();
    }

    void IBeforeCopyPreviousState.BeforeCopyPreviousState()
    {
        CopyToBuffer();
    }

    void Awake()
    {
        TryGetComponent(out _controller);
    }

    void CopyToBuffer()
    {
        Data.TRSPData.Position = transform.position;
        Data.TRSPData.Rotation = transform.rotation;
    }

    void CopyToEngine()
    {
        var targetPos = Data.TRSPData.Position;
        var targetRot = Data.TRSPData.Rotation;

        if (transform.position == targetPos && transform.rotation == targetRot)
            return;

        _controller.enabled = false;
        transform.SetPositionAndRotation(targetPos, targetRot);
        _controller.enabled = true;
    }
}

[StructLayout(LayoutKind.Explicit)]
[NetworkStructWeaved(WORDS + 4)]
public unsafe struct NetworkPlayerCCData : INetworkStruct
{
    // 7 word cũ + 3 word mới (anchor yaw, yaw offset, cờ WasInWater)
    public const int WORDS = NetworkTRSPData.WORDS + 10;
    public const int SIZE  = WORDS * 4;

    [FieldOffset(0)]
    public NetworkTRSPData TRSPData;

    [FieldOffset((NetworkTRSPData.WORDS + 0) * Allocator.REPLICATE_WORD_SIZE)]
    int _grounded;

    [FieldOffset((NetworkTRSPData.WORDS + 1) * Allocator.REPLICATE_WORD_SIZE)]
    Vector3Compressed _velocityData;

    [FieldOffset((NetworkTRSPData.WORDS + 4) * Allocator.REPLICATE_WORD_SIZE)]
    float _targetSpeed;

    [FieldOffset((NetworkTRSPData.WORDS + 5) * Allocator.REPLICATE_WORD_SIZE)]
    int _jumpCount;

    [FieldOffset((NetworkTRSPData.WORDS + 6) * Allocator.REPLICATE_WORD_SIZE)]
    float _animSpeedMultiplier;

    // --- State yaw khi câu dưới nước ---
    [FieldOffset((NetworkTRSPData.WORDS + 7) * Allocator.REPLICATE_WORD_SIZE)]
    float _waterAnchorYaw;

    [FieldOffset((NetworkTRSPData.WORDS + 8) * Allocator.REPLICATE_WORD_SIZE)]
    float _waterYawOffset;

    [FieldOffset((NetworkTRSPData.WORDS + 9) * Allocator.REPLICATE_WORD_SIZE)]
    int _wasInWater;

    public bool Grounded
    {
        get => _grounded == 1;
        set => _grounded = value ? 1 : 0;
    }

    public Vector3 Velocity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _velocityData;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _velocityData = value;
    }

    public float TargetSpeed
    {
        get => _targetSpeed;
        set => _targetSpeed = value;
    }

    public int JumpCount
    {
        get => _jumpCount;
        set => _jumpCount = value;
    }

    public float AnimSpeedMultiplier
    {
        get => _animSpeedMultiplier;
        set => _animSpeedMultiplier = value;
    }

    public float WaterAnchorYaw
    {
        get => _waterAnchorYaw;
        set => _waterAnchorYaw = value;
    }

    public float WaterYawOffset
    {
        get => _waterYawOffset;
        set => _waterYawOffset = value;
    }

    public bool WasInWater
    {
        get => _wasInWater == 1;
        set => _wasInWater = value ? 1 : 0;
    }
}