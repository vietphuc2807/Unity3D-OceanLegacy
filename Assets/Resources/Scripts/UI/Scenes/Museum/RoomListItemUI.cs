using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomListItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _hostText;
    [SerializeField] private TMP_Text _mapText;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private GameObject _selectedBorder;
    [SerializeField] private Button _button;

    public string SessionName { get; private set; }

    private FindGameUI _owner;

    public void Setup(SessionInfo session, FindGameUI owner)
    {
        _owner = owner;
        SessionName = session.Name;

        string hostName = "Unknown";
        if (session.Properties.TryGetValue("hostname", out var hostProp))
        {
            hostName = hostProp.PropertyValue.ToString();
        }

        string mapName = "Unknown Map";
        if (session.Properties.TryGetValue("mapname", out var mapProp))
        {
            mapName = mapProp.PropertyValue.ToString();
        }

        _hostText.text = hostName;
        _mapText.text = mapName;
        _statusText.text = $"{session.PlayerCount}/{session.MaxPlayers}";

        SetSelected(false);

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _owner.SelectRoom(this));
    }

    public void SetSelected(bool selected)
    {
        if (_selectedBorder != null)
        {
            _selectedBorder.SetActive(selected);
        }
    }
}