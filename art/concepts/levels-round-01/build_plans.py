"""Draw reviewable floor plans and audit solo access. Standard-library only."""
from pathlib import Path
from collections import deque, Counter
import json, html

ROOT = Path(__file__).resolve().parent
DATE = '13 September 2026'

def station(id, kind, x, y, face='S', label=None):
    return dict(id=id, kind=kind, x=x, y=y, face=face, label=label)

LEVELS = [
 dict(id=1, title='First Service', subtitle='Learn one complete service loop.', cols=8, rows=6,
      ingredients=['tomato','cucumber'], recipes=['salad'], skill='Pick up · chop · assemble · serve · wash',
      orders=4, active_orders=1, dishes={'plates':3,'bowls':0}, start=[3,3],
      stations=[station('tomato','tomato',1,0),station('cucumber','cucumber',2,0),station('prep','prep',4,0),station('assemble','assemble',5,0),
                station('return','return',1,5,'N'),station('sink','sink',2,5,'N'),station('plates','plates',3,5,'N'),station('serve','serve',5,5,'N'),station('bin','bin',6,5,'N')],
      routes=[dict(kind='food',ids=['tomato','prep','assemble','serve']),dict(kind='wash',ids=['return','sink','plates'])],
      dash=None, throws=[],
      lessons=['Place a clean plate on the salad counter. Chop and add the two ingredients, one at a time.',
               'Serve the salad. After the customer eats, collect the dirty plate, wash it and restock the rack.',
               'Only one ticket is active. The first full loop is an untimed guided practice.'],
      review='Does the short, open route make each station easy to find?',
      intention='A perimeter kitchen leaves the middle open. The two ingredients sit together; the return tray, sink and plate rack form one short washing route.'),
 dict(id=2,title='Something Simmering',subtitle='Prepare the next task while the soup cooks.',cols=10,rows=6,
      ingredients=['carrot','mushroom'],recipes=['soup'],skill='Cooking + dash',orders=6,active_orders=2,dishes={'plates':0,'bowls':3},start=[4,3],
      stations=[station('carrot','carrot',1,0),station('mushroom','mushroom',2,0),station('prep','prep',4,0),station('pot','pot',7,0),station('rest','counter',8,0),
                station('return','return',1,5,'N'),station('sink','sink',2,5,'N'),station('bowls','bowls',3,5,'N'),station('serve','serve',7,5,'N'),station('bin','bin',8,5,'N')],
      routes=[dict(kind='food',ids=['carrot','prep','pot','serve']),dict(kind='wash',ids=['return','sink','bowls'])],
      dash=[[3,3],[7,3]],throws=[],
      lessons=['Chop carrot and mushroom, then load both into the fixed pot. Cooking starts when both are present.',
               'Use the cooking window to wash a bowl or prepare the next ingredient. Bring a clean bowl to the ready pot, fill it and serve.',
               'Learn dash in the open aisle. Walking must still be sufficient; the cooker stays visible from the washing area.'],
      review='Does the longer aisle make dash useful without making soup preparation feel like a long commute?',
      intention='The wider kitchen separates preparation from cooking. A clear horizontal aisle gives dash a useful role, while the sink and bowl rack remain together.'),
 dict(id=3,title='Lunch Rush',subtitle='Stage ingredients across the island, then walk around.',cols=11,rows=8,
      ingredients=['tomato','cucumber','carrot','mushroom'],recipes=['salad','soup'],skill='Ingredient throw',orders=8,active_orders=2,dishes={'plates':3,'bowls':3},start=[5,6],
      stations=[station('tomato','tomato',2,0),station('cucumber','cucumber',3,0),station('carrot','carrot',7,0),station('mushroom','mushroom',8,0)] +
               [station(f'back{x}','counter',x,3,'N') for x in range(3,8)] +
               [station('landSalad','landing',3,4),station('prepSalad','prep',4,4),station('assemble','assemble',5,4),station('prepSoup','prep',6,4),station('landSoup','landing',7,4),
                station('pot','pot',10,3,'W'),station('rest','counter',10,4,'W'),station('bin','bin',10,6,'W'),
                station('return','return',1,7,'N'),station('sink','sink',2,7,'N'),station('plates','plates',4,7,'N'),station('bowls','bowls',6,7,'N'),station('serve','serve',8,7,'N')],
      routes=[dict(kind='food',ids=['tomato','prepSalad','assemble','serve']),dict(kind='soup',ids=['carrot','prepSoup','pot','serve']),dict(kind='wash',ids=['return','sink','plates','bowls'])],
      dash=[[2,6],[8,6]],throws=[dict(start=[3,2],target='landSalad'),dict(start=[7,2],target='landSoup')],
      lessons=['Practice one throw into a visibly empty landing counter. Only ingredients can be thrown; dishes and cookware are carried.',
               'A throw carries an ingredient over the low island to the far landing counter. The chef walks around either end to retrieve it.',
               'A highlighted target makes aiming clear. A full target rejects the throw and keeps the ingredient in the chef’s paws.'],
      review='Are the landing counters and the two walking routes around the island obvious?',
      intention='A two-module-deep island separates supply and working aisles. Throws can stage one ingredient at each end for a later visit; they never require a second chef.'),
 dict(id=4,title='Café Finale',subtitle='Two preparation areas, one shared service loop.',cols=12,rows=8,
      ingredients=['tomato','cucumber','carrot','mushroom'],recipes=['salad','soup'],skill='Combine all learned actions',orders=10,active_orders=3,dishes={'plates':3,'bowls':3},start=[6,3],
      stations=[station('tomato','tomato',1,0),station('cucumber','cucumber',2,0),station('carrot','carrot',8,0),station('mushroom','mushroom',9,0)] +
               [station(f'back{x}','counter',x,3,'N') for x in [3,4,7,8]] +
               [station('landSalad','landing',3,4),station('prepSalad','prep',4,4),station('prepSoup','prep',7,4),station('landSoup','landing',8,4),
                station('assemble','assemble',0,4,'E'),station('pot','pot',11,4,'W'),
                station('plates','plates',2,7,'N'),station('sink','sink',4,7,'N'),station('return','return',5,7,'N'),station('serve','serve',6,7,'N'),station('bowls','bowls',9,7,'N'),station('bin','bin',10,7,'N')],
      routes=[dict(kind='food',ids=['tomato','prepSalad','assemble','serve']),dict(kind='soup',ids=['carrot','prepSoup','pot','serve']),dict(kind='wash',ids=['return','sink','plates','bowls'])],
      dash=[[2,5],[9,5]],throws=[dict(start=[3,2],target='landSalad'),dict(start=[8,2],target='landSoup')],
      lessons=['The salad and soup work areas feed the same serving pass. Choose which preparation to finish while the soup cooks.',
               'Use the two-module-wide central passage, the side routes and optional throwing shortcuts. All routes remain open.',
               'A shared sink makes clean-dish planning matter. Increase order overlap gradually; add no new recipe, ingredient or hazard.'],
      review='Does coordinating two recipe areas feel like a satisfying finale for one chef?',
      intention='Two small islands open a central passage. Preparation separates by recipe; serving and dish return meet at the centre so the finale tests priorities with the same familiar kit.')
]

