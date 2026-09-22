using UnityEngine;

namespace Jointure.NPC.ActiveRagdoll
{
    public class RagdollLocomotion : MonoBehaviour
    {
        public ActiveRagdollRig rig;

        [Header("Gait Tuning")]
        public float stepFrequency = 1.6f;
        public float maxSwingThighAngle = 26f;
        public float maxSwingKneeAngle = 52f;
        public float maxSwingAnkleAngle = 14f;
        public float maxArmAngle = 22f;
        public float pelvisRollSway = 2.8f;
        public float stancePushForce = 260f;
        public float maxWalkSpeed = 1.35f;

        [Header("Attack Tuning (Bonelab Style)")]
        public float attackWindupTime = 0.25f;
        public float attackStrikeTime = 0.35f;
        public float attackCooldown = 1.2f;

        public Vector3 MoveDirection { get; set; }
        public float SpeedMultiplier { get; set; } = 1f;
        public bool IsMoving => MoveDirection.sqrMagnitude > 0.01f;
        public bool IsAttacking => _attackState != AttackState.Idle;

        public enum AttackState { Idle, Windup, Strike, Recovery }
        private AttackState _attackState = AttackState.Idle;
        private float _attackTimer;
        private int _attackArmIndex; // 0 = right, 1 = left
        private float _lastAttackTime = -10f;

        private float _stepPhase;

        private void Awake()
        {
            if (rig == null)
            {
                rig = GetComponent<ActiveRagdollRig>();
            }
        }

        public bool CanAttack()
        {
            return _attackState == AttackState.Idle && Time.time >= _lastAttackTime + attackCooldown;
        }

        public void TriggerAttack(int armPreference = -1)
        {
            if (!CanAttack()) return;

            _attackArmIndex = armPreference >= 0 ? armPreference : Random.Range(0, 2);
            _attackState = AttackState.Windup;
            _attackTimer = attackWindupTime;
        }

