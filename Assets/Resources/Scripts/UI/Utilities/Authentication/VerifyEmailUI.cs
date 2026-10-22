using System.Collections;
using Firebase.Auth;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class VerifyEmailUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button _continueButton;
    [SerializeField] private AuthenticationUI _authenticationUI;
    [SerializeField] private PanelEffect _panelEffect;

    [Header("Scene")]
    [SerializeField] private string _menuScene = "Menu";

    private FirebaseAuthService _authService;
    private FirebaseUser _user;

    private void Awake()
    {
        _authService = new FirebaseAuthService();
    }

    private void OnEnable()
    {
        _continueButton.interactable = false;
        StartCoroutine(CheckEmail());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private IEnumerator CheckEmail()
    {
        while (true)
        {
            _user = FirebaseManager.Instance.Auth.CurrentUser;

            if (_user == null)
                yield break;

            var reloadTask = _user.ReloadAsync();

            yield return new WaitUntil(() => reloadTask.IsCompleted);

            _user = FirebaseManager.Instance.Auth.CurrentUser;

            if (_user == null)
                yield break;

            if (_user.IsEmailVerified)
            {
                var verifyTask = _authService.VerifyPlayer();

                yield return new WaitUntil(() => verifyTask.IsCompleted);

                _continueButton.interactable = true;
                yield break;
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    public void Continue()
    {
        if (!_continueButton.interactable)
            return;

        _panelEffect.Hide(() =>
        {
            SceneManager.LoadScene(_menuScene);
        });
    }

    public async void ResendEmail()
    {
        _user = FirebaseManager.Instance.Auth.CurrentUser;

        if (_user == null)
            return;

        await _user.SendEmailVerificationAsync();
    }
}