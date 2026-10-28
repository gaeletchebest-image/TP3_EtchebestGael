using UnityEngine;
using UnityEngine.AI;

namespace HandShooter
{
    [RequireComponent(typeof(DamageTarget), typeof(NavMeshAgent))]
    public sealed class SniperEnemy : MonoBehaviour
    {
        public enum AttackState { Searching, Repositioning, Settling, Charging, Recovering }

        public SniperSettings settings;
        public PrototypePlayer target;
        public Transform muzzle;
        public Transform chargeOrb;
        public TextMesh statusLabel;
        public AttackState State { get; private set; }
        public float ChargeProgress { get; private set; }
        public bool IsAimLocked { get; private set; }
        public int ShotsFired { get; private set; }

        private DamageTarget health;
        private NavMeshAgent agent;
        private LineRenderer trajectory, chargeRing, shotBeam;
        private Material fallbackMaterial;
        private MaterialPropertyBlock orbProperties;
        private Renderer orbRenderer;
        private Vector3 aimDirection, plannedPosition;
        private float stateElapsed, cooldownRemaining, repathRemaining, beamRemaining;
        private bool acquiredTarget, hasPosition;
        private NavMeshPath candidatePath, bestPath;

        private Vector3 Origin => muzzle ? muzzle.position : transform.position + Vector3.up * 0.35f;
        private Vector3 TargetPoint
        {
            get
            {
                // Aim inside the damageable body. A camera can sit near the rounded top of
                // the controller, or outside it altogether when its position is adjusted.
                var controller = target.GetComponent<CharacterController>();
                return controller ? target.transform.TransformPoint(controller.center) :
                    target.transform.position + Vector3.up * 0.9f;
            }
        }
        private bool CanNavigate => agent && agent.enabled && agent.isOnNavMesh;

        private void Awake()
        {
            health = GetComponent<DamageTarget>();
            agent = GetComponent<NavMeshAgent>();
            candidatePath = new NavMeshPath();
            bestPath = new NavMeshPath();
        }

        private void Start()
        {
            if (!settings) { enabled = false; return; }
            if (!target) target = FindFirstObjectByType<PrototypePlayer>();
            SniperNavigation.EnsureReady(gameObject.scene);
            agent.enabled = false;
            agent.baseOffset = 1f;
            agent.height = 2f;
            agent.radius = 0.5f;
            agent.stoppingDistance = 0.12f;
            agent.updateRotation = false;
            if (NavMesh.SamplePosition(transform.position - Vector3.up, out var spawn, 2f, NavMesh.AllAreas))
            {
                transform.position = spawn.position + Vector3.up * agent.baseOffset;
                agent.enabled = true;
                agent.Warp(spawn.position);
            }
            CreateVisuals();
        }

        private void Update() => Simulate(Time.deltaTime);

        public void Simulate(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            if (!settings || !target || target.IsDead || health.Health <= 0f)
            {
                StopMoving();
                HideWarning();
                return;
            }

            float distance = HorizontalDistance(transform.position, target.transform.position);
            if (!acquiredTarget)
            {
                if (distance > settings.detectionRange) return;
                acquiredTarget = true;
            }
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - deltaTime);
            repathRemaining -= deltaTime;
            beamRemaining = Mathf.Max(0f, beamRemaining - deltaTime);
            if (shotBeam) shotBeam.enabled = beamRemaining > 0f;

            bool inRange = distance >= settings.minimumAttackRange && distance <= settings.maximumAttackRange;
            bool visible = HasSight(transform.position + Vector3.up * 0.35f, Origin) && HasSight(Origin, TargetPoint);
            if (!inRange || !visible)
            {
                SetState(AttackState.Repositioning);
                Reposition();
                UpdateWarning();
                return;
            }

            if (State == AttackState.Searching || State == AttackState.Repositioning)
            {
                bool nearMiddle = Mathf.Abs(distance - settings.PreferredRange) <= settings.positioningTolerance;
                bool reachedPosition = hasPosition && CanNavigate && !agent.pathPending &&
                    HorizontalDistance(transform.position, plannedPosition) <= settings.positioningTolerance;
                if (!nearMiddle && !reachedPosition)
                {
                    SetState(AttackState.Repositioning);
                    Reposition();
                    UpdateWarning();
                    return;
                }
                StopMoving();
                SetState(cooldownRemaining > 0f ? AttackState.Recovering : AttackState.Settling);
            }