COLORS={'food':'#B55338','soup':'#B27A23','wash':'#30748B','throw':'#8063A8','dash':'#687747'}
LABELS={'tomato':'Tomato','cucumber':'Cucumber','carrot':'Carrot','mushroom':'Mushroom','prep':'Chop','assemble':'Salad','return':'Return','sink':'Wash','plates':'Plates','bowls':'Bowls','serve':'Serve','bin':'Bin','pot':'Cook','counter':'Rest','landing':'Landing'}
FACES={'N':(0,-1),'S':(0,1),'E':(1,0),'W':(-1,0)}

def access(s):
    dx,dy=FACES[s['face']]
    return s['x']+dx,s['y']+dy

def bfs(a,b,blocked,w,h):
    a,b=tuple(a),tuple(b)
    q=deque([a]);prev={a:None}
    while q:
        p=q.popleft()
        if p==b:
            path=[]
            while p is not None:path.append(p);p=prev[p]
            return path[::-1]
        for dx,dy in [(1,0),(0,1),(-1,0),(0,-1)]:
            n=(p[0]+dx,p[1]+dy)
            if 0<=n[0]<w and 0<=n[1]<h and n not in blocked and n not in prev:
                prev[n]=p;q.append(n)
    raise ValueError(f'No route from {a} to {b}')

