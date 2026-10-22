using UnityEngine;

[DefaultExecutionOrder(1000)]
public class FishingCastCameraEffect : MonoBehaviour
{
    [SerializeField] private Transform _cameraTransform;

    [Header("Debug")]
    [SerializeField] private bool _debugLog = true;

    [Header("Cast Dip")]
    [SerializeField] private float _dipDuration = 0.35f;
    [SerializeField] private float _dipTiltAngle = 4f;
    [SerializeField] private Vector3 _dipOffset = new Vector3(0f, -0.008f, 0.03f);
    [SerializeField] private float _recoverSpringK = 28f;
    [SerializeField] private float _recoverSpringDamping = 10f;
    [SerializeField] private float _settleThreshold = 0.001f;
    [SerializeField] private float _minDipBlend = 0.05f;
    [SerializeField] private float _maxDipBlend = 0.45f;

    [Header("Charge Tilt")]
    [SerializeField] private float _chargeTiltAngle = 10f;
    [SerializeField] private float _chargeTiltSmooth = 0.2f;

    private Vector3 _basePos;
    private Quaternion _baseRot;

    private enum Phase { Idle, Dipping, Recovering }
    private Phase _phase = Phase.Idle;

    private float _dipTimer;
    private float _dipTargetBlend;
    private float _currentBlend;
    private float _recoverVelocity;

    private float _currentChargeTilt;
    private float _chargeTiltVelocity;

    private bool _hasCachedBasePose;

    private void Awake()
    {
        if (_debugLog)
            Debug.Log($"[CamFX] Awake() on '{gameObject.name}', activeInHierarchy={gameObject.activeInHierarchy}, enabled={enabled}");

        CacheBasePose();
    }

    private void OnEnable()
    {
        if (_debugLog)
            Debug.Log($"[CamFX] OnEnable() on '{gameObject.name}'");

        CacheBasePose();
    }

    private void OnDisable()
    {
        // Nếu log này xuất hiện trên client mà không có action nào từ người
        // chơi (vd ngay sau khi Spawned), gần như chắc chắn có script khác
        // đang tắt GameObject/Component này.
        if (_debugLog)
            Debug.LogWarning($"[CamFX] OnDisable() on '{gameObject.name}' — component/GameObject vừa bị TẮT. " +
                              "Nếu đây không phải chủ đích, có script khác đang disable nhầm object này.");
    }

    private void CacheBasePose()
    {
        if (_cameraTransform == null)
        {
            if (_debugLog)
                Debug.LogWarning("[CamFX] _cameraTransform is NULL at CacheBasePose().");
            return;
        }

        _basePos = _cameraTransform.localPosition;
        _baseRot = _cameraTransform.localRotation;
        _hasCachedBasePose = true;

        if (_debugLog)
        {
            Debug.Log($"[CamFX] Cached base pose. cam='{_cameraTransform.name}' " +
                      $"instanceID={_cameraTransform.GetInstanceID()}, " +
                      $"camActive={_cameraTransform.gameObject.activeInHierarchy}, " +
                      $"basePos={_basePos}, baseRot={_baseRot.eulerAngles}");
        }
    }

