import os

import bpy


ROOT = r"E:\unity\Projekte\TowerDefenseGame"
TILE_MODEL_FOLDER = os.path.join(ROOT, "Assets", "Models", "Generated", "Tiles")

KEEP_FLOW_VISIBLE = {
    "TD_PathTile.fbx",
    "TD_PathGhostTile.fbx",
}

TARGET_FILES = {
    "TD_StartTile.fbx",
    "TD_TrapTile.fbx",
    "TD_SlowTile.fbx",
    "TD_KnockTile.fbx",
    "TD_ComboTile.fbx",
    "TD_SpecialTile.fbx",
    "TD_BridgeTile.fbx",
    "TD_GoldTile.fbx",
    "TD_WeakpointTile.fbx",
}

FLOW_MARKER_PARTS = (
    "_Flow_",
    "PathCenterLine",
    "Weakpoint_Path_Line",
)


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()

    for material in list(bpy.data.materials):
        bpy.data.materials.remove(material)


def is_visible_flow_marker(obj):
    if obj is None or obj.type != "MESH":
        return False

    return any(part in obj.name for part in FLOW_MARKER_PARTS)


def replace_mesh_with_empty(obj):
    parent = obj.parent
    children = list(obj.children)
    local_matrix = obj.matrix_local.copy()
    name = obj.name

    empty = bpy.data.objects.new(name + "_EmptyReplacement", None)
    empty.empty_display_type = "PLAIN_AXES"
    empty.empty_display_size = 0.08
    bpy.context.collection.objects.link(empty)
    empty.parent = parent
    empty.matrix_local = local_matrix

    for child in children:
        child.parent = empty

    bpy.data.objects.remove(obj, do_unlink=True)
    empty.name = name


def normalize_empty_marker_names():
    renamed = 0
    for obj in list(bpy.data.objects):
        if obj is None or obj.type != "EMPTY":
            continue

        if not any(part in obj.name for part in FLOW_MARKER_PARTS):
            continue

        if not obj.name.endswith(".001"):
            continue

        desired_name = obj.name[:-4]
        if bpy.data.objects.get(desired_name) is not None:
            continue

        obj.name = desired_name
        renamed += 1

    return renamed


def process_model(file_name):
    if file_name in KEEP_FLOW_VISIBLE:
        return False

    if file_name not in TARGET_FILES:
        return False

    path = os.path.join(TILE_MODEL_FOLDER, file_name)
    if not os.path.exists(path):
        return False

    reset_scene()
    bpy.ops.import_scene.fbx(filepath=path)

    markers = [obj for obj in list(bpy.data.objects) if is_visible_flow_marker(obj)]
    renamed = normalize_empty_marker_names()
    if not markers and renamed <= 0:
        print(file_name + ": keine sichtbaren Flow-Marker gefunden.")
        return False

    for marker in markers:
        replace_mesh_with_empty(marker)

    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=False,
        object_types={"EMPTY", "MESH"},
        axis_forward="-Z",
        axis_up="Y",
        apply_scale_options="FBX_SCALE_ALL",
        add_leaf_bones=False,
        bake_space_transform=False,
    )

    print(file_name + ": sichtbare Flow-Marker entfernt: " + str(len(markers)) + ", Marker-Namen normalisiert: " + str(renamed))
    return True


if __name__ == "__main__":
    changed = 0
    for file_name in sorted(TARGET_FILES):
        if process_model(file_name):
            changed += 1

    print("Fertig. Geaenderte FBX-Dateien: " + str(changed))
