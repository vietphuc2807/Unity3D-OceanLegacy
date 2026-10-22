using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Quản lý góc nhìn Inspect Fish.
///
/// Flow:
/// Player cầm cá
///     ↓
/// Nhấn C
///     ↓
/// FishingTutorialUI bắt phím C
///     ↓
/// OnInspectFishPressed
///     ↓
/// FishInspectView nhận event
///     ↓
/// Đổi camera sang góc Inspect
///
/// Khi đang Inspect:
///     - C = thoát Inspect
///     - Mouse Wheel Up = Zoom In
///     - Mouse Wheel Down = Zoom Out
///     - Mouse Wheel KHÔNG đổi item
///
/// FOV UI:
///     - Khi FOV thay đổi bằng Mouse Wheel
///       → hiện "FOV: 45°"
///       → Fade In
///       → giữ một lúc
///       → Fade Out
///
/// Khi thoát Inspect:
///     - Camera trở lại vị trí ban đầu
///     - FOV Inspect đã chỉnh được ghi nhớ
///     - Lần sau Inspect sẽ dùng lại FOV đó
///
/// IMPORTANT:
/// T-FOV nằm trong Scene Canvas, còn FishInspectView nằm
/// trong GameplayPlayer Prefab.
///
/// Script sẽ tự tìm T-FOV trong Scene.
/// Kể cả T-FOV đang inactive cũng có thể tìm được.
/// </summary>
public class FishInspectView : MonoBehaviour
{
    // >>> FIX TUTORIAL TEXT: bắn ra mỗi khi trạng thái Inspect THỰC SỰ thay
    // đổi (vào/ra), chỉ khi EnterInspect()/ExitInspect() thành công — KHÔNG
    // bắn nếu EnterInspect() bị early-return (ví dụ không cầm cá, chưa có
    // Input Authority, camera null...).
    //
    // true  = vừa VÀO Inspect (góc nhìn thứ ba).
    // false = vừa RA Inspect (về góc nhìn thứ nhất).
    //
    // Dùng để các UI khác (ví dụ FishingTutorialUI) đổi nội dung text theo
    // đúng trạng thái hiện tại, không cần tự đoán qua OnInspectFishPressed
    // (event đó chỉ là tín hiệu "người chơi bấm C", không chắc Inspect có
    // thực sự bắt đầu/kết thúc hay không — ví dụ bấm C nhưng không cầm cá
    // thì EnterInspect() sẽ return sớm, không có gì xảy ra).
    public static event System.Action<bool> OnInspectStateChanged;
    // <<< FIX TUTORIAL TEXT

    [Header("References")]
    [SerializeField] private NetPlayerLook _look;
    [SerializeField] private PlayerInventory _inventory;


    // ============================================================
    // FOV UI
    // ============================================================

    [Header("FOV UI")]

    [Tooltip(
        "Tên GameObject Text FOV trong Scene. " +
        "Ví dụ: T-FOV."
    )]
    [SerializeField] private string _fovTextObjectName = "T-FOV";

    [Tooltip(
        "Text hiển thị FOV. " +
        "Có thể để None vì script sẽ tự tìm T-FOV."
    )]
    [SerializeField] private TMP_Text _fovText;

    [Tooltip("Thời gian Fade In của text FOV.")]
    [SerializeField] private float _fovFadeInDuration = 0.15f;

    [Tooltip("Thời gian giữ text FOV hiện rõ.")]
    [SerializeField] private float _fovVisibleDuration = 0.8f;

    [Tooltip("Thời gian Fade Out của text FOV.")]
    [SerializeField] private float _fovFadeOutDuration = 0.4f;


    // ============================================================
    // INSPECT CAMERA
    // ============================================================

    [Header("Inspect Camera Offset")]

    [Tooltip(
        "Vị trí camera tương đối so với Player."
    )]
    [SerializeField] private Vector3 _positionOffset =
        new Vector3(0.6f, 1.6f, -1.8f);

    [Tooltip(
        "Rotation camera tương đối so với hướng Player."
    )]
    [SerializeField] private Vector3 _rotationOffsetEuler =
        new Vector3(10f, -25f, 0f);


