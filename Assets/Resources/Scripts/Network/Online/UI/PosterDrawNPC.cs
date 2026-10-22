using UnityEngine;

/// <summary>
/// Detect người chơi đứng gần + nhìn vào tờ giấy (cùng pattern
/// RodUpgradeNPC / FishTraderNPC). Nhấn E để mở góc vẽ.
/// </summary>
public class PosterDrawNPC : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float _interactRange = 2.5f;
    [SerializeField] private float _lookAngleThreshold = 30f;

    [Header("Look Target")]
    [SerializeField] private Transform _lookTargetPoint;
    [SerializeField] private float _fallbackTargetHeight = 1.2f;

    [Header("UI References")]
    [SerializeField] private GameObject _interactPromptUI;
    [SerializeField] private PosterDrawView _drawView;

    private Transform _playerTransform;
    private Camera _playerCamera;

    private bool _isInRange;
    private bool _drawOpen;

    private void Start()
    {
        if (_interactPromptUI != null)
            _interactPromptUI.SetActive(false);
    }

    // Gọi thêm dòng này trong BaitTraderNPC.RegisterLocalPlayer, cùng chỗ
    // gọi FishTraderNPC.RegisterLocalPlayer / RodUpgradeNPC.RegisterLocalPlayer.
    public static void RegisterLocalPlayer(Transform playerTransform, Camera camera)
    {
        var allNPCs = FindObjectsByType<PosterDrawNPC>(FindObjectsSortMode.None);

        // DEBUG: xác nhận hàm này có được gọi không, và tìm được bao nhiêu instance.
        // Nếu "found 0 instance(s)" -> GameObject poster đang inactive hoặc
        // được spawn SAU thời điểm hàm này chạy -> FindObjectsByType bỏ sót.
        Debug.Log($"[PosterDrawNPC] RegisterLocalPlayer called, found {allNPCs.Length} instance(s)");

        foreach (var npc in allNPCs)
        {
            npc._playerTransform = playerTransform;
            npc._playerCamera = camera;
        }
    }

    private void Update()
    {
        if (_playerTransform == null || _playerCamera == null)
        {
            // DEBUG: nếu dòng này in ra liên tục -> RegisterLocalPlayer chưa
            // từng set được _playerTransform/_playerCamera cho instance này.
            Debug.Log($"[PosterDrawNPC] ({name}) player/camera null, skip. " +
                      $"playerTransform={_playerTransform}, playerCamera={_playerCamera}");
            return;
        }

        // Đang trong góc vẽ (chính mình đang vẽ) thì khỏi check detect nữa.
        if (_drawView != null && _drawView.IsActive) return;

        // Có người KHÁC đang vẽ trên poster này (đồng bộ qua network) ->
        // ẩn prompt + chặn phím E, không cho vào chế độ vẽ.
        bool posterBusy = _drawView != null && _drawView.IsPosterInUse;

        Vector3 targetPos = GetLookTargetPosition();

        float dist = Vector3.Distance(targetPos, _playerTransform.position);
        bool inRange = dist <= _interactRange;

        bool lookingAt = false;
        if (inRange)
        {
            Vector3 toTarget = (targetPos - _playerCamera.transform.position).normalized;
            float angle = Vector3.Angle(_playerCamera.transform.forward, toTarget);
            lookingAt = angle <= _lookAngleThreshold;
        }

        bool shouldShowPrompt = inRange && lookingAt && !_drawOpen && !posterBusy;

        // DEBUG: theo dõi dist/inRange/lookingAt/posterBusy để biết prompt
        // không hiện là do đứng ngoài range, góc nhìn không đủ, hay do
        // người khác đang vẽ.
        Debug.Log($"[PosterDrawNPC] ({name}) dist={dist:F2}/{_interactRange}, " +
                  $"inRange={inRange}, lookingAt={lookingAt}, posterBusy={posterBusy}, shouldShowPrompt={shouldShowPrompt}");

        if (shouldShowPrompt != _isInRange)
        {
            _isInRange = shouldShowPrompt;
            if (_interactPromptUI != null)
                _interactPromptUI.SetActive(_isInRange);
        }

        if (_isInRange &&
            Input.GetKeyDown(KeyCode.E) &&
            !LobbyChatUI.IsTyping &&
            !NetPlayerLook.IsInspectingFish &&
            !posterBusy)
        {
            // DEBUG: nếu prompt đang hiện (_isInRange = true) nhưng dòng
            // này không bao giờ in ra khi nhấn E -> LobbyChatUI.IsTyping
            // hoặc NetPlayerLook.IsInspectingFish đang bị kẹt = true.
            Debug.Log($"[PosterDrawNPC] ({name}) E pressed, opening draw view. " +
                      $"IsTyping={LobbyChatUI.IsTyping}, IsInspectingFish={NetPlayerLook.IsInspectingFish}");
            OpenDraw();
        }
        else if (_isInRange && Input.GetKeyDown(KeyCode.E))
        {
            // DEBUG: prompt hiện, đã nhấn E, nhưng bị 1 trong các cờ trên chặn.
            Debug.Log($"[PosterDrawNPC] ({name}) E pressed but BLOCKED. " +
                      $"IsTyping={LobbyChatUI.IsTyping}, IsInspectingFish={NetPlayerLook.IsInspectingFish}, posterBusy={posterBusy}");
        }
    }

    private Vector3 GetLookTargetPosition()
    {
        if (_lookTargetPoint != null)
            return _lookTargetPoint.position;

        return transform.position + Vector3.up * _fallbackTargetHeight;
    }

    private void OpenDraw()
    {
        if (_drawView == null)
        {
            // DEBUG: nếu _drawView chưa gán trong Inspector, OpenDraw không
            // làm gì cả và không có lỗi nào hiện ra.
            Debug.LogWarning($"[PosterDrawNPC] ({name}) _drawView is NULL, cannot open.");
            return;
        }

        _drawOpen = true;
        _isInRange = false;

        if (_interactPromptUI != null)
            _interactPromptUI.SetActive(false);

        _drawView.Open(OnDrawClosed);
    }

    private void OnDrawClosed()
    {
        _drawOpen = false;
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