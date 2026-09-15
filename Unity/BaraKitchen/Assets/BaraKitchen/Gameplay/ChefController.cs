using UnityEngine;
namespace BaraKitchen.Gameplay {
    public sealed class ChefController : MonoBehaviour {
        public KitchenGame game; public KitchenCharacterAnimator motion; public CharacterController motor; public KitchenItem held;
        public KitchenStation focused,working,throwTarget; public float workProgress,dashCooldown; public string workLabel;
        Vector3 dashDirection; float dashTime,throwTime; KitchenItem thrown; KitchenStation landing; Vector3 throwOrigin; bool released; float workSoundAt,stepSoundAt;
        GameObject tool; ExtinguisherSpray spray; DashAfterimage trail; float trailTime=1; ChefAction lastAction=(ChefAction)(-1);
        public void Configure(KitchenGame owner,GameObject prefab){game=owner;var v=Instantiate(prefab,transform);v.transform.localPosition=Vector3.zero;v.transform.localRotation=Quaternion.identity;motion=v.GetComponent<KitchenCharacterAnimator>();
foreach(var skin in v.GetComponentsInChildren<SkinnedMeshRenderer>())skin.quality=SkinQuality.Bone4;
            foreach(var c in v.GetComponentsInChildren<Collider>())c.enabled=false;motor=gameObject.AddComponent<CharacterController>();motor.height=1.12f;motor.radius=.22f;motor.center=new Vector3(0,.56f,0);motor.stepOffset=.04f;motor.slopeLimit=30;motor.skinWidth=.02f;trail=new GameObject("Dash afterimages").AddComponent<DashAfterimage>();trail.transform.SetParent(game.live.transform);}
        void LateUpdate(){if(game.phase==SessionPhase.Service&&trailTime<.71f){trailTime+=Time.deltaTime*2.4f;trail.Sample(motion.GetComponent<SkinnedMeshRenderer>(),trailTime);}}
        public void Animate(ChefAction a){if(lastAction==a)return;lastAction=a;motion.Play(a);}
        void Update(){if(!game||game.phase!=SessionPhase.Service||(game.voice&&game.voice.IsConfiguring))return;float dt=Time.deltaTime;dashCooldown=Mathf.Max(0,dashCooldown-dt);
            if(throwTime>0){TickThrow(dt);return;}
            Vector2 input=new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));
            Vector3 right=game.cam.transform.right;right.y=0;right.Normalize();Vector3 forward=Vector3.ProjectOnPlane(game.cam.transform.forward,Vector3.up).normalized;
            Vector3 move=Vector3.ClampMagnitude(right*input.x+forward*input.y,1);
            
