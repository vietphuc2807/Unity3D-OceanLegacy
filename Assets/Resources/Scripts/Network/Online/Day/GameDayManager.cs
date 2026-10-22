using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

/// <summary>
/// Quan ly vong lap ngay.
///
/// Flow chuyen ngay:
///
/// DAY CU chay het
///     |
///     v
/// Cho DU nguoi trong nha VA cua DA DONG (xem CheckReadyToEndDay())
///     |
///     v
/// Tinh ket qua
///     |
///     v
/// Luu Pending Day / Pending Target
///     |
///     v
/// (TUY CHON, mac dinh TAT) Show Stats Panel, cho tat ca bam "E"
/// -> xem _showStatsPanelOnSuccess
///     |
///     v
/// Show DAY Panel
///     |
///     v
/// Panel fade -> den 100%
///     |
///     v
/// COMMIT DAY MOI (Day / MoneyTarget) - CHUA start timer
///     |
///     v
/// Giu man hinh den, panel fade out
///     |
///     v
/// Panel bien mat HOAN TOAN
///     |
///     v
/// Reset timer = dau ngay (DUNG luc nay, khong som hon)
///     |
///     v
/// UI / Lighting / Skybox thay trang thai moi
///     |
///     v
/// Broadcast "Day 2 begins..." (hoac "A new adventure begins..." neu la
/// luc bat dau run - xem FIX 3 ben duoi)
/// </summary>
public class GameDayManager : NetworkBehaviour
{
    public static GameDayManager Instance { get; private set; }


    // ============================================================
    // MONEY TARGET
    // ============================================================

    [Header("Money Target")]

    [SerializeField]
    private int _startingMoneyTargetPerPlayer = 50;

    [Tooltip("He so nhan TOI THIEU ap len so tien team DANG CO de ra target " +
             "ngay ke tiep. Phai > 1 de target luon LUON cao hon tien hien " +
             "co (khong bao gio co ngay 'mien phi').")]
    [SerializeField]
    private float _minDifficultyMultiplier = 1.2f;

    [Tooltip("He so nhan TOI DA (ung voi team kiem tien RAT gioi o ngay " +
             "truoc, performance ratio cao) ap len so tien dang co.")]
    [SerializeField]
    private float _maxDifficultyMultiplier = 2f;

    [Tooltip("Performance ratio (tien kiem duoc / target ngay do) tuong ung " +
             "voi moc TOI DA o tren. Vi du 3 nghia la kiem gap 3 lan target " +
             "tro len thi coi nhu 'gioi toi da'.")]
    [SerializeField]
    private float _performanceRatioForMaxDifficulty = 3f;

    [Tooltip("Moi ngay troi qua (ke tu Day 2), do kho tu cong them bao " +
             "nhieu % vao he so nhan, KHONG phu thuoc performance - de " +
             "game cang choi lau cang kho, ke ca khi team chi kiem vua du.")]
    [SerializeField]
    private float _dayDifficultyGrowthPerDay = 0.05f;

    [Tooltip("Gioi han toi da phan cong them do so ngay (tranh multiplier " +
             "tang vo han neu choi qua nhieu ngay).")]
    [SerializeField]
    private float _maxDayDifficultyBonus = 1.2f;


    // ============================================================
    // DAY DURATION
    // ============================================================

    [Header("Day Duration")]

    [SerializeField]
    private float _realSecondsPerGameDay = 600f;

    [SerializeField]
    private float _dayStartHour = 5.5f;

    [SerializeField]
    private float _dayEndHour = 19f;

    [Tooltip("Gio (24h, VD 18 = 6 gio toi) BAT DAU canh bao nhay do " +
             "'sap het ngay'. Nhay lien tuc tu moc nay cho toi khi het " +
             "ngay (_dayEndHour). VD: 18 -> nhay tu 6h toi toi 7h toi.")]
    [SerializeField]
    private float _urgentStartHour = 18f;


    // ============================================================
    // RUN START
    // ============================================================

    [Header("Run Start Notification")]

    [Tooltip(
        "FIX: day gio CHI la LUOI AN TOAN cuoi cung (VD 1 player bi " +
        "crash/khong bao gio spawn duoc), KHONG con la duong chay chinh " +
        "nua - vi ReportPlayerJoinedGame() se TU GIA HAN lai timer nay " +
        "moi khi co them 1 player bao joined (xem ReportPlayerJoinedGame). " +
        "Vi vay co the de so lon (30-60s) ma khong so client load lau bi " +
        "'bo lai' som nhu truoc."
    )]
    [SerializeField]
    private float _runStartFallbackSeconds = 30f;

    [SerializeField]
    private float _dayIntroFallbackSeconds = 6f;

    [SerializeField]
    private float _runStartAnnounceDelaySeconds = 2f;


    // ============================================================
    // HOUSE DOOR
    // ============================================================

    [Header("House Door")]

    [Tooltip("Cua nha - phai DONG (IsDoorOpen == false) thi moi duoc " +
             "tinh la qua ngay moi, ke ca khi da du nguoi trong nha. " +
             "Neu de trong, se bo qua dieu kien cua (giu hanh vi cu).")]
    [SerializeField]
    private DoorInteractable _houseDoor;


    // ============================================================
    // DAY STATS PANEL
    // ============================================================

    [Header("Day Stats Panel")]

    [Tooltip("TAT (mac dinh): qua ngay THANH CONG thi hien THANG Day Panel " +
             "(ngay moi), KHONG hien Stats Panel. BAT: hien Stats Panel " +
             "truoc, cho tat ca player bam 'E' roi moi qua Day Panel (flow " +
             "cu). Cac setting ben duoi (_useStatsAckFallback, " +
             "_statsAckFallbackSeconds) chi co tac dung khi bat o day.")]
    [SerializeField]
    private bool _showStatsPanelOnSuccess = false;

