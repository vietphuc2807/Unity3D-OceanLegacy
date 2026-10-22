using Firebase.Auth;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Text.RegularExpressions;

public class SignInUI : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private TMP_InputField _emailInput;
    [SerializeField] private TMP_InputField _passwordInput;

    [Header("UI")]
    [SerializeField] private GameObject _loginFailed;
    [SerializeField] private GameObject _emailNotVerified; 
    [SerializeField] private PanelEffect _panelEffect;

    [Header("Scene")]
    [SerializeField] private string _menuScene = "Menu";

    private FirebaseAuthService _authService;

    private void Awake()
    {
        _authService = new FirebaseAuthService();

        _loginFailed.SetActive(false);
        _emailNotVerified.SetActive(false);
    }

    public async void SignIn()
    {
        _loginFailed.SetActive(false);
        _emailNotVerified.SetActive(false);

        string email = _emailInput.text.Trim();
        string password = _passwordInput.text;

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            _loginFailed.SetActive(true);
            return;
        }

        if (!Regex.IsMatch(email, @"^[A-Za-z0-9._%+-]+@gmail\.com$"))
        {
            _loginFailed.SetActive(true);
            return;
        }

        try
        {
            await _authService.Login(email, password);

            _panelEffect.Hide(() =>
            {
                SceneManager.LoadScene(_menuScene);
            });
        }
        catch (EmailNotVerifiedException)
        {
            _emailNotVerified.SetActive(true);
        }
        catch (Firebase.FirebaseException)
        {
            _loginFailed.SetActive(true);
        }
    }

    public void OnInputChanged(string _)
    {
        _loginFailed.SetActive(false);
        _emailNotVerified.SetActive(false);
    }
}