using System;
using UnityEngine;
namespace BaraKitchen.Gameplay {
    [Serializable] public class SoundAsset { public string id; public AudioClip clip; }
    [Serializable] public class GameAsset { public string id; public GameObject prefab; public Texture2D icon; }
    [Serializable] public class LevelDefinition {
        public int id,rows,cols,dishes,ticketCap; public string title,subtitle,lesson; public float duration,patience,module=.92f;
        public int[] stars; public string[] recipes; public GameObject room; public Vector3 spawn,cameraTarget; public float cameraSize; public Texture2D thumbnail;
    }
    [CreateAssetMenu(menuName="Bara Kitchen/Game catalog")]
    public class GameCatalog : ScriptableObject {
        public LevelDefinition[] levels; public GameAsset[] assets,chefs,customers; public SoundAsset[] audio; public Shader highlightShader; public Shader dashShader; public Font font, titleFont; public UnityEngine.UIElements.PanelSettings panel;
        public GameAsset Find(string id)=>Array.Find(assets,a=>a.id==id)??Array.Find(chefs,a=>a.id==id)??Array.Find(customers,a=>a.id==id);
        public GameObject Prefab(string id)=>Find(id)?.prefab;
        public Texture2D Icon(string id)=>Find(id)?.icon;
    }
    public enum SessionPhase { Menu, Briefing, Service, Paused, Results }
    public enum ItemKind { Ingredient, Dish, Extinguisher, Pot }
    public enum GuestPhase { Arriving, Waiting, Eating, Leaving }
    public static class Recipes {
        public const int Tomato=1,Cucumber=2,Carrot=4,Mushroom=8,Salad=3,Soup=16;
        public static int Ingredient(string id)=>id=="tomato"?1:id=="cucumber"?2:id=="carrot"?4:id=="mushroom"?8:0;
        public static string Name(string id)=>id=="salad"?"Garden salad":"Woodland soup";
        public static string Asset(string id)=>id=="salad"?"dish-salad":"dish-soup";
        public static string[] Parts(string id)=>id=="salad"?new[]{"tomato","cucumber"}:new[]{"carrot","mushroom"};
        public static string Match(int contents)=>contents==Salad?"salad":contents==Soup?"soup":null;
    }
    [Serializable] public class GameSettings { public float volume=.65f,musicVolume=.65f,effectsVolume=.85f; public float cameraTilt=KitchenCameraController.DefaultTilt,cameraYaw=KitchenCameraController.DefaultYaw; public bool calm,reduceFlash; public int chef; }
    [Serializable] public class ScoreSave { public int[] best=new int[8],stars=new int[8]; public GameSettings settings=new GameSettings(); }
}
