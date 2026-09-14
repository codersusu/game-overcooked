#!/usr/bin/env python3
"""Geometry, scene correspondence and conservative café circulation checks."""
import json,math,sys,collections
from pathlib import Path
from inspect_glb import inspect
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/production/round-01'
errors=[];reports=[]
for p in sorted((OUT/'models').glob('*.mesh.json')):
 d=json.loads(p.read_text())
 for part in d['parts']:
  n=len(part['vertices'])//3
  if len(part['normals'])!=n*3 or any(not math.isfinite(v) for v in part['vertices']+part['normals']):errors.append(p.name+': invalid attributes')
  if len(part['triangles'])%3 or any(i<0 or i>=n for i in part['triangles']):errors.append(p.name+': invalid indices')
 reports.append(inspect(p.with_name(p.name.replace('.mesh.json','.glb'))))
for p in sorted((OUT/'characters').glob('*/model/model.glb')):reports.append(inspect(p))
for p in sorted((OUT/'characters').glob('*/motion-preview/*glb')):
 r=inspect(p);reports.append(r)
 if not r['skins'] or r['vertices_with_invalid_weight_sum']:errors.append(str(p.relative_to(OUT))+': invalid skin')
 if p.name.startswith(('walking','running')) and not r['animations']:errors.append(str(p.relative_to(OUT))+': missing motion')
levels=json.loads((ROOT/'art/concepts/levels-round-02/levels.json').read_text());room_reports=[]
for level in levels:
 room=json.loads((OUT/('level-%02d.scene.json'%level['id'])).read_text());actual={s['name']:s for s in room['instances'] if s.get('station')}
 for s in level['stations']:
  a=actual.get(s['id']);expected=[-(s['x']-(level['cols']-1)/2)*room['module'],0,s['y']*room['module']]
  if not a or a['kind']!=s['kind'] or max(abs(x-y) for x,y in zip(a['position'],expected))>1e-5:errors.append('Level %d station %s differs from approved grid'%(level['id'],s['id']))
 if len(actual)!=len(level['stations']):errors.append('Unexpected station count')
 seats={s['name']:s for s in room['seats']}
 for inst in room['instances']:
  if inst['name'].startswith('DiningDish_') and inst['asset'].removeprefix('dish-') not in level['recipes']:errors.append('Dining meal not available in level '+str(level['id']))
 for ch in room['characters']:
  if ch['pose']=='seated':
   s=seats[ch['seat']]
   dish=next((i for i in room['instances'] if i['name']=='DiningDish_'+s['name']),None)
   if not dish or dish['position']!=s['dishPosition']:errors.append('Missing seated customer meal: '+ch['seat'])
   if ch['position']!=s['position'] or ch['yaw']!=s['yaw']:errors.append('Seat/character transform mismatch: '+ch['id'])
   nearest=min((s['position'][0]-i['position'][0])**2+(s['position'][2]-i['position'][2])**2 for i in room['instances'] if i['asset']=='cafe-chair')
   if nearest>1e-8:errors.append('Seat without matching chair: '+s['name'])
   dx=s['dishPosition'][0]-s['position'][0];dz=s['dishPosition'][2]-s['position'][2];yaw=math.radians(s['yaw'])
   if dx*math.sin(yaw)+dz*math.cos(yaw)<=0:errors.append('Customer faces away from table: '+s['name'])
 # 10cm sampling with a .22m radius, rectangular prop footprint expansion.
 obstacles=[]
 for inst in room['instances']:
  model=json.loads((OUT/'models'/(inst['asset']+'.mesh.json')).read_text());col=model.get('collider')
  if not col:continue
  x,_,z=inst['position'];w,h,d=col['size'];scale=inst.get('scale',[1,1,1]);w*=scale[0];d*=scale[2]
  if abs(round(inst.get('yaw',0))%180)==90:w,d=d,w
  obstacles.append((x-w/2-.22,x+w/2+.22,z-d/2-.22,z+d/2+.22))
 xmin,xmax=-room['cols']*room['module']/2+.23,room['cols']*room['module']/2-.23;zmin=(room['rows']-.15)*room['module']+.32;zmax=(room['rows']+room['cafeRows']-.5)*room['module']-.23
 step=.10;nx=int((xmax-xmin)/step)+1;nz=int((zmax-zmin)/step)+1
 def pt(cell):return (xmin+cell[0]*step,zmin+cell[1]*step)
 def free(cell):
  if not(0<=cell[0]<nx and 0<=cell[1]<nz):return False
  x,z=pt(cell);return not any(a<x<b and c<z<d for a,b,c,d in obstacles)
 def nearest(p):
  candidates=[(i,j) for i in range(nx) for j in range(nz) if free((i,j))]
  return min(candidates,key=lambda k:(pt(k)[0]-p[0])**2+(pt(k)[1]-p[2])**2)
 entrance=next(a['position'] for a in room['anchors'] if a['name']=='CustomerEntrance');start=nearest(entrance);seen={start};queue=collections.deque([start])
 while queue:
  x,z=queue.popleft()
  for q in ((x+1,z),(x-1,z),(x,z+1),(x,z-1)):
   if q not in seen and free(q):seen.add(q);queue.append(q)
 accesses=[]
 for s in room['seats']:
  target=nearest(s['approach']);p=pt(target);distance=math.hypot(p[0]-s['approach'][0],p[1]-s['approach'][2]);okay=target in seen and distance<.15
  accesses.append({'seat':s['name'],'reachable':okay,'sample_offset_m':round(distance,3)})
  if not okay:errors.append('Café approach not reachable: level %d %s'%(level['id'],s['name']))
 room_reports.append({'level':level['id'],'approved_station_count':len(actual),'seats':len(seats),'seat_approaches':accesses,'walk_radius_m':.22,'sample_grid_m':step})
for p in OUT.glob('level-*-populated.glb'):reports.append(inspect(p))
credits=json.loads((OUT/'meshy-audit.json').read_text());cost=sum(item.get(stage,{}).get('consumed_credits',0) for item in credits for stage in ('model','rig'))
report={'errors':errors,'geometry_files_inspected':len(reports),'rooms':room_reports,'meshy_credits':cost,'meshy_balance_before':2692,'meshy_balance_after':2307,'limitations':'Circulation is a sampled footprint check, not a Unity NavMesh/physics playtest. Approximate art-review rigs are not production animation.'}
(OUT/'geometry-inspection.json').write_text(json.dumps(reports,indent=2)+'\n');(OUT/'validation.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2));sys.exit(bool(errors))
