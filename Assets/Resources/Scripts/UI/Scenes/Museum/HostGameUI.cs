using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HostGameUI : MonoBehaviour
{
    [Header("Map Buttons")]
    [SerializeField] private MapButtonUI[] _mapButtons;

    [Header("Map Info")]
    [SerializeField] private TMP_Text _mapNameText;
    [SerializeField] private TMP_Text _mapDescriptionText;

    [Header("Privacy")]
    [SerializeField] private Button _publicButton;
    [SerializeField] private Button _privateButton;

    [Header("Create")]
    [SerializeField] private Button _createLobbyButton;

    [Header("Panel Effect")] 
    [SerializeField] private PanelEffect _hostGamePanel;

    [Header("Panels")] 
    [SerializeField] private GameObject _serverPanel;

    private MapButtonUI _selectedMap;
    private bool _isPrivate;

    private void Start()
    {
        foreach (var button in _mapButtons)
        {
            button.Initialize(this);
        }

        if (_mapButtons.Length > 0)
        {
            SelectMap(_mapButtons[0]);
        }

        SetPrivacy(false);
    }

    public void SelectMap(MapButtonUI button)
    {
        if (_selectedMap != null)
        {
            _selectedMap.SetSelected(false);
        }

        _selectedMap = button;
        _selectedMap.SetSelected(true);

        var data = button.MapData;

        _mapNameText.text = data.DisplayName;
        _mapDescriptionText.text = data.Description;
    }

    public void SetPublic()
    {
        SetPrivacy(false);
    }

    public void SetPrivate()
    {
        SetPrivacy(true);
    }

    private void SetPrivacy(bool isPrivate)
    {
        _isPrivate = isPrivate;

        _publicButton.interactable = isPrivate;
        _privateButton.interactable = !isPrivate;
    }

    public void CreateLobby()
    {
        if (_selectedMap == null)
            return;

        if (!_createLobbyButton.interactable)
            return;

        _createLobbyButton.interactable = false;

        string displayName = MuseumUI.Instance != null
            ? MuseumUI.Instance.PlayerDisplayName
            : "Player";

        RoomSessionData.PlayerDisplayName = displayName;

        string roomId = Random.Range(1000000, 9999999).ToString();

        var map = _selectedMap.MapData;

        RoomCreateData roomData = new RoomCreateData
        {
            RoomName = displayName + "'s Room",
            RoomId = roomId,
            HostName = displayName,

            MapId = map.MapId,
            MapName = map.DisplayName,
            SceneName = map.SceneName,

            IsPrivate = _isPrivate
        };

        RoomSessionData.IsPrivate = roomData.IsPrivate;

        _hostGamePanel.Hide(async () =>
        {
            // Hide luôn panel Server
            if (_serverPanel != null)
            {
                _serverPanel.SetActive(false);
            }

            bool success =
                await FusionRoomManager.Instance.CreateRoom(roomData);

            _createLobbyButton.interactable = true;

            if (!success)
            {
                // Hiện lại cả 2 panel nếu lỗi
                if (_serverPanel != null)
                {
                    _serverPanel.SetActive(true);
                }

                _hostGamePanel.Show();
            }
        });
    }
}