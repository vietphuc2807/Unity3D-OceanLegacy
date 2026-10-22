using Fusion;
using UnityEngine;

public class HookBaitVisual : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHeldItem _heldItem;   
    [SerializeField] private BaitCatalog _catalog;

    [Header("Attach Settings")]
    [SerializeField] private Vector3 _localPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 _localRotationOffset = Vector3.zero;

    [Networked, OnChangedRender(nameof(OnSelectedBaitIdChanged))]
    private NetworkString<_32> SelectedBaitId { get; set; }

    private GameObject _currentInstance;
    private string _lastAppliedBaitId;
    private FishingLine _currentFishingLine;   

    // Bật true ngay khi FishingCastController báo mồi của lượt câu hiện tại
    // đã thực sự bị tiêu hao (tới minigame 2 - CatchFishMinigame - dù thắng
    // hay thua/không chơi). Trong lúc bật, ApplyBait() sẽ KHÔNG hiện lại
    // model dù HeldItem có đổi (xem HandleHeldItemChanged).
    //
    // FIX: KHÔNG còn chờ tới StartCharge() mới mở khoá nữa — vì nếu player
    // mở Bait Bag ra CHỌN LẠI 1 mồi (dù cùng loại hay khác loại) trước khi
    // bắt đầu giữ chuột quăng cần, bản thân hành động CHỌN đó đã là tín
    // hiệu rõ ràng "muốn gắn mồi này lên NGAY". Trước đây do chỉ
    // ShowForNewRound() mới mở khoá (chỉ gọi từ StartCharge), nên chọn mồi
    // trong lúc _hiddenForRound còn true bị ApplyBait() chặn im, khiến
    // player thấy mình đã chọn (viền sáng lên trong UI) nhưng lưỡi câu vẫn
    // trống trơn cho tới tận khi bắt đầu charge cast tiếp theo.
    private bool _hiddenForRound;

    public override void Spawned()
    {
        if (_heldItem != null)
            _heldItem.OnHeldItemChanged += HandleHeldItemChanged;

        if (Object.HasInputAuthority)
        {
            BaitBagUI.OnSelectedBaitChanged += HandleLocalSelectionChanged;

            if (!string.IsNullOrEmpty(BaitBagUI.SelectedBaitId))
                HandleLocalSelectionChanged(BaitBagUI.SelectedBaitId);
        }

        string current = SelectedBaitId.ToString();
        if (!string.IsNullOrEmpty(current))
            ApplyBait(current);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_heldItem != null)
            _heldItem.OnHeldItemChanged -= HandleHeldItemChanged;

        if (Object.HasInputAuthority)
            BaitBagUI.OnSelectedBaitChanged -= HandleLocalSelectionChanged;

        // FIX: gỡ đăng ký event khỏi FishingLine đang theo dõi (nếu có) để
        // tránh gọi vào object đã bị despawn.
        if (_currentFishingLine != null)
            _currentFishingLine.OnCaughtFishCleared -= HandleCaughtFishCleared;

        ClearCurrentVisual();
    }

    private void HandleHeldItemChanged(ItemData item)
    {
        // FIX: gỡ đăng ký khỏi FishingLine cũ trước khi đổi sang cái mới
        // (hoặc null), tránh leak/gọi nhầm event từ rod đã không còn cầm.
        if (_currentFishingLine != null)
            _currentFishingLine.OnCaughtFishCleared -= HandleCaughtFishCleared;

        _currentFishingLine = null;

        if (item != null && item.IsFishingRod && _heldItem != null)
        {
            _currentFishingLine = GetComponentInChildren<FishingLine>(true);

            // FIX: lắng nghe lúc FishingLine gỡ cá đang gắn trên hook (do
            // cast mới, reset,...) để tự apply lại đúng mồi đang chọn ngay
            // khi hook trống trở lại.
            if (_currentFishingLine != null)
                _currentFishingLine.OnCaughtFishCleared += HandleCaughtFishCleared;
        }

        string current = SelectedBaitId.ToString();
        _lastAppliedBaitId = null; 
        ApplyBait(current);
    }

    private void HandleLocalSelectionChanged(string baitId)
    {
        baitId ??= "";

        // Player vừa chủ động chọn (hoặc đổi) mồi trong Bait Bag -> luôn
        // coi đây là ý định gắn mồi NGAY, mở khoá round-hidden nếu đang bị
        // ẩn từ lượt câu trước.
        _hiddenForRound = false;

        ApplyBait(baitId);
        RPC_SetSelectedBait(baitId);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetSelectedBait(string baitId)
    {
        SelectedBaitId = baitId;
    }

    private void OnSelectedBaitIdChanged()
    {
        if (Object.HasInputAuthority)
            return;

        // Đồng bộ hành vi mở khoá y hệt HandleLocalSelectionChanged cho các
        // client KHÔNG có input authority — người chơi ở máy chủ input
        // authority vừa chọn mồi mới thì mọi máy khác cũng phải hiện đúng
        // model đó lên, không được kẹt ở trạng thái ẩn của lượt câu cũ.
        _hiddenForRound = false;

        ApplyBait(SelectedBaitId.ToString());
    }

    // FIX: FishingLine vừa gỡ cá đang gắn trên hook (StartCast(), reset,
    // v.v.). Hook giờ đã trống -> thử hiện lại đúng mồi đang chọn (nếu
    // chưa bị HideForRound() khoá lại). Ép ApplyBait() chạy lại bằng cách
    // reset _lastAppliedBaitId, vì baitId có thể không đổi so với trước
    // khi bị chặn bởi HasCaughtFish.
    private void HandleCaughtFishCleared()
    {
        _lastAppliedBaitId = null;
        ApplyBait(SelectedBaitId.ToString());
    }

    private void ApplyBait(string baitId)
    {
        if (_lastAppliedBaitId == baitId)
            return;

        _lastAppliedBaitId = baitId;

        ClearCurrentVisual();

        // Đang ở trạng thái "mồi lượt này đã bị dùng/mất" -> không tạo lại
        // model dù held item vừa đổi (xem HandleHeldItemChanged). Một lựa
        // chọn MỚI từ Bait Bag (HandleLocalSelectionChanged/
        // OnSelectedBaitIdChanged) đã tự mở khoá cờ này trước khi gọi vào
        // đây, nên KHÔNG bao giờ bị chặn nhầm khi player chủ động chọn mồi.
        if (_hiddenForRound)
            return;

        // FIX: nếu đang có cá bị bắt gắn trên lưỡi câu (chưa cast lại /
        // chưa bị gỡ), TUYỆT ĐỐI không hiện model mồi đè lên — kể cả khi
        // player vừa chủ động chọn mồi mới trong Bait Bag lúc này (đó là
        // nguyên nhân gốc của bug "mồi hiện cùng con cá luôn"). Khi cá bị
        // gỡ (OnCaughtFishCleared), HandleCaughtFishCleared() ở trên sẽ tự
        // gọi lại ApplyBait() để hiện đúng mồi đang chọn.
        if (_currentFishingLine != null && _currentFishingLine.HasCaughtFish)
            return;

        if (string.IsNullOrEmpty(baitId) || _currentFishingLine == null)
            return;

        var data = _catalog != null ? _catalog.GetById(baitId) : null;
        if (data == null || data.WorldPrefab == null)
            return;

        Transform attachPoint = _currentFishingLine.HookAttachPoint;   // đổi từ HookModel sang HookAttachPoint
        if (attachPoint == null)
            return;

        _currentInstance = Instantiate(data.WorldPrefab, attachPoint);
        _currentInstance.transform.localPosition = _localPositionOffset + data.WorldPositionOffset;
        _currentInstance.transform.localRotation = Quaternion.Euler(_localRotationOffset) * Quaternion.Euler(data.WorldRotationOffset);
        _currentInstance.transform.localScale = data.WorldScale;
    }

    private void ClearCurrentVisual()
    {
        if (_currentInstance != null)
        {
            Destroy(_currentInstance);
            _currentInstance = null;
        }
    }

    // ============================================================
    // ROUND-BASED VISIBILITY (gọi từ FishingCastController)
    // ============================================================

    /// Gọi đúng lúc mồi của lượt câu hiện tại thực sự bị tiêu hao (đã tới
    /// minigame 2 - CatchFishMinigame - dù thắng/thua/không chơi). Ẩn model
    /// mồi trên lưỡi câu ngay lập tức và khoá lại không cho tự hiện lại
    /// theo HeldItem đổi — nhưng KHÔNG chặn được nếu player sau đó tự chọn
    /// lại 1 mồi mới trong Bait Bag (xem HandleLocalSelectionChanged).
    public void HideForRound()
    {
        _hiddenForRound = true;
        ClearCurrentVisual();
    }

    /// Gọi lúc bắt đầu 1 lượt charge/quăng cần MỚI. Mở khoá và hiện lại
    /// đúng loại mồi đang được chọn trong Bait Bag hiện tại (nếu có). Vẫn
    /// giữ lại làm lớp bảo hiểm cuối — dù player không hề đụng vào Bait Bag
    /// sau khi mất mồi (chỉ giữ nguyên lựa chọn cũ rồi bấm quăng luôn), lúc
    /// StartCharge() vẫn đảm bảo model được mở khoá & hiện đúng.
    public void ShowForNewRound()
    {
        _hiddenForRound = false;

        // Ép ApplyBait() chạy lại dù baitId không đổi so với lần trước.
        _lastAppliedBaitId = null;
        ApplyBait(SelectedBaitId.ToString());
    }
}