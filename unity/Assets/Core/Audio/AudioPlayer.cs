using UnityEngine;

namespace GameFactory.Core.Audio
{
    /// <summary>
    /// Music + SFX buses with independent on/off (player settings). SFX use a small round-robin pool of
    /// AudioSources created once, so playing a sound never allocates.
    /// </summary>
    public sealed class AudioPlayer : MonoBehaviour
    {
        const int SfxVoices = 6;

        AudioSource _music;
        readonly AudioSource[] _sfx = new AudioSource[SfxVoices];
        int _next;

        public bool MusicOn { get; private set; } = true;
        public bool SfxOn { get; private set; } = true;
        public float MusicVolume = 0.55f;
        public float SfxVolume = 0.9f;

        void Awake()
        {
            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;
            _music.spatialBlend = 0f;
            for (var i = 0; i < SfxVoices; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                _sfx[i] = s;
            }
        }

        public void Apply(bool music, bool sfx)
        {
            MusicOn = music;
            SfxOn = sfx;
            _music.volume = music ? MusicVolume : 0f;
        }

        public void PlayMusic(AudioClip clip)
        {
            if (clip == null) return;
            if (_music.clip != clip) _music.clip = clip;
            _music.volume = MusicOn ? MusicVolume : 0f;
            if (!_music.isPlaying) _music.Play();
        }

        public void PauseMusic(bool paused)
        {
            if (paused) _music.Pause();
            else if (_music.clip != null) _music.UnPause();
        }

        /// <summary>Fire-and-forget SFX. Pitch variation keeps repeated pickups from sounding mechanical.</summary>
        public void Play(AudioClip clip, float volume = 1f, float pitchJitter = 0f)
        {
            if (!SfxOn || clip == null) return;
            var s = _sfx[_next];
            _next = (_next + 1) % SfxVoices;
            s.pitch = pitchJitter > 0f ? 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter) : 1f;
            s.PlayOneShot(clip, volume * SfxVolume);
        }
    }
}
