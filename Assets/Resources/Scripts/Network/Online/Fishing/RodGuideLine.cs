using UnityEngine;

/// <summary>
/// Day tinh luon qua cac lo (guide ring) tren can cau, tu lo duoi cung
/// den dau can, BAM THEO DO UON cua can.
///
/// CACH HOAT DONG:
/// - Luc Start (can dang o tu the nghi), moi GuidePoint duoc "gan" vao
///   BONE nam ngay PHIA SAU no doc theo can (bone co hinh chieu doc truc
///   can lon nhat nhung van <= hinh chieu cua diem). Luu vi tri diem do
///   trong khong gian local cua bone.
/// - Moi LateUpdate: vi tri that cua diem = bone.TransformPoint(offset),
///   nen can uon (FishingRodBendEffect xoay cac bone) thi diem di theo.
/// - KHONG phu thuoc cac GuidePoint dang la con cua bone nao, nen khong lo
///   gan nham bone.
///
/// Chay LateUpdate execution order 300 (sau FishingRodBendEffect = 0 va
/// FishingLine = 200) de luon doc bone SAU KHI da uon xong trong frame.
/// </summary>
[DefaultExecutionOrder(300)]
[RequireComponent(typeof(LineRenderer))]
public class RodGuideLine : MonoBehaviour
{
    [Header("Bones (CUNG danh sach voi FishingRodBendEffect)")]
    [Tooltip("Keo y het mang Rod Bones cua FishingRodBendEffect: " +
             "Root_01 ... Root_12, thu tu TU GOC DEN NGON.")]
    [SerializeField] private Transform[] _rodBones;

    [Header("Diem di qua (thu tu: TU DUOI LEN TREN)")]
    [Tooltip("Tam tung lo, tu lo duoi cung len lo tren cung. Cac empty nay " +
             "dat o dau trong prefab cung duoc, chi can dung vi tri luc nghi.")]
    [SerializeField] private Transform[] _guidePoints;

    [Tooltip("(Khong bat buoc) Diem cuoi them vao. De trong neu lo tren " +
             "cung da la noi day cau bat dau.")]
    [SerializeField] private Transform _lineEnd;

    [Header("Hinh dang")]
    [SerializeField] private float _lineWidth = 0.01f;

    [SerializeField] private Color _lineColor =
        new Color(0.9f, 0.88f, 0.75f, 1f);

    [Tooltip("De trong = tu tao material giong day cau (Sprites/Default).")]
    [SerializeField] private Material _lineMaterial;

    [Header("Lam muot")]
    [Tooltip("1 = noi thang giua cac lo. 2-4 = duong cong muot.")]
    [Range(1, 12)]
    [SerializeField] private int _subdivisions = 2;

    private LineRenderer _lr;
    private Material _createdMaterial;

    // Moi diem: gan vao bone nao, vi tri trong khong gian local cua bone.
    private int[] _boundBone;
    private Vector3[] _localOffset;
    private bool _bound;

    private Vector3[] _control = new Vector3[0];
    private Vector3[] _output = new Vector3[0];

    private void Awake()
    {
        _lr = GetComponent<LineRenderer>();

        _lr.useWorldSpace = true;
        _lr.startWidth = _lineWidth;
        _lr.endWidth = _lineWidth;
        _lr.numCapVertices = 4;
        _lr.numCornerVertices = 4;
        _lr.alignment = LineAlignment.View;
        _lr.textureMode = LineTextureMode.Stretch;
        _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lr.receiveShadows = false;

        if (_lineMaterial != null)
        {
            _lr.material = _lineMaterial;
        }
        else
        {
            _createdMaterial = new Material(Shader.Find("Sprites/Default"));
            _createdMaterial.color = _lineColor;
            _lr.material = _createdMaterial;
        }
    }

    private void Start()
    {
        BindGuidePoints();
    }

    private void OnDestroy()
    {
        if (_createdMaterial != null)
            Destroy(_createdMaterial);
    }

