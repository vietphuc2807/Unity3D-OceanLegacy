using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

/// <summary>
/// Quan ly viec ca team xac nhan "muon ve Bao Tang" bang cach giu phim
/// E o chiec xe (xem CarGoHomeInteractable.cs).
///
/// Flow:
///
/// Player giu E du _holdDuration tren CarGoHomeInteractable
///     |
///     v
/// GameplayPlayer.RPC_RequestGoHome() (InputAuthority -> StateAuthority)
///     |
///     v
/// CarGoHomeManager.ReportPlayerReadyToGoHome() (chi chay tren Host)
///     |
///     v
/// NEU co TU 2 PLAYER TRO LEN (expected > 1):
///     RPC_BroadcastGoHomeProgress() -> banner "X is ready... (1/4)"
///     Moi lan co nguoi xac nhan moi, timer timeout duoc dem lai tu dau.
/// NEU CHI CO 1 PLAYER (solo): bo qua banner, ve luon.
///     |
///     v
/// TIMEOUT: qua _confirmTimeoutSeconds ma khong co nguoi tiep theo
/// xac nhan -> ResetReadiness(): xoa het xac nhan, an banner, mo khoa
/// de MOI NGUOI phai giu E lai tu dau.
///     |
///     v
/// HET NGAY (GameDayManager.IsGoHomeBlocked): huy het xac nhan, an
/// banner, khong nhan xac nhan moi cho toi khi ngay moi bat dau.
///     |
///     v
/// Du nguoi -> RPC_AllPlayersReadyGoHome() -> GoHomeLoadingPanelUI.Show()
/// </summary>
public class CarGoHomeManager : NetworkBehaviour
{
    public static CarGoHomeManager Instance { get; private set; }


    // ============================================================
    // SETTINGS
    // ============================================================

    [Header("Confirm Timeout")]

    [Tooltip("Sau ngan nay giay ke tu lan xac nhan GAN NHAT ma chua du " +
             "nguoi, banner bien mat va moi nguoi phai giu E lai tu dau.")]
    [SerializeField]
    private float _confirmTimeoutSeconds = 10f;


    // ============================================================
    // NETWORKED
    // ============================================================

    [Networked]
    public int PlayersReadyCount { get; private set; }


    [Networked]
    private TickTimer _confirmTimeoutTimer { get; set; }


    // ============================================================
    // HOST-ONLY STATE
    // ============================================================

    private readonly HashSet<PlayerRef> _playersReady = new();

    private bool _hasTriggeredGoHome;


    // ============================================================
    // SPAWNED / DESPAWNED
    // ============================================================

    public override void Spawned()
    {
        Instance = this;
    }


    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }


    // ============================================================
    // FIXED UPDATE - TIMEOUT + HET NGAY
    // ============================================================

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        if (_hasTriggeredGoHome)
            return;

        if (_playersReady.Count == 0)
            return;

        // Het ngay -> huy het xac nhan dang co.
        if (GameDayManager.Instance != null &&
            GameDayManager.Instance.IsGoHomeBlocked)
        {
            ResetReadiness("het ngay");
            return;
        }

        // Qua thoi gian ma khong co nguoi tiep theo xac nhan.
        if (_confirmTimeoutTimer.Expired(Runner))
        {
            ResetReadiness("timeout");
        }
    }


    // ============================================================
    // REPORT PLAYER READY
    // ============================================================

    public void ReportPlayerReadyToGoHome(
        PlayerRef player,
        string playerName)
    {
        if (Object == null ||
            !Object.HasStateAuthority)
        {
            return;
        }


        if (_hasTriggeredGoHome)
            return;


        // Het ngay: khong nhan xac nhan, dong thoi go trang thai
        // "da xac nhan" o client (neu co) de sang ngay moi giu E lai duoc.
        if (GameDayManager.Instance != null &&
            GameDayManager.Instance.IsGoHomeBlocked)
        {
            ResetReadiness("het ngay (bi chan)");
            return;
        }


        // Moi player chi duoc tinh 1 lan.
        if (!_playersReady.Add(player))
            return;


        PlayersReadyCount =
            _playersReady.Count;


        // Co nguoi moi xac nhan -> dem lai timeout tu dau.
        _confirmTimeoutTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                _confirmTimeoutSeconds
            );


        int expected =
            Mathf.Max(
                1,
                Runner.ActivePlayers.Count()
            );


        Debug.Log(
            $"[CarGoHomeManager] Player {player} ({playerName}) " +
            $"muon ve Bao Tang. ({PlayersReadyCount}/{expected})"
        );


        // Choi solo thi khong can banner tien do.
        if (expected > 1)
        {
            RPC_BroadcastGoHomeProgress(
                playerName,
                PlayersReadyCount,
                expected
            );
        }


        if (PlayersReadyCount >= expected)
        {
            _hasTriggeredGoHome = true;

            _confirmTimeoutTimer = TickTimer.None;

            RPC_AllPlayersReadyGoHome();
        }
    }


    // ============================================================
    // RESET READINESS
    // ============================================================

    private void ResetReadiness(string reason)
    {
        Debug.Log(
            $"[CarGoHomeManager] Reset xac nhan ve nha ({reason})."
        );

        _playersReady.Clear();

        PlayersReadyCount = 0;

        _confirmTimeoutTimer = TickTimer.None;

        RPC_ResetGoHome();
    }


    // ============================================================
    // RPC - RESET (moi client: an banner + mo khoa giu E)
    // ============================================================

    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All
    )]
    private void RPC_ResetGoHome()
    {
        GoHomeAnnouncementUI.Instance?.Hide();

        CarGoHomeInteractable.ResetAllLocal();
    }


    // ============================================================
    // RPC - BROADCAST PROGRESS
    // ============================================================

    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All
    )]
    private void RPC_BroadcastGoHomeProgress(
        string playerName,
        int readyCount,
        int totalCount)
    {
        string message =
            $"{playerName} is ready to head back to the Museum! ({readyCount}/{totalCount})";

        GoHomeAnnouncementUI.Instance?.Show(
            message
        );
    }


    // ============================================================
    // RPC - ALL PLAYERS READY
    // ============================================================

    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All
    )]
    private void RPC_AllPlayersReadyGoHome()
    {
        GoHomeAnnouncementUI.Instance?.Hide();

        GoHomeLoadingPanelUI.Instance?.Show();
    }
}