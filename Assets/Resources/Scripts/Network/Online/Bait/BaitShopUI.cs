using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

[RequireComponent(typeof(CanvasGroup))]
public class BaitShopUI : MonoBehaviour
{
    public static BaitShopUI Instance { get; private set; }

    [Header("Entries")]
    [SerializeField] private BaitShopEntry[] _entries;
    [SerializeField] private BaitData[] _baitDatas;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI _totalPriceText;
    [SerializeField] private TextMeshProUGUI _bagCapacityText;
    [SerializeField] private TextMeshProUGUI _warningText;
    [SerializeField] private Button _buyButton;
    [SerializeField] private Button _closeButton;

    [Header("Panel Effect")]
    [SerializeField] private PanelEffect _panelEffect;

    [Header("Price Effect")]
    [SerializeField] private float _pricePunchScale = 1.12f;
    [SerializeField] private float _pricePunchDuration = 0.18f;

    [Header("Floating Text")]
    [SerializeField] private FloatingBaitText _floatingBaitText;

    [Header("NPC Voice (Thank You)")]
    [Tooltip("Cac cau NPC noi cam on. Keo 1 hoac nhieu file vao day, moi " +
             "lan mua xong se chon NGAU NHIEN 1 cau (khong lap lai cau vua " +
             "phat neu co tu 2 cau tro len).")]
    [SerializeField] private AudioClip[] _thankYouClips;

    [Range(0f, 1f)]
    [SerializeField] private float _thankYouVolume = 1f;

    [Tooltip("BAT: am luong nhan them voi thanh SFX trong Settings.")]
    [SerializeField] private bool _thankYouFollowSfxVolume = true;

    [Tooltip("Do tre (giay) sau khi panel tat han roi moi phat tieng, de " +
             "nghe tu nhien hon. 0 = phat ngay.")]
    [SerializeField] private float _thankYouDelay = 0.15f;

    [Tooltip("(Khong bat buoc) AudioSource cua NPC ban hang de phat tieng " +
             "3D tai cho NPC dung. De trong = phat 2D thang vao tai nguoi " +
             "choi. KHONG dat AudioSource tren chinh panel nay, vi panel bi " +
             "tat se cat mat tieng.")]
    [SerializeField] private AudioSource _npcVoiceSource;

    private AudioSource _fallbackVoiceSource;
    private int _lastThankYouIndex = -1;

    private Action _onClosed;

    private int _totalPrice;
    private bool _bagOverflow;

    private Vector3 _priceOriginalScale;
    private int _previousTotalPrice = -1;

    // ------------------------------------------------------------
    // PURCHASE STATE
    // ------------------------------------------------------------

    private bool _waitingForPurchaseResult;

    private string _pendingPurchaseData;

    private int _pendingTotalQty;

    private void Awake()
    {
        Instance = this;

        gameObject.SetActive(false);

        if (_totalPriceText != null)
            _priceOriginalScale = _totalPriceText.transform.localScale;

        if (_warningText != null)
            _warningText.gameObject.SetActive(false);

        for (int i = 0; i < _entries.Length; i++)
        {
            if (i < _baitDatas.Length)
                _entries[i].Setup(_baitDatas[i]);

            _entries[i].OnQuantityChanged += RecalculateTotal;
        }

        if (_buyButton != null)
            _buyButton.onClick.AddListener(OnBuyClicked);

        if (_closeButton != null)
            _closeButton.onClick.AddListener(OnCloseClicked);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        foreach (var entry in _entries)
        {
            if (entry != null)
                entry.OnQuantityChanged -= RecalculateTotal;
        }

        if (_buyButton != null)
            _buyButton.onClick.RemoveListener(OnBuyClicked);

        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(OnCloseClicked);

        if (_totalPriceText != null)
            _totalPriceText.transform.DOKill();

        // AudioSource du phong nam tren object rieng nen phai tu huy.
        if (_fallbackVoiceSource != null)
            Destroy(_fallbackVoiceSource.gameObject);
    }

    // ============================================================
    // OPEN
    // ============================================================

