using UnityEngine;

/// <summary>
/// Tieng ve theo gio trong game: nhen tu 9h45 (khi chim da thua), ro dan
/// den 12h, giu den 15h, nho dan va ngung han luc 16h.
/// Dat script nay len 1 object trong scene Gameplay.
/// </summary>
public class GameDayCicadasong : GameDayAmbienceLoop
{
    [Header("Cicadasong")]
    [Tooltip("File tieng ve (WAV/OGG, loop muot).")]
    [SerializeField] private AudioClip _cicadaClip;

    protected override AudioClip Clip => _cicadaClip;

    protected override AnimationCurve CreateDefaultCurve()
    {
        return BuildSmoothCurve(
            new Vector2(9.75f, 0f),    // bat dau nhen khi chim da thua
            new Vector2(10.75f, 0.35f),
            new Vector2(12f, 1f),      // to nhat
            new Vector2(15f, 1f),
            new Vector2(16f, 0f),      // ngung han luc 16h
            new Vector2(16.5f, 0f)
        );
    }
}