using System.Collections.Generic;
using Firebase.Firestore;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel Leaderboard o scene Museum: bam L de mo (hieu ung Show cua
/// PanelEffect, giong ExitMuseumUI), bam nut Back hoac ESC de dong
/// (hieu ung Hide). L KHONG dong panel.
///
/// - Tab MUSEUM (mac dinh moi lan mo): bang xep hang tong Fame Point
///   cua Museum tat ca nguoi choi.
/// - Tab DAY: bang xep hang ky luc so NGAY cao nhat.
///
/// CA 2 TAB DEU CAP NHAT THEO THOI GIAN THUC (Firestore Listen). CHI
/// tab DANG HIEN THI moi lang nghe (bam sang tab kia se dung lang
/// nghe tab cu, bat lang nghe tab moi). Ca 2 listener deu bi dung khi
/// dong panel.
///
/// LUU Y: vi danh sach duoc XAY LAI moi lan co cap nhat, dong dang
/// XO XUONG (xem card ca) o tab MUSEUM se TU THU GON lai moi lan co
/// thay doi tu server.
///
/// 2 nut tab doi sprite theo tab dang chon.
///
/// Gan script nay len 1 object trong Canvas Museum. Nut Back tren
/// panel: OnClick -> MuseumLeaderboardUI.ClosePanel(). 2 nut tab:
/// OnClick -> ShowMuseumTab() / ShowDayTab().
/// </summary>
public class MuseumLeaderboardUI : MonoBehaviour
{
    /// <summary>
    /// True khi panel Leaderboard dang mo (cho toi khi animation Hide
    /// xong). Script khac dung de bo qua Esc.
    /// </summary>
    public static bool IsOpen { get; private set; }

    /// <summary>Frame ma Esc vua duoc panel nay dung de DONG chinh no.</summary>
    public static int EscClosedFrame { get; private set; } = -1;

    [Header("Panel")]
    [SerializeField] private PanelEffect _leaderboardPanel;

    [Header("Player (khoa di chuyen/nhin luc panel mo)")]
    [SerializeField] private PlayerMovement _playerMovement;
    [SerializeField] private PlayerLook _playerLook;

    [Header("Tabs")]
    [Tooltip("Noi dung tab MUSEUM (chua danh sach xep hang).")]
    [SerializeField] private GameObject _museumTabContent;

    [Tooltip("Noi dung tab DAY (chua danh sach ky luc so ngay).")]
    [SerializeField] private GameObject _dayTabContent;

    [Header("Tab Buttons (doi sprite bat/tat)")]
    [Tooltip("Image cua nut tab MUSEUM (B-TabMuseum).")]
    [SerializeField] private Image _museumTabImage;

    [Tooltip("Image cua nut tab DAY (B-TabDay).")]
    [SerializeField] private Image _dayTabImage;

    [Tooltip("Sprite khi tab DANG MO (khung mau chuoi - Group 228).")]
    [SerializeField] private Sprite _tabActiveSprite;

    [Tooltip("Sprite khi tab DANG TAT (khung den - Group 71).")]
    [SerializeField] private Sprite _tabInactiveSprite;

    [Header("Museum Tab List")]
    [Tooltip(
        "Container chua cac dong xep hang (Content cua ScrollRect). " +
        "Dat Vertical Layout Group tren chinh object nay."
    )]
    [SerializeField] private Transform _entryContainer;

    [SerializeField] private MuseumLeaderboardEntryUI _entryPrefab;

    [Tooltip("Catalog tra ItemData/FishData tu ItemId cua ca dang trung bay.")]
    [SerializeField] private ItemCatalog _itemCatalog;

    [Tooltip("So Museum toi da hien thi (top N).")]
    [SerializeField] private int _leaderboardLimit = 50;

    [Header("Day Tab List")]
    [Tooltip(
        "Container chua cac dong ky luc so ngay (Content cua ScrollRect " +
        "trong DayTabContent). Dat Vertical Layout Group tren chinh " +
        "object nay."
    )]
    [SerializeField] private Transform _dayEntryContainer;

    [SerializeField] private DayLeaderboardEntryUI _dayEntryPrefab;

    [Tooltip("So nguoi choi toi da hien thi o tab DAY (top N).")]
    [SerializeField] private int _dayLeaderboardLimit = 50;

    private readonly PlayerDatabase _playerDatabase = new();
    private readonly List<MuseumLeaderboardEntryUI> _spawnedEntries = new();
    private readonly List<DayLeaderboardEntryUI> _spawnedDayEntries = new();

    private bool _isPanelOpen;
    private bool _isClosing;

    // Trang thai TRUOC khi mo panel - de khoi phuc dung khi dong
    // (VD dang o AquariumView thi van giu frozen + IsUIOpen = true).
    private bool _prevMovementEnabled;
    private bool _prevLookEnabled;
    private bool _prevUIOpen;

    // Listener realtime cua tab dang hien thi - null neu khong dang
    // lang nghe tab do.
    private ListenerRegistration _museumListenerRegistration;
    private ListenerRegistration _dayListenerRegistration;

    private void Start()
    {
        // Dat san hinh 2 nut tab dung trang thai mac dinh (MUSEUM dang
        // mo) ngay tu dau, ke ca luc panel chua mo lan nao.
        UpdateTabVisuals(museumActive: true);
    }

    private void Update()
    {
        // Esc: dong panel neu dang mo.
        // (Nhuong Esc cho panel Exit neu no dang mo chong len tren.)
        if (_isPanelOpen && !_isClosing && !ExitMuseumUI.IsOpen &&
            Input.GetKeyDown(KeyCode.Escape))
        {
            EscClosedFrame = Time.frameCount;
            ClosePanel();
            return;
        }

        if (LobbyChatUI.IsTyping)
            return;

        if (!Input.GetKeyDown(KeyCode.L))
            return;

        // L chi de MO panel; dong bang Esc hoac nut Back.
        if (!_isPanelOpen && CanOpen())
            OpenPanel();
    }

    private void OnDestroy()
    {
        IsOpen = false;

        // Phong khi object nay bi huy trong luc panel dang mo (VD
        // chuyen scene) - dam bao khong ro ri listener Firestore.
        StopMuseumListening();
        StopDayListening();
    }

    // ============================================================
    // OPEN / CLOSE
    // ============================================================

    /// <summary>
    /// Mo duoc khi khong co UI toan man hinh nao khac dang mo, TRU
    /// truong hop dang o AquariumView (luc do IsUIOpen = true la do
    /// chinh view bay ra, van cho mo Leaderboard). Panel Exit dang mo
    /// thi khong mo chong.
    /// </summary>
    private bool CanOpen()
    {
        if (ExitMuseumUI.IsOpen)
            return false;

        return !PlayerCursor.IsUIOpen || AquariumView.AnyActive;
    }

    public void OpenPanel()
    {
        if (_isPanelOpen)
            return;

        _isPanelOpen = true;
        _isClosing = false;
        IsOpen = true;

        _prevMovementEnabled = _playerMovement != null && _playerMovement.enabled;
        _prevLookEnabled = _playerLook != null && _playerLook.enabled;
        _prevUIOpen = PlayerCursor.IsUIOpen;

        // Xoa sach dong cu TRUOC khi hien panel.
        ClearEntries();
        ClearDayEntries();

        // Luon bat dau o tab MUSEUM moi lan mo (tu bat lang nghe
        // realtime cho tab do).
        ShowMuseumTab();

        _leaderboardPanel.Show();

        if (_playerMovement != null) _playerMovement.enabled = false;
        if (_playerLook != null) _playerLook.enabled = false;

        PlayerCursor.IsUIOpen = true;
        PlayerCursor.UnlockCursor();
    }

    /// <summary>Gan vao OnClick cua nut Back.</summary>
    public void ClosePanel()
    {
        if (!_isPanelOpen || _isClosing)
            return;

        _isClosing = true;

        // Dung ca 2 listener - khong con can cap nhat gi khi panel dong.
        StopMuseumListening();
        StopDayListening();

        _leaderboardPanel.Hide(() =>
        {
            _isPanelOpen = false;
            _isClosing = false;
            IsOpen = false;

            // Panel da an xong -> xoa het dong, lan mo sau build lai tu dau.
            ClearEntries();
            ClearDayEntries();

            if (_playerMovement != null) _playerMovement.enabled = _prevMovementEnabled;
            if (_playerLook != null) _playerLook.enabled = _prevLookEnabled;

            PlayerCursor.IsUIOpen = _prevUIOpen;

            if (_prevUIOpen)
                PlayerCursor.UnlockCursor();
            else
                PlayerCursor.LockCursor();
        });
    }

    // ============================================================
    // TABS
    // ============================================================

    /// <summary>Gan vao OnClick cua nut tab MUSEUM.</summary>
    public void ShowMuseumTab()
    {
        if (_museumTabContent != null) _museumTabContent.SetActive(true);
        if (_dayTabContent != null) _dayTabContent.SetActive(false);

        UpdateTabVisuals(museumActive: true);

        StopDayListening();
        StartMuseumListening();
    }

    /// <summary>Gan vao OnClick cua nut tab DAY.</summary>
    public void ShowDayTab()
    {
        if (_museumTabContent != null) _museumTabContent.SetActive(false);
        if (_dayTabContent != null) _dayTabContent.SetActive(true);

        UpdateTabVisuals(museumActive: false);

        StopMuseumListening();
        StartDayListening();
    }

    private void UpdateTabVisuals(bool museumActive)
    {
        SetTabSprite(_museumTabImage, museumActive);
        SetTabSprite(_dayTabImage, !museumActive);
    }

    private void SetTabSprite(Image image, bool active)
    {
        if (image == null)
            return;

        Sprite sprite = active ? _tabActiveSprite : _tabInactiveSprite;

        if (sprite != null)
            image.sprite = sprite;
    }

    // ============================================================
    // MUSEUM TAB DATA (realtime)
    // ============================================================

    private void StartMuseumListening()
    {
        StopMuseumListening();

        if (_entryContainer == null || _entryPrefab == null)
        {
            Debug.LogWarning(
                "[MuseumLeaderboardUI] Thieu _entryContainer hoac " +
                "_entryPrefab trong Inspector."
            );
            return;
        }

        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsReady)
        {
            Debug.LogWarning("[MuseumLeaderboardUI] Firebase chua san sang.");
            return;
        }

        _museumListenerRegistration =
            _playerDatabase.ListenMuseumLeaderboard(
                _leaderboardLimit,
                HandleMuseumLeaderboardUpdate
            );
    }

    private void StopMuseumListening()
    {
        if (_museumListenerRegistration == null)
            return;

        _museumListenerRegistration.Stop();
        _museumListenerRegistration = null;
    }

    /// <summary>
    /// Duoc Firestore tu goi moi khi ket qua query TOP Museum thay doi.
    /// </summary>
    private void HandleMuseumLeaderboardUpdate(List<LeaderboardEntryData> players)
    {
        // Panel da dong hoac object bi huy trong luc cho callback ve.
        if (this == null || !_isPanelOpen || _isClosing)
            return;

        ClearEntries();

        int rank = 1;

        foreach (LeaderboardEntryData player in players)
        {
            // Bo qua Museum chua co ca/fame - chi hien nguoi da co fame.
            if (player.TotalFamePoint <= 0)
                continue;

            MuseumLeaderboardEntryUI entry =
                Instantiate(_entryPrefab, _entryContainer);

            entry.SetData(
                rank,
                player.Nickname,
                player.TotalFamePoint,
                player.AquariumTanks,
                _itemCatalog
            );

            _spawnedEntries.Add(entry);
            rank++;
        }
    }

    private void ClearEntries()
    {
        for (int i = _spawnedEntries.Count - 1; i >= 0; i--)
        {
            if (_spawnedEntries[i] != null)
                Destroy(_spawnedEntries[i].gameObject);
        }

        _spawnedEntries.Clear();
    }

    // ============================================================
    // DAY TAB DATA (realtime)
    // ============================================================

    private void StartDayListening()
    {
        StopDayListening();

        if (_dayEntryContainer == null || _dayEntryPrefab == null)
        {
            Debug.LogWarning(
                "[MuseumLeaderboardUI] Thieu _dayEntryContainer hoac " +
                "_dayEntryPrefab trong Inspector."
            );
            return;
        }

        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsReady)
        {
            Debug.LogWarning("[MuseumLeaderboardUI] Firebase chua san sang.");
            return;
        }

        _dayListenerRegistration =
            _playerDatabase.ListenDayLeaderboard(
                _dayLeaderboardLimit,
                HandleDayLeaderboardUpdate
            );
    }

    private void StopDayListening()
    {
        if (_dayListenerRegistration == null)
            return;

        _dayListenerRegistration.Stop();
        _dayListenerRegistration = null;
    }

    private void HandleDayLeaderboardUpdate(List<DayLeaderboardEntryData> players)
    {
        if (this == null || !_isPanelOpen || _isClosing)
            return;

        ClearDayEntries();

        int rank = 1;

        foreach (DayLeaderboardEntryData player in players)
        {
            if (player.BestDay <= 0)
                continue;

            DayLeaderboardEntryUI entry =
                Instantiate(_dayEntryPrefab, _dayEntryContainer);

            entry.SetData(rank, player.Nickname, player.BestDay);

            _spawnedDayEntries.Add(entry);
            rank++;
        }
    }

    private void ClearDayEntries()
    {
        for (int i = _spawnedDayEntries.Count - 1; i >= 0; i--)
        {
            if (_spawnedDayEntries[i] != null)
                Destroy(_spawnedDayEntries[i].gameObject);
        }

        _spawnedDayEntries.Clear();
    }
}