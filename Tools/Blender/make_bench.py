"""
Turns the downloaded `banketka.blend` studio scene into a kit piece for the level.

Run headless:

    /Applications/Blender.app/Contents/MacOS/Blender -b SourceArt/banketka.blend \
        --python Tools/Blender/make_bench.py -- <output.blend> [preview.png]

Why a script and not hand work in Blender: the same reason every other piece of this level is
generated. The steps below are exact and repeatable, so if the source asset is replaced or the
target polygon budget changes, the piece is rebuilt rather than re-sculpted. Doing it by hand is
perfectly valid too — the equivalent manual steps are named in the comments.

What the source actually is: a product render scene, not a game asset. It ships with a softbox,
a backdrop, subdivision surface modifiers pushing it to ~118k triangles, a missing texture, and
a model roughly 4.8x life size lying on its side, and both parts modelled as open shells with no
thickness. None of that is unusual for a downloaded asset, and all of it has to be dealt with
before it can be used.

About the missing texture: `dot_faktr02.jpg` fed a Mix Shader factor against a Transparent BSDF,
which is how the perforations in the backrest were made — the holes were never geometry. The
file does not ship with the .blend, so they are simply gone. Reproducing them with an alpha mask
was tried and looked worse than plain sheet at this fidelity, so the bench is solid metal.
"""
import sys
import bpy
from mathutils import Vector

# ---------------------------------------------------------------- knobs

# Real length of a three-seat corridor bench, in metres. Everything else is scaled from this,
# so the proportions of the original model are preserved exactly.
TARGET_LENGTH = 1.80

# Triangle budget per part. A PS1 prop lived between 100 and 400 triangles; this is a piece the
# torch lands on directly in a corridor the player walks a hundred times, so it gets a little
# more. The frame keeps a larger share because tubing needs more geometry. We're increasing it for the premium HD look.
TARGET_TRIS = {"Frame": 3500, "Seat": 2500}

# Shell thickness, in metres. The source is modelled as open shells with no thickness at all —
# 230 boundary edges on the seat — which is normal for a render asset and fatal in a game: an
# open shell is invisible from behind, because back faces are culled. Solidify gives it real
# sheet metal instead of a one-sided skin, so the backrest exists from both sides and casts a
# shadow shaped like a bench. Manual: wrench tab, Add Modifier > Generate > Solidify.
SOLIDIFY = {"Seat": 0.012}

# Blender material names, which is what KitMaterials keys on. Give a piece these and it picks up
# the level's palette automatically; give it a texture of your own instead and the level leaves
# it alone.
MATERIALS = {"Frame": "M_Metal", "Seat": "M_Metal"}

# An earlier pass split the shell at 0.50 m to give the backrest a perforated material of its
# own, since the original model's holes were an alpha mask that never shipped with the file.
# It was dropped: a height cut does not follow the real part boundary — the armrests reach above
# it — and a perforation drawn through a solidified panel reads as woven wire rather than as
# punched sheet. Plain metal on the whole bench looks better and costs nothing. If it ever comes
# back, split by material or by loose part in Blender, not by height.


def log(message):
    print(f"[bench] {message}")


def strip_scene():
    """Deletes the studio: lights, camera, backdrop, softbox. Manual: select them and X."""
    for obj in list(bpy.data.objects):
        if obj.type != 'MESH':
            bpy.data.objects.remove(obj, do_unlink=True)
    for block in list(bpy.data.materials):
        bpy.data.materials.remove(block)


def resolve_modifiers(obj):
    """
    Drops Subdivision Surface and bakes everything else in.

    Subdivision is the whole reason this model is 118k triangles: it is a render-time smoothing
    modifier, and the base cage underneath is already a reasonable shape. Removing it rather
    than decimating its result keeps far more of the original topology.

    Manual: in the wrench tab, X on the Subdivision modifier, then Ctrl+A on the rest.
    """
    bpy.context.view_layer.objects.active = obj
    for mod in list(obj.modifiers):
        if mod.type == 'SUBSURF':
            obj.modifiers.remove(mod)
        else:
            bpy.ops.object.modifier_apply(modifier=mod.name)


def bake_transform(obj):
    """Applies rotation and scale so mesh data sits in world orientation. Manual: Ctrl+A."""
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def world_bounds(objects):
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for obj in objects:
        for corner in obj.bound_box:
            w = obj.matrix_world @ Vector(corner)
            lo = Vector((min(lo.x, w.x), min(lo.y, w.y), min(lo.z, w.z)))
            hi = Vector((max(hi.x, w.x), max(hi.y, w.y), max(hi.z, w.z)))
    return lo, hi


def triangle_count(obj):
    me = obj.data
    me.calc_loop_triangles()
    return len(me.loop_triangles)


