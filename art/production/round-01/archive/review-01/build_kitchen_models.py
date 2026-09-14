#!/usr/bin/env python3
"""Editable, deterministic stylized mesh kit and approved room assemblies.
Writes open GLB/OBJ assets, source mesh JSON and shared placement manifests for Unity.
Coordinates: metres, +Y up, +Z toward the customer/front of a prop.
"""
import json, math, struct, collections, random
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'art/production/round-01'
MODELS=OUT/'models'
PALETTE={
 'oak':'C59159','oakLight':'DBAE77','oakDark':'936439','sage':'8F9D7C','sageDark':'718463',
 'ivory':'F3E8D4','cream':'FFF6E4','cocoa':'594132','teal':'326C71','navy':'344A60',
 'tomato':'EB5039','tomatoFlesh':'FF8460','seed':'FFD078','leaf':'628637','cucumber':'3E703B',
 'cucumberFlesh':'D6E995','carrot':'F78B30','carrotCore':'FFAC50','mushroom':'BB916C',
 'mushroomFlesh':'EDDCBE','gill':'8A634F','soup':'D9A554','metal':'AEBDB8','darkMetal':'526964',
 'water':'83C8D2','tile':'E6D3B8','tileLight':'F8EEDC','terracotta':'BF7852','plum':'845678',
 'red':'BB5348','glass':'B6D7D5','chalk':'30484A','black':'302B2B','burnt':'514131'}
ASSETS={}

def vadd(a,b):return [a[i]+b[i] for i in range(3)]
def vsub(a,b):return [a[i]-b[i] for i in range(3)]
def cross(a,b):return [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]
def norm(a):
 l=math.sqrt(sum(x*x for x in a));return [x/l for x in a] if l>1e-9 else [0,1,0]
def rotate(p,r):
 x,y,z=p
 for axis,angle in enumerate(r):
  c,s=math.cos(math.radians(angle)),math.sin(math.radians(angle))
  if axis==0:y,z=y*c-z*s,y*s+z*c
  if axis==1:x,z=x*c+z*s,-x*s+z*c
  if axis==2:x,y=x*c-y*s,x*s+y*c
 return [x,y,z]
def normals(v,f):
 n=[[0.,0.,0.] for _ in v]
 for a,b,c in f:
  q=cross(vsub(v[b],v[a]),vsub(v[c],v[a]))
  for i in (a,b,c):n[i]=vadd(n[i],q)
 shared={}
 keys=[tuple(round(x,6) for x in p) for p in v]
 for key,a in zip(keys,n):shared[key]=vadd(shared.get(key,[0,0,0]),a)
 return [norm(shared[key]) for key in keys]
def mesh(v,f):return {'v':v,'f':f}
def ellipsoid(s=(1,1,1),segments=20,rings=12):
 v=[];f=[]
 for j in range(rings+1):
  t=math.pi*j/rings
  for i in range(segments+1):
   p=2*math.pi*i/segments
   v.append([s[0]*math.sin(t)*math.cos(p),s[1]*math.cos(t),s[2]*math.sin(t)*math.sin(p)])
 for j in range(rings):
  for i in range(segments):
   a=j*(segments+1)+i;b=a+segments+1
   f.extend([(a,a+1,b),(a+1,b+1,b)])
 return mesh(v,f)
def rounded_box(s,r=.035):
 h=[x/2 for x in s];r=min(r,min(h)*.85);core=[x-r for x in h]
 v=[];f=[]
 for axis in range(3):
  u=(axis+1)%3;w=(axis+2)%3
  for sign in (-1,1):
   start=len(v);us=[-h[u],-core[u],core[u],h[u]];ws=[-h[w],-core[w],core[w],h[w]]
   for a in us:
    for b in ws:
     p=[0,0,0];p[axis]=h[axis]*sign;p[u]=a;p[w]=b
     q=[max(-core[k],min(core[k],p[k])) for k in range(3)]
     d=norm(vsub(p,q));v.append([q[k]+d[k]*r for k in range(3)])
   for i in range(3):
    for j in range(3):
     a=start+i*4+j
     faces=[(a,a+4,a+1),(a+1,a+4,a+5)]
     if sign<0:faces=[tuple(reversed(t)) for t in faces]
     f.extend(faces)
 return mesh(v,f)
def lathe(profile,seg=24,angle=360):
 v=[];f=[]
 for radius,y in profile:
  for i in range(seg+1):
   a=math.radians(angle)*i/seg
   v.append([radius*math.cos(a),y,radius*math.sin(a)])
 for j in range(len(profile)-1):
  for i in range(seg):
   a=j*(seg+1)+i;b=a+seg+1
   f.extend([(a,b,a+1),(a+1,b,b+1)])
 return mesh(v,f)
