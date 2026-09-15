"""Offline regression checks for contextual reasoning, live hints and the localhost boundary."""
import copy,json,unittest
from unittest.mock import AsyncMock
from aiohttp.test_utils import TestClient,TestServer
from chef_brain import HelpError,respond,burnt_pot_guidance
from live_chef_server import HintPolicy,LiveSession,create_app

BASE={'level':2,'phase':'Service','working':'','idleSeconds':0,'holding':{},'orders':[{'id':1,'recipe':'Woodland soup','secondsLeft':80}],'served':0,'streak':0,'missed':0,'stations':[{'id':'pot','kind':'pot','heat':'Cooking','potDocked':True,'reachable':True},{'id':'stand','kind':'extinguisher','reachable':True,'possibleActions':['E: put down Fire extinguisher']},{'id':'bin','kind':'bin','reachable':True},{'id':'serve','kind':'serve','reachable':True}]}
class HintTests(unittest.TestCase):
 def setUp(self):self.c=copy.deepcopy(BASE);self.policy=HintPolicy()
 def choose(self,now=100,last_user=0,last_voice=0,enabled=True):return self.policy.choose(self.c,now,last_user,last_voice,enabled)
 def test_warning_then_fire_once_and_cooldown(self):
  self.c['stations'][0]['heat']='Warning';self.assertEqual(self.choose()['kind'],'warning');self.assertIsNone(self.choose(110))
  self.c['stations'][0]['heat']='Burning';self.assertIsNone(self.choose(104));self.assertEqual(self.choose(106)['kind'],'burning');self.assertIsNone(self.choose(145))
 def test_new_cooking_cycle_can_warn_again(self):
  self.c['stations'][0]['heat']='Warning';self.choose();self.c['stations'][0]['heat']='Empty';self.choose(130);self.c['stations'][0]['heat']='Cooking';self.choose(140);self.c['stations'][0]['heat']='Warning';self.assertEqual(self.choose(150)['kind'],'warning')
 def test_no_hints_during_pause_menu_or_disabled(self):
  self.c['stations'][0]['heat']='Burning'
  for phase in ('Paused','Menu','Results'):
   self.c['phase']=phase;self.assertIsNone(self.choose())
  self.c['phase']='Service';self.assertIsNone(self.choose(enabled=False))
 def test_no_reminder_when_already_fighting_fire(self):
  self.choose();self.c['served']=3;self.c['working']='Extinguishing';self.c['stations'][0]['heat']='Burning';self.assertIsNone(self.choose())
 def test_warning_waits_for_speech_but_fire_is_urgent(self):
  self.c['stations'][0]['heat']='Warning';self.assertIsNone(self.choose(last_user=99));self.assertIsNone(self.choose(last_voice=99))
  self.c['stations'][0]['heat']='Burning';self.assertEqual(self.choose(last_user=99)['kind'],'burning')
 def test_idle_requires_order_quiet_inactivity_and_cooldown(self):
  self.c['idleSeconds']=17;self.assertIsNone(self.choose());self.c['idleSeconds']=18
  self.assertIsNone(self.choose(last_user=90));self.assertIsNone(self.choose(last_voice=90));self.c['working']='Chopping';self.assertIsNone(self.choose());self.c['working']=''
  self.assertEqual(self.choose()['kind'],'idle');self.assertIsNone(self.choose(144));self.assertEqual(self.choose(145)['kind'],'idle')
 def test_ready_plate_reminder_requires_exact_order(self):
  self.c['orders'][0]['secondsLeft']=8;self.c['holding']={'kind':'Dish','recipe':'salad'};self.assertIsNone(self.choose())
  self.c['holding']['recipe']='soup';self.assertEqual(self.choose()['kind'],'serve');self.assertIsNone(self.choose(160))
 def test_praise_after_real_progress_and_not_repeated(self):
  self.choose();self.c['served']=3;self.c['streak']=3;self.assertIn('3 dishes',self.choose()['content']);self.c['served']=6;self.assertIsNone(self.choose(160));self.assertEqual(self.choose(190)['kind'],'progress')

