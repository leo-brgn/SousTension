"""GDD annex 1+10 world/exterior V1 art library. All coordinates metres Z-up."""
import os,sys,math,json,random
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from lib import *

def asset(name):a=Asset(name);return a,a.part('Body')
def beam(p,start,end,r,col):
 v=Vector(end)-Vector(start);before=p._snapshot();bmesh.ops.create_cone(p.bm,cap_ends=True,cap_tris=False,segments=8,radius1=r,radius2=r,depth=v.length);mat=Matrix.Translation((Vector(start)+Vector(end))/2) @ v.to_track_quat('Z','Y').to_matrix().to_4x4();p._finish_prim(before,mat,col,0)
def card(p,points,col):
 before=p._snapshot();verts=[p.bm.verts.new(v) for v in points];p.bm.faces.new(verts);p._finish_prim(before,Matrix.Identity(4),col,0)
def hull(p,length,width,height,col,n=16,breach=False):
 before=p._snapshot();rings=[]
 for t,scale in [(-.5,.025),(-.43,.55),(-.29,.97),(.25,1),(.43,.55),(.5,.025)]:
  rings.append([p.bm.verts.new((math.cos(i*math.tau/n)*width*.5*scale,t*length,math.sin(i*math.tau/n)*height*.5*scale)) for i in range(n)])
 for k in range(len(rings)-1):
  for i in range(n):
   if breach and k==2 and math.sin((i+.5)*math.tau/n)>.40:continue
   p.bm.faces.new((rings[k][i],rings[k][(i+1)%n],rings[k+1][(i+1)%n],rings[k+1][i]))
 p.bm.faces.new(rings[0][::-1]);p.bm.faces.new(rings[-1]);p._finish_prim(before,Matrix.Identity(4),col,0)
def propeller(name='MolossPropeller',radius=1.05):
 a,p=asset(name);p.cyl((0,0,0),radius*.20,.32,C['brass_d'],axis='Y',segs=16)
 for i in range(5):
  ang=i*math.tau/5;q=a.part('Blade'+str(i),rotation=(0,ang,0));q.prism_xz([(radius*.13,0),(radius*.4,radius*.85),(radius*.7,radius*.70),(radius*.65,radius*.24)],.065,C['brass'])
 a.mount('Mount_Shaft',(0,0,0));return a

def sub(lod=0,wear=0):
 a,p=asset('MolossHull_LOD%d_Wear%d'%(lod,wear));hull(p,36,6.8,6.0,C['bottle_d'],[48,24,12][lod]);p.box((0,0,2.65),(3.9,22,.45),C['bottle'],bevel=.13)
 if lod<2:
  for y in(-8,-4,0,4,8):p.box((0,y,2.91),(3.7,.035,.045),C['steel_d'],bevel=.008)
 if wear:
  for i in range(5+wear*5):
   z=.4+(i%3)*.4;x=3.42*math.sqrt(1-(z/3)**2);p.box(((-1 if i%2 else 1)*x,-7+i*.90,z),(.035,.65,.28),C['rust_d' if wear==1 else 'rust'],rot=(0,.15,0),bevel=0 if lod==2 else .008,seg=1 if lod==1 else 2)
 for name,pos in [('Mount_Tower',(0,1,2.85)),('Mount_Propeller',(0,-18.3,0)),('Mount_Rudder',(0,-16,0)),('Mount_PlanePort',(-3.3,-12,0)),('Mount_PlaneStarboard',(3.3,-12,0)),('Mount_Interior',(0,0,-1.5))]:a.mount(name,pos)
 return a

def tower():
 a,p=asset('MolossConningTower');p.box((0,0,1.35),(1.85,4.4,2.7),C['bottle_d'],bevel=.35);p.box((0,0,2.78),(2.0,4.45,.18),C['steel_d'],bevel=.08)
 for x in(-.82,.82):
  for y in(-1.8,-.9,0,.9,1.8):beam(p,(x,y,2.8),(x,y,3.32),.025,C['steel'])
 for x in(-.82,.82):beam(p,(x,-1.8,3.3),(x,1.8,3.3),.025,C['steel'])
 for i,y in enumerate((-.9,0,.9)):a.mount('Mount_Mast'+str(i),(0,y,2.88))
 return a

