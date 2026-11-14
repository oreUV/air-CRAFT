"""Bake the user-supplied Tripo models into Unity-ready FBX files.

Run with:
  blender -b -P Tools/blender_bake_models.py

What it does per model:
  * imports the source FBX
  * joins meshes, applies all transforms
  * rotates the model so its long axis is +Z (up) and its working end faces down
  * moves the pivot to the bottom-centre of the bounding box
  * exports to Assets/Resources/Models/<name>.fbx with Unity's Y-up axes

Source files are expected at C:/Users/vaish/Downloads/models.
"""

import bpy
import math
import os
import numpy as np
from mathutils import Vector

SRC = r"C:\Users\vaish\Downloads\models"
PROJECT = r"C:\Users\vaish\Documents\Codex\2026-10-31\hw\outputs\Space Mission Design Game"
DST = os.path.join(PROJECT, "Assets", "Resources", "Models")
PREVIEW = r"C:\Users\vaish\AppData\Local\Temp\opencode\models_preview"

# (output name, source file, align by principal axis?)
JOBS = [
    ("cockpit", "cockpit.fbx", False),
    ("fueltank1", "fueltank1.fbx", False),
    ("thruster1", "thruster1.fbx", True),
]

os.makedirs(DST, exist_ok=True)
os.makedirs(PREVIEW, exist_ok=True)


def bounds(obj):
    mn = Vector((1e9, 1e9, 1e9))
    mx = Vector((-1e9, -1e9, -1e9))
    for corner in obj.bound_box:
        w = obj.matrix_world @ Vector(corner)
        mn.x = min(mn.x, w.x); mn.y = min(mn.y, w.y); mn.z = min(mn.z, w.z)
        mx.x = max(mx.x, w.x); mx.y = max(mx.y, w.y); mx.z = max(mx.z, w.z)
    return mn, mx


def vertex_array(obj):
    n = len(obj.data.vertices)
    co = np.empty(n * 3, dtype=np.float64)
    obj.data.vertices.foreach_get("co", co)
    return co.reshape(n, 3)


def align_principal_axis(obj):
    """Rotate the mesh so its longest (principal) axis becomes +Z."""
    co = vertex_array(obj)
    centred = co - co.mean(axis=0)
    values, vectors = np.linalg.eigh(np.cov(centred.T))
    axis = Vector(vectors[:, int(np.argmax(values))].tolist()).normalized()

    quat = axis.rotation_difference(Vector((0.0, 0.0, 1.0)))
    obj.rotation_mode = 'QUATERNION'
    obj.rotation_quaternion = quat
    bpy.ops.object.transform_apply(rotation=True)


def wide_end_is_down(obj):
    """True if the end with the larger cross-section sits at the bottom (-Z)."""
    co = vertex_array(obj)
    z = co[:, 2]
    lo_z, hi_z = z.min(), z.max()
    height = max(hi_z - lo_z, 1e-6)

    def spread(part):
        if len(part) < 12:
            return -1.0
        centre = part[:, :2].mean(axis=0)
        return float(np.mean(np.linalg.norm(part[:, :2] - centre, axis=1)))

    low = co[z <= lo_z + height * 0.18]
    high = co[z >= hi_z - height * 0.18]
    return spread(low) >= spread(high)


def render_preview(name, dims):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 12
    scene.cycles.device = 'CPU'
    scene.render.resolution_x = 420
    scene.render.resolution_y = 420
    scene.world = bpy.data.worlds.new("W")
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.05, 0.05, 0.07, 1)
    bg.inputs[1].default_value = 1.0

    ldata = bpy.data.lights.new("Sun", 'SUN')
    ldata.energy = 4
    lobj = bpy.data.objects.new("Sun", ldata)
    scene.collection.objects.link(lobj)
    lobj.rotation_euler = (0.9, 0.2, 0.6)

    size = max(dims.x, dims.y, dims.z)
    center = Vector((0, 0, dims.z / 2))

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = size * 1.3
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam

    views = {
        "front": (center + Vector((0, -size * 3, 0)), (math.radians(90), 0, 0)),
        "side": (center + Vector((size * 3, 0, 0)), (math.radians(90), 0, math.radians(90))),
    }
    for vname, (loc, rot) in views.items():
        cam.location = loc
        cam.rotation_euler = rot
        scene.render.filepath = os.path.join(PREVIEW, f"baked_{name}_{vname}.png")
        bpy.ops.render.render(write_still=True)


summary = {}
for out_name, src_name, align in JOBS:
    src = os.path.join(SRC, src_name)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src)

    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()

    obj = bpy.context.active_object
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    if align:
        align_principal_axis(obj)

    # All three models work with their widest end down (heat shield, tank base, nozzle bell).
    if not wide_end_is_down(obj):
        obj.rotation_mode = 'XYZ'
        obj.rotation_euler = (math.radians(180), 0.0, 0.0)
        bpy.ops.object.transform_apply(rotation=True)

    mn, mx = bounds(obj)
    obj.location.x -= (mn.x + mx.x) / 2
    obj.location.y -= (mn.y + mx.y) / 2
    obj.location.z -= mn.z
    bpy.ops.object.transform_apply(location=True)

    mn, mx = bounds(obj)
    dims = mx - mn

    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    dst = os.path.join(DST, out_name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=dst,
        use_selection=True,
        apply_unit_scale=True,
        axis_forward='-Z',
        axis_up='Y',
    )

    render_preview(out_name, dims)

    summary[out_name] = {
        "exported": dst,
        "dims_zup": [round(dims.x, 4), round(dims.y, 4), round(dims.z, 4)],
        "verts": len(obj.data.vertices),
        "materials": [m.name for m in obj.data.materials if m],
    }

print("BAKE_SUMMARY_START")
for k, v in summary.items():
    print(k, v)
print("BAKE_SUMMARY_END")
