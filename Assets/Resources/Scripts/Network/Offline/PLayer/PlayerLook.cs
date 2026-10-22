using UnityEngine;

public class PlayerLook : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _cameraHolder;

    [Header("Data")]
    [SerializeField] private PlayerLookData _data;

    [Header("Head Bob")]
    [Tooltip("Tham chieu toi movement script de doc toc do/trang thai grounded. " +
             "Neu de trong, head bob se bi tat hoan toan.")]
    [SerializeField] private PlayerMovement _movement;

    [Tooltip("Bien do bob theo chieu DOC (met) khi DI BO binh thuong.")]
    [SerializeField] private float _walkBobAmplitude = 0.025f;
    [Tooltip("Tan so bob (chu ky/giay) khi DI BO - so buoc chan moi giay, xap xi.")]
    [SerializeField] private float _walkBobFrequency = 1.8f;

    [Tooltip("Bien do bob theo chieu DOC (met) khi CHAY - nen lon hon walk ro ret.")]
    [SerializeField] private float _runBobAmplitude = 0.055f;
    [Tooltip("Tan so bob khi CHAY - nhanh hon walk vi buoc chan don dap hon.")]
    [SerializeField] private float _runBobFrequency = 2.6f;

    [Tooltip("Bien do bob NGANG (trai/phai) - nhan voi bien do doc, tao hinh " +
             "so 8 giong buoc chan that thay vi chi nhap nho len xuong thang dung.")]
    [SerializeField] [Range(0f, 1f)] private float _horizontalBobRatio = 0.5f;

    [Tooltip("Nguong toc do (m/s) de coi la 'dang chay' thay vi 'dang di bo' - " +
             "nen dat giua WalkSpeed va RunSpeed trong PlayerMovementData.")]
    [SerializeField] private float _runSpeedThreshold = 3.5f;

    [Tooltip("Thoi gian muot (giay) khi bien do bob doi giua cac trang thai " +
             "(dung yen/di/chay) - tranh bob nhay cuc ngay khi vua bat dau " +
             "hoac dung di chuyen.")]
    [SerializeField] private float _bobAmplitudeSmoothTime = 0.15f;

    [Tooltip("Toc do (giay) camera tra ve vi tri goc khi dung bob - de lon " +
             "hon 1 chut so voi chu ky bob de khong bi 'khung' giua chung.")]
    [SerializeField] private float _bobReturnSmoothTime = 0.12f;

    private float _cameraPitch;

    // --- Head bob state ---
    private float _bobTimer;
    private float _currentBobAmplitude;
    private float _bobAmplitudeVelocity;
    private Vector3 _currentBobOffset;
    private Vector3 _bobOffsetVelocity;
    private Vector3 _cameraHolderBasePos;

    private void Start()
    {
        // Luu vi tri goc cua cameraHolder de head bob cong offset len tren,
        // khong ghi de cung - tranh xung dot neu sau nay co script khac
        // cung dich chuyen camera holder (VD lean/crouch).
        if (_cameraHolder != null)
            _cameraHolderBasePos = _cameraHolder.localPosition;
    }

    private void Update()
    {
        HandleLook();
        UpdateHeadBob();
    }

    private void HandleLook()
    {
        float mouseX =
            Input.GetAxis("Mouse X") *
            _data.MouseSensitivity;

        float mouseY =
            Input.GetAxis("Mouse Y") *
            _data.MouseSensitivity;

        _cameraPitch -= mouseY;

        _cameraPitch = Mathf.Clamp(
            _cameraPitch,
            _data.MinLookAngle,
            _data.MaxLookAngle);

        _cameraHolder.localRotation =
            Quaternion.Euler(_cameraPitch, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }

    /// Tinh va ap dung head bob len _cameraHolder.localPosition moi frame.
    /// Chi bob khi dang grounded va co toc do di chuyen - dung yen/nhay/roi
    /// tu do deu tu dong khong bob (bien do muot ve 0).
    private void UpdateHeadBob()
    {
        if (_cameraHolder == null || _movement == null)
            return;

        float speed = _movement.CurrentSpeed;
        bool isMoving = _movement.Grounded && speed > 0.1f;

        // Tron bien do/tan so giua walk va run theo ti le toc do hien tai so
        // voi nguong chay - muot hon la chuyen dot ngot giua 2 bo so.
        float runBlend = Mathf.Clamp01((speed - 0f) / Mathf.Max(0.01f, _runSpeedThreshold));
        float targetAmplitude = isMoving
            ? Mathf.Lerp(_walkBobAmplitude, _runBobAmplitude, runBlend)
            : 0f;
        float frequency = Mathf.Lerp(_walkBobFrequency, _runBobFrequency, runBlend);

        _currentBobAmplitude = Mathf.SmoothDamp(
            _currentBobAmplitude, targetAmplitude,
            ref _bobAmplitudeVelocity, _bobAmplitudeSmoothTime
        );

        Vector3 targetOffset;

        if (_currentBobAmplitude > 0.0001f)
        {
            // Timer chi chay khi dang thuc su di chuyen - dung yen thi giu
            // nguyen pha, tranh bob "nhay coc" pha khi bat dau di lai.
            if (isMoving)
                _bobTimer += Time.deltaTime * frequency * Mathf.PI * 2f;

            // Truc doc: sin thuong (len xuong deu).
            // Truc ngang: sin tan so MOT NUA truc doc -> tao quy dao hinh so 8
            // giong trong tam co the dich qua lai theo tung buoc chan that,
            // thay vi chi nhap nho thang dung trong may moc.
            float vertical = Mathf.Sin(_bobTimer) * _currentBobAmplitude;
            float horizontal = Mathf.Sin(_bobTimer * 0.5f) * _currentBobAmplitude * _horizontalBobRatio;

            targetOffset = new Vector3(horizontal, vertical, 0f);
        }
        else
        {
            targetOffset = Vector3.zero;
        }

        _currentBobOffset = Vector3.SmoothDamp(
            _currentBobOffset, targetOffset,
            ref _bobOffsetVelocity, _bobReturnSmoothTime
        );

        _cameraHolder.localPosition = _cameraHolderBasePos + _currentBobOffset;
    }
}