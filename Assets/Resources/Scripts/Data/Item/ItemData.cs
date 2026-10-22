using UnityEngine;

[CreateAssetMenu(fileName = "Item", menuName = "Data/Item")]
public class ItemData : ScriptableObject
{
    [SerializeField] private string _itemId;
    [SerializeField] private string _displayName;
    [SerializeField] private Sprite _icon;
    [SerializeField] private GameObject _handPrefab;

    [Header("Hand Fit Offset")]
    [SerializeField] private Vector3 _localPosition;
    [SerializeField] private Vector3 _localRotationEuler;
    [SerializeField] private Vector3 _localScale = Vector3.one;

    [Header("Animation")]
    [SerializeField] private bool _isFishingRod;

    [Header("Fish Link")]
    [SerializeField] private FishData _fishData;

    [Header("Inventory Icon")]
    [SerializeField] private Vector3 _iconPosition = Vector3.zero;
    [SerializeField] private Vector3 _iconRotation = Vector3.zero;
    [SerializeField] private Vector3 _iconScale = Vector3.one;

    [Header("Fish Trader Icon (chỉnh icon cá trên ô của panel Fish Trader)")]
    [Tooltip("Dịch icon so với vị trí gốc của nó trong ô (pixel UI).")]
    [SerializeField] private Vector2 _traderIconPosition = Vector2.zero;
    [Tooltip("Góc xoay quanh tâm icon (độ, trục Z).")]
    [SerializeField] private float _traderIconRotation = 0f;
    [Tooltip("Scale icon (1 = giữ nguyên). Đặt X = -1 để lật ngang.")]
    [SerializeField] private Vector2 _traderIconScale = Vector2.one;

    [Header("Fish Return Icon (chỉnh icon cá trên panel Thùng cá mang về ở Museum)")]
    [Tooltip(
        "Ảnh RIÊNG dùng cho icon cá trong panel Thùng cá mang về. " +
        "Để trống nếu muốn dùng chung ảnh với Icon (Inventory Icon) " +
        "phía trên."
    )]
    [SerializeField] private Sprite _returnIcon;
    [Tooltip("Dịch icon so với vị trí gốc của nó trong panel Thùng cá mang về (pixel UI).")]
    [SerializeField] private Vector2 _returnIconPosition = Vector2.zero;
    [Tooltip("Góc xoay quanh tâm icon (độ, trục Z).")]
    [SerializeField] private float _returnIconRotation = 0f;
    [Tooltip(
        "Kích cỡ THẬT (Width/Height, pixel UI) của icon trong panel " +
        "Thùng cá mang về - chỉnh thẳng số này để ra đúng kích cỡ " +
        "mong muốn, không cần Scale rồi canh lại Position/Rotation, " +
        "cũng không cần bấm Set Native Size."
    )]
    [SerializeField] private Vector2 _returnIconSize = new Vector2(100f, 100f);

    [Header("Leaderboard Card Icon (RIÊNG cho card cá ở panel Leaderboard - không ảnh hưởng panel Thùng cá mang về)")]
    [Tooltip("Dịch icon so với vị trí gốc của icon trên prefab card (pixel UI).")]
    [SerializeField] private Vector2 _leaderboardIconPosition = Vector2.zero;
    [Tooltip("Góc xoay quanh tâm icon (độ, trục Z).")]
    [SerializeField] private float _leaderboardIconRotation = 0f;
    [Tooltip("Kích cỡ thật (Width/Height, pixel UI) của icon trên card Leaderboard.")]
    [SerializeField] private Vector2 _leaderboardIconSize = new Vector2(100f, 100f);

    [Header("Aquarium Display (vị trí + kích cỡ cá hiển thị trong bể ở Museum)")]
    [Tooltip(
        "Prefab 3D dùng khi trưng bày cá này trong bể. Để trống sẽ tự " +
        "dùng WorldPrefab của FishData (y hệt mô hình lúc câu được)."
    )]
    [SerializeField] private GameObject _aquariumPrefab;
    [Tooltip("Vị trí LOCAL (so với gốc bể - _spawnRoot của AquariumDisplayManager) nơi con cá này đứng khi trưng bày.")]
    [SerializeField] private Vector3 _aquariumPosition = Vector3.zero;
    [Tooltip("Góc xoay LOCAL (Euler, độ) của con cá khi trưng bày trong bể.")]
    [SerializeField] private Vector3 _aquariumRotation = Vector3.zero;
    [Tooltip(
        "Scale LOCAL 'CHUẨN' của con cá khi trưng bày trong bể - đây là " +
        "kích cỡ ứng với size multiplier = 1 (chưa nhân theo cân nặng)."
    )]
    [SerializeField] private Vector3 _aquariumScale = Vector3.one;

