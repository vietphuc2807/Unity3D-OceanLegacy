using System;
using System.Collections.Generic;
using UnityEngine;
public class BaitBag : MonoBehaviour
{
    public static BaitBag Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private int _maxCapacity = 5;

    private Dictionary<string, int> _baitCounts = new Dictionary<string, int>();
    private List<string> _baitOrder = new List<string>();

    public int MaxCapacity => _maxCapacity;
    public int CurrentTotal
    {
        get
        {
            int total = 0;
            foreach (var v in _baitCounts.Values) total += v;
            return total;
        }
    }

    public event Action OnBagChanged;

    // FIX: bắn riêng đúng lúc 1 đơn vị mồi thực sự bị TIÊU HAO cho 1 lượt
    // câu (khác với OnBagChanged, vốn bắn cho cả trường hợp mua thêm mồi
    // qua AddBaits()). BaitBagUI lắng nghe event này để chủ động xoá hẳn
    // selection sau mỗi lượt câu, bắt player phải tự chọn lại mồi cho lượt
    // tiếp theo — thay vì để RefreshSlots() tự động giữ nguyên selection
    // cũ theo baitId như trước đây.
    public event Action<string> OnBaitUsed;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        // FIX: thất bại (về lại Day 1) → mất hết mồi hiện có trong túi
        // (balancing), song song với việc TeamWallet reset tiền và
        // PlayerInventory mất hết cá. Sự kiện bắn trên MỌI client, mỗi
        // instance BaitBag tự dọn dẹp túi mồi cục bộ của mình.
        GameDayManager.OnDayFailureReset += HandleDayFailureReset;
    }

    private void OnDisable()
    {
        GameDayManager.OnDayFailureReset -= HandleDayFailureReset;
    }

    private void HandleDayFailureReset()
    {
        ClearAllOnDayFailure();
    }

    public bool CanAdd(Dictionary<string, int> purchases)
    {
        int total = 0;
        foreach (var v in purchases.Values) total += v;
        return CurrentTotal + total <= _maxCapacity;
    }

    public void AddBaits(Dictionary<string, int> purchases)
    {
        foreach (var kvp in purchases)
        {
            if (kvp.Value <= 0) continue;

            if (!_baitCounts.ContainsKey(kvp.Key))
            {
                _baitCounts[kvp.Key] = 0;
                _baitOrder.Add(kvp.Key);
            }
            _baitCounts[kvp.Key] += kvp.Value;
        }

        OnBagChanged?.Invoke();
    }

    public int GetCount(string baitId)
    {
        return _baitCounts.TryGetValue(baitId, out int v) ? v : 0;
    }

    public List<(string baitId, int count)> GetAllBaits()
    {
        var result = new List<(string, int)>();
        foreach (var id in _baitOrder)
        {
            if (_baitCounts.TryGetValue(id, out int c) && c > 0)
                result.Add((id, c));
        }
        return result;
    }

    public bool UseBait(string baitId)
    {
        if (!_baitCounts.TryGetValue(baitId, out int c) || c <= 0)
            return false;

        _baitCounts[baitId] = c - 1;

        // FIX: bắn OnBaitUsed TRƯỚC OnBagChanged, để BaitBagUI kịp
        // ClearSelection() (xoá _selectedSlot/SelectedBaitId + bắn
        // OnSelectedBaitChanged(null)) trước khi RefreshSlots() chạy —
        // nhờ vậy RefreshSlots() sẽ không còn previouslySelectedBaitId nào
        // để tự động tô sáng lại slot cũ nữa.
        OnBaitUsed?.Invoke(baitId);
        OnBagChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Xoá SẠCH toàn bộ mồi đang có trong túi. Gọi khi cả team thất bại,
    /// không đạt chỉ tiêu tiền trước khi hết ngày, và bị reset về Day 1 —
    /// phần "phạt" cân bằng độ khó, song song với TeamWallet.ResetToStartingMoney()
    /// và PlayerInventory.ClearAllFishOnDayFailure().
    /// </summary>
    public void ClearAllOnDayFailure()
    {
        if (CurrentTotal <= 0)
            return;

        _baitCounts.Clear();
        _baitOrder.Clear();

        OnBagChanged?.Invoke();
    }
}