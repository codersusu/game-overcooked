using System;
using UnityEngine;

namespace BaraKitchen {
    public enum ChefAction { ChefIdle, Walk, Dash, CarryIdle, CarryWalk, Throw, Chop, Wash, Extinguish, CustomerIdle, CustomerEat }

    // One pose source is baked into native AnimationClips and sampled for review renders.
    // Locomotion is in-place; the gameplay mover owns travel, collisions and dash distance.
    public static class CharacterMotion {
        public static readonly string[] Paths={"PreviewSkeleton/Hips","PreviewSkeleton/Hips/Chest","PreviewSkeleton/Hips/Chest/Head","PreviewSkeleton/Hips/Chest/LeftArm","PreviewSkeleton/Hips/Chest/LeftArm/LeftForearm","PreviewSkeleton/Hips/Chest/RightArm","PreviewSkeleton/Hips/Chest/RightArm/RightForearm","PreviewSkeleton/Hips/LeftLeg","PreviewSkeleton/Hips/LeftLeg/LeftFoot","PreviewSkeleton/Hips/RightLeg","PreviewSkeleton/Hips/RightLeg/RightFoot","CarryAnchor","RightToolAnchor","LeftToolAnchor"};
        public static float Duration(ChefAction a) {
            switch(a) {case ChefAction.Walk:return .8f;case ChefAction.Dash:return .6f;case ChefAction.CarryWalk:return .9f;case ChefAction.Throw:return .95f;case ChefAction.Chop:return .72f;case ChefAction.Wash:return 1.2f;case ChefAction.Extinguish:return 1.4f;case ChefAction.CustomerEat:return 2.6f;default:return 3.2f;}
        }
        public static bool Loops(ChefAction a)=>a!=ChefAction.Dash&&a!=ChefAction.Throw;
        public static Transform[] Bind(Transform root) { var t=new Transform[Paths.Length];for(int i=0;i<t.Length;i++)t[i]=root.Find(Paths[i]);return t; }
        static void Rot(Transform t,float x=0,float y=0,float z=0){t.localRotation=Quaternion.Euler(x,y,z);}
        static void Tool(Transform root,Transform socket,Vector3 local,Vector3 angles){socket.position=root.TransformPoint(local);socket.rotation=root.rotation*Quaternion.Euler(angles);}
        static void Arm(Transform root,Transform upper,Transform fore,Transform paw,Vector3 goal) {
            var target=root.TransformPoint(goal);
            // Small two-bone CCD solve keeps rounded paws on their prop contact points.
            for(int i=0;i<10;i++) {
                fore.rotation=Quaternion.FromToRotation(paw.position-fore.position,target-fore.position)*fore.rotation;
                upper.rotation=Quaternion.FromToRotation(paw.position-upper.position,target-upper.position)*upper.rotation;
            }
        }
        public static void Sample(Transform root,Transform[] b,ChefAction action,float seconds) {
            float d=Duration(action),u=Mathf.Clamp01(seconds/d),p=u*Mathf.PI*2,s=Mathf.Sin(p),c=Mathf.Cos(p),breathe=Mathf.Sin(p);
            foreach(var t in b){t.localRotation=Quaternion.identity;t.localScale=Vector3.one;}
            b[0].localPosition=new Vector3(0,.18f,0);
            b[4].localPosition=new Vector3(-.14f,-.13f,0);b[6].localPosition=new Vector3(.14f,-.13f,0);
            if(action==ChefAction.Chop||action==ChefAction.Wash){b[4].localPosition*=1.8f;b[6].localPosition*=1.8f;}
            if(action==ChefAction.CarryIdle||action==ChefAction.CarryWalk){b[4].localPosition*=1.2f;b[6].localPosition*=1.2f;}
            b[11].localPosition=new Vector3(0,.52f,.30f);b[12].localPosition=new Vector3(.16f,.62f,.25f);b[13].localPosition=new Vector3(-.16f,.62f,.25f);
            Rot(b[3],0,0,-3);Rot(b[5],0,0,3);
            bool seated=action==ChefAction.CustomerIdle||action==ChefAction.CustomerEat;
            bool carry=action==ChefAction.CarryIdle||action==ChefAction.CarryWalk;
            if(seated) {
                Rot(b[7],-67);Rot(b[9],-67);Rot(b[8],28);Rot(b[10],28);
                Rot(b[2],6+1.4f*s,4*Mathf.Sin(p),1.2f*Mathf.Sin(p));
                b[1].localScale=new Vector3(1+.004f*s,1+.007f*s,1+.004f*s);
                Rot(b[3],-30,0,12);Rot(b[5],-30,0,-12);Rot(b[4],-15);Rot(b[6],-15);
                if(action==ChefAction.CustomerEat) {
                    float bite=Mathf.Pow(Mathf.Max(0,Mathf.Sin(p)),2);
                    Rot(b[5],-35-bite*44,0,-12);Rot(b[6],-18-bite*27);Rot(b[2],6+bite*5,2*s);
                }
                return;
            }
            if(action==ChefAction.ChefIdle||action==ChefAction.CarryIdle) {
                b[0].localPosition+=Vector3.up*(.004f*breathe);
                b[1].localScale=new Vector3(1+.004f*s,1+.008f*s,1+.004f*s);
                Rot(b[2],.8f*s,3.5f*s,1.0f*Mathf.Sin(p));Rot(b[3],2*s,0,-3);Rot(b[5],-2*s,0,3);
            }
            if(action==ChefAction.Walk||action==ChefAction.CarryWalk) {
                b[0].localPosition+=new Vector3(0,.016f*(1-Mathf.Cos(p*2)),0);
                Rot(b[0],0,2*s,2*s);Rot(b[1],3,0,-1.3f*s);Rot(b[2],-2,0,-1.2f*s);
                Rot(b[7],26*s);Rot(b[9],-26*s);Rot(b[8],-9*Mathf.Max(0,s));Rot(b[10],-9*Mathf.Max(0,-s));
                Rot(b[3],-20*s,0,-3);Rot(b[5],20*s,0,3);Rot(b[4],-8);Rot(b[6],-8);
            }
            if(carry) {
                Arm(root,b[3],b[4],b[4].Find("Paw"),new Vector3(-.155f,.46f,.37f));
                Arm(root,b[5],b[6],b[6].Find("Paw"),new Vector3(.155f,.46f,.37f));
                b[11].localPosition=new Vector3(0,.442f,.37f); // Level dish, independent of gait roll.
            }
            if(action==ChefAction.Dash) {
                float burst=Mathf.Sin(u*Mathf.PI);b[0].localPosition+=Vector3.up*(-.026f*burst);
                Rot(b[1],20*burst);Rot(b[2],-11*burst);Rot(b[3],38*burst,0,-12*burst);Rot(b[5],38*burst,0,12*burst);
                Rot(b[7],Mathf.Sin(u*Mathf.PI*4)*40*burst);Rot(b[9],-Mathf.Sin(u*Mathf.PI*4)*40*burst);
            }
            if(action==ChefAction.Throw) {
                float wind=Mathf.Sin(Mathf.Clamp01(u/.38f)*Mathf.PI/2),release=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.32f,.48f,u)),recover=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.6f,1,u));
                Rot(b[1],Mathf.Lerp(-8*wind,14,release)*(1-recover),-12*wind*(1-release));Rot(b[2],4*release*(1-recover));
                var goal=Vector3.Lerp(new Vector3(.23f,.66f,-.10f),new Vector3(.12f,.69f,.29f),release);
                goal=Vector3.Lerp(goal,new Vector3(.34f,.32f,.035f),recover);
                Arm(root,b[5],b[6],b[6].Find("Paw"),goal);Tool(root,b[12],root.InverseTransformPoint(b[6].Find("Paw").position),Vector3.zero);
                Rot(b[3],-15*(1-recover),0,15*(1-recover));
            }
            if(action==ChefAction.Chop) {
                float lift=.5f+.5f*Mathf.Cos(p);Rot(b[1],5);Rot(b[2],12);
                var right=new Vector3(.04f,.715f+.09f*lift,.46f);
                Arm(root,b[5],b[6],b[6].Find("Paw"),right);Arm(root,b[3],b[4],b[4].Find("Paw"),new Vector3(-.12f,.695f,.48f));
                Tool(root,b[12],root.InverseTransformPoint(b[6].Find("Paw").position),new Vector3(-12*lift,0,-90));
            }
            if(action==ChefAction.Wash) {
                Rot(b[1],6);Rot(b[2],12,2*s);
                var right=new Vector3(.065f+.075f*s,.727f,.47f+.025f*c);
                Arm(root,b[5],b[6],b[6].Find("Paw"),right);Arm(root,b[3],b[4],b[4].Find("Paw"),new Vector3(-.13f,.70f,.48f));
                Tool(root,b[12],root.InverseTransformPoint(b[6].Find("Paw").position),new Vector3(0,12*s,0));
            }
            if(action==ChefAction.Extinguish) {
                Rot(b[1],-5,3*s);Rot(b[2],3,-2*s);Rot(b[7],-10);Rot(b[9],12);
                Arm(root,b[5],b[6],b[6].Find("Paw"),new Vector3(.13f,.72f,.25f));Arm(root,b[3],b[4],b[4].Find("Paw"),new Vector3(-.09f,.53f,.25f));
                Tool(root,b[11],new Vector3(.015f,.38f,.25f),new Vector3(0,4*s,-6));
            }
        }
    }
}
