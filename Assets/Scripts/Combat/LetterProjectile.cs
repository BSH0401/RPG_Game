using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>스킬 「날아가는 편지」: 곧게 날아가며 닿는 적을 모두 꿰뚫는다. 벽·나무에 닿으면 사라진다.</summary>
    public class LetterProjectile : MonoBehaviour
    {
        public float speed = 12f;
        public float maxDistance = 9f;
        /// <summary>공격력에 곱하는 배율. 적마다 따로 굴린다(치명타 포함).</summary>
        public float damageMul = 2.6f;

        Vector2 dir;
        Vector2 start;
        readonly List<Collider2D> hits = new List<Collider2D>();
        readonly HashSet<EnemyController> struck = new HashSet<EnemyController>();

        public static void Spawn(Vector2 pos, Vector2 direction, float damageMul)
        {
            var go = new GameObject("FlyingLetter");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GameAssets.Available ? GameAssets.Region("Items/PaperLetter", 0, 0, 12, 7, 0.5f, 0.5f) : null;
            if (sr.sprite == null) sr.sprite = Art.Envelope != null ? Sprite.Create(Art.Envelope, new Rect(0, 0, Art.Envelope.width, Art.Envelope.height), new Vector2(0.5f, 0.5f), 16) : null;
            sr.sortingOrder = 650;
            go.transform.localScale = Vector3.one * 1.5f;
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
            p.damageMul = damageMul;
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
                    if (!struck.Add(enemy)) continue;
                    int dmg = Stats.RollDamage(damageMul, out bool crit);
                    enemy.TakeHit(dmg, pos - dir, 7f, crit);
                    enemy.Stun(0.35f);
                    Juice.HitStop(0.03f);
                    continue;
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
