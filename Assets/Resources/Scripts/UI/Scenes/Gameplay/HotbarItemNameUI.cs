using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HotbarItemNameUI : MonoBehaviour
{
    [Header("UI - Item Name")]

    [SerializeField]
    private TMP_Text _itemNameText;

    [SerializeField]
    private CanvasGroup _canvasGroup;


    [Header("UI - Fame Point")]

    [Tooltip("Panel cha chứa toàn bộ Fame Point (I-BG, I-Star, T-FamePoints). Mặc định TẮT, chỉ bật khi item đang cầm là cá.")]
    [SerializeField]
    private GameObject _famePointPanel;

    [Tooltip("Text hiển thị số Fame Point.")]
    [SerializeField]
    private TMP_Text _famePointText;

    [Tooltip("Icon ngôi sao Fame Point.")]
    [SerializeField]
    private Image _fameStarImage;


    [Header("Display")]

    [SerializeField]
    private float _visibleDuration = 2f;

    [SerializeField]
    private float _fadeDuration = 0.5f;


    // ============================================================
    // Runtime
    // ============================================================

    private PlayerInventory _inventory;

    private Coroutine _hideRoutine;

    // Level cần câu cuối cùng đã hiển thị.
    // Dùng để phát hiện khi TeamRodUpgrade thay đổi level.
    private int _lastRodLevel = -1;


    // ============================================================
    // AWAKE
    // ============================================================

    private void Awake()
    {
        // Panel Fame Point phải tắt ngay từ đầu,
        // chỉ bật khi player cầm item là cá.
        if (_famePointPanel != null)
        {
            _famePointPanel.SetActive(false);
        }
    }


    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        // Không có inventory thì không làm gì.
        if (_inventory == null)
            return;

        // Lấy item đang cầm.
        ItemData currentItem =
            _inventory.GetSelectedItem();

        // Chỉ cần kiểm tra level nếu item hiện tại là fishing rod.
        if (currentItem == null ||
            !currentItem.IsFishingRod)
        {
            return;
        }

        // TeamRodUpgrade chưa sẵn sàng.
        if (TeamRodUpgrade.Instance == null)
            return;

        int currentRodLevel =
            TeamRodUpgrade.Instance.RodLevel;

        // Nếu level đã thay đổi thì refresh tên cần câu.
        if (currentRodLevel != _lastRodLevel)
        {
            _lastRodLevel =
                currentRodLevel;

            RefreshCurrentSlot();
        }
    }


    // ============================================================
    // SET INVENTORY
    // ============================================================

    /// <summary>
    /// HotbarUI gọi hàm này sau khi tìm được
    /// PlayerInventory của player local.
    /// </summary>
    public void SetInventory(
        PlayerInventory inventory)
    {
        // Nếu đang dùng inventory cũ
        if (_inventory != null)
        {
            _inventory.OnSelectedSlotChanged -=
                OnSelectedSlotChanged;
        }


        _inventory = inventory;

        // Reset level cache.
        _lastRodLevel = -1;


        // Subscribe inventory mới
        if (_inventory != null)
        {
            _inventory.OnSelectedSlotChanged +=
                OnSelectedSlotChanged;

            RefreshCurrentSlot();
        }
    }


    // ============================================================
    // DISABLE
    // ============================================================

    private void OnDisable()
    {
        if (_inventory != null)
        {
            _inventory.OnSelectedSlotChanged -=
                OnSelectedSlotChanged;
        }
    }


    // ============================================================
    // REFRESH CURRENT SLOT
    // ============================================================

    private void RefreshCurrentSlot()
    {
        if (_inventory == null)
            return;


        int index =
            _inventory.SelectedIndex;


        ItemData item =
            _inventory.GetSelectedItem();


        Debug.Log(
            $"[HotbarItemNameUI] Refresh Slot = {index}, " +
            $"Item = {(item != null ? item.name : "NULL")}"
        );


        OnSelectedSlotChanged(
            index,
            item
        );
    }


    // ============================================================
    // SELECTED SLOT CHANGED
    // ============================================================

    private void OnSelectedSlotChanged(
        int index,
        ItemData item)
    {
        if (item == null)
        {
            _lastRodLevel = -1;

            Hide();

            return;
        }


        // ========================================================
        // FISH
        // ========================================================

        if (item.IsFish)
        {
            _lastRodLevel = -1;

            ShowFishName(
                item,
                index
            );

            return;
        }


        // ========================================================
        // FISHING ROD
        // ========================================================

        if (item.IsFishingRod)
        {
            ShowFishingRodName(
                item
            );

            return;
        }


        // ========================================================
        // NORMAL ITEM
        // ========================================================

        _lastRodLevel = -1;

        ShowItemName(
            item.GetColoredDisplayName(),
            Color.white
        );
    }


    // ============================================================
    // SHOW FISHING ROD NAME
    // ============================================================

    private void ShowFishingRodName(
        ItemData item)
    {
        if (item == null)
        {
            Hide();

            return;
        }


        // Level mặc định.
        int rodLevel = 1;


        // Lấy level hiện tại của team.
        if (TeamRodUpgrade.Instance != null)
        {
            rodLevel =
                TeamRodUpgrade.Instance.RodLevel;
        }


        // Đảm bảo level không nhỏ hơn 1.
        rodLevel =
            Mathf.Max(
                1,
                rodLevel
            );


        // Cache level hiện tại.
        _lastRodLevel =
            rodLevel;


        // ========================================================
        // TÊN CẦN CÂU
        // ========================================================

        string displayName =
            $"{item.DisplayName} (Lv{rodLevel})";


        Debug.Log(
            $"[HotbarItemNameUI] " +
            $"Fishing Rod = {item.DisplayName} | " +
            $"Rod Level = {rodLevel} | " +
            $"Display = {displayName}"
        );


        ShowItemName(
            displayName,
            Color.white
        );
    }


    // ============================================================
    // SHOW FISH NAME + KG + FAME
    // ============================================================

    private void ShowFishName(
        ItemData item,
        int slotIndex)
    {
        if (item == null)
        {
            Hide();

            return;
        }


        FishData fishData =
            item.FishInfo;


        if (fishData == null)
        {
            ShowItemName(
                item.GetColoredDisplayName(),
                Color.white
            );

            return;
        }


        // ========================================================
        // LẤY SIZE CỦA CÁ
        // ========================================================

        float sizeMultiplier = 1f;


        if (_inventory != null)
        {
            sizeMultiplier =
                _inventory.GetSlotFishSize(
                    slotIndex
                );
        }


        // ========================================================
        // TÍNH KG
        // ========================================================

        float weightKg =
            fishData.GetWeightKg(
                sizeMultiplier
            );


        // ========================================================
        // TÊN CÁ CÓ MÀU RARITY
        // ========================================================

        string displayName =
            fishData.GetColoredDisplayName();


        // ========================================================
        // TEXT KG
        // ========================================================

        string weightText =
            fishData.GetWeightText(
                sizeMultiplier
            );


        // ========================================================
        // FINAL NAME
        // ========================================================

        string finalText =
            $"{displayName} - {weightText}";


        // ========================================================
        // FAME POINT
        // ========================================================

        int famePoint =
            fishData.GetFamePoint(
                weightKg
            );


        string fameText =
            fishData.FormatFamePoint(
                famePoint
            );


        // ========================================================
        // DEBUG
        // ========================================================

        Debug.Log(
            $"[HotbarItemNameUI] " +
            $"FISH = {fishData.FishName} | " +
            $"Slot = {slotIndex} | " +
            $"Size = {sizeMultiplier:0.##}x | " +
            $"Weight = {weightKg:0.##} KG | " +
            $"Fame = {famePoint} | " +
            $"Text = {finalText}"
        );


        // ========================================================
        // SHOW NAME
        // ========================================================

        ShowItemName(
            finalText,
            Color.white
        );


        // ========================================================
        // SHOW FAME
        // ========================================================

        ShowFamePoint(
            fameText,
            fishData
        );
    }


    // ============================================================
    // SHOW FAME POINT
    // ============================================================

    private void ShowFamePoint(
        string fameText,
        FishData fishData)
    {
        // --------------------------------------------------------
        // SHOW PANEL
        // --------------------------------------------------------

        if (_famePointPanel != null)
        {
            _famePointPanel.SetActive(
                true
            );
        }


        // --------------------------------------------------------
        // FAME TEXT
        // --------------------------------------------------------

        if (_famePointText != null)
        {
            _famePointText.text =
                fameText;
        }
    }


    // ============================================================
    // HIDE FAME
    // ============================================================

    private void HideFamePoint()
    {
        // Tắt nguyên panel — I-BG, I-Star, T-FamePoints
        // sẽ tự động ẩn theo panel cha.
        if (_famePointPanel != null)
        {
            _famePointPanel.SetActive(
                false
            );
        }
    }


    // ============================================================
    // SHOW ITEM NAME
    // ============================================================

    public void ShowItemName(
        string displayName,
        Color color)
    {
        if (_itemNameText == null)
        {
            Debug.LogWarning(
                "[HotbarItemNameUI] " +
                "_itemNameText chưa được gán!"
            );

            return;
        }


        if (_canvasGroup == null)
        {
            Debug.LogWarning(
                "[HotbarItemNameUI] " +
                "_canvasGroup chưa được gán!"
            );

            return;
        }


        if (string.IsNullOrEmpty(displayName))
        {
            Hide();

            return;
        }


        // --------------------------------------------------------
        // TEXT
        // --------------------------------------------------------

        _itemNameText.text =
            displayName;


        _itemNameText.color =
            color;


        // --------------------------------------------------------
        // ITEM THƯỜNG KHÔNG CÓ FAME
        // --------------------------------------------------------

        HideFamePoint();


        // --------------------------------------------------------
        // SHOW
        // --------------------------------------------------------

        _canvasGroup.alpha =
            1f;


        // --------------------------------------------------------
        // STOP OLD COROUTINE
        // --------------------------------------------------------

        if (_hideRoutine != null)
        {
            StopCoroutine(
                _hideRoutine
            );
        }


        // --------------------------------------------------------
        // START HIDE TIMER
        // --------------------------------------------------------

        _hideRoutine =
            StartCoroutine(
                HideAfterDelay()
            );
    }


    // ============================================================
    // HIDE
    // ============================================================

    private void Hide()
    {
        if (_hideRoutine != null)
        {
            StopCoroutine(
                _hideRoutine
            );

            _hideRoutine = null;
        }


        if (_canvasGroup != null)
        {
            _canvasGroup.alpha =
                0f;
        }


        HideFamePoint();
    }


    // ============================================================
    // HIDE AFTER DELAY
    // ============================================================

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(
            _visibleDuration
        );


        float t = 0f;


        while (t < _fadeDuration)
        {
            t += Time.deltaTime;


            if (_canvasGroup != null)
            {
                _canvasGroup.alpha =
                    Mathf.Lerp(
                        1f,
                        0f,
                        t / _fadeDuration
                    );
            }


            yield return null;
        }


        if (_canvasGroup != null)
        {
            _canvasGroup.alpha =
                0f;
        }


        HideFamePoint();


        _hideRoutine = null;
    }
}