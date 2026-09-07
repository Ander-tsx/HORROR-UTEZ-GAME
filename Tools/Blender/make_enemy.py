"""
Turns a downloaded humanoid .glb into a rigged, PS1-budget enemy for the level.

Run headless:

    /Applications/Blender.app/Contents/MacOS/Blender -b --python Tools/Blender/make_enemy.py -- \
        <source.glb> <output.blend> <basecolor.png> [preview.png]

The source is a 211k-triangle sculpt with 2048px textures and **no skeleton at all**. Three
things have to happen before it can be an enemy: it has to lose 99% of its triangles, it has to
get a rig, and its textures have to come down to something the project's own art density.

On rigging without Mixamo: the armature here is built by *measuring the mesh* rather than by
assuming proportions. Limb positions are found by taking horizontal slices and looking at where
the vertices actually are, so the same script rigs a lanky creature and a stocky one without
hand-tuning. Bones are then bound with Blender's automatic weights, which is good enough at this
fidelity — a PS1 character had around twenty bones and nobody was inspecting elbow deformation.

Bone names follow Unity's Humanoid convention, so Unity maps the rig to its own avatar and any
humanoid animation retargets onto it.
"""
import sys
import bpy
import bmesh
from mathutils import Vector

# ---------------------------------------------------------------- knobs

TARGET_TRIS = 2600      # a hero enemy; the level's props sit under 1000
TARGET_HEIGHT = 2.05    # metres. Taller than the player, on purpose
TEXTURE_SIZE = 256      # PS1 characters lived on 256px sheets


def log(message):
    print(f"[enemy] {message}")


# ---------------------------------------------------------------- mesh prep

def single_mesh():
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    for obj in list(bpy.data.objects):
        if obj.type != 'MESH':
            bpy.data.objects.remove(obj, do_unlink=True)

    if len(meshes) > 1:
        bpy.ops.object.select_all(action='DESELECT')
        for obj in meshes:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = meshes[0]
        bpy.ops.object.join()

    obj = [o for o in bpy.data.objects if o.type == 'MESH'][0]
    obj.name = "Body"
    return obj


