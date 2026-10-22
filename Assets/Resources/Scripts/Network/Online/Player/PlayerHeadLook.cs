using Fusion;
using UnityEngine;

public class PlayerHeadLook : NetworkBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private NetPlayerLook _playerLook;
    [SerializeField] private PlayerHeldItem _heldItem;

    [Range(0f, 1f)]
    [SerializeField] private float _headWeight = 0.7f;

    [Range(0f, 1f)]
    [SerializeField] private float _bodyWeightIdle = 0.3f;

    [Range(0f, 1f)]
    [SerializeField] private float _bodyWeightHolding = 0f;

    [Header("Arm Tilt (theo pitch camera)")]
    [SerializeField] private bool _tiltArmsWhenHolding = true;
    [SerializeField] private Transform _rightHandTarget; // để trống nếu dùng IK Humanoid trực tiếp
    [SerializeField] private Transform _leftHandTarget;
    [SerializeField] private float _armPitchMultiplier = 1f;
    [SerializeField] private float _armIkWeight = 1f;
    [SerializeField] private bool _invertArmTilt = false; // bật nếu tay xoay ngược hướng mong muốn

    private bool _isHoldingItem;

    private void OnEnable()
    {
        if (_heldItem != null)
            _heldItem.OnHeldItemChanged += HandleHeldItemChanged;
    }

    private void OnDisable()
    {
        if (_heldItem != null)
            _heldItem.OnHeldItemChanged -= HandleHeldItemChanged;
    }

    private void HandleHeldItemChanged(ItemData item)
    {
        _isHoldingItem = item != null;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (_animator == null || _playerLook == null)
            return;

        // Dùng CurrentPitch thay vì NetworkedPitch trực tiếp: với chính
        // người chơi (InputAuthority) giá trị này đọc tức thời từ local,
        // KHÔNG chờ network round-trip qua RPC -> tránh giật/lag khi xoay
        // camera trong lúc IK đang chỉnh tay cầm cần câu. Với máy khác nhìn
        // vào, CurrentPitch tự động fallback về NetworkedPitch đã replicate.
        float pitch = _playerLook.CurrentPitch;

        ApplyHeadLook(pitch);
        ApplyArmTilt(pitch);
    }

    private void ApplyHeadLook(float pitch)
    {
        Transform cameraHolder = _playerLook.CameraHolder;

        Quaternion pitchRotation = Quaternion.Euler(pitch, 0f, 0f);
        Vector3 lookDirection = transform.rotation * pitchRotation * Vector3.forward;
        Vector3 lookTarget = cameraHolder.position + lookDirection * 10f;

        float bodyWeight = _isHoldingItem ? _bodyWeightHolding : _bodyWeightIdle;

        _animator.SetLookAtWeight(
            1f,
            bodyWeight,
            _headWeight,
            0f,
            0.5f
        );

        _animator.SetLookAtPosition(lookTarget);
    }

    private void ApplyArmTilt(float pitch)
    {
        if (!_tiltArmsWhenHolding || !_isHoldingItem)
        {
            _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            return;
        }

        float signedPitch = _invertArmTilt ? -pitch : pitch;
        Quaternion tiltOffset = Quaternion.AngleAxis(signedPitch * _armPitchMultiplier, transform.right);

        ApplyHandTilt(AvatarIKGoal.RightHand, tiltOffset);
        ApplyHandTilt(AvatarIKGoal.LeftHand, tiltOffset);
    }

    private void ApplyHandTilt(AvatarIKGoal goal, Quaternion tiltOffset)
    {
        // Lấy pose tay hiện tại (đúng theo animation đang chạy) làm gốc,
        // rồi cộng thêm độ nghiêng theo pitch lên trên đó.
        Vector3 basePos = _animator.GetIKPosition(goal);
        Quaternion baseRot = _animator.GetIKRotation(goal);

        _animator.SetIKPositionWeight(goal, _armIkWeight);
        _animator.SetIKRotationWeight(goal, _armIkWeight);

        _animator.SetIKPosition(goal, basePos);
        _animator.SetIKRotation(goal, tiltOffset * baseRot);
    }
}