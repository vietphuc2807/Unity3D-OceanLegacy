using UnityEngine;

/// Gắn vào object chứa chuỗi xương cần câu. Dùng để mô phỏng việc cần câu
/// bị cong dọc theo NHIỀU khớp xương (thay vì 1 pivot duy nhất) khi đang
/// kéo cá trong CatchFishMinigame, và giật cong đột ngột khi vừa móc trúng
/// cá trong HookFishMinigame.
///
/// CẬP NHẬT (multiplayer): KHÔNG còn là singleton toàn scene nữa. Trước đây
/// dùng static Instance khiến TẤT CẢ người chơi share chung 1 component này
/// — trên máy của A, chỉ cần câu của A cong, cần câu của B (model B đang
/// cầm) hoàn toàn không nhận được lệnh cong nào vì code luôn gọi vào
/// Instance (chính là component của A, người đầu tiên Awake trong scene).
/// Giờ mỗi rig cần câu (của từng player) có 1 component FishingRodBendEffect
/// RIÊNG, và FishingLine tương ứng của player đó giữ reference thẳng tới nó
/// (xem FishingLine._rodBendEffect) — nhờ vậy mọi lệnh cong luôn tác động
/// đúng lên cần của đúng người chơi, dù xem từ máy nào.
///
/// CẬP NHẬT: cần giờ cong HƯỚNG VỀ TARGET (phao/điểm neo dây câu trong
/// world space) thay vì cong cứng theo 1 trục cục bộ cố định (_bendAxis).
/// Nhờ vậy khi camera/nhân vật xoay trái phải, đầu cần vẫn luôn cong đúng
/// về phía dây câu — giống cần câu ngoài đời luôn cong theo hướng lực kéo,
/// bất kể mình đứng quay mặt hướng nào. Gọi SetBendTargetPosition() mỗi
/// frame (từ FishingLine hoặc minigame) để báo vị trí phao/điểm neo dây.
/// Nếu KHÔNG gọi (chưa có target), cần tự rơi về hành vi cũ: cong theo
/// trục cục bộ cố định _bendAxis — nên không có gì bị hỏng nếu bạn chưa
/// kịp nối dây vào chỗ khác.
public class FishingRodBendEffect : MonoBehaviour
{
    private enum BendAxis { X, Y, Z }

    [Header("References")]
    [Tooltip("Chuỗi xương cần câu, xếp theo đúng thứ tự TỪ GỐC ĐẾN NGỌN " +
             "(vd: Root_01, Root_02, ..., Root_12). KHÔNG kéo bone 'Root' " +
             "gốc vào đây — đó là điểm neo cố định của cả armature, xoay " +
             "nó sẽ làm dịch/xoay toàn bộ cần thay vì uốn cong.")]
    [SerializeField] private Transform[] _rodBones;

    [Tooltip("Transform neo cố định của cần (thường là bone 'Root' gốc, " +
             "KHÔNG nằm trong _rodBones, không bị cong) — dùng làm hệ quy " +
             "chiếu để tính hướng cong về target. Để trống thì tự lấy " +
             "parent của bone đầu tiên trong _rodBones (thường là đúng).")]
    [SerializeField] private Transform _anchorRoot;

    [Header("Bend — hướng cong cố định (fallback khi chưa có target)")]
    [Tooltip("Trục cục bộ (local) mà mỗi bone sẽ xoay quanh để tạo độ cong " +
             "KHI CHƯA CÓ bend target (xem SetBendTargetPosition). Một khi " +
             "đã có target, trục cong được TÍNH TỰ ĐỘNG hướng về target nên " +
             "giá trị này không còn ảnh hưởng nữa. Nếu cần cong sai hướng " +
             "trong lúc chưa có target, thử đổi trục này trước khi đổi dấu góc.")]
    [SerializeField] private BendAxis _bendAxis = BendAxis.X;

    [Tooltip("Tổng góc cong tối đa (độ) của TOÀN BỘ cần khi bend = 1, " +
             "cộng dồn qua tất cả các khớp trong _rodBones. Khi ĐÃ có bend " +
             "target thì đây chỉ còn là ĐỘ LỚN góc cong (luôn cong theo " +
             "chiều hướng về target, không phụ thuộc dấu +/- nữa). Khi CHƯA " +
             "có target thì dấu vẫn quyết định chiều cong như cũ. Nếu thấy " +
             "cần cong quá ít khi nhìn ở góc FPP, thử tăng độ lớn giá trị " +
             "này lên (vd -45, -60) — an toàn để tăng vì đường cong Bend " +
             "Distribution vẫn giữ gốc cứng, chỉ ngọn cong nhiều hơn.")]
    [SerializeField] private float _maxBendAngle = 28f;