def audit(l):
    ss=l['stations'];blocked={(s['x'],s['y']) for s in ss};assert len(blocked)==len(ss)
    byid={s['id']:s for s in ss};paths={}
    for s in ss:
        a=access(s)
        assert a not in blocked,(l['id'],s['id'],'blocked interaction face')
        paths[s['id']]=len(bfs(l['start'],a,blocked,l['cols'],l['rows']))-1
    assert set(l['ingredients']) == {s['kind'] for s in ss if s['kind'] in ['tomato','cucumber','carrot','mushroom']}
    required={'prep','serve','return','sink','bin'}
    if 'salad' in l['recipes']:required|={'tomato','cucumber','plates','assemble'}
    if 'soup' in l['recipes']:required|={'carrot','mushroom','bowls','pot'}
    assert required<={s['kind'] for s in ss}
    for t in l['throws']:
        target=byid[t['target']]
        assert target['kind']=='landing' and tuple(t['start']) not in blocked
        bfs(l['start'],t['start'],blocked,l['cols'],l['rows'])
        assert target['x']==t['start'][0] and target['y']-t['start'][1]==2
        assert (target['x'],target['y']-1) in blocked
    if l['dash']:
        a,b=l['dash'];assert a[1]==b[1]
        assert all((x,a[1]) not in blocked for x in range(a[0],b[0]+1))
    return dict(level=l['id'],station_count=len(ss),all_station_faces_walkable=True,all_stations_reachable_without_skills=True,recipe_components_present=True,throw_targets_valid=True,clear_dash_lane=True,station_steps_from_spawn=paths,geometry_note='Cell-centre connectivity only; character width, collision and reach still need a Unity blockout.')

def esc(s):return html.escape(str(s),quote=True)
def txt(x,y,s,size=17,fill='#493C31',weight=500,anchor='middle',extra=''):
    return f'<text x="{x}" y="{y}" font-size="{size}" fill="{fill}" font-weight="{weight}" text-anchor="{anchor}" {extra}>{esc(s)}</text>'

