using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace BaraKitchen.Gameplay {
    // One state-driven music pair, bounded effects, and immediate work-loop cancellation.
    public sealed class KitchenAudio : MonoBehaviour {
        public KitchenGame game; public string mode="service",workId=""; public int effectsPlayed,guestCalls;
        public bool MusicPlaying=>music!=null&&music[active].isPlaying; public bool WorkPlaying=>work&&work.isPlaying; public float MusicGain=>music==null?0:music[active].volume; public int ClipCount=>clips.Count;
        AudioSource[] music; AudioSource fx,voice,work,fire; int active;float blend=1,guestCooldown; bool paused;
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        void Start(){foreach(var a in game.catalog.audio??Array.Empty<SoundAsset>())if(a.clip)clips[a.id]=a.clip;music=new[]{Source(false),Source(false)};fx=Source(false);voice=Source(false);work=Source(true);fire=Source(true);SwitchMusic("service",true);}
        AudioSource Source(bool loop){var s=gameObject.AddComponent<AudioSource>();s.playOnAwake=false;s.loop=loop;s.spatialBlend=0;s.volume=0;s.priority=loop?100:90;return s;}
        AudioClip Clip(string id)=>clips.TryGetValue(id,out var c)?c:null;
        public string DesiredMode(){if(game.phase==SessionPhase.Service&&game.stations.Any(s=>s.hazard&&s.hazard.state==HeatState.Burning))return "fire";if(game.phase==SessionPhase.Service&&!game.training&&game.remaining<=30)return "hurry";return "service";}
        void SwitchMusic(string next,bool first=false){var clip=Clip("music-"+next);if(!clip)return;mode=next;if(!first)active=1-active;var s=music[active];s.Stop();s.clip=clip;s.volume=0;s.Play();blend=first?1:0;}
        void Update(){if(music==null)return;guestCooldown-=Time.unscaledDeltaTime;bool stop=game.phase==SessionPhase.Paused;if(stop!=paused){paused=stop;foreach(var s in music){if(paused)s.Pause();else s.UnPause();}if(paused){StopWork();fx.Stop();voice.Stop();fire.Pause();}else fire.UnPause();}if(paused)return;
            string desired=DesiredMode();if(desired!=mode)SwitchMusic(desired);var current=music[active];if(current.clip&&current.time>=current.clip.length-1.1f&&blend>=1)SwitchMusic(mode);
            blend=Mathf.Min(1,blend+Time.unscaledDeltaTime/.9f);float master=(game.Settings.volume*(game.voice&&game.voice.IsActive?.32f:1f));float gain=master*game.Settings.musicVolume*(game.phase==SessionPhase.Service?.40f:.24f);music[active].volume=gain*blend;music[1-active].volume=gain*(1-blend);if(blend>=1&&music[1-active].isPlaying)music[1-active].Stop();fx.volume=voice.volume=master*game.Settings.effectsVolume;work.volume=master*game.Settings.effectsVolume*.44f;fire.volume=master*game.Settings.effectsVolume*.24f;
            bool burning=game.phase==SessionPhase.Service&&game.stations.Any(s=>s.hazard&&s.hazard.state==HeatState.Burning);if(burning&&!fire.isPlaying){fire.clip=Clip("fire");if(fire.clip)fire.Play();}else if(!burning)fire.Stop();if(game.phase!=SessionPhase.Service||!game.chef||!game.chef.working)StopWork();}
        public void Play(string id,float gain=1){if(!fx||!Clip(id)||game.Settings.volume<=0)return;fx.pitch=1+UnityEngine.Random.Range(-.035f,.035f);fx.PlayOneShot(Clip(id),gain);effectsPlayed++;}
        public void Guest(string id,float gain){if(!voice||guestCooldown>0||!Clip(id)||game.Settings.volume<=0)return;voice.pitch=UnityEngine.Random.Range(1.02f,1.18f);voice.PlayOneShot(Clip(id),gain);guestCooldown=.8f;guestCalls++;}
        public void WorkLoop(string id){if(!work||workId==id&&work.isPlaying)return;workId=id;work.clip=Clip(id);if(work.clip)work.Play();}
        public void StopWork(){workId="";if(work)work.Stop();}
        public void Cue(int frequency){Play(frequency>=950?"success":frequency>=700?"ready":frequency>=590?"new-order":frequency>=300&&frequency<400?"warning":frequency<200?"expired":frequency<300?"whoosh":"ui",.6f);}
    }
}
