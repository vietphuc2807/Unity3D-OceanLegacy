using Fusion;
using UnityEngine;
using TMPro;

// Object cửa cần có NetworkObject component.
// Scene Object -> State Authority mặc định thuộc về Host/Server,
// không cần Player spawn nó.
public class DoorInteractable : NetworkBehaviour
{
    [Header("Detection")]
    [SerializeField] private float _interactRange = 3f;
    [SerializeField] private float _lookAngleThreshold = 30f;

    [Header("Look Target")]
    [Tooltip("Collider bao trùm TOÀN BỘ cánh cửa. Nếu gán cái này, " +
             "script sẽ dùng ClosestPoint để tính điểm gần nhất trên bề mặt cửa " +
             "theo hướng camera đang nhìn.")]
    [SerializeField] private Collider _doorCollider;

    [Tooltip("Chỉ dùng khi KHÔNG gán Door Collider ở trên.")]
    [SerializeField] private Transform _lookTargetPoint;

    [SerializeField] private float _fallbackTargetHeight = 1.2f;

    [Header("UI References")]
    [SerializeField] private GameObject _interactPromptUI;
    [SerializeField] private TextMeshProUGUI _promptText;

    [SerializeField] private string _openText = "OPEN THE DOOR";
    [SerializeField] private string _closeText = "CLOSE THE DOOR";

    [Header("Door")]
    [SerializeField] private Transform _doorPivot;
    [SerializeField] private float _openAngle = 90f;
    [SerializeField] private float _rotateSpeed = 4f;

    [Header("Sound (3D)")]
    [Tooltip("Kéo THẲNG file âm thanh mở cửa vào đây (không cần qua " +
             "SoundManager, vì SoundManager phát 2D).")]
    [SerializeField] private AudioClip _openClip;

    [Tooltip("Kéo THẲNG file âm thanh đóng cửa vào đây. Có thể dùng " +
             "chung 1 clip với mở cửa.")]
    [SerializeField] private AudioClip _closeClip;

    [Tooltip("(Tuỳ chọn) AudioSource có sẵn trên cửa. Để trống = script " +
             "tự tạo 1 AudioSource 3D trên chính object này.")]
    [SerializeField] private AudioSource _audioSource;

    [Range(0f, 1f)]
    [SerializeField] private float _volume = 1f;

    [Tooltip("Trong khoảng này âm thanh to tối đa.")]
    [SerializeField] private float _minDistance = 2f;

    [Tooltip("Ngoài khoảng này KHÔNG nghe thấy gì (rolloff Linear).")]
    [SerializeField] private float _maxDistance = 15f;

    [Tooltip("BẬT: âm lượng nhân thêm với thanh SFX trong Settings.")]
    [SerializeField] private bool _followSfxVolume = true;

    [Tooltip("Bỏ qua tiếng trong khoảng thời gian này ngay sau khi cửa " +
             "spawn, tránh phát nhầm khi vừa vào scene và đồng bộ trạng " +
             "thái cửa từ mạng.")]
    [SerializeField] private float _ignoreSoundAfterSpawnSeconds = 0.5f;

    // ============================================================
    // NETWORK STATE
    // ============================================================

    [Networked, OnChangedRender(nameof(OnIsOpenChanged))]
    private NetworkBool IsOpen { get; set; }

    /// <summary>
    /// True khi cửa đang MỞ. Dùng bởi GameDayManager để biết đã đóng
    /// cửa hay chưa trước khi cho phép qua ngày mới
    /// (xem GameDayManager.CheckReadyToEndDay()).
    /// </summary>
    public bool IsDoorOpen => IsOpen;

    // Đánh dấu Fusion đã gọi Spawned() hay chưa.
    private bool _isSpawned;

    private float _spawnTime;

    // ============================================================
    // LOCAL REFERENCES
    // ============================================================

    private Transform _playerTransform;
    private Camera _playerCamera;

    private bool _isInRange;

    private Quaternion _closedRot;
    private Quaternion _openRot;

    // ============================================================
    // FUSION SPAWN
    // ============================================================

    public override void Spawned()
    {
        _isSpawned = true;

        _spawnTime = Time.time;

        SetupAudioSource();

        if (_interactPromptUI != null)
            _interactPromptUI.SetActive(false);

        if (_doorPivot != null)
        {
            _closedRot = _doorPivot.localRotation;

            _openRot =
                _closedRot *
                Quaternion.Euler(0f, _openAngle, 0f);

            _doorPivot.localRotation =
                IsOpen ? _openRot : _closedRot;
        }

        UpdatePromptText();
    }

    // ============================================================
    // REGISTER LOCAL PLAYER
    // ============================================================

    public static void RegisterLocalPlayer(
        Transform playerTransform,
        Camera camera)
    {
        var allDoors =
            FindObjectsByType<DoorInteractable>(
                FindObjectsSortMode.None);

        foreach (var door in allDoors)
        {
            door._playerTransform = playerTransform;
            door._playerCamera = camera;
        }
    }

    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        // Không được truy cập Networked Property
        // trước khi Fusion gọi Spawned().
        if (!_isSpawned)
            return;

        AnimateDoor();

        if (_playerTransform == null || _playerCamera == null)
            return;

