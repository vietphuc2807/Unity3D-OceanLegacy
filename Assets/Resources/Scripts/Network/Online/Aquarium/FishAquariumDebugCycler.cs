using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cong cu DEBUG rieng, giup chinh nhanh vi tri / xoay (Position,
/// Rotation trong khong gian 3D) cua mo hinh tung loai ca KHI TRUNG BAY
/// TRONG BE o Museum, ngay trong Play mode, khong can bat ca that + bam
/// nut trong panel moi lan muon xem thu.
///
/// KICH CO (SCALE): kich co hien thi trong be la SAN PHAM cua
/// "AquariumScale CHUAN" (canh o day, phim -/= dung de chinh CAI CHUAN
/// nay, o size multiplier = 1) NHAN VOI 1 "size multiplier debug" -
/// mo phong dung cong thuc that AquariumDisplayManager.PlaceFish dang
/// dung (AquariumScale * sizeMultiplier luc cau ca).
///
/// TICK NHANH MIN/MAX SIZE (moi): 2 checkbox trong Inspector -
/// "Preview Min Size" va "Preview Max Size" - tick vao 1 trong 2 se
/// LAP TUC doi preview sang dung FishData.MinSizeMultiplier hoac
/// MaxSizeMultiplier cua con ca dang xem (VD con ca sau o 1 KG hay
/// 100 KG se trong nhu the nao trong be), khong can nho phim tat. Tick
/// 1 o se TU BO TICK o con lai (chi preview 1 muc tai 1 thoi diem).
/// Bo tick ca 2 se quay ve size multiplier = 1 (kich co CHUAN).
///
/// Cach dung:
///
/// 1) Gan script nay len 1 GameObject trong scene Museum.
/// 2) Keo Transform goc cua be kinh (cho ca se dung, thuong trung
///    _spawnRoot cua AquariumDisplayManager) vao field Spawn Root.
/// 3) Keo danh sach cac ItemData ca muon test vao Fish To Test (ca can
///    co san FishData - IsFish = true - de doc duoc Min/Max Size).
/// 4) Bam Play, roi:
///
///    - Tick "Preview Min Size" / "Preview Max Size" trong Inspector
///      de xem nhanh o size nho nhat / lon nhat (cach nhanh nhat).
///
///    Hoac dung phim (van con day du nhu cu):
///    - Q / E                    : chuyen sang con ca truoc / sau
///                                  trong list (tu xoa preview cu,
///                                  spawn preview moi, bo tick Min/Max,
///                                  reset size multiplier debug ve 1).
///    - Mui ten trai/phai        : di chuyen theo truc X.
///    - Mui ten len/xuong        : di chuyen theo truc Z.
///    - Page Up / Page Down      : di chuyen theo truc Y (do cao).
///    - [ / ]                    : xoay quanh truc Y (quay dau ca).
///    - Shift + [ / ]            : xoay quanh truc X.
///    - Ctrl + [ / ]             : xoay quanh truc Z.
///    - - / =                    : giam / tang AQUARIUM SCALE CHUAN
///                                  (kich co luu vao ItemData, tuc kich
///                                  co o size multiplier = 1).
///    - , / .                    : nudge size multiplier debug (buoc
///                                  nho _sizeMultiplierStep) de do cac
///                                  muc trung gian giua Min va Max - tu
///                                  dong bo tick ca 2 checkbox Min/Max.
///    - R                        : reset ve dung Aquarium Position/
///                                  Rotation da luu san trong ItemData
///                                  cua con ca dang xem, bo tick Min/
///                                  Max, va reset size multiplier debug
///                                  ve 1.
///    - P                        : in gia tri Position/Rotation/
///                                  AquariumScale CHUAN hien tai + size
///                                  multiplier debug dang xem + can
///                                  nang tuong ung ra Console - COPY 3
///                                  dong Position/Rotation/Scale dan
///                                  lai vao ItemData (muc "Aquarium
///                                  Display", o Project window) de luu
///                                  vinh vien.
///
/// 5) NGOAI RA, ban cung co the go THANG so vao 3 field Aquarium
///    Position / Rotation / Scale cua ItemData (o Inspector, luc dang
///    Play) - script se TU DONG phat hien gia tri asset thay doi va
///    ap ngay vao preview trong be (van nhan them size multiplier debug
///    dang chon), khong can bam R.
///
/// Script nay CHI de test/debug - khong anh huong gi den luong choi
/// that (FishReturnPanelUI / AquariumDisplayManager), co the xoa
/// GameObject chua no truoc khi build ban chinh thuc.
/// </summary>
public class FishAquariumDebugCycler : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private Transform _spawnRoot;
    [SerializeField] private List<ItemData> _fishToTest = new();

    [Header("Quick Preview (tick de xem nhanh Min/Max theo can nang)")]
    [Tooltip("Tick de xem preview o size NHO NHAT (FishData.MinSizeMultiplier) cua con ca dang chon. Tick o nay se tu bo tick 'Preview Max Size'.")]
    [SerializeField] private bool _previewMinSize;
    [Tooltip("Tick de xem preview o size LON NHAT (FishData.MaxSizeMultiplier) cua con ca dang chon. Tick o nay se tu bo tick 'Preview Min Size'.")]
    [SerializeField] private bool _previewMaxSize;

    [Header("Nudge Step")]
    [SerializeField] private float _moveStep = 0.1f;
    [SerializeField] private float _rotateStep = 15f;
    [SerializeField] private float _scaleStep = 0.1f;
    [Tooltip("Scale toi thieu (moi truc, AQUARIUM SCALE CHUAN) de tranh ca bi am/bien mat.")]
    [SerializeField] private float _minScale = 0.01f;

    [Header("Size Multiplier Debug (phim , / . - nudge tinh hon giua Min/Max)")]
    [Tooltip("Buoc nudge cho phim , / . khi dieu chinh size multiplier debug.")]
    [SerializeField] private float _sizeMultiplierStep = 0.05f;

    private int _index;
    private GameObject _previewInstance;

    // "AQUARIUM SCALE CHUAN" - kich co o size multiplier = 1, chinh
    // bang phim -/= , se duoc luu vao ItemData.AquariumScale.
    private Vector3 _currentPosition;
    private Vector3 _currentRotation;
    private Vector3 _currentBaseScale = Vector3.one;

    // Size multiplier DANG XEM THU (tick Min/Max hoac phim ,/.) - nhan
    // vao _currentBaseScale de mo phong dung cong thuc that cua
    // AquariumDisplayManager.PlaceFish. KHONG luu vao ItemData - chi
    // de xem truoc.
    private float _debugSizeMultiplier = 1f;

    // Gia tri tick Min/Max frame truoc - de phat hien nguoi dung vua
    // tick/bo tick trong Inspector.
    private bool _lastPreviewMinSize;
    private bool _lastPreviewMaxSize;

    // Gia tri Aquarium cua ItemData tai lan doc gan nhat - de phat
    // hien luc ban go so thang vao Inspector cua ItemData thay vi
    // nudge bang phim.
    private Vector3 _lastAssetPosition;
    private Vector3 _lastAssetRotation;
    private Vector3 _lastAssetScale;

    private void Start()
    {
        if (_fishToTest.Count > 0)
            ShowFish(0);
    }

    private void Update()
    {
        if (_spawnRoot == null || _fishToTest.Count == 0)
            return;

        HandleCycleInput();
        HandleNudgeInput();
        HandleQuickPreviewToggles();
        HandleSizeMultiplierNudgeInput();
        HandleUtilityInput();
        CheckLiveAssetEdits();
    }

    // ============================================================
    // CYCLE FISH (Q / E)
    // ============================================================

    private void HandleCycleInput()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            int newIndex = (_index - 1 + _fishToTest.Count) % _fishToTest.Count;
            ShowFish(newIndex);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            int newIndex = (_index + 1) % _fishToTest.Count;
            ShowFish(newIndex);
        }
    }

    // ============================================================
    // NUDGE POSITION / ROTATION / BASE SCALE
    // ============================================================

    private void HandleNudgeInput()
    {
        bool changed = false;

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            _currentPosition.x -= _moveStep;
            changed = true;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            _currentPosition.x += _moveStep;
            changed = true;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            _currentPosition.z += _moveStep;
            changed = true;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            _currentPosition.z -= _moveStep;
            changed = true;
        }

        if (Input.GetKeyDown(KeyCode.PageUp))
        {
            _currentPosition.y += _moveStep;
            changed = true;
        }

        if (Input.GetKeyDown(KeyCode.PageDown))
        {
            _currentPosition.y -= _moveStep;
            changed = true;
        }

        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        if (Input.GetKeyDown(KeyCode.LeftBracket))
        {
            if (shift) _currentRotation.x -= _rotateStep;
            else if (ctrl) _currentRotation.z -= _rotateStep;
            else _currentRotation.y -= _rotateStep;
            changed = true;
        }

        if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            if (shift) _currentRotation.x += _rotateStep;
            else if (ctrl) _currentRotation.z += _rotateStep;
            else _currentRotation.y += _rotateStep;
            changed = true;
        }

        if (Input.GetKeyDown(KeyCode.Equals))
        {
            _currentBaseScale += Vector3.one * _scaleStep;
            changed = true;
        }

        if (Input.GetKeyDown(KeyCode.Minus))
        {
            _currentBaseScale -= Vector3.one * _scaleStep;
            _currentBaseScale = Vector3.Max(_currentBaseScale, Vector3.one * _minScale);
            changed = true;
        }

        if (changed)
        {
            ApplyToPreview();
        }
    }

    // ============================================================
    // QUICK PREVIEW TOGGLES (tick "Preview Min Size" / "Preview Max Size")
    // ============================================================

    private void HandleQuickPreviewToggles()
    {
        bool minJustChanged = _previewMinSize != _lastPreviewMinSize;
        bool maxJustChanged = _previewMaxSize != _lastPreviewMaxSize;

        if (!minJustChanged && !maxJustChanged)
            return;

        ItemData fish = _fishToTest[_index];
        FishData fishData = (fish != null && fish.IsFish) ? fish.FishInfo : null;

        if (fishData == null)
        {
            // Con ca nay khong co FishData nen khong co Min/Max de xem -
            // tu bo tick lai ca 2 de tranh hieu lam.
            _previewMinSize = false;
            _previewMaxSize = false;
            _lastPreviewMinSize = false;
            _lastPreviewMaxSize = false;
            return;
        }

        if (minJustChanged && _previewMinSize)
        {
            // Vua tick Min -> bo tick Max, xem o size nho nhat.
            _previewMaxSize = false;
            _debugSizeMultiplier = fishData.MinSizeMultiplier;
        }
        else if (maxJustChanged && _previewMaxSize)
        {
            // Vua tick Max -> bo tick Min, xem o size lon nhat.
            _previewMinSize = false;
            _debugSizeMultiplier = fishData.MaxSizeMultiplier;
        }
        else if (minJustChanged && !_previewMinSize && !_previewMaxSize)
        {
            // Vua BO tick Min (va Max cung dang tat) -> ve size = 1.
            _debugSizeMultiplier = 1f;
        }
        else if (maxJustChanged && !_previewMaxSize && !_previewMinSize)
        {
            // Vua BO tick Max (va Min cung dang tat) -> ve size = 1.
            _debugSizeMultiplier = 1f;
        }

        _lastPreviewMinSize = _previewMinSize;
        _lastPreviewMaxSize = _previewMaxSize;

        ApplyToPreview();

        float weightKg = fishData.GetWeightKg(_debugSizeMultiplier);
        Debug.Log(
            $"[FishAquariumDebugCycler] Size multiplier debug = " +
            $"{_debugSizeMultiplier:0.###} (~{weightKg:0.##} KG)"
        );
    }

    // ============================================================
    // SIZE MULTIPLIER DEBUG (phim , / .) - nudge tinh hon giua Min/Max
    // ============================================================

    private void HandleSizeMultiplierNudgeInput()
    {
        ItemData fish = _fishToTest[_index];

        if (fish == null || !fish.IsFish)
            return;

        FishData fishData = fish.FishInfo;
        bool changed = false;

        if (Input.GetKeyDown(KeyCode.Comma))
        {
            _debugSizeMultiplier = Mathf.Max(0.01f, _debugSizeMultiplier - _sizeMultiplierStep);
            changed = true;
        }

        if (Input.GetKeyDown(KeyCode.Period))
        {
            _debugSizeMultiplier += _sizeMultiplierStep;
            changed = true;
        }

        if (changed)
        {
            // Nudge bang tay -> khong con khop chinh xac Min/Max nua,
            // bo tick ca 2 checkbox de tranh gay hieu lam trang thai.
            _previewMinSize = false;
            _previewMaxSize = false;
            _lastPreviewMinSize = false;
            _lastPreviewMaxSize = false;

            ApplyToPreview();

            float weightKg = fishData.GetWeightKg(_debugSizeMultiplier);
            Debug.Log(
                $"[FishAquariumDebugCycler] Size multiplier debug = " +
                $"{_debugSizeMultiplier:0.###} (~{weightKg:0.##} KG)"
            );
        }
    }

    // ============================================================
    // LIVE-SYNC KHI GO SO THANG VAO ITEMDATA (INSPECTOR)
    // ============================================================

    private void CheckLiveAssetEdits()
    {
        ItemData fish = _fishToTest[_index];

        if (fish == null)
            return;

        bool assetChanged =
            fish.AquariumPosition != _lastAssetPosition ||
            fish.AquariumRotation != _lastAssetRotation ||
            fish.AquariumScale != _lastAssetScale;

        if (!assetChanged)
            return;

        _lastAssetPosition = fish.AquariumPosition;
        _lastAssetRotation = fish.AquariumRotation;
        _lastAssetScale = fish.AquariumScale;

        _currentPosition = _lastAssetPosition;
        _currentRotation = _lastAssetRotation;
        _currentBaseScale = _lastAssetScale;

        ApplyToPreview();
    }

    // ============================================================
    // RESET (R) / PRINT (P)
    // ============================================================

    private void HandleUtilityInput()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            ShowFish(_index);
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            ItemData currentFish = _fishToTest[_index];

            string weightInfo = "";

            if (currentFish.IsFish)
            {
                float weightKg = currentFish.FishInfo.GetWeightKg(_debugSizeMultiplier);
                float displayMultiplier = currentFish.GetAquariumDisplaySizeMultiplier(_debugSizeMultiplier);

                weightInfo =
                    $", ~{weightKg:0.##} KG o size multiplier THAT {_debugSizeMultiplier:0.###} " +
                    $"(size multiplier HIEN THI sau khi kep = {displayMultiplier:0.###})";
            }

            Debug.Log(
                $"[FishAquariumDebugCycler] {currentFish.name} -> " +
                $"Aquarium Position = {_currentPosition}, " +
                $"Aquarium Rotation = {_currentRotation}, " +
                $"Aquarium Scale (CHUAN, o multiplier = 1) = {_currentBaseScale}" +
                weightInfo
            );
        }
    }

    // ============================================================
    // SHOW FISH
    // ============================================================

    private void ShowFish(int index)
    {
        _index = index;

        ItemData fish = _fishToTest[_index];

        if (_previewInstance != null)
        {
            Destroy(_previewInstance);
            _previewInstance = null;
        }

        // Doi ca -> bo tick Min/Max, ve size = 1 mac dinh.
        _previewMinSize = false;
        _previewMaxSize = false;
        _lastPreviewMinSize = false;
        _lastPreviewMaxSize = false;
        _debugSizeMultiplier = 1f;

        if (fish == null)
            return;

        GameObject prefab = fish.AquariumPrefab;

        if (prefab != null)
        {
            _previewInstance = Instantiate(prefab, _spawnRoot);
        }
        else
        {
            Debug.LogWarning(
                $"[FishAquariumDebugCycler] {fish.name} chua co Aquarium " +
                "Prefab lan FishData.WorldPrefab, khong co gi de preview."
            );
        }

        _currentPosition = fish.AquariumPosition;
        _currentRotation = fish.AquariumRotation;
        _currentBaseScale = fish.AquariumScale;

        _lastAssetPosition = _currentPosition;
        _lastAssetRotation = _currentRotation;
        _lastAssetScale = _currentBaseScale;

        ApplyToPreview();

        Debug.Log($"[FishAquariumDebugCycler] Dang xem: {fish.name}");
    }

    private void ApplyToPreview()
    {
        if (_previewInstance == null)
            return;

        _previewInstance.transform.localPosition = _currentPosition;
        _previewInstance.transform.localEulerAngles = _currentRotation;

        // Mo phong DUNG cong thuc that: AquariumScale CHUAN * size
        // multiplier DA DUOC KEP (clamp) theo AquariumMinDisplaySizeMultiplier/
        // MaxDisplaySizeMultiplier cua ItemData - giong het
        // AquariumDisplayManager.PlaceFish dang lam, de preview phan
        // anh dung nhung gi nguoi choi se thay trong be that.
        ItemData fish = _fishToTest[_index];
        float displayMultiplier = (fish != null && fish.IsFish)
            ? fish.GetAquariumDisplaySizeMultiplier(_debugSizeMultiplier)
            : _debugSizeMultiplier;

        _previewInstance.transform.localScale = _currentBaseScale * displayMultiplier;
    }

    private void OnDestroy()
    {
        if (_previewInstance != null)
            Destroy(_previewInstance);
    }
}