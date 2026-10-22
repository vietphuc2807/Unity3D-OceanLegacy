using UnityEngine;

/// <summary>
/// Gan len chiec radio. Khi player den gan + nhin vao radio, hien Interact
/// Prompt UI (phim E + text). Bam E: bat nhac. Bam E lan nua: tat nhac.
///
/// Nhac phat tren 1 AudioSource 3D gan tai chiec radio (loop) - dung gan
/// thi nghe to, di xa thi nho dan.
///
/// Chi co tac dung tren may local (khong dong bo mang): moi nguoi choi tu
/// bat/tat radio rieng cua minh.
/// </summary>
public class RadioInteractable : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float _interactRange = 2.5f;
    [SerializeField] private float _lookAngleThreshold = 20f;

    [Header("Look Target")]
    [Tooltip("(Tuy chon) Diem de nhin vao. De trong = dung vi tri radio + do cao ben duoi.")]
    [SerializeField] private Transform _lookTargetPoint;
    [SerializeField] private float _fallbackTargetHeight = 0.1f;

    [Header("UI References")]
    [Tooltip("Root object chua phim E + text 'Interact' (giong CarGoHomeInteractable).")]
    [SerializeField] private GameObject _interactPromptUI;

    [Header("Music")]
    [Tooltip("Keo THANG file nhac vao day.")]
    [SerializeField] private AudioClip _musicClip;

    [Tooltip("(Tuy chon) AudioSource co san. De trong = script tu tao 1 AudioSource 3D.")]
    [SerializeField] private AudioSource _audioSource;

    [Range(0f, 1f)]
    [SerializeField] private float _musicVolume = 0.8f;

    [Tooltip("BAT: am luong nhan them voi thanh SFX trong Settings.")]
    [SerializeField] private bool _followSfxVolume = true;

    [Tooltip("Trong khoang nay nhac to toi da.")]
    [SerializeField] private float _soundMinDistance = 2f;

    [Tooltip("Ngoai khoang nay khong nghe thay gi (rolloff Linear).")]
    [SerializeField] private float _soundMaxDistance = 20f;

    private Transform _playerTransform;
    private Camera _playerCamera;

    private bool _isInRange;
    private bool _isPlaying;

    private void Start()
    {
        if (_interactPromptUI != null)
            _interactPromptUI.SetActive(false);

        SetupAudioSource();
    }

    // ============================================================
    // AUDIO
    // ============================================================

    private void SetupAudioSource()
    {
        if (_musicClip == null)
            return;

        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.clip = _musicClip;
        _audioSource.playOnAwake = false;
        _audioSource.loop = true;

        // 3D: cang xa radio cang nho.
        _audioSource.spatialBlend = 1f;
        _audioSource.rolloffMode = AudioRolloffMode.Linear;
        _audioSource.minDistance = _soundMinDistance;
        _audioSource.maxDistance = _soundMaxDistance;
        _audioSource.dopplerLevel = 0f;

        ApplyVolume();
    }

    private void ApplyVolume()
    {
        if (_audioSource == null)
            return;

        float volume = _musicVolume;

        if (_followSfxVolume && SoundManager.Instance != null)
            volume *= SoundManager.Instance.GetSFXVolume();

        _audioSource.volume = volume;
    }

    private void ToggleMusic()
    {
        SoundManager.Instance?.PlaySFX("turnonoff");

        if (_musicClip == null || _audioSource == null)
        {
            Debug.LogWarning($"[RadioInteractable] ({name}) chua gan _musicClip.");
            return;
        }

        _isPlaying = !_isPlaying;

        if (_isPlaying)
        {
            ApplyVolume();

            // Lan dau: Play tu dau. Lan sau: tiep tuc tu cho da tam dung.
            if (_audioSource.time > 0f)
                _audioSource.UnPause();
            else
                _audioSource.Play();
        }
        else
        {
            // Pause giu nguyen vi tri, Stop thi se reset ve giay 0.
            _audioSource.Pause();
        }
    }

    public static void RegisterLocalPlayer(Transform playerTransform, Camera camera)
    {
        var allRadios = FindObjectsByType<RadioInteractable>(FindObjectsSortMode.None);

        foreach (var radio in allRadios)
        {
            radio._playerTransform = playerTransform;
            radio._playerCamera = camera;
        }
    }

    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        if (_playerTransform == null || _playerCamera == null)
            return;

        // Cap nhat am luong theo thanh SFX khi dang phat.
        if (_isPlaying)
            ApplyVolume();

        Vector3 targetPos = GetLookTargetPosition();

        float dist = Vector3.Distance(targetPos, _playerTransform.position);
        bool inRange = dist <= _interactRange;

        bool lookingAt = false;
        if (inRange)
        {
            Vector3 toRadio = (targetPos - _playerCamera.transform.position).normalized;
            float angle = Vector3.Angle(_playerCamera.transform.forward, toRadio);
            lookingAt = angle <= _lookAngleThreshold;
        }

        bool shouldShowPrompt = inRange && lookingAt;

        if (shouldShowPrompt != _isInRange)
        {
            _isInRange = shouldShowPrompt;

            if (_interactPromptUI != null)
                _interactPromptUI.SetActive(_isInRange);
        }

        if (!_isInRange)
            return;

        if (LobbyChatUI.IsTyping || NetPlayerLook.IsInspectingFish)
            return;

        if (Input.GetKeyDown(KeyCode.E))
            ToggleMusic();
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

    private void OnDisable()
    {
        // Radio bi tat / doi scene: an prompt cho khoi ket lai tren man hinh.
        if (_isInRange && _interactPromptUI != null)
            _interactPromptUI.SetActive(false);

        _isInRange = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _interactRange);

        Gizmos.color = Color.cyan;
        Vector3 targetPos = _lookTargetPoint != null
            ? _lookTargetPoint.position
            : transform.position + Vector3.up * _fallbackTargetHeight;
        Gizmos.DrawSphere(targetPos, 0.05f);
    }
}