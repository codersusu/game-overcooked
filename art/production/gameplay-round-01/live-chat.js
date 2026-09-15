/* WebRTC media stays between the browser and OpenAI. Project keys stay in the local helper. */
(() => {
  const endpoint = 'http://127.0.0.1:54115';
  let current = null;
  const post = async (path, data, signal, keepalive = false) => {
    const response = await fetch(endpoint + path, {method:'POST',headers:{'Content-Type':'application/json','X-Bara-Help':'1'},body:JSON.stringify(data),signal,keepalive});
    let body;try {body=await response.json();} catch (_) {throw Error('The local voice helper is unavailable.');}
    if (!response.ok) throw Error(body.error || 'Could not connect live chat.');
    return body;
  };
  function notify(s, data) {if (s === current) s.emit(data);}
  function tracksOff(s) {s.stream?.getTracks().forEach(t => t.stop());}
  async function stop(s, message = '') {
    if (!s || s.closing) return;
    s.closing=true;clearTimeout(s.startTimeout);tracksOff(s);s.audio.pause();s.audio.srcObject=null;
    notify(s,{type:'closing',message});
    const finish = async () => {
      clearTimeout(s.closeTimeout);s.controller.abort();s.dc?.close();s.pc?.close();s.audio.remove();
      if (s === current) {notify(s,{type:'closed',message});current=null;}
    };
    s.finish=finish;
    if (s.dc?.readyState==='open') {try{s.dc.send(JSON.stringify({type:'session.close'}));}catch(_) {}}
    if (s.token) {
      // The server waits for final usage; the microphone is already stopped locally.
      post('/api/live/stop',{token:s.token},undefined,true).catch(()=>{}).finally(finish);
      s.closeTimeout=setTimeout(finish,12000);
    } else {await finish();}
  }
  async function start(context, emit, volume=.65) {
    if (current) return;
    const s=current={emit,context,controller:new AbortController(),stream:null,pc:null,dc:null,audio:new Audio(),token:'',ready:false,closing:false,updating:false,targetVersion:0};
    s.audio.autoplay=true;s.audio.volume=Math.max(0,Math.min(1,volume));s.audio.style.display='none';document.body.append(s.audio);
    notify(s,{type:'connecting'});
    s.startTimeout=setTimeout(()=>{notify(s,{type:'error',message:'Chat connection timed out. Check microphone permission and the local helper.'});stop(s);},35000);
    try {
      if (!navigator.mediaDevices || !window.RTCPeerConnection) throw Error('Live chat needs a microphone and a browser on localhost or HTTPS.');
      const health=await fetch(endpoint+'/health',{signal:s.controller.signal}).then(r=>r.json());
      if(!health.ready||health.model!=='gpt-live-1')throw Error('Start the GPT-Live helper: scripts/voice/live_chef_server.py');
      const stream=await navigator.mediaDevices.getUserMedia({audio:{echoCancellation:true,noiseSuppression:true,autoGainControl:true,channelCount:1},video:false});
      if (s.closing||s!==current) {stream.getTracks().forEach(t=>t.stop());return;}
      if(document.visibilityState==='hidden') {stream.getTracks().forEach(t=>t.stop());throw Error('Return to the game before starting chat.');}
      s.stream=stream;
      const pc=s.pc=new RTCPeerConnection();
      stream.getAudioTracks().forEach(track=>pc.addTrack(track,stream));
      pc.ontrack=event=>{if(s.closing)return;s.audio.srcObject=new MediaStream([event.track]);s.audio.play().catch(()=>notify(s,{type:'error',message:'Click the game to enable voice playback.'}));};
      pc.onconnectionstatechange=()=>{if(['failed','disconnected'].includes(pc.connectionState)&&!s.closing){notify(s,{type:'error',message:'Voice connection lost. Tap Chat to reconnect.'});stop(s);}};
      const dc=s.dc=pc.createDataChannel('oai-events');
      dc.onmessage=event=>{
        let data;try {data=JSON.parse(event.data);}catch(_){return;}
        // This hook is used only by synthetic QA harnesses, never persisted by the game.
        if (typeof window.baraLiveTestEvent==='function') window.baraLiveTestEvent(data);
        if(data.type==='session.started') {if(document.visibilityState==='hidden'||!document.hasFocus()){stop(s);return;}s.ready=true;clearTimeout(s.startTimeout);notify(s,{type:'connected'});update(s.context);}
        else if(data.type==='session.closed') {s.finalized=true;if(!s.closing)stop(s);}
        else if(data.type==='session.output_transcript.delta')notify(s,{type:'speaking'});
        else if(data.type==='error'){notify(s,{type:'error',message:'Live chat could not continue. Tap Chat to reconnect.'});stop(s);}
      };
      dc.onclose=()=>{if(!s.closing)stop(s,'Voice connection ended.');};
      await pc.setLocalDescription(await pc.createOffer());
      if(pc.iceGatheringState!=='complete') await new Promise((resolve,reject)=>{
        const timeout=setTimeout(()=>reject(Error('Could not establish the audio connection.')),10000);
        const changed=()=>{if(pc.iceGatheringState==='complete'){clearTimeout(timeout);pc.removeEventListener('icegatheringstatechange',changed);resolve();}};
        pc.addEventListener('icegatheringstatechange',changed);changed();
      });
      if(s.closing)return;
      const result=await post('/api/live/start',{sdp:pc.localDescription.sdp,context:s.context},s.controller.signal);
      s.token=result.token;
      if(s.closing||s!==current) {await post('/api/live/stop',{token:s.token}).catch(()=>{});return;}
      await pc.setRemoteDescription({type:'answer',sdp:result.transport.sdp});
    } catch(error) {
      if(!s.closing){notify(s,{type:'error',message:error.name==='NotAllowedError'?'Microphone permission denied. Allow it, then tap Chat.':error.message||'Start the local voice helper, then try again.'});await stop(s);}
    }
  }
  async function update(context) {
    const s=current;if(!s||s.closing)return;s.context=context;if(!s.ready||!s.token||s.updating)return;s.updating=true;
    try {
      const result=await post('/api/live/context',{token:s.token,context,ready:true},s.controller.signal);
      if(s!==current||s.closing)return;
      if(result.closing){await stop(s);return;}
      if(result.version!==s.targetVersion){s.targetVersion=result.version;notify(s,{type:'target',targetId:result.targetId});}
    } catch(error) {if(!s.closing){notify(s,{type:'error',message:'Kitchen voice connection lost. Tap Chat to reconnect.'});await stop(s);}}
    finally {s.updating=false;}
  }
  window.BaraLive={start,update,stop:()=>stop(current),volume:value=>{if(current)current.audio.volume=Math.max(0,Math.min(1,value));},get connected(){return !!current?.ready&&!current.closing;}};
  document.addEventListener('visibilitychange',()=>{if(document.visibilityState==='hidden')stop(current);});
  window.addEventListener('pagehide',()=>stop(current));
})();
