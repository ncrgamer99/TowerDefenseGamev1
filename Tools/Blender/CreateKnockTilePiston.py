import math
from pathlib import Path

import bpy
from mathutils import Vector


# Blender axes: X = tile right, Y = path forward, Z = up.
# Current game logic moves enemies forward along the path and knockback sends them
# back to previous path points. This model therefore points its piston toward -Y.


def hex_color(value, alpha=1.0):
    value = value.strip().lstrip("#")
    return (
        int(value[0:2], 16) / 255.0,
        int(value[2:4], 16) / 255.0,
        int(value[4:6], 16) / 255.0,
        alpha,
    )


def make_material(name, color_hex, metallic=0.05, roughness=0.72, emission_hex=None, emission_strength=0.0):
    material = bpy.data.materials.new(name)
    material.diffuse_color = hex_color(color_hex)
    material.use_nodes = True

    bsdf = material.node_tree.nodes.get("Principled BSDF")
    if bsdf is not None:
        if "Base Color" in bsdf.inputs:
            bsdf.inputs["Base Color"].default_value = hex_color(color_hex)
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = metallic
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = roughness

        if emission_hex is not None:
            if "Emission Color" in bsdf.inputs:
                bsdf.inputs["Emission Color"].default_value = hex_color(emission_hex)
            if "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Strength"].default_value = emission_strength

    return material


def assign_material(obj, material):
    if obj.data is not None:
        obj.data.materials.clear()
        obj.data.materials.append(material)


