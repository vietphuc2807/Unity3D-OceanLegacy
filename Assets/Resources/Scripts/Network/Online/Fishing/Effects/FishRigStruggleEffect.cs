// ===================== FishRigStruggleEffect.cs =====================
// Realistic hooked-fish body deformation.
//
// Behaviour:
// 1. Head remains relatively stable because the hook is in the mouth.
// 2. Body continuously moves with a subtle swimming/wriggling motion.
// 3. Freshly hooked fish starts with a strong violent reaction.
// 4. The fish then becomes tired.
// 5. After a recovery period, it suddenly struggles hard again.
// 6. Strong struggles create a deep C/S-shaped body curve.
// 7. Rear body and tail have much larger movement than the head.
// 8. Each struggle has:
//      Attack -> Hold -> Release -> Recovery
// 9. Strong bursts are irregular and alternate left/right.
// 10. Large fish move slower and have more inertia.
// 11. Small fish react faster and more sharply.
// 12. Fatigue reduces the average movement but does not completely stop it.
//
// Assign bones manually: HEAD -> TAIL.
//
// Recommended Bend Axis: Local Z
// ============================================================

using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(160)]
public class FishRigStruggleEffect : MonoBehaviour
{
    public enum BendAxis
    {
        LocalX,
        LocalY,
        LocalZ
    }


    // ============================================================
    // RIG
    // ============================================================

    [Header("RIG - HEAD -> TAIL")]

    [Tooltip("Bone closest to the head. Keep this bone relatively stable.")]
    [SerializeField] private Transform _headBone;

    [Tooltip("Body bones ordered HEAD -> TAIL.")]
    [SerializeField] private Transform[] _bodyBones;

    [Tooltip("Tail bones ordered BODY -> TIP.")]
    [SerializeField] private Transform[] _tailBones;


    // ============================================================
    // BENDING
    // ============================================================

    [Header("BENDING")]

    [Tooltip("For your current fish rigs, start with Local Z.")]
    [SerializeField] private BendAxis _bendAxis = BendAxis.LocalZ;

    [SerializeField] private bool _invertBendAxis = false;

    [Tooltip("Main continuous curvature of the body.")]
    [SerializeField] private float _bodyBendAngle = 28f;

    [Tooltip("Additional curvature toward the tail.")]
    [SerializeField] private float _tailBendAngle = 48f;

    [Tooltip("How much the rear half participates.")]
    [SerializeField] private float _rearFalloff = 1.05f;

    [Tooltip("Base travelling-wave speed.")]
    [SerializeField] private float _waveSpeed = 6.2f;

    [Tooltip("Phase difference between bones.")]
    [SerializeField] private float _phaseOffset = 0.58f;

    [Tooltip("Bone response. Lower values create more inertia.")]
    [SerializeField] private float _smoothSpeed = 13f;


    // ============================================================
    // DEEP C-CURVE
    // ============================================================

    [Header("DEEP C-CURVE")]

    [Tooltip("Maximum additional bend during a strong struggle.")]
    [SerializeField] private float _deepCurveAngle = 48f;

    [Tooltip("Where the deep C curve begins along the fish.")]
    [Range(0f, 1f)]
    [SerializeField] private float _deepCurveStart = 0.38f;

    [Tooltip("How quickly the curve becomes stronger toward the tail.")]
    [SerializeField] private float _deepCurvePower = 1.45f;

    [Tooltip("How strongly violent struggles activate the C curve.")]
    [SerializeField] private float _deepCurveReflexMultiplier = 1.65f;

    [Tooltip("Extra C curve even during normal movement.")]
    [SerializeField] private float _deepCurveBaseInfluence = 0.12f;


    // ============================================================
    // STRUGGLE CYCLE
    // ============================================================

    [Header("STRUGGLE CYCLE")]

    [Tooltip("How soon the first strong struggle happens after hooking.")]
    [SerializeField] private float _firstBurstDelay = 0.08f;

    [Tooltip("How often strong struggle bursts can happen.")]
    [SerializeField] private float _burstIntervalMin = 0.55f;

    [SerializeField] private float _burstIntervalMax = 1.45f;

    [Tooltip("Strong burst duration.")]
    [SerializeField] private float _burstDurationMin = 0.28f;

    [SerializeField] private float _burstDurationMax = 0.55f;

    [Tooltip("Recovery after a burst.")]
    [SerializeField] private float _recoveryDurationMin = 0.35f;

    [SerializeField] private float _recoveryDurationMax = 1.10f;

    [Tooltip("Minimum strength of a burst.")]
    [SerializeField] private float _burstStrengthMin = 0.75f;

