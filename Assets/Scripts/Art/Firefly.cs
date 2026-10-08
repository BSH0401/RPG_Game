using UnityEngine;

namespace MoonlightPost
{
    /// <summary>밤 분위기용 반딧불이·물결 반짝임. 제자리 근처를 떠다니며 깜빡인다.</summary>
    public class Firefly : MonoBehaviour
    {
        public float wander = 0.8f;
        public float speed = 0.6f;
        public Color color = new Color(0.85f, 1f, 0.6f, 1f);

        SpriteRenderer sr;
        Vector3 home;
        float seed;

        public static void Spawn(Transform parent, Vector2 pos, Color color, float wander, float size)
        {
            var go = new GameObject("Firefly");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * size;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Art.Dot;
            sr.sortingOrder = 1003;
            var glow = new GameObject("Glow");
            glow.transform.SetParent(go.transform, false);
            glow.transform.localScale = Vector3.one * 1.2f;
            var gsr = glow.AddComponent<SpriteRenderer>();
            gsr.sprite = Art.Glow;
            gsr.color = new Color(color.r, color.g, color.b, 0.25f);
            gsr.sortingOrder = 1002;
            var f = go.AddComponent<Firefly>();
            f.color = color;
            f.wander = wander;
        }

        void Start()
        {
            sr = GetComponent<SpriteRenderer>();
            home = transform.position;
            seed = Random.value * 100f;
        }

        void Update()
        {
            float t = Time.time * speed + seed;
            transform.position = home + new Vector3(Mathf.Sin(t * 1.3f) + Mathf.Sin(t * 0.7f + 2f), Mathf.Cos(t * 1.1f) * 0.8f, 0f) * wander * 0.5f;
            float blink = Mathf.Clamp01(Mathf.Sin(t * 2.3f) * 0.6f + 0.6f);
            var c = color;
            c.a = blink;
            sr.color = c;
        }
    }
}
