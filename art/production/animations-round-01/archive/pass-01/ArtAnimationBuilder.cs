using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using BaraKitchen;

public static partial class ArtProductionBuilder {
    static string MotionSource=>Path.Combine(Root,"art/production/animations-round-01");
    const string MotionBase=Base+"/Animation";
    [MenuItem("Bara Kitchen/Build animation pass")]
    public static void BuildAnimationPass() {
        RebuildEnvironment();BuildMotionAssets();BuildEffectPrefabs();
        for(int i=1;i<=4;i++)BuildRoom(JsonUtility.FromJson<Room>(File.ReadAllText(Path.Combine(Source,$"level-{i:00}.scene.json"))));
        BuildStudio();BuildAnimationReview();ValidateMotionPass();
        AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(Base+"/Scenes/Animation_Studio.unity");
        Debug.Log("BARA_ANIMATION_BUILD_OK clips=11 characterPrefabs=12 rooms=4");
    }
    public static void BuildAndRenderAnimations(){BuildAnimationPass();RenderAnimationPreviews();}
    static Transform Ensure(Transform parent,string name,Vector3 local) {
        var t=parent.Find(name);if(!t){t=new GameObject(name).transform;t.SetParent(parent,false);}t.localPosition=local;t.localRotation=Quaternion.identity;return t;
    }
    static void PrepareMotionRig(GameObject root) {
        var old=root.GetComponent<PreviewPose>();if(old){old.animate=false;old.seated=false;old.walking=false;old.ApplyPose();UnityEngine.Object.DestroyImmediate(old);}
        var anim=root.GetComponent<Animator>();if(!anim)anim=root.AddComponent<Animator>();anim.enabled=false;
        var bones=CharacterMotion.Bind(root.transform);
        // Slightly higher shoulder pivots give short connected paws useful worktop reach.
        // Mesh bind poses are rebuilt, preserving the original undeformed silhouette.
        foreach(var t in root.GetComponentsInChildren<Transform>())if(t.name!="Paw")t.localRotation=Quaternion.identity;
        bones[0].localPosition=new Vector3(0,.18f,0);bones[1].localPosition=new Vector3(0,.20f,0);
        bones[3].position=root.transform.TransformPoint(new Vector3(-.25f,.58f,0));bones[5].position=root.transform.TransformPoint(new Vector3(.25f,.58f,0));
        bones[4].position=root.transform.TransformPoint(new Vector3(-.39f,.45f,0));bones[6].position=root.transform.TransformPoint(new Vector3(.39f,.45f,0));
        Ensure(bones[4],"Paw",new Vector3(-.11f,-.095f,-.02f));Ensure(bones[6],"Paw",new Vector3(.11f,-.095f,-.02f));
        var skin=root.GetComponent<SkinnedMeshRenderer>();var mesh=UnityEngine.Object.Instantiate(skin.sharedMesh);mesh.bindposes=skin.bones.Select(b=>b.worldToLocalMatrix*root.transform.localToWorldMatrix).ToArray();
        var weights=mesh.boneWeights;var verts=mesh.vertices;
        for(int i=0;i<verts.Length;i++) {
            var v=verts[i];float arm=Smooth(.24f,.34f,Mathf.Abs(v.x))*(1-Smooth(.57f,.64f,v.y))*Smooth(.25f,.33f,v.y)*(1-Smooth(.14f,.23f,Mathf.Abs(v.z)));
            if(arm>.02f){float fore=Smooth(.35f,.45f,Mathf.Abs(v.x));weights[i]=new BoneWeight{boneIndex0=1,weight0=1-arm,boneIndex1=v.x<0?3:5,weight1=arm*(1-fore),boneIndex2=v.x<0?4:6,weight2=arm*fore};}
            else if(v.y>.50f){float head=Smooth(.55f,.64f,v.y);weights[i]=new BoneWeight{boneIndex0=1,weight0=1-head,boneIndex1=2,weight1=head};}
        }
        mesh.boneWeights=weights;
        string path=AssetDatabase.GetAssetPath(skin.sharedMesh);Asset(mesh,path);skin.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        var carry=Ensure(root.transform,"CarryAnchor",new Vector3(0,.522f,.30f));var right=Ensure(root.transform,"RightToolAnchor",new Vector3(.16f,.62f,.25f));var left=Ensure(root.transform,"LeftToolAnchor",new Vector3(-.16f,.62f,.25f));
        var motion=root.GetComponent<KitchenCharacterAnimator>();if(!motion)motion=root.AddComponent<KitchenCharacterAnimator>();motion.animator=anim;motion.carryAnchor=carry;motion.rightToolAnchor=right;motion.leftToolAnchor=left;
    }
    static void BuildMotionAssets() {
        Folder(MotionBase+"/Clips");Directory.CreateDirectory(MotionSource);
        foreach(var id in characters.Keys.ToArray()) {
            string path=Base+"/Characters/Prefabs/"+id+".prefab";var root=PrefabUtility.LoadPrefabContents(path);PrepareMotionRig(root);PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);
        }
        var sample=UnityEngine.Object.Instantiate(characters["capybara-male-chef"]);var bones=CharacterMotion.Bind(sample.transform);var clips=new Dictionary<ChefAction,AnimationClip>();
        foreach(ChefAction action in Enum.GetValues(typeof(ChefAction))) {
            float duration=CharacterMotion.Duration(action);int frames=Mathf.CeilToInt(duration*30);var values=new List<Keyframe>[bones.Length,10];
            for(int k=0;k<bones.Length;k++)for(int n=0;n<10;n++)values[k,n]=new List<Keyframe>();
            for(int f=0;f<=frames;f++) {
                float t=f*duration/frames;CharacterMotion.Sample(sample.transform,bones,action,t);
                for(int k=0;k<bones.Length;k++) {
                    var p=bones[k].localPosition;var q=bones[k].localRotation;var s=bones[k].localScale;float[] v={p.x,p.y,p.z,q.x,q.y,q.z,q.w,s.x,s.y,s.z};
                    for(int n=0;n<10;n++)values[k,n].Add(new Keyframe(t,v[n]));
                }
            }
            var clip=new AnimationClip{name=action.ToString(),frameRate=30};string[] properties={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w","m_LocalScale.x","m_LocalScale.y","m_LocalScale.z"};
            for(int k=0;k<bones.Length;k++)for(int n=0;n<10;n++) {
                var curve=new AnimationCurve(values[k,n].ToArray());for(int j=0;j<curve.length;j++){AnimationUtility.SetKeyLeftTangentMode(curve,j,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,j,AnimationUtility.TangentMode.Linear);}
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(CharacterMotion.Paths[k],typeof(Transform),properties[n]),curve);
            }
            clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=CharacterMotion.Loops(action);AnimationUtility.SetAnimationClipSettings(clip,settings);
            var events=new List<AnimationEvent>();
            void Mark(float phase,string label){events.Add(new AnimationEvent{time=duration*phase,functionName="OnAnimationMarker",stringParameter=label});}
            if(action==ChefAction.Throw)Mark(.40f,"ThrowRelease");if(action==ChefAction.Chop)Mark(.50f,"ChopImpact");if(action==ChefAction.Wash){Mark(.25f,"WashStroke");Mark(.75f,"WashStroke");}if(action==ChefAction.Dash){Mark(.02f,"DashStart");Mark(.96f,"DashEnd");}
            AnimationUtility.SetAnimationEvents(clip,events.ToArray());string path=MotionBase+"/Clips/"+action+".anim";Asset(clip,path);clips[action]=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }
        UnityEngine.Object.DestroyImmediate(sample);
        string controllerPath=MotionBase+"/Character.controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine=controller.layers[0].stateMachine;foreach(var old in machine.states)machine.RemoveState(old.state);
        var states=new Dictionary<ChefAction,AnimatorState>();int i=0;
        foreach(var pair in clips){var st=machine.AddState(pair.Key.ToString(),new Vector3((i%3)*220,(i/3)*70));st.motion=pair.Value;st.writeDefaultValues=true;states[pair.Key]=st;i++;}
        machine.defaultState=states[ChefAction.ChefIdle];
        foreach(var action in new[]{ChefAction.Dash,ChefAction.Throw}){var t=states[action].AddTransition(states[ChefAction.ChefIdle]);t.hasExitTime=true;t.exitTime=1;t.duration=.10f;t.hasFixedDuration=true;}
        EditorUtility.SetDirty(controller);
        foreach(var id in characters.Keys.ToArray()) {
            string path=Base+"/Characters/Prefabs/"+id+".prefab";var root=PrefabUtility.LoadPrefabContents(path);var motion=root.GetComponent<KitchenCharacterAnimator>();motion.animator.runtimeAnimatorController=controller;motion.animator.applyRootMotion=false;motion.animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;motion.SampleForReview(ChefAction.ChefIdle,0);motion.animator.enabled=true;PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);characters[id]=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        AssetDatabase.SaveAssets();
    }
    static Material EffectMaterial(string name,Color color) {
        var material=new Material(Shader.Find("Bara Kitchen/Soft Color")){name=name,color=color};material.SetFloat("_ShadeFloor",.82f);Asset(material,Base+"/Materials/"+name+".mat");return AssetDatabase.LoadAssetAtPath<Material>(Base+"/Materials/"+name+".mat");
    }
    static Transform EffectPart(string source,string name,Transform root,Material material) {
        var go=Spawn(source,root,Vector3.zero,Quaternion.identity);go.name=name;go.GetComponent<Renderer>().sharedMaterial=material;go.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return go.transform;
    }
    static void BuildEffectPrefabs() {
        Folder(Base+"/Prefabs/Effects");var amber=EffectMaterial("FireOrange",new Color(1,.25f,.025f));var gold=EffectMaterial("FireGold",new Color(1,.82f,.12f));var smoke=EffectMaterial("SoftSmoke",new Color(.74f,.77f,.75f));var foam=EffectMaterial("ExtinguisherFoam",new Color(.95f,.99f,1));
        var root=new GameObject("CookingFireVisual");var v=root.AddComponent<KitchenFireVisual>();v.flames=new Transform[5];v.cores=new Transform[5];v.smoke=new Transform[5];v.embers=new Transform[6];
        for(int i=0;i<5;i++){v.flames[i]=EffectPart("flame-lobe","Flame_"+i,root.transform,amber);v.cores[i]=EffectPart("flame-lobe","Core_"+i,root.transform,gold);v.smoke[i]=EffectPart("foam-puff","Smoke_"+i,root.transform,smoke);}
        for(int i=0;i<6;i++)v.embers[i]=EffectPart("foam-puff","Ember_"+i,root.transform,gold);
        var warning=new GameObject("Overcooking warning");warning.transform.SetParent(root.transform,false);warning.transform.localPosition=new Vector3(0,.7f,0);var text=warning.AddComponent<TextMesh>();text.text="!";text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");warning.GetComponent<Renderer>().sharedMaterial=text.font.material;text.fontSize=60;text.characterSize=.08f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(1,.3f,.06f);warning.transform.localRotation=Quaternion.Euler(0,180,0);v.warning=warning.transform;v.Sample(0,HeatState.Empty);
        prefabs["CookingFireVisual"]=PrefabUtility.SaveAsPrefabAsset(root,Base+"/Prefabs/Effects/CookingFireVisual.prefab");UnityEngine.Object.DestroyImmediate(root);
        root=new GameObject("ExtinguisherSpray");var spray=root.AddComponent<ExtinguisherSpray>();spray.nozzle=Ensure(root.transform,"Nozzle",Vector3.zero);spray.puffs=new Transform[18];for(int i=0;i<18;i++)spray.puffs[i]=EffectPart("foam-puff","Foam_"+i,root.transform,foam);spray.Sample(0,false);
        prefabs["ExtinguisherSpray"]=PrefabUtility.SaveAsPrefabAsset(root,Base+"/Prefabs/Effects/ExtinguisherSpray.prefab");UnityEngine.Object.DestroyImmediate(root);
    }
}
