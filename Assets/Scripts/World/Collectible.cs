using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 걸어서 닿기만 하면 줍는 숨은 수집품(잃어버린 우표). 한 번 주우면 다시 나오지 않는다.
    /// 보름달 밤에는 더 밝게 반짝이고 지도에도 표시된다.
    /// </summary>
    public class Collectible : MonoBehaviour
    {
        public static readonly List<Collectible> All = new List<Collectible>();

        public string collectId;
        public string itemId = "lost_stamp";
        public SpriteRenderer sparkle;

        string Flag => "collect:" + collectId;
        public bool Taken => GameState.HasFlag(Flag);

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            if (Taken) gameObject.SetActive(false);
        }

        void Update()
        {
            if (sparkle != null)
            {
                bool moon = NightDirector.Mood == NightMood.FullMoon;
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (moon ? 5f : 3f));
                sparkle.color = new Color(1f, 0.95f, 0.7f, (moon ? 0.35f : 0.12f) + pulse * (moon ? 0.4f : 0.15f));
                sparkle.transform.localScale = Vector3.one * (moon ? 2.4f : 1.4f);
            }

            var player = PlayerController.I;
            if (player == null || Time.timeScale == 0f || Taken) return;
            if (Vector2.Distance(player.transform.position, transform.position) > 0.8f) return;

            GameState.SetFlag(Flag);
            Inventory.Give(itemId);
            RingFx.Spawn(transform.position, 0.8f, new Color(1f, 0.9f, 0.5f, 0.9f), 0.35f);
            gameObject.SetActive(false);
        }
    }
}
