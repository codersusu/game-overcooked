using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BaraKitchen.Gameplay {
    // Read-only production snapshot. No debug hooks and no model-triggered game actions.
    public static class KitchenHelpContext {
        [Serializable] public class Item {public string label,kind,ingredient,recipe,homeStove;public bool chopped,dirty,burnt;public float chopProgress;public string[] portions;}
        [Serializable] public class Order {public int id;public string recipe;public float secondsLeft;}
        [Serializable] public class Station {public string id,kind,label,screenArea,directionFromChef,heat;public bool reachable,availableNow,potDocked;public string[] walkRoute,possibleActions,potIngredients;public float cookProgress,warningSecondsLeft;public Item[] items;}
        [Serializable] public class Snapshot {public int level,cleanPlates,dirtyPlates,score,served,missed,streak;public int[] starGoals;public string working;public float idleSeconds,chefX,chefZ;public string kitchen,phase,focusedStation;public bool practice,dashUnlocked,throwUnlocked;public float roundSecondsLeft;public Item holding;public Order[] orders;public string[] recipes;public Station[] stations;}
        public static Item Describe(KitchenItem i)=>!i?null:new Item{label=i.Label,kind=i.kind.ToString(),ingredient=i.ingredient??"",recipe=i.Recipe??"",chopped=i.chopped,dirty=i.dirty,burnt=i.BurntPot,chopProgress=Mathf.Clamp01(i.prep/2.16f),homeStove=i.potStation?i.potStation.id:"",portions=i.portions.Select(p=>p.Label).ToArray()};
        public static string Direction(Camera cam,Vector3 delta){
            if(delta.magnitude<.15f)return "here";
            var right=Vector3.ProjectOnPlane(cam.transform.right,Vector3.up).normalized;var up=Vector3.ProjectOnPlane(cam.transform.forward,Vector3.up).normalized;
            float x=Vector3.Dot(delta.normalized,right),y=Vector3.Dot(delta.normalized,up);
            if(Mathf.Abs(x)>.86f)return x>0?"right":"left";
            if(Mathf.Abs(y)>.86f)return y>0?"up":"down";
            return (y>0?"up":"down")+"-"+(x>0?"right":"left");
        }
        public static string[] Actions(KitchenGame g,KitchenStation s){
            var a=new List<string>();var h=g.chef.held;var item=s.item;
            if(Recipes.Ingredient(s.kind)!=0){if(!h)a.Add("E: pick up raw "+s.kind);}
            else if(s.kind=="dishes"){if(!h&&g.rack.Count>0)a.Add("E: pick up clean plate");if(h&&h.kind==ItemKind.Dish&&!h.dirty&&h.IsEmpty)a.Add("E: return clean empty plate");}
            else if(s.kind=="return"){if(!h&&g.returns.Count>0)a.Add("E: pick up dirty plate");}
            else if(s.kind=="serve"){if(h&&g.book.CanServe(h.Recipe))a.Add("E: serve "+h.Label);}
            else if(s.kind=="bin"){if(h&&h.BurntPot)a.Add("E: empty burnt food; keep empty pot in paws");else if(h&&h.kind==ItemKind.Ingredient)a.Add("E: discard held ingredient");else if(h&&h.kind==ItemKind.Dish&&!h.IsEmpty)a.Add("E: discard food; keep plate");}
            else if(s.kind=="pot"){
                if(!s.PotDocked){if(h==s.potItem)a.Add("E: return pot to its stove (burnt food remains if not emptied)");}
                else if(s.hazard.state==HeatState.Burning){if(h&&h.kind==ItemKind.Extinguisher)a.Add("SPACE: start extinguishing");}
                else if(s.hazard.state==HeatState.Extinguished){if(!h)a.Add("E: pick up burnt pot for food bin");}
                else if(s.hazard.state==HeatState.Ready||s.hazard.state==HeatState.Warning){if(h&&h.kind==ItemKind.Dish&&!h.dirty&&h.IsEmpty)a.Add("E: fill held empty plate with cooked soup");}
                else if(s.hazard.state!=HeatState.Cooking){
                    if(!h&&s.potContents!=0)a.Add("E: discard incomplete soup batch");
                    if(h){var ingredients=h.kind==ItemKind.Ingredient?new[]{h}:h.portions.ToArray();foreach(var i in ingredients){int bit=Recipes.Ingredient(i.ingredient);if(i.chopped&&(bit==4||bit==8)&&(s.potContents&bit)==0)a.Add("E: add chopped "+i.ingredient+" to pot");}}
                }
            }
            else if(s.IsSurface){
                if(!h&&item){a.Add("E: pick up "+item.Label);if(item.kind==ItemKind.Dish&&item.portions.Count>0)a.Add("R: lift one ingredient from plate");}
                if(h&&!s.reserved){if(!item){if(s.kind!="sink"&&s.kind!="extinguisher"||s.kind=="sink"&&h.kind==ItemKind.Dish&&h.dirty||s.kind=="extinguisher"&&h.kind==ItemKind.Extinguisher)a.Add("E: put down "+h.Label);}
                    else if(item.CanAdd(h)||h.CanAdd(item))a.Add("E: combine held item and station item on plate (recipe may be invalid)");
                    else if(h.kind==ItemKind.Ingredient&&s.CanStackIngredient)a.Add("E: stack held ingredient on worktop");}
                if(!h&&item&&s.kind=="prep"&&item.kind==ItemKind.Ingredient&&!item.chopped)a.Add("SPACE: start chopping "+item.ingredient);
                if(!h&&item&&s.kind=="sink"&&item.dirty)a.Add("SPACE: start washing plate");
            }
            return a.ToArray();
        }
        public static Snapshot Capture(KitchenGame g,SessionPhase phase){
            var routes=new Routes(g);return new Snapshot{level=g.level.id,score=g.book.score,served=g.book.served,missed=g.book.missed,streak=g.book.streak,starGoals=g.level.stars,working=g.chef.working?g.chef.workLabel:"",idleSeconds=g.voice?g.voice.IdleSeconds:0,chefX=g.chef.transform.position.x,chefZ=g.chef.transform.position.z,kitchen=g.level.title,phase=phase.ToString(),practice=g.training,dashUnlocked=g.DashAllowed,throwUnlocked=g.ThrowAllowed,roundSecondsLeft=g.remaining,cleanPlates=g.rack.Count,dirtyPlates=g.returns.Count,holding=Describe(g.chef.held),focusedStation=g.chef.focused?g.chef.focused.id:"",recipes=g.level.recipes,
                orders=g.book.orders.Select(o=>new Order{id=o.id,recipe=Recipes.Name(o.recipe),secondsLeft=o.remaining}).ToArray(),stations=g.stations.Select(s=>{
                    var p=g.cam.WorldToViewportPoint(s.transform.position);var path=routes.To(s);var heat=s.hazard;
                    return new Station{id=s.id,kind=s.kind,label=s.Label,screenArea=(p.y>.6f?"upper":p.y<.4f?"lower":"middle")+" "+(p.x<.4f?"left":p.x>.6f?"right":"centre"),directionFromChef=Direction(g.cam,s.transform.position-g.chef.transform.position),reachable=path!=null,walkRoute=path??Array.Empty<string>(),availableNow=g.chef.focused==s,possibleActions=Actions(g,s),items=s.items.Where(i=>i).Select(Describe).ToArray(),potDocked=s.PotDocked,heat=heat?heat.state.ToString():"",cookProgress=heat?Mathf.Clamp01(heat.elapsed/heat.cookingSeconds):0,warningSecondsLeft=heat&&heat.state==HeatState.Warning?heat.warningSeconds*(1-heat.WarningProgress):0,potIngredients=new[]{(s.potContents&4)!=0?"chopped carrot":"",(s.potContents&8)!=0?"chopped mushroom":""}.Where(i=>i!="").ToArray()};}).ToArray()};
        }
        sealed class Routes {
            readonly KitchenGame game;readonly Dictionary<Vector2Int,Vector3> cells=new Dictionary<Vector2Int,Vector3>();readonly Dictionary<Vector2Int,Vector2Int> parent=new Dictionary<Vector2Int,Vector2Int>();readonly Dictionary<Vector2Int,int> distance=new Dictionary<Vector2Int,int>();Vector2Int start;
            bool Free(Vector3 p)=>!Physics.OverlapCapsule(p+Vector3.up*.26f,p+Vector3.up*.90f,.22f,~0,QueryTriggerInteraction.Ignore).Any(c=>c.enabled&&!c.transform.IsChildOf(game.chef.transform));
            bool Clear(Vector3 from,Vector3 to){var delta=to-from;return !Physics.CapsuleCastAll(from+Vector3.up*.26f,from+Vector3.up*.90f,.21f,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore).Any(h=>!h.transform.IsChildOf(game.chef.transform));}
            public Routes(KitchenGame g){game=g;Physics.SyncTransforms();for(int x=0;x<g.level.cols;x++)for(int z=0;z<g.level.rows;z++){var p=new Vector3(((g.level.cols-1)*.5f-x)*g.level.module,0,z*g.level.module);if(Free(p))cells[new Vector2Int(x,z)]=p;}
                var origin=g.chef.transform.position;origin.y=0;var starts=cells.Where(c=>Clear(origin,c.Value)).OrderBy(c=>(c.Value-origin).sqrMagnitude).ToArray();if(starts.Length==0)return;start=starts[0].Key;distance[start]=0;var q=new Queue<Vector2Int>();q.Enqueue(start);
                while(q.Count>0){var c=q.Dequeue();foreach(var d in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right}){var n=c+d;if(!cells.ContainsKey(n)||distance.ContainsKey(n)||!Clear(cells[c],cells[n]))continue;parent[n]=c;distance[n]=distance[c]+1;q.Enqueue(n);}}
            }
            bool Faceable(Vector3 p,KitchenStation s){var delta=s.transform.position-p;delta.y=0;if(delta.magnitude>1.02f||delta.magnitude<.05f)return false;return !Physics.RaycastAll(p+Vector3.up*.30f,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore).Any(h=>!h.transform.IsChildOf(s.transform)&&!h.transform.IsChildOf(game.chef.transform));}
            public string[] To(KitchenStation s){
                if(Faceable(game.chef.transform.position,s))return new[]{"Stay here; face "+Direction(game.cam,s.transform.position-game.chef.transform.position)+" toward the station"};
                var candidates=distance.Keys.Where(k=>Faceable(cells[k],s)).OrderBy(k=>distance[k]).ToArray();if(candidates.Length==0)return null;
                var route=new List<Vector2Int>{candidates[0]};while(route[route.Count-1]!=start)route.Add(parent[route[route.Count-1]]);route.Reverse();var points=new List<Vector3>{game.chef.transform.position};points.AddRange(route.Select(k=>cells[k]));var legs=new List<string>();string last="";int count=0;
                for(int i=1;i<points.Count;i++){string dir=Direction(game.cam,points[i]-points[i-1]);if(dir=="here")continue;if(last==dir){count++;continue;}if(count>0)legs.Add(last+" about "+count+" worktop widths");last=dir;count=1;}if(count>0)legs.Add(last+" about "+count+" worktop widths");legs.Add("then face "+Direction(game.cam,s.transform.position-cells[candidates[0]])+" toward the station");return legs.ToArray();
            }
        }
    }
}