    [Tooltip("BAT: neu qua thoi gian _statsAckFallbackSeconds ma van CHUA " +
             "du nguoi bam 'E', se TU DONG cho qua Day Panel (phong AFK/mat " +
             "ket noi). TAT (mac dinh): LUON LUON cho DU TAT CA player active " +
             "bam 'E' moi qua ngay moi, khong co gioi han thoi gian - neu 1 " +
             "nguoi AFK hoai khong bam thi man hinh se dung yen tai Stats " +
             "Panel cho toi khi ho bam hoac roi phong (ActivePlayers giam " +
             "xuong se tu dieu chinh lai so nguoi can bam - xem " +
             "ExpectedActivePlayerCount).")]
    [SerializeField]
    private bool _useStatsAckFallback = false;

    [Tooltip("Chi co tac dung khi _useStatsAckFallback = true. Neu qua thoi " +
             "gian nay ma van CHUA du nguoi bam 'E' de xac nhan da xem Stats " +
             "Panel (VD co nguoi AFK/mat ket noi), tu dong cho qua Day Panel " +
             "de khong bi ket man hinh.")]
    [SerializeField]
    private float _statsAckFallbackSeconds = 20f;


    // ============================================================
    // NETWORKED
    // ============================================================

    [Networked]
    public int CurrentDay { get; private set; }


    [Networked]
    public int MoneyTarget { get; private set; }


    [Networked]
    private TickTimer _dayTimer { get; set; }


    [Networked]
    private TickTimer _runStartFallbackTimer { get; set; }


    [Networked]
    private TickTimer _runStartDelayTimer { get; set; }


    /// <summary>
    /// True trong luc DayStatsPanel dang hien (chi xay ra sau ngay
    /// THANH CONG, va chi khi _showStatsPanelOnSuccess = true) va Host
    /// dang cho tung player bam "E" xac nhan. Panel o MOI client poll co
    /// nay: con true thi con hien, ve false thi tu fade an (xem
    /// DayStatsPanel.Update()).
    /// </summary>
    [Networked]
    public NetworkBool IsWaitingForStatsAck { get; private set; }


    /// <summary>So player da bam "E" xac nhan Stats Panel cua ngay hien tai.</summary>
    [Networked]
    public int PlayersAckedStatsCount { get; private set; }


    [Networked]
    private TickTimer _statsAckFallbackTimer { get; set; }


    // ============================================================
    // CALL PLAYERS HOME (het gio, cho du nguoi vao nha + cua dong)
    // ============================================================

    /// <summary>
    /// True tu luc timer trong ngay het gio (7h toi) cho toi khi DU tat
    /// ca player dang active da buoc vao house trigger zone VA cua da
    /// dong. UI (GameDayHomeCallUI) poll co nay de hien/an text keu goi
    /// ve nha.
    /// </summary>
    [Networked]
    public NetworkBool IsWaitingForPlayersHome { get; private set; }


    /// <summary>
    /// True khi DA DU nguoi trong nha nhung CUA VAN DANG MO - chi con
    /// thieu buoc dong cua de qua ngay moi. UI dung co nay de doi
    /// message tu "con thieu N nguoi" sang "dong cua lai di!".
    /// </summary>
    [Networked]
    public NetworkBool IsWaitingForDoorClose { get; private set; }


    /// <summary>
    /// So player hien dang dung trong house trigger zone. Networked de
    /// UI o MOI client deu thay so dem giong nhau (VD "2/4").
    /// </summary>
    [Networked]
    public int PlayersInsideHouseCount { get; private set; }


    /// <summary>
    /// Tap hop player dang dung trong nha - CHI ton tai/dung nghia tren
    /// Host (khong networked, vi chi Host can biet CHINH XAC ai dang o
    /// trong de quyet dinh khi nao du nguoi). Client chi can xem con so
    /// PlayersInsideHouseCount o tren de hien thi UI.
    /// </summary>
    private readonly HashSet<PlayerRef> _playersInsideHouse = new();


    /// <summary>
    /// Tap hop player DA bam "E" xac nhan Stats Panel cua ngay hien tai -
    /// giong pattern _playersInsideHouse o tren, chi ton tai/dung nghia
    /// tren Host.
    /// </summary>
    private readonly HashSet<PlayerRef> _playersAckedStats = new();


    // ============================================================
    // LOCAL PENDING STATE
    // ============================================================

    /// <summary>
    /// Day se duoc ap dung sau khi panel den hoan toan.
    ///
    /// LUU Y: field nay CHI dung/duoc set tren Host (EvaluateDayEnd()
    /// chi chay khi co StateAuthority). Client KHONG bao gio thay gia
    /// tri dung cua field nay - vi vay UI khong duoc doc truc tiep field
    /// nay (hoac PendingDisplayDay ben duoi) o phia Client cho case
    /// chuyen ngay; xem RPC_ShowDayResultPanel, gia tri hien thi phai
    /// lay tu tham so cua RPC (da duoc Fusion dong bo) thay vi field nay.
    /// </summary>
    private int _pendingDay;


    /// <summary>
    /// Money Target se duoc ap dung sau khi panel den hoan toan.
    /// </summary>
    private int _pendingMoneyTarget;


    private bool _hasPendingDay;


    // ============================================================
    // PENDING DAY RESULT MESSAGE
    // ============================================================

    /// <summary>
    /// Ket qua Day dang cho hien thi.
    ///
    /// Khong broadcast ngay khi EvaluateDayEnd().
    /// Chi broadcast sau khi DAY panel da hoan tat.
    /// </summary>
    private bool _pendingDayResult;


    private bool _pendingDayResultSuccess;


    private int _pendingDayResultDay;


    private int _pendingDayResultTarget;


    /// <summary>
    /// FIX 3: True tu luc Host quyet dinh se bat dau run (het cho du
    /// nguoi join) cho toi khi message "A new adventure begins..." THUC
    /// SU duoc broadcast. Message nay KHONG con gui ngay trong
    /// RPC_NotifyRunStart nua - no chi duoc gui SAU KHI Day 1 Intro
    /// Panel da hien xong va bien mat hoan toan (giong het co che
    /// _pendingDayResult o tren dung cho cac lan chuyen ngay sau), de
    /// dong bao "Adventure begins" luon xuat hien SAU khi nguoi choi da
    /// thay xong hieu ung "DAY 1", khong con hien truoc/de len panel
    /// chao mung nua.
    /// </summary>
    private bool _pendingRunStartMessage;


