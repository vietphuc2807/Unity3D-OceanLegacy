using UnityEngine;

[CreateAssetMenu(fileName = "BaitData", menuName = "Data/Bait Data")]
public class BaitData : ScriptableObject
{
    [Header("Info")]
    public string BaitId;
    public string BaitName;
    public Sprite Icon;
    public int PricePerUnit = 1;

    [Header("World Visual")]
    public GameObject WorldPrefab;
    public Vector3 WorldScale = Vector3.one;   
    public Vector3 WorldPositionOffset = Vector3.zero;   
    public Vector3 WorldRotationOffset = Vector3.zero;

    [Header("Tỉ lệ ra cá theo độ hiếm (%) — nên để tổng 4 số = 100")]
    [Range(0f, 100f)]
    [Tooltip("% lượt câu ra cá Common. Khuyến nghị không để 0 để mồi nào cũng còn khả năng ra cá thường.")]
    public float CommonPercent = 70f;

    [Range(0f, 100f)]
    [Tooltip("% lượt câu ra cá Rare.")]
    public float RarePercent = 20f;

    [Range(0f, 100f)]
    [Tooltip("% lượt câu ra cá SuperRare.")]
    public float SuperRarePercent = 8f;

    [Range(0f, 100f)]
    [Tooltip("% lượt câu ra cá Legendary.")]
    public float LegendaryPercent = 2f;

    [Header("Xu hướng cá to/nhỏ (%) — balancing cân nặng cá câu được")]
    [Range(0f, 100f)]
    [Tooltip("0% = kéo mạnh xác suất về phía cá NHỎ (Min Size Multiplier). " +
             "50% = random đều trong khoảng Min-Max của loài cá (giống hành vi gốc, trung tính). " +
             "100% = kéo mạnh xác suất về phía cá TO (Max Size Multiplier). " +
             "Dù ở đầu nào cũng vẫn còn (rất nhỏ) khả năng ra size ngược lại, không bao giờ bị khoá cứng. " +
             "Mồi rẻ nên để thấp (gần 0) để cá câu được nhỏ hơn, mồi xịn để cao (gần 100).")]
    public float SizeBiasPercent = 50f;

    public float TotalRarityPercent =>
        Mathf.Max(0f, CommonPercent) +
        Mathf.Max(0f, RarePercent) +
        Mathf.Max(0f, SuperRarePercent) +
        Mathf.Max(0f, LegendaryPercent);

    public float GetRarityPercent(FishRarity rarity)
    {
        switch (rarity)
        {
            case FishRarity.Common: return Mathf.Max(0f, CommonPercent);
            case FishRarity.Rare: return Mathf.Max(0f, RarePercent);
            case FishRarity.SuperRare: return Mathf.Max(0f, SuperRarePercent);
            case FishRarity.Legendary: return Mathf.Max(0f, LegendaryPercent);
            default: return 0f;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        float total = TotalRarityPercent;
        if (total > 0f && Mathf.Abs(total - 100f) > 0.01f)
        {
            Debug.LogWarning(
                $"[BaitData] '{name}': tổng % rarity = {total:0.#} (khác 100). " +
                "Game vẫn tự chuẩn hoá đúng tỉ lệ, nhưng nên sửa lại 4 số cho tổng = 100 để dễ đọc.",
                this
            );
        }
    }
#endif
}