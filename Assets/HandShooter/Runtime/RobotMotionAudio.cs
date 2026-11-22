using UnityEngine;

namespace HandShooter
{
    // Movement sounds follow actual displacement, including patrol and chase.
    internal sealed class RobotMotionAudio : MonoBehaviour
    {
        private Vector3 previous;
        private void OnEnable() => previous = transform.position;
        private void LateUpdate()
        {
            float travel = Vector3.Distance(previous, transform.position);
            previous = transform.position;
            if (Time.deltaTime <= 0f) return;
            bool moving = travel > 0.001f && travel < 2f;
            ShooterAudio.Loop(transform, SoundCue.RobotMove, moving, 0.7f,
                Mathf.Lerp(0.75f, 1.4f, Mathf.Clamp01(travel / Time.deltaTime / 6f)));
        }
        private void OnDisable() => ShooterAudio.Loop(transform, SoundCue.RobotMove, false);
    }
}
