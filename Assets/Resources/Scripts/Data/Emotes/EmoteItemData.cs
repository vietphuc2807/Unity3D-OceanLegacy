using UnityEngine;

[CreateAssetMenu(fileName = "EmoteItem", menuName = "Data/Emote Item")]
public class EmoteItemData : ScriptableObject
{
    [SerializeField] private string _emoteId;
    [SerializeField] private string _label;
    [SerializeField] private Sprite _icon;
    [SerializeField] private string _animatorStateName;
    [SerializeField] private AnimationClip _clip;

    [Header("Loop Settings")]
    [SerializeField] private bool _loop;
    [SerializeField] private float _playDuration = 2.5f; 

    public string EmoteId => _emoteId;
    public string Label => _label;
    public Sprite Icon => _icon;
    public string AnimatorStateName => _animatorStateName;
    public AnimationClip Clip => _clip;
    public bool Loop => _loop;
    public float PlayDuration => _playDuration;
}