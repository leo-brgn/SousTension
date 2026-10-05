"""Portable tools and blank diegetic paper, issue 122. Metres, vertex colours."""
import os,sys,math,json
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from lib import *

def tool(name):
 a=Asset(name); a.mount('Mount_Grip',(0,0,0)); return a,a.part('Body')

def build_all():
 out=[]
 a,p=tool('Hammer'); p.cyl((0,0,.16),.024,.36,C['rust'],segs=12,bevel=.008); p.box((0,0,.35),(.22,.07,.075),C['steel'],bevel=.012); p.cyl((-.12,0,.35),.045,.03,C['steel_l'],axis='X'); out.append(a)
 a,p=tool('AdjustableWrench'); p.box((0,0,.13),(.043,.022,.28),C['steel'],bevel=.008); p.disc_ring((0,0,.005),.008,.022,.021,C['steel'],axis='Y'); p.box((-.019,0,.28),(.052,.027,.10),C['steel'],bevel=.007); p.box((.011,0,.322),(.066,.029,.025),C['steel_l'],bevel=.005)
 jaw=a.part('SlidingJaw',location=(.036,0,.269)); jaw.box((0,0,0),(.026,.029,.043),C['steel_l'],bevel=.004); jaw.box((-.018,0,-.026),(.062,.025,.016),C['steel'],bevel=.003); wheel=a.part('AdjustmentWheel',location=(0,0,.259)); wheel.cyl((0,0,0),.015,.025,C['steel_d'],axis='X',segs=16); out.append(a)
 a,p=tool('HullPatch'); p.box((0,0,.008),(.38,.30,.016),C['steel_d'],bevel=.005); p.box((0,0,.018),(.29,.22,.008),C['rust'],bevel=.002)
 for x in (-.15,.15):
  for y in (-.11,.11): p.cyl((x,y,.024),.015,.02,C['brass_d'],segs=6)
 a.mount('Mount_Seal',(0,0,0));out.append(a)
 a,p=tool('WoodWedge');p.prism_xz([(-.12,0),(.12,0),(-.12,.065)],.09,C['rust']);out.append(a)
 a,p=tool('Flashlight'); p.cyl((0,0,.1),.032,.23,C['bottle'],segs=16,bevel=.006);p.cyl((0,0,.24),.057,.055,C['steel_d'],r2=.041,segs=20,bevel=.004);p.disc_ring((0,0,.269),.044,.057,.015,C['steel'],segs=20);p.cyl((0,0,.267),.043,.006,C['cream'],segs=20); p.box((0,-.033,.13),(.019,.009,.045),C['bakelite'],bevel=.004);a.mount('Mount_Light',(0,0,.275));out.append(a)
 a,p=tool('Bucket');p.cyl((0,0,.13),.12,.26,C['steel'],r2=.16,caps=False,segs=24);p.cyl((0,0,.009),.12,.018,C['steel_d'],segs=24);p.torus((0,0,.26),.158,.009,C['steel_l']);h=a.part('Handle',location=(0,0,.24));h.torus((0,0,0),.167,.009,C['steel_d'],axis='Y',arc=math.pi,major_segs=20);out.append(a)
 a,p=tool('Mop');p.cyl((0,0,.60),.016,1.2,C['rust'],segs=12);p.box((0,0,.045),(.25,.065,.055),C['steel_d'],bevel=.009)
 for i in range(12):p.cyl(((i-5.5)*.020,0,-.018),.011,.16,C['cream_d'],segs=8)
 out.append(a)
 a,p=tool('Screwdriver');p.cyl((0,0,.075),.025,.15,C['bakelite'],segs=10,bevel=.009);p.cyl((0,0,.22),.006,.16,C['steel_l'],segs=10);p.box((0,0,.305),(.016,.004,.025),C['steel_l'],bevel=.001);out.append(a)
 a,p=tool('Crowbar');p.cyl((0,0,.27),.013,.48,C['steel_d'],segs=10);p.torus((-.065,0,.5),.065,.013,C['steel_d'],axis='Y',arc=math.pi/2,major_segs=10);p.box((-.066,0,.571),(.025,.015,.055),C['steel'],bevel=.003);p.box((0,0,.018),(.035,.009,.05),C['steel'],bevel=.003);out.append(a)
 a,p=tool('WristDosimeter');p.box((0,0,0),(.10,.045,.13),C['bottle'],bevel=.013);p.cyl((0,-.026,.018),.040,.012,C['brass_d'],axis='Y',segs=24);p.cyl((0,-.034,.018),.034,.007,C['cream'],axis='Y',segs=24)
 for i in range(9):
  ang=(-.75+i*.1875)*math.pi;p.box((math.sin(ang)*.027,-.039,.018+math.cos(ang)*.027),(.002,.002,.007),C['black'],rot=(0,ang,0))
 n=a.part('DoseNeedle',location=(0,-.042,.018));n.box((0,0,.012),(.002,.003,.026),C['black']);p.box((0,-.026,-.038),(.065,.004,.018),C['cream']);hp=a.part('HealthNeedle',location=(-.027,-.030,-.038));hp.box((0,0,0),(.004,.003,.015),C['red']);p.torus((0,.027,0),.046,.010,C['bakelite'],axis='X');a.mount('Mount_Wrist',(0,.027,0));out.append(a)
 a,p=tool('InkStamp');p.box((0,0,.013),(.11,.065,.026),C['bakelite'],bevel=.006);p.cyl((0,0,.06),.023,.08,C['rust'],segs=14,bevel=.009);p.sphere((0,0,.1),(.037,.027,.019),C['bakelite']);out.append(a)
 a,p=tool('InkPad');p.box((0,0,.015),(.14,.09,.03),C['steel_d'],bevel=.006);p.box((0,0,.032),(.12,.07,.008),C['bakelite'],bevel=.002);out.append(a)
 for name in ['ManualLoosePage','MissionOrder','PatrolNote','Form_K90B','Form_Maintenance','Form_Incident','Form_Requisition','Form_Radiation']:
  a,p=tool(name);p.box((0,0,.0007),(.21,.297,.0014),C['cream'],bevel=.0003);a.mount('Mount_PrintFront',(0,0,.0015));a.mount('Mount_PrintBack',(0,0,-.001));out.append(a)
 a,p=tool('RolledOrder');p.cyl((0,0,0),.025,.21,C['cream'],axis='X',segs=16,caps=False);p.torus((0,0,0),.026,.003,C['rust'],axis='X');out.append(a)
 a,p=tool('Headlamp');p.torus((0,.035,0),.095,.011,C['bakelite']);p.box((0,-.065,0),(.10,.045,.065),C['bottle'],bevel=.009);p.cyl((0,-.092,0),.034,.015,C['steel'],axis='Y',segs=20);p.cyl((0,-.102,0),.026,.006,C['cream'],axis='Y',segs=20);a.mount('Mount_Light',(0,-.108,0));a.mount('Mount_Head',(0,.035,0));out.append(a)
 a,p=tool('Extinguisher');p.cyl((0,0,.24),.09,.40,C['rust'],segs=20,bevel=.015);p.sphere((0,0,.44),(.09,.09,.06),C['rust']);p.cyl((0,0,.49),.022,.07,C['brass_d'],segs=12);p.box((0,-.091,.27),(.09,.004,.15),C['cream'],bevel=.003)
 lever=a.part('SqueezeLever',location=(0,0,.52));lever.box((.04,0,0),(.12,.027,.020),C['steel_d'],bevel=.004);p.torus((.06,0,.43),.065,.010,C['black'],axis='Y',arc=math.pi,major_segs=14);p.cyl((.13,0,.35),.021,.12,C['black'],segs=12,r2=.012);a.mount('Mount_Nozzle',(.13,0,.29));out.append(a)
 a,p=tool('Blowtorch');p.cyl((0,0,.10),.063,.20,C['brass_d'],segs=18,bevel=.008);p.cyl((0,0,.23),.022,.06,C['brass'],segs=12);p.cyl((.07,0,.26),.015,.15,C['steel'],axis='X',segs=12);p.cyl((.15,0,.26),.024,.04,C['steel_d'],axis='X',segs=12);p.torus((-.071,0,.15),.047,.008,C['bakelite'],axis='Y');knob=a.part('FuelKnob',location=(0,-.035,.23));knob.cyl((0,0,0),.022,.022,C['bakelite'],axis='Y',segs=12);a.mount('Mount_Flame',(.175,0,.26),(0,math.pi/2,0));out.append(a)
 return out

