using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 1 ô cá trong panel Fish Trader.
/// Gắn lên từng button ô (6 ô, tương ứng ô 2 → 7 của túi đồ).
/// </summary>
public class FishTraderSlot : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _priceText;

    [Tooltip("Object viền trắng — được bật khi ô này đang được chọn để bán.")]
    [SerializeField] private GameObject _selectedBorder;

    private Action<FishTraderSlot> _onClicked;

    // Transform GỐC của Icon (lấy 1 lần) — offset của từng loài cá được
    // cộng / nhân lên trên nền này, nên không làm hỏng layout đã canh sẵn.
    private bool _iconBaseCached;
    private Vector2 _iconBasePos;
    private Vector3 _iconBaseScale;
    private Quaternion _iconBaseRot;

    /// <summary>Index của ô này trong PlayerInventory.Slots.</summary>
    public int InventoryIndex { get; private set; } = -1;

    public ItemData Item { get; private set; }
    public float Size { get; private set; }
    public int Price { get; private set; }
    public bool IsSelected { get; private set; }
    public bool HasFish => Item != null;

    private void Reset()
    {
        _button = GetComponent<Button>();
    }

    // Được FishTraderUI gọi 1 lần (không phụ thuộc thứ tự Awake).
    public void Bind(Action<FishTraderSlot> onClicked)
    {
        _onClicked = onClicked;

        if (_button == null)
            return;

        _button.onClick.RemoveListener(HandleClick);
        _button.onClick.AddListener(HandleClick);
    }

    private void HandleClick()
    {
        // Ô trống → bỏ qua click.
        if (!HasFish)
            return;

        _onClicked?.Invoke(this);
    }

    public void SetFish(int inventoryIndex, ItemData item, float size, int price)
    {
        InventoryIndex = inventoryIndex;
        Item = item;
        Size = size;
        Price = price;

        if (_icon != null)
        {
            Sprite sprite = item != null ? item.Icon : null;
            _icon.sprite = sprite;
            _icon.enabled = sprite != null;
        }

        if (_priceText != null)
        {
            _priceText.text = $"{price}$";
            _priceText.gameObject.SetActive(true);
        }

        RefreshIconTransform();
    }

    public void SetEmpty(int inventoryIndex)
    {
        InventoryIndex = inventoryIndex;
        Item = null;
        Size = 0f;
        Price = 0;

        if (_icon != null)
        {
            _icon.sprite = null;
            _icon.enabled = false;
        }

        if (_priceText != null)
            _priceText.gameObject.SetActive(false);

        RefreshIconTransform();

        SetSelected(false);
    }

    // ============================================================
    // ICON TRANSFORM (chỉnh riêng từng loài cá trong ItemData)
    // ============================================================

    private void CacheIconBase()
    {
        if (_iconBaseCached || _icon == null)
            return;

        RectTransform rt = _icon.rectTransform;

        _iconBasePos = rt.anchoredPosition;
        _iconBaseScale = rt.localScale;
        _iconBaseRot = rt.localRotation;

        _iconBaseCached = true;
    }

    /// <summary>
    /// Áp vị trí / xoay / scale của icon theo ItemData.TraderIcon*.
    /// Ô trống → trả về transform gốc.
    /// </summary>
    public void RefreshIconTransform()
    {
        if (_icon == null)
            return;

        CacheIconBase();

        RectTransform rt = _icon.rectTransform;

        if (Item == null)
        {
            rt.anchoredPosition = _iconBasePos;
            rt.localScale = _iconBaseScale;
            rt.localRotation = _iconBaseRot;
            return;
        }

        Vector2 s = Item.TraderIconScale;

        rt.anchoredPosition = _iconBasePos + Item.TraderIconPosition;

        rt.localScale = new Vector3(
            _iconBaseScale.x * s.x,
            _iconBaseScale.y * s.y,
            _iconBaseScale.z
        );

        rt.localRotation =
            _iconBaseRot *
            Quaternion.Euler(0f, 0f, Item.TraderIconRotation);
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected && HasFish;

        if (_selectedBorder != null)
            _selectedBorder.SetActive(IsSelected);
    }
}