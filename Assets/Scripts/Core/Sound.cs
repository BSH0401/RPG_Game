using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 효과음과 배경음악. 에셋이 없으면 조용히 아무것도 하지 않는다.
    /// 음악: 지역(마을·숲·해안·폐역·별빛 고개·채석장)과 보스전에 따라 서서히 바꾼다. 첫 열차가 달린 뒤 마을은 엔딩 곡.
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
        AudioClip village, forest, boss, coast, station, pass, quarry, ending, current;

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
                coast = GameAssets.Music("Coast");
                station = GameAssets.Music("Station");
                pass = GameAssets.Music("Pass");
                quarry = GameAssets.Music("Quarry");
                ending = GameAssets.Music("Ending");
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

        /// <summary>지역별 곡. 없는 곡은 마을/숲 곡으로 대신한다.</summary>
        AudioClip RegionMusic(Vector2 p)
        {
            AudioClip clip;
            if (p.x > 52.5f) clip = p.y > 11f ? pass : quarry;           // 2부: 별빛 고개 / 옛 채석장
            else if (p.x < -16.5f) clip = coast;                         // 서쪽 해안·북쪽 갯바위
            else if (p.x > forestStartX) clip = forest;                  // 동쪽 숲·북쪽 숲
            else if (p.y > 11f) clip = station;                          // 폐역
            else clip = GameState.HasFlag("train_runs") && ending != null ? ending : village;
            if (clip != null) return clip;
            return p.x > forestStartX ? forest : village;
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

            AudioClip wanted = RegionMusic(player.transform.position);
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
