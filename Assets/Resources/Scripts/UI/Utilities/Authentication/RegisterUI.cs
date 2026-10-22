using Firebase.Auth;
using Firebase;
using TMPro;
using UnityEngine;

public class RegisterUI : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private TMP_InputField _emailInput;
    [SerializeField] private TMP_InputField _passwordInput;
    [SerializeField] private TMP_InputField _firstNameInput;
    [SerializeField] private TMP_InputField _lastNameInput;
    [SerializeField] private TMP_InputField _nicknameInput;

    [Header("UI")]
    [SerializeField] private AuthenticationUI _authenticationUI;
    [SerializeField] private GameObject _emailAlreadyExists;

    [SerializeField] private int _verifyPanelIndex = 3;

    private FirebaseAuthService _authService;

    private void Awake()
    {
        _authService = new FirebaseAuthService();

        _emailAlreadyExists.SetActive(false);
    }

    public async void Register()
    {
        _emailAlreadyExists.SetActive(false);

        if (!_authenticationUI.ValidateCurrentPanel())
            return;

        string email = _emailInput.text.Trim();
        string password = _passwordInput.text;
        string fullName = $"{_firstNameInput.text.Trim()} {_lastNameInput.text.Trim()}";
        string nickname = _nicknameInput.text.Trim();

        FirebaseUser currentUser = FirebaseManager.Instance.Auth.CurrentUser;

        if (currentUser != null)
        {
            try
            {
                await currentUser.ReloadAsync();

                currentUser = FirebaseManager.Instance.Auth.CurrentUser;

                if (currentUser != null)
                {
                    if (currentUser.Email == email && !currentUser.IsEmailVerified)
                    {
                        _authenticationUI.ShowPanel(_verifyPanelIndex, false);
                        return;
                    }

                    _authService.Logout();
                }
            }
            catch
            {
                _authService.Logout();
            }
        }

        try
        {
            await _authService.Register(
                email,
                password,
                fullName,
                nickname);

            Debug.Log("Register Success");

            _authenticationUI.ShowPanel(_verifyPanelIndex, false);
        }
        catch (FirebaseException e)
        {
            if (e.ErrorCode == (int)AuthError.EmailAlreadyInUse)
            {
                _emailAlreadyExists.SetActive(true);
                return;
            }

            Debug.LogError(e);
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
        }
    }

    public void OnEmailChanged(string _)
    {
        _emailAlreadyExists.SetActive(false);
    }
}