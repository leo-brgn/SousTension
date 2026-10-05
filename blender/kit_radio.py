"""Issue120 P2 radio/sonar art kit. No simulated electronics or invented message text."""
import os,sys,json,math
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from lib import *

def knob(a,name,loc,r=.033):
    p=a.part(name,location=loc)
    p.cyl((0,0,0),r,.04,C['bakelite'],axis='Y',segs=16,bevel=.005)
    p.box((0,-.023,r*.5),(.005,.005,r*.55),C['cream'])
    return p

def dial(p,x,z,r=.08,y=-.245):
    p.cyl((x,y,z),r,.015,C['cream'],axis='Y',segs=24)
    p.disc_ring((x,y-.013,z),r*.93,r*1.12,.02,C['brass'],axis='Y')
    for i in range(9):
        angle=math.radians(-120+i*30)
        p.box((x+math.sin(angle)*r*.77,y-.014,z+math.cos(angle)*r*.77),(.004,.003,.012),C['black'],rot=(0,angle,0))

def cabinet(p,w,h,d):
    p.box((0,.06,h/2),(w,d,h),C['bottle'],bevel=.03)
    p.box((0,.055-d/2,h*.65),(w*.93,.03,h*.58),C['bottle_d'],bevel=.013)
    for x in (-w*.43,w*.43):
        for z in (h*.40,h*.90): p.cyl((x,.03-d/2,z),.013,.02,C['brass_d'],axis='Y',segs=8)

def sonar():
    a=Asset('SonarConsole'); p=a.part('Cabinet'); cabinet(p,1.15,1.30,.62)
    p.box((0,-.25,.60),(1.23,.47,.10),C['bottle'],bevel=.025)
    p.disc_ring((0,-.276,.98),.211,.249,.06,C['brass_d'],axis='Y',segs=40)
    screen=a.part('ScreenRenderTarget',location=(0,-.30,.98))
    screen.cyl((0,0,0),.209,.008,C['bottle_d'],axis='Y',segs=48)
    for x in (-.4,.4): knob(a,'GainKnob' if x<0 else 'RangeKnob',(x,-.30,.76),.045)
    for x in (-.39,-.13,.13,.39):
        p.cyl((x,-.30,1.18),.018,.019,C['amber'],axis='Y')
    cover=a.part('ServiceCover',location=(-.52,-.263,.25))
    cover.box((.52,0,0),(1.04,.035,.40),C['bottle_d'],bevel=.016)
    cover.box((.9,-.025,0),(.045,.025,.10),C['bakelite'],bevel=.007)
    a.mount('Mount_Screen',(0,-.309,.98)); a.mount('Mount_Headphones',(.50,-.31,.65)); a.mount('Mount_Floor',(0,0,0))
    return a

def reel(name='TapeReelBlank',recorded=False):
    a=Asset(name); p=a.part('Reel')
    # Open spokes and center bore read correctly from either side.
    for y in (-.014,.014):
        p.disc_ring((0,y,0),.110,.126,.008,C['steel_l'],axis='Y',segs=32)
        p.disc_ring((0,y,0),.017,.033,.012,C['steel'],axis='Y')
        for i in range(6):
            angle=i*math.pi/3
            p.box((math.sin(angle)*.073,y,math.cos(angle)*.073),(.021,.008,.085),C['steel_l'],rot=(0,angle,0),bevel=.003)
    p.disc_ring((0,0,0),.031,.099 if recorded else .074,.022,C['bakelite'],axis='Y',segs=32)
    if recorded: p.box((0,-.022,-.075),(.050,.002,.015),C['cream'])
    a.mount('Mount_Spindle',(0,0,0)); a.mount('Mount_Grip',(0,0,0)); return a

def recorder():
    a=Asset('MagneticTapeRecorder'); p=a.part('Cabinet'); cabinet(p,.80,.68,.29)
    for x in (-.205,.205):
        p.cyl((x,-.115,.47),.021,.058,C['brass'],axis='Y')
        a.mount('Mount_LeftReel' if x<0 else 'Mount_RightReel',(x,-.155,.47))
    for x in (-.10,.10): p.cyl((x,-.119,.29),.025,.043,C['steel_l'],axis='Y')
    p.box((0,-.13,.25),(.066,.04,.085),C['brass_d'],bevel=.005)
    tape=a.part('LoadedTapePath')
    tape.box((0,-.150,.344),(.41,.003,.009),C['bakelite'])
    for x in (-.20,.20): tape.box((x,-.15,.389),(.003,.003,.095),C['bakelite'])
    for i in range(4):
        button=a.part('TransportButton%02d'%i,location=(-.21+i*.14,-.15,.135))
        button.box((0,0,0),(.075,.038,.043),C['cream_d'] if i!=3 else C['bakelite'],bevel=.005)
    dial(p,.285,.26,.043,y=-.113)
    a.mount('Mount_Table',(0,0,0)); return a

