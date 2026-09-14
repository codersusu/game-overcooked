#!/usr/bin/env python3
"""Append the exact Unity-baked customer/chef preview poses to the editable room GLBs."""
import json,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'art/production/round-01'
def read_glb(path):
 data=path.read_bytes();jlen=struct.unpack_from('<I',data,12)[0];doc=json.loads(data[20:20+jlen]);start=20+jlen;blen=struct.unpack_from('<I',data,start)[0];return doc,bytearray(data[start+8:start+8+blen])
def write_glb(path,doc,binary):
 doc['buffers'][0]['byteLength']=len(binary);js=json.dumps(doc,separators=(',',':')).encode();js+=b' '*((-len(js))%4);binary+=b'\0'*((-len(binary))%4)
 path.write_bytes(struct.pack('<4sII',b'glTF',2,28+len(js)+len(binary))+struct.pack('<I4s',len(js),b'JSON')+js+struct.pack('<I4s',len(binary),b'BIN\0')+binary)
for level in range(1,5):
 source=OUT/('level-%02d.glb'%level);poses=OUT/('level-%02d.characters.json'%level)
 if not poses.exists():continue
 doc,data=read_glb(source);chars=json.loads(poses.read_text())['characters'];texcache={}
 def buffer(raw):
  data.extend(b'\0'*((-len(data))%4));start=len(data);data.extend(raw);doc['bufferViews'].append({'buffer':0,'byteOffset':start,'byteLength':len(raw)});return len(doc['bufferViews'])-1
 def acc(values,width,fmt='f',ctype=5126):
  view=buffer(struct.pack('<'+fmt*len(values),*values));a={'bufferView':view,'componentType':ctype,'count':len(values)//width,'type':{1:'SCALAR',2:'VEC2',3:'VEC3'}[width]}
  if width==3:a['min']=[min(values[i::3]) for i in range(3)];a['max']=[max(values[i::3]) for i in range(3)]
  doc['accessors'].append(a);return len(doc['accessors'])-1
 for ch in chars:
  id=ch['id']
  if id not in texcache:
   original,binary=read_glb(OUT/'characters'/id/'model/model.glb');im=original['images'][0];view=original['bufferViews'][im['bufferView']];raw=binary[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']]
   imageindex=len(doc.setdefault('images',[]));doc['images'].append({'bufferView':buffer(raw),'mimeType':im.get('mimeType','image/png')})
   textureindex=len(doc.setdefault('textures',[]));doc['textures'].append({'source':imageindex})
   matindex=len(doc['materials']);doc['materials'].append({'name':id,'pbrMetallicRoughness':{'baseColorTexture':{'index':textureindex},'baseColorFactor':[1,1,1,1],'metallicFactor':0,'roughnessFactor':.88}});texcache[id]=matindex
  vertices=ch['vertices'];normals=ch['normals'];uv=ch['uv'];indices=ch['triangles']
  for i in range(2,len(vertices),3):vertices[i]*=-1;normals[i]*=-1
  # Unity UV convention +Y up versus glTF texture coordinates +Y down.
  for i in range(1,len(uv),2):uv[i]=1-uv[i]
  for i in range(0,len(indices),3):indices[i],indices[i+2]=indices[i+2],indices[i]
  primitive={'attributes':{'POSITION':acc(vertices,3),'NORMAL':acc(normals,3),'TEXCOORD_0':acc(uv,2)},'indices':acc(indices,1,'I',5125),'material':texcache[id]}
  mi=len(doc['meshes']);doc['meshes'].append({'name':id,'primitives':[primitive]});ni=len(doc['nodes']);doc['nodes'].append({'name':id+' (Unity preview pose)','mesh':mi});doc['scenes'][0]['nodes'].append(ni)
 target=OUT/('level-%02d-populated.glb'%level);write_glb(target,doc,data);print(target.name,len(chars),'characters',target.stat().st_size,'bytes')
