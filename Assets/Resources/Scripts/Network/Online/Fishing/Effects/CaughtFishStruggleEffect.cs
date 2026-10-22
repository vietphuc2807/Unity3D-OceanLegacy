// ===================== CaughtFishStruggleEffect.cs =====================
// Realistic hooked-fish struggle:
// - Whole fish behaves like a suspended body at the mouth.
// - Uses irregular reflex pulses instead of a constant sine wave.
// - Strong initial shock, short frantic bursts, then fatigue.
// - Adds tiny roll/pitch variations so the fish does not look like a
//   perfectly mechanical pendulum.
// - Size response: large fish = heavier/slower/stronger, small fish = faster.
//
// This component stays on CaughtFishPivot.

using System;
using UnityEngine;

[DefaultExecutionOrder(150)]
public class CaughtFishStruggleEffect : MonoBehaviour
{
    [Header("WHOLE FISH - Suspended body")]
    [SerializeField] private float _baseYawAngle = 7f;
    [SerializeField] private float _basePitchAngle = 3.5f;
    [SerializeField] private float _baseRollAngle = 4f;

    [Tooltip("How strongly the body follows the random struggle target.")]
    [SerializeField] private float _springStiffness = 32f;

    [Range(0.2f, 1.2f)]
    [SerializeField] private float _dampingRatio = 0.58f;

    [Header("REFLEX - Irregular swimming reflex")]
    [SerializeField] private float _reflexIntervalMin = 0.16f;
    [SerializeField] private float _reflexIntervalMax = 0.48f;

    [SerializeField] private float _reflexDurationMin = 0.08f;
    [SerializeField] private float _reflexDurationMax = 0.24f;

    [SerializeField] private float _reflexAngleMin = 10f;
    [SerializeField] private float _reflexAngleMax = 32f;

    [Range(0f, 1f)]
    [SerializeField] private float _violentBurstChance = 0.22f;

    [Header("FIRST SHOCK - Vừa bị câu lên")]
    [SerializeField] private float _firstShockDelay = 0.08f;
    [SerializeField] private float _firstShockDuration = 0.35f;
    [SerializeField] private float _firstShockAngle = 30f;

    [Header("SIZE RESPONSE")]
    [Tooltip("0 = Min Size, 1 = Max Size.")]
    [SerializeField] private float _largeFishStrength = 1.45f;

    [SerializeField] private float _largeFishSpeed = 0.72f;
    [SerializeField] private float _smallFishSpeed = 1.22f;
    [SerializeField] private float _largeFishInertia = 1.35f;

    [Header("FATIGUE")]
    [SerializeField] private float _fatigueTime = 5f;

    [Range(0f, 1f)]
    [SerializeField] private float _minimumFatigue = 0.42f;

    [Header("LINE PULL")]
    [SerializeField] private float _linePullScale = 0.012f;
    [SerializeField] private float _maxLinePull = 0.06f;

    private CaughtFishFollowAnchor _followAnchor;

    private float _spawnTime;
    private float _time;
    private float _nextReflexTime;

    private float _reflexElapsed = -1f;
    private float _reflexDuration;
    private float _reflexStrength;
    private float _reflexDirection;
    private float _reflexPitch;
    private float _reflexRoll;

    private float _firstShockElapsed = -1f;

    private float _yaw;
    private float _pitch;
    private float _roll;

    private float _yawVelocity;
    private float _pitchVelocity;
    private float _rollVelocity;

    private float _sizeFactor01;
    private float _fishStrength01 = 0.5f;
    private bool _isReeling;

    private Vector3 _currentTugWorld;
    private float _currentTugStrength01;

    private float _noiseSeed;

    public Vector3 CurrentTugWorld => _currentTugWorld;
    public float CurrentTugStrength01 => _currentTugStrength01;

    public void SetSizeFactor01(float size01)
    {
        _sizeFactor01 = Mathf.Clamp01(size01);
    }

    public void SetFishStrength(float strength01)
    {
        _fishStrength01 = Mathf.Clamp01(strength01);
    }

    public void SetReeling(bool active)
    {
        _isReeling = active;
    }

