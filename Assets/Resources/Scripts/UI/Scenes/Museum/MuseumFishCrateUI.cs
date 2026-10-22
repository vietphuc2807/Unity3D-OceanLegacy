using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// UI "Thung ca mang ve" trong scene Museum (icon goc duoi trai, xem
/// anh minh hoa - bam Tab de mo/dong).
///
/// Hanh vi:
///
/// - Mac dinh (dong): CHI hien icon thung ca (_crateIconUI active,
///   _fishPanelContainer va _closeHintUI tat).
/// - Bam Tab lan 1: TAT icon thung ca, HIEN cac panel thong tin tung
///   con ca dang mang ve - lay tu ReturningCatchData (duoc
///   CarGoHomeInteractable "chup" lai luc con o Gameplay, DA TU BO
///   can cau). Neu chi mang 1 con thi chi co 1 panel, mang 2 thi co 2
///   panel, v.v. Cac panel duoc Instantiate vao _fishPanelContainer -
///   DAT GridLayoutGroup (hoac HorizontalOrVerticalLayoutGroup) ngay
///   tren object _fishPanelContainer trong Inspector de chung TU
///   XEP DEU; script nay chi tao panel, khong tu tinh vi tri/khoang
///   cach. Cung luc do, _closeHintUI (thanh text "TAB - Close" o duoi
///   cung danh sach) se duoc BAT len de nhac nguoi choi cach dong.
/// - Bam Tab lan 2: dong panel va _closeHintUI lai, hien icon thung
///   ca tro lai.
/// - NEU KHONG MANG CON CA NAO VE (_spawnedPanels rong) VA MO BANG
///   TAB: bam Tab se KHONG mo panel/close-hint gi ca - thay vao do
///   icon thung ca se TAT DI trong _emptyCrateFeedbackDuration giay
///   (bao hieu "khong co gi de xem") roi TU HIEN LAI, xem
///   PlayEmptyCrateFeedback().
///
/// NEU KHONG MANG CON CA NAO VE VA MO BANG AQUARIUMVIEW (bam E vao
/// be): vi luc nay camera dang GIU NGUYEN o goc nhin be (khong the
/// "flash roi tu tat" nhu kieu Tab), script se hien 1 dong TEXT THAN
/// THIEN (_emptyCrateMessageUI) bao "khong co ca nao ca" thay vi mo
/// danh sach panel/close-hint rong, va GIU NGUYEN cho toi khi nguoi
/// choi bam Esc thoat AquariumView. Xem OpenForAquarium().
///
/// NHIEU BE (moi): Museum co the co NHIEU BE trung bay (VD 4 be),
/// moi be la 1 bo AquariumDisplayManager + AquariumView +
/// AquariumInteractableObject rieng, cung mang 1 "TankId" (0..3).
/// Panel nay LA UI DUNG CHUNG cho ca 4 be (chi co 1 "Thung ca mang
/// ve" duy nhat), nhung no PHAI BIET dang thao tac VOI BE NAO tai
/// moi thoi diem:
///
/// - _activeTankId: TankId cua be dang duoc xem (do AquariumView goi
///   OpenForAquarium(tankId) truyen vao luc nguoi choi bam E vao be
///   do). = -1 khi khong dang xem be nao.
/// - _selectedPanelByTank: moi be (TankId) nho RIENG panel ca nao dang
///   duoc trung bay trong no (thay vi chi 1 bien duy nhat nhu truoc,
///   vi gio co the co toi 4 con ca dang trung bay cung luc o 4 be
///   khac nhau). 1 con ca (1 panel) chi co the nam trong TOI DA 1 be
///   tai 1 thoi diem - bam chon no cho be khac se tu dong RUT no khoi
///   be cu truoc.
///
///   QUAN TRONG (FIX): dictionary nay CHI biet ve nhung luot chon xay
///   ra TRONG PHIEN CHOI HIEN TAI (qua HandleFishSelected). Con ca
///   duoc AquariumPersistenceLoader dat lai vao be luc scene Museum
///   MOI load (tu du lieu Firestore) KHONG di qua HandleFishSelected,
///   nen KHONG co entry trong dictionary nay - neu chi dua vao
///   dictionary de quyet dinh hien _retrieveHintUI / cho phep bam C
///   thi be do se bi "cau ngau" (co ca that nhung UI tuong nhu
///   khong). Vi vay moi cho lien quan (Update, OpenForAquarium,
///   RetrieveCurrentFish) gio HOI THANG AquariumDisplayManager (nguon
///   su that ve viec be co ca hay khong) thay vi chi hoi dictionary
///   nay. Dictionary van duoc giu de biet "co panel nao can tra ve
///   thung ca hay khong" (co the co hoac khong, tuy ca do co phai la
///   ca duoc chon trong phien nay hay la ca duoc khoi phuc tu
///   Firestore).
///
/// Khi mo theo kieu OpenForAquarium(tankId): bam vao 1 panel se dua
/// con ca do vao DUNG BE dang xem (_activeTankId) thong qua
/// AquariumDisplayManager.GetByTankId(_activeTankId), VA AN HAN panel
/// do di khoi thung ca, dong thoi hien _retrieveHintUI (goi y phim
/// "C - Retrieve Fish"). Bam C se lay con ca ra khoi DUNG BE dang xem,
/// panel do XUAT HIEN TRO LAI trong thung ca (neu co panel tuong
/// ung), va _retrieveHintUI tat di.
///
/// LUU DU LIEU (moi): moi lan mot con ca duoc DAT vao be thanh cong
/// (HandleFishSelected) hoac LAY RA khoi be (RetrieveCurrentFish),
/// script goi AquariumSaveService de luu/xoa entry tuong ung trong
/// PlayerData.AquariumTanks (Firestore) - nho vay lan sau dang nhap
/// lai, AquariumPersistenceLoader (chay luc scene Museum load) se tu
/// dat lai dung con ca vao dung be.
///
/// Dat script nay len 1 object trong Canvas cua scene Museum (CHI 1
/// object DUY NHAT du co bao nhieu be).
/// </summary>
public class MuseumFishCrateUI : MonoBehaviour
{
    public static MuseumFishCrateUI Instance { get; private set; }