    [Tooltip("Đường cong phân bổ độ cong TÍCH LŨY dọc theo chuỗi xương. " +
             "Trục X = vị trí xương (0 = gốc, 1 = ngọn ngoài cùng). " +
             "Trục Y = tỉ lệ cong đã tích lũy tới khớp đó (0..1). " +
             "Vì các khớp lồng nhau nên góc cong sẽ CỘNG DỒN xuống các " +
             "khớp con — để gốc gần như thẳng (cứng, giống tay cầm) và " +
             "tăng dần về ngọn (mềm, cong nhiều) thì dùng easeIn/easeInOut " +
             "như mặc định bên dưới.")]
    [SerializeField] private AnimationCurve _bendDistribution =
        AnimationCurve.EaseInOut(0f, 0.03f, 1f, 1f);

    [SerializeField] private float _bendSmoothTime = 0.12f;
    [SerializeField] private float _releaseSmoothTime = 0.25f;

    [Header("Hook Snap (giật cong đột ngột khi vừa móc trúng cá)")]
    [Tooltip("Góc cong ĐỘT NGỘT (độ) được cộng thêm vào bend hiện tại ngay " +
             "lúc TriggerHookSnap() được gọi (khi người chơi bấm trúng " +
             "HookFishMinigame), rồi tự đàn hồi rất nhanh về lại mức bend " +
             "bình thường — mô phỏng cảm giác bị cá giật cần đột ngột. Khi " +
             "đã có bend target thì chỉ ĐỘ LỚN giá trị này có ý nghĩa (chiều " +
             "giật luôn hướng về phía target); khi chưa có target thì dấu " +
             "vẫn quyết định chiều như cũ, nên nên cùng DẤU với Max Bend Angle.")]
    [SerializeField] private float _hookSnapAngle = -45f;
    [Tooltip("Thời gian (giây) để cần vọt từ thẳng lên tới đỉnh cú giật " +
             "(_hookSnapAngle). Để RẤT NHỎ (vd 0.04-0.08) để vẫn cảm giác " +
             "\"đột ngột\", nhưng khác 0 để SmoothDamp có thời gian nội suy " +
             "mượt thay vì nhảy 1 frame — đây là chỗ chỉnh nếu thấy cần " +
             "\"giật cục\"/pop thay vì giật mượt. Tăng lên nếu vẫn thấy gắt, " +
             "giảm xuống nếu thấy lừ đừ không còn cảm giác đột ngột.")]
    [SerializeField] private float _hookSnapAttackTime = 0.05f;
    [Tooltip("Thời gian (giây) để cú giật đàn hồi trả về lại bend bình " +
             "thường sau khi đã lên tới đỉnh — để NHỎ (nhanh) vì đây là " +
             "hiệu ứng chớp nhoáng, khác với Release Smooth Time (vốn dùng " +
             "cho lúc cá kéo dây đi từ từ).")]
    [SerializeField] private float _hookSnapRecoverTime = 0.15f;
    [Tooltip("Khi độ lệch giữa góc cong hiện tại và đỉnh cú giật nhỏ hơn " +
             "giá trị này (độ), coi như đã \"tới đỉnh\" và chuyển sang pha " +
             "đàn hồi về 0. Không cần chỉnh trừ khi cú giật bị đàn hồi quá " +
             "sớm (chưa kịp lên tới đỉnh) hoặc quá trễ (đứng khựng ở đỉnh).")]
    [SerializeField] private float _hookSnapPeakThreshold = 1.5f;

    [Header("Per-Bone Sign Override (chỉ áp dụng cho rung/struggle + fallback)")]
    [Tooltip("Chỉ dùng khi các bone trong rig KHÔNG đồng nhất hướng trục " +
             "local (một số bone bị lật ngược so với các bone khác). Áp " +
             "dụng cho hiệu ứng rung (shake) và cho chế độ fallback trục cố " +
             "định — KHÔNG áp dụng cho bend hướng-về-target vì trục đó đã " +
             "được tính riêng cho từng bone rồi. Để trống (size 0) nếu tất " +
             "cả bone cong cùng chiều — trường hợp thường gặp.")]
    [SerializeField] private float[] _perBoneSign;

