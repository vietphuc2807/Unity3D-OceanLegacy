using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using DG.Tweening;
using XCharts.Runtime;

public class DayStatsXChartUI : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("References")]

    [SerializeField]
    private LineChart _lineChart;

    [SerializeField]
    private CanvasGroup _chartCanvasGroup;

    [SerializeField]
    private DayStatsPlayerCard _labelPrefab;


    // ============================================================
    // ICON
    // ============================================================

    [Header("Icon")]

    [SerializeField]
    private Sprite _defaultPlayerIcon;

    [SerializeField]
    private MonoBehaviour _iconProviderBehaviour;

    private IPlayerStatsIconProvider _iconProvider;


    // ============================================================
    // COLORS
    // ============================================================

    [Header("Line Colors")]

    [SerializeField]
    private Color32[] _lineColors = new Color32[]
    {
        new Color32(166, 89, 255, 255),
        new Color32(199, 61, 87, 255),
        new Color32(217, 191, 89, 255),
        new Color32(140, 217, 255, 255),
        new Color32(255, 140, 51, 255),
    };


    [Header("Money Colors")]

    [Tooltip("Tien duong luon mau xanh, khong phu thuoc mau duong bieu do.")]
    [SerializeField]
    private Color32 _positiveMoneyColor =
        new Color32(79, 219, 148, 255);

    [Tooltip("Tien am luon mau do, khong phu thuoc mau duong bieu do.")]
    [SerializeField]
    private Color32 _negativeMoneyColor =
        new Color32(255, 80, 80, 255);


    // ============================================================
    // Y AXIS STYLE
    // ============================================================

    [Header("Y Axis")]

    [Tooltip("Mau chu so tien o truc Y.")]
    [SerializeField]
    private Color32 _yAxisLabelColor =
        new Color32(150, 150, 150, 255);

    [Tooltip("Mau truc Y.")]
    [SerializeField]
    private Color32 _yAxisLineColor =
        new Color32(90, 90, 90, 80);

    [Tooltip("Mau ke ngang truc Y. Alpha thap de tao mau mo.")]
    [SerializeField]
    private Color32 _yAxisSplitLineColor =
        new Color32(255, 255, 255, 28);

    [SerializeField]
    private float _yAxisSplitLineWidth = 1f;


    // ============================================================
    // LAYOUT
    // ============================================================

    [Header("Layout")]

    [Tooltip(
        "Do lech ngang cua CARD so voi diem cuoi duong. " +
        "De 0 de ICON nam dung chinh xac len diem cuoi."
    )]
    [SerializeField]
    private float _labelOffsetX = 0f;


    [Tooltip(
        "Khoang cach nay KHONG con duoc dung de day card ra xa nhau. " +
        "Card co the chong len nhau de dam bao icon luon nam dung " +
        "tai diem cuoi cua duong bieu do."
    )]
    [SerializeField]
    private float _minLabelSpacing = 0f;


    // ============================================================
    // TIMING
    // ============================================================

    [Header("Timing")]

    [SerializeField]
    private float _fadeInDuration = 0.25f;


    [Tooltip(
        "Thoi gian MOI duong tu ve dan tu trai sang phai."
    )]
    [SerializeField]
    private float _lineRevealDuration = 1.2f;


    [Tooltip(
        "Do tre giua luc duong nay bat dau ve va duong tiep theo."
    )]
    [SerializeField]
    private float _staggerDelay = 0.15f;


    [Tooltip(
        "Thoi gian dem tien tu gia tri dau -> gia tri cuoi."
    )]
    [SerializeField]
    private float _moneyCountDuration = 0.6f;


    // ============================================================
    // RUNTIME
    // ============================================================

    private readonly List<DayStatsPlayerCard> _spawnedLabels =
        new List<DayStatsPlayerCard>();

    private Coroutine _playRoutine;

    private Sequence _moneyCountSequence;


    // ============================================================
    // PLAYER SERIES
    // ============================================================

    private struct PlayerSeries
    {
        public string PlayerName;
        public List<float> Points;
    }


    // ============================================================
    // AWAKE
    // ============================================================

    private void Awake()
    {
        _iconProvider =
            _iconProviderBehaviour as IPlayerStatsIconProvider;
    }


    // ============================================================
    // PLAY
    // ============================================================

    public void Play(
        DayStatsData data,
        Action onComplete)
    {
        Stop();

        ClearLabels();


        // --------------------------------------------------------
        // FADE CHART
        // --------------------------------------------------------

        if (_chartCanvasGroup != null)
        {
            _chartCanvasGroup.DOKill();

            _chartCanvasGroup.alpha = 0f;
        }


        // --------------------------------------------------------
        // VALIDATE
        // --------------------------------------------------------

        if (_lineChart == null ||
            data == null)
        {
            if (_chartCanvasGroup != null)
                _chartCanvasGroup.alpha = 1f;

            onComplete?.Invoke();

            return;
        }


        // --------------------------------------------------------
        // CLEAR OLD CHART DATA
        // --------------------------------------------------------

        _lineChart.RemoveData();


        // --------------------------------------------------------
        // BUILD PLAYER SERIES
        // --------------------------------------------------------

        List<PlayerSeries> series =
            BuildSeries(data.Transactions);


        if (series.Count == 0)
        {
            if (_chartCanvasGroup != null)
                _chartCanvasGroup.alpha = 1f;

            onComplete?.Invoke();

            return;
        }


        // --------------------------------------------------------
        // FIND MAX X
        // --------------------------------------------------------

        int maxX = 1;

        foreach (PlayerSeries s in series)
        {
            maxX =
                Mathf.Max(
                    maxX,
                    s.Points.Count - 1
                );
        }


        // --------------------------------------------------------
        // X AXIS DATA
        // --------------------------------------------------------

        for (int x = 0; x <= maxX; x++)
        {
            _lineChart.AddXAxisData(
                x.ToString()
            );
        }


        // ========================================================
        // Y AXIS
        // ========================================================

        YAxis yAxis =
            _lineChart.EnsureChartComponent<YAxis>();


        // --------------------------------------------------------
        // SHOW Y AXIS
        // --------------------------------------------------------

        yAxis.show = true;


        // --------------------------------------------------------
        // Y LABEL
        // --------------------------------------------------------

        yAxis.axisLabel.show = true;

        yAxis.axisLabel.formatter =
            "{value}$";


        // --------------------------------------------------------
        // Y AXIS LINE
        // --------------------------------------------------------

        yAxis.axisLine.show = true;

        yAxis.axisLine.lineStyle.color =
            _yAxisLineColor;


        // --------------------------------------------------------
        // Y AXIS TICK
        // --------------------------------------------------------

        yAxis.axisTick.show = false;


        // --------------------------------------------------------
        // HORIZONTAL GRID LINES
        // --------------------------------------------------------
        //
        // Quan trong:
        //
        // Luon bat splitLine tai day moi lan Play().
        // Khong de XCharts dung config cu cua prefab.
        //
        // Alpha thap -> ke ngang mo nhu hinh Client.
        //
        // --------------------------------------------------------

        yAxis.splitLine.show = true;

        yAxis.splitLine.lineStyle.color =
            _yAxisSplitLineColor;

        yAxis.splitLine.lineStyle.width =
            _yAxisSplitLineWidth;


        // ========================================================
        // X AXIS
        // ========================================================

        XAxis xAxis =
            _lineChart.EnsureChartComponent<XAxis>();


        // An hoan toan X axis.
        //
        // XCharts van dung category ben trong de ve duong,
        // nhung nguoi choi khong thay truc X.

        xAxis.show = false;

        xAxis.axisLabel.show = false;

        xAxis.axisLine.show = false;

        xAxis.axisTick.show = false;

        xAxis.splitLine.show = false;


        // ========================================================
        // FADE IN CHART
        // ========================================================

        if (_chartCanvasGroup != null)
        {
            _chartCanvasGroup.DOFade(
                1f,
                _fadeInDuration
            );
        }


        // ========================================================
        // CREATE SERIES
        // ========================================================

        var seriesIndexes =
            new List<int>();


        for (int i = 0; i < series.Count; i++)
        {
            Color32 color =
                _lineColors[
                    i % _lineColors.Length
                ];


            Serie serie =
                _lineChart.AddSerie<Line>(
                    series[i].PlayerName
                );


            // ----------------------------------------------------
            // LINE COLOR
            // ----------------------------------------------------

            serie.lineStyle.color =
                color;


            // ----------------------------------------------------
            // POINT SYMBOL
            // ----------------------------------------------------

            serie.symbol.show = true;


            // ----------------------------------------------------
            // FIRST POINT
            // ----------------------------------------------------

            _lineChart.AddData(
                serie.index,
                series[i].Points[0]
            );


            seriesIndexes.Add(
                serie.index
            );
        }


        // ========================================================
        // START REVEAL
        // ========================================================

        _playRoutine =
            StartCoroutine(
                PlaySeriesAndLabelsRoutine(
                    series,
                    seriesIndexes,
                    maxX,
                    onComplete
                )
            );
    }


    // ============================================================
    // PLAY SERIES + LABELS
    // ============================================================

    private IEnumerator PlaySeriesAndLabelsRoutine(
        List<PlayerSeries> series,
        List<int> seriesIndexes,
        int maxX,
        Action onComplete)
    {
        yield return new WaitForSeconds(
            _fadeInDuration
        );


        // ========================================================
        // START TIME
        // ========================================================

        float[] startTime =
            new float[series.Count];


        for (int i = 0; i < series.Count; i++)
        {
            startTime[i] =
                i * _staggerDelay;
        }


        // ========================================================
        // TOTAL DURATION
        // ========================================================

        float totalDuration =
            (series.Count - 1) *
            _staggerDelay +
            _lineRevealDuration;


        float elapsed = 0f;


        // ========================================================
        // NEXT POINT
        // ========================================================

        int[] nextPointIndex =
            new int[series.Count];


        for (int i = 0; i < series.Count; i++)
        {
            // Point 0 da duoc AddData() trong Play().
            nextPointIndex[i] = 1;
        }


        // ========================================================
        // REVEAL LOOP
        // ========================================================

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;


            for (int i = 0; i < series.Count; i++)
            {
                if (nextPointIndex[i] > maxX)
                    continue;


                float localElapsed =
                    elapsed -
                    startTime[i];


                if (localElapsed < 0f)
                    continue;


                float progress =
                    Mathf.Clamp01(
                        localElapsed /
                        _lineRevealDuration
                    );


                int targetIndex =
                    Mathf.Clamp(
                        Mathf.RoundToInt(
                            progress * maxX
                        ),
                        0,
                        maxX
                    );


                while (
                    nextPointIndex[i] <=
                    targetIndex
                )
                {
                    float y =
                        nextPointIndex[i] <
                        series[i].Points.Count
                            ? series[i].Points[
                                nextPointIndex[i]
                            ]
                            : series[i].Points[
                                series[i].Points.Count - 1
                            ];


                    _lineChart.AddData(
                        seriesIndexes[i],
                        y
                    );


                    nextPointIndex[i]++;
                }
            }


            yield return null;
        }


        // ========================================================
        // FORCE COMPLETE
        // ========================================================

        for (int i = 0; i < series.Count; i++)
        {
            while (
                nextPointIndex[i] <=
                maxX
            )
            {
                float y =
                    nextPointIndex[i] <
                    series[i].Points.Count
                        ? series[i].Points[
                            nextPointIndex[i]
                        ]
                        : series[i].Points[
                            series[i].Points.Count - 1
                        ];


                _lineChart.AddData(
                    seriesIndexes[i],
                    y
                );


                nextPointIndex[i]++;
            }
        }


        // ========================================================
        // WAIT FOR XCHARTS
        // ========================================================

        // Cho XCharts rebuild toa do pixel cua diem cuoi.
        yield return null;


        // ========================================================
        // PLACE CARDS
        // ========================================================

        PlaceLabels(
            series,
            seriesIndexes
        );


        onComplete?.Invoke();
    }


    // ============================================================
    // PLACE LABELS
    // ============================================================

    private void PlaceLabels(
        List<PlayerSeries> series,
        List<int> seriesIndexes)
    {
        // --------------------------------------------------------
        // Tao sequence dem tien.
        // --------------------------------------------------------

        _moneyCountSequence =
            DOTween.Sequence();


        // --------------------------------------------------------
        // Tao CARD cho tung player.
        // --------------------------------------------------------

        for (int i = 0; i < series.Count; i++)
        {
            Serie serie =
                _lineChart.GetSerie(
                    seriesIndexes[i]
                );


            if (serie == null ||
                serie.context.dataPoints.Count == 0)
            {
                continue;
            }


            // ====================================================
            // LAY DIEM CUOI CUNG CUA DUONG
            // ====================================================

            Vector3 lastPointLocal =
                serie.context.dataPoints[
                    serie.context.dataPoints.Count - 1
                ];


            // ====================================================
            // MONEY
            // ====================================================

            float startValue =
                series[i].Points[0];


            float finalTotal =
                series[i].Points[
                    series[i].Points.Count - 1
                ];


            Color32 lineColor =
                _lineColors[
                    i % _lineColors.Length
                ];


            // ====================================================
            // CREATE CARD
            // ====================================================

            DayStatsPlayerCard label =
                Instantiate(
                    _labelPrefab,
                    _lineChart.transform
                );


            _spawnedLabels.Add(
                label
            );


            SetupLabel(
                label,
                series[i].PlayerName,
                startValue,
                finalTotal,
                lineColor
            );


            RectTransform rt =
                label.GetComponent<RectTransform>();


            // ====================================================
            // IMPORTANT
            // ====================================================
            //
            // Pivot = CENTER
            //
            // Anchor = CENTER
            //
            // anchoredPosition = LAST POINT
            //
            // => TAM CARD nam tai diem cuoi.
            //
            // Neu icon trong prefab nam o giua card
            // thi ICON se nam CHINH XAC len diem cuoi.
            //
            // KHONG day card theo Y nua.
            //
            // 2 card co the CHONG LEN NHAU.
            //
            // ====================================================

            rt.anchorMin =
                new Vector2(
                    0.5f,
                    0.5f
                );

            rt.anchorMax =
                new Vector2(
                    0.5f,
                    0.5f
                );

            rt.pivot =
                new Vector2(
                    0.5f,
                    0.5f
                );


            rt.anchoredPosition =
                new Vector2(
                    lastPointLocal.x +
                    _labelOffsetX,
                    lastPointLocal.y
                );


            // ====================================================
            // CARD FADE
            // ====================================================

            CanvasGroup labelGroup =
                label.GetComponent<CanvasGroup>();


            if (labelGroup == null)
            {
                labelGroup =
                    label.gameObject.AddComponent<CanvasGroup>();
            }


            labelGroup.alpha = 0f;


            // ====================================================
            // FADE CARD
            // ====================================================

            labelGroup
                .DOFade(
                    1f,
                    0.3f
                )
                .SetDelay(
                    i * _staggerDelay
                );
        }


        // ========================================================
        // PLAY MONEY COUNT
        // ========================================================

        _moneyCountSequence.Play();
    }


    // ============================================================
    // STOP
    // ============================================================

    public void Stop()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(
                _playRoutine
            );

            _playRoutine = null;
        }


        if (_chartCanvasGroup != null)
        {
            _chartCanvasGroup.DOKill();
        }


        if (
            _moneyCountSequence != null &&
            _moneyCountSequence.IsActive()
        )
        {
            _moneyCountSequence.Kill();
        }
    }


    // ============================================================
    // FADE OUT
    // ============================================================

    public Tween FadeOutCards(
        float duration)
    {
        Stop();


        if (_chartCanvasGroup != null)
        {
            return _chartCanvasGroup.DOFade(
                0f,
                duration
            );
        }


        return DOVirtual.DelayedCall(
            0.01f,
            null
        );
    }


    // ============================================================
    // SETUP LABEL
    // ============================================================

    private void SetupLabel(
        DayStatsPlayerCard label,
        string playerName,
        float startValue,
        float finalTotal,
        Color lineColor)
    {
        // ========================================================
        // PLAYER NAME
        // ========================================================

        if (label.NameText != null)
        {
            label.NameText.text =
                playerName;
        }


        // ========================================================
        // PLAYER ICON
        // ========================================================

        if (label.IconImage != null)
        {
            Sprite icon =
                _iconProvider?
                    .GetIconForPlayer(
                        playerName
                    );


            label.IconImage.sprite =
                icon != null
                    ? icon
                    : _defaultPlayerIcon;


            label.IconImage.color =
                Color.white;
        }


        // ========================================================
        // MONEY
        // ========================================================

        if (label.MoneyText != null)
        {
            // ----------------------------------------------------
            // KHONG DUNG MAU CUA DUONG.
            //
            // + = XANH
            // - = DO
            // ----------------------------------------------------

            label.MoneyText.color =
                finalTotal >= 0f
                    ? _positiveMoneyColor
                    : _negativeMoneyColor;


            // ----------------------------------------------------
            // BAT DAU TU GIA TRI DAU
            // ----------------------------------------------------

            label.MoneyText.text =
                FormatMoney(
                    Mathf.RoundToInt(
                        startValue
                    )
                );


            // ----------------------------------------------------
            // COUNTING
            // ----------------------------------------------------

            float value =
                startValue;


            Tween countTween =
                DOTween.To(
                    () => value,
                    v =>
                    {
                        value = v;

                        label.MoneyText.text =
                            FormatMoney(
                                Mathf.RoundToInt(v)
                            );
                    },
                    finalTotal,
                    _moneyCountDuration
                )
                .SetEase(
                    Ease.OutCubic
                );


            _moneyCountSequence.Insert(
                0f,
                countTween
            );
        }
    }


    // ============================================================
    // FORMAT MONEY
    // ============================================================

    private static string FormatMoney(
        int value)
    {
        string sign =
            value < 0
                ? "- "
                : "+ ";


        return
            $"{sign}{Mathf.Abs(value):N0}$";
    }


    // ============================================================
    // BUILD SERIES
    // ============================================================
    //
    // Vi du:
    //
    // Transaction:
    //
    // A +20
    // B -10
    // A +5
    // B +3
    //
    // Ket qua:
    //
    // A: 0, 20, 20, 25, 25
    // B: 0,  0,-10,-10, -7
    //
    // Moi player dung chung MOT timeline.
    //
    // ============================================================

    private List<PlayerSeries> BuildSeries(
        List<DayStatsTransaction> transactions)
    {
        List<PlayerSeries> result =
            new List<PlayerSeries>();


        if (transactions == null ||
            transactions.Count == 0)
        {
            return result;
        }


        // ========================================================
        // PLAYER INDEX
        // ========================================================

        Dictionary<int, int> indexByPlayerId =
            new Dictionary<int, int>();


        // ========================================================
        // CREATE ALL PLAYERS
        // ========================================================

        for (int i = 0; i < transactions.Count; i++)
        {
            DayStatsTransaction tx =
                transactions[i];


            if (
                indexByPlayerId.ContainsKey(
                    tx.PlayerId
                )
            )
            {
                continue;
            }


            int index =
                result.Count;


            indexByPlayerId[
                tx.PlayerId
            ] = index;


            result.Add(
                new PlayerSeries
                {
                    PlayerName =
                        tx.PlayerName,

                    Points =
                        new List<float>
                        {
                            0f
                        }
                }
            );
        }


        // ========================================================
        // BUILD GLOBAL TIMELINE
        // ========================================================
        //
        // Moi transaction tao ra 1 moc X.
        //
        // Player nao khong giao dich:
        // => giu nguyen so tien hien tai.
        //
        // Player nao giao dich:
        // => cong Delta.
        //
        // ========================================================

        float[] currentValues =
            new float[result.Count];


        for (
            int transactionIndex = 0;
            transactionIndex < transactions.Count;
            transactionIndex++
        )
        {
            DayStatsTransaction tx =
                transactions[
                    transactionIndex
                ];


            int playerIndex =
                indexByPlayerId[
                    tx.PlayerId
                ];


            // ----------------------------------------------------
            // PLAYER GAY TRANSACTION
            // ----------------------------------------------------

            currentValues[playerIndex] +=
                tx.Delta;


            // ----------------------------------------------------
            // THEM DIEM CHO TAT CA PLAYER
            // ----------------------------------------------------

            for (
                int playerIndexLoop = 0;
                playerIndexLoop < result.Count;
                playerIndexLoop++
            )
            {
                result[
                    playerIndexLoop
                ].Points.Add(
                    currentValues[
                        playerIndexLoop
                    ]
                );
            }
        }


        // ========================================================
        // SAFETY
        // ========================================================
        //
        // Dam bao moi series co cung so luong point.
        //
        // ========================================================

        int expectedPointCount =
            transactions.Count + 1;


        for (
            int i = 0;
            i < result.Count;
            i++
        )
        {
            List<float> points =
                result[i].Points;


            while (
                points.Count <
                expectedPointCount
            )
            {
                float lastValue =
                    points.Count > 0
                        ? points[
                            points.Count - 1
                        ]
                        : 0f;


                points.Add(
                    lastValue
                );
            }
        }


        return result;
    }


    // ============================================================
    // CLEAR LABELS
    // ============================================================

    private void ClearLabels()
    {
        foreach (
            DayStatsPlayerCard label
            in _spawnedLabels
        )
        {
            if (label != null)
            {
                Destroy(
                    label.gameObject
                );
            }
        }


        _spawnedLabels.Clear();


        if (
            _moneyCountSequence != null &&
            _moneyCountSequence.IsActive()
        )
        {
            _moneyCountSequence.Kill();
        }
    }
}