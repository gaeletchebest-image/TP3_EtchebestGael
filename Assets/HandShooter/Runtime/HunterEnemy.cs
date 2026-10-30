using UnityEngine;
using UnityEngine.AI;

namespace HandShooter
{
    [RequireComponent(typeof(DamageTarget), typeof(NavMeshAgent))]
    public sealed class HunterEnemy : MonoBehaviour
    {
        public enum BehaviourState { Normal, Chasing, Firing, Patrolling, Waiting }

        public HunterSettings settings;
        public PrototypePlayer target;
        public Transform muzzle;
        public Renderer eye;
        public TextMesh statusLabel;
        [Tooltip("Punto alrededor del cual patrulla. Sin asignar, usa su posición inicial. Usar un objeto independiente del hunter.")]
        public Transform patrolCenter;
        public BehaviourState State { get; private set; }
        public bool IsAlerted { get; private set; }
        public int ShotsFired { get; private set; }

        private DamageTarget health;
        private NavMeshAgent agent;
        private LineRenderer shotBeam;
        private Material fallbackMaterial;
        private MaterialPropertyBlock eyeProperties;
        private float repathRemaining, beamRemaining;
        private Vector3 initialPatrolCenter;
        private NavMeshPath patrolPath;
        private bool hasPatrolDestination;
        private float patrolWaitRemaining;
        private Vector3 PatrolCenter => patrolCenter ? patrolCenter.position : initialPatrolCenter;
        private bool CanNavigate => agent && agent.enabled && agent.isOnNavMesh;
        private Vector3 EyeOrigin => transform.position + Vector3.up * 0.35f;
        private Vector3 Origin => muzzle ? muzzle.position : EyeOrigin;
        private Vector3 TargetPoint
        {
            get
            {
                var controller = target.GetComponent<CharacterController>();
                return controller ? target.transform.TransformPoint(controller.center) :
                    target.transform.position + Vector3.up * 0.9f;
            }
        }

        private void Awake()
        {
            health = GetComponent<DamageTarget>();
            agent = GetComponent<NavMeshAgent>();
            eyeProperties = new MaterialPropertyBlock();
            patrolPath = new NavMeshPath();
        }

        private void OnEnable() => health.Damaged += OnDamaged;

        private void Start()
        {
            if (!settings) { enabled = false; return; }
            if (!target) target = FindFirstObjectByType<PrototypePlayer>();
            SniperNavigation.EnsureReady(gameObject.scene);
            agent.enabled = false;
            agent.baseOffset = 1f;
            agent.height = 2f;
            agent.radius = 0.5f;
            agent.speed = settings.chaseSpeed;
            agent.acceleration = settings.acceleration;
            // Continue around cover even when already inside shooting range.
            agent.stoppingDistance = 0.1f;
            agent.updateRotation = false;
            initialPatrolCenter = transform.position;
            if (NavMesh.SamplePosition(transform.position - Vector3.up, out var spawn, 2f, agent.areaMask))
            {
                initialPatrolCenter = spawn.position;
                transform.position = spawn.position + Vector3.up * agent.baseOffset;
                agent.enabled = true;
                agent.Warp(spawn.position);
            }
            CreateBeam();
            UpdateStatus();
        }

        private void Update() => Simulate(Time.deltaTime);

        public void Simulate(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            beamRemaining = Mathf.Max(0f, beamRemaining - deltaTime);
            if (shotBeam) shotBeam.enabled = beamRemaining > 0f;
            if (!settings || (target && target.IsDead) || health.Health <= 0f)
            {
                StopMoving();
                return;
            }

            repathRemaining -= deltaTime;
            Vector3 offset = target ? target.transform.position - transform.position : Vector3.zero;
            offset.y = 0f;
            bool inRange = offset.sqrMagnitude <= settings.range * settings.range;
            if (target && inRange && HasSight(EyeOrigin, TargetPoint))
            {
                // Turning moves the muzzle. Check its final position before shooting.
                Face(offset);
                if (HasSight(EyeOrigin, Origin) && HasSight(Origin, TargetPoint))
                {
                    Fire();
                    return;
                }
            }

            if (IsAlerted && target)
            {
                State = BehaviourState.Chasing;
                Chase();
            }
            else Patrol(deltaTime);
            UpdateStatus();
        }

