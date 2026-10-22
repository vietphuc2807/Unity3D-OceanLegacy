using Fusion;
using UnityEngine;

public class TeamWallet : NetworkBehaviour
{
    public static TeamWallet Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Tiền khởi điểm cho MỖI người chơi. Tổng = giá trị này x số người chơi lúc bắt đầu.")]
    [SerializeField] private int _startingMoneyPerPlayer = 15;

    [Networked]
    public int Money { get; private set; }

    /// <summary>
    /// Tổng tiền khởi điểm của team, tính theo số người chơi được chốt
    /// lúc Host bấm Start Game (RoomSessionData.PlayerCountAtStart).
    /// 1 người = 15$, 2 người = 30$, 3 người = 45$, ...
    /// </summary>
    private int StartingMoney =>
        _startingMoneyPerPlayer * Mathf.Max(1, RoomSessionData.PlayerCountAtStart);

    public override void Spawned()
    {
        Instance = this;

        if (Object.HasStateAuthority)
        {
            Money = StartingMoney;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }

    public bool CanAfford(int amount)
    {
        if (amount <= 0)
            return false;

        return Money >= amount;
    }

    /// <summary>
    /// playerId: PlayerRef.PlayerId cua nguoi choi vua thuc hien giao dich -
    /// dung de Stats Panel cuoi ngay gop giao dich theo TUNG NGUOI THUC SU
    /// (khong phu thuoc playerName, vi 2 nguoi co the trung ten hien thi).
    /// playerName: tên người chơi vừa thực hiện giao dịch, dùng để hiện lên bảng tin.
    /// Truyền "" nếu không cần hiện bảng tin cho lần trừ tiền này.
    /// </summary>
    public bool TrySpend(int amount, int playerId, string playerName = "")
    {
        // CHỈ State Authority mới được thay đổi tiền.
        if (!Object.HasStateAuthority)
            return false;

        if (amount <= 0)
            return false;

        if (Money < amount)
            return false;

        Money -= amount;

        // Ghi lai giao dich cho Stats Panel cuoi ngay (chi ghi khi CO
        // ten nguoi choi - xem DayStatsRecorder.RecordTransaction()).
        DayStatsRecorder.Instance?.RecordTransaction(playerId, playerName, -amount);

        RPC_NotifyMoneyChange(playerName, -amount);

        return true;
    }

    /// <summary>
    /// playerId: PlayerRef.PlayerId cua nguoi choi vua thuc hien giao dich -
    /// dung de Stats Panel cuoi ngay gop giao dich theo TUNG NGUOI THUC SU
    /// (khong phu thuoc playerName, vi 2 nguoi co the trung ten hien thi).
    /// playerName: tên người chơi vừa thực hiện giao dịch, dùng để hiện lên bảng tin.
    /// Truyền "" nếu không cần hiện bảng tin cho lần cộng tiền này.
    /// </summary>
    public void AddMoney(int amount, int playerId, string playerName = "")
    {
        // CHỈ State Authority mới được cộng tiền.
        if (!Object.HasStateAuthority)
            return;

        if (amount <= 0)
            return;

        Money += amount;

        // Ghi lai giao dich cho Stats Panel cuoi ngay.
        DayStatsRecorder.Instance?.RecordTransaction(playerId, playerName, amount);

        RPC_NotifyMoneyChange(playerName, amount);
    }

    /// <summary>
    /// Dùng khi cả team KHÔNG đạt money target trước khi hết ngày —
    /// GameDayManager gọi hàm này để đưa tiền team về lại mức khởi điểm
    /// (đã nhân theo số người chơi) và cho cả team chơi lại từ Day 1.
    /// </summary>
    public void ResetToStartingMoney()
    {
        // CHỈ State Authority mới được thay đổi tiền.
        if (!Object.HasStateAuthority)
            return;

        Money = StartingMoney;

        // Không truyền playerName -> RPC_NotifyMoneyChange bỏ qua bảng tin
        // PUBG-style (vì đây không phải giao dịch của 1 người chơi cụ thể).
        // UI (TeamMoneyUI / MoneyTargetUI) vẫn tự động thấy Money đổi và
        // chạy hiệu ứng đếm/nhấp nháy như bình thường.
        RPC_NotifyMoneyChange("", 0);
    }

    // ============================================================
    // NOTI FEED (bảng tin cộng/trừ tiền - kiểu PUBG)
    // ============================================================

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyMoneyChange(string playerName, int delta)
    {
        if (string.IsNullOrEmpty(playerName))
            return;

        if (NotiFeedManager.Instance != null)
        {
            NotiFeedManager.Instance.ShowNotification(playerName, delta);
        }
    }
}