    [Header("Struggle Shake")]
    [SerializeField] private float _shakeAngleAmount = 2.5f;
    [SerializeField] private float _shakeNoiseSpeed = 9f;
    [SerializeField] private float _shakeFadeDuration = 0.3f;

    [Tooltip("Tương tự Bend Distribution nhưng cho biên độ rung — cũng là " +
             "đường cong tích lũy theo chuỗi xương.")]
    [SerializeField] private AnimationCurve _shakeDistribution =
        AnimationCurve.EaseInOut(0f, 0.05f, 1f, 1f);

    [Header("Struggle Intensity (rung theo tình trạng giằng co)")]
    [Tooltip("Hệ số nhân biên độ rung khi đang THẮNG THẾ (progress kéo cá " +
             "đứng yên hoặc tăng) — để < 1 để cần rung nhẹ hơn bình thường " +
             "lúc đang chiếm ưu thế, không bị rung đều đều suốt minigame.")]
    [SerializeField] private float _struggleIntensityMin = 0.4f;
    [Tooltip("Hệ số nhân biên độ rung khi CÁ ĐANG KÉO LẠI MẠNH (progress " +
             "giảm nhanh) — để > 1 để cần rung mạnh hẳn lên lúc đang bị cá " +
             "kéo, tạo cảm giác giằng co rõ rệt. Tăng lên nếu muốn cú giằng " +
             "co kịch tính hơn, giảm xuống nếu thấy quá lố.")]
    [SerializeField] private float _struggleIntensityMax = 1.8f;
    [Tooltip("Thời gian mượt (giây) khi cường độ rung đổi giữa min/max — " +
             "để nhỏ nếu muốn phản ứng nhanh theo từng nhịp giật của cá, " +
             "tăng lên nếu muốn cường độ đổi từ từ, êm hơn.")]
    [SerializeField] private float _struggleIntensitySmoothTime = 0.15f;

    private Quaternion[] _baseRots;
    private bool _hasCachedBase;

    // Hướng từ bone gốc tới bone ngọn LÚC NGHỈ (chưa cong gì), lưu trong hệ
    // quy chiếu local của _anchorRoot — đây là "đường thẳng chuẩn" để so
    // sánh với hướng tới target mỗi frame, tính ra trục cần xoay quanh.
    private Vector3 _restTipLocalDir;

    private float _currentBend;
    private float _targetBend;
    private float _bendVelocity;

    // Cú giật đột ngột (hook snap) — cộng thêm vào góc cong (độ), tách
    // biệt hoàn toàn khỏi _currentBend. _snapOffset đuổi theo _snapTarget
    // bằng SmoothDamp với 2 pha: vọt nhanh lên đỉnh (_hookSnapAttackTime)
    // rồi đàn hồi về 0 (_hookSnapRecoverTime) — thay vì nhảy tức thời 1
    // frame như trước, để cú giật vẫn nhanh/gắt nhưng không bị "pop".
    private float _snapOffset;
    private float _snapVelocity;
    private float _snapTarget;
    private bool _snapAttacking;

    private bool _struggling;
    private float _struggleWeight;
    private float _noiseSeedX;
    private float _noiseSeedZ;

    // Cường độ rung hiện tại/mục tiêu theo tình trạng giằng co — 1 =
    // bình thường (dùng lúc chưa từng nhận SetStrugglePull, hoặc đã reset),
    // < 1 khi đang thắng thế, > 1 khi cá đang kéo lại mạnh (xem
    // SetStrugglePull()).
    private float _targetStruggleIntensity = 1f;
    private float _currentStruggleIntensity = 1f;
    private float _struggleIntensityVelocity;

    private float _strengthScale = 1f; // 0 = cá nhỏ, 1 = cá lớn — set qua SetStrengthScale()
    private float _struggleIntensityMinBase;
    private float _struggleIntensityMaxBase;
    private float _hookSnapAngleBase;

    // Vị trí target (phao/điểm neo dây câu) trong world space — đầu cần sẽ
    // luôn cong hướng về điểm này. _hasBendTarget = false → dùng fallback
    // trục cố định _bendAxis như bản cũ.
    private Vector3 _bendTargetWorldPos;
    private bool _hasBendTarget;

