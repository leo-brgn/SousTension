"""Release 1.0 cargo V1: distinct salvage, spares and five modular Signal pieces."""
import os, sys, json, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lib import *

def grip(a, z=.3):
    a.mount('Mount_Grip', (0,0,z)); return a

def drum(name, color):
    a=Asset(name); p=a.part('Body')
    p.cyl((0,0,.46), .29,.92,color,segs=24,bevel=.018)
    for z in (.04,.24,.68,.9): p.disc_ring((0,0,z),.285,.305,.035,C['steel_d'],segs=24)
    cap=a.part('Bung',location=(.14,0,.94)); cap.cyl((0,0,0),.036,.025,C['brass_d'],segs=12,bevel=.004)
    p.box((0,-.293,.5),(.28,.009,.20),C['cream'],bevel=.003)
    a.mount('Mount_Label',(0,-.30,.5))
    return grip(a,.46)

def fuel_cask():
    a=Asset('NuclearFuelTransportCask');p=a.part('ShieldBody')
    p.cyl((0,0,.52),.37,.98,C['lead'],segs=24,bevel=.02)
    for i in range(16):
        t=i*math.tau/16;p.box((.36*math.cos(t),.36*math.sin(t),.53),(.085,.045,.84),C['steel_d'],rot=(0,0,t),bevel=.008)
    lid=a.part('BoltedLid',location=(0,0,1.03));lid.cyl((0,0,0),.39,.065,C['steel'],segs=24,bevel=.009)
    for i in range(8):
        t=i*math.tau/8;lid.cyl((.32*math.cos(t),.32*math.sin(t),.045),.025,.025,C['brass_d'],segs=6)
    for x in(-.44,.44):p.torus((x,0,.72),.09,.017,C['steel'],axis='X',major_segs=16)
    a.mount('Mount_GripLeft',(-.44,0,.72));a.mount('Mount_GripRight',(.44,0,.72));a.mount('Mount_Radiation',(0,0,.5));return a

def spare(name,kind):
    a=Asset(name);p=a.part('Body')
    if kind=='fuse':
        p.box((0,0,.045),(.32,.22,.09),C['bottle'],bevel=.01)
        for x in(-.10,0,.10):
            p.cyl((x,0,.10),.015,.15,C['cream'],axis='Y',segs=12)
            for y in(-.075,.075):p.cyl((x,y,.10),.017,.026,C['brass'],axis='Y',segs=12)
    elif kind in('gasket','bearing'):
        p.disc_ring((0,0,.035),.07,.125,.05,C['bakelite'] if kind=='gasket' else C['steel_l'])
        if kind=='bearing':
            for i in range(10):t=i*math.tau/10;p.sphere((.095*math.cos(t),.095*math.sin(t),.065),(.015,.015,.015),C['brass_d'])
    elif kind=='impeller':
        p.cyl((0,0,.04),.15,.04,C['brass_d'],segs=24)
        p.cyl((0,0,.085),.04,.10,C['steel'],segs=12)
        for i in range(8):t=i*math.tau/8;p.box((.085*math.cos(t),.085*math.sin(t),.085),(.12,.018,.065),C['brass'],rot=(0,0,t+.35),bevel=.003)
    elif kind=='motor':
        p.cyl((0,0,.18),.16,.42,C['bottle'],axis='X',segs=20,bevel=.01)
        p.cyl((.29,0,.18),.035,.18,C['steel_l'],axis='X',segs=12)
        for x in(-.16,-.10,-.04,.02,.08,.14):p.disc_ring((x,0,.18),.15,.17,.02,C['steel_d'],axis='X',segs=20)
        for x in(-.12,.12):p.box((x,0,.035),(.09,.28,.07),C['steel_d'],bevel=.006)
    else:
        p.disc_ring((0,0,.20),.08,.105,.40,C['steel'],segs=20)
        for z in(.02,.38):p.disc_ring((0,0,z),.08,.145,.04,C['brass_d'])
    return grip(a,.12)

