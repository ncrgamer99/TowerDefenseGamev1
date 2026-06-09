import math
import os

import bpy


ROOT = r"E:\unity\Projekte\TowerDefenseGame"
OUTPUT_FBX = os.path.join(ROOT, "Assets", "Models", "Generated", "Tiles", "TD_KnockTile.fbx")
OUTPUT_BLEND = os.path.join(ROOT, "Assets", "Art", "Tiles", "KnockTile", "KnockTile_PathSizedPiston.blend")
OUTPUT_PREVIEW = os.path.join(ROOT, "Tools", "Blender", "Previews", "TD_KnockTile_PathSizedPiston.png")


def ensure_dir(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def create_material(name, color, emission=False):
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    material.use_nodes = True

    bsdf = material.node_tree.nodes.get("Principled BSDF")
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Roughness"].default_value = 0.46
        bsdf.inputs["Metallic"].default_value = 0.08
        if emission:
            bsdf.inputs["Emission Color"].default_value = color
            bsdf.inputs["Emission Strength"].default_value = 1.2

    return material


def make_cube(name, location, scale, material, parent=None, rotation=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.name = name + "_Mesh"
    obj.dimensions = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if material is not None:
        obj.data.materials.append(material)
    if parent is not None:
        obj.parent = parent
    return obj


def make_cylinder(name, location, radius, depth, material, parent=None, vertices=32, rotation=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.name = name + "_Mesh"
    if material is not None:
        obj.data.materials.append(material)
    if parent is not None:
        obj.parent = parent
    return obj


def make_empty(name, rotation=(0.0, 0.0, 0.0), parent=None):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "CUBE"
    obj.empty_display_size = 0.2
    obj.rotation_euler = rotation
    bpy.context.collection.objects.link(obj)
    if parent is not None:
        obj.parent = parent
    return obj


def create_path_base(materials):
    make_cube("TD_KnockTile_BasePlate", (0.0, 0.0, 0.04), (1.0, 1.0, 0.08), materials["base"])
    make_cube("TD_KnockTile_Surface", (0.0, 0.0, 0.095), (0.8, 0.8, 0.035), materials["surface"])

    make_cube("TD_KnockTile_Rail_North", (0.0, 0.49, 0.145), (1.08, 0.06, 0.12), materials["edge"])
    make_cube("TD_KnockTile_Rail_South", (0.0, -0.49, 0.145), (1.08, 0.06, 0.12), materials["edge"])
    make_cube("TD_KnockTile_Rail_East", (0.49, 0.0, 0.145), (0.06, 1.08, 0.12), materials["edge"])
    make_cube("TD_KnockTile_Rail_West", (-0.49, 0.0, 0.145), (0.06, 1.08, 0.12), materials["edge"])

    make_cube("TD_KnockTile_Corner_NE", (0.45, 0.45, 0.145), (0.09, 0.09, 0.13), materials["edge"])
    make_cube("TD_KnockTile_Corner_NW", (-0.45, 0.45, 0.145), (0.09, 0.09, 0.13), materials["edge"])
    make_cube("TD_KnockTile_Corner_SE", (0.45, -0.45, 0.145), (0.09, 0.09, 0.13), materials["edge"])
    make_cube("TD_KnockTile_Corner_SW", (-0.45, -0.45, 0.145), (0.09, 0.09, 0.13), materials["edge"])

    make_empty("Flow_North")
    make_empty("Flow_South")
    make_empty("Flow_East")
    make_empty("Flow_West")


def create_directional_ram(group_name, rotation_z, materials):
    group = make_empty(group_name, rotation=(0.0, 0.0, rotation_z))

    make_cube(group_name + "_RamBaseInset", (0.0, 0.065, 0.142), (0.39, 0.50, 0.024), materials["piston_dark"], group)
    make_cube(group_name + "_BackHousing", (0.0, 0.27, 0.202), (0.34, 0.16, 0.085), materials["piston_dark"], group)
    make_cylinder(group_name + "_EnergyCore", (0.0, 0.27, 0.252), 0.072, 0.018, materials["piston_blue"], group)
    make_cube(group_name + "_GuideLeft", (-0.13, 0.02, 0.198), (0.035, 0.42, 0.04), materials["piston_metal"], group)
    make_cube(group_name + "_GuideRight", (0.13, 0.02, 0.198), (0.035, 0.42, 0.04), materials["piston_metal"], group)

    moving = make_empty(group_name + "_Moving", parent=group)
    make_cube(group_name + "_PistonRod", (0.0, 0.02, 0.209), (0.058, 0.43, 0.045), materials["piston_metal"], moving)
    make_cube(group_name + "_ImpactPlate", (0.0, -0.22, 0.218), (0.34, 0.055, 0.075), materials["piston_metal"], moving)
    make_cube(group_name + "_ImpactGlow", (0.0, -0.22, 0.266), (0.22, 0.022, 0.015), materials["cyan"], moving)


def create_tile():
    reset_scene()

    materials = {
        "base": create_material("knock_base", (0.10, 0.12, 0.15, 1.0)),
        "surface": create_material("knock_surface", (0.10, 0.17, 0.25, 1.0)),
        "edge": create_material("knock_edge", (0.36, 0.39, 0.42, 1.0)),
        "cyan": create_material("knock_path_cyan", (0.00, 0.88, 1.00, 1.0), emission=True),
        "piston_dark": create_material("knock_piston_dark", (0.10, 0.17, 0.25, 1.0)),
        "piston_metal": create_material("knock_piston_metal", (0.36, 0.39, 0.42, 1.0)),
        "piston_blue": create_material("knock_piston_blue", (0.00, 0.88, 1.00, 1.0), emission=True),
    }

    create_path_base(materials)
    create_directional_ram("Knock_South", 0.0, materials)
    create_directional_ram("Knock_East", math.pi / 2.0, materials)
    create_directional_ram("Knock_North", math.pi, materials)
    create_directional_ram("Knock_West", -math.pi / 2.0, materials)

    ensure_dir(OUTPUT_BLEND)
    bpy.ops.wm.save_as_mainfile(filepath=OUTPUT_BLEND)

    ensure_dir(OUTPUT_FBX)
    bpy.ops.export_scene.fbx(
        filepath=OUTPUT_FBX,
        use_selection=False,
        object_types={"EMPTY", "MESH"},
        axis_forward="-Z",
        axis_up="Y",
        apply_scale_options="FBX_SCALE_ALL",
        add_leaf_bones=False,
        bake_space_transform=False,
    )


def render_preview():
    ensure_dir(OUTPUT_PREVIEW)
    bpy.ops.object.light_add(type="AREA", location=(0.0, -2.5, 2.6))
    light = bpy.context.object
    light.name = "Preview_AreaLight"
    light.data.energy = 450.0
    light.data.size = 4.0

    bpy.ops.object.camera_add(location=(0.0, -1.85, 1.65), rotation=(math.radians(58.0), 0.0, 0.0))
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 1.55
    bpy.context.scene.camera = camera

    bpy.context.scene.render.engine = "BLENDER_EEVEE"
    bpy.context.scene.render.resolution_x = 1024
    bpy.context.scene.render.resolution_y = 1024
    bpy.context.scene.view_settings.view_transform = "Standard"
    bpy.context.scene.render.filepath = OUTPUT_PREVIEW
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    create_tile()
    render_preview()
