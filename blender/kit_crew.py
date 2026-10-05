"""Moodboard sailor and independent first-person hands, Blender 5.2.
Continuous sleeves and fused trousers with two-bone transitions.
Generic clips remain prototypes; rest proportions follow reference image 11.
"""
import os, sys, json, math
sys.path.insert(0, os.path.dirname(__file__))
from lib import *
from crew_shapes import clean_part, loft, tube, panel, crew_material

SKIN = srgb('#E8AD83')
UNIFORM = srgb('#476F35')
TROUSERS = srgb('#395B2D')
HAIR = srgb('#25251F')

def crew(fp=False):
    a = Asset('FirstPersonArms' if fp else 'SailorBase')
    prefix = 'FP_' if fp else ''
    bones = {}
    parts = {}
    def bone(name, head, tail, parent=None):
        bones[prefix+name] = (head, tail, prefix+parent if parent else None)
        p = clean_part(a.part(name)); parts[name] = p
        return p
    def sphere(p, c, r, color): p.sphere(c, r, color, u=24, v=16)
    bone('Root', (0,0,0), (0,0,.15))
    if not fp:
        p=bone('Hips',(0,0,.30),(0,0,.55),'Root')
        # One fused pair of trousers, weighted below the crotch to the legs.
        loft(p,[(.29,.25,.20,0),(.32,.335,.25,0),(.39,.425,.29,0),(.50,.465,.31,0),(.57,.46,.30,0)],TROUSERS)
        for sign in (-1,1):
            p.cyl((sign*.225,0,.305),.132,.20,TROUSERS,segs=32,r2=.193)
        p=bone('Spine',(0,0,.55),(0,0,.98),'Hips')
        loft(p,[(.515,.461,.31,0),(.54,.48,.32,0),(.66,.49,.335,0),(.82,.46,.32,0),(.98,.405,.28,0),(1.085,.35,.24,0),(1.12,.305,.215,0)],UNIFORM)
        # Hem and sewn centre line follow the coat surface rather than floating.
        loft(p,[(.516,.462,.311,0),(.526,.476,.319,0),(.540,.480,.321,0)],shade(UNIFORM,.94))
        for z,y in ((.585,-.333),(.765,-.338),(.945,-.300)):
            p.cyl((0,y,z),.028,.019,C['brass'],axis='Y',segs=28,bevel=.005,seg=3)
            p.cyl((0,y-.011,z),.020,.002,shade(C['brass'],1.08),axis='Y',segs=24)
        for sign in (-1,1):
            panel(p,[(sign*.018,-.245,1.086),(sign*.32,-.205,1.125),(sign*.355,-.233,1.065),(sign*.19,-.282,.997)],.047,shade(UNIFORM,1.02),bevel=.014)
            p.box((sign*.363,.005,1.077),(.145,.22,.029),srgb('#683F26'),rot=(0,sign*.13,0),bevel=.012,seg=3)
            sphere(p,(sign*.336,-.072,1.095),(.019,.017,.009),C['brass'])
        p=bone('Neck',(0,0,.98),(0,0,1.12),'Spine')
        sphere(p,(0,0,1.085),(.265,.198,.12),SKIN)
        p=bone('Head',(0,0,1.12),(0,0,1.60),'Neck')
        loft(p,[(1.045,.24,.188,0),(1.073,.33,.23,0),(1.14,.345,.253,0),(1.255,.33,.26,0),(1.385,.30,.248,0),(1.51,.264,.223,0),(1.605,.235,.195,0),(1.65,.17,.14,0)],SKIN,segments=48)
        for sign in (-1,1):
            sphere(p,(sign*.314,.018,1.30),(.027,.038,.049),SKIN)
            sphere(p,(sign*.086,-.235,1.437),(.021,.012,.027),HAIR)
            sphere(p,(sign*.081,-.246,1.446),(.0045,.0025,.0055),C['white'])
            tube(p,[(sign*.047,-.219,1.505),(sign*.078,-.224,1.512),(sign*.12,-.211,1.501)],[.010,.014,.008],HAIR,segments=12,smooth=5,depth=.65)
            # Broad moustache lobes taper into a hooked tip. Longitudinal ridges
            # are actual geometry, kept subtle so the face reads at game distance.
            points=[(sign*.010,-.280,1.343),(sign*.050,-.300,1.316),(sign*.105,-.305,1.303),(sign*.161,-.293,1.311),(sign*.208,-.272,1.337),(sign*.225,-.256,1.375)]
            tube(p,points,[.018,.043,.048,.038,.022,.002],HAIR,segments=16,smooth=5,depth=.68)
            for j in range(4):
                zoff=(j-1.5)*.014
                ridge=[(sign*.022,-.306,1.337+zoff*.45),(sign*.074,-.336,1.317+zoff),(sign*.13,-.333,1.312+zoff),(sign*.177,-.314,1.330+zoff*.65),(sign*.205,-.284,1.352+zoff*.2)]
                tube(p,ridge,[.001,.0023,.0025,.002,.0007],shade(HAIR,1.16),segments=6,smooth=3,depth=.6)
        sphere(p,(0,-.277,1.376),(.056,.050,.036),SKIN)
        tube(p,[(-.040,-.254,1.264),(0,-.263,1.258),(.039,-.254,1.264)],[.002,.004,.002],srgb('#7C4932'),segments=8,smooth=4,depth=.7)
        # Soft peaked cap, leaning slightly to the wearer's right.
        existing=set(p.bm.verts)
        loft(p,[(1.579,.246,.206,0),(1.59,.255,.216,0),(1.65,.262,.225,.006)],srgb('#30352C'),segments=48)
        loft(p,[(1.642,.255,.221,.005),(1.671,.31,.248,.01),(1.733,.35,.266,.018),(1.787,.313,.240,.026),(1.806,.22,.17,.026),(1.810,.045,.035,.026)],UNIFORM,segments=48)
        sphere(p,(0,-.179,1.585),(.272,.158,.020),srgb('#38392D'))
        tube(p,[(-.213,-.148,1.628),(-.14,-.190,1.632),(0,-.225,1.633),(.14,-.19,1.632),(.213,-.148,1.628)],[.009]*5,srgb('#69614B'),segments=8,smooth=4,depth=.65)
        for sign in (-1,1):
            p.box((sign*.165,-.183,1.633),(.075,.015,.013),C['brass'],rot=(0,0,sign*.30),bevel=.004,seg=3)
        sphere(p,(0,-.251,1.717),(.044,.014,.051),C['brass'])
        star=[]
        for j in range(10):
            angle=math.pi/2+j*math.pi/5;radius=.028 if j%2==0 else .0135
            star.append((radius*math.cos(angle),-.267,1.718+radius*math.sin(angle)))
        panel(p,star,.008,srgb('#B33124'),bevel=.001)
        pivot=Vector((0,0,1.60));rot=Euler((-.025,-.10,-.02)).to_matrix()
        for vertex in p.bm.verts:
            if vertex not in existing:vertex.co=pivot+rot@(vertex.co-pivot)
        for side,sign in (('L',1),('R',-1)):
            bone('UpperLeg_'+side,(sign*.225,0,.40),(sign*.225,0,.235),'Hips')
            p=bone('LowerLeg_'+side,(sign*.225,0,.235),(sign*.225,0,.10),'UpperLeg_'+side)
            p.cyl((sign*.225,0,.153),.101,.14,srgb('#20251F'),segs=28,bevel=.014,seg=3)
            p.cyl((sign*.225,0,.224),.119,.045,srgb('#30362C'),segs=28,bevel=.007,seg=3)
            p=bone('Foot_'+side,(sign*.225,0,.10),(sign*.225,-.16,.07),'LowerLeg_'+side)
            sphere(p,(sign*.225,-.052,.083),(.129,.178,.083),srgb('#20251F'))
            p.box((sign*.225,-.05,.024),(.262,.337,.047),srgb('#1B201A'),bevel=.023,seg=4)
    for side,sign in (('L',1),('R',-1)):
        h=(sign*.335,0,1.065);e=(sign*.452,0,.845);w=(sign*.50,-.018,.657)
        p=bone('UpperArm_'+side,h,e,'Root' if fp else 'Spine')
        tube(p,[(sign*.32,0,1.07),(sign*.366,0,1.055),(sign*.412,0,.979),(sign*.45,0,.87),(sign*.474,-.008,.765),(sign*.50,-.018,.672)],[.075,.136,.14,.13,.12,.108],UNIFORM,segments=24,smooth=5)
        p=bone('Forearm_'+side,e,w,'UpperArm_'+side)
        tube(p,[(sign*.493,-.016,.704),(sign*.505,-.019,.663)],[.123,.12],shade(UNIFORM,.94),segments=32,smooth=2)
        p=bone('Hand_'+side,w,(sign*.518,-.02,.535),'Forearm_'+side)
        sphere(p,(sign*.513,-.025,.591),(.085,.069,.090),SKIN)
        for j in range(4):
            x=sign*(.468+j*.030)
            p=bone('Finger%d_%s'%(j+1,side),(x,-.030,.571),(x,-.044,.510),'Hand_'+side)
            sphere(p,(x,-.029,.547),(.021,.032,.041-(.005 if j in (0,3) else 0)),SKIN)
        p=bone('Thumb_'+side,(sign*.45,-.065,.615),(sign*.432,-.087,.562),'Hand_'+side)
        sphere(p,(sign*.448,-.069,.590),(.032,.030,.046),SKIN)
    a.build()
    # Fuse the trouser legs into the hips before binding, with no visible
    # overlapping thigh balls. Sleeves are now continuous ring surfaces.
    for name,p in parts.items():
        if name != 'Hips': continue
        bpy.ops.object.select_all(action='DESELECT')
        p.ob.select_set(True);bpy.context.view_layer.objects.active=p.ob
        remesh=p.ob.modifiers.new('ContinuousTrousers','REMESH')
        remesh.mode='VOXEL';remesh.voxel_size=.018;remesh.use_smooth_shade=True
        bpy.ops.object.modifier_apply(modifier=remesh.name)
        relax=p.ob.modifiers.new('TrouserRelax','SMOOTH');relax.factor=.7;relax.iterations=7
        bpy.ops.object.modifier_apply(modifier=relax.name)
        me=p.ob.data
        col=me.color_attributes.get('Col') or me.color_attributes.new(name='Col',type='FLOAT_COLOR',domain='CORNER')
        linear_uniform=tuple(((v+.055)/1.055)**2.4 if v>.04045 else v/12.92 for v in TROUSERS[:3])+(1,)
        for datum in col.data:
            if col.data_type == 'BYTE_COLOR': datum.color_srgb=TROUSERS
            else: datum.color=linear_uniform
        for poly in me.polygons:poly.use_smooth=True
        me.color_attributes.active_color=col;me.color_attributes.render_color_index=me.color_attributes.find('Col')
    rigdata=bpy.data.armatures.new(a.name+'_Skeleton')
    rig=bpy.data.objects.new(a.name+'_Rig',rigdata); bpy.context.collection.objects.link(rig)
    rig.parent=a.root
    bpy.context.view_layer.objects.active=rig; rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    for name,(h,t,parent) in bones.items():
        b=rigdata.edit_bones.new(name); b.head=h; b.tail=t
        if parent: b.parent=rigdata.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    for name,p in parts.items():
        if not p.ob.data.vertices: bpy.data.objects.remove(p.ob,do_unlink=True); continue
        g=p.ob.vertex_groups.new(name=prefix+name)
        g.add(list(range(len(p.ob.data.vertices))),1.0,'REPLACE')
        # Blend sleeve ends through the shared elbow rather than pinning two
        # overlapping volumes to opposite bones. Hand/cuff/fingers stay rigid.
        if name.startswith(('UpperArm_', 'Forearm_')):
            side=name[-1]
            upper=name.startswith('UpperArm')
            other=('Forearm_' if upper else 'UpperArm_')+side
            partner=p.ob.vertex_groups.new(name=prefix+other)
            elbow=Vector(bones[prefix+'Forearm_'+side][0])
            for v in p.ob.data.vertices:
                distance=(v.co-elbow).length
                if upper:
                    blend=max(0,min(1,(.91-v.co.z)/.14))
                else:
                    blend=0
                if blend > 0:
                    g.add([v.index],1-blend,'REPLACE')
                    partner.add([v.index],blend,'REPLACE')
        if name == 'Hips':
            legs={sign:p.ob.vertex_groups.new(name='UpperLeg_'+side) for side,sign in (('L',1),('R',-1))}
            for vertex in p.ob.data.vertices:
                blend=max(0,min(1,(.47-vertex.co.z)/.18))
                if blend>0:
                    g.add([vertex.index],1-blend,'REPLACE')
                    legs[1 if vertex.co.x>=0 else -1].add([vertex.index],blend,'REPLACE')
        mod=p.ob.modifiers.new('Skin','ARMATURE'); mod.object=rig
    # A single skinned renderer per character; disconnected components retain groups.
    bpy.ops.object.select_all(action='DESELECT')
    meshes=[ob for ob in a.root.children_recursive if ob.type=='MESH']
    for ob in meshes: ob.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0]
    bpy.ops.object.join()
    bpy.context.object.name=a.name+'_Skin'
    # BMesh byte-colour storage decodes sRGB on conversion to FLOAT_COLOR.
    # The shared Unity shader expects raw sRGB codes, so explicitly re-encode
    # this character instead of letting body and remeshed sleeves disagree.
    colors=bpy.context.object.data.color_attributes['Col']
    if colors.data_type != 'FLOAT_COLOR':
        values=[tuple(d.color) for d in colors.data]
        bpy.context.object.data.color_attributes.remove(colors)
        colors=bpy.context.object.data.color_attributes.new(name='Col',type='FLOAT_COLOR',domain='CORNER')
    else: values=[tuple(d.color) for d in colors.data]
    for datum,c in zip(colors.data,values):
        datum.color=tuple(1.055*(max(0,v)**(1/2.4))-.055 if v>.0031308 else 12.92*v for v in c[:3])+(c[3],)
        r,g,b=datum.color[:3]
        if r>.65 and b>.40: code=.25
        elif r>.60 and g>.40 and b<.42: code=.5
        elif g>r*1.08 and g>b*1.20 and r>.16: code=0
        elif max(r,g,b)<.20: code=.75 if g-r>.008 else 1
        elif r>.4 and g<.3: code=.75
        elif g>r*1.03 and r>.16: code=0
        else: code=1
        datum.color=(r,g,b,code)
    bpy.context.object.data.color_attributes.active_color=colors
    bpy.context.object.data.materials.clear()
    bpy.context.object.data.materials.append(crew_material())
    for polygon in bpy.context.object.data.polygons:polygon.material_index=0
    # Export an editable UV layout as well as the vertex-colour material.
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.008)
    bpy.ops.object.mode_set(mode='OBJECT')
    rig.animation_data_create()
    clips=['Idle','Walk','ValveTurn','Faint'] if not fp else ['HandsIdle','HandsGrip']
    for clip in clips:
        action=bpy.data.actions.new(a.name+'_'+clip); rig.animation_data.action=action
        for frame in (1,9,17,25,33):
            phase=(frame-1)/32*math.tau
            for pb in rig.pose.bones:
                pb.rotation_mode='XYZ'; pb.rotation_euler=(0,0,0); pb.location=(0,0,0)
                n=pb.name.removeprefix(prefix); sign=1 if n.endswith('_L') else -1
                if clip=='Idle' and n=='Spine': pb.rotation_euler.x=.025*math.sin(phase)
                if clip=='Walk':
                    if n.startswith('UpperLeg'): pb.rotation_euler.x=sign*.32*math.sin(phase)
                    if n.startswith('LowerLeg'): pb.rotation_euler.x=max(0,-sign*math.sin(phase))*.40
                    if n.startswith('UpperArm'): pb.rotation_euler.x=-sign*.22*math.sin(phase)
                if clip in ('ValveTurn','HandsGrip'):
                    if n.startswith('UpperArm'): pb.rotation_euler.x=-.65; pb.rotation_euler.y=-sign*.25
                    if n.startswith('Forearm'): pb.rotation_euler.x=-.65+.09*math.sin(phase)
                    if n.startswith('Hand'): pb.rotation_euler.z=sign*.16*math.sin(phase)
                    if n.startswith('Finger'): pb.rotation_euler.x=-.90
                    if n.startswith('Thumb'): pb.rotation_euler.x=-.45; pb.rotation_euler.z=sign*.25
                    if n=='Spine': pb.rotation_euler.z=.08*math.sin(phase); pb.rotation_euler.x=.04
                if clip=='Faint':
                    progress=(frame-1)/32
                    if n=='Root': pb.rotation_euler.x=progress*math.pi/2; pb.location.z=.18*progress
                    if n.startswith('UpperArm'): pb.rotation_euler.y=sign*.55*progress
                pb.keyframe_insert('rotation_euler',frame=frame,group=pb.name)
                pb.keyframe_insert('location',frame=frame,group=pb.name)
        track=rig.animation_data.nla_tracks.new(); track.name=clip
        strip=track.strips.new(clip,1,action); strip.name=clip
    rig.animation_data.action=None
    for tr in rig.animation_data.nla_tracks: tr.mute=True
    for pb in rig.pose.bones: pb.rotation_euler=(0,0,0); pb.location=(0,0,0)
    return a,rig,clips

