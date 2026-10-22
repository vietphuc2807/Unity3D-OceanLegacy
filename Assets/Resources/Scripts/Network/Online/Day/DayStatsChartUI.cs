using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Hien danh sach "card" cho TUNG nguoi choi (icon + ten + tong so tien
/// kiem duoc trong ngay, co hieu ung dem so tu 0 -> gia tri that) trong
/// khung thu 3 cua DayStatsPanel. Moi nguoi choi CHI co 1 card duy nhat,
/// gom TONG tat ca DayStatsTransaction cua nguoi do trong ngay.
///
/// GOP THEO PlayerId (khong phai PlayerName) - 2 nguoi choi CO THE trung
/// ten hien thi (VD chua kip doi ten khoi mac dinh "Player"), nhung van
/// PHAI hien ra 2 card rieng biet vi ho la 2 nguoi choi khac nhau. Ten
/// hien thi CHI dung de HIEN THI len card, khong dung de gop.
///
/// Cac card duoc Instantiate dong vao _cardsContainer va TU DONG canh
/// deu/can giua du la 1, 2, 3 hay 4 nguoi choi - nho component
/// HorizontalLayoutGroup gan tren _cardsContainer.
///
/// MOI: them _cardsCanvasGroup bao quanh _cardsContainer, de
/// DayStatsPanel co the fade in/out CA KHUNG THE (rieng voi hieu ung
/// dem tien tung card) luc mo/dong Stats Panel - xem DayStatsPanel.cs.
/// SETUP: gan CanvasGroup nay len chinh object chua/la cha cua
/// _cardsContainer trong Inspector.
/// </summary>
public class DayStatsChartUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Object chua cac card - phai co component HorizontalLayoutGroup " +
             "(Child Alignment: Middle Center) de tu canh deu cac card.")]
    [SerializeField] private RectTransform _cardsContainer;

    [Tooltip("CanvasGroup bao quanh _cardsContainer (co the la cung object hoac " +
             "object cha) - dung de DayStatsPanel fade in/out CA KHUNG khi mo/dong.")]
    [SerializeField] private CanvasGroup _cardsCanvasGroup;

    [Tooltip("Prefab 1 card cho 1 nguoi choi (icon + ten + tien).")]
    [SerializeField] private DayStatsPlayerCard _playerCardPrefab;

    [Header("Icon")]
    [Tooltip("Icon mac dinh dung khi khong co icon rieng cho nguoi choi do (xem _iconProviderBehaviour).")]
    [SerializeField] private Sprite _defaultPlayerIcon;

    [Tooltip("(Tuy chon) component implement IPlayerStatsIconProvider de tra ve " +
             "icon rieng cho tung nguoi choi. De trong neu chua co he thong " +
             "avatar - moi nguoi se dung chung _defaultPlayerIcon.")]
    [SerializeField] private MonoBehaviour _iconProviderBehaviour;

    private IPlayerStatsIconProvider _iconProvider;

    [Header("Colors")]
    [SerializeField] private Color _positiveColor =
        new Color(0.4392f, 1f, 0.4392f);

    [SerializeField] private Color _negativeColor =
        new Color(1f, 0.2941f, 0.2941f);

    [Header("Timing")]
    [Tooltip("Thoi gian fade CA KHUNG the (_cardsCanvasGroup) khi bat dau Play().")]
    [SerializeField] private float _cardsGroupFadeInDuration = 0.25f;

    [Tooltip("Thoi gian dem tien tu 0 -> gia tri that cua MOI card.")]
    [SerializeField] private float _countDuration = 0.8f;

    [Tooltip("Do tre giua luc card nay bat dau dem va card ke tiep bat dau dem. " +
             "De 0 neu muon tat ca card dem tien CUNG LUC.")]
    [SerializeField] private float _staggerDelay = 0.15f;


    private readonly List<DayStatsPlayerCard> _spawnedCards =
        new List<DayStatsPlayerCard>();

    private Sequence _playSequence;


    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        _iconProvider = _iconProviderBehaviour as IPlayerStatsIconProvider;
    }


    // ============================================================
    // PLAY
    // ============================================================

    /// <summary>Fade khung the, spawn 1 card/nguoi choi va dem tien tu 0. onComplete goi khi TAT CA xong.</summary>
    public void Play(
        DayStatsData data,
        Action onComplete)
    {
        Stop();

        ClearCards();

        if (_cardsCanvasGroup != null)
        {
            _cardsCanvasGroup.DOKill();

            _cardsCanvasGroup.alpha = 0f;
        }

        if (data == null ||
            _cardsContainer == null ||
            _playerCardPrefab == null)
        {
            if (_cardsCanvasGroup != null)
                _cardsCanvasGroup.alpha = 1f;

            onComplete?.Invoke();

            return;
        }

        List<(int playerId, string name, int total)> perPlayer =
            AggregateByPlayer(data.Transactions);

        if (perPlayer.Count == 0)
        {
            if (_cardsCanvasGroup != null)
                _cardsCanvasGroup.alpha = 1f;

            onComplete?.Invoke();

            return;
        }

        _playSequence = DOTween.Sequence();

        // Fade ca khung the vao truoc/song song voi luc cac card bat dau dem.
        if (_cardsCanvasGroup != null)
        {
            _playSequence.Insert(
                0f,
                _cardsCanvasGroup.DOFade(1f, _cardsGroupFadeInDuration)
            );
        }

        for (int i = 0; i < perPlayer.Count; i++)
        {
            (int playerId, string name, int total) entry = perPlayer[i];

            DayStatsPlayerCard card =
                Instantiate(_playerCardPrefab, _cardsContainer);

            _spawnedCards.Add(card);

            SetupCard(card, entry.name, entry.total);

            float startTime =
                _cardsGroupFadeInDuration +
                i * _staggerDelay;

            _playSequence.Insert(
                startTime,
                AnimateCardMoney(card, entry.total)
            );
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


    /// <summary>Fade ca khung the ve 0 - dung boi DayStatsPanel khi dong panel.</summary>
    public Tween FadeOutCards(
        float duration)
    {
        Stop();

        if (_cardsCanvasGroup == null)
            return DOVirtual.DelayedCall(0.01f, null);

        return _cardsCanvasGroup.DOFade(0f, duration);
    }


    // ============================================================
    // AGGREGATE (gop tat ca giao dich trong ngay theo TUNG NGUOI CHOI
    // THUC SU - dung PlayerId, KHONG dung PlayerName, vi 2 nguoi co the
    // trung ten hien thi VD chua kip doi ten khoi mac dinh "Player")
    // ============================================================

    private List<(int playerId, string name, int total)> AggregateByPlayer(
        List<DayStatsTransaction> transactions)
    {
        List<(int playerId, string name, int total)> result =
            new List<(int playerId, string name, int total)>();

        Dictionary<int, int> indexByPlayerId =
            new Dictionary<int, int>();

        foreach (DayStatsTransaction tx in transactions)
        {
            if (indexByPlayerId.TryGetValue(tx.PlayerId, out int index))
            {
                (int playerId, string name, int total) existing = result[index];

                result[index] = (existing.playerId, existing.name, existing.total + tx.Delta);
            }
            else
            {
                indexByPlayerId[tx.PlayerId] = result.Count;

                result.Add((tx.PlayerId, tx.PlayerName, tx.Delta));
            }
        }

        return result;
    }


    // ============================================================
    // CARD SETUP / ANIMATION
    // ============================================================

    private void SetupCard(
        DayStatsPlayerCard card,
        string playerName,
        int total)
    {
        if (card.NameText != null)
            card.NameText.text = playerName;

        if (card.IconImage != null)
        {
            Sprite icon = _iconProvider?.GetIconForPlayer(playerName);

            card.IconImage.sprite =
                icon != null ? icon : _defaultPlayerIcon;
        }

        if (card.MoneyText != null)
        {
            card.MoneyText.color =
                total < 0 ? _negativeColor : _positiveColor;

            card.MoneyText.text = FormatMoney(0);
        }
    }


    private Tween AnimateCardMoney(
        DayStatsPlayerCard card,
        int total)
    {
        float value = 0f;

        return DOTween.To(
                () => value,
                v =>
                {
                    value = v;

                    if (card.MoneyText != null)
                        card.MoneyText.text =
                            FormatMoney(Mathf.RoundToInt(v));
                },
                (float)total,
                _countDuration
            )
            .SetEase(Ease.OutCubic);
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

    private void ClearCards()
    {
        for (int i = 0; i < _spawnedCards.Count; i++)
        {
            if (_spawnedCards[i] != null)
                Destroy(_spawnedCards[i].gameObject);
        }

        _spawnedCards.Clear();
    }
}