using System;
using UnityEngine;

namespace Jointure.NPC.ActiveRagdoll
{
    [DefaultExecutionOrder(-50)]
    public class ActiveRagdollNPC : MonoBehaviour
    {
        public ActiveRagdollRig rig;
        public RagdollLocomotion locomotion;
        public KnockoutController knockoutController;
        public NPCBrain brain;
        public NPCFactionMember factionMember;
        public HeadImpactReceiver headImpactReceiver;
        public float maxHealth = 100f;
        public float currentHealth = 100f;

        public event Action<float, float> OnHealthChanged;
        public event Action OnDied;

        public bool IsDead => currentHealth <= 0f;

        private void Awake()
        {
            if (rig == null) rig = GetComponent<ActiveRagdollRig>();
            if (locomotion == null) locomotion = GetComponent<RagdollLocomotion>();
            if (knockoutController == null) knockoutController = GetComponent<KnockoutController>();
            if (brain == null) brain = GetComponent<NPCBrain>();
            if (factionMember == null) factionMember = GetComponent<NPCFactionMember>();
            if (headImpactReceiver == null) headImpactReceiver = GetComponentInChildren<HeadImpactReceiver>();

            currentHealth = maxHealth;

            if (headImpactReceiver != null)
            {
                headImpactReceiver.OnHeadImpact += HandleHeadImpact;
            }

            if (knockoutController != null)
            {
                knockoutController.OnKnockedOut += HandleKnockedOut;
                knockoutController.OnRecovered += HandleRecovered;
            }
        }

        private void OnDestroy()
        {
            if (headImpactReceiver != null)
            {
                headImpactReceiver.OnHeadImpact -= HandleHeadImpact;
            }

            if (knockoutController != null)
            {
                knockoutController.OnKnockedOut -= HandleKnockedOut;
                knockoutController.OnRecovered -= HandleRecovered;
            }
        }

        private void HandleHeadImpact(float impulse, Vector3 position)
        {
            if (IsDead) return;
            TakeDamage(impulse * 0.4f);
        }

        private void HandleKnockedOut()
        {
            if (brain != null)
            {
                brain.enabled = false;
            }
        }

        private void HandleRecovered()
        {
            if (brain != null && !IsDead)
            {
                brain.enabled = true;
            }
        }

        public void TakeDamage(float amount)
        {
            if (IsDead) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            if (rig != null)
            {
                rig.TriggerFlinch(0.2f, 0.25f);
            }

            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        public void Die()
        {
            currentHealth = 0f;

            if (knockoutController != null)
            {
                knockoutController.enabled = false;
            }

            if (brain != null)
            {
                brain.enabled = false;
            }

            if (locomotion != null)
            {
                locomotion.enabled = false;
            }

            if (rig != null)
            {
                rig.SetJointStrengthMultiplier(0f);
                rig.ResetAllTargetRotations();
            }

            OnDied?.Invoke();
        }

        public void Knockout(float duration = 4f)
        {
            if (IsDead || knockoutController == null) return;
            knockoutController.knockoutDuration = duration;
            knockoutController.TriggerKnockout(Vector3.zero, transform.position);
        }

        public void Revive()
        {
            if (IsDead)
            {
                currentHealth = maxHealth;
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
            }

            if (knockoutController != null)
            {
                knockoutController.enabled = true;
                knockoutController.ReviveImmediately();
            }

            if (brain != null)
            {
                brain.enabled = true;
            }

            if (locomotion != null)
            {
                locomotion.enabled = true;
            }
        }
    }
}
