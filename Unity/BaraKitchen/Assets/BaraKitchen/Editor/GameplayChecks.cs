using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using BaraKitchen;
using BaraKitchen.Gameplay;
[InitializeOnLoad]
public static class GameplayChecks {
    static readonly List<string> checks=new List<string>();static readonly Stack<IEnumerator> flow=new Stack<IEnumerator>();static bool started;static double began;static KitchenGame game;
    static string Out=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../art/production/gameplay-round-01"));
    static GameplayChecks(){EditorApplication.update+=Update;}
    public static void Begin(){Directory.CreateDirectory(Out+"/qa");SessionState.SetBool("Bara.GameChecks",true);began=EditorApplication.timeSinceStartup;EditorSceneManager.OpenScene("Assets/BaraKitchen/Scenes/BaraKitchen_Game.unity");EditorApplication.isPlaying=true;}
    static void Require(bool condition,string label){if(!condition)throw new Exception(label);checks.Add(label);Debug.Log("BARA_GAME_CHECK "+label);}
    static void Update(){if(!SessionState.GetBool("Bara.GameChecks",false))return;try{if(!EditorApplication.isPlaying)return;if(!started){game=UnityEngine.Object.FindFirstObjectByType<KitchenGame>();if(!game||!game.live)return;started=true;flow.Push(Run());}if(flow.Count>0){var top=flow.Peek();if(!top.MoveNext())flow.Pop();else if(top.Current is IEnumerator nested)flow.Push(nested);}else Finish(true,null);}catch(Exception e){Debug.LogException(e);Finish(false,e.ToString());}}
    static void Finish(bool passed,string error){SessionState.SetBool("Bara.GameChecks",false);File.WriteAllText(Out+"/gameplay-validation.json",JsonUtility.ToJson(new Result{passed=passed,checks=checks.ToArray(),error=error},true));Debug.Log(passed?"BARA_GAMEPLAY_CHECKS_OK":"BARA_GAMEPLAY_CHECKS_FAILED");EditorApplication.Exit(passed?0:1);}
    [Serializable] class Result{public bool passed;public string[] checks;public string error;}
    static KitchenStation Station(string id)=>game.stations.First(s=>s.id==id||s.kind==id);
    static bool Free(Vector3 p){return !Physics.OverlapCapsule(p+Vector3.up*.26f,p+Vector3.up*.90f,.22f).Any(c=>c.enabled&&!c.isTrigger&&!c.transform.IsChildOf(game.chef.transform));}
    static Vector3 Approach(KitchenStation station){var dirs=new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right};foreach(var dir in dirs)foreach(float distance in new[]{.76f,.84f,.94f}){var p=station.transform.position+dir*distance;p.y=0;if(!Free(p))continue;game.chef.Warp(p,-dir);game.chef.focused=game.FindStation(p,-dir);if(game.chef.focused==station)return p;}throw new Exception("No physically clear interaction approach: L"+game.level.id+" "+station.id);}
    static void Use(string id){var s=Station(id);Approach(s);Require(game.chef.Interact(),"L"+game.level.id+" interact "+id);}
    static void Work(string station){Approach(Station(station));Require(game.chef.BeginWork(),"L"+game.level.id+" begin "+station);for(int i=0;i<70&&game.chef.working;i++)game.chef.TickWork(.05f);Require(!game.chef.working,"L"+game.level.id+" complete "+station);}
    static void Chop(string ingredient){Use(ingredient);Use("prep");Work("prep");Use("prep");Require(game.chef.held.chopped,"Prepared "+ingredient);}
    static void Salad(bool reverse=false){var counter=game.stations.First(s=>s.kind=="counter");Use("dishes");Use(counter.id);Chop(reverse?"cucumber":"tomato");Use(counter.id);Require(counter.item.contents==(reverse?2:1),"Persistent partial salad state");Chop(reverse?"tomato":"cucumber");Use(counter.id);Require(counter.item.Recipe=="salad","Salad assembled on ordinary worktop");Use(counter.id);}
    static void Soup(){Chop("carrot");Use("pot");Chop("mushroom");Use("pot");var pot=Station("pot");Require(pot.hazard.state==HeatState.Cooking,"Pot starts after two correct ingredients");pot.hazard.Tick(12.05f);Require(pot.hazard.state==HeatState.Ready,"Cooked soup ready");Use("dishes");Use("pot");Require(game.chef.held.Recipe=="soup","One universal dish holds soup");}
    static void CheckBurntCleanup(KitchenStation p){
        var pot=p.potItem;var size=pot.transform.lossyScale;int pool=game.rack.Count;
        Require(p.PotDocked&&p.potContents==12&&p.pot.currentState==6,"Extinguished batch stays visibly burnt in the original pot");
        Approach(p);Require(!game.chef.Interact()&&game.chef.held.kind==ItemKind.Extinguisher,"Extinguisher cannot clear or collect burnt food");Use("extinguisher");
        Use("dishes");Approach(p);Require(!game.chef.Interact()&&game.chef.held.IsEmpty&&p.potContents==12,"Clean plate cannot remove or serve burnt food");Use("dishes");
        Chop("carrot");var carrot=game.chef.held;Approach(p);Require(!game.chef.Interact()&&game.chef.held==carrot&&p.potContents==12,"Fresh ingredients cannot restart an uncleared burnt pot");Use("bin");
        Use("pot");Require(game.chef.held==pot&&!p.PotDocked&&pot.BurntPot&&pot.Recipe==null,"Pick up the same burnt pot with empty paws; stove becomes unavailable");
        Require(Vector3.Distance(size,pot.transform.lossyScale)<.0001f,"Carried pot retains its world size");Require(!game.chef.Throw(),"Cookware cannot be thrown away");Approach(Station("serve"));Require(!game.chef.Interact()&&game.chef.held==pot,"Burnt pot cannot be served");
        var counter=game.stations.First(s=>s.kind=="counter"&&!s.item);Use(counter.id);Require(counter.item==pot&&pot.BurntPot,"Counter placement retains burnt contents and pot identity");
        Chop("carrot");Approach(p);Require(!game.chef.Interact()&&game.chef.held.chopped&&!p.PotDocked,"Empty stove cannot accept ingredients while pot is elsewhere");Use("bin");Use(counter.id);
        Use("pot");Require(p.PotDocked&&p.hazard.state==HeatState.Extinguished&&p.potContents==12,"Returning burnt pot without visiting bin keeps cooking blocked");
        Use("pot");game.Pause();game.Tick(30);Require(game.chef.held==pot&&pot.BurntPot&&!p.PotDocked,"Pause preserves carried pot and its burnt batch");game.Resume();
        Use("bin");Require(game.chef.held==pot&&!pot.BurntPot&&p.hazard.state==HeatState.Empty&&p.potContents==0&&p.pot.currentState==0&&!p.PotDocked,"Bin removes burnt food but keeps the same empty pot in paws");
        Approach(Station("bin"));Require(!game.chef.Interact()&&game.chef.held==pot,"Repeated emptying cannot delete the pot");Use(counter.id);Chop("carrot");Approach(p);Require(!game.chef.Interact()&&!p.PotDocked,"Stove remains unavailable until emptied pot is returned");Use("bin");Use(counter.id);Use("pot");
        Require(p.PotDocked&&p.potItem==pot&&!game.chef.held&&Vector3.Distance(size,pot.transform.lossyScale)<.0001f,"Return the original empty pot at its original size");
        Soup();Require(game.chef.held.Recipe=="soup"&&p.PotDocked&&p.hazard.state==HeatState.Empty,"Cleaned and returned pot cooks a fresh batch successfully");Use("bin");Use("dishes");Require(game.rack.Count==pool,"Cleanup does not consume reusable plates");
    }
    static void AwaitGuest(){for(int i=0;i<800&&game.book.orders.Count==0;i++)game.Tick(.05f);Require(game.book.orders.Count>0,"L"+game.level.id+" guest walked in, sat and ordered");}
    static void CheckRoutes(){
        int cols=game.level.cols,rows=game.level.rows;float m=game.level.module;
        Func<int,int,Vector3> point=(x,z)=>new Vector3(((cols-1)*.5f-x)*m,0,z*m);
        var cells=new Dictionary<Vector2Int,Vector3>();for(int x=0;x<cols;x++)for(int z=0;z<rows;z++){var p=point(x,z);if(Free(p))cells[new Vector2Int(x,z)]=p;}
        var start=cells.OrderBy(k=>Vector3.Distance(k.Value,game.level.spawn)).First().Key;var seen=new HashSet<Vector2Int>{start};var queue=new Queue<Vector2Int>();queue.Enqueue(start);var parent=new Dictionary<Vector2Int,Vector2Int>();
        while(queue.Count>0){var key=queue.Dequeue();foreach(var d in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right}){var next=key+d;if(!cells.ContainsKey(next)||!seen.Add(next))continue;parent[next]=key;queue.Enqueue(next);}}
        foreach(var station in game.stations){var candidates=seen.Where(k=>Vector3.Distance(cells[k],station.transform.position)<1.03f).ToArray();Require(candidates.Length>0,"L"+game.level.id+" connected walk route to "+station.id);}
        var far=seen.OrderByDescending(k=>Vector3.Distance(cells[k],cells[start])).First();var route=new List<Vector2Int>{far};while(route[route.Count-1]!=start)route.Add(parent[route[route.Count-1]]);route.Reverse();game.chef.Warp(cells[start],Vector3.forward);
        foreach(var node in route.Skip(1)){for(int n=0;n<40&&Vector3.Distance(game.chef.transform.position,cells[node])>.05f;n++){var delta=cells[node]-game.chef.transform.position;delta.y=0;game.chef.motor.Move(Vector3.ClampMagnitude(delta,.08f));}Require(Vector3.Distance(game.chef.transform.position,cells[node])<.09f,"L"+game.level.id+" controller follows legal route");}
        if(game.level.id>=3){var target=Station(game.level.id==3?"C4":"C5");Vector3 dir=game.level.id==3?Vector3.forward:Vector3.left;var origin=target.transform.position-dir*m*2;origin.y=0;Require(Free(origin),"Throw origin is walkable");game.chef.Warp(origin,dir);game.chef.Hold(game.NewItem(ItemKind.Ingredient,"tomato"));game.chef.throwTarget=game.FindThrowTarget(origin,dir);Require(game.chef.throwTarget==target,"Ordinary counter targeted across a rail");Require(game.chef.Throw(),"Throw starts and reserves target");for(int n=0;n<60;n++)game.chef.TickThrow(.02f);Require(target.item&&target.item.ingredient=="tomato"&&!target.reserved&&!game.chef.held,"Ingredient crosses rail and occupies one counter slot");game.chef.motor.Move(dir*m*1.5f);Require(Vector3.Dot(game.chef.transform.position-origin,dir)<m,"Controller dash cannot cross rail");UnityEngine.Object.Destroy(target.Take().gameObject);}
    }
    static void ExplorePlate(){
        var plate=game.NewItem(ItemKind.Dish);var tomato=game.NewItem(ItemKind.Ingredient,"tomato");var cucumber=game.NewItem(ItemKind.Ingredient,"cucumber");tomato.prep=.7f;
        Require(plate.Add(tomato)&&plate.Add(cucumber)&&plate.Recipe==null,"Whole tomato and cucumber can share a plate without becoming salad");
        var lifted=plate.TakePortion();Require(lifted==cucumber&&!lifted.chopped&&Mathf.Abs(lifted.transform.lossyScale.x-1)<.001f,"Lifting restores the same raw ingredient and its standalone size");
        var first=plate.TakePortion();Require(first==tomato&&Mathf.Approximately(first.prep,.7f),"Stored ingredients retain their chopping progress");
        first.chopped=true;first.Refresh();lifted.chopped=true;lifted.Refresh();plate.Add(first);plate.Add(lifted);Require(plate.Recipe=="salad"&&plate.portions.Count==2,"Exactly two correct prepared ingredients make salad");
        var carrot=game.NewItem(ItemKind.Ingredient,"carrot");carrot.chopped=true;carrot.Refresh();plate.Add(carrot);Require(plate.Recipe==null&&plate.portions.All(i=>i.gameObject.activeSelf),"Adding another ingredient shows a natural pile and invalidates the recipe");
        Require(plate.TakePortion()==carrot&&plate.Recipe=="salad","Removing the extra ingredient restores the valid recipe");UnityEngine.Object.Destroy(carrot.gameObject);plate.ClearFood();
        foreach(var id in new[]{"carrot","mushroom"}){var piece=game.NewItem(ItemKind.Ingredient,id);piece.chopped=true;piece.Refresh();plate.Add(piece);}
        Require(plate.Recipe==null&&plate.portions.Count==2,"Chopped carrot and mushroom on a plate are not soup");
        if(game.level.id>=2){game.chef.Hold(plate);Use("pot");Require(plate.IsEmpty&&Station("pot").hazard.state==HeatState.Cooking,"A plate can carry prepared soup ingredients and tip them into the pot");game.chef.Release();Station("pot").ResetPot();}
        plate.ClearFood();foreach(var id in new[]{"tomato","tomato","cucumber"}){var piece=game.NewItem(ItemKind.Ingredient,id);piece.chopped=true;plate.Add(piece);}Require(plate.Recipe==null,"Duplicate ingredients cannot exploit the recipe bitmask");plate.ClearFood();plate.FillSoup();var extra=game.NewItem(ItemKind.Ingredient,"mushroom");plate.Add(extra);Require(plate.Recipe==null,"Extra raw food can sit on soup without becoming a valid order");Require(plate.TakePortion()==extra&&plate.Recipe=="soup","Removing the extra ingredient restores soup");UnityEngine.Object.Destroy(extra.gameObject);UnityEngine.Object.Destroy(plate.gameObject);
        var counter=game.stations.First(i=>i.kind=="counter");var one=game.NewItem(ItemKind.Ingredient,"carrot");var two=game.NewItem(ItemKind.Ingredient,"mushroom");game.chef.Hold(one);Approach(counter);Require(game.chef.Interact(),"Raw carrot can be left on a normal worktop");game.chef.Hold(two);Require(game.chef.Interact()&&counter.items.Count==2,"Raw ingredients can stack on an occupied worktop");Require(counter.Take()==two&&counter.Take()==one,"Worktop stack is reversible in order");UnityEngine.Object.Destroy(one.gameObject);UnityEngine.Object.Destroy(two.gameObject);
    }
    static void CheckCamera(){
        float preferred=game.Settings.cameraTilt;var position=game.chef.transform.position;var movementAxis=Vector3.ProjectOnPlane(game.cam.transform.forward,Vector3.up).normalized;
        foreach(float angle in new[]{35f,60f,80f}){
            game.view.SetAngle(angle,true);var bounds=game.view.RoomViewportBounds();
            Require(bounds.xMin>=0&&bounds.yMin>=0&&bounds.xMax<=1&&bounds.yMax<=1,"L"+game.level.id+" complete room fits at camera tilt "+angle);
            Require(Vector3.Dot(movementAxis,Vector3.ProjectOnPlane(game.cam.transform.forward,Vector3.up).normalized)>.999f&&game.chef.transform.position==position,"L"+game.level.id+" tilting preserves movement axes and chef position at "+angle);
        }
        game.view.SetAngle(150,true);Require(game.view.Angle==80,"Camera cannot flip past overhead");game.view.SetAngle(-40,true);Require(game.view.Angle==35,"Camera cannot fall below the playable angle");game.view.SetAngle(preferred,true);
        float preferredYaw=game.Settings.cameraYaw;
        foreach(float yaw in new[]{-80f,0f,80f})foreach(float tilt in new[]{35f,80f}){
            game.view.SetYaw(yaw,true);game.view.SetAngle(tilt,true);var bounds=game.view.RoomViewportBounds();
            Require(bounds.xMin>=0&&bounds.yMin>=0&&bounds.xMax<=1&&bounds.yMax<=1,"L"+game.level.id+" room fits horizontal angle "+yaw+" at tilt "+tilt);
            Require(game.chef.transform.position==position,"L"+game.level.id+" horizontal rotation leaves chef in place");
        }
        game.view.SetYaw(-150,true);Require(game.view.Yaw==-80,"Horizontal rotation stops before the back wall blocks the kitchen");game.view.SetYaw(150,true);Require(game.view.Yaw==80,"Opposite horizontal limit keeps the open side visible");game.view.SetYaw(preferredYaw,true);game.view.SetAngle(preferred,true);
    }
    static IEnumerator Run(){
        for(int i=0;i<8;i++)yield return null;
        Require(game.phase==SessionPhase.Menu,"Main menu loads");
        Require(QualitySettings.antiAliasing==4,"Web and native quality use 4x MSAA");
        Require(game.catalog.audio.Length>=18&&game.catalog.audio.All(a=>a.clip&&a.clip.length>0),"Every music and action sound resolves to an imported clip");
        Require(game.sound.MusicPlaying&&game.sound.ClipCount>=18,"Calm background music starts");
        var testDish=game.NewItem(ItemKind.Dish);float dishSize=testDish.transform.lossyScale.x;
        var scaled=new GameObject("Scale regression parent");scaled.transform.localScale=Vector3.one*1.15f;
        bool stable=true;foreach(var variant in game.catalog.chefs){var chef=UnityEngine.Object.Instantiate(variant.prefab);var anchor=chef.GetComponent<KitchenCharacterAnimator>().carryAnchor;for(int transfer=0;transfer<20;transfer++){testDish.Attach(scaled.transform,Vector3.zero);stable&=Mathf.Abs(testDish.transform.lossyScale.x-dishSize)<.0001f;testDish.Attach(anchor,Vector3.zero);stable&=Mathf.Abs(testDish.transform.lossyScale.x-dishSize)<.0001f;}testDish.Attach(game.live.transform,Vector3.zero);UnityEngine.Object.Destroy(chef);}
        for(int state=0;state<6;state++){testDish.visual.SetState(state);stable&=Mathf.Abs(testDish.transform.lossyScale.x-dishSize)<.0001f;}
        Require(stable,"Dish size stays fixed through 20 repeated table/hand transfers for every chef and all six states");UnityEngine.Object.Destroy(testDish.gameObject);UnityEngine.Object.Destroy(scaled);
        string oldSettings="{\"best\":[0,0,0,0,0,0,0,0],\"stars\":[0,0,0,0,0,0,0,0],\"settings\":{\"volume\":0.4,\"chef\":0}}";PlayerPrefs.SetString("BaraKitchen.Validation.v1",oldSettings);game.Load();Require(Mathf.Approximately(game.Settings.musicVolume,.65f)&&Mathf.Approximately(game.Settings.effectsVolume,.85f)&&Mathf.Approximately(game.Settings.volume,.4f),"Old saves receive new music/effects defaults without changing master volume");game.Settings.musicVolume=0;game.Save();game.Load();Require(game.Settings.musicVolume==0,"An intentionally muted music setting survives reload");game.Settings.musicVolume=.65f;
        Require(game.Settings.cameraTilt==KitchenCameraController.DefaultTilt,"Old saves get the more overhead camera default");game.view.SetAngle(73,true);game.Save();game.Load();Require(Mathf.Approximately(game.view.Angle,73),"Chosen camera tilt survives save reload");game.view.SetAngle(KitchenCameraController.DefaultTilt,true);
        Require(Mathf.Approximately(game.Settings.cameraYaw,KitchenCameraController.DefaultYaw),"Old camera saves retain the existing horizontal angle");game.view.SetYaw(0,true);game.Save();game.Load();Require(game.view.Yaw==0,"A front-facing zero-degree rotation survives save reload");game.view.SetYaw(KitchenCameraController.DefaultYaw,true);
        var b=new OrderBook();var a=b.Add("salad",100);var z=b.Add("soup",100);Require(b.Serve("salad")==a&&b.score==80&&b.streak==1,"Correct delivery scores base and patience tip");Require(b.Serve("salad")==null&&b.score==80,"Duplicate/wrong delivery cannot rescore");Require(b.Serve("soup")==z&&b.streak==2,"Oldest-order streak increases");b.Add("salad",1);b.Tick(2);Require(b.missed==1&&b.streak==0,"Expired order penalizes once and resets streak");int old=b.score;b.Tick(100);Require(b.score==old&&b.missed==1,"Expiry is idempotent");
        for(int level=1;level<=4;level++){
            game.Brief(level);yield return null;game.StartService();game.chef.enabled=false;Physics.SyncTransforms();
            CheckCamera();ExplorePlate();CheckRoutes();foreach(var station in game.stations)Approach(station);Require(true,"L"+level+" every station has a valid physical approach");
            Require(game.sound.DesiredMode()=="service","Normal service selects calm music");float time=game.remaining;game.Tick(2);Require(game.remaining==time,"L"+level+" tutorial freezes round clock");AwaitGuest();
            int pool=game.level.dishes; if(level==2)Soup();else Salad(level==3);Use("serve");Require(game.book.served==1&&!game.training,"L"+level+" first delivery starts timed service");Require(game.guests.Any(g=>g.phase==GuestPhase.Eating),"L"+level+" served customer begins eating");
            if(level==2)Require(game.DashAllowed,"Dash introduced after first soup");
            game.Pause();yield return null;yield return null;Require(!game.sound.MusicPlaying&&!game.sound.WorkPlaying,"Pause stops music and action audio");time=game.remaining;var positions=game.guests.Select(g=>g.transform.position).ToArray();game.Tick(10);Require(game.remaining==time&&game.guests.Select((g,i)=>g.transform.position==positions[i]).All(x=>x),"L"+level+" pause freezes timers and guests");game.Resume();
            for(int i=0;i<150;i++)game.Tick(.05f);Require(game.returns.Count==1,"L"+level+" eating generates one dirty return");Use("return");Use("sink");Approach(Station("sink"));Require(game.chef.BeginWork(),"Tap starts washing");game.chef.TickWork(.5f);Require(game.sound.WorkPlaying,"Washing water loops during work");game.chef.CancelWork();Require(!game.sound.WorkPlaying&&Station("sink").washing>=.5f,"Cancel stops wash audio and preserves completed work");Work("sink");Require(game.chef.held&&!game.chef.held.dirty,"L"+level+" washing hands back a clean dish");Use("dishes");Require(game.rack.Count==pool,"L"+level+" finite dish pool conserved");
            for(int i=0;i<500;i++)game.Tick(.05f);Require(game.guests.All(g=>g.phase!=GuestPhase.Leaving),"L"+level+" departing guests reach exit and free seats");
            if(level>=2){Chop("carrot");Use("pot");Chop("mushroom");Use("pot");var p=Station("pot");p.hazard.Tick(17);Require(p.hazard.state==HeatState.Warning,"L"+level+" pre-fire warning");p.hazard.Tick(7.99f);Require(p.hazard.state==HeatState.Warning,"Full eight-second response window");p.hazard.Tick(.02f);Require(p.hazard.state==HeatState.Burning,"L"+level+" warning transitions to fire");yield return null;yield return null;Require(game.sound.mode=="fire"&&game.sound.MusicPlaying,"Fire music crossfade starts in the running audio system");Require(game.sound.DesiredMode()=="fire","Fire takes priority in music state");float previousTime=game.remaining;game.remaining=20;Require(game.sound.DesiredMode()=="fire","Fire music has priority over final countdown");game.remaining=previousTime;Use("extinguisher");Approach(p);Require(game.chef.BeginWork(),"Extinguisher action starts");for(int i=0;i<200&&game.chef.working;i++){game.chef.TickWork(.03f);yield return null;}Require(p.hazard.state==HeatState.Extinguished,"Real aimed spray extinguishes fire");CheckBurntCleanup(p);}
            var bad=game.NewItem(ItemKind.Dish);game.chef.Hold(bad);int before=game.book.score;Approach(Station("serve"));Require(!game.chef.Interact()&&game.chef.held==bad&&game.book.score==before,"L"+level+" invalid dish preserved at service");game.rack.Add(game.chef.Release());game.ArrangeDishes();
            if(game.book.orders.Count==0)AwaitGuest();var exp=game.book.orders[0];exp.remaining=.01f;game.Tick(.02f);Require(!exp.live&&game.book.missed>0,"L"+level+" order expiry dismisses guest");
            game.remaining=20;yield return null;yield return null;Require(game.sound.mode=="hurry"&&game.sound.MusicPlaying,"Hurry music crossfade starts in the running audio system");Require(game.sound.DesiredMode()=="hurry","Final 30 seconds select hurry music");game.remaining=.01f;game.Tick(.02f);Require(game.phase==SessionPhase.Results,"L"+level+" overall timer reaches results");int total=game.book.score;game.EndRound();game.Tick(10);Require(game.book.score==total,"L"+level+" results settle exactly once");
            game.chef.enabled=true;yield return null;
        }
        game.Load();Require(game.save.best.Any(s=>s>0),"Best scores survive save reload");game.ShowMenu();yield return null;
    }
}