    public void Open(Action onClosed)
    {
        _onClosed = onClosed;

        _waitingForPurchaseResult = false;
        _pendingPurchaseData = null;
        _pendingTotalQty = 0;

        gameObject.SetActive(true);

        if (_totalPriceText != null)
        {
            _totalPriceText.transform.DOKill();
            _totalPriceText.transform.localScale = _priceOriginalScale;
        }

        _previousTotalPrice = -1;

        foreach (var entry in _entries)
        {
            if (entry != null)
                entry.ResetQuantity();
        }

        RecalculateTotal();

        _panelEffect.Show();
    }

    // ============================================================
    // CLOSE
    // ============================================================

    private void OnCloseClicked()
    {
        if (_waitingForPurchaseResult)
            return;

        EventSystem.current?.SetSelectedGameObject(null);

        _panelEffect.Hide(() =>
        {
            gameObject.SetActive(false);

            _onClosed?.Invoke();
        });
    }

    // ============================================================
    // BUY
    // ============================================================

    private void OnBuyClicked()
    {
        if (_waitingForPurchaseResult)
            return;

        if (_bagOverflow)
            return;

        if (_totalPrice <= 0)
            return;

        if (GameplayPlayer.LocalInstance == null)
        {
            ShowPurchaseFailed(
                "Player is not ready."
            );

            return;
        }

        var bag = BaitBag.Instance;

        if (bag == null)
            return;

        // --------------------------------------------------------
        // CREATE PURCHASE DATA
        // --------------------------------------------------------

        Dictionary<string, int> purchases =
            new Dictionary<string, int>();

        int totalQty = 0;

        foreach (var entry in _entries)
        {
            if (entry == null)
                continue;

            if (entry.Data == null)
                continue;

            if (entry.Quantity <= 0)
                continue;

            purchases[entry.Data.BaitId] = entry.Quantity;

            totalQty += entry.Quantity;
        }

        if (purchases.Count == 0)
            return;

        // --------------------------------------------------------
        // CHECK BAG CAPACITY LOCAL
        // --------------------------------------------------------

        if (!bag.CanAdd(purchases))
        {
            ShowWarning(
                "Please purchase the appropriate amount."
            );

            return;
        }

        // --------------------------------------------------------
        // SERIALIZE PURCHASE
        //
        // Ví dụ:
        //
        // Crayfish:2|Worm:3|Shrimp:1
        //
        // --------------------------------------------------------

        string purchaseData =
            SerializePurchases(purchases);

        if (string.IsNullOrEmpty(purchaseData))
            return;

        // --------------------------------------------------------
        // SAVE PENDING PURCHASE
        // --------------------------------------------------------

        _pendingPurchaseData = purchaseData;

        _pendingTotalQty = totalQty;

        _waitingForPurchaseResult = true;

        if (_buyButton != null)
            _buyButton.interactable = false;

        if (_closeButton != null)
            _closeButton.interactable = false;

        if (_warningText != null)
            _warningText.gameObject.SetActive(false);

        // --------------------------------------------------------
        // SEND TO STATE AUTHORITY
        // --------------------------------------------------------

        GameplayPlayer.LocalInstance.RPC_RequestBuyBaits(
            _totalPrice,
            purchaseData
        );
    }

    // ============================================================
    // SERIALIZE PURCHASES
    // ============================================================

    private string SerializePurchases(
        Dictionary<string, int> purchases)
    {
        List<string> parts = new List<string>();

        foreach (var pair in purchases)
        {
            if (string.IsNullOrEmpty(pair.Key))
                continue;

            if (pair.Value <= 0)
                continue;

            string part =
                pair.Key + ":" + pair.Value;

            parts.Add(part);
        }

        return string.Join("|", parts);
    }

    // ============================================================
    // DESERIALIZE PURCHASES
    // ============================================================

    private Dictionary<string, int> DeserializePurchases(
        string purchaseData)
    {
        Dictionary<string, int> result =
            new Dictionary<string, int>();

        if (string.IsNullOrEmpty(purchaseData))
            return result;

        string[] entries =
            purchaseData.Split('|');

        foreach (string entry in entries)
        {
            if (string.IsNullOrEmpty(entry))
                continue;

            string[] pair =
                entry.Split(':');

            if (pair.Length != 2)
                continue;

            string baitId = pair[0];

            if (!int.TryParse(
                    pair[1],
                    out int quantity))
            {
                continue;
            }

            if (string.IsNullOrEmpty(baitId))
                continue;

            if (quantity <= 0)
                continue;

            if (result.ContainsKey(baitId))
            {
                result[baitId] += quantity;
            }
            else
            {
                result.Add(
                    baitId,
                    quantity
                );
            }
        }

        return result;
    }

