using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using BaraKitchen;

public static partial class ArtProductionBuilder {
    const string Base = "Assets/BaraKitchen";
    static string Root => Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
    static string Source => Path.Combine(Root,"art/production/round-01");
    static Dictionary<string, Material> materials = new Dictionary<string, Material>();
    static Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
    static Dictionary<string, GameObject> characters = new Dictionary<string, GameObject>();
    [Serializable] class Palette { public string name, hex; }
    [Serializable] class Catalog { public Palette[] palette; }
    [Serializable] class Part { public string material; public float[] vertices, normals; public int[] triangles; }
    [Serializable] class Anchor { public string name; public float[] position; }
    [Serializable] class Collision { public float[] center,size; }
    [Serializable] class Model { public string name, category; public Part[] parts; public Anchor[] anchors; public Collision collider; }
    [Serializable] class Instance { public string asset,name,kind; public float[] position,scale; public float yaw; public bool station,barrier; }
    [Serializable] class Seat { public string name; public float[] position,approach,dishPosition; public float yaw; }
    [Serializable] class Character { public string id,pose,seat; public float[] position; public float yaw; }
    [Serializable] class Room { public int id,cols,rows; public string name; public Instance[] instances; public Seat[] seats; public Anchor[] anchors; public Character[] characters; public float[] cameraTarget; public float cameraSize,module,cafeRows; }
    [Serializable] class Audit { public int prefabCount, characterCount, sceneCount; public string unityVersion; public string[] scenes; public string rigStatus; }
    [Serializable] class BakedCharacter { public string id; public float[] vertices,normals,uv; public int[] triangles; }
    [Serializable] class BakedCharacters { public BakedCharacter[] characters; }
    static Vector3 V(float[] a) => a == null ? Vector3.zero : new Vector3(a[0],a[1],a[2]);
    static void Folder(string path) { Directory.CreateDirectory(path); }
    static void Asset(UnityEngine.Object obj, string path) {
        var previous = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
        if(previous) { EditorUtility.CopySerialized(obj,previous); UnityEngine.Object.DestroyImmediate(obj); }
        else AssetDatabase.CreateAsset(obj,path);
    }
    static GameObject Spawn(string id,Transform parent,Vector3 position,Quaternion rotation) {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[id]);
        go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation;return go;
    }
    [MenuItem("Bara Kitchen/Build first art batch")]
    public static void BuildAll() { Build(true); }
    [MenuItem("Bara Kitchen/Rebuild rooms and materials")]
    public static void RebuildEnvironment() { Build(false); }
    static void Build(bool rebuildCharacters) {
        Folder(Base+"/Meshes");Folder(Base+"/Materials");Folder(Base+"/Prefabs");Folder(Base+"/Scenes");Folder(Base+"/Characters/Source");Folder(Base+"/Characters/Prefabs");
        AssetDatabase.Refresh();
        PlayerSettings.companyName="Bara Kitchen";PlayerSettings.productName="Bara Kitchen — Art Preview";
        PlayerSettings.colorSpace=ColorSpace.Linear;
        QualitySettings.antiAliasing=4;QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.High;QualitySettings.shadowDistance=40;
        var softShader=Shader.Find("Bara Kitchen/Soft Color");
        if(!softShader || !softShader.isSupported)throw new Exception("Soft Color shader is unavailable on this graphics device.");
        foreach(var message in ShaderUtil.GetShaderMessages(softShader)) {
            if(message.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)throw new Exception("Soft Color shader: "+message.message);
        }
        Debug.Log("BARA_SHADER_OK graphics="+SystemInfo.graphicsDeviceType+" colourSpace="+PlayerSettings.colorSpace);
        var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(Source,"model-catalog.json")));
        materials.Clear();prefabs.Clear();characters.Clear();
        foreach(var p in catalog.palette) {
            var m=new Material(Shader.Find("Bara Kitchen/Soft Color"));m.name=p.name;
            ColorUtility.TryParseHtmlString("#"+p.hex,out var color);m.color=color;
            Asset(m,Base+"/Materials/"+p.name+".mat");materials[p.name]=AssetDatabase.LoadAssetAtPath<Material>(Base+"/Materials/"+p.name+".mat");
        }
        foreach(var file in Directory.GetFiles(Path.Combine(Source,"models"),"*.mesh.json")) BuildModel(JsonUtility.FromJson<Model>(File.ReadAllText(file)));
        BuildVisualStates();
        if(rebuildCharacters)BuildCharacters();
        else foreach(var file in Directory.GetFiles(Base+"/Characters/Prefabs","*.prefab")) {
            var id=Path.GetFileNameWithoutExtension(file);
            characters[id]=AssetDatabase.LoadAssetAtPath<GameObject>(file);
            var material=AssetDatabase.LoadAssetAtPath<Material>(Base+"/Materials/"+id+".mat");
            material.shader=Shader.Find("Bara Kitchen/Soft Color");material.color=Color.white;EditorUtility.SetDirty(material);
        }
        var scenePaths=new List<string>();
        for(int i=1;i<=4;i++) { var room=JsonUtility.FromJson<Room>(File.ReadAllText(Path.Combine(Source,$"level-{i:00}.scene.json")));scenePaths.Add(BuildRoom(room)); }
        BuildStudio();
        EditorBuildSettings.scenes=scenePaths.Select(p=>new EditorBuildSettingsScene(p,true)).ToArray();
        AssetDatabase.SaveAssets();
        File.WriteAllText(Path.Combine(Source,"unity-build-audit.json"),JsonUtility.ToJson(new Audit { prefabCount=prefabs.Count,characterCount=characters.Count,sceneCount=5,unityVersion=Application.unityVersion,scenes=scenePaths.ToArray(),rigStatus="Approximate local art-review rigs; not final production animation." },true));
        EditorSceneManager.OpenScene(scenePaths[0]);
        Debug.Log($"BARA_ART_BUILD_OK props={prefabs.Count} characters={characters.Count} rooms=4 studio=1");
    }
    static void BuildModel(Model src) {
        var mesh=new Mesh{name=src.name,indexFormat=IndexFormat.UInt32};var verts=new List<Vector3>();var norms=new List<Vector3>();var sub=new List<int[]>();
        foreach(var p in src.parts) {
            int offset=verts.Count;
            for(int i=0;i<p.vertices.Length;i+=3) { verts.Add(new Vector3(p.vertices[i],p.vertices[i+1],p.vertices[i+2]));norms.Add(new Vector3(p.normals[i],p.normals[i+1],p.normals[i+2])); }
            sub.Add(p.triangles.Select(n=>n+offset).ToArray());
        }
        mesh.SetVertices(verts);mesh.SetNormals(norms);mesh.subMeshCount=sub.Count;
        for(int i=0;i<sub.Count;i++)mesh.SetTriangles(sub[i],i);mesh.RecalculateBounds();
        string meshPath=Base+"/Meshes/"+src.name+".asset";Asset(mesh,meshPath);
        var go=new GameObject(src.name);go.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        go.AddComponent<MeshRenderer>().sharedMaterials=src.parts.Select(p=>materials[p.material]).ToArray();
        if(src.collider!=null && src.collider.size!=null) { var col=go.AddComponent<BoxCollider>();col.center=V(src.collider.center);col.size=V(src.collider.size); }
        if(src.anchors!=null)foreach(var a in src.anchors) {var anchor=new GameObject(a.name);anchor.transform.SetParent(go.transform,false);anchor.transform.localPosition=V(a.position);anchor.AddComponent<SceneAnchor>().purpose=a.name;}
        Folder(Base+"/Prefabs/"+src.category);string path=Base+"/Prefabs/"+src.category+"/"+src.name+".prefab";
        prefabs[src.name]=PrefabUtility.SaveAsPrefabAsset(go,path);UnityEngine.Object.DestroyImmediate(go);
    }
    static void BuildVisualStates() {
        var sets=new Dictionary<string,string[]>();
        foreach(string ingredient in new[]{"tomato","cucumber","carrot","mushroom"})sets["Ingredient_"+ingredient]=new[]{ingredient+"-whole",ingredient+"-chopped"};
        sets["UniversalDish"]=new[]{"dish-clean","dish-tomato","dish-cucumber","dish-salad","dish-soup","dish-dirty"};
        sets["CookingPot"]=new[]{"pot-empty","pot-carrot","pot-mushroom","pot-both","pot-cooking","pot-ready","pot-burnt"};
        Folder(Base+"/Prefabs/Stateful");
        foreach(var set in sets) {
            var root=new GameObject(set.Key);var state=root.AddComponent<VisualState>();state.itemType=set.Key;state.stateNames=set.Value;state.states=new GameObject[set.Value.Length];
            for(int i=0;i<set.Value.Length;i++)state.states[i]=Spawn(set.Value[i],root.transform,Vector3.zero,Quaternion.identity);
            state.SetState(0);PrefabUtility.SaveAsPrefabAsset(root,Base+"/Prefabs/Stateful/"+set.Key+".prefab");UnityEngine.Object.DestroyImmediate(root);
        }
    }
    static void BuildCharacters() {
        foreach(var dir in Directory.GetDirectories(Path.Combine(Source,"characters"))) {
            var id=Path.GetFileName(dir);var file=Path.Combine(dir,"model/model.fbx");if(!File.Exists(file))continue;
            string target=Base+"/Characters/Source/"+id+".fbx";File.Copy(file,target,true);
            var texFile=Path.Combine(dir,"model/texture-0-base_color.png");string texPath=Base+"/Characters/Source/"+id+".png";
            if(File.Exists(texFile))File.Copy(texFile,texPath,true);
            AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport);AssetDatabase.ImportAsset(texPath,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(target) as ModelImporter;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importAnimation=false;importer.animationType=ModelImporterAnimationType.None;importer.isReadable=true;importer.SaveAndReimport();
            var texImporter=AssetImporter.GetAtPath(texPath) as TextureImporter;if(texImporter!=null){texImporter.maxTextureSize=2048;texImporter.textureCompression=TextureImporterCompression.Uncompressed;texImporter.SaveAndReimport();}
            var material=new Material(Shader.Find("Bara Kitchen/Soft Color")){name=id};material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);material.color=Color.white;
            Asset(material,Base+"/Materials/"+id+".mat");material=AssetDatabase.LoadAssetAtPath<Material>(Base+"/Materials/"+id+".mat");
            var root=new GameObject(id);var visual=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(target));visual.name="SourceVisual";visual.transform.SetParent(root.transform,false);
            var renderers=visual.GetComponentsInChildren<Renderer>();if(renderers.Length==0)throw new Exception("No renderers in "+id);
            Bounds bounds=renderers[0].bounds;foreach(var ren in renderers)bounds.Encapsulate(ren.bounds);
            float height=id.EndsWith("chef")?1.40f:1.25f;visual.transform.localScale*=height/bounds.size.y;
            bounds=renderers[0].bounds;foreach(var ren in renderers)bounds.Encapsulate(ren.bounds);visual.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            // Preserve untouched Meshy files. This local, approximate rig is for scene fitting only.
            var combine=new List<CombineInstance>();
            foreach(var filter in visual.GetComponentsInChildren<MeshFilter>()) {
                for(int sm=0;sm<filter.sharedMesh.subMeshCount;sm++) combine.Add(new CombineInstance{mesh=filter.sharedMesh,subMeshIndex=sm,transform=root.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix});
            }
            foreach(var sk in visual.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                for(int sm=0;sm<sk.sharedMesh.subMeshCount;sm++)combine.Add(new CombineInstance{mesh=sk.sharedMesh,subMeshIndex=sm,transform=root.transform.worldToLocalMatrix*sk.transform.localToWorldMatrix});
            }
            var combined=new Mesh{name=id+"_PreviewMesh",indexFormat=IndexFormat.UInt32};combined.CombineMeshes(combine.ToArray(),true,true);combined.RecalculateBounds();
            UnityEngine.Object.DestroyImmediate(visual);
            RigPreview(root,combined,material,id);
            var anchor=new GameObject("CarryAnchor");anchor.transform.SetParent(root.transform,false);anchor.transform.localPosition=new Vector3(0,.44f,.28f);anchor.AddComponent<SceneAnchor>().purpose="Held item preview position; tune with carry animation";
            characters[id]=PrefabUtility.SaveAsPrefabAsset(root,Base+"/Characters/Prefabs/"+id+".prefab");UnityEngine.Object.DestroyImmediate(root);
        }
    }
    static float Smooth(float a,float b,float v)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,v));
    static void RigPreview(GameObject root,Mesh mesh,Material mat,string id) {
        string[] names={"Hips","Chest","Head","LeftArm","LeftForearm","RightArm","RightForearm","LeftLeg","LeftFoot","RightLeg","RightFoot"};
        int[] parents={-1,0,1,1,3,1,5,0,7,0,9};
        Vector3[] positions={new Vector3(0,.18f,0),new Vector3(0,.38f,0),new Vector3(0,.56f,0),new Vector3(-.19f,.48f,0),new Vector3(-.30f,.35f,0),new Vector3(.19f,.48f,0),new Vector3(.30f,.35f,0),new Vector3(-.10f,.17f,0),new Vector3(-.10f,.06f,.02f),new Vector3(.10f,.17f,0),new Vector3(.10f,.06f,.02f)};
        var skeleton=new GameObject("PreviewSkeleton");skeleton.transform.SetParent(root.transform,false);var bones=new Transform[names.Length];
        for(int i=0;i<names.Length;i++){bones[i]=new GameObject(names[i]).transform;bones[i].SetParent(parents[i]<0?skeleton.transform:bones[parents[i]],false);bones[i].position=root.transform.TransformPoint(positions[i]);}
        var weights=new BoneWeight[mesh.vertexCount];var verts=mesh.vertices;
        for(int i=0;i<verts.Length;i++) {
            Vector3 v=verts[i];float head=Smooth(.50f,.61f,v.y);float arm=Smooth(.20f,.29f,Mathf.Abs(v.x))*(1-Smooth(.48f,.58f,v.y))*Smooth(.16f,.23f,v.y);float leg=1-Smooth(.13f,.22f,v.y);
            int a=1,b=2;float t=head;
            if(leg>.05f && head<.1f && arm<.1f){a=0;b=v.x<0?7:9;t=leg;if(v.y<.065f){a=b;b=v.x<0?8:10;t=1-Smooth(.035f,.065f,v.y);}}
            else if(arm>.05f && head<.4f){a=1;b=v.x<0?3:5;t=arm;if(Mathf.Abs(v.x)>.30f){a=b;b=v.x<0?4:6;t=Smooth(.30f,.40f,Mathf.Abs(v.x));}}
            weights[i]=new BoneWeight{boneIndex0=a,weight0=1-t,boneIndex1=b,weight1=t};
        }
        mesh.boneWeights=weights;mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*root.transform.localToWorldMatrix).ToArray();
        string meshPath=Base+"/Meshes/"+id+"_PreviewRig.asset";Asset(mesh,meshPath);
        var skinned=root.AddComponent<SkinnedMeshRenderer>();skinned.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);skinned.sharedMaterial=mat;skinned.bones=bones;skinned.rootBone=bones[0];skinned.updateWhenOffscreen=true;skinned.localBounds=new Bounds(new Vector3(0,.7f,0),new Vector3(2,2,2));
        var pose=root.AddComponent<PreviewPose>();pose.head=bones[2];pose.leftArm=bones[3];pose.leftForearm=bones[4];pose.rightArm=bones[5];pose.rightForearm=bones[6];pose.leftLeg=bones[7];pose.leftFoot=bones[8];pose.rightLeg=bones[9];pose.rightFoot=bones[10];pose.ApplyPose();
    }
    static void Lighting() {
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.85f,.91f,1);RenderSettings.ambientEquatorColor=new Color(.83f,.87f,.82f);RenderSettings.ambientGroundColor=new Color(.64f,.58f,.50f);RenderSettings.ambientIntensity=1;
        RenderSettings.skybox=null;RenderSettings.fog=false;
        var sun=new GameObject("Soft daylight").AddComponent<Light>();sun.type=LightType.Directional;sun.color=Color.white;sun.intensity=1;sun.transform.rotation=Quaternion.Euler(48,35,0);sun.shadows=LightShadows.Soft;sun.shadowStrength=.72f;sun.shadowBias=.02f;sun.shadowNormalBias=.06f;
        RenderSettings.sun=sun;
    }
    static Camera CameraAt(Vector3 target,float size) {
        var cam=new GameObject("Main Camera").AddComponent<Camera>();cam.tag="MainCamera";cam.orthographic=true;cam.orthographicSize=size;cam.nearClipPlane=.1f;cam.farClipPlane=100;
        cam.transform.position=target+new Vector3(-7.4f,14,17);cam.transform.LookAt(target);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.94f,.93f,.86f);cam.allowHDR=false;cam.allowMSAA=true;return cam;
    }
    static void FitRoomCamera(Camera cam,GameObject root) {
        // Fit projected renderer bounds, including door leaves and wall decorations.
        // Grid-only estimates clipped corners of the wider levels at a 4:3 aspect.
        cam.aspect=4f/3;
        float minX=float.PositiveInfinity,maxX=float.NegativeInfinity,minY=float.PositiveInfinity,maxY=float.NegativeInfinity;
        foreach(var renderer in root.GetComponentsInChildren<Renderer>()) {
            var b=renderer.bounds;
            for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++) {
                var p=cam.transform.InverseTransformPoint(new Vector3(x==0?b.min.x:b.max.x,y==0?b.min.y:b.max.y,z==0?b.min.z:b.max.z));
                minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);
            }
        }
        cam.transform.position+=cam.transform.right*((minX+maxX)/2)+cam.transform.up*((minY+maxY)/2);
        cam.orthographicSize=Mathf.Max((maxY-minY)/2,(maxX-minX)/(2*cam.aspect))*1.08f;
    }
    static string BuildRoom(Room room) {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Lighting();
        var root=new GameObject($"Level {room.id:00} — {room.name}");var kitchen=new GameObject("Kitchen");kitchen.transform.SetParent(root.transform);var cafe=new GameObject("Cafe and customer area");cafe.transform.SetParent(root.transform);
        foreach(var inst in room.instances) {
            bool customer=inst.position[2]>=(room.rows-.48f)*room.module;var go=Spawn(inst.asset,customer?cafe.transform:kitchen.transform,V(inst.position),Quaternion.Euler(0,inst.yaw,0));go.name=inst.name;
            if(inst.scale!=null)go.transform.localScale=V(inst.scale);
            if(inst.barrier){var marker=go.AddComponent<SceneAnchor>();marker.purpose="Blocks walking; ingredient throws may cross. No placement slot.";}
        }
        // Invisible floor collider does not alter the approved station footprints.
        var floor=new GameObject("Walkable floor");floor.transform.SetParent(root.transform);var col=floor.AddComponent<BoxCollider>();col.center=new Vector3(0,-.085f,(room.rows+room.cafeRows-1)*room.module/2);col.size=new Vector3(room.cols*room.module,.1f,(room.rows+room.cafeRows)*room.module);
        var anchors=new GameObject("Customer navigation and seats");anchors.transform.SetParent(cafe.transform);
        foreach(var a in room.anchors) { var go=new GameObject(a.name);go.transform.SetParent(anchors.transform,false);go.transform.localPosition=V(a.position);go.AddComponent<SceneAnchor>().purpose=a.name; }
        foreach(var s in room.seats) {
            var go=new GameObject(s.name);go.transform.SetParent(anchors.transform,false);go.transform.localPosition=V(s.position);go.transform.rotation=Quaternion.Euler(0,s.yaw,0);var seat=go.AddComponent<SceneAnchor>();seat.purpose="Customer seat: approach, sit, eat, leave";
            var approach=new GameObject("Approach");approach.transform.SetParent(anchors.transform,false);approach.transform.position=V(s.approach);seat.approach=approach.transform;
            var dish=new GameObject("EatingDish");dish.transform.SetParent(anchors.transform,false);dish.transform.position=V(s.dishPosition);seat.dishPosition=dish.transform;
        }
        foreach(var ch in room.characters) if(characters.ContainsKey(ch.id)) {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(characters[ch.id]);go.transform.SetParent(ch.pose=="idle"?kitchen.transform:cafe.transform,false);go.transform.localPosition=V(ch.position);go.transform.localRotation=Quaternion.Euler(0,ch.yaw,0);
            var motion=go.GetComponent<KitchenCharacterAnimator>();
            if(ch.pose=="seated")go.transform.localPosition-=Vector3.up*.18f;
            if(motion) { motion.SampleForReview(ch.pose=="seated"?ChefAction.CustomerIdle:ch.pose=="walk"?ChefAction.Walk:ChefAction.ChefIdle,.4f);motion.animator.enabled=true; }
            else { var pose=go.GetComponent<PreviewPose>();pose.seated=ch.pose=="seated";pose.walking=ch.pose=="walk";pose.animate=true;pose.ApplyPose(.4f); }
        }
        var cam=CameraAt(V(room.cameraTarget),room.cameraSize);
        FitRoomCamera(cam,root);
        ExportCharacterPoses(Path.Combine(Source,$"level-{room.id:00}.characters.json"));
        string path=Base+$"/Scenes/Level_{room.id:00}.unity";EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),path);
        Capture(cam,Path.Combine(Source,$"level-{room.id:00}-unity.png"));return path;
    }
    static void BuildStudio() {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Lighting();
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Studio floor";floor.transform.position=new Vector3(0,-.075f,1);floor.transform.localScale=new Vector3(15,.15f,10);floor.GetComponent<Renderer>().sharedMaterial=materials["ivory"];
        int j=0;foreach(var pair in characters.OrderBy(p=>p.Key)) {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(pair.Value);go.transform.position=new Vector3((j%4-1.5f)*1.35f,0,(j/4)*1.85f);j++;
        }
        var cam=CameraAt(new Vector3(0,.5f,1.4f),3.6f);cam.transform.position=new Vector3(0,4.2f,9.6f);cam.transform.LookAt(new Vector3(0,.6f,1.5f));
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),Base+"/Scenes/Character_Studio.unity");Capture(cam,Path.Combine(Source,"characters-unity.png"));
        // Separate close-up renders provide a useful modeling review at ingredient scale.
        foreach(var go in UnityEngine.Object.FindObjectsByType<PreviewPose>(FindObjectsSortMode.None))UnityEngine.Object.DestroyImmediate(go.gameObject);
        foreach(var go in UnityEngine.Object.FindObjectsByType<KitchenCharacterAnimator>(FindObjectsSortMode.None))UnityEngine.Object.DestroyImmediate(go.gameObject);
        string[] food={"tomato-whole","tomato-chopped","cucumber-whole","cucumber-chopped","carrot-whole","carrot-chopped","mushroom-whole","mushroom-chopped","dish-clean","dish-salad","dish-soup","dish-dirty"};
        for(int i=0;i<food.Length;i++)Spawn(food[i],null,new Vector3((i%4-1.5f)*.62f,0,(i/4)*.55f),Quaternion.identity);
        cam.orthographicSize=1.27f;cam.transform.position=new Vector3(1.3f,3.7f,4.7f);cam.transform.LookAt(new Vector3(0,0,.55f));Capture(cam,Path.Combine(Source,"foods-unity.png"));
    }
    static void Capture(Camera cam,string path) {
        var rt=new RenderTexture(1600,1200,24,RenderTextureFormat.ARGB32);rt.antiAliasing=4;cam.targetTexture=rt;cam.aspect=4f/3;cam.Render();RenderTexture.active=rt;
        var image=new Texture2D(1600,1200,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1200),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
        RenderTexture.active=null;cam.targetTexture=null;UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
    }
    static void ExportCharacterPoses(string path) {
        var output=new List<BakedCharacter>();
        foreach(var renderer in UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None)) {
            var mesh=new Mesh();renderer.BakeMesh(mesh);
            var verts=mesh.vertices.SelectMany(v=>{var p=renderer.transform.TransformPoint(v);return new[]{p.x,p.y,p.z};}).ToArray();
            var norms=mesh.normals.SelectMany(v=>{var p=renderer.transform.TransformDirection(v).normalized;return new[]{p.x,p.y,p.z};}).ToArray();
            output.Add(new BakedCharacter {id=renderer.name,vertices=verts,normals=norms,uv=mesh.uv.SelectMany(v=>new[]{v.x,v.y}).ToArray(),triangles=mesh.triangles});
            UnityEngine.Object.DestroyImmediate(mesh);
        }
        File.WriteAllText(path,JsonUtility.ToJson(new BakedCharacters{characters=output.ToArray()}));
    }
}