    private void Awake()
    {
        _noiseSeedX = Random.Range(0f, 1000f);
        _noiseSeedZ = Random.Range(0f, 1000f);

        CacheBasePose();

        _struggleIntensityMinBase = _struggleIntensityMin;
        _struggleIntensityMaxBase = _struggleIntensityMax;
        _hookSnapAngleBase = _hookSnapAngle;
    }

    private void CacheBasePose()
    {
        if (_rodBones == null || _rodBones.Length == 0)
            return;

        _baseRots = new Quaternion[_rodBones.Length];
        for (int i = 0; i < _rodBones.Length; i++)
        {
            if (_rodBones[i] != null)
                _baseRots[i] = _rodBones[i].localRotation;
        }

        Transform anchor = GetAnchor();
        Transform tip = _rodBones[_rodBones.Length - 1];

        if (anchor != null && tip != null)
        {
            Vector3 worldRestDir = tip.position - _rodBones[0].position;

            // Nếu các bone đang trùng vị trí (rig dựng bằng rotation thuần,
            // không có offset vị trí) thì fallback về trục cố định để tránh
            // vector 0 gây lỗi chuẩn hoá.
            _restTipLocalDir = worldRestDir.sqrMagnitude > 0.0001f
                ? anchor.InverseTransformDirection(worldRestDir.normalized)
                : AxisToVector(_bendAxis);
        }
        else
        {
            _restTipLocalDir = AxisToVector(_bendAxis);
        }

        _hasCachedBase = true;
    }

    private Transform GetAnchor()
    {
        if (_anchorRoot != null)
            return _anchorRoot;

        if (_rodBones != null && _rodBones.Length > 0 && _rodBones[0] != null && _rodBones[0].parent != null)
            return _rodBones[0].parent;

        return _rodBones != null && _rodBones.Length > 0 ? _rodBones[0] : transform;
    }

    private static Vector3 AxisToVector(BendAxis axis)
    {
        return axis switch
        {
            BendAxis.X => Vector3.right,
            BendAxis.Y => Vector3.up,
            BendAxis.Z => Vector3.forward,
            _ => Vector3.right
        };
    }

