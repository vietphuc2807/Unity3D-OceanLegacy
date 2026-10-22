using UnityEngine;
using UnityEngine.UI;

public class HotbarSlotUI : MonoBehaviour
{
    [SerializeField] private Image _iconImage;
    [SerializeField] private GameObject _selectedBorder;

    private ItemData _currentItem;

    private Vector3 _lastPosition;
    private Vector3 _lastRotation;
    private Vector3 _lastScale;

    public void SetItem(ItemData item)
    {
        _currentItem = item;

        if (_iconImage == null)
            return;

        if (item != null && item.Icon != null)
        {
            _iconImage.sprite = item.Icon;
            _iconImage.enabled = true;

            ApplyIconTransform(true);
        }
        else
        {
            _iconImage.sprite = null;
            _iconImage.enabled = false;

            ResetIconTransform();
        }
    }

    private void Update()
    {
        if (_currentItem == null || _iconImage == null || !_iconImage.enabled)
            return;

        if (_lastPosition != _currentItem.IconPosition ||
            _lastRotation != _currentItem.IconRotation ||
            _lastScale != _currentItem.IconScale)
        {
            ApplyIconTransform(false);
        }
    }

    private void ApplyIconTransform(bool force)
    {
        if (_currentItem == null || _iconImage == null)
            return;

        RectTransform iconRect = _iconImage.rectTransform;

        Vector3 position = _currentItem.IconPosition;
        Vector3 rotation = _currentItem.IconRotation;
        Vector3 scale = _currentItem.IconScale;

        if (force || _lastPosition != position)
        {
            iconRect.anchoredPosition = new Vector2(
                position.x,
                position.y
            );

            _lastPosition = position;
        }

        if (force || _lastRotation != rotation)
        {
            iconRect.localRotation = Quaternion.Euler(rotation);

            _lastRotation = rotation;
        }

        if (force || _lastScale != scale)
        {
            iconRect.localScale = scale;

            _lastScale = scale;
        }
    }

    private void ResetIconTransform()
    {
        RectTransform iconRect = _iconImage.rectTransform;

        iconRect.anchoredPosition = Vector2.zero;
        iconRect.localRotation = Quaternion.identity;
        iconRect.localScale = Vector3.one;

        _lastPosition = Vector3.zero;
        _lastRotation = Vector3.zero;
        _lastScale = Vector3.one;
    }

    public void SetSelected(bool selected)
    {
        if (_selectedBorder != null)
        {
            _selectedBorder.SetActive(selected);
        }
    }
}