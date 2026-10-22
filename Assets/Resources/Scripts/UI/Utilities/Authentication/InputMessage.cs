using TMPro;
using UnityEngine;

public class InputMessage : MonoBehaviour
{
    [SerializeField] private TMP_InputField _inputField;
    [SerializeField] private GameObject _message;

    private void Start()
    {
        _message.SetActive(false);

        _inputField.onSelect.AddListener(OnSelect);
        _inputField.onDeselect.AddListener(OnDeselect);
    }

    private void OnSelect(string text)
    {
        _message.SetActive(true);
    }

    private void OnDeselect(string text)
    {
        _message.SetActive(false);
    }
}