    // ============================================================
    // LOCAL STATE
    // ============================================================

    private bool _isEvaluatingDayEnd;

    private bool _dayTimerStarted;

    private int _scaledStartingTarget;

    private int _playersJoinedCount;

    // Moi PlayerRef chi duoc tinh 1 lan khi GameplayPlayer da spawn thanh cong.
    // Quan trong vi GameplayPlayerSpawner co watchdog co the thu spawn lai.
    private readonly HashSet<PlayerRef> _playersReportedJoined = new();

    private bool _hasAnnouncedRunStart;

    private bool _pendingRunStartAnnounce;

    private Coroutine _dayIntroFallbackCoroutine;


    // ============================================================
    // PUBLIC
    // ============================================================

    /// <summary>
    /// True tu moc _urgentStartHour (VD 18 = 6h toi) cho toi het ngay -
    /// dung de nhay do dong ho canh bao sap het ngay.
    /// </summary>
    public bool IsUrgent =>
        GetCurrentHour24() >= _urgentStartHour;

    public bool IsGoHomeBlocked =>
    IsWaitingForPlayersHome ||
    IsWaitingForStatsAck ||
    IsWaitingForDayPanel ||
    (GameDayIntroPanel.Instance != null &&
     GameDayIntroPanel.Instance.IsVisible);


    /// <summary>
    /// Day ma panel DAY se hien thi.
    ///
    /// Neu dang chuyen ngay:
    ///     Pending Day
    ///
    /// Binh thuong:
    ///     CurrentDay
    ///
    /// LUU Y (quan trong): field _pendingDay dung boi getter nay CHI
    /// duoc set dung tren HOST (xem EvaluateDayEnd()). Tren CLIENT,
    /// _hasPendingDay luon la false nen getter nay se fallback ve
    /// CurrentDay - dieu nay gay ra bug Host/Client hien khac so ngay
    /// khi dung cho case chuyen ngay. CHI dung property nay cho case
    /// Show(false) (bat dau run) hoac cac cho hien thi KHONG can chinh
    /// xac tuyet doi giua cac client. Voi RPC_ShowDayResultPanel, PHAI
    /// truyen thang gia tri newDay cua RPC vao GameDayIntroPanel.Show(
    /// bool, int) thay vi doc property nay.
    /// </summary>
    public int PendingDisplayDay
    {
        get
        {
            if (_hasPendingDay)
                return _pendingDay;

            return CurrentDay > 0
                ? CurrentDay
                : 1;
        }
    }


    /// <summary>
    /// True khi GameDayManager dang cho panel den
    /// de commit ngay moi.
    /// </summary>
    public bool IsWaitingForDayPanel
    {
        get
        {
            return _hasPendingDay;
        }
    }


    /// <summary>
    /// True tu luc het gio trong ngay (dayTimer expired, bat dau cho
    /// nguoi ve nha) cho toi khi ngay moi THUC SU bat dau (panel bien
    /// mat hoan toan, CommitPendingDay da chay). Dung boi
    /// FishingCastController de chan KHOI TAO cu cau MOI trong khoang
    /// nay - luot cau dang do (dang charge/bite/reel) khong bi anh
    /// huong, chi chan lan tha can TIEP THEO.
    /// </summary>
    public bool IsDayOver =>
        IsWaitingForPlayersHome || IsWaitingForDayPanel;


    /// <summary>
    /// So player active hien tai - dung boi DayStatsPanel de hien text
    /// "Waiting for players (X/Y)...". Tinh truc tiep tu Runner nen
    /// giong nhau tren moi client, khong can them field [Networked] rieng.
    /// </summary>
    public int ExpectedActivePlayerCount =>
        Runner != null
            ? Runner.ActivePlayers.Count()
            : 0;


    // ============================================================
    // EVENTS
    // ============================================================

    public static event Action OnRunStarted;

    public static event Action OnDayFailureReset;


    // ============================================================
    // UNITY
    // ============================================================

    private void OnEnable()
    {
        GameDayIntroPanel.OnDayPanelFullyBlack +=
            HandleDayPanelFullyBlack;

        GameDayIntroPanel.OnDayIntroFinished +=
            HandleDayIntroFinished;
    }


    private void OnDisable()
    {
        GameDayIntroPanel.OnDayPanelFullyBlack -=
            HandleDayPanelFullyBlack;

        GameDayIntroPanel.OnDayIntroFinished -=
            HandleDayIntroFinished;
    }


    // ============================================================
    // DAY PANEL FULLY BLACK
    // ============================================================

    private void HandleDayPanelFullyBlack()
    {
        if (Object == null ||
            !Object.HasStateAuthority)
        {
            return;
        }


        if (!_hasPendingDay)
            return;


        // ========================================================
        // COMMIT DAY MOI
        // ========================================================

        CommitPendingDay();
    }


    // ============================================================
    // COMMIT PENDING DAY
    // ============================================================

    private void CommitPendingDay()
    {
        if (!_hasPendingDay)
            return;


        // ========================================================
        // APPLY DAY
        // ========================================================

        CurrentDay =
            _pendingDay;

        MoneyTarget =
            _pendingMoneyTarget;

        _hasPendingDay = false;


        // ========================================================
        // RESET DAY STATS RECORDER - ngay moi bat dau dem lai tu dau,
        // baseline = tien team dang co NGAY LUC NAY (that bai thi
        // TeamWallet.ResetToStartingMoney() da chay truoc do trong
        // EvaluateDayEnd(), nen baseline se dung la tien khoi diem).
        // ========================================================

        DayStatsRecorder.Instance?.ResetForNewDay();


        // ========================================================
        // KHONG start Day Timer o day - xem HandleDayIntroFinished().
        // ========================================================
        //
        // LY DO (fix bug "ngay moi dem tu 6h/7h thay vi 5h30"):
        //
        // Neu start timer NGAY LUC nay (panel vua den 100%), thoi
        // gian THUC van tiep tuc troi qua trong luc panel con HOLD
        // DEN + FADE OUT (1-2+ giay thuc te, tuy Inspector). Timer
        // ngay moi da chay ngam suot khoang do, nen khi panel bien
        // mat va nguoi choi thuc su nhin thay dong ho, no da nhay
        // qua moc _dayStartHour (5:30 sang) roi - va moi lan chuyen
        // ngay lai cong don them (Day 2 lech +30p, Day 3 lech +60p...).
        //
        // Timer that su chi duoc start trong HandleDayIntroFinished(),
        // DUNG luc panel da bien mat HOAN TOAN - giong het cach Day 1
        // hoat dong. Nho dong nhat cach nay, ngay nao cung bat dau
        // DUNG _dayStartHour, khong bi lech/cong don nua.
        //

        Debug.Log(
            $"[GameDayManager] " +
            $"Day committed while panel is fully black. " +
            $"Day={CurrentDay}, " +
            $"Target=${MoneyTarget}. " +
            $"Timer se start khi panel bien mat hoan toan."
        );
    }


