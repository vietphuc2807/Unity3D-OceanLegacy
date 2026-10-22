using UnityEngine;
using UnityEngine.EventSystems;

public class FishTraderNPC : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float _interactRange = 3f;
    [SerializeField] private float _lookAngleThreshold = 30f;

    [Header("Look Target")]
    [SerializeField] private Transform _lookTargetPoint;
    [SerializeField] private float _fallbackTargetHeight = 1.2f;

    [Header("UI References")]
    [SerializeField] private GameObject _interactPromptUI;   // ảnh phím E + text "Interact"
    [SerializeField] private FishTraderUI _fishTraderUI;
    [SerializeField] private BaitBagUI _baitBagUI;           // không bắt buộc

    private Transform _playerTransform;
    private Camera _playerCamera;
    private PlayerInventory _playerInventory;

    private bool _isInRange;
    private bool _shopOpen;
    private bool _wasBagOpenBeforeShop;

    private void Start()
    {
        if (_interactPromptUI != null)
            _interactPromptUI.SetActive(false);
    }

    // Được gọi từ BaitTraderNPC.RegisterLocalPlayer (cùng chỗ đăng ký player local).
    public static void RegisterLocalPlayer(Transform playerTransform, Camera camera)
    {
        var allNPCs = FindObjectsByType<FishTraderNPC>(FindObjectsSortMode.None);

        foreach (var npc in allNPCs)
        {
            npc._playerTransform = playerTransform;
            npc._playerCamera = camera;
            npc._playerInventory = null; // resolve lại khi cần
        }
    }

    private void Update()
    {
        if (_playerTransform == null || _playerCamera == null) return;

        Vector3 targetPos = GetLookTargetPosition();

        float dist = Vector3.Distance(targetPos, _playerTransform.position);
        bool inRange = dist <= _interactRange;

        bool lookingAt = false;
        if (inRange)
        {
            Vector3 toNPC = (targetPos - _playerCamera.transform.position).normalized;
            float angle = Vector3.Angle(_playerCamera.transform.forward, toNPC);
            lookingAt = angle <= _lookAngleThreshold;
        }

        bool shouldShowPrompt = inRange && lookingAt && !_shopOpen;

        if (shouldShowPrompt != _isInRange)
        {
            _isInRange = shouldShowPrompt;
            if (_interactPromptUI != null)
                _interactPromptUI.SetActive(_isInRange);
        }

        if (_isInRange &&
            Input.GetKeyDown(KeyCode.E) &&
            !LobbyChatUI.IsTyping &&
            !NetPlayerLook.IsInspectingFish)
        {
            OpenShop();
        }
    }

    private Vector3 GetLookTargetPosition()
    {
        if (_lookTargetPoint != null)
            return _lookTargetPoint.position;

        return transform.position + Vector3.up * _fallbackTargetHeight;
    }

    private PlayerInventory ResolveInventory()
    {
        if (_playerInventory != null)
            return _playerInventory;

        if (_playerTransform != null)
        {
            _playerInventory = _playerTransform.GetComponentInChildren<PlayerInventory>(true);

            if (_playerInventory == null)
                _playerInventory = _playerTransform.GetComponentInParent<PlayerInventory>();
        }

        if (_playerInventory == null && GameplayPlayer.LocalInstance != null)
        {
            _playerInventory = GameplayPlayer.LocalInstance.GetComponentInChildren<PlayerInventory>(true);

            if (_playerInventory == null)
                _playerInventory = GameplayPlayer.LocalInstance.GetComponentInParent<PlayerInventory>();
        }

        return _playerInventory;
    }

    private void OpenShop()
    {
        if (_fishTraderUI == null) return;

        SoundManager.Instance.PlaySFX("wolftalking");

        PlayerInventory inventory = ResolveInventory();

        if (inventory == null)
        {
            Debug.LogWarning("[FishTraderNPC] Không tìm thấy PlayerInventory của player local.");
            return;
        }

        _shopOpen = true;
        _isInRange = false;

        if (_interactPromptUI != null)
            _interactPromptUI.SetActive(false);

        _wasBagOpenBeforeShop = _baitBagUI != null && _baitBagUI.IsOpen;

        _baitBagUI?.Hide(manageCursor: false);
        _fishTraderUI.Open(inventory, OnShopClosed);

        PlayerCursor.IsUIOpen = true;
        PlayerCursor.UnlockCursor();
    }

    private void OnShopClosed()
    {
        _shopOpen = false;
        EventSystem.current?.SetSelectedGameObject(null);

        PlayerCursor.IsUIOpen = false;
        PlayerCursor.LockCursor();

        if (_wasBagOpenBeforeShop)
            _baitBagUI?.Show();
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