        // Co UI khac dang mo toan man hinh (VD DayStatsPanel, BaitShopUI,
        // RodUpgradeUI...) - AN HAN prompt va KHONG nhan phim "E", tranh
        // truong hop nguoi choi bam E de confirm UI kia nhung lai vo tinh
        // mo/dong luon ca cua neu dang dung gan/nhin ve phia cua.
        if (PlayerCursor.IsUIOpen)
        {
            if (_isInRange)
            {
                _isInRange = false;

                if (_interactPromptUI != null)
                    _interactPromptUI.SetActive(false);
            }

            return;
        }

        Vector3 targetPos;

        if (_doorCollider != null)
        {
            targetPos =
                _doorCollider.ClosestPoint(
                    _playerCamera.transform.position);
        }
        else
        {
            targetPos = GetLookTargetPosition();
        }

        float dist =
            Vector3.Distance(
                transform.position,
                _playerTransform.position);

        bool inRange =
            dist <= _interactRange;

        bool lookingAt = false;

        if (inRange)
        {
            Vector3 toDoor =
                (
                    targetPos -
                    _playerCamera.transform.position
                ).normalized;

            float angle =
                Vector3.Angle(
                    _playerCamera.transform.forward,
                    toDoor);

            lookingAt =
                angle <= _lookAngleThreshold;
        }

        bool shouldShowPrompt =
            inRange && lookingAt;

        if (shouldShowPrompt != _isInRange)
        {
            _isInRange = shouldShowPrompt;

            if (_interactPromptUI != null)
            {
                _interactPromptUI.SetActive(
                    _isInRange);
            }
        }

        if (_isInRange &&
            Input.GetKeyDown(KeyCode.E))
        {
            RPC_RequestToggle();
        }
    }

    // ============================================================
    // DOOR ANIMATION
    // ============================================================

    private void AnimateDoor()
    {
        if (_doorPivot == null)
            return;

        Quaternion target =
            IsOpen
                ? _openRot
                : _closedRot;

        _doorPivot.localRotation =
            Quaternion.Slerp(
                _doorPivot.localRotation,
                target,
                Time.deltaTime * _rotateSpeed);
    }

    // ============================================================
    // RPC
    // ============================================================

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority)]
    private void RPC_RequestToggle()
    {
        IsOpen = !IsOpen;
    }

    // ============================================================
    // NETWORKED STATE CHANGED
    // ============================================================

    private void OnIsOpenChanged()
    {
        if (!_isSpawned)
            return;

        UpdatePromptText();

        PlayDoorSound();
    }

    // ============================================================
    // DOOR SOUND
    // ============================================================
    //
    // OnChangedRender chạy trên MỌI máy mỗi khi IsOpen đổi, nên tất cả
    // người chơi (kể cả người nhấn E) đều nghe tiếng cùng lúc, đúng lúc
    // trạng thái cửa thực sự đổi.
    // ============================================================

    private void SetupAudioSource()
    {
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.playOnAwake = false;
        _audioSource.loop = false;

        // 3D: càng xa càng nhỏ, vượt _maxDistance thì im hẳn.
        _audioSource.spatialBlend = 1f;
        _audioSource.rolloffMode = AudioRolloffMode.Linear;
        _audioSource.minDistance = _minDistance;
        _audioSource.maxDistance = _maxDistance;
        _audioSource.dopplerLevel = 0f;
    }

    private void PlayDoorSound()
    {
        // Vừa spawn / vừa đồng bộ trạng thái ban đầu từ mạng -> không
        // phát, vì chưa ai mở hay đóng cả.
        if (Time.time - _spawnTime < _ignoreSoundAfterSpawnSeconds)
            return;

        if (_audioSource == null)
            return;

        AudioClip clip =
            IsOpen
                ? _openClip
                : _closeClip;

        if (clip == null)
            return;

        float volume = _volume;

        if (_followSfxVolume && SoundManager.Instance != null)
            volume *= SoundManager.Instance.GetSFXVolume();

        // Master volume tự áp qua AudioListener.volume.
        _audioSource.PlayOneShot(clip, volume);
    }

    // ============================================================
    // PROMPT TEXT
    // ============================================================

    private void UpdatePromptText()
    {
        if (_promptText == null)
            return;

        _promptText.text =
            IsOpen
                ? _closeText
                : _openText;
    }

    // ============================================================
    // LOOK TARGET FALLBACK
    // ============================================================

    private Vector3 GetLookTargetPosition()
    {
        if (_lookTargetPoint != null)
            return _lookTargetPoint.position;

        return transform.position +
               Vector3.up * _fallbackTargetHeight;
    }

    // ============================================================
    // GIZMOS
    // ============================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            _interactRange);

        if (_doorCollider != null)
        {
            Gizmos.color =
                new Color(
                    0f,
                    1f,
                    1f,
                    0.3f);

            Gizmos.matrix =
                _doorCollider.transform.localToWorldMatrix;

            if (_doorCollider is BoxCollider box)
            {
                Gizmos.DrawCube(
                    box.center,
                    box.size);
            }

            Gizmos.matrix =
                Matrix4x4.identity;
        }
        else
        {
            Gizmos.color = Color.cyan;

            Vector3 targetPos =
                _lookTargetPoint != null
                    ? _lookTargetPoint.position
                    : transform.position +
                      Vector3.up *
                      _fallbackTargetHeight;

            Gizmos.DrawSphere(
                targetPos,
                0.1f);
        }
    }
}