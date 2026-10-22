using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingTarget : MonoBehaviour
{
    [Header("Next Scene")]
    [SerializeField] private string _nextSceneAuthentication;
    [SerializeField] private string _nextSceneMenu;

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

        string targetScene = _nextSceneAuthentication;

        var user = FirebaseManager.Instance.Auth.CurrentUser;

        if (user != null)
        {
            var reloadTask = user.ReloadAsync();
            yield return new WaitUntil(() => reloadTask.IsCompleted);

            user = FirebaseManager.Instance.Auth.CurrentUser;

            if (user != null && user.IsEmailVerified)
            {
                targetScene = _nextSceneMenu;
            }
            else
            {
                FirebaseManager.Instance.Auth.SignOut();
                targetScene = _nextSceneAuthentication;
            }
        }

        yield return Fade(1f, 0f, _fadeOutTime);

        SceneManager.LoadScene(targetScene);
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