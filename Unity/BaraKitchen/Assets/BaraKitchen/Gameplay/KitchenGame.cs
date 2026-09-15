using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace BaraKitchen.Gameplay {
    public sealed class KitchenGame : MonoBehaviour {
        public KitchenVoiceGuide voice; public GameCatalog catalog; public KitchenCameraController view; public KitchenAudio sound; public KitchenHUD ui; public Camera cam; public SessionPhase phase=SessionPhase.Menu; public LevelDefinition level;
        public GameObject live,room; public ChefController chef; public KitchenStation[] stations; public SceneAnchor[] seats; public Transform entrance,aisle;
        public readonly List<CafeGuest> guests=new List<CafeGuest>(); public readonly List<KitchenItem> rack=new List<KitchenItem>(),returns=new List<KitchenItem>();
        public OrderBook book; public ScoreSave save; public float remaining,elapsed; public bool training=true; public string toast,feedback; public float toastTime,feedbackTime; public int resultStars;
        public bool DashAllowed=>level!=null&&(level.id>2||(level.id==2&&!training)); public bool ThrowAllowed=>level!=null&&level.id>=3;
        float arrivalTimer; int recipeCursor,customerCursor; bool saved; AudioSource audioSource; readonly Dictionary<int,AudioClip> sounds=new Dictionary<int,AudioClip>();
        public GameSettings Settings=>save.settings;
        static string SaveKey {get {
#if UNITY_EDITOR
            if(UnityEditor.SessionState.GetBool("Bara.GameChecks",false))return "BaraKitchen.Validation.v1";
#endif
            return "BaraKitchen.Save.v1";
        }}
        void Awake(){QualitySettings.skinWeights=SkinWeights.FourBones;QualitySettings.antiAliasing=4;QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.VeryHigh;QualitySettings.shadowDistance=35;QualitySettings.shadowCascades=4;Load();cam=Camera.main;view=gameObject.AddComponent<KitchenCameraController>();view.game=this;view.SnapToPreference();var backdrop=new GameObject("Full-screen background").AddComponent<Camera>();backdrop.clearFlags=CameraClearFlags.SolidColor;backdrop.backgroundColor=new Color(.79f,.84f,.66f);backdrop.cullingMask=0;backdrop.depth=-10;sound=gameObject.AddComponent<KitchenAudio>();sound.game=this;audioSource=gameObject.AddComponent<AudioSource>();audioSource.spatialBlend=0;ui=GetComponent<KitchenHUD>();ui.game=this;voice=gameObject.AddComponent<KitchenVoiceGuide>();voice.game=this;}
        void Start(){ShowMenu();
#if UNITY_WEBGL && DEVELOPMENT_BUILD
            gameObject.AddComponent<GameReviewTelemetry>();
#endif
        }
        public void Load(){string savedJson=PlayerPrefs.GetString(SaveKey,"");try{save=JsonUtility.FromJson<ScoreSave>(savedJson);}catch{save=null;}if(save==null||save.best==null||save.best.Length!=8||save.stars==null||save.stars.Length!=8)save=new ScoreSave();if(save.settings==null)save.settings=new GameSettings();if(!savedJson.Contains("\"musicVolume\""))save.settings.musicVolume=.65f;if(!savedJson.Contains("\"effectsVolume\""))save.settings.effectsVolume=.85f;if(!savedJson.Contains("\"cameraTilt\"")||float.IsNaN(save.settings.cameraTilt)||float.IsInfinity(save.settings.cameraTilt))save.settings.cameraTilt=KitchenCameraController.DefaultTilt;save.settings.cameraTilt=Mathf.Clamp(save.settings.cameraTilt,KitchenCameraController.MinTilt,KitchenCameraController.MaxTilt);if(!savedJson.Contains("\"cameraYaw\"")||float.IsNaN(save.settings.cameraYaw)||float.IsInfinity(save.settings.cameraYaw))save.settings.cameraYaw=KitchenCameraController.DefaultYaw;save.settings.cameraYaw=Mathf.Clamp(save.settings.cameraYaw,KitchenCameraController.MinYaw,KitchenCameraController.MaxYaw);save.settings.chef=Mathf.Clamp(save.settings.chef,0,catalog.chefs.Length-1);if(view)view.SnapToPreference();}
        public void Save(){PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(save));PlayerPrefs.Save();}
        public void ShowMenu(){Time.timeScale=1;PrepareLevel(1);phase=SessionPhase.Menu;ui.ShowMenu();}
        public GameObject Spawn(string id,Transform parent,Vector3 position){var prefab=catalog.Prefab(id);if(!prefab)throw new Exception("Missing game asset "+id);var go=Instantiate(prefab,parent);go.name=id;go.transform.localPosition=position;go.transform.localRotation=Quaternion.identity;foreach(var c in go.GetComponentsInChildren<Collider>(true))c.enabled=false;return go;}
        public KitchenItem NewItem(ItemKind kind,string ingredient=null){string id=kind==ItemKind.Ingredient?"Ingredient_"+ingredient:kind==ItemKind.Dish?"UniversalDish":kind==ItemKind.Pot?"CookingPot":"fire-extinguisher";var go=Spawn(id,live.transform,Vector3.zero);var item=go.AddComponent<KitchenItem>();item.kind=kind;item.ingredient=ingredient;item.visual=go.GetComponent<VisualState>();item.SetWorldSize(Vector3.one*(kind==ItemKind.Dish?1.15f:kind==ItemKind.Ingredient?1f:1f));item.Refresh();return item;}
        public void PrepareLevel(int id){if(voice)voice.CancelForSceneChange();Time.timeScale=1;if(live){live.SetActive(false);Destroy(live);}guests.Clear();rack.Clear();returns.Clear();book=new OrderBook();book.Expired+=OnExpired;level=catalog.levels[id-1];training=true;remaining=level.duration*(Settings.calm?1.5f:1);elapsed=0;recipeCursor=0;customerCursor=0;arrivalTimer=.3f;saved=false;toast=null;feedback=null;feedbackTime=0;
            live=new GameObject("Live kitchen "+id);room=Instantiate(level.room,live.transform);room.name="Kitchen scene";stations=room.GetComponentsInChildren<KitchenStation>();
            var anchors=room.GetComponentsInChildren<SceneAnchor>();seats=anchors.Where(a=>a.approach&&a.dishPosition).ToArray();entrance=anchors.First(a=>a.name=="CustomerEntrance").transform;aisle=anchors.First(a=>a.name=="CustomerAisleEast").transform;
            chef=new GameObject("Player chef").AddComponent<ChefController>();chef.transform.SetParent(live.transform);chef.transform.position=level.spawn;chef.Configure(this,catalog.chefs[Settings.chef].prefab);
            foreach(var s in stations){s.item=null;s.reserved=false;s.potContents=0;s.washing=0;if(s.kind=="pot"){var pot=Spawn("CookingPot",s.transform,s.slotOffset);s.pot=pot.GetComponent<VisualState>();s.hazard=pot.AddComponent<CookingHazard>();s.hazard.pot=s.pot;s.potItem=pot.AddComponent<KitchenItem>();s.potItem.kind=ItemKind.Pot;s.potItem.potStation=s;s.potItem.visual=s.pot;s.potItem.SetWorldSize(pot.transform.lossyScale);s.hazard.cookingSeconds=12;s.hazard.readySeconds=5;s.hazard.warningSeconds=8;s.hazard.visual=Spawn("CookingFireVisual",s.transform,s.slotOffset+Vector3.up*.19f).GetComponent<KitchenFireVisual>();s.hazard.StateChanged+=state=>{if(state==HeatState.Ready){Toast("Soup ready! Bring a clean dish.");AudioCue(880,.13f);}if(state==HeatState.Warning){Toast("Pot warning — serve it before it burns!");AudioCue(330,.18f);}if(state==HeatState.Burning){Toast("Fire! Grab the extinguisher.");AudioCue(160,.25f);}};}}
            for(int i=0;i<level.dishes;i++)rack.Add(NewItem(ItemKind.Dish));ArrangeDishes();var stand=stations.FirstOrDefault(s=>s.kind=="extinguisher");if(stand)stand.Put(NewItem(ItemKind.Extinguisher));
            SetCamera();phase=SessionPhase.Briefing;
        }
        public void Brief(int id){PrepareLevel(id);ui.ShowBriefing();}
        public void StartService(){phase=SessionPhase.Service;ui.ShowHUD();Toast("Welcome! First order has no time pressure.");}
        public void Pause(){if(phase!=SessionPhase.Service)return;chef.CancelWork();phase=SessionPhase.Paused;Time.timeScale=0;ui.ShowPause();}
        public void Resume(){Time.timeScale=1;phase=SessionPhase.Service;ui.ShowHUD();}
        public void EndRound(){if(phase!=SessionPhase.Service||saved)return;saved=true;chef.CancelWork();phase=SessionPhase.Results;Time.timeScale=0;resultStars=level.stars.Count(t=>book.score>=t);int i=level.id-1+(Settings.calm?4:0);save.best[i]=Math.Max(save.best[i],book.score);save.stars[i]=Math.Max(save.stars[i],resultStars);Save();ui.ShowResults();}
        public void SetCamera(){if(view)view.Apply();}
        public void FitScene(){if(view)view.FitScene();}
        void Update(){if(voice&&voice.IsConfiguring)return;if(Input.GetKeyDown(KeyCode.Escape)){if(phase==SessionPhase.Service)Pause();else if(phase==SessionPhase.Paused)Resume();}if(phase==SessionPhase.Service)Tick(Time.deltaTime);if(feedbackTime>0)feedbackTime-=Time.unscaledDeltaTime;}
        public void Tick(float dt){if(phase!=SessionPhase.Service)return;elapsed+=dt;
            if(!training){remaining=Mathf.Max(0,remaining-dt);if(remaining<=0){EndRound();return;}book.Tick(dt);}
            foreach(var guest in guests.ToArray())if(guest)guest.Tick(dt);
            arrivalTimer-=dt;int waiting=guests.Count(g=>g.phase==GuestPhase.Arriving||g.phase==GuestPhase.Waiting);int cap=training?1:level.ticketCap;
            if(arrivalTimer<=0&&waiting<cap){var seat=seats.FirstOrDefault(s=>!guests.Any(g=>g.seat==s));if(seat){var guest=new GameObject("Cafe guest").AddComponent<CafeGuest>();guest.transform.SetParent(live.transform);guest.Configure(this,seat,catalog.customers[customerCursor++%catalog.customers.Length].prefab);guests.Add(guest);arrivalTimer=training?5:14;}}
            foreach(var s in stations){if(s.hazard){if(!training||s.hazard.state!=HeatState.Ready)s.hazard.Tick(dt);s.hazard.visual.Sample(elapsed,s.hazard.state,s.hazard.fireRemaining,s.hazard.WarningProgress,s.hazard.IgnitionProgress);}bool serve=s.kind=="serve"&&chef.held&&book.CanServe(chef.held.Recipe);bool target=s==chef.focused||s==chef.throwTarget&&chef.held&&chef.held.kind==ItemKind.Ingredient&&ThrowAllowed;bool guided=voice&&voice.ShowingTarget&&voice.Target==s;s.SetHighlight(serve||target||guided,guided?new Color(.35f,.78f,1,.95f):serve?new Color(.35f,1,.63f,.95f):new Color(1,.76f,.25f,.88f),serve,Settings.reduceFlash);}
        }
        public FoodOrder CreateOrder(CafeGuest guest){string recipe=level.recipes[recipeCursor++%level.recipes.Length];var o=book.Add(recipe,level.patience*(Settings.calm?1.5f:1),guest);Toast("New order · "+Recipes.Name(recipe));AudioCue(600,.08f);return o;}
        void OnExpired(FoodOrder o){feedback="−15";feedbackTime=2;if(o.guest)o.guest.Leave();Toast("Order #"+o.id+" expired · streak reset",4);AudioCue(180,.2f);}
        public void ReturnDish(KitchenItem dish){if(!dish)return;dish.ClearFood();dish.dirty=true;dish.Refresh();returns.Add(dish);ArrangeDishes();}
        public void ArrangeDishes(){var clean=stations.First(s=>s.kind=="dishes");var dirty=stations.First(s=>s.kind=="return");for(int i=0;i<rack.Count;i++)rack[i].Attach(clean.transform,clean.slotOffset+Vector3.up*.04f*i);for(int i=0;i<returns.Count;i++)returns[i].Attach(dirty.transform,dirty.slotOffset+new Vector3(0,.032f*(i/3),((i%3)-1)*.15f));}
        public bool Interact(ChefController c,KitchenStation s){if(phase!=SessionPhase.Service)return false;var held=c.held;
            if(Recipes.Ingredient(s.kind)!=0){if(held)return Fail("Your paws are full");c.Hold(NewItem(ItemKind.Ingredient,s.kind));AudioCue(420,.05f);return true;}
            if(s.kind=="dishes"){if(!held){if(rack.Count==0)return Fail("No clean dishes — collect and wash a dirty one");var i=rack[rack.Count-1];rack.Remove(i);c.Hold(i);ArrangeDishes();return true;}if(held.kind==ItemKind.Dish&&!held.dirty&&held.IsEmpty){rack.Add(c.Release());ArrangeDishes();return true;}return Fail("Only clean, empty dishes belong on the rack");}
            if(s.kind=="return"){if(held)return Fail("Free your paws before taking a dirty dish");if(returns.Count==0)return Fail("Guests return dishes after eating");var i=returns[0];returns.RemoveAt(0);c.Hold(i);ArrangeDishes();return true;}
            if(s.kind=="bin"){if(!held)return Fail("Hold food to discard it");if(held.kind==ItemKind.Extinguisher)return Fail("Keep the extinguisher for kitchen fires");if(held.kind==ItemKind.Pot){if(!held.BurntPot)return false;held.potStation.ResetPot();}else if(held.kind==ItemKind.Dish){held.ClearFood();Toast("Food discarded · dish kept");}else Destroy(c.Release().gameObject);return true;}
            if(s.kind=="serve"){if(!held||held.Recipe==null)return Fail("Bring a completed salad or soup");int before=book.score;var order=book.Serve(held.Recipe);if(order==null)return Fail("No matching order — keep this dish for later");var meal=c.Release();if(order.guest)order.guest.Eat(meal);else ReturnDish(meal);Toast("Served! +"+(held.Recipe=="salad"?60:80)+" + tip · streak ×"+book.streak,4);feedback="+"+(book.score-before);feedbackTime=2;AudioCue(980,.2f);Puff(s.Slot);if(training){training=false;arrivalTimer=1;Toast(level.id==2?"First soup served! SHIFT now dashes. Service begins.":"First order served! Timed service begins.",5);}return true;}
            if(s.kind=="pot")return PotInteract(c,s);
            if(s.IsSurface){
                if(!held){if(!s.item)return Fail("Empty "+s.Label.ToLower());c.Hold(s.Take());return true;}
                if(s.item){
                    if(s.item.CanAdd(held)){var plate=s.item;plate.Add(c.Release());if(plate.Recipe!=null)AudioCue(810,.1f);return true;}
                    if(held.CanAdd(s.item)){held.Add(s.Take());if(held.Recipe!=null)AudioCue(810,.1f);return true;}
                    if(held.kind==ItemKind.Ingredient&&s.CanStackIngredient&&!s.reserved){s.Put(c.Release());return true;}return false;
                }
                if(s.reserved)return Fail("An ingredient is landing here");if(s.kind=="sink"&&(held.kind!=ItemKind.Dish||!held.dirty))return Fail("Put a dirty dish in the sink");if(s.kind=="extinguisher"&&held.kind!=ItemKind.Extinguisher)return Fail("This stand holds the extinguisher");s.Put(c.Release());return true;
            }return false;
        }
        bool PotInteract(ChefController c,KitchenStation s){var held=c.held;var h=s.hazard;
            // Returning the same pot preserves its state: redocking burnt food cannot bypass the bin.
            if(!s.PotDocked){if(!held||held!=s.potItem)return false;c.Release().Attach(s.transform,s.slotOffset);return true;}
            if(h.state==HeatState.Burning)return Fail("Hold the extinguisher and SPACE to fight the fire");
            if(h.state==HeatState.Extinguished){if(held)return false;c.Hold(s.potItem);return true;}
            if(h.state==HeatState.Ready||h.state==HeatState.Warning){if(!held||held.kind!=ItemKind.Dish||held.dirty||!held.IsEmpty)return Fail("Bring a clean, empty dish to fill with soup");held.FillSoup();s.ResetPot();Toast("Soup plated — serve at the highlighted hatch");AudioCue(810,.1f);return true;}
            if(h.state==HeatState.Cooking)return Fail("Soup is cooking — prepare or wash while you wait");
            if(!held){if(s.potContents!=0){s.ResetPot();Toast("Incomplete pot emptied");return true;}return Fail("Add chopped carrot and mushroom");}
            if(held.kind==ItemKind.Dish){bool added=false;foreach(var ingredient in held.portions.ToArray()){int part=Recipes.Ingredient(ingredient.ingredient);if(ingredient.chopped&&(part==4||part==8)&&(s.potContents&part)==0){s.potContents|=part;held.portions.Remove(ingredient);Destroy(ingredient.gameObject);added=true;}}if(added){held.Refresh();UpdatePot(s);}return added;}
            int bit=Recipes.Ingredient(held.ingredient);if(held.kind!=ItemKind.Ingredient||!held.chopped||(bit!=4&&bit!=8)||(s.potContents&bit)!=0)return Fail("Soup needs one chopped carrot and one chopped mushroom");
            s.potContents|=bit;Destroy(c.Release().gameObject);UpdatePot(s);return true;
        }
        void UpdatePot(KitchenStation s){s.pot.SetState(s.potContents==4?1:s.potContents==8?2:3);if(s.potContents==12)s.hazard.BeginCooking();}
        bool Fail(string message){return false;}
        public KitchenStation FindStation(Vector3 position,Vector3 forward){KitchenStation best=null;float value=float.MaxValue;foreach(var s in stations){var delta=s.transform.position-position;delta.y=0;float d=delta.magnitude;if(d>1.02f||d<.05f||Vector3.Dot(delta.normalized,forward)<.45f)continue;if(Physics.Raycast(position+Vector3.up*.30f,delta.normalized,out var hit,d,~0,QueryTriggerInteraction.Ignore)&&!hit.transform.IsChildOf(s.transform))continue;float score=d+(1-Vector3.Dot(delta.normalized,forward))*.2f;if(score<value){best=s;value=score;}}return best;}
        public KitchenStation FindThrowTarget(Vector3 position,Vector3 forward){if(!ThrowAllowed)return null;return stations.Where(s=>(s.kind=="counter"||s.kind=="prep")&&!s.item&&!s.reserved).Select(s=>new{station=s,delta=Vector3.ProjectOnPlane(s.transform.position-position,Vector3.up)}).Where(x=>x.delta.magnitude>1.1f&&x.delta.magnitude<3.1f&&Vector3.Dot(x.delta.normalized,forward)>.94f).OrderBy(x=>x.delta.magnitude).Select(x=>x.station).FirstOrDefault();}
        // Routine instructions live in the illustrated notebook, not in gameplay pop-ups.
        public void Toast(string message,float duration=3){}
        public void AudioCue(int frequency,float duration){if(sound){sound.Cue(frequency);return;}if(!audioSource||Settings.volume<=0)return;int key=frequency*100+(int)(duration*100);if(!sounds.TryGetValue(key,out var clip)){int count=(int)(22050*duration);var data=new float[count];for(int i=0;i<count;i++)data[i]=Mathf.Sin(2*Mathf.PI*frequency*i/22050f)*.15f*Mathf.Sin(Mathf.PI*i/count);clip=AudioClip.Create("Kitchen cue",count,1,22050,false);clip.SetData(data,0);sounds[key]=clip;}audioSource.PlayOneShot(clip,Settings.volume);}
        public void Puff(Vector3 position){var fx=new GameObject("Success sparkle");fx.transform.SetParent(live.transform);fx.transform.position=position;var particles=fx.AddComponent<ParticleSystem>();var main=particles.main;main.startLifetime=.4f;main.startSpeed=.8f;main.startSize=.035f;main.startColor=new Color(1,.78f,.28f);main.maxParticles=12;var em=particles.emission;em.rateOverTime=0;particles.Emit(10);Destroy(fx,.7f);}
        void OnDestroy(){Time.timeScale=1;foreach(var clip in sounds.Values)if(clip)Destroy(clip);}
    }
}
