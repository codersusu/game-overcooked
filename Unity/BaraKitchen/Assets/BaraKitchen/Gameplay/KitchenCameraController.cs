using System.Collections.Generic;
using UnityEngine;

namespace BaraKitchen.Gameplay {
    // Player-controlled viewing angles with automatic room framing.
    [DefaultExecutionOrder(-50)]
    public sealed class KitchenCameraController : MonoBehaviour {
        public const float MinTilt=35f, MaxTilt=80f, DefaultTilt=60f;
        // Stay on the open side of the cafe so the solid back wall cannot hide the worktops.
        public const float MinYaw=-80f, MaxYaw=80f, DefaultYaw=-25.201124f;
        public KitchenGame game;
        public float Angle { get; private set; }=DefaultTilt;
        public float Yaw { get; private set; }=DefaultYaw;
        float velocity,yawVelocity,saveAfter,lastPointerX;
        int dragButton=-1;
        bool pendingSave;
        GameObject framedRoom;
        readonly List<Vector3> corners=new List<Vector3>();

        public void SnapToPreference(){
            if(!game||game.save==null)return;
            Angle=game.Settings.cameraTilt;Yaw=game.Settings.cameraYaw;velocity=0;yawVelocity=0;dragButton=-1;pendingSave=false;
            if(game.level!=null&&game.cam)Apply();
        }
        public void SetAngle(float degrees,bool immediate=false){
            if(float.IsNaN(degrees)||float.IsInfinity(degrees))return;
            float target=Mathf.Clamp(degrees,MinTilt,MaxTilt);
            if(!Mathf.Approximately(target,game.Settings.cameraTilt)){
                game.Settings.cameraTilt=target;pendingSave=true;saveAfter=Time.unscaledTime+.8f;
            }
            if(immediate){Angle=target;velocity=0;Apply();}
        }
        public void SetYaw(float degrees,bool immediate=false){
            if(float.IsNaN(degrees)||float.IsInfinity(degrees))return;
            float target=Mathf.Clamp(degrees,MinYaw,MaxYaw);
            if(!Mathf.Approximately(target,game.Settings.cameraYaw)){
                game.Settings.cameraYaw=target;pendingSave=true;saveAfter=Time.unscaledTime+.8f;
            }
            if(immediate){Yaw=target;yawVelocity=0;Apply();}
        }
        public void ResetView(){SetAngle(DefaultTilt);SetYaw(DefaultYaw);}
        void Update(){
            if(!game||game.phase!=SessionPhase.Service){dragButton=-1;return;}
            bool overScene=game.cam.pixelRect.Contains(Input.mousePosition);
            bool modified=Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl)||Input.GetKey(KeyCode.LeftCommand)||Input.GetKey(KeyCode.RightCommand);
            if(overScene&&!modified){float scroll=Input.mouseScrollDelta.y;if(Mathf.Abs(scroll)>.001f)SetAngle(game.Settings.cameraTilt+scroll*3f);}
            if(overScene&&!modified&&(Input.GetMouseButtonDown(0)||Input.GetMouseButtonDown(1))){dragButton=Input.GetMouseButtonDown(0)?0:1;lastPointerX=Input.mousePosition.x;}
            if(dragButton>=0){
                if(!overScene||modified||!Input.GetMouseButton(dragButton))dragButton=-1;
                else{float delta=Input.mousePosition.x-lastPointerX;lastPointerX=Input.mousePosition.x;SetYaw(game.Settings.cameraYaw-delta/Mathf.Max(1,Screen.width)*180f);}
            }
            float turn=(Input.GetKey(KeyCode.Comma)?1:0)-(Input.GetKey(KeyCode.Period)?1:0);
            if(turn!=0&&!modified)SetYaw(game.Settings.cameraYaw+turn*48f*Time.unscaledDeltaTime);
            float direction=(Input.GetKey(KeyCode.RightBracket)?1:0)-(Input.GetKey(KeyCode.LeftBracket)?1:0);
            if(direction!=0)SetAngle(game.Settings.cameraTilt+direction*28f*Time.unscaledDeltaTime);
            if(Input.GetKeyDown(KeyCode.C))ResetView();
        }
        void LateUpdate(){
            if(!game||game.save==null)return;
            if(game.phase==SessionPhase.Service&&(Mathf.Abs(Angle-game.Settings.cameraTilt)>.001f||Mathf.Abs(Yaw-game.Settings.cameraYaw)>.001f)){
                Angle=Mathf.SmoothDamp(Angle,game.Settings.cameraTilt,ref velocity,.14f,120f,Time.unscaledDeltaTime);
                Yaw=Mathf.SmoothDamp(Yaw,game.Settings.cameraYaw,ref yawVelocity,.10f,240f,Time.unscaledDeltaTime);
                if(Mathf.Abs(Angle-game.Settings.cameraTilt)<.01f){Angle=game.Settings.cameraTilt;velocity=0;}
                if(Mathf.Abs(Yaw-game.Settings.cameraYaw)<.01f){Yaw=game.Settings.cameraYaw;yawVelocity=0;}
                Apply();
            }
            if(pendingSave&&Time.unscaledTime>=saveAfter)FlushPreference();
        }
        void FlushPreference(){if(pendingSave&&game&&game.save!=null){pendingSave=false;game.Save();}}
        void OnApplicationFocus(bool focused){if(!focused)FlushPreference();}
        void OnApplicationPause(bool paused){if(paused)FlushPreference();}
        void OnDisable(){FlushPreference();}

