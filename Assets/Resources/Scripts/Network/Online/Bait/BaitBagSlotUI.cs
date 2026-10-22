using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))] 
public class BaitBagSlotUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image _icon;
    [SerializeField] private GameObject _emptyOverlay;   
    [SerializeField] private GameObject _selectionBorder; 

    private string _baitId;
    private bool _hasItem;

    public string BaitId => _baitId;
    public bool HasItem => _hasItem;

    public event Action<BaitBagSlotUI> OnClicked;

    public void SetBait(Sprite icon, int count, string baitId)
    {
        _baitId = baitId;
        _hasItem = count > 0;

        if (_icon != null)
        {
            _icon.sprite = icon;
            _icon.enabled = _hasItem;
        }

        if (_emptyOverlay != null)
            _emptyOverlay.SetActive(!_hasItem);

        SetSelected(false);
    }

    public void SetEmpty()
    {
        _baitId = null;
        _hasItem = false;

        if (_icon != null) _icon.enabled = false;
        if (_emptyOverlay != null) _emptyOverlay.SetActive(true);

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (_selectionBorder != null)
            _selectionBorder.SetActive(selected && _hasItem);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_hasItem) return;
        OnClicked?.Invoke(this);
    }

    public string GetBaitId() => _baitId;
}