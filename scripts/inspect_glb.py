#!/usr/bin/env python3
"""Inspect actual GLB geometry, skin bindings, and animation tracks without a DCC app."""
import hashlib
import json
import math
import struct
import sys
from pathlib import Path

COMPONENTS = {5120: ('b',1),5121: ('B',1),5122: ('h',2),5123: ('H',2),5125: ('I',4),5126: ('f',4)}
WIDTHS = {'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}


def inspect(path):
    raw=path.read_bytes()
    magic,version,length=struct.unpack_from('<4sII',raw)
    if magic!=b'glTF' or version!=2 or length!=len(raw):
        raise ValueError('Invalid GLB header: '+str(path))
    chunks={}
    offset=12
    while offset<len(raw):
        size,kind=struct.unpack_from('<II',raw,offset)
        chunks[kind]=raw[offset+8:offset+8+size]
        offset+=8+size
    doc=json.loads(chunks[0x4E4F534A])
    binary=chunks.get(0x004E4942,b'')
    accessors=doc.get('accessors',[])

    def values(index):
        a=accessors[index]
        if 'bufferView' not in a: return []
        view=doc['bufferViews'][a['bufferView']]
        if view.get('buffer',0)!=0 or a.get('sparse'): return []
        fmt,size=COMPONENTS[a['componentType']]
        width=WIDTHS[a['type']]
        stride=view.get('byteStride',size*width)
        start=view.get('byteOffset',0)+a.get('byteOffset',0)
        rows=[struct.unpack_from('<'+fmt*width,binary,start+i*stride) for i in range(a['count'])]
        if a.get('normalized') and a['componentType'] in (5121,5123):
            divisor=255 if a['componentType']==5121 else 65535
            rows=[tuple(x/divisor for x in row) for row in rows]
        return rows

    primitives=[p for m in doc.get('meshes',[]) for p in m['primitives']]
    vertices=sum(accessors[p['attributes']['POSITION']]['count'] for p in primitives)
    triangles=sum((accessors[p['indices']]['count'] if 'indices' in p else accessors[p['attributes']['POSITION']]['count'])//3 for p in primitives if p.get('mode',4)==4)
    animations=[]
    for animation in doc.get('animations',[]):
        times=[v[0] for sampler in animation['samplers'] for v in values(sampler['input'])]
        animations.append({'name':animation.get('name',''), 'channels':len(animation['channels']),
            'duration_seconds':max(times)-min(times) if times else None,
            'target_properties':sorted({c['target']['path'] for c in animation['channels']}),
            'animated_nodes':len({c['target'].get('node') for c in animation['channels']})})
    skins=[]
    for skin in doc.get('skins',[]):
        skins.append({'joint_count':len(skin['joints']),
            'joint_names':[doc['nodes'][j].get('name',str(j)) for j in skin['joints']],
            'inverse_bind_matrix_count':accessors[skin['inverseBindMatrices']]['count'] if 'inverseBindMatrices' in skin else None})
    weight_errors=[]
    for p in primitives:
        attrs=p['attributes']
        if 'WEIGHTS_0' in attrs:
            rows=values(attrs['WEIGHTS_0'])
            extra=values(attrs['WEIGHTS_1']) if 'WEIGHTS_1' in attrs else None
            for i,row in enumerate(rows):
                total=sum(row)+(sum(extra[i]) if extra else 0)
                if not math.isfinite(total) or abs(total-1)>0.02: weight_errors.append(i)
    return {'file':str(path),'bytes':len(raw),'sha256':hashlib.sha256(raw).hexdigest(),
        'mesh_count':len(doc.get('meshes',[])),'primitive_count':len(primitives),
        'vertices_across_primitives':vertices,'triangles':triangles,
        'materials':len(doc.get('materials',[])),'embedded_images':sum('bufferView' in i for i in doc.get('images',[])),
        'skins':skins,'animations':animations,'vertices_with_invalid_weight_sum':len(weight_errors),
        'extensions_used':doc.get('extensionsUsed',[])}


if __name__=='__main__':
    root=Path(sys.argv[1])
    reports=[inspect(p) for p in sorted(root.glob('*/*.glb'))]
    (root/'glb-inspection.json').write_text(json.dumps(reports,indent=2)+'\n')
    for r in reports:
        print(json.dumps({k:r[k] for k in ('file','triangles','materials','embedded_images','animations','vertices_with_invalid_weight_sum')} | {'skin_joint_counts':[s['joint_count'] for s in r['skins']]}))