def bake_transform(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def bounds(obj):
    lo = Vector((1e9,) * 3)
    hi = Vector((-1e9,) * 3)
    for corner in obj.bound_box:
        w = obj.matrix_world @ Vector(corner)
        lo = Vector((min(lo.x, w.x), min(lo.y, w.y), min(lo.z, w.z)))
        hi = Vector((max(hi.x, w.x), max(hi.y, w.y), max(hi.z, w.z)))
    return lo, hi


def normalise(obj):
    """Stands the model on Z=0, centres it in plan, and scales it to a real height."""
    lo, hi = bounds(obj)
    scale = TARGET_HEIGHT / (hi.z - lo.z)
    obj.scale = (scale, scale, scale)
    bake_transform(obj)

    lo, hi = bounds(obj)
    obj.location = Vector((-(lo.x + hi.x) * 0.5, -(lo.y + hi.y) * 0.5, -lo.z))
    bake_transform(obj)

    lo, hi = bounds(obj)
    log(f"normalised to {hi.x - lo.x:.2f} x {hi.y - lo.y:.2f} x {hi.z - lo.z:.2f} m")


def triangle_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def decimate(obj, target):
    current = triangle_count(obj)
    if current <= target:
        log(f"already within budget at {current} tris")
        return

    ratio = target / current
    bpy.context.view_layer.objects.active = obj
    mod = obj.modifiers.new(name="Decimate", type='DECIMATE')
    mod.decimate_type = 'COLLAPSE'
    mod.ratio = ratio
    bpy.ops.object.modifier_apply(modifier=mod.name)
    log(f"decimated {current} -> {triangle_count(obj)} tris, "
        f"{len(obj.data.vertices)} verts (ratio {ratio:.4f})")


def remove_loose(obj):
    """
    Drops vertices no face uses.

    Decimation left 37,357 of them behind on a 2,599-triangle mesh. They are invisible, but they
    are counted by the slice measurements that place the bones and they are skinned like any
    other vertex, so they quietly distort both.
    """
    before = len(obj.data.vertices)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(obj.data)
    bm.free()
    log(f"removed {before - len(obj.data.vertices)} loose vertices, {len(obj.data.vertices)} left")


# ---------------------------------------------------------------- measuring

def slice_centroid(obj, z, thickness, side):
    """
    Centre of the vertices on one side of the body at a given height.

    `side` is -1 for the model's left (negative X) and +1 for its right. Used to find where the
    arms and legs actually are instead of assuming a proportion, which is what lets this rig a
    creature with unusually long arms without any hand-tuning.
    """
    total = Vector((0.0, 0.0, 0.0))
    count = 0
    for v in obj.data.vertices:
        p = v.co
        if abs(p.z - z) > thickness:
            continue
        if side != 0 and p.x * side <= 0:
            continue
        total += p
        count += 1

    if count == 0:
        return None
    return total / count


def outermost(obj, z, thickness, side):
    """Furthest-out point on one side at a height: a fingertip or the outside of a foot."""
    best = None
    for v in obj.data.vertices:
        p = v.co
        if abs(p.z - z) > thickness:
            continue
        if p.x * side <= 0:
            continue
        if best is None or p.x * side > best.x * side:
            best = p.copy()
    return best


# ---------------------------------------------------------------- rigging

def build_armature(obj):
    """
    A Unity-Humanoid-named skeleton, sized from the mesh.

    Only the bones Unity's avatar actually requires, plus hands and feet. Fingers and toes are
    left out deliberately: nothing in this game ever sees them, and every extra bone is another
    chance for automatic weights to do something stupid.
    """
    lo, hi = bounds(obj)
    height = hi.z - lo.z
    t = height * 0.02   # slice thickness for measuring

    def at(fraction):
        return lo.z + height * fraction

    # Measured, not assumed.
    hips_z = at(0.50)
    knee_z = at(0.27)
    ankle_z = at(0.06)
    shoulder_z = at(0.80)
    elbow_z = at(0.62)
    wrist_z = at(0.44)

    def limb(z, side, fallback_x):
        centroid = slice_centroid(obj, z, t, side)
        if centroid is None:
            return Vector((fallback_x * side, 0.0, z))
        return Vector((centroid.x, centroid.y, z))

    armature_data = bpy.data.armatures.new("EnemyRig")
    rig = bpy.data.objects.new("Rig", armature_data)
    bpy.context.scene.collection.objects.link(rig)

    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    edit = armature_data.edit_bones

    def bone(name, head, tail, parent=None):
        b = edit.new(name)
        b.head = head
        b.tail = tail
        if parent is not None:
            b.parent = parent
            b.use_connect = False
        return b

    spine_x = 0.0
    hips = bone("Hips", Vector((spine_x, 0, hips_z)), Vector((spine_x, 0, at(0.58))))
    spine = bone("Spine", hips.tail, Vector((spine_x, 0, at(0.68))), hips)
    chest = bone("Chest", spine.tail, Vector((spine_x, 0, at(0.78))), spine)
    neck = bone("Neck", chest.tail, Vector((spine_x, 0, at(0.86))), chest)
    bone("Head", neck.tail, Vector((spine_x, 0, at(0.99))), neck)

    for side, tag in ((-1, "Left"), (1, "Right")):
        shoulder = limb(shoulder_z, side, height * 0.09)
        elbow = limb(elbow_z, side, height * 0.13)
        wrist = limb(wrist_z, side, height * 0.15)
        hand_tip = outermost(obj, wrist_z - height * 0.06, t * 2, side) or wrist

        clav = bone(f"{tag}Shoulder", Vector((spine_x, 0, shoulder_z)), shoulder, chest)
        upper = bone(f"{tag}UpperArm", shoulder, elbow, clav)
        lower = bone(f"{tag}LowerArm", elbow, wrist, upper)
        bone(f"{tag}Hand", wrist, Vector((hand_tip.x, hand_tip.y, wrist.z - height * 0.05)), lower)

        hip_point = limb(hips_z - height * 0.02, side, height * 0.06)
        knee = limb(knee_z, side, height * 0.06)
        ankle = limb(ankle_z, side, height * 0.05)

        # Feet point forward, which in Blender's Z-up is -Y once the glTF importer is done.
        toe = Vector((ankle.x, ankle.y - height * 0.09, ankle.z * 0.4))

        thigh = bone(f"{tag}UpperLeg", Vector((hip_point.x, hip_point.y, hips_z)), knee, hips)
        shin = bone(f"{tag}LowerLeg", knee, ankle, thigh)
        bone(f"{tag}Foot", ankle, toe, shin)

    bpy.ops.object.mode_set(mode='OBJECT')
    log(f"built rig with {len(armature_data.bones)} bones")
    return rig


def bind(obj, rig):
    """
    Skins the mesh by distance to each bone, instead of trusting Blender's automatic weights.

    Automatic weights ("bone heat") **fails silently on this model**. It creates a vertex group
    per bone, assigns nothing to any of them, and reports success; the mesh then hangs rigidly off
    whichever bone the exporter picks first. In Unity that came out as 100% of vertices bound to
    the hips — the skeleton swinging a full stride while the creature glided down the corridor
    without bending a knee, and not one warning anywhere.

    Heat weighting needs clean, closed geometry, and this is a decimated sculpt full of loose
    shards of hair. Rather than repair the mesh to suit the algorithm, weights are computed
    directly: for each vertex, distance to every bone segment, and the nearest few bones share it.

    Cruder than heat weighting, and completely adequate here — a PS1 character had about twenty
    bones and nobody inspected its elbows. What matters is that it is deterministic and can be
    checked, which the next function does.
    """
    influences = 3      # per vertex. Unity is set to keep 4, so this always survives the import
    falloff = 3.0       # higher concentrates weight on the single nearest bone

    bones = [(b.name, b.head_local.copy(), b.tail_local.copy()) for b in rig.data.bones]

    groups = {}
    for name, _, _ in bones:
        groups[name] = obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)

    for v in obj.data.vertices:
        scored = []
        for name, head, tail in bones:
            scored.append((distance_to_segment(v.co, head, tail), name))
        scored.sort()

        nearest = scored[:influences]
        weights = [(1.0 / max(d, 1e-4) ** falloff, name) for d, name in nearest]
        total = sum(w for w, _ in weights)

        for weight, name in weights:
            groups[name].add([v.index], weight / total, 'REPLACE')

    modifier = obj.modifiers.new(name="Armature", type='ARMATURE')
    modifier.object = rig
    modifier.use_vertex_groups = True
    obj.parent = rig

    verify_weights(obj)