    // ============================================================
    // DAY INTRO FINISHED
    // ============================================================

    private void HandleDayIntroFinished()
    {
        if (Object == null ||
            !Object.HasStateAuthority)
        {
            return;
        }


        // ========================================================
        // START TIMER O DAY - panel da bien mat HOAN TOAN, dam bao
        // ngay nao cung bat dau DUNG _dayStartHour.
        // ========================================================

        if (!_dayTimerStarted)
        {
            BeginDayCountdown();
        }


        // ========================================================
        // FIX 3: BROADCAST "ADVENTURE BEGINS" (chi luc BAT DAU RUN)
        // ========================================================
        //
        // Day la Day 1 Intro Panel (Show(false)) vua fade xong LAN DAU
        // TIEN cua run nay. Neu dang co pending message run-start, day
        // moi la luc gui - dam bao message luon xuat hien SAU khi
        // nguoi choi da thay xong hieu ung "DAY 1", khong con bi hien
        // truoc/de len panel nua.
        // ========================================================

        if (_pendingRunStartMessage)
        {
            _pendingRunStartMessage = false;

            RPC_BroadcastRunStartMessage();
        }


        // ========================================================
        // BROADCAST DAY RESULT (cac lan CHUYEN NGAY sau, khong phai
        // luc bat dau run - 2 co nay khong bao gio cung true 1 luc).
        // ========================================================
        //
        // QUAN TRONG:
        //
        // Message "Day 2 begins..."
        // CHI duoc gui SAU KHI panel DAY da bien mat.
        //
        // ========================================================

        if (_pendingDayResult)
        {
            _pendingDayResult = false;

            RPC_BroadcastDayResult(
                _pendingDayResultSuccess,
                _pendingDayResultDay,
                _pendingDayResultTarget
            );
        }
    }


    // ============================================================
    // BEGIN DAY COUNTDOWN
    // ============================================================

    private void BeginDayCountdown()
    {
        if (_dayTimerStarted)
            return;

        _dayTimerStarted = true;

        StartDayTimer();
    }


    // ============================================================
    // FALLBACK
    // ============================================================

    private IEnumerator DayIntroFallbackRoutine()
    {
        yield return new WaitForSecondsRealtime(
            _dayIntroFallbackSeconds
        );


        if (!_dayTimerStarted)
        {
            BeginDayCountdown();
        }


        // ========================================================
        // FIX 3: luoi an toan - neu vi ly do gi do
        // GameDayIntroPanel.OnDayIntroFinished KHONG bao gio bac ra
        // (VD panel instance null tren 1 may nao do), dam bao message
        // "Adventure begins" van duoc gui sau fallback nay, khong bi
        // mat trang vinh vien.
        // ========================================================

        if (_pendingRunStartMessage)
        {
            _pendingRunStartMessage = false;

            RPC_BroadcastRunStartMessage();
        }
    }


    // ============================================================
    // SPAWNED
    // ============================================================

    public override void Spawned()
    {
        Instance = this;


        if (Object.HasStateAuthority)
        {
            StartNewRun();
        }
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
    // FIXED UPDATE
    // ============================================================

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;


        // ========================================================
        // RUN START FALLBACK
        // ========================================================

        if (!_hasAnnouncedRunStart &&
            !_pendingRunStartAnnounce &&
            _runStartFallbackTimer.Expired(Runner))
        {
            ScheduleRunStartAnnounce();
        }


        // ========================================================
        // RUN START DELAY
        // ========================================================

        if (_pendingRunStartAnnounce &&
            _runStartDelayTimer.Expired(Runner))
        {
            _pendingRunStartAnnounce = false;

            _hasAnnouncedRunStart = true;


            // ----------------------------------------------------
            // FIX 3: KHONG gui message o day nua. Chi danh dau "co
            // message dang cho" - RPC_NotifyRunStart ben duoi gio chi
            // con nhiem vu bac OnRunStarted (an overlay -> Day 1 Intro
            // Panel se tu hien len). Message that su chi duoc gui SAU
            // KHI panel do fade xong, trong HandleDayIntroFinished()
            // (hoac fallback trong DayIntroFallbackRoutine() neu event
            // khong bac ra vi ly do nao do).
            // ----------------------------------------------------

            _pendingRunStartMessage = true;


            RPC_NotifyRunStart();


            if (_dayIntroFallbackCoroutine != null)
            {
                StopCoroutine(
                    _dayIntroFallbackCoroutine
                );
            }


            _dayIntroFallbackCoroutine =
                StartCoroutine(
                    DayIntroFallbackRoutine()
                );
        }


        // ========================================================
        // DANG CHO STATS PANEL (chi xay ra sau ngay THANH CONG khi
        // _showStatsPanelOnSuccess = true) - cho tat ca player bam "E",
        // hoac fallback het gio thi tu cho qua.
        // ========================================================

        if (IsWaitingForStatsAck)
        {
            if (_useStatsAckFallback &&
                _statsAckFallbackTimer.Expired(Runner))
            {
                ProceedAfterStatsAck();
            }

            return;
        }


        // ========================================================
        // DANG CHO PANEL DEN
        // ========================================================

        if (_hasPendingDay)
            return;


        // ========================================================
        // DAY END
        // ========================================================

        if (_isEvaluatingDayEnd)
            return;


        // ========================================================
        // DANG CHO DU NGUOI VE NHA + DONG CUA
        // ========================================================
        //
        // Poll MOI TICK o day (thay vi chi check khi co nguoi
        // vao/ra) de bat duoc ca truong hop: da du nguoi trong nha
        // tu truoc, nhung cua van dang mo - chi khi nguoi choi DONG
        // CUA thi moi thuc su qua ngay moi.
        // ========================================================

        if (IsWaitingForPlayersHome)
        {
            CheckReadyToEndDay();

            return;
        }


        if (!_dayTimer.IsRunning)
            return;


        if (_dayTimer.Expired(Runner))
        {
            BeginWaitingForPlayersHome();
        }
    }


