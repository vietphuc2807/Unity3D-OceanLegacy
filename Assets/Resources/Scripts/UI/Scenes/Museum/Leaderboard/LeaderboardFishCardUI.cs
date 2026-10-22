using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 1 card cá DÙNG ĐỂ XEM (không bấm được) trong danh sách "liệt kê"
/// của 1 dòng xếp hạng ở panel MuseumLeaderboardUI.
///
/// Ảnh dùng ItemData.ReturnIcon, còn vị trí/góc xoay/kích cỡ dùng
/// field RIÊNG của Leaderboard (LeaderboardIconPosition/Rotation/Size)
/// nên không ảnh hưởng panel Thùng cá mang về.
/// </summary>
public class LeaderboardFishCardUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image _fishIcon;
    [SerializeField] private TMP_Text _fishNameText;
    [SerializeField] private TMP_Text _weightText;
    [SerializeField] private TMP_Text _famePointText;

    private Vector2 _iconBasePosition;
    private bool _baseCaptured;

    /// <summary>ItemData đang hiển thị (LeaderboardIconTuner dùng để lọc card).</summary>
    public ItemData Item { get; private set; }

    private void Awake()
    {
        CaptureBase();
    }

    private void CaptureBase()
    {
        if (_baseCaptured || _fishIcon == null) return;
        _iconBasePosition = _fishIcon.rectTransform.anchoredPosition;
        _baseCaptured = true;
    }

    public void SetData(ItemData item, float sizeMultiplier)
    {
        FishData fish = item != null ? item.FishInfo : null;

        if (fish == null)
        {
            gameObject.SetActive(false);
            return;
        }

        Item = item;

        if (_fishNameText != null)
            _fishNameText.text = fish.GetColoredDisplayName();

        float weightKg = fish.GetWeightKg(sizeMultiplier);

        if (_weightText != null)
            _weightText.text = fish.GetWeightText(sizeMultiplier);

        if (_famePointText != null)
        {
            string fame = fish.GetFamePointText(weightKg);

            // Đảm bảo luôn có hậu tố "(FP)", VD "120 (FP)".
            if (!fame.Contains("FP"))
                fame += " (FP)";

            _famePointText.text = fame;
        }

        if (_fishIcon != null)
        {
            _fishIcon.sprite = item.ReturnIcon;
            _fishIcon.enabled = item.ReturnIcon != null;
            _fishIcon.preserveAspect = true;

            ApplyIconLayout(
                item.LeaderboardIconPosition,
                item.LeaderboardIconRotation,
                item.LeaderboardIconSize);
        }
    }

    /// <summary>offset = dịch so với vị trí gốc của icon trên prefab card.</summary>
    public void ApplyIconLayout(Vector2 offset, float rotationZ, Vector2 size)
    {
        if (_fishIcon == null) return;
        CaptureBase();

        RectTransform rt = _fishIcon.rectTransform;
        rt.anchoredPosition = _iconBasePosition + offset;
        rt.localEulerAngles = new Vector3(0f, 0f, rotationZ);
        rt.sizeDelta = size;
    }
}