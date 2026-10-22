using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 1 dong xep hang trong panel Leaderboard (tab MUSEUM) - hien so thu
/// tu, ten Museum (nickname nguoi choi), tong Fame Point cua Museum
/// do, va 1 nut "liet ke" (mui ten) de XO XUONG danh sach card tung
/// con ca dang trung bay trong cac be cua Museum do.
///
/// Danh sach card ca CHI duoc TAO (Instantiate) o LAN DAU TIEN bam mo
/// rong (lazy-build) - nhung LAN SAU chi don gian SetActive lai
/// container, KHONG tao lai - vi du lieu 1 dong KHONG doi trong suot
/// thoi gian panel Leaderboard dang mo (chi fetch 1 lan luc mo panel,
/// xem MuseumLeaderboardUI).
///
/// Duoc MuseumLeaderboardUI Instantiate hang loat (1 dong / 1 nguoi
/// choi trong top N) vao 1 container CO Vertical Layout Group (dat
/// san trong Inspector) de cac dong TU XEP DEU theo chieu doc.
/// </summary>
public class MuseumLeaderboardEntryUI : MonoBehaviour
{
    [Header("Row Info")]
    [SerializeField] private TMP_Text _rankText;
    [SerializeField] private TMP_Text _museumNameText;
    [SerializeField] private TMP_Text _famePointText;

    [Header("Expand (liet ke ca dang trung bay)")]
    [Tooltip("Nut mui ten - bam de xo xuong/thu gon danh sach card ca.")]
    [SerializeField] private Button _expandButton;

    [Tooltip(
        "Container chua cac card ca sau khi xo xuong - DAT Horizontal " +
        "Layout Group (hoac Grid Layout Group) ngay tren CHINH object " +
        "nay de cac card tu xep deu theo hang ngang. Mac dinh TAT " +
        "(SetActive(false)) tren prefab - script se tu BAT/TAT."
    )]
    [SerializeField] private GameObject _fishCardContainer;

    [SerializeField] private LeaderboardFishCardUI _fishCardPrefab;

    [Tooltip(
        "Xoay Icon mui ten (neu co) 180 do luc mo rong / tra ve 0 do " +
        "luc thu gon - CHI la hieu ung phu, de trong (null) van hoat " +
        "dong binh thuong, chi la khong co animation xoay mui ten."
    )]
    [SerializeField] private RectTransform _expandArrowIcon;

    private ItemCatalog _itemCatalog;
    private List<AquariumTankEntry> _tankEntries;

    private bool _isExpanded;
    private bool _cardsBuilt;

    private void Awake()
    {
        if (_expandButton != null)
            _expandButton.onClick.AddListener(ToggleExpand);
    }

    private void OnDestroy()
    {
        if (_expandButton != null)
            _expandButton.onClick.RemoveListener(ToggleExpand);
    }

    /// <summary>
    /// Dien du lieu cho 1 dong xep hang.
    /// </summary>
    /// <param name="rank">So thu tu (1-based) trong bang xep hang.</param>
    /// <param name="museumName">Ten hien thi cua Museum (nickname nguoi choi).</param>
    /// <param name="totalFamePoint">Tong Fame Point cua Museum nay.</param>
    /// <param name="tankEntries">
    /// Danh sach be dang co ca cua nguoi choi nay (PlayerData.AquariumTanks) -
    /// dung de "liet ke" ra tung card ca khi bam mo rong. Co the null
    /// (tai khoan cu chua co field nay, hoac Museum dang trong).
    /// </param>
    /// <param name="itemCatalog">
    /// Catalog dung de tra ItemData (roi FishData) tu ItemId luu trong
    /// tung entry - truyen tu MuseumLeaderboardUI (dung 1 catalog CHUNG
    /// cho ca panel, khong can gan rieng tung prefab dong trong
    /// Inspector).
    /// </param>
    public void SetData(
        int rank,
        string museumName,
        int totalFamePoint,
        List<AquariumTankEntry> tankEntries,
        ItemCatalog itemCatalog)
    {
        _tankEntries = tankEntries;
        _itemCatalog = itemCatalog;

        if (_rankText != null)
            _rankText.text = rank.ToString();

        if (_museumNameText != null)
            _museumNameText.text = museumName;

        if (_famePointText != null)
            _famePointText.text = FormatFamePoint(totalFamePoint);

        // Reset trang thai mo rong moi lan SetData (VD dong nay dang
        // duoc tai su dung cho 1 lan fetch Leaderboard MOI) - luon bat
        // dau o trang thai THU GON, va can build lai card tu dau vi du
        // lieu (tankEntries) co the da khac.
        _cardsBuilt = false;
        SetExpanded(false, buildIfNeeded: false);

        if (_fishCardContainer != null)
        {
            // Xoa sach card cu (neu co, tu 1 lan SetData truoc do) truoc
            // khi co the build lai o lan bam mo rong tiep theo.
            for (int i = _fishCardContainer.transform.childCount - 1; i >= 0; i--)
                Destroy(_fishCardContainer.transform.GetChild(i).gameObject);
        }
    }

