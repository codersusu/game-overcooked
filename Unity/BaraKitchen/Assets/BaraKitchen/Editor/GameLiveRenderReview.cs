using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using BaraKitchen;
using BaraKitchen.Gameplay;
[InitializeOnLoad]
public static class GameLiveRenderReview {
    static int frames;static GameLiveRenderReview(){EditorApplication.update+=Update;}
    public static void Begin(){SessionState.SetBool("Bara.LiveReview",true);EditorSceneManager.OpenScene("Assets/BaraKitchen/Scenes/BaraKitchen_Game.unity");EditorApplication.isPlaying=true;}
    static void Update(){if(!SessionState.GetBool("Bara.LiveReview",false)||!EditorApplication.isPlaying)return;var g=Object.FindFirstObjectByType<KitchenGame>();if(!g||!g.chef)return;if(++frames<50)return;SessionState.SetBool("Bara.LiveReview",false);var actor=g.chef.motion;var skin=actor.GetComponent<SkinnedMeshRenderer>();g.ui.GetComponent<UnityEngine.UIElements.UIDocument>().enabled=false;var cam=g.cam;cam.rect=new Rect(0,0,1,1);cam.transform.position=actor.transform.position+new Vector3(1.5f,1.6f,2.5f);cam.transform.LookAt(actor.transform.position+new Vector3(0,.6f,0));cam.orthographicSize=.8f;Debug.Log("LIVE_QUALITY="+QualitySettings.skinWeights+" mesh="+skin.sharedMesh.vertexCount);Capture(cam,"native-live");actor.animator.enabled=false;actor.SampleForReview(ChefAction.ChefIdle,.5f);Capture(cam,"native-sampled");var mesh=new Mesh();skin.BakeMesh(mesh);var go=new GameObject("Bake");go.transform.SetParent(actor.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;Capture(cam,"native-baked");EditorApplication.Exit(0);}
    static void Capture(Camera cam,string name){var rt=new RenderTexture(600,600,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(600,600,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,600,600),0,0);tex.Apply();File.WriteAllBytes(Path.GetFullPath(Application.dataPath+"/../../../.local/"+name+".png"),tex.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=null;Object.Destroy(rt);Object.Destroy(tex);}
}
