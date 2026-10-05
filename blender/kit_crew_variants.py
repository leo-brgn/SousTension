"""GDD annex11 V1 geometry library; original Crew kit remains unchanged."""
import os,sys,json,math,hashlib
sys.path.insert(0,os.path.dirname(__file__))
from lib import *
from kit_crew import crew,SKIN

META={}
def addon(name,bone='Head',offset=(0,0,0),category='cosmetic'):
    a=Asset(name); a.mount('Mount_Attachment',(0,0,0))
    META[name]={'category':category,'skeletonBone':bone,'boneLocalOffset':offset,'massKg':.08,'massSource':'prototype assumption'}
    return a,a.part('Body')

def build_heads():
    shapes=[(.265,.225,.305),(.29,.22,.27),(.235,.22,.34),(.30,.235,.30),(.25,.20,.285),(.28,.24,.32)]
    for i,r in enumerate(shapes):
        a,p=addon('Head_%02d'%(i+1),'Head',category='head'); skin=srgb(['#D6A780','#C38E66','#E3BDA0','#A16F50','#B9805D','#DFAC86'][i])
        p.sphere((0,0,.17),r,skin,u=20,v=12)
        for s in (-1,1):
            p.sphere((s*(r[0]-.005),0,.15),(.047,.062,.065+i*.002),skin)
            p.sphere((s*(.072+i*.004),-r[1]+.009,.225),(.025,.020,.028+i*.001),C['black'])
            p.box((s*.085,-r[1],.282),(.075+i*.004,.025,.021),C['bakelite'],rot=(0,s*(i-2)*.05,0),bevel=.005)
        p.sphere((0,-r[1]-.025,.16),(.045+i*.006,.052+i*.003,.040),skin)
        p.box((0,-r[1]+.005,.072),(.071,.013,.012),C['bakelite'],bevel=.004)
    for i in range(12):
        a,p=addon('Moustache_%02d'%(i+1),'Head',(0,-.235,.10),'moustache')
        col=[C['bakelite'],C['rust_d'],C['steel_d'],C['cream_d']][i%4]
        for s in (-1,1):
            if i<4: p.sphere((s*(.055+i*.009),0,0),(.069+i*.012,.027,.026+i*.007),col)
            elif i<8:
                p.sphere((s*.080,0,0),(.10,.033,.041),col)
                p.sphere((s*.155,0,.024+(i-4)*.009),(.038,.025,.042+(i-4)*.008),col)
            else:
                p.box((s*.055,0,0),(.115,.035,.025+(i-8)*.014),col,rot=(0,s*(i-7)*.07,0),bevel=.01)
                if i==11: p.sphere((s*.115,0,-.040),(.026,.026,.055),col)
    for i in range(8):
        a,p=addon('Hair_%02d'%(i+1),'Head',(0,0,.18),'hair')
        col=[C['bakelite'],C['rust_d'],C['steel'],C['cream_d']][i%4]
        # Scalp cap is cut at the brow; vary locks, sideburns and profile.
        for j in range(5+i%3):
            ang=math.pi*j/(4+i%3)
            p.sphere((.21*math.cos(ang),.04+.12*math.sin(ang),.20),(.075,.09,.070+i*.006),col)
        for s in (-1,1):
            p.sphere((s*.23,.05,.05),(.040,.12,.14 if i%2 else .09),col)
        if i in (2,5): p.sphere((0,.20,.12),(.10,.065,.115),col)
        if i==7:
            for x in (-.08,0,.08): p.sphere((x,-.13,.25),(.047,.074,.085),col)

