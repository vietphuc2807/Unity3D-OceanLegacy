using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class FindGameUI : MonoBehaviour
{
    public static FindGameUI Instance { get; private set; }

    [Header("List")]
    [SerializeField] private Transform _listContent;
    [SerializeField] private GameObject _roomItemPrefab;

    [Header("Buttons")]
    [SerializeField] private Button _joinGameButton;
    [SerializeField] private Button _refreshButton;

    private readonly List<RoomListItemUI> _spawnedItems = new();
    private RoomListItemUI _selectedItem;

    private void Awake()
    {
        Instance = this;

        _joinGameButton.interactable = false;
    }

    public async void OpenPanel()
    {
        _selectedItem = null;
        _joinGameButton.interactable = false;

        ClearList();

        if (FusionRoomManager.Instance == null)
            return;

        await FusionRoomManager.Instance.JoinBrowsingLobby();
    }

    public void ClosePanel()
    {
        // FIX: chỉ leave browsing lobby nếu chưa join room
        if (FusionRoomManager.Instance != null && !FusionRoomManager.Instance.HasJoinedRoom)
        {
            FusionRoomManager.Instance.LeaveBrowsingLobby();
        }
        ClearList();
    }

    public void UpdateRoomList(List<SessionInfo> sessionList)
    {
        ClearList();

        _selectedItem = null;
        _joinGameButton.interactable = false;

        foreach (var session in sessionList)
        {
            if (!session.IsVisible || !session.IsOpen)
                continue;

            var obj = Instantiate(_roomItemPrefab, _listContent);
            var item = obj.GetComponent<RoomListItemUI>();

            item.Setup(session, this);

            _spawnedItems.Add(item);
        }
    }

    public void SelectRoom(RoomListItemUI item)
    {
        if (_selectedItem != null)
        {
            _selectedItem.SetSelected(false);
        }

        _selectedItem = item;
        _selectedItem.SetSelected(true);

        _joinGameButton.interactable = true;
    }

    public async void RefreshList()
    {
        _refreshButton.interactable = false;

        _selectedItem = null;
        _joinGameButton.interactable = false;

        await FusionRoomManager.Instance.JoinBrowsingLobby();

        _refreshButton.interactable = true;
    }

    public async void JoinSelectedRoom()
    {
        if (_selectedItem == null)
            return;

        _joinGameButton.interactable = false;

        string displayName = MuseumUI.Instance != null
            ? MuseumUI.Instance.PlayerDisplayName
            : "Player";

        RoomSessionData.PlayerDisplayName = displayName;

        bool success = await FusionRoomManager.Instance.JoinRoom(_selectedItem.SessionName);

        if (!success)
        {
            _joinGameButton.interactable = true;
        }
    }

    private void ClearList()
    {
        foreach (var item in _spawnedItems)
        {
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }

        _spawnedItems.Clear();
    }
}