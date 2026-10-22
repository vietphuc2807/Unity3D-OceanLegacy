using Fusion;
using UnityEngine;

public class LocalPlayerRegistrar : NetworkBehaviour
{
    [SerializeField] private Camera _playerCamera;

    public override void Spawned()
    {
        if (!Object.HasInputAuthority) return;

        BaitTraderNPC.RegisterLocalPlayer(transform, _playerCamera);
    }
}