"""Standalone FBX roundtrip validation: bones, weighted meshes and animation clips."""
import bpy, os, json, sys
root=sys.argv[sys.argv.index('--')+1]
results=[]
for name, expected in [('SailorBase',4),('FirstPersonArms',2)]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(root,name+'.fbx'))
    rigs=[o for o in bpy.data.objects if o.type=='ARMATURE']
    meshes=[o for o in bpy.data.objects if o.type=='MESH']
    assert len(rigs)==1
    rig_actions=[a for a in bpy.data.actions if a.name.startswith(name+'_Rig|')]
    assert len(rig_actions)==expected,(name,len(rig_actions))
    assert all(any(m.type=='ARMATURE' and m.object==rigs[0] for m in o.modifiers) for o in meshes)
    assert all(all(len(v.groups)>0 for v in o.data.vertices) for o in meshes)
    assert all(all(1<=len(v.groups)<=2 and abs(sum(g.weight for g in v.groups)-1)<1e-5 for v in o.data.vertices) for o in meshes)
    blended=sum(sum(len(v.groups)==2 for v in o.data.vertices) for o in meshes)
    assert blended>0
    assert all('Col' in o.data.color_attributes for o in meshes)
    for action in rig_actions:
        rigs[0].animation_data.action=action
        for frame in (1,9,17,25,33):
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
            assert all(all(abs(c)<100 for c in pb.matrix.translation) for pb in rigs[0].pose.bones)
            deps=bpy.context.evaluated_depsgraph_get()
            for mesh in meshes:
                evaluated=mesh.evaluated_get(deps)
                assert all(all(abs(c)<5 for c in v.co) for v in evaluated.data.vertices)
    results.append({'asset':name,'bones':len(rigs[0].data.bones),'skinned_meshes':len(meshes),'blended_elbow_vertices':blended,'sampled_frames_per_clip':5,'clips':[a.name for a in rig_actions],'validation':'FBX roundtrip normalized weights and sampled deformations passed; Unity runtime pending'})
with open(os.path.join(root,'crew_validation.json'),'w') as f: json.dump(results,f,indent=2)
print('CREW FBX ROUNDTRIP PASS',results)
