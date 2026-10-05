"""GDD annex1–7 missing V1 interior fixtures. Geometry/art supports, no simulation."""
import os,sys,json,math
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from lib import *

def wheel(a,name,loc,r=.09,axis='Y'):
 p=a.part(name,location=loc);p.torus((0,0,0),r,.012,C['brass_d'],axis=axis)
 for i in range(4):
  angle=i*math.pi/2
  if axis=='Y':p.box((math.sin(angle)*r/2,0,math.cos(angle)*r/2),(.012,.018,r),C['brass'],rot=(0,angle,0))
  else:p.box((math.sin(angle)*r/2,math.cos(angle)*r/2,0),(.012,r,.018),C['brass'],rot=(0,0,-angle))
 p.cyl((0,0,0),.024,.035,C['brass'],axis=axis);return p

def studs(p,center,r,axis='Y',n=8):
 for i in range(n):
  q=i*math.tau/n;x,y,z=center
  pos=(x+r*math.cos(q),y,z+r*math.sin(q)) if axis=='Y' else (x+r*math.cos(q),y+r*math.sin(q),z)
  p.cyl(pos,.011,.022,C['steel_l'],axis=axis,segs=8)

def beam(p,a,b,width,col):
 d=Vector(b)-Vector(a);m=(Vector(a)+Vector(b))/2
 p.box(m,(width,width,d.length),col,rot=d.to_track_quat('Z','Y').to_euler(),bevel=width*.12)

def hull(end):
 a=Asset('HullInteriorCurved'+end);p=a.part('Shell')
 # Six longitudinal segments taper the existing 6m profile into curved endcap.
 direction=1 if end=='Bow' else -1
 profile=[(-3,0),(-3,2.1),(-2.6,2.5),(2.6,2.5),(3,2.1),(3,0)]
 for j in range(6):
  t=j/6;s=1-.58*t*t;sn=1-.58*((j+1)/6)**2
  for i in range(5):
   x,z=profile[i];xx,zz=profile[i+1]
   vs=[p.bm.verts.new(v) for v in ((x*s,direction*j*.5,z),(xx*s,direction*j*.5,zz),(xx*sn,direction*(j+1)*.5,zz),(x*sn,direction*(j+1)*.5,z))]
   f=p.bm.faces.new(vs);p._paint([f],C['bottle'])
  if j%2==0:
   for i in range(5):beam(p,(profile[i][0]*s,direction*j*.5,profile[i][1]),(profile[i+1][0]*s,direction*j*.5,profile[i+1][1]),.095,C['steel_d'])
 a.mount('Mount_StraightSection',(0,0,0));a.mount('Mount_EndBulkhead',(0,direction*3,0));return a

def ladder():
 a=Asset('ConningLadderHatch');p=a.part('Ladder')
 for x in (-.25,.25):p.cyl((x,0,1.36),.027,2.72,C['steel'],bevel=.004)
 for i in range(10):p.cyl((0,-.02,.18+i*.26),.023,.50,C['steel'],axis='X',bevel=.003)
 p.disc_ring((0,0,2.55),.44,.54,.12,C['bottle_d'],segs=32)
 lid=a.part('HatchLid',location=(-.49,0,2.62));lid.cyl((.49,0,0),.46,.045,C['bottle'],bevel=.012,segs=32)
 wh=wheel(a,'HatchWheel',(.49,0,.08),.13,axis='Z');wh.parent=lid
 a.mount('Mount_ConningTower',(0,0,2.55));return a

def conning():
 a=Asset('ConningTowerInterior');p=a.part('CurvedWalls')
 for i in range(20):
  ang=math.tau*i/24
  p.box((math.cos(ang)*1.15,math.sin(ang)*1.15,.8),(.12,.31,1.6),C['bottle'],rot=(0,0,ang),bevel=.018)
 for z in (.15,1.5):p.torus((0,0,z),1.1,.045,C['steel_d'],major_segs=32)
 p.disc_ring((0,0,0),.43,1.15,.08,C['grey_floor'],segs=32)
 a.mount('Mount_Ladder',(0,0,0));a.mount('Mount_Periscope',(.35,.20,0));return a

