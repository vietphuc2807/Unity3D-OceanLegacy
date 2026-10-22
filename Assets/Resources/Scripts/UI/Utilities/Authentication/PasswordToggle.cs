using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PasswordToggle : MonoBehaviour
{
    [Header("Passwords")]
    [SerializeField] private List<TMP_InputField> _passwordInputs = new();

    [Header("Icon")]
    [SerializeField] private Image _icon;
    [SerializeField] private Sprite _showIcon;
    [SerializeField] private Sprite _hideIcon;

    private bool _isVisible;

    private void Start()
    {
        foreach (TMP_InputField input in _passwordInputs)
        {
            input.asteriskChar = '•';
        }

        ResetPassword();
    }

    public void TogglePassword()
    {
        SetPasswordVisible(!_isVisible);
    }

    public void ResetPassword()
    {
        SetPasswordVisible(false);
    }

    private void SetPasswordVisible(bool visible)
    {
        _isVisible = visible;

        foreach (TMP_InputField input in _passwordInputs)
        {
            input.contentType = visible
                ? TMP_InputField.ContentType.Standard
                : TMP_InputField.ContentType.Password;

            input.ForceLabelUpdate();
        }

        _icon.sprite = visible ? _showIcon : _hideIcon;
    }
}