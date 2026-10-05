"""E11-24: fifteen enamel/brass plates, lettering baked into vertex-colour meshes."""
import os, sys, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lib import *

LABELS = [
    ('Plate_Torpedoes', '01  TORPILLES', .66, .15),
    ('Plate_Central', '02  POSTE CENTRAL', .78, .15),
    ('Plate_Radio', '03  RADIO / SONAR', .78, .15),
    ('Plate_Reactor', '04  REACTEUR', .66, .15),
    ('Plate_Machines', '05  MACHINES', .66, .15),
    ('Plate_Living', '06  SAS ET VIE', .66, .15),
    ('Plate_Primary', 'CIRCUIT PRIMAIRE', .65, .12),
    ('Plate_Bilge', 'POMPE DE CALE', .58, .12),
    ('Plate_Electric', 'TABLEAU ELECTRIQUE', .76, .12),
    ('Plate_Interphone', 'INTERPHONE', .36, .10),
    ('Plate_Manual', 'MANUEL OK-114', .52, .12),
    ('Plate_Pneumatic', 'COURRIER PNEUMATIQUE', .78, .12),
    ('Plate_Battery', 'BATTERIES', .38, .12),
    ('Plate_Scram', 'SCRAM', .28, .12),
    ('Plate_Registry', 'MOLOSSE / KR-114', .68, .20),
]

def lettering(part, label, width, height):
    # Convert native font curves to triangles and integrate into the same part.
    curve = bpy.data.curves.new('PlateLettering', 'FONT')
    curve.body = label; curve.align_x = 'CENTER'; curve.align_y = 'CENTER'
    curve.size = 1; curve.extrude = .003; curve.resolution_u = 3
    ob = bpy.data.objects.new('TemporaryLetters', curve)
    bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.update()
    evaluated = ob.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = bpy.data.meshes.new_from_object(evaluated)
    scale = min((width-.07)/max(ob.dimensions.x, .001), (height-.06)/max(ob.dimensions.y, .001))
    before = part._snapshot()
    part.bm.from_mesh(mesh)
    verts = [v for v in part.bm.verts if v not in before[1]]
    for v in verts:
        x, y, z = v.co
        v.co = (x*scale, -.016-z*scale, y*scale)
    part._paint([f for f in part.bm.faces if f not in before[0]], C['red'] if label=='SCRAM' else C['black'], jitter=0)
    bpy.data.objects.remove(ob, do_unlink=True)
    bpy.data.meshes.remove(mesh); bpy.data.curves.remove(curve)

def plate(name, label, width, height):
    a = Asset(name); p = a.part('Body')
    p.box((0, 0, 0), (width, .022, height), C['brass_d'], bevel=.005)
    p.box((0, -.012, 0), (width-.014, .003, height-.014), C['cream'], bevel=.001)
    for x in (-width/2+.015, width/2-.015):
        for z in (-height/2+.015, height/2-.015):
            p.cyl((x, -.017, z), .004, .004, C['steel_d'], axis='Y', segs=8)
    lettering(p, label, width, height)
    a.mount('Mount_Wall', (0, .011, 0))
    return a

def build_all():
    return [plate(*row) for row in LABELS]

if __name__ == '__main__':
    out, preview = cli_args(); reset_scene(); assets = build_all()
    os.makedirs(out, exist_ok=True); report=[]
    for a, row in zip(assets, LABELS):
        a.build(); a.export(out)
        report.append({'name':a.name, 'label':row[1], 'triangles':a.stats(), 'width':row[2], 'height':row[3]})
        print('EXPORT', a.name, a.stats())
    with open(os.path.join(out, 'signage_manifest.json'), 'w', encoding='utf-8') as f:
        json.dump({'issue':132, 'lettering':'baked mesh; ASCII prototype labels', 'assets':report}, f, indent=2)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(__file__), 'sources', 'Signage.blend'))
    if preview:
        os.makedirs(preview, exist_ok=True)
        render_sheet(assets, os.path.join(preview, 'sheet.png'), cols=3, cell=.82, resolution=(1600,1200), cam_dir=(0,-1,.04), layout='XZ')
        render_each([assets[1]], preview, views=(('front', (0,-1,0)),))