    private void LateUpdate()
    {
        if (_rodBones == null || _rodBones.Length == 0)
            return;

        if (!_hasCachedBase)
            CacheBasePose();

        float smoothTime = _targetBend > _currentBend ? _bendSmoothTime : _releaseSmoothTime;
        _currentBend = Mathf.SmoothDamp(_currentBend, _targetBend, ref _bendVelocity, Mathf.Max(0.01f, smoothTime));

        // Cú giật (nếu có) chạy qua 2 pha, cả 2 đều là SmoothDamp (liên tục,
        // không nhảy khung hình nào) nên luôn mượt dù nhanh:
        // 1) Attack: đuổi nhanh từ 0 lên _snapTarget (_hookSnapAttackTime).
        // 2) Khi đã gần tới đỉnh (trong _hookSnapPeakThreshold độ) → đổi
        //    target về 0 và tiếp tục đuổi theo, giờ dùng _hookSnapRecoverTime
        //    (thường lớn hơn attack) → đàn hồi chậm rãi hơn lúc vọt lên.
        if (_snapAttacking && Mathf.Abs(_snapOffset - _snapTarget) <= _hookSnapPeakThreshold)
        {
            _snapAttacking = false;
            _snapTarget = 0f;
        }

        float snapSmoothTime = _snapAttacking ? _hookSnapAttackTime : _hookSnapRecoverTime;
        _snapOffset = Mathf.SmoothDamp(_snapOffset, _snapTarget, ref _snapVelocity, Mathf.Max(0.01f, snapSmoothTime));

        _struggleWeight = Mathf.MoveTowards(
            _struggleWeight,
            _struggling ? 1f : 0f,
            Time.deltaTime / Mathf.Max(0.01f, _shakeFadeDuration)
        );

        // Cường độ rung theo tình trạng giằng co, đuổi mượt theo target do
        // SetStrugglePull() đặt mỗi frame (xem CatchFishMinigame.Update()).
        _currentStruggleIntensity = Mathf.SmoothDamp(
            _currentStruggleIntensity, _targetStruggleIntensity,
            ref _struggleIntensityVelocity, Mathf.Max(0.01f, _struggleIntensitySmoothTime)
        );

        int count = _rodBones.Length;
        bool hasPerBoneSign = _perBoneSign != null && _perBoneSign.Length == count;

        // Tổng góc cong (độ) tại thời điểm này = bend theo tiến độ + cú giật
        // đang đàn hồi.
        float totalAngleScale = _currentBend * _maxBendAngle + _snapOffset;

        // Trục cong dùng chung cho cả cần trong frame này, tính 1 lần
        // (world space) rồi quy đổi riêng cho từng bone bên dưới.
        Vector3 worldBendAxis = GetWorldBendAxis();

        // Khi đã có target: chiều cong do trục quyết định rồi, chỉ cần ĐỘ
        // LỚN góc cong, không cần dấu +/- của _maxBendAngle/_hookSnapAngle
        // nữa (tránh cong ngược nếu người thiết kế set giá trị âm).
        float bendMagnitude = _hasBendTarget ? Mathf.Abs(totalAngleScale) : totalAngleScale;

        float prevBendCum  = 0f;
        float prevShakeCum = 0f;

        for (int i = 0; i < count; i++)
        {
            Transform bone = _rodBones[i];
            if (bone == null)
                continue;

            // 0 = khớp gần gốc nhất, 1 = khớp ngọn nhất
            float t = count > 1 ? (float)i / (count - 1) : 1f;

            // Lấy HIỆU SỐ giữa 2 mốc tích lũy liền kề → góc cục bộ thực sự
            // cần xoay tại khớp này, vì góc sẽ tự cộng dồn xuống các con.
            float bendCum   = _bendDistribution.Evaluate(t);
            float bendDelta = bendCum - prevBendCum;
            prevBendCum = bendCum;

            float shakeCum   = _shakeDistribution.Evaluate(t);
            float shakeDelta = shakeCum - prevShakeCum;
            prevShakeCum = shakeCum;

            float sign = hasPerBoneSign ? _perBoneSign[i] : 1f;

            // Quy đổi trục cong world → local space của PARENT bone này
            // (bone trước đó trong chuỗi, đã được xoay ở vòng lặp này rồi,
            // vì _rodBones xếp từ gốc đến ngọn) — nhờ vậy hướng cong nhất
            // quán dọc suốt cần dù rig có bao nhiêu khớp, và tự động đúng
            // chiều ngay cả khi bone bị lật hướng trục local so với bone kia.
            Transform parentSpace = bone.parent != null ? bone.parent : bone;
            Vector3 localAxis = parentSpace.InverseTransformDirection(worldBendAxis);

            if (localAxis.sqrMagnitude < 0.0001f)
                localAxis = AxisToVector(_bendAxis);
            else
                localAxis.Normalize();

            // Chế độ fallback (chưa có target) giữ nguyên hành vi cũ: sign
            // quyết định chiều; chế độ target-tracking bỏ qua sign cho phần
            // bend vì trục đã tự đúng chiều theo từng bone.
            float bendAngle = bendMagnitude * bendDelta * (_hasBendTarget ? 1f : sign);

            Quaternion bendOffset = Quaternion.AngleAxis(bendAngle, localAxis);

            float shakeX = (Mathf.PerlinNoise(Time.time * _shakeNoiseSpeed + i * 10f, _noiseSeedX) - 0.5f) * 2f
                           * _shakeAngleAmount * shakeDelta * _struggleWeight * _currentStruggleIntensity * sign;
            float shakeZ = (Mathf.PerlinNoise(i * 10f, Time.time * _shakeNoiseSpeed + _noiseSeedZ) - 0.5f) * 2f
                           * _shakeAngleAmount * shakeDelta * _struggleWeight * _currentStruggleIntensity * 0.6f * sign;

            Quaternion shakeOffset = Quaternion.Euler(shakeX, 0f, shakeZ);

            bone.localRotation = _baseRots[i] * bendOffset * shakeOffset;
        }
    }