    [Header("Aquarium Size Clamp (giới hạn to/nhỏ cho VỪA MẮT trong bể)")]
    [Tooltip(
        "Cân nặng thật (Min/Max Size Multiplier trong FishData) có thể " +
        "chênh lệch rất lớn (VD cá sấu 1 KG so với 100 KG), nhưng bể " +
        "kính lại có kích thước cố định - nếu scale thẳng theo cân " +
        "nặng, con nhỏ nhất có thể tí hin, con to nhất có thể tràn ra " +
        "ngoài bể. 2 giá trị dưới đây KẸP LẠI size multiplier THỰC TẾ " +
        "dùng để tính scale hiển thị (không ảnh hưởng cân nặng/Fame/giá " +
        "bán - chỉ ảnh hưởng kích cỡ MÔ HÌNH trong bể), giúp con cá nào " +
        "cũng 'vừa mắt' trong bể dù cân nặng chênh lệch bao nhiêu. Để " +
        "0 (hoặc bất kỳ giá trị <= 0) ở 1 trong 2 ô nghĩa là KHÔNG kẹp " +
        "phía đó - giữ hành vi cũ (scale thẳng theo cân nặng)."
    )]
    [SerializeField] private float _aquariumMinDisplaySizeMultiplier = 0f;
    [SerializeField] private float _aquariumMaxDisplaySizeMultiplier = 0f;

    public string ItemId => _itemId;
    public string DisplayName => _displayName;

    public Sprite Icon =>
        _icon != null
            ? _icon
            : (_fishData != null ? _fishData.Icon : null);

    public GameObject HandPrefab => _handPrefab;

    public Vector3 LocalPosition => _localPosition;
    public Vector3 LocalRotationEuler => _localRotationEuler;
    public Vector3 LocalScale => _localScale;

    public bool IsFishingRod => _isFishingRod;

    public FishData FishInfo => _fishData;
    public bool IsFish => _fishData != null;

    public Vector3 IconPosition => _iconPosition;
    public Vector3 IconRotation => _iconRotation;
    public Vector3 IconScale => _iconScale;

    public Vector2 TraderIconPosition => _traderIconPosition;
    public float TraderIconRotation => _traderIconRotation;
    public Vector2 TraderIconScale => _traderIconScale;

    public Sprite ReturnIcon =>
        _returnIcon != null ? _returnIcon : Icon;

    public Vector2 ReturnIconPosition => _returnIconPosition;
    public float ReturnIconRotation => _returnIconRotation;
    public Vector2 ReturnIconSize => _returnIconSize;

    public Vector2 LeaderboardIconPosition => _leaderboardIconPosition;
    public float LeaderboardIconRotation => _leaderboardIconRotation;
    public Vector2 LeaderboardIconSize => _leaderboardIconSize;

    public GameObject AquariumPrefab =>
        _aquariumPrefab != null
            ? _aquariumPrefab
            : (IsFish ? _fishData.WorldPrefab : null);

    public Vector3 AquariumPosition => _aquariumPosition;
    public Vector3 AquariumRotation => _aquariumRotation;
    public Vector3 AquariumScale => _aquariumScale;

    public float AquariumMinDisplaySizeMultiplier => _aquariumMinDisplaySizeMultiplier;
    public float AquariumMaxDisplaySizeMultiplier => _aquariumMaxDisplaySizeMultiplier;

    public float GetAquariumDisplaySizeMultiplier(float rawSizeMultiplier)
    {
        float value = rawSizeMultiplier;

        if (_aquariumMinDisplaySizeMultiplier > 0f)
            value = Mathf.Max(value, _aquariumMinDisplaySizeMultiplier);

        if (_aquariumMaxDisplaySizeMultiplier > 0f)
            value = Mathf.Min(value, _aquariumMaxDisplaySizeMultiplier);

        return value;
    }

    public Color DisplayColor =>
        IsFish ? _fishData.RarityColor : Color.white;

    public string GetColoredDisplayName()
    {
        return IsFish
            ? _fishData.GetColoredDisplayName()
            : _displayName;
    }
}