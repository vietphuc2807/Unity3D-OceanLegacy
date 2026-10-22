using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FishingTutorialUI : MonoBehaviour
{
    [Header("Fishing Rod Tutorial")]

    [SerializeField]
    private GameObject _tutorialPanel;

    [SerializeField]
    private TextMeshProUGUI _attachBaitText;

    [Header("Inspect Fish Tutorial")]

    [Tooltip(
        "Text nằm trong Inspect Fish Panel. " +
        "Nội dung sẽ tự đổi giữa First Person và Third Person."
    )]
    [SerializeField]
    private GameObject _inspectFishPanel;

    [SerializeField]
    private TextMeshProUGUI _inspectFishText;

    [Tooltip("Text khi đang ở First Person.")]
    [SerializeField]
    private string _inspectPromptText = "Inspect Fish";

    [Tooltip("Text khi đang ở Third Person / Inspect.")]
    [SerializeField]
    private string _exitInspectPromptText = "Exit Inspect";


    // ============================================================
    // TAKE PHOTO UI
    // ============================================================

    [Header("Take Photo Tutorial")]

    [Tooltip(
        "UI chứa phím P + text Take Photo. " +
        "Chỉ hiện khi đang Inspect Fish."
    )]
    [SerializeField]
    private GameObject _takePhotoUI;


    // ============================================================
    // PHOTO CAPTURE UI
    // ============================================================

    [Header("Photo Capture UI")]

    [Tooltip(
        "CanvasGroup của P-UI. " +
        "Dùng để ẩn UI trong lúc chụp ảnh."
    )]
    [SerializeField]
    private CanvasGroup _photoCaptureCanvasGroup;


    // ============================================================
    // PHOTO TIMESTAMP
    // ============================================================

    [Header("Photo Timestamp")]

    [Tooltip(
        "Text hiển thị ngày giờ ở góc phải dưới ảnh. " +
        "Sẽ tự BẬT lúc chụp và TẮT lại sau khi chụp xong " +
        "(trừ khi _timestampAlwaysVisible = true)."
    )]
    [SerializeField]
    private TextMeshProUGUI _photoTimestampText;

    [Tooltip("Định dạng ngày giờ hiển thị trên ảnh.")]
    [SerializeField]
    private string _photoTimestampFormat = "dd/MM/yyyy HH:mm";

    [Tooltip(
        "True: timestamp luôn hiện sẵn trên UI (không tự ẩn/hiện). " +
        "False: chỉ hiện đúng lúc chụp, chụp xong tự ẩn."
    )]
    [SerializeField]
    private bool _timestampAlwaysVisible = false;


    // ============================================================
    // PHOTO TAKEN EFFECT
    // ============================================================

    [Header("Photo Taken Effect")]

    [Tooltip(
        "Panel chứa toàn bộ hiệu ứng ảnh sau khi chụp."
    )]
    [SerializeField]
    private GameObject _photoTakenEffect;

    [Tooltip(
        "CanvasGroup của PhotoTakenEffect. " +
        "Script sẽ tự lấy nếu để trống."
    )]
    [SerializeField]
    private CanvasGroup _photoTakenCanvasGroup;

    [Tooltip(
        "RectTransform của PhotoTakenEffect. " +
        "Dùng để tính toán rơi từ trên xuống. " +
        "Script sẽ tự lấy nếu để trống."
    )]
    [SerializeField]
    private RectTransform _photoTakenRectTransform;

    [Tooltip(
        "RawImage dùng để hiển thị tấm ảnh vừa chụp. " +
        "Kéo PhotoImage vào đây."
    )]
    [SerializeField]
    private RawImage _photoTakenImage;


    // ============================================================
    // PHOTO DROP EFFECT (rơi từ trên xuống vị trí cố định)
    // ============================================================

    [Header("Photo Drop Effect")]

    [Tooltip(
        "Nếu true: script tự lấy vị trí (anchoredPosition) hiện tại " +
        "của PhotoTakenEffect trong Editor làm điểm dừng cố định. " +
        "Đây chính là 'chỗ cố định mình set' trong Editor, không cần " +
        "gán gì thêm."
    )]
    [SerializeField]
    private bool _useCurrentPositionAsTarget = true;

    [Tooltip(
        "Chỉ dùng khi _useCurrentPositionAsTarget = false. " +
        "Vị trí (anchoredPosition) cố định mà ảnh sẽ rơi xuống."
    )]
    [SerializeField]
    private Vector2 _photoTargetAnchoredPosition;

    [Tooltip("Ảnh sẽ bắt đầu rơi từ độ cao này (pixel) phía trên điểm dừng.")]
    [SerializeField]
    private float _photoDropHeight = 900f;

    [Tooltip("Thời gian rơi xuống tới điểm dừng.")]
    [SerializeField]
    private float _photoDropDuration = 0.55f;

    [Tooltip(
        "Góc nghiêng ngẫu nhiên (độ) khi ảnh bắt đầu rơi, " +
        "để có cảm giác giống ảnh polaroid. Đặt 0 để không nghiêng."
    )]
    [SerializeField]
    private float _photoDropStartRotationRange = 12f;

    [Tooltip(
        "Scale phình ra khi ảnh vừa chạm điểm dừng (hiệu ứng 'khựng'). " +
        "1 = không phình."
    )]
    [SerializeField]
    private float _photoLandingBounceScale = 1.12f;


    // ============================================================
    // CAMERA FLASH (tùy chọn)
    // ============================================================

    [Header("Camera Flash (tùy chọn)")]

    [Tooltip(
        "CanvasGroup của một Image trắng full-screen, alpha = 0. " +
        "Để trống nếu không muốn hiệu ứng chớp sáng khi chụp."
    )]
    [SerializeField]
    private CanvasGroup _cameraFlashCanvasGroup;

    [Tooltip("Tổng thời gian chớp sáng (sáng lên rồi tắt).")]
    [SerializeField]
    private float _cameraFlashDuration = 0.35f;

    [Tooltip(
        "Alpha tối đa của flash (0-1). Để dưới 1 (vd 0.85) sẽ trông " +
        "tự nhiên hơn là chói trắng hoàn toàn."
    )]
    [SerializeField]
    [Range(0f, 1f)]
    private float _cameraFlashMaxAlpha = 0.85f;

    [Tooltip(
        "Tỉ lệ thời gian dành cho việc sáng LÊN so với tổng thời gian " +
        "(0-1). Flash thật bừng lên rất nhanh rồi tắt từ từ, nên để " +
        "nhỏ (vd 0.15 = 15% thời gian để sáng lên, 85% còn lại để tắt)."
    )]
    [SerializeField]
    [Range(0.05f, 0.9f)]
    private float _cameraFlashRiseRatio = 0.15f;


    // ============================================================
    // PHOTO EFFECT SETTINGS
    // ============================================================

    [Header("Photo Effect Settings")]

    [Tooltip("Thời gian giữ ảnh trên màn hình sau khi rơi xong.")]
    [SerializeField]
    private float _photoEffectVisibleDuration = 1.8f;

    [Tooltip("Thời gian fade out.")]
    [SerializeField]
    private float _photoEffectFadeOutDuration = 0.5f;


    // ============================================================
    // PHOTO SETTINGS
    // ============================================================

    [Header("Photo Settings")]

    [Tooltip(
        "Tên file ảnh được lưu trong Downloads."
    )]
    [SerializeField]
    private string _photoFilePrefix = "OceanLegacy_Photo";


    // ============================================================
    // RUNTIME
    // ============================================================

    private bool _isHoldingFish;

    private bool _isTakingPhoto;

    private Coroutine _photoEffectCoroutine;

    private Coroutine _cameraFlashCoroutine;

    // Texture hiện tại đang được RawImage sử dụng.
    private Texture2D _currentPhotoTexture;

    // Vị trí cố định (đích) mà ảnh sẽ rơi xuống.
    private Vector2 _photoTargetAnchoredPositionCached;

    public static event Action OnInspectFishPressed;


    // ============================================================
    // AWAKE
    // ============================================================

    private void Awake()
    {
        // --------------------------------------------------------
        // Fishing Rod Tutorial
        // --------------------------------------------------------

        if (_tutorialPanel != null)
        {
            _tutorialPanel.SetActive(false);
        }


        // --------------------------------------------------------
        // Inspect Fish Tutorial
        // --------------------------------------------------------

        if (_inspectFishPanel != null)
        {
            _inspectFishPanel.SetActive(false);
        }


        // --------------------------------------------------------
        // Take Photo UI
        // --------------------------------------------------------

        if (_takePhotoUI != null)
        {
            _takePhotoUI.SetActive(false);
        }


        // --------------------------------------------------------
        // Photo Timestamp
        // --------------------------------------------------------

        if (_photoTimestampText != null && !_timestampAlwaysVisible)
        {
            _photoTimestampText.gameObject.SetActive(false);
        }


        // --------------------------------------------------------
        // Camera Flash
        // --------------------------------------------------------

        if (_cameraFlashCanvasGroup != null)
        {
            _cameraFlashCanvasGroup.alpha = 0f;

            _cameraFlashCanvasGroup.gameObject.SetActive(false);
        }


        // --------------------------------------------------------
        // Photo Effect
        // --------------------------------------------------------

        SetupPhotoTakenEffect();


        // --------------------------------------------------------
        // Initial Text
        // --------------------------------------------------------

        SetAttachBaitText(false);

        SetInspectFishText(false);
    }


    // ============================================================
    // ENABLE
    // ============================================================

    private void OnEnable()
    {
        PlayerHeldItem.OnLocalHeldItemChanged += HandleHeldItemChanged;

        BaitBagUI.OnBagVisibilityChanged += SetAttachBaitText;

        FishInspectView.OnInspectStateChanged += HandleInspectStateChanged;
    }


    // ============================================================
    // DISABLE
    // ============================================================

    private void OnDisable()
    {
        PlayerHeldItem.OnLocalHeldItemChanged -= HandleHeldItemChanged;

        BaitBagUI.OnBagVisibilityChanged -= SetAttachBaitText;

        FishInspectView.OnInspectStateChanged -= HandleInspectStateChanged;


        if (_takePhotoUI != null)
        {
            _takePhotoUI.SetActive(false);
        }


        StopPhotoEffect();
    }


    // ============================================================
    // DESTROY
    // ============================================================

    private void OnDestroy()
    {
        // --------------------------------------------------------
        // Destroy current photo texture
        // --------------------------------------------------------

        if (_currentPhotoTexture != null)
        {
            Destroy(_currentPhotoTexture);

            _currentPhotoTexture = null;
        }
    }


    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        // --------------------------------------------------------
        // Không cầm cá
        // --------------------------------------------------------

        if (!_isHoldingFish)
        {
            return;
        }


        // --------------------------------------------------------
        // Đang chat
        // --------------------------------------------------------

        if (LobbyChatUI.IsTyping)
        {
            return;
        }


        // --------------------------------------------------------
        // Đang mở UI khác
        // --------------------------------------------------------

        if (PlayerCursor.IsUIOpen)
        {
            return;
        }


        // --------------------------------------------------------
        // Đang chụp ảnh
        // --------------------------------------------------------

        if (_isTakingPhoto)
        {
            return;
        }


        // ========================================================
        // C - INSPECT / EXIT INSPECT
        // ========================================================

        if (Input.GetKeyDown(KeyCode.C))
        {
            int subscriberCount =
                OnInspectFishPressed?.GetInvocationList().Length ?? 0;


            Debug.Log(
                $"[FishingTutorialUI] Bắt được phím C. " +
                $"Subscriber: {subscriberCount}"
            );


            if (subscriberCount == 0)
            {
                Debug.LogWarning(
                    "[FishingTutorialUI] Không có FishInspectView " +
                    "đang lắng nghe OnInspectFishPressed!"
                );
            }


            OnInspectFishPressed?.Invoke();
        }


        if (Input.GetKeyDown(KeyCode.P))
        {
            TakePhoto();
        }
    }


    // ============================================================
    // HANDLE HELD ITEM
    // ============================================================

    private void HandleHeldItemChanged(ItemData item)
    {
        bool isFishingRod =
            item != null &&
            item.IsFishingRod;


        bool isFish =
            item != null &&
            item.IsFish;


        // ========================================================
        // FISHING ROD TUTORIAL
        // ========================================================

        if (_tutorialPanel != null)
        {
            _tutorialPanel.SetActive(isFishingRod);
        }


        // ========================================================
        // INSPECT FISH TUTORIAL
        // ========================================================

        if (_inspectFishPanel != null)
        {
            _inspectFishPanel.SetActive(isFish);
        }


        // ========================================================
        // FISH
        // ========================================================

        if (isFish)
        {
            // Khi vừa cầm cá:
            // bắt đầu ở First Person.

            SetInspectFishText(false);


            // FIX: cho phép chụp ảnh ngay ở First Person (miễn là cầm cá),
            // không cần đợi bấm C vào Inspect nữa -> hiện UI gợi ý phím P
            // ngay lập tức.
            if (_takePhotoUI != null)
            {
                _takePhotoUI.SetActive(true);
            }
        }
        else
        {
            if (_takePhotoUI != null)
            {
                _takePhotoUI.SetActive(false);
            }
        }


        // ========================================================
        // SAVE STATE
        // ========================================================

        _isHoldingFish = isFish;


        Debug.Log(
            "[FishingTutorialUI] Held Item: " +
            $"{(item != null ? item.name : "NULL")} | " +
            $"FishingRod: {isFishingRod} | " +
            $"Fish: {isFish}"
        );
    }


    // ============================================================
    // ATTACH BAIT TEXT
    // ============================================================

    private void SetAttachBaitText(bool bagOpen)
    {
        if (_attachBaitText == null)
        {
            return;
        }


        _attachBaitText.text =
            bagOpen
                ? "(CLOSE) Attach Bait"
                : "Attach Bait";
    }


    // ============================================================
    // INSPECT STATE CHANGED
    // ============================================================

    private void HandleInspectStateChanged(bool isInspecting)
    {
        // --------------------------------------------------------
        // Inspect Fish Text
        // --------------------------------------------------------

        SetInspectFishText(isInspecting);


        // --------------------------------------------------------
        // Take Photo UI
        // --------------------------------------------------------
        //
        // FIX: KHÔNG còn set active theo isInspecting nữa.
        // _takePhotoUI giờ chỉ phụ thuộc vào việc có đang cầm cá hay
        // không (xem HandleHeldItemChanged) -> luôn hiện ở cả First
        // Person lẫn Inspect, cho phép chụp ảnh ở cả 2 chế độ.


        // --------------------------------------------------------
        // Debug
        // --------------------------------------------------------

        Debug.Log(
            "[FishingTutorialUI] Inspect State Changed: " +
            $"{isInspecting}"
        );


        if (isInspecting)
        {
            Debug.Log(
                "[FishingTutorialUI] THIRD PERSON / INSPECT -> " +
                "Take Photo UI ON."
            );
        }
        else
        {
            Debug.Log(
                "[FishingTutorialUI] FIRST PERSON -> " +
                "Take Photo UI OFF."
            );
        }
    }


    // ============================================================
    // SET INSPECT TEXT
    // ============================================================

    private void SetInspectFishText(bool isInspecting)
    {
        if (_inspectFishText == null)
        {
            return;
        }


        _inspectFishText.text =
            isInspecting
                ? _exitInspectPromptText
                : _inspectPromptText;
    }


    // ============================================================
    // SHOW / HIDE PHOTO TIMESTAMP
    // ============================================================

    private void ShowPhotoTimestamp()
    {
        if (_photoTimestampText == null)
        {
            return;
        }


        _photoTimestampText.text =
            DateTime.Now.ToString(_photoTimestampFormat);


        if (!_timestampAlwaysVisible)
        {
            _photoTimestampText.gameObject.SetActive(true);
        }
    }


    private void HidePhotoTimestampIfNeeded()
    {
        if (_photoTimestampText == null)
        {
            return;
        }


        if (!_timestampAlwaysVisible)
        {
            _photoTimestampText.gameObject.SetActive(false);
        }
    }


    // ============================================================
    // SETUP PHOTO TAKEN EFFECT
    // ============================================================

    private void SetupPhotoTakenEffect()
    {
        if (_photoTakenEffect == null)
        {
            return;
        }


        // --------------------------------------------------------
        // CanvasGroup
        // --------------------------------------------------------

        if (_photoTakenCanvasGroup == null)
        {
            _photoTakenCanvasGroup =
                _photoTakenEffect.GetComponent<CanvasGroup>();
        }


        if (_photoTakenCanvasGroup == null)
        {
            _photoTakenCanvasGroup =
                _photoTakenEffect.AddComponent<CanvasGroup>();
        }


        // --------------------------------------------------------
        // RectTransform + Target Position
        // --------------------------------------------------------

        if (_photoTakenRectTransform == null)
        {
            _photoTakenRectTransform =
                _photoTakenEffect.GetComponent<RectTransform>();
        }


        if (_photoTakenRectTransform != null)
        {
            // Điểm dừng cố định = vị trí bạn đã set sẵn trong Editor
            // (hoặc vị trí tùy chỉnh nếu _useCurrentPositionAsTarget = false).

            _photoTargetAnchoredPositionCached =
                _useCurrentPositionAsTarget
                    ? _photoTakenRectTransform.anchoredPosition
                    : _photoTargetAnchoredPosition;
        }


        // --------------------------------------------------------
        // Initial State
        // --------------------------------------------------------

        _photoTakenEffect.SetActive(false);

        _photoTakenCanvasGroup.alpha = 0f;

        _photoTakenCanvasGroup.interactable = false;

        _photoTakenCanvasGroup.blocksRaycasts = false;


        // --------------------------------------------------------
        // Photo Image
        // --------------------------------------------------------

        if (_photoTakenImage != null)
        {
            _photoTakenImage.texture = null;

            _photoTakenImage.uvRect =
                new Rect(
                    0f,
                    0f,
                    1f,
                    1f
                );
        }
    }


    // ============================================================
    // TAKE PHOTO
    // ============================================================

    private void TakePhoto()
    {
        // --------------------------------------------------------
        // SAFETY CHECK 1 (ĐÃ BỎ)
        // --------------------------------------------------------
        //
        // FIX: trước đây bắt buộc phải đang Inspect (NetPlayerLook.
        // IsInspectingFish) mới cho chụp. Giờ chỉ cần đang cầm cá là
        // đủ (check ở SAFETY CHECK 2 ngay dưới), cho phép chụp ở cả
        // First Person.


        // --------------------------------------------------------
        // SAFETY CHECK 2
        // --------------------------------------------------------

        if (!_isHoldingFish)
        {
            Debug.Log(
                "[FishingTutorialUI] Không thể chụp: " +
                "Player không cầm cá."
            );

            return;
        }


        // --------------------------------------------------------
        // SAFETY CHECK 3
        // --------------------------------------------------------

        if (_isTakingPhoto)
        {
            return;
        }


        // --------------------------------------------------------
        // START
        // --------------------------------------------------------

        StartCoroutine(
            TakePhotoRoutine()
        );
    }


    // ============================================================
    // TAKE PHOTO ROUTINE
    // ============================================================

    private IEnumerator TakePhotoRoutine()
    {
        _isTakingPhoto = true;


        Debug.Log(
            "[FishingTutorialUI] ============================="
        );

        Debug.Log(
            "[FishingTutorialUI] BẮT ĐẦU CHỤP ẢNH..."
        );


        // ========================================================
        // 1. HIDE P-UI
        // ========================================================

        HidePhotoCaptureUI();


        // ========================================================
        // 1.5 SHOW TIMESTAMP (để nó xuất hiện trong ảnh)
        // ========================================================

        ShowPhotoTimestamp();


        // ========================================================
        // 2. FORCE UI UPDATE
        // ========================================================

        Canvas.ForceUpdateCanvases();


        // ========================================================
        // 3. WAIT FOR END OF FRAME
        // ========================================================

        yield return new WaitForEndOfFrame();


        // ========================================================
        // 4. CAPTURE SCREEN
        // ========================================================

        Texture2D screenshot = null;


        try
        {
            screenshot =
                ScreenCapture.CaptureScreenshotAsTexture();

            // ----------------------------------------------------
            // FIX MÀU BỊ PHAI TRẮNG
            // ----------------------------------------------------
            //
            // Texture trả về từ CaptureScreenshotAsTexture() có thể
            // bị GPU đọc sai không gian màu (Gamma/Linear) khi
            // project dùng Color Space = Linear, khiến ảnh bị
            // convert màu 2 lần -> cháy sáng, bạc trắng.
            //
            // Tạo lại 1 Texture2D mới, đánh dấu đúng là ảnh sRGB
            // thông thường, để tránh bị convert sai lần nữa.
            // ----------------------------------------------------

            screenshot = FixScreenshotColor(screenshot);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[FishingTutorialUI] " +
                "CaptureScreenshotAsTexture failed: " +
                exception
            );
        }


        // ========================================================
        // 5. SAVE SCREENSHOT
        // ========================================================

        bool saveSuccess = false;

        string savedPath = string.Empty;


        if (screenshot != null)
        {
            saveSuccess =
                SaveScreenshotToDownloads(
                    screenshot,
                    out savedPath
                );
        }


        // ========================================================
        // 6. SET PHOTO TEXTURE
        // ========================================================

        if (saveSuccess && screenshot != null)
        {
            SetPhotoTexture(screenshot);

            // ----------------------------------------------------
            // QUAN TRỌNG
            // ----------------------------------------------------
            //
            // Không Destroy screenshot ở đây.
            //
            // Vì RawImage đang sử dụng Texture2D này.
            //
            // Texture sẽ được Destroy khi:
            // 1. Chụp ảnh mới.
            // 2. FishingTutorialUI bị Destroy.
            //
            screenshot = null;
        }


        // ========================================================
        // 6.5 HIDE TIMESTAMP LẠI
        // ========================================================

        HidePhotoTimestampIfNeeded();


        // ========================================================
        // 7. RESTORE P-UI
        // ========================================================

        RestorePhotoCaptureUI();


        // ========================================================
        // 8. SHOW PHOTO EFFECT
        // ========================================================

        if (saveSuccess)
        {
            ShowPhotoTakenEffect();


            Debug.Log(
                "[FishingTutorialUI] ẢNH ĐÃ ĐƯỢC LƯU THÀNH CÔNG!"
            );


            Debug.Log(
                $"[FishingTutorialUI] Saved: {savedPath}"
            );
        }
        else
        {
            Debug.LogError(
                "[FishingTutorialUI] KHÔNG THỂ LƯU ẢNH!"
            );
        }


        // ========================================================
        // 9. DESTROY SCREENSHOT IF NOT USED
        // ========================================================

        if (screenshot != null)
        {
            Destroy(screenshot);
        }


        // ========================================================
        // 10. FINISH
        // ========================================================

        _isTakingPhoto = false;


        Debug.Log(
            "[FishingTutorialUI] ============================="
        );
    }


    // ============================================================
    // FIX SCREENSHOT COLOR (chống phai trắng)
    // ============================================================

    private Texture2D FixScreenshotColor(Texture2D source)
    {
        if (source == null)
        {
            return null;
        }


        Texture2D fixedTexture =
            new Texture2D(
                source.width,
                source.height,
                TextureFormat.RGB24,
                false,  // mipChain
                false   // linear = false -> đọc như ảnh sRGB thông thường
            );


        fixedTexture.SetPixels32(
            source.GetPixels32()
        );

        fixedTexture.Apply(
            false,
            false
        );


        Destroy(source);


        return fixedTexture;
    }


    // ============================================================
    // SET PHOTO TEXTURE
    // ============================================================

    private void SetPhotoTexture(Texture2D screenshot)
    {
        if (screenshot == null)
        {
            return;
        }


        if (_photoTakenImage == null)
        {
            Debug.LogWarning(
                "[FishingTutorialUI] " +
                "_photoTakenImage chưa được gán!"
            );

            return;
        }


        // --------------------------------------------------------
        // Destroy texture cũ
        // --------------------------------------------------------

        if (_currentPhotoTexture != null)
        {
            Destroy(
                _currentPhotoTexture
            );

            _currentPhotoTexture = null;
        }


        // --------------------------------------------------------
        // Lưu texture mới
        // --------------------------------------------------------

        _currentPhotoTexture =
            screenshot;


        // --------------------------------------------------------
        // Assign vào RawImage
        // --------------------------------------------------------

        _photoTakenImage.texture =
            _currentPhotoTexture;


        // --------------------------------------------------------
        // UV
        // --------------------------------------------------------

        _photoTakenImage.uvRect =
            new Rect(
                0f,
                0f,
                1f,
                1f
            );


        Debug.Log(
            "[FishingTutorialUI] " +
            "Photo Texture đã được gán vào RawImage."
        );
    }


    // ============================================================
    // HIDE PHOTO CAPTURE UI
    // ============================================================

    private void HidePhotoCaptureUI()
    {
        if (_photoCaptureCanvasGroup == null)
        {
            Debug.LogWarning(
                "[FishingTutorialUI] " +
                "_photoCaptureCanvasGroup chưa được gán!"
            );

            return;
        }

        if (_photoTakenEffect != null && _photoTakenEffect.activeSelf)
        {
            StopPhotoEffect();
        }


        _photoCaptureCanvasGroup.alpha = 0f;

        _photoCaptureCanvasGroup.interactable = false;

        _photoCaptureCanvasGroup.blocksRaycasts = false;


        Debug.Log(
            "[FishingTutorialUI] P-UI -> HIDDEN FOR PHOTO."
        );
    }


    // ============================================================
    // RESTORE PHOTO CAPTURE UI
    // ============================================================

    private void RestorePhotoCaptureUI()
    {
        if (_photoCaptureCanvasGroup == null)
        {
            return;
        }


        _photoCaptureCanvasGroup.alpha = 1f;

        _photoCaptureCanvasGroup.interactable = true;

        _photoCaptureCanvasGroup.blocksRaycasts = true;


        Debug.Log(
            "[FishingTutorialUI] P-UI -> RESTORED."
        );
    }


    // ============================================================
    // SAVE SCREENSHOT
    // ============================================================

    private bool SaveScreenshotToDownloads(
        Texture2D screenshot,
        out string savedPath)
    {
        savedPath = string.Empty;


        if (screenshot == null)
        {
            return false;
        }


        try
        {
            // ====================================================
            // DOWNLOADS PATH
            // ====================================================

            string downloadsPath =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile
                    ),
                    "Downloads"
                );


            // ====================================================
            // CREATE FOLDER
            // ====================================================

            if (!Directory.Exists(downloadsPath))
            {
                Directory.CreateDirectory(
                    downloadsPath
                );
            }


            // ====================================================
            // FILE NAME
            // ====================================================

            string fileName =
                $"{_photoFilePrefix}_" +
                $"{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";


            // ====================================================
            // FULL PATH
            // ====================================================

            savedPath =
                Path.Combine(
                    downloadsPath,
                    fileName
                );


            // ====================================================
            // ENCODE PNG
            // ====================================================

            byte[] pngData =
                screenshot.EncodeToPNG();


            if (pngData == null ||
                pngData.Length == 0)
            {
                Debug.LogError(
                    "[FishingTutorialUI] " +
                    "EncodeToPNG failed."
                );

                return false;
            }


            // ====================================================
            // WRITE FILE
            // ====================================================

            File.WriteAllBytes(
                savedPath,
                pngData
            );


            // ====================================================
            // CHECK FILE
            // ====================================================

            if (!File.Exists(savedPath))
            {
                Debug.LogError(
                    "[FishingTutorialUI] File không tồn tại sau " +
                    "khi WriteAllBytes."
                );

                return false;
            }


            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[FishingTutorialUI] " +
                "Lỗi khi lưu screenshot: " +
                exception
            );

            return false;
        }
    }


    // ============================================================
    // SHOW PHOTO TAKEN EFFECT
    // ============================================================

    private void ShowPhotoTakenEffect()
    {
        if (_photoTakenEffect == null)
        {
            Debug.LogWarning(
                "[FishingTutorialUI] " +
                "_photoTakenEffect chưa được gán."
            );

            return;
        }


        // --------------------------------------------------------
        // Stop effect cũ
        // --------------------------------------------------------

        StopPhotoEffect();


        // --------------------------------------------------------
        // Setup CanvasGroup
        // --------------------------------------------------------

        if (_photoTakenCanvasGroup == null)
        {
            _photoTakenCanvasGroup =
                _photoTakenEffect.GetComponent<CanvasGroup>();
        }


        if (_photoTakenCanvasGroup == null)
        {
            _photoTakenCanvasGroup =
                _photoTakenEffect.AddComponent<CanvasGroup>();
        }


        if (_photoTakenRectTransform == null)
        {
            _photoTakenRectTransform =
                _photoTakenEffect.GetComponent<RectTransform>();
        }


        // --------------------------------------------------------
        // Activate - hiện ngay, hiệu ứng chính nằm ở việc RƠI
        // --------------------------------------------------------

        _photoTakenEffect.SetActive(true);

        _photoTakenCanvasGroup.alpha = 1f;

        _photoTakenCanvasGroup.interactable = false;

        _photoTakenCanvasGroup.blocksRaycasts = false;


        // --------------------------------------------------------
        // Đặt vị trí bắt đầu: phía trên điểm dừng cố định,
        // nghiêng ngẫu nhiên một chút cho giống ảnh polaroid.
        // --------------------------------------------------------

        if (_photoTakenRectTransform != null)
        {
            Vector2 startPos =
                _photoTargetAnchoredPositionCached +
                new Vector2(0f, _photoDropHeight);

            _photoTakenRectTransform.anchoredPosition = startPos;


            float randomTilt =
                _photoDropStartRotationRange > 0f
                    ? UnityEngine.Random.Range(
                        -_photoDropStartRotationRange,
                        _photoDropStartRotationRange
                    )
                    : 0f;

            _photoTakenRectTransform.localRotation =
                Quaternion.Euler(0f, 0f, randomTilt);
        }


        _photoTakenEffect.transform.localScale = Vector3.one;


        // --------------------------------------------------------
        // Flash chụp ảnh (tùy chọn)
        // --------------------------------------------------------

        PlayCameraFlash();


        // --------------------------------------------------------
        // Start animation
        // --------------------------------------------------------

        _photoEffectCoroutine =
            StartCoroutine(
                PhotoTakenEffectRoutine()
            );
    }


    // ============================================================
    // PHOTO EFFECT ROUTINE (rơi xuống -> giữ -> fade out)
    // ============================================================

    private IEnumerator PhotoTakenEffectRoutine()
    {
        if (_photoTakenEffect == null ||
            _photoTakenCanvasGroup == null)
        {
            yield break;
        }


        // ========================================================
        // DROP DOWN
        // ========================================================

        if (_photoTakenRectTransform != null)
        {
            float dropDuration =
                Mathf.Max(
                    0.01f,
                    _photoDropDuration
                );


            float t = 0f;


            Vector2 startPos =
                _photoTakenRectTransform.anchoredPosition;

            Vector2 targetPos =
                _photoTargetAnchoredPositionCached;


            Quaternion startRot =
                _photoTakenRectTransform.localRotation;

            Quaternion targetRot =
                Quaternion.identity;


            Vector3 startScale =
                Vector3.one *
                Mathf.Max(1f, _photoLandingBounceScale);

            Vector3 targetScale =
                Vector3.one;


            while (t < dropDuration)
            {
                t += Time.deltaTime;


                float progress =
                    Mathf.Clamp01(
                        t / dropDuration
                    );


                // Ease Out Back: rơi xuống, hơi lố qua điểm dừng
                // rồi khựng nhẹ lại đúng vị trí -> cảm giác thật hơn
                // rơi thẳng đơ.

                float eased =
                    EaseOutBack(progress);


                _photoTakenRectTransform.anchoredPosition =
                    Vector2.LerpUnclamped(
                        startPos,
                        targetPos,
                        eased
                    );


                _photoTakenRectTransform.localRotation =
                    Quaternion.Slerp(
                        startRot,
                        targetRot,
                        progress
                    );


                _photoTakenEffect.transform.localScale =
                    Vector3.LerpUnclamped(
                        startScale,
                        targetScale,
                        Mathf.Clamp01(progress * 1.4f)
                    );


                yield return null;
            }


            _photoTakenRectTransform.anchoredPosition = targetPos;

            _photoTakenRectTransform.localRotation = targetRot;

            _photoTakenEffect.transform.localScale = targetScale;
        }


        // ========================================================
        // HOLD
        // ========================================================

        if (_photoEffectVisibleDuration > 0f)
        {
            yield return new WaitForSeconds(
                _photoEffectVisibleDuration
            );
        }


        // ========================================================
        // FADE OUT
        // ========================================================

        float fadeOutDuration =
            Mathf.Max(
                0.01f,
                _photoEffectFadeOutDuration
            );


        float tf = 0f;


        while (tf < fadeOutDuration)
        {
            tf += Time.deltaTime;


            float progress =
                Mathf.Clamp01(
                    tf / fadeOutDuration
                );


            _photoTakenCanvasGroup.alpha =
                1f - progress;


            yield return null;
        }


        // ========================================================
        // RESET
        // ========================================================

        _photoTakenCanvasGroup.alpha = 0f;


        _photoTakenEffect.transform.localScale =
            Vector3.one;


        _photoTakenEffect.SetActive(false);


        _photoEffectCoroutine = null;
    }


    // ============================================================
    // EASE OUT BACK
    // ============================================================
    //
    // Công thức easing chuẩn "easeOutBack": tiến vượt qua đích
    // một chút (overshoot) rồi lùi lại đúng đích, cho cảm giác
    // "rớt xuống và khựng nhẹ" giống vật thể thật rơi.
    // ============================================================

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        float t = x - 1f;

        return 1f + c3 * (t * t * t) + c1 * (t * t);
    }


    // ============================================================
    // CAMERA FLASH
    // ============================================================

    private void PlayCameraFlash()
    {
        if (_cameraFlashCanvasGroup == null)
        {
            return;
        }


        if (_cameraFlashCoroutine != null)
        {
            StopCoroutine(_cameraFlashCoroutine);
        }


        _cameraFlashCoroutine =
            StartCoroutine(
                CameraFlashRoutine()
            );
    }


    private IEnumerator CameraFlashRoutine()
    {
        if (_cameraFlashCanvasGroup == null)
        {
            yield break;
        }


        float duration =
            Mathf.Max(
                0.01f,
                _cameraFlashDuration
            );

        // --------------------------------------------------------
        // Rise nhanh, decay chậm hơn nhiều
        // --------------------------------------------------------
        //
        // Flash thật: bừng lên gần như tức thì, rồi mờ dần từ từ.
        // Chia đôi đều 50/50 như trước trông rất giả (tam giác đối
        // xứng). Ở đây rise chỉ chiếm một phần nhỏ tổng thời gian.
        // --------------------------------------------------------

        float riseDuration =
            Mathf.Max(
                0.01f,
                duration * _cameraFlashRiseRatio
            );

        float decayDuration =
            Mathf.Max(
                0.01f,
                duration - riseDuration
            );

        float maxAlpha =
            Mathf.Clamp01(_cameraFlashMaxAlpha);


        _cameraFlashCanvasGroup.gameObject.SetActive(true);


        // ========================================================
        // RISE (ease out - bừng nhanh, khựng nhẹ ở đỉnh)
        // ========================================================

        float t = 0f;

        while (t < riseDuration)
        {
            t += Time.deltaTime;

            float progress =
                Mathf.Clamp01(t / riseDuration);

            float eased =
                EaseOutQuad(progress);

            _cameraFlashCanvasGroup.alpha =
                eased * maxAlpha;

            yield return null;
        }

        _cameraFlashCanvasGroup.alpha = maxAlpha;


        // ========================================================
        // DECAY (ease out - tắt nhanh lúc đầu, mờ dần chậm rãi)
        // ========================================================

        t = 0f;

        while (t < decayDuration)
        {
            t += Time.deltaTime;

            float progress =
                Mathf.Clamp01(t / decayDuration);

            float eased =
                EaseOutQuad(progress);

            _cameraFlashCanvasGroup.alpha =
                maxAlpha * (1f - eased);

            yield return null;
        }


        _cameraFlashCanvasGroup.alpha = 0f;

        _cameraFlashCanvasGroup.gameObject.SetActive(false);

        _cameraFlashCoroutine = null;
    }


    // ============================================================
    // EASE OUT QUAD
    // ============================================================
    //
    // Dùng cho flash: tiến nhanh lúc đầu rồi chậm dần lúc cuối,
    // tự nhiên hơn nhiều so với nội suy tuyến tính.
    // ============================================================

    private static float EaseOutQuad(float x)
    {
        return 1f - (1f - x) * (1f - x);
    }


    // ============================================================
    // STOP PHOTO EFFECT
    // ============================================================

    private void StopPhotoEffect()
    {
        if (_photoEffectCoroutine != null)
        {
            StopCoroutine(
                _photoEffectCoroutine
            );

            _photoEffectCoroutine = null;
        }


        if (_cameraFlashCoroutine != null)
        {
            StopCoroutine(_cameraFlashCoroutine);

            _cameraFlashCoroutine = null;
        }


        if (_photoTakenCanvasGroup != null)
        {
            _photoTakenCanvasGroup.alpha = 0f;
        }


        if (_photoTakenEffect != null)
        {
            _photoTakenEffect.SetActive(false);

            _photoTakenEffect.transform.localScale =
                Vector3.one;
        }


        if (_cameraFlashCanvasGroup != null)
        {
            _cameraFlashCanvasGroup.alpha = 0f;

            _cameraFlashCanvasGroup.gameObject.SetActive(false);
        }
    }
}