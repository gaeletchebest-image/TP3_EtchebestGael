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
        private Vector3 aimDirection, plannedPosition, zigzagWaypoint;
        private float stateElapsed, cooldownRemaining, repathRemaining, beamRemaining;
        private bool acquiredTarget, hasPosition, followingZigzag, retreating;
        private float zigzagSide;
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
            agent.angularSpeed = 720f;
            agent.updateRotation = false;
            zigzagSide = Random.value < 0.5f ? -1f : 1f;
            if (NavMesh.SamplePosition(transform.position - Vector3.up, out var spawn, 2f, NavMesh.AllAreas))
            {
                transform.position = spawn.position + Vector3.up * agent.baseOffset;
                agent.enabled = true;
                agent.Warp(spawn.position);
            }
            CreateVisuals();
        }

        private void Update()
        {
            if (Time.timeScale > 0f) Simulate(Time.deltaTime);
        }

        internal SavedGame.Sniper CaptureState() => new SavedGame.Sniper
        {
            state = State, chargeProgress = ChargeProgress, aimLocked = IsAimLocked, shotsFired = ShotsFired,
            aimDirection = aimDirection, plannedPosition = plannedPosition, zigzagWaypoint = zigzagWaypoint,
            stateElapsed = stateElapsed, cooldownRemaining = cooldownRemaining, repathRemaining = repathRemaining,
            acquiredTarget = acquiredTarget, hasPosition = hasPosition, followingZigzag = followingZigzag,
            retreating = retreating, zigzagSide = zigzagSide
        };

        internal void RestoreState(SavedGame.Sniper saved)
        {
            State = saved.state;
            ChargeProgress = saved.chargeProgress;
            IsAimLocked = saved.aimLocked;
            ShotsFired = saved.shotsFired;
            aimDirection = saved.aimDirection;
            plannedPosition = saved.plannedPosition;
            zigzagWaypoint = saved.zigzagWaypoint;
            stateElapsed = saved.stateElapsed;
            cooldownRemaining = saved.cooldownRemaining;
            repathRemaining = saved.repathRemaining;
            acquiredTarget = saved.acquiredTarget;
            hasPosition = saved.hasPosition;
            followingZigzag = saved.followingZigzag;
            retreating = saved.retreating;
            zigzagSide = saved.zigzagSide;
            if (settings && target) UpdateWarning();
        }

        public void Simulate(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            if (!settings || !target || target.IsDead || health.Health <= 0f)
            {
                StopMoving();
                ShooterAudio.Loop(transform, SoundCue.SniperCharge, false);
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
                Reposition(distance);
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
                    Reposition(distance);
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
                bool wasLocked = IsAimLocked;
                IsAimLocked = stateElapsed >= duration - Mathf.Clamp(settings.aimLockDuration, 0.01f, duration);
                if (!wasLocked && IsAimLocked) ShooterAudio.Play(SoundCue.SniperLock, Origin);
                ShooterAudio.Loop(transform, SoundCue.SniperCharge, true, 0.7f + ChargeProgress * 0.3f, 0.7f + ChargeProgress * 0.9f);
                if (stateElapsed >= duration) Fire();
            }
            UpdateWarning();
        }

        private void SetState(AttackState state)
        {
            if (State == state) return;
            if (State == AttackState.Charging)
            {
                ShooterAudio.Loop(transform, SoundCue.SniperCharge, false);
                if (state == AttackState.Repositioning) ShooterAudio.Play(SoundCue.SniperCancel, Origin);
            }
            if (state == AttackState.Charging)
            {
                ShooterAudio.Threat();
                ShooterAudio.Loop(transform, SoundCue.SniperCharge, true, 0.7f, 0.7f);
            }
            State = state;
            stateElapsed = 0f;
            ChargeProgress = 0f;
            IsAimLocked = false;
        }

        private void Reposition(float distance)
        {
            if (!CanNavigate) return;
            agent.speed = Mathf.Max(0.1f, settings.moveSpeed);
            agent.acceleration = agent.speed * 8f;
            agent.isStopped = false;
            bool tooClose = distance < settings.minimumAttackRange;
            if (repathRemaining <= 0f || tooClose != retreating)
            {
                retreating = tooClose;
                repathRemaining = Mathf.Max(0.1f, settings.repathInterval);
                FindFiringPosition(tooClose);
            }
            else if (followingZigzag && HorizontalDistance(transform.position, zigzagWaypoint) <= 0.65f)
                FollowFiringPath();
            if (agent.desiredVelocity.sqrMagnitude > 0.01f) Face(agent.desiredVelocity);
        }

        private void FindFiringPosition(bool tooClose)
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
            followingZigzag = false;
            agent.autoBraking = true;
            if (hasPosition) FollowFiringPath();
            else if (tooClose) RetreatFromPlayer(fromPlayer.normalized);
            else if (NavMesh.SamplePosition(target.transform.position, out var nearby, 2f, agent.areaMask))
                agent.SetDestination(nearby.position);
            else agent.ResetPath();
        }

        private void FollowFiringPath()
        {
            followingZigzag = false;
            agent.autoBraking = true;
            if (!agent.CalculatePath(plannedPosition, bestPath) || bestPath.status != NavMeshPathStatus.PathComplete)
            {
                hasPosition = false;
                repathRemaining = 0f;
                agent.ResetPath();
                return;
            }

            var corners = bestPath.corners;
            if (corners.Length >= 2)
            {
                Vector3 forward = corners[1] - corners[0];
                forward.y = 0f;
                float stride = Random.Range(3.5f, 5f);
                if (forward.magnitude > stride + 1f)
                {
                    forward.Normalize();
                    Vector3 side = Vector3.Cross(Vector3.up, forward) * zigzagSide;
                    Vector3 probe = corners[0] + forward * stride + side * stride * Random.Range(0.3f, 0.5f);
                    // Take a lateral step only when it can be reached directly on the mesh.
                    if (NavMesh.SamplePosition(probe, out var waypoint, 0.5f, agent.areaMask) &&
                        !agent.Raycast(waypoint.position, out _) && agent.CalculatePath(waypoint.position, candidatePath) &&
                        candidatePath.status == NavMeshPathStatus.PathComplete && agent.SetPath(candidatePath))
                    {
                        zigzagWaypoint = waypoint.position;
                        zigzagSide = -zigzagSide;
                        followingZigzag = true;
                        agent.autoBraking = false;
                        return;
                    }
                }
            }
            agent.SetPath(bestPath);
        }

        private void RetreatFromPlayer(Vector3 away)
        {
            // A cramped room may have no reachable point in the attack band.
            Vector3 start = agent.nextPosition - Vector3.up * agent.baseOffset;
            Vector3 retreat = start;
            float bestDistance = HorizontalDistance(start, target.transform.position);
            for (int angle = -60; angle <= 60; angle += 30)
            {
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * away;
                Vector3 probe = start + direction * 6f;
                if (agent.Raycast(probe, out var edge)) probe = edge.position - direction * 0.25f;
                if (!NavMesh.SamplePosition(probe, out var sample, 0.5f, agent.areaMask) ||
                    agent.Raycast(sample.position, out _)) continue;
                float distance = HorizontalDistance(sample.position, target.transform.position);
                if (distance <= bestDistance) continue;
                bestDistance = distance;
                retreat = sample.position;
            }
            if (HorizontalDistance(start, retreat) > agent.stoppingDistance) agent.SetDestination(retreat);
            else agent.ResetPath();
        }

        private void StopMoving()
        {
            followingZigzag = false;
            repathRemaining = 0f;
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
            ShooterAudio.Play(SoundCue.SniperFire, Origin);
            ShooterAudio.Loop(transform, SoundCue.SniperCharge, false);
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
            if (victim) victim.Die("Sniper", transform);
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
            }
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
            ShooterAudio.Loop(transform, SoundCue.SniperCharge, false);
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