if __name__=='__main__':
    out,preview=cli_args(); reset_scene(); bpy.context.scene.render.fps=24
    items=[crew(),crew(True)]; report={'blender':bpy.app.version_string,'issues':[112,130],'assets':[]}
    for a,rig,clips in items:
        meshes=[o for o in a.root.children_recursive if o.type=='MESH']
        for o in meshes:
            assert 'Col' in o.data.color_attributes
            assert all(1<=len(v.groups)<=2 and abs(sum(g.weight for g in v.groups)-1)<1e-6 for v in o.data.vertices)
            assert any(len(v.groups)==2 for v in o.data.vertices)
            assert all(g.name in rig.data.bones for g in o.vertex_groups)
            assert any(m.type=='ARMATURE' and m.object==rig for m in o.modifiers)
        bpy.ops.object.select_all(action='DESELECT')
        for o in [a.root]+list(a.root.children_recursive): o.select_set(True)
        bpy.context.view_layer.objects.active=rig
        for tr in rig.animation_data.nla_tracks: tr.mute=False
        bpy.ops.export_scene.fbx(filepath=os.path.join(out,a.name+'.fbx'),use_selection=True,
            object_types={'MESH','EMPTY','ARMATURE'},axis_forward='-Z',axis_up='Y',
            apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=False,
            add_leaf_bones=False,colors_type='LINEAR',bake_anim=True,bake_anim_use_all_actions=False,
            bake_anim_use_nla_strips=True,bake_anim_simplify_factor=0,mesh_smooth_type='FACE')
        for tr in rig.animation_data.nla_tracks: tr.mute=True
        for pb in rig.pose.bones: pb.rotation_euler=(0,0,0); pb.location=(0,0,0)
        for o in meshes:o.data.calc_loop_triangles()
        tris=sum(len(o.data.loop_triangles) for o in meshes)
        report['assets'].append({'name':a.name,'bones':len(rig.data.bones),'mesh_parts':len(meshes),'triangles':tris,'clips':clips,'weighted_vertices':sum(len(o.data.vertices) for o in meshes),'blended_vertices':sum(sum(len(v.groups)==2 for v in o.data.vertices) for o in meshes),'weighting':'normalized two-bone elbows and trouser-leg transitions','material':'CrewSurface: raw sRGB RGB, surface family in alpha','reference':'moodboard/v2_references/11_equipage_personnages.png','animation_status':'prototype; Generic rig; game IK and Animator integration pending'})
        print('EXPORT',a.name,len(rig.data.bones),'bones')
    bpy.context.scene.frame_set(1); bpy.context.view_layer.update()
    # Open the editable source on the complete sailor, with FP arms available
    # separately instead of superimposed over the body in the viewport.
    for ob in [items[1][0].root]+list(items[1][0].root.children_recursive):
        ob.hide_render=True;ob.hide_set(True)
    bpy.ops.object.select_all(action='DESELECT')
    skin=next(o for o in items[0][0].root.children_recursive if o.type=='MESH')
    skin.select_set(True);bpy.context.view_layer.objects.active=skin
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.shading.color_type='VERTEX'
                area.spaces.active.region_3d.view_location=(0,0,.92)
                area.spaces.active.region_3d.view_distance=3.2
                area.spaces.active.region_3d.view_rotation=Vector((.36,-1,.08)).to_track_quat('Z','Y')
    source=os.path.join(os.path.dirname(__file__),'sources','Crew.blend'); bpy.ops.wm.save_as_mainfile(filepath=source)
    with open(os.path.join(out,'crew_manifest.json'),'w') as f: json.dump(report,f,indent=2)
    if preview:
        for ob in [items[1][0].root]+list(items[1][0].root.children_recursive):ob.hide_set(False)
        os.makedirs(preview,exist_ok=True); render_each([a for a,_,_ in items],preview)
        a,rig,_=items[0]
        for ob in [items[1][0].root]+list(items[1][0].root.children_recursive): ob.hide_render=True
        tr=next(t for t in rig.animation_data.nla_tracks if t.name=='ValveTurn'); tr.mute=False
        bpy.context.scene.frame_set(9)
        render_each([a],preview,views=(('valve_pose',(.8,-1,.4)),))
        tr.mute=True
        tr=next(t for t in rig.animation_data.nla_tracks if t.name=='Faint'); tr.mute=False
        bpy.context.scene.frame_set(33)
        render_each([a],preview,views=(('faint_pose',(.8,-1,.8)),))
        tr.mute=True
        fp,fp_rig,_=items[1]
        for ob in [a.root]+list(a.root.children_recursive): ob.hide_render=True
        for ob in [fp.root]+list(fp.root.children_recursive): ob.hide_render=False
        next(t for t in fp_rig.animation_data.nla_tracks if t.name=='HandsGrip').mute=False
        bpy.context.scene.frame_set(9)
        render_each([fp],preview,views=(('grip_pose',(.8,-1,.4)),))