HATS=['PeakedCap','WatchBeanie','WoolBonnet','ServiceBeret','CookCap','SideCap','EarflapCap','FlatCap','DeckHardhat','WinterHood']
def hats():
    for i,name in enumerate(HATS):
        a,p=addon(name,'Head',(0,0,.40),'headwear'); col=C['cream'] if i==4 else C['bottle']
        if i in (1,2):
            p.sphere((0,0,.06),(.27,.23,.16+i*.02),col)
            p.torus((0,0,0),.235,.035,col)
            if i==2:p.sphere((0,0,.25),.045,C['cream_d'])
        elif i==3:
            p.sphere((.05,0,.055),(.32,.255,.11),col)
            p.cyl((0,0,-.015),.235,.05,C['bottle_d'])
        elif i==4:
            p.cyl((0,0,.10),.25,.23,col,bevel=.025)
            for j in range(6): p.sphere((.16*math.cos(j*math.tau/6),.16*math.sin(j*math.tau/6),.24),(.11,.11,.085),col)
        elif i==5:p.box((0,0,.065),(.18,.42,.15),col,bevel=.05)
        elif i in (6,9):
            p.sphere((0,.02,.06),(.28,.25,.17),col)
            for s in (-1,1):p.box((s*.245,.06,-.11),(.06,.20,.25),col,bevel=.025)
            if i==9:p.box((0,.225,-.09),(.40,.05,.20),col,bevel=.03)
        else:
            p.sphere((0,0,.06),(.29,.25,.10 if i==7 else .15),col)
            p.box((0,-.20,-.015),(.36,.20,.025),C['bakelite'] if i==0 else col,bevel=.02)
            if i==8:
                for x in (-.12,0,.12):p.box((x,0,.18),(.025,.35,.03),C['cream_d'],bevel=.008)
        if i in (0,3,5):p.sphere((0,-.23,.05),(.032,.014,.036),C['brass'])

