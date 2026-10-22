using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class MenuButtonFishEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Fish - bơi từ phải sang trái")]
    [SerializeField] private RectTransform _fish;
    [SerializeField] private CanvasGroup _fishCanvasGroup;
    [SerializeField] private float _fishStartX = 40f;    // bắt đầu bên phải
    [SerializeField] private float _fishEndX = -10f;      // sát mép nút bên trái (chỗ "rỉa")
    [SerializeField] private float _fishSwimDuration = 0.6f;

    [Header("Nibble (rỉa nút)")]
    [SerializeField] private float _nibbleDistance = 6f;
    [SerializeField] private float _nibbleDuration = 0.15f;
    [SerializeField] private int _nibbleCount = 3;

    [Header("Bob (nhấp nhô)")]
    [SerializeField] private float _fishBobAmount = 4f;
    [SerializeField] private float _fishBobDuration = 0.4f;
    [SerializeField] private float _fishFadeDuration = 0.2f;

    private float _fishBaseY;
    private Sequence _fishSequence;

    private void Awake()
    {
        if (_fish != null)
            _fishBaseY = _fish.anchoredPosition.y;

        if (_fishCanvasGroup != null)
            _fishCanvasGroup.alpha = 0f;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_fish == null)
            return;

        _fishSequence?.Kill();
        _fish.anchoredPosition = new Vector2(_fishStartX, _fishBaseY);

        if (_fishCanvasGroup != null)
        {
            _fishCanvasGroup.DOKill();
            _fishCanvasGroup.DOFade(1f, _fishFadeDuration);
        }

        _fishSequence = DOTween.Sequence();

        // 1. Bơi từ phải vào sát nút
        _fishSequence.Append(
            _fish.DOAnchorPosX(_fishEndX, _fishSwimDuration).SetEase(Ease.OutSine)
        );

        // 2. Rỉa rỉa tại chỗ, không đổi hướng
        for (int i = 0; i < _nibbleCount; i++)
        {
            _fishSequence.Append(
                _fish.DOAnchorPosX(_fishEndX - _nibbleDistance, _nibbleDuration).SetEase(Ease.InOutSine)
            );
            _fishSequence.Append(
                _fish.DOAnchorPosX(_fishEndX + _nibbleDistance * 0.5f, _nibbleDuration).SetEase(Ease.InOutSine)
            );
        }

        // 3. Bơi lùi ra lại bên phải (vẫn giữ nguyên hướng mặt)
        _fishSequence.Append(
            _fish.DOAnchorPosX(_fishStartX, _fishSwimDuration).SetEase(Ease.InSine)
        );

        _fishSequence.SetLoops(-1);

        _fish.DOAnchorPosY(_fishBaseY + _fishBobAmount, _fishBobDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetId(_fish);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_fish == null)
            return;

        _fishSequence?.Kill();
        DOTween.Kill(_fish);

        if (_fishCanvasGroup != null)
        {
            _fishCanvasGroup.DOKill();
            _fishCanvasGroup.DOFade(0f, _fishFadeDuration);
        }
    }

    private void OnDisable()
    {
        _fishSequence?.Kill();

        if (_fish != null)
        {
            DOTween.Kill(_fish);
            _fish.anchoredPosition = new Vector2(_fishStartX, _fishBaseY);
        }

        if (_fishCanvasGroup != null)
        {
            _fishCanvasGroup.DOKill();
            _fishCanvasGroup.alpha = 0f;
        }
    }
}