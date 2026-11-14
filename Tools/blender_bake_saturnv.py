"""Bake the Saturn V STL parts into Unity-ready FBX files.

STL import is done manually (no Blender STL addon required):
  * parse binary or ASCII STL
  * build a Blender mesh from the triangles
  * align the longest axis to +Z (up)
  * flip so the wider end faces down (nozzle / base down)
  * move the pivot to the bottom-centre of the bounding box
  * export to Assets/Resources/Models/SaturnV/<name>.fbx with Unity Y-up axes

Run with:
  blender -b -P Tools/blender_bake_saturnv.py
"""

import bpy
import math
import os
import struct
import numpy as np
from mathutils import Vector

SRC = r"C:\Users\vaish\Downloads\Saturn V Rocket"
PROJECT = r"C:\Users\vaish\Documents\Codex\2026-10-31\hw\outputs\Space Mission Design Game"
DST = os.path.join(PROJECT, "Assets", "Resources", "Models", "SaturnV")

os.makedirs(DST, exist_ok=True)


def parse_stl(filepath):
    """Return (vertices, triangles) from a binary or ASCII STL file."""
    with open(filepath, "rb") as f:
        data = f.read()

    # Check for ASCII STL
    if data[:5].upper() == b"SOLID":
        # ASCII STL
        text = data.decode("utf-8", errors="replace")
        verts = []
        tris = []
        lines = text.splitlines()
        i = 0
        while i < len(lines):
            line = lines[i].strip()
            if line.lower().startswith("facet"):
                # Read 3 vertices
                vtri = []
                j = i + 1
                while j < len(lines):
                    l = lines[j].strip()
                    if l.lower().startswith("vertex"):
                        parts = l.split()
                        vtri.append((float(parts[1]), float(parts[2]), float(parts[3])))
                    if l.lower().startswith("endfacet"):
                        break
                    j += 1
                if len(vtri) == 3:
                    tris.append(len(verts))
                    verts.extend(vtri)
                i = j
            else:
                i += 1
        return np.array(verts, dtype=np.float64), np.array(tris, dtype=np.int32)

    # Binary STL: 80-byte header, 4-byte triangle count, then 50 bytes per triangle
    if len(data) < 84:
        return np.zeros((0, 3)), np.zeros((0,), dtype=np.int32)

    tri_count = struct.unpack("<I", data[80:84])[0]
    expected = 84 + tri_count * 50
    if len(data) < expected:
        tri_count = (len(data) - 84) // 50

    verts = []
    tris = []
    offset = 84
    for t in range(tri_count):
        # 12 floats (normal + 3 vertices) + 2 bytes attribute = 50 bytes
        vals = struct.unpack_from("<12fH", data, offset)
        offset += 50
        tri_base = len(verts)
        for v in range(3):
            verts.append((vals[3 + v * 3], vals[4 + v * 3], vals[5 + v * 3]))
        tris.extend([tri_base, tri_base + 1, tri_base + 2])

    return np.array(verts, dtype=np.float64), np.array(tris, dtype=np.int32)


def make_mesh(name, verts, tris):
    mesh = bpy.data.meshes.new(name)
    mesh.vertices.add(len(verts))
    mesh.vertices.foreach_set("co", verts.ravel())
    mesh.loops.add(len(tris))
    mesh.loops.foreach_set("vertex_index", tris.ravel())
    mesh.polygons.add(len(tris) // 3)
    loop_start = np.arange(0, len(tris), 3, dtype=np.int32)
    loop_total = np.full(len(tris) // 3, 3, dtype=np.int32)
    mesh.polygons.foreach_set("loop_start", loop_start)
    mesh.polygons.foreach_set("loop_total", loop_total)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def bounds(obj):
    mn = Vector((1e9, 1e9, 1e9))
    mx = Vector((-1e9, -1e9, -1e9))
    for corner in obj.bound_box:
        w = obj.matrix_world @ Vector(corner)
        mn.x = min(mn.x, w.x); mn.y = min(mn.y, w.y); mn.z = min(mn.z, w.z)
        mx.x = max(mx.x, w.x); mx.y = max(mx.y, w.y); mx.z = max(mx.z, w.z)
    return mn, mx


def vertex_array(obj):
    co = np.empty(len(obj.data.vertices) * 3, dtype=np.float64)
    obj.data.vertices.foreach_get("co", co)
    return co.reshape(len(obj.data.vertices), 3)


def align_principal_axis(obj):
    """Rotate the mesh so its longest axis becomes +Z."""
    co = vertex_array(obj)
    centred = co - co.mean(axis=0)
    values, vectors = np.linalg.eigh(np.cov(centred.T))
    axis = Vector(vectors[:, int(np.argmax(values))].tolist()).normalized()
    quat = axis.rotation_difference(Vector((0.0, 0.0, 1.0)))
    obj.rotation_mode = 'QUATERNION'
    obj.rotation_quaternion = quat
    bpy.ops.object.transform_apply(rotation=True)


def wide_end_is_down(obj):
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


summary = []
for fname in sorted(os.listdir(SRC)):
    if not fname.lower().endswith(".stl"):
        continue

    src = os.path.join(SRC, fname)
    name = os.path.splitext(fname)[0]
    bpy.ops.wm.read_factory_settings(use_empty=True)

    verts, tris = parse_stl(src)
    if len(verts) == 0:
        print(f"SKIP {name}: no vertices")
        continue

    obj = make_mesh(name, verts, tris)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # Stand the part up on its longest axis.
    align_principal_axis(obj)

    # Wide end (nozzle/base) down.
    if not wide_end_is_down(obj):
        obj.rotation_mode = 'XYZ'
        obj.rotation_euler = (math.radians(180), 0.0, 0.0)
        bpy.ops.object.transform_apply(rotation=True)

    # Bottom-centre pivot.
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
    dst = os.path.join(DST, name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=dst,
        use_selection=True,
        apply_unit_scale=True,
        axis_forward='-Z',
        axis_up='Y',
    )

    summary.append((name, dst, dims, len(obj.data.vertices)))

print("BAKE_SUMMARY_START")
for name, dst, dims, verts in summary:
    print(f"{name}: {dst} dims=[{dims.x:.3f},{dims.y:.3f},{dims.z:.3f}] verts={verts}")
print("BAKE_SUMMARY_END")
