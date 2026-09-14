using System;
using UnityEngine;
namespace BaraKitchen {
    [Serializable] public class ReviewAsset { public string id;public GameObject prefab; }
    public sealed class AnimationReviewStage : MonoBehaviour {
        public ReviewAsset[] assets,animals;
        public string scenario="ChefIdle",animal="capybara-male-chef";
        public bool autoplay=true,showControls=true;
        public float playhead;
        public KitchenCharacterAnimator actor;
        public CookingHazard hazard;
        public ExtinguisherSpray spray;
        Transform content,held,food;VisualState foodState,dishState;
        Vector3 throwStart;float previous=-1;DashAfterimage dashTrail;Transform biteFood;
        public static string[] Scenarios={"ChefIdle","Walk","Dash","CarryIdle","CarryWalk","Throw","Chop","Wash","Extinguish","CustomerIdle","CustomerEat","Overcook"};
        public static float Length(string id){switch(id){case "Walk":return 2.4f;case "Dash":return 1.15f;case "CarryWalk":return 2.7f;case "Throw":return 2.0f;case "Extinguish":return 4.5f;case "CustomerEat":return 2.6f;case "Overcook":return 12.5f;default:return 3.2f;}}
        GameObject Make(string id,Vector3 position,Transform parent=null,float scale=1) {
            var asset=Array.Find(assets,a=>a.id==id);if(asset==null)throw new Exception("Missing review asset "+id);
            var go=Instantiate(asset.prefab,parent?parent:content);go.name=id;go.transform.localPosition=position;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one*scale;return go;
        }
        static void Remove(GameObject go){if(Application.isPlaying)Destroy(go);else DestroyImmediate(go);}
        public void SetScenario(string next,string character=null) {
            if(!content)content=transform.Find("Review arrangement");
            if(content)Remove(content.gameObject);
            scenario=next;if(!string.IsNullOrEmpty(character))animal=character;
            playhead=0;previous=-1;held=null;food=null;foodState=null;dishState=null;hazard=null;spray=null;dashTrail=null;biteFood=null;
            content=new GameObject("Review arrangement").transform;content.SetParent(transform,false);
            var source=Array.Find(animals,a=>a.id==animal);var go=Instantiate(source.prefab,content);go.name=animal;actor=go.GetComponent<KitchenCharacterAnimator>();actor.transform.localPosition=Vector3.zero;
            if(next=="CustomerIdle"||next=="CustomerEat") {
                actor.transform.localPosition=new Vector3(0,.1765f,0);Make("cafe-chair",Vector3.zero,null,1.15f);Make("cafe-table",new Vector3(0,0,.83f),null,1.15f);Make("dish-salad",new Vector3(0,.621f,.60f));Make("table-flower-vase",new Vector3(.23f,.62f,.93f));
            }
            if(next=="CustomerEat")biteFood=Make("tomato-whole",Vector3.zero,actor.rightToolAnchor,.20f).transform;
            if(next=="Dash")dashTrail=content.gameObject.AddComponent<DashAfterimage>();
            if(next=="CarryIdle"||next=="CarryWalk")held=Make("dish-salad",Vector3.zero,actor.carryAnchor).transform;
            if(next=="Throw") {
                Make("rail",new Vector3(0,0,1.15f),null,1.15f);Make("counter",new Vector3(0,0,2.13f),null,1.15f);
                held=Make("tomato-whole",Vector3.zero,actor.rightToolAnchor,.85f).transform;
                actor.SampleForReview(ChefAction.Throw,CharacterMotion.Duration(ChefAction.Throw)*.4f);throwStart=actor.rightToolAnchor.position;
            }
            if(next=="Chop") {
                var station=Make("counter",new Vector3(.12f,0,.71f),null,1.15f);float top=station.transform.Find("Worktop").position.y;
                Make("cutting-board",new Vector3(.23f,top,.42f));
                foodState=Make("Ingredient_tomato",new Vector3(.30f,top+.03f,.43f),null,.68f).GetComponent<VisualState>();food=foodState.transform;held=Make("knife",new Vector3(0,0,.085f),actor.rightToolAnchor).transform;
            }
            if(next=="Wash") {
                var station=Make("station-sink",new Vector3(.10f,0,.69f),null,1.15f);station.transform.localRotation=Quaternion.Euler(0,180,0);float top=station.transform.Find("Worktop").position.y;
                dishState=Make("UniversalDish",new Vector3(.18f,top+.01f,.34f)).GetComponent<VisualState>();dishState.SetState(5);
                held=Make("sponge",new Vector3(0,-.019f,0),actor.rightToolAnchor).transform;
            }
            if(next=="Overcook"||next=="Extinguish") {
                var station=Make("counter",new Vector3(0,0,.8f),null,1.15f);float top=station.transform.Find("Worktop").position.y;
                var pot=Make("CookingPot",new Vector3(0,top+.018f,.80f)).GetComponent<VisualState>();
                var ring=Make("pot-lid",new Vector3(0,top+.007f,.8f));ring.transform.localScale=new Vector3(1.3f,.08f,1.3f);
                var fx=Make("CookingFireVisual",new Vector3(0,top+.19f,.8f)).GetComponent<KitchenFireVisual>();hazard=pot.gameObject.AddComponent<CookingHazard>();hazard.visual=fx;hazard.pot=pot;hazard.cookingSeconds=1.2f;hazard.readySeconds=1.0f;hazard.warningSeconds=8f;hazard.BeginCooking();
                if(next=="Overcook"){actor.transform.localPosition=new Vector3(-.90f,0,-.15f);actor.transform.localRotation=Quaternion.Euler(0,0,0);}
                else {
                    actor.transform.localPosition=new Vector3(0,0,-.18f);held=Make("fire-extinguisher",Vector3.zero,actor.carryAnchor).transform;spray=Make("ExtinguisherSpray",Vector3.zero).GetComponent<ExtinguisherSpray>();hazard.Tick(hazard.cookingSeconds+hazard.readySeconds+hazard.warningSeconds+1);spray.range=2.5f;
                }
            }
            SampleFrame(0);if(Camera.main)FrameCamera(Camera.main,scenario);
        }
        public static void FrameCamera(Camera cam,string scenario) {
            bool station=Array.IndexOf(new[]{"Chop","Wash","Extinguish","Overcook","CustomerIdle","CustomerEat"},scenario)>=0;bool throwing=scenario=="Throw";
            Vector3 target=throwing?new Vector3(0,.67f,.85f):station?new Vector3(0,.66f,.40f):new Vector3(0,.70f,.1f);
            cam.transform.position=target+(station?new Vector3(3.8f,2.9f,4.2f):new Vector3(3.4f,1.95f,4.4f));cam.transform.LookAt(target);cam.orthographicSize=throwing?1.60f:station?1.22f:1.02f;cam.aspect=1;
            if(scenario=="Overcook") {target=new Vector3(-.15f,.79f,.35f);cam.transform.position=target+new Vector3(3.8f,2.9f,4.2f);cam.transform.LookAt(target);cam.orthographicSize=1.37f;}
        }
        public void SampleFrame(float t) {
            if(!actor)return;
            var action=scenario=="Overcook"?ChefAction.ChefIdle:(ChefAction)Enum.Parse(typeof(ChefAction),scenario);
            float poseTime=CharacterMotion.Loops(action)?Mathf.Repeat(t,CharacterMotion.Duration(action)):Mathf.Min(t,CharacterMotion.Duration(action));
            if(scenario=="Dash") {float travel=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.06f,.66f,t));actor.transform.localPosition=new Vector3(0,0,-.65f+travel*1.3f);}
            if(scenario=="Chop"&&t>2.16f){action=ChefAction.ChefIdle;poseTime=Mathf.Repeat(t-2.16f,CharacterMotion.Duration(action));}
            if(scenario=="Wash"&&t>2.4f){action=ChefAction.CarryIdle;poseTime=0;}
            actor.SampleForReview(action,poseTime);
            if(dashTrail)dashTrail.Sample(actor.GetComponent<SkinnedMeshRenderer>(),t);
            if(biteFood)biteFood.gameObject.SetActive(Mathf.Repeat(t,CharacterMotion.Duration(ChefAction.CustomerEat))<1.15f);
            if(scenario=="Throw"&&held) {
                float release=CharacterMotion.Duration(ChefAction.Throw)*.4f;
                if(t<release){held.SetParent(actor.rightToolAnchor,false);held.localPosition=Vector3.zero;held.localRotation=Quaternion.identity;}
                else{held.SetParent(content,true);float u=Mathf.Clamp01((t-release)/.72f);held.position=Vector3.Lerp(throwStart,new Vector3(0,.46f,2.13f),u)+Vector3.up*(1.25f*4*u*(1-u));held.rotation=Quaternion.Euler(u<1?u*180:0,0,0);}
            }
            if(foodState){foodState.SetState(t>=2.16f?1:0);held.gameObject.SetActive(t<2.16f);}
            if(dishState){dishState.SetState(t>=2.4f?0:5);held.gameObject.SetActive(t<2.4f);if(t>=2.4f){dishState.transform.SetParent(actor.carryAnchor,false);dishState.transform.localPosition=Vector3.zero;dishState.transform.localRotation=Quaternion.identity;}}
            float delta=Mathf.Max(0,t-(previous<0?0:previous));previous=t;
            if(hazard) {
                hazard.Tick(delta);
                if(spray) {
                    var nozzle=held.Find("Nozzle");spray.nozzle.position=nozzle.position;spray.nozzle.LookAt(hazard.FlameOrigin);spray.visualDistance=Mathf.Min(spray.range,Vector3.Distance(spray.nozzle.position,hazard.FlameOrigin));bool active=t>=.7f&&t<3.4f&&hazard.state==HeatState.Burning;
                    spray.Sample(t,active);spray.TrySuppress(hazard,delta);
                }
                hazard.visual.Sample(t,hazard.state,hazard.fireRemaining,hazard.WarningProgress,hazard.IgnitionProgress);
            }
            playhead=t;
        }
        void Start(){SetScenario(scenario,animal);}
        void Update(){if(!autoplay||!actor)return;float t=playhead+Time.deltaTime;if(t>=Length(scenario))SetScenario(scenario,animal);else SampleFrame(t);}
        void OnGUI() {
            if(!showControls)return;
            GUILayout.BeginArea(new Rect(12,12,520,230),GUI.skin.box);GUILayout.Label("Bara Kitchen · animation review");
            int old=Array.IndexOf(Scenarios,scenario),next=GUILayout.SelectionGrid(old,Scenarios,4);if(next!=old)SetScenario(Scenarios[next]);
            int index=Array.FindIndex(animals,a=>a.id==animal);if(GUILayout.Button("Animal: "+animal+"  →"))SetScenario(scenario,animals[(index+1)%animals.Length].id);
            if(GUILayout.Button(autoplay?"Pause":"Play"))autoplay=!autoplay;if(GUILayout.Button("Replay"))SetScenario(scenario,animal);
            GUILayout.Label(hazard?"Pot: "+hazard.state:"In-place clips; dash/throw travel is a review demonstration.");GUILayout.EndArea();
        }
    }
}
