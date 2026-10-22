using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Hien thi 2 chi so tong quan cua toan bo Museum o header:
/// "X/Y" (so be dang co ca) va tong Fame Point.
///
/// Tu dong CAP NHAT moi khi BAT KY be nao thay doi trang thai (dat ca /
/// lay ca) qua AquariumDisplayManager.OnAnyTankChanged, va moi lan tong
/// Fame Point thuc su doi thi day len Firestore (AquariumSaveService).
///
/// MOI (hieu ung): khi gia tri THUC SU doi (khong tinh lan tinh dau
/// tien luc vua bat script), 2 text se:
///  - Fame Point: dem tu so cu len/xuong so moi + phong to roi thu ve
///    + nhay mau (xanh khi tang, do khi giam) + (tuy chon) chu "+N"
///    bay len roi mo dan (_fameDeltaText).
///  - So ca X/Y: phong to roi thu ve + nhay mau.
/// Tat ca dung Time.unscaledDeltaTime nen van chay khi game pause.
///
/// Dat script nay len 1 object trong Canvas cua scene Museum (CHI 1
/// object DUY NHAT).
/// </summary>
public class MuseumStatsUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Text hien so be dang co ca, dang 'X/Y' (VD '2/4').")]
    [SerializeField] private TMP_Text _fishCountText;

    [Tooltip("Text hien tong Fame Point cua tat ca con ca dang trung bay.")]
    [SerializeField] private TMP_Text _famePointText;

    [Tooltip(
        "Catalog chua toan bo ItemData trong game - dung de tra " +
        "ItemData (roi FishData) tu ItemId dang luu trong tung " +
        "AquariumDisplayManager, de tinh Fame Point cua con ca do."
    )]
    [SerializeField] private ItemCatalog _itemCatalog;

    [Tooltip(
        "So be TOI DA cua Museum, dung cho phan mau so khi hien 'X/Y' " +
        "(VD de 4 se hien '2/4'). Neu de 0 hoac am, tu dung SO BE THUC " +
        "TE dang co trong scene lam mau so."
    )]
    [SerializeField] private int _maxTankCount = 4;

    [Header("Effect - Chung")]
    [Tooltip("Tat neu muon tat het hieu ung (cap nhat text tuc thi nhu cu).")]
    [SerializeField] private bool _enableEffects = true;

    [Tooltip("Mau nhay len khi gia tri TANG.")]
    [SerializeField] private Color _increaseColor = new Color(0.45f, 1f, 0.45f, 1f);

    [Tooltip("Mau nhay len khi gia tri GIAM.")]
    [SerializeField] private Color _decreaseColor = new Color(1f, 0.4f, 0.4f, 1f);

    [Tooltip("Do phong to them luc 'punch' (0.3 = to them 30%).")]
    [SerializeField] private float _punchAmount = 0.3f;

    [Tooltip("Thoi gian (giay) cua 1 lan phong to/thu ve + nhay mau.")]
    [SerializeField] private float _punchDuration = 0.45f;

    [Header("Effect - Fame Point")]
    [Tooltip("Thoi gian (giay) dem tu so cu sang so moi.")]
    [SerializeField] private float _countDuration = 0.8f;

    [Header("Effect - Chu '+N' bay len (TUY CHON, de trong neu khong dung)")]
    [Tooltip(
        "1 TMP_Text RIENG (dat cung cho / ngay canh text Fame Point, " +
        "cung Canvas) de hien '+227' / '-112' bay len roi mo dan. Mac " +
        "dinh tat (script tu bat/tat). De trong = khong co hieu ung nay."
    )]
    [SerializeField] private TMP_Text _fameDeltaText;

    [SerializeField] private float _deltaRiseDistance = 40f;
    [SerializeField] private float _deltaDuration = 1.0f;

    // ---- trang thai ----
    private int _lastFishCount = -1;
    private int _lastTotalFame = -1;
    private float _displayedFame;

    // ---- gia tri goc de reset ----
    private Vector3 _fishBaseScale = Vector3.one;
    private Vector3 _fameBaseScale = Vector3.one;
    private Color _fishBaseColor = Color.white;
    private Color _fameBaseColor = Color.white;
    private Vector2 _deltaBasePos;
    private bool _baseCaptured;

    private Coroutine _fishPunchRoutine;
    private Coroutine _famePunchRoutine;
    private Coroutine _fameCountRoutine;
    private Coroutine _deltaRoutine;

    private void Awake()
    {
        CaptureBase();
    }

    private void CaptureBase()
    {
        if (_baseCaptured)
            return;

        if (_fishCountText != null)
        {
            _fishBaseScale = _fishCountText.rectTransform.localScale;
            _fishBaseColor = _fishCountText.color;
        }

        if (_famePointText != null)
        {
            _fameBaseScale = _famePointText.rectTransform.localScale;
            _fameBaseColor = _famePointText.color;
        }

        if (_fameDeltaText != null)
        {
            _deltaBasePos = _fameDeltaText.rectTransform.anchoredPosition;
            _fameDeltaText.gameObject.SetActive(false);
        }

        _baseCaptured = true;
    }

    private void OnEnable()
    {
        AquariumDisplayManager.OnAnyTankChanged += HandleAnyTankChanged;

        // Tinh ngay 1 lan luc bat dau de UI khong bi trong - KHONG day len
        // Firestore (saveToServer: false), xem giai thich cu: luc nay
        // AquariumPersistenceLoader co the chua kip dat ca nen totalFame
        // = 0 se ghi de len gia tri dung tren Firestore.
        RefreshStats(saveToServer: false);
    }

    private void OnDisable()
    {
        AquariumDisplayManager.OnAnyTankChanged -= HandleAnyTankChanged;

        // Dung moi hieu ung + tra ve trang thai cuoi cung, tranh bi ket
        // giua chung (scale/mau lech, so dem do) khi object bi tat.
        StopAllEffects();
        ResetVisuals();
    }

    private void HandleAnyTankChanged()
    {
        RefreshStats(saveToServer: true);
    }

    // ============================================================
    // REFRESH
    // ============================================================

    private void RefreshStats(bool saveToServer)
    {
        int fishCount = 0;
        int totalFame = 0;
        List<AquariumTankEntry> tankEntries = new();

        foreach (AquariumDisplayManager tank in AquariumDisplayManager.AllTanks.Values)
        {
            if (tank == null || tank.CurrentItemId == null)
                continue;

            fishCount++;
            totalFame += GetFamePointForTank(tank);

            tankEntries.Add(new AquariumTankEntry(
                tank.TankId,
                tank.CurrentItemId,
                tank.CurrentSizeMultiplier));
        }

        int maxTanks = _maxTankCount > 0
            ? _maxTankCount
            : AquariumDisplayManager.AllTanks.Count;

        // Lan tinh dau tien (chua co gia tri truoc do) -> KHONG hieu ung.
        bool isFirst = _lastFishCount < 0;
        bool fishChanged = !isFirst && fishCount != _lastFishCount;
        bool fameChanged = !isFirst && totalFame != _lastTotalFame;

        int fameDelta = totalFame - _lastTotalFame;
        int fishDelta = fishCount - _lastFishCount;

        _lastFishCount = fishCount;
        _lastTotalFame = totalFame;

        UpdateFishCountText(fishCount, maxTanks, fishChanged, fishDelta);
        UpdateFameText(totalFame, fameChanged, fameDelta, isFirst);

        if (saveToServer)
            AquariumSaveService.SaveMuseumStats(totalFame, tankEntries);
    }

    private void UpdateFishCountText(int fishCount, int maxTanks, bool changed, int delta)
    {
        if (_fishCountText == null)
            return;

        // Text so ca doi NGAY (khong dem), chi kem hieu ung punch.
        _fishCountText.text = $"{fishCount}/{maxTanks}";

        if (changed && _enableEffects && isActiveAndEnabled)
        {
            RestartRoutine(
                ref _fishPunchRoutine,
                PunchRoutine(_fishCountText, _fishBaseScale, _fishBaseColor,
                             delta >= 0 ? _increaseColor : _decreaseColor));
        }
    }

    private void UpdateFameText(int totalFame, bool changed, int delta, bool isFirst)
    {
        if (_famePointText == null)
            return;

        bool animate = changed && _enableEffects && isActiveAndEnabled;

        if (!animate)
        {
            StopRoutine(ref _fameCountRoutine);
            _displayedFame = totalFame;
            _famePointText.text = totalFame.ToString();
            return;
        }

        Color flash = delta >= 0 ? _increaseColor : _decreaseColor;

        // Dem tu gia tri DANG HIEN THI (neu dang dem do thi tiep tuc tu
        // do, khong nhay ve gia tri cu).
        RestartRoutine(ref _fameCountRoutine, CountRoutine(_displayedFame, totalFame));
        RestartRoutine(ref _famePunchRoutine,
            PunchRoutine(_famePointText, _fameBaseScale, _fameBaseColor, flash));

        if (_fameDeltaText != null && delta != 0)
            RestartRoutine(ref _deltaRoutine, DeltaRoutine(delta, flash));
    }

    // ============================================================
    // EFFECT ROUTINES
    // ============================================================

    /// <summary>Dem so Fame tu 'from' sang 'to' voi ease-out.</summary>
    private IEnumerator CountRoutine(float from, float to)
    {
        float t = 0f;

        while (t < _countDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / Mathf.Max(0.0001f, _countDuration));
            float eased = 1f - Mathf.Pow(1f - p, 3f); // ease-out cubic

            _displayedFame = Mathf.Lerp(from, to, eased);
            _famePointText.text = Mathf.RoundToInt(_displayedFame).ToString();

            yield return null;
        }

        _displayedFame = to;
        _famePointText.text = Mathf.RoundToInt(to).ToString();
        _fameCountRoutine = null;
    }

    /// <summary>
    /// Phong to roi thu ve (theo sin) + nhay tu mau flash ve mau goc.
    /// </summary>
    private IEnumerator PunchRoutine(TMP_Text text, Vector3 baseScale, Color baseColor, Color flashColor)
    {
        RectTransform rt = text.rectTransform;
        float t = 0f;

        while (t < _punchDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / Mathf.Max(0.0001f, _punchDuration));

            float pulse = Mathf.Sin(p * Mathf.PI);              // 0 -> 1 -> 0
            rt.localScale = baseScale * (1f + _punchAmount * pulse);

            // Mau: bat dau o flashColor, mo dan ve baseColor
            text.color = Color.Lerp(flashColor, baseColor, p * p);

            yield return null;
        }

        rt.localScale = baseScale;
        text.color = baseColor;

        if (text == _fishCountText) _fishPunchRoutine = null;
        else if (text == _famePointText) _famePunchRoutine = null;
    }

    /// <summary>Chu '+N' / '-N' bay len va mo dan.</summary>
    private IEnumerator DeltaRoutine(int delta, Color color)
    {
        RectTransform rt = _fameDeltaText.rectTransform;

        _fameDeltaText.gameObject.SetActive(true);
        _fameDeltaText.text = delta > 0 ? $"+{delta}" : delta.ToString();

        float t = 0f;

        while (t < _deltaDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / Mathf.Max(0.0001f, _deltaDuration));
            float eased = 1f - Mathf.Pow(1f - p, 2f);

            rt.anchoredPosition = _deltaBasePos + new Vector2(0f, _deltaRiseDistance * eased);

            // Hien nhanh o 15% dau, sau do mo dan den het.
            float alpha = p < 0.15f ? p / 0.15f : 1f - (p - 0.15f) / 0.85f;
            Color c = color;
            c.a = Mathf.Clamp01(alpha);
            _fameDeltaText.color = c;

            yield return null;
        }

        rt.anchoredPosition = _deltaBasePos;
        _fameDeltaText.gameObject.SetActive(false);
        _deltaRoutine = null;
    }

    // ============================================================
    // ROUTINE HELPERS
    // ============================================================

    private void RestartRoutine(ref Coroutine handle, IEnumerator routine)
    {
        if (handle != null)
            StopCoroutine(handle);

        handle = StartCoroutine(routine);
    }

    private void StopRoutine(ref Coroutine handle)
    {
        if (handle != null)
        {
            StopCoroutine(handle);
            handle = null;
        }
    }

    private void StopAllEffects()
    {
        StopRoutine(ref _fishPunchRoutine);
        StopRoutine(ref _famePunchRoutine);
        StopRoutine(ref _fameCountRoutine);
        StopRoutine(ref _deltaRoutine);
    }

    /// <summary>Tra scale/mau/so ve trang thai cuoi cung (dung khi bi tat giua hieu ung).</summary>
    private void ResetVisuals()
    {
        if (!_baseCaptured)
            return;

        if (_fishCountText != null)
        {
            _fishCountText.rectTransform.localScale = _fishBaseScale;
            _fishCountText.color = _fishBaseColor;
        }

        if (_famePointText != null)
        {
            _famePointText.rectTransform.localScale = _fameBaseScale;
            _famePointText.color = _fameBaseColor;

            if (_lastTotalFame >= 0)
            {
                _displayedFame = _lastTotalFame;
                _famePointText.text = _lastTotalFame.ToString();
            }
        }

        if (_fameDeltaText != null)
        {
            _fameDeltaText.rectTransform.anchoredPosition = _deltaBasePos;
            _fameDeltaText.gameObject.SetActive(false);
        }
    }

    // ============================================================
    // FAME POINT PER TANK
    // ============================================================

    private int GetFamePointForTank(AquariumDisplayManager tank)
    {
        if (_itemCatalog == null)
        {
            Debug.LogWarning(
                "[MuseumStatsUI] Thieu _itemCatalog trong Inspector - " +
                "khong the tinh Fame Point cho be dang co ca."
            );
            return 0;
        }

        ItemData item = _itemCatalog.GetById(tank.CurrentItemId);

        if (item == null || !item.IsFish || item.FishInfo == null)
        {
            Debug.LogWarning(
                $"[MuseumStatsUI] Khong tim thay ItemData/FishData hop " +
                $"le cho ItemId='{tank.CurrentItemId}' (Tank " +
                $"{tank.TankId}) - bo qua khong tinh Fame Point cho be nay."
            );
            return 0;
        }

        float weightKg = item.FishInfo.GetWeightKg(tank.CurrentSizeMultiplier);

        return item.FishInfo.GetFamePoint(weightKg);
    }
}