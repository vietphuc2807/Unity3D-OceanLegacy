using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gắn lên I-Fish (child của I-Needle).
/// Làm con cá lắc lư + quay đầu theo hướng di chuyển của needle.
/// Không cần animation clip, chạy hoàn toàn bằng code.
/// </summary>
public class FishNeedleAnimator : MonoBehaviour
{
    [Header("Flip — quay đầu theo hướng bơi")]
    [Tooltip("Bật để cá tự lật trái/phải theo hướng needle đang chạy.")]
    [SerializeField] private bool _flipWithDirection = true;

    [Header("Body Wiggle — thân cá lắc")]
    [Tooltip("Góc lắc tối đa (độ) của thân cá.")]
    [SerializeField] private float _wiggleAngle  = 12f;
    [Tooltip("Tốc độ lắc — cao hơn = vẫy nhanh hơn.")]
    [SerializeField] private float _wiggleSpeed  = 8f;

    [Header("Bob — nhấp nhô lên xuống nhẹ")]
    [Tooltip("Biên độ nhấp nhô theo trục Y (pixel).")]
    [SerializeField] private float _bobAmount = 3f;
    [Tooltip("Tốc độ nhấp nhô.")]
    [SerializeField] private float _bobSpeed  = 5f;

    [Header("Squash & Stretch — bóp/kéo dài khi đổi hướng")]
    [Tooltip("Bật hiệu ứng bóp/kéo nhẹ khi needle đảo chiều.")]
    [SerializeField] private bool  _squashStretch = true;
    [Tooltip("Mức độ kéo dài tối đa (1 = không đổi, 1.2 = kéo 20%).")]
    [SerializeField] private float _stretchAmount = 1.15f;
    [Tooltip("Tốc độ phục hồi về scale bình thường.")]
    [SerializeField] private float _stretchRecovery = 8f;

    RectTransform _rt;
    RectTransform _needleRt;

    float _prevNeedleX;
    int   _currentDir    = 1;
    float _wiggleTime;
    float _bobTime;
    float _targetScaleX  = 1f;
    float _currentScaleX = 1f;
    float _baseY;   // Pos Y đặt trong Inspector — bob cộng thêm vào đây

    void Awake()
    {
        _rt       = GetComponent<RectTransform>();
        _needleRt = transform.parent as RectTransform;
        // Lưu Pos Y gốc từ Inspector, không bao giờ ghi đè nó
        _baseY = _rt.anchoredPosition.y;
    }

    void OnEnable()
    {
        if (_needleRt != null)
            _prevNeedleX = _needleRt.anchoredPosition.x;

        _wiggleTime    = Random.Range(0f, 100f);
        _bobTime       = Random.Range(0f, 100f);
        _currentScaleX = 1f;
        _targetScaleX  = 1f;
    }

    void Update()
    {
        if (_needleRt == null) return;

        float needleX = _needleRt.anchoredPosition.x;
        float delta   = needleX - _prevNeedleX;

        // ── 1. Flip theo hướng ──────────────────────────────────────────
        if (_flipWithDirection && Mathf.Abs(delta) > 0.01f)
        {
            int newDir = delta > 0 ? 1 : -1;

            if (newDir != _currentDir)
            {
                _currentDir = newDir;

                // Squash khi đổi chiều
                if (_squashStretch)
                    _targetScaleX = 1f / _stretchAmount; // bóp ngang lại xíu
            }
        }

        // Mặt cá nhìn phải mặc định — flip X âm = nhìn trái
        float flipX = _currentDir > 0 ? 1f : -1f;

        // ── 2. Squash & Stretch ─────────────────────────────────────────
        if (_squashStretch)
        {
            // Kéo dài khi đang chạy nhanh
            float speed = Mathf.Abs(delta) / Time.deltaTime;
            if (Mathf.Abs(delta) > 0.5f)
                _targetScaleX = Mathf.Lerp(1f, _stretchAmount, Mathf.Clamp01(speed / 600f));

            _currentScaleX = Mathf.Lerp(_currentScaleX, _targetScaleX, Time.deltaTime * _stretchRecovery);
            // Phục hồi target về 1 mỗi frame
            _targetScaleX  = Mathf.Lerp(_targetScaleX, 1f, Time.deltaTime * _stretchRecovery);
        }

        // ── 3. Wiggle (xoay thân) ───────────────────────────────────────
        _wiggleTime += Time.deltaTime * _wiggleSpeed;
        float wiggle = Mathf.Sin(_wiggleTime) * _wiggleAngle;

        // ── 4. Bob (nhấp nhô Y) ─────────────────────────────────────────
        _bobTime += Time.deltaTime * _bobSpeed;
        float bobY = Mathf.Sin(_bobTime) * _bobAmount;

        // ── 5. Apply ────────────────────────────────────────────────────
        _rt.localRotation = Quaternion.Euler(0f, 0f, wiggle);
        _rt.localScale    = new Vector3(flipX * _currentScaleX, 1f, 1f);

        // Bob cộng thêm vào Pos Y gốc từ Inspector — không ghi đè
        Vector2 ap = _rt.anchoredPosition;
        ap.y = _baseY + bobY;
        _rt.anchoredPosition = ap;

        _prevNeedleX = needleX;
    }
}