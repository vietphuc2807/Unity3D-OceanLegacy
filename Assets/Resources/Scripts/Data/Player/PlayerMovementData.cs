using UnityEngine;

[CreateAssetMenu(fileName = "PlayerMovement", menuName = "Data/Player Movement")]
public class PlayerMovementData : ScriptableObject
{
    [Header("Movement")]
    [SerializeField] private float _walkSpeed = 3.5f;
    [SerializeField] private float _runSpeed = 5.5f;

    [Header("Jump")]
    [SerializeField] private float _jumpHeight = 1.2f;

    [Header("Gravity")]
    [SerializeField] private float _gravity = -20f;

    public float WalkSpeed => _walkSpeed;
    public float RunSpeed => _runSpeed;
    public float JumpHeight => _jumpHeight;
    public float Gravity => _gravity;
}