def appendage(name):
 a,p=asset(name)
 if name=='MolossRudder':p.prism_xz([(-.08,-1.1),(.1,1.4),(1.25,.9),(1.0,-.9)],.15,C['bottle']);a.mount('Mount_Pivot',(0,0,0))
 elif name=='MolossDivingPlane':p.box((.88,0,0),(1.9,1.25,.12),C['bottle'],rot=(0,0,-.12),bevel=.04);a.mount('Mount_Pivot',(0,0,0))
 elif name=='MolossAntenna':p.cyl((0,0,1.6),.055,3.2,C['steel_d'],segs=12);p.cyl((0,0,3.12),.15,.30,C['bakelite'],segs=12);a.mount('Mount_Extension',(0,0,0))
 else:p.cyl((0,0,1.25),.11,2.5,C['steel'],segs=16);p.box((0,-.1,2.60),(.38,.48,.25),C['bottle_d'],bevel=.065);p.box((0,-.345,2.6),(.22,.005,.12),C['glass'],bevel=.001);a.mount('Mount_Extension',(0,0,0))
 return a

def building(name,sx,sy,h):
 a,p=asset(name);p.box((0,0,h/2),(sx,sy,h),C['bottle_d'],bevel=.07);p.prism_xz([(-sx/2-.12,h),(0,h+.8),(sx/2+.12,h)],sy+.24,C['rust_d']);p.box((0,-sy/2-.02,.95),(1,.06,1.9),C['rust'],bevel=.025)
 for x in(-sx*.28,sx*.28):p.box((x,-sy/2-.04,1.65),(.8,.06,.9),C['cream'],bevel=.02);p.box((x,-sy/2-.08,1.65),(.66,.02,.76),C['glass'],bevel=.006)
 if name=='FjordWorkshop':
  p.cyl((sx*.32,sy*.24,h+.75),.22,1.5,C['steel_d'],segs=12);p.cyl((sx*.32,sy*.24,h+1.5),.32,.12,C['steel'],segs=12)
  p.box((0,-sy/2-.35,.18),(2.4,.8,.36),C['steel_d'],bevel=.045)
  for x in(-1.8,1.8):p.box((x,-sy/2-.2,.60),(.13,.13,1.2),C['steel'],bevel=.018)
 elif name=='FjordStampOffice':
  p.box((0,-sy/2-.75,2.32),(2.8,1.6,.12),C['rust_d'],rot=(.12,0,0),bevel=.025)
  for x in(-1.2,1.2):p.cyl((x,-sy/2-1.35,1.13),.075,2.26,C['cream'],segs=10)
  p.box((0,-sy/2-1,.10),(2.8,2,.20),C['steel'],bevel=.04)
 elif name=='FjordBarracks':
  for x in(-3,-1,1,3):
   p.box((x,sy/2+.035,1.55),(.72,.05,.80),C['cream'],bevel=.015);p.box((x,sy/2+.066,1.55),(.60,.012,.67),C['glass'],bevel=.004)
  for x in(-sx*.40,sx*.40):
   for y in(-sy*.38,sy*.38):p.box((x,y,-.20),(.28,.28,.4),C['steel_d'],bevel=.025)
  p.box((0,-sy/2-.16,2.30),(1.55,.075,.24),C['cream'],bevel=.012)
 elif name=='FjordStorageShed':
  p.box((0,-sy/2-.06,1.08),(1.8,.06,2.16),C['rust'],bevel=.02)
  for x in(-.78,-.52,-.26,0,.26,.52,.78):p.box((x,-sy/2-.10,1.08),(.022,.020,2.1),C['rust_d'],bevel=.004)
 a.mount('Mount_Entrance',(0,-sy/2-.2,0));return a