class BrainTests(unittest.TestCase):
 def test_structured_grounding_and_unreachable_marker(self):
  c=copy.deepcopy(BASE);c['stations'].append({'id':'blocked','reachable':False})
  def fake(key,path,body):
   self.assertEqual(path,'responses');data=json.loads(body);self.assertFalse(data['store']);self.assertNotIn('blocked',data['text']['format']['schema']['properties']['targetId']['enum'])
   return {'output':[{'type':'message','content':[{'type':'output_text','text':'{"answer":"Pick up a carrot.","targetId":"blocked","topic":"next_step"}'}]}]}
  self.assertEqual(respond({'context':c,'question':'How do I start?'},'fake',fake)['targetId'],'')
 def test_invalid_context_and_question_cannot_call_model(self):
  for payload in ({},{'context':BASE,'question':''},{'context':BASE,'question':'x'*1001},{'context':{'stations':[{}]},'question':'help'}):
   with self.assertRaises(HelpError):respond(payload,'fake',lambda *_:self.fail('Unexpected API call'))
 def test_burnt_recovery_for_full_empty_and_cleaned_pot(self):
  c=copy.deepcopy(BASE);c['stations'][0]['heat']='Extinguished';c['holding']={'kind':'Extinguisher'}
  answer=burnt_pot_guidance(c,'next_step',False);self.assertEqual(answer['targetId'],'stand');self.assertIn('food bin',answer['answer']);self.assertIn('return',answer['answer'])
  c['holding']={'kind':'Pot','burnt':True};self.assertEqual(burnt_pot_guidance(c,'next_step',True)['targetId'],'bin')
  c['holding']['burnt']=False;c['stations'][0].update(heat='Empty',potDocked=False);self.assertEqual(burnt_pot_guidance(c,'next_step',False)['targetId'],'pot')
  self.assertIsNone(burnt_pot_guidance(c,'controls',False))

class LifecycleTests(unittest.IsolatedAsyncioTestCase):
 async def test_close_waits_for_finalization_and_clears_session(self):
  app={'sessions':{},'http':None,'auth':{}};s=LiveSession(app,copy.deepcopy(BASE));app['sessions'][s.token]=s;s.upstream=AsyncMock();s.upstream.closed=False
  async def close_event(e):self.assertEqual(e['type'],'session.close');s.finalized=True;s.closed.set()
  s.upstream.send_json.side_effect=close_event
  await s.close();self.assertTrue(s.finalized);self.assertFalse(app['sessions']);s.upstream.close.assert_awaited_once();await s.close()
 async def test_late_response_cannot_target_a_new_level(self):
  s=LiveSession({'key':'fake'},copy.deepcopy(BASE));s.send=AsyncMock();s.user_text='Where do I go?';s.delegation_counter=1
  import live_chef_server
  from unittest.mock import patch
  def changed(*_):s.context=copy.deepcopy(BASE);s.context['level']=3;return {'answer':'Old directions','targetId':'pot'}
  with patch.object(live_chef_server,'respond',changed):await s.delegate('delegation',1)
  self.assertEqual(s.target,'');self.assertIn('changed kitchens',s.send.call_args.args[0]['content'])
 async def test_loopback_no_secrets_origin_header_and_missing_key(self):
  app=create_app('');server=TestServer(app);await server.start_server();app['port']=server.port;client=TestClient(server);await client.start_server()
  try:
   self.assertFalse((await (await client.get('/health')).json())['ready'])
   self.assertEqual((await client.get('/health',headers={'Origin':'https://evil.example'})).status,403)
   self.assertEqual((await client.get('/health',headers={'Host':'evil.example'})).status,403)
   headers={'X-Bara-Help':'1','Origin':'http://127.0.0.1:54114'}
   for path in ('/.env','/scripts/voice/live_chef_server.py'):
    self.assertEqual((await client.get(path,headers=headers)).status,404)
   self.assertEqual((await client.post('/api/live/start',json={})).status,403)
   self.assertEqual((await client.options('/api/live/start',headers=headers)).status,204)
   self.assertEqual((await client.post('/api/live/start',headers=headers,json={})).status,503)
  finally:await client.close()
if __name__=='__main__':unittest.main(verbosity=2)
