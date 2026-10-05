"""Issue119 Sas & vie. Authored geometry, metre scale, Z up, front -Y."""
import os,sys,math,json
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from lib import *

def bolts(p,c,r,axis='Y',count=10):
 for i in range(count):
  t=math.tau*i/count;v=(c[0]+r*math.sin(t),c[1],c[2]+r*math.cos(t)) if axis=='Y' else(c[0]+r*math.sin(t),c[1]+r*math.cos(t),c[2]);p.cyl(v,.014,.018,C['steel_l'],axis=axis,segs=6)
def bunk():
 a=Asset('BunkBed_Double');p=a.part('Frame')
 for x in(-.98,.98):
  for y in(-.38,.38):p.cyl((x,y,.96),.027,1.92,C['bottle'],segs=12)
 for level,z in enumerate((.43,1.30)):
  p.box((0,0,z),(2.05,.85,.075),C['bottle_d'],bevel=.025);p.box((0,0,z+.092),(1.96,.77,.11),C['cream_d'],bevel=.035);p.box((-.76,0,z+.16),(.38,.57,.13),C['cream'],bevel=.045);p.box((.26,0,z+.163),(1.28,.75,.032),C['bottle'],bevel=.01)
  if level:
   p.cyl((0,-.40,z+.29),.021,1.65,C['bottle'],axis='X',segs=12)
   for x in(-.81,0,.81):p.cyl((x,-.40,z+.17),.02,.25,C['bottle'],segs=10)
  a.mount('Mount_Sleep'+str(level),(0,0,z+.15))
 for x in(.56,.91):p.cyl((x,-.49,.76),.020,1.45,C['steel'],segs=10)
 for z in(.18,.43,.68,.93,1.18,1.43):p.cyl((.735,-.49,z),.018,.35,C['steel'],axis='X',segs=10)
 return a

def locker():
 a=Asset('PersonalLocker');p=a.part('Cabinet');p.box((0,.21,.90),(.57,.04,1.76),C['bottle_d'],bevel=.012)
 for x in(-.28,.28):p.box((x,0,.90),(.035,.45,1.8),C['bottle'],bevel=.01)
 for z in(.04,.65,1.35,1.78):p.box((0,0,z),(.56,.45,.035),C['steel_d'],bevel=.008)
 d=a.part('Door',location=(-.28,-.24,.04));d.box((.28,0,.86),(.55,.035,1.72),C['bottle'],bevel=.01);d.box((.45,-.028,.83),(.025,.025,.17),C['brass'],bevel=.006);d.box((.27,-.020,1.4),(.18,.004,.08),C['cream'],bevel=.001)
 for z in(1.16,1.20,1.24):d.box((.28,-.02,z),(.28,.006,.008),C['black'],bevel=.001)
 a.mount('Mount_Storage',(0,0,.7));return a

def stove():
 a=Asset('GalleyStove');p=a.part('Body');p.box((0,0,.46),(.86,.64,.86),C['cream_d'],bevel=.035);p.box((0,0,.91),(.92,.69,.065),C['cream'],bevel=.020)
 for x in(-.22,.22):
  p.disc_ring((x,0,.955),.075,.145,.018,C['steel_d'],segs=24)
  for k in range(4):ang=k*math.pi/2;p.box((x+math.sin(ang)*.12,math.cos(ang)*.12,.978),(.045,.08,.025),C['black'],rot=(0,0,-ang),bevel=.006)
  knob=a.part('HeatKnob'+str(x),location=(x,-.337,.77));knob.cyl((0,0,0),.043,.04,C['bakelite'],axis='Y',segs=12);knob.box((0,-.025,.018),(.008,.006,.025),C['cream'],bevel=.002)
 d=a.part('OvenDoor',location=(0,-.34,.19));d.box((0,0,.24),(.70,.035,.48),C['bottle'],bevel=.012);d.box((0,-.025,.25),(.48,.015,.28),C['black'],bevel=.006);d.cyl((0,-.05,.43),.016,.52,C['steel'],axis='X',segs=12)
 for x in(-.33,.33):
  for y in(-.24,.24):p.cyl((x,y,.06),.035,.12,C['steel_d'],segs=10)
 a.mount('Mount_Pot',(-.22,0,.99));return a

