using UnityEngine;

[CreateAssetMenu(fileName = "FishData", menuName = "Data/Fish Data")]
public class FishData : ScriptableObject
{
    [Header("Info")]
    public string FishId;
    public string FishName;
    public Sprite Icon;


    [Header("Rarity")]
    public FishRarity Rarity = FishRarity.Common;

    [Tooltip("Bảng màu dùng chung cho tất cả FishData.")]
    [SerializeField]
    private RarityColors _rarityColors;


    [Header("Fame Point")]
    [Tooltip(
        "Fame cơ bản của loài cá. " +
        "Giá trị này thể hiện mức độ nổi tiếng cơ bản của loài."
    )]

    [SerializeField]
    private float _baseFamePoint = 10f;


    [Header("World Visual")]
    public GameObject WorldPrefab;


    [Header("Random Selection (câu được con nào)")]

    [Tooltip(
        "Trọng số random khi câu — số càng lớn thì càng DỄ câu được " +
        "con này. Không cần tổng = 100, hệ thống tự tính tỉ lệ."
    )]
    public float SpawnWeight = 10f;


    [Header("Size (kích thước random khi câu được)")]

    [Tooltip(
        "Scale GỐC của cá. Đây là kích thước nhỏ nhất khi " +
        "Size Multiplier = 1."
    )]
    public Vector3 BaseScale = Vector3.one;


    [Tooltip(
        "Hệ số kích thước nhỏ nhất. 1 = BaseScale."
    )]
    public float MinSizeMultiplier = 1f;


    [Tooltip(
        "Hệ số kích thước lớn nhất."
    )]
    public float MaxSizeMultiplier = 1.6f;


    [Header("Weight (cân nặng)")]

    [Tooltip(
        "Cân nặng của cá khi ở kích thước BaseScale " +
        "(Size Multiplier = 1). Đơn vị: KG."
    )]
    public float BaseWeightKg = 1f;


    [Tooltip(
        "Số mũ dùng để tính cân nặng theo kích thước. " +
        "3 = tăng theo thể tích, phù hợp nhất với cá."
    )]
    [SerializeField]
    private float _weightScaleExponent = 3f;


    [Header("Sell Price (giá bán cho Fish Trader)")]

    [Tooltip(
        "Giá bán GỐC của 1 KG loài cá này. " +
        "Giá thật = Cân nặng × PricePerKg × Trophy Bonus."
    )]
    public int PricePerKg = 10;


    [Tooltip(
        "Thưởng thêm cho cá TO gần mức tối đa. " +
        "0 = không thưởng, 0.25 = con to nhất được +25% giá. " +
        "Bonus tăng theo bình phương nên cá size trung bình gần như " +
        "không được thưởng."
    )]
    [SerializeField]
    private float _trophyBonusMax = 0.25f;


    [Header("Hook Attach (vị trí gắn lên lưỡi câu)")]

    [Tooltip(
        "Không bắt buộc — chỉ dùng để nudge/tinh chỉnh thêm " +
        "sau khi đã tự canh theo FishMouthPoint."
    )]
    public Vector3 HookPositionOffset = Vector3.zero;


    [Tooltip(
        "Offset XOAY (Euler, độ) để hướng con cá treo tự nhiên " +
        "trên lưỡi câu."
    )]
    public Vector3 HookRotationOffset = Vector3.zero;



    [Header("Hand Attach (vị trí cầm trên tay)")]

    [Tooltip(
        "Offset VỊ TRÍ riêng cho con cá này khi được CẦM TRÊN TAY."
    )]
    public Vector3 HandPositionOffset = Vector3.zero;


    [Tooltip(
        "Offset XOAY (Euler, độ) riêng cho con cá này khi " +
        "được CẦM TRÊN TAY."
    )]
    public Vector3 HandRotationOffset = Vector3.zero;


    [Header("Inventory Link")]

    [Tooltip(
        "ItemData tương ứng dùng để hiển thị/lưu con cá này " +
        "trong túi đồ."
    )]
    public ItemData InventoryItem;


    public Color RarityColor
    {
        get
        {
            if (_rarityColors == null)
                return Color.white;

            return _rarityColors.Get(Rarity);
        }
    }