def periscope():
 a=Asset('PeriscopeInterior');p=a.part('FixedSleeve');p.cyl((0,0,.62),.11,1.24,C['brass_d'],bevel=.013)
 for z in (.08,1.16):p.disc_ring((0,0,z),.10,.15,.065,C['brass'])
 mast=a.part('SlidingMast',location=(0,0,1.2));mast.cyl((0,0,.20),.064,2.35,C['steel_l'],bevel=.006)
 head=a.part('RotatingEyepiece',parent=mast,location=(0,0,-.10));head.box((0,-.06,0),(.23,.21,.16),C['brass'],bevel=.018);head.cyl((0,-.18,0),.035,.09,C['bakelite'],axis='Y')
 for side in (-1,1):
  h=a.part('FoldHandleLeft' if side<0 else 'FoldHandleRight',parent=head,location=(side*.115,0,0));h.cyl((side*.10,0,0),.022,.20,C['bakelite'],axis='X',bevel=.005)
 crank=wheel(a,'ElevationCrank',(.0,-.15,.62),.12);crank.cyl((.12,-.03,0),.020,.07,C['bakelite'],axis='Y')
 a.mount('Mount_ExternalHead',(0,0,2.58));a.mount('Mount_Floor',(0,0,0));return a

def externalhead():
 a=Asset('PeriscopeExternalHead');p=a.part('Head');p.cyl((0,0,.18),.064,.36,C['steel'],bevel=.006);p.box((0,-.025,.39),(.16,.19,.14),C['bottle_d'],bevel=.024);p.cyl((0,-.125,.39),.045,.012,C['glass'],axis='Y');a.mount('Mount_Mast',(0,0,0));return a

def cabletray():
 a=Asset('CableTrayStraight');p=a.part('Tray')
 for x in (-.16,.16):p.box((x,0,.035),(.025,2,.07),C['steel'],bevel=.004)
 for i in range(14):p.box((0,-.94+i*.145,0),(.32,.022,.022),C['steel_d'],bevel=.003)
 for i in range(4):p.cyl((-.10+i*.065,0,.04),.020,2,C['bakelite'] if i%2 else C['rust_d'],axis='Y',segs=10)
 a.mount('Mount_A',(0,-1,0));a.mount('Mount_B',(0,1,0));return a

def junction():
 a=Asset('CableJunctionBox');p=a.part('Housing');p.box((0,.04,0),(.32,.14,.28),C['bottle_d'],bevel=.015)
 for x in (-.10,0,.10):p.cyl((x,.04,-.19),.025,.10,C['brass_d'])
 lid=a.part('ServiceLid',location=(-.16,-.04,0));lid.box((.16,0,0),(.32,.03,.28),C['bottle'],bevel=.008)
 for x in (.04,.28):
  for z in (-.10,.10):lid.cyl((x,-.021,z),.012,.01,C['steel_l'],axis='Y',segs=8)
 a.mount('Mount_Wall',(0,.11,0));return a

def compass():
 a=Asset('BearingCompass');p=a.part('Bowl');p.cyl((0,0,.035),.092,.07,C['brass_d'],bevel=.008);p.disc_ring((0,0,.075),.077,.098,.02,C['brass'])
 p.cyl((0,0,.071),.076,.006,C['cream']);rose=a.part('CompassRose',location=(0,0,.08))
 for i in range(16):
  q=i*math.tau/16;rose.box((math.sin(q)*.064,math.cos(q)*.064,0),(.002,.012,.002),C['black'],rot=(0,0,-q))
 rose.prism_tri((0,.061),(-.009,0),(.009,0),.003,C['black']);rose.prism_tri((0,-.061),(-.009,0),(.009,0),.003,C['brass'])
 p.box((0,.115,.02),(.024,.085,.028),C['brass'],bevel=.004);a.mount('Mount_Grip',(0,0,.03));return a

def cras():
 a=Asset('CrasNavigationRule');p=a.part('Rule');p.box((0,0,.002),(.36,.10,.004),C['cream'],bevel=.0005)
 for side in (-1,1):
  p.disc_ring((side*.085,0,.0045),.037,.038,.0008,C['brass_d'],segs=40)
  for i in range(13):
   ang=i*math.pi/12;p.box((side*.085+math.cos(ang)*.035,math.sin(ang)*.035,.005),(.001,.008,.001),C['black'],rot=(0,0,ang))
 for i in range(37):p.box((-.18+i*.01,-.045,.005),(.0008,.008 if i%5 else .014,.001),C['black'])
 a.mount('Mount_Grip',(0,0,.003));return a

