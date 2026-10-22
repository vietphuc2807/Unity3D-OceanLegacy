using UnityEngine;

/// <summary>
/// Điều khiển môi trường sáng/tối theo vòng lặp ngày của GameDayManager
/// (5:30 sáng -> 7:00 tối, đọc qua GameDayManager.Instance.GetElapsedFraction01()).
///
/// Làm 3 việc mỗi frame:
///  1) Xoay SunAxis theo trục Z:
///     - Sáng -> Trưa (t: 0 -> 0.5): xoay Z từ _sunriseAngleZ (bình minh)
///       -> _noonAngleZ (trưa, thường là góc nhỏ nhất = ánh sáng "đứng"
///       nhất / sáng nhất), tốc độ xoay theo đường cong _sunArcEase
///       (nhanh lúc mới mọc, chậm dần khi gần trưa) thay vì đều đều tuyến
///       tính, để bóng nắng di chuyển giống thật hơn.
///     - Trưa -> Tối (t: 0.5 -> 1): KHÔNG xoay Z nữa, giữ nguyên ở
///       _noonAngleZ. Độ sáng (intensity) của Directional Light giữ
///       _dayIntensity (2) đến mốc _intensityFadeStart rồi mới giảm dần
///       xuống _eveningIntensity (0.5) - nên đặt mốc này trùng với
///       timeOfDay01 của phase "Buổi Chiều" trong Sky Phases để ánh sáng
///       và skybox tối dần cùng lúc, không bị lệch pha.
///  2) Đổi CƯỜNG ĐỘ Directional Light (MorningSun) theo t như mô tả trên.
///     KHÔNG đổi màu ánh sáng (giữ nguyên màu bạn set sẵn trong Inspector).
///  3) Crossfade (blend mượt) giữa nhiều skybox chụp sẵn (mỗi skybox ứng
///     với 1 thời điểm trong ngày, VD: Đêm - Bình Minh - Trưa - Hoàng Hôn -
///     Đêm) bằng shader Custom/SkyboxBlend6Sided, tốc độ mờ dần theo
///     _skyBlendEase (S-curve: chậm-nhanh-chậm) thay vì mờ đều đều tuyến
///     tính, để trông giống mây/ánh sáng thay đổi tự nhiên hơn.
///
/// LƯU Ý về việc "mặt trời trong ảnh skybox di chuyển giống ngoài đời":
/// Skybox 6-sided là ẢNH TĨNH, không thể tự nâng/hạ mặt trời trong ảnh chỉ
/// bằng xoay. Ảo giác mặt trời di chuyển thật sự đến từ việc CROSSFADE
/// giữa các ảnh skybox có mặt trời ở vị trí khác nhau, kết hợp với việc
/// SunAxis (Directional Light) xoay đồng bộ để bóng đổ trong scene khớp
/// với hướng mặt trời trong skybox tại từng thời điểm.
///
/// SETUP:
/// 1. Tạo 1 Material mới, Shader = "Custom/SkyboxBlend6Sided", đặt tên VD
///    "SkyboxBlend_Runtime". Kéo material này vào field _blendSkyboxMat
///    bên dưới, ĐỒNG THỜI gán nó vào Lighting > Environment > Skybox
///    Material (thay cho 6SidedMegaSun) trước khi Play.
/// 2. Điền mảng _skyPhases: mỗi phần tử kéo THẲNG 1 material skybox có sẵn
///    trong project vào field "Source Material" (VD DayIn..., Cosmic...,
///    DarkSt...) — không cần kéo tay từng texture, script tự đọc 6 texture
///    + giá trị Rotation bạn đã canh trên material đó. Set timeOfDay01
///    tương ứng (0 = 5:30 sáng, 1 = 19:00 tối). Mảng PHẢI sắp xếp tăng dần
///    theo timeOfDay01, phần tử đầu và cuối nên cùng là "Đêm" để ngày mới
///    không bị giật hình.
/// 3. Gắn script này vào 1 object trong scene (VD Manager), kéo SunAxis
///    và Light (MorningSun) vào 2 field _sunAxis / _sunLight.
/// </summary>
public class GameDayLightingController : MonoBehaviour
{
    [System.Serializable]
    public class SkyPhase
    {
        [Tooltip("0 = đầu ngày (5:30 sáng), 1 = cuối ngày (19:00 tối). " +
                 "VD gợi ý: Đêm=0, Bình minh=0.05, Trưa=0.5, Hoàng hôn=0.9, Đêm=1")]
        [Range(0f, 1f)] public float timeOfDay01;

