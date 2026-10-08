using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// "배달 후 변화가 보인다." 조건(플래그)에 따라 마을 오브젝트를 켜고 끈다.
    /// 예: bakery_open → 빵집 노점과 불빛, lamp_lit → 우체국 등불, 주민 위치 변화.
    /// </summary>
    public class WorldVisuals : MonoBehaviour
    {
        struct Entry
        {
            public GameObject target;
            public string condition;
        }

        public static WorldVisuals I { get; private set; }

        readonly List<Entry> entries = new List<Entry>();

        void Awake() => I = this;

        void Start()
        {
            GameState.Changed += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            GameState.Changed -= Refresh;
            if (I == this) I = null;
        }

        public void Register(GameObject target, string condition)
        {
            entries.Add(new Entry { target = target, condition = condition });
            target.SetActive(GameState.Check(condition));
        }

        void Refresh()
        {
            foreach (var e in entries)
                if (e.target != null)
                    e.target.SetActive(GameState.Check(e.condition));
        }
    }
}
