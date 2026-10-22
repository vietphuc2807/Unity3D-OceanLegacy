using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 1 panel thong tin cho 1 con ca dang mang ve, hien trong "Thung ca
/// mang ve" o scene Museum: icon ca + text so Kg + text Fame Point.
///
/// Duoc MuseumFishCrateUI Instantiate hang loat, moi con ca trong
/// ReturningCatchData mot panel.
///
/// Icon (anh + vi tri / xoay / kich co) duoc lay RIENG cho panel nay
/// tu muc "Fish Return Icon" trong ItemData (_returnIcon,
/// _returnIconPosition / _returnIconRotation / _returnIconSize) -
/// KHONG dung chung anh/vi tri voi muc "Inventory Icon" hay "Fish
/// Trader Icon" da co san, de chinh o day khong lam anh huong may
/// cho UI khac.
///
/// Kich co icon duoc gan THANG bang Width/Height (sizeDelta) theo
/// ReturnIconSize cua ItemData - KHONG dung Scale + SetNativeSize,
/// nen chinh 1 con so la ra dung kich thuoc mong muon, khong phai
/// vua Scale vua canh lai Position.
///
/// MOI: panel gio co the BAM DUOC (_selectButton) - dung khi dang o
/// AquariumView (xem AquariumInteractable / AquariumView). Bam vao 1
/// panel se ban su kien OnFishSelected de MuseumFishCrateUI dua con
/// ca do vao AquariumDisplayManager (spawn vao be). Vi MOI BE CHI
/// CHUA DUOC 1 CON CA, panel KHONG tu khoa Button sau khi bam - thay
/// vao do chi BAT/TAT 1 khung vien (_selectedBorderUI) de danh dau
/// "day la con ca DANG CO trong be" - MuseumFishCrateUI se tu TAT
/// khung vien cua panel cu khi 1 panel khac duoc chon, dam bao chi
/// co toi da 1 panel dang duoc "khoanh vien" cung luc, dung voi thuc
/// te be chi chua duoc 1 con. Nguoi choi van bam duoc panel bat ky
/// luc nao de doi con ca dang trung bay.
///
/// Ngoai luc dang o AquariumView (VD dang xem "Thung ca mang ve"
/// binh thuong bang Tab) thi bam vao panel se khong co tac dung gi vi
/// khong ai lang nghe su kien - hoan toan an toan.
/// </summary>
public class FishReturnPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image _fishIcon;
    [SerializeField] private TMP_Text _fishNameText;
    [SerializeField] private TMP_Text _weightText;
    [SerializeField] private TMP_Text _famePointText;

    [Header("Chọn cá cho vào bể (dùng lúc đang ở AquariumView)")]
    [Tooltip(
        "Button bấm để chọn con cá này đưa vào bể trưng bày. Có thể " +
        "gán ngay Button trên root của prefab (kéo Image làm Target " +
        "Graphic) hoặc thêm 1 Button riêng phủ lên toàn panel."
    )]
    [SerializeField] private Button _selectButton;

    [Tooltip(
        "Khung viền (border) BẬT LÊN khi con cá này ĐANG LÀ con cá có " +
        "trong bể, TẮT khi người chơi chọn 1 con cá khác thay thế. " +
        "Đặt 1 Image viền (chỉ vẽ khung, không tô nền) chồng lên panel, " +
        "mặc định để tắt (SetActive(false)) trên prefab."
    )]
    [SerializeField] private GameObject _selectedBorderUI;

    /// <summary>
    /// Bắn ra khi người chơi bấm chọn panel này. Tham số: ItemData của
    /// con cá, size multiplier lúc bắt được, và chính panel này (để
    /// nơi lắng nghe gọi lại SetSelected(true) sau khi xử lý xong).
    /// </summary>
    public event Action<ItemData, float, FishReturnPanelUI> OnFishSelected;

    private ItemData _currentFish;
    private float _currentSizeMultiplier;

    public bool IsSelected { get; private set; }

    /// <summary>
    /// RectTransform cua icon - dung cho FishIconDebugCycler chinh
    /// truc tiep luc Play mode.
    /// </summary>
    public RectTransform IconRectTransform =>
        _fishIcon != null ? _fishIcon.rectTransform : null;

    private void Awake()
    {
        if (_selectButton != null)
            _selectButton.onClick.AddListener(HandleButtonClicked);
    }

    private void OnDestroy()
    {
        if (_selectButton != null)
            _selectButton.onClick.RemoveListener(HandleButtonClicked);
    }

    public void SetData(ItemData fishItem, float sizeMultiplier)
    {
        if (fishItem == null || !fishItem.IsFish)
        {
            gameObject.SetActive(false);
            return;
        }

        _currentFish = fishItem;
        _currentSizeMultiplier = sizeMultiplier;

        // Panel moi luon bat dau chua duoc chon (be co the dang chua
        // con ca khac, hoac dang trong) - MuseumFishCrateUI se tu bat
        // lai SetSelected(true) neu day dung la con ca dang co trong be.
        SetSelected(false);

        FishData fishData = fishItem.FishInfo;

        if (_fishNameText != null)
        {
            // GetColoredDisplayName() tra ve ten ca da boc san the
            // <color=#...> theo dung Rarity cua ca (lay tu
            // RarityColors dung chung), nen chi can gan thang vao
            // text - nho bat "Rich Text" (mac dinh TMP da bat san)
            // tren component TextMeshPro - Text (UI) cua text nay.
            _fishNameText.text = fishItem.GetColoredDisplayName();
        }

        if (_fishIcon != null)
        {
            // Dung anh RIENG cho panel Thung ca mang ve (ReturnIcon),
            // KHONG dung fishItem.Icon (anh Inventory) nua. ReturnIcon
            // tu fallback ve Icon neu chua gan anh rieng trong
            // Inspector, nen cac con ca cu van hien binh thuong.
            _fishIcon.sprite = fishItem.ReturnIcon;
            _fishIcon.enabled = fishItem.ReturnIcon != null;

            ApplyIconTransform(
                fishItem.ReturnIconPosition,
                fishItem.ReturnIconRotation,
                fishItem.ReturnIconSize
            );
        }

        if (_weightText != null)
        {
            _weightText.text = fishData != null
                ? fishData.GetWeightText(sizeMultiplier)
                : "";
        }

        if (_famePointText != null)
        {
            if (fishData != null)
            {
                float weightKg = fishData.GetWeightKg(sizeMultiplier);
                _famePointText.text = fishData.GetFamePointText(weightKg);
            }
            else
            {
                _famePointText.text = "";
            }
        }
    }

    /// <summary>
    /// Set thang vi tri (pixel UI, tuc anchoredPosition) / xoay quanh
    /// truc Z / kich co (Width, Height pixel UI, tuc sizeDelta) cua
    /// icon, khong dong qua ItemData - dung boi FishIconDebugCycler
    /// de "nudge" truc tiep luc Play mode roi doc lai gia tri de dien
    /// vao 3 field Return Icon Position / Rotation / Size cua
    /// ItemData sau.
    ///
    /// localScale luon giu = 1 - kich co that su duoc quyet dinh boi
    /// sizeDelta (Width/Height) chu khong phai Scale, nen khong con
    /// truong hop chinh Scale xong phai canh lai Position nua.
    /// </summary>
    public void ApplyIconTransform(Vector2 position, float rotationZ, Vector2 size)
    {
        RectTransform rt = IconRectTransform;

        if (rt == null)
            return;

        rt.anchoredPosition = position;
        rt.localEulerAngles = new Vector3(0f, 0f, rotationZ);
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
    }

    // ============================================================
    // SELECT BUTTON (chọn cá cho vào bể)
    // ============================================================

    private void HandleButtonClicked()
    {
        if (_currentFish == null)
            return;

        OnFishSelected?.Invoke(_currentFish, _currentSizeMultiplier, this);
    }

    /// <summary>
    /// Bật/tắt khung viền đánh dấu "con cá này đang có trong bể".
    /// Button KHÔNG bị khoá dù đang selected - người chơi vẫn có thể
    /// bấm panel khác bất kỳ lúc nào để thay con cá trong bể.
    /// </summary>
    public void SetSelected(bool selected)
    {
        IsSelected = selected;

        if (_selectedBorderUI != null)
            _selectedBorderUI.SetActive(selected);
    }
}