def add_cube(name, location, scale, material, rotation=(0.0, 0.0, 0.0), parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.name = name + "_Mesh"
    obj.scale = scale
    assign_material(obj, material)
    if parent is not None:
        obj.parent = parent
    return obj


def add_cylinder(name, location, radius, depth, material, vertices=32, rotation=(0.0, 0.0, 0.0), parent=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.name = name + "_Mesh"
    assign_material(obj, material)
    if parent is not None:
        obj.parent = parent
    return obj


def add_arrow(name, location, material, parent=None):
    # Arrow points to -Y, the intended knockback direction for a +Y path.
    verts = [
        (-0.11, 0.18, 0.0),
        (0.11, 0.18, 0.0),
        (0.11, -0.08, 0.0),
        (0.22, -0.08, 0.0),
        (0.0, -0.34, 0.0),
        (-0.22, -0.08, 0.0),
        (-0.11, -0.08, 0.0),
    ]
    faces = [(0, 1, 2, 3, 4, 5, 6)]
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    assign_material(obj, material)
    if parent is not None:
        obj.parent = parent
    return obj


def add_empty(name, location=(0.0, 0.0, 0.0), parent=None):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = 0.08
    obj.location = location
    bpy.context.collection.objects.link(obj)
    if parent is not None:
        obj.parent = parent
    return obj


def add_beveled_corner(name, x, y, material, parent=None):
    obj = add_cube(name, (x, y, 0.08), (0.16, 0.055, 0.035), material, rotation=(0.0, 0.0, math.radians(45.0)), parent=parent)
    return obj


def keyframe_transform(obj, frame, location=None, scale=None):
    if location is not None:
        obj.location = location
        obj.keyframe_insert(data_path="location", frame=frame)
    if scale is not None:
        obj.scale = scale
        obj.keyframe_insert(data_path="scale", frame=frame)


def push_current_action_to_nla(obj, strip_name):
    if obj.animation_data is None or obj.animation_data.action is None:
        return

    action = obj.animation_data.action
    track = obj.animation_data.nla_tracks.new()
    track.name = strip_name
    strip = track.strips.new(strip_name, int(action.frame_range[0]), action)
    strip.name = strip_name
    obj.animation_data.action = None


def create_fire_animation(objects):
    rod = objects["PistonRod"]
    head = objects["PistonHead"]
    plate = objects["PistonImpactPlate"]
    core = objects["PistonEnergyCore_Cyan"]

    for obj in (rod, head, plate, core):
        obj.animation_data_create()
        obj.animation_data.action = bpy.data.actions.new("KnockTile_Piston_Fire_" + obj.name)

    rod_start_loc = Vector((-0.22, -0.02, 0.19))
    rod_fire_loc = Vector((-0.22, -0.16, 0.19))
    rod_start_scale = Vector((0.038, 0.22, 0.038))
    rod_fire_scale = Vector((0.038, 0.42, 0.038))
    head_start_loc = Vector((-0.22, -0.27, 0.19))
    head_fire_loc = Vector((-0.22, -0.47, 0.19))
    plate_start_loc = Vector((-0.22, -0.39, 0.19))
    plate_fire_loc = Vector((-0.22, -0.56, 0.19))

    for frame in (1, 20):
        keyframe_transform(rod, frame, rod_start_loc, rod_start_scale)
        keyframe_transform(head, frame, head_start_loc)
        keyframe_transform(plate, frame, plate_start_loc)
    for frame in (5, 8):
        keyframe_transform(rod, frame, rod_fire_loc, rod_fire_scale)
        keyframe_transform(head, frame, head_fire_loc)
        keyframe_transform(plate, frame, plate_fire_loc)
    keyframe_transform(rod, 12, rod_fire_loc + Vector((0.0, 0.025, 0.0)), rod_fire_scale)
    keyframe_transform(head, 12, head_fire_loc + Vector((0.0, 0.025, 0.0)))
    keyframe_transform(plate, 12, plate_fire_loc + Vector((0.0, 0.025, 0.0)))

    for frame, scale in ((1, (0.055, 0.055, 0.055)), (5, (0.08, 0.08, 0.08)), (9, (0.095, 0.095, 0.095)), (20, (0.055, 0.055, 0.055))):
        keyframe_transform(core, frame, scale=scale)

    for obj in (rod, head, plate, core):
        push_current_action_to_nla(obj, "KnockTile_Piston_Fire")


def create_idle_animation(objects):
    core = objects["PistonEnergyCore_Cyan"]
    glow = objects["ArrowGlow_Cyan"]

    for obj in (core, glow):
        obj.animation_data_create()
        obj.animation_data.action = bpy.data.actions.new("KnockTile_Piston_Idle_" + obj.name)

    for frame, scale in ((1, (0.055, 0.055, 0.055)), (30, (0.068, 0.068, 0.068)), (60, (0.055, 0.055, 0.055))):
        keyframe_transform(core, frame, scale=scale)
    for frame, scale in ((1, (0.5, 0.5, 0.5)), (30, (0.58, 0.58, 0.58)), (60, (0.5, 0.5, 0.5))):
        keyframe_transform(glow, frame, scale=scale)

    for obj in (core, glow):
        push_current_action_to_nla(obj, "KnockTile_Piston_Idle")


def build_tile():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()

    scene = bpy.context.scene
    scene.frame_start = 1
    scene.frame_end = 60
    scene.render.fps = 50
    scene.unit_settings.system = "METRIC"

    root = bpy.data.objects.new("KnockTile_Piston", None)
    bpy.context.collection.objects.link(root)
    root.empty_display_type = "CUBE"
    root.empty_display_size = 1.0

    mat_base = make_material("MAT_KnockTile_Base_Dark", "#263442", metallic=0.12, roughness=0.78)
    mat_panel = make_material("MAT_KnockTile_Panel_Dark", "#303D4C", metallic=0.1, roughness=0.72)
    mat_rail = make_material("MAT_KnockTile_Rail_Cyan", "#21D6DF", metallic=0.05, roughness=0.45, emission_hex="#21D6DF", emission_strength=0.65)
    mat_piston = make_material("MAT_KnockTile_Piston_Metal", "#4D5B67", metallic=0.25, roughness=0.58)
    mat_dark = make_material("MAT_KnockTile_Piston_DarkMetal", "#202A34", metallic=0.2, roughness=0.68)
    mat_blue = make_material("MAT_KnockTile_Piston_Blue", "#1664B8", metallic=0.12, roughness=0.52)
    mat_gold = make_material("MAT_KnockTile_Piston_GoldAccent", "#D6A441", metallic=0.18, roughness=0.5)
    mat_glow = make_material("MAT_KnockTile_CyanGlow", "#27E6F2", metallic=0.0, roughness=0.32, emission_hex="#27E6F2", emission_strength=1.2)

    add_cube("TileBase_Dark", (0.0, 0.0, 0.025), (0.5, 0.5, 0.05), mat_base, parent=root)
    add_cube("TileTopPanel_Dark", (0.0, 0.0, 0.078), (0.44, 0.44, 0.025), mat_panel, parent=root)
    add_cube("InnerPanel_Dark", (0.0, 0.0, 0.108), (0.34, 0.34, 0.012), mat_dark, parent=root)

    add_beveled_corner("BeveledCorner_NW", -0.36, 0.36, mat_panel, parent=root)
    add_beveled_corner("BeveledCorner_NE", 0.36, 0.36, mat_panel, parent=root)
    add_beveled_corner("BeveledCorner_SW", -0.36, -0.36, mat_panel, parent=root)
    add_beveled_corner("BeveledCorner_SE", 0.36, -0.36, mat_panel, parent=root)

    add_cube("Rail_North", (0.0, 0.475, 0.135), (0.46, 0.025, 0.055), mat_rail, parent=root)
    add_cube("Rail_East", (0.475, 0.0, 0.135), (0.025, 0.46, 0.055), mat_rail, parent=root)
    add_cube("Rail_South", (0.0, -0.475, 0.135), (0.46, 0.025, 0.055), mat_rail, parent=root)
    add_cube("Rail_West", (-0.475, 0.0, 0.135), (0.025, 0.46, 0.055), mat_rail, parent=root)
    add_cube("RailCorner_NE", (0.475, 0.475, 0.14), (0.035, 0.035, 0.065), mat_rail, parent=root)
    add_cube("RailCorner_NW", (-0.475, 0.475, 0.14), (0.035, 0.035, 0.065), mat_rail, parent=root)
    add_cube("RailCorner_SE", (0.475, -0.475, 0.14), (0.035, 0.035, 0.065), mat_rail, parent=root)
    add_cube("RailCorner_SW", (-0.475, -0.475, 0.14), (0.035, 0.035, 0.065), mat_rail, parent=root)

    add_cube("PistonHousing_Left", (-0.22, 0.14, 0.18), (0.13, 0.16, 0.08), mat_dark, parent=root)
    add_cylinder("PistonMotor_Cylinder", (-0.22, 0.18, 0.2), 0.09, 0.12, mat_blue, vertices=32, rotation=(math.radians(90.0), 0.0, 0.0), parent=root)
    rod = add_cylinder("PistonRod", (-0.22, -0.02, 0.19), 0.038, 0.44, mat_piston, vertices=24, rotation=(math.radians(90.0), 0.0, 0.0), parent=root)
    head = add_cube("PistonHead", (-0.22, -0.27, 0.19), (0.11, 0.045, 0.07), mat_piston, parent=root)
    plate = add_cube("PistonImpactPlate", (-0.22, -0.39, 0.19), (0.18, 0.035, 0.105), mat_piston, parent=root)
    add_cube("PistonMount_Top", (-0.22, 0.31, 0.245), (0.14, 0.025, 0.025), mat_gold, parent=root)
    add_cube("PistonMount_Bottom", (-0.22, 0.02, 0.115), (0.14, 0.025, 0.025), mat_gold, parent=root)

    for i, (x, y) in enumerate(((-0.31, 0.25), (-0.13, 0.25), (-0.31, 0.07), (-0.13, 0.07)), 1):
        add_cylinder("PistonBolts_%02d" % i, (x, y, 0.255), 0.022, 0.014, mat_gold, vertices=16, parent=root)

    arrow = add_arrow("KnockArrow_Cyan", (0.18, -0.02, 0.132), mat_glow, parent=root)
    add_cube("KnockSpeedLine_01", (0.12, 0.25, 0.134), (0.015, 0.12, 0.006), mat_glow, parent=root)
    add_cube("KnockSpeedLine_02", (0.21, 0.20, 0.134), (0.015, 0.09, 0.006), mat_glow, parent=root)
    add_cube("KnockSpeedLine_03", (0.30, 0.15, 0.134), (0.015, 0.065, 0.006), mat_glow, parent=root)
    core = add_cylinder("PistonEnergyCore_Cyan", (-0.22, 0.18, 0.29), 0.055, 0.022, mat_glow, vertices=24, parent=root)
    glow = add_arrow("ArrowGlow_Cyan", (0.18, -0.02, 0.129), mat_glow, parent=root)
    glow.scale = (0.5, 0.5, 0.5)

    # Flow marker names for the existing TileManager path-visual logic; kept invisible on special tiles.
    add_empty("Flow_North", parent=root)
    add_empty("Flow_East", parent=root)
    add_empty("Flow_South", parent=root)
    add_empty("Flow_West", parent=root)

    objects = {
        "PistonRod": rod,
        "PistonHead": head,
        "PistonImpactPlate": plate,
        "PistonEnergyCore_Cyan": core,
        "ArrowGlow_Cyan": glow,
    }
    create_fire_animation(objects)
    create_idle_animation(objects)

    return root


def main():
    repo_root = Path(__file__).resolve().parents[2]
    asset_dir = repo_root / "Assets" / "Art" / "Tiles" / "KnockTile"
    asset_dir.mkdir(parents=True, exist_ok=True)

    build_tile()

    blend_path = asset_dir / "KnockTile_Piston.blend"
    fbx_path = asset_dir / "KnockTile_Piston.fbx"

    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    bpy.ops.export_scene.fbx(
        filepath=str(fbx_path),
        use_selection=False,
        object_types={"EMPTY", "MESH"},
        apply_unit_scale=True,
        bake_anim=True,
        bake_anim_use_all_bones=False,
        bake_anim_use_nla_strips=True,
        bake_anim_use_all_actions=False,
        add_leaf_bones=False,
        axis_forward="-Z",
        axis_up="Y",
    )

    print("Created:")
    print("  " + str(blend_path))
    print("  " + str(fbx_path))


if __name__ == "__main__":
    main()
