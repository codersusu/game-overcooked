#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BaraKitchen.Gameplay {
    // Native desktop transport: the localhost gateway owns the authenticated OpenAI socket.
    public sealed class NativeLiveVoice : MonoBehaviour {
        public KitchenVoiceGuide guide;
        ClientWebSocket socket;CancellationTokenSource cancel;readonly SemaphoreSlim sendGate=new SemaphoreSlim(1,1);readonly ConcurrentQueue<string> events=new ConcurrentQueue<string>();
        AudioClip microphone,playback;AudioSource speaker;int lastSample,generation;bool live,stopping;float sendAt;
        readonly float[] ring=new float[12000];int read,write,count;readonly object audioLock=new object();
        [Serializable] class Message {public string type,audio,delta,message,targetId;}
        void Awake(){speaker=gameObject.AddComponent<AudioSource>();speaker.spatialBlend=0;speaker.ignoreListenerPause=true;}
        public void Connect(string context){if(socket!=null)return;generation++;stopping=false;live=false;StartCoroutine(PermissionAndConnect(context,generation));}
        IEnumerator PermissionAndConnect(string context,int token){
            if(!Application.HasUserAuthorization(UserAuthorization.Microphone))yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if(token!=generation||stopping)yield break;
            if(!Application.HasUserAuthorization(UserAuthorization.Microphone)||Microphone.devices.Length==0){Error("Microphone unavailable. Allow access in System Settings, then tap Chat.");yield break;}
            try{microphone=Microphone.Start(null,true,1,24000);}catch{Error("Could not start the microphone.");yield break;}
            if(!microphone){Error("Could not start the microphone.");yield break;}lastSample=0;
            playback=AudioClip.Create("Bara live voice",2400,1,24000,true,ReadAudio);speaker.clip=playback;speaker.loop=true;speaker.Play();cancel=new CancellationTokenSource();socket=new ClientWebSocket();socket.Options.SetRequestHeader("X-Bara-Help","1");_ = Run(context,token);
        }
        async Task Run(string context,int token){
            var connection=socket;var cancellation=cancel;
            try {
                await connection.ConnectAsync(new Uri("ws://127.0.0.1:54115/api/live/native"),cancellation.Token);
                await Send("{\"context\":"+context+"}");
                var buffer=new byte[32768];
                while(connection.State==WebSocketState.Open&&token==generation){
                    using(var data=new MemoryStream()){
                        WebSocketReceiveResult result;
                        do{result=await connection.ReceiveAsync(new ArraySegment<byte>(buffer),cancellation.Token);if(result.MessageType==WebSocketMessageType.Close)break;data.Write(buffer,0,result.Count);if(data.Length>2_000_000)throw new IOException("Oversized voice message");}while(!result.EndOfMessage);
                        if(result.MessageType==WebSocketMessageType.Close)break;if(token==generation)events.Enqueue(Encoding.UTF8.GetString(data.ToArray()));
                    }
                }
            }catch(Exception){if(!stopping&&token==generation)events.Enqueue("{\"type\":\"bara.error\",\"message\":\"Start the local GPT-Live helper, then tap Chat again.\"}");}
            finally{if(token==generation)events.Enqueue("{\"type\":\"bara.closed\"}");}
        }
        async Task Send(string text){
            int token=generation;
            var connection=socket;var cancellation=cancel;if(connection==null||cancellation==null)return;
            try {await sendGate.WaitAsync(cancellation.Token);try{if(connection.State==WebSocketState.Open){byte[] data=Encoding.UTF8.GetBytes(text);await connection.SendAsync(new ArraySegment<byte>(data),WebSocketMessageType.Text,true,cancellation.Token);}}finally{sendGate.Release();}}
            catch(Exception){if(!stopping&&token==generation)events.Enqueue("{\"type\":\"bara.error\",\"message\":\"Voice connection lost. Tap Chat to reconnect.\"}");}
        }
        public void UpdateContext(string json){if(live&&!stopping)_=Send("{\"type\":\"context\",\"context\":"+json+"}");}
        void Update(){
            while(events.TryDequeue(out string json)){
                Message e;try{e=JsonUtility.FromJson<Message>(json);}catch{continue;}
                if(e.type=="session.output_audio.delta"){
                    if(stopping)continue;try{var audioBytes=Convert.FromBase64String(e.delta);lock(audioLock){for(int i=0;i+1<audioBytes.Length;i+=2){if(count==ring.Length){read=(read+1)%ring.Length;count--;}ring[write]=(short)(audioBytes[i]|audioBytes[i+1]<<8)/32768f;write=(write+1)%ring.Length;count++;}}}catch{}
                }else if(e.type=="session.input_transcript.delta"||e.type=="session.output_transcript.delta"){} // No transcript UI or disk logging.
                else{if(e.type=="session.started")live=true;guide.OnLiveEvent(json);if(e.type=="bara.closed"||e.type=="session.closed"||e.type=="bara.error")Cleanup();}
            }
            if(guide&&guide.game)speaker.volume=guide.game.Settings.volume;
            if(!live||stopping||!microphone||Time.unscaledTime<sendAt)return;sendAt=Time.unscaledTime+.04f;
            int now=Microphone.GetPosition(null),samples=(now-lastSample+microphone.samples)%microphone.samples;if(samples<=0)return;
            var values=new float[samples*microphone.channels];microphone.GetData(values,lastSample);lastSample=now;
            // Bound chunks to roughly 40 ms; resampling is performed by Microphone.Start at 24 kHz.
            var bytes=new byte[samples*2];for(int i=0;i<samples;i++){float sum=0;for(int c=0;c<microphone.channels;c++)sum+=values[i*microphone.channels+c];short value=(short)(Mathf.Clamp(sum/microphone.channels,-1,1)*32767);bytes[i*2]=(byte)value;bytes[i*2+1]=(byte)(value>>8);}
            for(int i=0;i<bytes.Length;i+=4800){int n=Math.Min(4800,bytes.Length-i);_ = Send(JsonUtility.ToJson(new Message{type="audio",audio=Convert.ToBase64String(bytes,i,n)}));}
        }
        void ReadAudio(float[] data){lock(audioLock){for(int i=0;i<data.Length;i++){if(count>0){data[i]=ring[read];read=(read+1)%ring.Length;count--;}else data[i]=0;}}}
        public void Stop(){if(stopping)return;stopping=true;live=false;StopMic();speaker.Stop();if(socket==null){generation++;guide.OnLiveEvent("{\"type\":\"closed\"}");return;}_=Send("{\"type\":\"close\"}");StartCoroutine(StopTimeout(generation));}
        IEnumerator StopTimeout(int token){yield return new WaitForSecondsRealtime(10);if(token==generation&&socket!=null){Cleanup();guide.OnLiveEvent("{\"type\":\"closed\"}");}}
        void StopMic(){if(microphone){Microphone.End(null);Destroy(microphone);microphone=null;}}
        void Cleanup(){generation++;while(events.TryDequeue(out _)){}live=false;stopping=false;StopMic();speaker.Stop();cancel?.Cancel();socket?.Dispose();socket=null;cancel?.Dispose();cancel=null;if(playback)Destroy(playback);playback=null;lock(audioLock){read=write=count=0;}}
        void Error(string message){guide.OnLiveEvent(JsonUtility.ToJson(new Message{type="bara.error",message=message}));Cleanup();}
        void OnDisable(){Stop();}
        void OnDestroy(){Cleanup();}
    }
}
#endif