    public string GetColoredDisplayName()
    {
        if (_rarityColors == null)
            return FishName;

        return $"<color=#{_rarityColors.GetHex(Rarity)}>{FishName}</color>";
    }


    public float GetRandomSizeMultiplier()
    {
        return Random.Range(
            MinSizeMultiplier,
            MaxSizeMultiplier
        );
    }


    public float GetRandomSizeMultiplier(float sizeBias01)
    {
        sizeBias01 = Mathf.Clamp01(sizeBias01);

        float t = Random.value;

        const float minExponent = 50f;
        const float maxExponent = 0.02f;

        float exponent = minExponent *
            Mathf.Pow(maxExponent / minExponent, sizeBias01);

        float skewed = Mathf.Pow(
            t,
            exponent
        );

        return Mathf.Lerp(
            MinSizeMultiplier,
            MaxSizeMultiplier,
            skewed
        );
    }

    public float GetWeightKg(float sizeMultiplier)
    {
        sizeMultiplier = Mathf.Max(
            0f,
            sizeMultiplier
        );

        float weight =
            BaseWeightKg *
            Mathf.Pow(
                sizeMultiplier,
                _weightScaleExponent
            );

        return weight;
    }



    public string GetWeightText(float sizeMultiplier)
    {
        float weight = GetWeightKg(sizeMultiplier);

        if (weight >= 10f)
        {
            return $"{weight:0.0} KG";
        }

        return $"{weight:0.##} KG";
    }


    public string GetDisplayNameWithWeight(
        float sizeMultiplier)
    {
        return $"{FishName} - {GetWeightText(sizeMultiplier)}";
    }

    public string GetColoredDisplayNameWithWeight(
        float sizeMultiplier)
    {
        return
            $"{GetColoredDisplayName()} - " +
            $"{GetWeightText(sizeMultiplier)}";
    }


    public int GetSellPrice(float sizeMultiplier)
    {
        float weightKg =
            GetWeightKg(sizeMultiplier);

        float range = Mathf.Max(
            0.0001f,
            MaxSizeMultiplier -
            MinSizeMultiplier
        );

        float t = Mathf.Clamp01(
            (
                sizeMultiplier -
                MinSizeMultiplier
            ) / range
        );

        float trophyBonus =
            1f +
            _trophyBonusMax *
            t *
            t;

        float price =
            weightKg *
            PricePerKg *
            trophyBonus;

        return Mathf.Max(
            1,
            Mathf.RoundToInt(price)
        );
    }

    public float BaseFamePoint
    {
        get
        {
            return _baseFamePoint;
        }
    }

    public float GetRarityFameMultiplier()
    {
        switch (Rarity)
        {
            case FishRarity.Common:
                return 1f;

            case FishRarity.Rare:
                return 2f;

            case FishRarity.SuperRare:
                return 3f;

            case FishRarity.Legendary:
                return 4f;

            default:
                return 1f;
        }
    }


    public int GetFamePoint(float weightKg)
    {
        weightKg = Mathf.Max(
            0.1f,
            weightKg
        );

        float rarityMultiplier =
            GetRarityFameMultiplier();

        float weightMultiplier =
            Mathf.Sqrt(weightKg);

        float fame =
            _baseFamePoint *
            rarityMultiplier *
            weightMultiplier;

        return Mathf.RoundToInt(fame);
    }

    public int GetFamePointFromSize(
        float sizeMultiplier)
    {
        float weightKg =
            GetWeightKg(sizeMultiplier);

        return GetFamePoint(weightKg);
    }


    public string GetFamePointText(
        float weightKg)
    {
        int fame =
            GetFamePoint(weightKg);

        return FormatFamePoint(fame);
    }


    public string FormatFamePoint(
        int fame)
    {
        if (fame >= 1000000)
        {
            return $"{fame / 1000000f:0.#}M";
        }

        if (fame >= 1000)
        {
            return $"{fame / 1000f:0.#}K";
        }

        return fame.ToString();
    }


    public float GetRarityDifficulty01()
    {
        switch (Rarity)
        {
            case FishRarity.Common:
                return 0f;

            case FishRarity.Rare:
                return 0.33f;

            case FishRarity.SuperRare:
                return 0.66f;

            case FishRarity.Legendary:
                return 1f;

            default:
                return 0f;
        }
    }
}