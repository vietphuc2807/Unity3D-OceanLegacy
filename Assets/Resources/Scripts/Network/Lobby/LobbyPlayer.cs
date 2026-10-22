using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;

public class LobbyPlayer : NetworkBehaviour, IChatSender
{
    public static LobbyPlayer LocalInstance { get; private set; }

    public static event Action OnRosterOrReadyChanged;

    private static readonly HashSet<LobbyPlayer> _allPlayers = new();
    public static IReadOnlyCollection<LobbyPlayer> AllPlayers => _allPlayers;

    [Networked]
    private NetworkString<_32> PlayerName { get; set; }

    [Networked]
    public NetworkBool IsHost { get; set; }

    [Networked, OnChangedRender(nameof(OnReadyChanged))]
    public NetworkBool IsReady { get; private set; }

    [Networked, OnChangedRender(nameof(OnEmoteChanged))]
    private NetworkString<_32> CurrentEmoteId { get; set; }

    [Networked]
    private float EmoteStartTime { get; set; }

    [Header("UI")]
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private GameObject _crownIcon;
    [SerializeField] private TMP_Text _readyTag;

    [Header("Idle Animation Sync")]
    [SerializeField] private Animator _idleAnimator;
    [SerializeField] private AnimationClip _idleClip;
    [SerializeField] private float _emoteToIdleBlendDuration = 0.25f;

    [Header("Emote")]
    [SerializeField] private EmoteCatalog _emoteCatalog;

    private static readonly int IdleHash = Animator.StringToHash("Idle_2");

    private bool _isPlayingEmote;
    private Coroutine _emoteClearCoroutine;
    private float _blendUntilTime = -1f;

    public override void Spawned()
    {
        IsTransitioningToGameplay = false;

        if (Object.HasInputAuthority)
        {
            LocalInstance = this;

            // Runner.IsServer đúng ngay lập tức tại đây, không cần chờ đồng
            // bộ mạng như IsHost (networked property, được set SAU thời
            // điểm này trong LobbyPlayerSpawner.OnPlayerJoined) -> dùng cờ
            // này để biết chính máy hiện tại có phải Host/Server (người TẠO
            // phòng) hay không, nhằm bỏ qua thông báo "đã vào lobby" cho
            // chính người tạo phòng (họ tạo phòng chứ không phải "vào").
            bool isHostJoining = Runner.IsServer;

            RPC_SetName(RoomSessionData.PlayerDisplayName, isHostJoining);
            LobbyChatUI.Instance?.SetSender(this);
        }

        _allPlayers.Add(this);
        UpdateName();
        UpdateCrown();
        UpdateReadyTag();
        OnRosterOrReadyChanged?.Invoke();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (LocalInstance == this)
            LocalInstance = null;

        _allPlayers.Remove(this);

        // FIX: chỉ invoke nếu runner còn hoạt động, tránh invoke lúc scene transition
        if (runner != null && runner.IsRunning)
        {
            OnRosterOrReadyChanged?.Invoke();

            // Bỏ qua thông báo "left" nếu đây là do chuyển sang scene gameplay
            // (mọi LobbyPlayer đều bị despawn khi unload lobby scene), không
            // phải người chơi thật sự rời phòng.
            if (!IsTransitioningToGameplay)
                LobbyChatUI.Instance?.AddPlayerLeftMessage(PlayerName.ToString());
        }
    }

    // Set = true NGAY TRƯỚC khi gọi lệnh load scene gameplay (ở chỗ bạn đang
    // xử lý nút Start/chuyển scene, ví dụ trong LobbyManager hoặc tương tự).
    // Không cần set lại về false — object này sẽ bị destroy cùng lobby scene.
    public static bool IsTransitioningToGameplay = false;

    public override void Render()
    {
        UpdateCrown();
    }

    private void Update()
    {
        UpdateName();

        if (_nameText != null && Camera.main != null)
        {
            _nameText.transform.forward = Camera.main.transform.forward;
        }

        if (_crownIcon != null && _crownIcon.activeSelf && Camera.main != null)
        {
            _crownIcon.transform.forward = Camera.main.transform.forward;
        }

        if (_readyTag != null && _readyTag.gameObject.activeSelf && Camera.main != null)
        {
            _readyTag.transform.forward = Camera.main.transform.forward;
        }
    }

    private void LateUpdate()
    {
        if (_isPlayingEmote)
            return;

        // Đang trong lúc blend mượt từ emote về idle -> không ép Play() cứng
        if (Time.time < _blendUntilTime)
            return;

        SyncIdleAnimation();
    }