def cosmetics():
    # Forty small props + ten unique headwear = exactly fifty cosmetics.
    specs=[('RoundBadge','Spine'),('SquareBadge','Spine'),('AnchorBadge','Spine'),('ChevronBadge','UpperArm_L'),('WingBadge','Spine'),('MedalSingle','Spine'),('MedalPair','Spine'),('RibbonBar','Spine'),('ServiceStars','Spine'),('Epaulette','Spine'),
    ('DeckBelt','Hips'),('UtilityBelt','Hips'),('Suspenders','Spine'),('Scarf','Neck'),('Neckerchief','Neck'),('BowTie','Neck'),('Tie','Spine'),('GloveLeft','Hand_L'),('GloveRight','Hand_R'),('MittensPair','Hand_L'),
    ('SpectaclesRound','Head'),('SpectaclesSquare','Head'),('Monocle','Head'),('Goggles','Head'),('EarDefenders','Head'),('Whistle','Spine'),('PocketWatch','Spine'),('PocketNotebook','Spine'),('PenBundle','Spine'),('PencilClip','Spine'),
    ('KeyRing','Hips'),('ToolPouch','Hips'),('Canteen','Hips'),('Satchel','Hips'),('ShoulderBag','Spine'),('Armband','UpperArm_L'),('WristCuff','Forearm_L'),('CompassPendant','Spine'),('ClothPatch','Spine'),('NamePlateBlank','Spine')]
    for i,(name,bone) in enumerate(specs):
        offset={'Spine':(.16,-.27,.17),'Hips':(.25,-.20,.20),'Neck':(0,-.14,.17),'Head':(0,-.22,.22),'Hand_L':(0,0,-.06),'Hand_R':(0,0,-.06),'UpperArm_L':(.04,0,-.10),'Forearm_L':(.02,0,-.07)}[bone]
        a,p=addon(name,bone,offset)
        col=C['brass'] if i<10 or i in (25,26,30,37) else C['cream_d'] if i in (13,14,15,16,27,28,29,38,39) else C['bakelite']
        if i==0:p.cyl((0,0,0),.035,.012,col,axis='Y')
        elif i==1:p.box((0,0,0),(.065,.014,.065),col,bevel=.008)
        elif i==2:
            p.torus((0,0,.02),.018,.005,col,axis='Y');p.box((0,0,-.018),(.009,.010,.08),col,bevel=.003);p.torus((0,0,-.04),.032,.007,col,axis='Y',arc=math.pi)
        elif i==3:
            for j in range(3):
                for s in (-1,1):p.box((s*.018,0,j*.018),(.040,.010,.010),col,rot=(0,s*.45,0),bevel=.003)
        elif i==4:
            for s in (-1,1):p.sphere((s*.035,0,0),(.045,.012,.016),col)
            p.cyl((0,0,0),.018,.017,col,axis='Y')
        elif i in (5,6):
            for j in range(i-4):p.cyl((j*.05,0,-.02),.024,.010,col,axis='Y');p.box((j*.05,0,.022),(.025,.013,.042),C['bottle'])
        elif i==7:
            for j in range(4):p.box((j*.022,0,0),(.021,.012,.024),[C['cream'],C['bottle'],C['brass'],C['rust']][j])
        elif i==8:
            for j in range(3):p.sphere((j*.025,0,0),(.01,.008,.014),col)
        elif i==9:p.box((0,0,0),(.11,.035,.055),col,bevel=.007);p.box((0,-.02,0),(.08,.008,.009),C['brass'])
        elif i in (10,11):
            META[name]['boneLocalOffset']=(0,0,.20)
            p.torus((0,0,0),.32,.025,col);p.box((0,-.33,0),(.065,.03,.055),C['brass'],bevel=.008)
            if i==11:
                for s in (-1,1):p.box((s*.25,-.20,-.065),(.09,.065,.11),C['bottle_d'],bevel=.015)
        elif i==12:
            META[name]['boneLocalOffset']=(0,-.27,.15)
            for s in (-1,1):p.box((s*.19,0,0),(.035,.035,.47),col,rot=(0,s*.15,0),bevel=.008)
        elif i in (13,14):
            p.torus((0,.12,0),.14,.032,col)
            for s in (-1,1):p.box((s*.065,0,-.075),(.065,.025,.17 if i==13 else .10),col,rot=(0,s*.2,0),bevel=.01)
        elif i==15:
            for s in (-1,1):p.sphere((s*.04,0,0),(.046,.026,.025),col)
        elif i==16:p.box((0,0,-.10),(.04,.023,.22),col,bevel=.008);p.sphere((0,0,.01),(.027,.025,.033),col)
        elif i in (17,18,19):
            p.sphere((0,0,0),(.096,.069,.10),col)
            for j in range(1 if i==19 else 4):p.sphere((-.065+j*.04,-.01,-.09),(.038 if i==19 else .024,.03,.060),col)
            p.sphere(((.075 if i==18 else -.075),-.04,.01),(.04,.045,.055),col)
        elif i in (20,21,22,23):
            for s in ([1] if i==22 else [-1,1]):
                if i in (20,22):p.torus((s*.073,0,0),.048,.006,col,axis='Y')
                else:
                    p.box((s*.073,0,0),(.12,.035,.085) if i==23 else (.10,.014,.065),col,bevel=.009)
                    p.box((s*.073,-.008,0),(.078,.014,.044),C['glass'],bevel=.007)
            if i!=22:p.box((0,0,0),(.054,.010,.009),col)
            if i==23:p.torus((0,.18,0),.235,.013,C['bottle_d'])
        elif i==24:
            for s in (-1,1):p.sphere((s*.24,.13,0),(.055,.10,.10),col)
            p.torus((0,.10,.03),.245,.013,C['steel'],axis='Y',arc=math.pi)
        elif i in (25,26,37):
            p.torus((0,0,.07),.017,.005,col,axis='Y')
            p.cyl((0,0,0),.026 if i==25 else .044,.025,col,axis='Y',bevel=.004)
            if i==25:p.cyl((0,-.027,0),.010,.034,C['steel'],axis='Y')
            if i==37:p.box((0,-.016,0),(.008,.008,.055),C['black'],rot=(0,.35,0))
        elif i in (27,38,39):p.box((0,0,0),(.085 if i==27 else .080 if i==38 else .105,.025 if i==27 else .012,.11 if i==27 else .055 if i==38 else .035),col,bevel=.005)
        elif i in (28,29):
            for j in range(3 if i==28 else 1):p.cyl((j*.020,0,0),.008,.13,C['brass'] if j%2 else C['bottle']);p.box((j*.020,-.01,.035),(.006,.01,.055),C['steel'])
        elif i==30:
            p.torus((0,0,.04),.032,.005,col,axis='Y')
            for j in range(3):p.box((j*.025-.025,0,-.018-j*.012),(.012,.009,.075),C['steel'],bevel=.003);p.box((j*.025-.018,0,-.04-j*.012),(.028,.009,.014),C['steel'])
        elif i in (31,33,34):
            p.box((0,0,0),(.18 if i==31 else .25,.085,.18 if i==31 else .27),col,bevel=.02)
            p.box((0,-.045,.06),(.17,.015,.055),C['bottle_d'],bevel=.008)
            if i==34:p.torus((0,0,.13),.13,.013,C['cream_d'],axis='Y',arc=math.pi)
        elif i==32:p.sphere((0,0,0),(.065,.044,.095),C['bottle']);p.cyl((0,0,.10),.025,.025,C['steel'])
        elif i==35:p.torus((0,0,0),.145,.018,C['cream'],axis='X');p.box((.15,0,0),(.012,.11,.08),C['bottle_d'],bevel=.004)
        elif i==36:p.torus((0,0,0),.115,.018,C['cream_d']);p.box((0,-.115,0),(.045,.025,.045),C['brass'],bevel=.004)

