using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingScene : MonoBehaviour
{
    [Header("Next Scene")]
    [SerializeField] private string _nextScene;

    [Header("Fade Targets")]
    [SerializeField] private List<CanvasGroup> _targets = new();

    [Header("Fade Settings")]
    [SerializeField] private float _fadeInTime = 0.6f;
    [SerializeField] private float _holdTime = 1.2f;
    [SerializeField] private float _fadeOutTime = 0.6f;

    private IEnumerator Start()
    {
        yield return Fade(0f, 1f, _fadeInTime);

        yield return new WaitForSeconds(_holdTime);

        yield return Fade(1f, 0f, _fadeOutTime);

        SceneManager.LoadScene(_nextScene);
    }

    private IEnumerator Fade(float start, float end, float duration)
    {
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;

            float alpha = Mathf.Lerp(start, end, time / duration);

            foreach (CanvasGroup target in _targets)
            {
                if (target != null) target.alpha = alpha;
            }

            yield return null;
        }

        foreach (CanvasGroup target in _targets)
        {
            if (target != null) target.alpha = end;
        }
    }
}