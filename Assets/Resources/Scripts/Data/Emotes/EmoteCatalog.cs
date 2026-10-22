using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EmoteCatalog", menuName = "Data/Emote Catalog")]
public class EmoteCatalog : ScriptableObject
{
    [SerializeField] private List<EmoteItemData> _items;

    public IReadOnlyList<EmoteItemData> Items => _items;

    public EmoteItemData GetById(string emoteId)
    {
        foreach (var item in _items)
        {
            if (item.EmoteId == emoteId)
                return item;
        }

        return null;
    }
}