using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 잠시 뒤 그 자리에 떨어지는 공격(보스의 촉수). 주황 원이 차오르는 동안 피해야 한다.
    /// 막을 수 없고, 떨어지는 순간에 맞춰 막기를 누르면 받아치기로만 버틸 수 있다.
    /// </summary>
    public class GroundHazard : MonoBehaviour
    {
        float radius, fireAt, start;
        int damage;
        SpriteRenderer fill, ring;

        public static void Spawn(Vector2 pos, float radius, float delay, int damage)
        {
            var go = new GameObject("Hazard");
            go.transform.position = pos;
            var h = go.AddComponent<GroundHazard>();
            h.radius = radius;
            h.damage = damage;
            h.start = Time.time;
            h.fireAt = Time.time + delay;

            var f = new GameObject("Fill");
            f.transform.SetParent(go.transform, false);
            h.fill = f.AddComponent<SpriteRenderer>();
            h.fill.sprite = SpriteFactory.Circle;
            h.fill.color = new Color(1f, 0.55f, 0.05f, 0.35f);
            h.fill.sortingOrder = -490;
            f.transform.localScale = Vector3.zero;

            var r = new GameObject("Ring");
            r.transform.SetParent(go.transform, false);
            r.transform.localScale = Vector3.one * radius * 2f;
            h.ring = r.AddComponent<SpriteRenderer>();
            h.ring.sprite = SpriteFactory.Ring;
            h.ring.color = new Color(1f, 0.6f, 0.1f, 0.7f);
            h.ring.sortingOrder = -489;
        }

        void Update()
        {
            float t = Mathf.Clamp01((Time.time - start) / Mathf.Max(0.01f, fireAt - start));
            fill.transform.localScale = Vector3.one * radius * 2f * t;
            if (Time.time < fireAt) return;

            var player = PlayerController.I;
            if (player != null && Vector2.Distance(player.transform.position, transform.position) <= radius)
                player.ReceiveAttack(null, damage, transform.position, true);
            RingFx.Spawn(transform.position, radius, new Color(0.7f, 0.4f, 1f, 0.9f), 0.3f);
            if (GameAssets.Available) FrameAnimator.PlayOnce(GameAssets.SmokeFrames, transform.position, 18f, new Color(0.6f, 0.4f, 0.9f), 600, 1.2f);
            CameraFollow.Shake(0.08f, 0.08f);
            Destroy(gameObject);
        }
    }
}