        private void FixedUpdate()
        {
            if (rig == null || !rig.enabled || rig.StrengthMultiplier <= 0.001f) return;

            UpdateAttackState();

            // When unbalanced or knocked down, return to resting pose without forcing walk cycle
            if (!rig.IsBalanced || !rig.IsGrounded)
            {
                rig.RollAngleOffset = 0f;
                if (!IsAttacking)
                {
                    rig.ResetAllTargetRotations();
                }
                return;
            }

            if (!IsMoving && !IsAttacking)
            {
                rig.RollAngleOffset = 0f;
                // Natural standing idle pose with active closed-loop self-balancing
                if (rig.targetAnimator == null || rig.targetAnimator.runtimeAnimatorController == null || !rig.useAnimationDriving)
                {
                    rig.ApplyStandingSelfBalance();
                }
                return;
            }

            if (IsMoving)
            {
                _stepPhase += Time.fixedDeltaTime * stepFrequency * SpeedMultiplier * Mathf.PI * 2f;
                if (_stepPhase > Mathf.PI * 2f)
                {
                    _stepPhase -= Mathf.PI * 2f;
                }

                Vector3 pushDir = MoveDirection.normalized;
                rig.TargetHeading = pushDir;

                bool animationDriving = rig.useAnimationDriving 
                    && rig.targetAnimator != null 
                    && rig.targetAnimator.runtimeAnimatorController != null 
                    && rig.targetAnimator.enabled;

                // Step phases: 0 to PI = Left Stance / Right Swing; PI to 2PI = Right Stance / Left Swing
                bool leftIsStance = _stepPhase < Mathf.PI;
                float progress = leftIsStance ? (_stepPhase / Mathf.PI) : ((_stepPhase - Mathf.PI) / Mathf.PI);

                if (!animationDriving)
                {
                    // Balance offset trims from inverted pendulum
                    float anklePitchBal = Mathf.Clamp(rig.SagittalLean * rig.ankleBalanceKp, -rig.maxAnkleBalanceAngle, rig.maxAnkleBalanceAngle);

                    // Heading turn angle handled through internal hip yaw
                    Vector3 currentFwd = Vector3.ProjectOnPlane(rig.AnatomicalForward, Vector3.up);
                    if (currentFwd.sqrMagnitude > 0.001f) currentFwd.Normalize(); else currentFwd = Vector3.forward;
                    float turnAngle = Vector3.SignedAngle(currentFwd, pushDir, Vector3.up);
                    float hipYaw = Mathf.Clamp(turnAngle * 0.45f, -25f, 25f);

                    // Pelvis lateral weight shift via hip roll
                    float hipRoll = (leftIsStance ? 1f : -1f) * Mathf.Sin(progress * Mathf.PI) * pelvisRollSway;

                    if (leftIsStance)
                    {
                        // === LEFT LEG: STANCE (Pushes against ground to propel body forward, zero moonwalk) ===
                        float stanceThigh = Mathf.Lerp(-10f, 18f, progress);
                        float pushOff = Mathf.Lerp(0f, 12f, Mathf.Clamp01((progress - 0.6f) / 0.4f));

                        Quaternion leftThighRot = Quaternion.AngleAxis(stanceThigh, Vector3.right)
                                                * Quaternion.AngleAxis(hipYaw, Vector3.up)
                                                * Quaternion.AngleAxis(-hipRoll, Vector3.forward);
                        rig.leftThighBone?.SetTargetRotationCharacterSpace(leftThighRot);
                        rig.leftCalfBone?.SetTargetRotationCharacterSpace(Quaternion.AngleAxis(4f, Vector3.right));
                        rig.leftFootBone?.SetTargetRotationCharacterSpace(Quaternion.AngleAxis(anklePitchBal + pushOff, Vector3.right));

                        // === RIGHT LEG: SWING (Lifts high, swings forward through air, plants ahead) ===
                        float swingKnee = Mathf.Sin(progress * Mathf.PI) * maxSwingKneeAngle + 4f;
                        float swingThigh = Mathf.Lerp(12f, -maxSwingThighAngle, progress);
                        float swingAnkle = -Mathf.Sin(progress * Mathf.PI) * 8f;

                        Quaternion rightThighRot = Quaternion.AngleAxis(swingThigh, Vector3.right)
                                                 * Quaternion.AngleAxis(-hipYaw, Vector3.up)
                                                 * Quaternion.AngleAxis(-hipRoll, Vector3.forward);
                        rig.rightThighBone?.SetTargetRotationCharacterSpace(rightThighRot);
                        rig.rightCalfBone?.SetTargetRotationCharacterSpace(Quaternion.AngleAxis(swingKnee, Vector3.right));
                        rig.rightFootBone?.SetTargetRotationCharacterSpace(Quaternion.AngleAxis(swingAnkle, Vector3.right));
                    }
                    else
                    {
                        // === RIGHT LEG: STANCE (Pushes against ground to propel body forward, zero moonwalk) ===
                        float stanceThigh = Mathf.Lerp(-10f, 18f, progress);
                        float pushOff = Mathf.Lerp(0f, 12f, Mathf.Clamp01((progress - 0.6f) / 0.4f));

                        Quaternion rightThighRot = Quaternion.AngleAxis(stanceThigh, Vector3.right)
                                                 * Quaternion.AngleAxis(-hipYaw, Vector3.up)
                                                 * Quaternion.AngleAxis(-hipRoll, Vector3.forward);
                        rig.rightThighBone?.SetTargetRotationCharacterSpace(rightThighRot);
                        rig.rightCalfBone?.SetTargetRotationCharacterSpace(Quaternion.AngleAxis(4f, Vector3.right));
                        rig.rightFootBone?.SetTargetRotationCharacterSpace(Quaternion.AngleAxis(anklePitchBal + pushOff, Vector3.right));

                        // === LEFT LEG: SWING (Lifts high, swings forward through air, plants ahead) ===
                        float swingKnee = Mathf.Sin(progress * Mathf.PI) * maxSwingKneeAngle + 4f;
                        float swingThigh = Mathf.Lerp(12f, -maxSwingThighAngle, progress);
                        float swingAnkle = -Mathf.Sin(progress * Mathf.PI) * 8f;

                        Quaternion leftThighRot = Quaternion.AngleAxis(swingThigh, Vector3.right)
                                                * Quaternion.AngleAxis(hipYaw, Vector3.up)
                                                * Quaternion.AngleAxis(-hipRoll, Vector3.forward);
                        rig.leftThighBone?.SetTargetRotationCharacterSpace(leftThighRot);
                        rig.leftCalfBone?.SetTargetRotationCharacterSpace(Quaternion.AngleAxis(swingKnee, Vector3.right));
                        rig.leftFootBone?.SetTargetRotationCharacterSpace(Quaternion.AngleAxis(swingAnkle, Vector3.right));
                    }

                    // --- Upper Body Natural Counter Arm Sway ---
                    if (!IsAttacking)
                    {
                        float armSway = (leftIsStance ? 1f : -1f) * Mathf.Sin(progress * Mathf.PI) * maxArmAngle;

                        rig.leftUpperArmBone?.SetTargetRotationCharacterSpace(Quaternion.AngleAxis(75f, Vector3.forward) * Quaternion.AngleAxis(armSway, Vector3.right));
                        rig.rightUpperArmBone?.SetTargetRotationCharacterSpace(Quaternion.AngleAxis(-75f, Vector3.forward) * Quaternion.AngleAxis(-armSway, Vector3.right));

                        rig.leftForearmBone?.SetTargetRotationCharacterSpace(Quaternion.AngleAxis(15f, Vector3.right));
                        rig.rightForearmBone?.SetTargetRotationCharacterSpace(Quaternion.AngleAxis(15f, Vector3.right));
                    }
                }
            }
        }

