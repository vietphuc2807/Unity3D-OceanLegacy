using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Panel "STATS FOR DAY X" - CHI hien khi team VUOT chi tieu tien qua
/// ngay moi (xem GameDayManager.EvaluateDayEnd() nhanh THANH CONG). Neu
/// THAT BAI thi KHONG qua panel nay - GameDayManager se goi thang
/// RPC_ShowDayResultPanel nhu flow cu (hien GameDayIntroPanel ve Day 1).
///
/// FLOW (khi THANH CONG):
///
///     Host: EvaluateDayEnd() thanh cong
///         -> BeginWaitingForStatsAck() + RPC_ShowDayStatsPanel(day, payload)
///         -> panel nay Show() tren MOI client
///         -> nguoi choi bam "E" -> GameplayPlayer.RPC_ReportStatsAcknowledged()
///         -> Host dem so nguoi da bam (GameDayManager.PlayersAckedStatsCount)
///         -> khi DU nguoi (hoac fallback - xem GameDayManager._useStatsAckFallback)
///            -> Host set IsWaitingForStatsAck = false
///         -> panel nay (dang poll co IsWaitingForStatsAck moi Update())
///            tu dong chay CLOSE SEQUENCE (xem duoi) roi bien mat
///         -> Host moi goi RPC_ShowDayResultPanel(...) nhu flow cu -> Day
///            Panel (GameDayIntroPanel) hien len, ngay moi thuc su bat dau.
///
/// ============================================================
/// KHOA INPUT NGUOI CHOI LUC PANEL DANG HIEN
/// ============================================================
///
/// Panel nay dung chung co PlayerCursor.IsUIOpen voi cac UI khac (Bait
/// Shop, Rod Upgrade...) de:
///     - NetPlayerLook.Update() dung xoay camera theo chuot.
///     - NetPlayerMovementFusion.FixedUpdateNetwork() dung nhan input
///       di chuyen (xem NetworkInputData.IsUIOpen).
///     - DoorInteractable.Update() an prompt "E TO OPEN/CLOSE DOOR" va
///       khong nhan phim E, tranh nguoi choi lo bam E vua confirm Stats
///       Panel vua mo/dong cua cung luc.
///
/// Bat luc Show() (panel bat dau hien), tat luc Hide() DA XONG HOAN
/// TOAN (cuoi _closeSequence), KHONG tat ngay luc bam "E" - vi sau khi
/// bam "E" panel van con dang chay close sequence, doi player van chua
/// nen dieu khien duoc trong luc do.
///
/// ============================================================
/// HIEU UNG MO / DONG
/// ============================================================
///
/// CHI CO 3 CANVASGROUP CAN GAN THEM (moi khung 1 cai, KHONG can nhieu
/// hon): _progressSectionGroup (khung 1), _infoRowGroup (khung 2),
/// _chart._chartCanvasGroup (khung 3, gan ben trong DayStatsLineChartUI -
/// truoc day la _chart._cardsCanvasGroup cua DayStatsChartUI, gio doi
/// component nhung van 1 CanvasGroup bao quanh khung 3, cach dung y het).
///
/// Rieng hieu ung "lan luot" BEN TRONG khung 2 (Target -> Fish ->
/// Earnings) KHONG dung them CanvasGroup con nao ca - dung thang
/// TextMeshProUGUI.DOFade() tren tung Text gia tri (_infoTargetText,
/// _infoFishText, _infoEarningsText), vi TextMeshProUGUI ke thua tu
/// Graphic nen DOTween ho tro fade truc tiep, khong can wrapper.
///
/// FIX/UPDATE - REVEAL DEP HON:
/// Ca RevealGroup() (khung 1 + khoi khung 2) va RevealTextAlpha() (tung
/// gia tri TARGET/FISH/EARNINGS) gio KHONG con fade suong nua, ma them
/// hieu ung TRUOT LEN + PHONG TO CO NAY (overshoot) + o RevealTextAlpha
/// con co them 1 cu "NAY" nho (punch scale) o cuoi de nhin song dong,
/// noi bat hon - xem comment ngay tren tung ham.
///
/// MO (PlayOpenSequence):
///     1. Ca panel fade in (_canvasGroup 0 -> 1)
///     2. Khung TARGET + progress bar fade in (_progressSectionGroup)
///     3. Progress bar chay 0% -> 100%
///     4. Khung 2 (_infoRowGroup) fade in CA KHOI (nhan chu TARGET/FISH/
///        EARNINGS hien ra), nhung 3 gia tri $ / so luong dang alpha = 0
///        tu truoc do nen CHUA thay - roi lan luot fade tung gia tri:
///        Target -> Fish -> Earnings.
///     5. Khung 3 (bieu do duong tung nguoi choi) fade in + ve dan +
///        dem tien (xem DayStatsLineChartUI.Play())
///     6. Sau khi TAT CA xong -> moi hien "(PRESS E TO CONTINUE)"
///
/// DONG (Hide, khi Host bao IsWaitingForStatsAck = false):
///     Lam NGUOC LAI - khung 3 mo dan truoc, roi khung 2 (ca khoi, 1
///     fade duy nhat), roi khung 1, CUOI CUNG moi la ca panel fade het
///     alpha = 0. Muc dich: khi GameDayIntroPanel (den) phu len tren,
///     noi dung Stats Panel da "bien mat" gan het truoc do, tranh cam
///     giac 2 panel de chong len nhau.
///
///     LUU Y SETUP: de hieu ung nay muot, GameDayIntroPanel (panel den
///     DAY X) PHAI nam o Canvas/sibling index CAO HON (ve sau/tren)
///     DayStatsPanel trong Hierarchy, vi no la lop phu toan man hinh.
///     I-Background (anh nen chung ca panel) la con truc tiep cua object
///     gan _canvasGroup nen se TU fade cung buoc cuoi cung (buoc 6 khi
///     mo / buoc dau khi doc nguoc luc dong) - KHONG can xu ly rieng.
///
/// ============================================================
/// KHUNG 3 (FIX/UPDATE)
/// ============================================================
///
/// Truoc day khung 3 dung DayStatsChartUI (moi nguoi choi 1 "the" xep
/// hang ngang, dem tien tu 0). Gio doi sang DayStatsLineChartUI (bieu do
/// duong - moi nguoi choi 1 duong theo tien trinh giao dich trong ngay,
/// ket thuc bang icon+ten+tong tien o cuoi duong) vi API 2 component nay
/// GIONG HET NHAU (Play/Stop/FadeOutCards), nen ĐÂY LÀ THAY ĐỔI DUY NHẤT
/// trong file nay so voi truoc: kieu cua field _chart doi tu
/// DayStatsChartUI -> DayStatsLineChartUI. Moi logic goi _chart ben duoi
/// (PlayOpenSequence, HandleChartFinished, Hide, AppendChartFadeOut) giu
/// nguyen KHONG DOI mot dong nao.
/// </summary>
public class DayStatsPanel : MonoBehaviour
{
    public static DayStatsPanel Instance { get; private set; }

