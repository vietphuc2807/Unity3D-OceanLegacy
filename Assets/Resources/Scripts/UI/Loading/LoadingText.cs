using System.Collections;
using TMPro;
using UnityEngine;

public class LoadingText : MonoBehaviour
{
    [SerializeField] private TMP_Text _loadingText;
    [SerializeField] private float _interval = 0.4f;

    private const string BaseText = "Loading";

    private IEnumerator Start()
    {
        while (true)
        {
            for (int i = 0; i <= 3; i++)
            {
                _loadingText.text = BaseText + new string('.', i);
                yield return new WaitForSeconds(_interval);
            }
        }
    }
}