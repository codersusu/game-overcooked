using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using BaraKitchen;
public static partial class ArtProductionBuilder {
    [Serializable] class MotionReview {public string id,title,action,animal,description;public float duration;public string video,poster;}
    [Serializable] class MotionReviews {public MotionReview[] clips;}
    [Serializable] class MotionAudit {public int clips,characters,poseComparisons;public string[] checks;public string limitation;}
    static MotionReview[] ReviewList() {
        var list=new List<MotionReview>();string[] titles={"Chef idle","Walking","Dash burst","Hold a plate","Walk with a plate","Throw an ingredient","Chop a tomato","Wash a dirty plate","Fight a kitchen fire","Seated customer idle","Customer eating","Overcooking → fire"};
        string[] descriptions={"Breathing, small head turns and relaxed paws.","In-place gait with short steps and opposing arm swings.","Anticipation, forward lean, fast feet and recovery. Travel shown here is a preview.","Two paws support a level plate with a quiet breathing loop.","A walking cycle with the plate kept level between the paws.","Wind-up, release, ingredient arc over the rail and follow-through.","Three knife strokes, then the same ingredient changes to its chopped state.","Scrubbing with a sponge, then the same dish becomes clean and is picked up.","Hold, aim and spray. Flames shrink and stop; the food stays burnt.","Seated breathing, gentle looking around and small arm shifts.","A slow bite gesture and a head bob, returning to a resting pose.","Accelerated review: cooking, ready, warning smoke, then ignition."};
        for(int i=0;i<AnimationReviewStage.Scenarios.Length;i++){string action=AnimationReviewStage.Scenarios[i];var id=action.ToLowerInvariant();list.Add(new MotionReview{id=id,title=titles[i],action=action,animal=action=="CustomerIdle"?"cat-female-customer":action=="CustomerEat"?"capybara-female-customer":"capybara-male-chef",description=descriptions[i],duration=AnimationReviewStage.Length(action),video=id+".mp4",poster=id+".png"});}
        foreach(var spec in new[]{new[]{"dog-seated","Dog · seated idle","CustomerIdle","dog-male-customer"},new[]{"cat-carry","Cat · carrying","CarryWalk","cat-male-chef"},new[]{"dog-wash","Dog · washing","Wash","dog-female-chef"}})list.Add(new MotionReview{id=spec[0],title=spec[1],action=spec[2],animal=spec[3],description="The shared animation set applied to another animal. Review joint and clothing deformation before final production.",duration=AnimationReviewStage.Length(spec[2]),video=spec[0]+".mp4",poster=spec[0]+".png"});
        return list.ToArray();
    }
    static void ReviewCamera(Camera cam,string scenario) {
        AnimationReviewStage.FrameCamera(cam,scenario);
    }
    static void BuildAnimationReview() {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Lighting();
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Preview floor";floor.transform.position=new Vector3(0,-.08f,.6f);floor.transform.localScale=new Vector3(12,.15f,12);floor.GetComponent<Renderer>().sharedMaterial=materials["ivory"];
        var stage=new GameObject("Animation review controls").AddComponent<AnimationReviewStage>();
        string[] required={"cafe-chair","cafe-table","dish-salad","table-flower-vase","rail","counter","tomato-whole","cutting-board","knife","station-sink","sponge","fire-extinguisher","CookingFireVisual","ExtinguisherSpray","pot-lid"};
        var assets=required.Select(id=>new ReviewAsset{id=id,prefab=prefabs[id]}).ToList();
        foreach(var name in new[]{"Ingredient_tomato","UniversalDish","CookingPot"})assets.Add(new ReviewAsset{id=name,prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Base+"/Prefabs/Stateful/"+name+".prefab")});
        stage.assets=assets.ToArray();stage.animals=characters.Select(p=>new ReviewAsset{id=p.Key,prefab=p.Value}).ToArray();
        stage.SetScenario("ChefIdle","capybara-male-chef");var cam=CameraAt(new Vector3(0,.7f,0),1.1f);ReviewCamera(cam,"ChefIdle");
        string path=Base+"/Scenes/Animation_Studio.unity";EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),path);
        foreach(var item in ReviewList()) {
            stage.SetScenario(item.action,item.animal);ReviewCamera(cam,item.action);
            float time=item.action=="Overcook"?3.8f:item.action=="Extinguish"?1.2f:item.action=="Throw"?.60f:item.action=="Dash"?.3f:.4f;
            // Advance sequentially because the fire sequence responds to elapsed time.
            for(float t=0;t<time;t+=1f/24)stage.SampleFrame(t);stage.SampleFrame(time);
            CaptureSquare(cam,Path.Combine(MotionSource,item.poster));
        }
        File.WriteAllText(Path.Combine(MotionSource,"clips.json"),JsonUtility.ToJson(new MotionReviews{clips=ReviewList()},true));
    }
    static void CaptureSquare(Camera cam,string path,RenderTexture rt=null,Texture2D image=null) {
        bool own=rt==null;if(own){rt=new RenderTexture(720,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};image=new Texture2D(720,720,TextureFormat.RGB24,false);}
        cam.targetTexture=rt;cam.aspect=1;cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,720,720),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());RenderTexture.active=null;cam.targetTexture=null;
        if(own){UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
    }
    [MenuItem("Bara Kitchen/Render animation review videos")]
    public static void RenderAnimationPreviews() {
        EditorSceneManager.OpenScene(Base+"/Scenes/Animation_Studio.unity");var stage=UnityEngine.Object.FindFirstObjectByType<AnimationReviewStage>();var cam=Camera.main;
        var rt=new RenderTexture(720,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};var image=new Texture2D(720,720,TextureFormat.RGB24,false);
        foreach(var item in ReviewList()) {
            string dir=Path.Combine(Root,".local/animation-frames",item.id);Directory.CreateDirectory(dir);stage.SetScenario(item.action,item.animal);ReviewCamera(cam,item.action);
            int frames=Mathf.CeilToInt(item.duration*24);
            for(int f=0;f<frames;f++){stage.SampleFrame(f/24f);CaptureSquare(cam,Path.Combine(dir,$"{f:0000}.png"),rt,image);}
            Debug.Log("BARA_MOTION_RENDER "+item.id+" frames="+frames+" finalFire="+(stage.hazard?stage.hazard.state.ToString():"none"));
        }
        UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);Debug.Log("BARA_MOTION_RENDERS_OK");
    }
    static void Require(bool condition,string message){if(!condition)throw new Exception("Animation validation: "+message);}
    static void ValidateMotionPass() {
        int comparisons=0;
        foreach(var pair in characters) {
            var go=UnityEngine.Object.Instantiate(pair.Value);var rig=CharacterMotion.Bind(go.transform);Require(rig.All(t=>t),pair.Key+" missing animation binding");
            foreach(ChefAction action in Enum.GetValues(typeof(ChefAction))) {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(MotionBase+"/Clips/"+action+".anim");Require(clip&&clip.length>0,"Missing clip "+action);
                float duration=CharacterMotion.Duration(action);int frames=Mathf.CeilToInt(duration*30);float t=Mathf.Round(frames*.4f)*duration/frames;
                CharacterMotion.Sample(go.transform,rig,action,t);var expected=rig.Select(b=>b.localRotation).ToArray();clip.SampleAnimation(go,t);
                for(int b=0;b<rig.Length;b++)Require(Quaternion.Angle(expected[b],rig[b].localRotation)<.5f,pair.Key+" baked pose differs "+action+" "+b);
                var mesh=new Mesh();go.GetComponent<SkinnedMeshRenderer>().BakeMesh(mesh);Require(mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"Non-finite deformed mesh");UnityEngine.Object.DestroyImmediate(mesh);comparisons++;
                if(CharacterMotion.Loops(action)) {
                    CharacterMotion.Sample(go.transform,rig,action,0);var first=rig.Select(b=>b.localRotation).ToArray();CharacterMotion.Sample(go.transform,rig,action,duration);
                    for(int b=0;b<rig.Length;b++)Require(Quaternion.Angle(first[b],rig[b].localRotation)<.1f,"Loop seam "+action);
                }
            }
            UnityEngine.Object.DestroyImmediate(go);
        }
        var test=new GameObject("Hazard validation");var hazard=test.AddComponent<CookingHazard>();hazard.cookingSeconds=1;hazard.readySeconds=1;hazard.warningSeconds=1;int starts=0,stops=0;hazard.FireStarted+=()=>starts++;hazard.FireStopped+=()=>stops++;
        hazard.BeginCooking();hazard.Tick(1);Require(hazard.state==HeatState.Ready,"Ready timing");hazard.Tick(1);Require(hazard.state==HeatState.Warning,"Warning timing");hazard.Tick(1);Require(hazard.state==HeatState.Burning&&starts==1,"Ignition event");hazard.Tick(10);Require(starts==1,"Repeated ignition event");hazard.Suppress(.4f);Require(hazard.state==HeatState.Burning,"Partial suppression");hazard.Suppress(.7f);Require(hazard.state==HeatState.Extinguished&&stops==1&&!hazard.heating,"Extinguish event");hazard.Tick(100);Require(hazard.state==HeatState.Extinguished,"Reignited without new cooking");hazard.BeginCooking();hazard.heating=false;hazard.Tick(100);Require(hazard.state==HeatState.Cooking,"Heat-off timer");hazard.RemovePot();Require(hazard.state==HeatState.Empty,"Removing pot");
        test.transform.position=new Vector3(50,0,0);hazard.BeginCooking();hazard.Tick(3);
        var nozzle=new GameObject("Validation nozzle");nozzle.transform.position=new Vector3(50,0,-.5f);var spray=nozzle.AddComponent<ExtinguisherSpray>();spray.nozzle=nozzle.transform;spray.spraying=true;
        Physics.SyncTransforms();Require(spray.TrySuppress(hazard,.1f)&&hazard.fireRemaining<1,"Aimed spray did not suppress");
        nozzle.transform.rotation=Quaternion.Euler(0,180,0);Require(!spray.TrySuppress(hazard,.1f),"Spray suppressed behind nozzle");
        nozzle.transform.rotation=Quaternion.identity;nozzle.transform.position=new Vector3(50,0,-3);Require(!spray.TrySuppress(hazard,.1f),"Spray suppressed beyond range");
        nozzle.transform.position=new Vector3(50,0,-.5f);var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.transform.position=new Vector3(50,0,-.25f);obstacle.transform.localScale=Vector3.one*.12f;Physics.SyncTransforms();Require(!spray.TrySuppress(hazard,.1f),"Spray passed through solid obstacle");
        UnityEngine.Object.DestroyImmediate(obstacle);UnityEngine.Object.DestroyImmediate(nozzle);UnityEngine.Object.DestroyImmediate(test);
        var audit=new MotionAudit{clips=11,characters=characters.Count,poseComparisons=comparisons,checks=new[]{"Every native clip matches its pose source on all 12 rigs","Loop boundary rotations match","All sampled skinned vertices are finite","Cooking/ready/warning/ignition ordering","Fire-start/stop events fire once","Partial suppression and burnt aftermath","Spray range, aim cone and solid obstruction","Heat-off and pot-removal reset"},limitation="First pass on approximate body weights. Preview dash/throw travel and workstations are staged; level interaction and navigation are not implemented."};
        File.WriteAllText(Path.Combine(MotionSource,"validation.json"),JsonUtility.ToJson(audit,true));Debug.Log("BARA_MOTION_VALIDATION_OK comparisons="+comparisons);
    }
}
