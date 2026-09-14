using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using BaraKitchen;
using BaraKitchen.Gameplay;
public static class GameRenderReview {
    public static void Capture(){EditorSceneManager.OpenScene("Assets/BaraKitchen/Scenes/BaraKitchen_Game.unity");var g=Object.FindFirstObjectByType<KitchenGame>();var prefab=g.catalog.chefs[0].prefab;var actor=Object.Instantiate(prefab);var motion=actor.GetComponent<KitchenCharacterAnimator>();motion.SampleForReview(ChefAction.ChefIdle,.5f);var skin=actor.GetComponent<SkinnedMeshRenderer>();var baked=new Mesh();skin.BakeMesh(baked);Debug.Log("RENDER_MESH verts="+skin.sharedMesh.vertexCount+" format="+skin.sharedMesh.indexFormat+" baked="+baked.vertexCount+" bakedFormat="+baked.indexFormat+" materials="+skin.sharedMaterials.Length+" renderers="+actor.GetComponentsInChildren<Renderer>().Length);
        var cam=Camera.main;cam.rect=new Rect(0,0,1,1);cam.transform.position=new Vector3(1.5f,1.6f,2.5f);cam.transform.LookAt(new Vector3(0,.6f,0));cam.orthographicSize=.8f;var rt=new RenderTexture(600,600,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(600,600,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,600,600),0,0);tex.Apply();File.WriteAllBytes(Path.GetFullPath(Application.dataPath+"/../../../.local/native-character.png"),tex.EncodeToPNG());Debug.Log("RENDER_NATIVE_OK");}
}
