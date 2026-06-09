import os
import sys

import bpy
from mathutils import Vector


ROOT = r"E:\unity\Projekte\TowerDefenseGame"
TILES = [
    "TD_PathTile",
    "TD_GoldTile",
    "TD_TrapTile",
    "TD_KnockTile",
]


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def world_bounds(obj):
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    minimum = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    maximum = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    return minimum, maximum, maximum - minimum


def material_name(obj):
    if not hasattr(obj.data, "materials") or not obj.data.materials:
        return "-"
    material = obj.data.materials[0]
    return material.name if material else "-"


def inspect_tile(tile_name):
    reset_scene()
    path = os.path.join(ROOT, "Assets", "Models", "Generated", "Tiles", tile_name + ".fbx")
    print(f"\n=== {tile_name} ===")
    print(path)
    bpy.ops.import_scene.fbx(filepath=path)

    for obj in sorted(bpy.context.scene.objects, key=lambda item: item.name.lower()):
        if obj.type != "MESH":
            continue

        minimum, maximum, size = world_bounds(obj)
        print(
            f"{obj.name:34s} mat={material_name(obj):24s} "
            f"loc=({obj.location.x:.3f},{obj.location.y:.3f},{obj.location.z:.3f}) "
            f"rot=({obj.rotation_euler.x:.3f},{obj.rotation_euler.y:.3f},{obj.rotation_euler.z:.3f}) "
            f"size=({size.x:.3f},{size.y:.3f},{size.z:.3f}) "
            f"min=({minimum.x:.3f},{minimum.y:.3f},{minimum.z:.3f}) "
            f"max=({maximum.x:.3f},{maximum.y:.3f},{maximum.z:.3f})"
        )


if __name__ == "__main__":
    for tile in TILES:
        inspect_tile(tile)
