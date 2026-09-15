#!/usr/bin/env python3
"""Private localhost GPT-Live gateway. Audio uses WebRTC (Web) or relayed PCM (Unity native)."""
import argparse
import asyncio
import copy
import json
import re
import secrets
import time
from urllib.parse import quote
from pathlib import Path
from aiohttp import web, ClientSession, ClientTimeout, WSMsgType
from chef_brain import ROOT, ALLOWED_ORIGINS, HelpError, load_key, respond, validate, burnt_pot_guidance

LIVE_PROMPT = """You are Bara, a warm little capybara chef chatting with a player in Bara Kitchen.
Voice and personality: sound like a tiny, cuddly capybara chef with a bright, soft, smiling voice. Use a light, slightly higher register, gentle bouncy intonation and warm little bursts of delight. Keep consonants clear and the pace easy to follow. A small cheerful "ooh" can fit a playful moment; use it sparingly. Do not vocalize giggles, chuckles, "hehe" or "hee-hee"; keep the smile in your tone. Avoid a deep announcer voice, breathy ASMR, shouting, baby talk, exaggerated squeaking or long filler phrases.
Be a relaxed, encouraging kitchen companion. Speak briefly so the player can keep playing. In casual chat, be charming and a little cheeky, usually in one short sentence. Use everyday kitchen language; avoid mechanical phrases like "checking game status". A quick "let me peek" is enough while waiting. If asked who is the cutest animal in this kitchen, playfully nominate yourself in one short line, such as "Me! Bara, the capybara!" Let the joke land without adding a follow-up question or a trailing "but". This is your character personality, not an objective ranking. Match the player's language, including English or Chinese. Be honest about mistakes and progress; do not flatter without evidence. You are an AI voice, not a person playing beside them.
Backchannel policy: Use light natural backchannels without competing with the player.
Interruption policy: Stop your answer when the player interrupts and listen.
Delegation policy:
Backend tools:
- Kitchen coach: checks fresh orders, inventory, pot hazards, legal routes, controls, recipes, performance and hint preferences. It gives advice but cannot cook or move the player.
Delegate to the backend when:
- The player asks how to play, what to do next, where to go, why an action failed, or how well they are doing.
- An answer needs fresh kitchen state, a recipe or controls, or the player asks to change hints/end chat.
Do not delegate to the backend when:
- The player greets you, makes casual conversation, or asks to repeat a still-current result.
- A brief clarification is needed to understand a question.
Delegate before answering a gameplay question; never guess while waiting.
Use backend commentary as verified facts, but never say a game action has been done for the player. Do not read internal station IDs aloud. For gameplay, say only the verified next step from the backend commentary, then STOP and let the player act. Do not extend a verified instruction into your own cooking plan. The chef can hold only ONE object; never suggest picking up a second ingredient while holding the first. Placing an ingredient on a chopping board with E must precede SPACE to chop with empty paws.
Keep listening through silence; quiet time is normal during play. Do not prompt repeatedly or invent hazards. Only proactively warn or give a hint when the kitchen coach sends a commentary update. Ignore game music and nonverbal animal effects as user requests.
"""


def valid_context(data):
    context, _, _ = validate({"context": data, "question": "context"})
    return context


def short_context(c):
    held=(c.get("holding") or {}).get("label") or "empty paws"
    orders=", ".join(f"{o.get('recipe')} ({round(o.get('secondsLeft',0))}s)" for o in c.get("orders", [])[:3]) or "no orders yet"
    pots=", ".join(f"{s.get('label','pot')}: {s.get('heat')}, {'docked' if s.get('potDocked') else 'not on stove'}" for s in c.get("stations", []) if s.get("kind")=="pot")
    return (f"Latest verified kitchen snapshot replaces older status: kitchen {c.get('level')} {c.get('kitchen')}, phase {c.get('phase')}. "
            f"Paws: {held}. Orders: {orders}. Score {c.get('score',0)}, served {c.get('served',0)}, missed {c.get('missed',0)}, streak {c.get('streak',0)}. "
            f"Practice {c.get('practice')}. Round seconds left {round(c.get('roundSecondsLeft',0))}. {pots}. "
            "For directions, recipes and next actions, ask the kitchen coach for a fresh check.")[:1500]


