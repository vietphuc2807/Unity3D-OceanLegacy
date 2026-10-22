using Fusion;
using UnityEngine;

/// <summary>
/// Lưu trạng thái nâng cấp cần câu DÙNG CHUNG cho cả team (giống TeamWallet).
/// Level bắt đầu = 1 (mặc định, miễn phí). Level 2, 3 phải mua TUẦN TỰ
/// (chỉ mua được level kế tiếp, không được nhảy cóc).
///
/// FIX (balancing khi thất bại về Day 1): khi cả team KHÔNG đạt chỉ tiêu
/// tiền trước khi hết ngày, GameDayManager bắn GameDayManager.OnDayFailureReset
/// (RPC RpcTargets.All, nên chạy trên MỌI client). Ở đây lắng nghe event đó
/// và — CHỈ trên Host/State Authority — đưa RodLevel về lại MinLevel, song
/// song với việc TeamWallet.ResetToStartingMoney(), PlayerInventory mất hết
/// cá, BaitBag mất hết mồi.
///
/// Đặt script này lên cùng object với TeamWallet (hoặc 1 object riêng có
/// NetworkObject), trong scene gameplay.
/// </summary>
public class TeamRodUpgrade : NetworkBehaviour
{
    public static TeamRodUpgrade Instance { get; private set; }

    public const int MinLevel = 1;
    public const int MaxLevel = 3;

    [Header("Giá nâng cấp")]
    [Tooltip("Index 0 = Level 1 (mặc định, miễn phí, để 0). Index 1 = giá lên Level 2. Index 2 = giá lên Level 3.")]
    [SerializeField] private int[] _upgradePrices = { 0, 500, 1500 };

    [Header("Hệ số tốc độ thanh tiến độ câu cá theo từng Level")]
    [Tooltip("Index 0 = Level 1 (x1, mặc định). Index 1 = Level 2. Index 2 = Level 3. Level càng cao, hệ số càng lớn → kéo cá nhanh hơn.")]
    [SerializeField] private float[] _progressGainMultiplierByLevel = { 1f, 1.25f, 1.6f };

    [Networked]
    public int RodLevel { get; private set; }

    // ============================================================
    // UNITY (subscribe reset khi thất bại)
    // ============================================================

    private void OnEnable()
    {
        // FIX: subscribe ở OnEnable (không phải Spawned) để không bỏ lỡ
        // sự kiện trong trường hợp NetworkBehaviour này bị disable/enable
        // lại giữa chừng — giống pattern GameDayManager đang dùng cho
        // GameDayIntroPanel.OnDayIntroFinished.
        GameDayManager.OnDayFailureReset += HandleDayFailureReset;
    }

    private void OnDisable()
    {
        GameDayManager.OnDayFailureReset -= HandleDayFailureReset;
    }

    private void HandleDayFailureReset()
    {
        // Event này bắn ra trên MỌI client (RPC RpcTargets.All bên
        // GameDayManager). RodLevel là [Networked] nên CHỈ State Authority
        // (Host) mới được phép đổi — client bỏ qua, chờ giá trị tự đồng bộ
        // xuống như bình thường.
        if (Object == null || !Object.HasStateAuthority)
            return;

        ResetToLevel1();
    }

    public override void Spawned()
    {
        Instance = this;

        if (Object.HasStateAuthority)
            RodLevel = MinLevel;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }

    public bool IsLevelOwned(int level) => level <= RodLevel;

    /// <summary>True nếu "level" chính là level kế tiếp có thể mua (nâng tuần tự).</summary>
    public bool IsNextLevel(int level) => level == RodLevel + 1;

    public int GetPrice(int level)
    {
        int index = level - 1;

        if (_upgradePrices == null || index < 0 || index >= _upgradePrices.Length)
            return 0;

        return _upgradePrices[index];
    }

    /// <summary>Hệ số nhân tốc độ thanh tiến độ ứng với Level HIỆN TẠI của team.</summary>
    public float GetProgressGainMultiplier()
    {
        if (_progressGainMultiplierByLevel == null || _progressGainMultiplierByLevel.Length == 0)
            return 1f;

        int index = Mathf.Clamp(RodLevel - 1, 0, _progressGainMultiplierByLevel.Length - 1);

        return _progressGainMultiplierByLevel[index];
    }

    /// <summary>
    /// Chỉ State Authority (host) mới được gọi — set level mới SAU KHI đã trừ
    /// tiền team thành công. Trả về false nếu level không hợp lệ (không phải
    /// bước tuần tự kế tiếp, hoặc vượt MaxLevel).
    /// </summary>
    public bool TrySetLevel(int newLevel)
    {
        if (!Object.HasStateAuthority)
            return false;

        if (newLevel != RodLevel + 1 || newLevel > MaxLevel)
            return false;

        RodLevel = newLevel;
        return true;
    }

    /// <summary>
    /// Dùng khi cả team KHÔNG đạt money target trước khi hết ngày —
    /// GameDayManager (qua GameDayManager.OnDayFailureReset, xem
    /// HandleDayFailureReset() ở trên) gọi hàm này để đưa cấp độ cần câu
    /// về lại mức khởi điểm, y hệt TeamWallet.ResetToStartingMoney().
    /// </summary>
    public void ResetToLevel1()
    {
        // CHỈ State Authority mới được thay đổi RodLevel.
        if (!Object.HasStateAuthority)
            return;

        RodLevel = MinLevel;

        // RodLevel là [Networked] nên tự đồng bộ tới mọi client. UI
        // (RodUpgradeUI) đang tự poll RodLevel mỗi Update() lúc panel mở
        // (xem RodUpgradeUI.Update()) nên sẽ tự refresh, không cần RPC
        // thông báo riêng.
    }
}