using System;
using UnityEngine;

namespace Jointure.NPC.ActiveRagdoll
{
    public class RagdollFootContact : MonoBehaviour
    {
        public LayerMask groundLayer = -1;
        public float minNormalY = 0.5f;

        public bool IsGrounded { get; private set; }
        public Vector3 ContactPoint { get; private set; }
        public Vector3 ContactNormal { get; private set; } = Vector3.up;
        public event Action<bool> OnGroundedStateChanged;

        private int _groundContacts;

        private void OnCollisionEnter(Collision collision)
        {
            EvaluateCollision(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            EvaluateCollision(collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            if (((1 << collision.gameObject.layer) & groundLayer.value) == 0) return;

            _groundContacts = Mathf.Max(0, _groundContacts - 1);
            if (_groundContacts == 0 && IsGrounded)
            {
                IsGrounded = false;
                OnGroundedStateChanged?.Invoke(false);
            }
        }

        private void EvaluateCollision(Collision collision)
        {
            if (((1 << collision.gameObject.layer) & groundLayer.value) == 0) return;

            bool foundValidContact = false;
            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                if (contact.normal.y >= minNormalY)
                {
                    foundValidContact = true;
                    ContactPoint = contact.point;
                    ContactNormal = contact.normal;
                    break;
                }
            }

            if (foundValidContact)
            {
                _groundContacts = Mathf.Max(1, _groundContacts);
                if (!IsGrounded)
                {
                    IsGrounded = true;
                    OnGroundedStateChanged?.Invoke(true);
                }
            }
        }
    }
}
