using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace BaraKitchen.Gameplay {
    // Voice-only companion. It never captures controls, pauses service, or performs game actions.
    public sealed class KitchenVoiceGuide : MonoBehaviour {
        public KitchenGame game;public string State {get;private set;}="off";
        public bool IsActive=>State=="connected";
        public KitchenStation Target {get;private set;}public bool ShowingTarget=>IsActive&&Target&&Time.unscaledTime<targetUntil;
        public float IdleSeconds {get;private set;}
        float nextContext,targetUntil,errorUntil;string error="";Vector3 lastPosition;Button button;Label status;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void BaraLiveStart(string name,string context,float volume);
        [DllImport("__Internal")] static extern void BaraLiveContext(string context,float volume);
        [DllImport("__Internal")] static extern void BaraLiveStop();
#else
        NativeLiveVoice native;
#endif
        [Serializable] class Event {public string type,message,targetId,delta;}
        void Awake(){
#if !UNITY_WEBGL || UNITY_EDITOR
            native=gameObject.AddComponent<NativeLiveVoice>();native.guide=this;
#endif
        }
        void Update(){if(!game||!game.chef)return;
            if(game.phase==SessionPhase.Service){if(game.chef.working||Input.anyKeyDown||Vector3.Distance(lastPosition,game.chef.transform.position)>.02f)IdleSeconds=0;else IdleSeconds+=Time.deltaTime;}lastPosition=game.chef.transform.position;
            if(Input.GetKeyDown(KeyCode.V))Toggle();
            if(IsActive&&Time.unscaledTime>=nextContext){nextContext=Time.unscaledTime+2;string context=JsonUtility.ToJson(KitchenHelpContext.Capture(game,game.phase));
#if UNITY_WEBGL && !UNITY_EDITOR
                BaraLiveContext(context,game.Settings.volume);
#else
                native.UpdateContext(context);
#endif
            }
        }
        void LateUpdate(){if(!game||!game.ui)return;var doc=GetComponent<UIDocument>();if(!doc)return;var root=doc.rootVisualElement;
            if(button==null||button.parent!=root){button=new Button(Toggle){name="live-chat-toggle"};button.AddToClassList("live-chat-toggle");button.focusable=false;button.tooltip="Live AI voice chat. Microphone audio and kitchen context go to OpenAI while chat is on. Tap again to end.";root.Add(button);status=new Label();status.AddToClassList("live-chat-status");root.Add(status);}
            button.text=State=="connecting"?"Connecting…":State=="closing"?"Ending chat…":IsActive?"End chat · V":"AI chat · V";button.EnableInClassList("live",IsActive);
            status.text=Time.unscaledTime<errorUntil?error:IsActive?"Mic on · OpenAI":State=="connecting"?"Allow microphone access":"";
            status.style.display=string.IsNullOrEmpty(status.text)?DisplayStyle.None:DisplayStyle.Flex;
        }
        public void Toggle(){if(State=="closing")return;if(IsActive||State=="connecting"){Close();return;}Open();}
        public void Open(){if(State=="connecting"||IsActive||State=="closing"||!game||!game.chef)return;State="connecting";errorUntil=0;Target=null;nextContext=0;string context=JsonUtility.ToJson(KitchenHelpContext.Capture(game,game.phase));
#if UNITY_WEBGL && !UNITY_EDITOR
            BaraLiveStart(gameObject.name,context,game.Settings.volume);
#else
            native.Connect(context);
#endif
        }
        public void Close(){Target=null;if(State=="off")return;State="closing";
#if UNITY_WEBGL && !UNITY_EDITOR
            BaraLiveStop();
#else
            native.Stop();
#endif
        }
        public void OnLiveEvent(string json){Event e;try{e=JsonUtility.FromJson<Event>(json);}catch{return;}if(e==null)return;
            if(e.type=="connected"||e.type=="session.started"){State="connected";nextContext=0;if(!Application.isFocused&&!Application.isBatchMode)Close();}
            else if(e.type=="closed"||e.type=="bara.closed"||e.type=="session.closed"){State="off";Target=null;}
            else if(e.type=="closing")State="closing";
            else if(e.type=="error"||e.type=="bara.error"){error=string.IsNullOrEmpty(e.message)?"Start the local GPT-Live helper, then tap Chat.":e.message;errorUntil=Time.unscaledTime+12;State="off";Target=null;}
            else if(e.type=="target"||e.type=="bara.target"){Target=game.stations.FirstOrDefault(s=>s.id==e.targetId);targetUntil=Time.unscaledTime+10;}
        }
        public void CancelForSceneChange(){Target=null;IdleSeconds=0;nextContext=0;}
        void OnApplicationFocus(bool focused){if(!focused&&IsActive)Close();}
        void OnApplicationQuit(){Close();}
        void OnDisable(){Close();}
    }
}
