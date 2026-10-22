using System.Collections;
using UnityEngine;
using GaMo.BlenderEditorTools;

public class PlayerHeldFish : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private Transform _fishHandSocket;

    [SerializeField]
    private IKConstraint _ikConstraint;

    [Header("Hand Offset")]

    [Tooltip(
        "Vị trí cộng thêm cho cá, TƯƠNG ĐỐI so với hướng player " +
        "hiện tại (không phải trục World tuyệt đối). " +
        "Dùng để chỉnh cá lên/xuống/trái/phải/trước/sau so với tay."
    )]
    [SerializeField]
    private Vector3 _handPositionOffset = Vector3.zero;

    [Tooltip(
        "Rotation X/Y/Z của cá, TƯƠNG ĐỐI so với rotation của " +
        "FollowRoot (player). Không phụ thuộc rotation của Socket."
    )]
    [SerializeField]
    private Vector3 _handRotationOffset = Vector3.zero;

    [Tooltip("Thời gian tay giơ lên / hạ xuống.")]
    [SerializeField]
    private float _ikTurnDuration = 0.25f;

    [Header("Fish Appearance")]

    [Tooltip(
        "Thời gian chờ sau khi tay giơ xong trước khi cá xuất hiện."
    )]
    [SerializeField]
    private float _fishAppearDelay = 0f;

    [Header("Fish Follow")]

    [Tooltip(
        "Root mà cá đi theo khi player di chuyển. " +
        "Thường là GameplayPlayer."
    )]
    [SerializeField]
    private Transform _fishFollowRoot;

    // ============================================================
    // Runtime
    // ============================================================

    private Transform _pivotTracker;

    private GameObject _heldFishRoot;

    private Transform _fishObject;

    private Coroutine _equipCoroutine;

    // Vị trí GỐC của cá (vị trí tay lúc equip xong), dưới dạng LOCAL
    // POSITION so với FollowRoot — KHÔNG bao gồm bất kỳ offset nào
    // (offset được cộng thêm mỗi frame trong ApplyHandOffset, xem
    // bên dưới).
    //
    // Vì cá là child của FollowRoot, khi player xoay hướng, cá sẽ
    // TỰ ĐỘNG "vẽ cung" theo đúng chuyển động thật của thân/tay
    // (giống hệt như đang thật sự cầm vật trên tay) — đây là hành vi
    // MONG MUỐN theo yêu cầu, KHÔNG phải bug.
    private Vector3 _fishBaseLocalPosition;

    // Scale WORLD của cá.
    private Vector3 _fishWorldScale;

    // FishData của con cá đang cầm — lưu lại để mỗi frame trong
    // ApplyHandOffset cũng áp được đúng offset RIÊNG của con cá này
    // (fishData.HandPositionOffset / HandRotationOffset), không chỉ
    // lúc equip. Vì đọc trực tiếp field này mỗi frame, sửa giá trị
    // trên FishData lúc đang Play sẽ có hiệu lực ngay lập tức.
    private FishData _currentFishData;

    // ============================================================
    // AWAKE
    // ============================================================

    private void Awake()
    {
        if (_fishFollowRoot == null)
        {
            _fishFollowRoot = transform;
        }
    }

    // ============================================================
    // EQUIP
    // ============================================================

    public void EquipFish(
        FishData fishData,
        float sizeMultiplier)
    {
        // --------------------------------------------------------
        // Hủy coroutine equip cũ
        // --------------------------------------------------------

        if (_equipCoroutine != null)
        {
            StopCoroutine(_equipCoroutine);
            _equipCoroutine = null;
        }

        // --------------------------------------------------------
        // Xóa cá cũ
        // --------------------------------------------------------

        ClearHeldFishImmediate();

        // --------------------------------------------------------
        // Validate
        // --------------------------------------------------------

        if (fishData == null)
        {
            Debug.LogWarning(
                "[PlayerHeldFish] FishData is NULL."
            );

            return;
        }

        if (fishData.WorldPrefab == null)
        {
            Debug.LogWarning(
                $"[PlayerHeldFish] '{fishData.name}' " +
                "không có WorldPrefab."
            );

            return;
        }

        if (_fishHandSocket == null)
        {
            Debug.LogWarning(
                "[PlayerHeldFish] Fish Hand Socket chưa được gán."
            );

            return;
        }

        // --------------------------------------------------------
        // Start routine
        // --------------------------------------------------------

        _equipCoroutine = StartCoroutine(
            EquipFishRoutine(
                fishData,
                sizeMultiplier
            )
        );
    }

    // ============================================================
    // EQUIP ROUTINE
    // ============================================================

    private IEnumerator EquipFishRoutine(
        FishData fishData,
        float sizeMultiplier)
    {
        _currentFishData = fishData;

        // ========================================================
        // 1. GIƠ TAY TRƯỚC
        // ========================================================

        if (_ikConstraint != null)
        {
            _ikConstraint.TurnOn(
                _ikTurnDuration
            );
        }

        // Chờ IK hoàn thành
        if (_ikTurnDuration > 0f)
        {
            yield return new WaitForSeconds(
                _ikTurnDuration
            );
        }

        // --------------------------------------------------------
        // Kiểm tra
        // --------------------------------------------------------

        if (_fishHandSocket == null)
        {
            _equipCoroutine = null;
            yield break;
        }

        // ========================================================
        // 2. DELAY
        // ========================================================

        if (_fishAppearDelay > 0f)
        {
            yield return new WaitForSeconds(
                _fishAppearDelay
            );
        }

        // ========================================================
        // 3. LẤY VỊ TRÍ CUỐI CÙNG CỦA TAY
        // ========================================================

        // Dùng vị trí THẬT SỰ của bone tay tại thời điểm này — tức
        // SAU KHI IK đã giơ tay lên xong. Đây là vị trí đúng để cá
        // nằm trên lòng bàn tay.
        //
        // (Không cache/đóng băng vị trí này, vì tư thế tay sau IK là
        // kết quả runtime của IKConstraint — cache lại 1 lần ở Awake
        // sẽ lấy nhầm tư thế tay lúc NGHỈ, trước khi IK giơ lên,
        // khiến cá bị spawn sai chỗ, không nằm trên tay nữa.)

        Vector3 finalHandPosition =
            _fishHandSocket.position;

        // ========================================================
        // 4. TẠO PIVOT
        // ========================================================

        _pivotTracker =
            new GameObject(
                "HeldFishPivot"
            ).transform;

        // Pivot KHÔNG làm child của bone tay.
        //
        // Nó chỉ nằm độc lập trong World Space.
        _pivotTracker.SetParent(null);

        // Pivot chỉ lấy POSITION của tay.
        _pivotTracker.position =
            finalHandPosition;

        // Rotation pivot luôn Identity.
        _pivotTracker.rotation =
            Quaternion.identity;

        // Scale pivot luôn 1.
        _pivotTracker.localScale =
            Vector3.one;

        // ========================================================
        // 5. SPAWN FISH
        // ========================================================

        GameObject fishObj =
            Instantiate(
                fishData.WorldPrefab
            );

        _fishObject =
            fishObj.transform;

        // Lưu lại FishData để dùng ở các bước sau (rotation/position
        // offset riêng của con cá này) và trong ApplyHandOffset mỗi
        // frame.
        _currentFishData =
            fishData;

        // ========================================================
        // 6. SCALE
        // ========================================================

        // Nhân ĐỦ CẢ 3 TRỤC của BaseScale với sizeMultiplier — giống
        // hệt cách FishingLine.AttachCaughtFish() đang làm — để giữ
        // đúng tỉ lệ hình dạng gốc của model (VD mai rùa dẹt: X to,
        // Y/Z nhỏ). Trước đây chỉ lấy BaseScale.x rồi áp đều cho cả
        // 3 trục, khiến những con có BaseScale KHÔNG đều (như rùa)
        // bị "thổi phồng" sai tỉ lệ trên trục Y/Z khi cầm trên tay,
        // dù vẫn đúng khi hiển thị treo trên lưỡi câu.

        _fishWorldScale =
            fishData.BaseScale *
            sizeMultiplier;

        _fishObject.localScale =
            _fishWorldScale;

        // ========================================================
        // 7. POSITION (chỉ đặt vị trí GỐC tại tay)
        // ========================================================

        // Đặt cá ngay tại vị trí tay.
        //
        // KHÔNG sử dụng FishHoldPoint để tự động cộng delta nữa.
        //
        // LƯU Ý: Offset (chung + riêng con cá) KHÔNG còn được cộng
        // vào đây nữa — chúng được tính lại mỗi frame trong
        // ApplyHandOffset() để có thể chỉnh sống (live-tune) trong
        // lúc Play. Ở đây chỉ set đúng vị trí GỐC (chưa offset).
        //

        _fishObject.position =
            finalHandPosition;

        // ========================================================
        // 8. ROTATION (chỉ đặt rotation GỐC theo FollowRoot)
        // ========================================================

        // Rotation cuối cùng (bao gồm cả offset chung + offset riêng
        // con cá) được tính lại mỗi frame trong ApplyHandOffset(), y
        // hệt cách position được xử lý ở bước 7. Ở đây chỉ cần set
        // tạm rotation GỐC để tránh cá bị "giật" 1 frame trước khi
        // ApplyHandOffset() chạy lần đầu (LateUpdate).
        //
        // Không lấy rotation của Socket.
        // Không lấy rotation của Pivot.
        //

        Transform followRootForRotation =
            _fishFollowRoot != null
                ? _fishFollowRoot
                : transform;

        _fishObject.rotation =
            followRootForRotation.rotation;

        // ========================================================
        // 9. FOLLOW PLAYER
        // ========================================================

        // Cho fish làm child của FollowRoot. true = giữ nguyên WORLD
        // position / rotation lúc parent (không bị "nhảy" tại thời
        // điểm parent). localPosition tính ra sau bước này sẽ được
        // lưu lại ở bước 10 để dùng cho mọi frame sau.
        //

        Transform followRoot =
            _fishFollowRoot != null
                ? _fishFollowRoot
                : transform;

        _fishObject.SetParent(
            followRoot,
            true
        );

        // ========================================================
        // 10. LƯU LOCAL POSITION GỐC SO VỚI FOLLOW ROOT
        // ========================================================

        // Vì cá đã là child của followRoot (bước 9), localPosition
        // hiện tại chính là vị trí GỐC (chưa offset) tương đối so
        // với thân player ngay tại thời điểm spawn. Lưu lại giá trị
        // này — mỗi frame ApplyHandOffset() sẽ cộng thêm offset hiện
        // tại (đọc trực tiếp field/FishData) vào giá trị GỐC này.
        //
        // Nhờ localPosition (gốc + offset) luôn được Unity tự nhân
        // với followRoot.rotation khi tính world position, cá vẫn
        // TỰ ĐỘNG "vẽ cung" theo đúng chuyển động thật của thân/tay
        // khi player xoay hướng — hành vi này không đổi so với trước.

        _fishBaseLocalPosition =
            _fishObject.localPosition;

        // ========================================================
        // 11. TẮT SCRIPT CỦA FISH
        // ========================================================

        MonoBehaviour[] behaviours =
            fishObj.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour != null)
            {
                behaviour.enabled = false;
            }
        }

        // ========================================================
        // 12. TẮT ANIMATOR
        // ========================================================

        Animator[] animators =
            fishObj.GetComponentsInChildren<Animator>(true);

        foreach (Animator animator in animators)
        {
            if (animator != null)
            {
                animator.enabled = false;
            }
        }

        // ========================================================
        // 13. LƯU ROOT
        // ========================================================

        _heldFishRoot =
            fishObj;

        // ========================================================
        // 14. XÓA PIVOT
        // ========================================================

        // Không cần pivot nữa.
        //
        // Cá trực tiếp follow GameplayPlayer.
        //

        if (_pivotTracker != null)
        {
            Destroy(_pivotTracker.gameObject);
            _pivotTracker = null;
        }

        // Áp offset ngay lần đầu, tránh chờ tới LateUpdate kế tiếp
        // mới thấy đúng vị trí/rotation có offset.
        ApplyHandOffset();

        _equipCoroutine = null;
    }

    // ============================================================
    // APPLY OFFSET
    // ============================================================

    private void ApplyHandOffset()
    {
        if (_fishObject == null)
        {
            return;
        }

        Transform followRoot =
            _fishFollowRoot != null
                ? _fishFollowRoot
                : transform;

        // --------------------------------------------------------
        // POSITION
        // --------------------------------------------------------

        // Vị trí GỐC (chưa offset) + offset CHUNG (_handPositionOffset)
        // + offset RIÊNG của con cá đang cầm (đọc trực tiếp từ
        // _currentFishData mỗi frame, KHÔNG cache) — tất cả đều là
        // local-space so với FollowRoot nên chỉ cần cộng thẳng vào
        // localPosition (không cần nhân rotation ở đây, vì Unity tự
        // nhân followRoot.rotation khi tính world position cho object
        // con).
        //
        // Nhờ đọc trực tiếp field/FishData mỗi frame, sửa offset lúc
        // đang Play sẽ có hiệu lực NGAY LẬP TỨC, không cần equip lại.

        Vector3 perFishPositionOffset =
            _currentFishData != null
                ? _currentFishData.HandPositionOffset
                : Vector3.zero;

        _fishObject.localPosition =
            _fishBaseLocalPosition +
            _handPositionOffset +
            perFishPositionOffset;

        // --------------------------------------------------------
        // ROTATION
        // --------------------------------------------------------

        // Tính lại rotation MỖI FRAME dựa trên rotation hiện tại
        // của FollowRoot, thay vì dùng 1 giá trị World cache cố định.
        // Nhờ vậy nếu player xoay hướng (kể cả khi đang cầm cá, không
        // chỉ lúc re-equip), cá vẫn xoay theo đúng hướng player.
        //
        // Nhân thêm offset RIÊNG của con cá đang cầm (đọc trực tiếp
        // từ _currentFishData mỗi frame).

        Quaternion perFishRotationOffset =
            _currentFishData != null
                ? Quaternion.Euler(
                    _currentFishData.HandRotationOffset
                  )
                : Quaternion.identity;

        _fishObject.rotation =
            followRoot.rotation *
            Quaternion.Euler(_handRotationOffset) *
            perFishRotationOffset;

        // --------------------------------------------------------
        // SCALE
        // --------------------------------------------------------

        _fishObject.localScale =
            _fishWorldScale;
    }

    // ============================================================
    // CLEAR
    // ============================================================

    public void ClearHeldFish()
    {
        // Hủy coroutine
        if (_equipCoroutine != null)
        {
            StopCoroutine(
                _equipCoroutine
            );

            _equipCoroutine = null;
        }

        // Xóa cá
        ClearHeldFishImmediate();

        // Hạ tay
        if (_ikConstraint != null)
        {
            _ikConstraint.TurnOff(
                _ikTurnDuration
            );
        }
    }

    // ============================================================
    // CLEAR IMMEDIATE
    // ============================================================

    private void ClearHeldFishImmediate()
    {
        // Xóa fish
        if (_heldFishRoot != null)
        {
            Destroy(
                _heldFishRoot
            );

            _heldFishRoot = null;
        }

        // Nếu fishObject vẫn tồn tại
        if (_fishObject != null)
        {
            Destroy(
                _fishObject.gameObject
            );

            _fishObject = null;
        }

        // Xóa pivot nếu còn
        if (_pivotTracker != null)
        {
            Destroy(
                _pivotTracker.gameObject
            );

            _pivotTracker = null;
        }

        _currentFishData = null;

        _fishBaseLocalPosition = Vector3.zero;

        _fishWorldScale =
            Vector3.one;
    }

    // ============================================================
    // LATE UPDATE
    // ============================================================

    private void LateUpdate()
    {
        if (_fishObject == null)
        {
            return;
        }

        // ========================================================
        // QUAN TRỌNG
        // ========================================================
        //
        // Không lấy vị trí của Socket-HeldFish nữa.
        //
        // Vì vậy animation của tay:
        //
        // lên
        // xuống
        // trái
        // phải
        //
        // sẽ KHÔNG làm cá nhấp nhô.
        //
        // Position VÀ Rotation đều được tính lại theo FollowRoot +
        // offset (chung + riêng từng con cá, đọc trực tiếp mỗi frame)
        // trong ApplyHandOffset() nên:
        //
        // - Khi player xoay hướng, cá xoay theo đúng, không bị đứng
        //   yên 1 hướng World cố định.
        // - Khi chỉnh Hand Position/Rotation Offset (trên component
        //   này hoặc trên FishData) lúc đang Play, cá cập nhật ngay
        //   lập tức, không cần equip lại.
        //
        // ========================================================

        ApplyHandOffset();
    }

    public void EquipFishImmediate(
    FishData fishData,
    float sizeMultiplier)
    {
        if (_equipCoroutine != null)
        {
            StopCoroutine(_equipCoroutine);
            _equipCoroutine = null;
        }

        ClearHeldFishImmediate();

        if (fishData == null)
        {
            return;
        }

        if (fishData.WorldPrefab == null)
        {
            Debug.LogWarning(
                $"[PlayerHeldFish] '{fishData.name}' không có WorldPrefab."
            );

            return;
        }

        if (_fishHandSocket == null)
        {
            Debug.LogWarning(
                "[PlayerHeldFish] Fish Hand Socket chưa được gán."
            );

            return;
        }

        SpawnFishImmediately(
            fishData,
            sizeMultiplier
        );
    }

    private void SpawnFishImmediately(
    FishData fishData,
    float sizeMultiplier)
    {
        _currentFishData = fishData;

        Vector3 handPosition =
            _fishHandSocket.position;

        GameObject fishObj =
            Instantiate(
                fishData.WorldPrefab
            );

        _fishObject =
            fishObj.transform;

        _currentFishData =
            fishData;

        _fishWorldScale =
            fishData.BaseScale *
            sizeMultiplier;

        _fishObject.localScale =
            _fishWorldScale;

        _fishObject.position =
            handPosition;

        Transform followRoot =
            _fishFollowRoot != null
                ? _fishFollowRoot
                : transform;

        _fishObject.rotation =
            followRoot.rotation;

        _fishObject.SetParent(
            followRoot,
            true
        );

        _fishBaseLocalPosition =
            _fishObject.localPosition;

        MonoBehaviour[] behaviours =
            fishObj.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour != null)
            {
                behaviour.enabled = false;
            }
        }

        Animator[] animators =
            fishObj.GetComponentsInChildren<Animator>(true);

        foreach (Animator animator in animators)
        {
            if (animator != null)
            {
                animator.enabled = false;
            }
        }

        _heldFishRoot =
            fishObj;

        ApplyHandOffset();
    }
}