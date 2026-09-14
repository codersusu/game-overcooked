#!/usr/bin/env python3
import json,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/production/round-01'
def glb_stats(path):
 d=path.read_bytes();n=struct.unpack_from('<I',d,12)[0];g=json.loads(d[20:20+n]);tri=sum(g['accessors'][p['indices']]['count']//3 for m in g.get('meshes',[]) for p in m['primitives'] if 'indices'in p);return tri
rooms=[]
descs=["An open U-shaped kitchen for learning the first salad and dish-washing loop.","A central work island separates chopping from cooking. Choose your route around it.","Preparation and service sit across a low rail. The walking passage stays on the left.","Three work areas with offset passages. Ingredients can cross two rails while the chef follows the zigzag route."]
for i in range(1,5):
 scene=json.loads((OUT/('level-%02d.scene.json'%i)).read_text());pop='level-%02d-populated.glb'%i
 rooms.append(dict(id=str(i),title=scene['name'],subtitle=scene['shape'],glb=pop if (OUT/pop).exists() else 'level-%02d.glb'%i,still='level-%02d-unity.png'%i,source='../../concepts/levels-round-02/level-%02d.svg'%i,description=descs[i-1]+' The adjoining café now has 40% more depth, with tables, chairs, table flowers and colourful planters. Kitchen worktops are now about 32% lower so food and tools sit below the animals’ heads.',stats=[[len(scene['seats']),'Customer seats'],[scene['kitchenRules']['freeCounterCount'],'Free counters']],note='The dining area has a wider front aisle and the same seat count. Native Unity scenes include chef and seated customer idle loops. The full customer navigation and service loop comes later.'))
chars=[]
for species in ('capybara','cat','dog'):
 for outfit in ('male-chef','female-chef','male-customer','female-customer'):
  id=species+'-'+outfit;base=OUT/'characters'/id;model=base/'model/model.glb'
  if not model.exists():continue
  motions=[]
  for label,name in [('Walk','walking_glb.glb'),('Run','running_glb.glb')]:
   path=base/'motion-preview'/name
   if path.exists():motions.append({'label':label+' · automatic rig test','url':str(path.relative_to(OUT))})
  chars.append(dict(id=id,title=species.capitalize(),species=species,subtitle=outfit.replace('-',' ').title(),glb=str(model.relative_to(OUT)),thumbnail='characters/'+id+'/model/preview-front.png',still='characters/'+id+'/input.png',source='../../concepts/roster-round-02/'+species+'.png',description='A first textured 3D model from the approved four-look roster. Oversized animal head, short limbs, connected paws and a distinct outfit.',stats=[[format(glb_stats(model),','),'Triangles'],['2K','Colour texture']],motions=motions,note='Costume is baked into this model. Separate wardrobe pieces and final deformation weights remain future work. Unity includes the shared native animation set; review it in the animation gallery.'))
props=[]
catalog=json.loads((OUT/'model-catalog.json').read_text())
for m in catalog['models']:
 name=m['name'];note='Reusable native Unity prefab plus GLB and OBJ mesh sources.'
 if name=='cafe-bench':continue  # Retain the source asset, but retire it from the active café design.
 if m['category']=='Decor':note='Rounded 3D flowers and café decorations. Placed on dining tables, windowsills and room edges, away from usable kitchen worktops.'
 if m['category']=='Food':note='Whole and chopped meshes also share a persistent Ingredient prefab root in Unity.'
 if m['category']=='Dishes':note='The same universal dish is used for salads and soup. The Unity dish prefab exposes clean, partial, finished and dirty visual states.'
 if name=='counter':note='A generic free worktop. Any empty counter can hold a dish or ingredients; there is no dedicated salad station.'
 if name=='fire-extinguisher':note='A rounded red extinguisher with named grip and nozzle attachments. Unity includes an aimed spray effect and cooking-fire suppression component.'
 if name=='rail':note='Blocks walking and has no worktop slot. Ingredient throws may pass over it in gameplay.'
 props.append(dict(id=name,title=name.replace('-',' ').title(),category=m['category'],glb='models/'+name+'.glb',still='foods-unity.png',source='../../concepts/kitchen-round-01/',description=note,stats=[[format(m['triangles'],','),'Triangles'],[len(m['anchors']),'Named anchors']],note='Initial stylized mesh pass. Dimensions and contact points will be tuned during playtests.'))
(OUT/'review-data.json').write_text(json.dumps({'rooms':rooms,'characters':chars,'props':props},indent=2)+'\n')
print('Review catalog:',len(rooms),'rooms,',len(chars),'characters,',len(props),'props')
