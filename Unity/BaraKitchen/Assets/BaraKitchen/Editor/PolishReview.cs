using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using BaraKitchen;
public static partial class ArtProductionBuilder {
    static void SmoothCharacterNormals(Mesh mesh){
        var v=mesh.vertices;var triangles=mesh.triangles;var accum=new Dictionary<Vector3Int,Vector3>();var keys=new Vector3Int[v.Length];
        for(int i=0;i<v.Length;i++)keys[i]=new Vector3Int(Mathf.RoundToInt(v[i].x*100000),Mathf.RoundToInt(v[i].y*100000),Mathf.RoundToInt(v[i].z*100000));
        for(int i=0;i<triangles.Length;i+=3){int a=triangles[i],b=triangles[i+1],c=triangles[i+2];var n=Vector3.Cross(v[b]-v[a],v[c]-v[a]);foreach(int index in new[]{a,b,c}){var k=keys[index];accum.TryGetValue(k,out var old);accum[k]=old+n;}}
        var normals=mesh.normals;for(int i=0;i<v.Length;i++)if(accum.TryGetValue(keys[i],out var n)&&n.sqrMagnitude>1e-14f)normals[i]=n.normalized;mesh.normals=normals;
    }
    public static void ReviewSurfaceDetails(){
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Lighting();QualitySettings.skinWeights=SkinWeights.FourBones;QualitySettings.shadowResolution=UnityEngine.ShadowResolution.VeryHigh;
        var chef=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Base+"/Characters/Prefabs/capybara-female-chef.prefab"));chef.GetComponent<KitchenCharacterAnimator>().SampleForReview(ChefAction.ChefIdle,.7f);
        var camera=CameraAt(new Vector3(0,.7f,0),.85f);camera.transform.position=new Vector3(0,2.3f,2.8f);camera.transform.LookAt(new Vector3(0,.7f,0));
        Capture(camera,Path.Combine(GameOutput,"qa/character-shadow-study.png"));RenderSettings.sun.shadows=LightShadows.None;Capture(camera,Path.Combine(GameOutput,"qa/character-no-shadow-study.png"));
        foreach(var skin in chef.GetComponentsInChildren<SkinnedMeshRenderer>()){var mesh=Object.Instantiate(skin.sharedMesh);SmoothCharacterNormals(mesh);skin.sharedMesh=mesh;}Capture(camera,Path.Combine(GameOutput,"qa/character-smoothed-study.png"));
        foreach(var renderer in chef.GetComponentsInChildren<Renderer>()){var material=new Material(renderer.sharedMaterial);material.mainTexture=null;material.color=new Color(.9f,.75f,.55f);renderer.sharedMaterial=material;}
        Capture(camera,Path.Combine(GameOutput,"qa/character-geometry-study.png"));
    }
}
