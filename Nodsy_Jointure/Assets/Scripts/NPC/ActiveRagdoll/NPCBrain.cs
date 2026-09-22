using UnityEngine;
using UnityEngine.AI;

namespace Jointure.NPC.ActiveRagdoll
{
    public enum NPCState
    {
        Idle,
        Roam,
        Investigate,
        Combat,
        Flee
    }

    public class NPCBrain : MonoBehaviour
    {
        public Transform eyesTransform;
        public float sightRange = 16f;
        public float sightAngle = 120f;
        public LayerMask perceptionMask = -1;
        public LayerMask obstacleMask = 1;
        public float whiskerLength = 2.5f;
        public float avoidanceWeight = 2f;
        public float wanderRadius = 12f;
        public float minIdleTime = 0.4f;
        public float maxIdleTime = 1.0f;
        public float destinationThreshold = 1.2f;
        public float combatEngagementRange = 2f;
        public float fleeDistance = 18f;
        public bool prefersFleeing = false;
        public NPCFactionMember factionMember;

        public NPCState CurrentState { get; private set; } = NPCState.Idle;
        public Transform CurrentTarget { get; private set; }
        public Vector3 CurrentDestination { get; private set; }

        private RagdollLocomotion _locomotion;
        private ActiveRagdollRig _rig;
        private Vector3 _spawnPoint;
        private float _stateTimer;
        private Collider[] _perceptionBuffer = new Collider[16];

        private void Awake()
        {
            _locomotion = GetComponent<RagdollLocomotion>();
            _rig = GetComponent<ActiveRagdollRig>();

            if (factionMember == null)
            {
                factionMember = GetComponent<NPCFactionMember>();
            }

            if (eyesTransform == null)
            {
                eyesTransform = transform;
            }

            _spawnPoint = transform.position;
            PickNewIdleDuration();
        }

        private void Start()
        {
            CurrentState = NPCState.Idle;
            PickNewIdleDuration();
        }

        private void OnEnable()
        {
            _spawnPoint = transform.position;
        }

        private void OnDisable()
        {
            if (_locomotion != null)
            {
                _locomotion.MoveDirection = Vector3.zero;
            }
        }

        private void Update()
        {
            if (_rig != null && _rig.StrengthMultiplier <= 0.001f)
            {
                if (_locomotion != null) _locomotion.MoveDirection = Vector3.zero;
                return;
            }

            UpdatePerception();

            if (_rig != null && !_rig.IsBalanced)
            {
                if (_locomotion != null) _locomotion.MoveDirection = Vector3.zero;
                return;
            }

            ExecuteStateMachine();
        }

        private void UpdatePerception()
        {
            if (factionMember != null && factionMember.CurrentProvoker != null)
            {
                CurrentTarget = factionMember.CurrentProvoker;
                CurrentState = prefersFleeing ? NPCState.Flee : NPCState.Combat;
                return;
            }

            Vector3 eyePos = eyesTransform != null ? eyesTransform.position : transform.position;
            int count = Physics.OverlapSphereNonAlloc(eyePos, sightRange, _perceptionBuffer, perceptionMask.value);

            Transform bestHostile = null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = _perceptionBuffer[i];
                if (col.transform.IsChildOf(transform)) continue;

                Vector3 targetCenter = col.bounds.center;
                Vector3 toTarget = targetCenter - eyePos;
                float dist = toTarget.magnitude;

                if (dist > sightRange) continue;

                Vector3 forward = eyesTransform != null ? eyesTransform.forward : transform.forward;
                float angle = Vector3.Angle(forward, toTarget.normalized);
                if (dist > 3.5f && angle > sightAngle * 0.5f) continue;

                if (Physics.Raycast(eyePos, toTarget.normalized, out RaycastHit hit, dist, obstacleMask.value))
                {
                    if (!hit.transform.IsChildOf(col.transform)) continue;
                }

                if (factionMember != null)
                {
                    Attitude attitude = factionMember.GetAttitudeTowards(col.transform);
                    if (attitude == Attitude.Hostile && dist < closestDist)
                    {
                        closestDist = dist;
                        bestHostile = col.transform;
                    }
                }
            }

            if (bestHostile != null)
            {
                CurrentTarget = bestHostile;
                CurrentState = prefersFleeing ? NPCState.Flee : NPCState.Combat;
            }
            else if (CurrentState == NPCState.Combat || CurrentState == NPCState.Flee)
            {
                CurrentTarget = null;
                CurrentState = NPCState.Idle;
                PickNewIdleDuration();
            }
        }