    [Header("Root")]
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Title")]
    [SerializeField] private TextMeshProUGUI _titleText;

    [Header("Target Progress")]
    [Tooltip("Bao quanh chu TARGET + gia tri $ + thanh progress bar (khung tren cung) - dung de fade in luc mo panel.")]
    [SerializeField] private CanvasGroup _progressSectionGroup;

    [Tooltip("Text '$X' nam canh chu TARGET, NGAY TREN progress bar. Truoc day bi thieu binding nen luon hien $0.")]
    [SerializeField] private TextMeshProUGUI _progressTargetValueText;

    [SerializeField] private Image _progressFillImage;
    [SerializeField] private TextMeshProUGUI _progressPercentText;

    [Header("Info Row")]
    [Tooltip("CHI 1 CanvasGroup bao quanh CA khung 2 (Target/Fish/Earnings) - dung de fade CA KHOI luc mo/dong.")]
    [SerializeField] private CanvasGroup _infoRowGroup;

    [SerializeField] private TextMeshProUGUI _infoTargetText;
    [SerializeField] private TextMeshProUGUI _infoFishText;
    [SerializeField] private TextMeshProUGUI _infoEarningsText;

    [Header("Chart")]
    // FIX: doi kieu tu DayStatsChartUI -> DayStatsLineChartUI (khung the
    // -> bieu do duong). 2 component co API giong het nhau nen CHI CAN
    // doi kieu field nay, gan lai reference trong Inspector la xong,
    // khong can sua gi them ben duoi.
    [SerializeField] private DayStatsXChartUI _chart;