def distance_to_segment(point, a, b):
    """Shortest distance from a point to a bone, treating the bone as its head-to-tail segment."""
    ab = b - a
    length_squared = ab.dot(ab)
    if length_squared < 1e-9:
        return (point - a).length

    t = max(0.0, min(1.0, (point - a).dot(ab) / length_squared))
    return (point - (a + ab * t)).length


def verify_weights(obj):
    """
    Fails loudly if the skinning did not take.

    This is the check that was missing. Silent weighting failure does not throw, does not warn,
    and does not look wrong until the creature is walking in the game — several build steps and
    a play session later.
    """
    weighted = sum(1 for v in obj.data.vertices if any(g.weight > 0.0 for g in v.groups))
    if weighted < len(obj.data.vertices):
        raise RuntimeError(
            f"only {weighted} of {len(obj.data.vertices)} vertices got a skin weight")

    from collections import Counter
    names = [g.name for g in obj.vertex_groups]
    dominant = Counter()
    for v in obj.data.vertices:
        best = max(v.groups, key=lambda g: g.weight)
        dominant[names[best.group]] += 1

    log(f"skinned {weighted} vertices across {len(dominant)} bones; "
        f"largest share {dominant.most_common(1)[0][0]} at "
        f"{dominant.most_common(1)[0][1] * 100 // weighted}%")

    if len(dominant) < 6:
        raise RuntimeError(f"weights collapsed onto {len(dominant)} bones; the rig will look rigid")


# ---------------------------------------------------------------- textures

def export_base_colour(path):
    """
    Pulls the base colour map out of the .glb, shrinks it, and writes it beside the model.

    The source ships three 2048px maps — colour, normal and a packed roughness/metallic. The
    project's shader only samples a base map, and at 360p a 2048px texture on a creature two
    metres away is resolving detail the framebuffer cannot hold. Normal maps are dropped for the
    same reason: the shader is Blinn-Phong with flat shading, so they would do nothing.
    """
    candidates = [img for img in bpy.data.images if img.size[0] > 0]
    if not candidates:
        log("no images in the source; the enemy will be untextured")
        return False

    # The base colour is whichever image feeds Base Color; fall back to the largest.
    chosen = None
    for mat in bpy.data.materials:
        if not mat.use_nodes:
            continue
        for node in mat.node_tree.nodes:
            if node.type != 'BSDF_PRINCIPLED':
                continue
            link = next((l for l in node.inputs['Base Color'].links), None)
            if link is not None and getattr(link.from_node, "image", None) is not None:
                chosen = link.from_node.image
    if chosen is None:
        chosen = max(candidates, key=lambda i: i.size[0] * i.size[1])

    chosen.scale(TEXTURE_SIZE, TEXTURE_SIZE)
    chosen.filepath_raw = path
    chosen.file_format = 'PNG'
    chosen.save()
    log(f"base colour -> {path} at {TEXTURE_SIZE}px")
    return True


# ---------------------------------------------------------------- preview

def render_preview(path):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x = 520
    scene.render.resolution_y = 760
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.show_object_outline = True
    scene.display.render_aa = 'OFF'

    obj = bpy.data.objects["Body"]
    lo, hi = bounds(obj)
    centre = (lo + hi) * 0.5
    radius = max(hi - lo)

    cam_data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.location = centre + Vector((radius * 0.9, -radius * 1.6, radius * 0.35))
    cam.rotation_euler = (centre - cam.location).normalized().to_track_quat('-Z', 'Y').to_euler()

    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam, do_unlink=True)
    log(f"preview {path}")


# ---------------------------------------------------------------- main

def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    source, output, texture = argv[0], argv[1], argv[2]
    preview = argv[3] if len(argv) > 3 else None

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=source)

    obj = single_mesh()
    bake_transform(obj)
    normalise(obj)
    decimate(obj, TARGET_TRIS)
    remove_loose(obj)

    obj.data.shade_flat()
    export_base_colour(texture)

    rig = build_armature(obj)
    bind(obj, rig)

    if preview:
        render_preview(preview)

    bpy.ops.wm.save_as_mainfile(filepath=output)
    log(f"saved {output}")


main()