            if(Input.GetKeyDown(KeyCode.H)){game.ui.ToggleHelp();return;}
            if(working&&(move.sqrMagnitude>.1f||Input.GetKeyDown(KeyCode.E)||Input.GetKeyDown(KeyCode.Q)||Input.GetKeyDown(KeyCode.R)||Input.GetKeyDown(KeyCode.LeftShift))){CancelWork();}
            if(dashTime>0){dashTime-=dt;motor.Move(dashDirection*7.5f*dt+Vector3.down*dt);Animate(ChefAction.Dash);return;}
            if(!working){
                if(move.sqrMagnitude>.01f){transform.rotation=Quaternion.LookRotation(move);motor.Move(move*3.15f*dt+Vector3.down*dt);if(Time.time>=stepSoundAt){game.sound?.Play("step",.16f);stepSoundAt=Time.time+.29f;}Animate(held?ChefAction.CarryWalk:ChefAction.Walk);}else{motor.Move(Vector3.down*dt);Animate(held?ChefAction.CarryIdle:ChefAction.ChefIdle);}
                focused=game.FindStation(transform.position,transform.forward);throwTarget=game.FindThrowTarget(transform.position,transform.forward);
                if(Input.GetKeyDown(KeyCode.LeftShift)||Input.GetKeyDown(KeyCode.RightShift)){if(game.DashAllowed&&dashCooldown<=0){dashDirection=transform.forward;dashTime=.22f;dashCooldown=1.2f;trailTime=.061f;game.AudioCue(220,.08f);}else if(!game.DashAllowed)game.Toast("Dash is introduced after your first soup.");}
                if(Input.GetKeyDown(KeyCode.E))Interact();
                if(Input.GetKeyDown(KeyCode.R))TakePortion();
                if(Input.GetKeyDown(KeyCode.Space)&&move.sqrMagnitude<.01f)BeginWork();
                if(Input.GetKeyDown(KeyCode.Q))Throw();
            }
            if(working)TickWork(dt);
        }
        public bool Interact(){if(game.phase!=SessionPhase.Service||working||throwTime>0||!focused)return false;bool plate=held&&held.kind==ItemKind.Dish||focused.item&&focused.item.kind==ItemKind.Dish||focused.kind=="dishes"||focused.kind=="return";bool acted=game.Interact(this,focused);if(acted)game.sound?.Play(plate?"plate":"pickup",plate?.5f:.24f);return acted;}
        public bool TakePortion(){if(game.phase!=SessionPhase.Service||working||held||!focused||!focused.item||focused.item.kind!=ItemKind.Dish)return false;var portion=focused.item.TakePortion();if(!portion)return false;Hold(portion);game.sound?.Play("pickup",.24f);return true;}
        public void Hold(KitchenItem item){held=item;if(item)item.Attach(motion.carryAnchor,Vector3.zero);Animate(item?ChefAction.CarryIdle:ChefAction.ChefIdle);}
        public KitchenItem Release(){var i=held;held=null;if(i)i.transform.SetParent(game.live.transform,true);return i;}
        public bool BeginWork(){if(game.phase!=SessionPhase.Service||working||!focused)return false;var s=focused;
            bool chop=s.kind=="prep"&&s.item&&s.item.kind==ItemKind.Ingredient&&!s.item.chopped&&!held;
            bool wash=s.kind=="sink"&&s.item&&s.item.dirty&&!held;
            bool fire=held&&held.kind==ItemKind.Extinguisher&&s.hazard&&s.hazard.state==HeatState.Burning;
            if(!chop&&!wash&&!fire)return false;
            working=s;workProgress=0;workSoundAt=(s.item?s.item.prep:0)+.29f;workLabel=chop?"Chopping":wash?"Washing":"Extinguishing";transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(s.transform.position-transform.position,Vector3.up));
            if(chop||wash){var p=s.item.transform.position;p=transform.TransformPoint(chop?new Vector3(.30f,.47f,.32f):new Vector3(.18f,.47f,.34f));s.item.transform.position=p;}
            if(chop){tool=game.Spawn("knife",motion.rightToolAnchor,new Vector3(0,0,.085f));Animate(ChefAction.Chop);}
            else if(wash){tool=game.Spawn("sponge",motion.rightToolAnchor,new Vector3(0,-.019f,0));Animate(ChefAction.Wash);}
            else{spray=game.Spawn("ExtinguisherSpray",game.live.transform,Vector3.zero).GetComponent<ExtinguisherSpray>();Animate(ChefAction.Extinguish);}
            return true;
        }
        public void TickWork(float dt){if(!working)return;var s=working;
            if(game.sound){if(s.kind=="prep"&&s.item.prep>=workSoundAt){game.sound.Play("chop",.6f);workSoundAt=s.item.prep+.72f;}else if(s.kind=="sink")game.sound.WorkLoop("wash");else if(s.hazard)game.sound.WorkLoop("spray");}
            if(s.kind=="prep"){s.item.prep+=dt;workProgress=s.item.prep/2.16f;if(s.item.prep>=2.16f){s.item.chopped=true;s.item.Refresh();game.AudioCue(680,.07f);game.Toast("Chopped "+s.item.ingredient+" ready");CancelWork();}}
            else if(s.kind=="sink"){s.washing+=dt;workProgress=s.washing/2.4f;if(s.washing>=2.4f){s.washing=0;s.item.ClearFood();s.item.dirty=false;s.item.Refresh();var dish=s.Take();CancelWork();Hold(dish);game.Toast("Clean dish — ready to reuse");game.AudioCue(760,.12f);}}
            else{if(!spray||!held){CancelWork();return;}var nozzle=held.transform.Find("Nozzle");spray.nozzle.position=nozzle?nozzle.position:motion.carryAnchor.position;spray.nozzle.LookAt(s.hazard.FlameOrigin);spray.visualDistance=Vector3.Distance(spray.nozzle.position,s.hazard.FlameOrigin);spray.Sample(Time.time,true);spray.TrySuppress(s.hazard,dt);workProgress=1-s.hazard.fireRemaining;if(s.hazard.state!=HeatState.Burning){CancelWork();game.Toast("Fire out! Take the burnt pot to the food bin.");}}
        }
        public void CancelWork(){if(game&&game.sound)game.sound.StopWork();if(working&&working.item){working.Arrange();}working=null;workProgress=0;if(tool)Destroy(tool);if(spray)Destroy(spray.gameObject);tool=null;spray=null;Animate(held?ChefAction.CarryIdle:ChefAction.ChefIdle);}
        public bool Throw(){if(!game.ThrowAllowed){game.Toast("Ingredient throwing starts in kitchen 3.");return false;}if(!held||held.kind!=ItemKind.Ingredient){game.Toast("Only ingredients can be thrown.");return false;}var target=throwTarget;if(!target||target.item||target.reserved){game.Toast("Face an empty worktop within throwing range.");return false;}game.sound?.Play("whoosh",.55f);landing=target;landing.reserved=true;thrown=held;throwTime=.001f;released=false;Animate(ChefAction.Throw);return true;}
        public void TickThrow(float dt){if(throwTime<=0)return;throwTime+=dt;if(!released&&throwTime>=.46f){released=true;Release();thrown.flying=true;throwOrigin=motion.rightToolAnchor.position;}
            if(released){float u=Mathf.Clamp01((throwTime-.46f)/.55f);thrown.transform.position=Vector3.Lerp(throwOrigin,landing.Slot,u)+Vector3.up*(1.1f*4*u*(1-u));thrown.transform.rotation=Quaternion.Euler(u*180,0,0);if(u>=1){thrown.flying=false;landing.Put(thrown);landing.reserved=false;throwTime=0;thrown=null;landing=null;Animate(ChefAction.ChefIdle);game.Toast("Ingredient landed on the worktop");}}
        }
        public void Warp(Vector3 position,Vector3 facing){motor.enabled=false;transform.position=position;transform.rotation=Quaternion.LookRotation(facing);motor.enabled=true;Physics.SyncTransforms();}
        public string Prompt(){if(working)return workLabel+"… move to cancel";if(!focused)return held?"Carry to a highlighted station":"Face a station to interact";var s=focused;
            if(s.kind=="prep"&&s.item&&s.item.kind==ItemKind.Ingredient&&!s.item.chopped&&!held)return "SPACE · start chopping   /   E · pick up";
            if(s.kind=="sink"&&s.item&&s.item.dirty&&!held)return "SPACE · start washing";
            if(s.hazard&&s.hazard.state==HeatState.Burning)return held&&held.kind==ItemKind.Extinguisher?"SPACE · start extinguishing":"Find the red fire extinguisher";
            return "E · "+s.Label;
        }
    }
}
