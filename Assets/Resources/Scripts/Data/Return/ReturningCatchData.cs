using System.Collections.Generic;

public static class ReturningCatchData
{
    public struct CaughtFishEntry
    {
        public ItemData Item;
        public float SizeMultiplier;

        public CaughtFishEntry(ItemData item, float sizeMultiplier)
        {
            Item = item;
            SizeMultiplier = sizeMultiplier;
        }
    }

    private static readonly List<CaughtFishEntry> _entries = new();

    public static IReadOnlyList<CaughtFishEntry> Entries => _entries;

    public static bool HasData => _entries.Count > 0;

    public static void CaptureFromInventory(PlayerInventory inventory)
    {
        _entries.Clear();

        if (inventory == null)
            return;

        ItemData[] slots = inventory.Slots;

        if (slots == null)
            return;

        for (int i = 1; i < slots.Length; i++)
        {
            ItemData item = slots[i];

            if (item == null || !item.IsFish)
                continue;

            float size = inventory.GetSlotFishSize(i);

            _entries.Add(new CaughtFishEntry(item, size));
        }
    }
    
    public static void Clear()
    {
        _entries.Clear();
    }
}