using Fusion;
using UnityEngine;

public class NetPlayerLook : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _cameraHolder;
    [SerializeField] private Camera _playerCamera;
    [SerializeField] private AudioListener _audioListener;

    [Header("Data")]
    [SerializeField] private PlayerLookData _data;

    [Header("Network Sync")]
    [Tooltip("Khoảng thời gian giữa các lần Client báo pitch lên StateAuthority.")]
    [SerializeField] private float _pitchSyncInterval = 0.05f;

    [Header("Head Bob")]
    [Tooltip("Tham chiếu tới movement script để đọc tốc độ/trạng thái grounded. " +
             "Nếu để trống, head bob sẽ bị tắt hoàn toàn.")]
    [SerializeField] private NetPlayerMovementFusion _movement;

    [Tooltip("Biên độ bob theo chiều DỌC (mét) khi ĐI BỘ bình thường.")]
    [SerializeField] private float _walkBobAmplitude = 0.025f;
    [Tooltip("Tần số bob (chu kỳ/giây) khi ĐI BỘ — số bước chân mỗi giây, xấp xỉ.")]
    [SerializeField] private float _walkBobFrequency = 1.8f;

    [Tooltip("Biên độ bob theo chiều DỌC (mét) khi CHẠY — nên lớn hơn walk rõ rệt.")]
    [SerializeField] private float _runBobAmplitude = 0.055f;
    [Tooltip("Tần số bob khi CHẠY — nhanh hơn walk vì bước chân dồn dập hơn.")]
    [SerializeField] private float _runBobFrequency = 2.6f;

    [Tooltip("Biên độ bob NGANG (trái/phải) — nhân với biên độ dọc, tạo hình " +
             "số 8 giống bước chân thật thay vì chỉ nhấp nhô lên xuống thẳng đứng.")]
    [SerializeField] [Range(0f, 1f)] private float _horizontalBobRatio = 0.5f;

    [Tooltip("Ngưỡng tốc độ (m/s) để coi là 'đang chạy' thay vì 'đang đi bộ' — " +
             "nên đặt giữa WalkSpeed và RunSpeed trong PlayerMovementData.")]
    [SerializeField] private float _runSpeedThreshold = 3.5f;

    [Tooltip("Thời gian mượt (giây) khi biên độ bob đổi giữa các trạng thái " +
             "(đứng yên/đi/chạy) — tránh bob nhảy cục ngay khi vừa bắt đầu " +
             "hoặc dừng di chuyển.")]
    [SerializeField] private float _bobAmplitudeSmoothTime = 0.15f;

    [Tooltip("Tốc độ (giây) camera trả về vị trí gốc khi dừng bob — để lớn " +
             "hơn 1 chút so với chu kỳ bob để không bị 'khựng' giữa chừng.")]
    [SerializeField] private float _bobReturnSmoothTime = 0.12f;

    public Transform CameraHolder => _cameraHolder;

    // Camera của player, expose ra ngoài để các script khác (ví dụ
    // FishInspectView) có thể tạm tách camera này ra khỏi CameraHolder để
    // đổi sang góc nhìn third-person, rồi gắn lại sau.
    public Camera PlayerCamera => _playerCamera;

    // Cờ static: true khi player LOCAL đang ở màn inspect cá. Dùng để tạm
    // dừng việc NetPlayerLook điều khiển look/head-bob (xem Update() dưới
    // đây) và (ở NetPlayerMovementFusion) tạm dừng xoay yaw + di chuyển
    // theo input, để góc camera đã tune không bị lệch khi người chơi lỡ
    // tay động chuột/WASD lúc đang ngắm cá.
    public static bool IsInspectingFish;

    [Networked]
    public float NetworkedPitch { get; set; }

    private float _cameraPitch;
    private float _lastPitchSyncTime;

    // Dùng riêng cho việc HIỂN THỊ pitch của người chơi KHÁC (không phải
    // input authority) — NetworkedPitch chỉ cập nhật rời rạc theo tick/RPC
    // (~20Hz, xem _pitchSyncInterval), nếu PlayerHeadLook đọc thẳng giá trị
    // đó thì đầu người chơi khác sẽ "nhảy cóc" theo từng bước thay vì xoay
    // mượt mỗi frame -> làm mượt dần tới NetworkedPitch trong Render() bên
    // dưới rồi mới cho đọc qua CurrentPitch.
    private float _remoteDisplayPitch;
    private float _remotePitchVelocity;

    // --- Head bob state ---
    private float _bobTimer;
    private float _currentBobAmplitude;
    private float _bobAmplitudeVelocity;
    private Vector3 _currentBobOffset;
    private Vector3 _bobOffsetVelocity;
    private Vector3 _cameraHolderBasePos;

    private Coroutine _cameraWatchdogCoroutine;

    public float CurrentPitch => Object.HasInputAuthority ? _cameraPitch : _remoteDisplayPitch;

    // Bật/tắt layer PlayerHidden trong cullingMask của camera local — để
    // camera nhìn thấy chính thân mình khi đang ở góc third-person inspect
    // (bình thường layer này bị loại ra để tránh clip vào đầu ở first-person,
    // xem Spawned() bên dưới).
    public void SetBodyRenderVisible(bool visible)
    {
        if (_playerCamera == null)
            return;

        int hiddenLayer = LayerMask.NameToLayer("PlayerHidden");
        int selfHiddenGearLayer = LayerMask.NameToLayer("SelfHiddenGear");

        if (hiddenLayer >= 0)
        {
            if (visible)
                _playerCamera.cullingMask |= (1 << hiddenLayer);
            else
                _playerCamera.cullingMask &= ~(1 << hiddenLayer);
        }

        if (selfHiddenGearLayer >= 0)
        {
            if (visible)
                _playerCamera.cullingMask |= (1 << selfHiddenGearLayer);
            else
                _playerCamera.cullingMask &= ~(1 << selfHiddenGearLayer);
        }
    }

    public override void Spawned()
    {
        bool isLocal = Object.HasInputAuthority;

        Debug.LogWarning($"[NetPlayerLook] SPAWNED chạy trên object '{gameObject.name}'. " +
                          $"isLocal={isLocal}, InputAuthority={Object.InputAuthority}, " +
                          $"LocalPlayer={Runner.LocalPlayer}, IsServer={Runner.IsServer}, " +
                          $"Time={Time.time:F2}");

        int hiddenLayer = LayerMask.NameToLayer("PlayerHidden");
        int defaultLayer = LayerMask.NameToLayer("Default");

        if (_playerCamera != null)
        {
            _playerCamera.enabled = isLocal;
            _playerCamera.gameObject.SetActive(isLocal);

            if (isLocal)
            {
                if (hiddenLayer >= 0)
                {
                    _playerCamera.cullingMask &= ~(1 << hiddenLayer);
                }

                DoorInteractable.RegisterLocalPlayer(transform, _playerCamera);

                StartCoroutine(NotifyLoadingCameraReadyWhenAvailable());

                if (_cameraWatchdogCoroutine != null)
                    StopCoroutine(_cameraWatchdogCoroutine);

                _cameraWatchdogCoroutine = StartCoroutine(CameraWatchdogRoutine());
            }
        }
        else
        {
            Debug.LogError("[NetPlayerLook] _playerCamera chưa được gán trong Inspector!");
        }

        if (!isLocal && hiddenLayer >= 0)
        {
            AutoResetHiddenLayer(transform, hiddenLayer, defaultLayer);
        }

        if (_audioListener != null)
        {
            _audioListener.enabled = isLocal;
        }

        // Lưu vị trí gốc của cameraHolder để head bob cộng offset lên trên,
        // không ghi đè cứng — tránh xung đột nếu sau này có script khác
        // cũng dịch chuyển camera holder (ví dụ lean/crouch).
        if (_cameraHolder != null)
            _cameraHolderBasePos = _cameraHolder.localPosition;

        // Khởi tạo pitch hiển thị = pitch network hiện tại ngay từ đầu, tránh
        // bị Snap/giật từ 0 về giá trị thật ở lần Render() đầu tiên.
        _remoteDisplayPitch = NetworkedPitch;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_cameraWatchdogCoroutine != null)
        {
            StopCoroutine(_cameraWatchdogCoroutine);
            _cameraWatchdogCoroutine = null;
        }
    }

    public override void Render()
    {
        if (Object.HasInputAuthority)
            return;

        // Làm mượt pitch nhận từ mạng trước khi PlayerHeadLook đọc qua
        // CurrentPitch — NetworkedPitch đổi theo bước rời rạc (~mỗi
        // _pitchSyncInterval), SmoothDamp ở đây khiến đầu người chơi khác
        // xoay liên tục thay vì khựng theo từng nấc. Áp dụng cho MỌI người
        // xem (Host lẫn Client) miễn không phải input authority của chính
        // object đó. smoothTime ~ khoảng cách giữa 2 lần cập nhật để bắt
        // kịp mà không trễ quá đà — tăng lên nếu vẫn thấy khựng, giảm
        // xuống nếu thấy trễ so với chuyển động thật.
        _remoteDisplayPitch = Mathf.SmoothDampAngle(
            _remoteDisplayPitch, NetworkedPitch,
            ref _remotePitchVelocity, _pitchSyncInterval * 2f
        );
    }

    private System.Collections.IEnumerator CameraWatchdogRoutine()
    {
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForSeconds(0.2f);

            if (_playerCamera == null)
                yield break;

            if (!_playerCamera.enabled || !_playerCamera.gameObject.activeSelf)
            {
                Debug.LogError("[NetPlayerLook] Camera local player bị TẮT sau khi Spawned()! " +
                                "Có khả năng bị object/spawner khác ghi đè. Đang bật lại.");
                _playerCamera.enabled = true;
                _playerCamera.gameObject.SetActive(true);
            }
        }

        _cameraWatchdogCoroutine = null;
    }

    private System.Collections.IEnumerator NotifyLoadingCameraReadyWhenAvailable()
    {
        int maxRetries = 60;
        int retries = 0;

        while (LoadingCameraHandler.Instance == null && retries < maxRetries)
        {
            retries++;
            yield return null;
        }

        if (LoadingCameraHandler.Instance != null)
        {
            LoadingCameraHandler.Instance.OnPlayerCameraReady();
            Debug.LogWarning("[NetPlayerLook] Đã gọi OnPlayerCameraReady()");
        }
        else
        {
            Debug.LogWarning("[NetPlayerLook] LoadingCameraHandler.Instance vẫn NULL sau khi chờ! Kiểm tra lại scene S-Gameplay có object LoadingCameraHandler không.");
        }
    }

    private void AutoResetHiddenLayer(Transform root, int hiddenLayer, int defaultLayer)
    {
        var allChildren = root.GetComponentsInChildren<Transform>(true);

        foreach (var t in allChildren)
        {
            if (t.gameObject.layer == hiddenLayer)
            {
                t.gameObject.layer = defaultLayer;
            }
        }
    }

    private void Update()
    {
        if (!Object.HasInputAuthority)
            return;

        if (LobbyChatUI.IsTyping || PlayerCursor.IsUIOpen)
            return;

        float mouseY = Input.GetAxis("Mouse Y") * _data.MouseSensitivity;

        _cameraPitch -= mouseY;
        _cameraPitch = Mathf.Clamp(_cameraPitch, _data.MinLookAngle, _data.MaxLookAngle);
        _cameraHolder.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);

        UpdateHeadBob();

        if (Object.HasStateAuthority)
        {
            NetworkedPitch = _cameraPitch;
            return;
        }

        if (Time.time - _lastPitchSyncTime >= _pitchSyncInterval)
        {
            _lastPitchSyncTime = Time.time;
            RPC_ReportPitch(_cameraPitch);
        }
    }

    /// Tính và áp dụng head bob lên _cameraHolder.localPosition mỗi frame.
    /// Chỉ bob khi đang grounded và có tốc độ di chuyển — đứng yên/nhảy/rơi
    /// tự do đều tự động không bob (biên độ mượt về 0).
    private void UpdateHeadBob()
    {
        if (_cameraHolder == null || _movement == null)
            return;

        float speed = _movement.CurrentSpeed;
        bool isMoving = _movement.Grounded && speed > 0.1f;

        // Trộn biên độ/tần số giữa walk và run theo tỉ lệ tốc độ hiện tại so
        // với ngưỡng chạy — mượt hơn là chuyển đột ngột giữa 2 bộ số.
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
            // Timer chỉ chạy khi đang thực sự di chuyển — đứng yên thì giữ
            // nguyên pha, tránh bob "nhảy cóc" pha khi bắt đầu đi lại.
            if (isMoving)
                _bobTimer += Time.deltaTime * frequency * Mathf.PI * 2f;

            // Trục dọc: sin thường (lên xuống đều).
            // Trục ngang: sin tần số MỘT NỬA trục dọc -> tạo quỹ đạo hình số 8
            // giống trọng tâm cơ thể dịch qua lại theo từng bước chân thật,
            // thay vì chỉ nhấp nhô thẳng đứng trông máy móc.
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

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_ReportPitch(float pitch)
    {
        NetworkedPitch = pitch;
    }
}