using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

public class LobbyPlayerSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkPrefabRef _lobbyPlayerPrefab;
    [SerializeField] private Transform[] _spawnPoints;

    private Dictionary<PlayerRef, int> _playerSpawnIndex = new();
    private Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new();

    private NetworkRunner _runner;

    private void Start()
    {
        _runner = FindFirstObjectByType<NetworkRunner>();

        if (_runner != null)
        {
            _runner.AddCallbacks(this);
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer)
            return;

        int index = _playerSpawnIndex.Count;

        if (index >= _spawnPoints.Length)
            index = _spawnPoints.Length - 1;

        _playerSpawnIndex[player] = index;

        Transform spawnPoint = _spawnPoints[index];

        Vector3 pos = spawnPoint.position;
        Quaternion rot = spawnPoint.rotation;

        var obj = runner.Spawn(
            _lobbyPlayerPrefab,
            pos,
            rot,
            player);

        if (obj != null)
        {
            _spawnedPlayers[player] = obj;

            var lobbyPlayer = obj.GetComponent<LobbyPlayer>();

            bool isHost = player == runner.LocalPlayer;
            lobbyPlayer.SetHost(isHost);

            var outfit = obj.GetComponent<PlayerOutfit>();
            string storedOutfit = FusionRoomManager.Instance != null
                ? FusionRoomManager.Instance.GetPlayerOutfit(player)
                : "";

            outfit?.ApplyStoredOutfit(string.IsNullOrEmpty(storedOutfit) ? "none" : storedOutfit);
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        _playerSpawnIndex.Remove(player);

        if (!runner.IsServer)
            return;

        if (_spawnedPlayers.TryGetValue(player, out var obj) && obj != null)
        {
            runner.Despawn(obj);
            Debug.Log("Xoa");
        }

        _spawnedPlayers.Remove(player);
    }

    public void OnInput(NetworkRunner runner, NetworkInput input) { }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }

    public void OnConnectedToServer(NetworkRunner runner) { }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    public void OnSceneLoadDone(NetworkRunner runner) { }

    public void OnSceneLoadStart(NetworkRunner runner) { }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}