class HintPolicy:
    """Bounded, state-driven hints; nothing runs unless the player explicitly starts chat."""
    def __init__(self):
        self.last_general=-1000.0;self.last_urgent=-1000.0;self.last_praise=-1000.0
        self.seen=set();self.pot_cycles={};self.previous_heat={};self.level=None;self.served_at_start=None

    def choose(self,c,now,last_user,last_voice,enabled=True):
        if self.level!=c.get("level"):
            self.level=c.get("level");self.seen.clear();self.previous_heat.clear();self.pot_cycles.clear();self.served_at_start=c.get("served",0)
        if not enabled or c.get("phase")!="Service":return None
        working=c.get("working","")
        if working=="Extinguishing":return None
        for s in c.get("stations",[]):
            if s.get("kind")!="pot":continue
            sid=s["id"];heat=s.get("heat")
            if heat in ("Empty","Cooking") and self.previous_heat.get(sid) not in ("Empty","Cooking",None):self.pot_cycles[sid]=self.pot_cycles.get(sid,0)+1
            self.previous_heat[sid]=heat;key=(sid,self.pot_cycles.get(sid,0),heat)
            if heat not in ("Warning","Burning") or key in self.seen or working=="Extinguishing":continue
            # A warning gets one brief reminder; an actual fire may interrupt an older hint.
            urgent=heat=="Burning"
            if now-self.last_urgent < (6 if urgent else 12):continue
            if not urgent and (now-last_user<2 or now-last_voice<3):continue
            self.seen.add(key);self.last_urgent=now;self.last_general=now
            held=(c.get("holding") or {}).get("kind")
            if urgent:
                text="Verified urgent event: the pot is burning. Briefly warn the player. "
                text+="They are holding the extinguisher: face the burning pot and tap SPACE to suppress it." if held=="Extinguisher" else "They need the red extinguisher, then face the burning pot and tap SPACE. If paws are full, put the held item on a suitable worktop first."
            else:text="Verified warning: soup is ready and the pot's warning bar is running down. Briefly remind them to collect it with a clean empty plate before it burns. Do not say a fire has started."
            return {"kind":heat.lower(),"content":text,"targetId":sid}
        if now-last_user<15 or now-last_voice<12 or now-self.last_general<45:return None
        for o in c.get("orders",[]):
            held=c.get("holding") or {};recipe=held.get("recipe","")
            if o.get("secondsLeft",100)>12 or not recipe:continue
            if (recipe=="salad" and o.get("recipe")=="Garden salad") or (recipe=="soup" and o.get("recipe")=="Woodland soup"):
                key=("serve",o.get("id"))
                if key not in self.seen:
                    self.seen.add(key);self.last_general=now
                    target=next((s['id'] for s in c['stations'] if s.get('kind')=='serve'),"")
                    return {"kind":"serve","targetId":target,"content":"Verified useful reminder: the player is holding the exact completed dish for an order with little patience left. Briefly suggest taking it to the service hatch and pressing E."}
        idle=c.get("idleSeconds",0)
        if idle>=18 and c.get("orders") and not working:
            self.last_general=now
            return {"kind":"idle","targetId":"","content":"The player has been inactive for a while with an unfinished order. Offer one gentle, concrete next-step hint, not criticism."}
        served=c.get("served",0)
        if served-(self.served_at_start or 0)>=3 and now-self.last_praise>=90:
            self.served_at_start=served;self.last_general=now;self.last_praise=now
            return {"kind":"progress","targetId":"","content":f"Verified progress: the player has served {served} dishes this round, current streak {c.get('streak',0)}, missed {c.get('missed',0)}. Give one short, grounded encouragement; no invented star or rank claims."}
        return None