def clock(name='ShipClock',portable=False):
 r=.055 if portable else .19;a=Asset(name);p=a.part('Case');p.cyl((0,0,0),r,r*.24,C['brass_d'],axis='Y',segs=32,bevel=r*.04);p.cyl((0,-r*.14,0),r*.88,.004,C['cream'],axis='Y');p.disc_ring((0,-r*.16,0),r*.85,r,.018,C['brass'],axis='Y')
 for i in range(12):q=i*math.tau/12;p.box((math.sin(q)*r*.72,-r*.19,math.cos(q)*r*.72),(.003,.003,r*.11),C['black'],rot=(0,q,0))
 for name,length,width in (('HourHand',r*.40,.007),('MinuteHand',r*.65,.005),('SecondHand',r*.72,.002)):
  h=a.part(name,location=(0,-r*.21,0));h.box((0,0,length/2),(width,.004,length),C['black'])
 if portable:p.cyl((0,0,r*1.12),.012,.025,C['brass']);p.torus((0,0,r*1.35),.018,.003,C['brass'],axis='Y')
 a.mount('Mount_Grip' if portable else 'Mount_Wall',(0,r*.12,0));return a

def flat_support(name,w,h,col,frame=False):
 a=Asset(name);p=a.part('ArtworkSurface');p.box((0,0,0),(w,.004,h),col,bevel=.001)
 if frame:
  f=a.part('Frame')
  for x in (-w/2-.02,w/2+.02):f.box((x,0,0),(.04,.035,h+.08),C['rust_d'],bevel=.006)
  for z in (-h/2-.02,h/2+.02):f.box((0,0,z),(w+.08,.035,.04),C['rust_d'],bevel=.006)
 a.mount('Mount_Wall',(0,.006,0));a.mount('Mount_Artwork',(0,-.003,0));return a

def chalk():
 a=Asset('ChalkStick');p=a.part('Stick');p.cyl((0,0,.045),.006,.09,C['white'],segs=8,bevel=.001);a.mount('Mount_Tip',(0,0,0));return a

def chair():
 a=Asset('WatchOfficerChair');p=a.part('Frame');p.cyl((0,0,.25),.065,.48,C['steel'],bevel=.007)
 for i in range(4):ang=i*math.pi/2;beam(p,(0,0,.07),(.34*math.cos(ang),.34*math.sin(ang),.025),.055,C['steel_d'])
 seat=a.part('SwivelSeat',location=(0,0,.52));seat.box((0,0,0),(.55,.48,.12),C['bakelite'],bevel=.045);seat.box((0,.20,.37),(.52,.11,.64),C['bakelite'],rot=(math.radians(-8),0,0),bevel=.040)
 for x in (-.31,.31):seat.box((x,0,.22),(.08,.42,.07),C['rust_d'],bevel=.020);seat.box((x,.10,.11),(.04,.045,.22),C['steel'],bevel=.005)
 a.mount('Mount_SeatedPlayer',(0,0,.61));return a

def valvesrack():
 a=Asset('VacuumValveElectronicsRack');p=a.part('Rack')
 for x in (-.45,.45):p.box((x,.1,.9),(.06,.5,1.8),C['steel_d'],bevel=.01)
 for z in (.1,.6,1.1,1.6):
  p.box((0,.1,z),(.92,.55,.045),C['bottle'],bevel=.01)
  for i in range(6):
   tube=a.part('VacuumTube_%02d_%02d'%(int(z*10),i),location=(-.35+i*.14,.02,z+.06));tube.cyl((0,0,.03),.043,.06,C['bakelite']);tube.sphere((0,0,.13),(.033,.033,.093),C['glass']);tube.cyl((0,0,.115),.012,.09,C['amber'])
 a.mount('Mount_Power',(0,.38,.12));return a

