using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class FusionRoomManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static FusionRoomManager Instance { get; private set; }

    [SerializeField] private string _museumScene = "S-Museum";
    [SerializeField] private NetworkRunner _runnerPrefab;
    [SerializeField] private float _sceneTransitionGraceSeconds = 5f;

    private NetworkRunner _runner;
    private bool _isHandlingHostLeft;
    private bool _hasJoinedRoom;
    private bool _isLeavingIntentionally;
    private bool _isSceneTransitioning;
    private float _sceneTransitionGraceUntil;

    // ============================================================
    // FIX: cờ đánh dấu lần LoadScene SẮP TỚI là bước Lobby -> Gameplay
    // (do StartGameplay() gọi), KHÔNG phải lần load S-Lobby đầu tiên lúc
    // CreateRoom/JoinRoom. OnSceneLoadStart dùng cờ này để quyết định có
    // Show() GameplayLoadingOverlay hay không — tránh bug: overlay hiện
    // ngay khi vừa Host Game / Join Room, trước khi vào tới Lobby.
    // ============================================================
    private bool _isLobbyToGameplayTransition;

    public bool HasJoinedRoom => _hasJoinedRoom;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private NetworkRunner CreateFreshRunner()
    {
        if (_runner != null)
        {
            _runner.RemoveCallbacks(this);
            Destroy(_runner.gameObject);
        }

        _runner = Instantiate(_runnerPrefab);
        _runner.AddCallbacks(this);

        _isHandlingHostLeft = false;
        _isLeavingIntentionally = false;
        _isSceneTransitioning = false;
        _sceneTransitionGraceUntil = 0f;
        _isLobbyToGameplayTransition = false;

        _playerOutfits.Clear();

        return _runner;
    }

    public async Task<bool> CreateRoom(RoomCreateData data)
    {
        var runner = CreateFreshRunner();

        var properties = new Dictionary<string, SessionProperty>
        {
            { "map", data.MapId },
            { "mapname", data.MapName },
            { "hostname", data.HostName },
            { "private", data.IsPrivate ? 1 : 0 },
            { "roomid", data.RoomId },
            { "roomname", data.RoomName }
        };

        StartGameResult result = await runner.StartGame(
            new StartGameArgs
            {
                GameMode = GameMode.Host,
                SessionName = data.RoomId,
                Scene = SceneRef.FromIndex(
                    SceneUtility.GetBuildIndexByScenePath(
                        "Assets/Scenes/S-Lobby.unity")),
                IsVisible = !data.IsPrivate,
                IsOpen = true,
                PlayerCount = 4,
                SessionProperties = properties,
                // FIX: unique UserId tránh Photon kick khi test bằng ParrelSync
                AuthValues = CreateUniqueAuthValues()
            });

        if (!result.Ok)
        {
            Debug.LogWarning(result.ShutdownReason);
            return false;
        }

        _hasJoinedRoom = true;
        RoomSessionData.RoomName = data.RoomName;
        RoomSessionData.RoomId = data.RoomId;
        RoomSessionData.SelectedMapScene = data.SceneName;
        RoomSessionData.SelectedMapName = data.MapId;
        RoomSessionData.IsPrivate = data.IsPrivate;

        return true;
    }

    public async Task<bool> JoinRoom(string roomId)
    {
        var runner = CreateFreshRunner();

        StartGameResult result = await runner.StartGame(
            new StartGameArgs
            {
                GameMode = GameMode.Client,
                SessionName = roomId,
                AuthValues = CreateUniqueAuthValues()
                // Không có Scene ở đây
            });

        if (!result.Ok)
        {
            Debug.LogWarning(result.ShutdownReason);
            return false;
        }

        _hasJoinedRoom = true;

        RoomSessionData.RoomId = roomId;

        if (runner.SessionInfo.Properties.TryGetValue("private", out var isPrivateProp))
        {
            RoomSessionData.IsPrivate = isPrivateProp.PropertyValue.ToString() == "1";
        }

        return true;
    }

    // Đổi async void -> async Task, để nơi gọi await được, biết chắc
