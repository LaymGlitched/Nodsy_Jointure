using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jointure.NPC.ActiveRagdoll
{
    [Serializable]
    public class RagdollBoneData
    {
        public Rigidbody boneRigidbody;
        public ConfigurableJoint boneJoint;
        public Transform animatedTargetBone;
        public float positionSpring = 2500f;
        public float positionDamper = 250f;
        public float maxForce = 6000f;

        [HideInInspector] public Quaternion initialLocalRotation = Quaternion.identity;
        [HideInInspector] public Quaternion charToBoneRest = Quaternion.identity;

        public void Initialize(Transform characterRoot = null)
        {
            if (boneRigidbody != null)
            {
                initialLocalRotation = boneRigidbody.transform.localRotation;
                if (characterRoot != null)
                {
                    charToBoneRest = Quaternion.Inverse(boneRigidbody.transform.rotation) * characterRoot.rotation;
                }
            }

            if (boneJoint != null)
            {
                boneJoint.rotationDriveMode = RotationDriveMode.Slerp;
                boneJoint.projectionMode = JointProjectionMode.PositionAndRotation;
                boneJoint.projectionDistance = 0.005f;
                boneJoint.projectionAngle = 5f;
                boneJoint.xMotion = ConfigurableJointMotion.Locked;
                boneJoint.yMotion = ConfigurableJointMotion.Locked;
                boneJoint.zMotion = ConfigurableJointMotion.Locked;
                boneJoint.targetRotation = Quaternion.identity;
                SetDriveStrength(1f);
            }
        }

        public void SetDriveStrength(float multiplier)
        {
            if (boneJoint == null) return;

            JointDrive drive = new JointDrive
            {
                positionSpring = positionSpring * multiplier,
                positionDamper = positionDamper * multiplier,
                maximumForce = maxForce * multiplier
            };
            boneJoint.slerpDrive = drive;
        }

        public void SetTargetRotation(Quaternion targetLocalRotation)
        {
            if (boneJoint == null) return;
            boneJoint.targetRotation = Quaternion.Inverse(targetLocalRotation);
        }

        /// <summary>
        /// Sets the joint target rotation using a desired rotation expressed in Character Root coordinates
        /// (Right = +X / Sagittal Pitch, Up = +Y / Transverse Yaw, Forward = +Z / Coronal Roll).
        /// Invariant to FBX bone axis rotations and mirroring.
        /// </summary>
        public void SetTargetRotationCharacterSpace(Quaternion charSpaceRotation)
        {
            if (boneJoint == null) return;
            Quaternion deltaLocal = charToBoneRest * charSpaceRotation * Quaternion.Inverse(charToBoneRest);
            boneJoint.targetRotation = Quaternion.Inverse(deltaLocal);
        }

        public void ResetTargetRotation()
        {
            if (boneJoint == null) return;
            boneJoint.targetRotation = Quaternion.identity;
        }

        public void FollowAnimatedTarget()
        {
            if (boneJoint == null || animatedTargetBone == null) return;
            // The difference between the animated bone's current localRotation and its rest localRotation:
            Quaternion animatedDelta = Quaternion.Inverse(initialLocalRotation) * animatedTargetBone.localRotation;
            boneJoint.targetRotation = Quaternion.Inverse(animatedDelta);
        }
    }

    [DefaultExecutionOrder(-100)]
    public class ActiveRagdollRig : MonoBehaviour
    {
        [Header("Root & Core Bones")]
        public Rigidbody pelvisRigidbody;
        public RagdollBoneData spineBone;
        public RagdollBoneData chestBone;
        public RagdollBoneData headBone;

        [Header("Leg Bones")]
        public RagdollBoneData leftThighBone;
        public RagdollBoneData leftCalfBone;
        public RagdollBoneData leftFootBone;
        public RagdollFootContact leftFootContact;
        public RagdollBoneData rightThighBone;
        public RagdollBoneData rightCalfBone;
        public RagdollBoneData rightFootBone;
        public RagdollFootContact rightFootContact;

        [Header("Arm Bones")]
        public RagdollBoneData leftUpperArmBone;
        public RagdollBoneData leftForearmBone;
        public RagdollBoneData rightUpperArmBone;
        public RagdollBoneData rightForearmBone;

        [Header("Balance & Stability (Bonelab Style)")]
        public float uprightKp = 90f;
        public float uprightKd = 38f;
        public float maxUprightTorque = 300f;
        public float turnKp = 40f;
        public float turnKd = 18f;
        public float maxTurnTorque = 150f;
        public float targetPelvisHeight = 0.82f;
        public float heightSpring = 18f;
        public float heightDamper = 14f;
        public float maxSuspensionAcceleration = 8f;
        public float horizontalDamping = 1.8f;
        public float maxBalanceTiltAngle = 55f;
        public float maxAirborneTimeBeforeUnbalanced = 0.5f;
        public LayerMask groundLayer = -1;

        [Header("Inverted Pendulum Self-Balancing")]
        public float ankleBalanceKp = 35f;
        public float ankleBalanceKd = 10f;
        public float maxAnkleBalanceAngle = 18f;
        public float hipBalanceKp = 15f;
        public float hipBalanceKd = 6f;
        public float maxHipBalanceAngle = 14f;
        [Range(0f, 1f)] public float gravityAssistFactor = 0.5f;
        public float stanceSupportSpring = 24f;
        public float stanceSupportDamper = 14f;

        [Header("Animation Driving (Mecanim / Shadow Skeleton)")]
        public Animator targetAnimator;
        public bool useAnimationDriving = true;
        [Range(0f, 1f)] public float animationDriveStrength = 1f;

        public Vector3 TargetHeading { get; set; } = Vector3.forward;
        public float RollAngleOffset { get; set; } = 0f;
        public float StrengthMultiplier { get; private set; } = 1f;
        public bool IsBalanced { get; private set; } = true;
        public bool IsGrounded { get; private set; } = true;
        public float TotalMass => _totalRagdollMass;
        public RagdollLocomotion Locomotion { get; private set; }

        public float SagittalLean { get; private set; }
        public float CoronalLean { get; private set; }
        public float SagittalVelocity { get; private set; }
        public float CoronalVelocity { get; private set; }
        public Vector3 FeetCenter { get; private set; }
        public bool IsLeftGrounded => leftFootContact != null && leftFootContact.IsGrounded;
        public bool IsRightGrounded => rightFootContact != null && rightFootContact.IsGrounded;

        public Quaternion AnatomicalRotation => pelvisRigidbody != null ? pelvisRigidbody.transform.rotation * _pelvisToCharacterRot : transform.rotation;
        public Vector3 AnatomicalUp => AnatomicalRotation * Vector3.up;
        public Vector3 AnatomicalForward => AnatomicalRotation * Vector3.forward;
        public Vector3 AnatomicalRight => AnatomicalRotation * Vector3.right;

        public event Action<bool> OnBalanceChanged;

        private Quaternion _pelvisToCharacterRot = Quaternion.identity;
        private RagdollBoneData[] _allBones;
        private float _airborneTimer;
        private float _flinchTimer;
        private float _flinchStrength = 1f;
        private float _groundDistance = 0.82f;
        private float _totalRagdollMass = 82f;

        private void Awake()
        {
            // Auto-heal foot joint connections if disconnected
            if (leftFootBone?.boneJoint != null && leftFootBone.boneJoint.connectedBody == null && leftCalfBone?.boneRigidbody != null)
            {
                leftFootBone.boneJoint.connectedBody = leftCalfBone.boneRigidbody;
            }
            if (rightFootBone?.boneJoint != null && rightFootBone.boneJoint.connectedBody == null && rightCalfBone?.boneRigidbody != null)
            {
                rightFootBone.boneJoint.connectedBody = rightCalfBone.boneRigidbody;
            }

            // Auto-heal foot contact references if unassigned
            if (leftFootContact == null && leftFootBone?.boneRigidbody != null)
            {
                leftFootContact = leftFootBone.boneRigidbody.GetComponent<RagdollFootContact>();
                if (leftFootContact == null) leftFootContact = leftFootBone.boneRigidbody.gameObject.AddComponent<RagdollFootContact>();
            }
            if (rightFootContact == null && rightFootBone?.boneRigidbody != null)
            {
                rightFootContact = rightFootBone.boneRigidbody.GetComponent<RagdollFootContact>();
                if (rightFootContact == null) rightFootContact = rightFootBone.boneRigidbody.gameObject.AddComponent<RagdollFootContact>();
            }

            if (pelvisRigidbody != null)
            {
                _pelvisToCharacterRot = Quaternion.Inverse(pelvisRigidbody.transform.rotation) * transform.rotation;
            }

            CollectAllBones();

            foreach (RagdollBoneData bone in _allBones)
            {
                bone.Initialize(transform);
                if (bone.boneRigidbody != null)
                {
                    bone.boneRigidbody.solverIterations = 25;
                    bone.boneRigidbody.solverVelocityIterations = 12;
                    bone.boneRigidbody.maxDepenetrationVelocity = 8f;
                }
            }

            if (pelvisRigidbody != null)
            {
                pelvisRigidbody.solverIterations = 25;
                pelvisRigidbody.solverVelocityIterations = 12;
                pelvisRigidbody.maxDepenetrationVelocity = 8f;
                TargetHeading = AnatomicalForward;
            }

            // Calculate total ragdoll mass across all rigidbodies
            _totalRagdollMass = 0f;
            Rigidbody[] allRbs = GetComponentsInChildren<Rigidbody>();
            for (int i = 0; i < allRbs.Length; i++)
            {
                _totalRagdollMass += allRbs[i].mass;
            }
            if (_totalRagdollMass < 5f) _totalRagdollMass = 82f;

            Locomotion = GetComponent<RagdollLocomotion>();
            if (targetAnimator != null)
            {
                BindAnimatedTarget(targetAnimator.transform);
            }

            // Self-collision ignoring across the ragdoll
            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                for (int j = i + 1; j < colliders.Length; j++)
                {
                    Physics.IgnoreCollision(colliders[i], colliders[j], true);
                }
            }
        }

        public void CollectAllBones()
        {
            List<RagdollBoneData> boneList = new List<RagdollBoneData>();
            AddBoneIfValid(boneList, spineBone);
            AddBoneIfValid(boneList, chestBone);
            AddBoneIfValid(boneList, headBone);
            AddBoneIfValid(boneList, leftThighBone);
            AddBoneIfValid(boneList, leftCalfBone);
            AddBoneIfValid(boneList, leftFootBone);
            AddBoneIfValid(boneList, rightThighBone);
            AddBoneIfValid(boneList, rightCalfBone);
            AddBoneIfValid(boneList, rightFootBone);
            AddBoneIfValid(boneList, leftUpperArmBone);
            AddBoneIfValid(boneList, leftForearmBone);
            AddBoneIfValid(boneList, rightUpperArmBone);
            AddBoneIfValid(boneList, rightForearmBone);
            _allBones = boneList.ToArray();
        }

        public void BindAnimatedTarget(Transform targetRoot)
        {
            if (targetRoot == null) return;
            if (_allBones == null || _allBones.Length == 0)
            {
                CollectAllBones();
            }

            Transform[] targetTransforms = targetRoot.GetComponentsInChildren<Transform>(true);
            Dictionary<string, Transform> map = new Dictionary<string, Transform>();
            for (int i = 0; i < targetTransforms.Length; i++)
            {
                if (!map.ContainsKey(targetTransforms[i].name))
                {
                    map.Add(targetTransforms[i].name, targetTransforms[i]);
                }
            }

            for (int i = 0; i < _allBones.Length; i++)
            {
                if (_allBones[i].boneRigidbody != null && map.TryGetValue(_allBones[i].boneRigidbody.gameObject.name, out Transform targetBone))
                {
                    _allBones[i].animatedTargetBone = targetBone;
                }
            }
        }

        private void Start()
        {
            // Set initial standing natural stance (arms down at sides, NO T-POSE)
            ResetAllTargetRotations();
        }

        private void AddBoneIfValid(List<RagdollBoneData> list, RagdollBoneData bone)
        {
            if (bone != null && (bone.boneJoint != null || bone.boneRigidbody != null))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].boneRigidbody == bone.boneRigidbody) return;
                }
                list.Add(bone);
            }
        }

        public void SetJointStrengthMultiplier(float multiplier)
        {
            StrengthMultiplier = Mathf.Clamp01(multiplier);
            ApplyCurrentDriveStrength();
        }

        public void TriggerFlinch(float duration = 0.18f, float strength = 0.25f)
        {
            _flinchTimer = duration;
            _flinchStrength = strength;
            ApplyCurrentDriveStrength();
        }

        private void ApplyCurrentDriveStrength()
        {
            if (_allBones == null) return;
            float eff = StrengthMultiplier;
            if (_flinchTimer > 0f)
            {
                eff *= _flinchStrength;
            }
            for (int i = 0; i < _allBones.Length; i++)
            {
                _allBones[i].SetDriveStrength(eff);
            }
        }

        public void ResetAllTargetRotations()
        {
            ApplyStandingSelfBalance();
        }

        public void ApplyStandingSelfBalance()
        {
            if (_allBones == null || pelvisRigidbody == null) return;

            // 1. Natural relaxed standing idle arms (arms down at sides, NO T-POSE)
            leftUpperArmBone?.SetTargetRotationCharacterSpace(Quaternion.AngleAxis(75f, Vector3.forward));
            rightUpperArmBone?.SetTargetRotationCharacterSpace(Quaternion.AngleAxis(-75f, Vector3.forward));
            leftForearmBone?.SetTargetRotationCharacterSpace(Quaternion.AngleAxis(15f, Vector3.right));
            rightForearmBone?.SetTargetRotationCharacterSpace(Quaternion.AngleAxis(15f, Vector3.right));

            // 2. Trunk Pitch & Roll Stabilization via Hip Motors (internal joint torque between thighs & pelvis)
            Vector3 flatFwd = Vector3.ProjectOnPlane(AnatomicalForward, Vector3.up);
            if (flatFwd.sqrMagnitude > 0.001f) flatFwd.Normalize(); else flatFwd = Vector3.forward;
            Vector3 flatRight = Vector3.ProjectOnPlane(AnatomicalRight, Vector3.up);
            if (flatRight.sqrMagnitude > 0.001f) flatRight.Normalize(); else flatRight = Vector3.right;

            // Forward/backward pelvis tilt angle
            float pelvisPitch = Vector3.SignedAngle(flatFwd, AnatomicalForward, flatRight);
            float pitchAngVel = Vector3.Dot(pelvisRigidbody.angularVelocity, flatRight);
            float hipTrunkPitch = (pelvisPitch * hipBalanceKp) + (pitchAngVel * hipBalanceKd);
            hipTrunkPitch = Mathf.Clamp(hipTrunkPitch, -maxHipBalanceAngle, maxHipBalanceAngle);

            // Lateral pelvis roll angle
            float pelvisRoll = Vector3.SignedAngle(flatRight, AnatomicalRight, flatFwd);
            float rollAngVel = Vector3.Dot(pelvisRigidbody.angularVelocity, flatFwd);
            float hipTrunkRoll = (pelvisRoll * hipBalanceKp * 0.5f) + (rollAngVel * hipBalanceKd * 0.5f);
            hipTrunkRoll = Mathf.Clamp(hipTrunkRoll, -10f, 10f);

            // 3. Pelvis Heading Alignment via Hip Yaw (turns pelvis towards TargetHeading via leg joints)
            Vector3 flatHeading = Vector3.ProjectOnPlane(TargetHeading, Vector3.up);
            float yawDiff = 0f;
            if (flatHeading.sqrMagnitude > 0.001f)
            {
                yawDiff = Mathf.Clamp(Vector3.SignedAngle(flatFwd, flatHeading.normalized, Vector3.up) * 0.4f, -25f, 25f);
            }

            // 4. Inverted Pendulum Ankle Ground-Reaction Drive (pushes shoe soles into floor to restore balance)
            // Positive anklePitch tilts shin backward (plantarflexion), negative tilts shin forward (dorsiflexion).
            // When SagittalLean > 0 (leaning forward), push shin backward (positive pitch).
            // When SagittalLean < 0 (leaning backward), pull shin forward (negative pitch).
            float anklePitch = (SagittalLean * ankleBalanceKp) + (SagittalVelocity * ankleBalanceKd);
            anklePitch = Mathf.Clamp(anklePitch, -maxAnkleBalanceAngle, maxAnkleBalanceAngle);

            // If CoronalLean > 0 (leaning right), roll left (negative roll around character forward):
            float ankleRoll = -(CoronalLean * ankleBalanceKp * 0.35f) - (CoronalVelocity * ankleBalanceKd * 0.35f);
            ankleRoll = Mathf.Clamp(ankleRoll, -8f, 8f);

            // 5. Knee Height Support (keeps knees near-extended as vertical support pillars)
            float heightError = targetPelvisHeight - _groundDistance;
            float kneeAngle = Mathf.Clamp(3.5f - heightError * 10f, 1.5f, 8f);

            // Set Left Leg Target Rotations in Character Space
            Quaternion hipRot = Quaternion.AngleAxis(hipTrunkPitch, Vector3.right)
                              * Quaternion.AngleAxis(yawDiff, Vector3.up)
                              * Quaternion.AngleAxis(-hipTrunkRoll, Vector3.forward);

            leftThighBone?.SetTargetRotationCharacterSpace(hipRot);
            leftCalfBone?.SetTargetRotationCharacterSpace(Quaternion.AngleAxis(kneeAngle, Vector3.right));
            leftFootBone?.SetTargetRotationCharacterSpace(Quaternion.AngleAxis(anklePitch, Vector3.right) * Quaternion.AngleAxis(ankleRoll, Vector3.forward));

            // Set Right Leg Target Rotations in Character Space
            rightThighBone?.SetTargetRotationCharacterSpace(hipRot);
            rightCalfBone?.SetTargetRotationCharacterSpace(Quaternion.AngleAxis(kneeAngle, Vector3.right));
            rightFootBone?.SetTargetRotationCharacterSpace(Quaternion.AngleAxis(anklePitch, Vector3.right) * Quaternion.AngleAxis(ankleRoll, Vector3.forward));

            // 6. Core posture
            spineBone?.ResetTargetRotation();
            chestBone?.ResetTargetRotation();
            headBone?.ResetTargetRotation();
        }

        private void Update()
        {
            if (_flinchTimer > 0f)
            {
                _flinchTimer -= Time.deltaTime;
                if (_flinchTimer <= 0f)
                {
                    ApplyCurrentDriveStrength();
                }
            }
        }

        private void FixedUpdate()
        {
            if (pelvisRigidbody == null || StrengthMultiplier <= 0.001f) return;

            UpdateBalanceState();

            // Check if animation driving is active
            bool isAnimationActive = useAnimationDriving 
                && targetAnimator != null 
                && targetAnimator.runtimeAnimatorController != null 
                && targetAnimator.enabled 
                && StrengthMultiplier > 0.05f;

            if (isAnimationActive && _allBones != null)
            {
                for (int i = 0; i < _allBones.Length; i++)
                {
                    _allBones[i].FollowAnimatedTarget();
                }
            }
            else if (Locomotion == null || !Locomotion.IsMoving)
            {
                // Pure joint-driven active self-balancing when standing idle (ZERO artificial forces or torques)
                ApplyStandingSelfBalance();
            }
        }

        private void UpdateBalanceState()
        {
            float tiltAngle = Vector3.Angle(AnatomicalUp, Vector3.up);
            bool leftG = leftFootContact != null && leftFootContact.IsGrounded;
            bool rightG = rightFootContact != null && rightFootContact.IsGrounded;
            bool groundedByFeet = leftG || rightG;

            if (Physics.Raycast(pelvisRigidbody.position, Vector3.down, out RaycastHit hit, 5f, groundLayer, QueryTriggerInteraction.Ignore))
            {
                _groundDistance = hit.distance;
            }
            else
            {
                _groundDistance = targetPelvisHeight;
            }

            bool groundedByRay = _groundDistance <= targetPelvisHeight * 1.35f && _groundDistance > 0.01f;

            IsGrounded = groundedByFeet || groundedByRay;

            if (IsGrounded)
            {
                _airborneTimer = 0f;
            }
            else
            {
                _airborneTimer += Time.fixedDeltaTime;
            }

            bool newBalance = tiltAngle < maxBalanceTiltAngle && _airborneTimer < maxAirborneTimeBeforeUnbalanced;
            if (newBalance != IsBalanced)
            {
                IsBalanced = newBalance;
                OnBalanceChanged?.Invoke(IsBalanced);
            }

            // Calculate Base of Support (FeetCenter) and Center of Mass offset
            Vector3 pelvisPos = pelvisRigidbody.position;
            Vector3 leftFootPos = (leftFootContact != null && leftFootContact.IsGrounded) 
                ? leftFootContact.ContactPoint 
                : (leftFootBone?.boneRigidbody != null ? leftFootBone.boneRigidbody.position : pelvisPos);
            Vector3 rightFootPos = (rightFootContact != null && rightFootContact.IsGrounded) 
                ? rightFootContact.ContactPoint 
                : (rightFootBone?.boneRigidbody != null ? rightFootBone.boneRigidbody.position : pelvisPos);

            if (leftG && rightG)
            {
                FeetCenter = (leftFootPos + rightFootPos) * 0.5f;
            }
            else if (leftG)
            {
                FeetCenter = leftFootPos;
            }
            else if (rightG)
            {
                FeetCenter = rightFootPos;
            }
            else
            {
                FeetCenter = (leftFootPos + rightFootPos) * 0.5f;
            }

            Vector3 horizOffset = pelvisPos - FeetCenter;
            horizOffset.y = 0f;

            Vector3 flatFwd = Vector3.ProjectOnPlane(AnatomicalForward, Vector3.up);
            if (flatFwd.sqrMagnitude > 0.001f) flatFwd.Normalize(); else flatFwd = Vector3.forward;

            Vector3 flatRight = Vector3.ProjectOnPlane(AnatomicalRight, Vector3.up);
            if (flatRight.sqrMagnitude > 0.001f) flatRight.Normalize(); else flatRight = Vector3.right;

            SagittalLean = Vector3.Dot(horizOffset, flatFwd);
            CoronalLean = Vector3.Dot(horizOffset, flatRight);

            Vector3 pelvisVel = pelvisRigidbody.linearVelocity;
            SagittalVelocity = Vector3.Dot(pelvisVel, flatFwd);
            CoronalVelocity = Vector3.Dot(pelvisVel, flatRight);
        }
    }
}
