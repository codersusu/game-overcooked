"""Generate exact floor plans, browser review and checks from plan_data.py."""
from pathlib import Path
from collections import deque,Counter
import json,html,re
from plan_data import LEVELS
from symbols import icon

ROOT=Path(__file__).resolve().parent
DIRS={'N':(0,-1),'E':(1,0),'S':(0,1),'W':(-1,0)}
COL={'food':'#B65D42','soup':'#AF7D22','wash':'#337B91','throw':'#8460A8','dash':'#657E55'}
NAMES={'tomato':'Tomato','cucumber':'Cucumber','carrot':'Carrot','mushroom':'Mushroom','prep':'Chop','pot':'Cook','dishes':'Dishes','sink':'Wash','return':'Return','serve':'Serve','bin':'Bin'}
def e(s):return html.escape(str(s),quote=True)
def text(x,y,s,size=15,color='#514335',weight=500,anchor='middle'):
    return f'<text x="{x}" y="{y}" fill="{color}" font-size="{size}" font-weight="{weight}" text-anchor="{anchor}">{e(s)}</text>'
def blocks(l):return {(s['x'],s['y']) for s in l['stations']}|{tuple(c) for b in l['barriers'] for c in b['cells']}
def within(l,p):return 0<=p[0]<l['cols'] and 0<=p[1]<l['rows']
def faces(l,s):
    b=blocks(l)
    options=list(DIRS) if s['kind']=='counter' else [s['face']]
    return [(d,(s['x']+DIRS[d][0],s['y']+DIRS[d][1])) for d in options if within(l,(s['x']+DIRS[d][0],s['y']+DIRS[d][1])) and (s['x']+DIRS[d][0],s['y']+DIRS[d][1]) not in b]
def path(l,start,goals):
    start=tuple(start);goals=set(map(tuple,goals));blocked=blocks(l)
    assert start not in blocked and within(l,start)
    q=deque([start]);prev={start:None}
    while q:
        p=q.popleft()
        if p in goals:
            found=[]
            while p is not None:found.append(p);p=prev[p]
            return found[::-1]
        for dx,dy in DIRS.values():
            n=p[0]+dx,p[1]+dy
            if within(l,n) and n not in blocked and n not in prev:prev[n]=p;q.append(n)
    raise AssertionError(f'Level {l["id"]}: no path from {start} to {goals}')
