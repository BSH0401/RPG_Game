using UnityEngine;

namespace MoonlightPost
{
    /// <summary>한국어 조사를 앞 낱말의 받침에 맞춰 고른다. 예: Josa.Eul("문어") → "문어를".</summary>
    public static class Josa
    {
        public static string Eul(string word) => word + Pick(word, "을", "를");
        public static string Ga(string word) => word + Pick(word, "이", "가");
        public static string Gwa(string word) => word + Pick(word, "과", "와");

        const string Skip = "」』)]\"'”’ ";

        /// <summary>마지막 글자(닫는 괄호·따옴표는 건너뜀)에 받침이 있으면 withFinal. 한글·숫자가 아니면 둘 다 보여준다.</summary>
        static string Pick(string word, string withFinal, string withoutFinal)
        {
            for (int i = (word?.Length ?? 0) - 1; i >= 0; i--)
            {
                char c = word[i];
                if (Skip.IndexOf(c) >= 0) continue;
                if (c >= '가' && c <= '힣') return (c - '가') % 28 != 0 ? withFinal : withoutFinal;
                if (char.IsDigit(c)) return "013678".IndexOf(c) >= 0 ? withFinal : withoutFinal;
                break;
            }
            return withFinal + "(" + withoutFinal + ")";
        }
    }

    public static class PhysicsCompat
    {
        public static void SetVelocity(this Rigidbody2D rb, Vector2 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }
    }
}