def icon(kind):
    # Native diagram symbols, centered at (0,0); no generated raster art.
    if kind=='tomato':return '<ellipse cy="1" rx="19" ry="16" fill="#D65D46"/><path d="M-9 -13 0 -17 9 -12 2 -11 0 -5 -3 -11Z" fill="#527B46"/>'
    if kind=='cucumber':return '<rect x="-24" y="-11" width="48" height="22" rx="11" fill="#517744" transform="rotate(-23)"/><path d="M-17 5 16 -9" stroke="#A9BE74" stroke-width="3" stroke-linecap="round"/>'
    if kind=='carrot':return '<path d="M-11 -14Q0 -22 12 -12L0 23Q-3 25 -5 17Z" fill="#E3933E"/><path d="M-1 -16 -10 -26 M1 -17 9 -26" stroke="#64884C" stroke-width="5" stroke-linecap="round"/>'
    if kind=='mushroom':return '<rect x="-6" y="-2" width="12" height="22" rx="5" fill="#EADBBF" stroke="#9C8266"/><path d="M-23 0C-21 -29 21 -29 23 0Q0 11 -23 0Z" fill="#BAA18A" stroke="#8E7967" stroke-width="2"/>'
    if kind in ['plates','bowls','assemble']:
        r='<ellipse cy="4" rx="24" ry="16" fill="#F8EDD8" stroke="#397479" stroke-width="3"/><ellipse cy="2" rx="17" ry="10" fill="none" stroke="#B8C7BA" stroke-width="2"/>'
        if kind=='plates':r='<ellipse cy="10" rx="24" ry="16" fill="#E5DCCB" stroke="#397479" stroke-width="2"/>'+r
        if kind=='bowls':r='<path d="M-24 2Q-21 26 0 25Q21 26 24 2" fill="#EDE1CA" stroke="#397479" stroke-width="3"/>'+r
        if kind=='assemble':r+='<path d="M-16 0-4 -7 -7 6Z M2 -6 15 -2 6 8Z" fill="#D65D46"/><circle cx="-1" cy="4" r="5" fill="#BBCC8D" stroke="#527B46" stroke-width="2"/>'
        return r
    if kind=='prep':return '<rect x="-25" y="-20" width="50" height="36" rx="6" fill="#D0A36F" stroke="#A37950" stroke-width="2"/><path d="M-14 7 4 -13 13 -5 -5 14Z" fill="#CBD2CB" stroke="#6D7D7A"/><path d="M-14 8-20 15" stroke="#435977" stroke-width="7" stroke-linecap="round"/>'
    if kind=='pot':return '<circle r="26" fill="#4D5651"/><rect x="-29" y="-5" width="58" height="13" rx="4" fill="#66503E"/><circle r="20" fill="#A1B3A0" stroke="#F5E9D1" stroke-width="3"/><circle r="15" fill="#D9A84E"/><circle cx="-6" cy="-4" r="4" fill="#DE8436"/><path d="M3 8Q1 -1 10 1L11 6Z" fill="#E6D7B6"/>'
    if kind=='sink':return '<rect x="-25" y="-18" width="50" height="38" rx="8" fill="#CAD7D5" stroke="#74928F" stroke-width="3"/><rect x="-18" y="-11" width="36" height="25" rx="6" fill="#96B6BB"/><path d="M0 -19V-8" stroke="#506C70" stroke-width="6" stroke-linecap="round"/>'
    if kind=='return':return '<rect x="-28" y="-18" width="56" height="39" rx="7" fill="#A1B6BB" stroke="#6B898E" stroke-width="2"/><ellipse rx="19" ry="12" fill="#EDE2CB" stroke="#397479" stroke-width="2"/><path d="M-9 -4 0 1 9 -2" fill="none" stroke="#B88755" stroke-width="4" stroke-linecap="round"/>'
    if kind=='serve':return '<rect x="-26" y="-20" width="52" height="38" rx="6" fill="#F2E7CF" stroke="#397479" stroke-width="3"/><path d="M-15 9Q-15 -11 0 -11Q15 -11 15 9Z" fill="#D7B260"/><circle cy="-14" r="4" fill="#927544"/><path d="M-18 10H18" stroke="#806643" stroke-width="3"/>'
    if kind=='bin':return '<rect x="-21" y="-17" width="42" height="40" rx="9" fill="#B77B61" stroke="#845C49" stroke-width="2"/><ellipse cy="-12" rx="16" ry="6" fill="#745447"/>'
    if kind=='landing':return '<rect x="-25" y="-22" width="50" height="42" rx="6" fill="#E6DCEF" stroke="#8063A8" stroke-width="3" stroke-dasharray="5 4"/><path d="M-10 0H10M0 -10V10" stroke="#8063A8" stroke-width="3"/>'
    return '<rect x="-24" y="-17" width="48" height="34" rx="6" fill="#E8D2AF" stroke="#B8976E" stroke-width="2"/>'

