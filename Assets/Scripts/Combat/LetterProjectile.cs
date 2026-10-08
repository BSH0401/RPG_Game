using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>편지 도구 「날아가는 편지」: 곧게 날아가 처음 닿은 적에게 피해를 준다. 벽·나무에 닿으면 사라진다.</summary>
    public class LetterProjectile : MonoBehaviour
    {
        public float speed = 12f;
        public float maxDistance = 9f;
        public int damage = 2;

        Vector2 dir;
        Vector2 start;
        readonly List<Collider2D> hits = new List<Collider2D>();

        public static void Spawn(Vector2 pos, Vector2 direction, int damage)
        {
            var go = new GameObject("FlyingLetter");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GameAssets.Available ? GameAssets.Region("Items/PaperLetter", 0, 0, 12, 7, 0.5f, 0.5f) : null;
            if (sr.sprite == null) sr.sprite = Art.Envelope != null ? Sprite.Create(Art.Envelope, new Rect(0, 0, Art.Envelope.width, Art.Envelope.height), new Vector2(0.5f, 0.5f), 16) : null;
            sr.sortingOrder = 650;
            var glow = new GameObject("Glow");
            glow.transform.SetParent(go.transform, false);
            glow.transform.localScale = Vector3.one * 1.2f;
            var g = glow.AddComponent<SpriteRenderer>();
            g.sprite = Art.Glow;
            g.color = new Color(1f, 0.95f, 0.8f, 0.35f);
            g.sortingOrder = 1001;
            var p = go.AddComponent<LetterProjectile>();
            p.dir = direction.normalized;
            p.start = pos;
            p.damage = damage;
        }

        void Update()
        {
            transform.position += (Vector3)(dir * speed * Time.deltaTime);
            Vector2 pos = transform.position;

            hits.Clear();
            Physics2D.OverlapCircle(pos, 0.3f, new ContactFilter2D().NoFilter(), hits);
            foreach (var col in hits)
            {
                if (col.isTrigger) continue;
                var enemy = col.GetComponentInParent<EnemyController>();
                if (enemy != null)
                {
                    enemy.TakeHit(damage, pos - dir, 7f);
                    enemy.Stun(0.35f);
                    Finish();
                    return;
                }
                if (col.GetComponentInParent<PlayerController>() == null && col.GetComponentInParent<NpcInteractable>() == null)
                {
                    Finish(); // 벽, 나무, 건물
                    return;
                }
            }
            if (Vector2.Distance(start, pos) > maxDistance) Finish();
        }

        void Finish()
        {
            if (GameAssets.Available) FrameAnimator.PlayOnce(GameAssets.SmokeFrames, transform.position, 20f, new Color(1f, 1f, 1f, 0.8f), 600, 0.8f);
            Destroy(gameObject);
        }
    }
}
