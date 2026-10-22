using UnityEngine;

/// <summary>
/// Tieng chim hot theo gio trong game: to nhat tu 5h30, giu den 9h, thua
/// dan va tat han luc 10h.
/// Dat script nay len 1 object trong scene Gameplay (VD Manager).
/// </summary>
public class GameDayBirdsong : GameDayAmbienceLoop
{
    [Header("Birdsong")]
    [Tooltip("File tieng chim hot (WAV/OGG, loop muot, 1-2 phut tro len).")]
    [SerializeField] private AudioClip _birdClip;

    protected override AudioClip Clip => _birdClip;

    protected override AnimationCurve CreateDefaultCurve()
    {
        return BuildSmoothCurve(
            new Vector2(4.5f, 0.3f),
            new Vector2(5.5f, 1f),     // binh minh: to nhat
            new Vector2(9f, 1f),       // giu to nhat den 9h
            new Vector2(9.5f, 0.55f),  // bat dau thua ro
            new Vector2(10f, 0f),      // tat han luc 10h
            new Vector2(10.5f, 0f)
        );
    }
}