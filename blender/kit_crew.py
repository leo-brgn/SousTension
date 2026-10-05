"""P1 crew: articulated sailor and independent first-person hands, Blender 5.2.
Two-bone sleeve transitions soften elbows while preserving the thick silhouette.
Animation clips are prototypes for Generic rigs, not production locomotion/IK.
"""
import os, sys, json, math
sys.path.insert(0, os.path.dirname(__file__))
from lib import *

SKIN = srgb('#D6A780')

def crew(fp=False):
    a = Asset('FirstPersonArms' if fp else 'SailorBase')
    prefix = 'FP_' if fp else ''
    bones = {}
    parts = {}
    def bone(name, head, tail, parent=None):
        bones[prefix+name] = (head, tail, prefix+parent if parent else None)
        p = a.part(name); parts[name] = p
        return p
    def sphere(p, c, r, color): p.sphere(c, r, color, u=16, v=10)
    bone('Root', (0,0,0), (0,0,.15))
    if not fp:
        p=bone('Hips',(0,0,.48),(0,0,.83),'Root')
        sphere(p,(0,0,.66),(.36,.24,.29),C['bottle_d'])
        p=bone('Spine',(0,0,.83),(0,0,1.12),'Hips')
        sphere(p,(0,0,1.02),(.41,.27,.39),C['bottle'])
        p.box((0,-.255,1.09),(.15,.025,.16),C['bottle_d'],bevel=.018)
        for z in (.83,.96,1.10,1.23): sphere(p,(0,-.277,z),(.021,.012,.021),C['brass'])
        for s in (-1,1): p.box((s*.105,-.235,1.31),(.15,.035,.115),C['bottle_d'],rot=(0,s*.30,0),bevel=.015)
        p=bone('Neck',(0,0,1.12),(0,0,1.35),'Spine')
        sphere(p,(0,0,1.29),(.14,.13,.12),SKIN)
        p=bone('Head',(0,0,1.35),(0,0,1.70),'Neck')
        sphere(p,(0,0,1.52),(.265,.225,.305),SKIN)
        for s in (-1,1):
            sphere(p,(s*.26,0,1.49),(.052,.063,.078),SKIN)
            sphere(p,(s*.082,-.205,1.57),(.027,.015,.035),C['black'])
            p.box((s*.084,-.212,1.636),(.090,.020,.022),C['bakelite'],rot=(0,s*.12,0),bevel=.008)
            sphere(p,(s*.085,-.244,1.448),(.101,.038,.045),C['bakelite'])
        sphere(p,(0,-.240,1.50),(.066,.066,.050),SKIN)
        p.cyl((0,0,1.76),.275,.095,C['bottle_d'],segs=24,bevel=.018)
        sphere(p,(0,0,1.825),(.315,.255,.100),C['bottle'])
        p.box((0,-.24,1.748),(.36,.18,.024),C['bakelite'],bevel=.025)
        sphere(p,(0,-.25,1.815),(.038,.013,.045),C['brass'])
        for side,s in (('L',1),('R',-1)):
            p=bone('UpperLeg_'+side,(s*.18,0,.68),(s*.18,0,.37),'Hips')
            sphere(p,(s*.18,0,.48),(.15,.165,.25),C['bottle_d'])
            p=bone('LowerLeg_'+side,(s*.18,0,.37),(s*.18,0,.13),'UpperLeg_'+side)
            p.cyl((s*.18,0,.25),.115,.28,C['black'],bevel=.018)
            p=bone('Foot_'+side,(s*.18,0,.13),(s*.18,-.18,.09),'LowerLeg_'+side)
            p.box((s*.18,-.075,.095),(.25,.37,.18),C['bakelite'],bevel=.065)
            p.box((s*.18,-.075,.023),(.26,.38,.045),C['black'],bevel=.012)
    for side,s in (('L',1),('R',-1)):
        # A-pose with broad sleeves and separate mitten fingers.
        h=(s*.33,0,1.28); e=(s*.54,0,1.05); w=(s*.65,-.025,.83)
        p=bone('UpperArm_'+side,h,e,'Root' if fp else 'Spine')
        sphere(p,(s*.43,0,1.17),(.16,.145,.21),C['bottle'])
        p=bone('Forearm_'+side,e,w,'UpperArm_'+side)
        sphere(p,(s*.595,-.012,.955),(.12,.118,.18),C['bottle'])
        p.cyl((s*.64,-.025,.86),.117,.065,C['bottle_d'],bevel=.008)
        p=bone('Hand_'+side,w,(s*.68,-.025,.72),'Forearm_'+side)
        sphere(p,(s*.67,-.025,.775),(.093,.065,.10),SKIN)
        for j in range(4):
            x=s*(.605+j*.040)
            p=bone('Finger%d_%s'%(j+1,side),(x,-.04,.745),(x,-.06,.66),'Hand_'+side)
            sphere(p,(x,-.052,.704),(.024,.029,.062),SKIN)
        p=bone('Thumb_'+side,(s*.595,-.06,.80),(s*.57,-.10,.735),'Hand_'+side)
        sphere(p,(s*.585,-.09,.768),(.036,.038,.058),SKIN)
    a.build()
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
                if distance < .18:
                    blend=.5*(1-distance/.18)
                    g.add([v.index],1-blend,'REPLACE')
                    partner.add([v.index],blend,'REPLACE')
        mod=p.ob.modifiers.new('Skin','ARMATURE'); mod.object=rig
    # A single skinned renderer per character; disconnected components retain groups.
    bpy.ops.object.select_all(action='DESELECT')
    meshes=[ob for ob in a.root.children_recursive if ob.type=='MESH']
    for ob in meshes: ob.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0]
    bpy.ops.object.join()
    bpy.context.object.name=a.name+'_Skin'
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
        tris=sum(len(o.data.polygons) for o in meshes)
        report['assets'].append({'name':a.name,'bones':len(rig.data.bones),'mesh_parts':len(meshes),'clips':clips,'weighted_vertices':sum(len(o.data.vertices) for o in meshes),'blended_vertices':sum(sum(len(v.groups)==2 for v in o.data.vertices) for o in meshes),'weighting':'normalized two-bone elbow transitions; rigid hands/fingers/body components','animation_status':'prototype; Generic rig; game IK and Animator integration pending'})
        print('EXPORT',a.name,len(rig.data.bones),'bones')
    bpy.context.scene.frame_set(1); bpy.context.view_layer.update()
    source=os.path.join(os.path.dirname(__file__),'sources','Crew.blend'); bpy.ops.wm.save_as_mainfile(filepath=source)
    with open(os.path.join(out,'crew_manifest.json'),'w') as f: json.dump(report,f,indent=2)
    if preview:
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
