using Fusion;
using Fusion.Sockets;
using UnityEngine;

public class NetworkInputProvider : MonoBehaviour, INetworkRunnerCallbacks
{
    private bool _jumpQueued;
    private float _yawAccum;

    private NetPlayerMovementFusion _localMovement;

    private void Start()
    {
        var runner = FindFirstObjectByType<NetworkRunner>();
        if (runner != null)
            runner.AddCallbacks(this);
    }

    private void Update()
    {
        if (LobbyChatUI.IsTyping) return;

        if (Input.GetKeyDown(KeyCode.Space))
            _jumpQueued = true;

        _yawAccum += Input.GetAxis("Mouse X");
    }

    private NetPlayerMovementFusion GetLocalMovement()
    {
        if (_localMovement != null)
            return _localMovement;

        var all = FindObjectsByType<NetPlayerMovementFusion>(FindObjectsSortMode.None);
        foreach (var m in all)
        {
            if (m.Object != null && m.Object.HasInputAuthority)
            {
                _localMovement = m;
                break;
            }
        }

        return _localMovement;
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        bool typing = LobbyChatUI.IsTyping;
        bool uiOpen = PlayerCursor.IsUIOpen;

        var movement = GetLocalMovement();
        bool lineInWater = movement != null && movement.IsInWaterFishingLocal;

        var data = new NetworkInputData
        {
            IsTyping    = typing,
            IsUIOpen    = uiOpen,
            MoveX       = (typing || uiOpen) ? 0f : Input.GetAxisRaw("Horizontal"),
            MoveZ       = (typing || uiOpen) ? 0f : Input.GetAxisRaw("Vertical"),
            RunHeld     = (typing || uiOpen) ? false : Input.GetKey(KeyCode.LeftShift),
            JumpPressed = (typing || uiOpen) ? false : _jumpQueued,
            YawDelta    = (typing || uiOpen) ? 0f : _yawAccum,
            LineInWater = lineInWater
        };

        input.Set(data);

        _jumpQueued = false;
        _yawAccum   = 0f;
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, System.Collections.Generic.List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, System.Collections.Generic.Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}