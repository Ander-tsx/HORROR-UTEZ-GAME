"""
Generates the Cecadec modular kit: one .blend per piece.

Each piece is a blockout at its exact final dimensions, with the pivot where the level
builder expects it and UVs already laid out at the project's texel density (2 m per
texture repeat). Open any file, sculpt or detail it however you like, and as long as you
keep the bounding box and the origin where they are, it drops straight back into the level.

Blender is Z-up and Unity is Y-up; Unity's importer handles the conversion, so pieces are
authored here as X = width, Y = depth, Z = height.

Run:  blender --background --python Tools/Blender/generate_kit.py
"""

import bmesh
import bpy
import os
import sys
from mathutils import Vector

OUT_DIR = os.path.join(os.getcwd(), "Assets", "_Project", "Art", "Models", "Kit")
METRES_PER_REPEAT = 2.0

# ---------------------------------------------------------------- dimensions
WALL_H = 3.2
EXT_T = 0.35
INT_T = 0.22
SLAB_T = 0.35

DOOR_W, DOOR_H = 1.15, 2.15
GLASS_W, GLASS_H = 2.80, 2.45
# The blocked Servicios doors reuse the glass leaves, so the hole must match them.
BLOCKED_W = GLASS_W

MATERIALS = {
    "Wall":      (0.86, 0.86, 0.80),
    "Floor":     (0.62, 0.62, 0.60),
    "Ceiling":   (0.90, 0.90, 0.88),
    "Stair":     (0.34, 0.34, 0.33),
    "DoorWood":  (0.38, 0.26, 0.17),
    "DoorGlass": (0.55, 0.72, 0.78),
    "Metal":     (0.32, 0.33, 0.35),
    "Wood":      (0.42, 0.31, 0.21),
    "Plastic":   (0.16, 0.22, 0.30),
    "Case":      (0.20, 0.20, 0.22),
    "Screen":    (0.05, 0.05, 0.06),
    "Board":     (0.88, 0.89, 0.86),
    "Fixture":   (0.30, 0.30, 0.29),
    "Tube":      (0.72, 0.68, 0.60),
}


def box(centre, size):
    """One axis-aligned box: (centre, size) in metres."""
    return (Vector(centre), Vector(size))


def wall_parts(length, thickness, height, opening):
    """
    A wall with a hole, as separate named objects.

    Separate objects matter: the level gives every mesh its own box collider, and a single
    mesh with a hole in it has a bounding box that fills the hole straight back in — which
    silently walls up every doorway in the building.
    """
    ow, oh = opening
    side = (length - ow) / 2.0
    parts = []
    if side > 1e-4:
        parts.append(("Side_L", [box((-(ow + side) / 2.0, 0, height / 2), (side, thickness, height))]))
        parts.append(("Side_R", [box(((ow + side) / 2.0, 0, height / 2), (side, thickness, height))]))
    if height - oh > 1e-4:
        parts.append(("Lintel", [box((0, 0, oh + (height - oh) / 2.0), (ow, thickness, height - oh))]))
    return parts


def wall(length, thickness, height, opening=None):
    """
    A wall running along +X, centred on X and Y, sitting on Z=0.
    `opening` is (width, height) centred in the wall.
    """
    if opening is None:
        return [box((0, 0, height / 2), (length, thickness, height))]

    ow, oh = opening
    side = (length - ow) / 2.0
    parts = []
    if side > 1e-4:
        for sign in (-1, 1):
            parts.append(box((sign * (ow + side) / 2.0, 0, height / 2),
                             (side, thickness, height)))
    if height - oh > 1e-4:
        parts.append(box((0, 0, oh + (height - oh) / 2.0),
                         (ow, thickness, height - oh)))
    return parts


def uv_project(bm, uv_layer):
    """Planar projection per face from its dominant axis, scaled to metres."""
    inv = 1.0 / METRES_PER_REPEAT
    for face in bm.faces:
        n = face.normal
        ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
        for loop in face.loops:
            co = loop.vert.co
            if az >= ax and az >= ay:
                u, v = co.x, co.y
            elif ax >= ay:
                u, v = co.y, co.z
            else:
                u, v = co.x, co.z
            loop[uv_layer].uv = (u * inv, v * inv)


