using TMPro;
using UnityEngine;

public class InputFocus : MonoBehaviour
{
    [SerializeField] private TMP_InputField _firstInput;

    private void Start()
    {
        _firstInput.Select();
        _firstInput.ActivateInputField();
    }
}