    private void SyncIdleAnimation()
    {
        if (_idleAnimator == null || _idleClip == null || Runner == null)
            return;

        double time = Runner.SimulationTime;
        float normalized = (float)((time % _idleClip.length) / _idleClip.length);

        _idleAnimator.Play(IdleHash, 0, normalized);
        _idleAnimator.Update(0f);
    }

    private void UpdateName()
    {
        if (_nameText != null)
        {
            _nameText.text = PlayerName.ToString();
        }
    }

    private void UpdateCrown()
    {
        if (_crownIcon != null)
        {
            _crownIcon.SetActive(IsHost);
        }
    }

    private void UpdateReadyTag()
    {
        if (_readyTag == null)
            return;

        if (IsHost)
        {
            _readyTag.gameObject.SetActive(false);
            return;
        }

        _readyTag.gameObject.SetActive(true);

        if (IsReady)
        {
            _readyTag.text = "READY";
            _readyTag.color = new Color32(54, 255, 67, 255);
        }
        else
        {
            _readyTag.text = "NOT READY";
            _readyTag.color = new Color32(255, 74, 74, 255);
        }
    }

    private void OnReadyChanged()
    {
        UpdateReadyTag();
        OnRosterOrReadyChanged?.Invoke();
    }

    public void SetHost(bool isHost)
    {
        IsHost = isHost;
        UpdateReadyTag();

        OnRosterOrReadyChanged?.Invoke();
    }

    public void ToggleReady()
    {
        if (!Object.HasInputAuthority)
            return;

        if (IsHost)
            return;

        RPC_SetReady(!IsReady);
    }

    // ===== Emote =====

    public void RequestPlayEmote(string emoteId)
    {
        if (!Object.HasInputAuthority)
            return;

        if (_emoteCatalog == null)
            return;

        var data = _emoteCatalog.GetById(emoteId);

        if (data == null)
            return;

        RPC_SetEmote(emoteId);

        if (_emoteClearCoroutine != null)
        {
            StopCoroutine(_emoteClearCoroutine);
        }

        float duration = data.Loop
            ? (data.PlayDuration > 0f ? data.PlayDuration : 2f)
            : (data.Clip != null ? data.Clip.length : 2f);

        _emoteClearCoroutine = StartCoroutine(ClearEmoteAfterDelay(duration));
    }

    private IEnumerator ClearEmoteAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        RPC_SetEmote("");
        _emoteClearCoroutine = null;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetEmote(string emoteId)
    {
        CurrentEmoteId = emoteId;
        EmoteStartTime = (float)Runner.SimulationTime;
    }

    private void OnEmoteChanged()
    {
        string id = CurrentEmoteId.ToString();

        if (string.IsNullOrEmpty(id))
        {
            _isPlayingEmote = false;

            if (_idleAnimator != null && Runner != null && _idleClip != null)
            {
                double time = Runner.SimulationTime;
                float normalized = (float)((time % _idleClip.length) / _idleClip.length);

                _idleAnimator.CrossFade(IdleHash, _emoteToIdleBlendDuration, 0, normalized);
            }

            _blendUntilTime = Time.time + _emoteToIdleBlendDuration;
            return;
        }

        if (_emoteCatalog == null || _idleAnimator == null)
            return;

        var data = _emoteCatalog.GetById(id);

        if (data == null)
            return;

        int hash = Animator.StringToHash(data.AnimatorStateName);

        _idleAnimator.CrossFade(hash, 0.15f, 0, 0f);
        _isPlayingEmote = true;
    }

    public void SendChatMessage(string message)
    {
        if (!Object.HasInputAuthority)
            return;

        if (string.IsNullOrWhiteSpace(message))
            return;

        RPC_SendChatMessage(message);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetName(string playerName, NetworkBool isHostJoining)
    {
        PlayerName = playerName;

        // Player vừa spawn xong và tên vừa được StateAuthority xác nhận
        // -> đúng thời điểm để loan báo "đã vào lobby" cho MỌI client,
        // vì lúc này tên chắc chắn đã đúng (không còn rỗng/placeholder).
        // Bỏ qua nếu đây chính là Host vừa tạo phòng (không phải "vào").
        if (!isHostJoining)
            RPC_BroadcastPlayerJoined(playerName);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastPlayerJoined(string playerName)
    {
        LobbyChatUI.Instance?.AddPlayerJoinedMessage(playerName);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetReady(bool ready)
    {
        IsReady = ready;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SendChatMessage(string message)
    {
        RPC_BroadcastChatMessage(PlayerName.ToString(), message);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastChatMessage(string playerName, string message)
    {
        LobbyChatUI.Instance?.AddMessage(playerName, message);
    }
}