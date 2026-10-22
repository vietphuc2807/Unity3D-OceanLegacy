using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemCatalog", menuName = "Data/Item Catalog")]
public class ItemCatalog : ScriptableObject
{
    [SerializeField] private List<ItemData> _items;

    public IReadOnlyList<ItemData> Items => _items;

    public ItemData GetById(string itemId)
    {
        foreach (var item in _items)
        {
            if (item.ItemId == itemId)
                return item;
        }

        return null;
    }
}