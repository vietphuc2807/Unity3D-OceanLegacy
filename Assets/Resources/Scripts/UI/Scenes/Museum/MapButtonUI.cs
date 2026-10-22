using UnityEngine;
using UnityEngine.UI;

public class MapButtonUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private MapData _mapData;

    [Header("UI")]
    [SerializeField] private Image _buttonImage;
    [SerializeField] private GameObject _border;
    [SerializeField] private GameObject _lockIcon;

    private HostGameUI _hostGameUI;

    public MapData MapData => _mapData;

    public void Initialize(HostGameUI hostGameUI)
    {
        _hostGameUI = hostGameUI;

        if (_lockIcon != null)
        {
            _lockIcon.SetActive(!_mapData.IsUnlocked);
        }

        if (_border != null)
        {
            _border.SetActive(false);
        }

        if (_buttonImage != null)
        {
            _buttonImage.color = _mapData.IsUnlocked
                ? Color.white
                : new Color(1f, 1f, 1f, 0.45f);
        }
    }

    public void SetSelected(bool selected)
    {
        if (_border != null)
        {
            _border.SetActive(selected);
        }
    }

    public void OnClick()
    {
        if (!_mapData.IsUnlocked)
            return;

        if (_hostGameUI != null)
        {
            _hostGameUI.SelectMap(this);
        }
    }
}