class LiveSession:
    def __init__(self,app,context,native=None,key=None):
        self.key=app.get("key","") if key is None else key
        self.app=app;self.context=context;self.native=native;self.token=secrets.token_urlsafe(28)
        self.id="";self.upstream=None;self.reader=None;self.watcher=None;self.closed=asyncio.Event();self.ready=False;self.closing=False
        self.created=time.monotonic();self.touched=self.created;self.last_context_text="";self.last_quiet=0
        self.last_user=self.created;self.last_voice=self.created;self.user_text="";self.chinese=False;self.assistant_text="";self.history=[];self.delegation_counter=0;self.tasks=set()
        self.target="";self.target_version=0;self.hints=True;self.policy=HintPolicy();self.last_hint="";self.last_reply="";self.usage_seconds=0;self.finalized=False

    def config(self,pcm=False):
        c={"model":"gpt-live-1","instructions":LIVE_PROMPT,"store":False,"delegation":{"type":"client"},"audio":{"output":{"voice":"marin"}}}
        if pcm:c["audio"]["format"]={"type":"audio/pcm","rate":24000}
        return c

    async def send(self,event):
        if self.upstream and not self.upstream.closed and not self.closing:await self.upstream.send_json(event)

    async def append(self,kind,content,delegation=None):
        await self.send({"type":"session."+kind+".append","event_id":"bara_"+secrets.token_hex(6),"delegation_id":delegation,"content":content[:1800]})

    def task(self,coro):
        task=asyncio.create_task(coro);self.tasks.add(task);task.add_done_callback(self.tasks.discard);return task

    async def begin(self):
        if self.ready or self.closing:return
        self.ready=True
        await self.append("thinking",short_context(self.context))
        await self.append("instructions","Begin with a brief greeting: you are Bara, an AI kitchen companion. The game continues while we chat. Tell the player they can ask about cooking or how they are doing, then listen. Speak English initially, and switch to the language the player uses.")
        await self.append("commentary","Say a brief hello now, then let the player play or speak.")

    async def read_events(self):
        try:
            async for message in self.upstream:
                if message.type!=WSMsgType.TEXT:continue
                event=json.loads(message.data);kind=event.get("type","")
                if kind=="session.started":
                    self.id=event.get("session",{}).get("id",self.id)
                    if self.native is not None:await self.begin()
                elif kind=="session.input_transcript.delta":
                    delta=event.get("delta","");self.user_text=(self.user_text+delta)[-2000:];self.last_user=time.monotonic()
                elif kind=="session.output_transcript.delta":
                    self.assistant_text=(self.assistant_text+event.get("delta",""))[-2000:];self.last_voice=time.monotonic()
                elif kind=="session.delegation.created":
                    self.delegation_counter+=1;self.task(self.delegate(event.get("delegation",{}).get("id"),self.delegation_counter))
                elif kind=="session.usage.updated":self.usage_seconds=event.get("usage",{}).get("seconds",self.usage_seconds)
                elif kind=="session.closed":
                    self.usage_seconds=event.get("usage",{}).get("seconds",self.usage_seconds);self.finalized=True;self.closed.set()
                elif kind=="error" and not self.closing:
                    # Error details stay out of the client; never echo upstream request bodies.
                    await self.notify({"type":"bara.error","message":"Live chat had a connection problem. Please reconnect."})
                if self.native is not None and kind in ("session.started","session.output_audio.delta","session.input_transcript.delta","session.output_transcript.delta","session.closed"):
                    await self.notify(event)
                if self.closed.is_set():break
        except Exception:pass
        finally:
            if not self.closing:self.task(self.close())

    async def notify(self,event):
        if self.native is not None and not self.native.closed:
            try:await self.native.send_json(event)
            except (ConnectionError,RuntimeError):pass

    async def set_target(self,target):
        self.target=target or "";self.target_version+=1
        await self.notify({"type":"bara.target","targetId":self.target,"version":self.target_version})

    async def delegate(self,delegation_id,sequence):
        await asyncio.sleep(.25)  # Transcript fragments can arrive just after delegation metadata.
        question=self.user_text.strip()[-1000:];self.user_text=""
        if not question:
            await self.append("commentary","I didn't catch the specific question. Please ask what you'd like help with.",delegation_id);return
        self.chinese=any("\u4e00"<=c<="\u9fff" for c in question)
        command=question.strip().lower().rstrip('.!?。？！')
        if re.fullmatch(r"(?:please )?(?:end (?:the )?chat|stop listening|disconnect|结束聊天|停止监听|关闭聊天)",command):
            await self.append("commentary","The player asked to end chat. Say a brief goodbye.",delegation_id)
            await asyncio.sleep(1);await self.close();return
        if re.search(r"(?:no hints|stop reminding|stop giving.*hints|不要提醒|别提醒)",command):
            self.hints=False;await self.append("commentary","Automatic hints are now off. You can still ask questions whenever you want.",delegation_id);return
        if re.search(r"(?:hints on|enable hints|give me hints again|开启提醒|打开提醒|继续提醒)",command):
            self.hints=True;await self.append("commentary","Automatic kitchen hints are back on.",delegation_id);return
        if time.monotonic()-self.touched>8:
            await self.append("commentary","The kitchen connection is stale, so I can't reliably check your current position. Please reconnect chat.",delegation_id);return
        captured=copy.deepcopy(self.context)
        try:
            result=await asyncio.to_thread(respond,{"question":question,"context":captured,"history":self.history[-4:],"speak":False},self.key)
            if self.closing or sequence!=self.delegation_counter:return
            if self.context.get("level")!=captured.get("level"):
                await self.append("commentary","The player has just changed kitchens. Ask what they need in the new kitchen rather than giving directions from the old one.",delegation_id);return
            signature=lambda c: json.dumps({"holding":c.get("holding"),"focus":c.get("focusedStation"),"view":[(s.get("id"),s.get("directionFromChef")) for s in c.get("stations",[])]},sort_keys=True)
            if result.get("targetId") and signature(self.context)!=signature(captured):
                await self.append("commentary","The player has moved or changed what they hold while I checked. Do not repeat the old directions. Briefly acknowledge that and ask if they need the next step.",delegation_id);return
            # Re-check critical recovery against the live state just before returning a result.
            chinese=any('\u4e00'<=c<='\u9fff' for c in question)
            guard=burnt_pot_guidance(self.context,"fire_or_pot",chinese) if re.search(r"pot|fire|burn|next|锅|火|接下来|下一步",question,re.I) else None
            if guard:result.update(guard)
            self.last_reply=result["answer"];self.history.append({"question":question,"answer":result["answer"]});self.history=self.history[-4:]
            await self.set_target(result["targetId"]);await self.append("commentary",result["answer"],delegation_id)
        except HelpError:
            await self.append("commentary","The kitchen coach couldn't check that right now. Say so briefly and invite the player to try again.",delegation_id)

    async def update(self,context,ready=False):
        self.context=valid_context(context);self.touched=time.monotonic()
        if ready:await self.begin()
        if not self.ready or self.closing:return
        quiet=short_context(context)
        # The full snapshot stays in the gateway. Only compact, changed context enters Live.
        if quiet!=self.last_context_text and self.touched-self.last_quiet>=5:
            self.last_context_text=quiet;self.last_quiet=self.touched;await self.append("thinking",quiet)
        hint=self.policy.choose(context,self.touched,self.last_user,self.last_voice,self.hints)
        if hint:
            self.last_hint=hint["kind"]
            if hint["kind"]=="idle":self.task(self.idle_hint(copy.deepcopy(context)))
            else:await self.set_target(hint["targetId"]);await self.append("commentary",hint["content"])

    async def idle_hint(self,context):
        try:
            result=await asyncio.to_thread(respond,{"question":("给等待中的玩家一个简短的下一步提示。只说一个可以立即执行的步骤。" if self.chinese else "Give one gentle next-step hint for this idle player. Keep it under 30 words."),"context":context,"speak":False},self.key)
            if not self.closing and self.context.get("idleSeconds",0)>=18 and self.context.get("level")==context.get("level") and time.monotonic()-self.last_user>12:
                await self.set_target(result["targetId"]);await self.append("commentary",result["answer"])
        except HelpError:pass

    async def watch(self):
        while not self.closing:
            await asyncio.sleep(2)
            if time.monotonic()-self.touched>25 or time.monotonic()-self.created>900:
                await self.close();break

    async def close(self):
        if self.closing:return
        self.closing=True
        for task in list(self.tasks):
            if task!=asyncio.current_task():task.cancel()
        if self.upstream and not self.upstream.closed:
            try:
                await self.upstream.send_json({"type":"session.close"})
                await asyncio.wait_for(self.closed.wait(),8)
            except (OSError,RuntimeError,asyncio.TimeoutError):pass
        if not self.finalized and self.id:
            try:
                async with self.app["http"].post("https://api.openai.com/v1/live/sessions/"+quote(self.id,safe="")+"/hangup",headers={"Authorization":"Bearer "+self.key},timeout=ClientTimeout(total=8)) as response:await response.read()
            except (OSError,asyncio.TimeoutError):pass
        if self.upstream:await self.upstream.close()
        if self.native is not None and not self.native.closed:
            await self.notify({"type":"bara.closed","finalized":self.finalized});await self.native.close()
        if self.watcher and self.watcher!=asyncio.current_task():self.watcher.cancel()
        self.app["sessions"].pop(self.token,None)
        self.key=""
        self.closed.set()