def pot():
 a=Asset('SoupPot');p=a.part('Pot');p.cyl((0,0,.17),.24,.31,C['steel'],r2=.27,caps=False,segs=28);p.cyl((0,0,.02),.24,.025,C['steel'],segs=28);p.torus((0,0,.326),.27,.012,C['cream'],major_segs=28)
 for x in(-.31,.31):p.torus((x,0,.24),.062,.013,C['bakelite'],axis='Y',major_segs=16)
 soup=a.part('SoupSurface',location=(0,0,.28));soup.cyl((0,0,0),.251,.008,C['cream_d'],segs=28)
 for i in range(9):t=i*2.399;r=.05+.017*i;soup.sphere((r*math.cos(t),r*math.sin(t),.005),(.025,.018,.015),C['rust'],u=8,v=6)
 a.mount('Mount_Spill',(0,0,.325));return a

def table():
 a=Asset('MessTable');p=a.part('Body');p.box((0,0,.75),(1.25,.75,.06),C['rust'],bevel=.020)
 for x in(-.44,.44):p.cyl((x,0,.37),.038,.72,C['steel_d'],segs=12);p.box((x,0,.04),(.12,.62,.06),C['steel'],bevel=.012)
 return a

def bench():
 a=Asset('MessBench');p=a.part('Body');p.box((0,0,.46),(1.25,.38,.08),C['bottle'],bevel=.025);p.box((0,.15,.74),(1.25,.055,.46),C['bottle'],bevel=.018)
 for x in(-.45,.45):p.box((x,0,.24),(.055,.29,.40),C['steel_d'],bevel=.012)
 return a

def shower():
 a=Asset('DeconShowerCabin');p=a.part('Cabin');p.box((0,0,.04),(1.1,1.0,.08),C['bottle_d'],bevel=.025)
 for x in(-.52,.52):p.box((x,.15,1.09),(.045,.72,2.1),C['cream'],bevel=.012)
 p.box((0,.50,1.09),(1.08,.05,2.1),C['cream_d'],bevel=.012)
 for x in(-.51,.51):p.cyl((x,-.25,1.12),.027,2.15,C['bottle'],segs=12)
 p.cyl((0,-.25,2.18),.027,1.02,C['bottle'],axis='X',segs=12)
 for z in(.32,.70,1.08,1.46,1.84):p.box((0,.466,z),(1.01,.008,.012),C['cream'],bevel=.002)
 p.cyl((-.22,.43,1.34),.03,1.34,C['brass_d'],segs=12);p.cyl((-.22,.29,1.96),.025,.30,C['brass'],axis='Y',segs=12);p.cyl((-.22,.13,1.91),.09,.07,C['brass'],segs=20,r2=.06);p.disc_ring((0,0,.088),.03,.09,.015,C['steel'],segs=20)
 valve=a.part('MixingLever',location=(-.22,.36,1.20));valve.cyl((0,0,.07),.013,.14,C['brass'],segs=10);valve.sphere((0,0,.15),.025,C['bakelite'])
 p.box((.28,.40,1.37),(.23,.13,.34),C['cream'],bevel=.025);p.cyl((.28,.40,1.58),.018,.09,C['rust'],segs=12)
 a.mount('Mount_Spray',(-.22,.13,1.86));a.mount('Mount_Hose',(-.22,.38,.55));return a

def lance():
 a=Asset('DeconWaterLance');p=a.part('Body');p.cyl((0,0,.08),.025,.20,C['brass_d'],segs=12);p.cyl((0,-.08,.19),.021,.18,C['brass'],axis='Y',segs=12);p.cyl((0,-.17,.19),.036,.03,C['steel'],axis='Y',segs=16,r2=.022)
 trigger=a.part('Trigger',location=(0,-.031,.14));trigger.box((0,0,-.035),(.017,.018,.07),C['steel'],bevel=.004);a.mount('Mount_Grip',(0,0,.06));a.mount('Mount_Spray',(0,-.19,.19));a.mount('Mount_Hose',(0,0,-.025));return a

def helmet():
 a=Asset('DivingHelmet');p=a.part('Body');p.sphere((0,0,.18),(.20,.18,.21),C['brass'],u=24,v=16);p.disc_ring((0,0,.02),.13,.22,.035,C['brass_d'],segs=24);p.disc_ring((0,-.17,.18),.104,.132,.035,C['brass_d'],axis='Y',segs=24);p.cyl((0,-.19,.18),.103,.009,C['glass'],axis='Y',segs=24);bolts(p,(0,-.199,.18),.12,count=8)
 for x in(-.20,.20):p.cyl((x,0,.18),.065,.04,C['brass_d'],axis='X',segs=16)
 a.mount('Mount_Neck',(0,0,0));a.mount('Mount_Air',(.22,0,.18));return a

