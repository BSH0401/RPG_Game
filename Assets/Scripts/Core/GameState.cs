using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MoonlightPost
{
    [Serializable]
    public class SaveData
    {
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

        static SaveData data = new SaveData();
        static readonly HashSet<string> flags = new HashSet<string>();

        /// <summary>플래그·편지·체력 등 무엇이든 바뀌면 호출된다.</summary>
        public static event Action Changed;
        /// <summary>새 밤이 시작되면 호출된다(숲의 길과 적 배치가 바뀐다).</summary>
        public static event Action NightChanged;

        static string SavePath => Path.Combine(Application.persistentDataPath, "moonlight_post_save.json");

        public static int Night => data.night;
        public static int MaxHp => data.maxHp;
        public static string CarryingLetterId => data.carryingLetterId;

        public static bool HasFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag)) return true;
            if (flag.StartsWith(CarryingPrefix, StringComparison.Ordinal))
                return data.carryingLetterId == flag.Substring(CarryingPrefix.Length);
            return flags.Contains(flag);
        }

        /// <summary>
        /// "a,b,!c" 형식의 조건을 검사한다. 쉼표는 AND, 느낌표는 NOT. 빈 조건은 항상 참.
        /// "carrying:편지id" 는 해당 편지를 들고 있을 때 참.
        /// </summary>
        public static bool Check(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition)) return true;
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
            if (data.maxHp <= 0) data.maxHp = BaseMaxHp;
            foreach (var f in data.flags) flags.Add(f);
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
