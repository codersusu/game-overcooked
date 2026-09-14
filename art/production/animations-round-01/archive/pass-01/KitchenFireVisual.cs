using UnityEngine;
namespace BaraKitchen {
    public sealed class KitchenFireVisual : MonoBehaviour {
        public Transform[] flames,cores,smoke,embers;
        public Transform warning;
        public void Sample(float time,HeatState state,float strength=1) {
            bool fire=state==HeatState.Burning;float amount=fire?Mathf.Sqrt(Mathf.Clamp01(strength)):0;
            for(int i=0;i<flames.Length;i++) {
                float a=i*2.399f,beat=.86f+.14f*Mathf.Sin(time*13+i*1.8f),h=(i==0?1.2f:.78f)*beat;
                flames[i].gameObject.SetActive(fire);cores[i].gameObject.SetActive(fire);
                flames[i].localPosition=new Vector3(Mathf.Sin(a)*.10f,0,Mathf.Cos(a)*.10f);
                flames[i].localScale=new Vector3(.72f,h,.72f)*amount;
                flames[i].localRotation=Quaternion.Euler(0,i*72+Mathf.Sin(time*8+i)*12,Mathf.Sin(time*9+i)*8);
                cores[i].localPosition=flames[i].localPosition+new Vector3(.035f,.006f,.095f);cores[i].localScale=flames[i].localScale*.62f;cores[i].localRotation=flames[i].localRotation;
            }
            bool haze=state==HeatState.Warning||fire||state==HeatState.Cooking||state==HeatState.Ready;
            for(int i=0;i<smoke.Length;i++) {
                float t=Mathf.Repeat(time*.5f+i/(float)smoke.Length,1);
                smoke[i].gameObject.SetActive(haze);smoke[i].localPosition=new Vector3(Mathf.Sin(i*2+time)*.08f*t,.12f+t*.9f,0);
                smoke[i].localScale=Vector3.one*(.25f+t*.95f)*(fire?1:.55f)*(1-t);
            }
            for(int i=0;i<embers.Length;i++){
                float t=Mathf.Repeat(time*1.2f+i*.19f,1);embers[i].gameObject.SetActive(fire);embers[i].localPosition=new Vector3(Mathf.Sin(i*9)*t*.22f,.12f+t*.7f,Mathf.Cos(i*9)*t*.22f);embers[i].localScale=Vector3.one*.13f*(1-t)*amount;
            }
            if(warning){warning.gameObject.SetActive(state==HeatState.Warning);warning.localScale=Vector3.one*(.9f+.1f*Mathf.Sin(time*8));}
        }
    }
}
