using UnityEngine;
using UnityEngine.UI;

public class FishingCastUI : MonoBehaviour
{
    public static FishingCastUI Instance { get; private set; }

    [SerializeField] private GameObject _root;
    [SerializeField] private Image _fillImage;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (_root != null)
            _root.SetActive(false);
    }

    public void Show()
    {
        if (_root != null)
            _root.SetActive(true);

        SetFill(0f);
    }

    public void Hide()
    {
        if (_root != null)
            _root.SetActive(false);
    }

    public void SetFill(float value)
    {
        if (_fillImage != null)
            _fillImage.fillAmount = Mathf.Clamp01(value);
    }
}