        private void ExecuteStateMachine()
        {
            Vector3 rootPos = _rig != null && _rig.pelvisRigidbody != null ? _rig.pelvisRigidbody.position : transform.position;

            switch (CurrentState)
            {
                case NPCState.Idle:
                    if (_locomotion != null) _locomotion.MoveDirection = Vector3.zero;
                    _stateTimer -= Time.deltaTime;
                    if (_stateTimer <= 0f)
                    {
                        SampleNewDestination(rootPos);
                        CurrentState = NPCState.Roam;
                    }
                    break;

                case NPCState.Roam:
                    Vector3 toDest = CurrentDestination - rootPos;
                    toDest.y = 0f;
                    if (toDest.magnitude <= destinationThreshold)
                    {
                        CurrentState = NPCState.Idle;
                        PickNewIdleDuration();
                        if (_locomotion != null) _locomotion.MoveDirection = Vector3.zero;
                    }
                    else
                    {
                        MoveAlongDirection(rootPos, toDest.normalized, 0.7f);
                    }
                    break;

                case NPCState.Investigate:
                    Vector3 toInvestigate = CurrentDestination - rootPos;
                    toInvestigate.y = 0f;
                    if (toInvestigate.magnitude <= destinationThreshold)
                    {
                        CurrentState = NPCState.Idle;
                        PickNewIdleDuration();
                        if (_locomotion != null) _locomotion.MoveDirection = Vector3.zero;
                    }
                    else
                    {
                        MoveAlongDirection(rootPos, toInvestigate.normalized, 0.85f);
                    }
                    break;

                case NPCState.Combat:
                    if (CurrentTarget == null)
                    {
                        CurrentState = NPCState.Idle;
                        PickNewIdleDuration();
                        return;
                    }
                    Vector3 toEnemy = CurrentTarget.position - rootPos;
                    toEnemy.y = 0f;
                    float enemyDist = toEnemy.magnitude;

                    if (enemyDist > combatEngagementRange)
                    {
                        MoveAlongDirection(rootPos, toEnemy.normalized, 1.1f);
                    }
                    else
                    {
                        if (_rig != null) _rig.TargetHeading = toEnemy.normalized;
                        if (_locomotion != null)
                        {
                            _locomotion.MoveDirection = Vector3.zero;
                            if (_locomotion.CanAttack())
                            {
                                _locomotion.TriggerAttack();
                            }
                        }
                    }
                    break;

                case NPCState.Flee:
                    if (CurrentTarget == null)
                    {
                        CurrentState = NPCState.Idle;
                        PickNewIdleDuration();
                        return;
                    }
                    Vector3 awayFromEnemy = rootPos - CurrentTarget.position;
                    awayFromEnemy.y = 0f;
                    float threatDist = awayFromEnemy.magnitude;

                    if (threatDist >= fleeDistance)
                    {
                        CurrentTarget = null;
                        CurrentState = NPCState.Idle;
                        PickNewIdleDuration();
                        if (_locomotion != null) _locomotion.MoveDirection = Vector3.zero;
                    }
                    else
                    {
                        MoveAlongDirection(rootPos, awayFromEnemy.normalized, 1.25f);
                    }
                    break;
            }
        }

        private void MoveAlongDirection(Vector3 origin, Vector3 desiredDir, float speed)
        {
            Vector3 finalDir = CalculateObstacleAvoidance(origin, desiredDir);
            if (_locomotion != null)
            {
                _locomotion.MoveDirection = finalDir;
                _locomotion.SpeedMultiplier = speed;
            }
            if (_rig != null)
            {
                _rig.TargetHeading = finalDir;
            }
        }

        private Vector3 CalculateObstacleAvoidance(Vector3 origin, Vector3 forward)
        {
            Vector3 avoidance = Vector3.zero;
            Vector3 centerRay = forward;
            Vector3 leftRay = Quaternion.Euler(0f, -32f, 0f) * forward;
            Vector3 rightRay = Quaternion.Euler(0f, 32f, 0f) * forward;

            if (Physics.Raycast(origin + Vector3.up * 0.4f, centerRay, out RaycastHit centerHit, whiskerLength, obstacleMask.value))
            {
                if (!centerHit.transform.IsChildOf(transform))
                {
                    avoidance += centerHit.normal * avoidanceWeight * 1.5f;
                }
            }
            if (Physics.Raycast(origin + Vector3.up * 0.4f, leftRay, out RaycastHit leftHit, whiskerLength * 0.85f, obstacleMask.value))
            {
                if (!leftHit.transform.IsChildOf(transform))
                {
                    avoidance += leftHit.normal * avoidanceWeight;
                }
            }
            if (Physics.Raycast(origin + Vector3.up * 0.4f, rightRay, out RaycastHit rightHit, whiskerLength * 0.85f, obstacleMask.value))
            {
                if (!rightHit.transform.IsChildOf(transform))
                {
                    avoidance += rightHit.normal * avoidanceWeight;
                }
            }

            // Ledge / Pit avoidance: verify that floor exists ahead before stepping
            Vector3 aheadPoint = origin + forward * 1.5f + Vector3.up * 0.5f;
            if (!Physics.Raycast(aheadPoint, Vector3.down, out RaycastHit ledgeHit, 2.5f, obstacleMask.value))
            {
                avoidance += -forward * 4f;
            }

            Vector3 combined = forward + avoidance;
            combined.y = 0f;
            return combined.sqrMagnitude > 0.001f ? combined.normalized : forward;
        }

        private void SampleNewDestination(Vector3 currentPos)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;
                float dist = Random.Range(5f, wanderRadius);
                Vector3 targetCandidate = currentPos + new Vector3(dir.x * dist, 0f, dir.y * dist);

                if (NavMesh.SamplePosition(targetCandidate, out NavMeshHit navHit, wanderRadius * 0.5f, NavMesh.AllAreas))
                {
                    if (Vector3.Distance(navHit.position, currentPos) > destinationThreshold * 2f)
                    {
                        CurrentDestination = navHit.position;
                        return;
                    }
                }

                if (Physics.Raycast(targetCandidate + Vector3.up * 5f, Vector3.down, out RaycastHit groundHit, 15f, obstacleMask.value))
                {
                    if (Vector3.Distance(groundHit.point, currentPos) > destinationThreshold * 2f)
                    {
                        CurrentDestination = groundHit.point;
                        return;
                    }
                }
            }

            CurrentDestination = currentPos + transform.forward * 6f;
        }

        private void PickNewIdleDuration()
        {
            _stateTimer = Random.Range(minIdleTime, maxIdleTime);
        }

        public void InvestigatePosition(Vector3 position)
        {
            CurrentDestination = position;
            CurrentState = NPCState.Investigate;
        }
    }
}
