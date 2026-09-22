using System;
using UnityEngine;

namespace Jointure.NPC.ActiveRagdoll
{
    public enum KnockoutState
    {
        Active,
        KnockedOut,
        Recovering
    }

    public class KnockoutController : MonoBehaviour
    {
        public ActiveRagdollRig rig;
        public RagdollLocomotion locomotion;
        public float knockoutDuration = 4f;
        public float recoveryDuration = 2f;
        public float maxSettledLinearVelocity = 0.4f;
        public float maxSettledAngularVelocity = 1f;

        public KnockoutState CurrentState { get; private set; } = KnockoutState.Active;
        public bool IsKnockedOut => CurrentState != KnockoutState.Active;

        public event Action OnKnockedOut;
        public event Action OnRecoveryStarted;
        public event Action OnRecovered;

        private float _knockoutTimer;
        private float _recoveryTimer;
        private float _maxSettlingWait = 3f;
        private float _settlingTimer;

        private void Awake()
        {
            if (rig == null)
            {
                rig = GetComponent<ActiveRagdollRig>();
            }
            if (locomotion == null)
            {
                locomotion = GetComponent<RagdollLocomotion>();
            }
        }

        public void TriggerKnockout(Vector3 impactImpulse, Vector3 impactPoint)
        {
            _knockoutTimer = knockoutDuration;
            _settlingTimer = 0f;

            if (CurrentState == KnockoutState.KnockedOut) return;

            CurrentState = KnockoutState.KnockedOut;

            if (rig != null)
            {
                rig.SetJointStrengthMultiplier(0f);
                rig.ResetAllTargetRotations();
            }

            if (locomotion != null)
            {
                locomotion.enabled = false;
            }

            OnKnockedOut?.Invoke();
        }

        public void ReviveImmediately()
        {
            CurrentState = KnockoutState.Active;
            _knockoutTimer = 0f;
            _recoveryTimer = 0f;

            if (rig != null)
            {
                rig.SetJointStrengthMultiplier(1f);
            }

            if (locomotion != null)
            {
                locomotion.enabled = true;
            }

            OnRecovered?.Invoke();
        }

        private void Update()
        {
            if (CurrentState == KnockoutState.Active) return;

            if (CurrentState == KnockoutState.KnockedOut)
            {
                _knockoutTimer -= Time.deltaTime;
                if (_knockoutTimer <= 0f)
                {
                    _settlingTimer += Time.deltaTime;
                    bool settled = true;

                    if (rig != null && rig.pelvisRigidbody != null)
                    {
                        float linVel = rig.pelvisRigidbody.linearVelocity.magnitude;
                        float angVel = rig.pelvisRigidbody.angularVelocity.magnitude;
                        if (linVel > maxSettledLinearVelocity || angVel > maxSettledAngularVelocity)
                        {
                            settled = false;
                        }
                    }

                    if (settled || _settlingTimer >= _maxSettlingWait)
                    {
                        CurrentState = KnockoutState.Recovering;
                        _recoveryTimer = recoveryDuration;
                        OnRecoveryStarted?.Invoke();
                    }
                }
            }
            else if (CurrentState == KnockoutState.Recovering)
            {
                _recoveryTimer -= Time.deltaTime;
                float progress = Mathf.Clamp01(1f - (_recoveryTimer / recoveryDuration));
                float curve = Mathf.SmoothStep(0f, 1f, progress);

                if (rig != null)
                {
                    rig.SetJointStrengthMultiplier(curve);
                }

                if (_recoveryTimer <= 0f)
                {
                    CurrentState = KnockoutState.Active;

                    if (rig != null)
                    {
                        rig.SetJointStrengthMultiplier(1f);
                    }

                    if (locomotion != null)
                    {
                        locomotion.enabled = true;
                    }

                    OnRecovered?.Invoke();
                }
            }
        }
    }
}
