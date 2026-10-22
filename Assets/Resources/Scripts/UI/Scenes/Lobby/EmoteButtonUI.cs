using UnityEngine;
using UnityEngine.UI;

public class EmoteButtonUI : MonoBehaviour
{
    [SerializeField] private EmoteItemData _itemData;
    [SerializeField] private Button _button;

    private EmotePanelUI _owner;

    public void Initialize(EmotePanelUI owner)
    {
        _owner = owner;
        _button.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (_itemData == null)
            return;

        LobbyPlayer.LocalInstance?.RequestPlayEmote(_itemData.EmoteId);
        _owner.ClosePanel();
    }
}