from pathlib import Path
p=Path('Unity/BaraKitchen/Assets/BaraKitchen/Gameplay/KitchenHUD.cs');s=p.read_text()
a=s.index('        public KitchenGame game;');b=s.index('        void Ready()',a)
s=s[:a]+'''        public KitchenGame game; public StyleSheet theme; UIDocument document; VisualElement root,overlay,hud,tickets,workFill,workTrack,heldIcon; Label score,timer,streak,prompt,training,feedbackLabel; KitchenMotif clockFace; float nextRefresh; string ticketKey="",heldKey="";int screenWidth,screenHeight;
        class PotMeter {public VisualElement root,fill;public KitchenMotif icon;}
        readonly Dictionary<KitchenStation,PotMeter> potMeters=new Dictionary<KitchenStation,PotMeter>();
''' +s[b:]
s=s.replace('potLabels.Clear();serviceLabel=null;','potMeters.Clear();').replace('hud=null;help=false;','hud=null;')
s=s.replace('Clear();game.SetCamera();game.cam.rect=new Rect(.31f,0,.69f,1);','Clear();game.cam.rect=new Rect(.31f,0,.69f,1);game.SetCamera();')
a=s.index('        public void ShowBriefing()');b=s.index('        public void ShowPause()',a)
s=s[:a]+'''        VisualElement Picture(string cls,VisualElement parent){return Box(cls,parent);}
        void PictureIcon(string id,VisualElement picture,float x,float y,int size){var v=Icon(id,picture,size);v.style.position=Position.Absolute;v.style.left=x;v.style.top=y;}
        void Arrow(VisualElement parent,int size=28){var v=Motif(KitchenMotif.Shape.Arrow,"lesson-arrow",parent);v.style.width=size;v.style.height=size;}
        void Step(VisualElement row,string action,string recipe){
            var step=Box("lesson-step",row);var picture=Picture("lesson-picture",step);var parts=Recipes.Parts(recipe);
            if(action=="Chop"){PictureIcon(game.catalog.chefs[game.Settings.chef].id,picture,0,-14,105);PictureIcon("cutting-board",picture,65,52,90);PictureIcon(parts[0]+"-whole",picture,80,32,40);PictureIcon(parts[1]+"-whole",picture,116,44,36);PictureIcon("knife",picture,67,21,50);}
            else if(action=="Cook"){PictureIcon("pot-both",picture,41,34,100);PictureIcon(parts[0]+"-chopped",picture,26,0,42);PictureIcon(parts[1]+"-chopped",picture,116,0,42);var v=Motif(KitchenMotif.Shape.Clock,"lesson-clock",picture);v.style.left=126;v.style.top=77;}
            else if(action=="Plate"){PictureIcon("dish-clean",picture,37,42,115);PictureIcon(recipe=="salad"?"tomato-chopped":"pot-ready",picture,11,0,66);PictureIcon(recipe=="salad"?"cucumber-chopped":"dish-soup",picture,111,5,65);}
            else if(action=="Serve"){PictureIcon("station-serve",picture,26,5,137);PictureIcon(Recipes.Asset(recipe),picture,58,54,57);}
            else {PictureIcon("station-sink",picture,48,9,122);PictureIcon("dish-dirty",picture,4,26,58);PictureIcon("dish-clean",picture,117,0,45);}
            var caption=Box("lesson-step-caption",step);Text(action,"lesson-word",caption);if(action!="Cook")Text(action=="Chop"||action=="Wash"?"E · SPACE":"E","keycap",caption);
        }
        void Notebook(bool briefing){var l=game.level;var p=Modal("KITCHEN 0"+l.id,l.title,null,true);p.AddToClassList("notebook");
            foreach(string recipe in l.recipes){var recipeRow=Box("lesson-recipe",p);var badge=Box("lesson-recipe-name",recipeRow);Icon(Recipes.Asset(recipe),badge,48);Text(Recipes.Name(recipe),"lesson-word",badge);var flow=Box("lesson-flow",recipeRow);string[] steps=recipe=="salad"?new[]{"Chop","Plate","Serve","Wash"}:new[]{"Chop","Cook","Plate","Serve"};for(int i=0;i<steps.Length;i++){if(i>0)Arrow(flow);Step(flow,steps[i],recipe);}}
            var footer=Box("lesson-foot",p);
            var orders=Box("lesson-small",footer);PictureIcon(game.catalog.customers[0].id,orders,0,0,64);var arrow=Motif(KitchenMotif.Shape.Arrow,"lesson-mini-arrow",orders);arrow.style.left=60;var ticket=Motif(KitchenMotif.Shape.Ticket,"lesson-ticket",orders);ticket.style.left=91;Text("Orders ↖","lesson-mini-caption",orders);
            var undo=Box("lesson-small",footer);PictureIcon("dish-clean",undo,0,19,70);PictureIcon("carrot-whole",undo,18,6,39);var lift=Motif(KitchenMotif.Shape.Arrow,"lesson-mini-arrow",undo);lift.style.left=65;PictureIcon("carrot-whole",undo,101,5,49);Text("R · Lift one","lesson-mini-caption",undo);
            var move=Box("lesson-small",footer);Motif(KitchenMotif.Shape.Move,"lesson-skill",move);Text("WASD","lesson-mini-caption",move);
            if(l.id>=2){var dash=Box("lesson-small",footer);Motif(KitchenMotif.Shape.Dash,"lesson-skill",dash);Text("SHIFT · Dash","lesson-mini-caption",dash);}
            if(l.id>=3){var toss=Box("lesson-small",footer);Motif(KitchenMotif.Shape.Throw,"lesson-skill",toss);Text("Q · Throw","lesson-mini-caption",toss);}
            var buttons=Box("button-row lesson-buttons",p);Button(briefing?"Open the cafe":"Back to service",briefing?(Action)game.StartService:game.Resume,buttons);if(briefing)Button("Choose another kitchen",ShowLevels,buttons,true);
        }
        public void ShowBriefing(){Notebook(true);}
        void ServiceViewport(){game.cam.rect=new Rect(0,.055f,1,.81f);game.SetCamera();screenWidth=Screen.width;screenHeight=Screen.height;}
        public void ShowHUD(){Clear();ServiceViewport();hud=Box("hud",root);hud.pickingMode=PickingMode.Ignore;Box("service-rail",hud);tickets=Box("tickets",hud);tickets.pickingMode=PickingMode.Ignore;
            var summary=Box("summary",hud);Motif(KitchenMotif.Shape.Coin,"coin-icon",summary);var tally=Box("tally",summary);score=Text("0","score",tally);streak=Text("","caption",tally);
            var clock=Box("clock",hud);clockFace=Motif(KitchenMotif.Shape.Clock,"clock-face",clock);timer=Text("","timer",clock);training=Text("","clock-caption",clock);
            feedbackLabel=Text("","score-feedback",hud);
            var contextual=Box("context",hud);heldIcon=Box("held-icon",contextual);var info=Box("context-info",contextual);prompt=Text("","prompt",info);workTrack=Box("work-track",info);workFill=Box("work-fill",workTrack);
            var pause=Button("Pause",game.Pause,hud,true);pause.AddToClassList("pause-button");pause.focusable=false;var footer=Box("hud-footer",hud);foreach(var pair in new[]{("WASD","Move"),("E","Place"),("R","Lift"),("SPACE","Work"),("H","Guide")}){var key=Box("key-hint",footer);Text(pair.Item1,"keycap",key);Text(pair.Item2,"key-name",key);}
            foreach(var station in game.stations.Where(s=>s.kind=="pot")){var meter=Box("pot-meter",hud);meter.name="pot-meter-"+station.id;meter.pickingMode=PickingMode.Ignore;var track=Box("pot-track",meter);var fill=Box("pot-fill",track);var icon=Motif(KitchenMotif.Shape.Check,"pot-status",meter);potMeters[station]=new PotMeter{root=meter,fill=fill,icon=icon};}
            ticketKey="!";heldKey="!";Refresh();
        }
''' +s[b:]
a=s.index('        public void ToggleHelp()');b=s.index('        public static string TimeLabel(',a)
s=s[:a]+'''        public void ToggleHelp(){if(game.phase!=SessionPhase.Service)return;game.Pause();Notebook(false);}
        void Update(){if(game==null||game.phase!=SessionPhase.Service||hud==null)return;if(screenWidth!=Screen.width||screenHeight!=Screen.height)ServiceViewport();if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.08f;Refresh();}
        void Refresh(){if(score==null)return;
            foreach(var pair in potMeters){var station=pair.Key;var meter=pair.Value;var heat=station.hazard;meter.root.style.display=heat.state==HeatState.Empty?DisplayStyle.None:DisplayStyle.Flex;
                float amount=heat.state==HeatState.Cooking?heat.elapsed/heat.cookingSeconds:heat.state==HeatState.Warning?1-heat.WarningProgress:heat.state==HeatState.Burning?heat.fireRemaining:1;meter.fill.style.width=Length.Percent(Mathf.Clamp01(amount)*100);
                bool danger=heat.state==HeatState.Warning||heat.state==HeatState.Burning||heat.state==HeatState.Extinguished;meter.fill.style.backgroundColor=danger?new Color(.9f,.44f,.20f):new Color(.38f,.69f,.47f);meter.icon.symbol=danger?KitchenMotif.Shape.Flame:KitchenMotif.Shape.Check;meter.icon.style.display=heat.state==HeatState.Cooking?DisplayStyle.None:DisplayStyle.Flex;meter.icon.MarkDirtyRepaint();PositionMeter(meter.root,station.Slot+Vector3.up*.36f);}
            score.text=game.book.score.ToString();streak.text="×"+Mathf.Max(1,game.book.streak);timer.text=TimeLabel(game.remaining);clockFace.amount=game.remaining/(game.level.duration*(game.Settings.calm?1.5f:1));clockFace.MarkDirtyRepaint();timer.EnableInClassList("urgent",!game.training&&game.remaining<=30);training.text=game.training?"PRACTICE":"";
            feedbackLabel.text=game.feedback??"";feedbackLabel.style.display=game.feedbackTime>0?DisplayStyle.Flex:DisplayStyle.None;
            var item=game.chef.held;string keyHeld=item?item.Label+item.portions.Count:"empty";if(keyHeld!=heldKey){heldKey=keyHeld;heldIcon.Clear();if(!item)Motif(KitchenMotif.Shape.Paw,"paw-icon",heldIcon);else Icon(item.kind==ItemKind.Ingredient?item.ingredient+(item.chopped?"-chopped":"-whole"):item.kind==ItemKind.Extinguisher?"fire-extinguisher":item.Recipe!=null?Recipes.Asset(item.Recipe):"dish-clean",heldIcon,34);}
            var focused=game.chef.focused;prompt.text=game.chef.working?"SPACE":focused?(!item&&focused.item&&focused.item.kind==ItemKind.Dish&&focused.item.portions.Count>0?"E / R":"E"):"";workTrack.style.display=game.chef.working?DisplayStyle.Flex:DisplayStyle.None;workFill.style.width=Length.Percent(Mathf.Clamp01(game.chef.workProgress)*100);
            string key=string.Join(",",game.book.orders.Select(o=>o.id));if(key!=ticketKey){ticketKey=key;tickets.Clear();if(game.book.orders.Count==0){var empty=Box("ticket empty-ticket",tickets);Motif(KitchenMotif.Shape.Paw,"waiting-paw",empty);}foreach(var o in game.book.orders){var c=Box("ticket",tickets);c.name="ticket-"+o.id;Text("#"+o.id,"ticket-number",c);var row=Box("ticket-top",c);Icon(Recipes.Asset(o.recipe),row,48);var parts=Box("ingredient-row",row);foreach(var ingredient in Recipes.Parts(o.recipe))Icon(ingredient+"-chopped",parts,27);var track=Box("patience-track",c);var fill=Box("patience-fill",track);fill.name="fill";}}
            foreach(var o in game.book.orders){var c=tickets.Q<VisualElement>("ticket-"+o.id);if(c==null)continue;float t=o.remaining/o.total;var fill=c.Q<VisualElement>("fill");fill.style.width=Length.Percent(t*100);fill.style.backgroundColor=t>.5f?new Color(.35f,.65f,.45f):t>.2f?new Color(.91f,.64f,.23f):new Color(.84f,.30f,.24f);c.EnableInClassList("urgent-ticket",!game.training&&t<.2f);}
        }
        void PositionMeter(VisualElement meter,Vector3 world){var point=RuntimePanelUtils.CameraTransformWorldToPanel(root.panel,world,game.cam);meter.style.left=point.x-34;meter.style.top=point.y-8;}
''' +s[b:]
# Multiple classes must be attached separately in UI Toolkit.
s=s.replace('v.AddToClassList(cls);','foreach(var c in cls.Split(\' \'))v.AddToClassList(c);')
p.write_text(s)