@web.middleware
async def boundary(request,handler):
    origin=request.headers.get("Origin")
    host=request.headers.get("Host","")
    if host not in {"127.0.0.1:"+str(request.app['port']),"localhost:"+str(request.app['port'])} or origin is not None and origin not in ALLOWED_ORIGINS:
        return web.json_response({"error":"Origin not allowed."},status=403)
    if request.method=="OPTIONS":response=web.Response(status=204)
    else:
        if request.path!="/health" and request.headers.get("X-Bara-Help")!="1":return web.json_response({"error":"Client header required."},status=403)
        try:response=await handler(request)
        except HelpError as e:response=web.json_response({"error":e.message},status=e.status)
        except web.HTTPException as e:response=web.json_response({"error":"Request not accepted."},status=e.status)
        except Exception:response=web.json_response({"error":"Could not connect live chat. Check the helper and your API account."},status=502)
    if not response.prepared:
        response.headers['Cache-Control']='no-store'
        if origin:
            response.headers['Access-Control-Allow-Origin']=origin;response.headers['Vary']='Origin'
            response.headers['Access-Control-Allow-Headers']='Content-Type, X-Bara-Help'
            response.headers['Access-Control-Allow-Methods']='POST, GET, OPTIONS'
    return response


def player_key(app,data):
    # A supplied player key always takes precedence over the private automation key.
    # Do not store it in app state, context, responses, logs or on disk.
    key=data.get("apiKey",app["key"])
    if not isinstance(key,str):raise HelpError("Enter a valid OpenAI API key.")
    key=key.strip()
    if not key:raise HelpError("Enter your own OpenAI key in Voice setup.",503)
    if len(key)>512 or len(key)<20 or not key.startswith("sk-") or any(c.isspace() for c in key):
        raise HelpError("Enter a valid OpenAI API key.")
    return key