    // ============================================================
    // PURCHASE SUCCESS
    // ============================================================

    public void CompletePurchaseFromNetwork(
        string purchaseData)
    {
        // --------------------------------------------------------
        // CHỈ XỬ LÝ ĐÚNG PURCHASE ĐANG CHỜ
        // --------------------------------------------------------

        if (!_waitingForPurchaseResult)
            return;

        if (string.IsNullOrEmpty(purchaseData))
        {
            ResetPurchaseState();

            ShowPurchaseFailed(
                "Invalid purchase."
            );

            return;
        }

        var bag = BaitBag.Instance;

        if (bag == null)
        {
            ResetPurchaseState();

            ShowPurchaseFailed(
                "Bait bag is not ready."
            );

            return;
        }

        // --------------------------------------------------------
        // DESERIALIZE
        // --------------------------------------------------------

        Dictionary<string, int> purchases =
            DeserializePurchases(
                purchaseData
            );

        if (purchases.Count == 0)
        {
            ResetPurchaseState();

            ShowPurchaseFailed(
                "Invalid purchase."
            );

            return;
        }

        // --------------------------------------------------------
        // CHECK CAPACITY AGAIN
        //
        // Phòng trường hợp bag thay đổi trong lúc chờ RPC.
        // --------------------------------------------------------

        if (!bag.CanAdd(purchases))
        {
            ResetPurchaseState();

            ShowWarning(
                "Please purchase the appropriate amount."
            );

            return;
        }

        // --------------------------------------------------------
        // ADD BAITS
        //
        // TIỀN ĐÃ ĐƯỢC HOST TRỪ THÀNH CÔNG TRƯỚC ĐÓ.
        // --------------------------------------------------------

        bag.AddBaits(purchases);

        int totalQty = _pendingTotalQty;

        ResetPurchaseState();

        // --------------------------------------------------------
        // CLOSE SHOP
        // --------------------------------------------------------

        EventSystem.current?.SetSelectedGameObject(null);

        _panelEffect.Hide(() =>
        {
            gameObject.SetActive(false);

            // NPC noi "thank you" ngay sau khi panel tat han.
            // CHI chay o day (mua thanh cong), cac nhanh that bai o tren
            // deu return truoc khi toi day.
            ScheduleThankYouVoice();

            _onClosed?.Invoke();

            if (_floatingBaitText != null)
                _floatingBaitText.Show(totalQty);
        });
    }

    // ============================================================
    // NPC THANK YOU VOICE
    // ============================================================

    private void ScheduleThankYouVoice()
    {
        if (_thankYouClips == null ||
            _thankYouClips.Length == 0)
        {
            return;
        }

        if (_thankYouDelay > 0f)
        {
            // DOVirtual.DelayedCall khong phu thuoc object nay dang bat
            // hay tat, nen van chay du panel da SetActive(false).
            DOVirtual.DelayedCall(
                _thankYouDelay,
                PlayThankYouVoice
            );
        }
        else
        {
            PlayThankYouVoice();
        }
    }

    private void PlayThankYouVoice()
    {
        // Phong truong hop scene bi huy trong luc cho delay.
        if (this == null)
            return;

        if (_thankYouClips == null ||
            _thankYouClips.Length == 0)
        {
            return;
        }

        // Chon ngau nhien, tranh lap lai cau vua phat.
        int index = 0;

        if (_thankYouClips.Length > 1)
        {
            do
            {
                index =
                    UnityEngine.Random.Range(
                        0,
                        _thankYouClips.Length
                    );
            }
            while (index == _lastThankYouIndex);
        }

        _lastThankYouIndex = index;

        AudioClip clip = _thankYouClips[index];

        if (clip == null)
            return;

        AudioSource source = GetVoiceSource();

        if (source == null)
            return;

        float volume = _thankYouVolume;

        if (_thankYouFollowSfxVolume &&
            SoundManager.Instance != null)
        {
            volume *= SoundManager.Instance.GetSFXVolume();
        }

        source.PlayOneShot(clip, volume);
    }

