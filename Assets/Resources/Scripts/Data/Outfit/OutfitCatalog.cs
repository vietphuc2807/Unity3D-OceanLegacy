using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "OutfitCatalog", menuName = "Data/Outfit Catalog")]
public class OutfitCatalog : ScriptableObject
{
    [SerializeField] private List<OutfitItemData> _items;

    public IReadOnlyList<OutfitItemData> Items => _items;

    public OutfitItemData GetById(string itemId)
    {
        foreach (var item in _items)
        {
            if (item.ItemId == itemId)
                return item;
        }

        return null;
    }
}