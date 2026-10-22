using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gan len chiec xe. Khi player den gan + nhin vao xe, hien Interact
/// Prompt UI (phim E + text). Giu phim E se lam vien vuong quanh chu
/// E lap day dan (Image.fillAmount) - lap day het thi tinh la XAC NHAN
/// ve nha.
///
/// - Xac nhan xong thi khoa tuong tac cho toi khi Host bao RESET
///   (timeout khong ai xac nhan them, hoac het ngay) -> ResetAllLocal()
///   mo khoa, nguoi choi phai giu E lai tu dau.
/// - Het ngay (GameDayManager.IsGoHomeBlocked): an prompt, khong cho
///   giu E cho toi khi ngay moi bat dau.
///
/// TIENG XE KHOI DONG (3D): khi giu E het (CompleteHold), phat clip
/// _carStartClip tren 1 AudioSource 3D gan tai chiec xe - dung gan xe thi
/// nghe to, di xa thi nho dan.
/// </summary>
public class CarGoHomeInteractable : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float _interactRange = 3f;
    [SerializeField] private float _lookAngleThreshold = 30f;

    [Header("Look Target")]
    [SerializeField] private Transform _lookTargetPoint;
    [SerializeField] private float _fallbackTargetHeight = 1.2f;

    [Header("UI References")]
    [Tooltip("Root object chua phim E + text 'Interact' (giong FishTraderNPC).")]
    [SerializeField] private GameObject _interactPromptUI;

    [Tooltip(
        "Image kieu Filled DAT CHONG len chu E. fillAmount se tang " +
        "dan 0 -> 1 trong luc giu phim E."
    )]
    [SerializeField] private Image _holdFillImage;

    [Header("Hold To Confirm")]
    [SerializeField] private float _holdDuration = 2f;

    [Header("Car Start Sound (3D)")]
    [Tooltip("Keo THANG file tieng xe khoi dong (carstart) vao day. " +
             "De trong = khong phat tieng.")]
    [SerializeField] private AudioClip _carStartClip;

    [Tooltip("(Tuy chon) AudioSource co san tren xe. De trong = script tu " +
             "tao 1 AudioSource 3D tren chinh object nay.")]
    [SerializeField] private AudioSource _audioSource;

    [Range(0f, 1f)]
    [SerializeField] private float _soundVolume = 1f;

    [Tooltip("BAT: am luong nhan them voi thanh SFX trong Settings.")]
    [SerializeField] private bool _followSfxVolume = true;

    [Tooltip("Trong khoang nay tieng to toi da.")]
    [SerializeField] private float _soundMinDistance = 3f;

    [Tooltip("Ngoai khoang nay khong nghe thay gi (rolloff Linear).")]
    [SerializeField] private float _soundMaxDistance = 30f;

    [Header("Debug")]
    [Tooltip("Sau khi da xac nhan, khoa tuong tac tren xe nay cho toi " +
             "khi Host reset (timeout / het ngay).")]
    [SerializeField] private bool _lockAfterConfirm = true;

    private Transform _playerTransform;
    private Camera _playerCamera;

    private bool _isInRange;
    private bool _isHolding;
    private float _holdProgress;
    private bool _hasRequestedGoHome;

    private void Start()
    {
        if (_interactPromptUI != null)
            _interactPromptUI.SetActive(false);

        if (_holdFillImage != null)
            _holdFillImage.fillAmount = 0f;

        SetupAudioSource();
    }

    // ============================================================
    // AUDIO
    // ============================================================

    private void SetupAudioSource()
    {
        if (_carStartClip == null)
            return;

        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.playOnAwake = false;
        _audioSource.loop = false;

        // 3D: cang xa xe cang nho, vuot _soundMaxDistance thi im han.
        _audioSource.spatialBlend = 1f;
        _audioSource.rolloffMode = AudioRolloffMode.Linear;
        _audioSource.minDistance = _soundMinDistance;
        _audioSource.maxDistance = _soundMaxDistance;
        _audioSource.dopplerLevel = 0f;
    }

    private void PlayCarStartSound()
    {
        if (_carStartClip == null || _audioSource == null)
            return;

        float volume = _soundVolume;

        if (_followSfxVolume && SoundManager.Instance != null)
            volume *= SoundManager.Instance.GetSFXVolume();

        // Master volume tu ap qua AudioListener.volume.
        _audioSource.PlayOneShot(_carStartClip, volume);
    }

    // ============================================================
    // REGISTER LOCAL PLAYER
    // ============================================================

    public static void RegisterLocalPlayer(Transform playerTransform, Camera camera)
    {
        var allCars = FindObjectsByType<CarGoHomeInteractable>(FindObjectsSortMode.None);

        foreach (var car in allCars)
        {
            car._playerTransform = playerTransform;
            car._playerCamera = camera;
        }
    }

    // ============================================================
    // RESET (Host bao: timeout hoac het ngay)
    // ============================================================

    public static void ResetAllLocal()
    {
        var allCars = FindObjectsByType<CarGoHomeInteractable>(FindObjectsSortMode.None);

        foreach (var car in allCars)
        {
            car._hasRequestedGoHome = false;
            car._isInRange = false;
            car.CancelHold();
        }
    }

    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        if (_hasRequestedGoHome && _lockAfterConfirm)
            return;

        if (_playerTransform == null || _playerCamera == null)
            return;

        // Het ngay: an prompt, khong cho giu E.
        if (GameDayManager.Instance != null &&
            GameDayManager.Instance.IsGoHomeBlocked)
        {
            if (_isInRange)
            {
                _isInRange = false;

                if (_interactPromptUI != null)
                    _interactPromptUI.SetActive(false);
            }

            CancelHold();
            return;
        }

        Vector3 targetPos = GetLookTargetPosition();

        float dist = Vector3.Distance(targetPos, _playerTransform.position);
        bool inRange = dist <= _interactRange;

        bool lookingAt = false;
        if (inRange)
        {
            Vector3 toCar = (targetPos - _playerCamera.transform.position).normalized;
            float angle = Vector3.Angle(_playerCamera.transform.forward, toCar);
            lookingAt = angle <= _lookAngleThreshold;
        }

        bool shouldShowPrompt = inRange && lookingAt;

        if (shouldShowPrompt != _isInRange)
        {
            _isInRange = shouldShowPrompt;

            if (_interactPromptUI != null)
                _interactPromptUI.SetActive(_isInRange);

            if (!_isInRange)
                CancelHold();
        }

        if (!_isInRange)
            return;

        if (LobbyChatUI.IsTyping || NetPlayerLook.IsInspectingFish)
        {
            CancelHold();
            return;
        }

        HandleHoldInput();
    }

    // ============================================================
    // HOLD INPUT
    // ============================================================

    private void HandleHoldInput()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            _isHolding = true;
        }

        if (Input.GetKeyUp(KeyCode.E))
        {
            CancelHold();
        }

        if (!_isHolding)
            return;

        _holdProgress += Time.deltaTime / Mathf.Max(0.01f, _holdDuration);

        if (_holdFillImage != null)
            _holdFillImage.fillAmount = Mathf.Clamp01(_holdProgress);

        if (_holdProgress >= 1f)
        {
            CompleteHold();
        }
    }

    private void CancelHold()
    {
        _isHolding = false;
        _holdProgress = 0f;

        if (_holdFillImage != null)
            _holdFillImage.fillAmount = 0f;
    }

    private void CompleteHold()
    {
        _isHolding = false;
        _hasRequestedGoHome = true;
        _isInRange = false;

        if (_holdFillImage != null)
            _holdFillImage.fillAmount = 1f;

        if (_interactPromptUI != null)
            _interactPromptUI.SetActive(false);

        // Giu E het: phat tieng xe khoi dong (3D tai chiec xe).
        PlayCarStartSound();

        // "Chup" lai ca dang mang trong tui do CUA RIENG MINH.
        // Neu bi reset roi giu E lai thi se chup lai, khong sai du lieu.
        PlayerInventory inventory =
            _playerTransform != null
                ? _playerTransform.GetComponent<PlayerInventory>()
                : null;

        ReturningCatchData.CaptureFromInventory(inventory);

        GameplayPlayer.LocalInstance?.RPC_RequestGoHome();
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private Vector3 GetLookTargetPosition()
    {
        if (_lookTargetPoint != null)
            return _lookTargetPoint.position;

        return transform.position + Vector3.up * _fallbackTargetHeight;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _interactRange);

        Gizmos.color = Color.cyan;
        Vector3 targetPos = _lookTargetPoint != null
            ? _lookTargetPoint.position
            : transform.position + Vector3.up * _fallbackTargetHeight;
        Gizmos.DrawSphere(targetPos, 0.1f);
    }
}