        public string phaseName = "Phase";

        [Tooltip("Kéo thẳng material skybox gốc (VD DayIn..., DarkSt..., Casual... " +
                 "trong SkySeries Freebie) vào đây — KHÔNG cần kéo tay 6 texture. " +
                 "Script tự đọc 6 texture Front/Back/Left/Right/Up/Down VÀ giá trị " +
                 "Rotation bạn đã chỉnh trên material đó ra, giữ đúng hướng bạn " +
                 "canh sẵn.")]
        public Material sourceMaterial;

        // Cache lại lúc runtime (không serialize) để khỏi GetTexture/GetFloat
        // lại từ material mỗi lần đổi phase.
        [System.NonSerialized] public Texture cachedFront;
        [System.NonSerialized] public Texture cachedBack;
        [System.NonSerialized] public Texture cachedLeft;
        [System.NonSerialized] public Texture cachedRight;
        [System.NonSerialized] public Texture cachedUp;
        [System.NonSerialized] public Texture cachedDown;
        [System.NonSerialized] public Cubemap cachedCubemap;
        [System.NonSerialized] public bool isCubeFormat;
        [System.NonSerialized] public float cachedRotation;
        [System.NonSerialized] public bool isCached;

        /// <summary>
        /// Đọc texture + Rotation từ sourceMaterial, tự nhận diện 2 định dạng:
        ///  - Skybox/Cubemap (property "_Tex", kiểu Cubemap) -> isCubeFormat = true.
        ///  - Skybox/6 Sided (property "_FrontTex"...) -> isCubeFormat = false.
        /// Gọi 1 lần, cache lại.
        /// </summary>
        public void EnsureCached()
        {
            if (isCached || sourceMaterial == null)
                return;

            if (sourceMaterial.HasProperty("_Tex") &&
                sourceMaterial.GetTexture("_Tex") is Cubemap cube)
            {
                isCubeFormat = true;
                cachedCubemap = cube;
            }
            else
            {
                isCubeFormat = false;
                cachedFront = sourceMaterial.HasProperty("_FrontTex") ? sourceMaterial.GetTexture("_FrontTex") : null;
                cachedBack = sourceMaterial.HasProperty("_BackTex") ? sourceMaterial.GetTexture("_BackTex") : null;
                cachedLeft = sourceMaterial.HasProperty("_LeftTex") ? sourceMaterial.GetTexture("_LeftTex") : null;
                cachedRight = sourceMaterial.HasProperty("_RightTex") ? sourceMaterial.GetTexture("_RightTex") : null;
                cachedUp = sourceMaterial.HasProperty("_UpTex") ? sourceMaterial.GetTexture("_UpTex") : null;
                cachedDown = sourceMaterial.HasProperty("_DownTex") ? sourceMaterial.GetTexture("_DownTex") : null;
            }

            cachedRotation = sourceMaterial.HasProperty("_Rotation") ? sourceMaterial.GetFloat("_Rotation") : 0f;

            isCached = true;
        }
    }

    [Header("References")]
    [Tooltip("Object cha dùng để xoay mặt trời (VD SunAxis trong hierarchy).")]
    [SerializeField] private Transform _sunAxis;
    [Tooltip("Directional Light chính (VD MorningSun).")]
    [SerializeField] private Light _sunLight;

