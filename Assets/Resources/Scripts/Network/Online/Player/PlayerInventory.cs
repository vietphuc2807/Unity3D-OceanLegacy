using System;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [SerializeField] private ItemData[] _slots = new ItemData[7];
    [SerializeField] private ItemData _defaultFishingRod;
    [SerializeField] private PlayerHeldItem _heldItem;

    // Size multiplier của từng con cá trong từng slot.
    //
    // Ví dụ:
    // Slot 1 = CrayFish
    // _slotFishSize[1] = 1.37
    //
    // FishData sẽ dùng 1.37 để tính KG.
    private float[] _slotFishSize = new float[7];

    private ItemData _pendingFishItem;
    private float _pendingFishSize;
    private Action _onPendingFishConsumed;

    public bool HasPendingCaughtFish => _pendingFishItem != null;

    public int SelectedIndex { get; private set; }

    public ItemData[] Slots => _slots;

    public delegate void SelectedSlotChanged(
        int index,
        ItemData item
    );

    public event SelectedSlotChanged OnSelectedSlotChanged;

    /// <summary>
    /// Bắn ra mỗi khi NỘI DUNG các ô thay đổi
    /// (thêm / bán / xoá cá).
    /// FishTraderUI dùng để tự refresh khi đang mở.
    /// </summary>
    public event Action OnInventoryChanged;


    // ============================================================
    // AWAKE
    // ============================================================

    private void Awake()
    {
        if (_defaultFishingRod != null)
        {
            _slots[0] = _defaultFishingRod;
        }

        if (_slots == null)
        {
            _slots = new ItemData[7];
        }

        if (_slotFishSize == null ||
            _slotFishSize.Length != _slots.Length)
        {
            _slotFishSize = new float[_slots.Length];
        }
    }


    // ============================================================
    // ENABLE / DISABLE
    // ============================================================

    private void OnEnable()
    {
        // FIX: thất bại (về lại Day 1) → mất hết cá đang mang theo, chỉ
        // giữ lại cần câu ở slot 0 (balancing). Sự kiện này bắn trên MỌI
        // client (RPC RpcTargets.All bên GameDayManager), nên mỗi instance
        // PlayerInventory tự lo dọn dẹp phần dữ liệu cục bộ của nó, không
        // cần GameDayManager biết chi tiết cấu trúc inventory.
        GameDayManager.OnDayFailureReset += HandleDayFailureReset;
    }

    private void OnDisable()
    {
        GameDayManager.OnDayFailureReset -= HandleDayFailureReset;
    }

    private void HandleDayFailureReset()
    {
        ClearAllFishOnDayFailure();
    }


    // ============================================================
    // START
    // ============================================================

    private void Start()
    {
        SelectSlot(1);
    }


    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        if (LobbyChatUI.IsTyping)
            return;


        // ========================================================
        // SCROLL
        // ========================================================

        if (!NetPlayerLook.IsInspectingFish)
        {
            HandleScrollInput();
        }


        // ========================================================
        // NUMBER KEY
        // ========================================================

        HandleNumberKeyInput();
    }


    // ============================================================
    // HANDLE SCROLL INPUT
    // ============================================================

    private void HandleScrollInput()
    {
        if (NetPlayerLook.IsInspectingFish)
            return;

        float scroll =
            Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Approximately(scroll, 0f))
            return;

        int direction =
            scroll > 0f
                ? -1
                : 1;

        int newIndex =
            (SelectedIndex + direction + _slots.Length)
            % _slots.Length;

        SelectSlot(newIndex);
    }


    // ============================================================
    // HANDLE NUMBER KEY INPUT
    // ============================================================

    private void HandleNumberKeyInput()
    {
        for (int i = 0;
             i < _slots.Length && i < 9;
             i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SelectSlot(i);
                break;
            }
        }
    }


    // ============================================================
    // SET PENDING CAUGHT FISH
    // ============================================================

    public void SetPendingCaughtFish(
        ItemData fishItem,
        float sizeMultiplier,
        Action onConsumed)
    {
        if (fishItem == null ||
            !fishItem.IsFish)
        {
            return;
        }

        _pendingFishItem = fishItem;

        _pendingFishSize =
            Mathf.Max(
                0f,
                sizeMultiplier
            );

        _onPendingFishConsumed =
            onConsumed;


        // ========================================================
        // REPORT FISH CAUGHT → CHAT
        // ========================================================
        //
        // Báo ngay khi cá cắn câu thành công.
        //
        // Không chờ tới lúc cá được add vào inventory.
        //
        // Format:
        //
        // SERVER: PlayerName ~ FishName [10.5 Kg]
        //
        // ========================================================

        FishData fishData =
            fishItem.FishInfo;

        if (fishData != null)
        {
            GameplayPlayer.LocalInstance?.RPC_ReportFishCaught(
                fishData.FishName,
                fishData.GetWeightKg(_pendingFishSize)
            );
        }
    }


    // ============================================================
    // CLEAR PENDING CAUGHT FISH
    // ============================================================

    public void ClearPendingCaughtFish()
    {
        _pendingFishItem = null;

        _pendingFishSize = 0f;

        _onPendingFishConsumed = null;
    }


    // ============================================================
    // CLEAR ALL FISH ON DAY FAILURE (balancing)
    // ============================================================

    /// <summary>
    /// Xoá TOÀN BỘ cá đang có trong túi (mọi slot trừ slot 0 = cần câu),
    /// kể cả con cá đang "chờ nhặt" (pending, chưa add vào slot). Gọi khi
    /// cả team thất bại, không đạt chỉ tiêu tiền trước khi hết ngày, và bị
    /// reset về Day 1 — đây là phần "phạt" cân bằng độ khó song song với
    /// việc TeamWallet reset tiền về mức khởi điểm.
    ///
    /// FIX (bug "tự nhiên cầm cần câu lên"):
    ///
    /// TRƯỚC ĐÂY: bất kể slot đang CHỌN có bị xoá cá hay không, cứ
    /// SelectedIndex != 0 là code ép SelectSlotWithoutPendingCheck(0) —
    /// tức LUÔN equip cần câu lên tay mỗi khi ngày thất bại, kể cả khi
    /// người chơi đang cầm 1 item/cá KHÔNG liên quan gì tới slot bị xoá
    /// (hoặc slot đang chọn vốn đã trống sẵn từ trước, chẳng có gì để
    /// xoá). Vì ngày giờ chỉ kết thúc khi bước vào nhà + đóng cửa, nên
    /// nhìn giống như "vô nhà tự nhiên cầm cần câu lên".
    ///
    /// GIỜ: chỉ "buông tay" (hands rỗng) nếu ĐÚNG slot đang được CHỌN
    /// bị xoá cá trong lượt reset này. Nếu KHÔNG, giữ NGUYÊN y hệt item
    /// đang cầm — không đổi SelectedIndex, không ép cầm cần câu.
    /// </summary>
    public void ClearAllFishOnDayFailure()
    {
        // Huỷ luôn cá đang chờ nhặt (chưa kịp add vào slot).
        ClearPendingCaughtFish();

        bool removedAny = false;
        bool selectedSlotWasCleared = false;

        for (int i = 1; i < _slots.Length; i++)
        {
            if (_slots[i] == null)
                continue;

            _slots[i] = null;
            _slotFishSize[i] = 0f;
            removedAny = true;

            if (i == SelectedIndex)
            {
                selectedSlotWasCleared = true;
            }
        }

        if (removedAny)
        {
            OnInventoryChanged?.Invoke();
        }

        // Chỉ cập nhật lại tay (thành rỗng) nếu ĐÚNG con cá đang cầm
        // trên tay (slot đang chọn) bị xoá. KHÔNG đổi SelectedIndex,
        // KHÔNG ép về slot 0 / cần câu — nếu đang cầm cần câu hoặc bất
        // kỳ item nào khác không bị ảnh hưởng, giữ nguyên như cũ.
        if (selectedSlotWasCleared)
        {
            RefreshHeldItemForCurrentSlot();
        }
    }


    // ============================================================
    // CONSUME PENDING CAUGHT FISH
    // ============================================================

    public bool TryConsumePendingCaughtFish()
    {
        if (_pendingFishItem == null)
            return false;

        ItemData fishItem =
            _pendingFishItem;

        float size =
            _pendingFishSize;

        bool added =
            AddFishAndEquip(
                fishItem,
                size
            );

        if (!added)
        {
            return false;
        }


        Action onConsumed =
            _onPendingFishConsumed;

        _pendingFishItem = null;
        _pendingFishSize = 0f;
        _onPendingFishConsumed = null;

        onConsumed?.Invoke();

        return true;
    }


    // ============================================================
    // SELECT SLOT
    // ============================================================

    public void SelectSlot(int index)
    {
        // ========================================================
        // PENDING FISH
        // ========================================================

        if (SelectedIndex == 0 &&
            TryConsumePendingCaughtFish())
        {
            return;
        }


        // ========================================================
        // CHECK INDEX
        // ========================================================

        if (index < 0 ||
            index >= _slots.Length)
        {
            return;
        }


        // ========================================================
        // SET SELECTED INDEX
        // ========================================================

        SelectedIndex =
            index;


        // ========================================================
        // GET ITEM
        // ========================================================

        ItemData item =
            GetSelectedItem();


        // ========================================================
        // NOTIFY UI
        // ========================================================

        OnSelectedSlotChanged?.Invoke(
            SelectedIndex,
            item
        );


        // ========================================================
        // HOLD ITEM
        // ========================================================

        string itemId =
            item != null
                ? item.ItemId
                : "";

        float size =
            (item != null && item.IsFish)
                ? _slotFishSize[SelectedIndex]
                : 1f;


        _heldItem?.RequestHoldItem(
            itemId,
            size
        );
    }


    // ============================================================
    // GET SELECTED ITEM
    // ============================================================

    public ItemData GetSelectedItem()
    {
        if (_slots == null ||
            _slots.Length == 0)
        {
            return null;
        }

        if (SelectedIndex < 0 ||
            SelectedIndex >= _slots.Length)
        {
            return null;
        }

        return _slots[SelectedIndex];
    }


    // ============================================================
    // GET SLOT FISH SIZE
    // ============================================================

    public float GetSlotFishSize(int index)
    {
        if (_slotFishSize == null)
            return 1f;

        if (index < 0 ||
            index >= _slotFishSize.Length)
        {
            return 1f;
        }

        return _slotFishSize[index];
    }


    // ============================================================
    // GET SELECTED FISH SIZE
    // ============================================================

    public float GetSelectedFishSize()
    {
        ItemData item =
            GetSelectedItem();

        if (item == null ||
            !item.IsFish)
        {
            return 1f;
        }

        return GetSlotFishSize(
            SelectedIndex
        );
    }


    // ============================================================
    // GET SLOT FISH WEIGHT
    // ============================================================

    public float GetSlotFishWeightKg(int index)
    {
        if (_slots == null)
            return 0f;

        if (index < 0 ||
            index >= _slots.Length)
        {
            return 0f;
        }

        ItemData item =
            _slots[index];

        if (item == null ||
            !item.IsFish)
        {
            return 0f;
        }


        FishData fishData =
            item.FishInfo;

        if (fishData == null)
            return 0f;


        float sizeMultiplier =
            GetSlotFishSize(index);


        return fishData.GetWeightKg(
            sizeMultiplier
        );
    }


    // ============================================================
    // GET SELECTED FISH WEIGHT
    // ============================================================

    public float GetSelectedFishWeightKg()
    {
        return GetSlotFishWeightKg(
            SelectedIndex
        );
    }


    // ============================================================
    // GET SLOT FISH WEIGHT TEXT
    // ============================================================

    public string GetSlotFishWeightText(int index)
    {
        if (_slots == null)
            return "";

        if (index < 0 ||
            index >= _slots.Length)
        {
            return "";
        }

        ItemData item =
            _slots[index];

        if (item == null ||
            !item.IsFish)
        {
            return "";
        }


        FishData fishData =
            item.FishInfo;

        if (fishData == null)
            return "";


        float sizeMultiplier =
            GetSlotFishSize(index);


        return fishData.GetWeightText(
            sizeMultiplier
        );
    }


    // ============================================================
    // GET SELECTED FISH WEIGHT TEXT
    // ============================================================

    public string GetSelectedFishWeightText()
    {
        return GetSlotFishWeightText(
            SelectedIndex
        );
    }


    // ============================================================
    // ADD ITEM
    // ============================================================

    public bool AddItem(ItemData item)
    {
        if (item == null)
            return false;

        for (int i = 0;
             i < _slots.Length;
             i++)
        {
            if (_slots[i] == null)
            {
                _slots[i] = item;

                if (!item.IsFish)
                {
                    _slotFishSize[i] = 1f;
                }

                OnInventoryChanged?.Invoke();

                return true;
            }
        }

        return false;
    }


    // ============================================================
    // ADD FISH AND EQUIP
    // ============================================================

    public bool AddFishAndEquip(
        ItemData fishItem,
        float sizeMultiplier)
    {
        if (fishItem == null ||
            !fishItem.IsFish)
        {
            return false;
        }


        int slotCount =
            _slots.Length;

        int assignedIndex =
            -1;


        // ========================================================
        // TÌM SLOT TRỐNG
        // ========================================================

        for (int offset = 1;
             offset <= slotCount;
             offset++)
        {
            int idx =
                (SelectedIndex + offset)
                % slotCount;


            // Slot 0 luôn dành cho cần câu.
            if (idx == 0)
                continue;

            if (_slots[idx] == null)
            {
                assignedIndex =
                    idx;

                break;
            }
        }


        // ========================================================
        // KHÔNG CÒN SLOT
        // ========================================================

        if (assignedIndex < 0)
            return false;


        // ========================================================
        // ADD FISH
        // ========================================================

        _slots[assignedIndex] =
            fishItem;

        _slotFishSize[assignedIndex] =
            Mathf.Max(
                0f,
                sizeMultiplier
            );

        OnInventoryChanged?.Invoke();


        // ========================================================
        // EQUIP FISH
        // ========================================================

        SelectSlotWithoutPendingCheck(
            assignedIndex
        );


        return true;
    }


    // ============================================================
    // REMOVE FISH
    // ============================================================

    public int RemoveFishSlots(
    System.Collections.Generic.List<int> indices)
    {
        Debug.Log($"[DEBUG-FISH] RemoveFishSlots called. count={indices?.Count ?? 0}, " +
                $"SelectedIndex={SelectedIndex}");

        if (indices == null || indices.Count == 0)
            return 0;

        int removed = 0;

        for (int i = 0; i < indices.Count; i++)
        {
            int idx = indices[i];

            if (idx <= 0 || idx >= _slots.Length)
                continue;

            ItemData item = _slots[idx];

            if (item == null || !item.IsFish)
                continue;

            _slots[idx] = null;
            _slotFishSize[idx] = 0f;

            removed++;
        }

        Debug.Log($"[DEBUG-FISH] RemoveFishSlots removed={removed}");

        if (removed == 0)
            return 0;

        SelectSlotWithoutPendingCheck(SelectedIndex);

        OnInventoryChanged?.Invoke();

        return removed;
    }


    // ============================================================
    // SELECT SLOT WITHOUT PENDING CHECK
    // ============================================================

    private void SelectSlotWithoutPendingCheck(
        int index)
    {
        if (index < 0 ||
            index >= _slots.Length)
        {
            return;
        }


        // ========================================================
        // SET SELECTED INDEX
        // ========================================================

        SelectedIndex =
            index;


        RefreshHeldItemForCurrentSlot();
    }


    // ============================================================
    // REFRESH HELD ITEM FOR CURRENT SLOT
    // ============================================================
    //
    // Notify UI + cập nhật item đang cầm trên tay dựa theo
    // SelectedIndex HIỆN TẠI (không đổi SelectedIndex). Tách riêng ra
    // để ClearAllFishOnDayFailure() có thể "buông tay" (hands rỗng nếu
    // slot đang chọn giờ trống) mà KHÔNG cần nhảy SelectedIndex về
    // slot 0 như trước.
    // ============================================================

    private void RefreshHeldItemForCurrentSlot()
    {
        ItemData item = GetSelectedItem();

        Debug.Log($"[DEBUG-FISH] RefreshHeldItemForCurrentSlot. " +
                $"SelectedIndex={SelectedIndex}, item={(item != null ? item.ItemId : "NULL")}");

        OnSelectedSlotChanged?.Invoke(SelectedIndex, item);

        string itemId = item != null ? item.ItemId : "";

        float size = (item != null && item.IsFish) ? _slotFishSize[SelectedIndex] : 1f;

        Debug.Log($"[DEBUG-FISH] Calling RequestHoldItem(itemId='{itemId}', size={size})");

        _heldItem?.RequestHoldItem(itemId, size);
    }
}