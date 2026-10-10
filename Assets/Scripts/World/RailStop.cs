using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 철길 빠른 이동. 폐역 · 북쪽 숲 신호소 · 고개 역에 하나씩 있다.
    /// 13번째 편지를 배달하면(bau_met) 바우가 철길 손수레를 내주고, 첫 열차가 달린 뒤(train_runs)에는 우편 열차가 된다.
    /// 타면 다른 정거장을 고르고, 화면이 어두워졌다 밝아지면서 그 정거장 승강장에 내린다.
    /// </summary>
    public class RailStop : Interactable
    {
        public const string UnlockFlag = "bau_met";
        public static readonly List<RailStop> Stops = new List<RailStop>();

        public string stopName;
        /// <summary>내리는 자리(승강장 쪽, 걸을 수 있는 곳).</summary>
        public Vector2 arrival;
        /// <summary>목록에 보이는 순서(서쪽부터).</summary>
        public int order;

        public static bool Unlocked => GameState.HasFlag(UnlockFlag);
        static string Vehicle => GameState.HasFlag("train_runs") ? "우편 열차" : "철길 손수레";

        public override string DisplayName => Unlocked ? Vehicle + " · " + stopName : "녹슨 철길 손수레";
        public override string Prompt => Unlocked ? Vehicle + " 타기 (" + stopName + ")" : "녹슨 손수레 살펴보기";

        protected override void OnEnable()
        {
            base.OnEnable();
            Stops.Add(this);
        }

        /// <summary>서쪽부터 차례로 정렬한 정거장 목록(order 는 AddComponent 뒤에 정해지므로 쓸 때 정렬한다).</summary>
        public static List<RailStop> Ordered()
        {
            var list = new List<RailStop>(Stops);
            list.Sort((a, b) => a.order.CompareTo(b.order));
            return list;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Stops.Remove(this);
        }

        public override void Interact(PlayerController player)
        {
            if (!Unlocked)
            {
                string line = GameState.HasFlag("tunnel_open")
                    ? "녹슨 철길 손수레다. 고개 역의 역장이라면 이걸 움직이는 법을 알지도 모른다."
                    : "녹슨 철길 손수레다. 철길 끝 터널이 무너진 뒤로 아무도 타지 않은 것 같다.";
                DialogueSystem.I.Show(new[] { new DialogueLine("", line) });
                return;
            }

            var destinations = new List<RailStop>();
            foreach (var s in Ordered())
                if (s != this) destinations.Add(s);
            var texts = new string[destinations.Count + 1];
            for (int i = 0; i < destinations.Count; i++) texts[i] = Josa.Euro(destinations[i].stopName) + " 가기";
            texts[destinations.Count] = "그만두기";

            DialogueSystem.I.Show(new[] { new DialogueLine("", Vehicle + "에 올랐다. 어디로 갈까?") }, texts, index =>
            {
                if (index < destinations.Count) Travel(destinations[index], player);
            });
        }

        /// <summary>화면을 어둡게 했다가 dest 승강장에 내린다.</summary>
        public static void Travel(RailStop dest, PlayerController player)
        {
            Sound.Play("Tool", 0.6f);
            GameMenu.FadeThrough(() =>
            {
                Place(player, dest.arrival);
                HUD.Toast(dest.stopName + "에 도착했다.");
            });
        }

        public static void Place(PlayerController player, Vector2 pos)
        {
            var rb = player.GetComponent<Rigidbody2D>();
            rb.position = pos;
            rb.SetVelocity(Vector2.zero);
            player.transform.position = pos;
            if (CameraFollow.I != null) CameraFollow.I.SnapToTarget();
        }
    }
}