def cylinder(rad,height):
 return lathe([(0,0),(rad*.94,0),(rad,height*.08),(rad,height*.92),(rad*.94,height),(0,height)])
def torus(radius,tube,seg=24,ring=8):
 v=[];f=[]
 for i in range(seg+1):
  a=i*2*math.pi/seg
  for j in range(ring+1):
   b=j*2*math.pi/ring
   v.append([(radius+tube*math.cos(b))*math.cos(a),tube*math.sin(b),(radius+tube*math.cos(b))*math.sin(a)])
 for i in range(seg):
  for j in range(ring):
   a=i*(ring+1)+j;b=a+ring+1
   f.extend([(a,a+1,b),(a+1,b+1,b)])
 return mesh(v,f)
def extrusion(poly,depth):
 # Convex profile in x/y, thickness along z. Duplicated face vertices keep cut faces clean.
 v=[];f=[];n=len(poly)
 area=sum(poly[i][0]*poly[(i+1)%n][1]-poly[(i+1)%n][0]*poly[i][1] for i in range(n))
 if area<0:poly=list(reversed(poly))
 for side in (-1,1):
  start=len(v);v.extend([[x,y,side*depth/2] for x,y in poly])
  for i in range(1,n-1):
   t=(start,start+i,start+i+1);f.append(t if side>0 else tuple(reversed(t)))
 for i in range(n):
  a,b=poly[i],poly[(i+1)%n];s=len(v)
  v.extend([[a[0],a[1],-depth/2],[b[0],b[1],-depth/2],[b[0],b[1],depth/2],[a[0],a[1],depth/2]])
  f.extend([(s,s+1,s+2),(s,s+2,s+3)])
 return mesh(v,f)
class Model:
 def __init__(self,name,category,anchors=None,collider=None):
  self.name=name;self.category=category;self.groups={};self.anchors=anchors or [];self.collider=collider
 def add(self,m,color,pos=(0,0,0),rot=(0,0,0),scale=(1,1,1)):
  g=self.groups.setdefault(color,{'v':[],'f':[]});off=len(g['v'])
  g['v'].extend([vadd(rotate([p[k]*scale[k] for k in range(3)],rot),pos) for p in m['v']])
  g['f'].extend([tuple(off+i for i in f) for f in m['f']]);return self
 def box(self,s,color,pos=(0,0,0),rot=(0,0,0),r=.035):return self.add(rounded_box(s,r),color,pos,rot)
 def ball(self,s,color,pos=(0,0,0),rot=(0,0,0)):return self.add(ellipsoid(s),color,pos,rot)
 def cyl(self,rad,h,color,pos=(0,0,0),rot=(0,0,0)):return self.add(cylinder(rad,h),color,pos,rot)
 def ring(self,rad,tube,color,pos=(0,0,0),rot=(0,0,0)):return self.add(torus(rad,tube),color,pos,rot)
 def include(self,other,pos=(0,0,0),rot=(0,0,0),scale=(1,1,1)):
  for c,g in other.groups.items():self.add(g,c,pos,rot,scale)
  return self
 def save(self):ASSETS[self.name]=self;return self