        private void Patrol(float deltaTime)
        {
            if (!CanNavigate || settings.patrolRadius <= 0f)
            {
                StopMoving();
                State = BehaviourState.Normal;
                return;
            }

            if (State == BehaviourState.Waiting)
            {
                patrolWaitRemaining -= deltaTime;
                if (patrolWaitRemaining > 0f) return;
            }

            agent.speed = Mathf.Max(0.1f, settings.patrolSpeed);
            agent.acceleration = Mathf.Max(0.1f, settings.acceleration);
            if (hasPatrolDestination)
            {
                if (agent.pathPending) return;
                if (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete ||
                    agent.remainingDistance <= agent.stoppingDistance + 0.05f)
                {
                    BeginPatrolWait();
                    return;
                }
                State = BehaviourState.Patrolling;
                if (agent.desiredVelocity.sqrMagnitude > 0.01f) Face(agent.desiredVelocity);
                return;
            }

            Vector3 center = PatrolCenter;
            float radius = Mathf.Max(0f, settings.patrolRadius);
            float minimumStep = Mathf.Min(1f, radius * 0.5f);
            // Only accept destinations on a reachable part of the NavMesh, within the patrol area.
            for (int attempt = 0; attempt < 16; attempt++)
            {
                Vector2 random = Random.insideUnitCircle * radius;
                Vector3 candidate = center + new Vector3(random.x, 0f, random.y);
                if (!NavMesh.SamplePosition(candidate, out var destination, 2f, agent.areaMask)) continue;
                Vector3 fromCenter = destination.position - center;
                fromCenter.y = 0f;
                Vector3 fromHunter = destination.position - transform.position;
                fromHunter.y = 0f;
                if (fromCenter.sqrMagnitude > radius * radius ||
                    fromHunter.sqrMagnitude < minimumStep * minimumStep) continue;
                if (!agent.CalculatePath(destination.position, patrolPath) ||
                    patrolPath.status != NavMeshPathStatus.PathComplete) continue;
                if (!agent.SetPath(patrolPath)) continue;
                agent.isStopped = false;
                hasPatrolDestination = true;
                State = BehaviourState.Patrolling;
                return;
            }

            // An obstructed or disconnected area is retried after a pause.
            BeginPatrolWait();
        }

        private void BeginPatrolWait()
        {
            StopMoving();
            State = BehaviourState.Waiting;
            float minimum = Mathf.Max(0f, settings.patrolWaitMin);
            patrolWaitRemaining = Random.Range(minimum, Mathf.Max(minimum, settings.patrolWaitMax));
        }

        private void OnDamaged(float amount)
        {
            if (!settings || health.Health <= 0f) return;
            if (!target) target = FindFirstObjectByType<PrototypePlayer>();
            IsAlerted = true;
            State = BehaviourState.Chasing;
            repathRemaining = 0f;
            StopMoving();
            if (target && !target.IsDead) Chase();
            UpdateStatus();
        }

        private void Chase()
        {
            if (!CanNavigate) return;
            agent.speed = Mathf.Max(0.1f, settings.chaseSpeed);
            agent.acceleration = Mathf.Max(0.1f, settings.acceleration);
            agent.isStopped = false;
            if (repathRemaining <= 0f)
            {
                repathRemaining = Mathf.Max(0.02f, settings.repathInterval);
                if (NavMesh.SamplePosition(target.transform.position, out var destination, 2f, agent.areaMask))
                    agent.SetDestination(destination.position);
            }
            if (agent.desiredVelocity.sqrMagnitude > 0.01f) Face(agent.desiredVelocity);
        }

        private void StopMoving()
        {
            hasPatrolDestination = false;
            if (CanNavigate) { agent.ResetPath(); agent.isStopped = true; }
        }

