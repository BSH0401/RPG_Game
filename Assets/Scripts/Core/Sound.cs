using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 효과음과 배경음악. 에셋이 없으면 조용히 아무것도 하지 않는다.
    /// 음악: 마을 / 숲 / 보스전을 플레이어 위치와 상황에 따라 서서히 바꾼다.
    /// </summary>
    public class Sound : MonoBehaviour
    {
        static Sound instance;

        public float sfxVolume = 0.6f;
        public float musicVolume = 0.35f;
        /// <summary>이 x 좌표보다 오른쪽이면 숲 음악.</summary>
        public float forestStartX = 17f;

        AudioSource sfx;
        AudioSource musicA, musicB;
        AudioClip village, forest, boss, current;

        void Awake()
        {
            instance = this;
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            musicA = MakeMusicSource();
            musicB = MakeMusicSource();
            if (GameAssets.Available)
            {
                village = GameAssets.Music("Village");
                forest = GameAssets.Music("Forest");
                boss = GameAssets.Music("Boss");
            }
        }

        AudioSource MakeMusicSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = true;
            s.playOnAwake = false;
            s.volume = 0f;
            return s;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void Play(string name, float volume = 1f)
        {
            if (instance == null || !GameAssets.Available) return;
            var clip = GameAssets.Sfx(name);
            if (clip != null) instance.sfx.PlayOneShot(clip, instance.sfxVolume * volume);
        }

        void Update()
        {
            var player = PlayerController.I;
            if (player == null) return;

            AudioClip wanted = player.transform.position.x > forestStartX ? forest : village;
            foreach (var e in EnemyController.Active)
                if (e.isBoss && Vector2.Distance(e.transform.position, player.transform.position) < 12f) wanted = boss;

            if (wanted != current && wanted != null)
            {
                // 지금 소리가 작은 쪽 소스에서 새 곡을 시작하고 서로 교차시킨다.
                var next = musicA.volume <= musicB.volume ? musicA : musicB;
                next.clip = wanted;
                next.Play();
                current = wanted;
            }

            foreach (var s in new[] { musicA, musicB })
            {
                float target = s.clip == current && s.isPlaying ? musicVolume : 0f;
                s.volume = Mathf.MoveTowards(s.volume, target, Time.unscaledDeltaTime * musicVolume / 1.5f);
                if (s.volume <= 0f && s.clip != current && s.isPlaying) s.Stop();
            }
        }
    }
}