def food_models():
 m=Model('tomato-whole','Food').ball((.125,.108,.117),'tomato',(0,.113,0))
 for a in range(0,360,72):
  m.ball((.028,.008,.066),'leaf',rotate([0,.217,.025],(0,a,0)),(0,a,0))
 m.cyl(.014,.043,'leaf',(0,.214,0)).save()
 m=Model('cucumber-whole','Food').ball((.07,.067,.165),'cucumber',(0,.07,0),(0,-25,0))
 for a in (-35,0,35):m.ball((.005,.005,.128),'leaf',(.032*math.sin(math.radians(a)),.132,0),(0,-25,0))
 m.save()
 m=Model('carrot-whole','Food').add(lathe([(0,0),(.025,.015),(.059,.14),(.058,.205),(.043,.232),(0,.238)]),'carrot',(0,.073,-.12),(68,0,0))
 for i in (-1,0,1):m.ball((.018,.013,.07),'leaf',(i*.025,.125,.11),(0,i*20,0))
 m.save()
 Model('mushroom-whole','Food').add(lathe([(0,0),(.05,0),(.046,.05),(.034,.105),(0,.115)]),'mushroomFlesh').add(lathe([(0,.105),(.103,.105),(.115,.125),(.102,.173),(.064,.208),(0,.22)]),'mushroom').save()
 # Chopped piles are a single movable ingredient unit.
 for kind in ('tomato','cucumber','carrot','mushroom'):
  m=Model(kind+'-chopped','Food')
  for i,(x,z,angle) in enumerate([(-.062,-.04,-25),(.05,-.038,35),(-.04,.061,12),(.075,.058,-35)]):
   part=Model('_piece','Food')
   if kind=='tomato':
    poly=[(-.062,0),(.062,0)]+[(.062*math.cos(t*math.pi/12),.075*math.sin(t*math.pi/12)) for t in range(1,12)]
    part.add(extrusion(poly,.039),'tomato').add(extrusion([(a*.81,b*.80+.006) for a,b in poly],.040),'tomatoFlesh')
    for j in range(3):part.ball((.007,.012,.003),'seed',((j-1)*.023,.028+(j%2)*.009,.022),(0,0,(j-1)*25))
   elif kind=='cucumber':
    part.cyl(.055,.035,'cucumber').cyl(.046,.003,'cucumberFlesh',(0,.035,0))
    for a in (0,120,240):part.ball((.005,.002,.013),'seed',rotate([0,.039,.019],(0,a,0)),(0,a,0))
   elif kind=='carrot':
    poly=[(-.06,0),(.06,0)]+[(.06*math.cos(t*math.pi/12),.071*math.sin(t*math.pi/12)) for t in range(1,12)]
    part.add(extrusion(poly,.036),'carrot').add(extrusion([(a*.55,b*.6) for a,b in poly],.037),'carrotCore')
   else:
    poly=[(-.058,.031),(-.052,.067),(-.029,.092),(0,.103),(.029,.092),(.052,.067),(.058,.031)]
    part.add(extrusion(poly,.03),'mushroom').add(extrusion([(a*.84,b*.87+.002) for a,b in poly],.031),'mushroomFlesh')
    part.box((.035,.060,.031),'mushroomFlesh',(0,.03,0),r=.014)
    for side in (-1,1):part.ball((.012,.019,.004),'gill',(side*.03,.043,.018))
   m.include(part,(x,.007+(i//3)*.025,z),(0,angle,0))
  m.save()

def dish_models():
 plate=Model('dish-clean','Dishes').add(lathe([(0,0),(.096,0),(.12,.012),(.151,.064),(.16,.073),(.162,.081),(.155,.088),(.146,.081),(.116,.026),(0,.025)]),'ivory').ring(.152,.004,'teal',(0,.081,0)).save()
 for state in ('tomato','cucumber','salad','soup','dirty'):
  m=Model('dish-'+state,'Dishes').include(plate)
  if state in ('tomato','salad'):m.include(ASSETS['tomato-chopped'],(-.017,.035,-.008),scale=(.76,.76,.76))
  if state in ('cucumber','salad'):m.include(ASSETS['cucumber-chopped'],(.01,.048,.003),(0,25,0),(.78,.78,.78))
  if state=='soup':
   m.cyl(.138,.004,'soup',(0,.063,0))
   m.include(ASSETS['carrot-chopped'],(-.02,.055,.01),scale=(.6,.6,.6))
   m.include(ASSETS['mushroom-chopped'],(.025,.055,-.018),(85,0,0),(.5,.5,.5))
  if state=='dirty':
   m.ball((.08,.002,.05),'soup',(0,.03,0));m.ball((.021,.003,.015),'tomato',(0.06,.05,.065))
  m.save()

def tool_models():
 Model('cutting-board','Tools').box((.4,.028,.28),'oakLight',(0,.014,0),r=.05).box((.16,.032,.23),'oak',(0,.012,0),r=.02).save()
 Model('knife','Tools',anchors=[{'name':'Grip','position':[0,0,-.085]}]).box((.04,.025,.125),'cocoa',(0,.015,-.075),r=.012).box((.073,.012,.15),'metal',(.015,.016,.055),r=.012).cyl(.006,.003,'metal',(0,.03,-.08)).save()
 Model('sponge','Tools').box((.115,.04,.075),'seed',(0,.023,0),r=.02).box((.117,.012,.076),'sageDark',(0,.045,0),r=.015).save()
 Model('ladle','Tools').cyl(.012,.22,'oakDark',(0,.05,0),(35,0,0)).ball((.046,.025,.042),'metal',(0,.036,.032)).save()
 pot=Model('pot-empty','Tools').add(lathe([(0,0),(.125,0),(.155,.027),(.166,.165),(.17,.19),(.16,.2),(.152,.187),(.141,.035),(0,.035)]),'teal').ring(.159,.011,'metal',(0,.194,0))
 for x in (-.195,.195):pot.ring(.04,.012,'cocoa',(x,.15,0),(90,0,0))
 pot.save()
 Model('pot-lid','Tools').add(lathe([(0,0),(.168,0),(.169,.014),(.145,.025),(.09,.055),(0,.062)]),'teal').ball((.035,.022,.026),'cocoa',(0,.078,0)).save()
 for state in ('carrot','mushroom','both','cooking','ready','burnt'):
  m=Model('pot-'+state,'Tools').include(pot)
  if state in ('cooking','ready','burnt'):m.cyl(.149,.004,'burnt' if state=='burnt' else 'soup',(0,.162,0))
  if state!='burnt':
   if state!='mushroom':m.include(ASSETS['carrot-chopped'],(0,.07 if state in ('carrot','both') else .153,0),scale=(.85,.85,.85))
   if state!='carrot':m.include(ASSETS['mushroom-chopped'],(0,.09 if state in ('mushroom','both') else .145,0),(75,0,0),(.7,.7,.7))
  m.save()

def base_counter(name='counter',color='sage',top=True):
 m=Model(name,'Stations',anchors=[{'name':'Worktop','position':[0,.59,0]},{'name':'InteractionFront','position':[0,0,.66]}],collider={'center':[0,.275,0],'size':[.76,.55,.76]})
 m.box((.7,.10,.7),'oakDark',(0,.055,0),r=.025).box((.73,.44,.71),color,(0,.29,0),r=.035)
 if top:m.box((.78,.075,.78),'oakLight',(0,.548,0),r=.045)
 for x in (-.172,.172):
  m.box((.315,.31,.02),color,(x,.298,.365),r=.018).ball((.031,.016,.017),'cocoa',(x,.365,.387))
 return m

def station_models():
 base_counter().save()
 prep=base_counter('station-prep').include(ASSETS['cutting-board'],(0,.586,0)).include(ASSETS['knife'],(.22,.592,.02),(0,-25,0)).save()
 for kind in ('tomato','cucumber','carrot','mushroom'):
  m=base_counter('source-'+kind,'sageDark')
  for x in (-.30,.30):m.box((.035,.12,.61),'oak',(x,.642,0),r=.012)
  for z in (-.30,.30):m.box((.60,.12,.035),'oak',(0,.642,z),r=.012)
  for i in range(4):m.include(ASSETS[kind+'-whole'],((i%2-.5)*.26,.60,(i//2-.5)*.23),rot=(0,i*47,0),scale=(.85,.85,.85))
  m.save()
 m=base_counter('station-pot','teal').box((.64,.035,.63),'darkMetal',(0,.592,0),r=.035).ring(.22,.013,'metal',(0,.617,0)).include(ASSETS['pot-empty'],(0,.623,0))
 for x in (-.18,.18):m.cyl(.035,.017,'cocoa',(x,.47,.397),(90,0,0))
 m.anchors.append({'name':'PotContents','position':[0,.623,0]});m.save()
 m=base_counter('station-sink','teal',False)
 m.box((.78,.04,.78),'metal',(0,.545,0),r=.035)
 m.box((.46,.009,.41),'darkMetal',(0,.574,.055),r=.06).box((.37,.008,.32),'water',(0,.58,.055),r=.045)
 for x in (-.245,.245):m.box((.065,.04,.48),'ivory',(x,.591,.055),r=.025)
 for z in (-.195,.305):m.box((.54,.04,.04),'ivory',(0,.591,z),r=.019)
 m.cyl(.024,.28,'metal',(0,.565,-.24)).box((.18,.04,.04),'metal',(.069,.82,-.24),r=.019).cyl(.022,.075,'metal',(.14,.747,-.24))
 m.include(ASSETS['sponge'],(.31,.568,.23)).save()
 m=base_counter('station-dishes')
 for i in range(5):m.include(ASSETS['dish-clean'],(0,.586+i*.026,0))
 for x in (-.22,.22):m.box((.027,.19,.36),'oak',(x,.675,0),r=.012)
 m.save()
 m=base_counter('station-serve','ivory').box((.68,.02,.42),'teal',(0,.598,.09),r=.03)
 for x in (-.32,.32):m.cyl(.016,.33,'oakDark',(x,.58,-.26))
 m.box((.71,.11,.045),'oakLight',(0,.92,-.26),r=.018)
 # raised decorative bowl icon on pass sign
 m.ball((.06,.026,.01),'ivory',(0,.915,-.232)).save()
 m=base_counter('station-return','navy').box((.62,.025,.67),'darkMetal',(0,.59,0),r=.02)
 for z in (-.25,-.15,-.05,.05,.15,.25):m.box((.57,.015,.018),'metal',(0,.609,z),r=.007)
 for x in (-.34,.34):m.box((.04,.09,.7),'metal',(x,.623,0),r=.018)
 m.anchors.append({'name':'DirtyDishSpawn','position':[0,.63,-.22]});m.save()
 m=Model('station-bin','Stations',collider={'center':[0,.24,0],'size':[.49,.48,.49]})
 m.box((.43,.45,.43),'teal',(0,.24,0),r=.075).box((.49,.05,.49),'cocoa',(0,.47,0),r=.07).box((.16,.008,.21),'black',(0,.499,0),r=.065).save()

def furniture_models():
 rail=Model('rail','Environment',collider={'center':[0,.30,0],'size':[.8,.6,.18]})
 for x in (-.365,.365):rail.box((.07,.61,.10),'oakDark',(x,.305,0),r=.02).ball((.053,.048,.053),'oakLight',(x,.63,0))
 rail.box((.8,.09,.13),'oakLight',(0,.53,0),r=.025).box((.75,.07,.08),'oak',(0,.14,0),r=.025)
 for x in (-.23,0,.23):rail.box((.034,.34,.04),'oak',(x,.33,0),r=.01)
 rail.save()
 table=Model('cafe-table','Cafe',anchors=[{'name':'DishA','position':[-.19,.55,0]},{'name':'DishB','position':[.19,.55,0]}],collider={'center':[0,.265,0],'size':[.85,.53,.85]})
 table.cyl(.28,.06,'oakDark').cyl(.105,.46,'oak',(0,.045,0)).cyl(.46,.075,'oakLight',(0,.46,0)).save()
 chair=Model('cafe-chair','Cafe',anchors=[{'name':'Seat','position':[0,.24,0]},{'name':'Approach','position':[0,0,.62]}],collider={'center':[0,.28,0],'size':[.47,.56,.47]})
 for x in (-.16,.16):
  for z in (-.15,.15):chair.box((.047,.24,.047),'oak',(x,.12,z),r=.016)
 chair.box((.47,.06,.45),'oakLight',(0,.24,0),r=.07).box((.40,.045,.38),'navy',(0,.284,0),r=.07)
 for x in (-.17,.17):chair.box((.036,.37,.04),'oak',(x,.405,-.17),r=.012)
 chair.box((.46,.20,.064),'oakLight',(0,.55,-.19),r=.06).save()
 m=Model('cafe-bench','Cafe',anchors=[{'name':'SeatA','position':[-.28,.27,0]},{'name':'SeatB','position':[.28,.27,0]}],collider={'center':[0,.3,0],'size':[1.2,.6,.55]})
 for x in (-.49,.49):m.box((.10,.13,.38),'oakDark',(x,.065,0),r=.03)
 m.box((1.22,.14,.58),'oakLight',(0,.19,0),r=.07).box((1.2,.4,.10),'oak',(0,.43,-.24),r=.05)
 for x in (-.30,.30):m.box((.57,.10,.48),'sage',(x,.30,.02),r=.07).box((.57,.31,.10),'sage',(x,.48,-.165),r=.06)
 m.save()
 m=Model('planter','Cafe').add(lathe([(0,0),(.12,0),(.16,.27),(.18,.28),(.18,.32),(.147,.32),(.137,.04),(0,.04)]),'terracotta').cyl(.15,.012,'cocoa',(0,.285,0))
 for i in range(6):
  a=i*60;m.ball((.045,.17,.065),'leaf' if i%2 else 'sageDark',rotate([0,.43,.074],(0,a,0)),(20,a,0))
 m.save()
 m=Model('menu-board','Cafe')
 for x in (-.27,.27):m.box((.045,.71,.06),'oak',(x,.35,-.1),(14,0,0),r=.02)
 m.box((.62,.64,.065),'oakLight',(0,.4,.02),r=.05).box((.52,.54,.018),'chalk',(0,.4,.064),r=.035)
 for y,w in ((.54,.33),(.45,.25),(.27,.34),(.20,.24)):m.box((w,.012,.008),'ivory',(0,y,.078),r=.004)
 m.ball((.08,.028,.006),'seed',(0,.34,.083)).save()
 m=Model('cafe-door','Cafe',anchors=[{'name':'Entrance','position':[0,0,.6]}])
 for x in (-.53,.53):m.box((.10,1.65,.15),'ivory',(x,.825,0),r=.045)
 m.box((1.1,.12,.15),'ivory',(0,1.65,0),r=.055)
 # Door leaf is modeled open so the entrance has real clear space.
 leaf=Model('_leaf','Cafe').box((.94,1.55,.055),'oak',(0,.78,0),r=.08).box((.49,.83,.022),'glass',(0,1.01,.041),r=.13).ball((.035,.037,.032),'cocoa',(.34,.58,.064))
 m.include(leaf,(-.52,0,-.45),(0,85,0)).save()
 m=Model('window','Cafe').box((1.25,.85,.06),'oak',(0,1.04,0),r=.045).box((1.1,.70,.025),'glass',(0,1.04,.046),r=.03)
 m.box((.035,.71,.04),'ivory',(0,1.04,.067),r=.01).box((1.1,.035,.04),'ivory',(0,1.04,.067),r=.01).box((1.34,.07,.22),'oakLight',(0,.61,.07),r=.03)
 for i in range(6):m.box((.225,.05,.39),'sage' if i%2 else 'ivory',((i-2.5)*.216,1.57,.16),(12,0,0),r=.024)
 m.save()
 Model('wall-panel','Environment',collider={'center':[0,.65,0],'size':[.8,1.3,.12]}).box((.8,1.3,.12),'ivory',(0,.65,0),r=.018).box((.8,.40,.025),'sage',(0,.23,.075),r=.008).box((.8,.045,.055),'oakLight',(0,.46,.08),r=.013).box((.8,.06,.05),'oak',(0,.055,.075),r=.012).save()
 Model('floor-cream','Environment').box((.799,.06,.799),'tileLight',(0,-.03,0),r=.009).save()
 Model('floor-sand','Environment').box((.799,.06,.799),'tile',(0,-.03,0),r=.009).save()
 Model('floor-wood','Environment').box((.799,.06,.799),'oak',(0,-.03,0),r=.008).box((.784,.006,.01),'oakDark',(0,.002,0),r=.002).save()
 Model('welcome-mat','Cafe').box((1.03,.016,.62),'teal',(0,.01,0),r=.06).box((.85,.006,.44),'sage',(0,.02,0),r=.045).save()

def linear(c):return c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4

def write_glb(path,instances):
 # glTF uses right-handed coordinates: mirror Z and reverse triangle winding.
 data=bytearray();views=[];accessors=[];meshes=[];nodes=[];meshmap={}
 mats=[{'name':k,'pbrMetallicRoughness':{'baseColorFactor':[linear(int(h[i:i+2],16)/255) for i in (0,2,4)]+[1],'metallicFactor':.12 if k=='metal' else 0,'roughnessFactor':.75},'doubleSided':False} for k,h in PALETTE.items()]
 matids={k:i for i,k in enumerate(PALETTE)}
 def accessor(values,fmt,ty,component):
  while len(data)%4:data.append(0)
  start=len(data);flat=[x for a in values for x in a] if ty=='VEC3' else values
  data.extend(struct.pack('<'+fmt*len(flat),*flat));vi=len(views);views.append({'buffer':0,'byteOffset':start,'byteLength':len(data)-start})
  a={'bufferView':vi,'componentType':component,'count':len(values),'type':ty}
  if ty=='VEC3':a['min']=[min(v[i] for v in values) for i in range(3)];a['max']=[max(v[i] for v in values) for i in range(3)]
  accessors.append(a);return len(accessors)-1
 for instance in instances:
  name=instance['asset']
  if name not in meshmap:
   model=ASSETS[name];primitives=[]
   for color,g in model.groups.items():
    n=normals(g['v'],g['f']);p=[[a,b,-c] for a,b,c in g['v']];n=[[a,b,-c] for a,b,c in n];inds=[j for f in g['f'] for j in reversed(f)]
    primitives.append({'attributes':{'POSITION':accessor(p,'f','VEC3',5126),'NORMAL':accessor(n,'f','VEC3',5126)},'indices':accessor(inds,'I','SCALAR',5125),'material':matids[color]})
   meshmap[name]=len(meshes);meshes.append({'name':name,'primitives':primitives})
  x,y,z=instance.get('position',[0,0,0]);yaw=-math.radians(instance.get('yaw',0))/2
  node={'name':instance.get('name',name),'mesh':meshmap[name],'translation':[x,y,-z],'rotation':[0,math.sin(yaw),0,math.cos(yaw)]}
  if instance.get('scale'):node['scale']=instance['scale']
  nodes.append(node)
 doc={'asset':{'version':'2.0','generator':'Bara Kitchen editable mesh kit v1'},'scene':0,'scenes':[{'nodes':list(range(len(nodes)))}],'nodes':nodes,'meshes':meshes,'materials':mats,'buffers':[{'byteLength':len(data)}],'bufferViews':views,'accessors':accessors}
 js=json.dumps(doc,separators=(',',':')).encode();js+=b' '*((-len(js))%4);data+=b'\0'*((-len(data))%4)
 total=12+8+len(js)+8+len(data)
 path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(struct.pack('<4sII',b'glTF',2,total)+struct.pack('<I4s',len(js),b'JSON')+js+struct.pack('<I4s',len(data),b'BIN\0')+data)

def export_assets():
 MODELS.mkdir(parents=True,exist_ok=True);manifest=[]
 mtl='\n'.join('newmtl '+k+'\nKd '+' '.join(str(int(v[i:i+2],16)/255) for i in (0,2,4))+'\nNs 15\n' for k,v in PALETTE.items())
 (MODELS/'palette.mtl').write_text(mtl)
 for name,m in ASSETS.items():
  parts=[];obj=['# Bara Kitchen - metres, +Y up; editable mesh source','mtllib palette.mtl'];offset=1;tri=0;vertices=0
  for color,g in m.groups.items():
   n=normals(g['v'],g['f']);parts.append({'material':color,'vertices':[round(v,6) for p in g['v'] for v in p],'normals':[round(v,6) for p in n for v in p],'triangles':[i for f in g['f'] for i in f]})
   obj+=['o '+name+'_'+color,'usemtl '+color]
   obj+=['v %.6f %.6f %.6f'%tuple(p) for p in g['v']]
   obj+=['vn %.6f %.6f %.6f'%tuple(p) for p in n]
   obj+=['f '+' '.join(str(i+offset)+'//'+str(i+offset) for i in f) for f in g['f']]
   offset+=len(g['v']);tri+=len(g['f']);vertices+=len(g['v'])
  entry={'name':name,'category':m.category,'parts':parts,'anchors':m.anchors,'collider':m.collider}
  (MODELS/(name+'.mesh.json')).write_text(json.dumps(entry,separators=(',',':')))
  (MODELS/(name+'.obj')).write_text('\n'.join(obj)+'\n')
  write_glb(MODELS/(name+'.glb'),[{'asset':name}])
  manifest.append({'name':name,'category':m.category,'triangles':tri,'vertices':vertices,'anchors':m.anchors,'collider':m.collider})
 (OUT/'model-catalog.json').write_text(json.dumps({'palette':[{'name':k,'hex':v} for k,v in PALETTE.items()],'models':manifest},indent=2)+'\n')
 return manifest

def build_rooms():
 levels=json.loads((ROOT/'art/concepts/levels-round-02/levels.json').read_text());result=[]
 kinds={'prep':'station-prep','pot':'station-pot','sink':'station-sink','dishes':'station-dishes','serve':'station-serve','return':'station-return','bin':'station-bin','counter':'counter'}
 for k in ('tomato','cucumber','carrot','mushroom'):kinds[k]='source-'+k
 for level in levels:
  c,r=level['cols'],level['rows'];instances=[];seats=[];anchors=[];s=.8
  def add(asset,name,pos,yaw=0,**kwargs):
   inst={'asset':asset,'name':name,'position':pos,'yaw':yaw,**kwargs};instances.append(inst);return inst
  def grid(x,y,h=0):return [(x-(c-1)/2)*s,h,y*s]
  # Full kitchen footprint is unchanged; café extension starts after kitchen near boundary.
  for y in range(r+5):
   for x in range(c):add('floor-wood' if y>=r else ('floor-cream' if (x+y)%2 else 'floor-sand'),'Floor_%d_%d'%(x,y),grid(x,y))
  for station in level['stations']:
   yaw={'S':0,'W':-90,'N':180,'E':90}[station['face']]
   add(kinds[station['kind']],station['id'],grid(station['x'],station['y']),yaw,station=True,kind=station['kind'],grid=[station['x'],station['y']])
  for barrier in level['barriers']:
   cells=barrier['cells'];vertical=len({p[0] for p in cells})==1
   for x,y in cells:
    add('rail' if barrier['kind']=='rail' else 'counter','Barrier_%d_%d'%(x,y),grid(x,y),90 if vertical else 0,barrier=True)
  for x in range(c):add('wall-panel','BackWall_'+str(x),grid(x,-.5))
  for y in range(r+5):
   add('wall-panel','LeftWall_'+str(y),grid(-.5,y),90,scale=[1,.35,1])
   if y not in (r+2,r+3):add('wall-panel','RightWall_'+str(y),grid(c-.5,y),-90,scale=[1,.35,1])
  # Low divider prevents the café becoming a bypass around the kitchen's puzzle rails.
  for x in range(c):add('rail','CafeBoundary_'+str(x),grid(x,r-.15))
  # Tables leave a 1.28m circulation lane behind the kitchen; seats approach from near aisle.
  n=2 if c<=9 else 3
  centers=[(i+1)*c/(n+1)-.5 for i in range(n)]
  for i,x in enumerate(centers):
   p=grid(x,r+2.1);add('cafe-table','Table_'+str(i+1),p)
   for side in (-1,1):
    seatpos=[p[0]+side*.72,0,p[2]];yaw=-90 if side==1 else 90
    add('cafe-chair','Chair_%d_%d'%(i,side),seatpos,yaw)
    anchor={'name':'Seat_%d_%s'%(i+1,'L' if side<0 else 'R'),'position':[seatpos[0],.31,seatpos[2]],'yaw':yaw,'approach':[seatpos[0],0,seatpos[2]+.68],'dishPosition':[p[0]+side*.19,.542,p[2]]}
    seats.append(anchor)
  for i,seat in enumerate(seats[:3]):
   recipe=level['recipes'][i % len(level['recipes'])]
   add('dish-'+recipe,'DiningDish_'+seat['name'],list(seat['dishPosition']))
  entrance=grid(c-.5,r+2.5);add('cafe-door','CustomerEntrance',entrance,-90)
  add('welcome-mat','WelcomeMat',grid(c-1.3,r+2.5),90)
  add('menu-board','Menu',grid(c-1.1,r+.65),-20)
  add('cafe-bench','WaitingBench',grid(.25,r+.55),90)
  add('planter','EntrancePlant',grid(c-1.2,r+4.15))
  add('planter','CafePlant',grid(.15,r+4.1))
  for x in (c*.25,c*.72):add('window','KitchenWindow_'+str(x),grid(x,-.45))
  # Customer area anchors are data for future navigation/seat/eating behaviours.
  anchors=[{'name':'CustomerEntrance','position':grid(c-1.0,r+2.5)}, {'name':'CustomerQueue','position':grid(c-1.1,r+1.15)}, {'name':'CustomerAisleEast','position':grid(c-1.1,r+3.65)}, {'name':'CustomerAisleWest','position':grid(.8,r+3.65)}]
  chars=[{'id':'capybara-male-chef','position':grid(*level['start']),'yaw':0,'pose':'idle'}]
  for i,seat in enumerate(seats[:min(3,n*2)]):chars.append({'id':['cat-female-customer','dog-male-customer','capybara-female-customer'][i],'position':list(seat['position']),'yaw':seat['yaw'],'pose':'seated','seat':seat['name']})
  chars.append({'id':'dog-female-customer','position':grid(c-2.1,r+3.8),'yaw':-60,'pose':'walk'})
  # In Unity the front-facing camera is on +Z; mirror map X so plan-left stays screen-left.
  for item in instances + chars + anchors:
   item['position'][0] *= -1
   if 'yaw' in item:item['yaw'] *= -1
  for seat in seats:
   for key in ('position','approach','dishPosition'):seat[key][0] *= -1
   seat['yaw'] *= -1
  room={'id':level['id'],'name':level['title'],'shape':level['shape'],'cols':c,'rows':r,'module':s,'cafeRows':5,'instances':instances,'seats':seats,'anchors':anchors,'characters':chars,'cameraTarget':[0,.3,(r+3)*s/2],'cameraSize':max(c*s*.42,(r+5)*s*.44),'kitchenRules':{'universalDish':True,'freeCounterCount':sum(t['kind']=='counter' for t in level['stations']),'railsBlockWalking':True,'railsAllowIngredientThrows':True,'customerAreaSeparate':True}}
  (OUT/('level-%02d.scene.json'%level['id'])).write_text(json.dumps(room,indent=2)+'\n')
  write_glb(OUT/('level-%02d.glb'%level['id']),instances)
  result.append({'id':level['id'],'name':level['title'],'instances':len(instances),'seats':len(seats),'freeCounters':room['kitchenRules']['freeCounterCount']})
 (OUT/'rooms-catalog.json').write_text(json.dumps(result,indent=2)+'\n')
 return result

if __name__=='__main__':
 food_models();dish_models();tool_models();station_models();furniture_models()
 catalog=export_assets();rooms=build_rooms()
 print(json.dumps({'models':len(catalog),'meshTriangles':sum(x['triangles'] for x in catalog),'rooms':rooms},indent=2))
