using UnityEngine;

/// <summary>
/// Tieng chim chieu (ve to) theo gio trong game: nhen tu 15h30 (chong len
/// doan ve dang tat), nhe nhang hon chim sang, to nhat ~16h30-17h15, thua
/// dan va tat han luc 18h30.
/// Dat len 1 object trong scene Gameplay.
/// </summary>
public class GameDayEveningBirdsong : GameDayAmbienceLoop
{
    [Header("Evening Birdsong")]
    [Tooltip("File tieng chim chieu / chim ve to (WAV/OGG, loop muot).")]
    [SerializeField] private AudioClip _eveningBirdClip;

    protected override AudioClip Clip => _eveningBirdClip;

    protected override AnimationCurve CreateDefaultCurve()
    {
        return BuildSmoothCurve(
            new Vector2(15.5f, 0f),
            new Vector2(16.5f, 0.7f),  // chim ve to, nhe hon chim sang
            new Vector2(17.25f, 0.7f),
            new Vector2(18f, 0.3f),    // thua dan
            new Vector2(18.5f, 0f),    // tat han
            new Vector2(19f, 0f)
        );
    }
}