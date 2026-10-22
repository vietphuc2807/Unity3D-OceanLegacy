using UnityEngine;

/// <summary>
/// Đặt component này lên CÙNG GameObject với Animator (thường là model con,
/// không phải root có NetPlayerMovementFusion), vì Animation Event dùng
/// GameObject.SendMessage và chỉ gọi được component cùng object với Animator.
///
/// Cách gán Animation Event trong Animation clip (Walk/Run):
///   - Mở clip trong Animation window, tìm frame chân TRÁI chạm đất
///     -> thêm Event -> function = OnFootstepLeft
///   - Tương tự cho chân PHẢI -> function = OnFootstepRight
///
/// Water check: mỗi lần đạp chân, raycast từ chân LÊN TRÊN để tìm mặt nước
/// (_waterLayerMask). Nếu có mặt nước nằm cao hơn vị trí chân -> chân đang
/// ngập nước -> spawn _footstepWaterSplashPrefab (bong bóng/splash nhỏ)
/// thay vì _footstepDustPrefab (bụi đất). Nếu chưa gán _waterLayerMask hoặc
/// chưa gán prefab splash, hành vi tự động fallback về y như cũ (chỉ bụi đất,
/// hoặc im lặng bỏ qua nếu đang dưới nước mà chưa có splash prefab).
/// </summary>
public sealed class PlayerFootstepFX : MonoBehaviour
{
    [Header("Foot Anchors")]
    [SerializeField] private Transform _leftFoot;
    [SerializeField] private Transform _rightFoot;

    [Header("Ground Check")]
    [SerializeField] private LayerMask _groundMask;
    [SerializeField] private float _raycastHeight = 0.7f;   // tăng từ 0.3 -> đủ cao để bắt được chân ở đỉnh bước
    [SerializeField] private float _raycastDistance = 1f;   // tăng theo để vẫn phủ tới mặt đất

    [Header("VFX - Trên cạn")]
    [SerializeField] private ParticleSystem _footstepDustPrefab;

    [Header("VFX - Dưới nước")]
    [Tooltip("Prefab bong bóng/splash nhỏ khi chân đang ngập nước. Để trống " +
             "nếu chưa làm — lúc đó chân ngập nước sẽ tự động KHÔNG spawn gì " +
             "cả (thay vì spawn nhầm bụi đất dưới nước).")]
    [SerializeField] private ParticleSystem _footstepWaterSplashPrefab;

    [Header("Water Check")]
    [Tooltip("Layer của mặt nước (water plane/mesh) — dùng để phát hiện chân " +
             "đang ngập nước. Để trống (None) nếu game chưa có nước, lúc đó " +
             "toàn bộ water check bị bỏ qua, hành vi y hệt bản cũ.")]
    [SerializeField] private LayerMask _waterLayerMask;
    [Tooltip("Raycast từ chân LÊN TRÊN 1 khoảng bằng giá trị này để tìm mặt " +
             "nước phía trên — nếu mực nước nằm trong khoảng này so với chân " +
             "thì coi là ngập. Tăng lên nếu nhân vật cao/nước sâu, nhưng đừng " +
             "để quá lớn vì sẽ raycast xuyên qua cả nước ở xa/khác khu vực.")]
    [SerializeField] private float _waterCheckHeight = 2f;

    [Header("Movement Filter")]
    // Lọc bleed-through animation event từ Blend Tree khi đứng yên
    // (Walk clip vẫn được sample ở weight nhỏ và event vẫn bắn dù không di chuyển).
    // Cần expose CurrentSpeed (hoặc velocity magnitude) từ NetPlayerMovementFusion.
    [SerializeField] private float _minSpeedToSpawnDust = 0.1f;

    [Header("Optional Reference")]
    // Nếu movement script nằm ở object cha, gán để check Grounded/tránh bụi khi nhảy/rơi tự do
    [SerializeField] private NetPlayerMovementFusion _movement;

    // Gọi từ Animation Event tại frame chân TRÁI đạp đất
    public void OnFootstepLeft()
    {
        Debug.Log("[Footstep] LEFT event fired");
        SpawnDust(_leftFoot);
    }

