using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 먹물 박쥐의 먹물 구슬. 막으면 사라지고, 받아치면 되돌아가 적에게 피해를 준다.
    /// </summary>
    public class EnemyProjectile : MonoBehaviour
    {
        public float speed = 6.5f;
        public float life = 2.5f;
        public int damage = 1;

        Vector2 dir;
        bool reflected;
        float dieAt;
        SpriteRenderer sr;
        Sprite[] frames;
        readonly List<Collider2D> hits = new List<Collider2D>();

        public static void Spawn(EnemyController owner, Vector2 pos, Vector2 direction, int damage)
        {
            var go = new GameObject("InkOrb");
            go.transform.position = pos;
            var p = go.AddComponent<EnemyProjectile>();
            p.sr = go.AddComponent<SpriteRenderer>();
            p.frames = GameAssets.Available ? GameAssets.Strip("FX/Orb", 16, 16, 4) : null;
            p.sr.sprite = p.frames != null ? p.frames[0] : SpriteFactory.Circle;
            if (p.frames == null) go.transform.localScale = Vector3.one * 0.45f;
            p.sr.color = new Color(0.75f, 0.45f, 1f);
            p.sr.sortingOrder = 650;
            var glow = new GameObject("Glow");
            glow.transform.SetParent(go.transform, false);
            glow.transform.localScale = Vector3.one * (p.frames == null ? 3f : 1.4f);
            var g = glow.AddComponent<SpriteRenderer>();
            g.sprite = Art.Glow;
            g.color = new Color(0.7f, 0.4f, 1f, 0.45f);
            g.sortingOrder = 1001;
            p.dir = direction.normalized;
            p.damage = damage;
            p.dieAt = Time.time + p.life;
        }

        void Update()
        {
            transform.position += (Vector3)(dir * speed * Time.deltaTime);
            if (frames != null) sr.sprite = frames[(int)(Time.time * 12f) % frames.Length];
            Vector2 pos = transform.position;

            if (!reflected)
            {
                var player = PlayerController.I;
                if (player != null && Vector2.Distance(pos, player.transform.position) < 0.45f)
                {
                    var result = player.ReceiveAttack(null, damage, pos - dir * 0.3f, false);
                    if (result == AttackResult.Parried)
                    {
                        // 받아치기: 방향을 뒤집고 빨라져서 적을 노린다.
                        reflected = true;
                        dir = -dir;
                        speed *= 1.8f;
                        damage += 1;
                        sr.color = new Color(1f, 0.9f, 0.4f);
                        dieAt = Time.time + life;
                        HUD.Popup(pos + Vector2.up * 0.6f, "되돌려 보내기!", new Color(1f, 0.9f, 0.4f));
                        return;
                    }
                    if (result != AttackResult.Ignored) Finish();
                    return;
                }
            }

            hits.Clear();
            Physics2D.OverlapCircle(pos, 0.2f, new ContactFilter2D().NoFilter(), hits);
            foreach (var c in hits)
            {
                if (c.isTrigger) continue;
                var enemy = c.GetComponentInParent<EnemyController>();
                if (enemy != null)
                {
                    if (!reflected) continue; // 쏜 쪽 편은 통과
                    enemy.TakeHit(damage, pos - dir, 7f);
                    enemy.Stun(0.8f);
                    Finish();
                    return;
                }
                if (c.GetComponentInParent<PlayerController>() != null || c.GetComponentInParent<NpcInteractable>() != null) continue;
                Finish(); // 벽·나무·건물
                return;
            }
            if (Time.time > dieAt) Finish();
        }

        void Finish()
        {
            RingFx.Spawn(transform.position, 0.5f, new Color(0.75f, 0.45f, 1f, 0.8f), 0.25f);
            Destroy(gameObject);
        }
    }
}
