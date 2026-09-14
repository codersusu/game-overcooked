using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BaraKitchen;

// Runs in actual Play Mode, on separate rendered frames, with the normal GPU
// skinned renderer and Animator. This catches failures hidden by offline baking.
[InitializeOnLoad]
public static class NativeAnimationCheck {
    const string Key="BaraNativeMotionCheck";
    static AnimationReviewStage stage;
    static int mode,shot;
    static float started;
    static Vector3 firstPaw;
    static readonly string Root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
    static NativeAnimationCheck(){EditorApplication.update+=Tick;}
    public static void Begin() {
        EditorSceneManager.OpenScene("Assets/BaraKitchen/Scenes/Animation_Studio.unity");
        SessionState.SetBool(Key,true);SessionState.SetFloat(Key+"Deadline",(float)EditorApplication.timeSinceStartup+90);
        EditorApplication.EnterPlaymode();
    }
    static void Capture(string id) {
        var cam=Camera.main;var rt=new RenderTexture(720,720,24){antiAliasing=4};var image=new Texture2D(720,720,TextureFormat.RGB24,false);
        cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,720,720),0,0);image.Apply();
        string dir=Path.Combine(Root,"art/production/animations-round-01/qa/native");Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,id+".png"),image.EncodeToPNG());
        cam.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
    }
    static void Tick() {
        if(!SessionState.GetBool(Key,false))return;
        try {
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+"Deadline",0))throw new Exception("Native animation check timed out");
            if(!EditorApplication.isPlaying)return;
            if(!stage){stage=UnityEngine.Object.FindAnyObjectByType<AnimationReviewStage>();started=Time.time;return;}
            if(mode==0){if(Time.time-started<.25f)return;stage.autoplay=false;stage.SetScenario("Walk","capybara-male-chef");stage.actor.Play(ChefAction.Walk,0);mode=1;shot=0;return;}
            if(mode==1||mode==2) {
                string name=mode==1?"Walk":"ChefIdle";var info=stage.actor.animator.GetCurrentAnimatorStateInfo(0);if(!info.IsName(name))return;
                float phase=Mathf.Repeat(info.normalizedTime,1);
                bool moment=mode==1?(shot==0?phase>.20f&&phase<.40f:phase>.70f&&phase<.90f):(shot==0?phase<.15f:phase>.47f&&phase<.63f);
                if(!moment)return;
                var paw=CharacterMotion.Bind(stage.actor.transform)[6].Find("Paw").position;
                Capture(name+"-"+shot);
                if(shot==0){firstPaw=paw;shot=1;return;}
                if(Vector3.Distance(firstPaw,paw)<.075f)throw new Exception(name+" native paw remained static");
                Debug.Log("BARA_NATIVE_MOTION "+name+" pawTravel="+Vector3.Distance(firstPaw,paw));
                if(mode==1){stage.SetScenario("ChefIdle","capybara-male-chef");stage.actor.Play(ChefAction.ChefIdle,0);mode=2;shot=0;return;}
                stage.SetScenario("Overcook","capybara-male-chef");stage.autoplay=true;mode=3;shot=0;return;
            }
            if(mode==3) {
                if(shot==0&&stage.playhead>3.1f){if(stage.hazard.state!=HeatState.Warning)throw new Exception("Native warning phase skipped");Capture("Warning");shot=1;}
                if(shot==1&&stage.playhead>4.9f){if(stage.hazard.state!=HeatState.Burning||stage.hazard.visual.flameParticles.particleCount==0)throw new Exception("Native particles missing");Capture("Burning");
                    string result="{\"passed\":true,\"mode\":\"Unity Play Mode, native Animator and GPU skinning\",\"checks\":[\"Walking paw movement\",\"Sideways idle paw movement\",\"Warning phase before fire\",\"Live native particle rendering\"]}";
                    File.WriteAllText(Path.Combine(Root,"art/production/animations-round-01/native-playmode-validation.json"),result);SessionState.SetBool(Key,false);Debug.Log("BARA_NATIVE_PLAYMODE_OK");EditorApplication.Exit(0);}
            }
        }catch(Exception e){SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
