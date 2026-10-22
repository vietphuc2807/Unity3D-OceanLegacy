using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Tach camera khoi CameraHolder, di chuyen toi goc nhin can canh 1
/// BE CU THE (giong co che PosterDrawView / FishInspectView ben
/// Gameplay). Khi mo: goi MuseumFishCrateUI.OpenForAquarium(TankId)
/// de tu dong hien panel "Thung ca mang ve", DA BAO NO BIET dang xem
/// be nao (de chon ca dung vao dung be nay, khong bi lan sang be
/// khac). Khi dong: an panel do lai + tra camera ve vi tri cu.
///
/// MOI (nhieu be): moi be trong Museum can 1 AquariumView RIENG (gan
/// _tankId trung voi AquariumDisplayManager cua chinh be do), vi moi
/// be co _viewPoint (goc camera) khac nhau.
///
/// MOI: AquariumView.AnyActive = true khi BAT KY view nao dang mo -
/// dung cho script khac (VD MuseumLeaderboardUI) biet IsUIOpen dang
/// true la do AquariumView chu khong phai UI khac.
///
/// KHONG tu xu ly phim Esc / khoa chuot / freeze player o day -
/// nhung viec do da duoc AquariumInteractableObject (ke thua
/// InteractableObject) lo qua FreezePlayer()/UnfreezePlayer(), va
/// Update() cua InteractableObject da tu bat phim Esc de goi Close().
/// AquariumView chi lo phan RIENG cua no: camera + panel thung ca.
///
/// KHONG dung NetPlayerLook (co che khoa camera cua scene Gameplay
/// online) vi scene Museum la offline, dung PlayerLook rieng - da bi
/// AquariumInteractableObject.FreezePlayer() disable san nen khong
/// can khoa gi them o day.
/// </summary>
public class AquariumView : MonoBehaviour
{
    /// <summary>True khi co It nhat 1 AquariumView dang mo.</summary>
    public static bool AnyActive { get; private set; }

    [Header("Tank Identity")]
    [Tooltip(
        "TankId cua be ma view nay dai dien - PHAI trung voi TankId " +
        "cua AquariumDisplayManager gan tren chinh be do."
    )]
    [SerializeField] private int _tankId;

    public int TankId => _tankId;

    [Header("References")]
    [Tooltip("Transform dat san vi tri + huong camera luc dang ngam be (keo 1 Empty GameObject dat truoc be kinh).")]
    [SerializeField] private Transform _viewPoint;
    [SerializeField] private GameObject _viewUI; // panel goi y phim Esc, chi de hien thi

    [Header("Timing")]
    [Tooltip("Thoi gian animate camera luc MO va DONG (cung toc do).")]
    [SerializeField] private float _moveDuration = 0.4f;

    public bool IsActive { get; private set; }

    private Action _onClosed;

    private Camera _cam;
    private Transform _camOriginalParent;
    private Vector3 _camOriginalLocalPos;
    private Quaternion _camOriginalLocalRot;
    private float _camOriginalFov;

    private Coroutine _moveRoutine;

    private void OnDestroy()
    {
        if (IsActive)
            AnyActive = false;
    }

    public void Open(Action onClosed)
    {
        if (IsActive)
        {
            Debug.Log("[AquariumView] Open() called but already IsActive, ignoring.");
            return;
        }

        _cam = Camera.main;

        if (_cam == null || _viewPoint == null)
        {
            Debug.LogWarning($"[AquariumView] Cannot open: Camera.main={_cam}, _viewPoint={_viewPoint}");
            return;
        }

        _onClosed = onClosed;
        IsActive = true;
        AnyActive = true;

        _camOriginalParent = _cam.transform.parent;
        _camOriginalLocalPos = _cam.transform.localPosition;
        _camOriginalLocalRot = _cam.transform.localRotation;
        _camOriginalFov = _cam.fieldOfView;

        // Tach khoi CameraHolder de tu dieu khien transform trong luc
        // animate - PlayerLook da bi FreezePlayer() disable roi nen
        // khong tranh chap voi ai ca.
        _cam.transform.SetParent(null, true);

        if (_viewUI != null)
            _viewUI.SetActive(true);

        if (_moveRoutine != null) StopCoroutine(_moveRoutine);
        _moveRoutine = StartCoroutine(MoveTo(_viewPoint.position, _viewPoint.rotation));

        // Tu hien panel "Thung ca mang ve" ngay - khong can bam Tab.
        MuseumFishCrateUI.Instance?.OpenForAquarium(_tankId);
    }

    public void Close()
    {
        if (!IsActive)
        {
            Debug.Log("[AquariumView] Close() called but not IsActive, ignoring.");
            return;
        }

        IsActive = false;
        AnyActive = false;

        if (_viewUI != null)
            _viewUI.SetActive(false);

        // An lai panel "Thung ca mang ve" khi dong view.
        MuseumFishCrateUI.Instance?.CloseForAquarium(_tankId);

        if (_moveRoutine != null) StopCoroutine(_moveRoutine);
        _moveRoutine = StartCoroutine(ReturnAndFinish());
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

        // Callback nay la UnfreezePlayer() cua AquariumInteractableObject -
        // chi tra lai quyen di chuyen/nhin + khoa chuot SAU KHI animation
        // tra camera ve da chay xong hoan toan.
        _onClosed?.Invoke();
    }
}