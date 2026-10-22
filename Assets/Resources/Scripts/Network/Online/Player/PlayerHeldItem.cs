using System;
using Fusion;
using UnityEngine;

public class PlayerHeldItem : NetworkBehaviour
{
    [SerializeField] private ItemCatalog _catalog;
    [SerializeField] private Transform _handSocket;
    [SerializeField] private PlayerHeldFish _heldFish;

    [Networked, OnChangedRender(nameof(OnHeldItemIdChanged))]
    private NetworkString<_32> HeldItemId { get; set; }

    [Networked]
    private float HeldItemSizeMultiplier { get; set; }

    private GameObject _currentInstance;

    // FIX: instance của CẦN CÂU được cache riêng, KHÔNG bị Destroy mỗi lần
    // đổi item nữa — xem giải thích chi tiết trong ApplyItem() bên dưới.
    private GameObject _rodInstance;

    public event Action<ItemData> OnHeldItemChanged;
    public static event Action<ItemData> OnLocalHeldItemChanged;

    private string _lastAppliedItemId = null;
    private float _lastAppliedSize = 1f;

    public override void Spawned()
    {
        if (!string.IsNullOrEmpty(HeldItemId.ToString()))
        {
            ApplyItem(
                HeldItemId.ToString(),
                HeldItemSizeMultiplier
            );
        }
    }