    [Tooltip("Maximum strength of a burst.")]
    [SerializeField] private float _burstStrengthMax = 1.35f;

    [Range(0f, 1f)]
    [Tooltip("Chance of an especially violent burst.")]
    [SerializeField] private float _violentBurstChance = 0.28f;

    [SerializeField] private float _violentBurstMultiplierMin = 1.35f;

    [SerializeField] private float _violentBurstMultiplierMax = 1.85f;


    // ============================================================
    // BURST SHAPE
    // ============================================================

    [Header("BURST SHAPE")]

    [Tooltip("How fast the fish reaches maximum force.")]
    [SerializeField] private float _burstAttackTime = 0.12f;

    [Tooltip("How long the fish stays at high force.")]
    [SerializeField] private float _burstHoldTime = 0.16f;

    [Tooltip("How slowly the fish releases the contraction.")]
    [SerializeField] private float _burstReleasePower = 1.65f;

    [Tooltip("Variation in burst shape.")]
    [Range(0f, 1f)]
    [SerializeField] private float _burstShapeRandomness = 0.22f;

    [Tooltip("How much the burst can reverse the normal body wave.")]
    [Range(0f, 1f)]
    [SerializeField] private float _burstWaveOverride = 0.45f;


    // ============================================================
    // REFLEX
    // ============================================================

    [Header("REFLEX - SMALL FAST TWITCHES")]

    [SerializeField] private float _reflexIntervalMin = 0.18f;

    [SerializeField] private float _reflexIntervalMax = 0.50f;

    [SerializeField] private float _reflexDurationMin = 0.12f;

    [SerializeField] private float _reflexDurationMax = 0.28f;

    [SerializeField] private float _reflexStrengthMin = 0.35f;

    [SerializeField] private float _reflexStrengthMax = 0.75f;

    [Range(0f, 1f)]
    [SerializeField] private float _reflexChanceDuringRecovery = 0.65f;


    // ============================================================
    // FIRST HOOK REACTION
    // ============================================================

    [Header("FIRST HOOK REACTION")]

    [SerializeField] private float _firstReactionDelay = 0.02f;

    [SerializeField] private float _firstReactionDuration = 0.55f;

    [SerializeField] private float _firstReactionMultiplier = 1.55f;

    [SerializeField] private float _firstReactionSpeed = 25f;


    // ============================================================
    // TAIL
    // ============================================================

    [Header("TAIL")]

    [Tooltip("Extra movement concentrated toward the tail.")]
    [SerializeField] private float _tailWaveMultiplier = 1.45f;

    [Tooltip("How much the tail follows the body.")]
    [Range(0f, 1f)]
    [SerializeField] private float _tailFollow = 0.82f;

    [Tooltip("Additional local rotation allowed for tail bones.")]
    [SerializeField] private float _tailLocalAngleBonus = 14f;


    // ============================================================
    // 3D DETAIL
    // ============================================================

    [Header("3D DETAIL")]

    [SerializeField] private float _pitchFactor = 0.08f;

    [SerializeField] private float _rollFactor = 0.12f;

    [Range(0f, 1f)]
    [SerializeField] private float _noiseWeight = 0.16f;


    // ============================================================
    // SIZE RESPONSE
    // ============================================================

    [Header("SIZE RESPONSE")]

    [Tooltip("Large fish bend more deeply.")]
    [SerializeField] private float _largeFishBendMultiplier = 1.55f;

    [Tooltip("Small fish react faster.")]
    [SerializeField] private float _smallFishSpeedMultiplier = 1.35f;

    [Tooltip("Large fish move more slowly.")]
    [SerializeField] private float _largeFishSpeedMultiplier = 0.60f;

    [Tooltip("Large fish have more physical inertia.")]
    [SerializeField] private float _largeFishInertia = 1.35f;

    [Tooltip("Large fish get stronger contractions.")]
    [SerializeField] private float _largeFishBurstMultiplier = 1.45f;


    // ============================================================
    // FATIGUE
    // ============================================================

    [Header("FATIGUE")]

    [Tooltip("Time over which the fish gradually becomes tired.")]
    [SerializeField] private float _fatigueTime = 8f;

    [Range(0f, 1f)]
    [Tooltip("Movement never becomes completely dead.")]
    [SerializeField] private float _minimumFatigue = 0.30f;

    [Tooltip("How much fatigue affects strong bursts.")]
    [Range(0f, 1f)]
    [SerializeField] private float _fatigueBurstInfluence = 0.70f;


    // ============================================================
    // FISH STRENGTH
    // ============================================================

