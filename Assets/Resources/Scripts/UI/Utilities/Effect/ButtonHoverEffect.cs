using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float _hoverScale = 1.1f;
    [SerializeField] private float _duration = 0.2f;

    [Header("Sound")]
    [Tooltip("Ten clip trong SoundManager > Sfx Clips (de trong = khong phat tieng).")]
    [SerializeField] private string _hoverSfxName = "ButtonHover";

    private Vector3 _defaultScale;

    private void Awake()
    {
        _defaultScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.DOKill();
        transform.DOScale(_defaultScale * _hoverScale, _duration);

        if (!string.IsNullOrEmpty(_hoverSfxName) && SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(_hoverSfxName);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.DOKill();
        transform.DOScale(_defaultScale, _duration);
    }

    private void OnDisable()
    {
        transform.DOKill();
        transform.localScale = _defaultScale;
    }
}