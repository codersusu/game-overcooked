using System;
using UnityEngine;
namespace BaraKitchen {
    public enum HeatState { Empty, Cooking, Ready, Warning, Burning, Extinguished }
    public sealed class CookingHazard : MonoBehaviour {
        public float cookingSeconds=20,readySeconds=10,warningSeconds=4;
        public bool heating=true,autoTick;
        public HeatState state=HeatState.Empty;
        [Range(0,1)] public float fireRemaining;
        public KitchenFireVisual visual;
        public VisualState pot;
        public event Action<HeatState> StateChanged;
        public event Action FireStarted,FireStopped;
        public float elapsed {get;private set;}
        public float burningAge {get;private set;}
        public float WarningProgress=>Mathf.Clamp01((elapsed-cookingSeconds-readySeconds)/Mathf.Max(.01f,warningSeconds));
        public float IgnitionProgress=>Mathf.Clamp01(burningAge/.65f);
        public Vector3 FlameOrigin=>visual?visual.transform.position:transform.position;
        public void BeginCooking(){elapsed=0;burningAge=0;fireRemaining=0;heating=true;Set(HeatState.Cooking);}
        public void RemovePot(){bool burning=state==HeatState.Burning;elapsed=0;fireRemaining=0;Set(HeatState.Empty);if(burning)FireStopped?.Invoke();}
        public void Tick(float delta) {
            if(delta<=0||!heating||state==HeatState.Empty||state==HeatState.Extinguished)return;
            if(state==HeatState.Burning){burningAge+=delta;return;}
            elapsed+=delta;
            if(elapsed>=cookingSeconds+readySeconds+warningSeconds){burningAge=elapsed-cookingSeconds-readySeconds-warningSeconds;fireRemaining=1;Set(HeatState.Burning);FireStarted?.Invoke();}
            else if(elapsed>=cookingSeconds+readySeconds)Set(HeatState.Warning);
            else if(elapsed>=cookingSeconds)Set(HeatState.Ready);
        }
        public void Suppress(float amount) {
            if(state!=HeatState.Burning||amount<=0)return;
            fireRemaining=Mathf.Max(0,fireRemaining-amount);
            if(fireRemaining==0){heating=false;Set(HeatState.Extinguished);FireStopped?.Invoke();}
        }
        void Set(HeatState next) {
            if(state==next)return;state=next;
            if(pot)pot.SetState(next==HeatState.Empty?0:next==HeatState.Cooking?4:next==HeatState.Ready||next==HeatState.Warning?5:6);
            StateChanged?.Invoke(next);
        }
        void Update(){if(autoTick){Tick(Time.deltaTime);if(visual)visual.Sample(Time.time,state,fireRemaining,WarningProgress,IgnitionProgress);}}
    }
}