    [Header("FISH STRENGTH")]

    [Range(0f, 1f)]
    [SerializeField] private float _strengthInfluence = 0.75f;


    // ============================================================
    // REELING
    // ============================================================

    [Header("REELING")]

    [Range(0f, 2f)]
    [SerializeField] private float _reelingMultiplier = 1.10f;


    // ============================================================
    // ANIMATOR MODE
    // ============================================================

    [Header("ANIMATOR MODE")]

    [Tooltip("When enabled, this fish uses its single Animator animation instead of the procedural bend effect.")]
    [SerializeField] private bool _useAnimator = false;

    [Tooltip("Animator used for this fish's single struggle animation. No parameter or Idle state is required.")]
    [SerializeField] private Animator _animator;

    [Tooltip("Disable the Animator when this component starts. StartStruggle() enables it and StopStruggle() disables it.")]
    [SerializeField] private bool _disableAnimatorOnAwake = true;


    // ============================================================
    // INTERNAL
    // ============================================================

    private sealed class BoneState
    {
        public Transform Transform;
        public Quaternion InitialLocalRotation;
        public float Position01;
        public float Phase;
    }

    private readonly List<BoneState> _bones =
        new List<BoneState>();

    private float _spawnTime;
    private float _time;
    private float _noiseSeed;

    // ------------------------------------------------------------
    // STRONG BURST
    // ------------------------------------------------------------

    private float _burstTimer;
    private float _burstElapsed = -1f;
    private float _burstDuration;
    private float _burstStrength;
    private float _burstDirection;
    private float _burstCurveDirection;

    // ------------------------------------------------------------
    // RECOVERY
    // ------------------------------------------------------------

    private float _recoveryElapsed = -1f;
    private float _recoveryDuration;

    // ------------------------------------------------------------
    // SMALL REFLEX
    // ------------------------------------------------------------

    private float _reflexTimer;
    private float _reflexElapsed = -1f;
    private float _reflexDuration;
    private float _reflexStrength;
    private float _reflexDirection;

    // ------------------------------------------------------------
    // FIRST REACTION
    // ------------------------------------------------------------

    private float _firstReactionElapsed = -1f;

    // ------------------------------------------------------------
    // STATE
    // ------------------------------------------------------------

    private float _sizeFactor01;
    private float _fishStrength01 = 0.5f;

    private bool _isReeling = true;
    private bool _struggleEnabled = true;

    private float[] _desiredAngles;
    private float[] _smoothedLocalAngles;


    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        CacheBones();

        _spawnTime =
            Time.time;

        _time =
            Random.Range(
                0f,
                100f
            );

        _noiseSeed =
            Random.Range(
                0f,
                1000f
            );

        // Start with the first burst very soon.
        _burstTimer =
            Mathf.Max(
                0.02f,
                _firstBurstDelay
            );

        _reflexTimer =
            Random.Range(
                _reflexIntervalMin,
                _reflexIntervalMax
            );

        _firstReactionElapsed =
            -Mathf.Max(
                0f,
                _firstReactionDelay
            );