    [ContextMenu("Rebind Guide Points")]
    private void BindGuidePoints()
    {
        _bound = false;

        if (_guidePoints == null || _guidePoints.Length == 0)
            return;

        _boundBone = new int[_guidePoints.Length];
        _localOffset = new Vector3[_guidePoints.Length];

        bool hasBones =
            _rodBones != null &&
            _rodBones.Length > 0 &&
            _rodBones[0] != null &&
            _rodBones[_rodBones.Length - 1] != null;

        Vector3 origin = Vector3.zero;
        Vector3 axis = Vector3.up;

        if (hasBones)
        {
            origin = _rodBones[0].position;

            Vector3 rodDir =
                _rodBones[_rodBones.Length - 1].position - origin;

            if (rodDir.sqrMagnitude > 0.000001f)
                axis = rodDir.normalized;
        }

        for (int i = 0; i < _guidePoints.Length; i++)
        {
            Transform p = _guidePoints[i];

            if (p == null || !hasBones)
            {
                _boundBone[i] = -1;
                continue;
            }

            // Bone nam ngay PHIA SAU diem (doc theo can): hinh chieu lon
            // nhat nhung van <= hinh chieu cua diem.
            float pointProj = Vector3.Dot(p.position - origin, axis);

            int best = 0;

            for (int b = 0; b < _rodBones.Length; b++)
            {
                if (_rodBones[b] == null)
                    continue;

                float boneProj =
                    Vector3.Dot(_rodBones[b].position - origin, axis);

                if (boneProj <= pointProj + 0.0001f)
                    best = b;
            }

            _boundBone[i] = best;

            _localOffset[i] =
                _rodBones[best].InverseTransformPoint(p.position);
        }

        _bound = true;
    }

    private void LateUpdate()
    {
        int count = CollectControlPoints();

        if (count < 2)
        {
            _lr.positionCount = 0;
            return;
        }

        int sub = Mathf.Max(1, _subdivisions);
        int outCount = (count - 1) * sub + 1;

        if (_output.Length != outCount)
            _output = new Vector3[outCount];

        int o = 0;

        for (int i = 0; i < count - 1; i++)
        {
            Vector3 p0 = _control[Mathf.Max(i - 1, 0)];
            Vector3 p1 = _control[i];
            Vector3 p2 = _control[i + 1];
            Vector3 p3 = _control[Mathf.Min(i + 2, count - 1)];

            for (int s = 0; s < sub; s++)
            {
                float t = (float)s / sub;
                _output[o++] = sub == 1 ? p1 : CatmullRom(p0, p1, p2, p3, t);
            }
        }

        _output[o] = _control[count - 1];

        _lr.positionCount = outCount;
        _lr.SetPositions(_output);
    }

    private int CollectControlPoints()
    {
        int max = (_guidePoints != null ? _guidePoints.Length : 0) + 1;

        if (_control.Length != max)
            _control = new Vector3[max];

        int n = 0;

        if (_guidePoints != null)
        {
            for (int i = 0; i < _guidePoints.Length; i++)
            {
                Transform p = _guidePoints[i];

                if (p == null)
                    continue;

                if (_bound &&
                    _boundBone != null &&
                    i < _boundBone.Length &&
                    _boundBone[i] >= 0 &&
                    _rodBones[_boundBone[i]] != null)
                {
                    _control[n++] =
                        _rodBones[_boundBone[i]].TransformPoint(_localOffset[i]);
                }
                else
                {
                    // Chua bind duoc (thieu bone) -> dung vi tri thang.
                    _control[n++] = p.position;
                }
            }
        }

        if (_lineEnd != null)
            _control[n++] = _lineEnd.position;

        return n;
    }

    private static Vector3 CatmullRom(
        Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        Transform prev = null;

        if (_guidePoints != null)
        {
            foreach (Transform p in _guidePoints)
            {
                if (p == null) continue;

                Gizmos.DrawWireSphere(p.position, 0.01f);

                if (prev != null)
                    Gizmos.DrawLine(prev.position, p.position);

                prev = p;
            }
        }
    }
}