using System.Collections;
using UnityEngine;

/// <summary>
/// Gắn lên cùng GameObject với FishingLine (hoặc root nhân vật).
/// Lắng nghe OnFloatLanded để spawn VFX splash khi phao chạm nước.
/// Lắng nghe OnBiteStarted để spawn VFX gợn nước + bong bóng khi cá rỉa mồi.
/// Toàn bộ particle tạo bằng code, không cần prefab.
/// </summary>
[RequireComponent(typeof(PlayerHeldItem))]
public class FishingVFX : MonoBehaviour
{
    [Header("Splash Tuning")]
    [SerializeField] private int   _dropletCount    = 18;
    [SerializeField] private float _dropletSpeed    = 2.2f;
    [SerializeField] private float _dropletLifetime = 1.1f;
    [SerializeField] private float _dropletSize     = 0.06f;
    [SerializeField] private float _dropletGravity  = 0.75f;
    [SerializeField] [Range(0f, 0.25f)]
    private float _burstSpread = 0.09f;
    [SerializeField] private Color _waterColor = new Color(0.5f, 0.8f, 1f, 0.85f);

    [Header("Ring Tuning")]
    [SerializeField] private int   _ringCount     = 1;
    [SerializeField] private float _ringLifetime  = 2.2f;
    [SerializeField] private float _ringStartSize = 0.1f;
    [SerializeField] private float _ringEndSize   = 1.8f;
    [SerializeField] [Range(0.3f, 1f)]
    private float _ringGrowCurve = 0.5f;

    [Header("Bite VFX — Ripple")]
    [Tooltip("Giây giữa 2 vòng gợn nhỏ khi cá đang rỉa mồi.")]
    [SerializeField] private float _biteRingInterval  = 0.8f;
    [Tooltip("Kích thước ban đầu vòng gợn bite — nhỏ hơn splash nhiều.")]
    [SerializeField] private float _biteRingStartSize = 0.03f;
    [Tooltip("Kích thước cuối vòng gợn bite.")]
    [SerializeField] private float _biteRingEndSize   = 0.45f;
    [SerializeField] private float _biteRingLifetime  = 0.85f;
    [SerializeField] [Range(0.3f, 1f)]
    private float _biteRingGrowCurve = 0.6f;

    [Header("Bite VFX — Bubbles")]
    [Tooltip("Số bong bóng nhỏ mỗi đợt rỉa mồi.")]
    [SerializeField] private int   _biteBubbleCount    = 4;
    [Tooltip("Kích thước bong bóng — rất nhỏ cho tự nhiên.")]
    [SerializeField] private float _biteBubbleSize     = 0.018f;
    [SerializeField] private float _biteBubbleLifetime = 1.0f;
    [Tooltip("Tốc độ nổi lên — nhỏ để trông chậm rãi như bong bóng nước thật.")]
    [SerializeField] private float _biteBubbleRiseSpeed = 0.12f;
    [Tooltip("Bán kính tản ra xung quanh phao khi nổi lên.")]
    [SerializeField] private float _biteBubbleSpread   = 0.08f;

    PlayerHeldItem _heldItem;
    FishingLine    _currentLine;

    Coroutine _biteVFXCoroutine;