def chest(name='SalvageChest',music=False):
    a=Asset(name);p=a.part('Case');w=.26 if music else .72;d=.20 if music else .46;h=.16 if music else .43
    # Hollow case keeps a usable storage cavity.
    p.box((0,0,.02),(w,d,.04),C['rust'],bevel=.008)
    for x in(-w/2,w/2):p.box((x,0,h/2),(.035,d,h),C['rust_d'],bevel=.007)
    for y in(-d/2,d/2):p.box((0,y,h/2),(w,.035,h),C['rust'],bevel=.007)
    lid=a.part('Lid',location=(0,d/2,h));lid.box((0,-d/2,0),(w+.03,d+.03,.045),C['rust_d'],bevel=.012)
    p.box((0,-d/2-.024,h*.72),(.07,.03,.09),C['brass'],bevel=.004)
    if music:
        p.cyl((0,0,.11),.05,.16,C['brass'],axis='X',segs=16)
        for x in(-.075,-.025,.025,.075):p.box((x,-.05,.11),(.008,.075,.02),C['steel_l'])
        a.mount('Mount_Sound',(0,0,.13))
    return grip(a,h/2)

def bell():
    a=Asset('SalvageShipBell');p=a.part('Bell')
    p.disc_ring((0,0,.08),.12,.15,.045,C['brass'])
    p.cyl((0,0,.20),.145,.24,C['brass'],r2=.045,segs=24,caps=False)
    p.torus((0,0,.35),.045,.012,C['brass_d'],axis='Y')
    c=a.part('Clapper',location=(0,0,.27));c.cyl((0,0,-.10),.01,.20,C['steel']);c.sphere((0,0,-.20),(.025,.025,.025),C['steel'])
    a.mount('Mount_Sound',(0,0,.15));return grip(a,.35)

def propeller():
    a=Asset('SalvageBronzePropeller');p=a.part('Body');p.disc_ring((0,0,.055),.028,.075,.11,C['brass_d'])
    for i in range(3):
        t=i*math.tau/3;p.box((.17*math.cos(t),.17*math.sin(t),.055),(.30,.12,.025),C['brass'],rot=(.18,0,t+.25),bevel=.025)
    return grip(a,.055)

def sextant():
    a=Asset('AntiqueSextant');p=a.part('Frame');p.torus((0,0,.15),.18,.014,C['brass'],axis='Y',arc=math.pi*.65)
    for t in(0,math.pi*.65):p.box((.085*math.cos(t),0,.15+.085*math.sin(t)),(.19,.025,.023),C['brass_d'],rot=(0,-t,0),bevel=.004)
    arm=a.part('IndexArm',location=(0,0,.15));arm.box((.085,0,0),(.18,.035,.025),C['steel'],bevel=.004)
    p.cyl((.02,-.07,.15),.025,.16,C['bakelite'],axis='Y',segs=16);return grip(a,.15)

def jetski():
    a=Asset('BrokenJetSki');p=a.part('Hull');p.sphere((0,0,.25),(.42,1.08,.25),C['cream_d'],u=20,v=10)
    p.sphere((0,.15,.52),(.23,.58,.17),C['bakelite'],u=16,v=10)
    p.box((0,-.45,.68),(.08,.08,.28),C['steel'],rot=(.25,0,0),bevel=.01)
    p.cyl((0,-.49,.81),.025,.48,C['steel_d'],axis='X');p.box((.32,.58,.32),(.14,.15,.08),C['rust'],bevel=.007)
    return grip(a,.4)

def container():
    a=Asset('ModernContainer');p=a.part('Shell');w,d,h=1.2,1.9,1.15
    p.box((0,0,.04),(w,d,.08),C['steel_d'],bevel=.01)
    p.box((0,0,h),(w,d,.06),C['cream_d'],bevel=.01)
    p.box((0,d/2,h/2),(w,.05,h),C['bottle'],bevel=.01)
    for x in(-w/2,w/2):
        p.box((x,0,h/2),(.05,d,h),C['bottle'],bevel=.01)
        for y in(-.8,-.6,-.4,-.2,0,.2,.4,.6,.8):p.box((x*1.04,y,h/2),(.025,.035,h-.1),C['bottle_d'])
    for side in(-1,1):
        door=a.part('DoorLeft' if side<0 else 'DoorRight',location=(side*w/2,-d/2,.06))
        door.box((-side*w/4,0,(h-.06)/2),(w/2,.045,h-.06),C['bottle_d'],bevel=.01)
        door.cyl((-side*w/4,-.04,.53),.015,.94,C['steel'],segs=8)
    return grip(a,.58)

