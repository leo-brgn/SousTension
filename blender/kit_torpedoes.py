"""Issue121 development minimum: hollow converted tubes and cargo fastening supports."""
import os,sys,math,json
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from lib import *

def tube():
 a=Asset('CargoTorpedoTube');p=a.part('TubeBody');n=32;outer=.43;inner=.385;z=.48;front=-1.40;back=1.40
 before=p._snapshot();rings=[]
 for y in(front,back):
  rings.append([(p.bm.verts.new((outer*math.cos(t),y,z+outer*math.sin(t))),p.bm.verts.new((inner*math.cos(t),y,z+inner*math.sin(t)))) for t in [i*math.tau/n for i in range(n)]])
 for i in range(n):
  j=(i+1)%n
  p.bm.faces.new((rings[0][i][0],rings[0][j][0],rings[1][j][0],rings[1][i][0]));p.bm.faces.new((rings[0][i][1],rings[1][i][1],rings[1][j][1],rings[0][j][1]))
  for k in(0,1):p.bm.faces.new((rings[k][i][0],rings[k][i][1],rings[k][j][1],rings[k][j][0]))
 p._finish_prim(before,Matrix.Identity(4),C['bottle_d'],0)
 p.cyl((0,1.38,z),inner,.025,C['steel_d'],axis='Y',segs=32)
 for y in(-1.36,0,1.32):p.disc_ring((0,y,z),outer,.47,.06,C['steel'],axis='Y',segs=32)
 for x in(-.32,.32):
  for y in(-.95,.95):p.box((x,y,.11),(.12,.16,.20),C['steel_d'],bevel=.018)
 for y in(-.75,.50):p.box((0,y,.20),(.52,.04,.025),C['steel'],bevel=.007)
 door=a.part('BreechDoor',location=(-.44,front-.045,z));door.cyl((.44,0,0),.418,.065,C['bottle'],axis='Y',segs=32,bevel=.012);door.disc_ring((.44,-.04,0),.31,.385,.02,C['steel'],axis='Y',segs=32)
 for side in(-1,1):door.cyl((0,0,side*.21),.033,.14,C['brass_d'],segs=12)
 wheel=a.part('LockWheel',location=(.44,-.085,0),parent=door);wheel.torus((0,0,0),.19,.023,C['brass_d'],axis='Y',major_segs=24)
 for i in range(4):t=i*math.pi/2;wheel.box((math.sin(t)*.09,0,math.cos(t)*.09),(.025,.025,.19),C['brass'],rot=(0,t,0),bevel=.005)
 wheel.cyl((0,0,0),.042,.06,C['steel_d'],axis='Y',segs=12)
 for i in range(6):
  t=i*math.tau/6;l=a.part('Dog'+str(i),location=(.44+.35*math.sin(t),-.045,.35*math.cos(t)),parent=door);l.box((0,0,0),(.09,.03,.035),C['steel_l'],rot=(0,-t,0),bevel=.006)
 a.mount('Mount_BreechHinge',(-.44,front-.045,z));a.mount('Mount_CargoInside',(0,0,.21));a.mount('Mount_Front',(0,front,z));a.mount('Mount_Back',(0,back,z));return a

def rack():
 a=Asset('CargoRack');p=a.part('Frame')
 for x in(-.66,.66):
  for y in(-.43,.43):p.box((x,y,.77),(.055,.055,1.54),C['bottle'],bevel=.012)
 for z in(.12,.75,1.40):
  p.box((0,0,z),(1.36,.91,.05),C['steel_d'],bevel=.013)
  for x in(-.50,-.25,0,.25,.50):p.box((x,0,z+.034),(.025,.84,.018),C['steel'],bevel=.005)
 for i,x in enumerate((-.56,.56)):
  for j,z in enumerate((.18,.82,1.47)):
   p.torus((x,-.46,z),.035,.008,C['brass_d'],axis='X',major_segs=12);a.mount('Mount_TieDown%d%d'%(i,j),(x,-.46,z))
 for i,z in enumerate((.18,.82,1.47)):a.mount('Mount_CargoShelf'+str(i),(0,0,z))
 return a

def strap():
 a=Asset('CargoTieDownStrap');p=a.part('Webbing');p.box((0,0,0),(.055,1.2,.006),C['rust'],bevel=.0015)
 for x in(-.021,.021):p.box((x,0,.004),(.003,1.16,.0015),C['cream_d'],bevel=.0003)
 buckle=a.part('RatchetBody',location=(0,-.35,.02));buckle.box((0,0,0),(.078,.11,.025),C['steel_d'],bevel=.006)
 h=a.part('RatchetHandle',location=(0,-.39,.035));h.box((0,.045,0),(.062,.09,.015),C['steel'],bevel=.004);h.box((0,.080,.015),(.061,.025,.025),C['bakelite'],bevel=.005)
 for y in(-.60,.60):p.torus((0,y,0),.022,.005,C['steel'],axis='X',arc=math.pi*1.5,major_segs=12)
 a.mount('Mount_EndA',(0,-.60,0));a.mount('Mount_EndB',(0,.60,0));a.mount('Mount_CargoContact',(0,0,0));return a

def build_all():return[tube(),rack(),strap()]

if __name__=='__main__':
 out,preview=cli_args();reset_scene();assets=build_all()
 for a in assets:a.build()
 bpy.context.view_layer.update();report={'blender':bpy.app.version_string,'issue':121,'scope':'Development support meshes, no torpedo decoration or cargo gameplay','assets':[]}
 for a in assets:
  for p in a.parts:
   me=p.ob.data;me.calc_loop_triangles();assert all(t.area>1e-12 for t in me.loop_triangles),a.name+'/'+p.name;assert all(math.isfinite(c) for v in me.vertices for c in v.co)
   uv=me.uv_layers.new(name='UVMap');xs=[v.co.x for v in me.vertices];ys=[v.co.y for v in me.vertices]
   for l in me.loops:v=me.vertices[l.vertex_index].co;uv.data[l.index].uv=((v.x-min(xs))/max(max(xs)-min(xs),.00001),(v.y-min(ys))/max(max(ys)-min(ys),.00001))
  a.export(out);report['assets'].append({'name':a.name,'triangles':a.stats(),'parts':[p.name for p in a.parts],'mounts':[{'name':m[0],'position_z_up':list(m[1]),'rotation_xyz_radians':list(m[2])} for m in a.mounts],'animation_axes':{'BreechDoor':'local Z (Unity Y)','LockWheel':'local Y (Unity Z)','Dogs':'local Y (Unity Z)'} if a.name=='CargoTorpedoTube' else {'RatchetHandle':'local X'} if a.name=='CargoTieDownStrap' else {}});print('EXPORT',a.name,a.stats())
 with open(os.path.join(out,'torpedoes_manifest.json'),'w') as f:json.dump(report,f,indent=2)
 bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(__file__),'sources','Torpedoes.blend'))
 if preview:
  os.makedirs(preview,exist_ok=True);render_each(assets,preview,resolution=(960,720))
  next(p for p in assets[0].parts if p.name=='BreechDoor').ob.rotation_euler.z=-math.pi*.60
  for other in assets[1:]:
   for ob in [other.root]+list(other.root.children_recursive):ob.hide_render=True
  bpy.context.view_layer.update();render_each([assets[0]],preview,views=(('open',(0.4,-1,.35)),),resolution=(960,720))