    private void LateUpdate()
    {
        if (_cameraTransform == null)
            return;

        // FIX: đang ở góc inspect cá — FishInspectView đã tách camera ra
        // khỏi CameraHolder và tự set localPosition/localRotation riêng
        // (offset _positionOffset/_rotationOffsetEuler của nó) mỗi frame
        // trong Update(). Nếu không guard ở đây, LateUpdate này vẫn chạy
        // và ghi đè cameraTransform.localPosition/localRotation theo
        // _basePos/_baseRot (dù _phase = Idle thì blend = 0, finalPos vẫn
        // = _basePos, không phải giá trị inspect) — khiến góc inspect
        // không hiển thị được dù FishInspectView đã set đúng.
        if (NetPlayerLook.IsInspectingFish)
            return;

        switch (_phase)
        {
            case Phase.Dipping:
                _dipTimer += Time.deltaTime;
                float t = _dipDuration > 0f ? Mathf.Clamp01(_dipTimer / _dipDuration) : 1f;
                float eased = Mathf.SmoothStep(0f, 1f, t);
                _currentBlend = eased * _dipTargetBlend;

                if (t >= 1f)
                {
                    _phase = Phase.Recovering;
                    _recoverVelocity = 0f;
                }
                break;

            case Phase.Recovering:
                float acc = -_recoverSpringK * _currentBlend - _recoverSpringDamping * _recoverVelocity;
                _recoverVelocity += acc * Time.deltaTime;
                _currentBlend += _recoverVelocity * Time.deltaTime;

                if (Mathf.Abs(_currentBlend) < _settleThreshold && Mathf.Abs(_recoverVelocity) < _settleThreshold)
                {
                    _currentBlend = 0f;
                    _recoverVelocity = 0f;
                    _phase = Phase.Idle;
                }
                break;
        }

        _currentChargeTilt = Mathf.SmoothDamp(_currentChargeTilt, 0f, ref _chargeTiltVelocity, _chargeTiltSmooth);

        Vector3 castPosOffset = _dipOffset * _currentBlend;
        float castTiltOffset = _dipTiltAngle * _currentBlend;

        Vector3 finalPos = _basePos + castPosOffset;
        Quaternion finalRot = _baseRot * Quaternion.Euler(castTiltOffset + _currentChargeTilt, 0f, 0f);

        _cameraTransform.localPosition = finalPos;
        _cameraTransform.localRotation = finalRot;

        if (_debugLog && (_currentBlend != 0f || Mathf.Abs(_currentChargeTilt) > 0.001f))
        {
            Debug.Log($"[CamFX] LateUpdate applying blend={_currentBlend:F3}, tilt={_currentChargeTilt:F2}, finalLocalPos={finalPos}");
        }
    }

    public void UpdateChargeTilt(float power)
    {
        // FIX-DEBUG: log đầy đủ trạng thái enable/active NGAY TẠI thời điểm
        // gọi. Nếu enabled=false hoặc activeInHierarchy=false ở đây, nghĩa là
        // method vẫn được gọi thành công (Unity cho phép) nhưng Awake/OnEnable/
        // LateUpdate của chính script này KHÔNG chạy -> hiệu ứng không hiển
        // thị dù log "called" vẫn xuất hiện bình thường.
        if (_debugLog)
        {
            Debug.Log($"[CamFX] UpdateChargeTilt() called, power={power:F2}, " +
                      $"camAssigned={_cameraTransform != null}, " +
                      $"thisEnabled={enabled}, thisActiveInHierarchy={gameObject.activeInHierarchy}, " +
                      $"hasCachedBasePose={_hasCachedBasePose}");
        }

        // FIX: nếu vì lý do nào đó component đang bị disable (nên LateUpdate
        // không chạy), tự ép bật lại để hiệu ứng có cơ hội hiển thị thay vì
        // âm thầm không làm gì. Đây là lưới an toàn — vẫn cần tìm ra ai đã
        // tắt nó nếu log OnDisable() phía trên có xuất hiện.
        if (!enabled)
        {
            Debug.LogWarning("[CamFX] Component đang bị disable khi UpdateChargeTilt() được gọi — tự động bật lại.");
            enabled = true;
        }

        if (!_hasCachedBasePose)
            CacheBasePose();

        float target = power * _chargeTiltAngle;
        _currentChargeTilt = Mathf.SmoothDamp(_currentChargeTilt, target, ref _chargeTiltVelocity, _chargeTiltSmooth);
    }

    public void ReleaseCast(float power01)
    {
        if (_debugLog)
        {
            Debug.Log($"[CamFX] ReleaseCast() called, power01={power01:F2}, " +
                      $"camAssigned={_cameraTransform != null}, " +
                      $"thisEnabled={enabled}, thisActiveInHierarchy={gameObject.activeInHierarchy}, " +
                      $"hasCachedBasePose={_hasCachedBasePose}");
        }

        if (!enabled)
        {
            Debug.LogWarning("[CamFX] Component đang bị disable khi ReleaseCast() được gọi — tự động bật lại.");
            enabled = true;
        }

        if (!_hasCachedBasePose)
            CacheBasePose();

        _dipTargetBlend = Mathf.Lerp(_minDipBlend, _maxDipBlend, power01);
        _dipTimer = 0f;
        _phase = Phase.Dipping;
        _currentChargeTilt = 0f;
        _chargeTiltVelocity = 0f;
    }
}