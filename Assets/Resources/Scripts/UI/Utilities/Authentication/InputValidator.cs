using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

public class InputValidator : MonoBehaviour
{
    public enum InputType
    {
        Email,
        Password,
        ConfirmPassword,
        Name,
        Nickname
    }

    [Header("Input")]
    [SerializeField] private TMP_InputField _input;
    [SerializeField] private InputType _type;

    [Header("Confirm Password")]
    [SerializeField] private TMP_InputField _passwordInput;

    [Header("Color")]
    [SerializeField] private Color _errorColor = Color.red;

    private Color _defaultColor;
    private bool _isValid;

    private void Awake()
    {
        _defaultColor = _input.textComponent.color;

        _input.onValueChanged.AddListener(_ => Validate());
        _input.onDeselect.AddListener(_ => Validate());
    }

    public bool IsValid()
    {
        Validate();
        return _isValid;
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(_input.text))
        {
            _isValid = false;
            _input.textComponent.color = _defaultColor;
            return;
        }

        bool valid = _type switch
        {
            InputType.Email => IsValidEmail(_input.text),
            InputType.Password => IsValidPassword(_input.text),
            InputType.ConfirmPassword => _input.text == _passwordInput.text,
            InputType.Name => IsValidName(_input.text),
            InputType.Nickname => IsValidNickname(_input.text),
            _ => true
        };

        SetState(valid);
    }

    private void SetState(bool valid)
    {
        _isValid = valid;
        _input.textComponent.color = valid ? _defaultColor : _errorColor;
    }

    private bool IsValidEmail(string email)
    {
        return Regex.IsMatch(email,
            @"^[A-Za-z0-9._%+-]+@gmail\.com$",
            RegexOptions.IgnoreCase);
    }

    private bool IsValidPassword(string password)
    {
        return Regex.IsMatch(password, @"^.{8,}$");
    }

    private bool IsValidName(string name)
    {
        return Regex.IsMatch(name.Trim(), @"^[\p{L}\s]+$");
    }

    private bool IsValidNickname(string nickname)
    {
        return Regex.IsMatch(nickname, @"^(?=.*[A-Za-z])[A-Za-z0-9]{6,}$");
    }
}