    [Header("Continue")]
    [SerializeField] private GameObject _pressEHint;
    [SerializeField] private TextMeshProUGUI _waitingForPlayersText;
    [SerializeField] private KeyCode _continueKey = KeyCode.E;

    [Header("Timing - Open")]
    [SerializeField] private float _fadeDuration = 0.35f;
    [SerializeField] private float _progressFillDuration = 0.8f;
    [Tooltip("Thoi gian fade cho khung TARGET+progress, cho CA KHOI khung 2, va cho MOI gia tri rieng le trong khung 2.")]
    [SerializeField] private float _sectionRevealFadeDuration = 0.3f;
    [Tooltip("Khoang tre giua luc 1 GIA TRI trong khung 2 bat dau fade va gia tri ke tiep bat dau (Target -> Fish -> Earnings).")]
    [SerializeField] private float _infoItemStaggerDelay = 0.2f;

    [Header("Timing - Close")]
    [SerializeField] private float _closeItemFadeDuration = 0.2f;
    [Tooltip("Khoang tre giua cac khung khi dong (khung 3 -> khung 2 -> khung 1).")]
    [SerializeField] private float _closeItemStagger = 0.08f;

    [Header("Reveal FX (khung TARGET/FISH/EARNINGS + cac khung group)")]
    [Tooltip("Khoang cach truot len (pixel) luc cac khung/gia tri bat dau xuat hien.")]
    [SerializeField] private float _revealSlideDistance = 14f;
    [Tooltip("Scale ban dau cua TUNG GIA TRI (Target/Fish/Earnings) truoc khi phong to ve 1 - cang nho cang 'giat minh' manh.")]
    [SerializeField] private float _revealStartScale = 0.55f;
    [Tooltip("Do 'nay' (overshoot) khi gia tri phong to ve 1 - cang lon cang nay manh qua khoi 1 roi mem lai.")]
    [SerializeField] private float _revealOvershoot = 3.2f;
    [Tooltip("Bien do cu 'nay' nho (punch scale) sau khi 1 gia tri da hien xong hoan toan.")]
    [SerializeField] private float _revealPunchScale = 0.12f;
    [SerializeField] private float _revealPunchDuration = 0.22f;


    private bool _isVisible;

    private bool _localAcked;

    private Sequence _openSequence;

    private Sequence _closeSequence;


    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        Instance = this;

        if (_panelRoot != null)
            _panelRoot.SetActive(false);