    private void Awake()
    {
        _followAnchor = GetComponent<CaughtFishFollowAnchor>();

        _spawnTime = Time.time;
        _time = UnityEngine.Random.Range(0f, 100f);
        _noiseSeed = UnityEngine.Random.Range(0f, 1000f);

        _nextReflexTime = UnityEngine.Random.Range(0.03f, 0.12f);
        _firstShockElapsed = -_firstShockDelay;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        _time += dt;

        float fatigue = GetFatigueMultiplier();

        float sizeSpeed = Mathf.Lerp(
            _smallFishSpeed,
            _largeFishSpeed,
            _sizeFactor01
        );

        float strength = Mathf.Lerp(
            0.78f,
            1.12f,
            _fishStrength01
        );

        float reelMultiplier = _isReeling ? 1.12f : 1f;

        UpdateFirstShock(dt, fatigue, strength);
        UpdateReflex(dt, fatigue, sizeSpeed, strength);

        // Small continuous movement is deliberately subtle.
        // Real fish do not continuously swing with a clean sine wave.
        float idleYaw =
            Mathf.PerlinNoise(_noiseSeed, _time * 0.9f * sizeSpeed) * 2f - 1f;

        float idlePitch =
            Mathf.PerlinNoise(_noiseSeed + 17f, _time * 0.72f * sizeSpeed) * 2f - 1f;

        float idleRoll =
            Mathf.PerlinNoise(_noiseSeed + 39f, _time * 0.63f * sizeSpeed) * 2f - 1f;

        float targetYaw =
            idleYaw * _baseYawAngle * fatigue * strength;

        float targetPitch =
            idlePitch * _basePitchAngle * fatigue * strength;

        float targetRoll =
            idleRoll * _baseRollAngle * fatigue * strength;

        if (_reflexElapsed >= 0f)
        {
            float envelope = GetReflexEnvelope();

            targetYaw +=
                _reflexDirection *
                _reflexStrength *
                envelope *
                fatigue *
                reelMultiplier;

            targetPitch +=
                _reflexPitch *
                envelope *
                fatigue;

            targetRoll +=
                _reflexRoll *
                envelope *
                fatigue;
        }

        if (_firstShockElapsed >= 0f &&
            _firstShockElapsed <= _firstShockDuration)
        {
            float t = Mathf.Clamp01(
                _firstShockElapsed / Mathf.Max(0.001f, _firstShockDuration)
            );

            // Sharp start, softer release.
            float envelope = Mathf.Sin(t * Mathf.PI);

            targetYaw +=
                Mathf.Sin(t * Mathf.PI * 1.6f) *
                _firstShockAngle *
                envelope *
                fatigue;

            targetPitch +=
                Mathf.Sin(t * Mathf.PI * 1.1f + 0.8f) *
                _firstShockAngle *
                0.25f *
                envelope;

            targetRoll +=
                Mathf.Cos(t * Mathf.PI * 1.35f) *
                _firstShockAngle *
                0.20f *
                envelope;
        }

        // Large fish have more inertia.
        float stiffness =
            _springStiffness /
            Mathf.Lerp(1f, _largeFishInertia, _sizeFactor01);

        float damping =
            2f *
            Mathf.Sqrt(Mathf.Max(0.01f, stiffness)) *
            _dampingRatio;

        StepSpring(
            ref _yaw,
            ref _yawVelocity,
            targetYaw,
            stiffness,
            damping,
            dt
        );

        StepSpring(
            ref _pitch,
            ref _pitchVelocity,
            targetPitch,
            stiffness * 0.9f,
            damping,
            dt
        );

        StepSpring(
            ref _roll,
            ref _rollVelocity,
            targetRoll,
            stiffness * 0.85f,
            damping,
            dt
        );

        Quaternion baseRotation = GetAnchorBaseRotation();

        transform.rotation =
            baseRotation *
            Quaternion.Euler(
                _pitch,
                _yaw,
                _roll
            );

        UpdateLinePull();
    }

    private void UpdateFirstShock(
        float dt,
        float fatigue,
        float strength
    )
    {
        if (_firstShockElapsed < 0f)
        {
            _firstShockElapsed += dt;
            return;
        }

        if (_firstShockElapsed <= _firstShockDuration)
            _firstShockElapsed += dt;
    }

