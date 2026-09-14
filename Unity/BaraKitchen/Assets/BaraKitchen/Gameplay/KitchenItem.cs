using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace BaraKitchen.Gameplay {
    public sealed class KitchenItem : MonoBehaviour {
        public const int PlateCapacity=6;
        public ItemKind kind; public string ingredient; public bool chopped,dirty; public int contents; public float prep;
        public VisualState visual; public bool flying; public bool soup;
        // A carried pot retains its stove, contents, hazard state and original mesh root.
        public KitchenStation potStation;
        public bool BurntPot=>kind==ItemKind.Pot&&potStation&&potStation.hazard.state==HeatState.Extinguished;
        // Actual ingredients remain owned by the plate, retaining preparation progress.
        public readonly List<KitchenItem> portions=new List<KitchenItem>();
        public Vector3 worldScale=Vector3.one; float displayScale=1;
        public bool IsEmpty=>kind==ItemKind.Dish&&!soup&&portions.Count==0;
        public void SetWorldSize(Vector3 size){worldScale=size;ApplyWorldSize();}
        void ApplyWorldSize(){var p=transform.parent?transform.parent.lossyScale:Vector3.one;var size=worldScale*displayScale;transform.localScale=new Vector3(size.x/Mathf.Max(.0001f,Mathf.Abs(p.x)),size.y/Mathf.Max(.0001f,Mathf.Abs(p.y)),size.z/Mathf.Max(.0001f,Mathf.Abs(p.z)));}
        public string Recipe {
            get {if(kind!=ItemKind.Dish||dirty)return null;if(soup)return portions.Count==0?"soup":null;
                return portions.Count==2&&portions.All(p=>p.chopped)&&portions.Count(p=>p.ingredient=="tomato")==1&&portions.Count(p=>p.ingredient=="cucumber")==1?"salad":null;}
        }
        public string Label=>kind==ItemKind.Pot?(BurntPot?"Burnt pot":"Empty pot"):kind==ItemKind.Extinguisher?"Fire extinguisher":kind==ItemKind.Ingredient?(chopped?"Chopped ":"")+ingredient:dirty?"Dirty dish":Recipe!=null?Recipes.Name(Recipe):IsEmpty?"Clean dish":"Ingredients on dish";
        public void Refresh(){
            if(!visual||kind==ItemKind.Pot)return;if(kind==ItemKind.Ingredient){visual.SetState(chopped?1:0);return;}
            contents=soup?Recipes.Soup:0;foreach(var p in portions)if(p.chopped)contents|=Recipes.Ingredient(p.ingredient);
            bool salad=Recipe=="salad";visual.SetState(dirty?5:soup?4:salad?3:0);
            for(int i=0;i<portions.Count;i++){var p=portions[i];p.gameObject.SetActive(!salad);p.displayScale=portions.Count==1?.85f:.62f;float angle=i*2.39996f;float radius=portions.Count==1?0:.064f;
                p.Attach(transform,new Vector3(Mathf.Cos(angle)*radius,(soup?.13f:.035f)+.018f*(i/3),Mathf.Sin(angle)*radius));p.transform.localRotation=Quaternion.Euler(0,i*57,0);}
        }
        public bool CanAdd(KitchenItem other)=>kind==ItemKind.Dish&&portions.Count<PlateCapacity&&other&&other.kind==ItemKind.Ingredient;
        public bool Add(KitchenItem other){if(!CanAdd(other))return false;portions.Add(other);Refresh();return true;}
        public KitchenItem TakePortion(){if(portions.Count==0)return null;var p=portions[portions.Count-1];portions.RemoveAt(portions.Count-1);p.gameObject.SetActive(true);p.displayScale=1;p.Attach(transform.parent,Vector3.zero);Refresh();return p;}
        public void ClearFood(){foreach(var p in portions)if(p)Destroy(p.gameObject);portions.Clear();soup=false;contents=0;Refresh();}
        public void FillSoup(){ClearFood();soup=true;Refresh();}
        public void Attach(Transform parent,Vector3 local){transform.SetParent(parent,true);transform.localPosition=local;transform.localRotation=Quaternion.identity;ApplyWorldSize();}
    }
}