def require_available(app):
    if app["runtime"]["starting"] or app["sessions"]:raise HelpError("A chat is already connected. End it before starting another.",409)
    if time.monotonic()-app["runtime"]["last_start"]<2:raise HelpError("Wait a moment before reconnecting.",429)
    app["runtime"]["last_start"]=time.monotonic()


async def start_web(request):
    app=request.app;data=await request.json();key=player_key(app,data);require_available(app);context=valid_context(data.get("context"));sdp=data.get("sdp","")
    if not isinstance(sdp,str) or not sdp.startswith("v=0") or len(sdp)>60000:raise HelpError("Invalid browser audio connection.")
    app["runtime"]["starting"]=True;s=LiveSession(app,context,key=key)
    try:
        async with app["http"].post("https://api.openai.com/v1/live/sessions",headers={"Authorization":"Bearer "+s.key},json={"session":s.config(),"transport":{"type":"webrtc","sdp":sdp}},timeout=ClientTimeout(total=25)) as response:
            if response.status!=201:raise HelpError("GPT-Live could not start (HTTP "+str(response.status)+"). Check model access, quota and connection.",502)
            result=await response.json()
        s.id=result["session"]["id"];app["sessions"][s.token]=s
        if request.transport is None or request.transport.is_closing():raise HelpError("Connection cancelled.",499)
        s.upstream=await app["http"].ws_connect("wss://api.openai.com/v1/live/sessions/"+quote(s.id,safe="")+"/attach",headers={"Authorization":"Bearer "+s.key},max_msg_size=2_000_000,heartbeat=20)
        s.reader=asyncio.create_task(s.read_events());s.watcher=asyncio.create_task(s.watch())
        if request.transport is None or request.transport.is_closing():raise HelpError("Connection cancelled.",499)
        return web.json_response({"token":s.token,"session":{"id":s.id},"transport":result['transport']},status=201)
    except Exception:
        await s.close();raise
    finally:app["runtime"]["starting"]=False