    /// <summary>
    /// Format Fame Point cho dong xep hang theo kieu nhom hang nghin
    /// bang dau cham (VD 12167 -> "12.167"), khop voi cach hien thi
    /// trong hinh minh hoa. Dung rieng 1 CultureInfo co dinh (vi-VN)
    /// thay vi CultureInfo.CurrentCulture cua may nguoi choi, de dam
    /// bao LUON hien dung dau cham lam phan cach hang nghin bat ke
    /// build tren may/vung nao.
    /// </summary>
    private static string FormatFamePoint(int fame)
    {
        return fame.ToString(
            "N0",
            new System.Globalization.CultureInfo("vi-VN")
        );
    }

    private void ToggleExpand()
    {
        SetExpanded(!_isExpanded, buildIfNeeded: true);
    }

    private void SetExpanded(bool expanded, bool buildIfNeeded)
    {
        _isExpanded = expanded;

        if (_fishCardContainer != null)
            _fishCardContainer.SetActive(expanded);

        if (_expandArrowIcon != null)
            _expandArrowIcon.localEulerAngles = new Vector3(0f, 0f, expanded ? 180f : 0f);

        if (expanded && buildIfNeeded && !_cardsBuilt)
            BuildFishCards();
    }

    /// <summary>
    /// Tao 1 LeaderboardFishCardUI cho MOI entry trong _tankEntries -
    /// tra ItemData (roi FishData) qua _itemCatalog bang ItemId cua
    /// tung entry. Chi chay 1 LAN DUY NHAT cho moi lan SetData (lazy-
    /// build, xem _cardsBuilt).
    /// </summary>
    private void BuildFishCards()
    {
        _cardsBuilt = true;

        if (_tankEntries == null || _tankEntries.Count == 0)
            return;

        if (_itemCatalog == null)
        {
            Debug.LogWarning(
                "[MuseumLeaderboardEntryUI] Thieu ItemCatalog - khong " +
                "the tao card ca cho dong xep hang nay."
            );
            return;
        }

        if (_fishCardContainer == null || _fishCardPrefab == null)
        {
            Debug.LogWarning(
                "[MuseumLeaderboardEntryUI] Thieu _fishCardContainer " +
                "hoac _fishCardPrefab trong Inspector."
            );
            return;
        }

        foreach (AquariumTankEntry entry in _tankEntries)
        {
            if (string.IsNullOrEmpty(entry.ItemId))
                continue;

            ItemData item = _itemCatalog.GetById(entry.ItemId);

            if (item == null || !item.IsFish || item.FishInfo == null)
            {
                Debug.LogWarning(
                    $"[MuseumLeaderboardEntryUI] Khong tim thay " +
                    $"ItemData/FishData hop le cho ItemId=" +
                    $"'{entry.ItemId}' - bo qua, khong tao card cho " +
                    "con ca nay."
                );
                continue;
            }

            LeaderboardFishCardUI card =
                Instantiate(_fishCardPrefab, _fishCardContainer.transform);

            // Truyen ca ItemData (de lay ReturnIcon + layout Leaderboard).
            card.SetData(item, entry.SizeMultiplier);
        }
    }
}