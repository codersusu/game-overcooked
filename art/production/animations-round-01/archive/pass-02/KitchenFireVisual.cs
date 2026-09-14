using UnityEngine;
namespace BaraKitchen {
    // Native billboard particles with explicit sampling for reproducible offline
    // renders. The same system is used by the runtime CookingHazard component.
    public sealed class KitchenFireVisual : MonoBehaviour {
        public ParticleSystem flameParticles,smokeParticles,emberParticles;
        public Transform warning;
        public Light glow;
        readonly ParticleSystem.Particle[] fire=new ParticleSystem.Particle[80];
        readonly ParticleSystem.Particle[] haze=new ParticleSystem.Particle[28];
        readonly ParticleSystem.Particle[] sparks=new ParticleSystem.Particle[22];
        static ParticleSystem.Particle Dot(Vector3 position,Vector3 size,Color color,float rotation=0) {
            return new ParticleSystem.Particle{position=position,startSize3D=size,startColor=color,startLifetime=10,remainingLifetime=10,rotation3D=new Vector3(0,0,rotation)};
        }
        static void Apply(ParticleSystem system,ParticleSystem.Particle[] particles,int count) {
            // Activate simulation once, then retain explicit deterministic particles.
            system.Play(false);system.Pause(false);system.SetParticles(particles,count);
        }
        public void Sample(float time,HeatState state,float strength=1,float warningProgress=0,float ignitionProgress=1) {
            bool burning=state==HeatState.Burning,warn=state==HeatState.Warning;
            float amount=burning?Mathf.Clamp01(strength)*Mathf.Lerp(.34f,1,Mathf.SmoothStep(0,1,ignitionProgress)):warn?Mathf.Lerp(.045f,.34f,Mathf.SmoothStep(0,1,warningProgress)):0;
            int count=amount>0?fire.Length:0;
            for(int i=0;i<count;i++) {
                float seed=i*2.399963f,u=Mathf.Repeat(time*(1.2f+(i%5)*.1f)+i*.618f,1);
                float height=(.13f+.67f*u)*Mathf.Sqrt(amount),spread=.13f*(1-u)*Mathf.Sqrt(amount);
                float flutter=Mathf.Sin(time*13+i*2.1f)*.035f*u;
                var pos=new Vector3(Mathf.Sin(seed)*spread+flutter,height,warn?.10f:Mathf.Cos(seed)*spread);
                bool core=i%4==0;
                var color=core?new Color(1,.94f,.34f,.88f):Color.Lerp(new Color(1,.62f,.035f,.93f),new Color(1,.075f,.01f,0),u);
                float size=(core?.13f:.24f)*(1-u*.65f)*Mathf.Sqrt(amount);
                fire[i]=Dot(pos,new Vector3(size,size*(core?1.5f:2.1f),size),color,Mathf.Sin(time*9+i)*15);
            }
            Apply(flameParticles,fire,count);
            bool smoking=state==HeatState.Cooking||state==HeatState.Ready||warn||burning;
            int smokeCount=smoking?haze.Length:0;
            for(int i=0;i<smokeCount;i++) {
                float u=Mathf.Repeat(time*(burning?.44f:.30f)+i*.618f,1);
                float heat=warn?warningProgress:burning?strength:0;
                var pos=new Vector3(Mathf.Sin(i*2.4f+time*.7f)*(.03f+u*.18f),.16f+u*(.75f+heat*.4f),.035f);
                float size=(.055f+.23f*u)*(1+heat*.6f);
                var color=Color.Lerp(new Color(.96f,.99f,1,.32f),new Color(.29f,.26f,.24f,.46f),heat);
                color.a*=Mathf.Sin(u*Mathf.PI)*(burning?strength:1);
                haze[i]=Dot(pos,Vector3.one*size,color,i*29+time*14);
            }
            Apply(smokeParticles,haze,smokeCount);
            int sparkCount=amount>.10f?sparks.Length:0;
            for(int i=0;i<sparkCount;i++) {
                float u=Mathf.Repeat(time*(.7f+i%3*.15f)+i*.3819f,1),a=i*2.4f;
                var pos=new Vector3(Mathf.Sin(a)*u*.27f,.1f+u*.95f,Mathf.Cos(a)*u*.20f)*Mathf.Sqrt(amount);
                sparks[i]=Dot(pos,new Vector3(.014f,.035f,.014f)*(1-u),new Color(1,.62f,.06f,(1-u)*.9f),-Mathf.Sin(a)*25);
            }
            Apply(emberParticles,sparks,sparkCount);
            if(glow){glow.enabled=amount>0;glow.intensity=amount*(.7f+.18f*Mathf.Sin(time*23));}
            if(warning){warning.gameObject.SetActive(warn);warning.localScale=Vector3.one*(.9f+.1f*Mathf.Sin(time*8));}
        }
    }
}
