using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Tách camera khỏi CameraHolder, di chuyển tới góc nhìn cận cảnh tờ giấy
/// (giống cơ chế FishInspectView đang dùng ở chỗ khác), bật chế độ vẽ, và
/// trả camera về vị trí cũ khi đóng.
/// </summary>
public class PosterDrawView : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Transform đặt sẵn vị trí + hướng camera lúc đang vẽ (kéo 1 Empty GameObject đặt trước tờ giấy).")]
    [SerializeField] private Transform _viewPoint;
    [SerializeField] private PosterCanvasPainter _painter;
    [SerializeField] private GameObject _drawUI; // panel gợi ý 2 phím Esc/C, chỉ để hiển thị

    [Header("Timing")]
    [Tooltip("Thời gian animate camera lúc MỞ và ĐÓNG (cùng tốc độ).")]
    [SerializeField] private float _moveDuration = 0.4f;

    public bool IsActive { get; private set; }

    /// <summary>TRUE khi CÓ NGƯỜI BẤT KỲ (mình hoặc người khác) đang vẽ trên poster này — đọc từ cờ networked của painter.</summary>
    public bool IsPosterInUse => _painter != null && _painter.IsBeingUsed;

    private Action _onClosed;

    private Camera _cam;
    private Transform _camOriginalParent;
    private Vector3 _camOriginalLocalPos;
    private Quaternion _camOriginalLocalRot;
    private float _camOriginalFov;

    private Coroutine _moveRoutine;

    // Phím tắt thật: Esc = đóng, C = xoá nét vẽ. Panel UI chỉ còn là hình
    // minh hoạ, không còn Button/onClick nào cả.
    private void Update()
    {
        if (!IsActive) return;

        if (Input.GetKeyDown(KeyCode.Escape))
            Close();

        if (Input.GetKeyDown(KeyCode.C))
            RequestClear();
    }

    public void Open(Action onClosed)
    {
        if (IsActive)
        {
            Debug.Log("[PosterDrawView] Open() called but already IsActive, ignoring.");
            return;
        }

        _cam = Camera.main;

        if (_cam == null || _viewPoint == null)
        {
            Debug.LogWarning($"[PosterDrawView] Cannot open: Camera.main={_cam}, _viewPoint={_viewPoint}");
            return;
        }

        Debug.Log($"[PosterDrawView] Open() start. viewPoint pos={_viewPoint.position}, rot={_viewPoint.rotation.eulerAngles}");

        _onClosed = onClosed;
        IsActive = true;

        _camOriginalParent = _cam.transform.parent;
        _camOriginalLocalPos = _cam.transform.localPosition;
        _camOriginalLocalRot = _cam.transform.localRotation;
        _camOriginalFov = _cam.fieldOfView;

        // Tách khỏi CameraHolder để tự điều khiển transform, tránh
        // NetPlayerLook/FishingBiteCameraEffect ghi đè trong lúc vẽ.
        _cam.transform.SetParent(null, true);

        // Tái dùng cờ khoá look sẵn có — FishingBiteCameraEffect.LateUpdate
        // đã tự bỏ qua khi cờ này bật (xem đoạn "if (NetPlayerLook.IsInspectingFish) return;").
        NetPlayerLook.IsInspectingFish = true;

        PlayerCursor.IsUIOpen = true;
        PlayerCursor.UnlockCursor();

        if (_drawUI != null)
        {
            _drawUI.SetActive(true);
            Debug.Log($"[PosterDrawView] drawUI activeSelf={_drawUI.activeSelf}, " +
                      $"activeInHierarchy={_drawUI.activeInHierarchy}");
        }
        else
        {
            Debug.LogWarning("[PosterDrawView] _drawUI is NULL! Kéo panel gợi ý phím vào field này trong Inspector.");
        }

        if (_moveRoutine != null) StopCoroutine(_moveRoutine);
        _moveRoutine = StartCoroutine(MoveTo(_viewPoint.position, _viewPoint.rotation));

        if (_painter != null)
        {
            // Báo NGAY cho mọi client khác biết poster này đang bận, để
            // prompt/E của họ bị chặn ngay từ giây đầu tiên, không cần đợi
            // animate camera xong.
            _painter.RequestBeginUse();

            _painter.BeginDrawSession(_cam);
            Debug.Log("[PosterDrawView] BeginDrawSession called on painter.");
        }
        else
        {
            Debug.LogWarning("[PosterDrawView] _painter is NULL! Không thể vẽ vì chưa gán Painter trong Inspector.");
        }
    }

    public void Close()
    {
        if (!IsActive)
        {
            Debug.Log("[PosterDrawView] Close() called but not IsActive, ignoring.");
            return;
        }

        // DEBUG: in ra stack trace để biết CHÍNH XÁC đoạn code nào đã gọi
        // Close(). Nếu panel bị tắt ngay sau khi mở mà bạn không hề bấm nút
        // Close, dòng stack trace này sẽ chỉ ra thủ phạm thực sự.
        Debug.Log($"[PosterDrawView] Close() called. Stack trace:\n{System.Environment.StackTrace}");

        IsActive = false;

        _painter?.EndDrawSession();

        // Trả lại poster cho người khác NGAY LẬP TỨC khi bấm Esc, không
        // đợi animate camera trả về xong — không có lý do gì phải giữ
        // "đang bận" thêm trong lúc đó, người khác nên được vào lại càng
        // sớm càng tốt.
        _painter?.RequestEndUse();

        if (_drawUI != null)
            _drawUI.SetActive(false);

        // Không đổi NetPlayerLook/cursor ở đây — cố tình giữ nguyên trạng
        // thái "đang trong draw view" (IsInspectingFish = true, cursor vẫn
        // unlock) cho tới khi animation trả camera về xong, tránh
        // NetPlayerLook giành quyền set rotation cùng lúc với MoveTo() bên
        // dưới gây giật/lệch góc nhìn.
        if (_moveRoutine != null) StopCoroutine(_moveRoutine);
        _moveRoutine = StartCoroutine(ReturnAndFinish());
    }

    public void RequestClear()
    {
        if (_painter == null)
        {
            Debug.LogWarning("[PosterDrawView] _painter is NULL, không thể Clear.");
            return;
        }

        _painter.RequestClear();
    }

    private IEnumerator MoveTo(Vector3 targetPos, Quaternion targetRot)
    {
        Vector3 startPos = _cam.transform.position;
        Quaternion startRot = _cam.transform.rotation;

        float t = 0f;
        while (t < _moveDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / _moveDuration);

            _cam.transform.position = Vector3.Lerp(startPos, targetPos, p);
            _cam.transform.rotation = Quaternion.Slerp(startRot, targetRot, p);

            yield return null;
        }

        _cam.transform.position = targetPos;
        _cam.transform.rotation = targetRot;

        Debug.Log($"[PosterDrawView] MoveTo finished. cam.position={_cam.transform.position}, cam.rotation={_cam.transform.rotation.eulerAngles}");
    }

    private IEnumerator ReturnAndFinish()
    {
        Vector3 targetPos = _camOriginalParent != null
            ? _camOriginalParent.TransformPoint(_camOriginalLocalPos)
            : _camOriginalLocalPos;

        Quaternion targetRot = _camOriginalParent != null
            ? _camOriginalParent.rotation * _camOriginalLocalRot
            : _camOriginalLocalRot;

        yield return MoveTo(targetPos, targetRot);

        if (_camOriginalParent != null)
        {
            _cam.transform.SetParent(_camOriginalParent, true);
            _cam.transform.localPosition = _camOriginalLocalPos;
            _cam.transform.localRotation = _camOriginalLocalRot;
        }

        _cam.fieldOfView = _camOriginalFov;

        // Chỉ trả quyền look + khoá lại chuột SAU khi animation chạy xong.
        NetPlayerLook.IsInspectingFish = false;
        PlayerCursor.IsUIOpen = false;
        PlayerCursor.LockCursor();

        _onClosed?.Invoke();
    }
}