    [Header("Sun Arc - xoay trục Z (sáng -> trưa)")]
    [Tooltip("Góc Z của SunAxis lúc bắt đầu ngày / bình minh.")]
    [SerializeField] private float _sunriseAngleZ = 77.5f;
    [Tooltip("Góc Z của SunAxis lúc trưa (điểm sáng nhất trong ngày). " +
             "Sau mốc trưa (t > 0.5) trục Z sẽ GIỮ NGUYÊN ở góc này, " +
             "không xoay thêm nữa.")]
    [SerializeField] private float _noonAngleZ = 72f;
    [Tooltip("Đường cong TỐC ĐỘ xoay từ bình minh -> trưa. Trục X = tiến " +
             "trình thời gian trong nửa đầu ngày (0 = bình minh, 1 = trưa). " +
             "Trục Y = vị trí nội suy tương ứng giữa _sunriseAngleZ (0) và " +
             "_noonAngleZ (1). Mặc định dốc đứng ở đầu (0) và bằng phẳng ở " +
             "cuối (1) -> xoay NHANH lúc mới mọc, CHẬM DẦN khi gần đứng " +
             "bóng, giống chuyển động bóng nắng thật ngoài đời (khác với " +
             "Lerp tuyến tính cũ, xoay đều đều nhìn giả). Kéo tangent 2 đầu " +
             "trong Inspector nếu muốn chỉnh độ 'chậm dần' nhiều/ít hơn.")]
    [SerializeField] private AnimationCurve _sunArcEase =
        new AnimationCurve(
            new Keyframe(0f, 0f, 1.6f, 1.6f),
            new Keyframe(1f, 1f, 0f, 0f)
        );

    [Header("Light Intensity theo giờ (không đổi màu)")]
    [Tooltip("Intensity của Directional Light trong khoảng sáng -> lúc bắt đầu " +
             "giảm (t: 0 -> _intensityFadeStart). Giữ cố định ở giá trị này " +
             "suốt giai đoạn đó (mặc định 2).")]
    [SerializeField] private float _dayIntensity = 2f;
    [Tooltip("Intensity của Directional Light lúc cuối ngày (t = 1). " +
             "Từ mốc _intensityFadeStart (intensity = _dayIntensity = 2) sẽ " +
             "giảm dần tuyến tính xuống giá trị này (0.5) khi về tối.")]
    [SerializeField] private float _eveningIntensity = 0.5f;
    [Tooltip("Mốc t (0 -> 1) BẮT ĐẦU giảm intensity. Nên đặt TRÙNG với " +
             "timeOfDay01 của phase 'Buổi Chiều' trong Sky Phases bên dưới, " +
             "để ánh sáng chỉ tối dần đúng lúc skybox cũng bắt đầu chuyển " +
             "sang chiều, tránh bị lệch pha (tối trước khi skybox kịp đổi).")]
    [Range(0f, 1f)]
    [SerializeField] private float _intensityFadeStart = 0.74f;