def controlrods():
 a=Asset('ControlRodDriveColumn');p=a.part('Frame')
 p.box((0,0,.08),(.72,.55,.16),C['bottle_d'],bevel=.02)
 for x in (-.30,.30):p.cyl((x,0,1.05),.044,1.95,C['steel'],bevel=.004)
 p.box((0,0,2.0),(.72,.32,.18),C['bottle'],bevel=.02)
 for i in range(4):
  rod=a.part('ControlRod%02d'%i,location=(-.21+i*.14,0,.16));rod.cyl((0,0,.82),.020,1.64,C['steel_l']);rod.cyl((0,0,1.56),.047,.12,C['brass_d'],bevel=.007)
 a.mount('Mount_Core',(0,0,0));a.mount('Mount_Actuator',(0,0,2.1));return a

def coreport():
 a=Asset('CoreViewingPorthole');p=a.part('Flange');p.disc_ring((0,0,0),.11,.21,.11,C['steel_d'],axis='Y',segs=32);studs(p,(0,-.06,0),.172)
 glass=a.part('CherenkovGlass');glass.cyl((0,-.028,0),.108,.04,C['glass'],axis='Y',segs=32)
 a.mount('Mount_CoreGlow',(0,.045,0));return a

def steam():
 a=Asset('SteamGenerator');p=a.part('PressureVessel');p.cyl((0,0,1.0),.45,1.5,C['steel'],bevel=.03,segs=32);p.sphere((0,0,1.75),(.45,.45,.24),C['steel']);p.sphere((0,0,.25),(.45,.45,.24),C['steel'])
 for z in (.36,1.64):p.disc_ring((0,0,z),.448,.475,.05,C['steel_d'])
 for x in (-.32,.32):p.box((x,0,.12),(.16,.6,.24),C['bottle_d'],bevel=.018)
 for side in (-1,1):p.cyl((side*.48,0,.85),.10,.34,C['steel'],axis='X');p.disc_ring((side*.65,0,.85),.088,.145,.055,C['brass_d'],axis='X')
 p.cyl((0,0,2.04),.09,.22,C['steel']);a.mount('Mount_SteamOutlet',(0,0,2.15));a.mount('Mount_PrimaryA',(-.65,0,.85));a.mount('Mount_PrimaryB',(.65,0,.85));a.mount('Mount_SteamLeak',(0,0,1.94));return a

def dosimetry():
 a=Asset('WallDosimetryAlarm');p=a.part('Panel');p.box((0,.05,0),(.40,.14,.55),C['cream_d'],bevel=.02);p.box((0,-.025,.04),(.32,.025,.21),C['cream'],bevel=.007)
 for i in range(9):p.box((-.12+i*.03,-.042,.04),(.002,.003,.08),C['black'])
 n=a.part('DoseNeedle',location=(-.12,-.05,-.02));n.box((.08,0,.035),(.16,.005,.005),C['black'],rot=(0,-.45,0))
 lamp=a.part('AlarmLens',location=(0,-.04,.23));lamp.sphere((0,0,0),(.065,.035,.045),C['amber'])
 p.cyl((0,-.045,-.18),.048,.036,C['bakelite'],axis='Y');a.mount('Mount_AlarmAudio',(0,.05,.28));return a

def curtain():
 a=Asset('ControlledZoneStripCurtain');p=a.part('Portal')
 for x in (-.64,.64):p.box((x,0,1.1),(.08,.15,2.2),C['bottle_d'],bevel=.01)
 p.box((0,0,2.15),(1.36,.15,.10),C['bottle_d'],bevel=.01)
 for i in range(9):
  strip=a.part('FlexibleStrip%02d'%i,location=(-.56+i*.14,0,2.12));strip.box((0,0,-1.02),(.153,.006,2.04),C['glass'],bevel=.002)
 a.mount('Mount_Doorway',(0,0,0));return a

def boron():
 a=Asset('EmergencyBoronCan');p=a.part('Can');p.box((0,0,.23),(.29,.17,.46),C['cream_d'],bevel=.032)
 p.box((0,0,.47),(.25,.04,.04),C['brass_d'],bevel=.009)
 for x in (-.10,.10):p.box((x,0,.50),(.025,.03,.08),C['brass_d'],bevel=.005)
 p.box((0,0,.54),(.22,.03,.025),C['brass'],bevel=.004)
 cap=a.part('ScrewCap',location=(.08,0,.47));cap.cyl((0,0,0),.037,.035,C['bakelite'],bevel=.005)
 a.mount('Mount_Label',(0,-.088,.25));a.mount('Mount_Grip',(0,0,.54));return a

