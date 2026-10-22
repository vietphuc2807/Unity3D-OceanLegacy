using UnityEngine;

public class LoadingCameraHandler : MonoBehaviour
{
    public static LoadingCameraHandler Instance { get; private set; }

    [SerializeField] private Camera _loadingCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        // FIX: dọn static reference khi object này bị huỷ (ví dụ khi rời
        // scene Gameplay), tránh Instance trỏ vào object đã destroy ở lần
        // load scene kế tiếp.
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        if (_loadingCamera != null)
        {
            _loadingCamera.gameObject.SetActive(true);
        }
    }

    public void OnPlayerCameraReady()
    {
        if (_loadingCamera != null)
        {
            _loadingCamera.gameObject.SetActive(false);
        }
    }
}