    [Header("Crate Icon (mac dinh hien)")]
    [SerializeField] private GameObject _crateIconUI;

    [Header("Fish Panels")]
    [Tooltip(
        "Container chua cac panel ca. Dat GridLayoutGroup (hoac " +
        "HorizontalOrVerticalLayoutGroup) tren CHINH object nay de " +
        "cac panel tu xep deu."
    )]
    [SerializeField] private Transform _fishPanelContainer;

    [SerializeField] private FishReturnPanelUI _fishPanelPrefab;

    [Tooltip(
        "Catalog chua toan bo ItemData trong game - dung de TAO LAI 1 " +
        "panel cho con ca vua duoc bam C lay ra khoi be, TRONG TRUONG " +
        "HOP no khong co san panel nao trong thung ca (VD ca duoc " +
        "AquariumPersistenceLoader dat vao be tu Firestore luc load " +
        "scene, khong di qua thung ca cua phien nay nen khong co panel " +
        "tuong ung). Neu de trong, ca loai nay se bi XOA KHOI BE nhung " +
        "KHONG hien lai duoc trong thung ca (mat panel, du du lieu be " +
        "van duoc xoa dung)."
    )]
    [SerializeField] private ItemCatalog _itemCatalog;

    [Header("Close Hint (thanh 'TAB - Close' o duoi cung)")]
    [Tooltip(
        "GameObject chua text/thanh 'TAB - Close', duoc BAT cung luc " +
        "voi _fishPanelContainer khi mo Thung ca, va TAT khi dong. " +
        "Dat rieng ngoai _fishPanelContainer de khong bi anh huong " +
        "boi GridLayoutGroup/xep deu cua cac panel ca."
    )]
    [SerializeField] private GameObject _closeHintUI;

    [Header("Retrieve Fish Hint (goc phai man hinh)")]
    [Tooltip(
        "Panel goi y phim C (icon phim C + text 'Retrieve Fish'), " +
        "BAT khi be DANG XEM (_activeTankId) co san 1 con ca trung " +
        "bay, TAT khi be do dang trong hoac roi khoi AquariumView."
    )]
    [SerializeField] private GameObject _retrieveHintUI;

    [Header("Empty Crate Feedback (bam Tab)")]
    [Tooltip(
        "Neu khong mang con ca nao ve, bam Tab se AN icon thung ca " +
        "trong khoang thoi gian nay (khong hien panel/close-hint gi) " +
        "roi tu HIEN LAI icon, thay vi mo 1 thung ca rong."
    )]
    [SerializeField] private float _emptyCrateFeedbackDuration = 1.5f;

