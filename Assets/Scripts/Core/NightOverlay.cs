using UnityEngine;

namespace MoonlightPost
{
    /// <summary>화면 가장자리 어둠. 반딧불이 병(light 효과)이 있으면 밝은 범위가 넓어진다.</summary>
    public class NightOverlay : MonoBehaviour
    {
        public float baseScale = 40f;
        public float scalePerLight = 16f;

        void LateUpdate()
        {
            float target = baseScale + scalePerLight * Inventory.EffectSum("light");
            switch (NightDirector.Mood)
            {
                case NightMood.Fog: target -= 12f; break;        // 안개: 더 좁게 보인다
                case NightMood.Fireflies: target += 10f; break;  // 반딧불이: 더 넓게
                case NightMood.FullMoon: target += 6f; break;
            }
            // 등대에 불이 켜지면 섬 전체가 밝아진다.
            if (GameState.HasFlag("lighthouse_lit")) target += 10f;
            float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
