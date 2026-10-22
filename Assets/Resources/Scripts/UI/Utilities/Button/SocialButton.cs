using UnityEngine;

public class SocialButton : MonoBehaviour
{
    [SerializeField] private string _url;

    public void OpenLink()
    {
        Application.OpenURL(_url);
    }
}