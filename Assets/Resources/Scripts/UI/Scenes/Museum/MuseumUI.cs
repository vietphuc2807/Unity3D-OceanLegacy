using System.Collections;
using TMPro;
using UnityEngine;

public class MuseumUI : MonoBehaviour
{
    public static MuseumUI Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private TMP_Text _museumText;
    [SerializeField] private PanelEffect _panelHostGame;
    [SerializeField] private PanelEffect _panelFindGame;

    public string PlayerDisplayName { get; private set; } = "Player";

    private FirebaseAuthService _authService;

    private void Awake()
    {
        Instance = this;
    }

    private IEnumerator Start()
    {
        _authService = new FirebaseAuthService();

        _museumText.text = "PLAYER'S MUSEUM";

        yield return new WaitUntil(() =>
            FirebaseManager.Instance != null &&
            FirebaseManager.Instance.IsReady);

        var loadTask = _authService.GetCurrentPlayer();
        yield return new WaitUntil(() => loadTask.IsCompleted);

        PlayerData player = loadTask.Result;

        if (player != null)
        {
            PlayerDisplayName = !string.IsNullOrWhiteSpace(player.Nickname)
                ? player.Nickname
                : player.Name;

            _museumText.text = $"{PlayerDisplayName.ToUpper()}'S MUSEUM";
        }
        else
        {
            PlayerDisplayName = "Player";
            _museumText.text = "PLAYER'S MUSEUM";
        }
    }

    public void OpenPanelHostGame()
    {
        _panelHostGame.Show();
    }

    public void ClosePanelHostGame()
    {
        _panelHostGame.Hide();
    }

    public void OpenPanelFindGame()
    {
        _panelFindGame.Show();
        FindGameUI.Instance?.OpenPanel();
    }

    public void ClosePanelFindGame()
    {
        _panelFindGame.Hide();
        FindGameUI.Instance?.ClosePanel();
    }
}