    [Header("Empty Crate Message (mo qua AquariumView - bam E vao be)")]
    [Tooltip(
        "GameObject chua dong text than thien bao 'khong co ca nao' - " +
        "hien khi nguoi choi bam E vao be luc CHUA mang con ca nao ve, " +
        "va GIU NGUYEN cho toi khi thoat AquariumView (khac voi kieu " +
        "flash-roi-tu-tat cua duong Tab, vi luc nay camera dang dung " +
        "yen o goc nhin be, khong the tu dong dong lai)."
    )]
    [SerializeField] private GameObject _emptyCrateMessageUI;

    [Tooltip(
        "Text hien thi ben trong _emptyCrateMessageUI (co the de trong " +
        "neu message da co san text tinh, khong can doi noi dung)."
    )]
    [SerializeField] private TMP_Text _emptyCrateMessageText;

    [Tooltip(
        "Noi dung cau thong bao khi thung ca trong luc mo qua " +
        "AquariumView. Doi thoai mai trong Inspector, khong can sua code."
    )]
    [SerializeField]
    private string _emptyCrateMessage =
        "Looks like your fish crate is empty. Go catch something worth bringing home!";

    [Header("Options")]
    [Tooltip(
        "Neu bat, du lieu trong ReturningCatchData se bi xoa sau khi " +
        "da doc/hien thi xong (luc Start) - tranh hien lai ca cua " +
        "luot truoc neu Museum load lai trong cung 1 phien app ma " +
        "chua kip capture moi."
    )]
    [SerializeField] private bool _clearDataAfterDisplay = true;

    private readonly List<FishReturnPanelUI> _spawnedPanels = new();

    private bool _isCrateOpen;

    /// <summary>
    /// True khi panel nay dang duoc 1 AquariumView giu mo (khong phai
    /// nguoi choi tu bam Tab) - luc nay Update() bo qua phim Tab, de
    /// AquariumView (phim Esc) toan quyen dong/mo, va thay vao do lang
    /// nghe phim C de "lay lai" con ca dang trung bay o be dang xem.
    /// </summary>
    private bool _openedByAquarium;

    /// <summary>
    /// TankId cua be DANG duoc xem (AquariumView nao vua goi
    /// OpenForAquarium truyen vao). -1 = khong dang xem be nao. Dung
    /// de biet: bam chon 1 con ca thi dat vao BE NAO, bam C thi lay ca
    /// ra khoi BE NAO.
    /// </summary>
    private int _activeTankId = -1;

    /// <summary>
    /// Panel dang duoc chon CHO TUNG BE - key la TankId, value la panel
    /// (con ca) dang trung bay trong be do. Panel trong dictionary nay
    /// dang bi AN (SetActive(false)) khoi thung ca trong luc con ca cua
    /// no con nam trong be tuong ung. Dung de: (1) AN panel cu di khi
    /// be do duoc gan con ca khac, va (2) HIEN lai panel khi nguoi choi
    /// bam C de lay ca ra khoi be dang xem.
    ///
    /// LUU Y (FIX): dictionary nay KHONG phai nguon su that ve viec be
    /// co ca hay khong (xem giai thich o phan doc XML cua class ben
    /// tren) - de biet be co ca that hay khong, luon hoi
    /// AquariumDisplayManager.GetByTankId(tankId).CurrentItemId.
    /// </summary>
    private readonly Dictionary<int, FishReturnPanelUI> _selectedPanelByTank = new();

    /// <summary>Coroutine dang chay cua PlayEmptyCrateFeedback(), null neu khong co.</summary>
    private Coroutine _emptyFeedbackRoutine;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        BuildFishPanels();
        SetOpenState(false);

        if (_retrieveHintUI != null)
            _retrieveHintUI.SetActive(false);

        if (_emptyCrateMessageUI != null)
            _emptyCrateMessageUI.SetActive(false);

