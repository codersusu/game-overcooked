using System;
using UnityEngine;
namespace BaraKitchen {
    [DisallowMultipleComponent]
    public sealed class KitchenCharacterAnimator : MonoBehaviour {
        public Animator animator;
        public ChefAction action=ChefAction.ChefIdle;
        public Transform carryAnchor,rightToolAnchor,leftToolAnchor;
        public event Action<string> Marker;
        Transform[] poseBones;
        public void Play(ChefAction next,float blend=.12f) {
            action=next;
            if(animator&&animator.runtimeAnimatorController){animator.enabled=true;animator.CrossFadeInFixedTime(next.ToString(),blend);}
        }
        public void SampleForReview(ChefAction next,float time) {
            if(animator)animator.enabled=false;
            if(poseBones==null)poseBones=CharacterMotion.Bind(transform);
            action=next;CharacterMotion.Sample(transform,poseBones,next,time);
        }
        public void OnAnimationMarker(string marker){Marker?.Invoke(marker);}
        void Start(){if(animator&&animator.enabled){animator.Play(action.ToString(),0,0);}}
    }
}