def shaft():
 a=Asset('PropellerShaftLine');p=a.part('BearingSupports')
 for y in (-.75,.75):p.box((0,y,.12),(.46,.26,.24),C['bottle_d'],bevel=.02);p.disc_ring((0,y,.34),.09,.15,.15,C['steel_d'],axis='Y')
 r=a.part('RotatingShaft',location=(0,0,.34));r.cyl((0,0,0),.087,2.1,C['steel_l'],axis='Y');r.disc_ring((0,0,0),.085,.19,.13,C['steel'],axis='Y');studs(r,(0,-.07,0),.145)
 a.mount('Mount_Turbine',(0,-1.05,.34));a.mount('Mount_StuffingBox',(0,1.05,.34));return a

def stuffing():
 a=Asset('ShaftStuffingBox');p=a.part('Sleeve');p.disc_ring((0,0,0),.09,.16,.30,C['steel_d'],axis='Y');p.disc_ring((0,.13,0),.09,.21,.045,C['brass_d'],axis='Y');studs(p,(0,.16,0),.18)
 nut=a.part('PackingCompressionNut',location=(0,-.19,0));nut.disc_ring((0,0,0),.09,.19,.10,C['brass'],axis='Y',segs=6)
 a.mount('Mount_Shaft',(0,0,0));a.mount('Mount_Leak',(0,-.24,-.10));return a

def compressor():
 a=Asset('AirCompressor');p=a.part('Reservoir');p.cyl((0,0,.35),.23,.90,C['bottle'],axis='X',bevel=.015);p.sphere((-.45,0,.35),(.11,.23,.23),C['bottle']);p.sphere((.45,0,.35),(.11,.23,.23),C['bottle'])
 for x in (-.35,.35):p.box((x,0,.09),(.13,.38,.18),C['steel_d'],bevel=.015)
 p.box((-.23,0,.68),(.32,.26,.23),C['steel_d'],bevel=.025)
 for z in (.62,.66,.70,.74):p.box((-.23,0,z),(.36,.30,.018),C['steel_l'],bevel=.004)
 p.cyl((.18,0,.70),.12,.34,C['bottle_d'],axis='X',bevel=.010)
 fly=a.part('Flywheel',location=(-.24,-.19,.68));fly.torus((0,0,0),.145,.018,C['steel'],axis='Y');fly.cyl((0,0,0),.035,.05,C['brass'],axis='Y')
 for i in range(5):q=i*math.tau/5;fly.box((math.sin(q)*.07,0,math.cos(q)*.07),(.018,.018,.14),C['steel'],rot=(0,q,0))
 a.mount('Mount_AirOutlet',(.48,0,.38));a.mount('Mount_Filter',(0,.26,.7));return a

def co2(used=False):
 a=Asset('CO2CartridgeUsed' if used else 'CO2CartridgeNew');p=a.part('Canister');p.cyl((0,0,.22),.085,.44,C['rust_d'] if used else C['cream_d'],bevel=.01)
 for z in (.025,.40):p.torus((0,0,z),.083,.007,C['steel_d'])
 for z in (.435,.008):p.disc_ring((0,0,z),.020,.073,.01,C['steel'],segs=24)
 if used:
  for i in range(7):p.box((-.048+i*.014,-.075,.16),(.008,.005,.12),C['rust'])
 a.mount('Mount_Filter',(0,0,0));a.mount('Mount_Grip',(0,0,.22));return a

def workbench():
 a=Asset('MachineWorkbench');p=a.part('Bench');p.box((0,0,.85),(1.4,.64,.11),C['rust_d'],bevel=.024)
 for x in (-.58,.58):
  for y in (-.23,.23):p.box((x,y,.4),(.09,.09,.8),C['bottle_d'],bevel=.008)
 p.box((0,0,.20),(1.20,.47,.04),C['bottle'],bevel=.007)
 vice=a.part('ViceFixedJaw',location=(.44,-.30,.97));vice.box((0,0,0),(.18,.20,.12),C['steel_d'],bevel=.01);vice.box((0,-.055,.08),(.20,.025,.07),C['steel_l'],bevel=.005)
 jaw=a.part('ViceMovingJaw',location=(.44,-.43,1.05));jaw.box((0,0,0),(.20,.036,.07),C['steel_l'],bevel=.005);jaw.cyl((0,-.065,-.06),.017,.15,C['steel'],axis='Y');jaw.cyl((0,-.14,-.06),.012,.20,C['steel'],axis='X')
 a.mount('Mount_Toolboard',(0,.30,1.35));return a

