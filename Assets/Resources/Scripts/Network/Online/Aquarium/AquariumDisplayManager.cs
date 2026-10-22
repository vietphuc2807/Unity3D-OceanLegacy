using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quan ly viec SPAWN mo hinh ca 3D vao trong 1 BE KINH CU THE o Museum.
///
/// MOI (ho tro NHIEU BE): script nay gio KHONG con la singleton
/// "Instance" duy nhat nua - moi be trong Museum se co 1 GameObject
/// rieng gan component nay, moi be duoc gan 1 "_tankId" KHAC NHAU
/// trong Inspector (VD 0, 1, 2, 3 cho 4 be). Cac script khac (VD
/// MuseumFishCrateUI) tra cuu dung be can thao tac bang
/// AquariumDisplayManager.GetByTankId(tankId), KHONG con dung
/// ".Instance" nua.
///
/// QUAN TRONG: MOI BE CHI CHUA DUOC 1 CON CA TAI 1 THOI DIEM (van dung
/// nhu cu, chi khac la gio co NHIEU be doc lap voi nhau).
///
/// Dat script nay len 1 object trong scene Museum CHO MOI BE, keo
/// _spawnRoot la Transform goc cua chinh be do, va gan _tankId duy
/// nhat cho tung be (khong trung nhau).
/// </summary>
public class AquariumDisplayManager : MonoBehaviour
{
    [Header("Tank Identity")]
    [Tooltip(
        "ID duy nhat cua be nay (VD 0, 1, 2, 3 neu co 4 be). KHONG " +
        "duoc trung voi be khac trong cung 1 scene Museum."
    )]
    [SerializeField] private int _tankId;

    public int TankId => _tankId;

    [Header("Setup")]
    [Tooltip(
        "Transform goc cua be kinh nay - ca duoc Instantiate lam con " +
        "cua Transform nay, Position/Rotation trong ItemData la LOCAL " +
        "so voi goc nay."
    )]
    [SerializeField] private Transform _spawnRoot;

    private GameObject _currentInstance;
    private string _currentItemId;
    private float _currentSizeMultiplier;

    // ============================================================
    // REGISTRY THEO TANK ID
    // ============================================================

    private static readonly Dictionary<int, AquariumDisplayManager> _instancesById = new();

    /// <summary>Lay AquariumDisplayManager cua be co TankId tuong ung, hoac null neu chua co/khong ton tai.</summary>
    public static AquariumDisplayManager GetByTankId(int tankId)
    {
        return _instancesById.TryGetValue(tankId, out var mgr) ? mgr : null;
    }

    /// <summary>Toan bo cac be dang co trong scene, key la TankId.</summary>
    public static IReadOnlyDictionary<int, AquariumDisplayManager> AllTanks => _instancesById;

    /// <summary>
    /// Ban CHUNG cho TAT CA be (khong phan biet TankId nao) moi khi
    /// MOT be bat ky THUC SU thay doi trang thai co-ca/khong-co-ca -
    /// dat ca vao thanh cong (PlaceFish) hoac lay ca ra khoi be dang
    /// co ca (ClearTank). Dung boi UI thong ke tong quan cua ca Museum
    /// (VD MuseumStatsUI, hien so be dang co ca "X/4" + tong Fame
    /// Point) de biet luc nao can tinh lai - nguoi nghe KHONG can biet
    /// chinh xac be nao vua thay doi, vi RefreshStats() luon duyet lai
    /// TOAN BO AllTanks tu dau moi lan duoc goi.
    ///
    /// Bat ky noi nao gan/xoa ca truc tiep qua AquariumDisplayManager
    /// (HandleFishSelected, RetrieveCurrentFish, AquariumPersistenceLoader
    /// luc load scene, v.v.) deu tu dong lam ban su kien nay, khong can
    /// tu goi tay o noi khac.
    /// </summary>
    public static event Action OnAnyTankChanged;

    private void Awake()
    {
        if (_instancesById.ContainsKey(_tankId))
        {
            Debug.LogWarning(
                $"[AquariumDisplayManager] TankId={_tankId} bi TRUNG voi " +
                "1 be khac trong scene! Kiem tra lai Inspector, moi be " +
                "phai co TankId duy nhat."
            );
        }

        _instancesById[_tankId] = this;
    }

