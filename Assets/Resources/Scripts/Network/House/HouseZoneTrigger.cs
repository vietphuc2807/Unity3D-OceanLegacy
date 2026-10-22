using System.Collections.Generic;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HouseZoneTrigger : MonoBehaviour
{
    [Header("Self-Correction (fix mất OnTriggerExit)")]

    [Tooltip("Layer của player. Để 'Everything' nếu không chắc, vẫn " +
             "hoạt động đúng nhưng quét rộng hơn cần thiết.")]
    [SerializeField]
    private LayerMask _playerLayerMask = ~0;

    [Tooltip("Bao lâu quét lại 1 lần để tự sửa sai (giây, real-time). " +
             "Không cần quá nhanh — chỉ là lưới an toàn.")]
    [SerializeField]
    private float _reVerifyIntervalSeconds = 0.5f;

    private NetworkRunner _runner;

    private Collider _col;

    private readonly HashSet<PlayerRef> _confirmedInside = new();

    private float _reVerifyTimer;

    private void Awake()
    {
        var col = GetComponent<Collider>();

        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning("[HouseZoneTrigger] Collider chưa bật " +
                              "isTrigger — tự động bật.");

            col.isTrigger = true;
        }

        _col = col;
    }

    private void Start()
    {
        _runner = FindFirstObjectByType<NetworkRunner>();

        if (_runner == null)
        {
            Debug.LogError("[HouseZoneTrigger] Không tìm thấy NetworkRunner!");
        }
    }


    private void OnTriggerEnter(Collider other)
    {
        if (_runner == null || !_runner.IsServer)
            return;

        PlayerRef player = GetPlayerRef(other);

        if (player == PlayerRef.None)
            return;

        Debug.Log($"[HouseZoneTrigger] Player {player} bước vào nhà.");

        _confirmedInside.Add(player);

        GameDayManager.Instance?.ReportPlayerEnteredHouse(player);
    }

    private void OnTriggerExit(Collider other)
    {
        if (_runner == null || !_runner.IsServer)
            return;

        PlayerRef player = GetPlayerRef(other);

        if (player == PlayerRef.None)
            return;

        Debug.Log($"[HouseZoneTrigger] Player {player} rời khỏi nhà.");

        _confirmedInside.Remove(player);

        GameDayManager.Instance?.ReportPlayerExitedHouse(player);
    }


    private void Update()
    {
        if (_runner == null || !_runner.IsServer)
            return;

        _reVerifyTimer += Time.deltaTime;

        if (_reVerifyTimer < _reVerifyIntervalSeconds)
            return;

        _reVerifyTimer = 0f;

        ReVerifyPlayersInside();
    }

    private void ReVerifyPlayersInside()
    {
        if (_col == null)
            return;

        Bounds bounds = _col.bounds;

        Collider[] hits =
            Physics.OverlapBox(
                bounds.center,
                bounds.extents,
                Quaternion.identity,
                _playerLayerMask,
                QueryTriggerInteraction.Collide
            );

        var currentlyOverlapping = new HashSet<PlayerRef>();

        foreach (var hit in hits)
        {
            PlayerRef player = GetPlayerRef(hit);

            if (player == PlayerRef.None)
                continue;

            currentlyOverlapping.Add(player);
        }

        foreach (var player in currentlyOverlapping)
        {
            if (_confirmedInside.Add(player))
            {
                Debug.Log(
                    $"[HouseZoneTrigger] (Re-verify) Phát hiện " +
                    $"{player} đang ở trong nhà nhưng chưa được ghi " +
                    $"nhận — tự sửa."
                );

                GameDayManager.Instance?.ReportPlayerEnteredHouse(player);
            }
        }


        List<PlayerRef> toRemove = null;

        foreach (var player in _confirmedInside)
        {
            if (!currentlyOverlapping.Contains(player))
            {
                toRemove ??= new List<PlayerRef>();
                toRemove.Add(player);
            }
        }

        if (toRemove != null)
        {
            foreach (var player in toRemove)
            {
                _confirmedInside.Remove(player);

                Debug.Log(
                    $"[HouseZoneTrigger] (Re-verify) Phát hiện " +
                    $"{player} đã rời nhà nhưng chưa được ghi nhận " +
                    $"(OnTriggerExit bị rớt) — tự sửa."
                );

                GameDayManager.Instance?.ReportPlayerExitedHouse(player);
            }
        }
    }

    private PlayerRef GetPlayerRef(Collider other)
    {
        var netObj = other.GetComponentInParent<NetworkObject>();

        if (netObj == null)
            return PlayerRef.None;

        return netObj.InputAuthority;
    }
}