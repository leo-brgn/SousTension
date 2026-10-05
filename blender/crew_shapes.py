"""Clean, smooth character surfaces; no per-face colour noise."""
from lib import *

def clean_part(p):
    paint = p._paint
    p._paint = lambda faces, color, jitter=0: paint(faces, color, jitter=0)
    return p

def loft(p, rings, color, segments=40):
    """Closed elliptical rings (z, radius x, radius y, centre y)."""
    # Interpolate the silhouette rather than leaving visible horizontal corners.
    controls=[Vector(r) for r in rings]; rings=[]
    for k in range(len(controls)-1):
        a=controls[max(0,k-1)];b=controls[k];c=controls[k+1];d=controls[min(k+2,len(controls)-1)]
        for j in range(4):
            t=j/4
            rings.append(tuple(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t)))
    rings.append(tuple(controls[-1]))
    before=p._snapshot(); rows=[]
    for z,rx,ry,cy in rings:
        rows.append([p.bm.verts.new((rx*math.cos(i*math.tau/segments),cy+ry*math.sin(i*math.tau/segments),z)) for i in range(segments)])
    for a,b in zip(rows,rows[1:]):
        for i in range(segments):
            j=(i+1)%segments;p.bm.faces.new((a[i],a[j],b[j],b[i]))
    p.bm.faces.new(rows[0][::-1]);p.bm.faces.new(rows[-1])
    p._finish_prim(before,Matrix.Identity(4),color,0)

def tube(p, points, radii, color, segments=16, smooth=1, depth=1):
    if smooth>1:
        controls=[Vector((*pt,r)) for pt,r in zip(points,radii)]; samples=[]
        for k in range(len(controls)-1):
            a=controls[max(0,k-1)];b=controls[k];c=controls[k+1];d=controls[min(k+2,len(controls)-1)]
            for j in range(smooth):
                t=j/smooth
                samples.append(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t))
        samples.append(controls[-1]);points=[v[:3] for v in samples];radii=[max(.0005,v[3]) for v in samples]
    before=p._snapshot();rows=[]
    for k,pt in enumerate(points):
        tangent=Vector(points[min(k+1,len(points)-1)])-Vector(points[max(0,k-1)])
        tangent.normalize();u=tangent.cross(Vector((0,1,0))).normalized();v=tangent.cross(u).normalized()
        rows.append([p.bm.verts.new(Vector(pt)+radii[k]*(math.cos(i*math.tau/segments)*u+depth*math.sin(i*math.tau/segments)*v)) for i in range(segments)])
    for a,b in zip(rows,rows[1:]):
        for i in range(segments):
            j=(i+1)%segments;p.bm.faces.new((a[i],a[j],b[j],b[i]))
    p.bm.faces.new(rows[0][::-1]);p.bm.faces.new(rows[-1])
    p._finish_prim(before,Matrix.Identity(4),color,0)

def panel(p, points, thickness, color, bevel=.012):
    before=p._snapshot()
    front=[p.bm.verts.new((x,y-thickness/2,z)) for x,y,z in points]
    back=[p.bm.verts.new((x,y+thickness/2,z)) for x,y,z in points]
    p.bm.faces.new(front);p.bm.faces.new(back[::-1])
    for i in range(len(points)):
        j=(i+1)%len(points);p.bm.faces.new((front[i],back[i],back[j],front[j]))
    p._finish_prim(before,Matrix.Identity(4),color,bevel,seg=3)

def crew_material():
    """Single material, with physical surface family encoded in vertex alpha.

    Codes: cloth=0, skin=.25, brass=.5, leather=.75, hair=1.
    Unity's dedicated crew material uses the same codes without extra submeshes.
    """
    mat=bpy.data.materials.get('CrewSurface')
    if mat:return mat
    mat=bpy.data.materials.new('CrewSurface');mat.use_nodes=True
    nt=mat.node_tree;nt.nodes.clear()
    output=nt.nodes.new('ShaderNodeOutputMaterial');bs=nt.nodes.new('ShaderNodeBsdfPrincipled')
    attr=nt.nodes.new('ShaderNodeVertexColor');attr.layer_name='Col'
    decode=nt.nodes.new('ShaderNodeGamma');decode.name='CrewDecode';decode.inputs[1].default_value=2.2
    nt.links.new(attr.outputs['Color'],decode.inputs[0]);nt.links.new(decode.outputs[0],bs.inputs['Base Color'])
    masks={}
    for name,code in [('Cloth',0),('Skin',.25),('Brass',.5),('Leather',.75),('Hair',1)]:
        mask=nt.nodes.new('ShaderNodeMath');mask.operation='COMPARE';mask.label=name
        mask.inputs[1].default_value=code;mask.inputs[2].default_value=.05
        nt.links.new(attr.outputs['Alpha'],mask.inputs[0]);masks[name]=mask.outputs[0]
    total=None
    for name,roughness in [('Cloth',.86),('Skin',.52),('Brass',.30),('Leather',.31),('Hair',.57)]:
        mul=nt.nodes.new('ShaderNodeMath');mul.operation='MULTIPLY';mul.inputs[1].default_value=roughness
        nt.links.new(masks[name],mul.inputs[0])
        if total is None: total=mul.outputs[0]
        else:
            add=nt.nodes.new('ShaderNodeMath');add.operation='ADD';nt.links.new(total,add.inputs[0]);nt.links.new(mul.outputs[0],add.inputs[1]);total=add.outputs[0]
    nt.links.new(total,bs.inputs['Roughness'])
    metallic=nt.nodes.new('ShaderNodeMath');metallic.operation='MULTIPLY';metallic.inputs[1].default_value=.78
    nt.links.new(masks['Brass'],metallic.inputs[0]);nt.links.new(metallic.outputs[0],bs.inputs['Metallic'])
    noise=nt.nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=220;noise.inputs['Detail'].default_value=2
    bump=nt.nodes.new('ShaderNodeBump');bump.inputs['Distance'].default_value=.0012
    strength=nt.nodes.new('ShaderNodeMath');strength.operation='MULTIPLY';strength.inputs[1].default_value=.18
    nt.links.new(masks['Cloth'],strength.inputs[0]);nt.links.new(strength.outputs[0],bump.inputs['Strength'])
    nt.links.new(noise.outputs['Fac'],bump.inputs['Height']);nt.links.new(bump.outputs['Normal'],bs.inputs['Normal'])
    nt.links.new(bs.outputs[0],output.inputs['Surface'])
    return mat
