using UnityEngine;

public class EmotePanelUI : MonoBehaviour
{
    [SerializeField] private GameObject _smileyButtonObject;
    [SerializeField] private GameObject _emotePanelObject;
    [SerializeField] private EmoteButtonUI[] _emoteButtons;

    private void Start()
    {
        foreach (var button in _emoteButtons)
        {
            button.Initialize(this);
        }

        ClosePanel();
    }

    public void OpenPanel()
    {
        if (_emotePanelObject != null) _emotePanelObject.SetActive(true);
        if (_smileyButtonObject != null) _smileyButtonObject.SetActive(false);
    }

    public void ClosePanel()
    {
        if (_emotePanelObject != null) _emotePanelObject.SetActive(false);
        if (_smileyButtonObject != null) _smileyButtonObject.SetActive(true);
    }
}