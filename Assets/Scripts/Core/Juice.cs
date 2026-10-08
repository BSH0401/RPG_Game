using UnityEngine;

namespace MoonlightPost
{
    /// <summary>타격감: 아주 짧게 시간을 멈추는 히트스톱.</summary>
    public class Juice : MonoBehaviour
    {
        static Juice instance;
        float stopUntil;
        const float SlowScale = 0.05f;

        void Awake() => instance = this;

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void HitStop(float seconds)
        {
            // 지도·가방으로 멈춰 있을 때는 건드리지 않는다.
            if (instance == null || Time.timeScale == 0f) return;
            instance.stopUntil = Mathf.Max(instance.stopUntil, Time.unscaledTime + seconds);
            Time.timeScale = SlowScale;
        }

        void Update()
        {
            if (Mathf.Approximately(Time.timeScale, SlowScale) && Time.unscaledTime >= stopUntil)
                Time.timeScale = 1f;
        }
    }
}