def rig_variant(name,kind):
    a,rig,clips=crew();a.name=name;a.root.name=name;rig.name=name+'_Rig'
    skin=next(o for o in a.root.children_recursive if o.type=='MESH');skin.name=name+'_Skin'
    if kind=='DivingSuit':
        head_group=skin.vertex_groups['Head'].index
        indices={v.index for v in skin.data.vertices if any(g.group==head_group and g.weight>.99 for g in v.groups)}
        bm=bmesh.new();bm.from_mesh(skin.data)
        bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.index in indices],context='VERTS')
        bm.to_mesh(skin.data);bm.free()
    # Preserve skin, skeleton hierarchy and rest bind poses. Recolor textile only.
    textile={'Vareuse':C['bottle_d'],'DressUniform':C['steel_d'],'CookApron':C['bottle'],'RegulationPyjamas':C['cream_d'],'DivingSuit':C['rust_d'],'ContaminatedSailor':C['bottle_d'],'CommanderVarga':C['bottle_d']}[kind]
    colors=skin.data.color_attributes['Col']
    for datum in colors.data:
        c=datum.color
        if c[1]>c[0]*1.18 and c[1]>c[2]*1.18:datum.color=textile
    extra=Asset(name+'_Equipment')
    weighted=[]
    def part(bone):
        p=extra.part(bone);weighted.append((p,bone));return p
    p=part('Spine')
    if kind=='Vareuse':
        p.box((0,-.265,1.14),(.32,.04,.24),C['cream'],bevel=.016)
        for z in (1.10,1.17,1.24):p.box((0,-.29,z),(.31,.009,.018),C['bottle_d'])
    elif kind=='DressUniform':
        for s in (-1,1):
            p.box((s*.19,-.25,1.22),(.08,.035,.24),C['bottle_d'],rot=(0,s*.3,0),bevel=.01)
            for z in (.95,1.08,1.21):p.sphere((s*.085,-.285,z),.021,C['brass'])
    elif kind=='CookApron':
        p.box((0,-.285,1.06),(.47,.035,.49),C['cream'],bevel=.010)
        p.box((0,-.31,.99),(.25,.02,.16),C['cream_d'],bevel=.006)
        part('Hips').box((0,-.235,.67),(.45,.04,.29),C['cream'],bevel=.012)
    elif kind=='RegulationPyjamas':
        for z in (.86,.96,1.06,1.16,1.26):p.box((0,-.273,z),(.43,.025,.023),C['bottle_d'],bevel=.005)
    elif kind=='DivingSuit':
        p.box((0,.29,1.10),(.33,.17,.45),C['steel_d'],bevel=.045)
        p.box((0,-.29,1.10),(.24,.10,.21),C['brass_d'],bevel=.035)
        p.cyl((0,-.355,1.10),.055,.045,C['brass'],axis='Y',bevel=.008)
        for s in (-1,1):p.box((s*.25,-.225,1.10),(.052,.035,.44),C['bakelite'],bevel=.015)
        h=part('Head');h.sphere((0,0,1.58),(.315,.27,.34),C['brass_d'],u=20,v=12)
        h.cyl((0,-.275,1.59),.18,.07,C['brass'],axis='Y',segs=24,bevel=.01)
        h.cyl((0,-.318,1.59),.151,.018,C['glass'],axis='Y',segs=24)
        for x in (-.075,0,.075):h.box((x,-.335,1.59),(.012,.014,.285),C['brass'])
        h.torus((0,0,1.32),.245,.045,C['brass'])
    elif kind=='ContaminatedSailor':
        for x,z in ((-.18,.99),(.08,1.24),(.23,1.07)):
            p.sphere((x,-.28,z),(.06,.025,.09),C['cream_d'])
        p.box((.25,-.22,1.31),(.11,.045,.085),C['amber'],bevel=.012)
    elif kind=='CommanderVarga':
        for s in (-1,1):
            p.box((s*.30,0,1.33),(.18,.20,.05),C['brass_d'],bevel=.015)
            for j in range(4):p.box((s*.30,-.03+j*.035,1.362),(.15,.016,.012),C['brass'])
        for j in range(4):p.cyl((-.18+j*.047,-.29,1.13),.019,.02,C['brass'],axis='Y')
        h=part('Head')
        for s in (-1,1):h.sphere((s*.115,-.235,1.45),(.13,.05,.065),C['steel_d']);h.box((s*.087,-.22,1.64),(.10,.035,.030),C['steel_l'],bevel=.008)
        h.box((0,-.26,1.78),(.28,.02,.032),C['brass'],bevel=.008)
    extra.build()
    for p,bone in weighted:
        ob=p.ob;ob.parent=a.root
        vg=ob.vertex_groups.new(name=bone);vg.add(list(range(len(ob.data.vertices))),1,'REPLACE')
        mod=ob.modifiers.new('Skin','ARMATURE');mod.object=rig
    bpy.data.objects.remove(extra.root,do_unlink=True)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in [skin]+[p.ob for p,_ in weighted]:ob.select_set(True)
    bpy.context.view_layer.objects.active=skin;bpy.ops.object.join()
    for track in rig.animation_data.nla_tracks:
        for strip in track.strips:strip.action.name=name+'_'+track.name
    META[name]={'category':'rigged_character','skeleton':'Crew/SailorBase','bones':27,'clips':clips,'kind':kind,'limitations':'Generic prototype clips, no IK/ragdoll; contamination is static geometry/color, not shader simulation'}
    return a,rig