            if (State == AttackState.Recovering)
            {
                if (cooldownRemaining <= 0f) SetState(AttackState.Repositioning);
            }
            else if (State == AttackState.Settling)
            {
                AimAtTarget();
                stateElapsed += deltaTime;
                if (stateElapsed >= settings.settleDuration) SetState(AttackState.Charging);
            }
            else if (State == AttackState.Charging)
            {
                if (!IsAimLocked) AimAtTarget();
                stateElapsed += deltaTime;
                float duration = Mathf.Max(0.1f, settings.chargeDuration);
                ChargeProgress = Mathf.Clamp01(stateElapsed / duration);
                IsAimLocked = stateElapsed >= duration - Mathf.Clamp(settings.aimLockDuration, 0.01f, duration);
                if (stateElapsed >= duration) Fire();
            }
            UpdateWarning();
        }

        private void SetState(AttackState state)
        {
            if (State == state) return;
            State = state;
            stateElapsed = 0f;
            ChargeProgress = 0f;
            IsAimLocked = false;
        }

        private void Reposition()
        {
            if (!CanNavigate) return;
            agent.speed = Mathf.Max(0.1f, settings.moveSpeed);
            agent.isStopped = false;
            if (repathRemaining <= 0f)
            {
                repathRemaining = Mathf.Max(0.1f, settings.repathInterval);
                FindFiringPosition();
            }
            if (agent.desiredVelocity.sqrMagnitude > 0.01f) Face(agent.desiredVelocity);
        }

        private void FindFiringPosition()
        {
            hasPosition = false;
            float bestScore = float.PositiveInfinity;
            Vector3 fromPlayer = transform.position - target.transform.position;
            fromPlayer.y = 0f;
            if (fromPlayer.sqrMagnitude < 0.01f) fromPlayer = transform.forward;
            float startAngle = Mathf.Atan2(fromPlayer.z, fromPlayer.x);
            Vector3 muzzleOffset = Vector3.up * (Origin.y - transform.position.y + agent.baseOffset);

            // Prefer the middle of the attack band, but allow nearby rings in tight spaces.
            for (int ring = 0; ring < 3; ring++)
            {
                float radius = settings.PreferredRange + (ring == 1 ? -1f : ring == 2 ? 1f : 0f) *
                    (settings.maximumAttackRange - settings.minimumAttackRange) * 0.25f;
                for (int i = 0; i < 24; i++)
                {
                    float angle = startAngle + i * Mathf.PI * 2f / 24f;
                    Vector3 candidate = target.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    if (!NavMesh.SamplePosition(candidate, out var sample, 1.25f, agent.areaMask)) continue;
                    float range = HorizontalDistance(sample.position, target.transform.position);
                    if (range < settings.minimumAttackRange || range > settings.maximumAttackRange) continue;
                    if (!agent.CalculatePath(sample.position, candidatePath) || candidatePath.status != NavMeshPathStatus.PathComplete) continue;
                    float length = 0f;
                    var corners = candidatePath.corners;
                    for (int c = 1; c < corners.Length; c++) length += Vector3.Distance(corners[c - 1], corners[c]);
                    float score = length + Mathf.Abs(range - settings.PreferredRange) * 3f;
                    if (!HasSight(sample.position + muzzleOffset, TargetPoint)) score += 1000f;
                    if (score >= bestScore) continue;
                    bestScore = score;
                    plannedPosition = sample.position;
                    hasPosition = true;
                }
            }
            if (hasPosition)
            {
                agent.CalculatePath(plannedPosition, bestPath);
                agent.SetPath(bestPath);
            }
            else if (NavMesh.SamplePosition(target.transform.position, out var nearby, 2f, agent.areaMask))
                agent.SetDestination(nearby.position);
        }

        private void StopMoving()
        {
            if (CanNavigate) { agent.ResetPath(); agent.isStopped = true; }
        }

        private bool HasSight(Vector3 origin, Vector3 destination)
        {
            Vector3 offset = destination - origin;
            if (!Cast(origin, offset.normalized, offset.magnitude, out var hit)) return true;
            return hit.collider.GetComponentInParent<PrototypePlayer>() == target;
        }

        private bool Cast(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default;
            float closest = float.PositiveInfinity;
            // Player uses Ignore Raycast, but must still block and receive enemy shots.
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
            Vector3 origin = Origin;
            float length = settings.maximumAttackRange + Mathf.Abs(TargetPoint.y - origin.y) + 1f;
            Vector3 end = origin + aimDirection * length;
            PrototypePlayer victim = null;
            if (Cast(origin, aimDirection, length, out var hit))
            {
                end = hit.point;
                victim = hit.collider.GetComponentInParent<PrototypePlayer>();
            }
            if (shotBeam)
            {
                shotBeam.SetPosition(0, origin);
                shotBeam.SetPosition(1, end);
                shotBeam.startColor = shotBeam.endColor = settings.lockedColor;
                shotBeam.startWidth = shotBeam.endWidth = settings.beamWidth;
                shotBeam.enabled = true;
            }
            beamRemaining = settings.beamDuration;
            ShotsFired++;
            cooldownRemaining = Mathf.Max(0.1f, settings.shotCooldown);
            SetState(AttackState.Recovering);
            HideWarning();
            // Resolve damage in the same frame. There is no travelling projectile.
            if (victim) victim.Die();
        }

