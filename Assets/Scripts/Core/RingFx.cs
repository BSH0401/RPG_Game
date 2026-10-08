using UnityEngine;

namespace MoonlightPost
{
    /// <summary>커지면서 사라지는 원형 효과(봉인끈, 내려찍기, 처치 등).</summary>
    public class RingFx : MonoBehaviour
    {
        float startTime, duration, fromScale, toScale;
        SpriteRenderer sr;
        Color color;

        public static void Spawn(Vector2 position, float radius, Color color, float duration = 0.35f)
        {
            var go = new GameObject("RingFx");
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Ring;
            sr.color = color;
            sr.sortingOrder = 500;
            var fx = go.AddComponent<RingFx>();
            fx.sr = sr;
            fx.color = color;
            fx.startTime = Time.time;
            fx.duration = duration;
            fx.fromScale = radius * 0.4f;
            fx.toScale = radius * 2f;
            go.transform.localScale = Vector3.one * fx.fromScale;
        }

        void Update()
        {
            float t = Mathf.Clamp01((Time.time - startTime) / duration);
            transform.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, t);
            sr.color = new Color(color.r, color.g, color.b, color.a * (1f - t));
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
