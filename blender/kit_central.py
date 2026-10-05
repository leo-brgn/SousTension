"""P1 central station, issue 117. One metre units, editable parts and interaction mounts."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lib import *
import json

def wheel(a, name, loc, radius):
    p=a.part(name,location=loc)
    p.torus((0,0,0),radius,.018,C['bakelite'],axis='Y',major_segs=32)
    p.cyl((0,0,0),.05,.08,C['brass'],axis='Y',bevel=.005)
    for i in range(6):
        ang=i*math.pi/3
        p.box((math.sin(ang)*radius/2,0,math.cos(ang)*radius/2),(.018,.024,radius),C['brass'],rot=(0,ang,0),bevel=.003)
    return p

def bolts(p,xs,z,y):
    for x in xs: p.cyl((x,y,z),.013,.018,C['brass_d'],axis='Y',segs=8,bevel=.002)

def steering():
    a=Asset('CentralSteeringWheel'); p=a.part('Pedestal')
    p.box((0,0,.06),(.5,.45,.12),C['bottle_d'],bevel=.025)
    p.cyl((0,0,.53),.105,.94,C['bottle'],bevel=.015)
    p.cyl((0,-.07,.98),.085,.20,C['brass'],axis='Y',bevel=.012)
    wheel(a,'Wheel',(0,-.20,.98),.31)
    a.mount('Mount_Hands',(0,-.24,.98)); a.mount('Mount_Floor',(0,0,0))
    return a

def helm():
    a=Asset('CentralDepthTrimConsole'); p=a.part('Cabinet')
    p.box((0,.12,.52),(1.12,.54,1.04),C['bottle'],bevel=.045)
    p.box((0,-.165,.76),(1.03,.035,.43),C['bottle_d'],bevel=.014)
    p.box((0,.08,1.05),(1.2,.65,.09),C['steel_d'],bevel=.025)
    for x in (-.29,.29):
        p.cyl((x,-.207,.80),.102,.028,C['cream'],axis='Y')
        p.disc_ring((x,-.223,.80),.094,.112,.025,C['brass'],axis='Y')
        needle=a.part('DepthNeedle' if x<0 else 'TrimNeedle',location=(x,-.247,.80))
        needle.box((0,0,.037),(.006,.008,.075),C['black'],bevel=.001)
        wheel(a,'DepthWheel' if x<0 else 'TrimWheel',(x,-.24,.44),.19)
    bolts(p,(-.46,.46),.94,-.20); bolts(p,(-.46,.46),.58,-.20)
    a.mount('Mount_Floor',(0,0,0)); a.mount('Mount_Operator',(0,-.8,.0))
    return a

def telegraph():
    a=Asset('EngineTelegraphFivePosition'); p=a.part('Body')
    p.cyl((0,0,.04),.22,.08,C['brass_d'],bevel=.01)
    p.cyl((0,0,.38),.07,.68,C['brass'],r2=.11,bevel=.008)
    p.cyl((0,0,.80),.255,.16,C['brass'],axis='Y',segs=32,bevel=.01)
    p.cyl((0,-.089,.80),.224,.015,C['cream'],axis='Y',segs=32)
    p.disc_ring((0,-.103,.80),.222,.247,.025,C['brass_d'],axis='Y')
    for i in range(5):
        ang=math.radians(-70+i*35)
        p.box((math.sin(ang)*.177,-.113,.80+math.cos(ang)*.177),(.012,.008,.031),C['black'],rot=(0,ang,0))
        a.mount('Mount_Position%d'%i,(math.sin(ang)*.177,-.12,.80+math.cos(ang)*.177))
    h=a.part('CommandHandle',location=(0,-.145,.80))
    h.cyl((0,0,0),.038,.055,C['brass'],axis='Y',bevel=.006)
    h.box((0,0,.15),(.022,.024,.30),C['brass'],bevel=.006)
    h.cyl((0,0,.30),.039,.14,C['bakelite'],axis='X',bevel=.009)
    a.mount('Mount_HandlePivot',(0,-.145,.80))
    return a

def charttable():
    a=Asset('CentralChartTable'); p=a.part('Table')
    p.box((0,0,.88),(1.55,.94,.10),C['bottle'],bevel=.024)
    for x in (-.62,.62):
        for y in (-.32,.32): p.box((x,y,.425),(.085,.085,.85),C['bottle_d'],bevel=.015)
    p.box((0,.32,.35),(1.30,.055,.065),C['steel_d'],bevel=.009)
    for x in (-.72,.72): p.box((x,0,.96),(.035,.91,.07),C['brass_d'],bevel=.007)
    drawer=a.part('Drawer',location=(0,-.44,.75))
    drawer.box((0,.13,0),(.88,.31,.12),C['bottle_d'],bevel=.012)
    drawer.box((0,-.043,0),(.2,.03,.025),C['brass'],bevel=.006)
    a.mount('Mount_ChartPaper',(0,0,.936)); a.mount('Mount_Manual',(.42,.24,.936)); a.mount('Mount_Pencil',(-.48,-.32,.94))
    return a

def paper():
    a=Asset('ChartPaperBlank'); p=a.part('DrawablePaper')
    p.box((0,0,.002),(.99,.64,.004),C['cream'],bevel=.001)
    for x in (-.43,.43): p.box((x,0,.0045),(.002,.55,.001),C['cream_d'])
    for y in (-.27,.27): p.box((0,y,.0045),(.86,.002,.001),C['cream_d'])
    a.mount('Mount_DrawableSurface',(0,0,.005)); return a

def pencil():
    a=Asset('GreasePencil'); p=a.part('Pencil')
    p.cyl((0,0,.085),.008,.14,C['cream_d'],segs=8)
    p.cyl((0,0,.011),.008,.018,C['rust'],r2=.003,segs=8)
    p.cyl((0,0,.002),.003,.004,C['black'],r2=.001,segs=8)
    p.cyl((0,0,.158),.0085,.008,C['brass_d'],segs=8)
    a.mount('Mount_Tip',(0,0,0)); a.mount('Mount_Grip',(0,0,.075)); return a

def loosepage():
    a=Asset('ManualOK114LoosePage'); p=a.part('DrawablePage')
    p.box((0,0,.00025),(.276,.418,.0005),C['cream'])
    a.mount('Mount_Grip',(0,0,0)); a.mount('Mount_Artwork',(0,0,.0005)); return a

def manual():
    a=Asset('ManualOK114OpenBinder'); p=a.part('Spine')
    p.box((0,0,.035),(.065,.46,.07),C['rust_d'],bevel=.012)
    for side in (-1,1):
        cover=a.part('LeftCover' if side<0 else 'RightCover',location=(side*.028,0,.016))
        cover.box((side*.15,0,0),(.30,.46,.025),C['rust'],bevel=.008)
        stack=a.part('LeftPageBlock' if side<0 else 'RightPageBlock')
        stack.box((side*.165,0,.043),(.275,.42,.04),C['cream_d'],bevel=.004)
        for j in range(6): stack.box((side*.165,-.211,.027+j*.006),(.273,.001,.001),C['cream'])
    for y in (-.15,0,.15): p.torus((0,y,.062),.024,.004,C['steel_l'],axis='Y',major_segs=16)
    for i in range(30):
        page=a.part('TurnableLeaf%02d'%i,location=(.032,0,.065+i*.00025))
        page.box((.139,0,0),(.276,.418,.0002),C['cream'])
    a.mount('Mount_PageArtworkLeft',(-.166,0,.065)); a.mount('Mount_PageArtworkRight',(.17,0,.073))
    a.mount('Mount_Grip',(0,0,.035)); return a

def console():
    a=Asset('CentralInstrumentConsole'); p=a.part('Cabinet')
    p.box((0,.13,.59),(1.62,.58,1.18),C['bottle'],bevel=.05)
    p.box((0,-.18,.87),(1.50,.06,.53),C['bottle_d'],bevel=.025)
    p.box((0,-.15,.55),(1.68,.43,.09),C['bottle'],bevel=.024)
    for row in range(2):
        for col in range(4):
            x=-.56+col*.375; z=.75+row*.25
            p.cyl((x,-.221,z),.095,.015,C['cream'],axis='Y')
            p.disc_ring((x,-.231,z),.090,.106,.022,C['brass'],axis='Y')
            a.mount('Mount_Gauge%d%d'%(row,col),(x,-.255,z))
    for i in range(4): a.mount('Mount_Control%d'%i,(-.55+i*.37,-.22,.61))
    for x in (-.64,.64):
        for z in (.18,.40): bolts(p,(x,),z,-.174)
    a.mount('Mount_Floor',(0,0,0)); return a

def station():
    a=Asset('PneumaticArrivalStation'); p=a.part('Housing')
    p.box((0,.16,.5),(.43,.16,.94),C['bottle_d'],bevel=.025)
    for z in (.22,.62): p.cyl((0,0,z),.17,.19,C['brass'],segs=24,bevel=.010)
    p.box((0,.07,.42),(.29,.17,.23),C['brass'],bevel=.014)
    for x in (-.145,.145): p.box((x,-.02,.42),(.05,.17,.23),C['brass'],bevel=.01)
    p.cyl((0,-.021,.42),.103,.006,C['black'],axis='Y')
    p.cyl((0,0,.86),.09,.32,C['brass_d'],bevel=.007)
    for z in (.13,.72): p.disc_ring((0,0,z),.16,.192,.047,C['brass_d'])
    p.disc_ring((0,-.155,.42),.102,.13,.052,C['brass_d'],axis='Y')
    hatch=a.part('ArrivalHatch',location=(-.13,-.175,.42))
    hatch.cyl((.13,0,0),.11,.028,C['bottle'],axis='Y',bevel=.008)
    hatch.cyl((.13,-.019,0),.077,.012,C['glass'],axis='Y')
    hatch.box((.215,-.04,0),(.024,.033,.085),C['bakelite'],bevel=.005)
    a.mount('Mount_Capsule',(0,0,.40)); a.mount('Mount_AirInlet',(0,0,1.02)); a.mount('Mount_Whistle',(0,.02,.8)); return a

def capsule():
    a=Asset('PneumaticCapsule'); p=a.part('Body')
    p.disc_ring((0,0,.145),.051,.062,.27,C['brass'],segs=24)
    p.cyl((0,0,.007),.062,.014,C['brass'],bevel=.003)
    for z in (.022,.235): p.torus((0,0,z),.062,.009,C['bakelite'])
    lid=a.part('Cap',location=(.06,0,.28))
    lid.cyl((-.06,0,.014),.067,.028,C['brass_d'],bevel=.006)
    lid.cyl((-.06,0,.034),.016,.016,C['brass'],bevel=.004)
    a.mount('Mount_RolledOrder',(0,0,.06)); a.mount('Mount_Grip',(0,0,.14)); return a

def rolledpaper():
    a=Asset('RolledOrderPaper'); p=a.part('Roll')
    p.disc_ring((0,0,.10),.011,.024,.20,C['cream'],segs=24)
    p.disc_ring((0,0,.10),.006,.010,.198,C['cream_d'],segs=24)
    p.box((.023,0,.10),(.002,.013,.196),C['cream'],bevel=.0005)
    a.mount('Mount_Grip',(0,0,.1)); return a

def page_templates(out):
    """Thirty neutral printable layouts. Empty caption/diagram boxes, no invented procedures."""
    folder=os.path.join(out,'ManualArtworkTemplates'); os.makedirs(folder,exist_ok=True)
    w,h=1024,512
    for index in range(30):
        pixels=list(C['cream'])*(w*h)
        def rect(x,y,ww,hh,color):
            for yy in range(y,min(y+hh,h)):
                for xx in range(x,min(x+ww,w)):
                    off=(yy*w+xx)*4; pixels[off:off+4]=color
        def frame(x,y,ww,hh):
            for args in ((x,y,ww,2),(x,y+hh-2,ww,2),(x,y,2,hh),(x+ww-2,y,2,hh)): rect(*args,C['cream_d'])
        rect(510,0,4,h,C['cream_d'])
        for side in range(2):
            start=side*512
            frame(start+35,35,440,438); frame(start+55,420,250,24)
            if index%3==0:
                frame(start+55,205,400,190); frame(start+55,65,400,115)
            elif index%3==1:
                frame(start+55,70,185,325); frame(start+270,70,185,325)
            else:
                for row in range(3): frame(start+55,65+row*112,400,92)
        img=bpy.data.images.new('ManualLayout%02d'%index,width=w,height=h)
        img.pixels.foreach_set(pixels); img.filepath_raw=os.path.join(folder,'OK114_LayoutSpread%02d_TEMPLATE.png'%index)
        img.file_format='PNG'; img.save(); bpy.data.images.remove(img)
    with open(os.path.join(folder,'README.txt'),'w',encoding='utf-8') as f:
        f.write('ART TEMPLATES ONLY. 30 blank double page layout PNGs, each 1024x512. No game procedures, localized text or diagrams have been approved. UV0 each leaf maps a full page; use left/right half of each spread in the page material. Manual geometry has 30 separately hinged leaves and an 8 kg GDD target mass. Final procedures and illustration content remain a game-design task.\n')

if __name__=='__main__':
    out,preview=cli_args(); out=os.path.abspath(out)
    if preview: preview=os.path.abspath(preview)
    reset_scene()
    assets=[steering(),helm(),telegraph(),charttable(),paper(),pencil(),manual(),console(),station(),capsule(),rolledpaper(),loosepage()]
    report={'blender':bpy.app.version_string,'issue':117,'assets':[]}
    for a in assets: a.build()
    # UV0 covers each paper face in normalized page space; back faces reuse artwork.
    for a in (assets[4],assets[6],assets[11]):
        for p in a.parts:
            if a in (assets[4],assets[11]) or p.name.startswith('TurnableLeaf') or 'PageBlock' in p.name:
                me=p.ob.data; uv=me.uv_layers.new(name='PageUV')
                lo=min(v.co.x for v in me.vertices); hi=max(v.co.x for v in me.vertices)
                bottom=min(v.co.y for v in me.vertices); top=max(v.co.y for v in me.vertices)
                for loop in me.loops:
                    v=me.vertices[loop.vertex_index].co
                    uv.data[loop.index].uv=((v.x-lo)/(hi-lo),(v.y-bottom)/(top-bottom))
    bpy.context.view_layer.update()
    for a in assets:
        for p in a.parts:
            me=p.ob.data; assert len(me.vertices)>0 and 'Col' in me.color_attributes
            assert all(math.isfinite(c) for v in me.vertices for c in v.co)
            me.calc_loop_triangles(); assert all(t.area>1e-12 for t in me.loop_triangles),(a.name,p.name)
        a.export(out)
        mass={'ManualOK114OpenBinder':8.0,'GreasePencil':.035,'PneumaticCapsule':.45,'RolledOrderPaper':.008,'ChartPaperBlank':.025,'ManualOK114LoosePage':.005}.get(a.name)
        report['assets'].append({'name':a.name,'triangles':a.stats(),'parts':[p.name for p in a.parts],'pivots':[{'name':p.name,'positionBlender':p.location} for p in a.parts],'mounts':[{'name':m[0],'positionBlender':m[1]} for m in a.mounts],'massKg':mass,'massSource':'GDD' if a.name=='ManualOK114OpenBinder' else 'prototype assumption' if mass else None})
        print('EXPORT',a.name,a.stats())
    with open(os.path.join(out,'central_manifest.json'),'w',encoding='utf-8') as f: json.dump(report,f,indent=2)
    page_templates(out)
    src=os.path.join(os.path.dirname(__file__),'sources','Central.blend')
    bpy.ops.wm.save_as_mainfile(filepath=src)
    if preview:
        os.makedirs(preview,exist_ok=True); render_each(assets,preview,views=(('three_quarter',(.8,-1,.75)),),resolution=(1000,800))
        assets[6].parts[-1].ob.rotation_euler.y=math.radians(-70)
        assets[8].parts[1].ob.rotation_euler.z=math.radians(-105)
        assets[9].parts[1].ob.rotation_euler.y=math.radians(110)
        for other in assets:
            for ob in [other.root]+list(other.root.children_recursive): ob.hide_render=True
        render_each([assets[6],assets[8],assets[9]],preview,views=(('open',(.8,-1,.75)),),resolution=(1000,800))
