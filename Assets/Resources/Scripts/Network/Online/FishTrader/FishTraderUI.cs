using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Panel Fish Trader: hiển thị 6 ô cá (ô 2 → 7 của túi đồ), chọn cá để bán,
/// bán cá lấy tiền cộng vào TeamWallet.
///
/// Đặt script này lên P-FishTrader (cùng object với PanelEffect).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class FishTraderUI : MonoBehaviour
{
    public static FishTraderUI Instance { get; private set; }

    [Header("Slots (6 ô ↔ ô 2 → 7 của túi đồ)")]
    [SerializeField] private FishTraderSlot[] _slots;

    [Tooltip("Index trong PlayerInventory.Slots ứng với ô ĐẦU TIÊN của panel. " +
             "1 = bỏ qua ô 0 (cần câu mặc định).")]
    [SerializeField] private int _firstInventorySlot = 1;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI _totalPriceText;
    [SerializeField] private TextMeshProUGUI _warningText;   // không bắt buộc
    [SerializeField] private Button _sellButton;
    [SerializeField] private Button _selectAllButton;
    [SerializeField] private Button _closeButton;

    [Header("Panel Effect")]
    [SerializeField] private PanelEffect _panelEffect;

    [Header("Price Effect")]
    [SerializeField] private float _pricePunchScale = 1.12f;
    [SerializeField] private float _pricePunchDuration = 0.18f;

    private PlayerInventory _inventory;
    private Action _onClosed;

    private bool _openRequested;
    private bool _isOpen;

    private int _totalPrice;
    private int _previousTotalPrice = -1;
    private Vector3 _priceOriginalScale = Vector3.one;

    // ------------------------------------------------------------
    // SALE STATE
    // ------------------------------------------------------------

    private bool _waitingForSaleResult;
    private string _pendingSaleData;
    private readonly List<(int index, string itemId)> _pendingSale =
        new List<(int index, string itemId)>();

    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        Instance = this;

        if (_totalPriceText != null)
            _priceOriginalScale = _totalPriceText.transform.localScale;

        if (_warningText != null)
            _warningText.gameObject.SetActive(false);

        if (_slots != null)
        {
            foreach (var slot in _slots)
            {
                if (slot != null)
                    slot.Bind(OnSlotClicked);
            }
        }

        if (_sellButton != null)
            _sellButton.onClick.AddListener(OnSellClicked);

        if (_selectAllButton != null)
            _selectAllButton.onClick.AddListener(OnSelectAllClicked);

        if (_closeButton != null)
            _closeButton.onClick.AddListener(OnCloseClicked);

        // Ẩn panel lúc khởi động. Nếu Awake chạy là do chính Open() vừa
        // kích hoạt object (panel đang inactive sẵn trong scene) thì KHÔNG
        // được tắt lại, nếu không panel sẽ không bao giờ hiện.
        if (!_openRequested)
            gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        UnsubscribeInventory();

        if (_sellButton != null)
            _sellButton.onClick.RemoveListener(OnSellClicked);

        if (_selectAllButton != null)
            _selectAllButton.onClick.RemoveListener(OnSelectAllClicked);

        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(OnCloseClicked);

        if (_totalPriceText != null)
            _totalPriceText.transform.DOKill();
    }

#if UNITY_EDITOR
    // Chỉ trong Editor: khi panel đang mở, đọc lại icon transform mỗi frame
    // để bạn chỉnh Trader Icon trong ItemData (kể cả lúc đang Play) và
    // thấy kết quả ngay, không cần mở lại panel.
    private void Update()
    {
        if (!_isOpen || _slots == null)
            return;

        foreach (var slot in _slots)
        {
            if (slot != null)
                slot.RefreshIconTransform();
        }
    }
