using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BaitCatalog", menuName = "Data/Bait Catalog")]
public class BaitCatalog : ScriptableObject
{
    [SerializeField] private List<BaitData> _baits = new List<BaitData>();

    private Dictionary<string, BaitData> _cache;

    private void OnEnable()
    {
        BuildCache();
    }

    private void BuildCache()
    {
        _cache = new Dictionary<string, BaitData>();
        foreach (var b in _baits)
        {
            if (b != null && !string.IsNullOrEmpty(b.BaitId))
                _cache[b.BaitId] = b;
        }
    }

    public BaitData GetById(string baitId)
    {
        if (_cache == null) BuildCache();
        return _cache.TryGetValue(baitId, out var d) ? d : null;
    }

    public List<BaitData> GetAll() => _baits;
}