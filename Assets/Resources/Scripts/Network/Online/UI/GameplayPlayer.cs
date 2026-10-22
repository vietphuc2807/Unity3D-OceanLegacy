using System;
using Fusion;
using UnityEngine;

public class GameplayPlayer : NetworkBehaviour, IChatSender
{
    public static GameplayPlayer LocalInstance { get; private set; }

    [Header("Fish Trader")]
    [Tooltip(
        "Catalog chứa tất cả ItemData — Host dùng để tra cá và tự " +
        "tính giá bán. Kéo cùng ItemCatalog mà PlayerHeldItem đang dùng."
    )]
    [SerializeField] private ItemCatalog _itemCatalog;

    [Networked]
    public NetworkString<_32> PlayerName { get; set; }


    // ============================================================
    // SPAWNED
    // ============================================================

    public override void Spawned()
    {
        Debug.Log(
            $"[GameplayPlayer][Spawned] " +
            $"Object={Object.Id} | " +
            $"InputAuthority={Object.InputAuthority} | " +
            $"LocalPlayer={Runner.LocalPlayer} | " +
            $"HasInputAuthority={Object.HasInputAuthority} | " +
            $"IsServer={Runner.IsServer} | " +
            $"Runner.GetPlayerObject={Runner.GetPlayerObject(Object.InputAuthority)?.Id} | " +
            $"Runner.GetPlayerObject(LocalPlayer)=" +
            $"{Runner.GetPlayerObject(Runner.LocalPlayer)?.Id}"
        );

        if (Object.HasInputAuthority)
        {
            LocalInstance = this;

            RPC_SetName(
                RoomSessionData.PlayerDisplayName
            );

            LobbyChatUI.Instance?.SetSender(this);
        }
    }


    // ============================================================
    // DESPAWNED
    // ============================================================

    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        Debug.Log("vai");

        if (Object.HasInputAuthority)
            LocalInstance = null;