def hub():
 out=[building('FjordWorkshop',6,5,3),building('FjordStampOffice',4,4,2.8),building('FjordBarracks',8,5,3),building('FjordStorageShed',3,3,2.6)]
 a,p=asset('FjordPontoon_4m');p.box((0,0,.25),(3,4,.5),C['rust_d'],bevel=.06)
 for x in[-1.35+i*.3 for i in range(10)]:p.box((x,0,.525),(.27,3.95,.05),C['rust'],bevel=.009)
 for x in(-1.4,1.4):
  for y in(-1.8,1.8):p.cyl((x,y,.07),.13,1.5,C['steel_d'],segs=12)
 a.mount('Mount_Start',(0,-2,.55));a.mount('Mount_End',(0,2,.55));out.append(a)
 a,p=asset('FjordGantryCrane');p.box((0,0,.13),(3.8,3,.26),C['steel_d'],bevel=.06)
 for x in(-1.7,1.7):p.box((x,0,2.4),(.23,.3,4.8),C['bottle'],bevel=.045)
 p.box((0,0,4.8),(4,.4,.32),C['steel'],bevel=.04);t=a.part('Trolley',location=(0,0,4.55));t.box((0,0,0),(.65,.55,.35),C['rust'],bevel=.04);h=a.part('Hook',location=(0,0,2.8));h.cyl((0,0,.7),.024,1.4,C['steel'],segs=8);h.torus((0,0,-.10),.14,.03,C['steel_d'],axis='Y',arc=math.pi*1.65,major_segs=12);a.mount('Mount_Cargo',(0,0,2.55));out.append(a)
 a,p=asset('FjordNoticeBoard');p.box((0,0,1.6),(2.2,.16,1.2),C['rust'],bevel=.035);p.box((0,-.10,1.6),(1.98,.04,.98),C['cream_d'],bevel=.008)
 for x in(-.8,.8):p.box((x,0,.8),(.12,.12,1.6),C['rust_d'],bevel=.02)
 for x in(-.6,0,.6):p.box((x,-.13,1.6),(.4,.008,.6),C['cream'],bevel=.002)
 a.mount('Mount_Posters',(0,-.15,1.6));out.append(a)
 a,p=asset('FjordLighthouse');p.cyl((0,0,4),1.2,8,C['cream_d'],r2=.8,segs=24);p.cyl((0,0,8.15),1.05,.45,C['rust_d'],segs=24);p.cyl((0,0,8.75),.82,.8,C['glass'],segs=16);p.cyl((0,0,9.3),1,.4,C['steel_d'],r2=.08,segs=16);a.mount('Mount_Beacon',(0,0,8.8));out.append(a)
 a,p=asset('FjordMooringBollard');p.box((0,0,.08),(.6,.5,.16),C['steel_d'],bevel=.04);p.cyl((0,0,.36),.12,.58,C['steel'],segs=12);p.cyl((0,0,.63),.09,.48,C['steel'],axis='X',segs=12);out.append(a)
 a,p=asset('FjordGangway');p.box((0,0,.06),(1.2,3,.12),C['steel'],bevel=.025)
 for x in(-.57,.57):
  beam(p,(x,-1.45,.9),(x,1.45,.9),.035,C['steel_d'])
  for y in(-1.4,0,1.4):beam(p,(x,y,0),(x,y,.9),.03,C['steel_d'])
 out.append(a)
 a,p=asset('FjordDockLadder');
 for x in(-.27,.27):p.cyl((x,0,1.15),.028,2.3,C['steel'],segs=10)
 for z in[.1+i*.28 for i in range(8)]:p.cyl((0,0,z),.025,.54,C['steel'],axis='X',segs=10)
 out.append(a)
 a,p=asset('FjordFuelTank');p.cyl((0,0,1.25),1.0,2.5,C['bottle'],segs=20,bevel=.03);p.cyl((0,0,2.6),.15,.20,C['steel_d'],segs=12);a.mount('Mount_Hose',(0,-1,.4));out.append(a)
 a,p=asset('FjordCargoPallet');
 for x in(-.48,-.24,0,.24,.48):p.box((x,0,.12),(.20,1,.045),C['rust'],bevel=.008)
 for y in(-.40,0,.40):p.box((0,y,.06),(1.2,.12,.08),C['rust_d'],bevel=.012)
 out.append(a)
 a,p=asset('FjordDockFence_2m');
 for x in(-.95,.95):p.box((x,0,.7),(.07,.07,1.4),C['bottle'],bevel=.012)
 for z in(.5,1.1):p.box((0,0,z),(2,.045,.065),C['steel'],bevel=.009)
 out.append(a)
 a,p=asset('FjordWorkshopBench');p.box((0,0,.90),(1.8,.7,.08),C['rust'],bevel=.025)
 for x in(-.75,.75):
  for y in(-.25,.25):p.box((x,y,.46),(.07,.07,.88),C['steel_d'],bevel=.012)
 p.box((.4,0,1.05),(.28,.2,.22),C['steel'],bevel=.035);out.append(a)
 a,p=asset('FjordDockPowerPedestal');p.box((0,0,.65),(.40,.35,1.3),C['bottle'],bevel=.04);p.box((0,-.19,1),(.28,.06,.22),C['cream'],bevel=.015);a.mount('Mount_PowerCable',(0,-.24,.8));out.append(a)
 return out

