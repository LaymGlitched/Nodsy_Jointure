using System;
using UnityEngine;

namespace Jointure.NPC.ActiveRagdoll
{
    public enum FactionType
    {
        Friendly = 0,
        Neutral = 1,
        Hostile = 2
    }

    public enum Attitude
    {
        Friendly = 0,
        Neutral = 1,
        Hostile = 2
    }

    public class NPCFactionMember : MonoBehaviour
    {
        public FactionType faction = FactionType.Hostile;
        public float provocationDuration = 20f;

        public event Action<FactionType> OnFactionChanged;
        public event Action<Transform> OnProvoked;

        private Transform _provoker;
        private float _provocationTimer;

        public Transform CurrentProvoker => _provocationTimer > 0f ? _provoker : null;

        public void SetFaction(FactionType newFaction)
        {
            if (faction == newFaction) return;
            faction = newFaction;
            OnFactionChanged?.Invoke(faction);
        }

        public void Provoke(Transform instigator)
        {
            if (instigator == null) return;
            _provoker = instigator;
            _provocationTimer = provocationDuration;
            OnProvoked?.Invoke(instigator);
        }

        public void ClearProvocation()
        {
            _provoker = null;
            _provocationTimer = 0f;
        }

        public Attitude GetAttitudeTowards(Transform target)
        {
            if (target == null) return Attitude.Neutral;

            if (_provocationTimer > 0f && _provoker != null && (_provoker == target || target.IsChildOf(_provoker)))
            {
                return Attitude.Hostile;
            }

            NPCFactionMember otherMember = target.GetComponentInParent<NPCFactionMember>();
            if (otherMember != null)
            {
                if (otherMember == this) return Attitude.Friendly;

                if (faction == FactionType.Hostile) return Attitude.Hostile;
                if (otherMember.faction == FactionType.Hostile) return Attitude.Hostile;
                if (faction == FactionType.Friendly && otherMember.faction == FactionType.Friendly) return Attitude.Friendly;

                return Attitude.Neutral;
            }

            Jointure.Player player = target.GetComponentInParent<Jointure.Player>();
            if (player != null || target.CompareTag("Player"))
            {
                switch (faction)
                {
                    case FactionType.Friendly:
                        return Attitude.Friendly;
                    case FactionType.Neutral:
                        return Attitude.Neutral;
                    case FactionType.Hostile:
                    default:
                        return Attitude.Hostile;
                }
            }

            return Attitude.Neutral;
        }

        private void Update()
        {
            if (_provocationTimer > 0f)
            {
                _provocationTimer -= Time.deltaTime;
                if (_provocationTimer <= 0f)
                {
                    _provoker = null;
                }
            }
        }
    }
}
