using UnityEngine;

/// <summary>
/// De + ech theo gio trong game: nhen tu 17h30, ro dan theo hoang hon,
/// to nhat luc 19h va giu den het ngay.
/// Dat len 1 object trong scene Gameplay.
/// </summary>
public class GameDayNightAmbience : GameDayAmbienceLoop
{
    [Header("Night Ambience")]
    [Tooltip("File tieng de / ech dem (WAV/OGG, loop muot).")]
    [SerializeField] private AudioClip _nightClip;

    protected override AudioClip Clip => _nightClip;

    protected override AnimationCurve CreateDefaultCurve()
    {
        return BuildSmoothCurve(
            new Vector2(17.5f, 0f),
            new Vector2(18.5f, 0.8f),  // ro dan khi troi toi
            new Vector2(19f, 1f),
            new Vector2(19.5f, 1f)
        );
    }
}