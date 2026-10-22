using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BaitBagUI : MonoBehaviour
{
    [Header("Visual Root")]
    [SerializeField] private GameObject _visualRoot;

    [Header("Slots")]
    [SerializeField] private BaitBagSlotUI[] _slots;

    [Header("Catalog")]
    [SerializeField] private BaitCatalog _catalog;

    [Header("Capacity Text")]
    [SerializeField] private TextMeshProUGUI _capacityText;

    [Header("Panel Effect")]
    [SerializeField] private PanelEffect _panelEffect;

    private bool _isOpen;
    public bool IsOpen => _isOpen;
    private BaitBagSlotUI _selectedSlot;

    public static string SelectedBaitId { get; private set; }
    public static event System.Action<string> OnSelectedBaitChanged;
    public static event System.Action<bool> OnBagVisibilityChanged;

    private void Awake()
    {
        if (_visualRoot != null)
            _visualRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (BaitBag.Instance != null)
        {
            BaitBag.Instance.OnBagChanged += RefreshSlots;

            // FIX: mỗi khi 1 đơn vị mồi bị tiêu hao cho 1 lượt câu, xoá
            // hẳn selection (bắt player tự chọn lại mồi cho lượt tiếp
            // theo) thay vì để RefreshSlots() tự động giữ nguyên selection
            // cũ theo baitId.
            BaitBag.Instance.OnBaitUsed += HandleBaitUsed;
        }

        foreach (var slot in _slots)
            slot.OnClicked += HandleSlotClicked;
    }

    private void OnDisable()
    {
        if (BaitBag.Instance != null)
        {
            BaitBag.Instance.OnBagChanged -= RefreshSlots;
            BaitBag.Instance.OnBaitUsed -= HandleBaitUsed;
        }

        foreach (var slot in _slots)
            slot.OnClicked -= HandleSlotClicked;
    }

    private void Update()
    {
        if (LobbyChatUI.IsTyping) return;

        if (!_isOpen && PlayerCursor.IsUIOpen) return;

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            Toggle();
        }
    }

    private void Toggle()
    {
        if (_isOpen) Hide();
        else Show();
    }

    public void Show()
    {
        _isOpen = true;

        SoundManager.Instance.PlaySFX("openbait");

        if (_visualRoot != null)
            _visualRoot.SetActive(true);

        RefreshSlots();

        PlayerCursor.IsUIOpen = true;
        PlayerCursor.UnlockCursor();

        if (_panelEffect != null)
            _panelEffect.Show();

        OnBagVisibilityChanged?.Invoke(true);
    }

    public void Hide(bool manageCursor = true)
    {
        _isOpen = false;

        void OnHidden()
        {
            if (_visualRoot != null)
                _visualRoot.SetActive(false);

            if (manageCursor)
            {
                PlayerCursor.IsUIOpen = false;
                PlayerCursor.LockCursor();
            }
        }

        if (_panelEffect != null)
            _panelEffect.Hide(OnHidden);
        else
            OnHidden();

        OnBagVisibilityChanged?.Invoke(false);
    }

    // FIX: BaitBag báo 1 đơn vị mồi vừa bị tiêu hao cho lượt câu vừa rồi.
    // Chủ động xoá selection qua ĐÚNG path ClearSelection() (bắn
    // OnSelectedBaitChanged(null) để đồng bộ với HookBaitVisual) NGAY LÚC
    // NÀY — trước khi OnBagChanged/RefreshSlots() kịp chạy — để
    // RefreshSlots() không còn previouslySelectedBaitId nào mà tự động tô
    // sáng lại slot cũ nữa. Player bắt buộc phải tự bấm chọn lại mồi cho
    // lượt câu tiếp theo.
    private void HandleBaitUsed(string baitId)
    {
        ClearSelection();
    }

    private void RefreshSlots()
    {
        if (BaitBag.Instance == null) return;

        var bag = BaitBag.Instance;
        var baits = bag.GetAllBaits();

        // Nhớ lại baitId đang được chọn TRƯỚC khi rebuild slot, thay vì nhớ
        // tham chiếu tới slot GameObject — vì sau khi rebuild, slot ở cùng
        // index có thể đang hiển thị 1 loại mồi HOÀN TOÀN KHÁC (do loại mồi
        // cũ bị dùng hết -> biến mất khỏi list -> loại mồi kế tiếp bị dồn
        // lên đúng index đó). Nếu chỉ check "slot cũ vẫn active + có item"
        // như trước đây, code sẽ tưởng nhầm là "vẫn đang chọn mồi cũ" và tự
        // động bật viền sáng (selection border) lên slot đó — dù thực chất
        // nó đang hiển thị 1 loại mồi khác mà người chơi chưa hề click chọn.
        //
        // LƯU Ý: nhánh giữ-lại-selection này giờ chỉ còn tác dụng cho các
        // lượt refresh KHÔNG liên quan tới việc tiêu hao mồi (vd mua thêm
        // mồi qua AddBaits(), hoặc đơn giản là mở lại Bait Bag để xem) —
        // vì trường hợp tiêu hao mồi cho 1 lượt câu đã bị HandleBaitUsed()
        // ở trên chủ động ClearSelection() trước rồi, nên tới đây
        // _selectedSlot/SelectedBaitId đã là null sẵn, vòng lặp bên dưới
        // sẽ không tìm thấy gì và không tô sáng nhầm slot nào cả.
        string previouslySelectedBaitId = _selectedSlot != null ? SelectedBaitId : null;

        int slotIndex = 0;

        foreach (var (baitId, count) in baits)
        {
            var data = _catalog != null ? _catalog.GetById(baitId) : null;
            Sprite icon = data != null ? data.Icon : null;

            for (int i = 0; i < count; i++)
            {
                if (slotIndex >= _slots.Length)
                    break;

                var slot = _slots[slotIndex];
                slot.gameObject.SetActive(true);
                slot.SetBait(icon, 1, baitId);

                slotIndex++;
            }

            if (slotIndex >= _slots.Length)
                break;
        }

        for (int i = slotIndex; i < _slots.Length; i++)
        {
            _slots[i].SetEmpty();
            _slots[i].gameObject.SetActive(false);
        }

        // Tìm lại ĐÚNG slot nào (nếu còn) đang chứa baitId đã chọn trước đó
        // — dựa theo baitId thực tế trên slot sau khi rebuild, KHÔNG dựa
        // theo tham chiếu slot object hay index cũ.
        _selectedSlot = null;

        if (previouslySelectedBaitId != null)
        {
            foreach (var slot in _slots)
            {
                if (slot.gameObject.activeSelf &&
                    slot.HasItem &&
                    slot.BaitId == previouslySelectedBaitId)
                {
                    _selectedSlot = slot;
                    break;
                }
            }
        }

        if (_selectedSlot != null)
        {
            _selectedSlot.SetSelected(true);
        }
        else if (SelectedBaitId != null)
        {
            // Loại mồi đang chọn đã hết hẳn (không còn slot nào chứa nó
            // nữa) -> clear selection thật sự, không để viền sáng dính
            // nhầm sang loại mồi khác.
            SelectedBaitId = null;
            OnSelectedBaitChanged?.Invoke(null);
        }

        if (_capacityText != null)
        {
            _capacityText.text =
                $"Fishing Baits: {bag.CurrentTotal}/{bag.MaxCapacity}";
        }
    }

    private void HandleSlotClicked(BaitBagSlotUI slot)
    {
        if (_selectedSlot == slot)
        {
            ClearSelection();
            return;
        }

        if (_selectedSlot != null)
            _selectedSlot.SetSelected(false);

        _selectedSlot = slot;
        _selectedSlot.SetSelected(true);
        SelectedBaitId = slot.BaitId;

        OnSelectedBaitChanged?.Invoke(SelectedBaitId);
    }

    private void ClearSelection()
    {
        if (_selectedSlot != null)
            _selectedSlot.SetSelected(false);

        _selectedSlot = null;
        SelectedBaitId = null;

        OnSelectedBaitChanged?.Invoke(null);
    }
}