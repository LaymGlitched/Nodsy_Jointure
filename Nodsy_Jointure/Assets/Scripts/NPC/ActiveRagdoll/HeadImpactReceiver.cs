using System;
using UnityEngine;

namespace Jointure.NPC.ActiveRagdoll
{
    public class HeadImpactReceiver : MonoBehaviour
    {
        public Rigidbody headRigidbody;
        public KnockoutController knockoutController;
        public ActiveRagdollRig rig;
        public float flinchImpulseThreshold = 5f;
        public float knockoutImpulseThreshold = 35f;
        public float knockoutVelocityThreshold = 8f;
        public LayerMask ignoredLayers = 0;

        public event Action<float, Vector3> OnHeadImpact;

        private void Awake()
        {
            if (headRigidbody == null) headRigidbody = GetComponent<Rigidbody>();
            if (knockoutController == null) knockoutController = GetComponentInParent<KnockoutController>();
            if (rig == null) rig = GetComponentInParent<ActiveRagdollRig>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (((1 << collision.gameObject.layer) & ignoredLayers.value) != 0) return;

            float impulse = collision.impulse.magnitude;
            float relVel = collision.relativeVelocity.magnitude;

            if (impulse >= flinchImpulseThreshold || relVel >= 3f)
            {
                if (rig != null)
                {
                    rig.TriggerFlinch(0.2f, 0.2f);
                }
            }

            if (impulse >= knockoutImpulseThreshold || relVel >= knockoutVelocityThreshold)
            {
                Vector3 contactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
                OnHeadImpact?.Invoke(impulse, contactPoint);

                if (knockoutController != null)
                {
                    knockoutController.TriggerKnockout(collision.impulse, contactPoint);
                }
            }
        }
    }
}