    private void UpdateReflex(
        float dt,
        float fatigue,
        float speedMultiplier,
        float strength
    )
    {
        if (_reflexElapsed >= 0f)
        {
            _reflexElapsed += dt;

            if (_reflexElapsed >= _reflexDuration)
            {
                _reflexElapsed = -1f;

                float restMultiplier = Mathf.Lerp(
                    1.8f,
                    0.9f,
                    fatigue
                );

                _nextReflexTime =
                    UnityEngine.Random.Range(
                        _reflexIntervalMin,
                        _reflexIntervalMax
                    ) *
                    restMultiplier /
                    Mathf.Max(0.35f, speedMultiplier);
            }

            return;
        }

        _nextReflexTime -= dt;

        if (_nextReflexTime <= 0f)
            StartReflex(fatigue, strength);
    }

    private void StartReflex(
        float fatigue,
        float strength
    )
    {
        _reflexElapsed = 0f;

        _reflexDuration = UnityEngine.Random.Range(
            _reflexDurationMin,
            _reflexDurationMax
        );

        bool violent =
            UnityEngine.Random.value < _violentBurstChance;

        float angle = UnityEngine.Random.Range(
            _reflexAngleMin,
            _reflexAngleMax
        );

        if (violent)
            angle *= UnityEngine.Random.Range(1.35f, 1.8f);

        angle *= Mathf.Lerp(
            1f,
            _largeFishStrength,
            _sizeFactor01
        );

        angle *= fatigue * strength;

        _reflexStrength = angle;

        _reflexDirection =
            UnityEngine.Random.value < 0.5f
                ? -1f
                : 1f;

        _reflexPitch =
            UnityEngine.Random.Range(-0.35f, 0.35f) *
            angle;

        _reflexRoll =
            UnityEngine.Random.Range(-0.28f, 0.28f) *
            angle;
    }

    private float GetReflexEnvelope()
    {
        if (_reflexElapsed < 0f)
            return 0f;

        float t =
            Mathf.Clamp01(
                _reflexElapsed /
                Mathf.Max(0.001f, _reflexDuration)
            );

        // Fast contraction, slower release.
        float attack =
            Mathf.Clamp01(t / 0.22f);

        float release =
            1f - Mathf.Clamp01(
                (t - 0.22f) / 0.78f
            );

        return Mathf.Min(attack, release);
    }

    private void StepSpring(
        ref float value,
        ref float velocity,
        float target,
        float stiffness,
        float damping,
        float dt
    )
    {
        float displacement = value - target;

        float acceleration =
            -stiffness * displacement -
            damping * velocity;

        velocity += acceleration * dt;
        value += velocity * dt;
    }

    private float GetFatigueMultiplier()
    {
        float elapsed =
            Time.time - _spawnTime;

        float decay =
            Mathf.Exp(
                -elapsed /
                Mathf.Max(0.01f, _fatigueTime)
            );

        return Mathf.Lerp(
            _minimumFatigue,
            1f,
            decay
        );
    }

    private Quaternion GetAnchorBaseRotation()
    {
        if (_followAnchor != null &&
            _followAnchor.Anchor != null)
        {
            return _followAnchor.Anchor.rotation;
        }

        if (transform.parent != null)
            return transform.parent.rotation;

        return Quaternion.identity;
    }

    private void UpdateLinePull()
    {
        float sizePull =
            Mathf.Lerp(
                1f,
                _largeFishInertia,
                _sizeFactor01
            );

        Vector3 localTug =
            new Vector3(
                -_yawVelocity,
                -_pitchVelocity * 0.55f,
                -_rollVelocity * 0.18f
            ) *
            _linePullScale *
            sizePull;

        float magnitude = localTug.magnitude;

        if (magnitude > _maxLinePull &&
            magnitude > 0.0001f)
        {
            localTug *=
                _maxLinePull / magnitude;
        }

        _currentTugWorld =
            transform.parent != null
                ? transform.parent.TransformDirection(localTug)
                : Vector3.zero;

        _currentTugStrength01 =
            Mathf.Clamp01(
                localTug.magnitude /
                Mathf.Max(0.0001f, _maxLinePull)
            );
    }

    private void OnDestroy()
    {
        _yaw = 0f;
        _pitch = 0f;
        _roll = 0f;
    }
}