    private AudioSource GetVoiceSource()
    {
        // Uu tien AudioSource cua NPC (3D) neu co gan.
        if (_npcVoiceSource != null)
            return _npcVoiceSource;

        // Khong co thi tao 1 AudioSource 2D tren object RIENG (khong phai
        // object panel, vi panel bi tat se cat mat tieng).
        if (_fallbackVoiceSource == null)
        {
            GameObject go =
                new GameObject("BaitShopThankYouAudio");

            _fallbackVoiceSource =
                go.AddComponent<AudioSource>();

            _fallbackVoiceSource.playOnAwake = false;
            _fallbackVoiceSource.loop = false;
            _fallbackVoiceSource.spatialBlend = 0f; // 2D
        }

        return _fallbackVoiceSource;
    }

    // ============================================================
    // PURCHASE FAILED
    // ============================================================

    public void ShowPurchaseFailed(
        string message)
    {
        ResetPurchaseState();

        if (string.IsNullOrEmpty(message))
        {
            message = "Purchase failed.";
        }

        ShowWarning(message);
    }

    // ============================================================
    // RESET PURCHASE STATE
    // ============================================================

    private void ResetPurchaseState()
    {
        _waitingForPurchaseResult = false;

        _pendingPurchaseData = null;

        _pendingTotalQty = 0;

        if (_buyButton != null)
        {
            _buyButton.interactable =
                !_bagOverflow &&
                _totalPrice > 0;
        }

        if (_closeButton != null)
            _closeButton.interactable = true;
    }

    // ============================================================
    // RECALCULATE TOTAL
    // ============================================================

    private void RecalculateTotal()
    {
        var bag = BaitBag.Instance;

        _totalPrice = 0;

        int totalQty = 0;

        foreach (var entry in _entries)
        {
            if (entry == null)
                continue;

            if (entry.Data == null)
                continue;

            _totalPrice +=
                entry.Data.PricePerUnit *
                entry.Quantity;

            totalQty += entry.Quantity;
        }

        // --------------------------------------------------------
        // PRICE TEXT
        // --------------------------------------------------------

        if (_totalPriceText != null)
        {
            _totalPriceText.text =
                _totalPrice > 0
                    ? $"{_totalPrice}$"
                    : "0$";

            PlayPriceEffect();
        }

        // --------------------------------------------------------
        // BAG CAPACITY
        // --------------------------------------------------------

        if (bag != null &&
            _bagCapacityText != null)
        {
            int current =
                bag.CurrentTotal;

            int max =
                bag.MaxCapacity;

            _bagCapacityText.text =
                $"Fishing Baits: {current}/{max}";

            _bagOverflow =
                current + totalQty > max;
        }
        else
        {
            _bagOverflow = false;
        }

        // --------------------------------------------------------
        // WARNING
        // --------------------------------------------------------

        if (_warningText != null)
        {
            _warningText.gameObject.SetActive(
                _bagOverflow &&
                totalQty > 0
            );
        }

        // --------------------------------------------------------
        // BUY BUTTON
        // --------------------------------------------------------

        if (_buyButton != null &&
            !_waitingForPurchaseResult)
        {
            _buyButton.interactable =
                !_bagOverflow &&
                _totalPrice > 0;
        }
    }

    // ============================================================
    // PRICE EFFECT
    // ============================================================

    private void PlayPriceEffect()
    {
        if (_totalPriceText == null)
            return;

        if (_previousTotalPrice == _totalPrice)
            return;

        if (_previousTotalPrice == -1)
        {
            _previousTotalPrice = _totalPrice;
            return;
        }

        _previousTotalPrice = _totalPrice;

        Transform target =
            _totalPriceText.transform;

        target.DOKill();

        target.localScale =
            _priceOriginalScale;

        target
            .DOPunchScale(
                Vector3.one *
                (_pricePunchScale - 1f),
                _pricePunchDuration,
                5,
                0.7f
            )
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                target.localScale =
                    _priceOriginalScale;
            });
    }

    // ============================================================
    // WARNING
    // ============================================================

    private void ShowWarning()
    {
        ShowWarning(
            "Please purchase the appropriate amount."
        );
    }

    private void ShowWarning(string message)
    {
        if (_warningText == null)
            return;

        _warningText.gameObject.SetActive(true);

        _warningText.text = message;
    }
}