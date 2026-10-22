using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BaitShopEntry : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _priceText;
    [SerializeField] private TextMeshProUGUI _quantityText;
    [SerializeField] private Button _increaseBtn;
    [SerializeField] private Button _decreaseBtn;

    private BaitData _data;
    private int _quantity;

    public BaitData Data => _data;
    public int Quantity => _quantity;

    public event Action OnQuantityChanged;

    public void Setup(BaitData data)
    {
        _data = data;
        _quantity = 0;

        if (_icon != null) _icon.sprite = data.Icon;
        if (_nameText != null) _nameText.text = data.BaitName;
        if (_priceText != null) _priceText.text = $"{data.PricePerUnit}$";

        UpdateQuantityDisplay();

        _increaseBtn.onClick.RemoveAllListeners();
        _decreaseBtn.onClick.RemoveAllListeners();

        _increaseBtn.onClick.AddListener(Increase);
        _decreaseBtn.onClick.AddListener(Decrease);
    }

    public void ResetQuantity()
    {
        _quantity = 0;
        UpdateQuantityDisplay();
    }

    private void Increase()
    {
        _quantity++;
        UpdateQuantityDisplay();
        OnQuantityChanged?.Invoke();
    }

    private void Decrease()
    {
        if (_quantity <= 0) return;
        _quantity--;
        UpdateQuantityDisplay();
        OnQuantityChanged?.Invoke();
    }

    private void UpdateQuantityDisplay()
    {
        if (_quantityText != null)
            _quantityText.text = _quantity.ToString();
    }
}