        SetCanvasGroupInstant(_canvasGroup, 0f);
        SetCanvasGroupInstant(_progressSectionGroup, 0f);
        SetCanvasGroupInstant(_infoRowGroup, 0f);
    }


    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        KillAllTweens();

        // De phong panel bi destroy (VD doi scene) trong luc dang hien -
        // tranh player bi ket "khoa dieu khien" mai mai.
        if (_isVisible)
        {
            _isVisible = false;
            PlayerCursor.IsUIOpen = false;
        }
    }


    private void Update()
    {
        if (!_isVisible)
            return;

        PollWaitingText();

        if (!_localAcked &&
            Input.GetKeyDown(_continueKey))
        {
            AckLocal();
        }

        // Host da du nguoi xac nhan (hoac fallback - xem
        // GameDayManager._useStatsAckFallback) -> tu dong dong panel.
        if (GameDayManager.Instance != null &&
            !GameDayManager.Instance.IsWaitingForStatsAck)
        {
            Hide();
        }
    }


    // ============================================================
    // SHOW
    // ============================================================

    public void Show(
        int finishedDay,
        string statsPayload)
    {
        DayStatsData data =
            DayStatsData.Deserialize(statsPayload);

        _isVisible = true;

        _localAcked = false;

        KillAllTweens();

        // ----------------------------------------------------------
        // KHOA CAMERA / DI CHUYEN / PROMPT CUA - xem comment tren dau
        // class. Bat NGAY LUC panel bat dau hien, giu khoa cho toi khi
        // panel dong HOAN TOAN (xem Hide() -> _closeSequence.OnComplete).
        // ----------------------------------------------------------

        PlayerCursor.IsUIOpen = true;

        if (_panelRoot != null)
            _panelRoot.SetActive(true);

        // ----------------------------------------------------------
        // RESET VE TRANG THAI AN HOAN TOAN TRUOC KHI REVEAL DAN.
        // ----------------------------------------------------------

        SetCanvasGroupInstant(_canvasGroup, 0f);
        SetCanvasGroupInstant(_progressSectionGroup, 0f);
        SetCanvasGroupInstant(_infoRowGroup, 0f);

        // 3 gia tri trong khung 2 set alpha = 0 RIENG (khong dung
        // CanvasGroup) de dung luc _infoRowGroup fade in, nhan chu
        // TARGET/FISH/EARNINGS hien ra nhung gia tri $ / so luong VAN
        // dang trong suot - roi moi fade lan luot tung cai.
        SetTextAlphaInstant(_infoTargetText, 0f);
        SetTextAlphaInstant(_infoFishText, 0f);
        SetTextAlphaInstant(_infoEarningsText, 0f);

        if (_progressFillImage != null)
            _progressFillImage.fillAmount = 0f;

        if (_progressPercentText != null)
            _progressPercentText.text = "0%";

        if (_titleText != null)
            _titleText.text = $"STATS FOR DAY {finishedDay}";

        // Chi hien "PRESS E" SAU KHI toan bo hieu ung reveal xong.
        if (_pressEHint != null)
            _pressEHint.SetActive(false);

        PopulateInfo(data);

        PlayOpenSequence(data);
    }


    // ============================================================
    // OPEN SEQUENCE (khung 1 -> khung 2 (khoi + lan luot gia tri) -> khung 3 -> hint E)
    // ============================================================

    private void PlayOpenSequence(
        DayStatsData data)
    {
        _openSequence = DOTween.Sequence();

        // 1) Ca panel fade in.
        if (_canvasGroup != null)
            _openSequence.Append(_canvasGroup.DOFade(1f, _fadeDuration));

        // 2) Khung TARGET + progress bar fade in.
        _openSequence.Append(RevealGroup(_progressSectionGroup));

        // 3) Progress bar chay 0% -> 100%.
        _openSequence.Append(BuildProgressBarTween());

        // 4) Khung 2 - fade CA KHOI (nhan/label hien ra) roi lan luot
        // TUNG GIA TRI: Target -> Fish -> Earnings.
        _openSequence.Append(RevealGroup(_infoRowGroup));
        _openSequence.Append(RevealTextAlpha(_infoTargetText));
        _openSequence.AppendInterval(_infoItemStaggerDelay);
        _openSequence.Append(RevealTextAlpha(_infoFishText));
        _openSequence.AppendInterval(_infoItemStaggerDelay);
        _openSequence.Append(RevealTextAlpha(_infoEarningsText));

        // 5) Khung 3 - bieu do duong tung nguoi choi (fade in + ve dan +
        // dem tien), chay NGOAI sequence chinh vi no co timeline rieng
        // (stagger giua cac duong) - xem HandleChartFinished() de biet
        // khi nao xong.
        _openSequence.AppendCallback(
            () =>
            {
                if (_chart != null)
                    _chart.Play(data, HandleChartFinished);
                else
                    HandleChartFinished();
            }
        );

        _openSequence.Play();
    }


    private void HandleChartFinished()
    {
        // Panel co the da bi dong giua chung (VD fallback qua nhanh) -
        // dung hien hint E lam gi nua.
        if (!_isVisible)
            return;

        if (_pressEHint != null)
            _pressEHint.SetActive(true);
    }


    private Tween BuildProgressBarTween()
    {
        float value = 0f;

        return DOTween.To(
                () => value,
                v =>
                {
                    value = v;

                    if (_progressFillImage != null)
                        _progressFillImage.fillAmount = v;

                    if (_progressPercentText != null)
                        _progressPercentText.text =
                            $"{Mathf.RoundToInt(v * 100f)}%";
                },
                1f,
                _progressFillDuration
            )
            .SetEase(Ease.OutCubic);
    }


    /// <summary>
    /// Fade CA KHOI 1 CanvasGroup (khung TARGET+progress hoac khung
    /// Target/Fish/Earnings) - GIO THEM hieu ung TRUOT LEN nhe (tu duoi
    /// len dung vi tri) chay CUNG LUC voi fade, thay vi chi fade suong
    /// nhu truoc - nhin co "chieu sau", noi bat hon.
    /// </summary>
    private Tween RevealGroup(
        CanvasGroup group)
    {
        if (group == null)
            return DOVirtual.DelayedCall(0.01f, null);

        group.alpha = 0f;

        RectTransform rt = group.transform as RectTransform;
        if (rt == null)
            return group.DOFade(1f, _sectionRevealFadeDuration);

        rt.DOKill();

        Vector2 originalPos = rt.anchoredPosition;
        rt.anchoredPosition = originalPos + new Vector2(0f, -_revealSlideDistance);

        Sequence seq = DOTween.Sequence();
        seq.Join(group.DOFade(1f, _sectionRevealFadeDuration));
        seq.Join(
            rt.DOAnchorPos(originalPos, _sectionRevealFadeDuration)
                .SetEase(Ease.OutCubic)
        );

        // Dam bao vi tri VE DUNG CHINH XAC gia tri goc luc ket thuc,
        // phong khi truoc do KillAllTweens() cat ngang giua chung.
        seq.OnComplete(() => rt.anchoredPosition = originalPos);

        return seq;
    }


    /// <summary>
    /// Hien 1 GIA TRI rieng le (Target/Fish/Earnings) - GIO KHONG con
    /// fade alpha suong nua, ma la ca 1 "cu pop": bat dau nho (scale
    /// _revealStartScale) + hoi lech xuong duoi, roi PHONG TO VE 1 co
    /// NAY (overshoot, Ease.OutBack) + TRUOT LEN dung vi tri + fade in
    /// CUNG LUC, KET THUC bang 1 cu "nay" nho (punch scale) de nhin
    /// song dong, noi bat hon han so voi chi fade truoc day.
    /// </summary>
    private Tween RevealTextAlpha(
        TextMeshProUGUI text)
    {
        if (text == null)
            return DOVirtual.DelayedCall(0.01f, null);

        RectTransform rt = text.rectTransform;
        rt.DOKill();
        text.DOKill();

        Vector2 originalPos = rt.anchoredPosition;
        Vector3 originalScale = rt.localScale;

        SetTextAlphaInstant(text, 0f);
        rt.localScale = originalScale * _revealStartScale;
        rt.anchoredPosition = originalPos + new Vector2(0f, -_revealSlideDistance);

        Sequence seq = DOTween.Sequence();
        seq.Join(text.DOFade(1f, _sectionRevealFadeDuration));
        seq.Join(
            rt.DOScale(originalScale, _sectionRevealFadeDuration)
                .SetEase(Ease.OutBack, _revealOvershoot)
        );
        seq.Join(
            rt.DOAnchorPos(originalPos, _sectionRevealFadeDuration)
                .SetEase(Ease.OutCubic)
        );

        seq.AppendCallback(
            () =>
            {
                // Ep ve DUNG scale/vi tri goc truoc khi punch, tranh
                // sai so cong don tu cac ease overshoot ben tren.
                rt.localScale = originalScale;
                rt.anchoredPosition = originalPos;
            }
        );

        seq.Append(
            rt.DOPunchScale(Vector3.one * _revealPunchScale, _revealPunchDuration, 6, 0.6f)
        );

        return seq;
    }


    // ============================================================
    // INFO TEXTS
    // ============================================================

    private void PopulateInfo(
        DayStatsData data)
    {
        if (_progressTargetValueText != null)
            _progressTargetValueText.text = $"${data.Target}";

        if (_infoTargetText != null)
            _infoTargetText.text = $"${data.Target}";

        if (_infoFishText != null)
            _infoFishText.text = data.FishSold.ToString();

        if (_infoEarningsText != null)
            _infoEarningsText.text = $"${data.Earnings}";
    }


    // ============================================================
    // ACK / CONTINUE
    // ============================================================

    private void AckLocal()
    {
        _localAcked = true;

        if (_pressEHint != null)
            _pressEHint.SetActive(false);

        GameplayPlayer.LocalInstance?.RPC_ReportStatsAcknowledged();
    }


    private void PollWaitingText()
    {
        if (_waitingForPlayersText == null)
            return;

        if (GameDayManager.Instance == null)
            return;

        int acked =
            GameDayManager.Instance.PlayersAckedStatsCount;

        int expected =
            GameDayManager.Instance.ExpectedActivePlayerCount;

        _waitingForPlayersText.text =
            $"Waiting for players ({acked}/{expected})...";
    }


    // ============================================================
    // CLOSE SEQUENCE (khung 3 -> khung 2 (ca khoi) -> khung 1 -> ca panel)
    // ============================================================

    private void Hide()
    {
        _isVisible = false;

        KillAllTweens();

        if (_chart != null)
            _chart.Stop();

        if (_pressEHint != null)
            _pressEHint.SetActive(false);

        _closeSequence = DOTween.Sequence();

        // Khung 3 (bieu do duong) mo dan truoc tien.
        AppendChartFadeOut(_closeSequence);
        _closeSequence.AppendInterval(_closeItemStagger);

        // Khung 2 - dong CA KHOI (1 fade duy nhat, khong can lam nguoc
        // tung gia tri nhu luc mo - dong nhanh hon va don gian hon).
        AppendGroupFadeOut(_closeSequence, _infoRowGroup);
        _closeSequence.AppendInterval(_closeItemStagger);

        // Khung TARGET + progress bar.
        AppendGroupFadeOut(_closeSequence, _progressSectionGroup);

        // Cuoi cung ca panel (bao gom I-Background) moi fade het - luc
        // nay noi dung da "sach", nen dung luc GameDayIntroPanel phu den
        // len se rat muot.
        if (_canvasGroup != null)
        {
            _closeSequence.Append(_canvasGroup.DOFade(0f, _fadeDuration));
        }

        _closeSequence.OnComplete(
            () =>
            {
                if (_panelRoot != null)
                    _panelRoot.SetActive(false);

                // Panel da BIEN MAT HOAN TOAN - tra lai quyen dieu khien
                // camera/di chuyen/prompt cua cho player.
                PlayerCursor.IsUIOpen = false;
            }
        );

        _closeSequence.Play();
    }


    private void AppendChartFadeOut(
        Sequence seq)
    {
        if (_chart == null)
        {
            seq.AppendInterval(0.01f);

            return;
        }

        seq.Append(_chart.FadeOutCards(_closeItemFadeDuration));
    }


    private void AppendGroupFadeOut(
        Sequence seq,
        CanvasGroup group)
    {
        if (group == null)
        {
            seq.AppendInterval(0.01f);

            return;
        }

        seq.Append(group.DOFade(0f, _closeItemFadeDuration));
    }


    // ============================================================
    // HELPERS
    // ============================================================

    private static void SetCanvasGroupInstant(
        CanvasGroup group,
        float alpha)
    {
        if (group == null)
            return;

        group.DOKill();

        group.alpha = alpha;
    }


    private static void SetTextAlphaInstant(
        TextMeshProUGUI text,
        float alpha)
    {
        if (text == null)
            return;

        text.DOKill();

        Color c = text.color;

        c.a = alpha;

        text.color = c;
    }


    private void KillAllTweens()
    {
        if (_openSequence != null && _openSequence.IsActive())
            _openSequence.Kill();

        if (_closeSequence != null && _closeSequence.IsActive())
            _closeSequence.Kill();
    }
}