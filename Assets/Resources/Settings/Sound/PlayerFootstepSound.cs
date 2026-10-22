using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phat tieng buoc chan / nhay / tiep dat cua Player.
///
/// - Footstep(AnimationEvent): goi tu Animation Event tren clip walk2 va
///   Running. O clip Running, o String nhap "run"; clip walk de trong.
///   Chi phat khi clip chua event dang chiem >= _minClipWeight trong Blend
///   Tree (tranh tiep tuc keu khi dung lai hoac nham tieng walk/run).
/// - Jump() / Land(): goi tu code trong PlayerMovement (KHONG gan Animation
///   Event cho 2 ham nay, neu khong tieng se bi phat 2 lan).
///
/// THU TU UU TIEN BO CLIP: NUOC > CO > MAC DINH.
///
/// TIENG NUOC: nhan dien bang LAYER (_waterMask), khong dung Tag.
///   - Ban 1 tia tu do cao _waterRayStartHeight (ngang nguc/dau) xuong,
///     tim mat nuoc (collider thuoc layer Water). Neu mat nuoc CAO HON
///     chan (tru _waterSurfaceTolerance) thi coi nhu dang o trong nuoc.
///   - Them 1 CheckSphere o chan de bat truong hop nuoc la khoi collider
///     (tia bat dau ben trong khoi se khong trung duoc).
///   - Chua dien clip nuoc -> khong check nuoc, dung clip co/mac dinh nhu cu.
///
/// TIENG CO: ban 1 tia raycast xuong duoi chan, neu object trung co Tag =
/// _grassTag thi dung bo clip co, nguoc lai dung bo clip mac dinh.
/// Tia CHAM CA TRIGGER (QueryTriggerInteraction.Collide) de nhan duoc cac
/// san la trigger (VD san nha co HouseZoneTrigger) - hay dat san nha va
/// Terrain cung layer (VD Ground) roi chon _groundMask = Ground de tia
/// khong bi cac trigger khac (vung cau ca, vung tuong tac...) chan nham.
///
/// QUAN TRONG: script nay phai nam tren CUNG GameObject voi Animator thi
/// Animation Event moi goi duoc.
/// </summary>
public class PlayerFootstepSound : MonoBehaviour
{
    [Header("Default (san thuong)")]
    [SerializeField] private List<string> _walkClipNames = new();
    [SerializeField] private List<string> _runClipNames = new();

    [Header("Grass (chi dung khi dung tren object co Tag = Grass)")]
    [SerializeField] private string _grassTag = "Grass";
    [SerializeField] private List<string> _grassWalkClipNames = new();
    [SerializeField] private List<string> _grassRunClipNames = new();

    [Header("Water (dung khi chan cham nuoc - layer Water)")]
    [Tooltip("Chon layer cua nuoc (VD Water). CHI chon layer nuoc.")]
    [SerializeField] private LayerMask _waterMask = 0;

    [Tooltip("Ten SFX tieng buoc di bo duoi nuoc (VD walkwater).")]
    [SerializeField] private List<string> _waterWalkClipNames = new();

    [Tooltip("Ten SFX tieng buoc chay duoi nuoc (VD runwater).")]
    [SerializeField] private List<string> _waterRunClipNames = new();

    [Tooltip("Do cao diem BAT DAU tia tim mat nuoc (tinh tu chan Player). " +
             "Phai CAO HON muc nuoc sau nhat ma Player co the lui toi.")]
    [SerializeField] private float _waterRayStartHeight = 2f;

    [Tooltip("Do dai tia tim mat nuoc, tinh tu diem bat dau.")]
    [SerializeField] private float _waterRayDistance = 4f;

    [Tooltip("Mat nuoc phai cao hon (chan - gia tri nay) moi tinh la dang " +
             "o trong nuoc. Tang len neu muon bo tay nuoc nong sat bo cung " +
             "keu; giam ve 0 neu chi muon keu khi that su ngap chan.")]
    [SerializeField] private float _waterSurfaceTolerance = 0.1f;

    [Tooltip("Ban kinh CheckSphere o chan (dung cho nuoc la khoi collider).")]
    [SerializeField] private float _waterFootCheckRadius = 0.25f;

    [Header("Jump / Land")]
    [SerializeField] private List<string> _jumpClipNames = new();
    [SerializeField] private List<string> _landClipNames = new();

    [Header("Ground Check")]
    [Tooltip("Chon layer Ground (Terrain + san nha). Khong chon layer cua chinh Player.")]
    [SerializeField] private LayerMask _groundMask = ~0;
    [SerializeField] private float _rayStartHeight = 0.3f;
    [SerializeField] private float _rayDistance = 1.2f;

