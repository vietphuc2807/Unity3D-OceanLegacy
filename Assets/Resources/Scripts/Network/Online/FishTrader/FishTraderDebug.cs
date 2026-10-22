using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DEBUG: nạp nhanh mỗi ô (ô 2 → 7) 1 loài cá KHÁC NHAU vào túi đồ để
/// mở panel Fish Trader và chỉnh icon / kiểm tra giá.
///
/// Gắn lên 1 GameObject bất kỳ trong scene gameplay.
///  - F9  : xoá cá cũ rồi nạp 6 loài tiếp theo (bấm lại để xem 6 loài kế).
///  - F10 : xoá hết cá ở ô 2 → 7.
///
/// Danh sách cá để trống → tự quét toàn bộ ItemData là cá trong project
/// (chỉ hoạt động trong Editor). Muốn chọn tay thì kéo ItemData vào mảng.
/// Chỉ nhận phím trong Editor / Development Build.
/// </summary>
public class FishTraderDebug : MonoBehaviour
{
    public enum SizeMode { Min, Middle, Max, Random }

    [Header("Cá test (để trống = tự quét trong Editor)")]
    [SerializeField] private ItemData[] _fishItems;

    [Header("Kích thước cá")]
    [SerializeField] private SizeMode _sizeMode = SizeMode.Random;

    [Header("Phím tắt")]
    [SerializeField] private KeyCode _fillKey = KeyCode.F9;
    [SerializeField] private KeyCode _clearKey = KeyCode.F10;

    private List<ItemData> _autoList;
    private int _cursor;

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (LobbyChatUI.IsTyping)
            return;

        if (Input.GetKeyDown(_fillKey))
            FillSlots();

        if (Input.GetKeyDown(_clearKey))
            ClearSlots();
#endif
    }

    // ============================================================
    // FILL
    // ============================================================

    [ContextMenu("Fill Slots")]
    public void FillSlots()
    {
        PlayerInventory inv = ResolveInventory();

        if (inv == null)
            return;

        List<ItemData> source = GetSource();

        if (source.Count == 0)
        {
            Debug.LogWarning("[FishTraderDebug] Không có ItemData cá nào để nạp.");
            return;
        }

        RemoveAllFish(inv);

        // Chọn ô 0 (cần câu) trước để cá đầu tiên rơi đúng vào ô 2,
        // các con sau nối tiếp theo thứ tự → khớp thứ tự 6 ô trong panel.
        inv.SelectSlot(0);

        int slotCount = inv.Slots.Length - 1;
        int count = Mathf.Min(slotCount, source.Count);

        List<string> log = new List<string>();

        for (int i = 0; i < count; i++)
        {
            ItemData item = source[(_cursor + i) % source.Count];

            float size = PickSize(item.FishInfo);

            if (inv.AddFishAndEquip(item, size))
                log.Add($"{item.DisplayName} (size {size:0.00})");
        }

        // Trang kế tiếp: lần bấm sau sẽ ra loài khác.
        _cursor = (_cursor + count) % source.Count;

        // Trả tay về cần câu cho gọn.
        inv.SelectSlot(0);

        Debug.Log("[FishTraderDebug] Đã nạp: " + string.Join(", ", log));
    }

    [ContextMenu("Clear Fish Slots")]
    public void ClearSlots()
    {
        PlayerInventory inv = ResolveInventory();

        if (inv == null)
            return;

        RemoveAllFish(inv);

        inv.SelectSlot(0);
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private static void RemoveAllFish(PlayerInventory inv)
    {
        List<int> indices = new List<int>();

        for (int i = 1; i < inv.Slots.Length; i++)
            indices.Add(i);

        inv.RemoveFishSlots(indices);
    }

    private float PickSize(FishData fish)
    {
        switch (_sizeMode)
        {
            case SizeMode.Min:
                return fish.MinSizeMultiplier;

            case SizeMode.Middle:
                return (fish.MinSizeMultiplier + fish.MaxSizeMultiplier) * 0.5f;

            case SizeMode.Max:
                return fish.MaxSizeMultiplier;

            default:
                return fish.GetRandomSizeMultiplier();
        }
    }

    private List<ItemData> GetSource()
    {
        List<ItemData> result = new List<ItemData>();

        if (_fishItems != null && _fishItems.Length > 0)
        {
            foreach (ItemData item in _fishItems)
            {
                if (item != null && item.IsFish && item.FishInfo != null)
                    result.Add(item);
            }

            return result;
        }

#if UNITY_EDITOR
        if (_autoList == null)
        {
            _autoList = new List<ItemData>();

            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData");

            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                ItemData item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path);

                if (item != null && item.IsFish && item.FishInfo != null)
                    _autoList.Add(item);
            }

            _autoList.Sort((a, b) =>
                string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        }

        result.AddRange(_autoList);
#endif

        return result;
    }

    private PlayerInventory ResolveInventory()
    {
        GameplayPlayer player = GameplayPlayer.LocalInstance;

        if (player == null)
        {
            Debug.LogWarning("[FishTraderDebug] Chưa có player local (chưa vào game?).");
            return null;
        }

        PlayerInventory inv = player.GetComponentInChildren<PlayerInventory>(true);

        if (inv == null)
            inv = player.GetComponentInParent<PlayerInventory>();

        if (inv == null)
            Debug.LogWarning("[FishTraderDebug] Không tìm thấy PlayerInventory.");

        return inv;
    }
}