def suit():
 a=Asset('DivingSuitOnRack');p=a.part('Rack');p.box((0,.18,.04),(.70,.60,.08),C['steel_d'],bevel=.02);p.cyl((0,.20,.90),.04,1.7,C['steel'],segs=12);p.cyl((0,.10,1.48),.025,.75,C['steel'],axis='X',segs=12)
 b=a.part('Suit');b.sphere((0,0,1.06),(.26,.16,.39),C['cream_d'],u=16,v=12)
 for x in(-.15,.15):
  b.cyl((x,0,.47),.105,.56,C['cream_d'],segs=14);b.sphere((x,-.035,.16),(.12,.20,.10),C['bakelite']);b.box((x,-.146,1.10),(.035,.025,.58),C['bakelite'],bevel=.008);b.torus((x,0,.24),.11,.022,C['steel_d'],major_segs=16)
 for side in(-1,1):
  b.sphere((side*.29,0,1.30),(.12,.13,.15),C['cream_d']);b.cyl((side*.35,0,1.10),.095,.34,C['cream_d'],segs=14);b.sphere((side*.35,-.01,.89),(.092,.10,.13),C['cream'])
 b.box((0,-.17,.92),(.47,.027,.06),C['bakelite'],bevel=.009);b.disc_ring((0,-.18,1.15),.056,.084,.022,C['brass'],axis='Y',segs=20);b.cyl((0,0,1.48),.13,.07,C['brass_d'],segs=20);a.mount('Mount_Helmet',(0,0,1.51));return a

def reel():
 a=Asset('UmbilicalReel');p=a.part('Frame');p.box((0,0,.04),(.7,.5,.08),C['steel_d'],bevel=.02)
 for x in(-.29,.29):p.box((x,0,.33),(.06,.30,.56),C['bottle'],bevel=.02)
 r=a.part('Drum',location=(0,0,.48));r.cyl((0,0,0),.19,.45,C['bakelite'],axis='X',segs=24)
 for x in(-.23,.23):r.disc_ring((x,0,0),.035,.26,.025,C['steel'],axis='X',segs=24)
 for x in(-.18,-.12,-.06,0,.06,.12,.18):r.torus((x,0,0),.20,.019,C['rust_d'],axis='X',major_segs=24)
 h=a.part('Crank',location=(.33,0,.48));h.box((0,0,.09),(.035,.035,.20),C['steel'],bevel=.006);h.cyl((.065,0,.19),.022,.13,C['bakelite'],axis='X',segs=12);a.mount('Mount_Umbilical',(0,-.21,.48));return a

def officers_door():
 a=Asset('OfficersQuartersDoor');p=a.part('Frame');p.box((0,.035,1.05),(1.24,.13,2.10),C['bottle_d'],bevel=.045)
 d=a.part('Door',location=(-.48,-.045,.06));d.box((.48,0,.97),(.94,.075,1.90),C['bottle'],bevel=.025);d.box((.48,-.048,1.48),(.42,.022,.31),C['steel_d'],bevel=.006)
 for z in(1.45,1.50,1.55):d.box((.48,-.062,z),(.31,.007,.015),C['black'],bevel=.002)
 d.cyl((.75,-.09,.87),.023,.18,C['brass'],segs=12);d.box((.79,-.11,.85),(.13,.032,.025),C['brass'],bevel=.007)
 for x in(-.54,.54):
  for z in(.25,.55,.85,1.15,1.45,1.75):p.cyl((x,-.04,z),.024,.020,C['steel'],axis='Y',segs=10)
 p.box((0,-.05,.055),(.85,.025,.018),C['cream'],bevel=.005);a.mount('Mount_LightUnderDoor',(0,-.09,.055));a.mount('Mount_PneumaticSlot',(0,-.13,1.55));return a