def rock(i,name=None,scale=1):
 a,p=asset(name or 'UnderwaterRock_%02d'%i);p.sphere((0,0,.4*scale),(scale*(.7+.13*(i%3)),scale*(.6+.17*(i%4)),scale*(.8+.19*(i%2))),C['steel_d'],u=8+i%3*2,v=5+i%3)
 for v in p.bm.verts:v.co.x*=1+.18*math.sin(v.co.z*5+i);v.co.z+=.13*scale*math.sin(v.co.y*7+i)
 return a

def terrain():
 out=[rock(i,scale=.8+i*.27) for i in range(8)];out.append(rock(2,'UnderwaterCliff',10))
 a,p=asset('UnderwaterSandTile_8m');p.box((0,0,-.05),(8,8,.1),C['cream_d'],bevel=.025)
 for i in range(12):p.box((0,-3.7+i*.65,.012),(7.8,.08,.035),C['cream'],rot=(0,0,.08),bevel=.009)
 out.append(a)
 a,p=asset('UnderwaterKelpCluster');
 for i in range(7):
  x=(i-3)*.22;h=2.1+(i%3)*.55
  beam(p,(x,0,0),(x+.14,.1,h),.015,C['bottle_d'])
  for j in range(4):z=.45+j*h/5;side=(-1 if j%2 else 1);card(p,[(x,0,z),(x+side*.45,-.03,z+.3),(x+side*.18,0,z+.7)],C['bottle'])
 out.append(a)
 a,p=asset('HydrothermalVent');p.cyl((0,0,1.0),.65,2,C['steel_d'],r2=.28,caps=False,segs=12);p.disc_ring((0,0,2),.17,.28,.10,C['black'],segs=12)
 for i in range(5):t=i*math.tau/5;p.cyl((math.sin(t)*.43,math.cos(t)*.43,.52),.17,1.04,C['steel'],r2=.10,caps=False,segs=8)
 a.mount('Mount_VentParticles',(0,0,2.05));out.append(a);return out

def ship(name,length,width,kind,lod=0,wreck=False):
 a,p=asset(name);hull(p,length,width,width*.70,C['steel_d'] if wreck else C['bottle_d'],12 if lod else 24,breach=wreck)
 if wreck:
  # Open deck/cargo cavity with walked platforms; no solid box enclosing interior.
  for x in(-width*.42,width*.42):p.box((x,0,width*.24),(width*.12,length*.70,.14),C['rust'],bevel=.025)
  p.box((0,0,-width*.15),(width*.64,length*.65,.16),C['rust_d'],bevel=.035);a.mount('Mount_ExplorableInterior',(0,0,-width*.07));a.mount('Mount_Entry',(0,-length*.3,width*.1))
 else:p.box((0,0,width*.19),(width*.82,length*.72,.18),C['steel'],bevel=.05)
 if kind in('ferry','cargo','frigate','trawler'):
  y=length*.17 if kind!='cargo' else -length*.27;h=width*.70
  p.box((0,y,width*.25+h*.5),(width*.65,length*.19,h),C['cream_d'],bevel=.10)
  if not lod:
   for x in[-width*.24+i*width*.12 for i in range(5)]:p.box((x,y-length*.098,width*.25+h*.70),(width*.08,.04,width*.13),C['glass'],bevel=.009)
  if kind=='cargo' and not wreck:
   for row in range(4):
    for col in range(2):p.box(((col-.5)*width*.35,length*(-.1+row*.12),width*.4),(width*.30,length*.10,width*.45),C['rust' if row%2 else 'bottle'],bevel=.04)
  if kind=='ferry':p.box((0,-length*.12,width*.68),(width*.75,length*.32,.30),C['cream'],bevel=.08)
  if kind=='frigate':
   p.cyl((0,length*.26,width*.70),width*.08,width*.9,C['steel_d'],segs=10);p.box((0,length*.26,width*1.1),(width*.35,.18,width*.18),C['steel'],bevel=.03)
   gun=a.part('GunTurret',location=(0,-length*.25,width*.30));gun.cyl((0,0,0),width*.13,.35,C['steel'],segs=12);beam(gun,(0,0,.18),(0,-width*.7,.28),width*.025,C['steel_d'])
  if kind=='trawler':
   beam(p,(-width*.3,-length*.2,width*.3),(-width*.3,length*.1,width*1.1),.09,C['rust']);beam(p,(width*.3,-length*.2,width*.3),(width*.3,length*.1,width*1.1),.09,C['rust']);beam(p,(-width*.3,length*.1,width*1.1),(width*.3,length*.1,width*1.1),.09,C['steel'])
  a.mount('Mount_Propeller',(0,-length*.46,-width*.15))
 return a