def drawing(l):
    size=72;w=l['cols']*size+84;ox=42;oy=188;bottom=oy+l['rows']*size;h=bottom+235
    def centre(p):return (ox+(p[0]+.5)*size,oy+(p[1]+.5)*size)
    s=[f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" role="img" aria-labelledby="title desc">',f'<title id="title">Level {l["id"]}: {esc(l["title"])}</title>',f'<desc id="desc">Top-down kitchen floor plan. {esc(l["intention"])} Coloured lines show example task routes. Dark short marks show station interaction edges. All stations are accessible without dash or throwing.</desc>',
       '<style>text{font-family:system-ui,-apple-system,sans-serif}.flow{display:none}.food-flow{display:inline}.no-routes .flow{display:none}.show-food .food-flow,.show-soup .soup-flow,.show-wash .wash-flow{display:inline}.show-wash .food-flow,.show-soup .food-flow{display:none}</style>',
       f'<rect width="{w}" height="{h}" rx="22" fill="#F8F2E8"/>', '<defs>']
    for k,c in COLORS.items():s.append(f'<marker id="arrow-{k}" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse"><path d="M0 0 10 5 0 10Z" fill="{c}"/></marker>')
    s+=['</defs>',txt(42,40,f'BARA KITCHEN  /  LEVEL {l["id"]:02d}',16,'#778264',650,'start'),txt(42,83,l['title'],34,'#493C31',650,'start'),txt(42,116,l['subtitle'],19,'#786B5A',400,'start'),txt(w-42,39,'LAYOUT DRAFT 01',13,'#786B5A',550,'end')]
    s += [txt(ox,168,'FAR WALL · windows and tall decoration stay here',14,'#786B5A',450,'start')]
    for y in range(l['rows']):
        for x in range(l['cols']):
            s.append(f'<rect x="{ox+x*size}" y="{oy+y*size}" width="{size}" height="{size}" fill="{["#EEE3D0","#F5ECDB"][(x+y)%2]}" stroke="#E3D7C3" stroke-width=".6"/>')
    s.append(f'<rect x="{ox}" y="{oy}" width="{l["cols"]*size}" height="{l["rows"]*size}" rx="4" fill="none" stroke="#B89367" stroke-width="8"/>')
    # Routes follow actual free-cell paths rather than crossing counters.
    blocked={(a['x'],a['y']) for a in l['stations']};byid={a['id']:a for a in l['stations']}
    for route in l['routes']:
        kind=route['kind'];dash='stroke-dasharray="7 5"' if kind=='wash' else ''
        pairs=list(zip(route['ids'],route['ids'][1:]))
        if kind=='wash' and len(route['ids'])==4:pairs=[('return','sink'),('sink','plates'),('sink','bowls')]
        s.append(f'<g class="flow {kind}-flow">')
        for ia,ib in pairs:
            leg=bfs(access(byid[ia]),access(byid[ib]),blocked,l['cols'],l['rows'])
            pstr=' '.join(f'{x},{y}' for x,y in map(centre,leg))
            s.append(f'<polyline points="{pstr}" fill="none" stroke="#F8F2E8" stroke-width="10" stroke-linejoin="round"/><polyline points="{pstr}" fill="none" stroke="{COLORS[kind]}" stroke-width="4.5" {dash} stroke-linejoin="round" stroke-linecap="round" marker-end="url(#arrow-{kind})"/>')
        for n,sid in enumerate(route['ids']):
            px,py=centre(access(byid[sid]));num=min(n+1,3) if kind=='wash' else n+1
            s += [f'<circle cx="{px}" cy="{py}" r="12" fill="#F8F2E8" stroke="{COLORS[kind]}" stroke-width="2"/>',txt(px,py+5,str(num),13,COLORS[kind],700)]
        s.append('</g>')
    if l['dash']:
        p1,p2=map(centre,l['dash']);s.append(f'<g class="skill-layer"><path d="M{p1[0]} {p1[1]+17}H{p2[0]}" fill="none" stroke="{COLORS["dash"]}" stroke-width="3" stroke-dasharray="10 7" marker-end="url(#arrow-dash)"/>{txt((p1[0]+p2[0])/2,p1[1]+38,"Optional dash aisle",14,COLORS["dash"])}</g>')
    for a in l['stations']:
        x,y=centre((a['x'],a['y']));isSource=a['kind'] in ['tomato','cucumber','carrot','mushroom'];fill='#E9CEAA' if isSource else '#B2BEA2'
        s += [f'<g><rect x="{x-33}" y="{y-33}" width="66" height="66" rx="9" fill="{fill}" stroke="#9C8768" stroke-width="1.5"/><g transform="translate({x},{y-8}) scale(.82)">{icon(a["kind"])}</g>',txt(x,y+25,a.get('label') or LABELS[a['kind']],15,'#3D3E30',600)]
        if a['face'] in ['N','S']:
            fy=y+(-33 if a['face']=='N' else 33);s.append(f'<path d="M{x-11} {fy}H{x+11}" stroke="#4F6155" stroke-width="5" stroke-linecap="round"/>')
        else:
            fx=x+(-33 if a['face']=='W' else 33);s.append(f'<path d="M{fx} {y-11}V{y+11}" stroke="#4F6155" stroke-width="5" stroke-linecap="round"/>')
        s.append('</g>')
    for t in l['throws']:
        p1=centre(t['start']);target=byid[t['target']];p2=centre((target['x'],target['y']))
        s.append(f'<g class="skill-layer"><path d="M{p1[0]} {p1[1]}Q{p1[0]-29} {(p1[1]+p2[1])/2} {p2[0]} {p2[1]-14}" fill="none" stroke="#8063A8" stroke-width="4" stroke-dasharray="6 5" marker-end="url(#arrow-throw)"/><circle cx="{p1[0]}" cy="{p1[1]}" r="7" fill="#F8F2E8" stroke="#8063A8" stroke-width="3"/>{txt(p1[0]+13,p1[1]-11,"Throw",14,"#745998",600,"start")}</g>')
    cx,cy=centre(l['start'])
    s += [f'<g transform="translate({cx},{cy})"><circle r="23" fill="#F8F2E8" stroke="#AA8055" stroke-width="2"/><ellipse cy="2" rx="15" ry="13" fill="#C59966"/><circle cx="-10" cy="-9" r="5" fill="#C59966"/><circle cx="10" cy="-9" r="5" fill="#C59966"/><circle cx="-7" cy="1" r="2" fill="#493C31"/><circle cx="7" cy="1" r="2" fill="#493C31"/><ellipse cy="8" rx="7" ry="5" fill="#A17A53"/></g>',txt(cx,cy-31,'Start',13,'#715538',500)]
    # Customer strip is a separate, non-playable presentation zone.
    s += [f'<rect x="{ox-4}" y="{bottom+5}" width="{l["cols"]*size+8}" height="9" rx="4" fill="#B89367"/>',txt(ox,bottom+42,'CUSTOMER SIDE · automatic service and dirty-dish return',14,'#786B5A',500,'start')]
    for i in range(l['active_orders']):
        cx=ox+52+i*118;cy=bottom+94
        s += [f'<circle cx="{cx}" cy="{cy}" r="27" fill="#D2B081" stroke="#AF8E62" stroke-width="2"/><rect x="{cx-42}" y="{cy-12}" width="15" height="24" rx="5" fill="#A0B090"/><rect x="{cx+27}" y="{cy-12}" width="15" height="24" rx="5" fill="#A0B090"/>']
    legend='Numbered lines = task route. '+('Purple = optional ingredient throw.' if l['throws'] else 'Green = optional dash.' if l['dash'] else 'Dash and throw unlock later.')
    s += [txt(ox+l['cols']*size-8,bottom+88,'Café entrance →',15,'#786B5A',500,'end'),txt(ox+l['cols']*size-8,bottom+113,'Camera looks toward the far wall ↑',13,'#786B5A',400,'end'),
          txt(42,bottom+164,legend,15,'#594D3E',500,'start'),txt(42,bottom+190,'Dark edge = working face. Each square = one counter-width module.',14,'#786B5A',400,'start'),txt(42,bottom+212,'Draft only; collision, reach, camera and timing need a playable blockout.',13,'#786B5A',400,'start'),'</svg>']
    return '\n'.join(s)

if __name__=='__main__':
    audits=[]
    for level in LEVELS:
        audits.append(audit(level));(ROOT/f'level-{level["id"]:02d}.svg').write_text(drawing(level))
    (ROOT/'levels.json').write_text(json.dumps(LEVELS,indent=2)+'\n')
    (ROOT/'layout-audit.json').write_text(json.dumps(audits,indent=2)+'\n')
    print(json.dumps([{'level':a['level'],'stations':a['station_count'],'all_stations_reachable_without_skills':a['all_stations_reachable_without_skills']} for a in audits]))
