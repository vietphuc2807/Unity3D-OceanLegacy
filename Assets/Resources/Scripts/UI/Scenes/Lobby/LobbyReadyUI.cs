using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyReadyUI : MonoBehaviour
{
    [Header("Client - Ready Button")]
    [SerializeField] private GameObject _readyButtonObject;
    [SerializeField] private Button _readyButton;
    [SerializeField] private TMP_Text _readyButtonText;

    [Header("Host - Start Game Button")]
    [SerializeField] private GameObject _startGameButtonObject;
    [SerializeField] private Button _startGameButton;
    [SerializeField] private TMP_Text _startGameButtonText;

    private void OnEnable()
    {
        LobbyPlayer.OnRosterOrReadyChanged += Refresh;
    }

    private void OnDisable()
    {
        LobbyPlayer.OnRosterOrReadyChanged -= Refresh;
    }

    private void Start()
    {
        if (_readyButton != null)
            _readyButton.onClick.AddListener(OnReadyClicked);

        if (_startGameButton != null)
            _startGameButton.onClick.AddListener(OnStartGameClicked);

        Refresh();
    }

    private void OnReadyClicked()
    {
        LobbyPlayer.LocalInstance?.ToggleReady();
    }

    private void OnStartGameClicked()
    {
        if (_startGameButton != null)
        {
            _startGameButton.interactable = false;
        }

        FusionRoomManager.Instance?.StartGameplay();
    }

    private void Refresh()
    {
        var local = LobbyPlayer.LocalInstance;

        if (local == null)
        {
            if (_readyButtonObject != null)
                _readyButtonObject.SetActive(false);

            if (_startGameButtonObject != null)
                _startGameButtonObject.SetActive(false);

            return;
        }

        bool isHost = local.IsHost;

        if (_readyButtonObject != null)
            _readyButtonObject.SetActive(!isHost);

        if (_startGameButtonObject != null)
            _startGameButtonObject.SetActive(isHost);

        if (!isHost && _readyButtonText != null)
        {
            _readyButtonText.text = local.IsReady ? "CANCEL READY" : "READY";
        }

        if (isHost && _startGameButton != null)
        {
            if (_startGameButtonText != null)
                _startGameButtonText.text = "START GAME";

            bool allReady = true;

            foreach (var player in LobbyPlayer.AllPlayers)
            {
                if (player == null || player.IsHost)
                    continue;

                if (!player.IsReady)
                {
                    allReady = false;
                    break;
                }
            }

            _startGameButton.interactable = allReady;
        }
    }
}