        if (runner != null &&
            runner.IsRunning)
        {
            LobbyChatUI.Instance?.AddPlayerLeftGameMessage(
                PlayerName.ToString()
            );
        }
    }


    // ============================================================
    // CHAT
    // ============================================================

    public void SendChatMessage(
        string message)
    {
        if (!Object.HasInputAuthority)
            return;

        if (string.IsNullOrWhiteSpace(message))
            return;

        RPC_SendChatMessage(message);
    }


    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority
    )]
    private void RPC_SetName(
        string playerName)
    {
        PlayerName = playerName;

        RPC_BroadcastPlayerJoinedGame(
            playerName
        );

        // Báo cho GameDayManager biết 1 người nữa đã "vào game" xong —
        // khi đủ số người (RoomSessionData.PlayerCountAtStart) đã báo,
        // GameDayManager sẽ tự bắn message "Day 1" NGAY SAU message join
        // của người chơi cuối cùng ở trên.
        GameDayManager.Instance?.ReportPlayerJoinedGame(
            Object.InputAuthority
        );
    }


    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All
    )]
    private void RPC_BroadcastPlayerJoinedGame(
        string playerName)
    {
        LobbyChatUI.Instance?.AddPlayerJoinedGameMessage(
            playerName
        );
    }


    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority
    )]
    private void RPC_SendChatMessage(
        string message)
    {
        RPC_BroadcastChatMessage(
            PlayerName.ToString(),
            message
        );
    }


    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All
    )]
    private void RPC_BroadcastChatMessage(
        string playerName,
        string message)
    {
        LobbyChatUI.Instance?.AddMessage(
            playerName,
            message
        );
    }


    // ============================================================
    // TEAM MONEY / BUY BAITS
    // ============================================================

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority
    )]
    public void RPC_RequestBuyBaits(
        int totalPrice,
        string purchaseData)
    {
        if (!Object.HasStateAuthority)
            return;


        // --------------------------------------------------------
        // CHECK TEAM WALLET
        // --------------------------------------------------------

        if (TeamWallet.Instance == null)
        {
            RPC_BuyBaitsResult(
                false,
                "Team wallet is not ready.",
                purchaseData
            );

            return;
        }


        // --------------------------------------------------------
        // CHECK PRICE
        // --------------------------------------------------------

        if (totalPrice <= 0)
        {
            RPC_BuyBaitsResult(
                false,
                "Invalid purchase.",
                purchaseData
            );

            return;
        }


        // --------------------------------------------------------
        // CHECK MONEY
        // --------------------------------------------------------

        if (!TeamWallet.Instance.CanAfford(totalPrice))
        {
            RPC_BuyBaitsResult(
                false,
                "Not enough money.",
                purchaseData
            );

            return;
        }


        // --------------------------------------------------------
        // SPEND TEAM MONEY
        // --------------------------------------------------------

        // Truyền id + tên player để TeamWallet gộp đúng người và tạo NotiFeed.
        bool spent =
            TeamWallet.Instance.TrySpend(
                totalPrice,
                Object.InputAuthority.PlayerId,
                PlayerName.ToString()
            );

        if (!spent)
        {
            RPC_BuyBaitsResult(
                false,
                "Purchase failed.",
                purchaseData
            );

            return;
        }


        // --------------------------------------------------------
        // PURCHASE SUCCESS
        // --------------------------------------------------------

        RPC_BuyBaitsResult(
            true,
            "",
            purchaseData
        );
    }


    // ============================================================
    // BUY BAITS RESULT
    // ============================================================

    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.InputAuthority
    )]
    private void RPC_BuyBaitsResult(
        bool success,
        string message,
        string purchaseData)
    {
        if (!success)
        {
            BaitShopUI.Instance?.ShowPurchaseFailed(
                message
            );

            return;
        }

        BaitShopUI.Instance?.CompletePurchaseFromNetwork(
            purchaseData
        );
    }


    // ============================================================
    // SELL FISH
    // ============================================================

    private const int MaxFishPerSale = 6;


    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority
    )]
    public void RPC_RequestSellFish(
        string saleData)
    {
        if (!Object.HasStateAuthority)
            return;


        if (TeamWallet.Instance == null)
        {
            RPC_SellFishResult(
                false,
                "Team wallet is not ready.",
                saleData,
                0
            );

            return;
        }


        if (_itemCatalog == null)
        {
            Debug.LogError(
                "[GameplayPlayer] Chưa gán Item Catalog — " +
                "không thể tính giá bán cá."
            );

            RPC_SellFishResult(
                false,
                "Trader is not ready.",
                saleData,
                0
            );

            return;
        }


        int totalEarned = 0;
        int fishCount = 0;

        bool valid =
            !string.IsNullOrEmpty(
                saleData
            );


        if (valid)
        {
            string[] entries =
                saleData.Split('|');

            foreach (string entry in entries)
            {
                string[] pair =
                    entry.Split(':');

                if (pair.Length != 2 ||
                    string.IsNullOrEmpty(pair[0]) ||
                    !float.TryParse(
                        pair[1],
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float size))
                {
                    valid = false;
                    break;
                }


                ItemData item =
                    _itemCatalog.GetById(
                        pair[0]
                    );


                if (item == null ||
                    !item.IsFish ||
                    item.FishInfo == null)
                {
                    valid = false;
                    break;
                }


                FishData fish =
                    item.FishInfo;


                size =
                    Mathf.Clamp(
                        size,
                        fish.MinSizeMultiplier,
                        fish.MaxSizeMultiplier
                    );


                totalEarned +=
                    fish.GetSellPrice(
                        size
                    );

                fishCount++;
            }
        }


        if (!valid ||
            fishCount <= 0 ||
            fishCount > MaxFishPerSale ||
            totalEarned <= 0)
        {
            RPC_SellFishResult(
                false,
                "Invalid sale.",
                saleData,
                0
            );

            return;
        }


        // --------------------------------------------------------
        // ADD TEAM MONEY
        // --------------------------------------------------------

        // Truyền id + tên player để TeamWallet gộp đúng người và hiện NotiFeed +$Amount.
        TeamWallet.Instance.AddMoney(
            totalEarned,
            Object.InputAuthority.PlayerId,
            PlayerName.ToString()
        );

        // Cong don so ca team da ban trong ngay - dung cho Stats Panel
        // cuoi ngay (xem DayStatsRecorder.RecordFishSold()).
        DayStatsRecorder.Instance?.RecordFishSold(
            fishCount
        );


        // --------------------------------------------------------
        // SALE SUCCESS
        // --------------------------------------------------------

        RPC_SellFishResult(
            true,
            "",
            saleData,
            totalEarned
        );
    }


    // ============================================================
    // SELL FISH RESULT
    // ============================================================

    [Rpc(
    RpcSources.StateAuthority,
    RpcTargets.InputAuthority
    )]
    private void RPC_SellFishResult(
        bool success,
        string message,
        string saleData,
        int totalEarned)
    {
        Debug.Log($"[DEBUG-FISH] RPC_SellFishResult RECEIVED. " +
                $"Machine={(Runner.IsServer ? "HOST" : "CLIENT")}, " +
                $"ObjectId={Object.Id}, InputAuthority={Object.InputAuthority}, " +
                $"success={success}, totalEarned={totalEarned}");

        if (!success)
        {
            FishTraderUI.Instance?.ShowSaleFailed(message);
            return;
        }

        FishTraderUI.Instance?.CompleteSaleFromNetwork(saleData, totalEarned);
    }


    // ============================================================
    // ROD UPGRADE
    // ============================================================
    //
    // Client bấm nút Level kế tiếp trong RodUpgradeUI
    //      ↓
    // RPC_RequestUpgradeRod(targetLevel)
    //      ↓
    // Host: kiểm tra TeamRodUpgrade.IsNextLevel + TeamWallet đủ tiền
    //      ↓
    // Trừ tiền team (TeamWallet.TrySpend) → set RodLevel (TeamRodUpgrade)
    //      ↓
    // RPC_UpgradeRodResult → RodUpgradeUI đóng panel
    // (mọi client tự thấy RodLevel mới vì đó là [Networked] property,
    // không cần broadcast riêng).
    //
    // ============================================================

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority
    )]
    public void RPC_RequestUpgradeRod(
        int targetLevel)
    {
        if (!Object.HasStateAuthority)
            return;


        // --------------------------------------------------------
        // CHECK TEAM WALLET / ROD UPGRADE READY
        // --------------------------------------------------------

        if (TeamWallet.Instance == null)
        {
            RPC_UpgradeRodResult(
                false,
                "Team wallet is not ready.",
                targetLevel
            );

            return;
        }


        if (TeamRodUpgrade.Instance == null)
        {
            RPC_UpgradeRodResult(
                false,
                "Rod upgrade is not ready.",
                targetLevel
            );

            return;
        }


        // --------------------------------------------------------
        // CHECK LEVEL TUẦN TỰ
        // --------------------------------------------------------

        if (!TeamRodUpgrade.Instance.IsNextLevel(targetLevel))
        {
            RPC_UpgradeRodResult(
                false,
                "Invalid upgrade level.",
                targetLevel
            );

            return;
        }


        // --------------------------------------------------------
        // CHECK PRICE / MONEY
        // --------------------------------------------------------

        int price =
            TeamRodUpgrade.Instance.GetPrice(
                targetLevel
            );


        if (price <= 0)
        {
            RPC_UpgradeRodResult(
                false,
                "Invalid price.",
                targetLevel
            );

            return;
        }


        if (!TeamWallet.Instance.CanAfford(price))
        {
            RPC_UpgradeRodResult(
                false,
                "Not enough money.",
                targetLevel
            );

            return;
        }


        // --------------------------------------------------------
        // SPEND TEAM MONEY
        // --------------------------------------------------------

        // Truyền id + tên player để TeamWallet gộp đúng người và hiện NotiFeed -$Price.
        bool spent =
            TeamWallet.Instance.TrySpend(
                price,
                Object.InputAuthority.PlayerId,
                PlayerName.ToString()
            );


        if (!spent)
        {
            RPC_UpgradeRodResult(
                false,
                "Purchase failed.",
                targetLevel
            );

            return;
        }


        // --------------------------------------------------------
        // SET NEW LEVEL
        // --------------------------------------------------------

        bool leveled =
            TeamRodUpgrade.Instance.TrySetLevel(
                targetLevel
            );


        if (!leveled)
        {
            // Hoàn tiền nếu vì lý do gì đó set level thất bại.
            // Truyền id + tên player để Notice hoàn tiền cũng hiển thị và gộp đúng người.
            TeamWallet.Instance.AddMoney(
                price,
                Object.InputAuthority.PlayerId,
                PlayerName.ToString()
            );


            RPC_UpgradeRodResult(
                false,
                "Upgrade failed.",
                targetLevel
            );

            return;
        }


        // --------------------------------------------------------
        // BROADCAST UPGRADE
        // --------------------------------------------------------

        // Báo cho TẤT CẢ client biết ai vừa nâng cấp.
        RPC_BroadcastRodUpgraded(
            PlayerName.ToString(),
            targetLevel
        );


        RPC_UpgradeRodResult(
            true,
            "",
            targetLevel
        );
    }


    // ============================================================
    // ROD UPGRADE RESULT
    // ============================================================

    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.InputAuthority
    )]
    private void RPC_UpgradeRodResult(
        bool success,
        string message,
        int targetLevel)
    {
        if (!success)
        {
            RodUpgradeUI.Instance?.ShowUpgradeFailed(
                message
            );

            return;
        }


        RodUpgradeUI.Instance?.CompleteUpgradeFromNetwork(
            targetLevel
        );
    }


    // ============================================================
    // ROD UPGRADE → CHAT
    // ============================================================
    //
    // Kết quả trong chat, ví dụ:
    //
    // SERVER: Phuc đã nâng cấp cần câu lên Level 2
    //
    // ============================================================

    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All
    )]
    private void RPC_BroadcastRodUpgraded(
        string playerName,
        int newLevel)
    {
        string message =
            $"{playerName} upgraded fishing rod - Level {newLevel}!";

        LobbyChatUI.Instance?.AddServerMessage(
            message
        );
    }


    // ============================================================
    // FISH CAUGHT → CHAT
    // ============================================================
    //
    // Player bắt cá
    //      ↓
    // PlayerInventory
    //      ↓
    // RPC_ReportFishCaught()
    //      ↓
    // Host / StateAuthority
    //      ↓
    // RPC_BroadcastFishCaught()
    //      ↓
    // Tất cả client
    //      ↓
    // LobbyChatUI
    //
    // Kết quả:
    //
    // SERVER: Phuc ~ Largemouth Bass [4.25 Kg]
    //
    // ============================================================

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority
    )]
    public void RPC_ReportFishCaught(
        string fishName,
        float weightKg)
    {
        if (!Object.HasStateAuthority)
            return;


        string playerName =
            PlayerName.ToString();


        RPC_BroadcastFishCaught(
            playerName,
            fishName,
            weightKg
        );
    }


    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All
    )]
    private void RPC_BroadcastFishCaught(
        string playerName,
        string fishName,
        float weightKg)
    {
        string weightText =
            FormatFishWeight(
                weightKg
            );


        string message =
            $"{playerName} ~ {fishName} [{weightText} Kg]";


        // Gửi vào hệ thống chat dưới dạng SERVER message.
        LobbyChatUI.Instance?.AddServerMessage(
            message
        );
    }


    // ============================================================
    // FORMAT FISH WEIGHT
    // ============================================================

    private static string FormatFishWeight(
        float weightKg)
    {
        if (weightKg >= 10f)
        {
            return weightKg.ToString(
                "0.0",
                System.Globalization.CultureInfo.InvariantCulture
            );
        }


        return weightKg.ToString(
            "0.##",
            System.Globalization.CultureInfo.InvariantCulture
        );
    }


    // ============================================================
    // STATS PANEL - XAC NHAN "E" DE TIEP TUC
    // ============================================================
    //
    // Nguoi choi bam "E" o DayStatsPanel (local)
    //      ↓
    // RPC_ReportStatsAcknowledged()
    //      ↓
    // Host: GameDayManager.ReportPlayerAckedStats(InputAuthority cua
    // chinh player nay) - khi DU tat ca player active da bam, Host moi
    // cho qua Day Panel (xem GameDayManager.ProceedAfterStatsAck()).
    //
    // ============================================================

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority
    )]
    public void RPC_ReportStatsAcknowledged()
    {
        if (!Object.HasStateAuthority)
            return;

        GameDayManager.Instance?.ReportPlayerAckedStats(
            Object.InputAuthority
        );
    }


    // ============================================================
    // GO HOME (xe) - XAC NHAN "GIU E" O CarGoHomeInteractable
    // ============================================================
    //
    // Nguoi choi giu "E" du _holdDuration tren chiec xe
    // (CarGoHomeInteractable.CompleteHold())
    //      ↓
    // RPC_RequestGoHome()
    //      ↓
    // Host: CarGoHomeManager.ReportPlayerReadyToGoHome(InputAuthority
    // cua chinh player nay, PlayerName) - khi DU tat ca player active
    // da xac nhan, CarGoHomeManager tu broadcast RPC rieng cua no de
    // hien GoHomeLoadingPanelUI tren moi client (xem CarGoHomeManager.cs).
    //
    // ============================================================

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority
    )]
    public void RPC_RequestGoHome()
    {
        if (!Object.HasStateAuthority)
            return;

        CarGoHomeManager.Instance?.ReportPlayerReadyToGoHome(
            Object.InputAuthority,
            PlayerName.ToString()
        );
    }
}