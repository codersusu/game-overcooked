using UnityEngine;
namespace BaraKitchen {
    public sealed class ExtinguisherSpray : MonoBehaviour {
        public Transform nozzle;
        public Transform[] puffs;
        public float range=2.5f,coneAngle=35,suppressionPerSecond=.7f,visualDistance=1;
        public bool spraying;
        public bool TrySuppress(CookingHazard target,float delta) {
            if(!spraying||!target||!nozzle||delta<=0)return false;
            Vector3 direction=target.FlameOrigin-nozzle.position;float distance=direction.magnitude;
            if(distance>range||Vector3.Angle(nozzle.forward,direction)>coneAngle)return false;
            if(Physics.Raycast(nozzle.position,direction.normalized,out var hit,distance-.04f,~0,QueryTriggerInteraction.Ignore)&&!hit.transform.IsChildOf(target.transform))return false;
            target.Suppress(delta*suppressionPerSecond);return true;
        }
        public void Sample(float time,bool active) {
            spraying=active;
            for(int i=0;i<puffs.Length;i++) {
                var p=puffs[i];p.gameObject.SetActive(active);if(!active)continue;
                float t=Mathf.Repeat(time*1.9f+i/(float)puffs.Length,1),a=i*2.4f;
                p.position=nozzle.TransformPoint(new Vector3(Mathf.Sin(a)*t*.14f,Mathf.Cos(a)*t*.11f,t*visualDistance));
                p.localScale=Vector3.one*(.35f+t*1.15f)*(1-t*.5f);
            }
        }
    }
}
