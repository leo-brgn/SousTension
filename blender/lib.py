"""
Bibliothèque de modélisation procédurale pour Sous Pression (Blender 5.2, aucun asset téléchargé).

Principes
- Unités : 1 unité Blender = 1 m. Blender est en Z vers le haut ; l'export FBX convertit en Y vers le haut pour Unity.
- Style « réalisme pataud » : volumes épais, arêtes biseautées (arrondies), couleurs de sommets (pas de texture).
- Un ASSET = une racine (Empty) + des PARTIES (un mesh chacune, origine = pivot d'animation) + des POINTS DE MONTAGE
  (Empty nommés « Mount_* » : positions où Unity place les instruments/prefabs).
- Les couleurs sont données en sRGB (comme on les voit) et stockées/exportées telles quelles ; le shader Unity les convertit en linéaire.

Utilisation (dans un script de kit) :
    from lib import *
    a = Asset("GaugeRound_M")
    p = a.part("Body")                       # pivot au centre de l'asset
    p.cyl((0,0,0), 0.2, 0.05, C["brass"], axis="Y", bevel=0.01)
    a.mount("Mount_Face", (0, -0.03, 0))
"""
import bpy, bmesh, math, os, sys, random, zlib
from mathutils import Vector, Matrix, Euler

# ------------------------------------------------------------------ couleurs
def srgb(hexstr, a=1.0):
    """'#RRGGBB' -> RGBA en codes sRGB BRUTS (comme on voit la couleur). Ils sont stockés tels quels dans la couche « Col » et exportés
    tels quels (colors_type=LINEAR = aucune conversion) ; c'est le shader Unity qui convertit sRGB -> linéaire."""
    h = hexstr.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return (c[0], c[1], c[2], a)


# Palette du GDD : vert bouteille, crème administratif, rouille, UN rouge pour l'urgence. Laiton, bakélite, émail.
C = {
    "bottle":  srgb("#2F5A42"),   # vert bouteille (murs, tableaux)
    "bottle_d": srgb("#1F3D2D"),
    "cream":   srgb("#E6DCBC"),   # crème administratif (cadrans, papier)
    "cream_d": srgb("#C9BE9A"),
    "rust":    srgb("#8A4A2B"),
    "rust_d":  srgb("#5E301B"),
    "brass":   srgb("#C9A24A"),
    "brass_d": srgb("#8F7230"),
    "bakelite": srgb("#2A1A12"),
    "steel":   srgb("#7C8180"),
    "steel_d": srgb("#4E5352"),
    "steel_l": srgb("#A9AEAD"),
    "red":     srgb("#D9261C"),   # LE rouge : urgences réelles uniquement
    "red_d":   srgb("#9E1A13"),
    "black":   srgb("#15130F"),
    "white":   srgb("#F2EEE0"),
    "amber":   srgb("#E8A317"),
    "green_lamp": srgb("#4CAF50"),
    "glass":   srgb("#9FB8B5"),
    "lead":    srgb("#8E959A"),
    "grey_floor": srgb("#585E5B"),
}


def shade(col, k):
    """Éclaircit (k>1) ou assombrit (k<1) une couleur linéaire."""
    return (min(1, col[0] * k), min(1, col[1] * k), min(1, col[2] * k), col[3])