        private void UpdateAttackState()
        {
            if (_attackState == AttackState.Idle) return;

            _attackTimer -= Time.fixedDeltaTime;
            RagdollBoneData strikeArm = _attackArmIndex == 0 ? rig.rightUpperArmBone : rig.leftUpperArmBone;
            RagdollBoneData strikeForearm = _attackArmIndex == 0 ? rig.rightForearmBone : rig.leftForearmBone;

            switch (_attackState)
            {
                case AttackState.Windup:
                    // Cock arm back
                    strikeArm?.SetTargetRotation(Quaternion.Euler(_attackArmIndex == 0 ? 30f : -30f, -40f, 0f));
                    strikeForearm?.SetTargetRotation(Quaternion.Euler(0f, 0f, _attackArmIndex == 0 ? -70f : 70f));
                    if (_attackTimer <= 0f)
                    {
                        _attackState = AttackState.Strike;
                        _attackTimer = attackStrikeTime;
                    }
                    break;

                case AttackState.Strike:
                    // Punch forward vigorously towards target heading
                    strikeArm?.SetTargetRotation(Quaternion.Euler(_attackArmIndex == 0 ? -45f : 45f, 50f, 0f));
                    strikeForearm?.SetTargetRotation(Quaternion.Euler(0f, 0f, _attackArmIndex == 0 ? -15f : 15f));

                    if (_attackTimer <= 0f)
                    {
                        _attackState = AttackState.Recovery;
                        _attackTimer = 0.2f;
                        _lastAttackTime = Time.time;
                    }
                    break;

                case AttackState.Recovery:
                    strikeArm?.ResetTargetRotation();
                    strikeForearm?.ResetTargetRotation();
                    if (_attackTimer <= 0f)
                    {
                        _attackState = AttackState.Idle;
                    }
                    break;
            }
        }
    }
}