    // ============================================================
    // INSPECT FOV
    // ============================================================

    [Header("Inspect FOV")]

    [Tooltip(
        "FOV Inspect ban đầu."
    )]
    [SerializeField] private float _inspectFov = 45f;

    [Tooltip(
        "FOV nhỏ nhất khi zoom gần."
    )]
    [SerializeField] private float _minInspectFov = 30f;

    [Tooltip(
        "FOV lớn nhất khi zoom xa."
    )]
    [SerializeField] private float _maxInspectFov = 65f;

    [Tooltip(
        "Độ nhạy zoom bằng chuột giữa."
    )]
    [SerializeField] private float _scrollZoomSpeed = 5f;


    // ============================================================
    // DEBUG
    // ============================================================

    [Header("Debug")]

    [SerializeField] private bool _debugLog = true;


    // ============================================================
    // PRIVATE VARIABLES
    // ============================================================

    private Transform _pivot;

    private Transform _cameraOriginalParent;
    private Vector3 _cameraOriginalLocalPos;
    private Quaternion _cameraOriginalLocalRot;

    // FOV bình thường của camera trước khi vào Inspect.
    private float _originalFov;

    // FOV Inspect hiện tại.
    //
    // Không reset khi thoát Inspect.
    //
    // Ví dụ:
    //
    // Inspect = 45°
    // Scroll = 35°
    // Exit
    // Vào Inspect lại = 35°
    private float _currentInspectFov;

    private bool _isInspecting;

    // Coroutine hiệu ứng Fade của FOV Text.
    private Coroutine _fovFadeCoroutine;


    // ============================================================
    // ENABLE
    // ============================================================

    private void OnEnable()
    {
        FishingTutorialUI.OnInspectFishPressed += HandleInspectPressed;

        // --------------------------------------------------------
        // Khởi tạo FOV Inspect
        // --------------------------------------------------------

        _currentInspectFov = Mathf.Clamp(
            _inspectFov,
            _minInspectFov,
            _maxInspectFov
        );

        // --------------------------------------------------------
        // Tìm T-FOV trong Scene
        // --------------------------------------------------------

        FindFovText();

        // --------------------------------------------------------
        // Text FOV ban đầu phải ẩn
        // --------------------------------------------------------

        if (_fovText != null)
        {
            _fovText.alpha = 0f;
        }

        if (_debugLog)
        {
            Debug.Log(
                "[FishInspectView] Subscribe thành công. " +
                $"Initial Inspect FOV = {_currentInspectFov:F0}°"
            );
        }
    }


    // ============================================================
    // FIND FOV TEXT
    // ============================================================

    /// <summary>
    /// Tìm T-FOV trong Scene.
    ///
    /// Quan trọng:
    /// Unity GameObject.Find() không tìm được GameObject inactive.
    ///
    /// Vì vậy ở đây sử dụng:
    ///
    /// FindObjectsByType<TMP_Text>(
    ///     FindObjectsInactive.Include,
    ///     FindObjectsSortMode.None
    /// );
    ///
    /// Nhờ đó T-FOV vẫn tìm được kể cả khi
    /// GameObject đang inactive.
    /// </summary>
    private void FindFovText()
    {
        // Nếu đã có reference rồi thì không cần tìm.
        if (_fovText != null)
            return;

        // --------------------------------------------------------
        // Kiểm tra tên object
        // --------------------------------------------------------

        if (string.IsNullOrWhiteSpace(_fovTextObjectName))
        {
            Debug.LogWarning(
                "[FishInspectView] FOV Text Object Name đang trống."
            );

            return;
        }

        // --------------------------------------------------------
        // Tìm tất cả TMP_Text kể cả inactive
        // --------------------------------------------------------

        TMP_Text[] allTexts =
            FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        // --------------------------------------------------------
        // Tìm đúng object có tên T-FOV
        // --------------------------------------------------------

        for (int i = 0; i < allTexts.Length; i++)
        {
            TMP_Text text = allTexts[i];

            if (text == null)
                continue;

            if (text.gameObject.name != _fovTextObjectName)
                continue;

            _fovText = text;
            break;
        }

        // --------------------------------------------------------
        // Không tìm thấy
        // --------------------------------------------------------

        if (_fovText == null)
        {
            Debug.LogWarning(
                "[FishInspectView] Không tìm thấy TMP_Text " +
                $"có tên '{_fovTextObjectName}' trong Scene."
            );

            return;
        }

        // --------------------------------------------------------
        // Đảm bảo text ban đầu ẩn
        // --------------------------------------------------------

        _fovText.alpha = 0f;

        if (_debugLog)
        {
            Debug.Log(
                "[FishInspectView] Đã tìm thấy FOV Text: " +
                $"{_fovText.gameObject.name} " +
                $"(Inactive cũng được hỗ trợ)."
            );
        }
    }