        public void Apply(){
            if(!game||game.level==null||!game.cam)return;
            var cam=game.cam;float radians=Angle*Mathf.Deg2Rad,yaw=Yaw*Mathf.Deg2Rad;var target=game.level.cameraTarget;
            var horizontal=new Vector3(Mathf.Sin(yaw),0,Mathf.Cos(yaw));
            cam.orthographic=true;cam.allowMSAA=true;cam.renderingPath=RenderingPath.Forward;
            cam.transform.position=target+(horizontal*Mathf.Cos(radians)+Vector3.up*Mathf.Sin(radians))*24f;
            cam.transform.LookAt(target);cam.backgroundColor=new Color(.79f,.84f,.66f);cam.nearClipPlane=.1f;cam.farClipPlane=80;
            FitScene();
        }
        void CacheRoom(){
            if(framedRoom==game.room&&corners.Count>0)return;
            framedRoom=game.room;corners.Clear();if(!framedRoom)return;
            // Cache static geometry once per room; moving food and fire must not make the camera breathe.
            foreach(var renderer in framedRoom.GetComponentsInChildren<Renderer>()){
                if(renderer is ParticleSystemRenderer||renderer.GetComponent<StationHighlight>())continue;
                var b=renderer.bounds;
                for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)
                    corners.Add(new Vector3(x==0?b.min.x:b.max.x,y==0?b.min.y:b.max.y,z==0?b.min.z:b.max.z));
            }
        }
        public void FitScene(){
            if(!game.room||!game.cam)return;CacheRoom();if(corners.Count==0)return;
            var cam=game.cam;float minX=float.PositiveInfinity,maxX=float.NegativeInfinity,minY=float.PositiveInfinity,maxY=float.NegativeInfinity;
            var matrix=cam.transform.worldToLocalMatrix;
            foreach(var world in corners){var p=matrix.MultiplyPoint3x4(world);minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);}
            cam.transform.position+=cam.transform.right*((minX+maxX)/2)+cam.transform.up*((minY+maxY)/2);
            cam.orthographicSize=Mathf.Max((maxY-minY)/2,(maxX-minX)/(2*cam.aspect))*1.025f;
        }
        public Rect RoomViewportBounds(){
            CacheRoom();var min=Vector2.one*float.PositiveInfinity;var max=Vector2.one*float.NegativeInfinity;
            foreach(var world in corners){var p=(Vector2)game.cam.WorldToViewportPoint(world);min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
    }
}