    private void OnDestroy()
    {
        if (_instancesById.TryGetValue(_tankId, out var mgr) && mgr == this)
            _instancesById.Remove(_tankId);
    }

    /// <summary>
    /// Dat con ca nay vao be NAY (be cua chinh instance dang goi ham),
    /// VOI KICH CO ti le theo sizeMultiplier luc cau duoc. Neu be nay
    /// dang co san 1 con ca khac (hoac chinh con nay), con do se bi
    /// XOA TRUOC. Tra ve GameObject vua spawn, hoac null neu that bai.
    /// </summary>
    public GameObject PlaceFish(ItemData fish, float sizeMultiplier)
    {
        if (fish == null || !fish.IsFish)
        {
            Debug.LogWarning("[AquariumDisplayManager] PlaceFish: item null hoac khong phai ca.");
            return null;
        }

        if (_spawnRoot == null)
        {
            Debug.LogWarning($"[AquariumDisplayManager] Tank {_tankId}: thieu _spawnRoot trong Inspector.");
            return null;
        }

        GameObject prefab = fish.AquariumPrefab;

        if (prefab == null)
        {
            Debug.LogWarning(
                $"[AquariumDisplayManager] {fish.DisplayName} chua co " +
                "Aquarium Prefab lan FishData.WorldPrefab, khong the spawn."
            );
            return null;
        }

        // Xoa con ca dang co trong be NAY (neu co) TRUOC khi spawn con
        // moi - be chi chua duoc 1 con tai 1 thoi diem. Cac be KHAC
        // (TankId khac) hoan toan khong bi anh huong. Dung ClearTank(false)
        // de KHONG ban OnAnyTankChanged 2 lan thua (1 lan o day, 1 lan o
        // cuoi ham nay) - chi can 1 lan sau khi con ca MOI da dat xong.
        ClearTank(raiseEvent: false);

        GameObject instance = Instantiate(prefab, _spawnRoot);

        instance.transform.localPosition = fish.AquariumPosition;
        instance.transform.localEulerAngles = fish.AquariumRotation;

        float displaySizeMultiplier = fish.GetAquariumDisplaySizeMultiplier(sizeMultiplier);
        instance.transform.localScale = fish.AquariumScale * Mathf.Max(0.0001f, displaySizeMultiplier);

        _currentInstance = instance;
        _currentItemId = fish.ItemId;
        _currentSizeMultiplier = sizeMultiplier;

        OnAnyTankChanged?.Invoke();

        return instance;
    }

    /// <summary>Xoa con ca dang co trong be NAY (neu co), tra be ve trang thai trong.</summary>
    public void ClearTank()
    {
        ClearTank(raiseEvent: true);
    }

    /// <summary>
    /// Xoa con ca dang co trong be NAY (neu co), tra be ve trang thai
    /// trong. <paramref name="raiseEvent"/> = false dung NOI BO boi
    /// PlaceFish() (de tranh ban OnAnyTankChanged 2 lan lien tiep khi
    /// thay 1 con ca nay bang 1 con ca khac) - moi noi khac goi
    /// ClearTank() (khong tham so) nhu binh thuong.
    /// </summary>
    private void ClearTank(bool raiseEvent)
    {
        bool hadFish = _currentItemId != null;

        if (_currentInstance != null)
            Destroy(_currentInstance);

        _currentInstance = null;
        _currentItemId = null;
        _currentSizeMultiplier = 0f;

        if (raiseEvent && hadFish)
            OnAnyTankChanged?.Invoke();
    }

    /// <summary>ItemId cua con ca dang co trong be NAY, hoac null neu be dang trong.</summary>
    public string CurrentItemId => _currentItemId;

    /// <summary>SizeMultiplier cua con ca dang co trong be NAY (0 neu be dang trong).</summary>
    public float CurrentSizeMultiplier => _currentSizeMultiplier;

    /// <summary>Goc be NAY - dung cho debug cycler spawn preview dung cho.</summary>
    public Transform SpawnRoot => _spawnRoot;
}