using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text _accountNameText;

    [Header("Scene")]
    [SerializeField] private string _loginScene = "S-Authentication";

    [Header("Panel")]
    [SerializeField] private PanelEffect _panelLogoutEffect;
    [SerializeField] private PanelEffect _panelCreditEffect;

    [Header("Exit Confirm")]
    [SerializeField] private GameObject _exitConfirmGroup; 

    // Nhac nen cho scene Menu do SceneMusicPlayer (gan rieng len 1
    // object trong scene nay) phu trach - xem SceneMusicPlayer.cs. Tach
    // rieng ra khoi MenuUI de scene nao cung dung CHUNG 1 kieu setup
    // (1 SceneMusicPlayer + 1 ten clip), khong phai tu viet lai logic
    // PlayMusic() trong moi script UI cua tung scene.

    private FirebaseAuthService _authService;

    private async void Start()
    {
        _authService = new FirebaseAuthService();

        _accountNameText.text = "";

        _exitConfirmGroup.SetActive(false);

        PlayerData player = await _authService.GetCurrentPlayer();

        if (player != null)
        {
            string displayName = !string.IsNullOrWhiteSpace(player.Nickname)
                ? player.Nickname
                : player.Name;

            _accountNameText.text = displayName;
        }
        else
        {
            _accountNameText.text = "Người chơi";
        }
    }

    public void Logout()
    {
        FirebaseManager.Instance.Auth.SignOut();

        _panelLogoutEffect.Hide(() =>
        {
            SceneManager.LoadScene(_loginScene);
        });
    }

    public void HidePanelLogout()
    {
        _panelLogoutEffect.Hide();
    }

    public void HidePanelCredit()
    {
        _panelCreditEffect.Hide();
    }

    public void OpenLoadingMuseumScene()
    {
        SceneManager.LoadScene("S-LoadingMuseum");
    }

    // Bấm nút "Exit" trong menu chính
    public void OpenExitConfirm()
    {
        _exitConfirmGroup.SetActive(true);
    }

    // Bấm "NO"
    public void CancelExit()
    {
        _exitConfirmGroup.SetActive(false);
    }

    // Bấm "YES"
    public void ConfirmExit()
    {
        Application.Quit();
    }
}