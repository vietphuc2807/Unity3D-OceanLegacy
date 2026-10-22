using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public float MoveX;
    public float MoveZ;
    public NetworkBool RunHeld;
    public NetworkBool JumpPressed;
    public float YawDelta;
    public bool IsTyping;
    public bool IsUIOpen;

    public NetworkBool LineInWater;
}