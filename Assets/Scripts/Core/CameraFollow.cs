using UnityEngine;

namespace MoonlightPost
{
    public class CameraFollow : MonoBehaviour
    {
        public static CameraFollow I { get; private set; }
        public Transform target;
        public float sharpness = 8f;

        float shakeUntil;
        float shakeStrength;

        void Awake() => I = this;

        public static void Shake(float duration, float strength = 0.15f)
        {
            if (I == null || !Settings.ScreenShake) return;
            I.shakeUntil = Time.unscaledTime + duration;
            I.shakeStrength = strength;
        }

        public void SnapToTarget()
        {
            if (target != null)
                transform.position = new Vector3(target.position.x, target.position.y, transform.position.z);
        }

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 goal = new Vector3(target.position.x, target.position.y, transform.position.z);
            Vector3 pos = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-sharpness * Time.deltaTime));
            if (Time.unscaledTime < shakeUntil && Time.timeScale > 0f)
                pos += (Vector3)(Random.insideUnitCircle * shakeStrength);
            transform.position = pos;
        }
    }
}
