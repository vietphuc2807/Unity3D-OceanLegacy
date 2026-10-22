using System.Collections;
using TMPro;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class LobbyRoomInfo : MonoBehaviour
{
    [Header("Room Info")]
    [SerializeField] private TMP_Text _roomNameText;
    [SerializeField] private TMP_Text _roomIdText;

    [Header("Room Id Visibility")]
    [SerializeField] private Button _eyeButton;
    [SerializeField] private Image _eyeIcon;
    [SerializeField] private Sprite _eyeOpenSprite;
    [SerializeField] private Sprite _eyeClosedSprite;

    [Header("Copy")]
    [SerializeField] private Button _copyButton;
    [SerializeField] private GameObject _copyMessagePanel;
    [SerializeField] private float _copyMessageDuration = 1.5f;

    private const string MaskedId = "*******";

    private NetworkRunner _runner;
    private string _roomId;
    private bool _isVisible;
    private Coroutine _copyMessageCoroutine;

    private void Start()
    {
        _runner = FindFirstObjectByType<NetworkRunner>();

        _eyeButton.onClick.AddListener(ToggleVisibility);
        _copyButton.onClick.AddListener(CopyRoomId);

        if (_copyMessagePanel != null)
        {
            _copyMessagePanel.SetActive(false);
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_runner == null || _runner.SessionInfo == null)
            return;

        if (_roomNameText != null &&
            _runner.SessionInfo.Properties.TryGetValue("roomname", out var roomName))
        {
            _roomNameText.text = roomName.PropertyValue.ToString().ToUpper();
        }

        if (_runner.SessionInfo.Properties.TryGetValue("roomid", out var roomId))
        {
            _roomId = roomId.PropertyValue.ToString();
        }

        SetVisibility(!RoomSessionData.IsPrivate);
    }

    private void ToggleVisibility()
    {
        SetVisibility(!_isVisible);
    }

    private void SetVisibility(bool isVisible)
    {
        _isVisible = isVisible;

        if (_roomIdText != null)
        {
            _roomIdText.text = _isVisible ? _roomId : MaskedId;
        }

        if (_eyeIcon != null)
        {
            _eyeIcon.sprite = _isVisible ? _eyeOpenSprite : _eyeClosedSprite;
        }
    }

    private void CopyRoomId()
    {
        if (string.IsNullOrEmpty(_roomId))
            return;

        GUIUtility.systemCopyBuffer = _roomId;

        ShowCopyMessage();
    }

    private void ShowCopyMessage()
    {
        if (_copyMessagePanel == null)
            return;

        if (_copyMessageCoroutine != null)
        {
            StopCoroutine(_copyMessageCoroutine);
        }

        _copyMessageCoroutine = StartCoroutine(CopyMessageRoutine());
    }

    private IEnumerator CopyMessageRoutine()
    {
        _copyMessagePanel.SetActive(true);

        yield return new WaitForSeconds(_copyMessageDuration);

        _copyMessagePanel.SetActive(false);

        _copyMessageCoroutine = null;
    }
}