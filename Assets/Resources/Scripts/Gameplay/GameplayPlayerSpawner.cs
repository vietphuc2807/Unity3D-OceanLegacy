using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using System.Linq;

public class GameplayPlayerSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    public static GameplayPlayerSpawner Instance { get; private set; }

    [SerializeField] private NetworkPrefabRef _gameplayPlayerPrefab;
    [SerializeField] private Transform[] _spawnPoints;

    [Header("Watchdog")]
    [SerializeField] private float _watchdogInterval = 0.5f;
    [SerializeField] private float _initialWatchdogDelay = 1.5f;

    private NetworkRunner _runner;
    private readonly Dictionary<PlayerRef, NetworkObject> _spawned = new();
    private int _nextSpawnIndex = 0;
    private Coroutine _watchdogCoroutine;
    private bool _callbacksRegistered;
    private bool _sceneFullyLoaded = false;

    private void Awake()
    {
        var allSpawners = FindObjectsByType<GameplayPlayerSpawner>(FindObjectsSortMode.None);
        if (allSpawners.Length > 1)
        {
            Debug.LogError($"[GameplayPlayerSpawner] Phát hiện {allSpawners.Length} GameplayPlayerSpawner cùng tồn tại trong scene!");
        }

        Instance = this;
        PlayerCursor.IsUIOpen = false;
        _runner = FindFirstObjectByType<NetworkRunner>();

        if (_runner != null)
        {
            _runner.AddCallbacks(this);
            _callbacksRegistered = true;
        }
        else
        {
            Debug.LogError("[GameplayPlayerSpawner] Không tìm thấy NetworkRunner!");
        }
    }

    private void Start()
    {
        // intentionally empty; waits for OnSceneLoadDone
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (_runner != null && _callbacksRegistered)
            _runner.RemoveCallbacks(this);

        if (_watchdogCoroutine != null)
            StopCoroutine(_watchdogCoroutine);
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log($"[GameplayPlayerSpawner] OnSceneLoadDone. IsServer={runner.IsServer}");

        if (!runner.IsServer)
            return;

        _sceneFullyLoaded = true;
        StartSpawnWatchdog();
    }

    private void StartSpawnWatchdog()
    {
        if (_watchdogCoroutine != null)
            StopCoroutine(_watchdogCoroutine);

        _watchdogCoroutine = StartCoroutine(SpawnWatchdogRoutine());
    }

    private IEnumerator SpawnWatchdogRoutine()
    {
        Debug.Log("[GameplayPlayerSpawner] Watchdog bắt đầu quét.");

        if (_initialWatchdogDelay > 0f)
            yield return new WaitForSeconds(_initialWatchdogDelay);

        while (true)
        {
            if (_runner == null || !_runner.IsRunning)
                yield break;

            foreach (var player in _runner.ActivePlayers)
            {
                if (_spawned.ContainsKey(player))
                    continue;

                Debug.LogWarning($"[GameplayPlayerSpawner] Watchdog phát hiện player {player} chưa spawn. Thử spawn...");
                SpawnPlayer(player);

                yield return null;
                yield return null;
            }

            yield return new WaitForSeconds(_watchdogInterval);
        }
    }

    private void SpawnPlayer(PlayerRef player)
    {
        if (_spawned.ContainsKey(player))
            return;

        if (_spawnPoints == null || _spawnPoints.Length == 0)
        {
            Debug.LogError("[GameplayPlayerSpawner] _spawnPoints chưa được gán!");
            return;
        }

        if (!_runner.ActivePlayers.Contains(player))
        {
            Debug.LogWarning($"[GameplayPlayerSpawner] {player} không còn active, bỏ qua spawn.");
            return;
        }

        int index = Mathf.Clamp(_nextSpawnIndex, 0, _spawnPoints.Length - 1);
        Transform sp = _spawnPoints[index];
        NetworkObject obj = null;

        try
        {
            obj = _runner.Spawn(
                _gameplayPlayerPrefab,
                sp.position,
                sp.rotation,
                player,
                onBeforeSpawned: (r, spawnedObj) =>
                {
                    var cc = spawnedObj.GetComponent<CharacterController>();
                    if (cc != null)
                    {
                        cc.enabled = false;
                        spawnedObj.transform.SetPositionAndRotation(sp.position, sp.rotation);
                        cc.enabled = true;
                    }
                });
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameplayPlayerSpawner] Exception khi spawn {player}: {e}");
            return;
        }

        if (obj == null)
        {
            Debug.LogWarning($"[GameplayPlayerSpawner] Spawn null cho {player}. Sẽ thử lại.");
            return;
        }

        _nextSpawnIndex++;
        _spawned[player] = obj;
        _runner.SetPlayerObject(player, obj);

        string outfitId = FusionRoomManager.Instance?.GetPlayerOutfit(player) ?? "";
        obj.GetComponent<PlayerOutfit>()?.ApplyStoredOutfit(outfitId);

        // QUAN TRỌNG:
        // Chỉ báo sau khi NetworkObject đã spawn thành công và đã được
        // SetPlayerObject. GameDayManager dùng PlayerRef để chống đếm trùng.
        GameDayManager.Instance?.ReportPlayerJoinedGame(player);

        Debug.Log($"[GameplayPlayerSpawner] Spawn thành công player={player}. " +
                  $"Đã báo GameDayManager.");
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer)
            return;

        if (_spawned.ContainsKey(player))
            return;

        if (!_sceneFullyLoaded)
        {
            Debug.Log($"[GameplayPlayerSpawner] OnPlayerJoined {player} nhưng scene chưa load xong, bỏ qua (watchdog sẽ lo).");
            return;
        }

        Debug.Log($"[GameplayPlayerSpawner] OnPlayerJoined: {player}");
        SpawnPlayer(player);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[GameplayPlayerSpawner] Player left: {player}");

        if (!runner.IsServer)
            return;

        if (_spawned.TryGetValue(player, out var obj) && obj != null)
            runner.Despawn(obj);

        _spawned.Remove(player);
    }

    // ============================================================
    // Boilerplate callbacks
    // ============================================================

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
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
}