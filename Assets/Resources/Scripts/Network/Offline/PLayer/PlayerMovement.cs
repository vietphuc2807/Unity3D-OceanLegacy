using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private PlayerMovementData _data;

    [Header("Animation")]
    [SerializeField] private Animator _animator;
    [SerializeField] private float _animationSmoothTime = 0.1f;

    [Header("Sound")]
    [SerializeField] private PlayerFootstepSound _footstepSound;
    [Tooltip("Phai o tren khong it nhat chung nay (giay) thi moi phat tieng tiep dat, tranh keu khi di qua bac thang/mep nho.")]
    [SerializeField] private float _minAirTimeForLandSound = 0.15f;

    private CharacterController _characterController;
    private Vector3 _velocity;

    private float _currentSpeed;
    private float _speedVelocity;

    private bool _isGrounded;

    // Theo doi trang thai tiep dat de phat tieng Land
    private bool _wasGrounded = true;
    private float _airTime;

    // --- Thêm cho Head Bob (tốc độ di chuyển ngang thực tế, m/s) ---
    private float _planarSpeed;

    public float CurrentSpeed => _planarSpeed;
    public bool Grounded => _isGrounded;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private static readonly int JumpHash = Animator.StringToHash("Jump");

    private static readonly int GroundedHash = Animator.StringToHash("Grounded");

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();

        if (_footstepSound == null && _animator != null)
            _footstepSound = _animator.GetComponent<PlayerFootstepSound>();
    }

    private void Update()
    {
        _isGrounded = _characterController.isGrounded;

        HandleMovement();
        HandleJump();
        ApplyGravity();

        _isGrounded = _characterController.isGrounded;

        _animator.SetBool(GroundedHash, _isGrounded);

        HandleLandSound();
    }

    private void HandleMovement()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        Vector3 moveDirection =
            transform.right * moveX +
            transform.forward * moveZ;

        moveDirection.Normalize();

        bool isMoving = moveDirection.sqrMagnitude > 0.01f;

        float targetSpeed = 0f;

        if (isMoving)
        {
            targetSpeed = Input.GetKey(KeyCode.LeftShift)
                ? 1f
                : 0.5f;
        }

        _currentSpeed = Mathf.SmoothDamp(
            _currentSpeed,
            targetSpeed,
            ref _speedVelocity,
            _animationSmoothTime);

        _animator.SetFloat(SpeedHash, _currentSpeed);

        float moveSpeed = Input.GetKey(KeyCode.LeftShift)
            ? _data.RunSpeed
            : _data.WalkSpeed;

        // Tốc độ ngang thực tế (m/s) để phục vụ Head Bob — 0 khi đứng yên,
        // bằng WalkSpeed/RunSpeed khi đang di chuyển.
        _planarSpeed = isMoving ? moveSpeed : 0f;

        _characterController.Move(
            moveDirection * moveSpeed * Time.deltaTime);
    }

    private void HandleJump()
    {
        if (_isGrounded && Input.GetKeyDown(KeyCode.Space))
        {
            _velocity.y = Mathf.Sqrt(_data.JumpHeight * -2f * _data.Gravity);

            _animator.SetTrigger(JumpHash);

            // Bo dong nay neu ban gan Animation Event Jump tren clip Jump
            // (tranh phat 2 lan).
            if (_footstepSound != null)
                _footstepSound.Jump();
        }
    }

    private void ApplyGravity()
    {
        if (_isGrounded && _velocity.y < 0f)
        {
            _velocity.y = -2f; 
        }

        _velocity.y += _data.Gravity * Time.deltaTime;

        _characterController.Move(_velocity * Time.deltaTime);
    }

    private void HandleLandSound()
    {
        if (!_isGrounded)
        {
            _airTime += Time.deltaTime;
        }
        else
        {
            // Vua cham dat sau khi o tren khong du lau
            if (!_wasGrounded && _airTime >= _minAirTimeForLandSound
                && _footstepSound != null)
            {
                _footstepSound.Land();
            }

            _airTime = 0f;
        }

        _wasGrounded = _isGrounded;
    }

    public void StopMovementAnimation()
    {
        _currentSpeed = 0f;
        _speedVelocity = 0f;

        _velocity = Vector3.zero;

        if (_animator != null)
        {
            _animator.SetFloat(SpeedHash, 0f);
        }
    }
}