def run(kit,assets,notes):
 out,preview=cli_args();out=os.path.abspath(out);preview=os.path.abspath(preview) if preview else None;reset_scene();assets=assets()
 for a in assets:a.build()
 bpy.context.view_layer.update()
 report={'blender':bpy.app.version_string,'issue':122,'notes':notes,'assets':[]}
 for a in assets:
  for p in a.parts:
   assert len(p.ob.data.vertices)>0 and 'Col' in p.ob.data.color_attributes
   assert all(math.isfinite(c) for v in p.ob.data.vertices for c in v.co)
   p.ob.data.calc_loop_triangles()
   assert all(t.area>1e-12 for t in p.ob.data.loop_triangles), 'Degenerate triangles '+a.name+'/'+p.name
  for p in a.parts:
   me=p.ob.data;uv=me.uv_layers.new(name='UVMap');xs=[v.co.x for v in me.vertices];zs=[v.co.z for v in me.vertices];ys=[v.co.y for v in me.vertices];vertical=(max(zs)-min(zs))>(max(ys)-min(ys));second=zs if vertical else ys
   for loop in me.loops:
    v=me.vertices[loop.vertex_index].co;uv.data[loop.index].uv=((v.x-min(xs))/max(max(xs)-min(xs),.00001),((v.z if vertical else v.y)-min(second))/max(max(second)-min(second),.00001))
  corners=[ob.matrix_world @ Vector(c) for ob in a.root.children_recursive if ob.type=='MESH' for c in ob.bound_box]
  lo=[min(v[j] for v in corners) for j in range(3)];hi=[max(v[j] for v in corners) for j in range(3)]
  masses={'Hammer':1.2,'AdjustableWrench':.7,'HullPatch':4.0,'WoodWedge':.25,'Flashlight':.5,'Bucket':1.0,'Mop':1.0,'Screwdriver':.2,'Crowbar':1.5,'WristDosimeter':.25,'InkStamp':.2,'InkPad':.15,'RolledOrder':.03,'Headlamp':.3,'Extinguisher':4,'Blowtorch':1.0}
  a.export(out);report['assets'].append({'name':a.name,'triangles':a.stats(),'parts':[p.name for p in a.parts],'mounts':[m[0] for m in a.mounts],'bounds_blender_z_up':{'min':lo,'max':hi},'collider_hint':'box approximation for portable pickup; water/cards no collider','prototype_mass_kg':masses.get(a.name,.01) if kit=='Tools' else None});print('EXPORT',a.name,a.stats())
 with open(os.path.join(out,kit.lower()+'_manifest.json'),'w') as f:json.dump(report,f,indent=2)
 bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(__file__),'sources',kit+'.blend'))
 if preview:os.makedirs(preview,exist_ok=True);render_each(assets,preview,resolution=(720,576))
if __name__=='__main__':run('Tools',build_all,['Paper meshes intentionally blank; localized typography and form content require runtime print/decal material.','Dosimeter has independent dose and health indicators; game and wrist rig integration required.'])