        if (_emptyCrateMessageText != null)
            _emptyCrateMessageText.text = _emptyCrateMessage;
    }

    private void Update()
    {
        if (_openedByAquarium)
        {
            // Dang o AquariumView cua 1 be nao do: Tab bi khoa
            // (AquariumView/Esc toan quyen dong/mo), thay vao do lang
            // nghe phim C de lay lai con ca dang trung bay o be DANG
            // XEM (_activeTankId), neu co.
            //
            // FIX: hoi THANG AquariumDisplayManager (CurrentItemId)
            // thay vi chi dua vao _selectedPanelByTank - vi ca duoc
            // AquariumPersistenceLoader dat lai luc load scene
            // (khong di qua HandleFishSelected) se KHONG co entry
            // trong _selectedPanelByTank, nhung be do VAN CO ca that
            // su can duoc phep bam C de lay ra.
            if (_activeTankId >= 0 && Input.GetKeyDown(KeyCode.C))
            {
                AquariumDisplayManager activeTank =
                    AquariumDisplayManager.GetByTankId(_activeTankId);

                if (activeTank != null && activeTank.CurrentItemId != null)
                    RetrieveCurrentFish();
            }

            return;
        }

        if (LobbyChatUI.IsTyping)
            return;

        // Dang chay feedback "thung ca rong" - bo qua Tab cho toi khi
        // icon hien lai xong, tranh spam mo coroutine chong nhau.
        if (_emptyFeedbackRoutine != null)
            return;

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (!_isCrateOpen && !HasSelectableFish())
            {
                PlayEmptyCrateFeedback();
                return;
            }

            SetOpenState(!_isCrateOpen);
        }
    }

    /// <summary>
    /// True neu thung ca hien co IT NHAT 1 panel dang HIEN THI thuc su
    /// (co the bam chon) - KHONG dung _spawnedPanels.Count truc tiep,
    /// vi list do van giu nguyen panel cua nhung con ca DA duoc dat
    /// vao 1 be nao do (chi bi SetActive(false) di, khong bi xoa khoi
    /// list). Neu chi dua vao Count, thung ca se bi coi la "con ca"
    /// ngay ca khi TAT CA panel deu dang an (vi ca cua chung dang nam
    /// het trong cac be) - dan den hien 1 khung thung ca TRONG RONG
    /// thay vi thong bao "thung ca trong" than thien.
    /// </summary>
    private bool HasSelectableFish()
    {
        for (int i = 0; i < _spawnedPanels.Count; i++)
        {
            if (_spawnedPanels[i] != null && _spawnedPanels[i].gameObject.activeSelf)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Quyet dinh LAI xem, TRONG LUC dang o AquariumView cua 1 be
    /// (_openedByAquarium == true), nen hien danh sach panel + close
    /// hint ("TAB - Close" - danh cho kieu mo bang Tab, khong ap dung
    /// khi dang o AquariumView nhung van dung chung 1 GameObject nen
    /// van goi la "close hint" o day) HAY hien thong bao "thung ca
    /// trong" than thien, dua tren HasSelectableFish() (co it nhat 1
    /// panel dang HIEN THI thuc su hay khong).
    ///
    /// KHONG lam gi neu KHONG dang o AquariumView (_openedByAquarium ==
    /// false) - luc do trang thai mo/dong do nguoi choi tu bam Tab
    /// dieu khien qua SetOpenState(), ham nay khong can/khong nen can
    /// thiep.
    ///
    /// FIX: truoc day, moi ("Nguoi choi bam chon 1 panel de dat ca vao
    /// be" - HandleFishSelected) chi AN panel vua chon di ma KHONG
    /// kiem tra lai xem thung ca co con panel nao khac de hien khong -
    /// neu day la panel CUOI CUNG con lai, _fishPanelContainer +
    /// _closeHintUI se ket dinh o trang thai "dang mo" (dang BAT) mai
    /// mai du danh sach do gio trong rong, thay vi tu dong chuyen sang
    /// thong bao "thung ca trong". Goi ham nay o CA 3 noi co the lam
    /// thay doi so panel dang hien thi (OpenForAquarium luc moi mo,
    /// HandleFishSelected sau khi chon 1 con ca, RetrieveCurrentFish
    /// sau khi lay 1 con ca ra) dam bao UI luon nhat quan, khong bi sot
    /// truong hop nao.
    /// </summary>
    private void RefreshCrateVisibilityForAquarium()
    {
        if (!_openedByAquarium)
            return;

        if (HasSelectableFish())
        {
            if (_emptyCrateMessageUI != null)
                _emptyCrateMessageUI.SetActive(false);

            SetOpenState(true);
        }
        else
        {
            SetOpenState(false);

            if (_crateIconUI != null)
                _crateIconUI.SetActive(false);

            if (_emptyCrateMessageUI != null)
                _emptyCrateMessageUI.SetActive(true);
        }
    }

    // ============================================================
    // BUILD FISH PANELS
    // ============================================================

    private void BuildFishPanels()
    {
        if (_fishPanelContainer == null || _fishPanelPrefab == null)
        {
            Debug.LogWarning(
                "[MuseumFishCrateUI] Thieu _fishPanelContainer hoac " +
                "_fishPanelPrefab trong Inspector."
            );
            return;
        }

        // Don sach panel cu (phong khi scene nay bi load lai).
        for (int i = _spawnedPanels.Count - 1; i >= 0; i--)
        {
            if (_spawnedPanels[i] != null)
                Destroy(_spawnedPanels[i].gameObject);
        }

        _spawnedPanels.Clear();
        _selectedPanelByTank.Clear();

        IReadOnlyList<ReturningCatchData.CaughtFishEntry> entries =
            ReturningCatchData.Entries;

        for (int i = 0; i < entries.Count; i++)
        {
            ReturningCatchData.CaughtFishEntry entry = entries[i];

            if (entry.Item == null || !entry.Item.IsFish)
                continue;

            FishReturnPanelUI panel =
                Instantiate(_fishPanelPrefab, _fishPanelContainer);

            panel.SetData(entry.Item, entry.SizeMultiplier);
            panel.OnFishSelected += HandleFishSelected;

            _spawnedPanels.Add(panel);
        }

        if (_clearDataAfterDisplay)
            ReturningCatchData.Clear();
    }

    /// <summary>
    /// Nguoi choi bam chon 1 panel ca (chi co tac dung thuc su khi
    /// dang o AquariumView cua 1 be, vi chi luc do panel moi hien len
    /// va nguoi choi moi bam vao duoc) - dua con ca do vao BE DANG XEM
    /// (_activeTankId) thong qua AquariumDisplayManager.GetByTankId,
    /// roi:
    ///   - AN han panel cu CUA CHINH BE DO di (neu co, khac panel
    ///     moi) - tra no ve hien thi trong thung ca truoc, vi con ca
    ///     cua no khong con trong be do nua. Cac be KHAC hoan toan
    ///     khong bi anh huong.
    ///   - AN han panel moi di khoi thung ca (con ca cua no dang trong
    ///     be roi, khong can hien lai trong danh sach nua).
    ///   - Bat _retrieveHintUI de nhac nguoi choi bam C neu muon lay
    ///     ca ra.
    ///   - FIX: goi AquariumSaveService.SaveTank de LUU lai (Firestore)
    ///     con ca vua dat vao be nay - lan sau vao lai Museum se tu
    ///     dat lai dung con ca nay vao dung be (xem
    ///     AquariumPersistenceLoader).
    /// </summary>
    private void HandleFishSelected(
    ItemData fish,
    float sizeMultiplier,
    FishReturnPanelUI panel)
    {
        if (_activeTankId < 0)
        {
            Debug.LogWarning(
                "[MuseumFishCrateUI] HandleFishSelected duoc goi nhung " +
                "khong dang xem be nao (_activeTankId = -1)."
            );
            return;
        }

        AquariumDisplayManager tank = AquariumDisplayManager.GetByTankId(_activeTankId);

        // FIX: lay lai thong tin con ca CU (neu be dang co ca) TRUOC khi
        // PlaceFish() xoa no - can de tra ve thung ca, ke ca truong hop
        // con ca cu duoc khoi phuc tu Firestore (khong co panel san).
        string oldItemId = tank != null ? tank.CurrentItemId : null;
        float oldSizeMultiplier = tank != null ? tank.CurrentSizeMultiplier : 0f;

        GameObject placedInstance = tank != null
            ? tank.PlaceFish(fish, sizeMultiplier)
            : null;

        if (placedInstance == null)
        {
            Debug.LogWarning(
                $"[MuseumFishCrateUI] Khong the dat {fish.DisplayName} " +
                $"vao be (TankId={_activeTankId}) - thieu " +
                "AquariumDisplayManager cho TankId nay hoac thieu " +
                "Aquarium Prefab / FishData.WorldPrefab."
            );
            return;
        }

        // Tra con ca CU ve thung ca.
        if (!string.IsNullOrEmpty(oldItemId))
        {
            if (_selectedPanelByTank.TryGetValue(_activeTankId, out var oldPanel) &&
                oldPanel != null &&
                oldPanel != panel)
            {
                // Ca cu duoc chon trong CHINH PHIEN nay -> hien lai panel co san.
                oldPanel.SetSelected(false);
                oldPanel.gameObject.SetActive(true);
            }
            else
            {
                // Ca cu duoc khoi phuc tu Firestore (khong co panel) ->
                // tao panel MOI cho no, de khong bi "bien mat".
                CreatePanelForRetrievedFish(oldItemId, oldSizeMultiplier);
            }
        }

        panel.SetSelected(true);
        panel.gameObject.SetActive(false);
        _selectedPanelByTank[_activeTankId] = panel;

        if (_retrieveHintUI != null)
            _retrieveHintUI.SetActive(true);

        // Phai goi SAU khi da tra ca cu ve, de HasSelectableFish() thay
        // panel vua hien lai va giu danh sach mo (khong nhay sang thong
        // bao "thung ca trong").
        RefreshCrateVisibilityForAquarium();

        AquariumSaveService.SaveTank(
            _activeTankId,
            fish.ItemId,
            sizeMultiplier
        );
    }

    /// <summary>
    /// Nguoi choi bam C khi dang o AquariumView cua 1 be VA be do dang
    /// co 1 con ca trung bay - lay con ca do ra khoi DUNG BE dang xem
    /// (_activeTankId), TRA NO VE thung ca (panel co san se duoc hien
    /// lai, hoac 1 panel MOI se duoc tao neu chua co - xem
    /// CreatePanelForRetrievedFish), va tat _retrieveHintUI di.
    ///
    /// FIX (lan 1): ham nay gio luon clear duoc tank mien la
    /// AquariumDisplayManager cua be do dang co ca that (CurrentItemId
    /// != null), KHONG con doi hoi phai co san entry trong
    /// _selectedPanelByTank nua - vi con ca duoc
    /// AquariumPersistenceLoader dat vao luc load scene (tu Firestore)
    /// se khong co entry trong dictionary do, nhung van la ca that su
    /// dang nam trong be va nguoi choi van phai lay ra duoc.
    ///
    /// FIX (lan 2): TRUOC DAY, neu con ca do khong co san panel trong
    /// _selectedPanelByTank (dung 1 con ca duoc AquariumPersistenceLoader
    /// dat vao tu Firestore, KHONG di qua HandleFishSelected trong phien
    /// nay), ham nay se xoa ca khoi be nhung KHONG co gi de "tra ve"
    /// thung ca ca - ca coi nhu bien mat, du du lieu be van duoc xoa
    /// dung. Gio, neu khong tim thay panel co san, mot panel MOI se
    /// duoc TAO NGAY LUC NAY (CreatePanelForRetrievedFish) dua vao
    /// ItemId + SizeMultiplier lay tu chinh tank TRUOC KHI ClearTank(),
    /// dam bao ca luon co noi de hien lai trong thung ca sau khi lay ra,
    /// bat ke no den tu 1 luot chon trong phien nay hay tu du lieu da
    /// luu (Firestore) tu phien truoc.
    ///
    /// FIX: goi AquariumSaveService.ClearTank de XOA entry cua be nay
    /// trong Firestore - lan sau vao lai Museum, be nay se KHONG con
    /// tu dong dat ca vao nua (dung trang thai trong nhu vua go ra).
    /// </summary>
    private void RetrieveCurrentFish()
    {
        if (_activeTankId < 0)
            return;

        AquariumDisplayManager tank = AquariumDisplayManager.GetByTankId(_activeTankId);

        if (tank == null || tank.CurrentItemId == null)
            return;

        // Lay lai ItemId/SizeMultiplier TRUOC khi ClearTank() xoa mat
        // du lieu nay - can den chung neu phai tao panel moi ben duoi.
        string retrievedItemId = tank.CurrentItemId;
        float retrievedSizeMultiplier = tank.CurrentSizeMultiplier;

        tank.ClearTank();

        SoundManager.Instance.PlaySFX("fishout");

        if (_selectedPanelByTank.TryGetValue(_activeTankId, out var panel) && panel != null)
        {
            // Panel co san (ca duoc CHON trong CHINH PHIEN nay) - chi
            // can hien lai no trong thung ca.
            panel.SetSelected(false);
            panel.gameObject.SetActive(true);
        }
        else
        {
            // Khong co panel co san - day la ca duoc khoi phuc tu
            // Firestore luc load scene (ReturningCatchData cua phien
            // do da bi Clear tu lau). Tao 1 panel MOI ngay bay gio de
            // ca khong bi "mat tich" khoi thung ca sau khi lay ra.
            CreatePanelForRetrievedFish(retrievedItemId, retrievedSizeMultiplier);
        }

        _selectedPanelByTank.Remove(_activeTankId);

        if (_retrieveHintUI != null)
            _retrieveHintUI.SetActive(false);

        // Neu ket qua thay doi trang thai "co panel de chon hay
        // khong", chuyen UI theo cho dung - dung CHUNG 1 tieu chuan
        // (RefreshCrateVisibilityForAquarium) voi OpenForAquarium va
        // HandleFishSelected, thay vi tu kiem tra rieng o day.
        RefreshCrateVisibilityForAquarium();

        // FIX: xoa entry be nay trong du lieu luu.
        AquariumSaveService.ClearTank(_activeTankId);
    }

    /// <summary>
    /// Tao 1 FishReturnPanelUI MOI cho con ca vua duoc bam C lay ra
    /// khoi be, dung cho truong hop no KHONG co san panel nao trong
    /// _selectedPanelByTank (ca duoc AquariumPersistenceLoader dat vao
    /// be tu Firestore luc load scene, chua bao gio di qua thung ca
    /// cua phien choi nay). Tra ItemData tu _itemCatalog bang itemId,
    /// Instantiate 1 panel vao _fishPanelContainer y het BuildFishPanels(),
    /// dang ky OnFishSelected, va them vao _spawnedPanels de no duoc
    /// tinh vao danh sach ca hien co trong thung ca (Tab mo binh thuong
    /// se thay no, bam chon lai cung hoat dong binh thuong).
    /// </summary>
    private void CreatePanelForRetrievedFish(string itemId, float sizeMultiplier)
    {
        if (_itemCatalog == null)
        {
            Debug.LogWarning(
                "[MuseumFishCrateUI] Thieu _itemCatalog trong Inspector - " +
                $"khong the tao lai panel cho ca vua lay ra (ItemId=" +
                $"'{itemId}'), ca se KHONG hien trong thung ca."
            );
            return;
        }

        if (_fishPanelContainer == null || _fishPanelPrefab == null)
        {
            Debug.LogWarning(
                "[MuseumFishCrateUI] Thieu _fishPanelContainer hoac " +
                "_fishPanelPrefab trong Inspector - khong the tao lai " +
                $"panel cho ca vua lay ra (ItemId='{itemId}')."
            );
            return;
        }

        ItemData fish = _itemCatalog.GetById(itemId);

        if (fish == null || !fish.IsFish)
        {
            Debug.LogWarning(
                $"[MuseumFishCrateUI] Khong tim thay ItemData hop le cho " +
                $"ItemId='{itemId}' trong ItemCatalog - khong the tao lai " +
                "panel cho ca vua lay ra."
            );
            return;
        }

        FishReturnPanelUI panel = Instantiate(_fishPanelPrefab, _fishPanelContainer);

        panel.SetData(fish, sizeMultiplier);
        panel.OnFishSelected += HandleFishSelected;

        _spawnedPanels.Add(panel);
    }

    // ============================================================
    // OPEN / CLOSE (Tab thu cong)
    // ============================================================

    private void SetOpenState(bool open)
    {
        _isCrateOpen = open;

        if (_crateIconUI != null)
            _crateIconUI.SetActive(!open);

        if (_fishPanelContainer != null)
            _fishPanelContainer.gameObject.SetActive(open);

        if (_closeHintUI != null)
            _closeHintUI.SetActive(open);
    }

    // ============================================================
    // EMPTY CRATE FEEDBACK (khong mang con ca nao ve - duong Tab)
    // ============================================================

    private void PlayEmptyCrateFeedback()
    {
        if (_emptyFeedbackRoutine != null)
            StopCoroutine(_emptyFeedbackRoutine);

        _emptyFeedbackRoutine = StartCoroutine(EmptyCrateFeedbackRoutine());
    }

    private IEnumerator EmptyCrateFeedbackRoutine()
    {
        // An het - khong icon, khong panel, khong close hint - de bao
        // hieu "da bam Tab nhung khong co gi de xem".
        if (_crateIconUI != null)
            _crateIconUI.SetActive(false);

        if (_fishPanelContainer != null)
            _fishPanelContainer.gameObject.SetActive(false);

        if (_closeHintUI != null)
            _closeHintUI.SetActive(false);

        yield return new WaitForSeconds(_emptyCrateFeedbackDuration);

        _emptyFeedbackRoutine = null;

        // Chi hien lai icon neu khong co gi khac da thay doi trang thai
        // trong luc cho (VD nguoi choi vua bam E mo AquariumView).
        if (!_isCrateOpen && !_openedByAquarium && _crateIconUI != null)
            _crateIconUI.SetActive(true);
    }

    // ============================================================
    // OPEN / CLOSE (goi tu AquariumView cua 1 be cu the - khong dung
    // phim Tab)
    // ============================================================

    /// <summary>
    /// Mo panel nay len theo yeu cau cua AquariumView CUA BE co TankId
    /// tuong ung, KHONG can nguoi choi bam Tab. Khoa phim Tab lai cho
    /// toi khi CloseForAquarium() duoc goi.
    ///
    /// - Neu nguoi choi CO mang ca ve (_spawnedPanels.Count > 0): hien
    ///   danh sach panel ca binh thuong (SetOpenState(true)), va neu
    ///   be nay dang co san 1 con ca trung bay thi hien lai
    ///   _retrieveHintUI luon.
    /// - Neu KHONG mang con ca nao ve: KHONG mo danh sach/close-hint
    ///   (se trong rong, vo nghia) - thay vao do hien 1 dong text than
    ///   thien bao "thung ca dang trong", GIU NGUYEN cho toi khi thoat
    ///   AquariumView (xem CloseForAquarium).
    ///
    /// FIX: viec quyet dinh _retrieveHintUI co hien hay khong gio dua
    /// THANG vao AquariumDisplayManager.GetByTankId(tankId).CurrentItemId
    /// (nguon su that ve viec be co ca hay khong), thay vi chi hoi
    /// _selectedPanelByTank.ContainsKey(tankId) - vi ca duoc
    /// AquariumPersistenceLoader dat lai luc load scene se khong co
    /// entry trong dictionary do nhung van la ca that trong be.
    /// </summary>
    /// <param name="tankId">TankId cua be vua duoc mo xem.</param>
    public void OpenForAquarium(int tankId)
    {
        _openedByAquarium = true;
        _activeTankId = tankId;

        // Dung chung RefreshCrateVisibilityForAquarium() (dua tren
        // HasSelectableFish()) voi HandleFishSelected/RetrieveCurrentFish,
        // thay vi tu kiem tra rieng - dam bao ca 3 noi luon quyet dinh
        // giong nhau khi cung 1 trang thai.
        RefreshCrateVisibilityForAquarium();

        // _retrieveHintUI CHI phu thuoc vao viec be DANG XEM co ca that
        // hay khong (AquariumDisplayManager.CurrentItemId), hoan toan
        // doc lap voi viec thung ca dang hien danh sach hay thong bao
        // rong - 1 be van co the dang co ca (nen can hien hint bam C)
        // trong khi thung ca dong thoi rong (khong co con nao KHAC de
        // chon).
        if (_retrieveHintUI != null)
        {
            AquariumDisplayManager activeTank = AquariumDisplayManager.GetByTankId(tankId);
            _retrieveHintUI.SetActive(activeTank != null && activeTank.CurrentItemId != null);
        }
    }

    /// <summary>
    /// Dong panel nay lai theo yeu cau cua AquariumView (luc nguoi
    /// choi bam Esc de thoat AquariumView cua be co TankId tuong
    /// ung), mo khoa lai phim Tab, tat _retrieveHintUI va thong bao
    /// "thung ca trong" (neu dang hien) di.
    /// </summary>
    /// <param name="tankId">
    /// TankId cua be vua dong. Chi thuc su dong neu day dung la be
    /// DANG DUOC XEM (_activeTankId) - phong truong hop goi nham thu
    /// tu (khong nen xay ra trong luong choi binh thuong, vi chi 1
    /// AquariumView co the IsActive tai 1 thoi diem).
    /// </param>
    public void CloseForAquarium(int tankId)
    {
        if (_activeTankId != tankId)
            return;

        _openedByAquarium = false;
        _activeTankId = -1;
        SetOpenState(false);

        if (_retrieveHintUI != null)
            _retrieveHintUI.SetActive(false);

        if (_emptyCrateMessageUI != null)
            _emptyCrateMessageUI.SetActive(false);
    }
}