    // ============================================================
    // BEGIN WAITING FOR PLAYERS HOME
    // ============================================================
    //
    // Goi khi het gio trong ngay (7h toi). KHONG tinh ket qua ngay ngay -
    // cho tat ca player dang active buoc vao house trigger zone VA
    // dong cua lai (HouseZoneTrigger goi ReportPlayerEnteredHouse() moi
    // khi co 1 nguoi vao; CheckReadyToEndDay() poll moi tick de biet
    // khi nao du dieu kien goi EvaluateDayEnd()).
    // ============================================================

    private void BeginWaitingForPlayersHome()
    {
        _playersInsideHouse.Clear();

        PlayersInsideHouseCount = 0;

        IsWaitingForPlayersHome = true;

        IsWaitingForDoorClose = false;

        Debug.Log(
            "[GameDayManager] Het gio trong ngay - cho tat ca player " +
            "ve nha va dong cua truoc khi tinh ket qua."
        );
    }


    // ============================================================
    // REPORT PLAYER ENTERED / EXITED HOUSE
    // ============================================================
    //
    // Goi boi HouseZoneTrigger (Host-only, ca tu OnTriggerEnter/Exit
    // TUC THI lan tu co che tu kiem tra dinh ky de sua sai - xem
    // HouseZoneTrigger.cs).
    // ============================================================

    public void ReportPlayerEnteredHouse(PlayerRef player)
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (!IsWaitingForPlayersHome)
            return;

        if (!_playersInsideHouse.Add(player))
            return;

        PlayersInsideHouseCount = _playersInsideHouse.Count;

        Debug.Log(
            $"[GameDayManager] Player {player} da ve nha. " +
            $"({PlayersInsideHouseCount}/{Runner.ActivePlayers.Count()})"
        );

