using System;
using System.Collections.Generic;
using System.Linq;
namespace BaraKitchen.Gameplay {
    [Serializable] public class FoodOrder { public int id; public string recipe; public float remaining,total; public CafeGuest guest; public bool live=true; }
    public sealed class OrderBook {
        public readonly List<FoodOrder> orders=new List<FoodOrder>();
        public int score,served,missed,streak,baseEarned,tipsEarned,penalties; int nextID;
        public event Action<FoodOrder> Expired; public event Action<FoodOrder,int,int> Delivered;
        public FoodOrder Add(string recipe,float patience,CafeGuest guest=null){var o=new FoodOrder{id=++nextID,recipe=recipe,remaining=patience,total=patience,guest=guest};orders.Add(o);return o;}
        public void Tick(float dt){foreach(var o in orders.ToArray()){if(!o.live)continue;o.remaining=Math.Max(0,o.remaining-dt);if(o.remaining>0)continue;o.live=false;orders.Remove(o);missed++;streak=0;int loss=Math.Min(score,15);score-=loss;penalties+=loss;Expired?.Invoke(o);}}
        public bool CanServe(string recipe)=>recipe!=null&&orders.Any(o=>o.live&&o.recipe==recipe);
        public FoodOrder Serve(string recipe){var o=orders.FirstOrDefault(x=>x.live&&x.recipe==recipe);if(o==null)return null;streak=o==orders[0]?Math.Min(4,streak+1):1;int basePoints=recipe=="salad"?60:80;int tip=(int)Math.Ceiling(20*o.remaining/o.total)*streak;o.live=false;orders.Remove(o);score+=basePoints+tip;baseEarned+=basePoints;tipsEarned+=tip;served++;Delivered?.Invoke(o,basePoints,tip);return o;}
    }
}
