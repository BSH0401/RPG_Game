using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 낀 무기를 배달부 등에 메고 다니는 모습과, 무기 강화 단계에 따른 빛.
    ///   +3 ~ +5  푸른 빛: 무기가 은은하게 빛나고, 베기에 푸른 궤적
    ///   +6 ~ +8  보랏빛: 빛이 커지고 반짝이가 피어오르며, 맞힌 자리에 불꽃
    ///   +9 ~ +10 금빛: 발밑에 금빛 고리, 주위를 도는 별, 마무리 일격에 빛의 고리(+10 은 더 크게)
    /// 공격하는 동안은 칼을 든 동작 그림이 따로 있으므로 등의 무기를 잠깐 숨긴다.
    /// </summary>
    public class WeaponVisual : MonoBehaviour
    {
        public static WeaponVisual I { get; private set; }

        /// <summary>강화 단계 → 빛의 단계(0 없음, 1 푸른, 2 보랏빛, 3 금빛).</summary>
        public static int TierOf(int plus) => plus >= 9 ? 3 : plus >= 6 ? 2 : plus >= 3 ? 1 : 0;

        public static Color TierColor(int tier)
        {
            switch (tier)
            {
                case 1: return new Color(0.45f, 0.78f, 1f);
                case 2: return new Color(0.78f, 0.48f, 1f);
                case 3: return new Color(1f, 0.83f, 0.35f);
                default: return Color.white;
            }
        }

        public static string TierName(int tier) => tier == 3 ? "금빛" : tier == 2 ? "보랏빛" : tier == 1 ? "푸른 빛" : "";

        /// <summary>지금 낀 무기의 빛의 단계.</summary>
        public static int Tier
        {
            get
            {
                var w = GameState.EquippedIn(GearSlot.Weapon);
                return w != null ? TierOf(w.plus) : 0;
            }
        }

        public static int Plus
        {
            get
            {
                var w = GameState.EquippedIn(GearSlot.Weapon);
                return w != null ? w.plus : 0;
            }
        }

        SpriteRenderer body, weapon, weaponGlow, aura, footRing;
        Transform holder;
        readonly SpriteRenderer[] motes = new SpriteRenderer[3];
        int shownVersion = -1;
        float hideUntil, nextSpark;

        public void Init(SpriteRenderer bodyRenderer)
        {
            I = this;
            body = bodyRenderer;
            holder = new GameObject("BackWeapon").transform;
            holder.SetParent(transform, false);
            weapon = holder.gameObject.AddComponent<SpriteRenderer>();
            // 400 이상이면 YSort 가 건드리지 않는다(순서는 LateUpdate 에서 몸 기준으로 직접 정한다).
            weapon.sortingOrder = 500;
            weaponGlow = Child("WeaponGlow", holder, Art.Glow, 1001);
            aura = Child("WeaponAura", transform, Art.Glow, 1001);
            footRing = Child("WeaponRing", transform, SpriteFactory.Ring, -440);
            footRing.transform.localPosition = new Vector3(0f, -0.38f, 0f);
            footRing.transform.localScale = new Vector3(1.4f, 0.55f, 1f);
            for (int i = 0; i < motes.Length; i++)
            {
                motes[i] = Child("Mote", transform, Art.Glow, 1001);
                motes[i].transform.localScale = Vector3.one * 0.35f;
            }
            Refresh();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        static SpriteRenderer Child(string name, Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        /// <summary>공격 동작 동안 등의 무기를 숨긴다.</summary>
        public static void Hide(float seconds)
        {
            if (I != null) I.hideUntil = Mathf.Max(I.hideUntil, Time.time + seconds);
        }

        void Refresh()
        {
            shownVersion = GameState.GearVersion;
            var w = GameState.EquippedIn(GearSlot.Weapon);
            weapon.sprite = w != null && GameAssets.Available ? GameAssets.ItemSprite(w.Base.icon) : null;
        }

        void LateUpdate()
        {
            if (body == null) return;
            if (shownVersion != GameState.GearVersion) Refresh();
            var player = PlayerController.I;
            Vector2 face = player != null ? player.Facing : Vector2.down;
            int tier = Tier;
            Color c = TierColor(tier);
            float t = Time.time;
            bool visible = weapon.sprite != null && body.enabled && body.color.a > 0.6f && Time.time >= hideUntil;

            // 등에 멘 무기: 칼날이 어깨 위로 비스듬히. 위를 보면(등이 보이면) 몸 앞에, 그 밖에는 몸 뒤에 그린다.
            bool side = Mathf.Abs(face.x) > Mathf.Abs(face.y);
            bool up = !side && face.y > 0f;
            float sx = side ? -Mathf.Sign(face.x) * 0.12f : 0.1f;
            holder.localPosition = new Vector3(sx, 0.05f, 0f);
            holder.localRotation = Quaternion.Euler(0f, 0f, side ? Mathf.Sign(face.x) * 30f : up ? 28f : -28f);
            holder.localScale = Vector3.one * 0.7f;
            weapon.enabled = visible;
            weapon.sortingOrder = body.sortingOrder + (up ? 1 : -1);
            weapon.color = tier > 0 ? Color.Lerp(Color.white, c, 0.25f + 0.1f * Mathf.Sin(t * 3f)) : Color.white;

            // 무기 둘레의 빛
            weaponGlow.enabled = visible && tier > 0;
            if (weaponGlow.enabled)
            {
                weaponGlow.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                weaponGlow.transform.localScale = Vector3.one * (0.9f + 0.35f * tier) * (1f + 0.08f * Mathf.Sin(t * 4f));
                weaponGlow.color = new Color(c.r, c.g, c.b, 0.35f + 0.1f * tier);
            }

            // 몸 둘레의 은은한 빛(보랏빛부터)
            aura.enabled = tier >= 2 && body.enabled;
            if (aura.enabled)
            {
                aura.transform.localPosition = new Vector3(0f, 0.2f, 0f);
                aura.transform.localScale = Vector3.one * (tier == 3 ? 3.2f : 2.4f) * (1f + 0.05f * Mathf.Sin(t * 2f));
                aura.color = new Color(c.r, c.g, c.b, tier == 3 ? 0.22f : 0.14f);
            }

            // 금빛: 발밑 고리와 주위를 도는 별
            footRing.enabled = tier >= 3 && body.enabled;
            if (footRing.enabled)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.5f);
                footRing.color = new Color(c.r, c.g, c.b, 0.35f + 0.25f * pulse);
                footRing.transform.localScale = new Vector3(1.05f + 0.12f * pulse, 0.42f + 0.05f * pulse, 1f) * (Plus >= 10 ? 1.25f : 1f);
            }
            for (int i = 0; i < motes.Length; i++)
            {
                motes[i].enabled = tier >= 3 && body.enabled;
                if (!motes[i].enabled) continue;
                float a = t * 1.8f + i * Mathf.PI * 2f / motes.Length;
                motes[i].transform.localPosition = new Vector3(Mathf.Cos(a) * 0.75f, 0.25f + Mathf.Sin(a) * 0.3f, 0f);
                motes[i].color = new Color(c.r, c.g, c.b, 0.85f);
            }

            // 보랏빛부터 반짝이가 피어오른다
            if (tier >= 2 && body.enabled && Time.time >= nextSpark && Time.timeScale > 0f)
            {
                nextSpark = Time.time + (tier == 3 ? 0.18f : 0.32f);
                Vector2 p = (Vector2)transform.position + new Vector2(Random.Range(-0.45f, 0.45f), Random.Range(-0.2f, 0.7f));
                Spark.Spawn(p, c, tier == 3 ? 0.4f : 0.3f);
            }
        }

        // ------------------------------------------------------------------ 공격 이펙트 (PlayerController 가 부른다)

        /// <summary>베기마다: 단계 색의 궤적. 금빛 마무리 일격은 빛의 고리.</summary>
        public static void OnSwing(Vector2 origin, Vector2 aim, bool finisher)
        {
            int tier = Tier;
            if (tier == 0 || !GameAssets.Available) return;
            Color c = TierColor(tier);
            var go = FrameAnimator.PlayOnce(GameAssets.SlashFrames, origin + aim * 0.75f, 22f, new Color(c.r, c.g, c.b, 0.85f), 650,
                (finisher ? 1.6f : 1.2f) + 0.15f * tier);
            if (go != null) go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg + 90f);
            if (tier >= 3 && finisher)
                RingFx.Spawn(origin + aim * 0.8f, Plus >= 10 ? 2.4f : 1.8f, new Color(c.r, c.g, c.b, 0.9f), 0.35f);
        }

        /// <summary>맞힌 자리마다(보랏빛부터): 작은 빛 터짐.</summary>
        public static void OnHit(Vector2 at, bool crit)
        {
            int tier = Tier;
            if (tier < 2) return;
            Color c = TierColor(tier);
            RingFx.Spawn(at, crit ? 0.9f : 0.6f, new Color(c.r, c.g, c.b, 0.9f), 0.2f);
            for (int i = 0; i < (tier == 3 ? 4 : 2); i++) Spark.Spawn(at + Random.insideUnitCircle * 0.3f, c, 0.35f);
        }
    }

    /// <summary>위로 떠오르며 사라지는 작은 빛 하나.</summary>
    public class Spark : MonoBehaviour
    {
        SpriteRenderer sr;
        Color color;
        float born, life = 0.7f, size;
        Vector2 vel;

        public static void Spawn(Vector2 pos, Color color, float size)
        {
            var go = new GameObject("Spark");
            go.transform.position = pos;
            var s = go.AddComponent<Spark>();
            s.sr = go.AddComponent<SpriteRenderer>();
            s.sr.sprite = Art.Glow;
            s.sr.sortingOrder = 1002;
            s.color = color;
            s.size = size;
            s.born = Time.time;
            s.vel = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.6f, 1.1f));
        }

        void Update()
        {
            float k = (Time.time - born) / life;
            if (k >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            transform.position += (Vector3)(vel * Time.deltaTime);
            transform.localScale = Vector3.one * size * (1f - 0.5f * k);
            sr.color = new Color(color.r, color.g, color.b, 0.9f * (1f - k));
        }
    }
}