        // Animator-mode fish start with their Animator disabled.
        // It will be enabled by StartStruggle().
        if (_useAnimator && _animator != null && _disableAnimatorOnAwake)
        {
            _animator.enabled = false;
        }
    }


    private void OnDisable()
    {
        if (_useAnimator && _animator != null)
        {
            _animator.enabled = false;
        }

        RestorePose();
    }


    // ============================================================
    // CACHE BONES
    // ============================================================

    private void CacheBones()
    {
        _bones.Clear();

        AddBone(
            _headBone
        );

        if (_bodyBones != null)
        {
            for (int i = 0; i < _bodyBones.Length; i++)
            {
                AddBone(
                    _bodyBones[i]
                );
            }
        }

        if (_tailBones != null)
        {
            for (int i = 0; i < _tailBones.Length; i++)
            {
                AddBone(
                    _tailBones[i]
                );
            }
        }

        if (_bones.Count > 1)
        {
            for (int i = 0; i < _bones.Count; i++)
            {
                _bones[i].Position01 =
                    (float)i /
                    (_bones.Count - 1);

                _bones[i].Phase =
                    i *
                    _phaseOffset;
            }
        }

        _desiredAngles =
            new float[_bones.Count];

        _smoothedLocalAngles =
            new float[_bones.Count];
    }


    private void AddBone(
        Transform bone
    )
    {
        if (bone == null)
            return;

        for (int i = 0; i < _bones.Count; i++)
        {
            if (_bones[i].Transform == bone)
                return;
        }

        _bones.Add(
            new BoneState
            {
                Transform =
                    bone,

                InitialLocalRotation =
                    bone.localRotation,

                Position01 = 0f,

                Phase = 0f
            }
        );
    }


    // ============================================================
    // MAIN ANIMATION
    // ============================================================

    private void LateUpdate()
    {
        if (!_struggleEnabled)
            return;

        // Animator-mode fish are driven entirely by their single Animator animation.
        // Do not rotate the same bones procedurally.
        if (_useAnimator)
            return;

        if (_bones.Count < 2)
            return;

        float dt =
            Time.deltaTime;

        if (dt <= 0f)
            return;

        _time += dt;


        // ========================================================
        // FATIGUE
        // ========================================================

        float fatigue =
            GetFatigueMultiplier();


        // ========================================================
        // STRENGTH
        // ========================================================

        float strength =
            Mathf.Lerp(
                1f - _strengthInfluence,
                1f,
                _fishStrength01
            );


        // ========================================================
        // REELING
        // ========================================================

        float reel =
            _isReeling
                ? _reelingMultiplier
                : 1f;


        // ========================================================
        // SIZE
        // ========================================================

        float sizeSpeed =
            Mathf.Lerp(
                _smallFishSpeedMultiplier,
                _largeFishSpeedMultiplier,
                _sizeFactor01
            );

        float sizeBend =
            Mathf.Lerp(
                1f,
                _largeFishBendMultiplier,
                _sizeFactor01
            );

        float inertia =
            Mathf.Lerp(
                1f,
                _largeFishInertia,
                _sizeFactor01
            );


        // ========================================================
        // BASE INTENSITY
        // ========================================================

        float intensity =
            fatigue *
            strength *
            reel;


        // ========================================================
        // UPDATE SYSTEMS
        // ========================================================

        UpdateFirstReaction(
            dt
        );

        UpdateBurst(
            dt,
            fatigue,
            strength
        );

        UpdateRecovery(
            dt
        );

        UpdateReflex(
            dt,
            fatigue,
            strength
        );


        // ========================================================
        // WAVE TIME
        // ========================================================

        float waveTime =
            _time *
            _waveSpeed *
            sizeSpeed;


        // ========================================================
        // CURRENT BURST ENVELOPE
        // ========================================================

        float burst =
            GetBurstEnvelope();


        // ========================================================
        // CURRENT RECOVERY
        // ========================================================

        float recovery =
            GetRecoveryEnvelope();


        // ========================================================
        // 1. CREATE GLOBAL BODY CURVE
        // ========================================================

        for (int i = 0; i < _bones.Count; i++)
        {
            BoneState bone =
                _bones[i];

            float p =
                bone.Position01;


            // ----------------------------------------------------
            // HEAD STABILITY
            // ----------------------------------------------------

            float rear =
                Mathf.Pow(
                    Mathf.Clamp01(p),
                    Mathf.Max(
                        0.01f,
                        _rearFalloff
                    )
                );


            // ----------------------------------------------------
            // MAIN WAVE
            // ----------------------------------------------------

            float wave =
                Mathf.Sin(
                    waveTime -
                    bone.Phase
                );


            // ----------------------------------------------------
            // SECONDARY WAVE
            // ----------------------------------------------------

            float secondWave =
                Mathf.Sin(
                    waveTime * 0.53f -
                    bone.Phase * 0.72f +
                    1.7f
                );


            // ----------------------------------------------------
            // ORGANIC NOISE
            // ----------------------------------------------------

            float noise =
                Mathf.PerlinNoise(
                    _noiseSeed +
                    i * 0.61f,
                    _time * 0.70f
                ) *
                2f -
                1f;


            float motion =
                Mathf.Lerp(
                    wave,
                    wave * 0.82f +
                    secondWave * 0.18f +
                    noise * 0.12f,
                    _noiseWeight
                );


            // ----------------------------------------------------
            // RECOVERY REDUCES BASE MOTION
            // BUT NEVER COMPLETELY STOPS IT
            // ----------------------------------------------------

            float recoveryMotion =
                Mathf.Lerp(
                    0.55f,
                    1f,
                    recovery
                );


            motion *=
                recoveryMotion;


            // ----------------------------------------------------
            // MAIN BODY CURVE
            // ----------------------------------------------------

            float angle =
                _bodyBendAngle *
                rear *
                motion;


            // ----------------------------------------------------
            // TAIL CURVE
            // ----------------------------------------------------

            if (p > 0.52f)
            {
                float tail01 =
                    Mathf.InverseLerp(
                        0.52f,
                        1f,
                        p
                    );

                float tailCurve =
                    tail01 *
                    tail01 *
                    _tailBendAngle *
                    _tailWaveMultiplier;

                float tailMotion =
                    Mathf.Lerp(
                        motion,
                        Mathf.Sin(
                            waveTime -
                            bone.Phase -
                            0.20f
                        ),
                        _tailFollow
                    );

                angle +=
                    tailMotion *
                    tailCurve;
            }


            // ====================================================
            // DEEP C-CURVE
            // ====================================================

            if (p > _deepCurveStart)
            {
                float curve01 =
                    Mathf.InverseLerp(
                        _deepCurveStart,
                        1f,
                        p
                    );

                curve01 =
                    Mathf.Pow(
                        curve01,
                        _deepCurvePower
                    );


                // Base C curve is subtle.
                float baseCurve =
                    _deepCurveAngle *
                    _deepCurveBaseInfluence *
                    curve01;


                // Strong burst creates a deep C.
                float burstCurve =
                    _deepCurveAngle *
                    curve01 *
                    burst *
                    _deepCurveReflexMultiplier;


                float totalCurve =
                    baseCurve +
                    burstCurve;


                angle +=
                    totalCurve *
                    _burstCurveDirection;
            }


            // ====================================================
            // BURST BODY CONTRACTION
            // ====================================================

            if (burst > 0f)
            {
                float burstRear =
                    Mathf.Pow(
                        p,
                        1.10f
                    );


                float bodyCurve =
                    Mathf.Sin(
                        p *
                        Mathf.PI
                    );


                float rearCurve =
                    Mathf.Pow(
                        p,
                        1.35f
                    );


                float shape =
                    Mathf.Lerp(
                        rearCurve,
                        bodyCurve,
                        0.35f
                    );


                float burstAngle =
                    _burstStrength *
                    burst *
                    shape *
                    26f;


                // Stronger toward the rear.
                burstAngle *=
                    Mathf.Lerp(
                        0.25f,
                        1.30f,
                        burstRear
                    );


                angle +=
                    burstAngle *
                    _burstDirection;
            }


            // ====================================================
            // FINAL GLOBAL ANGLE
            // ====================================================

            _desiredAngles[i] =
                angle *
                intensity *
                sizeBend;
        }


        // ========================================================
        // 2. GLOBAL CURVE -> LOCAL JOINT ROTATION
        // ========================================================

        for (int i = 0; i < _bones.Count; i++)
        {
            BoneState bone =
                _bones[i];

            if (bone.Transform == null)
                continue;


            // ----------------------------------------------------
            // GLOBAL -> LOCAL
            // ----------------------------------------------------

            float localAngle =
                _desiredAngles[i];

            if (i > 0)
            {
                localAngle -=
                    _desiredAngles[i - 1];
            }


            // ====================================================
            // SMALL REFLEX TWITCH
            // ====================================================

            float reflex =
                GetReflexEnvelope();

            if (reflex > 0f)
            {
                float p =
                    bone.Position01;


                float rear =
                    Mathf.Pow(
                        p,
                        1.25f
                    );


                float tail =
                    Mathf.Pow(
                        p,
                        2f
                    );


                float reflexShape =
                    Mathf.Lerp(
                        rear,
                        Mathf.Sin(
                            p *
                            Mathf.PI
                        ),
                        0.35f
                    );


                float reflexAmount =
                    _reflexStrength *
                    reflex *
                    (
                        reflexShape *
                        0.75f
                        +
                        tail *
                        0.25f
                    );


                localAngle +=
                    _reflexDirection *
                    reflexAmount;
            }


            // ====================================================
            // FIRST HOOK REACTION
            // ====================================================

            float firstReaction =
                GetFirstReactionEnvelope();

            if (firstReaction > 0f)
            {
                float p =
                    bone.Position01;


                float rear =
                    Mathf.Pow(
                        p,
                        1.05f
                    );


                localAngle +=
                    Mathf.Sin(
                        _time *
                        _firstReactionSpeed -
                        bone.Phase
                    ) *
                    _firstReactionMultiplier *
                    8f *
                    rear *
                    firstReaction *
                    intensity *
                    sizeBend;
            }


            // ====================================================
            // LOCAL ANGLE LIMIT
            // ====================================================

            float maxLocal =
                Mathf.Lerp(
                    24f,
                    52f,
                    bone.Position01
                ) *
                sizeBend;


            if (bone.Position01 >
                0.50f)
            {
                float tail01 =
                    Mathf.InverseLerp(
                        0.50f,
                        1f,
                        bone.Position01
                    );


                maxLocal +=
                    tail01 *
                    _tailLocalAngleBonus *
                    sizeBend;
            }


            localAngle =
                Mathf.Clamp(
                    localAngle,
                    -maxLocal,
                    maxLocal
                );


            // ====================================================
            // AXIS
            // ====================================================

            float sign =
                _invertBendAxis
                    ? -1f
                    : 1f;

            localAngle *=
                sign;


            // ====================================================
            // INERTIA
            // ====================================================

            float responseSpeed =
                _smoothSpeed /
                Mathf.Max(
                    0.1f,
                    inertia
                );


            float lerp =
                1f -
                Mathf.Exp(
                    -responseSpeed *
                    dt
                );


            _smoothedLocalAngles[i] =
                Mathf.Lerp(
                    _smoothedLocalAngles[i],
                    localAngle,
                    lerp
                );


            float finalAngle =
                _smoothedLocalAngles[i];


            // ====================================================
            // 3D DETAIL
            // ====================================================

            float pitch =
                Mathf.Sin(
                    waveTime * 0.73f -
                    bone.Phase
                ) *
                _pitchFactor *
                Mathf.Abs(
                    finalAngle
                );


            float roll =
                Mathf.Cos(
                    waveTime * 0.57f -
                    bone.Phase
                ) *
                _rollFactor *
                Mathf.Abs(
                    finalAngle
                );


            // Make 3D detail stronger during hard struggles.
            float burst3D =
                1f +
                burst *
                0.45f;


            pitch *=
                burst3D;

            roll *=
                burst3D;


            // ====================================================
            // APPLY ROTATION
            // ====================================================

            Quaternion bend =
                Quaternion.AngleAxis(
                    finalAngle,
                    GetLocalAxis()
                );


            Quaternion detail =
                Quaternion.Euler(
                    pitch,
                    0f,
                    roll
                );


            bone.Transform.localRotation =
                bone.InitialLocalRotation *
                bend *
                detail;
        }
    }


    // ============================================================
    // STRONG BURST SYSTEM
    // ============================================================

    private void UpdateBurst(
        float dt,
        float fatigue,
        float strength
    )
    {
        // --------------------------------------------------------
        // ACTIVE BURST
        // --------------------------------------------------------

        if (_burstElapsed >= 0f)
        {
            _burstElapsed += dt;

            if (_burstElapsed >=
                _burstDuration)
            {
                _burstElapsed = -1f;

                // Start recovery after a strong contraction.
                _recoveryElapsed = 0f;

                _recoveryDuration =
                    Random.Range(
                        _recoveryDurationMin,
                        _recoveryDurationMax
                    );


                // Next burst after recovery.
                float fatigueDelay =
                    Mathf.Lerp(
                        0.80f,
                        1.35f,
                        1f - fatigue
                    );


                _burstTimer =
                    Random.Range(
                        _burstIntervalMin,
                        _burstIntervalMax
                    ) *
                    fatigueDelay;
            }

            return;
        }


        // --------------------------------------------------------
        // WAITING
        // --------------------------------------------------------

        _burstTimer -= dt;

        if (_burstTimer <= 0f)
        {
            StartBurst(
                fatigue,
                strength
            );
        }
    }


    private void StartBurst(
        float fatigue,
        float strength
    )
    {
        _burstElapsed =
            0f;


        // --------------------------------------------------------
        // DURATION
        // --------------------------------------------------------

        _burstDuration =
            Random.Range(
                _burstDurationMin,
                _burstDurationMax
            );


        // --------------------------------------------------------
        // POWER
        // --------------------------------------------------------

        float power =
            Random.Range(
                _burstStrengthMin,
                _burstStrengthMax
            );


        // --------------------------------------------------------
        // FATIGUE EFFECT
        // --------------------------------------------------------

        float fatigueBurst =
            Mathf.Lerp(
                1f,
                _minimumFatigue,
                (1f - fatigue) *
                _fatigueBurstInfluence
            );


        power *=
            fatigueBurst;


        // --------------------------------------------------------
        // FISH STRENGTH
        // --------------------------------------------------------

        power *=
            Mathf.Lerp(
                0.65f,
                1.25f,
                strength
            );


        // --------------------------------------------------------
        // LARGE FISH
        // --------------------------------------------------------

        float largeMultiplier =
            Mathf.Lerp(
                1f,
                _largeFishBurstMultiplier,
                _sizeFactor01
            );


        power *=
            largeMultiplier;


        // --------------------------------------------------------
        // OCCASIONAL VIOLENT BURST
        // --------------------------------------------------------

        if (Random.value <
            _violentBurstChance)
        {
            power *=
                Random.Range(
                    _violentBurstMultiplierMin,
                    _violentBurstMultiplierMax
                );
        }


        // --------------------------------------------------------
        // RANDOM VARIATION
        // --------------------------------------------------------

        power *=
            Random.Range(
                1f -
                _burstShapeRandomness,

                1f +
                _burstShapeRandomness
            );


        _burstStrength =
            power;


        // --------------------------------------------------------
        // LEFT / RIGHT
        // --------------------------------------------------------

        _burstDirection =
            Random.value <
            0.5f
                ? -1f
                : 1f;


        // --------------------------------------------------------
        // C CURVE DIRECTION
        // --------------------------------------------------------

        _burstCurveDirection =
            Random.value <
            0.5f
                ? -1f
                : 1f;
    }


    private float GetBurstEnvelope()
    {
        if (_burstElapsed < 0f)
            return 0f;


        float t =
            Mathf.Clamp01(
                _burstElapsed /
                Mathf.Max(
                    0.001f,
                    _burstDuration
                )
            );


        // ========================================================
        // ATTACK
        // ========================================================

        float attack01 =
            Mathf.Clamp01(
                _burstAttackTime /
                Mathf.Max(
                    0.001f,
                    _burstDuration
                )
            );


        if (t < attack01)
        {
            float attack =
                t /
                Mathf.Max(
                    0.001f,
                    attack01
                );


            return Mathf.Sin(
                attack *
                Mathf.PI *
                0.5f
            );
        }


        // ========================================================
        // HOLD
        // ========================================================

        float holdStart =
            attack01;


        float hold01 =
            Mathf.Clamp01(
                _burstHoldTime /
                Mathf.Max(
                    0.001f,
                    _burstDuration
                )
            );


        float holdEnd =
            Mathf.Clamp(
                holdStart +
                hold01,
                holdStart,
                0.90f
            );


        if (t < holdEnd)
        {
            float hold =
                Mathf.InverseLerp(
                    holdStart,
                    holdEnd,
                    t
                );


            return Mathf.Lerp(
                1f,
                0.94f,
                hold
            );
        }


        // ========================================================
        // SLOW RELEASE
        // ========================================================

        float release =
            Mathf.InverseLerp(
                holdEnd,
                1f,
                t
            );


        return Mathf.Pow(
            1f - release,
            _burstReleasePower
        );
    }


    // ============================================================
    // RECOVERY
    // ============================================================

    private void UpdateRecovery(
        float dt
    )
    {
        if (_recoveryElapsed < 0f)
            return;

        _recoveryElapsed += dt;

        if (_recoveryElapsed >=
            _recoveryDuration)
        {
            _recoveryElapsed = -1f;
        }
    }


    private float GetRecoveryEnvelope()
    {
        if (_recoveryElapsed < 0f)
            return 1f;


        float t =
            Mathf.Clamp01(
                _recoveryElapsed /
                Mathf.Max(
                    0.001f,
                    _recoveryDuration
                )
            );


        // Fish slowly recovers movement.
        return Mathf.Lerp(
            0.15f,
            0.70f,
            t
        );
    }


    // ============================================================
    // SMALL REFLEX SYSTEM
    // ============================================================

    private void UpdateReflex(
        float dt,
        float fatigue,
        float strength
    )
    {
        // --------------------------------------------------------
        // ACTIVE REFLEX
        // --------------------------------------------------------

        if (_reflexElapsed >= 0f)
        {
            _reflexElapsed += dt;

            if (_reflexElapsed >=
                _reflexDuration)
            {
                _reflexElapsed = -1f;

                _reflexTimer =
                    Random.Range(
                        _reflexIntervalMin,
                        _reflexIntervalMax
                    );
            }

            return;
        }


        // --------------------------------------------------------
        // REFLEXES ARE MORE COMMON DURING RECOVERY
        // --------------------------------------------------------

        _reflexTimer -= dt;


        if (_reflexTimer <= 0f)
        {
            if (_recoveryElapsed >= 0f)
            {
                if (Random.value <=
                    _reflexChanceDuringRecovery)
                {
                    StartReflex(
                        fatigue,
                        strength
                    );
                }
                else
                {
                    _reflexTimer =
                        Random.Range(
                            _reflexIntervalMin,
                            _reflexIntervalMax
                        );
                }
            }
            else
            {
                // Small reflexes can also happen outside recovery.
                if (Random.value < 0.35f)
                {
                    StartReflex(
                        fatigue,
                        strength
                    );
                }
                else
                {
                    _reflexTimer =
                        Random.Range(
                            _reflexIntervalMin,
                            _reflexIntervalMax
                        );
                }
            }
        }
    }


    private void StartReflex(
        float fatigue,
        float strength
    )
    {
        _reflexElapsed =
            0f;


        _reflexDuration =
            Random.Range(
                _reflexDurationMin,
                _reflexDurationMax
            );


        _reflexStrength =
            Random.Range(
                _reflexStrengthMin,
                _reflexStrengthMax
            ) *
            fatigue *
            Mathf.Lerp(
                0.75f,
                1.20f,
                strength
            );


        _reflexDirection =
            Random.value <
            0.5f
                ? -1f
                : 1f;
    }


    private float GetReflexEnvelope()
    {
        if (_reflexElapsed < 0f)
            return 0f;


        float t =
            Mathf.Clamp01(
                _reflexElapsed /
                Mathf.Max(
                    0.001f,
                    _reflexDuration
                )
            );


        if (t < 0.25f)
        {
            float attack =
                t /
                0.25f;

            return Mathf.Sin(
                attack *
                Mathf.PI *
                0.5f
            );
        }


        float release =
            Mathf.InverseLerp(
                0.25f,
                1f,
                t
            );


        return Mathf.Pow(
            1f - release,
            1.5f
        );
    }


    // ============================================================
    // FIRST REACTION
    // ============================================================

    private float GetFirstReactionEnvelope()
    {
        if (_firstReactionElapsed < 0f)
            return 0f;


        if (_firstReactionElapsed >
            _firstReactionDuration)
        {
            return 0f;
        }


        float t =
            Mathf.Clamp01(
                _firstReactionElapsed /
                Mathf.Max(
                    0.001f,
                    _firstReactionDuration
                )
            );


        return Mathf.Sin(
            t *
            Mathf.PI
        );
    }


    private void UpdateFirstReaction(
        float dt
    )
    {
        if (_firstReactionElapsed < 0f)
        {
            _firstReactionElapsed += dt;
            return;
        }


        if (_firstReactionElapsed <=
            _firstReactionDuration)
        {
            _firstReactionElapsed += dt;
        }
    }


    // ============================================================
    // FATIGUE
    // ============================================================

    private float GetFatigueMultiplier()
    {
        float elapsed =
            Time.time -
            _spawnTime;


        float decay =
            Mathf.Exp(
                -elapsed /
                Mathf.Max(
                    0.01f,
                    _fatigueTime
                )
            );


        return Mathf.Lerp(
            _minimumFatigue,
            1f,
            decay
        );
    }


    // ============================================================
    // BEND AXIS
    // ============================================================

    private Vector3 GetLocalAxis()
    {
        switch (_bendAxis)
        {
            case BendAxis.LocalX:
                return Vector3.right;

            case BendAxis.LocalY:
                return Vector3.up;

            case BendAxis.LocalZ:
            default:
                return Vector3.forward;
        }
    }


    // ============================================================
    // PUBLIC API
    // ============================================================

    public void SetFishStrength(
        float strength01
    )
    {
        _fishStrength01 =
            Mathf.Clamp01(
                strength01
            );
    }


    public void SetReeling(
        bool active
    )
    {
        _isReeling =
            active;
    }


    public void SetSizeFactor01(
        float size01
    )
    {
        _sizeFactor01 =
            Mathf.Clamp01(
                size01
            );
    }


    public void StartStruggle()
    {
        _struggleEnabled = true;

        if (_useAnimator && _animator != null)
        {
            // This fish has only ONE struggle animation.
            // No Bool parameter, Idle state, or transition is required.
            _animator.enabled = true;

            // Restart the single animation from the beginning.
            // The struggle animation should be the Animator's default state.
            _animator.Play(0, 0, 0f);
            _animator.Update(0f);
        }
    }


    public void StopStruggle()
    {
        _struggleEnabled = false;

        if (_useAnimator && _animator != null)
        {
            // Stop the single animation by disabling the Animator.
            _animator.enabled = false;
        }

        RestorePose();
    }


    public void ForceRebuildBones()
    {
        RestorePose();

        CacheBones();
    }


    public void RestorePose()
    {
        for (int i = 0; i < _bones.Count; i++)
        {
            BoneState bone =
                _bones[i];

            if (bone.Transform != null)
            {
                bone.Transform.localRotation =
                    bone.InitialLocalRotation;
            }
        }


        if (_smoothedLocalAngles != null)
        {
            for (
                int i = 0;
                i < _smoothedLocalAngles.Length;
                i++
            )
            {
                _smoothedLocalAngles[i] =
                    0f;
            }
        }
    }
}