    [Header("Ambient theo giờ")]
    [Tooltip("Nhân độ sáng môi trường (Lighting > Environment > Intensity Multiplier).")]
    [SerializeField] private AnimationCurve _ambientIntensityOverDay =
        AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1f);

    [Header("Skybox Blend")]
    [Tooltip("Material dùng shader Custom/SkyboxBlend6Sided (phải trùng với " +
             "material đang gán ở Lighting > Environment > Skybox Material).")]
    [SerializeField] private Material _blendSkyboxMat;
    [Tooltip("Danh sách skybox theo mốc thời gian, PHẢI sắp xếp timeOfDay01 tăng dần.")]
    [SerializeField] private SkyPhase[] _skyPhases;
    [Tooltip("Gọi DynamicGI.UpdateEnvironment() mỗi khi đổi cặp skybox để ambient " +
             "light phản chiếu đúng skybox mới. Tắt nếu thấy lag.")]
    [SerializeField] private bool _updateEnvironmentOnPhaseChange = true;
    [Tooltip("Đường cong tốc độ MỜ DẦN (crossfade _Blend) giữa 2 skybox liền " +
             "kề. Trục X = tiến trình trong khoảng giữa 2 mốc (0 -> 1), " +
             "trục Y = độ mờ thực tế áp dụng vào shader. Mặc định là S-curve " +
             "(chậm lúc mới bắt đầu -> nhanh ở giữa -> chậm lại lúc gần " +
             "xong), giống mây/ánh sáng thay đổi tự nhiên hơn so với mờ " +
             "đều đều tuyến tính.")]
    [SerializeField] private AnimationCurve _skyBlendEase =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private static readonly int FrontA = Shader.PropertyToID("_FrontA");
    private static readonly int BackA = Shader.PropertyToID("_BackA");
    private static readonly int LeftA = Shader.PropertyToID("_LeftA");
    private static readonly int RightA = Shader.PropertyToID("_RightA");
    private static readonly int UpA = Shader.PropertyToID("_UpA");
    private static readonly int DownA = Shader.PropertyToID("_DownA");

    private static readonly int FrontB = Shader.PropertyToID("_FrontB");
    private static readonly int BackB = Shader.PropertyToID("_BackB");
    private static readonly int LeftB = Shader.PropertyToID("_LeftB");
    private static readonly int RightB = Shader.PropertyToID("_RightB");
    private static readonly int UpB = Shader.PropertyToID("_UpB");
    private static readonly int DownB = Shader.PropertyToID("_DownB");

    private static readonly int RotationA = Shader.PropertyToID("_RotationA");
    private static readonly int RotationB = Shader.PropertyToID("_RotationB");
    private static readonly int CubeA = Shader.PropertyToID("_CubeA");
    private static readonly int CubeB = Shader.PropertyToID("_CubeB");
    private static readonly int UseCubeA = Shader.PropertyToID("_UseCubeA");
    private static readonly int UseCubeB = Shader.PropertyToID("_UseCubeB");
    private static readonly int BlendId = Shader.PropertyToID("_Blend");

    private int _cachedPhaseIndex = -1;

    private void Update()
    {
        if (GameDayManager.Instance == null)
            return;

        // ========================================================
        // DAY PANEL ĐANG CHUYỂN
        // ========================================================
        //
        // Không cho lighting / skybox thay đổi trong lúc
        // panel đang fade.
        //
        // Khi panel đen hoàn toàn, GameDayManager sẽ reset
        // timer về 0. Sau đó panel fade out và Update() chạy
        // bình thường với trạng thái đầu ngày.
        //

        if (GameDayIntroPanel.Instance != null &&
            GameDayIntroPanel.Instance.IsVisible)
        {
            return;
        }

        float t =
            GameDayManager.Instance.GetElapsedFraction01();

        UpdateSunTransform(t);
        UpdateLight(t);
        UpdateAmbient(t);
        UpdateSkyboxBlend(t);
    }

    // ============================================================
    // SUN ARC (trục Z, chỉ xoay từ sáng -> trưa)
    // ============================================================

    private void UpdateSunTransform(float t)
    {
        if (_sunAxis == null)
            return;

        // t <= 0.5 (sáng -> trưa): xoay Z từ bình minh -> trưa, tốc độ theo
        // _sunArcEase (nhanh lúc mới mọc, chậm dần khi gần trưa) thay vì
        // đều đều tuyến tính.
        // t > 0.5 (trưa -> tối): KHÔNG xoay Z nữa, giữ cố định ở _noonAngleZ.
        float angleZ;
        if (t <= 0.5f)
        {
            float normalizedT = Mathf.Clamp01(t / 0.5f);
            float easedProgress = _sunArcEase != null
                ? _sunArcEase.Evaluate(normalizedT)
                : normalizedT;
            angleZ = Mathf.Lerp(_sunriseAngleZ, _noonAngleZ, easedProgress);
        }
        else
        {
            angleZ = _noonAngleZ;
        }

        Vector3 e = _sunAxis.eulerAngles;
        _sunAxis.eulerAngles = new Vector3(e.x, e.y, angleZ);
    }

    // ============================================================
    // LIGHT INTENSITY (không đổi màu)
    // ============================================================

    private void UpdateLight(float t)
    {
        if (_sunLight == null)
            return;

        // Sáng -> mốc _intensityFadeStart: giữ nguyên intensity ở mức ban ngày.
        // Sau mốc đó -> tối: giảm dần tuyến tính từ _dayIntensity -> _eveningIntensity.
        float fadeSpan = Mathf.Max(0.0001f, 1f - _intensityFadeStart);
        _sunLight.intensity = t <= _intensityFadeStart
            ? _dayIntensity
            : Mathf.Lerp(_dayIntensity, _eveningIntensity, (t - _intensityFadeStart) / fadeSpan);

        // Màu ánh sáng KHÔNG bị đổi ở đây nữa - giữ nguyên màu bạn set trong Inspector.
    }

    // ============================================================
    // AMBIENT
    // ============================================================

    private void UpdateAmbient(float t)
    {
        if (_ambientIntensityOverDay != null)
            RenderSettings.ambientIntensity = _ambientIntensityOverDay.Evaluate(t);
    }

    // ============================================================
    // SKYBOX BLEND
    // ============================================================

    private void UpdateSkyboxBlend(float t)
    {
        if (_blendSkyboxMat == null || _skyPhases == null || _skyPhases.Length < 2)
            return;

        // Tìm 2 mốc skybox liền kề chứa t (mảng phải sắp theo timeOfDay01 tăng dần).
        int i = 0;
        while (i < _skyPhases.Length - 2 && t > _skyPhases[i + 1].timeOfDay01)
            i++;

        SkyPhase a = _skyPhases[i];
        SkyPhase b = _skyPhases[i + 1];

        a.EnsureCached();
        b.EnsureCached();

        float span = Mathf.Max(0.0001f, b.timeOfDay01 - a.timeOfDay01);
        float rawLocalT = Mathf.Clamp01((t - a.timeOfDay01) / span);
        float localT = _skyBlendEase != null ? _skyBlendEase.Evaluate(rawLocalT) : rawLocalT;

        // Chỉ set lại texture + rotation khi đổi cặp phase, đỡ tốn set property mỗi frame.
        if (i != _cachedPhaseIndex)
        {
            _cachedPhaseIndex = i;

            if (a.isCubeFormat)
            {
                _blendSkyboxMat.SetTexture(CubeA, a.cachedCubemap);
                _blendSkyboxMat.SetFloat(UseCubeA, 1f);
            }
            else
            {
                _blendSkyboxMat.SetTexture(FrontA, a.cachedFront);
                _blendSkyboxMat.SetTexture(BackA, a.cachedBack);
                _blendSkyboxMat.SetTexture(LeftA, a.cachedLeft);
                _blendSkyboxMat.SetTexture(RightA, a.cachedRight);
                _blendSkyboxMat.SetTexture(UpA, a.cachedUp);
                _blendSkyboxMat.SetTexture(DownA, a.cachedDown);
                _blendSkyboxMat.SetFloat(UseCubeA, 0f);
            }
            _blendSkyboxMat.SetFloat(RotationA, a.cachedRotation);

            if (b.isCubeFormat)
            {
                _blendSkyboxMat.SetTexture(CubeB, b.cachedCubemap);
                _blendSkyboxMat.SetFloat(UseCubeB, 1f);
            }
            else
            {
                _blendSkyboxMat.SetTexture(FrontB, b.cachedFront);
                _blendSkyboxMat.SetTexture(BackB, b.cachedBack);
                _blendSkyboxMat.SetTexture(LeftB, b.cachedLeft);
                _blendSkyboxMat.SetTexture(RightB, b.cachedRight);
                _blendSkyboxMat.SetTexture(UpB, b.cachedUp);
                _blendSkyboxMat.SetTexture(DownB, b.cachedDown);
                _blendSkyboxMat.SetFloat(UseCubeB, 0f);
            }
            _blendSkyboxMat.SetFloat(RotationB, b.cachedRotation);

            if (_updateEnvironmentOnPhaseChange)
                DynamicGI.UpdateEnvironment();
        }

        _blendSkyboxMat.SetFloat(BlendId, localT);
    }
}