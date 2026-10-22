using UnityEngine;
using Fusion;

/// <summary>
/// Texture vẽ được trên poster, đồng bộ nét vẽ cho MỌI người chơi qua RPC.
/// Mỗi client tự vẽ y hệt lên texture local của mình theo cùng thuật toán
/// (không truyền cả ảnh, chỉ truyền toạ độ UV + màu + kích cỡ cọ mỗi đoạn
/// nét, giữ băng thông rất nhỏ).
///
/// TIẾNG VẼ (3D): một AudioSource loop trên poster, bật khi có nét vẽ đang
/// được kéo (PaintLine với quãng di chuyển đủ lớn) và tắt ngay khi dừng.
/// Vì PaintLine chạy trên MỌI client cho mọi nét (kể cả nét của người khác
/// qua RPC) nên người đứng gần poster cũng nghe thấy, không cần thêm RPC.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class PosterCanvasPainter : NetworkBehaviour
{
    [Header("Canvas Settings")]
    [SerializeField] private int _textureSize = 512;
    [SerializeField] private Color _backgroundColor = Color.white;
    [Tooltip("Collider bám sát mặt tờ giấy, dùng để raycast chuột ra UV lúc vẽ.")]
    [SerializeField] private Collider _drawCollider;

    [Header("Brush")]
    [Tooltip("Màu cọ mặc định khi bắt đầu vẽ (mặc định: đen).")]
    [SerializeField] private Color _defaultColor = Color.black;
    [SerializeField] private float _brushSizePixels = 6f;

    [Header("Draw Sound (3D)")]
    [Tooltip("File tiếng vẽ (bút chì / bút lông, loop mượt, 1-3 giây). " +
             "Để trống = không có tiếng.")]
    [SerializeField] private AudioClip _drawLoopClip;

    [Range(0f, 1f)]
    [SerializeField] private float _soundVolume = 0.6f;

    [Tooltip("BẬT: âm lượng nhân thêm với thanh SFX trong Settings.")]
    [SerializeField] private bool _followSfxVolume = true;

    [Tooltip("Trong khoảng này tiếng to tối đa.")]
    [SerializeField] private float _soundMinDistance = 1f;

    [Tooltip("Ngoài khoảng này không nghe thấy gì.")]
    [SerializeField] private float _soundMaxDistance = 10f;

    [Tooltip("Sau nét vẽ cuối cùng, giữ tiếng thêm chừng này giây rồi mới tắt " +
             "(để tiếng liền mạch giữa các đoạn nét nhỏ).")]
    [SerializeField] private float _stopDelaySeconds = 0.1f;

    [Tooltip("Thời gian fade lên/xuống (giây). Nhỏ = bật/tắt nhanh, dứt khoát.")]
    [SerializeField] private float _soundFadeSeconds = 0.08f;

    [Tooltip("Nét phải di chuyển ít nhất chừng này pixel (trên texture) mới " +
             "tính là đang vẽ. Giữ chuột đứng yên sẽ KHÔNG có tiếng.")]
    [SerializeField] private float _minMovePixels = 0.5f;

    // Cờ đồng bộ: TRUE khi có 1 người bất kỳ đang trong chế độ vẽ trên
    // poster này. Các client khác dựa vào đây để tự ẩn prompt + chặn phím
    // E, tránh 2 người cùng mở chế độ vẽ 1 lúc.
    [Networked] public NetworkBool IsBeingUsed { get; private set; }
    public bool IsAvailable => !IsBeingUsed;

    private Renderer _renderer;
    private Texture2D _texture;
    private Color _currentColor;

    private Camera _drawCamera;
    private bool _isDrawSessionActive;
    private bool _isPainting;
    private Vector2? _lastUV;

    // Tiếng vẽ
    private AudioSource _drawSource;
    private float _lastStrokeTime = -999f;
    private float _soundFactor;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _currentColor = _defaultColor;
    }

    public override void Spawned()
    {
        InitTexture();

        SetupDrawSound();

        // GHI CHÚ (mở rộng sau): người vào sau chỉ thấy texture trắng vì
        // texture này chỉ tồn tại trong RAM từng client, KHÔNG tự đồng bộ
        // lúc join. Muốn người vào sau thấy hình cũ, cần thêm 1 bước:
        // State Authority giữ bản texture hiện tại, khi phát hiện player
        // mới join thì nén thành PNG (_texture.EncodeToPNG()), cắt thành
        // nhiều RPC nhỏ (Fusion giới hạn kích thước 1 RPC), gửi tuần tự
        // cho riêng client đó rồi ráp lại + Texture2D.LoadImage(). Đây là
        // phần cần làm thêm nếu bạn muốn "vẽ 1 lần, ai vào sau cũng thấy
        // full lịch sử", còn hiện tại chỉ đồng bộ LIVE giữa những người
        // đang ở trong phòng cùng lúc.
    }

    private void InitTexture()
    {
        _texture = new Texture2D(_textureSize, _textureSize, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp
        };

        FillBackground();

        // material instance riêng để không ảnh hưởng poster khác dùng
        // chung 1 material gốc.
        var mat = _renderer.material;

        // QUAN TRỌNG: shader "Universal Render Pipeline/Lit" (URP) KHÔNG
        // dùng property "_MainTex" như Built-in Render Pipeline, mà dùng
        // "_BaseMap". Gán qua "mainTexture" (dòng cũ) sẽ set nhầm property,
        // khiến texture tồn tại trong RAM nhưng KHÔNG hiển thị ra ngoài mà
        // không có lỗi/warning gì cả. Set cả 2 để an toàn dù đổi shader sau này.
        if (mat.HasProperty("_BaseMap"))
            mat.SetTexture("_BaseMap", _texture);

        if (mat.HasProperty("_MainTex"))
            mat.mainTexture = _texture;
    }

    /// <summary>Tô toàn bộ texture về màu nền (_backgroundColor), dùng chung cho Init và Clear.</summary>
    private void FillBackground()
    {
        var fill = new Color[_textureSize * _textureSize];
        for (int i = 0; i < fill.Length; i++)
            fill[i] = _backgroundColor;

        _texture.SetPixels(fill);
        _texture.Apply();
    }

    // ============================================================
    // DRAW SOUND
    // ============================================================

    private void SetupDrawSound()
    {
        if (_drawLoopClip == null)
            return;

        _drawSource = gameObject.AddComponent<AudioSource>();
        _drawSource.clip = _drawLoopClip;
        _drawSource.loop = true;
        _drawSource.playOnAwake = false;
        _drawSource.volume = 0f;

        // 3D: càng xa poster càng nhỏ, quá _soundMaxDistance thì im hẳn.
        _drawSource.spatialBlend = 1f;
        _drawSource.rolloffMode = AudioRolloffMode.Linear;
        _drawSource.minDistance = _soundMinDistance;
        _drawSource.maxDistance = _soundMaxDistance;
        _drawSource.dopplerLevel = 0f;
    }

    /// <summary>
    /// Bật tiếng khi có nét vẽ đang kéo (PaintLine vừa chạy trong
    /// _stopDelaySeconds gần đây), tắt khi dừng. Chạy trên MỌI client.
    /// </summary>
    private void UpdateDrawSound()
    {
        if (_drawSource == null)
            return;

        bool drawing = Time.time - _lastStrokeTime <= _stopDelaySeconds;

        float target = drawing ? 1f : 0f;

        float step =
            _soundFadeSeconds <= 0f
                ? 1f
                : Time.unscaledDeltaTime / _soundFadeSeconds;

        _soundFactor = Mathf.MoveTowards(_soundFactor, target, step);

        float sfxFactor =
            _followSfxVolume && SoundManager.Instance != null
                ? SoundManager.Instance.GetSFXVolume()
                : 1f;

        _drawSource.volume = _soundFactor * _soundVolume * sfxFactor;

        if (_soundFactor > 0.001f)
        {
            if (!_drawSource.isPlaying)
            {
                // Bắt đầu từ chỗ ngẫu nhiên trong clip để mỗi nét nghe hơi
                // khác nhau, đỡ lặp.
                _drawSource.time = Random.Range(0f, _drawLoopClip.length * 0.9f);
                _drawSource.Play();
            }
        }
        else if (_drawSource.isPlaying)
        {
            _drawSource.Stop();
        }
    }

    // ============================================================
    // DRAW SESSION
    // ============================================================

    public void BeginDrawSession(Camera cam)
    {
        _drawCamera = cam;
        _isDrawSessionActive = true;
        _currentColor = _defaultColor; // luôn bắt đầu phiên vẽ mới bằng màu mặc định (đen)

        Debug.Log($"[PosterCanvasPainter] BeginDrawSession. cam={cam?.name}, currentColor={_currentColor}");
    }

    public void EndDrawSession()
    {
        _isDrawSessionActive = false;
        _isPainting = false;
        _lastUV = null;
    }

    public void SetColor(Color color)
    {
        _currentColor = color;
    }

    // ============================================================
    // BUSY FLAG (chặn 2 người cùng vẽ 1 lúc)
    // ============================================================

    /// <summary>Gọi khi 1 người BẮT ĐẦU vẽ (PosterDrawView.Open), báo cho mọi client khác biết.</summary>
    public void RequestBeginUse()
    {
        RPC_SetInUse(true);
    }

    /// <summary>Gọi khi người đó ĐÓNG chế độ vẽ (PosterDrawView.Close), trả lại quyền cho người khác.</summary>
    public void RequestEndUse()
    {
        RPC_SetInUse(false);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetInUse(NetworkBool value)
    {
        IsBeingUsed = value;
        Debug.Log($"[PosterCanvasPainter] IsBeingUsed set to {value}");
    }

    // ============================================================
    // CLEAR
    // ============================================================

    /// <summary>Gọi từ phím C. Xoá toàn bộ nét vẽ, đồng bộ cho mọi người.</summary>
    public void RequestClear()
    {
        _lastUV = null;

        // Xoá ngay tại chỗ cho người bấm, không đợi round-trip qua State
        // Authority mới thấy hiệu ứng (đỡ delay như draw stroke).
        FillBackground();

        RPC_ClearCanvas();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ClearCanvas()
    {
        RPC_ApplyClear();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyClear()
    {
        FillBackground();
    }

    private void Update()
    {
        // Tiếng vẽ chạy trên mọi client, kể cả khi mình không trong phiên
        // vẽ (để nghe người khác vẽ), nên đặt TRƯỚC đoạn return sớm bên dưới.
        UpdateDrawSound();

        if (!_isDrawSessionActive || _drawCamera == null)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            _isPainting = true;
            _lastUV = null;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            _isPainting = false;
            _lastUV = null;
        }

        if (_isPainting)
            TryPaintAtMouse();
    }

    private void TryPaintAtMouse()
    {
        if (_drawCollider == null)
        {
            // DEBUG: nếu chưa gán Draw Collider trong Inspector, raycast sẽ
            // luôn fail ngay từ đầu, không vẽ được gì và không có lỗi nào khác.
            Debug.LogWarning("[PosterCanvasPainter] _drawCollider is NULL, cannot raycast to paint.");
            return;
        }

        Ray ray = _drawCamera.ScreenPointToRay(Input.mousePosition);

        if (!_drawCollider.Raycast(ray, out RaycastHit hit, 100f))
        {
            // DEBUG: chuột đang không trỏ trúng collider của tờ giấy (góc
            // camera lệch, hoặc collider quá nhỏ/quá xa so với ViewPoint).
            Debug.Log("[PosterCanvasPainter] Raycast missed the draw collider.");
            return;
        }

        Vector2 uv = hit.textureCoord;
        Vector2 from = _lastUV ?? uv;

        // Vẽ ngay tại chỗ cho người đang thao tác — không đợi round-trip
        // qua State Authority nên luôn mượt bất kể ping. RPC vẫn gửi để
        // đồng bộ cho người khác; khi echo ngược về máy mình, PaintLine
        // chạy lại đúng y hệt segment này (cùng toạ độ/màu/size) nên vô
        // hại, chỉ tốn thêm 1 lần tính toán không đáng kể.
        PaintLine(from, uv, _currentColor, _brushSizePixels);

        RPC_DrawStroke(from, uv, _currentColor, _brushSizePixels);

        _lastUV = uv;
    }

    // Client đang vẽ gửi lên State Authority; State Authority relay lại cho
    // TẤT CẢ client (kể cả người gửi) để đảm bảo thứ tự vẽ giống nhau trên
    // mọi máy, tránh mỗi người thấy 1 kiểu do độ trễ mạng khác nhau.
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_DrawStroke(Vector2 uvFrom, Vector2 uvTo, Color color, float brushSizePixels)
    {
        RPC_ApplyStroke(uvFrom, uvTo, color, brushSizePixels);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyStroke(Vector2 uvFrom, Vector2 uvTo, Color color, float brushSizePixels)
    {
        PaintLine(uvFrom, uvTo, color, brushSizePixels);
    }

    private void PaintLine(Vector2 uvFrom, Vector2 uvTo, Color color, float brushSizePixels)
    {
        if (_texture == null)
        {
            Debug.LogWarning("[PosterCanvasPainter] _texture is NULL in PaintLine, InitTexture chưa chạy?");
            return;
        }

        Vector2 pFrom = new Vector2(uvFrom.x * _textureSize, uvFrom.y * _textureSize);
        Vector2 pTo = new Vector2(uvTo.x * _textureSize, uvTo.y * _textureSize);

        float dist = Vector2.Distance(pFrom, pTo);

        // Chỉ tính là "đang vẽ" khi nét thực sự di chuyển, giữ chuột đứng
        // yên (from == to) thì không có tiếng.
        if (dist >= _minMovePixels)
            _lastStrokeTime = Time.time;

        int steps = Mathf.Max(1, Mathf.CeilToInt(dist / Mathf.Max(1f, brushSizePixels * 0.5f)));

        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(pFrom, pTo, (float)i / steps);
            StampBrush(p, color, brushSizePixels);
        }

        _texture.Apply();
    }

    private void StampBrush(Vector2 center, Color color, float radiusPixels)
    {
        int r = Mathf.CeilToInt(radiusPixels);
        int cx = Mathf.RoundToInt(center.x);
        int cy = Mathf.RoundToInt(center.y);

        for (int y = -r; y <= r; y++)
        {
            for (int x = -r; x <= r; x++)
            {
                if (x * x + y * y > r * r) continue;

                int px = cx + x;
                int py = cy + y;

                if (px < 0 || px >= _textureSize || py < 0 || py >= _textureSize) continue;

                _texture.SetPixel(px, py, color);
            }
        }
    }
}