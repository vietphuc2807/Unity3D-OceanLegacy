using UnityEngine;

public class OutfitUI : MonoBehaviour
{
    [SerializeField] private OutfitButtonUI[] _buttons;
    [SerializeField] private OutfitButtonUI _defaultButton; 

    private OutfitButtonUI _selectedButton;

    private void Start()
    {
        foreach (var button in _buttons)
        {
            button.Initialize(this);
        }

        if (_defaultButton != null)
        {
            _selectedButton = _defaultButton;
            _selectedButton.SetSelected(true);
        }
    }

    public void SelectItem(OutfitButtonUI button)
    {
        if (_selectedButton != null)
        {
            _selectedButton.SetSelected(false);
        }

        _selectedButton = button;
        _selectedButton.SetSelected(true);

        var localPlayer = LobbyPlayer.LocalInstance;

        if (localPlayer == null)
            return;

        var outfit = localPlayer.GetComponent<PlayerOutfit>();

        if (outfit == null)
            return;

        string itemId = button.ItemData != null ? button.ItemData.ItemId : "";
        outfit.RequestEquip(itemId);
    }
}