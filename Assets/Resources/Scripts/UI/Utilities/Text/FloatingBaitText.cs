using DG.Tweening;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class FloatingBaitText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private RectTransform _rectTransform;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Animation")]
    [SerializeField] private float _riseDistance = 40f;
    [SerializeField] private float _fadeInDuration = 0.25f;
    [SerializeField] private float _holdDuration = 0.8f;
    [SerializeField] private float _fadeOutDuration = 0.4f;
    [SerializeField] private float _fadeOutRiseDistance = 20f;

    private Vector2 _basePosition;
    private Sequence _sequence;
    private bool _initialized;

    public void Show(int amount)
    {
        if (amount <= 0)
            return;

        gameObject.SetActive(true); // Kích hoạt -> Awake/khởi tạo chạy đồng bộ ngay tại đây nếu chưa từng chạy

        EnsureInitialized();

        string label = amount == 1 ? "Bait" : "Baits";
        _text.text = $"+{amount} {label}";

        _sequence?.Kill();

        _rectTransform.anchoredPosition = _basePosition - new Vector2(0f, _riseDistance);
        _canvasGroup.alpha = 0f;

        _sequence = DOTween.Sequence();

        _sequence.Append(_rectTransform.DOAnchorPos(_basePosition, _fadeInDuration).SetEase(Ease.OutQuad));
        _sequence.Join(_canvasGroup.DOFade(1f, _fadeInDuration));

        _sequence.AppendInterval(_holdDuration);

        _sequence.Append(_canvasGroup.DOFade(0f, _fadeOutDuration));
        _sequence.Join(_rectTransform.DOAnchorPos(_basePosition + new Vector2(0f, _fadeOutRiseDistance), _fadeOutDuration).SetEase(Ease.OutQuad));

        _sequence.OnComplete(() => gameObject.SetActive(false));
    }

    private void EnsureInitialized()
    {
        if (_initialized)
            return;

        _initialized = true;

        if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();

        _basePosition = _rectTransform.anchoredPosition;
    }

    private void OnDestroy()
    {
        _sequence?.Kill();
    }
}