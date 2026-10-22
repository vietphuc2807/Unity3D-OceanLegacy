using UnityEngine;

public class LobbyUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private PanelEffect _panelEffect;

    private void Start()
    {
        _panelEffect.Show();
    }

    public void BackToMuseum()
    {
        _panelEffect.Hide(() =>
        {
            FusionRoomManager.Instance.LeaveRoom("S-Museum");
        });
    }
}