    /// Tính trục cong (world space) cho frame hiện tại.
    /// - Có target: trục = vuông góc giữa hướng cần LÚC NGHỈ và hướng từ
    ///   gốc cần tới target → xoay quanh trục này sẽ đưa đầu cần hướng dần
    ///   về phía target, đúng cảm giác cần bị kéo cong theo lực.
    /// - Không có target (hoặc target gần như thẳng hàng với cần, không có
    ///   trục cong rõ ràng): fallback về trục cục bộ cố định _bendAxis.
    private Vector3 GetWorldBendAxis()
    {
        Transform anchor = GetAnchor();
        Vector3 fallback = anchor.TransformDirection(AxisToVector(_bendAxis));

        if (!_hasBendTarget || _rodBones == null || _rodBones.Length == 0 || _rodBones[0] == null)
            return fallback;

        Vector3 worldRestDir = anchor.TransformDirection(_restTipLocalDir);
        Vector3 toTarget = _bendTargetWorldPos - _rodBones[0].position;

        if (toTarget.sqrMagnitude < 0.0001f)
            return fallback;

        Vector3 axis = Vector3.Cross(worldRestDir, toTarget.normalized);

        // Target gần như nằm ngay trên đường thẳng nối dài của cần (cùng
        // hướng hoặc ngược hướng hẳn) → cross gần 0, không có trục cong rõ
        // ràng. Giữ nguyên trục cũ thay vì để cần giật lung tung.
        if (axis.sqrMagnitude < 0.0001f)
            return fallback;

        return axis.normalized;
    }

    public void SetBend(float value01)
    {
        _targetBend = Mathf.Clamp01(value01);
    }

    /// Gọi mỗi frame (từ FishingLine, HookFishMinigame hoặc CatchFishMinigame)
    /// để báo vị trí phao/điểm neo dây câu trong world space. Đầu cần sẽ tự
    /// tính lại trục cong mỗi frame để luôn cong hướng về điểm này — nhờ vậy
    /// dù người chơi xoay camera/nhân vật sang trái hay phải, cần vẫn cong
    /// đúng về phía dây câu chứ không cong cứng theo 1 hướng cố định.
    public void SetBendTargetPosition(Vector3 worldPosition)
    {
        _bendTargetWorldPos = worldPosition;
        _hasBendTarget = true;
    }

    /// Bỏ theo dõi target — cần quay lại cong theo trục cục bộ cố định
    /// _bendAxis (dùng khi chưa quăng cần / dây không còn trong nước, ví dụ
    /// lúc reset về idle).
    public void ClearBendTarget()
    {
        _hasBendTarget = false;
    }

    public void StartStruggle()
    {
        _struggling = true;
    }

    public void StopStruggle()
    {
        _struggling = false;
    }

    /// Gọi mỗi frame từ CatchFishMinigame để báo mức độ "cá đang kéo lại"
    /// hiện tại: 0 = đang thắng thế (progress đứng yên/tăng) → rung nhẹ
    /// (_struggleIntensityMin), 1 = cá đang kéo mạnh (progress giảm nhanh)
    /// → rung mạnh (_struggleIntensityMax). Nhờ vậy rung không còn đều đều
    /// suốt minigame mà thực sự phản ánh đang giằng co ai thắng ai.
    public void SetStrugglePull(float pull01)
    {
        pull01 = Mathf.Clamp01(pull01);

        // Cá nhỏ: cả biên trên lẫn biên dưới đều bị kéo xuống thấp hơn.
        // Cá lớn: biên trên bị đẩy cao hơn hẳn, giằng co rung mạnh rõ rệt.
        float scaledMin = _struggleIntensityMinBase * Mathf.Lerp(0.5f, 1f, _strengthScale);
        float scaledMax = _struggleIntensityMaxBase * Mathf.Lerp(0.45f, 1.4f, _strengthScale);

        _targetStruggleIntensity = Mathf.Lerp(scaledMin, scaledMax, pull01);
    }

    public void SetStrengthScale(float scale01)
    {
        _strengthScale = Mathf.Clamp01(scale01);
    }

    public void ResetBend()
    {
        _targetBend = 0f;
        _struggling = false;
        _targetStruggleIntensity = 1f;
    }

    /// Gọi khi người chơi vừa bấm TRÚNG trong HookFishMinigame (móc trúng
    /// cá) — cần bất chợt cong mạnh thêm một nhịp rồi tự đàn hồi rất nhanh
    /// về lại mức bend bình thường, mô phỏng cú giật đầu tiên của cá.
    public void TriggerHookSnap()
    {
        float scaledAngle = _hookSnapAngleBase * Mathf.Lerp(0.45f, 1.6f, _strengthScale);
        _snapTarget = scaledAngle;
        _snapAttacking = true;
    }
}