"""Studio review of the actual Crew.blend geometry, with explicit sRGB decoding."""
import bpy, os, math, sys, json
from mathutils import Vector
root=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
bpy.ops.wm.open_mainfile(filepath=os.path.join(root,'blender/sources/Crew.blend'))
sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.samples=32;sc.cycles.use_denoising=True
sc.render.resolution_x=1200;sc.render.resolution_y=1100;sc.render.resolution_percentage=100
sc.view_settings.view_transform='AgX'
sc.view_settings.look='AgX - Medium High Contrast'
for ob in list(bpy.data.objects):
    if ob.type in {'LIGHT','CAMERA'}:bpy.data.objects.remove(ob,do_unlink=True)
    elif ob.name.startswith('FirstPersonArms') or ob.name.startswith('FP_'):ob.hide_render=True;ob.hide_set(True)
sc.world=bpy.data.worlds.new('CrewStudio');sc.world.use_nodes=True
sc.world.node_tree.nodes['Background'].inputs[0].default_value=(.65,.69,.72,1)
sc.world.node_tree.nodes['Background'].inputs[1].default_value=.35
for mat in bpy.data.materials:
    if not mat.use_nodes:continue
    nt=mat.node_tree;bs=next((n for n in nt.nodes if n.type=='BSDF_PRINCIPLED'),None)
    att=next((n for n in nt.nodes if n.type=='VERTEX_COLOR'),None)
    if not bs or not att or 'CrewDecode' in nt.nodes:continue
    gamma=nt.nodes.new('ShaderNodeGamma');gamma.inputs[1].default_value=2.2
    nt.links.new(att.outputs['Color'],gamma.inputs[0]);nt.links.new(gamma.outputs[0],bs.inputs['Base Color'])
    bs.inputs['Roughness'].default_value=.72
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,.005));floor=bpy.context.object;floor.name='StudioFloor'
mat=bpy.data.materials.new('StudioCream');mat.diffuse_color=(.47,.46,.41,1);mat.use_nodes=True
mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.47,.46,.41,1)
mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.9;floor.data.materials.append(mat)
for name,loc,power,size in [('Key',(-3,-4,5),650,3),('Fill',(3,-2,3),200,3),('Rim',(0,3,4),500,3)]:
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size
    ob=bpy.data.objects.new(name,data);sc.collection.objects.link(ob);ob.location=loc
    ob.rotation_euler=(Vector((0,0,1))-ob.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('ReviewCamera');data.type='ORTHO';cam=bpy.data.objects.new('ReviewCamera',data);sc.collection.objects.link(cam);sc.camera=cam
out=os.path.join(root,'scratch_out/preview/CrewRework');os.makedirs(out,exist_ok=True)
for name,direction,target,scale in [('studio_front',(0,-1,.025),(0,0,.92),2.18),('studio_three_quarter',(.36,-1,.08),(0,0,.92),2.18),('studio_profile',(1,0,.05),(0,0,.92),2.18),('studio_back',(.3,1,.06),(0,0,.92),2.18),('studio_face',(.12,-1,.02),(0,-.04,1.40),1.0)]:
    centre=Vector(target);cam.location=centre+Vector(direction).normalized()*8
    cam.rotation_euler=(centre-cam.location).to_track_quat('-Z','Y').to_euler();data.ortho_scale=scale
    sc.render.filepath=os.path.join(out,'SailorBase_'+name+'.png');bpy.ops.render.render(write_still=True)
skin=bpy.data.objects['SailorBase_Skin'];skin.data.calc_loop_triangles()
with open(os.path.join(out,'geometry.json'),'w') as f:json.dump({'vertices':len(skin.data.vertices),'triangles':len(skin.data.loop_triangles),'heightM':max(v.co.z for v in skin.data.vertices)-min(v.co.z for v in skin.data.vertices),'reference':'moodboard/v2_references/11_equipage_personnages.png','renderer':'Blender Cycles actual geometry; sRGB vertex colours decoded'},f,indent=2)
centre=Vector((0,0,.92));cam.location=centre+Vector((.36,-1,.08)).normalized()*8
cam.rotation_euler=(centre-cam.location).to_track_quat('-Z','Y').to_euler();data.ortho_scale=2.18
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA'
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(root,'blender/sources/CrewPresentation.blend'))
print('CREW_REVIEW_PASS')
