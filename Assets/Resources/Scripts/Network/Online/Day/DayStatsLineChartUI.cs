using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Ve BIEU DO DUONG (line chart) so sanh tien kiem duoc CUA TUNG NGUOI
/// CHOI theo tien trinh giao dich trong ngay - dung thay cho khung the
/// (DayStatsChartUI) o "khung thu 3" cua DayStatsPanel.
///
/// CUNG PUBLIC API voi DayStatsChartUI (Play(DayStatsData, Action) /
/// Stop() / FadeOutCards(float): Tween) NEN CO THE GAN THANG vao truong
/// _chart cua DayStatsPanel de THAY THE - CHI CAN DOI KIEU (TYPE) CUA
/// FIELD DO tu DayStatsChartUI sang DayStatsLineChartUI, KHONG CAN SUA
/// GI KHAC trong DayStatsPanel.cs.
///
/// KHONG CAN CAI THEM PACKAGE NGOAI - duong duoc ve bang UILinePolyline
/// (tu viet, xem file UILinePolyline.cs).
///
/// ============================================================
/// TRUC X / TRUC Y
/// ============================================================
///
/// Truc X: DayStatsTransaction HIEN CHUA co timestamp that (chi co
/// PlayerId/PlayerName/Delta), nen truc X o day dung THU TU giao dich
/// trong TOAN BO ngay (tat ca nguoi choi dung CHUNG 1 truc). Neu sau
/// nay muon truc X la THOI GIAN THAT, can them field "float Timestamp"
/// vao DayStatsTransaction (ghi Time.time luc DayStatsRecorder.
/// RecordTransaction()), roi doi ham BuildSeries() ben duoi dung
/// Timestamp thay vi chi so i.
///
/// Truc Y: tong tien LUY KE cua RIENG nguoi choi do, tinh tu 0 dau
/// ngay. Duong cua MOI NGUOI duoc KEO DAI toi het truc X bang gia tri
/// cuoi cung cua ho (ke ca sau giao dich cuoi cung cua rieng ho) - de
/// moi duong deu ket thuc thang hang o canh phai, giong hinh mau tham
/// khao.
/// </summary>
public class DayStatsLineChartUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Vung ve duong - PHAI co kich thuoc CO DINH (KHONG dung Content Size Fitter) vi UILinePolyline doc rect.width/height moi lan ve lai.")]
    [SerializeField] private RectTransform _plotRect;

    [Tooltip("CanvasGroup bao quanh CA khung (duong + nhan) - DayStatsPanel fade in/out cai nay giong _cardsCanvasGroup cu cua DayStatsChartUI.")]
    [SerializeField] private CanvasGroup _chartCanvasGroup;

    [Tooltip("Prefab duong ke - 1 object rong co component UILinePolyline, RectTransform STRETCH full _plotRect (anchors 0,0 - 1,1, offset 0). Xem huong dan setup trong UILinePolyline.cs.")]
    [SerializeField] private UILinePolyline _linePrefab;

    [Tooltip("Prefab nhan cuoi duong (icon + ten + tien) - dung lai component DayStatsPlayerCard. KHAC voi luc dung cho khung the: nhan nay se duoc dat TRUC TIEP vao _plotRect (KHONG dat trong HorizontalLayoutGroup) va TU DINH VI theo chieu cao diem cuoi cua duong. Nen them san 1 CanvasGroup tren prefab nay (root object) de code khong phai AddComponent luc runtime.")]
    [SerializeField] private DayStatsPlayerCard _labelPrefab;

    [Header("Icon (giong DayStatsChartUI)")]
    [SerializeField] private Sprite _defaultPlayerIcon;

    [SerializeField] private MonoBehaviour _iconProviderBehaviour;

    private IPlayerStatsIconProvider _iconProvider;

    [Header("Colors")]
    [Tooltip("Mau gan san cho tung duong theo THU TU nguoi choi xuat hien trong ngay. Neu so nguoi choi nhieu hon so mau khai bao o day, nguoi con lai se tu sinh mau (chia deu vong mau HSV) de van phan biet duoc.")]
    [SerializeField]
    private Color[] _lineColors = new Color[]
    {
        new Color(0.65f, 0.35f, 1f),    // tim
        new Color(0.78f, 0.24f, 0.34f), // do dam
        new Color(0.85f, 0.75f, 0.35f), // vang dat
        new Color(0.55f, 0.85f, 1f),    // xanh nhat
        new Color(1f, 0.55f, 0.2f),     // cam
    };

    [SerializeField]
    private Color _negativeLabelColor = new Color(1f, 0.4f, 0.4f);

    [Header("Layout")]
    [SerializeField] private float _lineThickness = 4f;

    [Tooltip("Le tren/duoi, tinh theo % KHOANG GIA TRI (max-min), de duong khong dinh sat canh tren/duoi khung.")]
    [Range(0f, 0.3f)]
    [SerializeField] private float _verticalPadding = 0.12f;

    [Tooltip("Khoang cach toi thieu (pixel) giua 2 nhan ke nhau theo chieu doc - tranh chong chu khi 2 nguoi co tong tien gan bang nhau.")]
    [SerializeField] private float _minLabelSpacing = 26f;

    [Tooltip("Do lech ngang (pixel) cua nhan so voi canh phai _plotRect.")]
    [SerializeField] private float _labelOffsetX = 8f;

    [Header("Timing")]
    [SerializeField] private float _chartGroupFadeInDuration = 0.25f;

    [Tooltip("Thoi gian duong TU VE DAN tu trai sang phai + tien dem tu 0, cho MOI nguoi choi.")]
    [SerializeField] private float _lineRevealDuration = 0.8f;

    [Tooltip("Do tre giua luc duong nguoi nay bat dau ve va duong nguoi ke tiep bat dau. De 0 neu muon TAT CA duong ve CUNG LUC.")]
    [SerializeField] private float _staggerDelay = 0.15f;


    private readonly List<UILinePolyline> _spawnedLines = new List<UILinePolyline>();

    private readonly List<DayStatsPlayerCard> _spawnedLabels = new List<DayStatsPlayerCard>();

    private Sequence _playSequence;


    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        _iconProvider = _iconProviderBehaviour as IPlayerStatsIconProvider;
    }


    // ============================================================
    // PLAY (cung chu ky goi voi DayStatsChartUI.Play)
    // ============================================================

    public void Play(
        DayStatsData data,
        Action onComplete)
    {
        Stop();

        ClearSpawned();

        if (_chartCanvasGroup != null)
        {
            _chartCanvasGroup.DOKill();

            _chartCanvasGroup.alpha = 0f;
        }

        if (data == null ||
            _plotRect == null ||
            _linePrefab == null ||
            _labelPrefab == null)
        {
            if (_chartCanvasGroup != null)
                _chartCanvasGroup.alpha = 1f;

            onComplete?.Invoke();

            return;
        }

        List<PlayerSeries> series = BuildSeries(data.Transactions);

        if (series.Count == 0)
        {
            if (_chartCanvasGroup != null)
                _chartCanvasGroup.alpha = 1f;

            onComplete?.Invoke();

            return;
        }

        // Truc Y dung CHUNG cho MOI duong - tim min/max qua TAT CA diem
        // cua TAT CA nguoi choi (bao gom ca diem xuat phat 0).
        float minY = 0f;
        float maxY = 0f;

        foreach (PlayerSeries s in series)
        {
            foreach (Vector2 p in s.Points)
            {
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }
        }

        if (Mathf.Approximately(minY, maxY))
        {
            // Tat ca bang nhau (VD ca ngay chi co 1 giao dich duy nhat) -
            // nong bien gia de duong khong bi ve dep thanh 1 net ngang
            // dinh giua khung.
            maxY += 1f;
        }

        int maxX = 1;

        foreach (PlayerSeries s in series)
        {
            Vector2 last = s.Points[s.Points.Count - 1];

            maxX = Mathf.Max(maxX, Mathf.RoundToInt(last.x));
        }

        _playSequence = DOTween.Sequence();

        if (_chartCanvasGroup != null)
        {
            _playSequence.Insert(
                0f,
                _chartCanvasGroup.DOFade(1f, _chartGroupFadeInDuration)
            );
        }

        // Ke hoach dat nhan - gom vi tri Y (pixel, tinh tu tam _plotRect)
        // cua diem CUOI CUNG tung nguoi, dung de: (1) dat nhan, (2) chong
        // chong chu giua cac nhan (xem ResolveLabelOverlap).
        List<LabelPlan> labelPlan = new List<LabelPlan>();

        for (int i = 0; i < series.Count; i++)
        {
            PlayerSeries s = series[i];

            Color lineColor = GetColorForIndex(i, series.Count);

            UILinePolyline line = Instantiate(_linePrefab, _plotRect);
            line.color = lineColor;
            line.Thickness = _lineThickness;
            line.Clear();
            _spawnedLines.Add(line);

            DayStatsPlayerCard label = Instantiate(_labelPrefab, _plotRect);
            _spawnedLabels.Add(label);

            SetupLabel(label, s.PlayerName, s.FinalTotal, lineColor);

            Vector2[] normalizedPoints =
                NormalizePoints(s.Points, maxX, minY, maxY);

            float finalYNormalized =
                normalizedPoints[normalizedPoints.Length - 1].y;

            float targetPixelY =
                (finalYNormalized - 0.5f) * _plotRect.rect.height;

            CanvasGroup labelGroup = label.GetComponent<CanvasGroup>();

            if (labelGroup == null)
                labelGroup = label.gameObject.AddComponent<CanvasGroup>();

            labelGroup.alpha = 0f;

            labelPlan.Add(
                new LabelPlan
                {
                    Label = label,
                    TargetY = targetPixelY,
                }
            );

            float startTime =
                _chartGroupFadeInDuration +
                i * _staggerDelay;

            _playSequence.Insert(
                startTime,
                AnimateLineReveal(
                    line,
                    labelGroup,
                    label.MoneyText,
                    normalizedPoints,
                    s.FinalTotal
                )
            );
        }

        ResolveLabelOverlap(labelPlan);

        foreach (LabelPlan plan in labelPlan)
        {
            RectTransform rt = plan.Label.GetComponent<RectTransform>();

            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(_labelOffsetX, plan.TargetY);
        }

        _playSequence.OnComplete(
            () => onComplete?.Invoke()
        );

        _playSequence.Play();
    }


    public void Stop()
    {
        if (_playSequence != null && _playSequence.IsActive())
            _playSequence.Kill();
    }


    /// <summary>Fade ca khung ve 0 - dung boi DayStatsPanel khi dong panel.</summary>
    public Tween FadeOutCards(
        float duration)
    {
        Stop();

        if (_chartCanvasGroup == null)
            return DOVirtual.DelayedCall(0.01f, null);

        return _chartCanvasGroup.DOFade(0f, duration);
    }


    // ============================================================
    // BUILD SERIES (gop giao dich theo TUNG NGUOI CHOI THUC SU - dung
    // PlayerId giong AggregateByPlayer cua DayStatsChartUI - va tinh
    // LUY KE theo THU TU giao dich trong toan bo ngay)
    // ============================================================

    private struct PlayerSeries
    {
        public int PlayerId;

        public string PlayerName;

        public int FinalTotal;

        /// <summary>x = vi tri giao dich (0..n, dung chung cho ca team), y = tong luy ke cua rieng nguoi nay.</summary>
        public List<Vector2> Points;
    }


    private List<PlayerSeries> BuildSeries(
        List<DayStatsTransaction> transactions)
    {
        List<PlayerSeries> result = new List<PlayerSeries>();

        Dictionary<int, int> indexByPlayerId = new Dictionary<int, int>();

        int lastGlobalX = Mathf.Max(1, transactions.Count);

        for (int i = 0; i < transactions.Count; i++)
        {
            DayStatsTransaction tx = transactions[i];

            int globalX = i + 1; // x = 0 danh rieng cho diem xuat phat (0 tien)

            if (!indexByPlayerId.TryGetValue(tx.PlayerId, out int idx))
            {
                idx = result.Count;

                indexByPlayerId[tx.PlayerId] = idx;

                result.Add(
                    new PlayerSeries
                    {
                        PlayerId = tx.PlayerId,
                        PlayerName = tx.PlayerName,
                        FinalTotal = 0,
                        Points = new List<Vector2> { new Vector2(0f, 0f) },
                    }
                );
            }

            PlayerSeries series = result[idx];

            series.FinalTotal += tx.Delta;

            series.Points.Add(new Vector2(globalX, series.FinalTotal));

            result[idx] = series;
        }

        // Keo dai duong cua tung nguoi toi het truc X (x = lastGlobalX),
        // giu nguyen gia tri cuoi cung cua ho - de MOI duong deu ket
        // thuc thang hang o canh phai, giong hinh mau.
        for (int i = 0; i < result.Count; i++)
        {
            PlayerSeries s = result[i];

            Vector2 lastPoint = s.Points[s.Points.Count - 1];

            if (lastPoint.x < lastGlobalX)
            {
                s.Points.Add(new Vector2(lastGlobalX, lastPoint.y));
            }

            result[i] = s;
        }

        return result;
    }


    private Vector2[] NormalizePoints(
        List<Vector2> points,
        int maxX,
        float minY,
        float maxY)
    {
        Vector2[] result = new Vector2[points.Count];

        float rangeY = Mathf.Max(0.0001f, maxY - minY);

        float paddedMin = minY - rangeY * _verticalPadding;
        float paddedMax = maxY + rangeY * _verticalPadding;
        float paddedRange = Mathf.Max(0.0001f, paddedMax - paddedMin);

        for (int i = 0; i < points.Count; i++)
        {
            float nx = maxX <= 0 ? 0f : points[i].x / maxX;
            float ny = (points[i].y - paddedMin) / paddedRange;

            result[i] = new Vector2(nx, ny);
        }

        return result;
    }


    // ============================================================
    // REVEAL ANIMATION (duong ve dan tu trai sang phai, dong bo voi
    // tien dem tu 0 -> tong cuoi cung tren nhan)
    // ============================================================

    private Tween AnimateLineReveal(
        UILinePolyline line,
        CanvasGroup labelGroup,
        TextMeshProUGUI moneyText,
        Vector2[] fullNormalizedPoints,
        int finalTotal)
    {
        float t = 0f;

        return DOTween.To(
                () => t,
                v =>
                {
                    t = v;

                    if (fullNormalizedPoints.Length < 2)
                    {
                        line.SetPoints(fullNormalizedPoints);
                    }
                    else
                    {
                        int visibleCount =
                            Mathf.Clamp(
                                Mathf.CeilToInt(fullNormalizedPoints.Length * v),
                                2,
                                fullNormalizedPoints.Length
                            );

                        Vector2[] slice = new Vector2[visibleCount];

                        Array.Copy(fullNormalizedPoints, slice, visibleCount);

                        line.SetPoints(slice);
                    }

                    if (moneyText != null)
                        moneyText.text =
                            FormatMoney(Mathf.RoundToInt(Mathf.Lerp(0, finalTotal, v)));

                    if (labelGroup != null)
                        labelGroup.alpha = v;
                },
                1f,
                _lineRevealDuration
            )
            .SetEase(Ease.OutCubic);
    }


    // ============================================================
    // LABELS
    // ============================================================

    private struct LabelPlan
    {
        public DayStatsPlayerCard Label;

        public float TargetY;
    }


    private void SetupLabel(
        DayStatsPlayerCard label,
        string playerName,
        int total,
        Color lineColor)
    {
        if (label.NameText != null)
            label.NameText.text = playerName;

        if (label.IconImage != null)
        {
            Sprite icon = _iconProvider?.GetIconForPlayer(playerName);

            label.IconImage.sprite =
                icon != null ? icon : _defaultPlayerIcon;

            label.IconImage.color = Color.white;
        }

        if (label.MoneyText != null)
        {
            // Dung MAU DUONG cho gia tri duong, de nguoi choi de doi
            // chieu icon/ten <-> duong tren bieu do; gia tri am van dung
            // 1 mau do rieng cho de nhan bien.
            label.MoneyText.color =
                total < 0 ? _negativeLabelColor : lineColor;

            label.MoneyText.text = FormatMoney(0);
        }
    }


    /// <summary>
    /// Day cac nhan bi chong nhau (gia tri gan bang nhau) ra xa nhau theo
    /// chieu doc, giu thu tu tu tren xuong duoi theo TargetY ban dau.
    /// </summary>
    private void ResolveLabelOverlap(
        List<LabelPlan> plan)
    {
        if (plan.Count <= 1)
            return;

        plan.Sort((a, b) => b.TargetY.CompareTo(a.TargetY)); // cao -> thap

        for (int i = 1; i < plan.Count; i++)
        {
            float maxAllowedY = plan[i - 1].TargetY - _minLabelSpacing;

            if (plan[i].TargetY > maxAllowedY)
            {
                LabelPlan item = plan[i];

                item.TargetY = maxAllowedY;

                plan[i] = item;
            }
        }
    }


    private Color GetColorForIndex(
        int index,
        int totalCount)
    {
        if (_lineColors != null && index < _lineColors.Length)
            return _lineColors[index];

        float hue = totalCount <= 0 ? 0f : (index / (float)totalCount);

        return Color.HSVToRGB(hue, 0.6f, 0.95f);
    }


    private static string FormatMoney(
        int value)
    {
        string sign = value < 0 ? "- " : "+ ";

        return $"{sign}{Mathf.Abs(value):N0}$";
    }


    // ============================================================
    // CLEAR
    // ============================================================

    private void ClearSpawned()
    {
        for (int i = 0; i < _spawnedLines.Count; i++)
        {
            if (_spawnedLines[i] != null)
                Destroy(_spawnedLines[i].gameObject);
        }

        _spawnedLines.Clear();

        for (int i = 0; i < _spawnedLabels.Count; i++)
        {
            if (_spawnedLabels[i] != null)
                Destroy(_spawnedLabels[i].gameObject);
        }

        _spawnedLabels.Clear();
    }
}