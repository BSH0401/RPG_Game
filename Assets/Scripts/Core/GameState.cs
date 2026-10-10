using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MoonlightPost
{
    [Serializable]
    public class ItemStack
    {
        public string id;
        public int count;
    }

    [Serializable]
    public class SaveData
    {
        public List<ItemStack> items = new List<ItemStack>();
        /// <summary>지금 끼운 편지 도구 부품(아이템 id).</summary>
        public string toolPart = "seal_string";
        public List<string> flags = new List<string>();
        public string carryingLetterId = "";
        public int night;
        public int maxHp = GameState.BaseMaxHp;
    }

    /// <summary>
    /// 진행 상태(플래그, 들고 있는 편지, 밤 번호, 최대 체력)와 저장·불러오기.
    /// 스토리 분기는 모두 문자열 플래그로 표현하고, 데이터(JSON)에서 조건으로 검사한다.
    /// </summary>
    public static class GameState
    {
        public const int BaseMaxHp = 6;
        const string CarryingPrefix = "carrying:";
        const string HasPrefix = "has:";

        static SaveData data = new SaveData();
        static readonly HashSet<string> flags = new HashSet<string>();

        /// <summary>플래그·편지·체력 등 무엇이든 바뀌면 호출된다.</summary>
        public static event Action Changed;
        /// <summary>새 밤이 시작되면 호출된다(숲의 길과 적 배치가 바뀐다).</summary>
        public static event Action NightChanged;

        /// <summary>저장 파일 이름. 자동 플레이테스트는 다른 이름을 써서 플레이어의 저장을 건드리지 않는다.</summary>
        public static string SaveFileName = "moonlight_post_save.json";
        static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        public static int Night => data.night;
        /// <summary>기본 체력 + 장비(maxHp 효과) 보너스.</summary>
        public static int MaxHp => data.maxHp + Mathf.RoundToInt(Inventory.EffectSum("maxHp"));
        public static IReadOnlyList<ItemStack> Items => data.items;
        public static string ToolPart => data.toolPart;

        public static void SetToolPart(string id)
        {
            data.toolPart = id;
            Changed?.Invoke();
        }
        public static string CarryingLetterId => data.carryingLetterId;

        public static bool HasFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag)) return true;
            if (flag.StartsWith(CarryingPrefix, StringComparison.Ordinal))
                return data.carryingLetterId == flag.Substring(CarryingPrefix.Length);
            if (flag.StartsWith(HasPrefix, StringComparison.Ordinal))
                return ItemCount(flag.Substring(HasPrefix.Length)) > 0;
            return flags.Contains(flag);
        }

        // ---------------------------------------------------------------- 소지품

        public static int ItemCount(string id)
        {
            foreach (var s in data.items)
                if (s.id == id) return s.count;
            return 0;
        }

        /// <summary>아이템을 더한다. 처음 얻으면 "got:아이템id" 플래그가 선다(대사 조건용).</summary>
        public static void AddItem(string id, int count = 1, int maxStack = 99)
        {
            if (string.IsNullOrEmpty(id) || count <= 0) return;
            ItemStack stack = null;
            foreach (var s in data.items)
                if (s.id == id) stack = s;
            if (stack == null)
            {
                stack = new ItemStack { id = id, count = 0 };
                data.items.Add(stack);
            }
            stack.count = Mathf.Min(maxStack, stack.count + count);
            flags.Add("got:" + id);
            Changed?.Invoke();
        }

        public static bool RemoveItem(string id, int count = 1)
        {
            for (int i = 0; i < data.items.Count; i++)
            {
                var s = data.items[i];
                if (s.id != id || s.count < count) continue;
                s.count -= count;
                if (s.count <= 0) data.items.RemoveAt(i);
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>
        /// "a,b,!c" 형식의 조건을 검사한다. 쉼표는 AND, 느낌표는 NOT. 빈 조건은 항상 참.
        /// 세로줄은 OR: "a,b|c" = (a 그리고 b) 또는 c.
        /// "carrying:편지id" 는 해당 편지를 들고 있을 때 참.
        /// </summary>
        public static bool Check(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition)) return true;
            foreach (var group in condition.Split('|'))
                if (CheckAll(group)) return true;
            return false;
        }

        static bool CheckAll(string condition)
        {
            foreach (var raw in condition.Split(','))
            {
                var term = raw.Trim();
                if (term.Length == 0) continue;
                bool negate = term[0] == '!';
                if (negate) term = term.Substring(1).Trim();
                if (HasFlag(term) == negate) return false;
            }
            return true;
        }

        public static void SetFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag)) return;
            if (flags.Add(flag)) Changed?.Invoke();
        }

        public static void SetCarrying(string letterId)
        {
            data.carryingLetterId = letterId ?? "";
            Changed?.Invoke();
        }

        public static void AddMaxHp(int amount)
        {
            data.maxHp += amount;
            Changed?.Invoke();
        }

        public static void AdvanceNight()
        {
            data.night++;
            NightChanged?.Invoke();
            Changed?.Invoke();
        }

        public static void Save()
        {
            data.flags = new List<string>(flags);
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[달빛 우체국] 저장 실패: " + e.Message);
            }
        }

        /// <summary>저장 파일이 있으면 불러오고, 없으면 새 게임 상태로 시작한다.</summary>
        public static void Load()
        {
            // 씬을 다시 불러올 때 이전 오브젝트의 구독이 남지 않도록 비운다.
            Changed = null;
            NightChanged = null;

            data = new SaveData();
            flags.Clear();
            if (File.Exists(SavePath))
            {
                try
                {
                    var loaded = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                    if (loaded != null) data = loaded;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[달빛 우체국] 저장 파일을 읽지 못해 새로 시작합니다: " + e.Message);
                }
            }
            if (data.flags == null) data.flags = new List<string>();
            if (data.carryingLetterId == null) data.carryingLetterId = "";
            if (data.items == null) data.items = new List<ItemStack>();
            if (string.IsNullOrEmpty(data.toolPart)) data.toolPart = "seal_string";
            // 기본 편지 도구는 처음부터 가지고 있다.
            if (!data.items.Exists(s => s.id == "seal_string")) data.items.Insert(0, new ItemStack { id = "seal_string", count = 1 });
            if (data.maxHp <= 0) data.maxHp = BaseMaxHp;
            foreach (var f in data.flags) flags.Add(f);

            // 예전 저장 파일: 우편가방 보상이 최대 체력 숫자로만 저장돼 있으면 아이템으로 바꾼다.
            if (data.maxHp > BaseMaxHp && ItemCount("mail_bag") == 0 && flags.Contains("delivered:flour_letter"))
            {
                data.maxHp = BaseMaxHp;
                data.items.Add(new ItemStack { id = "mail_bag", count = 1 });
                flags.Add("got:mail_bag");
            }
        }

        public static void DeleteSave()
        {
            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[달빛 우체국] 저장 파일 삭제 실패: " + e.Message);
            }
        }
    }
}