# ------------------------------------------------------------------ parties (un mesh, un pivot)
class Part:
    """Constructeur de maillage : primitives biseautées, couleur par face, origine locale = pivot."""

    def __init__(self, asset, name, parent=None):
        self.asset = asset
        self.name = name
        self.parent = parent                  # autre Part (hiérarchie : porte -> volant -> verrous), position relative au parent
        self.bm = bmesh.new()
        self.col = self.bm.loops.layers.color.new("Col")
        self.location = (0.0, 0.0, 0.0)       # position du pivot dans l'asset
        self.rotation = (0.0, 0.0, 0.0)       # rotation de repos (radians, XYZ)
        self.rng = random.Random(zlib.crc32((asset.name + "/" + name).encode("utf-8")))
        self.ob = None

    # --- utilitaires internes
    def _snapshot(self):
        return (set(self.bm.faces), set(self.bm.verts))

    def _paint(self, faces, color, jitter=0.03):
        for f in faces:
            c = color(f.calc_center_median(), f.normal) if callable(color) else color
            k = 1.0 + (self.rng.random() - 0.5) * 2 * jitter       # légère variation par face : casse l'aplat
            c = (min(1, c[0] * k), min(1, c[1] * k), min(1, c[2] * k), c[3])
            for l in f.loops:
                l[self.col] = c

    def _finish_prim(self, before, matrix, color, bevel, bevel_edges=None, seg=2):
        before_faces, before_verts = before
        verts = [v for v in self.bm.verts if v not in before_verts]
        bmesh.ops.transform(self.bm, matrix=matrix, verts=verts)
        if bevel and bevel > 0:
            new_faces = [f for f in self.bm.faces if f not in before_faces]
            edges = list({e for f in new_faces for e in f.edges}) if bevel_edges is None else bevel_edges(new_faces)
            if edges:
                bmesh.ops.bevel(self.bm, geom=edges, offset=bevel, segments=seg, profile=0.6, affect="EDGES")
        new_faces = [f for f in self.bm.faces if f not in before_faces]
        for f in new_faces:
            f.smooth = True
        self._paint(new_faces, color)
        return new_faces

    @staticmethod
    def _axis_matrix(axis):
        """Rotation qui envoie +Z vers l'axe demandé ('X','Y','Z' ou '-X', ...)."""
        table = {
            "Z": Matrix.Identity(4), "-Z": Matrix.Rotation(math.pi, 4, "X"),
            "X": Matrix.Rotation(math.pi / 2, 4, "Y"), "-X": Matrix.Rotation(-math.pi / 2, 4, "Y"),
            "Y": Matrix.Rotation(-math.pi / 2, 4, "X"), "-Y": Matrix.Rotation(math.pi / 2, 4, "X"),
        }
        return table[axis]

    # --- primitives
    def box(self, center, size, color, rot=(0, 0, 0), bevel=0.0, seg=2):
        """Boîte (sx, sy, sz) centrée en `center`, rotation Euler XYZ en radians."""
        before = self._snapshot()
        bmesh.ops.create_cube(self.bm, size=1.0)
        m = Matrix.Translation(center) @ Euler(rot, "XYZ").to_matrix().to_4x4() @ Matrix.Diagonal((size[0], size[1], size[2], 1.0))
        return self._finish_prim(before, m, color, bevel, seg=seg)

    def cyl(self, center, radius, height, color, axis="Z", segs=16, bevel=0.0, r2=None, seg=2, caps=True):
        """Cylindre ou cône tronqué (r2 = rayon du haut), `height` le long de `axis`."""
        before = self._snapshot()
        bmesh.ops.create_cone(self.bm, cap_ends=caps, cap_tris=False, segments=segs,
                              radius1=radius, radius2=radius if r2 is None else r2, depth=height)
        m = Matrix.Translation(center) @ self._axis_matrix(axis)
        cap_n = segs

        def edges_of_caps(new_faces):
            caps_faces = [f for f in new_faces if len(f.verts) == cap_n]
            return list({e for f in caps_faces for e in f.edges})

        return self._finish_prim(before, m, color, bevel, bevel_edges=edges_of_caps, seg=seg)

    def sphere(self, center, radii, color, u=14, v=10):
        before = self._snapshot()
        r = radii if isinstance(radii, (tuple, list)) else (radii, radii, radii)
        bmesh.ops.create_uvsphere(self.bm, u_segments=u, v_segments=v, radius=1.0)
        m = Matrix.Translation(center) @ Matrix.Diagonal((r[0], r[1], r[2], 1.0))
        return self._finish_prim(before, m, color, 0.0)

    def torus(self, center, major, minor, color, axis="Z", major_segs=24, minor_segs=8, arc=2 * math.pi):
        """Tore de grand rayon `major`, petit rayon `minor`, d'axe `axis`. `arc` < 2*pi : tube coudé (coude de tuyau), ouvert aux deux bouts."""
        before = self._snapshot()
        verts = []
        full = arc >= 2 * math.pi - 1e-6
        n_rings = major_segs if full else major_segs + 1
        for i in range(n_rings):
            a = arc * i / major_segs
            ring = []
            for j in range(minor_segs):
                b = 2 * math.pi * j / minor_segs
                r = major + minor * math.cos(b)
                ring.append(self.bm.verts.new((r * math.cos(a), r * math.sin(a), minor * math.sin(b))))
            verts.append(ring)
        for i in range(major_segs):
            for j in range(minor_segs):
                nxt = (i + 1) % major_segs if full else i + 1
                a, b = verts[i][j], verts[i][(j + 1) % minor_segs]
                c, d = verts[nxt][(j + 1) % minor_segs], verts[nxt][j]
                self.bm.faces.new((a, d, c, b))
        m = Matrix.Translation(center) @ self._axis_matrix(axis)
        return self._finish_prim(before, m, color, 0.0)

    def disc_ring(self, center, r_in, r_out, thick, color, axis="Z", segs=32, bevel=0.0):
        """Anneau plat (rondelle épaisse) : sert de lunette de cadran, de jante de volant."""
        before = self._snapshot()
        top, bot = [], []
        for i in range(segs):
            a = 2 * math.pi * i / segs
            ca, sa = math.cos(a), math.sin(a)
            top.append((self.bm.verts.new((r_in * ca, r_in * sa, thick / 2)), self.bm.verts.new((r_out * ca, r_out * sa, thick / 2))))
            bot.append((self.bm.verts.new((r_in * ca, r_in * sa, -thick / 2)), self.bm.verts.new((r_out * ca, r_out * sa, -thick / 2))))
        for i in range(segs):
            j = (i + 1) % segs
            self.bm.faces.new((top[i][1], top[j][1], top[j][0], top[i][0]))      # dessus
            self.bm.faces.new((bot[i][0], bot[j][0], bot[j][1], bot[i][1]))      # dessous
            self.bm.faces.new((top[i][1], bot[i][1], bot[j][1], top[j][1]))      # extérieur
            self.bm.faces.new((top[j][0], bot[j][0], bot[i][0], top[i][0]))      # intérieur
        m = Matrix.Translation(center) @ self._axis_matrix(axis)
        return self._finish_prim(before, m, color, bevel, seg=1)

    def prism_tri(self, a, b, c, thickness, color, axis="Z"):
        """Prisme triangulaire (a, b, c : points 2D dans le plan XY), épaisseur le long de `axis`."""
        before = self._snapshot()
        h = thickness / 2
        v0 = [self.bm.verts.new((p[0], p[1], h)) for p in (a, b, c)]
        v1 = [self.bm.verts.new((p[0], p[1], -h)) for p in (a, b, c)]
        self.bm.faces.new(v0)
        self.bm.faces.new(v1[::-1])
        for i in range(3):
            j = (i + 1) % 3
            self.bm.faces.new((v0[i], v1[i], v1[j], v0[j]))
        return self._finish_prim(before, self._axis_matrix(axis), color, 0.0)

    def prism_xz(self, pts, thickness, color, y=0.0):
        """Prisme à base polygonale CONVEXE définie par des points (x, z) ; épaisseur le long de Y, centrée sur `y`."""
        before = self._snapshot()
        h = thickness / 2
        front = [self.bm.verts.new((p[0], y - h, p[1])) for p in pts]
        back = [self.bm.verts.new((p[0], y + h, p[1])) for p in pts]
        self.bm.faces.new(front[::-1])
        self.bm.faces.new(back)
        n = len(pts)
        for i in range(n):
            j = (i + 1) % n
            self.bm.faces.new((front[i], front[j], back[j], back[i]))
        return self._finish_prim(before, Matrix.Identity(4), color, 0.0)

    # --- finalisation
    def build(self):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces[:])
        me = bpy.data.meshes.new(self.asset.name + "_" + self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        try:
            me.use_auto_smooth = True            # Blender 4.0 : arêtes dures au-delà de l'angle
            me.auto_smooth_angle = math.radians(40)
        except AttributeError:
            pass
        # La couche « Col » doit être l'attribut de couleur actif ET celui du rendu (sinon Workbench et l'export l'ignorent).
        if "Col" in me.color_attributes:
            me.color_attributes.active_color = me.color_attributes["Col"]
            me.color_attributes.render_color_index = me.color_attributes.find("Col")
        ob = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(ob)
        mat = _vertex_color_material()
        me.materials.append(mat)
        ob.location = self.location
        ob.rotation_euler = self.rotation
        self.ob = ob
        return ob


_MAT = None


def _vertex_color_material():
    global _MAT
    if _MAT is not None and _MAT.name in bpy.data.materials:
        return _MAT
    mat = bpy.data.materials.new("VertexColor")
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    att = nt.nodes.new("ShaderNodeVertexColor")
    att.layer_name = "Col"
    nt.links.new(att.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    bsdf.inputs["Roughness"].default_value = 0.7
    _MAT = mat
    return mat


# ------------------------------------------------------------------ assets
class Asset:
    """Racine (Empty) + parties (meshes) + points de montage (Empty « Mount_* »)."""

    registry = []

    def __init__(self, name):
        self.name = name
        self.parts = []
        self.mounts = []        # (nom, position, rotation)
        self.mount_objects = []
        self.root = None
        Asset.registry.append(self)

    def part(self, name, location=(0, 0, 0), rotation=(0, 0, 0), parent=None):
        p = Part(self, name, parent)
        p.location = location
        p.rotation = rotation
        self.parts.append(p)
        return p

    def mount(self, name, location, rotation=(0, 0, 0)):
        self.mounts.append((name, location, rotation))

    def build(self):
        root = bpy.data.objects.new(self.name, None)
        root.empty_display_type = "PLAIN_AXES"
        bpy.context.scene.collection.objects.link(root)
        self.root = root
        for p in self.parts:
            ob = p.build()
            ob.parent = p.parent.ob if p.parent is not None else root          # le parent est toujours déclaré avant l'enfant
        for name, loc, rot in self.mounts:
            e = bpy.data.objects.new(name, None)
            e.empty_display_type = "ARROWS"
            e.empty_display_size = 0.05
            e.location = loc
            e.rotation_euler = rot
            bpy.context.scene.collection.objects.link(e)
            e.parent = root
            self.mount_objects.append(e)
        return root

    def stats(self):
        tris = 0
        for p in self.parts:
            if p.ob:
                p.ob.data.calc_loop_triangles()
                tris += len(p.ob.data.loop_triangles)
        return tris

    def export(self, out_dir):
        # Blender names are scene-global. Repeated Body/Mount_* objects otherwise
        # acquire .001 suffixes, which break stable paths in the exported prefab.
        original_names = [(ob, ob.name) for ob in bpy.data.objects]
        for i, (ob, _) in enumerate(original_names):
            ob.name = "__ExportTemporary_%d" % i
        self.root.name = self.name
        for part in self.parts:
            part.ob.name = part.name
        for ob, (name, _, _) in zip(self.mount_objects, self.mounts):
            ob.name = name
        bpy.ops.object.select_all(action="DESELECT")
        self.root.select_set(True)
        for ch in self.root.children_recursive:
            ch.select_set(True)
        bpy.context.view_layer.objects.active = self.root
        path = os.path.join(out_dir, self.name + ".fbx")
        try:
            bpy.ops.export_scene.fbx(
                filepath=path, use_selection=True, object_types={"MESH", "EMPTY"},
                apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=not any(p.parent is not None for p in self.parts),
                axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", add_leaf_bones=False,
                colors_type="LINEAR", use_mesh_modifiers=True, use_custom_props=False)
        finally:
            for i, (ob, _) in enumerate(original_names):
                ob.name = "__RestoreTemporary_%d" % i
            for ob, name in original_names:
                ob.name = name
        return path


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    Asset.registry.clear()
    global _MAT
    _MAT = None


# ------------------------------------------------------------------ aperçu (planche de contact)
def render_sheet(assets, png_path, cols=4, cell=1.4, resolution=(1600, 1000), cam_dir=(0.9, -1.0, 0.7), layout="XY"):
    """Dispose les assets sur une grille, rendu Workbench (couleurs de sommets, aucun GPU requis).
    layout="XZ" : lignes en hauteur (instruments vus de face, regard vers +Y) ; layout="XY" : lignes au sol (pièces vues de dessus/3-4)."""
    scn = bpy.context.scene
    rows = (len(assets) + cols - 1) // cols
    clones = []
    for i, a in enumerate(assets):
        x, v = (i % cols) * cell, -(i // cols) * cell
        a.root.location = (x, 0, v) if layout == "XZ" else (x, v, 0)
        clones.append(a.root)
    # caméra orthographique en vue 3/4
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = "ORTHO"
    w, h = cols * cell, rows * cell
    cam_data.ortho_scale = max(w * 1.08, h * resolution[0] / resolution[1] * 1.08)
    cam = bpy.data.objects.new("Cam", cam_data)
    scn.collection.objects.link(cam)
    centre = Vector(((cols - 1) * cell / 2, 0, -(rows - 1) * cell / 2)) if layout == "XZ" else Vector(((cols - 1) * cell / 2, -(rows - 1) * cell / 2, 0.15))
    d = Vector(cam_dir).normalized()
    cam.location = centre + d * 20
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
    scn.camera = cam
    scn.render.engine = "BLENDER_WORKBENCH"
    scn.render.resolution_x, scn.render.resolution_y = resolution
    scn.render.filepath = png_path
    sh = scn.display.shading
    sh.light = "STUDIO"
    sh.color_type = "VERTEX"
    sh.show_shadows = True
    sh.show_cavity = False
    scn.display.render_aa = "8"
    scn.world = bpy.data.worlds.new("W")
    scn.world.color = (0.55, 0.62, 0.68)
    scn.view_settings.view_transform = "Standard"
    bpy.ops.render.render(write_still=True)
    return png_path


def cli_args():
    """Arguments après '--' : <dossier_sortie> [--preview image.png]."""
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = argv[0] if argv else os.path.join(os.getcwd(), "out")
    preview = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    os.makedirs(out, exist_ok=True)
    return out, preview


# ------------------------------------------------------------------ aperçu pièce par pièce (cadrage automatique)
def _bbox(asset):
    pts = []
    for ob in [asset.root] + list(asset.root.children_recursive):
        if ob.type == "MESH":
            pts += [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def render_each(assets, out_dir, views=(("front", (0.0, -1.0, 0.0)), ("three_quarter", (0.8, -1.0, 0.6))), resolution=(1000, 800)):
    """Un PNG par asset et par vue (`<asset>_<vue>.png`), caméra orthographique ajustée à la boîte englobante."""
    scn = bpy.context.scene
    scn.render.engine = "BLENDER_WORKBENCH"
    scn.render.resolution_x, scn.render.resolution_y = resolution
    sh = scn.display.shading
    sh.light, sh.color_type, sh.show_shadows, sh.show_cavity = "STUDIO", "VERTEX", True, False
    scn.display.render_aa = "8"
    scn.world = bpy.data.worlds.new("W2")
    scn.world.color = (0.55, 0.62, 0.68)
    scn.view_settings.view_transform = "Standard"
    cam_data = bpy.data.cameras.new("CamEach")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.new("CamEach", cam_data)
    scn.collection.objects.link(cam)
    scn.camera = cam
    for a in assets:
        for other in assets:
            hide = other is not a
            for ob in [other.root] + list(other.root.children_recursive):
                ob.hide_render = hide
        bpy.context.view_layer.update()
        lo, hi = _bbox(a)
        centre = (lo + hi) / 2
        size = max((hi - lo).x, (hi - lo).y, (hi - lo).z)
        for vname, vdir in views:
            d = Vector(vdir).normalized()
            cam.location = centre + d * 30
            cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
            # Fit the projected bounds, including diagonals in three-quarter views.
            inverse_rotation = cam.rotation_euler.to_matrix().transposed()
            corners = [inverse_rotation @ (Vector((x, y, z)) - centre)
                       for x in (lo.x, hi.x) for y in (lo.y, hi.y) for z in (lo.z, hi.z)]
            width = max(p.x for p in corners) - min(p.x for p in corners)
            height = max(p.y for p in corners) - min(p.y for p in corners)
            cam_data.ortho_scale = max(width, height * resolution[0] / resolution[1]) * 1.15
            scn.render.filepath = os.path.join(out_dir, "%s_%s.png" % (a.name, vname))
            bpy.ops.render.render(write_still=True)
    for a in assets:
        for ob in [a.root] + list(a.root.children_recursive):
            ob.hide_render = False
