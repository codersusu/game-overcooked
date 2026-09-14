using System;
using UnityEngine;

namespace BaraKitchen {
    public enum ChefAction { ChefIdle, Walk, Dash, CarryIdle, CarryWalk, Throw, Chop, Wash, Extinguish, CustomerIdle, CustomerEat }

    // Shared pose source for native AnimationClips and deterministic editor review.
    // The game mover owns travel; these clips animate the connected animal body.
    public static class CharacterMotion {
        public static readonly string[] Paths={"PreviewSkeleton/Hips","PreviewSkeleton/Hips/Chest","PreviewSkeleton/Hips/Chest/Head","PreviewSkeleton/Hips/Chest/LeftArm","PreviewSkeleton/Hips/Chest/LeftArm/LeftForearm","PreviewSkeleton/Hips/Chest/RightArm","PreviewSkeleton/Hips/Chest/RightArm/RightForearm","PreviewSkeleton/Hips/LeftLeg","PreviewSkeleton/Hips/LeftLeg/LeftFoot","PreviewSkeleton/Hips/RightLeg","PreviewSkeleton/Hips/RightLeg/RightFoot","CarryAnchor","RightToolAnchor","LeftToolAnchor"};
        public static float Duration(ChefAction a) {
            switch(a) {case ChefAction.Walk:return .8f;case ChefAction.Dash:return .7f;case ChefAction.CarryWalk:return .9f;case ChefAction.Throw:return 1.15f;case ChefAction.Chop:return .72f;case ChefAction.Wash:return 1.2f;case ChefAction.Extinguish:return 1.4f;case ChefAction.CustomerEat:return 2.6f;default:return 3.2f;}
        }
        public static bool Loops(ChefAction a)=>a!=ChefAction.Dash&&a!=ChefAction.Throw;
        public static Transform[] Bind(Transform root) { var t=new Transform[Paths.Length];for(int i=0;i<t.Length;i++)t[i]=root.Find(Paths[i]);return t; }
        static void Rot(Transform t,float x=0,float y=0,float z=0){t.localRotation=Quaternion.Euler(x,y,z);}
        static void Tool(Transform root,Transform socket,Vector3 local,Vector3 angles){socket.position=root.TransformPoint(local);socket.rotation=root.rotation*Quaternion.Euler(angles);}
        static void Arm(Transform root,Transform upper,Transform fore,Transform paw,Vector3 goal) {
            var target=root.TransformPoint(goal);
            for(int i=0;i<16;i++) {
                fore.rotation=Quaternion.FromToRotation(paw.position-fore.position,target-fore.position)*fore.rotation;
                upper.rotation=Quaternion.FromToRotation(paw.position-upper.position,target-upper.position)*upper.rotation;
            }
        }
        static void WorkArm(Transform root,Transform upper,Transform fore,Transform paw,Vector3 goal,float side) {
            var a=upper.position;float upperLength=Vector3.Distance(a,fore.position),lowerLength=Vector3.Distance(fore.position,paw.position);
            var direction=root.TransformPoint(goal)-a;float distance=Mathf.Clamp(direction.magnitude,Mathf.Abs(upperLength-lowerLength)+.001f,upperLength+lowerLength-.001f);
            var axis=direction.normalized;var target=a+axis*distance;
            // A fixed outward elbow plane avoids CCD folding/twisting the elbow
            // through the torso when the short paw moves across the work surface.
            var pole=root.TransformPoint(new Vector3(side*.70f,.35f,.10f))-a;
            var outward=Vector3.ProjectOnPlane(pole,axis).normalized;
            float along=(upperLength*upperLength+distance*distance-lowerLength*lowerLength)/(2*distance);
            var elbow=a+axis*along+outward*Mathf.Sqrt(Mathf.Max(0,upperLength*upperLength-along*along));
            upper.rotation=Quaternion.FromToRotation(fore.position-a,elbow-a)*upper.rotation;
            fore.rotation=Quaternion.FromToRotation(paw.position-fore.position,target-fore.position)*fore.rotation;
        }
        static void SideStretch(Transform[] b,float phase) {
            float lift=.5f-.5f*Mathf.Cos(phase);
            Rot(b[3],0,0,-4-21.5f*lift);Rot(b[5],0,0,4+21.5f*lift);
            Rot(b[4],0,0,-2.5f*lift);Rot(b[6],0,0,2.5f*lift);
        }
        static void Step(Transform[] b,float phase,float strength=1) {
            float step=Mathf.Sin(phase);
            Rot(b[7],36*step*strength);Rot(b[9],-36*step*strength);
            // Exaggerate the short paws' stride while leaving the torso upright.
            b[7].localPosition+=new Vector3(0,.018f*Mathf.Max(0,-step)*strength,-.047f*step*strength);
            b[9].localPosition+=new Vector3(0,.018f*Mathf.Max(0,step)*strength,.047f*step*strength);
            Rot(b[8],-18*Mathf.Max(0,step)*strength);Rot(b[10],-18*Mathf.Max(0,-step)*strength);
        }
        public static void Sample(Transform root,Transform[] b,ChefAction action,float seconds) {
            float d=Duration(action),u=Mathf.Clamp01(seconds/d),p=u*Mathf.PI*2,s=Mathf.Sin(p),c=Mathf.Cos(p);
            foreach(var t in b){t.localRotation=Quaternion.identity;t.localScale=Vector3.one;}
            b[0].localPosition=new Vector3(0,.18f,0);
            b[7].localPosition=new Vector3(-.10f,-.01f,0);b[9].localPosition=new Vector3(.10f,-.01f,0);
            b[4].localPosition=new Vector3(-.14f,-.13f,0);b[6].localPosition=new Vector3(.14f,-.13f,0);
            float reach=action==ChefAction.Chop||action==ChefAction.Wash?1.25f:action==ChefAction.CarryIdle||action==ChefAction.CarryWalk||action==ChefAction.CustomerEat?1.25f:1;
            b[4].localPosition*=reach;b[6].localPosition*=reach;
            b[11].localPosition=new Vector3(0,.52f,.30f);b[12].localPosition=new Vector3(.16f,.62f,.25f);b[13].localPosition=new Vector3(-.16f,.62f,.25f);
            Rot(b[3],0,0,-4);Rot(b[5],0,0,4);
            bool seated=action==ChefAction.CustomerIdle||action==ChefAction.CustomerEat;
            bool carry=action==ChefAction.CarryIdle||action==ChefAction.CarryWalk;
            if(seated) {
                Rot(b[7],-67);Rot(b[9],-67);Rot(b[8],28);Rot(b[10],28);
                Rot(b[2],0,2*s);
                if(action==ChefAction.CustomerIdle)SideStretch(b,p);
                else {
                    float bite=Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Sin(p)));
                    var goal=Vector3.Lerp(new Vector3(.36f,.47f,.34f),new Vector3(.23f,.73f,.34f),bite);
                    Arm(root,b[5],b[6],b[6].Find("Paw"),goal);
                    Arm(root,b[3],b[4],b[4].Find("Paw"),new Vector3(-.26f,.45f,.34f));
                    Tool(root,b[12],root.InverseTransformPoint(b[6].Find("Paw").position),new Vector3(0,0,-20));
                    Rot(b[2],2*bite,0,Mathf.Sin(p*4)*bite*1.5f);
                }
                return;
            }
            if(action==ChefAction.ChefIdle) {SideStretch(b,p);Rot(b[2],0,1.5f*s);}
            if(action==ChefAction.CarryIdle) {Rot(b[2],0,2*s);}
            if(action==ChefAction.Walk||action==ChefAction.CarryWalk) {
                b[0].localPosition+=new Vector3(0,.007f*(1-Mathf.Cos(p*2)),0);
                Rot(b[0],0,1.5f*s,1.5f*s);Rot(b[1],2,0,-s);Rot(b[2],-2,0,-s);
                Step(b,p);
                Rot(b[3],-30*s,0,-4);Rot(b[5],30*s,0,4);Rot(b[4],-5);Rot(b[6],-5);
            }
            if(carry) {
                Arm(root,b[3],b[4],b[4].Find("Paw"),new Vector3(-.155f,.46f,.37f));
                Arm(root,b[5],b[6],b[6].Find("Paw"),new Vector3(.155f,.46f,.37f));
                b[11].localPosition=new Vector3(0,.442f,.37f);
            }
            if(action==ChefAction.Dash) {
                float burst=Mathf.Sin(u*Mathf.PI);
                Rot(b[0],22*burst);b[0].localPosition+=new Vector3(0,.022f*burst,.015f*burst);
                Rot(b[1],7*burst);Rot(b[2],-9*burst);
                float run=u*Mathf.PI*8;Step(b,run,1.25f*burst);
                Rot(b[3],Mathf.Sin(run)*42*burst,0,-12*burst);Rot(b[5],-Mathf.Sin(run)*42*burst,0,12*burst);
                Rot(b[4],-18*burst);Rot(b[6],-18*burst);
            }
            if(action==ChefAction.Throw) {
                float wind=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.24f,u));
                float release=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,.40f,u));
                float recover=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1,u));
                Rot(b[1],Mathf.Lerp(-5*wind,8,release)*(1-recover),-9*wind*(1-release));
                var goal=Vector3.Lerp(new Vector3(.44f,.40f,.08f),new Vector3(.54f,.69f,-.16f),wind);
                goal=Vector3.Lerp(goal,new Vector3(.42f,.68f,.29f),release);
                goal=Vector3.Lerp(goal,new Vector3(.49f,.35f,-.02f),recover);
                Arm(root,b[5],b[6],b[6].Find("Paw"),goal);
                Tool(root,b[12],root.InverseTransformPoint(b[6].Find("Paw").position),Vector3.zero);
                Rot(b[3],-12*(1-recover),0,-9*(1-recover));
            }
            if(action==ChefAction.Chop) {
                float lift=.5f+.5f*c;
                // Work to one side of the muzzle, with a still head and torso.
                var right=new Vector3(.31f,.53f+.16f*lift,.30f-.018f*lift);
                WorkArm(root,b[5],b[6],b[6].Find("Paw"),right,1);
                WorkArm(root,b[3],b[4],b[4].Find("Paw"),new Vector3(-.10f,.49f,.31f),-1);
                Tool(root,b[12],root.InverseTransformPoint(b[6].Find("Paw").position),new Vector3(-5*lift,0,90));
            }
            if(action==ChefAction.Wash) {
                var right=new Vector3(.34f,.505f+.025f*s,.34f);
                WorkArm(root,b[5],b[6],b[6].Find("Paw"),right,1);
                WorkArm(root,b[3],b[4],b[4].Find("Paw"),new Vector3(-.10f,.48f,.32f),-1);
                Tool(root,b[12],root.InverseTransformPoint(b[6].Find("Paw").position),Vector3.zero);
            }
            if(action==ChefAction.Extinguish) {
                var shift=new Vector3(.015f*s,0,0);b[0].localPosition+=shift;
                Rot(b[0],0,1.5f*s,-1.5f*s);Rot(b[1],-3,1*s);Rot(b[2],2,-1*s);
                Rot(b[7],-8);Rot(b[9],10);
                Tool(root,b[11],new Vector3(.09f,.28f,.27f)+shift,new Vector3(0,2.5f*s,-6-1.5f*s));
                var grip=b[11].TransformPoint(new Vector3(0,.34f,0));var support=b[11].TransformPoint(new Vector3(-.08f,.14f,0));
                Arm(root,b[5],b[6],b[6].Find("Paw"),root.InverseTransformPoint(grip));
                Arm(root,b[3],b[4],b[4].Find("Paw"),root.InverseTransformPoint(support));
            }
        }
    }
}