def station_path(l,start,s):return path(l,start,[p for _,p in faces(l,s)])
def audit(l):
    station_cells=[(s['x'],s['y']) for s in l['stations']]
    barrier_cells=[tuple(c) for b in l['barriers'] for c in b['cells']]
    assert len(set(station_cells))==len(station_cells)
    assert len(set(barrier_cells))==len(barrier_cells)
    assert not set(station_cells)&set(barrier_cells)
    assert all(within(l,p) for p in station_cells+barrier_cells)
    for s in l['stations']:
        assert faces(l,s),s
        for _,p in faces(l,s):path(l,l['start'],[p])
    counts=Counter(s['kind'] for s in l['stations'])
    assert not (set(counts)&{'plates','bowls','assemble','landing'})
    assert counts['dishes']==1 and counts['counter']>=6
    for k in ['prep','serve','return','sink','bin']:assert counts[k]
    needed=set(l['ingredients'])
    assert needed==({s['kind'] for s in l['stations']}&{'tomato','cucumber','carrot','mushroom'})
    if 'salad' in l['recipes']:assert {'tomato','cucumber'}<=needed
    if 'soup' in l['recipes']:assert {'carrot','mushroom'}<=needed and counts['pot']
    byid={s['id']:s for s in l['stations']};throws=[]
    rails={tuple(c) for b in l['barriers'] if b['kind']=='rail' for c in b['cells']}
    for t in l['throws']:
        target=byid[t['target']];a=tuple(t['start']);b=target['x'],target['y']
        assert target['kind']=='counter'
        assert abs(a[0]-b[0])+abs(a[1]-b[1])==2
        assert a[0]==b[0] or a[1]==b[1]
        mid=((a[0]+b[0])//2,(a[1]+b[1])//2);assert mid in rails
        path(l,l['start'],[a]);walking=station_path(l,a,target)
        throws.append(dict(target=t['target'],throw_span_modules=2,chef_walk_to_target_modules=len(walking)-1))
    if l['dash']:
        a,b=l['dash'];assert a[0]==b[0] or a[1]==b[1]
        cells=[(x,y) for x in range(min(a[0],b[0]),max(a[0],b[0])+1) for y in range(min(a[1],b[1]),max(a[1],b[1])+1)]
        assert all(p not in blocks(l) for p in cells)
    return dict(level=l['id'],shape=l['shape'],free_counters=counts['counter'],station_counts=dict(counts),all_working_faces_reachable_on_foot=True,recipe_assets_complete=True,one_tableware_type=True,no_recipe_specific_counter=True,barriers_block_walking_and_placement=True,throws=throws,limit='Discrete grid audit, not a collision, throw-arc, timing or fun test.')

def render(l):
    cell=70;ox=38;oy=176;w=2*ox+cell*l['cols'];bottom=oy+cell*l['rows'];h=bottom+178
    def xy(p):return ox+(p[0]+.5)*cell,oy+(p[1]+.5)*cell
    out=[f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" class="plan" role="img" aria-labelledby="title desc">',f'<title id="title">{e(l["title"])}</title>',f'<desc id="desc">{e(l["headline"])} All C-numbered counters are interchangeable. Low rails block walking and placement but ingredients can be thrown over them.</desc>',
         '<style>text{font-family:system-ui,-apple-system,sans-serif}.flow{display:none}.show-food .food-flow,.show-soup .soup-flow,.show-wash .wash-flow{display:inline}.no-skills .skill-layer{display:none}</style>',f'<rect width="{w}" height="{h}" rx="18" fill="#F9F3E8"/>','<defs>']
    for k,c in COL.items():out.append(f'<marker id="arr-{k}" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="6" markerHeight="6" orient="auto"><path d="M0 0 10 5 0 10Z" fill="{c}"/></marker>')
    out+=['<pattern id="rail-hatch" width="10" height="10" patternUnits="userSpaceOnUse" patternTransform="rotate(45)"><path d="M0 0V10" stroke="#AE805F" stroke-width="3"/></pattern></defs>',text(ox,34,f'BARA KITCHEN / {l["id"]:02d}',14,'#6E805E',650,'start'),text(w-ox,34,'LAYOUT REVIEW 02',12,'#7B6C5A',550,'end'),text(ox,74,l['title'],31,'#493B30',650,'start'),text(ox,107,l['subtitle'],17,'#75624F',400,'start')]
    if l['id']==4:
        for x,label in [(1.5,'PANTRY'),(6.5,'PREPARATION'),(11.5,'COOK & SERVE')]:out.append(text(ox+(x+.5)*cell,158,label,14,'#6B715A',650))
    else:out.append(text(ox,156,l['shape'].upper(),14,'#6B715A',600,'start'))
    for y in range(l['rows']):
        for x in range(l['cols']):
            out.append(f'<rect x="{ox+x*cell}" y="{oy+y*cell}" width="{cell}" height="{cell}" fill="{["#F1E7D4","#FAF1E1"][(x+y)%2]}" stroke="#E8DCC8" stroke-width=".5"/>')
    out.append(f'<rect x="{ox}" y="{oy}" width="{cell*l["cols"]}" height="{cell*l["rows"]}" rx="3" fill="none" stroke="#B59264" stroke-width="7"/>')
    for b in l['barriers']:
        for x,y in b['cells']:
            px=ox+x*cell;py=oy+y*cell
            if b['kind']=='rail':
                out.append(f'<g><rect x="{px}" y="{py}" width="{cell}" height="{cell}" fill="#D5BA99"/><rect x="{px+4}" y="{py+4}" width="{cell-8}" height="{cell-8}" rx="7" fill="url(#rail-hatch)" stroke="#A57854" stroke-width="2"/><path d="M{px+27} {py+27}l16 16m0 -16-16 16" stroke="#896344" stroke-width="3"/></g>')
            else:out.append(f'<rect x="{px}" y="{py}" width="{cell}" height="{cell}" fill="#AEAD95"/>'+text(px+35,py+40,'Core',13,'#62644F'))
    byid={s['id']:s for s in l['stations']}
    # Draw route legs along free cells; choose the nearest reachable face of generic counters.
    for r in l['routes']:
        kind=r['kind'];current=station_path(l,l['start'],byid[r['ids'][0]])[-1];anchors=[current]
        out.append(f'<g class="flow {kind}-flow">')
        for sid in r['ids'][1:]:
            leg=station_path(l,current,byid[sid]);current=leg[-1];anchors.append(current)
            pts=' '.join(f'{x},{y}' for x,y in map(xy,leg));dash='stroke-dasharray="6 5"' if kind=='wash' else ''
            out.append(f'<polyline points="{pts}" fill="none" stroke="#FFFAEF" stroke-width="10" stroke-linejoin="round"/><polyline points="{pts}" fill="none" stroke="{COL[kind]}" stroke-width="4" {dash} stroke-linejoin="round" marker-end="url(#arr-{kind})"/>')
        for n,p in enumerate(anchors):
            x,y=xy(p);out+=[f'<circle cx="{x}" cy="{y}" r="12" fill="#FFFAEF" stroke="{COL[kind]}" stroke-width="2"/>',text(x,y+5,n+1,13,COL[kind],700)]
        out.append('</g>')
    for s in l['stations']:
        x,y=xy((s['x'],s['y']));counter=s['kind']=='counter';source=s['kind'] in l['ingredients']
        fill='#EDD3AE' if counter else '#E9C69E' if source else '#AEBEA0'
        out.append(f'<g class="station" data-kind="{s["kind"]}"><rect x="{x-32}" y="{y-32}" width="64" height="64" rx="8" fill="{fill}" stroke="#A4906C" stroke-width="1.5"/>')
        out.append(f'<g transform="translate({x},{y-8}) scale(.80)">{icon(s["kind"])}</g>')
        out.append(text(x,y+25,s['id'] if counter else NAMES[s['kind']],15,'#414635',600))
        for d,_ in faces(l,s):
            if d in ('N','S'):
                yy=y+(-32 if d=='N' else 32);out.append(f'<path d="M{x-9} {yy}H{x+9}" stroke="#536A59" stroke-width="4" stroke-linecap="round"/>')
            else:
                xx=x+(-32 if d=='W' else 32);out.append(f'<path d="M{xx} {y-9}V{y+9}" stroke="#536A59" stroke-width="4" stroke-linecap="round"/>')
        out.append('</g>')
    # Clearly distinguish air routes from walkable openings.
    for t in l['throws']:
        a=xy(t['start']);target=byid[t['target']];b=xy((target['x'],target['y']))
        dx,dy=b[0]-a[0],b[1]-a[1];end=(b[0]-(12 if dx>0 else -12 if dx<0 else 0),b[1]-(12 if dy>0 else -12 if dy<0 else 0))
        out.append(f'<g class="skill-layer"><circle cx="{a[0]}" cy="{a[1]}" r="6" fill="#F9F3E8" stroke="{COL["throw"]}" stroke-width="2.5"/><path d="M{a[0]} {a[1]}Q{(a[0]+b[0])/2-10} {(a[1]+b[1])/2-10} {end[0]} {end[1]}" fill="none" stroke="{COL["throw"]}" stroke-width="3.5" stroke-dasharray="5 4" marker-end="url(#arr-throw)"/><circle cx="{b[0]}" cy="{b[1]-7}" r="22" fill="none" stroke="{COL["throw"]}" stroke-width="2" stroke-dasharray="4 4"/></g>')
    if l['dash']:
        a,b=map(xy,l['dash']);out.append(f'<g class="skill-layer"><path d="M{a[0]} {a[1]}L{b[0]} {b[1]}" fill="none" stroke="{COL["dash"]}" stroke-width="3" stroke-dasharray="9 6" marker-end="url(#arr-dash)"/>{text(a[0]-10,(a[1]+b[1])/2-14,"Dash",13,COL["dash"],500,"end")}</g>')
    x,y=xy(l['start']);out.append(f'<g transform="translate({x},{y})"><circle r="20" fill="#FFF7E8" stroke="#B78E5D" stroke-width="2"/><ellipse cy="2" rx="13" ry="11" fill="#C39965"/><circle cx="-9" cy="-8" r="4" fill="#C39965"/><circle cx="9" cy="-8" r="4" fill="#C39965"/><circle cx="-6" cy="0" r="1.6" fill="#493B30"/><circle cx="6" cy="0" r="1.6" fill="#493B30"/><ellipse cy="7" rx="6" ry="4" fill="#9F7548"/></g>')
    out.append(text(x,y-28,'Start',12,'#735936'))
    if l['id']==3:out.append(text(ox+cell,oy+4.5*cell+12,'PASSAGE',11,'#846342',700))
    if l['id']==4:
        for p,label in [([4,7.5],'A'),([9,.5],'B')]:
            x,y=xy(p);out.append(f'<circle cx="{x}" cy="{y}" r="13" fill="#FAF1E1" stroke="#9D8459" stroke-width="2"/>'+text(x,y+5,label,14,'#7D613D',700))
    count=sum(s['kind']=='counter' for s in l['stations'])
    out += [text(ox,bottom+36,f'{count} free counters · one serving-dish type · all stations reachable on foot',15,'#495C44',600,'start'),
            text(ox,bottom+64,'C1, C2… are ordinary counters: ingredients, clean dishes, assembly or finished food.',13,'#776652',400,'start')]
    if l['barriers']:out.append(text(ox,bottom+89,'Hatched rail = no walking or placement. Purple arrows show ingredients passing over it.',13,'#776652',400,'start') if l['throws'] else text(ox,bottom+89,'The island blocks the centre. Choose a route around either side.',13,'#776652',400,'start'))
    out += [text(ox,bottom+115,'Dark counter edges = reachable working sides. Arrows are examples, not fixed recipes for a station.',12,'#776652',400,'start'),text(ox,bottom+141,'Customers are beyond the kitchen: serving and dish return happen at the labeled endpoints.',12,'#776652',400,'start'),text(ox,bottom+165,'Concept floor plan. Test collision, reach, throw clearance, camera and pacing in the blockout.',12,'#776652',400,'start'),'</svg>']
    return '\n'.join(out)

def prefix(svg,p):
    svg=re.sub(r'\bid="([^"]+)"',lambda m:f'id="{p}{m.group(1)}"',svg)
    svg=re.sub(r'url\(#([^)]+)\)',lambda m:f'url(#{p}{m.group(1)})',svg)
    return svg.replace('aria-labelledby="title desc"',f'aria-labelledby="{p}title {p}desc"')

def overview():
    out=['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1400 1200" role="img" aria-label="Four distinct kitchen route designs"><style>text{font-family:system-ui,sans-serif}</style><rect width="1400" height="1200" fill="#F5EFE4"/>',text(40,47,'BARA KITCHEN / FOUR DIFFERENT ROUTE PROBLEMS',26,'#493B30',650,'start')]
    for i,l in enumerate(LEVELS):
        x=40+(i%2)*700;y=83+(i//2)*555
        out += [text(x,y+22,f'{l["id"]:02d} · {l["shape"]}',24,'#493B30',600,'start'),text(x,y+51,f'{sum(s["kind"]=="counter" for s in l["stations"])} free counters · '+('Salad' if l['id']==1 else 'Soup + dash' if l['id']==2 else 'Both recipes + throws'),17,'#73775D',450,'start')]
        svg=prefix((ROOT/f'level-{l["id"]:02d}.svg').read_text(),f'ov{i}-')
        sw=76+l['cols']*70;sh=l['rows']*70+20
        opening=f'<svg x="{x}" y="{y+69}" width="640" height="435" viewBox="20 163 {sw-40} {sh}" overflow="hidden"><defs><clipPath id="map-crop-{i}"><rect x="20" y="163" width="{sw-40}" height="{sh}"/></clipPath></defs><g clip-path="url(#map-crop-{i})">'
        svg=re.sub(r'<svg [^>]+>',opening,svg,count=1)
        svg=svg.rsplit('</svg>',1)[0]+'</g></svg>'
        out.append(svg)
    out.append(text(40,1170,'Any free counter can assemble a meal. One reusable dish serves salad and soup.',20,'#51634A',550,'start'));out.append('</svg>')
    (ROOT/'overview.svg').write_text('\n'.join(out))

def main():
    audits=[audit(l) for l in LEVELS]
    for l in LEVELS:(ROOT/f'level-{l["id"]:02d}.svg').write_text(render(l))
    (ROOT/'levels.json').write_text(json.dumps(LEVELS,indent=2)+'\n')
    (ROOT/'layout-audit.json').write_text(json.dumps(audits,indent=2)+'\n')
    overview()
    print(json.dumps([{'level':a['level'],'free_counters':a['free_counters'],'walkable':a['all_working_faces_reachable_on_foot'],'throws':a['throws']} for a in audits],indent=2))

if __name__=='__main__':main()