def toolboard():
 a=Asset('WorkshopToolboard');p=a.part('Pegboard');p.box((0,.025,0),(1.2,.04,.70),C['bottle_d'],bevel=.015)
 for i in range(12):
  for j in range(5):p.cyl((-.52+i*.095,-.001,-.27+j*.135),.006,.006,C['black'],axis='Y',segs=8)
 for i in range(7):
  x=-.45+i*.15;p.cyl((x,-.045,.16),.007,.075,C['steel'],axis='Y');a.mount('Mount_Tool%02d'%i,(x,-.085,.16))
 return a

def fan():
 a=Asset('DuctVentilator');p=a.part('Duct');p.disc_ring((0,0,0),.24,.30,.32,C['steel_d'],axis='Y',segs=32)
 rotor=a.part('FanRotor');rotor.cyl((0,0,0),.057,.07,C['brass_d'],axis='Y')
 for i in range(5):q=i*math.tau/5;rotor.box((math.sin(q)*.14,0,math.cos(q)*.14),(.11,.018,.19),C['steel'],rot=(.15,q,.2),bevel=.015)
 for y in (-.17,.17):
  p.disc_ring((0,y,0),.24,.32,.028,C['steel'],axis='Y')
  for x in (-.16,-.08,0,.08,.16):p.box((x,y,0),(.012,.012,math.sqrt(.24**2-x*x)*2),C['steel_d'],bevel=.002)
 a.mount('Mount_DuctA',(0,-.19,0));a.mount('Mount_DuctB',(0,.19,0));return a

def gramophone():
 a=Asset('MessGramophone');p=a.part('Cabinet');p.box((0,0,.13),(.48,.40,.26),C['rust_d'],bevel=.025)
 deck=a.part('Turntable',location=(0,0,.29));deck.cyl((0,0,0),.18,.03,C['bakelite'],bevel=.005)
 p.cyl((.16,.12,.44),.020,.33,C['brass']);p.torus((.16,.05,.59),.07,.020,C['brass'],axis='X',arc=math.pi/2)
 beam(p,(.16,.05,.59),(.16,0,.62),.04,C['brass'])
 # Actual flared open horn, axis toward listener.
 p.cyl((.16,-.17,.62),.032,.34,C['brass'],axis='-Y',r2=.20,caps=False,segs=32)
 p.torus((.16,-.34,.62),.20,.008,C['brass_d'],axis='Y',major_segs=32)
 arm=a.part('Tonearm',location=(.17,.12,.32));beam(arm,(0,0,0),(-.10,-.14,.015),.017,C['steel']);arm.cyl((-.10,-.14,0),.024,.035,C['bakelite'])
 a.mount('Mount_Record',(0,0,.314));return a

def record():
 a=Asset('AnthemGramophoneRecord');p=a.part('Record');p.disc_ring((0,0,.002),.004,.17,.004,C['black'],segs=48)
 for r in (.06,.085,.11,.135,.16):p.disc_ring((0,0,.0043),r,r+.0008,.0003,C['bakelite'],segs=48)
 p.disc_ring((0,0,.0045),.004,.042,.001,C['cream'],segs=32);a.mount('Mount_Spindle',(0,0,0));return a

def samovar():
 a=Asset('ElectricSamovar');p=a.part('Boiler');p.cyl((0,0,.08),.15,.16,C['brass_d'],bevel=.010,segs=32);p.cyl((0,0,.34),.20,.34,C['brass'],r2=.16,bevel=.010,segs=32);p.sphere((0,0,.50),(.16,.16,.07),C['brass'],u=32,v=16)
 for side in (-1,1):p.torus((side*.21,0,.37),.055,.010,C['bakelite'],axis='Y',major_segs=16)
 p.cyl((0,-.22,.21),.026,.12,C['brass'],axis='Y');p.cyl((0,-.27,.17),.019,.09,C['brass'])
 tap=a.part('TapHandle',location=(0,-.22,.27));tap.box((0,0,0),(.08,.018,.016),C['bakelite'],bevel=.004)
 lid=a.part('BoilerLid',location=(0,0,.54));lid.cyl((0,0,0),.12,.025,C['brass_d'],bevel=.004);lid.sphere((0,0,.033),.020,C['bakelite'])
 a.mount('Mount_PowerCable',(0,.14,.07));a.mount('Mount_WaterStream',(0,-.27,.12));return a