// khi nào Shutdown() + LoadScene() đã thực sự xong.
    public async Task LeaveRoom(string sceneToLoad)
    {
        _isLeavingIntentionally = true;
        _hasJoinedRoom = false;
        _isLobbyToGameplayTransition = false;

        GameplayLoadingOverlay.Instance?.ForceHideImmediate();

        if (_runner != null)
        {
            await _runner.Shutdown();
        }

        PlayerCursor.IsUIOpen = false;

        SceneManager.LoadScene(sceneToLoad);
    }

    public async Task<bool> JoinBrowsingLobby()
    {
        if (_hasJoinedRoom)
        {
            Debug.LogWarning("[FusionRoomManager] JoinBrowsingLobby bị gọi khi đang trong room!");
            return false;
        }

        var runner = CreateFreshRunner();

        // Browsing lobby không cần AuthValues riêng
        var result = await runner.JoinSessionLobby(SessionLobby.ClientServer);

        if (!result.Ok)
        {
            Debug.LogWarning(result.ShutdownReason);
            return false;
        }

        return true;
    }

    public async void LeaveBrowsingLobby()
    {
        // FIX: không shutdown nếu đang trong room
        if (_hasJoinedRoom)
        {
            Debug.LogWarning("[FusionRoomManager] LeaveBrowsingLobby bị gọi khi đang trong room, bỏ qua.");
            return;
        }

        if (_runner != null)
        {
            await _runner.Shutdown();
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[FusionRoomManager] OnPlayerJoined player={player} " +
                $"isServer={runner.IsServer} time={Time.time:F2}");
    }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log("left");
    }

    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (shutdownReason == ShutdownReason.Ok) return;
        if (_isLeavingIntentionally || IsInSceneTransitionOrGrace() || runner.IsServer || !_hasJoinedRoom) return;

        HandleHostLeft();
    }

    public void OnConnectedToServer(NetworkRunner runner) { }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.LogError($"[FusionRoomManager] DISCONNECTED reason={reason} " +
                    $"hasJoinedRoom={_hasJoinedRoom} time={Time.time:F2}");

        if (_isLeavingIntentionally || runner.IsServer || !_hasJoinedRoom)
            return;

        if (reason == NetDisconnectReason.Requested)
            return;

        // FIX: nếu đang trong grace period, đừng bỏ qua âm thầm — chỉ hoãn xử lý,
        // không được huỷ xử lý.
        if (IsInSceneTransitionOrGrace())
        {
            Debug.LogWarning("[FusionRoomManager] Disconnected trong lúc grace, " +
                            "nhưng vẫn ghi nhận để xử lý sau nếu không phục hồi.");
            StartCoroutine(HandleDisconnectAfterGrace());
            return;
        }

        HandleHostLeft();
    }

    private IEnumerator HandleDisconnectAfterGrace()
    {
        // Chờ hết grace, nếu vẫn chưa reconnect / vẫn mất kết nối thì mới xử lý.
        yield return new WaitUntil(() => !IsInSceneTransitionOrGrace());

        if (_runner == null || !_runner.IsRunning)
        {
            HandleHostLeft();
        }
    }

    private bool IsInSceneTransitionOrGrace()
    {
        return _isSceneTransitioning || Time.time < _sceneTransitionGraceUntil;
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        FindGameUI.Instance?.UpdateRoomList(sessionList);
    }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        _isSceneTransitioning = true;

        // FIX: set grace ngay từ lúc START, không chờ đến DONE
        // Tránh disconnect trong khoảng giữa LoadStart và LoadDone
        _sceneTransitionGraceUntil = Time.time + _sceneTransitionGraceSeconds;

        LobbyPlayer.IsTransitioningToGameplay = true;

        // FIX: CHỈ Show() overlay khi đây thực sự là bước Lobby -> Gameplay
        // (do StartGameplay() set cờ _isLobbyToGameplayTransition = true
        // trước khi gọi LoadScene). OnSceneLoadStart cũng bắn ra cho lần
        // load S-Lobby đầu tiên lúc CreateRoom/JoinRoom — trước đây Show()
        // bị gọi luôn ở bước đó, khiến overlay hiện ra dù người chơi chưa
        // hề bấm Start Game, và vì overlay chỉ ẩn khi GameDayManager báo
        // OnRunStarted (chỉ xảy ra sau khi Start Game), nó bị kẹt _isVisible
        // = true mãi nếu người chơi bấm Back thoát Lobby giữa chừng.
        if (_isLobbyToGameplayTransition)
        {
            GameplayLoadingOverlay.Instance?.Show();
        }

        Debug.Log($"[FusionRoomManager] OnSceneLoadStart. IsServer={runner.IsServer}, Time={Time.time:F2}");

        if (LobbyChatUI.Instance != null)
        {
            Debug.LogWarning("[FusionRoomManager] LobbyChatUI vẫn còn sống lúc chuyển scene -> destroy.");
            Destroy(LobbyChatUI.Instance.gameObject);
        }
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        _isSceneTransitioning = false;
        // Grace tiếp tục từ lúc DONE thêm N giây nữa
        _sceneTransitionGraceUntil = Time.time + _sceneTransitionGraceSeconds;

        // FIX: đã dùng xong cờ cho lần load này, reset lại để không ảnh
        // hưởng tới lần load scene tiếp theo.
        _isLobbyToGameplayTransition = false;

        // FIX QUAN TRỌNG: Client cũng phải tự hiện Waiting Overlay.
        // _isLobbyToGameplayTransition là bool LOCAL và chỉ Host được set
        // trong StartGameplay(), vì vậy Client không thể dựa vào nó.
        //
        // Khi OnSceneLoadDone chạy, scene hiện tại đã là Gameplay trên
        // từng máy. Nếu scene không còn là S-Lobby thì đây chính là bước
        // Lobby -> Gameplay và máy đó phải hiện overlay.
        int lobbyBuildIndex = SceneUtility.GetBuildIndexByScenePath(
            "Assets/Scenes/S-Lobby.unity");

        if (_hasJoinedRoom &&
            SceneManager.GetActiveScene().buildIndex != lobbyBuildIndex)
        {
            GameplayLoadingOverlay.Instance?.Show();
        }

        // KHÔNG Hide() overlay ở đây. Overlay sẽ tự ẩn khi
        // GameDayManager broadcast RPC_NotifyRunStart() tới ALL.
        Debug.Log($"[FusionRoomManager] OnSceneLoadDone. IsServer={runner.IsServer}, " +
                  $"Scene={SceneManager.GetActiveScene().name}, Time={Time.time:F2}");
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    private async void HandleHostLeft()
    {
        if (_isHandlingHostLeft)
            return;

        _isHandlingHostLeft = true;
        _hasJoinedRoom = false;
        _isLobbyToGameplayTransition = false;

        // FIX: reset cứng overlay khi bị host kick / mất kết nối, cùng lý
        // do như trong LeaveRoom() — tránh state kẹt lại lần chơi sau.
        GameplayLoadingOverlay.Instance?.ForceHideImmediate();

        HostLeftNotice.ShouldShow = true;

        if (_runner != null)
        {
            await _runner.Shutdown();
        }

        PlayerCursor.IsUIOpen = false;

        SceneManager.LoadScene(_museumScene);
    }

    private readonly Dictionary<PlayerRef, string> _playerOutfits = new();

    public void SetPlayerOutfit(PlayerRef player, string itemId)
    {
        _playerOutfits[player] = itemId;
    }

    public string GetPlayerOutfit(PlayerRef player)
    {
        return _playerOutfits.TryGetValue(player, out var id) ? id : "";
    }

    public void StartGameplay()
    {
        if (_runner == null || !_runner.IsServer)
            return;

        // Chốt số người chơi NGAY LÚC rời Lobby — an toàn hơn đếm lại ở
        // scene Gameplay vì không phụ thuộc thứ tự load scene của client.
        RoomSessionData.PlayerCountAtStart = Mathf.Max(1, LobbyPlayer.AllPlayers.Count);

        // FIX: đánh dấu đây LÀ bước Lobby -> Gameplay TRƯỚC khi LoadScene,
        // để OnSceneLoadStart (chạy ngay sau LoadScene) biết cần Show()
        // GameplayLoadingOverlay. Lưu ý: StartGameplay() chỉ chạy trên
        // Host/Server (return sớm ở trên nếu không phải server), nên cờ
        // này chỉ có tác dụng phía Host. Nếu Client cũng cần overlay hiện
        // đúng lúc này, cần đồng bộ tín hiệu qua RPC/Networked property
        // thay vì dựa vào cờ local như hiện tại.
        _isLobbyToGameplayTransition = true;

        var sceneRef = SceneRef.FromIndex(
            SceneUtility.GetBuildIndexByScenePath(
                $"Assets/Scenes/{RoomSessionData.SelectedMapScene}.unity"));

        _runner.LoadScene(sceneRef, LoadSceneMode.Single);
    }

    private static Fusion.Photon.Realtime.AuthenticationValues CreateUniqueAuthValues()
    {
        return new Fusion.Photon.Realtime.AuthenticationValues
        {
            UserId = SystemInfo.deviceUniqueIdentifier + "_" +
                    System.Guid.NewGuid().ToString("N").Substring(0, 8)
        };
    }
}