using System.Collections.Generic;
using UnityEngine;
namespace BaraKitchen.Gameplay {
    public sealed class CafeGuest : MonoBehaviour {
        public KitchenGame game; public SceneAnchor seat; public GuestPhase phase; public FoodOrder order; public KitchenItem dish;
        public KitchenCharacterAnimator motion; public float eating;
        readonly Queue<Vector3> path=new Queue<Vector3>(); ChefAction last=(ChefAction)(-1); float sitTime; int bite;
        public void Configure(KitchenGame owner,SceneAnchor assigned,GameObject prefab){game=owner;seat=assigned;phase=GuestPhase.Arriving;var v=Instantiate(prefab,transform);v.transform.localPosition=Vector3.zero;motion=v.GetComponent<KitchenCharacterAnimator>();
foreach(var skin in v.GetComponentsInChildren<SkinnedMeshRenderer>())skin.quality=SkinQuality.Bone4;
            foreach(var c in v.GetComponentsInChildren<Collider>())c.enabled=false;transform.position=game.entrance.position;Vector3 aisle=game.aisle.position;path.Enqueue(new Vector3(transform.position.x,0,aisle.z));path.Enqueue(new Vector3(seat.approach.position.x,0,aisle.z));path.Enqueue(seat.approach.position);path.Enqueue(new Vector3(seat.transform.position.x,0,seat.transform.position.z));}
        void Animate(ChefAction a){if(last==a)return;last=a;motion.Play(a);}
        public void Tick(float dt){
            if(path.Count>0){var target=path.Peek();var delta=target-transform.position;delta.y=0;if(delta.magnitude>.025f){transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(delta),dt*10);transform.position=Vector3.MoveTowards(transform.position,target,dt*1.25f);Animate(ChefAction.Walk);}else{transform.position=target;path.Dequeue();}return;}
            if(phase==GuestPhase.Arriving){sitTime+=dt;transform.position=Vector3.Lerp(new Vector3(seat.transform.position.x,0,seat.transform.position.z),seat.transform.position-Vector3.up*.18f,Mathf.Clamp01(sitTime/.3f));transform.rotation=seat.transform.rotation;Animate(ChefAction.CustomerIdle);if(sitTime>=.3f){phase=GuestPhase.Waiting;order=game.CreateOrder(this);}return;}
            if(phase==GuestPhase.Waiting){Animate(ChefAction.CustomerIdle);return;}
            if(phase==GuestPhase.Eating){Animate(ChefAction.CustomerEat);eating+=dt;if(eating>1.1f+bite*2.3f){bite++;game.sound?.Guest("guest-eat",.32f);}if(dish)dish.transform.position=Vector3.Lerp(dish.transform.position,seat.dishPosition.position,Mathf.Min(1,dt*7));if(eating>=6){game.ReturnDish(dish);dish=null;Leave();}return;}
            if(phase==GuestPhase.Leaving){game.guests.Remove(this);Destroy(gameObject);}
        }
        public void Eat(KitchenItem meal){dish=meal;dish.transform.SetParent(game.live.transform,true);phase=GuestPhase.Eating;eating=0;bite=0;game.sound?.Guest("guest-happy",.60f);}
        public void Leave(){phase=GuestPhase.Leaving;transform.position=new Vector3(transform.position.x,0,transform.position.z);path.Clear();path.Enqueue(seat.approach.position);path.Enqueue(new Vector3(seat.approach.position.x,0,game.aisle.position.z));path.Enqueue(new Vector3(game.entrance.position.x,0,game.aisle.position.z));path.Enqueue(game.entrance.position);Animate(ChefAction.Walk);}
    }
}
