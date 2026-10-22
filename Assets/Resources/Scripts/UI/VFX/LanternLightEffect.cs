using UnityEngine;

[RequireComponent(typeof(Light))]
public class LanternLightEffect : MonoBehaviour
{
    [Header("Flicker Settings")]
    [Tooltip("Cường độ sáng tối thiểu")]
    public float minIntensity = 5f;
    [Tooltip("Cường độ sáng tối đa")]
    public float maxIntensity = 9f;
    [Tooltip("Tốc độ nhấp nháy - số nhỏ = mượt/chậm, số lớn = rung nhanh")]
    public float flickerSpeed = 1.5f;

    [Header("Optional: Emission theo Material")]
    [Tooltip("Kéo Renderer của Lamp_Glass_Geo vào đây nếu muốn Emission cũng nhấp nháy theo")]
    public Renderer glassRenderer;
    [Tooltip("Màu Emission gốc (dùng đúng màu bạn đã chọn, vd xanh ngọc)")]
    public Color emissionColor = new Color(0.37f, 1f, 0.88f); // #5FFFE0
    public float minEmissionIntensity = 2f;
    public float maxEmissionIntensity = 5f;

    private Light _light;
    private float _seed;
    private MaterialPropertyBlock _propBlock;

    void Awake()
    {
        _light = GetComponent<Light>();
        _seed = Random.Range(0f, 1000f); // để nhiều đèn không nhấp nháy giống hệt nhau
        _propBlock = new MaterialPropertyBlock();
    }

    void Update()
    {
        float noise = Mathf.PerlinNoise(_seed, Time.time * flickerSpeed);

        // Light intensity
        float lightValue = Mathf.Lerp(minIntensity, maxIntensity, noise);
        _light.intensity = lightValue;

        // Emission Material (optional)
        if (glassRenderer != null)
        {
            float emissionValue = Mathf.Lerp(minEmissionIntensity, maxEmissionIntensity, noise);
            Color finalEmission = emissionColor * Mathf.LinearToGammaSpace(emissionValue);

            glassRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor("_EmissionColor", finalEmission);
            glassRenderer.SetPropertyBlock(_propBlock);
        }
    }
}