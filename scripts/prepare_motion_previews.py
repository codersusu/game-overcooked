#!/usr/bin/env python3
"""Keep Meshy originals; remove emissive/metallic defaults in local motion-review copies."""
import json,struct
from pathlib import Path
OUT=Path(__file__).resolve().parents[1]/'art/production/round-01'
for src in sorted((OUT/'characters').glob('*/rig/*_glb.glb')):
 if src.name not in ('walking_glb.glb','running_glb.glb','rigged_character_glb.glb'):continue
 raw=src.read_bytes();size=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+size]);remaining=raw[20+size:]
 for mat in doc.get('materials',[]):
  mat.pop('emissiveTexture',None);mat['emissiveFactor']=[0,0,0];mat.pop('extensions',None)
  pbr=mat.setdefault('pbrMetallicRoughness',{});pbr['metallicFactor']=0;pbr['roughnessFactor']=.88;pbr['baseColorFactor']=[1,1,1,1]
 doc['extensionsUsed']=[e for e in doc.get('extensionsUsed',[]) if e not in ('KHR_materials_specular','KHR_materials_ior')]
 js=json.dumps(doc,separators=(',',':')).encode();js+=b' '*((-len(js))%4)
 dst=src.parent.parent/'motion-preview'/src.name;dst.parent.mkdir(exist_ok=True);dst.write_bytes(struct.pack('<4sII',b'glTF',2,20+len(js)+len(remaining))+struct.pack('<I4s',len(js),b'JSON')+js+remaining)
 print(str(dst.relative_to(OUT)))