def duck():
    a=Asset('InflatableDuck');p=a.part('Body');p.sphere((0,0,.16),(.25,.35,.17),C['amber'])
    p.sphere((0,-.24,.35),(.13,.13,.14),C['amber']);p.box((0,-.39,.33),(.14,.14,.05),C['rust'],bevel=.02)
    for x in(-.115,.115):p.sphere((x,-.28,.39),(.018,.016,.018),C['black'],u=10,v=8)
    return grip(a,.18)

def panel():
    a=Asset('SolarPanel');p=a.part('Frame');p.box((0,0,.025),(.68,1.0,.05),C['steel'],bevel=.012)
    p.box((0,0,.055),(.63,.95,.01),C['bottle_d'])
    for x in(-.21,0,.21):p.box((x,0,.064),(.005,.95,.003),C['steel_l'])
    for y in(-.38,-.19,0,.19,.38):p.box((0,y,.064),(.63,.005,.003),C['steel_l'])
    a.mount('Mount_Cable',(0,.50,.025));return grip(a,.025)

def beacon():
    a=Asset('ActiveBeacon');p=a.part('Housing');p.cyl((0,0,.15),.12,.3,C['bottle'],segs=16,bevel=.01)
    lens=a.part('Lens',location=(0,0,.32));lens.sphere((0,0,0),(.11,.11,.10),C['amber'])
    p.cyl((.08,0,.48),.009,.32,C['steel_l'],segs=8);a.mount('Mount_Sound',(0,0,.2));a.mount('Mount_Light',(0,0,.36));return grip(a,.15)

def raft():
    a=Asset('LifeRaft1983');p=a.part('Pontoons')
    for x in(-.65,.65):p.sphere((x,0,.2),(.18,1.08,.18),C['rust'],u=16,v=10)
    for y in(-.90,.90):p.sphere((0,y,.2),(.65,.18,.18),C['rust'],u=16,v=10)
    p.box((0,0,.11),(1.1,1.65,.10),C['cream_d'],bevel=.02)
    for x in(-.50,.50):p.box((x,0,.19),(.025,1.5,.015),C['bakelite'])
    return grip(a,.2)

def book():
    a=Asset('OldLogbook');p=a.part('Pages');p.box((0,0,.045),(.22,.30,.07),C['cream_d'],bevel=.003)
    p.box((0,0,.005),(.24,.32,.01),C['bottle_d'],bevel=.003)
    c=a.part('FrontCover',location=(-.12,0,.085));c.box((.12,0,0),(.24,.32,.012),C['bottle_d'],bevel=.003)
    c.box((.12,0,.009),(.12,.07,.003),C['brass_d']);return grip(a,.045)

def medals():
    a=chest('KravicMedalCase',True)
    a.parts[0].bm.clear();p=a.parts[0];p.col=p.bm.loops.layers.color.new('Col')
    p.box((0,0,.025),(.30,.21,.05),C['bakelite'],bevel=.01)
    for x in(-.09,0,.09):
        p.cyl((x,0,.065),.03,.01,C['brass'],segs=12);p.box((x,.045,.06),(.025,.055,.008),C['bottle'])
    return a

