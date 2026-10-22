using UnityEngine;

public enum FishRarity
{
    Common,     // Thường
    Rare,       // Hiếm
    SuperRare,  // Siêu hiếm
    Legendary   // Huyền thoại
}

[CreateAssetMenu(
    fileName = "RarityColors",
    menuName = "Data/Rarity Colors"
)]
public class RarityColors : ScriptableObject
{
    [Header("Rarity Colors")]

    [SerializeField]
    private Color _common = new Color32(
        0xAA, 0xAA, 0xAA, 0xFF
    );

    [SerializeField]
    private Color _rare = new Color32(
        0x2D, 0xD4, 0xBF, 0xFF
    );

    [SerializeField]
    private Color _superRare = new Color32(
        0xA8, 0x55, 0xF7, 0xFF
    );

    [SerializeField]
    private Color _legendary = new Color32(
        0xFF, 0xD7, 0x00, 0xFF
    );


    public Color Get(FishRarity rarity)
    {
        switch (rarity)
        {
            case FishRarity.Common:
                return _common;

            case FishRarity.Rare:
                return _rare;

            case FishRarity.SuperRare:
                return _superRare;

            case FishRarity.Legendary:
                return _legendary;

            default:
                return Color.white;
        }
    }

    public string GetHex(FishRarity rarity)
    {
        return ColorUtility.ToHtmlStringRGB(
            Get(rarity)
        );
    }
}