    // ============================================================
    // DISABLE
    // ============================================================

    private void OnDisable()
    {
        FishingTutorialUI.OnInspectFishPressed -= HandleInspectPressed;

        StopFovFade();

        if (_isInspecting)
        {
            ExitInspect();
        }
    }


    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        if (!_isInspecting)
            return;

        // --------------------------------------------------------
        // Nếu đổi sang item khác thì tự thoát Inspect
        // --------------------------------------------------------

        if (!IsCurrentlyHoldingFish())
        {
            if (_debugLog)
            {
                Debug.Log(
                    "[FishInspectView] Không còn cầm cá " +
                    "-> Exit Inspect."
                );
            }

            ExitInspect();

            return;
        }

        // --------------------------------------------------------
        // Zoom bằng chuột giữa
        // --------------------------------------------------------

        HandleInspectZoom();
    }


    // ============================================================
    // HANDLE ZOOM
    // ============================================================

    private void HandleInspectZoom()
    {
        if (!_isInspecting)
            return;

        // --------------------------------------------------------
        // Nếu đang chat thì không zoom
        // --------------------------------------------------------

        if (LobbyChatUI.IsTyping)
            return;

        // --------------------------------------------------------
        // Nếu UI đang mở thì không zoom
        // --------------------------------------------------------

        if (PlayerCursor.IsUIOpen)
            return;

        // --------------------------------------------------------
        // Đọc Mouse Wheel
        // --------------------------------------------------------

        float scroll =
            Input.mouseScrollDelta.y;

        if (Mathf.Abs(scroll) < 0.001f)
            return;

        // --------------------------------------------------------
        // Lưu FOV cũ
        // --------------------------------------------------------

        float previousFov =
            _currentInspectFov;

        // --------------------------------------------------------
        // Scroll lên = Zoom IN
        // FOV giảm
        //
        // Scroll xuống = Zoom OUT
        // FOV tăng
        // --------------------------------------------------------

        _currentInspectFov -=
            scroll * _scrollZoomSpeed;

        // --------------------------------------------------------
        // Clamp FOV
        // --------------------------------------------------------

        _currentInspectFov =
            Mathf.Clamp(
                _currentInspectFov,
                _minInspectFov,
                _maxInspectFov
            );

        // --------------------------------------------------------
        // Chỉ xử lý nếu FOV thực sự thay đổi
        // --------------------------------------------------------

        if (!Mathf.Approximately(
            previousFov,
            _currentInspectFov))
        {
            Camera cam =
                _look != null
                    ? _look.PlayerCamera
                    : null;

            // ----------------------------------------------------
            // Cập nhật Camera FOV
            // ----------------------------------------------------

            if (cam != null)
            {
                cam.fieldOfView =
                    _currentInspectFov;
            }

            // ----------------------------------------------------
            // Hiện FOV Text
            // ----------------------------------------------------

            ShowFovText();

            if (_debugLog)
            {
                Debug.Log(
                    "[FishInspectView] Inspect FOV: " +
                    $"{_currentInspectFov:F0}°"
                );
            }
        }
    }


    // ============================================================
    // SHOW FOV TEXT
    // ============================================================

    private void ShowFovText()
    {
        // --------------------------------------------------------
        // Nếu reference chưa có thì thử tìm lại.
        // --------------------------------------------------------

        if (_fovText == null)
        {
            FindFovText();
        }

        if (_fovText == null)
        {
            if (_debugLog)
            {
                Debug.LogWarning(
                    "[FishInspectView] Không thể hiển thị FOV Text " +
                    "vì _fovText vẫn NULL."
                );
            }

            return;
        }

        // --------------------------------------------------------
        // Nếu GameObject đang inactive thì bật nó lên
        // --------------------------------------------------------

        if (!_fovText.gameObject.activeSelf)
        {
            _fovText.gameObject.SetActive(true);

            if (_debugLog)
            {
                Debug.Log(
                    "[FishInspectView] T-FOV đang inactive -> " +
                    "đã tự động bật GameObject."
                );
            }
        }

        // --------------------------------------------------------
        // Nội dung text
        // --------------------------------------------------------

        _fovText.text =
            $"FOV: {_currentInspectFov:F0}°";

        // --------------------------------------------------------
        // Stop fade cũ
        // --------------------------------------------------------

        StopFovFade();

        // --------------------------------------------------------
        // Chạy fade mới
        // --------------------------------------------------------

        _fovFadeCoroutine =
            StartCoroutine(
                FovFadeRoutine()
            );
    }


    // ============================================================
    // STOP FOV FADE
    // ============================================================

    private void StopFovFade()
    {
        if (_fovFadeCoroutine != null)
        {
            StopCoroutine(
                _fovFadeCoroutine
            );

            _fovFadeCoroutine = null;
        }

        if (_fovText != null)
        {
            _fovText.alpha = 0f;
        }
    }


    // ============================================================
    // FOV FADE ROUTINE
    // ============================================================

    private IEnumerator FovFadeRoutine()
    {
        if (_fovText == null)
            yield break;

        // ========================================================
        // FADE IN
        // ========================================================

        float t = 0f;

        float fadeInDuration =
            Mathf.Max(
                0.01f,
                _fovFadeInDuration
            );

        while (t < fadeInDuration)
        {
            t += Time.deltaTime;

            _fovText.alpha =
                Mathf.Clamp01(
                    t / fadeInDuration
                );

            yield return null;
        }

        _fovText.alpha = 1f;


        // ========================================================
        // GIỮ TEXT
        // ========================================================

        if (_fovVisibleDuration > 0f)
        {
            yield return new WaitForSeconds(
                _fovVisibleDuration
            );
        }


        // ========================================================
        // FADE OUT
        // ========================================================

        t = 0f;

        float fadeOutDuration =
            Mathf.Max(
                0.01f,
                _fovFadeOutDuration
            );

        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;

            _fovText.alpha =
                1f -
                Mathf.Clamp01(
                    t / fadeOutDuration
                );

            yield return null;
        }

        _fovText.alpha = 0f;

        _fovFadeCoroutine = null;
    }


    // ============================================================
    // LATE UPDATE
    // ============================================================

    private void LateUpdate()
    {
        if (!_isInspecting)
            return;

        ApplyInspectTransform();
    }


    // ============================================================
    // RECEIVE EVENT
    // ============================================================

    private void HandleInspectPressed()
    {
        if (_debugLog)
        {
            Debug.Log(
                "[FishInspectView] " +
                "Nhận được OnInspectFishPressed."
            );
        }

        // --------------------------------------------------------
        // Đang Inspect
        // C lần nữa -> thoát
        // --------------------------------------------------------

        if (_isInspecting)
        {
            ExitInspect();

            return;
        }

        // --------------------------------------------------------
        // Chưa Inspect
        // C -> vào Inspect
        // --------------------------------------------------------

        EnterInspect();
    }


    // ============================================================
    // CHECK FISH
    // ============================================================

    private bool IsCurrentlyHoldingFish()
    {
        if (_inventory == null)
        {
            if (_debugLog)
            {
                Debug.LogWarning(
                    "[FishInspectView] " +
                    "_inventory chưa được gán!"
                );
            }

            return false;
        }

        ItemData selectedItem =
            _inventory.GetSelectedItem();

        if (selectedItem == null)
            return false;

        return selectedItem.IsFish;
    }


    // ============================================================
    // ENTER INSPECT
    // ============================================================

    private void EnterInspect()
    {
        // --------------------------------------------------------
        // Check NetPlayerLook
        // --------------------------------------------------------

        if (_look == null)
        {
            Debug.LogError(
                "[FishInspectView] " +
                "_look chưa được gán!"
            );

            return;
        }

        // --------------------------------------------------------
        // Check NetworkObject
        // --------------------------------------------------------

        if (_look.Object == null)
        {
            Debug.LogWarning(
                "[FishInspectView] " +
                "NetPlayerLook chưa được Network Spawn."
            );

            return;
        }

        // --------------------------------------------------------
        // Chỉ Local Player được Inspect
        // --------------------------------------------------------

        if (!_look.Object.HasInputAuthority)
        {
            Debug.LogWarning(
                "[FishInspectView] " +
                "Player này không có Input Authority."
            );

            return;
        }

        // --------------------------------------------------------
        // Check đang cầm cá
        // --------------------------------------------------------

        if (!IsCurrentlyHoldingFish())
        {
            if (_debugLog)
            {
                Debug.Log(
                    "[FishInspectView] Không thể Inspect vì " +
                    "player hiện không cầm cá."
                );
            }

            return;
        }

        // --------------------------------------------------------
        // Check Camera
        // --------------------------------------------------------

        Camera cam =
            _look.PlayerCamera;

        if (cam == null)
        {
            Debug.LogError(
                "[FishInspectView] " +
                "PlayerCamera đang NULL!"
            );

            return;
        }

        // ========================================================
        // TÌM FOV TEXT
        // ========================================================

        if (_fovText == null)
        {
            FindFovText();
        }

        // ========================================================
        // LƯU CAMERA BAN ĐẦU
        // ========================================================

        _cameraOriginalParent =
            cam.transform.parent;

        _cameraOriginalLocalPos =
            cam.transform.localPosition;

        _cameraOriginalLocalRot =
            cam.transform.localRotation;

        // --------------------------------------------------------
        // Lưu FOV gameplay hiện tại
        // --------------------------------------------------------

        _originalFov =
            cam.fieldOfView;


        // ========================================================
        // ĐẢM BẢO FOV INSPECT HỢP LỆ
        // ========================================================

        _currentInspectFov =
            Mathf.Clamp(
                _currentInspectFov,
                _minInspectFov,
                _maxInspectFov
            );


        // ========================================================
        // TẠO PIVOT
        // ========================================================

        _pivot =
            new GameObject(
                "FishInspectPivot"
            ).transform;

        _pivot.SetParent(null);

        _pivot.SetPositionAndRotation(
            transform.position,
            transform.rotation
        );


        // ========================================================
        // TÁCH CAMERA KHỎI CAMERA HOLDER
        // ========================================================

        cam.transform.SetParent(
            _pivot,
            false
        );


        // ========================================================
        // CHO CAMERA NHÌN THẤY PLAYER BODY
        // ========================================================

        _look.SetBodyRenderVisible(true);


        // ========================================================
        // BẬT INSPECT FLAG
        // ========================================================

        NetPlayerLook.IsInspectingFish = true;

        _isInspecting = true;


        // ========================================================
        // TEXT FOV ẨN KHI VỪA VÀO
        // ========================================================

        StopFovFade();


        // ========================================================
        // APPLY NGAY LẬP TỨC
        // ========================================================

        ApplyInspectTransform();


        // >>> FIX TUTORIAL TEXT: báo cho các UI khác biết Inspect đã BẮT
        // ĐẦU thành công (chỉ tới đây, sau mọi check ở trên, mới coi là
        // "vào Inspect" thật sự — tránh bắn event sai nếu bị early-return
        // phía trên).
        OnInspectStateChanged?.Invoke(true);
        // <<< FIX TUTORIAL TEXT


        // ========================================================
        // DEBUG
        // ========================================================

        if (_debugLog)
        {
            Debug.Log(
                "[FishInspectView] ============================="
            );

            Debug.Log(
                "[FishInspectView] ENTER INSPECT THÀNH CÔNG!"
            );

            Debug.Log(
                "[FishInspectView] Inspect FOV: " +
                $"{_currentInspectFov:F0}°"
            );

            Debug.Log(
                "[FishInspectView] Camera Parent: " +
                $"{cam.transform.parent?.name}"
            );

            Debug.Log(
                "[FishInspectView] Camera Position: " +
                $"{cam.transform.position}"
            );

            Debug.Log(
                "[FishInspectView] Camera Rotation: " +
                $"{cam.transform.rotation.eulerAngles}"
            );

            if (_fovText != null)
            {
                Debug.Log(
                    "[FishInspectView] FOV Text: " +
                    $"{_fovText.gameObject.name}"
                );
            }
            else
            {
                Debug.LogWarning(
                    "[FishInspectView] FOV Text = NULL."
                );
            }

            Debug.Log(
                "[FishInspectView] ============================="
            );
        }
    }


    // ============================================================
    // APPLY CAMERA
    // ============================================================

    private void ApplyInspectTransform()
    {
        if (!_isInspecting)
            return;

        if (_pivot == null)
            return;

        if (_look == null)
            return;

        Camera cam =
            _look.PlayerCamera;

        if (cam == null)
            return;

        // --------------------------------------------------------
        // Pivot bám theo Player
        // --------------------------------------------------------

        _pivot.SetPositionAndRotation(
            transform.position,
            transform.rotation
        );

        // --------------------------------------------------------
        // Camera Position
        // --------------------------------------------------------

        cam.transform.localPosition =
            _positionOffset;

        // --------------------------------------------------------
        // Camera Rotation
        // --------------------------------------------------------

        cam.transform.localRotation =
            Quaternion.Euler(
                _rotationOffsetEuler
            );

        // --------------------------------------------------------
        // FOV
        // --------------------------------------------------------

        cam.fieldOfView =
            _currentInspectFov;
    }


    // ============================================================
    // EXIT INSPECT
    // ============================================================

    private void ExitInspect()
    {
        if (!_isInspecting)
            return;

        // ========================================================
        // TEXT FOV BIẾN MẤT
        // ========================================================

        StopFovFade();

        Camera cam =
            _look != null
                ? _look.PlayerCamera
                : null;


        // ========================================================
        // RESTORE CAMERA
        // ========================================================

        if (cam != null)
        {
            cam.transform.SetParent(
                _cameraOriginalParent,
                false
            );

            cam.transform.localPosition =
                _cameraOriginalLocalPos;

            cam.transform.localRotation =
                _cameraOriginalLocalRot;

            // ----------------------------------------------------
            // Trả FOV về FOV gameplay.
            //
            // KHÔNG reset _currentInspectFov.
            // ----------------------------------------------------

            cam.fieldOfView =
                _originalFov;
        }


        // ========================================================
        // ẨN BODY TRỞ LẠI
        // ========================================================

        if (_look != null)
        {
            _look.SetBodyRenderVisible(false);
        }


        // ========================================================
        // XÓA PIVOT
        // ========================================================

        if (_pivot != null)
        {
            Destroy(
                _pivot.gameObject
            );

            _pivot = null;
        }


        // ========================================================
        // TẮT FLAG
        // ========================================================

        NetPlayerLook.IsInspectingFish = false;

        _isInspecting = false;


        // >>> FIX TUTORIAL TEXT: báo cho các UI khác biết Inspect đã KẾT
        // THÚC — dù ExitInspect() được gọi từ HandleInspectPressed() (bấm
        // C lần nữa), từ Update() (đổi item khác), hay từ OnDisable().
        OnInspectStateChanged?.Invoke(false);
        // <<< FIX TUTORIAL TEXT


        // ========================================================
        // DEBUG
        // ========================================================

        if (_debugLog)
        {
            Debug.Log(
                "[FishInspectView] EXIT INSPECT. " +
                $"FOV được lưu = {_currentInspectFov:F0}°"
            );
        }
    }
}