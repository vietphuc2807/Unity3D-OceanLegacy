using UnityEngine;

[CreateAssetMenu(fileName = "MapData", menuName = "Data/Map")]
public class MapData : ScriptableObject
{
    [Header("Info")]
    public string MapId;
    public string DisplayName;

    [TextArea(4, 6)]
    public string Description;

    [Header("Scene")]
    public string SceneName;

    [Header("UI")]
    public Sprite Thumbnail;

    [Header("Unlock")]
    public bool IsUnlocked = true;
}