def toilet():
 a=Asset('MarineToiletSevenValve');p=a.part('BowlAndPump');p.cyl((0,0,.12),.15,.24,C['cream'],r2=.12,bevel=.014);p.cyl((0,-.04,.34),.13,.20,C['cream'],r2=.225,caps=False,segs=32);p.cyl((0,-.04,.245),.13,.012,C['cream']);p.disc_ring((0,-.04,.45),.155,.23,.048,C['cream'],segs=32)
 p.cyl((.30,.12,.35),.035,.64,C['brass_d']);p.cyl((.30,-.01,.66),.019,.25,C['brass'],axis='Y');p.cyl((.30,-.15,.66),.027,.12,C['bakelite'],axis='X',bevel=.005)
 # Seven visible distinct branch bodies and independent handwheels.
 for i in range(7):
  x=-.42+(i%4)*.25;z=.72+(i//4)*.28
  p.cyl((x,.22,z),.036,.17,C['brass'],axis='Y');p.cyl((x,.25,z-.08),.028,.17,C['steel']);p.sphere((x,.18,z),.055,C['brass_d'])
  wheel(a,'ProcedureValve%02d'%i,(x,.10,z),.073)
  a.mount('Mount_ValveLabel%02d'%i,(x,.19,z+.10))
 for z in (.62,.90):p.cyl((-.045,.25,z),.025,.95,C['steel'],axis='X')
 p.cyl((.38,.25,.49),.025,.83,C['steel'])
 lid=a.part('SeatLid',location=(0,.22,.49));lid.cyl((0,-.25,0),.23,.025,C['bakelite'],segs=32,bevel=.009)
 a.mount('Mount_Procedure',(0,.31,1.28));a.mount('Mount_Inlet',(.30,.25,.04));a.mount('Mount_Outlet',(0,.12,.02));return a

def hoistrail():
 a=Asset('CargoCeilingHoistRail');p=a.part('Rail')
 for z in (-.07,.07):p.box((0,0,z),(.18,3.0,.025),C['steel'],bevel=.005)
 p.box((0,0,0),(.024,3,.14),C['steel_d'],bevel=.004)
 for y in (-1.3,1.3):p.box((0,y,.11),(.38,.10,.06),C['bottle_d'],bevel=.006)
 a.mount('Mount_Hoist',(0,0,-.11));a.mount('Mount_A',(0,-1.5,0));a.mount('Mount_B',(0,1.5,0));return a

def hoist():
 a=Asset('CargoCeilingHoist');p=a.part('Trolley')
 for x in (-.11,.11):
  p.box((x,0,0),(.035,.25,.20),C['steel_d'],bevel=.006)
  for y in (-.085,.085):p.cyl((x,y,.07),.047,.030,C['steel'],axis='X')
 p.cyl((0,0,-.13),.095,.18,C['bottle_d'],axis='X',bevel=.015)
 chain=a.part('ChainAndHook',location=(0,0,-.24))
 for i in range(16):chain.torus((0,0,-i*.032),.020,.006,C['steel_d'],axis='X' if i%2 else 'Y',major_segs=12,minor_segs=6)
 chain.torus((0,0,-.56),.065,.015,C['steel'],axis='Y',major_segs=20,arc=math.pi*1.55)
 a.mount('Mount_Rail',(0,0,.05));a.mount('Mount_Load',(0,0,-.84));return a

def torpedo():
 a=Asset('InertTrainingTorpedo');p=a.part('Body');p.cyl((0,0,0),.24,2.4,C['bottle_d'],axis='Y',segs=32);p.sphere((0,-1.2,0),(.24,.34,.24),C['bottle']);p.cyl((0,1.45,0),.24,.50,C['steel'],axis='Y',r2=.08,segs=32)
 for y in (-.8,.8):p.disc_ring((0,y,0),.239,.245,.075,C['cream'],axis='Y',segs=32)
 for i in range(4):ang=i*math.pi/2;p.box((math.sin(ang)*.21,1.45,math.cos(ang)*.21),(.022,.45,.25),C['steel_d'],rot=(0,ang,0),bevel=.008)
 prop=a.part('TailPropeller',location=(0,1.74,0));prop.cyl((0,0,0),.044,.08,C['brass_d'],axis='Y')
 for i in range(3):q=i*math.tau/3;prop.box((math.sin(q)*.085,0,math.cos(q)*.085),(.055,.018,.13),C['brass'],rot=(0,q,.20),bevel=.008)
 a.mount('Mount_LiftingPoint',(0,0,.245));a.mount('Mount_Grip',(0,0,0));return a

def build_all():
 return [hull('Bow'),hull('Stern'),ladder(),conning(),periscope(),externalhead(),cabletray(),junction(),compass(),cras(),clock('NavigationStopwatch',True),clock(),flat_support('Chalkboard',1.2,.70,C['black'],True),chalk(),flat_support('CommandantPortraitFrame',.40,.54,C['cream'],True),chair(),valvesrack(),controlrods(),coreport(),steam(),dosimetry(),curtain(),boron(),shaft(),stuffing(),compressor(),co2(),co2(True),workbench(),toolboard(),fan(),gramophone(),record(),samovar(),toilet(),flat_support('MarineToiletProcedureBlank',.38,.56,C['cream'],True),hoistrail(),hoist(),torpedo(),flat_support('PropagandaPosterBlank',.42,.60,C['cream']),flat_support('PeriodPhotographBlank',.15,.10,C['cream_d']),flat_support('PaperLabelBlank',.10,.045,C['cream'])]

if __name__=='__main__':
 out,preview=cli_args();out=os.path.abspath(out);preview=os.path.abspath(preview) if preview else None;reset_scene();assets=build_all()
 for a in assets:a.build()
 bpy.context.view_layer.update();report={'blender':bpy.app.version_string,'scope':'V1 interior geometry, blank artwork supports','assets':[]}
 for a in assets:
  for p in a.parts:
   me=p.ob.data;assert me.vertices and 'Col' in me.color_attributes;assert all(math.isfinite(c) for v in me.vertices for c in v.co);me.calc_loop_triangles();assert all(t.area>1e-12 for t in me.loop_triangles),(a.name,p.name)
   if p.name=='ArtworkSurface':
    uv=me.uv_layers.new(name='ArtworkUV');xs=[v.co.x for v in me.vertices];zs=[v.co.z for v in me.vertices]
    for loop in me.loops:
     v=me.vertices[loop.vertex_index].co;uv.data[loop.index].uv=((v.x-min(xs))/(max(xs)-min(xs)),(v.z-min(zs))/(max(zs)-min(zs)))
  a.export(out);report['assets'].append({'name':a.name,'triangles':a.stats(),'parts':[p.name for p in a.parts],'pivots':[{'name':p.name,'positionBlender':p.location} for p in a.parts],'mounts':[{'name':m[0],'positionBlender':m[1]} for m in a.mounts]});print('EXPORT',a.name,a.stats())
 with open(os.path.join(out,'fixtures_manifest.json'),'w',encoding='utf8') as f:json.dump(report,f,indent=2)
 bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(__file__),'sources','Fixtures.blend'))
 if preview:
  os.makedirs(preview,exist_ok=True);render_each(assets,preview,views=(('three_quarter',(.8,-1,.65)),),resolution=(1000,800))
  for a in assets:
   for ob in [a.root]+list(a.root.children_recursive):ob.hide_render=True
  scope=next(a for a in assets if a.name=='PeriscopeInterior');scope.parts[1].ob.location.z+=.65;scope.parts[3].ob.rotation_euler.y=math.radians(70)
  wc=next(a for a in assets if a.name=='MarineToiletSevenValve');wc.parts[-1].ob.rotation_euler.x=math.radians(-95)
  render_each([scope,wc],preview,views=(('deployed_open',(.8,-1,.65)),))
