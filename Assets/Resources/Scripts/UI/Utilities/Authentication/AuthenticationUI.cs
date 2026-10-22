using TMPro;
using UnityEngine;

[System.Serializable]
public class PanelValidatorGroup
{
    public InputValidator[] validators;
}

public class AuthenticationUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject[] _panels;

    [Header("Panel Validators")]
    [SerializeField] private PanelValidatorGroup[] _panelValidators;

    [Header("Input Fields")]
    [SerializeField] private TMP_InputField[] _inputFields;

    [Header("Password Toggles")]
    [SerializeField] private PasswordToggle[] _passwordToggles;

    private int _currentPanelIndex;

    private void Start()
    {
        ShowPanel(0);
    }

    public void ShowPanel(int index, bool clearInput = true)
    {
        if (index < 0 || index >= _panels.Length)
            return;

        if (clearInput)
            ClearInputs(index);

        for (int i = 0; i < _panels.Length; i++)
            _panels[i].SetActive(i == index);

        foreach (PasswordToggle toggle in _passwordToggles)
            toggle.ResetPassword();

        _currentPanelIndex = index;
    }

    public void NextPanel(int index)
    {
        if (!ValidateCurrentPanel())
            return;

        ShowPanel(index, false);
    }

    public void BackPanel(int index)
    {
        ShowPanel(index, true);
    }

    public void BackPanelKeepInput(int index)
    {
        ShowPanel(index, false);
    }

    public bool ValidateCurrentPanel()
    {
        if (_currentPanelIndex >= _panelValidators.Length)
            return true;

        foreach (InputValidator validator in _panelValidators[_currentPanelIndex].validators)
        {
            if (!validator.IsValid())
                return false;
        }

        return true;
    }

    private void ClearInputs(int panelIndex)
    {
        foreach (TMP_InputField input in _inputFields)
        {
            if (input.transform.IsChildOf(_panels[panelIndex].transform))
            {
                input.text = "";
                input.ForceLabelUpdate();
            }
        }
    }
}