    public void RequestHoldItem(
        string itemId,
        float sizeMultiplier = 1f)
    {
        Debug.Log($"[DEBUG-FISH] RequestHoldItem called on Machine={(Runner.IsServer ? "HOST" : "CLIENT")}, " +
              $"ObjectId={Object.Id}, HasInputAuthority={Object.HasInputAuthority}, " +
              $"HasStateAuthority={Object.HasStateAuthority}, itemId='{itemId}'");

        if (!Object.HasInputAuthority)
        {
            Debug.Log($"[DEBUG-FISH] RequestHoldItem BLOCKED - no InputAuthority.");
            return;
        }

        ApplyItem(
            itemId,
            sizeMultiplier
        );

        Debug.Log($"[DEBUG-FISH] Sending RPC_SetHeldItem('{itemId}')...");

        RPC_SetHeldItem(
            itemId,
            sizeMultiplier
        );
    }

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority)]
    private void RPC_SetHeldItem(
        string itemId,
        float sizeMultiplier)
    {
        Debug.Log($"[DEBUG-FISH] RPC_SetHeldItem RECEIVED on Machine={(Runner.IsServer ? "HOST" : "CLIENT")}, " +
              $"ObjectId={Object.Id}, HasStateAuthority={Object.HasStateAuthority}, itemId='{itemId}'");

        HeldItemId = itemId;
        HeldItemSizeMultiplier = sizeMultiplier;

        Debug.Log($"[DEBUG-FISH] HeldItemId SET to '{HeldItemId}' on this machine.");
    }

    private void OnHeldItemIdChanged()
    {
        Debug.Log($"[DEBUG-FISH] OnHeldItemIdChanged FIRED on Machine={(Runner.IsServer ? "HOST" : "CLIENT")}, " +
              $"ObjectId={Object.Id}, HasInputAuthority={Object.HasInputAuthority}, " +
              $"newValue='{HeldItemId}'");

        if (Object.HasInputAuthority)
        {
            Debug.Log($"[DEBUG-FISH] OnHeldItemIdChanged SKIPPED - this machine has InputAuthority (already applied locally).");
            return;
        }

        Debug.Log($"[DEBUG-FISH] OnHeldItemIdChanged -> calling ApplyItem('{HeldItemId}')");

        ApplyItem(
            HeldItemId.ToString(),
            HeldItemSizeMultiplier
        );
    }

    private void ApplyItem(
        string itemId,
        float sizeMultiplier)
    {
        Debug.Log($"[DEBUG-FISH] ApplyItem ENTER on Machine={(Runner.IsServer ? "HOST" : "CLIENT")}, " +
              $"ObjectId={Object.Id}, itemId='{itemId}', lastApplied='{_lastAppliedItemId}'");

        if (_lastAppliedItemId == itemId &&
            Mathf.Approximately(
                _lastAppliedSize,
                sizeMultiplier))
        {
            Debug.Log($"[DEBUG-FISH] ApplyItem SKIPPED - same as last applied.");
            return;
        }

        ItemData newData = null;

        if (!string.IsNullOrEmpty(itemId) &&
            _catalog != null)
        {
            newData = _catalog.GetById(itemId);
        }

        ItemData oldData = null;

        if (!string.IsNullOrEmpty(_lastAppliedItemId) &&
            _catalog != null)
        {
            oldData = _catalog.GetById(
                _lastAppliedItemId
            );
        }

        bool oldIsFish =
            oldData != null &&
            oldData.IsFish;

        bool newIsFish =
            newData != null &&
            newData.IsFish;

        bool switchingFishToFish =
            oldIsFish &&
            newIsFish;

        _lastAppliedItemId = itemId;
        _lastAppliedSize = sizeMultiplier;

        // FIX (mất cá trên lưỡi câu khi đổi item):
        //
        // Cần câu (HandPrefab của _defaultFishingRod) CHỨA component
        // FishingLine bên trong nó. Con cá câu được (nếu túi đầy, đang
        // "chờ nhặt") được gắn làm CHILD của FishingLine.
        //
        // TRƯỚC ĐÂY: đoạn dưới đây Destroy(_currentInstance) VÔ ĐIỀU KIỆN
        // mỗi khi đổi item — kể cả khi đang rời khỏi cần câu. Vì FishingLine
        // nằm trong _currentInstance lúc đó, Destroy() hủy luôn CẢ
        // FishingLine LẪN con cá đang gắn trên nó (con cá là child, bị hủy
        // theo cha). Khi player chọn lại cần câu, Instantiate() tạo ra 1
        // FishingLine HOÀN TOÀN MỚI, không còn liên quan gì tới con cá cũ
        // — đây là nguyên nhân thật sự khiến cá "biến mất" dù logic
        // _pendingFishItem bên PlayerInventory vẫn còn nguyên.
        //
        // FIX: nếu instance hiện tại ĐANG LÀ cần câu (so sánh với
        // _rodInstance đã cache), KHÔNG Destroy nữa — chỉ SetActive(false)
        // để ẩn đi, giữ nguyên GameObject (và con cá con của nó). Khi cầm
        // lại cần câu, dùng lại CHÍNH _rodInstance đã cache (SetActive(true))
        // thay vì Instantiate() bản mới — nhờ vậy FishingLine và con cá
        // đang treo trên lưỡi câu được giữ nguyên trạng thái xuyên suốt.
        //
        // Các item KHÁC (không phải cần câu) vẫn Destroy/Instantiate lại
        // như cũ — không đổi hành vi, không ảnh hưởng gì thêm.
        bool currentIsRodInstance =
            _currentInstance != null &&
            _rodInstance != null &&
            _currentInstance == _rodInstance;

        if (_currentInstance != null && !currentIsRodInstance)
        {
            FishingLine fishingLine =
                _currentInstance
                    .GetComponentInChildren<FishingLine>(true);

            if (fishingLine != null)
            {
                fishingLine.ForceImmediateIdle();
            }

            Destroy(_currentInstance);
            _currentInstance = null;
        }
        else if (currentIsRodInstance)
        {
            // Chỉ ẩn cần câu đi — KHÔNG Destroy — để giữ nguyên FishingLine
            // và con cá (nếu có) đang gắn trên lưỡi câu.
            _currentInstance.SetActive(false);
            _currentInstance = null;
        }

        if (switchingFishToFish)
        {
            _heldFish?.EquipFishImmediate(
                newData.FishInfo,
                sizeMultiplier
            );
        }
        else
        {
            _heldFish?.ClearHeldFish();

            if (newIsFish &&
                _heldFish != null)
            {
                _heldFish.EquipFish(
                    newData.FishInfo,
                    sizeMultiplier
                );
            }
            else if (newData != null &&
                     newData.IsFishingRod &&
                     _rodInstance != null)
            {
                // Cầm lại cần câu → dùng lại instance đã cache thay vì tạo
                // mới, để FishingLine (và con cá còn đang treo trên lưỡi
                // câu, nếu có) được giữ nguyên, không bị mất.
                _rodInstance.SetActive(true);
                _currentInstance = _rodInstance;
            }
            else if (newData != null &&
                     newData.HandPrefab != null &&
                     _handSocket != null)
            {
                _currentInstance =
                    Instantiate(
                        newData.HandPrefab,
                        _handSocket
                    );

                _currentInstance.transform.localPosition =
                    newData.LocalPosition;

                _currentInstance.transform.localRotation =
                    Quaternion.Euler(
                        newData.LocalRotationEuler
                    );

                _currentInstance.transform.localScale =
                    newData.LocalScale;

                // Lần ĐẦU TIÊN cầm cần câu → cache lại instance này để tái
                // sử dụng cho những lần cầm lại sau (xem nhánh phía trên).
                if (newData.IsFishingRod)
                {
                    _rodInstance = _currentInstance;
                }
            }
        }

        OnHeldItemChanged?.Invoke(newData);

        if (Object.HasInputAuthority)
        {
            OnLocalHeldItemChanged?.Invoke(newData);
        }
    }
}