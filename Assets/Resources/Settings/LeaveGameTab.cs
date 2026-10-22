using UnityEngine;
using UnityEngine.UI;

public class LeaveGameTab : MonoBehaviour
{
    [SerializeField] private Button _quitButton;

    private void Awake()
    {
        _quitButton.onClick.AddListener(Quit);
    }

    private void Quit()
    {
        // Neu can luu/don dep gi truoc khi thoat (SignOut, Shutdown...) thi lam o day.

#if UNITY_EDITOR
        // Application.Quit() khong lam gi trong Editor -> tu dung Play mode.
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}