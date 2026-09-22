using UnityEngine;

namespace Jointure.NPC.ActiveRagdoll
{
    [DefaultExecutionOrder(-50)]
    public class RagdollBone : MonoBehaviour
    {
        [HideInInspector] public ActiveRagdollRig Rig;

        public ConfigurableJoint Joint { get; private set; }
        public Rigidbody Rb { get; private set; }
        public Quaternion InitialLocalRotation { get; private set; }

        private float _baseSpring;
        private float _baseDamper;

        private void Awake()
        {
            Rb = GetComponent<Rigidbody>();
            Joint = GetComponent<ConfigurableJoint>();
            InitialLocalRotation = transform.localRotation;

            if (Joint != null)
            {
                JointDrive drive = Joint.slerpDrive;
                _baseSpring = drive.positionSpring;
                _baseDamper = drive.positionDamper;
            }

            if (Rb != null)
            {
                Rb.solverIterations = 12;
                Rb.solverVelocityIterations = 6;
            }
        }

        public void SetDriveStrength(float normalizedStrength)
        {
            if (Joint == null) return;
            float clamped = Mathf.Clamp01(normalizedStrength);
            JointDrive drive = Joint.slerpDrive;
            drive.positionSpring = _baseSpring * clamped;
            drive.positionDamper = _baseDamper * clamped;
            Joint.slerpDrive = drive;
        }

        public void SetTargetRotation(Quaternion target)
        {
            if (Joint == null) return;
            Joint.targetRotation = target;
        }

        public void ResetTargetRotation()
        {
            if (Joint == null) return;
            Joint.targetRotation = Quaternion.identity;
        }

        public void OverrideBaseDriveValues(float spring, float damper)
        {
            _baseSpring = spring;
            _baseDamper = damper;
            SetDriveStrength(Rig != null ? Rig.StrengthMultiplier : 1f);
        }
    }
}
