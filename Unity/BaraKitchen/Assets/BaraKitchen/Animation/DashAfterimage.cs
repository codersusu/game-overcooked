using UnityEngine;
namespace BaraKitchen {
    public sealed class DashAfterimage : MonoBehaviour {
        Mesh mesh;
        readonly Transform[] ghosts=new Transform[3];
        readonly Material[] materials=new Material[3];
        public void Sample(SkinnedMeshRenderer skin,float time) {
            if(!mesh) {
                mesh=new Mesh(){name="Dash silhouette"};
                for(int i=0;i<3;i++) {
                    var go=new GameObject("Dash echo "+i);go.transform.SetParent(transform,false);ghosts[i]=go.transform;
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=go.AddComponent<MeshRenderer>();materials[i]=new Material(Shader.Find("Bara Kitchen/Afterimage"));
                    var slots=new Material[skin.sharedMaterials.Length];for(int j=0;j<slots.Length;j++)slots[j]=materials[i];renderer.sharedMaterials=slots;
                    renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                }
            }
            float strength=Mathf.Sin(Mathf.Clamp01((time-.06f)/.62f)*Mathf.PI);
            if(strength>0)skin.BakeMesh(mesh);
            for(int i=0;i<3;i++) {
                ghosts[i].gameObject.SetActive(strength>.02f);
                ghosts[i].position=skin.transform.position-skin.transform.forward*(.13f+i*.14f);
                ghosts[i].rotation=skin.transform.rotation;
                materials[i].color=new Color(.23f,.27f,.34f,strength*(.15f-i*.037f));
            }
        }
        void OnDestroy(){if(mesh)DestroyImmediate(mesh);foreach(var m in materials)if(m)DestroyImmediate(m);}
    }
}
