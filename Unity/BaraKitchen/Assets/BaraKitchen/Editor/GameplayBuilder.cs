using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using BaraKitchen;
using BaraKitchen.Gameplay;
public static partial class ArtProductionBuilder {
    const string GameBase=Base+"/Gameplay";
    static string GameOutput=>Path.Combine(Root,"art/production/gameplay-round-01");
    [MenuItem("Bara Kitchen/Build playable game")]
    public static void BuildGameplay(){
        int originalQuality=QualitySettings.GetQualityLevel();for(int quality=0;quality<QualitySettings.names.Length;quality++){QualitySettings.SetQualityLevel(quality);QualitySettings.antiAliasing=4;QualitySettings.skinWeights=SkinWeights.FourBones;QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.VeryHigh;QualitySettings.shadowDistance=35;QualitySettings.shadowCascades=4;}QualitySettings.SetQualityLevel(originalQuality);
        Directory.CreateDirectory(GameBase+"/Generated");Directory.CreateDirectory(GameBase+"/Icons");Directory.CreateDirectory(GameOutput);AssetDatabase.Refresh();
        var catalog=AssetDatabase.LoadAssetAtPath<GameCatalog>(GameBase+"/Generated/Catalog.asset");if(!catalog){catalog=ScriptableObject.CreateInstance<GameCatalog>();AssetDatabase.CreateAsset(catalog,GameBase+"/Generated/Catalog.asset");}catalog.name="Bara Kitchen game catalog";catalog.font=AssetDatabase.LoadAssetAtPath<Font>(GameBase+"/UI/Fonts/VarelaRound.ttf");catalog.titleFont=AssetDatabase.LoadAssetAtPath<Font>(GameBase+"/UI/Fonts/LilitaOne.ttf");catalog.dashShader=Shader.Find("Bara Kitchen/Afterimage");catalog.highlightShader=Shader.Find("Bara Kitchen/Station Glow");
        catalog.audio=Directory.GetFiles(GameBase+"/Audio","*.wav").OrderBy(p=>p).Select(p=>{var im=(AudioImporter)AssetImporter.GetAtPath(p);im.forceToMono=!Path.GetFileName(p).StartsWith("music-");var settings=im.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.75f;settings.preloadAudioData=true;im.defaultSampleSettings=settings;im.SaveAndReimport();return new SoundAsset{id=Path.GetFileNameWithoutExtension(p),clip=AssetDatabase.LoadAssetAtPath<AudioClip>(p)};}).ToArray();
        var panel=ScriptableObject.CreateInstance<PanelSettings>();panel.scaleMode=PanelScaleMode.ScaleWithScreenSize;panel.referenceResolution=new Vector2Int(1280,900);panel.screenMatchMode=PanelScreenMatchMode.MatchWidthOrHeight;panel.match=.5f;Asset(panel,GameBase+"/Generated/Panel.asset");catalog.panel=AssetDatabase.LoadAssetAtPath<PanelSettings>(GameBase+"/Generated/Panel.asset");
        // Default runtime controls need Unity's theme in addition to the game's own stylesheet.
        string themePath=GameBase+"/UI/Default.tss";File.WriteAllText(themePath,"@import url(\"unity-theme://default\");\n");AssetDatabase.ImportAsset(themePath,ImportAssetOptions.ForceSynchronousImport);catalog.panel.themeStyleSheet=AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath);
        prefabs.Clear();foreach(string path in Directory.GetFiles(Base+"/Prefabs","*.prefab",SearchOption.AllDirectories))prefabs[Path.GetFileNameWithoutExtension(path)]=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        catalog.assets=prefabs.Select(p=>new GameAsset{id=p.Key,prefab=p.Value}).ToArray();
        var characterPaths=Directory.GetFiles(Base+"/Characters/Prefabs","*.prefab").OrderBy(p=>p).ToArray();
        catalog.chefs=characterPaths.Where(p=>p.Contains("-chef.")).Select(p=>new GameAsset{id=Path.GetFileNameWithoutExtension(p),prefab=AssetDatabase.LoadAssetAtPath<GameObject>(p)}).ToArray();
        catalog.customers=characterPaths.Where(p=>p.Contains("-customer.")).Select(p=>new GameAsset{id=Path.GetFileNameWithoutExtension(p),prefab=AssetDatabase.LoadAssetAtPath<GameObject>(p)}).ToArray();
        foreach(var path in Directory.GetFiles(Base+"/Characters/Source","*.png")){var im=(TextureImporter)AssetImporter.GetAtPath(path);im.filterMode=FilterMode.Trilinear;im.anisoLevel=4;im.mipmapEnabled=true;im.SaveAndReimport();}
        catalog.levels=new LevelDefinition[4];
        for(int i=1;i<=4;i++){
            var src=JsonUtility.FromJson<Room>(File.ReadAllText(Path.Combine(Source,$"level-{i:00}.scene.json")));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var root=new GameObject("Playable kitchen "+i);
            foreach(var inst in src.instances){if(inst.name.StartsWith("DiningDish_"))continue;string asset=inst.station&&(inst.kind=="pot"||inst.kind=="dishes")?"counter":inst.asset;var go=Spawn(asset,root.transform,V(inst.position),Quaternion.Euler(0,inst.yaw,0));go.name=inst.name;if(inst.scale!=null)go.transform.localScale=V(inst.scale);
                if(inst.station){var station=go.AddComponent<KitchenStation>();station.id=inst.name;station.kind=inst.kind;station.slotOffset=new Vector3(0,.47f/go.transform.localScale.y,0);station.highlight=Highlight(go.transform,.47f/go.transform.localScale.y);}
            }
            var floor=new GameObject("Collision floor");floor.transform.SetParent(root.transform);var fc=floor.AddComponent<BoxCollider>();fc.center=new Vector3(0,-.075f,(src.rows+src.cafeRows-1)*src.module/2);fc.size=new Vector3(src.cols*src.module,.15f,(src.rows+src.cafeRows)*src.module);
            // Bound the chef to the kitchen; the adjacent cafe remains visible for guests.
            float width=src.cols*src.module;float depth=src.rows*src.module;foreach(int side in new[]{-1,1}){var wall=new GameObject("Kitchen side boundary");wall.transform.SetParent(root.transform);wall.transform.position=new Vector3(side*width/2,0,(depth-src.module)/2);var c=wall.AddComponent<BoxCollider>();c.center=Vector3.up;c.size=new Vector3(.10f,2,depth+.4f);}
            foreach(float z in new[]{-src.module/2,(src.rows-.5f)*src.module}){var wall=new GameObject("Kitchen end boundary");wall.transform.SetParent(root.transform);wall.transform.position=new Vector3(0,0,z);var c=wall.AddComponent<BoxCollider>();c.center=Vector3.up;c.size=new Vector3(width,2,.10f);}
            foreach(var a in src.anchors){var go=new GameObject(a.name);go.transform.SetParent(root.transform);go.transform.position=V(a.position);go.AddComponent<SceneAnchor>().purpose=a.name;}
            foreach(var s in src.seats){var go=new GameObject(s.name);go.transform.SetParent(root.transform);go.transform.position=V(s.position);go.transform.rotation=Quaternion.Euler(0,s.yaw,0);var anchor=go.AddComponent<SceneAnchor>();anchor.purpose="Reserved customer seat";anchor.approach=new GameObject("Approach").transform;anchor.approach.SetParent(root.transform);anchor.approach.position=V(s.approach);anchor.dishPosition=new GameObject("EatingDish").transform;anchor.dishPosition.SetParent(root.transform);anchor.dishPosition.position=V(s.dishPosition);}
            var bin=root.GetComponentsInChildren<KitchenStation>().First(s=>s.kind=="bin");var stand=new GameObject("Extinguisher stand");stand.transform.SetParent(root.transform);stand.transform.position=bin.transform.position+bin.transform.forward*.85f;var fire=stand.AddComponent<KitchenStation>();fire.id="extinguisher";fire.kind="extinguisher";fire.slotOffset=new Vector3(0,.07f,0);fire.highlight=Highlight(stand.transform,.04f);
            string roomPath=GameBase+$"/Generated/Kitchen_{i:00}.prefab";PrefabUtility.SaveAsPrefabAsset(root,roomPath);
            string[] subtitles={"An open kitchen for your first salad","Find your rhythm around the island","Prepare, throw, then cross the divide","Plan a batch through three work areas"};
            string[] lessons={"Pick up, chop and combine tomato + cucumber on any free worktop.","Cook carrot + mushroom soup. Your first delivery introduces dash.","Both recipes return. Q throws ingredients over the rail onto empty counters.","Use both throws and dash to move small batches through the zigzag kitchen."};
            var durations=new[]{180,210,240,270};var patience=new[]{100,125,145,180};var dishes=new[]{3,3,4,5};
            catalog.levels[i-1]=new LevelDefinition{id=i,rows=src.rows,cols=src.cols,title=src.name,subtitle=subtitles[i-1],lesson=lessons[i-1],duration=durations[i-1],patience=patience[i-1],dishes=dishes[i-1],ticketCap=i==4?3:2,stars=i<3?new[]{100,250,430}:new[]{150,350,580},recipes=i==1?new[]{"salad"}:i==2?new[]{"soup"}:new[]{"salad","soup"},room=AssetDatabase.LoadAssetAtPath<GameObject>(roomPath),spawn=V(src.characters[0].position),cameraTarget=new Vector3(0,.25f,(src.rows+src.cafeRows-1)*src.module*.50f),cameraSize=src.cameraSize,module=src.module};
            string thumb=GameBase+$"/Icons/level-{i:00}.png";File.Copy(Path.Combine(Source,$"level-{i:00}-unity.png"),thumb,true);ImportIcon(thumb,512);catalog.levels[i-1].thumbnail=AssetDatabase.LoadAssetAtPath<Texture2D>(thumb);
        }
        BuildGameIcons(catalog);
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Lighting();var camera=CameraAt(catalog.levels[0].cameraTarget,catalog.levels[0].cameraSize);camera.gameObject.AddComponent<AudioListener>();
        var system=new GameObject("Bara Kitchen Game");var game=system.AddComponent<KitchenGame>();game.catalog=catalog;var ui=system.AddComponent<KitchenHUD>();ui.theme=AssetDatabase.LoadAssetAtPath<StyleSheet>(GameBase+"/UI/Kitchen.uss");game.ui=ui;
        var doc=system.AddComponent<UIDocument>();doc.panelSettings=catalog.panel;
        string scene=Base+"/Scenes/BaraKitchen_Game.unity";EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),scene);EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(scene,true)};
        PlayerSettings.companyName="Bara Kitchen";PlayerSettings.productName="Bara Kitchen";PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=900;PlayerSettings.runInBackground=true;PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled;PlayerSettings.WebGL.dataCaching=true;PlayerSettings.WebGL.initialMemorySize=256;PlayerSettings.WebGL.maximumMemorySize=1024;PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL,ManagedStrippingLevel.Low);
        AssetDatabase.SaveAssets();File.WriteAllText(Path.Combine(GameOutput,"build-manifest.json"),JsonUtility.ToJson(new GameManifest{levels=4,chefVariants=catalog.chefs.Length,customerVariants=catalog.customers.Length,recipes=2,scene=scene},true));Debug.Log("BARA_GAME_BUILD_OK levels=4 chefs="+catalog.chefs.Length+" customers="+catalog.customers.Length);
    }
    [Serializable] class GameManifest {public int levels,chefVariants,customerVariants,recipes;public string scene;}
    static GameObject Highlight(Transform parent,float y){
        var go=GameObject.CreatePrimitive(PrimitiveType.Quad);go.name="Animated worktop outline";UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(0,y+.013f,0);go.transform.localRotation=Quaternion.Euler(90,0,0);go.transform.localScale=Vector3.one*1.02f;
        string path=GameBase+"/Generated/Glow.mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Bara Kitchen/Station Glow"));AssetDatabase.CreateAsset(m,path);}else{m.shader=Shader.Find("Bara Kitchen/Station Glow");EditorUtility.SetDirty(m);}go.GetComponent<Renderer>().sharedMaterial=m;go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;go.GetComponent<Renderer>().receiveShadows=false;go.AddComponent<StationHighlight>();go.SetActive(false);return go;
    }
    static void ImportIcon(string path,int max){AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var im=(TextureImporter)AssetImporter.GetAtPath(path);im.textureType=TextureImporterType.Default;im.alphaIsTransparency=true;im.maxTextureSize=max;im.textureCompression=TextureImporterCompression.Uncompressed;im.SaveAndReimport();}
    public static void BuildPotCleanupIcons(){
        var ids=new[]{"pot-empty","pot-burnt"};
        BuildGameIcons(AssetDatabase.LoadAssetAtPath<GameCatalog>(GameBase+"/Generated/Catalog.asset"),ids);
        // The temporary render scene may unload unreferenced assets; reload before saving the icons.
        var catalog=AssetDatabase.LoadAssetAtPath<GameCatalog>(GameBase+"/Generated/Catalog.asset");
        foreach(var id in ids)catalog.Find(id).icon=AssetDatabase.LoadAssetAtPath<Texture2D>(GameBase+"/Icons/"+id+".png");
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
    }
    static void BuildGameIcons(GameCatalog catalog,string[] only=null){
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Lighting();var camera=CameraAt(new Vector3(0,.10f,0),.24f);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
        var chosen=catalog.assets.Where(a=>a.id.EndsWith("-whole")||a.id.EndsWith("-chopped")||a.id.StartsWith("dish-")||new[]{"cutting-board","knife","pot-both","pot-ready","pot-empty","pot-burnt","station-serve","station-sink","fire-extinguisher"}.Contains(a.id)).Concat(catalog.chefs).Concat(catalog.customers).ToArray();
        foreach(var a in chosen.Where(a=>only==null||only.Contains(a.id))){var go=UnityEngine.Object.Instantiate(a.prefab);bool chef=a.id.EndsWith("-chef")||a.id.EndsWith("-customer");var target=chef?new Vector3(0,.65f,0):new Vector3(0,.10f,0);camera.transform.position=target+(chef?new Vector3(2,1.2f,3):new Vector3(1,1.8f,2));camera.transform.LookAt(target);camera.orthographicSize=chef?.85f:.23f;if(!chef)FitRoomCamera(camera,go);var rt=new RenderTexture(256,256,24){antiAliasing=4};camera.targetTexture=rt;camera.aspect=1;camera.Render();RenderTexture.active=rt;var image=new Texture2D(256,256,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();string path=GameBase+"/Icons/"+a.id+".png";File.WriteAllBytes(path,image.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);ImportIcon(path,256);a.icon=AssetDatabase.LoadAssetAtPath<Texture2D>(path);}
    }
    public static void BuildPolishedGame(){RebuildEnvironment();BuildGameWeb();}
    public static void BuildGameWeb(){BuildGameplay();BuildWebOnly();}
    public static void BuildWebOnly(){ApplyReleaseBrand();var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Base+"/Scenes/BaraKitchen_Game.unity"},locationPathName=Path.Combine(GameOutput,"web"),target=BuildTarget.WebGL,options=BuildOptions.Development});if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Web build failed: "+report.summary.result);Debug.Log("BARA_GAME_WEB_OK bytes="+report.summary.totalSize);}
}