def headphones():
    a=Asset('SonarHeadphones'); p=a.part('Headband')
    p.torus((0,0,.095),.13,.012,C['steel_d'],axis='-Y',major_segs=24,arc=math.pi)
    # Torus half lies above the earcups in XZ.
    for x in (-.13,.13): p.box((x,0,.065),(.014,.019,.09),C['steel'],bevel=.003)
    for side in (-1,1):
        ear=a.part('LeftEarcup' if side<0 else 'RightEarcup',location=(side*.13,0,.015))
        ear.cyl((0,0,0),.059,.041,C['bakelite'],axis='X',segs=20,bevel=.008)
        ear.disc_ring((-side*.024,0,0),.03,.052,.012,C['black'],axis='X')
    a.mount('Mount_Cable',(-.13,0,-.033)); a.mount('Mount_Head',(0,0,.04)); return a

def cable():
    a=Asset('HeadphoneCableCoiled'); p=a.part('Cable')
    for i in range(18): p.torus((0,0,.021+i*.018),.014,.003,C['bakelite'],major_segs=12,minor_segs=6)
    for z in (0,.36): p.cyl((0,0,z),.004,.035,C['bakelite'])
    p.cyl((0,0,.395),.005,.023,C['brass'],bevel=.001)
    a.mount('Mount_Headphones',(0,0,-.017)); a.mount('Mount_RadioJack',(0,0,.406)); return a

def hydrophone():
    a=Asset('HydrophoneCrankStation'); p=a.part('Housing')
    p.box((0,.055,.22),(.39,.15,.44),C['bottle'],bevel=.022)
    p.cyl((0,-.049,.27),.117,.043,C['brass'],axis='Y',bevel=.006)
    dial(p,0,.27,.10,y=-.078)
    crank=a.part('OrientationCrank',location=(0,-.115,.27))
    crank.cyl((0,0,0),.033,.03,C['brass_d'],axis='Y')
    crank.box((.085,0,0),(.17,.025,.022),C['brass'],bevel=.005)
    grip=a.part('CrankGrip',parent=crank,location=(.17,-.04,0))
    grip.cyl((0,0,0),.020,.085,C['bakelite'],axis='Y',bevel=.004)
    a.mount('Mount_Wall',(0,.13,.22)); a.mount('Mount_AcousticCable',(0,.05,.44)); return a

def radio():
    a=Asset('VLFRadioStation'); p=a.part('Cabinet'); cabinet(p,1.03,.70,.36)
    dial(p,-.24,.46,.115,y=-.146)
    needle=a.part('FrequencyNeedle',location=(-.24,-.167,.46))
    needle.box((0,0,.045),(.006,.006,.09),C['black'])
    p.box((.245,-.143,.49),(.30,.015,.18),C['cream'],bevel=.006)
    for i in range(7): p.box((.14+i*.035,-.153,.49),(.004,.006,.11 if i%2==0 else .065),C['black'])
    for i in range(3): knob(a,'RadioKnob%d'%i,(-.28+i*.27,-.165,.19),.045)
    for i in range(9): p.box((.39,-.140,.48+(i-4)*.015),(.10,.015,.005),C['black'])
    a.mount('Mount_AntennaCable',(.47,.08,.62)); a.mount('Mount_Table',(0,0,0)); a.mount('Mount_HeadphoneJack',(-.44,-.15,.2)); return a

def antenna():
    a=Asset('VLFAntennaDeployable'); p=a.part('MastHousing')
    p.box((0,.06,.20),(.35,.19,.40),C['bottle_d'],bevel=.025)
    p.cyl((0,0,.52),.068,.64,C['steel_d'],bevel=.008)
    for z in (.27,.82): p.disc_ring((0,0,z),.058,.084,.035,C['brass_d'])
    mast=a.part('ExtendableMast',location=(0,0,.81))
    mast.cyl((0,0,.33),.034,.66,C['steel_l'],bevel=.004)
    mast.cyl((0,0,.74),.012,.20,C['brass'],bevel=.002)
    crank=a.part('DeploymentCrank',location=(.13,-.10,.28))
    crank.cyl((0,0,0),.031,.025,C['brass'],axis='Y')
    crank.box((0,0,.075),(.024,.025,.15),C['brass'],bevel=.004)
    crank.cyl((0,-.035,.15),.021,.07,C['bakelite'],axis='Y',bevel=.004)
    a.mount('Mount_Ceiling',(0,0,.84)); a.mount('Mount_AerialTip',(0,0,1.65)); a.mount('Mount_RadioCable',(0,.15,.10)); return a

