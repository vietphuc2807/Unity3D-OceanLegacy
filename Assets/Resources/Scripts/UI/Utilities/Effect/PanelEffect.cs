using DG.Tweening;
using System;
using UnityEngine;

public class PanelEffect : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject _panel;

    [Header("Animation")]
    [SerializeField] private float _duration = 0.3f;

    private RectTransform _panelRect;
    private Tween _tween;
    private bool _isHiding;

    private void Awake()
    {
        _panelRect = _panel.GetComponent<RectTransform>();
    }

    private void OnDestroy()
    {
        _tween?.Kill();
    }

    public void Show()
    {
        _tween?.Kill();
        _isHiding = false;

        _panel.SetActive(true);
        _panelRect.localScale = Vector3.zero;

        _tween = _panelRect.DOScale(Vector3.one, _duration).SetEase(Ease.OutBack, 1.2f);
    }

    public void Hide(Action onComplete = null)
    {
        if (_isHiding)
            return;

        if (!_panel.activeSelf)
            return;

        _isHiding = true;
        _tween?.Kill();

        _tween = _panelRect.DOScale(Vector3.zero, _duration).SetEase(Ease.InBack, 1.2f)
            .OnComplete(() =>
            {
                _isHiding = false;
                _panel.SetActive(false);
                onComplete?.Invoke();
            });
    }
}