def sailboat(index=0):
 a,p=asset('RaceSailboat_%02d'%index if index else 'CivilSailboat');hull(p,6,1.8,.95,C['cream_d'],16);p.box((0,0,.38),(1.4,4.2,.15),C['rust'],bevel=.04);p.cyl((0,0,3),.055,5.8,C['steel'],segs=10);p.box((0,0,-.9),(.10,1.1,1.5),C['steel_d'],bevel=.025)
 sail=a.part('MainSail',location=(0,0,.60),rotation=(0,0,(index%5-.5)*.10));color=[C['cream'],C['bottle'],C['rust'],C['steel_l']][index%4];card(sail,[(0,0,0),(0,0,4.8),(0,-2.0-.08*index,.1)],color);card(sail,[(0,.15,.1),(0,.15,4.5),(0,2.1,.1)],C['cream']);beam(p,(0,0,.6),(0,-2.0,.6),.04,C['steel_d']);a.mount('Mount_Skipper',(0,-1.7,.55));return a

def misc_world():
 out=[]
 a,p=asset('IndustrialFishingNet');
 for i in range(17):
  x=-4+i*.5;beam(p,(x,-3,0),(x,3,0),.014,C['rust_d'])
 for j in range(13):y=-3+j*.5;beam(p,(-4,y,0),(4,y,0),.014,C['rust_d'])
 for i in range(9):p.sphere((-4+i,3,.06),(.10,.13,.10),C['cream'],u=8,v=6)
 for name,pos in [('Mount_CornerA',(-4,-3,0)),('Mount_CornerB',(4,-3,0)),('Mount_CornerC',(-4,3,0)),('Mount_CornerD',(4,3,0))]:a.mount(name,pos)
 out.append(a)
 a,p=asset('UnderwaterMilitaryDepot');p.box((0,0,-.15),(10,8,.3),C['steel_d'],bevel=.08)
 for x in(-4.9,4.9):p.box((x,0,1.7),(.2,8,3.4),C['bottle_d'],bevel=.055)
 p.box((0,3.9,1.7),(10,.2,3.4),C['bottle_d'],bevel=.055);p.box((0,0,3.5),(10,8,.2),C['steel'],bevel=.065)
 for x in(-3,3):p.box((x,-3.9,1.7),(4,.2,3.4),C['bottle_d'],bevel=.04)
 a.mount('Mount_Airlock',(0,-4,0));a.mount('Mount_Loot',(0,0,.1));out.append(a)
 a,p=asset('DepotAirlock');
 for x in(-1,1):p.box((x,0,1.3),(.12,2,2.6),C['steel'],bevel=.035)
 p.box((0,0,2.6),(2.1,2,.12),C['steel'],bevel=.035);p.box((0,0,.05),(2.1,2,.1),C['steel_d'],bevel=.025)
 for i,y in enumerate((-1,1)):
  d=a.part('Door'+str(i),location=(-.95,y,.10));d.box((.95,0,1.2),(1.88,.08,2.36),C['bottle'],bevel=.03);d.cyl((.95,-.07,1.2),.22,.06,C['brass_d'],axis='Y',segs=16)
 a.mount('Mount_Entrance',(0,-1.1,0));a.mount('Mount_Exit',(0,1.1,0));out.append(a)
 a,p=asset('CivilJetSki');hull(p,2.8,1.05,.75,C['cream'],12);p.box((0,-.3,.46),(.60,1.0,.20),C['rust'],bevel=.075);beam(p,(0,.4,.38),(0,.45,.9),.04,C['steel']);beam(p,(-.4,.45,.9),(.4,.45,.9),.035,C['black']);a.mount('Mount_Rider',(0,-.3,.65));out.append(a)
 a,p=asset('CivilPedalBoat');
 for x in(-.65,.65):p.sphere((x,0,0),(.32,1.45,.3),C['cream'],u=12,v=8)
 p.box((0,0,.20),(1.5,1.8,.12),C['bottle'],bevel=.045)
 for x in(-.4,.4):p.box((x,-.25,.43),(.5,.7,.15),C['rust'],bevel=.04);p.box((x,-.50,.65),(.5,.15,.45),C['rust'],bevel=.035)
 paddle=a.part('PaddleWheel',location=(0,.55,-.08));paddle.cyl((0,0,0),.24,.85,C['steel'],axis='X',segs=12)
 for i in range(6):t=i*math.tau/6;paddle.box((0,math.sin(t)*.23,math.cos(t)*.23),(.88,.08,.18),C['steel_d'],rot=(-t,0,0),bevel=.015)
 out.append(a)
 a,p=asset('EntentePatrolPlane');p.sphere((0,0,0),(.65,5,.7),C['steel_l'],u=16,v=12);p.box((0,.6,.08),(10.5,1.3,.12),C['steel'],rot=(0,0,.13),bevel=.04);p.box((0,-3.4,.2),(3.8,.7,.10),C['steel'],bevel=.03);p.box((0,-3.5,.8),(.11,1.2,1.4),C['steel'],bevel=.03)
 for x in(-2.3,2.3):p.cyl((x,.65,-.1),.30,1.4,C['steel_d'],axis='Y',segs=12);r=a.part('Propeller'+str(x),location=(x,1.4,-.1));r.box((0,0,0),(1.25,.06,.13),C['black'],bevel=.025)
 out.append(a)
 for name in('SonarBuoy','ChannelBuoy'):
  a,p=asset(name);p.cyl((0,0,.25),.52,.50,C['bottle'],r2=.35,segs=16);p.cyl((0,0,1),.13,1.2,C['steel'],segs=12);p.cyl((0,0,1.70),.23,.25,C['brass'],r2=.05,segs=12);a.mount('Mount_Light',(0,0,1.8));a.mount('Mount_Anchor',(0,0,-.1))
  if name=='SonarBuoy':p.cyl((0,0,-.75),.08,1.8,C['black'],segs=10);a.mount('Mount_Sonar',(0,0,-1.7))
  out.append(a)
 a,p=asset('ExerciseGrenade');p.cyl((0,0,.11),.075,.22,C['rust'],segs=12,bevel=.012);p.cyl((0,0,.25),.029,.06,C['steel_d'],segs=10);p.torus((.03,0,.30),.025,.006,C['steel'],axis='Y',major_segs=12);out.append(a)
 a,p=asset('OffshoreWindTurbine');p.cyl((0,0,14),.9,28,C['cream_d'],r2=.45,segs=16);p.box((0,.6,28),(1.5,3,1.2),C['cream'],bevel=.20);r=a.part('Rotor',location=(0,-1,28));r.sphere((0,0,0),(.45,.4,.45),C['steel_l'])
 for i in range(3):ang=i*math.tau/3;b=a.part('Blade'+str(i),rotation=(0,ang,0),parent=r);b.prism_xz([(-.22,.3),(.22,.3),(.50,10),(-.05,12)],.12,C['cream'])
 a.mount('Mount_Rotor',(0,-1,28));out.append(a)
 a,p=asset('OffshorePlatform');p.box((0,0,8),(12,10,.6),C['steel_d'],bevel=.12)
 for x in(-4.5,4.5):
  for y in(-3.5,3.5):p.cyl((x,y,4),.40,8,C['rust_d'],segs=12)
 p.box((2,0,10),(5,4,3.4),C['cream_d'],bevel=.08);p.cyl((-3,0,10.3),.65,4,C['steel'],segs=16);beam(p,(-4,-3,8.5),(-4,3,13),.14,C['bottle']);beam(p,(-4,3,13),(2,3,13),.14,C['bottle']);a.mount('Mount_Access',(0,-4.8,8.4));out.append(a)
 for i in range(3):out.append(rock(i,'Iceberg_%d'%i,5+i*3));[p._paint(list(p.bm.faces),C['cream']) for p in out[-1].parts]
 a,p=asset('RaceSpectatorLowPoly');p.cyl((0,0,.8),.18,.65,C['bottle'],segs=8);p.sphere((0,0,1.3),.19,C['cream_d'],u=8,v=6)
 for x in(-.11,.11):p.cyl((x,0,.28),.065,.56,C['steel_d'],segs=8)
 for x in(-.25,.25):beam(p,(x,0,1.0),(x*1.6,0,1.45),.06,C['cream_d'])
 out.append(a);return out

