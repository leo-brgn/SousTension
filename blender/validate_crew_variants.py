import bpy,os,sys,json,math,hashlib
root=sys.argv[sys.argv.index('--')+1]
manifest=json.load(open(os.path.join(root,'crew_variants_manifest.json')))
results=[];fingerprints={}
for entry in manifest['assets']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(root,entry['name']+'.fbx'))
    meshes=[o for o in bpy.data.objects if o.type=='MESH']
    assert meshes
    signature=[]
    for ob in meshes:
        me=ob.data;assert 'Col' in me.color_attributes and me.uv_layers
        me.calc_loop_triangles();assert all(t.area>1e-12 for t in me.loop_triangles)
        assert all(math.isfinite(c) for v in me.vertices for c in v.co)
        signature+= [tuple(round(c,6) for c in v.co) for v in me.vertices]
    if entry['category']=='rigged_character':
        rigs=[o for o in bpy.data.objects if o.type=='ARMATURE'];assert len(rigs)==1 and len(rigs[0].data.bones)==27
        clips=[a for a in bpy.data.actions if a.name.startswith(entry['name']+'_Rig|')]
        assert len(clips)==4,(entry['name'],len(clips))
        for ob in meshes:
            assert any(m.type=='ARMATURE' and m.object==rigs[0] for m in ob.modifiers)
            assert all(abs(sum(g.weight for g in v.groups)-1)<1e-5 for v in ob.data.vertices)
        for action in clips:
            rigs[0].animation_data.action=action
            for frame in (1,9,17,25,33):
                bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
                deps=bpy.context.evaluated_depsgraph_get()
                assert all(all(math.isfinite(c) and abs(c)<5 for c in v.co) for ob in meshes for v in ob.evaluated_get(deps).data.vertices)
    else:
        # Geometry uniqueness checked inside requested modular categories.
        digest=hashlib.sha256(repr(signature).encode()).hexdigest();cat=entry['category']
        assert (cat,digest) not in fingerprints,(entry['name'],fingerprints.get((cat,digest)))
        fingerprints[cat,digest]=entry['name']
    results.append({'name':entry['name'],'status':'Blender FBX roundtrip passed','triangles':sum(len(o.data.loop_triangles) for o in meshes)})
json.dump({'count':len(results),'checks':'finite geometry, positive triangle area, UV0, vertex colors, category geometric uniqueness, rig weights/bones/4clips/5sampled poses','assets':results},open(os.path.join(root,'crew_variants_validation.json'),'w'),indent=2)
print('CREW_VARIANTS_PASS',len(results))