def material(name):
    mat = bpy.data.materials.new(name=f"M_{name}")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        r, g, b = MATERIALS[name]
        bsdf.inputs["Base Color"].default_value = (r, g, b, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.85
    return mat


def build(name, groups):
    """`groups` is a list of (material_name, [boxes])."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0

    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)

    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.new("UVMap")

    for slot, (mat_name, boxes) in enumerate(groups):
        obj.data.materials.append(material(mat_name))
        for centre, size in boxes:
            start = len(bm.faces)
            bmesh.ops.create_cube(bm, size=1.0)
            bm.verts.ensure_lookup_table()
            bm.faces.ensure_lookup_table()
            new_faces = bm.faces[start:]
            verts = {v for f in new_faces for v in f.verts}
            for v in verts:
                v.co = Vector((v.co.x * size.x, v.co.y * size.y, v.co.z * size.z)) + centre
            for f in new_faces:
                f.material_index = slot

    bm.normal_update()
    uv_project(bm, uv_layer)
    bm.to_mesh(mesh)
    bm.free()

    mesh.update()
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, f"{name}.blend")
    bpy.ops.wm.save_as_mainfile(filepath=path)
    print(f"KIT_PIECE {name}")
    return path


def one(name, mat, boxes):
    return build(name, [(mat, boxes)])


def build_multi(name, objects):
    """
    Several separate objects in one file. Used where the level needs to drive parts
    independently — the light fitting's tube has to switch off on its own, which needs its
    own renderer rather than a second material slot on one mesh.
    `objects` is a list of (object_name, material_name, boxes).
    """
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"

    for obj_name, mat_name, boxes in objects:
        mesh = bpy.data.meshes.new(obj_name)
        obj = bpy.data.objects.new(obj_name, mesh)
        bpy.context.collection.objects.link(obj)
        obj.data.materials.append(material(mat_name))

        bm = bmesh.new()
        uv_layer = bm.loops.layers.uv.new("UVMap")
        for centre, size in boxes:
            start = len(bm.faces)
            bmesh.ops.create_cube(bm, size=1.0)
            bm.verts.ensure_lookup_table()
            bm.faces.ensure_lookup_table()
            verts = {v for f in bm.faces[start:] for v in f.verts}
            for v in verts:
                v.co = Vector((v.co.x * size.x, v.co.y * size.y, v.co.z * size.z)) + centre
        bm.normal_update()
        uv_project(bm, uv_layer)
        bm.to_mesh(mesh)
        bm.free()
        mesh.update()

    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, f"{name}.blend")
    bpy.ops.wm.save_as_mainfile(filepath=path)
    print(f"KIT_PIECE {name}")
    return path


# ================================================================ the kit
# Pivot conventions, which the level builder relies on:
#   walls    - centred on X and Y, sitting on Z = 0, running along +X
#   slabs    - centred on X and Y, top face at Z = 0 (so they hang below floor level)
#   props    - centred on X and Y, sitting on Z = 0 (floor level)
#   fixtures - centred on X and Y, top face at Z = 0 (so they hang from a ceiling)
#   leaves   - hinge on the X = 0 edge, so rotating about Z swings the door

def generate_architecture():
    for length in (4, 2, 1):
        one(f"Wall_Ext_{length}m", "Wall", wall(length, EXT_T, WALL_H))
        one(f"Wall_Int_{length}m", "Wall", wall(length, INT_T, WALL_H))

    for name, thickness, hole in (
        ("Wall_Ext_Entrance", EXT_T, (GLASS_W, GLASS_H)),
        ("Wall_Ext_Door", EXT_T, (DOOR_W, DOOR_H)),
        ("Wall_Int_Door", INT_T, (DOOR_W, DOOR_H)),
        ("Wall_Int_GlassDoor", INT_T, (BLOCKED_W, GLASS_H)),
    ):
        build_multi(name, [(part, "Wall", boxes)
                           for part, boxes in wall_parts(4, thickness, WALL_H, hole)])

    for side in (4, 2, 1):
        one(f"Floor_{side}x{side}", "Floor",
            [box((0, 0, -SLAB_T / 2), (side, side, SLAB_T))])
        one(f"Ceiling_{side}x{side}", "Ceiling",
            [box((0, 0, SLAB_T / 2), (side, side, SLAB_T))])


def generate_stairs():
    tread, rise, run_w = 0.32, 0.28, 1.2
    one("Stair_Step", "Stair", [box((0, 0, -rise / 2), (tread, run_w, rise))])
    one("Stair_Landing", "Stair", [box((0, 0, -rise / 2), (run_w, run_w, rise))])
    one("Stair_Core", "Wall", [box((0, 0, 3.55 / 2), (1.6, 1.6, 3.55))])


def generate_doors():
    leaf_t = 0.06
    # Leaves are centred on their own origin, NOT hinged at an edge. The importer mirrors X,
    # so an edge pivot lands the panel on the wrong side of its hinge; a centred panel looks
    # identical mirrored. The level builds the hinge as an empty parent and offsets the leaf
    # inside it, which keeps the swing under the builder's control instead of the importer's.
    one("Door_Leaf_Single", "DoorWood",
        [box((0, 0, DOOR_H / 2), (DOOR_W, leaf_t, DOOR_H))])
    one("Door_Leaf_Glass", "DoorGlass",
        [box((0, 0, GLASS_H / 2), (GLASS_W / 2, leaf_t, GLASS_H))])

    for name, w, h in (("Single", DOOR_W, DOOR_H), ("Glass", GLASS_W, GLASS_H)):
        one(f"Door_Frame_{name}", "Metal", [
            box((0, 0, h + 0.05), (w + 0.20, 0.14, 0.10)),
            box((-(w / 2 + 0.05), 0, h / 2), (0.10, 0.14, h)),
            box((w / 2 + 0.05, 0, h / 2), (0.10, 0.14, h)),
        ])


def generate_props():
    # Fixture hangs from the ceiling: top face at Z = 0. Body and Tube are separate objects
    # so the level can switch the glowing tube off with the light.
    build_multi("Light_Fixture", [
        ("Body", "Fixture", [box((0, 0, -0.05), (1.20, 0.24, 0.10))]),
        ("Tube", "Tube", [box((0, 0, -0.115), (1.06, 0.16, 0.03))]),
    ])

    build("Butaca", [
        ("Plastic", [box((0, 0, 0.45), (0.46, 0.44, 0.05)),
                     box((0, -0.21, 0.70), (0.46, 0.05, 0.46))]),
        ("Wood", [box((0.34, 0.06, 0.72), (0.34, 0.30, 0.04))]),
        ("Metal", [box((0.24, 0.06, 0.58), (0.04, 0.04, 0.28))] +
                  [box((x, y, 0.22), (0.04, 0.04, 0.44))
                   for x in (-0.19, 0.19) for y in (-0.18, 0.18)]),
    ])

    build("Chair_Office", [
        ("Plastic", [box((0, 0, 0.45), (0.46, 0.46, 0.07)),
                     box((0, -0.22, 0.72), (0.44, 0.06, 0.48))]),
        ("Metal", [box((0, 0, 0.22), (0.07, 0.07, 0.44)),
                   box((0, 0, 0.03), (0.52, 0.52, 0.06))]),
    ])

    build("Desk_PC", [
        ("Wood", [box((0, 0, 0.75), (1.40, 0.70, 0.05)),
                  box((-0.66, 0, 0.375), (0.05, 0.66, 0.75)),
                  box((0.66, 0, 0.375), (0.05, 0.66, 0.75)),
                  box((0, -0.32, 0.45), (1.30, 0.04, 0.40))]),
        ("Case", [box((-0.22, -0.08, 0.835), (0.44, 0.42, 0.12)),
                  box((-0.22, -0.08, 0.945), (0.16, 0.14, 0.10)),
                  box((0.16, 0.12, 0.785), (0.44, 0.15, 0.02))]),
        ("Screen", [box((-0.22, -0.10, 1.175), (0.52, 0.03, 0.34))]),
    ])

    build("Teacher_Desk", [
        ("Wood", [box((0, 0, 0.76), (1.60, 0.80, 0.06)),
                  box((0, -0.36, 0.38), (1.56, 0.05, 0.72)),
                  box((-0.78, 0, 0.38), (0.05, 0.76, 0.76)),
                  box((0.78, 0, 0.38), (0.05, 0.76, 0.76))]),
    ])

    build("Cubicle", [
        ("Metal", [box((0, -1.20, 0.775), (2.40, 0.06, 1.55)),
                   box((-1.20, -0.40, 0.775), (0.06, 1.60, 1.55)),
                   box((1.05, -0.95, 0.35), (0.45, 0.45, 0.70))]),
        ("Wood", [box((0, -0.85, 0.75), (1.80, 0.65, 0.05)),
                  box((-0.86, -0.85, 0.375), (0.05, 0.62, 0.75)),
                  box((0.86, -0.85, 0.375), (0.05, 0.62, 0.75))]),
        ("Screen", [box((0.10, -1.00, 1.02), (0.48, 0.04, 0.32))]),
    ])

    build("Board_Wall", [
        ("Board", [box((-0.90, 0, 1.55), (3.00, 0.06, 1.25))]),
        ("Screen", [box((1.90, 0, 1.70), (1.80, 0.05, 1.20))]),
    ])

    build("Generator", [
        ("Floor", [box((0, 0, 0.06), (4.40, 3.20, 0.12))]),
        ("Metal", [box((0, 0, 0.97), (3.00, 1.60, 1.70)),
                   box((1.20, -0.60, 1.42), (0.25, 0.25, 2.60)),
                   box((0, -1.50, 0.67), (4.40, 0.08, 1.10)),
                   box((-2.16, 0, 0.67), (0.08, 3.20, 1.10)),
                   box((2.16, 0, 0.67), (0.08, 3.20, 1.10))]),
        ("Case", [box((0, 0.82, 1.42), (0.90, 0.08, 0.60))]),
    ])


if __name__ == "__main__":
    generate_architecture()
    generate_stairs()
    generate_doors()
    generate_props()
    print("KIT_DONE")
