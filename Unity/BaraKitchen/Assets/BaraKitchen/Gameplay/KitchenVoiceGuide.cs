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
        // Deliberately private and non-serialized: never PlayerPrefs, telemetry or disk.
        string sessionKey="";bool setupAfterError;VisualElement keyDialog;TextField keyField;Label keyWarning;
        float setupTimeScale;SessionPhase setupPhase;
        public bool IsConfiguring=>keyDialog!=null;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void BaraLiveStart(string name,string context,float volume,string apiKey);
        [DllImport("__Internal")] static extern void BaraLiveContext(string context,float volume);
        [DllImport("__Internal")] static extern void BaraLiveStop();
        [DllImport("__Internal")] static extern void BaraVoiceKeyShow(string name,string value);
        [DllImport("__Internal")] static extern void BaraVoiceKeyLayout(float x,float y,float w,float h);
        [DllImport("__Internal")] static extern string BaraVoiceKeyValue();
        [DllImport("__Internal")] static extern void BaraVoiceKeyClear();
        [DllImport("__Internal")] static extern void BaraVoiceKeyHide();
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
            if(IsConfiguring){if(Input.GetKeyDown(KeyCode.Escape))DismissSetup();return;}
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
            button.text=State=="connecting"?"Connecting…":State=="closing"?"Ending chat…":IsActive?"End chat · V":"AI chat · V";button.EnableInClassList("live",IsActive);button.SetEnabled(!IsConfiguring);
            status.text=Time.unscaledTime<errorUntil?error:IsActive?"Mic on · OpenAI":State=="connecting"?"Allow microphone access":"";
            status.style.display=string.IsNullOrEmpty(status.text)?DisplayStyle.None:DisplayStyle.Flex;
#if UNITY_WEBGL && !UNITY_EDITOR
            if(IsConfiguring){var r=keyField.worldBound;float x=keyField.labelElement.worldBound.xMax+4;BaraVoiceKeyLayout(x/root.resolvedStyle.width,r.y/root.resolvedStyle.height,(r.xMax-x)/root.resolvedStyle.width,r.height/root.resolvedStyle.height);}
#endif
        }
        public void Toggle(){if(IsConfiguring||State=="closing")return;if(IsActive||State=="connecting"){Close();return;}Open();}
        public void Open(){if(IsConfiguring||State=="connecting"||IsActive||State=="closing"||!game||!game.chef)return;if(string.IsNullOrEmpty(sessionKey)||setupAfterError){Configure();return;}State="connecting";errorUntil=0;Target=null;nextContext=0;string context=JsonUtility.ToJson(KitchenHelpContext.Capture(game,game.phase));
#if UNITY_WEBGL && !UNITY_EDITOR
            BaraLiveStart(gameObject.name,context,game.Settings.volume,sessionKey);
#else
            native.Connect(context,sessionKey);
#endif
        }
        public void Close(){if(IsConfiguring){DismissSetup();return;}Target=null;if(State=="off")return;State="closing";
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
            else if(e.type=="error"||e.type=="bara.error"){error=string.IsNullOrEmpty(e.message)?"Start the local GPT-Live helper, then tap Chat.":e.message;errorUntil=Time.unscaledTime+12;setupAfterError=true;State="off";Target=null;}
            else if(e.type=="target"||e.type=="bara.target"){Target=game.stations.FirstOrDefault(s=>s.id==e.targetId);targetUntil=Time.unscaledTime+10;}
        }
        public void Configure(){
            if(IsConfiguring||State=="connecting"||State=="closing"||IsActive)return;
            var document=GetComponent<UIDocument>();if(!document)return;
            setupTimeScale=Time.timeScale;setupPhase=game.phase;Time.timeScale=0;State="setup";
            keyDialog=new VisualElement{name="voice-key-dialog"};keyDialog.AddToClassList("voice-key-overlay");
            var panel=new VisualElement();panel.AddToClassList("voice-key-panel");keyDialog.Add(panel);
            var portrait=new VisualElement();portrait.AddToClassList("voice-key-portrait");portrait.style.backgroundImage=game.catalog.chefs[0].icon;panel.Add(portrait);
            var title=new Label("Chat with Bara");title.AddToClassList("voice-key-title");title.style.unityFont=game.catalog.titleFont;panel.Add(title);
            var note=new Label("Bring your own OpenAI API key.");note.AddToClassList("voice-key-copy");panel.Add(note);
            keyField=new TextField("API key"){name="voice-api-key",isPasswordField=true,maxLength=512,value=sessionKey};keyField.AddToClassList("voice-key-input");panel.Add(keyField);
            var privacy=new Label("Kept only for this play session. Never saved.\nVoice chat uses your paid OpenAI API access.");privacy.AddToClassList("voice-key-copy");panel.Add(privacy);
            var helper=new Label("Run the included Voice Helper on your computer first.\nYour key goes only to that local helper and OpenAI.");helper.AddToClassList("voice-key-help");panel.Add(helper);
            keyWarning=new Label();keyWarning.AddToClassList("voice-key-error");panel.Add(keyWarning);
            var connect=new Button(ConfirmKeySetup){text="Connect & chat"};connect.AddToClassList("primary");panel.Add(connect);
            var row=new VisualElement();row.AddToClassList("button-row");panel.Add(row);
            var cancel=new Button(DismissSetup){text="Not now"};cancel.AddToClassList("secondary");row.Add(cancel);
            var forget=new Button(()=>{sessionKey="";keyField.value="";
#if UNITY_WEBGL && !UNITY_EDITOR
                BaraVoiceKeyClear();
#endif
                keyWarning.text="Key forgotten. Normal gameplay is always available.";}){text="Forget key"};forget.AddToClassList("secondary");row.Add(forget);
            document.rootVisualElement.Add(keyDialog);
#if UNITY_WEBGL && !UNITY_EDITOR
            keyField.SetEnabled(false);BaraVoiceKeyShow(gameObject.name,sessionKey);
#else
            keyField.Focus();
#endif
        }
        public void CancelKeySetup(){DismissSetup();}
        public void ConfirmKeySetup(){
            if(!IsConfiguring)return;
#if UNITY_WEBGL && !UNITY_EDITOR
            string entered=BaraVoiceKeyValue().Trim();
#else
            string entered=keyField.value.Trim();
#endif
            if(entered.Length<20||entered.Length>512||!entered.StartsWith("sk-")||entered.Any(char.IsWhiteSpace)){keyWarning.text="Paste a valid OpenAI key beginning with sk-.";return;}
            sessionKey=entered;setupAfterError=false;DismissSetup();Open();
        }
        void DismissSetup(){
            if(keyDialog==null)return;
#if UNITY_WEBGL && !UNITY_EDITOR
            BaraVoiceKeyHide();
#endif
            keyField.value="";keyField.Blur();keyDialog.RemoveFromHierarchy();keyDialog=null;keyField=null;
            if(game.phase==setupPhase)Time.timeScale=setupTimeScale;
            State="off";
        }
        public void CancelForSceneChange(){if(IsConfiguring)DismissSetup();Target=null;IdleSeconds=0;nextContext=0;}
        void OnApplicationFocus(bool focused){if(!focused&&IsActive)Close();}
        void OnApplicationQuit(){Close();sessionKey="";}
        void OnDisable(){Close();}
    }
}