def decimate_to(obj, target):
    """
    Collapse decimation down to a triangle budget.

    Collapse rather than Planar because this is a curved, organic-ish frame: Planar only merges
    faces that are already coplanar, which barely touches tubing. Watch the silhouette when
    tuning the budget — the number at which a shape stops reading is the number that matters,
    not the triangle count.

    Manual: wrench tab, Add Modifier > Generate > Decimate, drag Ratio down, then Apply.
    """
    current = triangle_count(obj)
    if current <= target:
        log(f"{obj.name}: {current} tris already within budget")
        return

    ratio = target / current
    bpy.context.view_layer.objects.active = obj
    mod = obj.modifiers.new(name="Decimate", type='DECIMATE')
    mod.decimate_type = 'COLLAPSE'
    mod.ratio = ratio
    bpy.ops.object.modifier_apply(modifier=mod.name)
    log(f"{obj.name}: {current} -> {triangle_count(obj)} tris (ratio {ratio:.3f})")


def solidify(obj, thickness):
    """Gives an open shell real thickness so it is not invisible from behind."""
    before = triangle_count(obj)
    bpy.context.view_layer.objects.active = obj
    mod = obj.modifiers.new(name="Solidify", type='SOLIDIFY')
    mod.thickness = thickness
    mod.offset = 0.0
    bpy.ops.object.modifier_apply(modifier=mod.name)
    log(f"{obj.name}: solidified {thickness * 1000:.0f} mm, {before} -> {triangle_count(obj)} tris")


def shade_flat(obj):
    """
    Flat shading, which is half the period look.

    Smoothed vertex normals are the single clearest tell of a modern model: the PS1 had no
    normal interpolation worth the name, so every facet read as a facet. Manual: Object > Shade
    Flat.
    """
    obj.data.shade_flat()
    # Blender 4.x dropped `use_auto_smooth`; older files still carry it and it would re-smooth
    # the normals behind shade_flat's back.
    if hasattr(obj.data, "use_auto_smooth"):
        obj.data.use_auto_smooth = False


def assign_material(obj, name):
    obj.data.materials.clear()
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    obj.data.materials.append(material)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    output = argv[0]
    preview = argv[1] if len(argv) > 1 else None

    strip_scene()

    # The source names its parts 'axis' (the tubular frame and legs) and 'seat'.
    rename = {"axis": "Frame", "seat": "Seat"}
    parts = []
    for obj in bpy.data.objects:
        obj.name = rename.get(obj.name, obj.name)
        parts.append(obj)

    for obj in parts:
        resolve_modifiers(obj)
        bake_transform(obj)

    lo, hi = world_bounds(parts)
    size = hi - lo
    log(f"source size x={size.x:.2f} y={size.y:.2f} z={size.z:.2f}")

    # The bench arrives lying along world Y. Kit convention runs a piece's length along +X, the
    # same way walls do, so the level can place it with a plain yaw.
    for obj in parts:
        obj.rotation_euler = (0.0, 0.0, -1.5707963)
        bake_transform(obj)

    lo, hi = world_bounds(parts)
    size = hi - lo
    scale = TARGET_LENGTH / size.x
    log(f"scaling by {scale:.4f} to a {TARGET_LENGTH:.2f} m bench")

    for obj in parts:
        obj.scale = (scale, scale, scale)
        bake_transform(obj)

    # Origin centred in plan and sitting on Z=0, the kit's convention for props. Getting this
    # wrong is why a piece ends up floating or sunk into the floor once the level places it.
    lo, hi = world_bounds(parts)
    offset = Vector((-(lo.x + hi.x) * 0.5, -(lo.y + hi.y) * 0.5, -lo.z))
    for obj in parts:
        obj.location = offset
        bake_transform(obj)

    for obj in parts:
        decimate_to(obj, TARGET_TRIS.get(obj.name, 400))
        # Solidify after decimating, not before: collapsing a mesh that already has thin walls
        # pinches them shut, and the triangle budget stops being predictable.
        if obj.name in SOLIDIFY:
            solidify(obj, SOLIDIFY[obj.name])
        shade_flat(obj)
        assign_material(obj, MATERIALS.get(obj.name, "M_Metal"))

    lo, hi = world_bounds(parts)
    size = hi - lo
    total = sum(triangle_count(o) for o in parts)
    log(f"final size x={size.x:.2f} y={size.y:.2f} z={size.z:.2f}, {total} tris, "
        f"origin at ({lo.x + size.x / 2:.2f}, {lo.y + size.y / 2:.2f}, {lo.z:.2f})")

    if preview:
        render_preview(parts, preview)

    bpy.ops.wm.save_as_mainfile(filepath=output)
    log(f"saved {output}")


def render_preview(parts, path):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x = 900
    scene.render.resolution_y = 700
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.show_object_outline = True
    # Wireframe over the solid, so the polygon budget is visible rather than described.
    scene.display.shading.show_xray = False
    scene.display.render_aa = 'OFF'

    lo, hi = world_bounds(parts)
    centre = (lo + hi) * 0.5
    radius = max(hi - lo)

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = centre + Vector((radius * 0.9, -radius * 1.3, radius * 0.7))
    cam.rotation_euler = (centre - cam.location).normalized().to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam

    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)

    bpy.data.objects.remove(cam, do_unlink=True)
    log(f"preview {path}")


main()