    [Header("Misc")]
    [Tooltip("Tranh phat trung lap 2 lan lien tiep cung 1 clip (can >= 2 clip trong danh sach).")]
    [SerializeField] private bool _avoidImmediateRepeat = true;

    [Tooltip("Chi phat khi clip chua event dang chiem it nhat ty le nay trong Blend Tree.")]
    [SerializeField, Range(0f, 1f)] private float _minClipWeight = 0.5f;

    private readonly Dictionary<List<string>, int> _lastIndex = new();

    // ============================================================
    // ANIMATION EVENT
    // ============================================================

    // Animation Event: String = "run" cho clip Running, de trong cho walk2.
    public void Footstep(AnimationEvent evt)
    {
        if (evt.animatorClipInfo.weight < _minClipWeight)
            return;

        bool isRun = evt.stringParameter == "run";

        List<string> list = isRun ? _runClipNames : _walkClipNames;

        // ------------------------------------------------------------
        // UU TIEN 1: NUOC
        // ------------------------------------------------------------

        List<string> waterList =
            isRun ? _waterRunClipNames : _waterWalkClipNames;

        if (waterList.Count > 0 && IsInWater())
        {
            PlayRandom(waterList);
            return;
        }

        // ------------------------------------------------------------
        // UU TIEN 2: CO
        // ------------------------------------------------------------

        if (IsOnGrass())
        {
            List<string> grassList =
                isRun ? _grassRunClipNames : _grassWalkClipNames;

            // Chua dien clip co thi van dung tieng mac dinh.
            if (grassList.Count > 0)
                list = grassList;
        }

        PlayRandom(list);
    }

    // ============================================================
    // GOI TU CODE (PlayerMovement)
    // ============================================================

    public void Jump() => PlayRandom(_jumpClipNames);

    public void Land() => PlayRandom(_landClipNames);

    // ============================================================
    // CHECK NUOC
    // ============================================================

    private bool IsInWater()
    {
        // Chua chon layer nuoc -> khong check.
        if (_waterMask.value == 0)
            return false;

        Vector3 feet = transform.position;

        // 1) Nuoc la khoi collider bao quanh chan (tia bat dau trong khoi
        //    se khong trung duoc, nen check rieng bang sphere).
        if (Physics.CheckSphere(
                feet + Vector3.up * _waterFootCheckRadius,
                _waterFootCheckRadius,
                _waterMask,
                QueryTriggerInteraction.Collide))
        {
            return true;
        }

        // 2) Nuoc la mat phang mong: ban tia tu tren cao xuong tim mat nuoc,
        //    roi so sanh voi chan. Mat nuoc cao hon chan = dang ngap chan.
        Vector3 origin = feet + Vector3.up * _waterRayStartHeight;

        if (!Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                _waterRayDistance,
                _waterMask,
                QueryTriggerInteraction.Collide))
        {
            return false;
        }

        // Bo comment dong duoi neu can xem tia dang trung object nao:
        // Debug.Log($"[Footstep] Water hit: {hit.collider.name} | surfaceY={hit.point.y} | feetY={feet.y}");

        return hit.point.y >= feet.y - _waterSurfaceTolerance;
    }

    // ============================================================
    // CHECK CO
    // ============================================================

    private bool IsOnGrass()
    {
        // Khong co clip co nao (VD prefab o scene Museum) -> khong can raycast.
        if (_grassWalkClipNames.Count == 0 && _grassRunClipNames.Count == 0)
            return false;

        Vector3 origin = transform.position + Vector3.up * _rayStartHeight;

        if (!Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                _rayDistance,
                _groundMask,
                QueryTriggerInteraction.Collide))
        {
            return false;
        }

        // Bo comment dong duoi neu can xem tia dang trung object nao:
        // Debug.Log($"[Footstep] {hit.collider.name} | tag: {hit.collider.tag} | layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");

        return hit.collider.CompareTag(_grassTag);
    }

    // ============================================================
    // PHAT AM
    // ============================================================

    private void PlayRandom(List<string> names)
    {
        if (SoundManager.Instance == null || names == null || names.Count == 0)
            return;

        int index = Random.Range(0, names.Count);

        if (names.Count > 1 &&
            _avoidImmediateRepeat &&
            _lastIndex.TryGetValue(names, out int last) &&
            index == last)
        {
            index = (index + 1) % names.Count;
        }

        _lastIndex[names] = index;

        SoundManager.Instance.PlaySFX(names[index]);
    }
}