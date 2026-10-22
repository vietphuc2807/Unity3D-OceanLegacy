using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Panel Rod Upgrade: hiển thị 3 ô Level (Level 1 mặc định, Level 2 và 3
/// phải mua tuần tự bằng tiền team). Mua nâng cấp nào thì ô đó sáng lên cho
/// CẢ TEAM (đồng bộ qua TeamRodUpgrade), panel tự đóng sau khi mua thành công.
///
/// Đặt script này lên P-RodUpgrade (cùng object với PanelEffect), giống hệt
/// cách FishTraderUI / BaitShopUI đang được setup.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class RodUpgradeUI : MonoBehaviour
{
    public static RodUpgradeUI Instance { get; private set; }

    [Header("Slots (đúng thứ tự Level 1, 2, 3)")]
    [SerializeField] private RodUpgradeSlot[] _slots;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI _warningText; // không bắt buộc
    [SerializeField] private Button _closeButton;

    [Header("Panel Effect")]
    [SerializeField] private PanelEffect _panelEffect;

    private Action _onClosed;

    private bool _openRequested;
    private bool _isOpen;

    private bool _waitingForUpgradeResult;
    private int _pendingLevel = -1;

    // Dùng để phát hiện người khác (host / đồng đội) vừa nâng cấp trong lúc
    // panel đang mở, để tự refresh — theo đúng kiểu poll đang dùng ở
    // TeamMoneyUI (Fusion project này không dùng OnChangedRender).
    private int _lastKnownLevel = -1;

    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        Instance = this;

        if (_warningText != null)
            _warningText.gameObject.SetActive(false);

        if (_slots != null)
        {
            foreach (var slot in _slots)
            {
                if (slot != null)
                    slot.Bind(OnSlotClicked);
            }
        }

        if (_closeButton != null)
            _closeButton.onClick.AddListener(OnCloseClicked);

        // Ẩn panel lúc khởi động, trừ khi Awake chạy do chính Open() vừa
        // kích hoạt object (panel đang inactive sẵn trong scene).
        if (!_openRequested)
            gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(OnCloseClicked);
    }

    private void Update()
    {
        if (!_isOpen || TeamRodUpgrade.Instance == null)
            return;

        int currentLevel = TeamRodUpgrade.Instance.RodLevel;

        if (currentLevel != _lastKnownLevel)
        {
            _lastKnownLevel = currentLevel;
            RefreshSlots();
        }
    }

    // ============================================================
    // OPEN / CLOSE
    // ============================================================

    public void Open(Action onClosed)
    {
        _onClosed = onClosed;

        _isOpen = true;
        ResetUpgradeState();
        SetSlotsLocked(false);

        _openRequested = true;
        gameObject.SetActive(true);

        _lastKnownLevel = -1;

        RefreshSlots();

        _panelEffect.Show();
    }

    private void OnCloseClicked()
    {
        if (_waitingForUpgradeResult)
            return;

        CloseWithEffect();
    }

    /// <summary>
    /// Đóng panel bằng PanelEffect.Hide(). Dùng chung cho nút Close và cho
    /// việc tự đóng sau khi nâng cấp thành công.
    /// </summary>
    private void CloseWithEffect()
    {
        if (!_isOpen)
            return;

        EventSystem.current?.SetSelectedGameObject(null);

        _isOpen = false;

        _panelEffect.Hide(() =>
        {
            gameObject.SetActive(false);

            _onClosed?.Invoke();
        });
    }

    // ============================================================
    // REFRESH
    // ============================================================

    private void RefreshSlots()
    {
        if (_slots == null || TeamRodUpgrade.Instance == null)
            return;

        int currentLevel = TeamRodUpgrade.Instance.RodLevel;

        foreach (var slot in _slots)
        {
            if (slot == null)
                continue;

            int price = TeamRodUpgrade.Instance.GetPrice(slot.Level);
            slot.Refresh(currentLevel, price);
        }
    }

    private void SetSlotsLocked(bool locked)
    {
        if (_slots != null)
        {
            foreach (var slot in _slots)
                slot?.SetLocked(locked);
        }

        if (_closeButton != null)
            _closeButton.interactable = !locked;
    }

    // ============================================================
    // UPGRADE
    // ============================================================

    private void OnSlotClicked(RodUpgradeSlot slot)
    {
        if (_waitingForUpgradeResult || TeamRodUpgrade.Instance == null)
            return;

        // Phòng trường hợp double-click / race: chỉ cho bấm đúng level kế tiếp.
        if (!TeamRodUpgrade.Instance.IsNextLevel(slot.Level))
            return;

        if (GameplayPlayer.LocalInstance == null)
        {
            ShowWarning("Player is not ready.");
            return;
        }

        EventSystem.current?.SetSelectedGameObject(null);

        _pendingLevel = slot.Level;
        _waitingForUpgradeResult = true;

        HideWarning();
        SetSlotsLocked(true);

        GameplayPlayer.LocalInstance.RPC_RequestUpgradeRod(_pendingLevel);
    }

    /// <summary>
    /// Host đã trừ tiền team + tăng RodLevel thành công → panel tự đóng.
    /// (Được gọi từ GameplayPlayer.RPC_UpgradeRodResult.)
    /// </summary>
    public void CompleteUpgradeFromNetwork(int level)
    {
        if (!_waitingForUpgradeResult || level != _pendingLevel)
            return;

        ResetUpgradeState();

        // RodLevel (Networked) đã tự đồng bộ tới mọi client; đóng panel là đủ.
        CloseWithEffect();
    }

    public void ShowUpgradeFailed(string message)
    {
        ResetUpgradeState();
        SetSlotsLocked(false);
        RefreshSlots();

        ShowWarning(string.IsNullOrEmpty(message) ? "Upgrade failed." : message);
    }

    private void ResetUpgradeState()
    {
        _waitingForUpgradeResult = false;
        _pendingLevel = -1;
    }

    // ============================================================
    // WARNING
    // ============================================================

    private void ShowWarning(string message)
    {
        if (_warningText == null)
        {
            Debug.LogWarning($"[RodUpgradeUI] {message}");
            return;
        }

        _warningText.gameObject.SetActive(true);
        _warningText.text = message;
    }

    private void HideWarning()
    {
        if (_warningText != null)
            _warningText.gameObject.SetActive(false);
    }
}