def validate_mesh(ob):
    me=ob.data
    assert len(me.vertices)>0 and 'Col' in me.color_attributes
    assert all(math.isfinite(c) for v in me.vertices for c in v.co)
    me.calc_loop_triangles();assert all(t.area>1e-12 for t in me.loop_triangles),(ob.name,'degenerate triangles')
    if not me.uv_layers:
        uv=me.uv_layers.new(name='UV0')
        for poly in me.polygons:
            axis=max(range(3),key=lambda i:abs(poly.normal[i]));axes=[i for i in range(3) if i!=axis]
            for li in poly.loop_indices:
                co=me.vertices[me.loops[li].vertex_index].co;uv.data[li].uv=(co[axes[0]],co[axes[1]])

if __name__=='__main__':
    out,preview=cli_args();reset_scene();bpy.context.scene.render.fps=24
    build_heads();hats();cosmetics()
    salts,p=addon('WakeUpSalts','Hand_R',(0,0,0),'portable');META[salts.name]['massKg']=.12
    p.cyl((0,0,.055),.028,.09,C['glass'],bevel=.004);p.cyl((0,0,.11),.031,.025,C['bakelite'],bevel=.005)
    p.box((0,-.028,.055),(.040,.003,.041),C['cream'],bevel=.002)
    static=list(Asset.registry)
    animated=[rig_variant(n,n) for n in ['Vareuse','DressUniform','CookApron','RegulationPyjamas','DivingSuit','ContaminatedSailor','CommanderVarga']]
    for a in static:
        meta=META[a.name];bone=animated[0][1].data.bones[meta['skeletonBone']]
        offset=Vector(meta.pop('boneLocalOffset'))
        meta['offsetBlenderRigAxes']=list(offset)
        meta['boneLocalOffsetBlender']=list(bone.matrix_local.to_3x3().transposed()@offset)
        meta['anchorRestPositionBlender']=list(bone.head_local)
    for a in static:a.build()
    assets=static+[a for a,_ in animated];report=[]
    for a in assets:
        meshes=[o for o in a.root.children_recursive if o.type=='MESH']
        for ob in meshes:validate_mesh(ob)
        if a in static:a.export(out)
        else:
            rig=next(r for item,r in animated if item is a)
            for tr in rig.animation_data.nla_tracks:tr.mute=False
            bpy.ops.object.select_all(action='DESELECT')
            for ob in [a.root]+list(a.root.children_recursive):ob.select_set(True)
            bpy.context.view_layer.objects.active=rig
            bpy.ops.export_scene.fbx(filepath=os.path.join(out,a.name+'.fbx'),use_selection=True,object_types={'MESH','EMPTY','ARMATURE'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=False,add_leaf_bones=False,colors_type='LINEAR',bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=0)
            for tr in rig.animation_data.nla_tracks:tr.mute=True
            for pb in rig.pose.bones:pb.rotation_euler=(0,0,0);pb.location=(0,0,0)
        triangles=sum(len(ob.data.loop_triangles) for ob in meshes)
        report.append(dict(name=a.name,triangles=triangles,**META[a.name]));print('EXPORT',a.name,triangles)
    # Editable library grid: exported local origins remain unchanged.
    for i,a in enumerate(static):a.root.location=((i%10)*.9,-(i//10)*.9,0)
    for i,(a,_) in enumerate(animated):a.root.location=(i*2.0,3,0)
    bpy.context.scene.frame_set(1);bpy.context.view_layer.update()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(__file__),'sources','CrewVariants.blend'))
    with open(os.path.join(out,'crew_variants_manifest.json'),'w') as f:json.dump({'blender':bpy.app.version_string,'annex':11,'coverage':{'heads':6,'moustaches':12,'hair':8,'headwear':10,'cosmetics_including_headwear':50,'uniform_variants':4,'rigged_characters':7,'wake_up_salts':1},'assets':report},f,indent=2)
    if preview:
        os.makedirs(preview,exist_ok=True)
        for a in static:
            for ob in [a.root]+list(a.root.children_recursive):ob.hide_render=True
        # Contact-sheet construction uses shallow wrappers and returns source transforms.
        render_each([a for a,_ in animated],preview,views=(('three_quarter',(.8,-1,.5)),),resolution=(800,800))
        for a in assets:
            for ob in [a.root]+list(a.root.children_recursive):ob.hide_render=a not in static
        render_sheet(static,os.path.join(preview,'accessories_sheet.png'),cols=10,cell=.9,resolution=(1800,1400))