        private void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
        }

        private void AimAtTarget()
        {
            Vector3 point = TargetPoint;
            // Turning moves the muzzle because the barrel projects out from the body.
            // Rotate first, then calculate the ray from its final world position.
            Face(point - transform.position);
            aimDirection = (point - Origin).normalized;
        }

        private void CreateVisuals()
        {
            Material material = settings.beamMaterial;
            if (!material)
            {
                fallbackMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                material = fallbackMaterial;
            }
            trajectory = MakeLine("Sniper aim trajectory", material, 2);
            shotBeam = MakeLine("Sniper hitscan flash", material, 2);
            chargeRing = MakeLine("Sniper charge progress", material, 48);
            chargeRing.startWidth = chargeRing.endWidth = 0.055f;
            if (chargeOrb) orbRenderer = chargeOrb.GetComponent<Renderer>();
            orbProperties = new MaterialPropertyBlock();
            HideWarning();
        }

        private LineRenderer MakeLine(string name, Material material, int points)
        {
            var go = new GameObject(name);
            go.layer = 2;
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = points;
            line.numCapVertices = 4;
            line.enabled = false;
            return line;
        }

        private void UpdateWarning()
        {
            bool preparing = State == AttackState.Settling || State == AttackState.Charging;
            Color color = IsAimLocked ? settings.lockedColor : settings.warningColor;
            if (trajectory)
            {
                trajectory.enabled = preparing;
                if (preparing)
                {
                    float length = settings.maximumAttackRange + Mathf.Abs(TargetPoint.y - Origin.y) + 1f;
                    Vector3 end = Cast(Origin, aimDirection, length, out var hit) ? hit.point : Origin + aimDirection * length;
                    trajectory.SetPosition(0, Origin);
                    trajectory.SetPosition(1, end);
                    trajectory.startColor = trajectory.endColor = color;
                    trajectory.startWidth = trajectory.endWidth = settings.warningWidth * (1f + ChargeProgress);
                }
            }
            if (chargeRing)
            {
                chargeRing.enabled = State == AttackState.Charging;
                chargeRing.startColor = chargeRing.endColor = color;
                Vector3 center = transform.position + Vector3.up * 1.45f;
                Vector3 right = target.viewCamera ? target.viewCamera.transform.right : Vector3.right;
                Vector3 up = target.viewCamera ? target.viewCamera.transform.up : Vector3.up;
                for (int i = 0; i < chargeRing.positionCount; i++)
                {
                    float angle = (float)i / (chargeRing.positionCount - 1) * ChargeProgress * Mathf.PI * 2f;
                    chargeRing.SetPosition(i, center + (right * Mathf.Sin(angle) + up * Mathf.Cos(angle)) * 0.38f);
                }
            }
            if (chargeOrb)
            {
                chargeOrb.gameObject.SetActive(preparing);
                float pulse = IsAimLocked ? 1f + Mathf.Sin(stateElapsed * 45f) * 0.12f : 1f;
                chargeOrb.localScale = Vector3.one * Mathf.Lerp(0.12f, 0.38f, ChargeProgress) * pulse;
                if (orbRenderer && orbProperties != null)
                {
                    orbProperties.SetColor("_BaseColor", color);
                    orbProperties.SetColor("_Color", color);
                    orbRenderer.SetPropertyBlock(orbProperties);
                }
            }
            if (statusLabel)
            {
                statusLabel.text = State == AttackState.Charging ? (IsAimLocked ? "¡FIJADO!" : $"CARGANDO {ChargeProgress:P0}") :
                    State == AttackState.Settling ? "COLOCÁNDOSE" : State == AttackState.Recovering ? "RECARGANDO" : "REPOSICIONANDO";
                statusLabel.color = preparing ? color : Color.white;
                if (target.viewCamera) statusLabel.transform.rotation = target.viewCamera.transform.rotation;
            }
            if (health.label && target.viewCamera) health.label.transform.rotation = target.viewCamera.transform.rotation;
        }

        private void HideWarning()
        {
            if (trajectory) trajectory.enabled = false;
            if (chargeRing) chargeRing.enabled = false;
            if (chargeOrb) chargeOrb.gameObject.SetActive(false);
            if (statusLabel) statusLabel.text = "";
        }

        private void OnDisable()
        {
            StopMoving();
            HideWarning();
            if (shotBeam) shotBeam.enabled = false;
        }

        private void OnDestroy() { if (fallbackMaterial) Destroy(fallbackMaterial); }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void OnDrawGizmosSelected()
        {
            if (!settings) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, settings.minimumAttackRange);
            Gizmos.DrawWireSphere(transform.position, settings.maximumAttackRange);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, settings.PreferredRange);
        }
    }
}
