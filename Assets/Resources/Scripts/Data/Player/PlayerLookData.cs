using UnityEngine;

[CreateAssetMenu(fileName = "PlayerLook", menuName = "Data/Player Look")]
public class PlayerLookData : ScriptableObject
{
    [Header("Mouse Look")]
    [SerializeField] private float _mouseSensitivity = 1.5f;
    [SerializeField] private float _minLookAngle = -80f;
    [SerializeField] private float _maxLookAngle = 80f;

    public float MouseSensitivity => _mouseSensitivity;
    public float MinLookAngle => _minLookAngle;
    public float MaxLookAngle => _maxLookAngle;
}