        CheckReadyToEndDay();
    }


    public void ReportPlayerExitedHouse(PlayerRef player)
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (!IsWaitingForPlayersHome)
            return;

        if (!_playersInsideHouse.Remove(player))
            return;

        PlayersInsideHouseCount = _playersInsideHouse.Count;

        Debug.Log(
            $"[GameDayManager] Player {player} roi khoi nha giua chung. " +
            $"({PlayersInsideHouseCount}/{Runner.ActivePlayers.Count()})"
        );

        CheckReadyToEndDay();
    }


    // ============================================================
    // CHECK READY TO END DAY (du nguoi + cua da dong)
    // ============================================================
    //
    // Thay the CheckAllPlayersHome() cu. Ngoai viec dem so nguoi,
    // con bat buoc _houseDoor.IsDoorOpen == false (neu co gan cua)
    // thi moi thuc su chuyen ngay.
    // ============================================================

    private void CheckReadyToEndDay()
    {
        int expected = Runner.ActivePlayers.Count();

        if (expected <= 0)
            return;

        bool allInside =
            _playersInsideHouse.Count >= expected;

        bool doorOpen =
            _houseDoor != null &&
            _houseDoor.IsDoorOpen;


        // ----------------------------------------------------------
        // Cap nhat co cho UI: du nguoi roi nhung cua con mo.
        // ----------------------------------------------------------

        bool waitingForDoor =
            allInside && doorOpen;

        if (IsWaitingForDoorClose != waitingForDoor)
        {
            IsWaitingForDoorClose = waitingForDoor;
        }


        if (!allInside)
            return;

        if (doorOpen)
            return;


        // ----------------------------------------------------------
        // DU NGUOI + CUA DA DONG - tinh ket qua ngay nhu flow cu.
        // ----------------------------------------------------------

        IsWaitingForPlayersHome = false;

        IsWaitingForDoorClose = false;

        EvaluateDayEnd();
    }


    // ============================================================
    // DAY STATS ACK (Stats Panel - chi hien khi THANH CONG va
    // _showStatsPanelOnSuccess = true)
    // ============================================================

    private void BeginWaitingForStatsAck()
    {
        _playersAckedStats.Clear();

        PlayersAckedStatsCount = 0;

        IsWaitingForStatsAck = true;

        _statsAckFallbackTimer =
            _useStatsAckFallback
                ? TickTimer.CreateFromSeconds(
                    Runner,
                    _statsAckFallbackSeconds
                )
                : TickTimer.None;
    }


    /// <summary>
    /// Goi tu GameplayPlayer (qua RPC_ReportStatsAcknowledged, chi chay
    /// tren Host) moi khi 1 player bam "E" o Stats Panel.
    /// </summary>
    public void ReportPlayerAckedStats(PlayerRef player)
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (!IsWaitingForStatsAck)
            return;

        if (!_playersAckedStats.Add(player))
            return;

        PlayersAckedStatsCount = _playersAckedStats.Count;

        Debug.Log(
            $"[GameDayManager] Player {player} da xac nhan Stats Panel. " +
            $"({PlayersAckedStatsCount}/{Runner.ActivePlayers.Count()})"
        );

        CheckReadyToProceedAfterStats();
    }


    private void CheckReadyToProceedAfterStats()
    {
        int expected = Runner.ActivePlayers.Count();

        if (expected <= 0)
            return;

        if (_playersAckedStats.Count < expected)
            return;

        ProceedAfterStatsAck();
    }


    /// <summary>
    /// Duoc goi khi TAT CA player da bam "E" (CheckReadyToProceedAfterStats)
    /// hoac khi fallback timer het gio (FixedUpdateNetwork). Tu day tro di
    /// flow tiep tuc GIONG HET nhu truoc day: show Day Panel roi commit
    /// ngay moi.
    /// </summary>
    private void ProceedAfterStatsAck()
    {
        IsWaitingForStatsAck = false;

        _statsAckFallbackTimer = TickTimer.None;

        RPC_ShowDayResultPanel(
            true,
            _pendingDay,
            _pendingMoneyTarget
        );
    }


    // ============================================================
    // START NEW RUN
    // ============================================================

    private void StartNewRun()
    {
        int playerCount =
            Mathf.Max(
                1,
                RoomSessionData.PlayerCountAtStart
            );


        _scaledStartingTarget =
            _startingMoneyTargetPerPlayer *
            playerCount;


        CurrentDay = 1;

        MoneyTarget =
            _scaledStartingTarget;


        _pendingDay = 0;

        _pendingMoneyTarget = 0;

        _hasPendingDay = false;


        // Reset pending result.

        _pendingDayResult = false;

        _pendingDayResultSuccess = false;

        _pendingDayResultDay = 0;

        _pendingDayResultTarget = 0;


        // FIX 3: reset co pending message run-start cho lan choi moi.

        _pendingRunStartMessage = false;


        _playersJoinedCount = 0;
        _playersReportedJoined.Clear();

        _hasAnnouncedRunStart = false;

        _pendingRunStartAnnounce = false;

        _dayTimerStarted = false;


        _playersInsideHouse.Clear();

        PlayersInsideHouseCount = 0;

        IsWaitingForPlayersHome = false;

        IsWaitingForDoorClose = false;


        _runStartFallbackTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                _runStartFallbackSeconds
            );


        _playersAckedStats.Clear();

        PlayersAckedStatsCount = 0;

        IsWaitingForStatsAck = false;

        _statsAckFallbackTimer = TickTimer.None;

        DayStatsRecorder.Instance?.ResetForNewDay();
    }


    // ============================================================
    // START DAY TIMER
    // ============================================================

    private void StartDayTimer()
    {
        _dayTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                _realSecondsPerGameDay
            );
    }


    // ============================================================
    // PLAYER JOINED
    // ============================================================

    public void ReportPlayerJoinedGame(PlayerRef player)
    {
        if (!Object.HasStateAuthority ||
            _hasAnnouncedRunStart ||
            _pendingRunStartAnnounce)
        {
            return;
        }

        // Watchdog co the goi lai SpawnPlayer cho cung PlayerRef neu lan
        // truoc spawn that bai/bi retry. Khong duoc tang count 2 lan.
        if (!_playersReportedJoined.Add(player))
            return;

        _playersJoinedCount = _playersReportedJoined.Count;

        // ========================================================
        // FIX 1: gia han lai fallback timer moi khi CO THEM 1 player
        // bao joined. Truoc day timer nay chi set 1 LAN duy nhat luc
        // StartNewRun() voi thoi han co dinh - neu 1 client load lau
        // hon moc do (VD lag), Host se coi nhu "khong cho duoc nua" va
        // tu ban RPC_NotifyRunStart som, bo qua luon client con dang
        // load. Gio moi lan co tien trien THAT (co them nguoi join),
        // fallback duoc gia han lai - no chi con la "luoi an toan cuoi
        // cung" cho truong hop THAT SU khong con ai join them nua (VD 1
        // nguoi bi crash vinh vien), khong con bi kich hoat oan vi 1
        // nguoi load hoi lau.
        // ========================================================

        _runStartFallbackTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                _runStartFallbackSeconds
            );

        // ========================================================
        // FIX 2: dung Runner.ActivePlayers.Count() (dem TRUC TIEP tren
        // NetworkRunner, phan anh dung so nguoi da THUC SU ket noi vao
        // session ngay luc doc) thay vi RoomSessionData.PlayerCountAtStart.
        //
        // PlayerCountAtStart la 1 con so SNAPSHOT lay tu
        // LobbyPlayer.AllPlayers (list tu track local) ngay luc Host
        // bam Start Game. AllPlayers chi duoc Add() ben trong
        // LobbyPlayer.Spawned() - ma Spawned() cua object thuoc VE MOT
        // CLIENT KHAC chi thuc su chay tren may Host SAU KHI Fusion
        // dong bo xong object do qua mang (mat vai tick, tuy do tre
        // mang). Neu Host bam Start Game ngay khi vua thay client kia
        // "vao phong" (VD qua room list UI) nhung TRUOC khi LobbyPlayer
        // cua client do kip Spawned() xong tren may Host, AllPlayers.Count
        // se bi THIEU (VD doc ra 1 thay vi 2).
        //
        // Runner.ActivePlayers la API GOC cua Fusion, dem truc tiep
        // tren chinh NetworkRunner (khong phai list tu track qua
        // Spawned()), nen luon dung voi so nguoi da ket noi vao session
        // tai thoi diem doc, khong bi lech pha nhu tren.
        // ========================================================

        int expected =
            Mathf.Max(
                1,
                Runner.ActivePlayers.Count()
            );


        if (_playersJoinedCount >= expected)
        {
            ScheduleRunStartAnnounce();
        }
    }


    // ============================================================
    // SCHEDULE RUN START
    // ============================================================

    private void ScheduleRunStartAnnounce()
    {
        _pendingRunStartAnnounce = true;


        _runStartDelayTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                _runStartAnnounceDelaySeconds
            );
    }


    // ============================================================
    // EVALUATE DAY END
    // ============================================================

    private void EvaluateDayEnd()
    {
        _isEvaluatingDayEnd = true;


        int currentMoney =
            TeamWallet.Instance != null
                ? TeamWallet.Instance.Money
                : 0;


        bool success =
            currentMoney >= MoneyTarget;


        // ========================================================
        // SUCCESS
        // ========================================================

        if (success)
        {
            int newTarget =
                CalculateNextTarget(
                    currentMoney,
                    MoneyTarget,
                    CurrentDay + 1
                );


            // ----------------------------------------------------
            // KHONG doi CurrentDay o day.
            //
            // Chi luu pending.
            // ----------------------------------------------------

            _pendingDay =
                CurrentDay + 1;


            _pendingMoneyTarget =
                newTarget;


            _hasPendingDay = true;


            // ----------------------------------------------------
            // LUU MESSAGE.
            //
            // KHONG GOI RPC O DAY.
            // ----------------------------------------------------

            _pendingDayResult = true;

            _pendingDayResultSuccess = true;

            _pendingDayResultDay =
                _pendingDay;

            _pendingDayResultTarget =
                _pendingMoneyTarget;


            // ----------------------------------------------------
            // THANH CONG: mac dinh qua Day Panel LUON, KHONG hien
            // Stats Panel. Bat _showStatsPanelOnSuccess de hien lai
            // Stats Panel truoc (flow cu): Day Panel
            // (RPC_ShowDayResultPanel) chi duoc goi sau khi TAT CA
            // player da bam "E" (hoac fallback het gio) - xem
            // ProceedAfterStatsAck().
            // ----------------------------------------------------

            if (_showStatsPanelOnSuccess)
            {
                DayStatsData statsData =
                    DayStatsRecorder.Instance?.BuildSummary(
                        CurrentDay,
                        MoneyTarget,
                        currentMoney
                    );

                string statsPayload =
                    statsData != null
                        ? statsData.Serialize()
                        : string.Empty;

                BeginWaitingForStatsAck();

                RPC_ShowDayStatsPanel(
                    CurrentDay,
                    statsPayload
                );
            }
            else
            {
                RPC_ShowDayResultPanel(
                    true,
                    _pendingDay,
                    _pendingMoneyTarget
                );
            }
        }


        // ========================================================
        // FAILURE
        // ========================================================

        else
        {
            // ----------------------------------------------------
            // Reset tien ngay tren Host.
            // ----------------------------------------------------

            TeamWallet.Instance?
                .ResetToStartingMoney();


            // ----------------------------------------------------
            // Khong doi CurrentDay ngay.
            // ----------------------------------------------------

            _pendingDay = 1;

            _pendingMoneyTarget =
                _scaledStartingTarget;


            _hasPendingDay = true;


            // ----------------------------------------------------
            // LUU MESSAGE.
            // ----------------------------------------------------

            _pendingDayResult = true;

            _pendingDayResultSuccess = false;

            _pendingDayResultDay = 1;

            _pendingDayResultTarget =
                _pendingMoneyTarget;


            // ----------------------------------------------------
            // CHI SHOW PANEL.
            // ----------------------------------------------------

            RPC_ShowDayResultPanel(
                false,
                _pendingDay,
                _pendingMoneyTarget
            );
        }


        // ========================================================
        // STOP OLD TIMER
        // ========================================================

        _dayTimerStarted = false;

        _dayTimer = TickTimer.None;


        _isEvaluatingDayEnd = false;
    }


    // ============================================================
    // CALCULATE NEXT TARGET
    // ============================================================
    //
    // NGUYEN TAC:
    //
    // 1) Target ngay moi LUON LUON > so tien team DANG CO (currentMoney).
    //    -> Khong co chuyen "qua ngay moi mien phi" vi lo kiem qua nhieu.
    //    -> Nhan multiplier TRUC TIEP len currentMoney (chu khong phai len
    //       target cu), vi currentMoney moi la so tien that dang giu.
    //
    // 2) Multiplier phu thuoc PERFORMANCE cua ngay vua roi (currentMoney so
    //    voi target ngay do): kiem cang vuot xa target -> multiplier cang
    //    tien gan _maxDifficultyMultiplier (buoc nhay tuong doi lon hon vi
    //    team da chung to kiem tien gioi). Chi vua du target -> multiplier
    //    gan _minDifficultyMultiplier (buoc nhay nhe nhang hon).
    //
    // 3) Multiplier CON duoc cong them 1 khoan baseline tang dan theo so
    //    ngay (newDayNumber), khong phu thuoc performance - de cang choi
    //    lau, game cang kho len, ke ca khi team chi kiem vua du moi ngay.
    //
    // ============================================================

    private int CalculateNextTarget(
        int currentMoney,
        int previousTarget,
        int newDayNumber)
    {
        // ------------------------------------------------------------
        // 1) PERFORMANCE MULTIPLIER
        // ------------------------------------------------------------

        float performanceRatio =
            previousTarget > 0
                ? (float)currentMoney / previousTarget
                : 1f;

        float performance01 =
            Mathf.InverseLerp(
                1f,
                Mathf.Max(1.01f, _performanceRatioForMaxDifficulty),
                performanceRatio
            );

        float performanceMultiplier =
            Mathf.Lerp(
                _minDifficultyMultiplier,
                _maxDifficultyMultiplier,
                performance01
            );


        // ------------------------------------------------------------
        // 2) DAY BASELINE GROWTH
        // ------------------------------------------------------------
        //
        // Day 2 (newDayNumber=2): +0.
        // Day 3: +1 buoc _dayDifficultyGrowthPerDay.
        // Day 4: +2 buoc. ... clamp tai _maxDayDifficultyBonus.
        //

        int daysPastFirstIncrease =
            Mathf.Max(0, newDayNumber - 2);

        float dayGrowthBonus =
            Mathf.Min(
                daysPastFirstIncrease * _dayDifficultyGrowthPerDay,
                _maxDayDifficultyBonus
            );


        // ------------------------------------------------------------
        // 3) FINAL MULTIPLIER -> TARGET DUA TREN TIEN DANG CO
        // ------------------------------------------------------------

        float finalMultiplier =
            performanceMultiplier + dayGrowthBonus;

        int newTarget =
            Mathf.CeilToInt(
                currentMoney * finalMultiplier
            );


        // ------------------------------------------------------------
        // GUARANTEE: target luon > tien dang co VA > target cu.
        // ------------------------------------------------------------

        newTarget =
            Mathf.Max(
                newTarget,
                currentMoney + 1,
                previousTarget + 1
            );

        return newTarget;
    }


    // ============================================================
    // RPC - RUN START
    // ============================================================
    //
    // FIX 3: RPC nay gio CHI con nhiem vu bac su kien OnRunStarted (khien
    // GameplayLoadingOverlay tu an -> GameDayIntroPanel tu hien len panel
    // "DAY 1"). Message chat "A new adventure begins..." KHONG con duoc
    // gui o day nua - no da duoc chuyen sang RPC_BroadcastRunStartMessage(),
    // chi goi SAU KHI Day 1 Intro Panel da fade xong (xem
    // HandleDayIntroFinished() / DayIntroFallbackRoutine()).
    // ============================================================

    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All
    )]
    private void RPC_NotifyRunStart()
    {
        OnRunStarted?.Invoke();
    }


    // ============================================================
    // RPC - BROADCAST RUN START MESSAGE
    // ============================================================
    //
    // FIX 3: tach rieng khoi RPC_NotifyRunStart. Chi duoc goi 1 LAN,
    // SAU KHI Day 1 Intro Panel da hien xong VA bien mat hoan toan -
    // xem HandleDayIntroFinished() (duong chay chinh) hoac
    // DayIntroFallbackRoutine() (luoi an toan neu event cua panel
    // khong bac ra vi ly do nao do).
    // ============================================================

    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All
    )]
    private void RPC_BroadcastRunStartMessage()
    {
        string message =
            "A new adventure begins! " +
            "Grab your rod, the rent's waiting to be paid!";


        LobbyChatUI.Instance?
            .AddDayEventMessage(
                message
            );
    }


    // ============================================================
    // RPC - SHOW DAY STATS PANEL (chi goi khi THANH CONG va
    // _showStatsPanelOnSuccess = true)
    // ============================================================

    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All
    )]
    private void RPC_ShowDayStatsPanel(
        int finishedDay,
        string statsPayload)
    {
        DayStatsPanel.Instance?.Show(
            finishedDay,
            statsPayload
        );
    }


    // ============================================================
    // RPC - SHOW DAY RESULT PANEL
    // ============================================================
    //
    // RPC nay KHONG con gui message chat.
    //
    // No chi co nhiem vu:
    //     -> mo DAY panel
    //
    // Message se duoc gui sau khi panel hoan tat.
    //
    // FIX (bug: Host hien "DAY 2" nhung Client hien "DAY 1"):
    //
    // Truoc day goi GameDayIntroPanel.Instance?.Show(true), panel se tu
    // doc GameDayManager.Instance.PendingDisplayDay de lay so ngay hien
    // thi. Nhung PendingDisplayDay dua vao field _pendingDay/_hasPendingDay
    // CHI duoc set dung tren Host (EvaluateDayEnd() chi chay khi co
    // StateAuthority) - Client khong bao gio thay _hasPendingDay = true,
    // nen no fallback ve CurrentDay (van con la ngay cu luc do) -> Client
    // hien sai so ngay so voi Host.
    //
    // Tham so newDay cua RPC nay DA duoc Fusion dong bo dung toi moi
    // client, nen chi can truyen thang no vao Show(bool, int) la du,
    // khong can panel tu doc field local nua.
    //
    // ============================================================

    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All
    )]
    private void RPC_ShowDayResultPanel(
        bool success,
        int newDay,
        int newTarget)
    {
        if (!success)
        {
            OnDayFailureReset?.Invoke();
        }


        GameDayIntroPanel.Instance?
            .Show(true, newDay);
    }


    // ============================================================
    // RPC - BROADCAST DAY RESULT
    // ============================================================
    //
    // RPC nay duoc goi SAU KHI:
    //
    // GameDayIntroPanel
    //      |
    //      v
    // fade out xong
    //      |
    //      v
    // OnDayIntroFinished
    //      |
    //      v
    // HandleDayIntroFinished()
    //
    // ============================================================

    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All
    )]
    private void RPC_BroadcastDayResult(
        bool success,
        int newDay,
        int newTarget)
    {
        string message =
            success
                ? $"Day {newDay} begins. New target: ${newTarget}"
                : $"Back to Day 1. Target: ${newTarget}";


        LobbyChatUI.Instance?
            .AddDayEventMessage(
                message
            );
    }


    // ============================================================
    // GAME CLOCK
    // ============================================================

    /// <summary>
    /// 0 = dau ngay.
    /// 1 = het ngay.
    ///
    /// Khi panel den:
    /// timer da duoc reset ve mot ngay moi,
    /// nen gia tri se bat dau tu 0.
    /// </summary>
    public float GetElapsedFraction01()
    {
        float? remaining =
            _dayTimer.RemainingTime(
                Runner
            );


        if (!remaining.HasValue ||
            _realSecondsPerGameDay <= 0f)
        {
            return 0f;
        }


        float elapsed =
            _realSecondsPerGameDay -
            remaining.Value;


        return Mathf.Clamp01(
            elapsed /
            _realSecondsPerGameDay
        );
    }


    /// <summary>
    /// Gio hien tai trong ngay, dang float 24h CHUA lam tron (VD
    /// 18.5 = 6 gio 30 toi). Dung de so sanh chinh xac voi
    /// _urgentStartHour, va de GetGameClockText() lam tron hien thi.
    /// </summary>
    public float GetCurrentHour24()
    {
        float fraction =
            GetElapsedFraction01();

        float totalMinutes =
            Mathf.Lerp(
                _dayStartHour * 60f,
                _dayEndHour * 60f,
                fraction
            );

        return totalMinutes / 60f;
    }


    // ============================================================
    // GAME CLOCK TEXT
    // ============================================================

    public string GetGameClockText()
    {
        float totalMinutes =
            GetCurrentHour24() * 60f;


        int roundedMinutes =
            Mathf.FloorToInt(
                totalMinutes / 30f
            ) * 30;


        int hour24 =
            (roundedMinutes / 60) % 24;


        int minute =
            roundedMinutes % 60;


        string period =
            hour24 >= 12
                ? "PM"
                : "AM";


        int hour12 =
            hour24 % 12;


        if (hour12 == 0)
        {
            hour12 = 12;
        }


        return $"{hour12:00}:{minute:00} {period}";
    }
}