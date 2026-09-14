using UnityEngine;
namespace BaraKitchen.Gameplay {
    // An outlined worktop marker: animation stays in the shader, leaving the item visible.
    public sealed class StationHighlight : MonoBehaviour {
        MaterialPropertyBlock block; Renderer surface; Color last; bool service,still,initialized;
        public void Configure(bool serve,bool reduced,Color color){
            if(initialized&&service==serve&&still==reduced&&last==color)return;
            if(!surface)surface=GetComponent<Renderer>();if(block==null)block=new MaterialPropertyBlock();
            service=serve;still=reduced;last=color;initialized=true;
            block.SetColor("_Color",color);block.SetFloat("_Service",serve?1:0);block.SetFloat("_Still",reduced?1:0);surface.SetPropertyBlock(block);
        }
    }
}