#endif

    // ============================================================
    // OPEN
    // ============================================================

    public void Open(PlayerInventory inventory, Action onClosed)
    {
        UnsubscribeInventory();

        _inventory = inventory;
        _onClosed = onClosed;

        if (_inventory != null)
            _inventory.OnInventoryChanged += HandleInventoryChanged;

        _isOpen = true;
        ResetSaleState();

        _openRequested = true;
        gameObject.SetActive(true);

        if (_totalPriceText != null)
        {
            _totalPriceText.transform.DOKill();
            _totalPriceText.transform.localScale = _priceOriginalScale;
        }

        _previousTotalPrice = -1;

        // Mở panel là bỏ chọn hết, sau đó nạp cá từ túi đồ.
        if (_slots != null)
        {
            foreach (var slot in _slots)
            {
                if (slot != null)
                    slot.SetEmpty(slot.InventoryIndex);
            }
        }

        RefreshSlots();

        _panelEffect.Show();
    }

    // ============================================================
    // CLOSE
    // ============================================================

    private void OnCloseClicked()
    {
        if (_waitingForSaleResult)
            return;

        CloseWithEffect();
    }

    /// <summary>
    /// Đóng panel bằng PanelEffect.Hide(). Dùng chung cho nút Close và
    /// cho việc tự đóng sau khi bán cá thành công.
    /// </summary>
    private void CloseWithEffect()
    {
        // Đã đang đóng rồi (bấm nhiều lần) → bỏ qua, tránh gọi Hide 2 lần.
        if (!_isOpen)
            return;

        EventSystem.current?.SetSelectedGameObject(null);

        _isOpen = false;
        UnsubscribeInventory();

        _panelEffect.Hide(() =>
        {
            gameObject.SetActive(false);

            _onClosed?.Invoke();
        });
    }

    private void UnsubscribeInventory()
    {
        if (_inventory != null)
            _inventory.OnInventoryChanged -= HandleInventoryChanged;
    }

    // ============================================================
    // INVENTORY SYNC
    // ============================================================

    private void HandleInventoryChanged()
    {
        if (!_isOpen)
            return;

        RefreshSlots();
    }

    /// <summary>
    /// Đọc lại 6 ô từ túi đồ. Ô nào vẫn giữ đúng con cá cũ thì giữ nguyên
    /// trạng thái "đang chọn".
    /// </summary>
    private void RefreshSlots()
    {
        if (_slots == null)
            return;

        for (int i = 0; i < _slots.Length; i++)
        {
            FishTraderSlot slot = _slots[i];

            if (slot == null)
                continue;

            int invIndex = _firstInventorySlot + i;

            ItemData item = null;
            float size = 0f;

            if (_inventory != null &&
                invIndex >= 0 &&
                invIndex < _inventory.Slots.Length)
            {
                item = _inventory.Slots[invIndex];
                size = _inventory.GetSlotFishSize(invIndex);
            }

            if (item != null && item.IsFish)
            {
                bool sameFish =
                    slot.HasFish &&
                    slot.InventoryIndex == invIndex &&
                    slot.Item == item &&
                    Mathf.Approximately(slot.Size, size);

                bool keepSelected = sameFish && slot.IsSelected;

                int price = item.FishInfo.GetSellPrice(size);

                slot.SetFish(invIndex, item, size, price);
                slot.SetSelected(keepSelected);
            }
            else
            {
                slot.SetEmpty(invIndex);
            }
        }

        RecalculateTotal();
    }

    // ============================================================
    // SELECT
    // ============================================================

    private void OnSlotClicked(FishTraderSlot slot)
    {
        if (_waitingForSaleResult)
            return;

        // Tránh Button giữ trạng thái "Selected" → lẫn với viền trắng.
        EventSystem.current?.SetSelectedGameObject(null);

        slot.SetSelected(!slot.IsSelected);

        RecalculateTotal();
    }

    /// <summary>
    /// Chưa chọn hết → chọn tất cả cá. Đã chọn hết rồi → bỏ chọn tất cả.
    /// </summary>
    private void OnSelectAllClicked()
    {
        if (_waitingForSaleResult)
            return;

        EventSystem.current?.SetSelectedGameObject(null);

        bool anyFish = false;
        bool allSelected = true;

        foreach (var slot in _slots)
        {
            if (slot == null || !slot.HasFish)
                continue;

            anyFish = true;

            if (!slot.IsSelected)
                allSelected = false;
        }

        if (!anyFish)
            return;

        bool target = !allSelected;

        foreach (var slot in _slots)
        {
            if (slot != null && slot.HasFish)
                slot.SetSelected(target);
        }

        RecalculateTotal();
    }

    // ============================================================
    // TOTAL
    // ============================================================

    private void RecalculateTotal()
    {
        _totalPrice = 0;

        if (_slots != null)
        {
            foreach (var slot in _slots)
            {
                if (slot != null && slot.HasFish && slot.IsSelected)
                    _totalPrice += slot.Price;
            }
        }

        if (_totalPriceText != null)
        {
            _totalPriceText.text = $"{_totalPrice}$";
            PlayPriceEffect();
        }

        UpdateButtons();
    }

    private void UpdateButtons()
    {
        if (_sellButton != null)
            _sellButton.interactable = !_waitingForSaleResult && _totalPrice > 0;

        if (_selectAllButton != null)
            _selectAllButton.interactable = !_waitingForSaleResult;

        if (_closeButton != null)
            _closeButton.interactable = !_waitingForSaleResult;
    }

    private void PlayPriceEffect()
    {
        if (_totalPriceText == null)
            return;

        if (_previousTotalPrice == _totalPrice)
            return;

        if (_previousTotalPrice == -1)
        {
            _previousTotalPrice = _totalPrice;
            return;
        }

        _previousTotalPrice = _totalPrice;

        Transform target = _totalPriceText.transform;

        target.DOKill();
        target.localScale = _priceOriginalScale;

        target
            .DOPunchScale(
                Vector3.one * (_pricePunchScale - 1f),
                _pricePunchDuration,
                5,
                0.7f
            )
            .SetEase(Ease.OutQuad)
            .OnComplete(() => target.localScale = _priceOriginalScale);
    }

    // ============================================================
    // SELL
    // ============================================================

    private void OnSellClicked()
    {
        if (_waitingForSaleResult)
            return;

        EventSystem.current?.SetSelectedGameObject(null);

        _pendingSale.Clear();

        List<string> parts = new List<string>();

        foreach (var slot in _slots)
        {
            if (slot == null || !slot.HasFish || !slot.IsSelected)
                continue;

            _pendingSale.Add((slot.InventoryIndex, slot.Item.ItemId));

            // InvariantCulture: tránh máy dùng dấu phẩy thập phân (vd. 1,37)
            // làm host parse sai.
            parts.Add(
                slot.Item.ItemId + ":" +
                slot.Size.ToString("R", CultureInfo.InvariantCulture)
            );
        }

        if (parts.Count == 0)
            return;

        if (GameplayPlayer.LocalInstance == null)
        {
            _pendingSale.Clear();
            ShowWarning("Player is not ready.");
            return;
        }

        // Ví dụ: "fish_carp:1.37|fish_bass:1.12"
        _pendingSaleData = string.Join("|", parts);
        _waitingForSaleResult = true;

        HideWarning();
        UpdateButtons();

        GameplayPlayer.LocalInstance.RPC_RequestSellFish(_pendingSaleData);
    }

    /// <summary>
    /// Host đã cộng tiền vào TeamWallet → giờ mới xoá cá khỏi túi đồ.
    /// (Được gọi từ GameplayPlayer.RPC_SellFishResult.)
    /// </summary>
    public void CompleteSaleFromNetwork(string saleData, int totalEarned)
    {
        Debug.Log($"[DEBUG-FISH] CompleteSaleFromNetwork called. " +
                $"waitingForSaleResult={_waitingForSaleResult}, saleDataMatch={saleData == _pendingSaleData}");

        if (!_waitingForSaleResult)
            return;

        if (saleData != _pendingSaleData)
            return;

        List<int> indicesToRemove = new List<int>();

        if (_inventory != null)
        {
            ItemData[] invSlots = _inventory.Slots;

            foreach (var entry in _pendingSale)
            {
                if (entry.index > 0 &&
                    entry.index < invSlots.Length &&
                    invSlots[entry.index] != null &&
                    invSlots[entry.index].ItemId == entry.itemId)
                {
                    indicesToRemove.Add(entry.index);
                }
            }

            Debug.Log($"[DEBUG-FISH] Removing {indicesToRemove.Count} slot(s): " +
                    string.Join(",", indicesToRemove));

            // ====================================================
            // FIX (host thấy client vẫn cầm cá đã bán)
            // ====================================================
            //
            // CompleteSaleFromNetwork() đang chạy BÊN TRONG luồng xử lý
            // RPC RPC_SellFishResult (host → client). Nếu gọi thẳng
            // _inventory.RemoveFishSlots(...) ngay tại đây, nó sẽ đi tới
            // PlayerHeldItem.RequestHoldItem() → gửi tiếp 1 RPC khác
            // (RPC_SetHeldItem, client → host) NGAY TRONG LÚC client
            // đang xử lý 1 RPC đến.
            //
            // Với HOST thì không sao (StateAuthority = chính nó nên set
            // Networked var trực tiếp, không cần gửi qua mạng). Nhưng
            // với CLIENT, gọi RPC lồng-trong-RPC kiểu này không được
            // gửi đi đúng cách — khiến HeldItemId (Networked) trên Host
            // KHÔNG BAO GIỜ được client cập nhật. Kết quả: client tự
            // thấy đúng (vì ApplyItem local đã chạy trước khi gửi RPC)
            // nhưng Host (và các client khác) vẫn thấy client đang cầm
            // con cá đã bán.
            //
            // FIX: hoãn RemoveFishSlots() sang FRAME KẾ TIẾP bằng
            // coroutine, để lúc RPC_SetHeldItem được gửi, nó KHÔNG còn
            // nằm lồng trong 1 RPC handler nào nữa → gửi đi bình thường
            // → Host nhận đúng, đồng bộ cho mọi client.
            // ====================================================

            StartCoroutine(
                RemoveFishSlotsNextFrame(indicesToRemove)
            );
        }

        ResetSaleState();
        CloseWithEffect();
    }

    /// <summary>
    /// Xem giải thích chi tiết trong CompleteSaleFromNetwork() ở trên:
    /// hoãn 1 frame để RPC_SetHeldItem (bên trong RemoveFishSlots →
    /// SelectSlotWithoutPendingCheck → RequestHoldItem) không bị gọi
    /// lồng trong RPC_SellFishResult đang xử lý.
    /// </summary>
    private IEnumerator RemoveFishSlotsNextFrame(
        List<int> indicesToRemove)
    {
        yield return null;

        _inventory?.RemoveFishSlots(indicesToRemove);
    }

    public void ShowSaleFailed(string message)
    {
        ResetSaleState();
        UpdateButtons();

        ShowWarning(
            string.IsNullOrEmpty(message) ? "Sale failed." : message
        );
    }

    private void ResetSaleState()
    {
        _waitingForSaleResult = false;
        _pendingSaleData = null;
        _pendingSale.Clear();
    }

    // ============================================================
    // WARNING
    // ============================================================

    private void ShowWarning(string message)
    {
        if (_warningText == null)
        {
            Debug.LogWarning($"[FishTraderUI] {message}");
            return;
        }

        _warningText.gameObject.SetActive(true);
        _warningText.text = message;
    }

    private void HideWarning()
    {
        if (_warningText != null)
            _warningText.gameObject.SetActive(false);
    }
}