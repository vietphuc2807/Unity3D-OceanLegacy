using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Banner thong bao noi giua/tren man hinh.
///
/// Co 2 kieu dung:
///
/// 1) Show(message)              - PERSISTENT: fade in roi GIU NGUYEN
///    tren man hinh cho toi khi bi thay bang message khac hoac bi
///    Hide() thu cong. Dung cho tien do "X wants to head back to the
///    Museum! (1/4)" - can dung yen de nguoi choi doc duoc ro, khong
///    tu bien mat sau vai giay.
///
/// 2) ShowTemporary(message)     - tu FADE IN -> GIU mot khoang ngan
///    -> tu FADE OUT. Dung cho cac thong bao ngan, khong can nguoi
///    choi phai bam gi de tat.
///
/// Goi Show()/ShowTemporary() lien tuc se tu huy hieu ung dang chay va
/// bat dau lai - tranh chong 2 vong fade cung luc.
///
/// Dat script nay len 1 object rieng trong Canvas cua Gameplay scene
/// (KHONG networked - moi client co 1 ban rieng, chi hien thi cuc bo).
/// </summary>
public class GoHomeAnnouncementUI : MonoBehaviour
{
    public static GoHomeAnnouncementUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TMP_Text _text;

    [Header("Timing")]
    [SerializeField] private float _fadeInDuration = 0.25f;
    [SerializeField] private float _holdDuration = 2.5f;
    [SerializeField] private float _fadeOutDuration = 0.5f;

    private Coroutine _routine;

    private void Awake()
    {
        Instance = this;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ============================================================
    // SHOW - PERSISTENT (khong tu an)
    // ============================================================

    public void Show(string message)
    {
        if (_canvasGroup == null || _text == null)
            return;

        _text.text = message;

        if (_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(ShowPersistentRoutine());
    }

    private IEnumerator ShowPersistentRoutine()
    {
        yield return Fade(_canvasGroup.alpha, 1f, _fadeInDuration);

        _routine = null;
    }

    // ============================================================
    // SHOW - TEMPORARY (tu fade in -> giu -> fade out)
    // ============================================================

    private IEnumerator ShowTemporaryRoutine()
    {
        yield return Fade(_canvasGroup.alpha, 1f, _fadeInDuration);

        yield return new WaitForSeconds(_holdDuration);

        yield return Fade(1f, 0f, _fadeOutDuration);

        _routine = null;
    }

    // ============================================================
    // HIDE - fade out thu cong (dung khi da du nguoi, chuyen sang
    // GoHomeLoadingPanelUI).
    // ============================================================

    public void Hide()
    {
        if (_canvasGroup == null)
            return;

        if (_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(HideRoutine());
    }

    private IEnumerator HideRoutine()
    {
        yield return Fade(_canvasGroup.alpha, 0f, _fadeOutDuration);

        _routine = null;
    }

    // ============================================================
    // FADE
    // ============================================================

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            _canvasGroup.alpha = to;
            yield break;
        }

        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;

            _canvasGroup.alpha = Mathf.Lerp(from, to, t / duration);

            yield return null;
        }

        _canvasGroup.alpha = to;
    }
}