        private bool HasSight(Vector3 origin, Vector3 destination)
        {
            Vector3 offset = destination - origin;
            if (offset.sqrMagnitude < 0.000001f) return true;
            return !Cast(origin, offset.normalized, offset.magnitude, out var hit) ||
                hit.collider.GetComponentInParent<PrototypePlayer>() == target;
        }

        private bool Cast(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default;
            float closest = float.PositiveInfinity;
            // The player's body is on Ignore Raycast, as with sniper shots.
            foreach (var hit in Physics.RaycastAll(origin, direction, distance, settings.hitMask.value | (1 << 2),
                QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.distance >= closest) continue;
                closest = hit.distance;
                nearest = hit;
            }
            return closest < float.PositiveInfinity;
        }

        private void Fire()
        {
            StopMoving();
            Vector3 origin = Origin;
            Vector3 offset = TargetPoint - origin;
            Vector3 end = TargetPoint;
            PrototypePlayer victim = null;
            if (Cast(origin, offset.normalized, offset.magnitude + 0.05f, out var hit))
            {
                end = hit.point;
                victim = hit.collider.GetComponentInParent<PrototypePlayer>();
            }
            State = BehaviourState.Firing;
            ShotsFired++;
            if (shotBeam)
            {
                shotBeam.SetPosition(0, origin);
                shotBeam.SetPosition(1, end);
                shotBeam.enabled = true;
            }
            beamRemaining = settings.beamDuration;
            UpdateStatus();
            if (victim) victim.Die("Hunter");
        }

        private void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
        }

        private void CreateBeam()
        {
            Material material = settings.beamMaterial;
            if (!material)
            {
                fallbackMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                material = fallbackMaterial;
            }
            var go = new GameObject("Hunter hitscan flash");
            go.layer = 2;
            go.transform.SetParent(transform, false);
            shotBeam = go.AddComponent<LineRenderer>();
            shotBeam.sharedMaterial = material;
            shotBeam.useWorldSpace = true;
            shotBeam.positionCount = 2;
            shotBeam.numCapVertices = 4;
            shotBeam.startWidth = shotBeam.endWidth = settings.beamWidth;
            shotBeam.startColor = shotBeam.endColor = settings.beamColor;
            shotBeam.enabled = false;
        }

        private void UpdateStatus()
        {
            if (!settings) return;
            Color color = IsAlerted ? settings.alertColor : settings.normalColor;
            if (eye)
            {
                eyeProperties.SetColor("_BaseColor", color);
                eyeProperties.SetColor("_Color", color);
                eye.SetPropertyBlock(eyeProperties);
            }
            if (statusLabel)
            {
                statusLabel.text = State == BehaviourState.Firing ? "DISPARANDO" : IsAlerted ? "¡ALERTA!" :
                    State == BehaviourState.Patrolling ? "PATRULLANDO" : State == BehaviourState.Waiting ? "ESPERANDO" : "NORMAL";
                statusLabel.color = color;
            }
            if (!target || !target.viewCamera) return;
            if (statusLabel) statusLabel.transform.rotation = target.viewCamera.transform.rotation;
            if (health.label) health.label.transform.rotation = target.viewCamera.transform.rotation;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            StopMoving();
            if (shotBeam) shotBeam.enabled = false;
        }

        private void OnDestroy() { if (fallbackMaterial) Destroy(fallbackMaterial); }

        private void OnDrawGizmosSelected()
        {
            if (!settings) return;
            Gizmos.color = IsAlerted ? settings.alertColor : settings.normalColor;
            Gizmos.DrawWireSphere(transform.position, settings.range);
            Gizmos.color = new Color(0.3f, 0.65f, 1f, 0.6f);
            Vector3 center = Application.isPlaying ? PatrolCenter : patrolCenter ? patrolCenter.position : transform.position;
            Gizmos.DrawWireSphere(center, settings.patrolRadius);
        }
    }
}
