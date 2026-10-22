using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cong cu DEBUG rieng, giup chinh nhanh vi tri / xoay / kich co
/// (Width, Height pixel) cua icon tung loai ca trong panel "Thung ca
/// mang ve" ngay trong Play mode, khong can vao Gameplay cau ca that
/// moi lan muon xem thu.
///
/// Cach dung:
///
/// 1) Gan script nay len 1 GameObject trong scene Museum (co the
///    dung chung scene test rieng cho nhanh).
/// 2) Keo 1 FishReturnPanelUI (VD 1 ban preview dat san trong scene,
///    hoac 1 instance cua prefab) vao field Preview Panel.
/// 3) Keo danh sach cac ItemData ca muon test vao Fish To Test.
/// 4) Bam Play, dung phim:
///
///    - Q / E          : chuyen sang con ca truoc / sau trong list.
///    - Mui ten trai/phai/len/xuong : di chuyen icon (Position X/Y).
///    - [ / ]           : xoay icon (Rotation Z).
///    - - / =           : giam / tang KICH CO icon (Width & Height
///                        cung luc, theo pixel UI - tuc sizeDelta,
///                        KHONG phai Scale) - chinh thang ra dung
///                        kich thuoc mong muon, khong can Set Native
///                        Size roi vua Scale vua canh lai Position.
///    - R               : reset ve dung Return Icon Position/Rotation
///                        /Size da luu san trong ItemData cua con ca
///                        dang xem.
///    - P               : in gia tri Position / Rotation / Size hien
///                        tai ra Console - COPY 3 dong nay dan lai vao
///                        3 field Return Icon Position / Return Icon
///                        Rotation / Return Icon Size trong muc
///                        "Fish Return Icon" cua ItemData con ca do
///                        (o Project window) de luu lai vinh vien.
///
/// 5) NGOAI RA, ban cung co the go THANG so vao 3 field Return Icon
///    Position / Rotation / Size cua ItemData (o Inspector, luc dang
///    Play) - script se TU DONG phat hien gia tri asset thay doi va
///    ap ngay vao panel preview trong scene, khong can bam R. Cach
///    nay tien hon neu ban muon go so chinh xac thay vi nudge bang
///    phim.
///
/// Script nay CHI de test/debug - khong anh huong gi den luong choi
/// that (ReturningCatchData / MuseumFishCrateUI), co the xoa GameObject
/// chua no truoc khi build ban chinh thuc.
/// </summary>
public class FishIconDebugCycler : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private FishReturnPanelUI _previewPanel;
    [SerializeField] private List<ItemData> _fishToTest = new();

    [Header("Nudge Step")]
    [SerializeField] private float _moveStep = 5f;
    [SerializeField] private float _rotateStep = 15f;
    [Tooltip("Moi lan bam -/=, Width va Height se giam/tang bao nhieu pixel.")]
    [SerializeField] private float _sizeStep = 10f;
    [Tooltip("Kich thuoc toi thieu (px) de tranh icon bi am/bien mat.")]
    [SerializeField] private float _minSize = 10f;

    private int _index;

    private Vector2 _currentPosition;
    private float _currentRotation;
    private Vector2 _currentSize = new Vector2(100f, 100f);

    // Gia tri Return Icon cua ItemData tai lan doc gan nhat - dung de
    // so sanh moi frame, phat hien luc ban go so thang vao Inspector
    // cua ItemData thay vi nudge bang phim.
    private Vector2 _lastAssetPosition;
    private float _lastAssetRotation;
    private Vector2 _lastAssetSize;

    private void Start()
    {
        if (_fishToTest.Count > 0)
            ShowFish(0);
    }

    private void Update()
    {
        if (_previewPanel == null || _fishToTest.Count == 0)
            return;

        HandleCycleInput();
        HandleNudgeInput();
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
    // NUDGE POSITION / ROTATION / SIZE
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
            _currentPosition.y += _moveStep;
            changed = true;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            _currentPosition.y -= _moveStep;
            changed = true;
        }

        if (Input.GetKeyDown(KeyCode.LeftBracket))
        {
            _currentRotation += _rotateStep;
            changed = true;
        }

        if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            _currentRotation -= _rotateStep;
            changed = true;
        }

        if (Input.GetKeyDown(KeyCode.Equals))
        {
            _currentSize += Vector2.one * _sizeStep;
            changed = true;
        }

        if (Input.GetKeyDown(KeyCode.Minus))
        {
            _currentSize -= Vector2.one * _sizeStep;
            _currentSize = Vector2.Max(_currentSize, Vector2.one * _minSize);
            changed = true;
        }

        if (changed)
        {
            _previewPanel.ApplyIconTransform(
                _currentPosition,
                _currentRotation,
                _currentSize
            );
        }
    }

    // ============================================================
    // LIVE-SYNC KHI GO SO THANG VAO ITEMDATA (INSPECTOR)
    // ============================================================

    /// <summary>
    /// So sanh gia tri Return Icon hien tai cua ItemData dang xem voi
    /// gia tri da doc lan truoc. Neu khac (nghia la ban vua go so
    /// khac vao Inspector cua ItemData), cap nhat lai preview ngay
    /// lap tuc ma khong can bam R.
    ///
    /// Chi so sanh voi "gia tri asset lan truoc", KHONG so sanh voi
    /// _currentPosition/Rotation/Size dang nudge bang phim, nen 2
    /// cach dieu chinh (nudge bang phim vs go so vao ItemData) khong
    /// dam vao nhau.
    /// </summary>
    private void CheckLiveAssetEdits()
    {
        ItemData fish = _fishToTest[_index];

        if (fish == null)
            return;

        bool assetChanged =
            fish.ReturnIconPosition != _lastAssetPosition ||
            !Mathf.Approximately(fish.ReturnIconRotation, _lastAssetRotation) ||
            fish.ReturnIconSize != _lastAssetSize;

        if (!assetChanged)
            return;

        _lastAssetPosition = fish.ReturnIconPosition;
        _lastAssetRotation = fish.ReturnIconRotation;
        _lastAssetSize = fish.ReturnIconSize;

        _currentPosition = _lastAssetPosition;
        _currentRotation = _lastAssetRotation;
        _currentSize = _lastAssetSize;

        _previewPanel.ApplyIconTransform(
            _currentPosition,
            _currentRotation,
            _currentSize
        );
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

            Debug.Log(
                $"[FishIconDebugCycler] {currentFish.name} -> " +
                $"Return Icon Position = {_currentPosition}, " +
                $"Return Icon Rotation = {_currentRotation}, " +
                $"Return Icon Size = {_currentSize}"
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

        _previewPanel.SetData(fish, 1f);

        _currentPosition = fish.ReturnIconPosition;
        _currentRotation = fish.ReturnIconRotation;
        _currentSize = fish.ReturnIconSize;

        _lastAssetPosition = _currentPosition;
        _lastAssetRotation = _currentRotation;
        _lastAssetSize = _currentSize;

        Debug.Log($"[FishIconDebugCycler] Dang xem: {fish.name}");
    }
}