using UnityEngine;
using System.Collections.Generic;
namespace BaraKitchen.Gameplay {
    public sealed class KitchenStation : MonoBehaviour {
        public string id,kind; public readonly List<KitchenItem> items=new List<KitchenItem>(); public KitchenItem item {get=>items.Count>0?items[items.Count-1]:null;set{items.Clear();if(value)items.Add(value);}} public Vector3 slotOffset=new Vector3(0,.47f,0); public bool reserved;
        public CookingHazard hazard; public VisualState pot; public int potContents; public float washing; public GameObject highlight;
        public Vector3 Slot=>transform.TransformPoint(slotOffset);
        public string Label=>kind=="counter"?"Worktop":kind=="prep"?"Chopping board":kind=="pot"?"Soup pot":kind=="dishes"?"Clean dishes":kind=="sink"?"Wash station":kind=="return"?"Dish return":kind=="serve"?"Service hatch":kind=="bin"?"Food bin":kind=="extinguisher"?"Extinguisher stand":kind+" crate";
        public bool IsSurface=>kind=="counter"||kind=="prep"||kind=="sink"||kind=="extinguisher";
        public bool CanStackIngredient=> (kind=="counter"||kind=="prep")&&items.Count<6&&items.TrueForAll(i=>i.kind==ItemKind.Ingredient);
        public void Put(KitchenItem value){items.Add(value);Arrange();}
        public void Arrange(){for(int i=0;i<items.Count;i++){float angle=i*2.39996f;float radius=items.Count>1?.13f:0;items[i].Attach(transform,slotOffset+new Vector3(Mathf.Cos(angle)*radius,.034f*(i/3),Mathf.Sin(angle)*radius));}}
        public KitchenItem Take(){var value=item;if(value){items.RemoveAt(items.Count-1);value.transform.SetParent(null,true);Arrange();}return value;}
        public void ResetPot(){potContents=0;hazard.RemovePot();pot.SetState(0);}
        public void SetHighlight(bool value,Color color,bool service=false,bool still=false){if(!highlight)return;highlight.SetActive(value);if(value)highlight.GetComponent<StationHighlight>()?.Configure(service,still,color);}
    }
}
