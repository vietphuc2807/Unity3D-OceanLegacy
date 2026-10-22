using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JoinGameUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField _roomCodeInput;
    [SerializeField] private Button _joinButton;
    [SerializeField] private PanelEffect _panelEffect;

    [Header("Error")]
    [SerializeField] private GameObject _errorMessageObject;
    [SerializeField] private float _errorMessageDuration = 1.5f;

    private TMP_Text _errorMessageText;
    private Coroutine _errorMessageCoroutine;

    private void Awake()
    {
        if (_errorMessageObject != null)
        {
            _errorMessageText = _errorMessageObject.GetComponent<TMP_Text>();
            _errorMessageObject.SetActive(false);
        }
    }

    public void JoinRoom()
    {
        string roomId = _roomCodeInput.text.Trim();

        if (roomId.Length != 7)
        {
            Debug.LogWarning("Room ID must be 7 digits");
            return;
        }

        string displayName = MuseumUI.Instance != null
            ? MuseumUI.Instance.PlayerDisplayName
            : "Player";

        RoomSessionData.PlayerDisplayName = displayName;

        _joinButton.interactable = false;

        _panelEffect.Hide(async () =>
        {
            bool success =
                await FusionRoomManager.Instance.JoinRoom(roomId);

            _joinButton.interactable = true;

            if (!success)
            {
                _panelEffect.Show();
                ShowError("Room not found.");
            }
        });
    }

    private void ShowError(string message)
    {
        if (_errorMessageObject == null)
            return;

        if (_errorMessageText != null)
        {
            _errorMessageText.text = message;
        }

        if (_errorMessageCoroutine != null)
        {
            StopCoroutine(_errorMessageCoroutine);
        }

        _errorMessageCoroutine = StartCoroutine(ErrorMessageRoutine());
    }

    private IEnumerator ErrorMessageRoutine()
    {
        _errorMessageObject.SetActive(true);

        yield return new WaitForSeconds(_errorMessageDuration);

        _errorMessageObject.SetActive(false);

        _errorMessageCoroutine = null;
    }
}