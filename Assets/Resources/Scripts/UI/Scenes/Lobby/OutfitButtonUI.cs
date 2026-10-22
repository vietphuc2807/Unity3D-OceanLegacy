using UnityEngine;
using UnityEngine.UI;

public class OutfitButtonUI : MonoBehaviour
{
    [SerializeField] private OutfitItemData _itemData;
    [SerializeField] private Image _iconImage;
    [SerializeField] private GameObject _selectedBorder;
    [SerializeField] private Button _button;

    public OutfitItemData ItemData => _itemData;

    private OutfitUI _owner;

    public void Initialize(OutfitUI owner)
    {
        _owner = owner;

        if (_iconImage != null && _itemData != null && _itemData.Icon != null)
        {
            _iconImage.sprite = _itemData.Icon;
        }

        SetSelected(false);

        _button.onClick.AddListener(() => _owner.SelectItem(this));
    }

    public void SetSelected(bool selected)
    {
        if (_selectedBorder != null)
        {
            _selectedBorder.SetActive(selected);
        }
    }
}