def cipher():
    a=Asset('MechanicalCipherMachine'); p=a.part('Case')
    p.box((0,.055,.125),(.62,.44,.25),C['bottle'],bevel=.029)
    p.box((0,-.205,.115),(.60,.025,.15),C['bottle_d'],bevel=.012)
    for row in range(3):
        for col in range(10):
            key=a.part('Key_%02d_%02d'%(row,col),location=(-.248+col*.055,-.115+row*.064,.268))
            key.cyl((0,0,0),.018,.026,C['bakelite'],bevel=.004)
            key.cyl((0,0,.015),.012,.003,C['cream_d'])
    carriage=a.part('PaperCarriage',location=(0,.17,.33))
    carriage.cyl((0,0,0),.033,.57,C['bakelite'],axis='X',bevel=.004)
    carriage.box((0,.03,-.019),(.58,.04,.04),C['steel_d'],bevel=.005)
    for x in (-.30,.30): carriage.cyl((x,0,0),.038,.045,C['brass_d'],axis='X',bevel=.005)
    space=a.part('SpaceBar',location=(0,-.185,.24))
    space.box((0,0,0),(.29,.035,.03),C['bakelite'],bevel=.007)
    a.mount('Mount_MessagePaper',(0,.195,.39)); a.mount('Mount_Table',(0,0,0)); return a

def messages():
    a=Asset('CipherMessagePad'); p=a.part('Pad')
    p.box((0,0,.005),(.21,.297,.01),C['cream_d'],bevel=.002)
    page=a.part('TopMessageSheet',location=(0,.14,.011))
    page.box((0,-.14,0),(.207,.29,.001),C['cream'])
    for x in (-.085,.085): page.box((x,-.14,.0006),(.001,.25,.0003),C['cream_d'])
    a.mount('Mount_Artwork',(0,0,.012)); a.mount('Mount_Grip',(0,0,.005)); return a

def build_all():
    return [sonar(),recorder(),reel(),reel('TapeReelRecorded',True),headphones(),cable(),hydrophone(),radio(),antenna(),cipher(),messages()]

if __name__=='__main__':
    out,preview=cli_args(); out=os.path.abspath(out); preview=os.path.abspath(preview) if preview else None
    reset_scene(); assets=build_all()
    for a in assets: a.build()
    bpy.context.view_layer.update()
    report={'issue':120,'blender':bpy.app.version_string,'scope':'P2 art kit, no gameplay','assets':[]}
    for a in assets:
        for p in a.parts:
            me=p.ob.data; assert len(me.vertices)>0 and 'Col' in me.color_attributes
            assert all(math.isfinite(c) for v in me.vertices for c in v.co)
            me.calc_loop_triangles(); assert all(t.area>1e-12 for t in me.loop_triangles),(a.name,p.name)
            if p.name in ('ScreenRenderTarget','TopMessageSheet'):
                uv=me.uv_layers.new(name='SurfaceUV')
                for loop in me.loops:
                    v=me.vertices[loop.vertex_index].co
                    uv.data[loop.index].uv=((v.x/.418+.5,v.z/.418+.5) if p.name=='ScreenRenderTarget' else (v.x/.207+.5,(v.y+.285)/.29))
        a.export(out)
        report['assets'].append({'name':a.name,'triangles':a.stats(),'parts':[p.name for p in a.parts],'pivots':[{'name':p.name,'positionBlender':p.location} for p in a.parts],'mounts':[{'name':m[0],'positionBlender':m[1]} for m in a.mounts]})
        print('EXPORT',a.name,a.stats())
    with open(os.path.join(out,'radio_manifest.json'),'w',encoding='utf8') as f: json.dump(report,f,indent=2)
    # Editable preview/source reel assembly; exports above remain modular.
    for i,x in enumerate((-.205,.205)):
        r=reel('RecorderPreviewReel%d'%i,True); r.build(); r.root.parent=assets[1].root; r.root.location=(x,-.155,.47)
    bpy.context.view_layer.update()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(__file__),'sources','RadioSonar.blend'))
    if preview:
        os.makedirs(preview,exist_ok=True); render_each(assets,preview)
        assets[0].parts[-1].ob.rotation_euler.z=math.radians(-105)
        assets[8].parts[1].ob.location.z+=.6
        for a in assets:
            for ob in [a.root]+list(a.root.children_recursive): ob.hide_render=True
        render_each([assets[0],assets[8]],preview,views=(('open_deployed',(.8,-1,.65)),))