    // ─── Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        TryGetComponent(out _heldItem);
    }

    void OnEnable()
    {
        if (_heldItem != null)
            _heldItem.OnHeldItemChanged += HandleHeldItemChanged;
    }

    void OnDisable()
    {
        if (_heldItem != null)
            _heldItem.OnHeldItemChanged -= HandleHeldItemChanged;

        UnsubscribeLine();
    }

    void HandleHeldItemChanged(ItemData item)
    {
        UnsubscribeLine();

        bool isRod = item != null && item.IsFishingRod;
        _currentLine = isRod
            ? GetComponentInChildren<FishingLine>(true)
            : null;

        if (_currentLine != null)
        {
            _currentLine.OnFloatLanded += SpawnSplash;
            _currentLine.OnBiteStarted += StartBiteVFX;
        }
    }

    void UnsubscribeLine()
    {
        StopBiteVFX();

        if (_currentLine != null)
        {
            _currentLine.OnFloatLanded -= SpawnSplash;
            _currentLine.OnBiteStarted -= StartBiteVFX;
        }

        _currentLine = null;
    }

    // ─── Splash (khi phao chạm nước) ─────────────────────────────────────

    void SpawnSplash(Vector3 pos)
    {
        SpawnDroplets(pos);
        for (int i = 0; i < _ringCount; i++)
            SpawnRing(pos, _ringStartSize, _ringEndSize, _ringLifetime, _ringGrowCurve, 0.6f);
    }

    // ─── Bite VFX (khi cá đang rỉa mồi) ─────────────────────────────────

    void StartBiteVFX()
    {
        StopBiteVFX();
        _biteVFXCoroutine = StartCoroutine(BiteVFXLoop());
    }

    void StopBiteVFX()
    {
        if (_biteVFXCoroutine != null)
        {
            StopCoroutine(_biteVFXCoroutine);
            _biteVFXCoroutine = null;
        }
    }

    /// Vòng lặp spawn định kỳ: mỗi _biteRingInterval giây thì spawn 1 vòng gợn nhỏ
    /// + 1 đợt bong bóng, cho đến khi cá ngừng rỉa (IsBiting = false).
    IEnumerator BiteVFXLoop()
    {
        while (_currentLine != null && _currentLine.IsBiting)
        {
            Vector3 floatPos = _currentLine.CurrentFloatPos;

            // Vòng gợn nhỏ ngay tại phao
            SpawnRing(floatPos,
                _biteRingStartSize, _biteRingEndSize,
                _biteRingLifetime,  _biteRingGrowCurve,
                alpha: 0.45f);

            // Bong bóng nhỏ nổi lên từ ngay dưới phao
            SpawnBiteBubbles(floatPos);

            yield return new WaitForSeconds(_biteRingInterval);
        }

        _biteVFXCoroutine = null;
    }

    // ─── Droplets ─────────────────────────────────────────────────────────

    void SpawnDroplets(Vector3 pos)
    {
        GameObject go = new GameObject("FishingSplash");
        go.transform.position = pos;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration        = Mathf.Max(0.3f, _burstSpread + 0.1f);
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(_dropletLifetime * 0.8f, _dropletLifetime * 1.15f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(_dropletSpeed * 0.6f, _dropletSpeed);
        main.startSize       = new ParticleSystem.MinMaxCurve(_dropletSize * 0.5f, _dropletSize * 1.4f);
        main.startColor      = _waterColor;
        main.gravityModifier = _dropletGravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = _dropletCount;

        var emission = ps.emission;
        emission.enabled = true;
        if (_burstSpread <= 0.001f)
        {
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, _dropletCount) });
        }
        else
        {
            int steps   = 4;
            int perStep = Mathf.Max(1, _dropletCount / steps);
            var bursts  = new ParticleSystem.Burst[steps];
            for (int i = 0; i < steps; i++)
            {
                float bt    = (i / (float)(steps - 1)) * _burstSpread;
                int   count = (i == steps - 1) ? _dropletCount - perStep * (steps - 1) : perStep;
                bursts[i] = new ParticleSystem.Burst(bt, Mathf.Max(1, count));
            }
            emission.SetBursts(bursts);
        }

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle     = 35f;
        shape.radius    = 0.05f;
        shape.rotation  = new Vector3(-90f, 0f, 0f);

        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.space   = ParticleSystemSimulationSpace.Local;
        vol.radial  = new ParticleSystem.MinMaxCurve(0.6f);

        var sol      = ps.sizeOverLifetime;
        sol.enabled  = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material   = CreateWaterMaterial();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Play();
        Destroy(go, _dropletLifetime * 1.2f + _burstSpread + 0.5f);
    }

    // ─── Ring (dùng chung cho splash và bite) ─────────────────────────────

    /// <param name="alpha">Alpha tối đa của ring — splash dùng 0.6f, bite dùng 0.45f nhỏ hơn một chút.</param>
    void SpawnRing(Vector3 pos, float startSize, float endSize, float lifetime, float growCurve, float alpha)
    {
        GameObject go = new GameObject("FishingRing");
        go.transform.position = pos;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration        = 0.1f;
        main.loop            = false;
        main.startLifetime   = lifetime;
        main.startSpeed      = 0f;
        main.startSize       = startSize;
        main.startColor      = new Color(_waterColor.r, _waterColor.g, _waterColor.b, alpha);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 1;

        var emission = ps.emission;
        emission.enabled = true;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        var shape = ps.shape;
        shape.enabled = false;

        // Easing: giãn nhanh lúc đầu rồi chậm dần — giống sóng nước mất động năng
        var sol    = ps.sizeOverLifetime;
        sol.enabled = true;
        var curve  = new AnimationCurve();
        int steps  = 10;
        for (int s = 0; s <= steps; s++)
        {
            float t     = s / (float)steps;
            float eased = Mathf.Pow(t, growCurve);
            curve.AddKey(t, Mathf.Lerp(startSize, endSize, eased));
        }
        sol.size = new ParticleSystem.MinMaxCurve(1f, curve);

        // Fade in nhanh → fade out chậm dần
        var col    = ps.colorOverLifetime;
        col.enabled = true;
        var grad   = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(_waterColor, 0f), new GradientColorKey(_waterColor, 1f) },
            new[] {
                new GradientAlphaKey(0f,    0f),
                new GradientAlphaKey(alpha, 0.08f),
                new GradientAlphaKey(0f,    1f)
            }
        );
        col.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material   = CreateWaterMaterial();
        renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;

        ps.Play();
        Destroy(go, lifetime + 0.2f);
    }

    // ─── Bite Bubbles ─────────────────────────────────────────────────────

    /// Bong bóng nhỏ nổi lên từ ngay dưới phao — random vị trí XZ trong bán kính nhỏ.
    void SpawnBiteBubbles(Vector3 floatPos)
    {
        GameObject go = new GameObject("FishingBiteBubbles");
        // Spawn từ ngay dưới mặt nước một chút
        go.transform.position = floatPos + Vector3.down * 0.05f;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration        = 0.15f;
        main.loop            = false;
        // Bong bóng nổi lên chậm rãi, tốc độ nhỏ
        main.startLifetime   = new ParticleSystem.MinMaxCurve(_biteBubbleLifetime * 0.8f, _biteBubbleLifetime);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(_biteBubbleRiseSpeed * 0.6f, _biteBubbleRiseSpeed);
        main.startSize       = new ParticleSystem.MinMaxCurve(_biteBubbleSize * 0.6f, _biteBubbleSize * 1.3f);
        // Màu trắng xanh nhạt, bán trong suốt — giống bong bóng nước
        main.startColor      = new Color(0.85f, 0.95f, 1f, 0.55f);
        // Gravity âm nhẹ để bong bóng nổi lên, không rơi xuống
        main.gravityModifier = -0.15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = _biteBubbleCount;

        var emission = ps.emission;
        emission.enabled = true;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, _biteBubbleCount) });

        // Spawn tán xạ trong hình tròn nhỏ quanh phao, hướng lên
        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = _biteBubbleSpread;
        shape.rotation  = new Vector3(-90f, 0f, 0f); // hướng lên

        // Nhỏ dần rồi biến mất — bong bóng vỡ khi chạm mặt nước
        var sol      = ps.sizeOverLifetime;
        sol.enabled  = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f,   1f);
        sizeCurve.AddKey(0.7f, 0.9f);
        sizeCurve.AddKey(1f,   0f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Mờ dần ở cuối vòng đời
        var col    = ps.colorOverLifetime;
        col.enabled = true;
        var grad   = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] {
                new GradientAlphaKey(0.55f, 0f),
                new GradientAlphaKey(0.55f, 0.6f),
                new GradientAlphaKey(0f,    1f)
            }
        );
        col.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material   = CreateWaterMaterial();
        // Billboard để bong bóng trông tròn từ mọi góc nhìn
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Play();
        Destroy(go, _biteBubbleLifetime + 0.3f);
    }

    // ─── Shared ───────────────────────────────────────────────────────────

    static Material CreateWaterMaterial()
    {
        var mat = new Material(Shader.Find("Particles/Standard Unlit"));
        if (mat.shader == null || !mat.shader.isSupported)
            mat = new Material(Shader.Find("Sprites/Default"));
        mat.SetFloat("_Mode", 2f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;
        return mat;
    }
}