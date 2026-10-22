using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "FishCatalog", menuName = "Data/Fish Catalog")]
public class FishCatalog : ScriptableObject
{
    [SerializeField] private List<FishData> _fishes = new List<FishData>();

    private Dictionary<string, FishData> _cache;

    private void OnEnable()
    {
        BuildCache();
    }

    private void BuildCache()
    {
        _cache = new Dictionary<string, FishData>();
        foreach (var f in _fishes)
        {
            if (f != null && !string.IsNullOrEmpty(f.FishId))
                _cache[f.FishId] = f;
        }
    }

    public FishData GetById(string fishId)
    {
        if (_cache == null) BuildCache();
        return _cache.TryGetValue(fishId, out var d) ? d : null;
    }

    public List<FishData> GetAll() => _fishes;

    public int IndexOf(FishData fish) => _fishes.IndexOf(fish);

    public FishData GetByIndex(int index)
    {
        if (index < 0 || index >= _fishes.Count) return null;
        return _fishes[index];
    }

    public FishData GetRandomFish()
    {
        return GetRandomFish(null);
    }

    public FishData GetRandomFish(BaitData bait)
    {
        if (_fishes == null || _fishes.Count == 0)
            return null;

        if (bait == null)
            return GetRandomFish();

        float totalPercent = bait.TotalRarityPercent;
        if (totalPercent <= 0f)
            return GetRandomFish();

        float roll = Random.Range(0f, totalPercent);
        float cumulative = 0f;
        FishRarity pickedRarity = FishRarity.Common;
        bool found = false;

        foreach (FishRarity rarity in RarityOrder)
        {
            cumulative += bait.GetRarityPercent(rarity);
            if (roll <= cumulative)
            {
                pickedRarity = rarity;
                found = true;
                break;
            }
        }

        if (!found)
            pickedRarity = RarityOrder[RarityOrder.Length - 1];

        FishData picked = GetRandomFishOfRarity(pickedRarity);

        return picked != null ? picked : GetRandomFish();
    }

    private static readonly FishRarity[] RarityOrder =
    {
        FishRarity.Common,
        FishRarity.Rare,
        FishRarity.SuperRare,
        FishRarity.Legendary
    };

    private FishData GetRandomFishOfRarity(FishRarity rarity)
    {
        float totalWeight = 0f;
        foreach (var f in _fishes)
            if (f != null && f.Rarity == rarity)
                totalWeight += Mathf.Max(0f, f.SpawnWeight);

        if (totalWeight <= 0f)
            return null;

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var f in _fishes)
        {
            if (f == null || f.Rarity != rarity) continue;
            cumulative += Mathf.Max(0f, f.SpawnWeight);
            if (roll <= cumulative)
                return f;
        }

        return null;
    }
}