def emitter_piece(index):
    names=['SignalEmitterFrame','SignalEmitterPower','SignalEmitterCoil','SignalEmitterAerial','SignalEmitterControls']
    a=Asset(names[index]);p=a.part('Body')
    if index==0:
        for x in(-.45,.45):
            for y in(-.28,.28):p.box((x,y,.55),(.06,.06,1.10),C['bottle'],bevel=.008)
        for z in(.08,.60,1.08):p.box((0,0,z),(.96,.64,.05),C['steel_d'],bevel=.008)
        for i,loc in enumerate(((0,0,.12),(0,0,.64),(0,0,1.12),(0,-.33,.78))):a.mount('Mount_Module'+str(i+1),loc)
    elif index==1:
        p.box((0,0,.21),(.72,.47,.42),C['bottle_d'],bevel=.02)
        for x in(-.22,0,.22):p.cyl((x,0,.48),.065,.22,C['glass'],segs=16,bevel=.008)
        for y in(-.15,.15):p.box((0,y,.43),(.62,.035,.025),C['brass'])
    elif index==2:
        p.cyl((0,0,.18),.11,.36,C['bakelite'],segs=16)
        for z in(.04,.07,.10,.13,.16,.19,.22,.25,.28,.31):p.torus((0,0,z),.13,.012,C['brass'],major_segs=20)
        for x in(-.25,.25):p.cyl((x,0,.16),.045,.32,C['cream'],segs=16)
    elif index==3:
        p.cyl((0,0,.24),.035,.48,C['steel'],segs=12)
        p.disc_ring((0,0,.5),.06,.28,.035,C['brass_d'],segs=32)
        for i in range(8):t=i*math.tau/8;p.box((.14*math.cos(t),.14*math.sin(t),.50),(.30,.014,.016),C['brass'],rot=(0,0,t))
    else:
        p.box((0,0,0),(.65,.16,.24),C['cream_d'],bevel=.014)
        for x in(-.22,0,.22):
            k=a.part('Knob'+str(x),location=(x,-.11,0));k.cyl((0,0,0),.035,.05,C['bakelite'],axis='Y',segs=16)
        a.mount('Mount_Meter',(0,-.09,.06))
    return grip(a,.18)

def assembled():
    a=Asset('SignalEmitterAssembled')
    for i,loc in enumerate(((0,0,0),(0,0,.12),(0,0,.64),(0,0,1.12),(0,-.33,.78))):
        other=emitter_piece(i)
        for src in other.parts:
            p=a.part('Module%d_%s'%(i,src.name),location=tuple(loc[j]+src.location[j] for j in range(3)))
            me=bpy.data.meshes.new('TemporaryModule');src.bm.to_mesh(me);p.bm.from_mesh(me);bpy.data.meshes.remove(me)
    a.mount('Mount_SignalOrigin',(0,0,1.62));return a

def build_all():
    return [drum('FuelDrum',C['bottle']),drum('BrineDrum',C['cream_d']),drum('SealedDrum',C['steel_d']),fuel_cask(),
        spare('SpareFuseBox','fuse'),spare('SpareGasket','gasket'),spare('SpareBearing','bearing'),spare('SpareImpeller','impeller'),spare('SpareMotor','motor'),spare('SparePipeSection','pipe'),
        chest(),bell(),propeller(),sextant(),jetski(),container(),duck(),panel(),beacon(),chest('JammedMusicBox',True),raft(),book(),medals()]+[emitter_piece(i) for i in range(5)]+[assembled()]

if __name__=='__main__':
    out,preview=cli_args();reset_scene();assets=build_all();os.makedirs(out,exist_ok=True)
    report=[]
    for a in assets:
        a.build()
        for p in a.parts:
            me=p.ob.data;me.calc_loop_triangles()
            assert all(t.area>1e-12 for t in me.loop_triangles),(a.name,p.name)
            assert all(math.isfinite(v) for vert in me.vertices for v in vert.co)
            uv=me.uv_layers.new(name='UVMap')
            for l in me.loops:
                v=me.vertices[l.vertex_index].co;uv.data[l.index].uv=(v.x+.5,v.y+.5)
        a.export(out);print('EXPORT',a.name,a.stats())
        report.append({'name':a.name,'triangles':a.stats(),'parts':[p.name for p in a.parts],
            'pivots':[{'name':p.name,'position':p.location} for p in a.parts],
            'mounts':[{'name':n,'position':loc} for n,loc,rot in a.mounts]})
    with open(os.path.join(out,'cargo_manifest.json'),'w') as f:json.dump({'scope':'1.0 cargo geometry V1; masses require gameplay balancing','assets':report},f,indent=2)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(__file__),'sources','Cargo.blend'))
    if preview:
        os.makedirs(preview,exist_ok=True);render_each(assets,preview,resolution=(800,650))
