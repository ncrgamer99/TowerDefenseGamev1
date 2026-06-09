import math
import os

import bpy


ROOT = r"E:\unity\Projekte\TowerDefenseGame"
TRAP_TILE_PATH = os.path.join(ROOT, "Assets", "Models", "Generated", "Tiles", "TD_TrapTile.fbx")


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def find_material(material_name_part):
    marker = material_name_part.lower()
    for material in bpy.data.materials:
        if material is not None and marker in material.name.lower():
            return material
    return None


def assign_material(obj, material):
    if obj is None or material is None:
        return

    obj.data.materials.clear()
    obj.data.materials.append(material)


def remove_orange_warning_geometry():
    removed = 0
    for obj in list(bpy.data.objects):
        if obj is None:
            continue

        if "DangerRing" not in obj.name and "WarningPlate" not in obj.name:
            continue

        bpy.data.objects.remove(obj, do_unlink=True)
        removed += 1

    return removed


def remove_existing_extra_spikes():
    removed = 0
    for obj in list(bpy.data.objects):
        if obj is None or "ExtraSpike" not in obj.name:
            continue

        bpy.data.objects.remove(obj, do_unlink=True)
        removed += 1

    return removed


def create_spike(name, x, y, radius, height, rotation_z, material):
    bpy.ops.mesh.primitive_cone_add(
        vertices=4,
        radius1=radius,
        radius2=0.0,
        depth=height,
        location=(x, y, 0.155 + height * 0.5),
        rotation=(0.0, 0.0, rotation_z),
    )

    spike = bpy.context.object
    spike.name = name
    spike.data.name = name + "_Mesh"
    assign_material(spike, material)
    return spike


def add_extra_spikes():
    spike_material = find_material("Trap_Red")
    if spike_material is None:
        spike_material = find_material("Red")

    specs = []

    for index in range(8):
        angle = math.radians(index * 45.0 + 22.5)
        specs.append((0.305 * math.cos(angle), 0.305 * math.sin(angle), 0.038, 0.125, angle))

    for index in range(4):
        angle = math.radians(index * 90.0 + 45.0)
        specs.append((0.105 * math.cos(angle), 0.105 * math.sin(angle), 0.032, 0.105, angle + math.radians(45.0)))

    for index, (x, y, radius, height, rotation_z) in enumerate(specs, start=1):
        create_spike("TD_TrapTile_ExtraSpike_%02d" % index, x, y, radius, height, rotation_z, spike_material)

    return len(specs)


def export_tile():
    bpy.ops.export_scene.fbx(
        filepath=TRAP_TILE_PATH,
        use_selection=False,
        object_types={"EMPTY", "MESH"},
        axis_forward="-Z",
        axis_up="Y",
        apply_scale_options="FBX_SCALE_ALL",
        add_leaf_bones=False,
        bake_space_transform=False,
    )


def main():
    if not os.path.exists(TRAP_TILE_PATH):
        raise FileNotFoundError(TRAP_TILE_PATH)

    reset_scene()
    bpy.ops.import_scene.fbx(filepath=TRAP_TILE_PATH)

    removed_warning = remove_orange_warning_geometry()
    removed_extra_spikes = remove_existing_extra_spikes()
    added = add_extra_spikes()
    export_tile()

    print(
        "TD_TrapTile aktualisiert: Warngeometrie entfernt=%d, alte Extra-Spikes entfernt=%d, neue Spikes=%d"
        % (removed_warning, removed_extra_spikes, added)
    )


if __name__ == "__main__":
    main()
