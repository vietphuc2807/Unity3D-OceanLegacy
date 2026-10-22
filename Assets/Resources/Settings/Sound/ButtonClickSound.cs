using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonClickSound : MonoBehaviour
{
    [Tooltip("Ten clip trong SoundManager > Sfx Clips (de trong = khong phat tieng).")]
    [SerializeField] private string _clickSfxName = "ButtonClick";

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(PlayClick);
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(PlayClick);
    }

    private void PlayClick()
    {
        if (string.IsNullOrEmpty(_clickSfxName))
            return;

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(_clickSfxName);
    }
}