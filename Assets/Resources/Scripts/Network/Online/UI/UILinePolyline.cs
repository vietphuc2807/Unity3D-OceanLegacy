using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ve 1 duong gap khuc (polyline) tren UI Canvas, KHONG CAN CAI THEM
/// PACKAGE NGOAI - dua tren Graphic.OnPopulateMesh (Unity ho tro san).
///
/// SETUP (lam 1 lan, luu thanh Prefab):
///   1. Tao 1 GameObject rong, dat ten "LineSegment" (hoac tuy y).
///   2. Add Component -> UILinePolyline.
///   3. RectTransform: set Anchors STRETCH ca 2 chieu (Anchor Min 0,0 -
///      Anchor Max 1,1), Left/Top/Right/Bottom = 0 - de no lap vua khop
///      voi vung ve (_plotRect trong DayStatsLineChartUI).
///   4. Bo tick "Raycast Target" trong Inspector cua UILinePolyline (no
///      khong can nhan click/hover, bo tick cho nhe).
///   5. Keo object nay thanh Prefab, gan vao truong _linePrefab cua
///      DayStatsLineChartUI.
///
/// CACH DUNG TU CODE:
///   line.SetPoints(danhSachDiemChuanHoa01);
///   // Diem chuan hoa: (0,0) = goc DUOI-TRAI cua rect, (1,1) = goc
///   // TREN-PHAI. Component tu quy doi ra pixel thuc theo kich thuoc
///   // RectTransform HIEN TAI moi lan ve lai (Canvas.willRenderCanvases),
///   // nen van dung neu box doi kich thuoc / doi do phan giai man hinh.
/// </summary>
[AddComponentMenu("UI/Extensions/UI Polyline (No Package)")]
public class UILinePolyline : Graphic
{
    [Tooltip("Do day net ve, tinh bang pixel.")]
    [SerializeField]
    private float _thickness = 4f;

    [Tooltip("So canh dung de ve khop noi tron o moi diem (kho i noi giua 2 doan thang). Cao hon = tron hon nhung nang hon. 6-10 la du muot.")]
    [SerializeField]
    private int _jointSegments = 8;

    private readonly List<Vector2> _normalizedPoints = new List<Vector2>();


    public float Thickness
    {
        get => _thickness;
        set
        {
            _thickness = value;
            SetVerticesDirty();
        }
    }


    /// <summary>
    /// Danh sach diem CHUAN HOA 0..1 theo kich thuoc RectTransform hien
    /// tai. (0,0) = goc duoi-trai, (1,1) = goc tren-phai. Can it nhat 2
    /// diem moi ve duoc; truyen < 2 diem se xoa duong (khong ve gi).
    /// </summary>
    public void SetPoints(IReadOnlyList<Vector2> normalizedPoints)
    {
        _normalizedPoints.Clear();

        if (normalizedPoints != null)
            _normalizedPoints.AddRange(normalizedPoints);

        SetVerticesDirty();
    }


    public void Clear()
    {
        _normalizedPoints.Clear();
        SetVerticesDirty();
    }


    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (_normalizedPoints.Count < 2)
            return;

        Rect r = rectTransform.rect;

        Vector2[] local = new Vector2[_normalizedPoints.Count];

        for (int i = 0; i < _normalizedPoints.Count; i++)
        {
            local[i] = new Vector2(
                r.x + Mathf.Clamp01(_normalizedPoints[i].x) * r.width,
                r.y + Mathf.Clamp01(_normalizedPoints[i].y) * r.height
            );
        }

        float half = _thickness * 0.5f;

        // ------------------------------------------------------------
        // Than duong - 1 hinh chu nhat (2 tam giac) cho MOI doan thang
        // giua 2 diem lien tiep.
        // ------------------------------------------------------------

        for (int i = 0; i < local.Length - 1; i++)
        {
            Vector2 p0 = local[i];
            Vector2 p1 = local[i + 1];

            Vector2 dir = p1 - p0;

            if (dir.sqrMagnitude < 0.0001f)
                continue;

            dir.Normalize();

            Vector2 normal = new Vector2(-dir.y, dir.x) * half;

            int vIdx = vh.currentVertCount;

            vh.AddVert(p0 - normal, color, Vector2.zero);
            vh.AddVert(p0 + normal, color, Vector2.zero);
            vh.AddVert(p1 + normal, color, Vector2.zero);
            vh.AddVert(p1 - normal, color, Vector2.zero);

            vh.AddTriangle(vIdx, vIdx + 1, vIdx + 2);
            vh.AddTriangle(vIdx, vIdx + 2, vIdx + 3);
        }

        // ------------------------------------------------------------
        // Khop noi tron o TUNG diem (ke ca 2 dau mut) - tranh bi ho /
        // goc nhon xau khi duong doi huong dot ngot.
        // ------------------------------------------------------------

        for (int i = 0; i < local.Length; i++)
        {
            AddRoundJoint(vh, local[i], half);
        }
    }


    private void AddRoundJoint(
        VertexHelper vh,
        Vector2 center,
        float radius)
    {
        if (_jointSegments < 3)
            return;

        int centerIdx = vh.currentVertCount;

        vh.AddVert(center, color, Vector2.zero);

        int firstRimIdx = vh.currentVertCount;

        for (int s = 0; s <= _jointSegments; s++)
        {
            float angle = (s / (float)_jointSegments) * Mathf.PI * 2f;

            Vector2 offset =
                new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            vh.AddVert(center + offset, color, Vector2.zero);
        }

        for (int s = 0; s < _jointSegments; s++)
        {
            vh.AddTriangle(
                centerIdx,
                firstRimIdx + s,
                firstRimIdx + s + 1
            );
        }
    }
}