def wrecks():
 out=[ship('WreckCargo',18,5,'cargo',wreck=True),ship('WreckTrawler',10,3,'trawler',wreck=True)]
 a,p=asset('WreckTwinSub');hull(p,24,5,4,C['rust_d'],24,breach=True)
 # Broken roof exposes a real interior deck; sides retain the submarine outline.
 p.box((0,0,.35),(3.4,12,.16),C['steel'],bevel=.04)
 for x in(-1.65,1.65):p.box((x,0,.88),(.1,12,.85),C['rust'],bevel=.035)
 a.mount('Mount_ExplorableInterior',(0,0,.48));a.mount('Mount_Entry',(0,-6,.48));out.append(a);return out

def build_all():
 return [sub(l,w) for l in range(3) for w in range(3)]+[tower(),propeller()]+[appendage(n) for n in ['MolossRudder','MolossDivingPlane','MolossAntenna','MolossPeriscopeHead']]+hub()+terrain()+wrecks()+[ship('CivilFerry',20,5,'ferry'),ship('CivilContainerShip',36,7,'cargo'),sailboat()]+[ship('EntenteFrigate_LOD%d'%l,30,5,'frigate',l) for l in range(2)]+misc_world()+[sailboat(i) for i in range(1,11)]

if __name__=='__main__':
 out,preview=cli_args();reset_scene();assets=build_all()
 for a in assets:a.build()
 bpy.context.view_layer.update();report={'blender':bpy.app.version_string,'scope':'GDD V1 annex1 exterior + annex10 world art meshes','assets':[]}
 for a in assets:
  for p in a.parts:
   me=p.ob.data;me.calc_loop_triangles();assert all(t.area>1e-12 for t in me.loop_triangles),a.name+'/'+p.name;assert all(math.isfinite(c) for v in me.vertices for c in v.co);assert 'Col' in me.color_attributes
   uv=me.uv_layers.new(name='UVMap');xs=[v.co.x for v in me.vertices];zs=[v.co.z for v in me.vertices];ys=[v.co.y for v in me.vertices];horizontal=max(ys)-min(ys)>max(zs)-min(zs);sec=ys if horizontal else zs
   for l in me.loops:v=me.vertices[l.vertex_index].co;uv.data[l.index].uv=((v.x-min(xs))/max(max(xs)-min(xs),.00001),((v.y if horizontal else v.z)-min(sec))/max(max(sec)-min(sec),.00001))
  a.export(out);report['assets'].append({'name':a.name,'triangles':a.stats(),'parts':[p.name for p in a.parts],'mounts':[m[0] for m in a.mounts],'mount_details':[{'name':m[0],'position_z_up':list(m[1]),'rotation_xyz':list(m[2])} for m in a.mounts]});print('EXPORT',a.name,a.stats())
 with open(os.path.join(out,'world_manifest.json'),'w') as f:json.dump(report,f,indent=2)
 bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(__file__),'sources','World.blend'))
 if preview:os.makedirs(preview,exist_ok=True);render_each(assets,preview,views=(('three_quarter',(.8,-1,.6)),),resolution=(800,600))

