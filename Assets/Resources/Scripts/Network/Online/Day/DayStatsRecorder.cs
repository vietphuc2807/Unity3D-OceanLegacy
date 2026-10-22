using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Ghi lai LICH SU GIAO DICH TIEN (mua moi, ban ca, nang cap can...) va
/// TONG SO CA DA BAN trong ngay hien tai - CHI co y nghia tren Host (State
/// Authority), giong cach TeamWallet/TeamRodUpgrade chi cho Host ghi.
///
/// Khong dung [Networked] cho du lieu ben trong vi KHONG can dong bo lien
/// tuc toi client - du lieu chi duoc Host doc 1 LAN duy nhat luc
/// GameDayManager.EvaluateDayEnd() thanh cong, dong goi thanh DayStatsData
/// roi Serialize() thanh 1 string duy nhat gui qua RPC_ShowDayStatsPanel
/// toi moi client (xem GameDayManager.cs).
///
/// SETUP: dat script nay len CUNG object voi TeamWallet (hoac 1 object
/// rieng co NetworkObject) trong scene gameplay - y het huong dan cua
/// TeamRodUpgrade.
/// </summary>
public class DayStatsRecorder : NetworkBehaviour
{
    public static DayStatsRecorder Instance { get; private set; }

    private readonly List<DayStatsTransaction> _transactions =
        new List<DayStatsTransaction>();

    private int _fishSold;

    private int _baselineMoney;


    // ============================================================
    // UNITY
    // ============================================================

    public override void Spawned()
    {
        Instance = this;

        if (Object.HasStateAuthority)
        {
            _baselineMoney =
                TeamWallet.Instance != null
                    ? TeamWallet.Instance.Money
                    : 0;
        }
    }

    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }


    // ============================================================
    // RECORD
    // ============================================================

    /// <summary>
    /// Goi tu TeamWallet.TrySpend()/AddMoney() (Host) moi khi co giao
    /// dich CO TEN nguoi choi cu the. playerId la PlayerRef.PlayerId -
    /// dung de GOP giao dich theo TUNG NGUOI THUC SU (khong phu thuoc
    /// PlayerName, vi 2 nguoi co the trung ten hien thi). Truyen
    /// playerName rong ("") se bi bo qua - giong logic NotiFeedManager
    /// (VD ResetToStartingMoney() khong duoc tinh la 1 giao dich cua ai
    /// ca).
    /// </summary>
    public void RecordTransaction(
        int playerId,
        string playerName,
        int delta)
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (string.IsNullOrEmpty(playerName) || delta == 0)
            return;

        _transactions.Add(
            new DayStatsTransaction(
                playerId,
                DayStatsData.SanitizeName(playerName),
                delta
            )
        );
    }


    /// <summary>Goi tu GameplayPlayer.RPC_RequestSellFish() (Host) khi ban ca thanh cong.</summary>
    public void RecordFishSold(
        int count)
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (count <= 0)
            return;

        _fishSold += count;
    }


    // ============================================================
    // RESET (goi moi khi 1 ngay MOI THUC SU bat dau)
    // ============================================================

    /// <summary>
    /// Goi boi GameDayManager (CommitPendingDay() / StartNewRun()) MOI KHI
    /// mot ngay THUC SU bat dau - baseline duoc chup lai = tien team dang
    /// co DUNG luc do (that bai thi TeamWallet da ResetToStartingMoney()
    /// truoc roi, nen baseline se la tien khoi diem).
    /// </summary>
    public void ResetForNewDay()
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        _transactions.Clear();

        _fishSold = 0;

        _baselineMoney =
            TeamWallet.Instance != null
                ? TeamWallet.Instance.Money
                : 0;
    }


    // ============================================================
    // BUILD SUMMARY
    // ============================================================

    /// <summary>
    /// Dong goi toan bo du lieu da ghi trong ngay thanh DayStatsData de
    /// GameDayManager Serialize() va gui qua RPC. currentMoney nen truyen
    /// TeamWallet.Instance.Money tai thoi diem goi (= tien cuoi ngay).
    /// </summary>
    public DayStatsData BuildSummary(
        int finishedDay,
        int target,
        int currentMoney)
    {
        DayStatsData data = new DayStatsData
        {
            FinishedDay = finishedDay,
            Target = target,
            FishSold = _fishSold,
            Earnings = currentMoney - _baselineMoney,
            BaselineMoney = _baselineMoney,
        };

        data.Transactions.AddRange(_transactions);

        return data;
    }
}