def airlock():
 a=Asset('DivingAirlockChamber');p=a.part('Chamber')
 p.box((0,0,.05),(1.5,1.7,.1),C['steel_d'],bevel=.03)
 for x in(-.72,.72):p.box((x,0,1.08),(.08,1.7,2.05),C['bottle'],bevel=.025)
 p.box((0,0,2.14),(1.5,1.7,.1),C['bottle'],bevel=.03)
 for side,y in enumerate((-.86,.86)):
  for x in(-.60,.60):p.box((x,y,1.10),(.24,.10,2.0),C['steel'],bevel=.025)
  p.box((0,y,2.04),(1.35,.10,.24),C['steel'],bevel=.03)
  d=a.part('Hatch'+str(side),location=(-.46,y,.12));d.box((.46,0,.92),(.89,.065,1.78),C['bottle_d'],bevel=.022)
  w=a.part('Wheel'+str(side),location=(.46,-.065,.92),parent=d);w.torus((0,0,0),.20,.024,C['brass_d'],axis='Y',major_segs=24)
  for i in range(4):t=i*math.pi/2;w.box((math.sin(t)*.1,0,math.cos(t)*.1),(.03,.025,.2),C['brass'],rot=(0,t,0),bevel=.006)
  w.cyl((0,0,0),.04,.06,C['steel'],axis='Y',segs=12)
 a.mount('Mount_WaterLevel',(0,0,.12));a.mount('Mount_Entrance',(0,-.95,0));a.mount('Mount_Exit',(0,.95,0));return a

def crockery():
 out=[]
 for name,r,h in [('EnamelPlate',.13,.02),('EnamelBowl',.11,.07),('EnamelMug',.045,.10),('EnamelSaucepan',.14,.12)]:
  a=Asset(name);p=a.part('Body');p.cyl((0,0,h/2),r*.82,h,C['cream'],r2=r,caps=False,segs=24);p.cyl((0,0,.004),r*.82,.008,C['cream'],segs=24);p.torus((0,0,h),r,.005,C['bottle'],major_segs=24)
  if name=='EnamelMug':p.torus((r*1.25,0,h*.55),.034,.006,C['bottle'],axis='Y',major_segs=16)
  if name=='EnamelSaucepan':p.cyl((r+.12,0,h*.8),.014,.27,C['bakelite'],axis='X',segs=12)
  out.append(a)
 a=Asset('EnamelSpoon');p=a.part('Body');p.sphere((0,0,.006),(.025,.038,.008),C['steel']);p.box((0,.09,.008),(.013,.15,.007),C['steel'],bevel=.002);out.append(a)
 a=Asset('EnamelPotLid');p=a.part('Body');p.cyl((0,0,.015),.14,.025,C['cream'],r2=.08,segs=24);p.sphere((0,0,.04),(.027,.027,.020),C['bakelite']);out.append(a);return out

def build_all():return [bunk(),locker(),stove(),pot(),table(),bench(),shower(),lance(),helmet(),suit(),reel(),officers_door(),airlock()]+crockery()

if __name__=='__main__':
 out,preview=cli_args();reset_scene();assets=build_all()
 for a in assets:a.build()
 bpy.context.view_layer.update();report={'blender':bpy.app.version_string,'issue':119,'notes':['Art meshes only; soup spilling, airlock cycle, decontamination and diving suit wearable rig require gameplay integration.','Officers door modeled with independent pivot but intended closed in current narrative.'],'assets':[]}
 for a in assets:
  for p in a.parts:
   me=p.ob.data;me.calc_loop_triangles();assert all(t.area>1e-12 for t in me.loop_triangles),a.name+'/'+p.name
   assert all(math.isfinite(c) for v in me.vertices for c in v.co)
   uv=me.uv_layers.new(name='UVMap');xs=[v.co.x for v in me.vertices];zs=[v.co.z for v in me.vertices]
   for l in me.loops:v=me.vertices[l.vertex_index].co;uv.data[l.index].uv=((v.x-min(xs))/max(max(xs)-min(xs),.00001),(v.z-min(zs))/max(max(zs)-min(zs),.00001))
  a.export(out);report['assets'].append({'name':a.name,'triangles':a.stats(),'parts':[p.name for p in a.parts],'mounts':[m[0] for m in a.mounts]});print('EXPORT',a.name,a.stats())
 with open(os.path.join(out,'living_manifest.json'),'w') as f:json.dump(report,f,indent=2)
 bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(__file__),'sources','Living.blend'))
 if preview:os.makedirs(preview,exist_ok=True);render_each(assets,preview,resolution=(800,640))
