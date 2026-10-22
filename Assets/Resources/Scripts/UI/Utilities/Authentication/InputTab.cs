using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class InputTab : MonoBehaviour
{
    [SerializeField] private TMP_InputField _nextInput;
    [SerializeField] private TMP_InputField _previousInput;

    private TMP_InputField _input;

    private void Awake()
    {
        _input = GetComponent<TMP_InputField>();
    }

    private void Update()
    {
        if (!_input.isFocused) return;

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            TMP_InputField target = shift ? _previousInput : _nextInput;

            if (target == null) return;

            EventSystem.current.SetSelectedGameObject(target.gameObject);
            target.ActivateInputField();
        }
    }
}