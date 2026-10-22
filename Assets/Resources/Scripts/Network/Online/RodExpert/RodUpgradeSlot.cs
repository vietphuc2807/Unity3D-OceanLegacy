using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 1 nút Level trong panel Rod Upgrade (Level 1 / 2 / 3).
/// Gắn lên từng button trong panel, set _level đúng số level ở Inspector.
/// </summary>
public class RodUpgradeSlot : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _priceText;

    [Tooltip("Object hiện chữ 'Default' — chỉ bật khi đây là Level 1 và đã sở hữu.")]
    [SerializeField] private GameObject _defaultLabel;

    [Tooltip("Object 'sáng lên' khi level này đã được sở hữu (mua rồi / mặc định).")]
    [SerializeField] private GameObject _ownedHighlight;

    [Tooltip("Level mà nút này đại diện (1, 2 hoặc 3).")]
    [SerializeField] private int _level = 1;

    private Action<RodUpgradeSlot> _onClicked;

    // isNext được cache lại từ lần Refresh() gần nhất, để SetLocked() có thể
    // bật/tắt tạm nút trong lúc chờ RPC mà không cần biết lại currentLevel.
    private bool _isNextLevel;
    private bool _locked;

    public int Level => _level;

    private void Reset()
    {
        _button = GetComponent<Button>();
    }

    public void Bind(Action<RodUpgradeSlot> onClicked)
    {
        _onClicked = onClicked;

        if (_button == null)
            return;

        _button.onClick.RemoveListener(HandleClick);
        _button.onClick.AddListener(HandleClick);
    }

    private void HandleClick()
    {
        _onClicked?.Invoke(this);
    }

    /// <summary>
    /// currentLevel: level team đang sở hữu. price: giá để mua lên _level này
    /// (bỏ qua nếu _level đã owned).
    /// </summary>
    public void Refresh(int currentLevel, int price)
    {
        bool owned = _level <= currentLevel;
        _isNextLevel = _level == currentLevel + 1;

        if (_ownedHighlight != null)
            _ownedHighlight.SetActive(owned);

        if (_defaultLabel != null)
            _defaultLabel.SetActive(owned && _level == 1);

        if (_priceText != null)
        {
            _priceText.gameObject.SetActive(!owned);
            _priceText.text = $"{price}$";
        }

        ApplyInteractable();
    }

    /// <summary>Khoá tạm nút (dùng trong lúc đang chờ kết quả RPC nâng cấp).</summary>
    public void SetLocked(bool locked)
    {
        _locked = locked;
        ApplyInteractable();
    }

    private void ApplyInteractable()
    {
        if (_button != null)
            _button.interactable = _isNextLevel && !_locked;
    }
}