    // Gọi từ Animation Event tại frame chân PHẢI đạp đất
    public void OnFootstepRight()
    {
        Debug.Log("[Footstep] RIGHT event fired");
        SpawnDust(_rightFoot);
    }

    void SpawnDust(Transform foot)
    {
        if (foot == null)
        {
            Debug.Log("[Footstep] Blocked - foot Transform is NULL (chưa gán Left/Right Foot)");
            return;
        }

        if (_movement != null)
        {
            if (!_movement.Grounded)
            {
                Debug.Log("[Footstep] Blocked - _movement.Grounded = false");
                return;
            }

            // Chặn hiệu ứng sinh ra khi đứng yên do animation event
            // bị bleed-through từ Blend Tree (Walk clip vẫn được sample
            // ở weight nhỏ ngay cả khi Speed param gần 0).
            if (_movement.CurrentSpeed < _minSpeedToSpawnDust)
            {
                Debug.Log($"[Footstep] Blocked - CurrentSpeed {_movement.CurrentSpeed:F3} < {_minSpeedToSpawnDust} (đứng yên / blend tree bleed)");
                return;
            }
        }

        bool underwater = IsFootUnderwater(foot);

        // Chọn prefab tương ứng theo trạng thái ngập nước hay không.
        ParticleSystem prefabToUse = underwater ? _footstepWaterSplashPrefab : _footstepDustPrefab;

        if (prefabToUse == null)
        {
            Debug.Log(underwater
                ? "[Footstep] Blocked - chân đang ngập nước nhưng chưa gán _footstepWaterSplashPrefab"
                : "[Footstep] Blocked - _footstepDustPrefab is NULL (chưa gán VFX prefab)");
            return;
        }

        Vector3 origin = foot.position + Vector3.up * _raycastHeight;

        if (!Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                _raycastDistance,
                _groundMask,
                QueryTriggerInteraction.Ignore))
        {
            Debug.Log($"[Footstep] Raycast MISS từ {origin}, distance={_raycastDistance}, mask={_groundMask.value} - không trúng Ground layer");
            Debug.DrawRay(origin, Vector3.down * _raycastDistance, Color.red, 2f); // đỏ = trượt
            return; // không đứng trên layer Ground -> không spawn bụi
        }

        Debug.Log($"[Footstep] Raycast HIT: {hit.collider.name}, layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}, underwater={underwater}, point={hit.point}");
        Debug.DrawRay(origin, Vector3.down * hit.distance, underwater ? Color.cyan : Color.green, 2f); // xanh dương = dưới nước, xanh lá = trên cạn

        Quaternion rot = Quaternion.LookRotation(
            Vector3.ProjectOnPlane(foot.forward, hit.normal).normalized,
            hit.normal
        );

        // Lấy instance từ pool dùng chung cho toàn scene thay vì Instantiate mới
        // mỗi lần đạp chân — pool tự trả instance về hàng chờ khi particle
        // phát xong (xem FootstepDustPool.ReturnWhenFinished). Pool tự tạo
        // queue riêng theo từng prefab (dust vs splash) nên không cần sửa gì
        // bên FootstepDustPool.
        var fx = FootstepDustPool.Instance.Get(prefabToUse, hit.point, rot);
        Debug.Log($"[Footstep] Spawned {(underwater ? "splash" : "dust")}: {(fx != null ? "OK" : "NULL")}");
    }

    /// Trả về true nếu tại vị trí chân đang có mặt nước phủ lên trên (tức
    /// chân đang ngập nước) — raycast từ chân lên trên tìm layer nước, rồi
    /// so sánh Y của điểm chạm với Y của chân.
    bool IsFootUnderwater(Transform foot)
    {
        if (_waterLayerMask == 0)
            return false; // chưa gán layer nước -> bỏ qua check, giữ hành vi cũ (luôn bụi đất)

        Vector3 origin = foot.position + Vector3.up * _waterCheckHeight;

        if (Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit waterHit,
                _waterCheckHeight * 2f,
                _waterLayerMask,
                QueryTriggerInteraction.Collide))
        {
            // Mặt nước cao hơn vị trí chân -> chân đang ở dưới mặt nước.
            return waterHit.point.y > foot.position.y;
        }

        return false;
    }
}