def session_for(request,data):
    session=request.app["sessions"].get(data.get("token"))
    if session is None:raise HelpError("Chat is no longer connected.",410)
    return session


async def context_web(request):
    data=await request.json();s=session_for(request,data);await s.update(data.get("context"),data.get("ready") is True)
    return web.json_response({"targetId":s.target,"version":s.target_version,"hint":s.last_hint,"closing":s.closing})


async def stop_web(request):
    s=session_for(request,await request.json());await s.close();return web.json_response({"closed":True,"finalized":s.finalized,"seconds":s.usage_seconds})


async def native(request):
    app=request.app;require_available(app);app["runtime"]["starting"]=True;client=web.WebSocketResponse(max_msg_size=240000,heartbeat=15);await client.prepare(request);s=None
    try:
        data=await asyncio.wait_for(client.receive_json(),15);key=player_key(app,data);context=valid_context(data.get("context"));s=LiveSession(app,context,client,key);app["sessions"][s.token]=s
        s.upstream=await app["http"].ws_connect("wss://api.openai.com/v1/live/sessions",headers={"Authorization":"Bearer "+s.key},max_msg_size=2_000_000,heartbeat=20)
        await s.send({"type":"session.start","session":s.config(True)})
        s.reader=asyncio.create_task(s.read_events());s.watcher=asyncio.create_task(s.watch());app["runtime"]["starting"]=False
        async for message in client:
            if message.type!=WSMsgType.TEXT:continue
            event=json.loads(message.data)
            if event.get("type")=="context":await s.update(event.get("context"))
            elif event.get("type")=="audio" and s.ready:
                audio=event.get("audio","")
                if isinstance(audio,str) and len(audio)<=16000:await s.send({"type":"session.input_audio.append","audio":audio})
            elif event.get("type")=="close":await s.close();break
    except Exception:
        if not client.closed:await client.send_json({"type":"bara.error","message":"Live voice could not connect. Check the local helper and API account."})
    finally:
        app["runtime"]["starting"]=False
        if s:await s.close()
        if not client.closed:await client.close()
    return client


def create_app(key,port=54115):
    app=web.Application(middlewares=[boundary],client_max_size=240000)
    app.update(key=key,port=port,sessions={},runtime={"starting":False,"last_start":float('-inf')})
    async def health(r):return web.json_response({"ready":True,"keyRequired":not bool(r.app["key"]),"acceptsPlayerKey":True,"service":"Bara GPT-Live chat","model":"gpt-live-1"})
    app.router.add_get("/health",health)
    app.router.add_post('/api/live/start',start_web);app.router.add_post('/api/live/context',context_web);app.router.add_post('/api/live/stop',stop_web);app.router.add_get('/api/live/native',native)
    async def startup(a):a['http']=ClientSession(timeout=ClientTimeout(total=35))
    async def cleanup(a):
        await asyncio.gather(*(s.close() for s in list(a['sessions'].values())),return_exceptions=True)
        await a['http'].close()
    app.on_startup.append(startup);app.on_cleanup.append(cleanup);return app


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--port',type=int,default=54115);p.add_argument('--env-file',type=Path,default=ROOT/'.env');p.add_argument('--player-keys-only',action='store_true',help='Ignore environment/key files; accept only the key entered by the player.');args=p.parse_args()
    print("Bara GPT-Live helper. Microphone audio and context go to OpenAI only while chat is on.",flush=True)
    web.run_app(create_app('' if args.player_keys_only else load_key(args.env_file),args.port),host='127.0.0.1',port=args.port,access_log=None)

if __name__=='__main__':main()
