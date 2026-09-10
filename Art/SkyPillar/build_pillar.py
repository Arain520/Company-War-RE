"""Run with Blender 4.5 --background --python Art/SkyPillar/build_pillar.py."""
import bpy
import math
import json
import uuid
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Art/SkyPillar'
GAME = ROOT / 'Assets/CompanyWarRE/Content/Environment/SkyPillar'
OUT.mkdir(parents=True, exist_ok=True)
(GAME / 'Materials').mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for coll in list(bpy.data.collections):
    if coll.name != 'Collection':
        bpy.data.collections.remove(coll)
model = bpy.data.collections['Collection']
model.name = 'SkyPillar_Editable'
materials = {}
specs = {
    'Pillar_Graphite': ((0.105, 0.128, 0.16), .65, .39),
    'Pillar_Armor': ((0.205, 0.235, 0.28), .55, .43),
    'Pillar_Deck': ((0.43, 0.46, 0.49), .35, .55),
    'Pillar_Steel': ((0.32, 0.36, 0.4), .75, .31),
    'Pillar_Gold': ((0.68, 0.43, 0.115), .7, .32),
    'Pillar_Recess': ((0.027, 0.037, 0.049), .3, .6),
    'Pillar_Light': ((1.0, .63, .22), .15, .26),
}
for name, (rgb, metal, rough) in specs.items():
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*rgb, 1)
    mat.use_nodes = True
    node = mat.node_tree.nodes.get('Principled BSDF')
    node.inputs['Base Color'].default_value = (*rgb, 1)
    node.inputs['Metallic'].default_value = metal
    node.inputs['Roughness'].default_value = rough
    if name == 'Pillar_Light':
        node.inputs['Emission Color'].default_value = (*rgb, 1)
        node.inputs['Emission Strength'].default_value = 2.5
    materials[name.split('_', 1)[1]] = mat

def finish(obj, name, mat, bevel):
    obj.name = name
    obj.data.materials.append(materials[mat])
    if bevel:
        mod = obj.modifiers.new('Machined edge', 'BEVEL')
        mod.width = bevel
        mod.segments = 2
        mod = obj.modifiers.new('Weighted face normals', 'WEIGHTED_NORMAL')
        mod.keep_sharp = True
        mod.weight = 40
    return obj

def box(name, loc, size, mat='Armor', bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.object
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, mat, bevel)

def octagon(name, width, depth, z0, z1, cut, mat, bevel=.025):
    x, y = width/2, depth/2
    ring = [(-x+cut,-y),(x-cut,-y),(x,-y+cut),(x,y-cut),
            (x-cut,y),(-x+cut,y),(-x,y-cut),(-x,-y+cut)]
    verts = [(a,b,z) for z in (z0,z1) for a,b in ring]
    faces = [tuple(reversed(range(8))), tuple(range(8,16))]
    faces += [(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name,mesh)
    model.objects.link(obj)
    return finish(obj,name,mat,bevel)

def face_box(name, side, u, outward, z, width, thickness, height, mat='Armor', bevel=.02):
    angle = side*math.pi/2
    obj = box(name, (u*math.cos(angle)-outward*math.sin(angle),
                    u*math.sin(angle)+outward*math.cos(angle),z),
              (width,thickness,height),mat,bevel)
    obj.rotation_euler.z = angle
    return obj

octagon('Core_Chamfered',3.64,3.64,0,15.68,.23,'Graphite',.035)
octagon('Base_Shoe',3.83,3.83,0,.3,.24,'Steel')
octagon('Crown_Shadow_Reveal',3.9,3.9,15.35,15.65,.25,'Recess')
octagon('Crown_Armored_Rim',4,4,15.64,15.9,.27,'Steel',.045)
octagon('Deck_Underlay',3.83,3.83,15.89,15.96,.23,'Recess',.008)
# The walkable deck is a planar 3 x 3 grid, with clipped outer corners.
for ix in range(3):
    for iy in range(3):
        x,y = (ix-1)*1.23,(iy-1)*1.23
        tile=octagon('Deck_Panel_%d_%d'%(ix,iy),1.2,1.2,15.95,16,.11 if ix!=1 and iy!=1 else .035,'Deck',.012)
        tile.location.x=x
        tile.location.y=y
        for dx,dy in [(-.47,-.47),(.47,.47)]:
            box('Deck_Fastener',(x+dx,y+dy,16.005),(.06,.06,.012),'Steel',.008)
# Four identical facades allow rotation and reuse in a board layout.
for side in range(4):
    for lane in [-1,0,1]:
        for tier, (z,h) in enumerate([(2.1,3.5),(6.02,4.23),(10.42,4.43),(14.25,3.08)]):
            face_box('Facade_%d_%d_%d'%(side,lane,tier),side,lane*.91,1.83,z,.85,.09,h,'Armor' if lane else 'Graphite',.018)
    for u in [-1.61,1.61]:
        face_box('Corner_Spine',side,u,1.865,7.72,.24,.20,14.8,'Steel',.035)
        face_box('Spine_Inner_Reveal',side,u*.85,1.889,8,.07,.016,14.5,'Recess',.006)
        face_box('Upper_Gold_Collar',side,u,1.9,15.39,.25,.24,.46,'Gold',.035)
        face_box('Collar_Downstand',side,u,1.97,14.9,.065,.055,.68,'Gold',.01)
    face_box('Crown_Center_Plate',side,0,1.958,15.74,2.6,.11,.16,'Armor',.025)
    for u in [-.58,.58]:
        face_box('Upper_Recess',side,u,1.9,14.53,.14,.08,.87,'Recess',.018)
        face_box('Warm_Status_Light',side,u,1.946,14.65,.042,.018,.5,'Light',.006)
    for j in range(5):
        face_box('Lower_Vent',side,.52,1.9,1.05+j*.12,.49,.035,.046,'Recess',.007)
    face_box('Lower_Service_Panel',side,-.38,1.908,1.33,.76,.035,.87,'Steel',.02)
    face_box('Service_Inset',side,-.38,1.931,1.33,.61,.015,.70,'Graphite',.012)
    face_box('Service_Latch',side,-.16,1.947,1.36,.045,.03,.17,'Gold',.005)
    for u in [-1.29,1.29]:
        # Flush steel tie plates bridge the seams on the deck edge.
        face_box('Deck_Edge_Clamp',side,u,1.80,16.025,.26,.26,.05,'Steel',.018)
    face_box('Deck_Gold_Index',side,.92,1.76,16.032,.32,.043,.014,'Gold',.004)

# Keep the construction pieces editable; export a merged copy with seven slots.
bpy.ops.object.select_all(action='DESELECT')
for obj in model.objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = next(iter(model.objects))
bpy.ops.object.duplicate()
export_parts = list(bpy.context.selected_objects)
for obj in export_parts:
    bpy.context.view_layer.objects.active=obj
    for mod in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.context.view_layer.objects.active=export_parts[0]
bpy.ops.object.join()
asset=bpy.context.object
asset.name='SM_SkyPillar_A'
bpy.context.scene.cursor.location=(0,0,0)
bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
asset.data.calc_loop_triangles()
stats={'triangles':len(asset.data.loop_triangles),'vertices':len(asset.data.vertices),
       'dimensions_m':list(asset.dimensions),'material_slots':len(asset.data.materials),
       'pivot':'bottom center','uv':'UV0 smart projected; no texture dependency'}
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(island_margin=.015)
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.export_scene.fbx(filepath=str(GAME/'SM_SkyPillar_A.fbx'),use_selection=True,
    object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_UNITS',use_mesh_modifiers=True,
    bake_anim=False,add_leaf_bones=False,path_mode='AUTO')
bpy.data.objects.remove(asset,do_unlink=True)
(OUT/'model_stats.json').write_text(json.dumps(stats,indent=2),encoding='utf-8')

# URP/Lit assets and explicit FBX material remaps for this Unity project.
def guid(key):
    return uuid.uuid5(uuid.NAMESPACE_URL,'companywar/sky-pillar/'+key).hex
remaps=[]
for name,(rgb,metal,rough) in specs.items():
    mg=guid(name)
    emission=name=='Pillar_Light'
    color='{r: %s, g: %s, b: %s, a: 1}'%rgb
    glow='{r: 2.5, g: 1.575, b: 0.55, a: 1}' if emission else '{r: 0, g: 0, b: 0, a: 1}'
    yaml='''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: NAME
  m_Shader: {fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}
  m_ValidKeywords: KEYWORDS
  m_InvalidKeywords: []
  m_LightmapFlags: FLAGS
  m_EnableInstancingVariants: 1
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap:
    RenderType: Opaque
  disabledShaderPasses: []
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs: []
    m_Ints: []
    m_Floats:
    - _Metallic: METAL
    - _Smoothness: SMOOTH
    - _Surface: 0
    - _Blend: 0
    - _Cull: 2
    - _SrcBlend: 1
    - _DstBlend: 0
    - _ZWrite: 1
    - _AlphaClip: 0
    - _ReceiveShadows: 1
    - _SpecularHighlights: 1
    - _EnvironmentReflections: 1
    m_Colors:
    - _BaseColor: COLOR
    - _Color: COLOR
    - _EmissionColor: GLOW
'''
    for key,val in {'NAME':name,'KEYWORDS':'[_EMISSION]' if emission else '[]','FLAGS':'2' if emission else '4','METAL':str(metal),'SMOOTH':str(1-rough),'COLOR':color,'GLOW':glow}.items():
        yaml=yaml.replace(key,val)
    (GAME/'Materials'/f'{name}.mat').write_text(yaml,encoding='utf-8')
    (GAME/'Materials'/f'{name}.mat.meta').write_text(f'fileFormatVersion: 2\nguid: {mg}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')
    remaps.append(f'  - first:\n      type: UnityEngine:Material\n      assembly: UnityEngine.CoreModule\n      name: {name}\n    second: {{fileID: 2100000, guid: {mg}, type: 2}}')
meta=f'''fileFormatVersion: 2
guid: {guid('fbx')}
ModelImporter:
  serializedVersion: 22200
  internalIDToNameTable: []
  externalObjects:
{chr(10).join(remaps)}
  materials:
    materialImportMode: 2
    materialName: 0
    materialSearch: 1
    materialLocation: 1
  meshes:
    globalScale: 1
    meshCompression: 0
    addColliders: 0
    useFileUnits: 1
    generateSecondaryUV: 1
    useFileScale: 1
  tangentSpace:
    normalImportMode: 0
    tangentImportMode: 3
  importAnimation: 0
  animationType: 0
  isReadable: 0
  userData:
  assetBundleName:
  assetBundleVariant:
'''
(GAME/'SM_SkyPillar_A.fbx.meta').write_text(meta,encoding='utf-8')

# Independent studio presentation, excluded from the exported FBX.
studio=bpy.data.collections.new('Studio_Preview_Only')
bpy.context.scene.collection.children.link(studio)
def move_to_studio(obj):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    studio.objects.link(obj)

floor=box('Studio_Ground',(0,0,-.15),(200,200,.2),'Graphite',0)
move_to_studio(floor)
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1
scene.render.engine='CYCLES'
scene.cycles.samples=48
scene.cycles.use_denoising=True
scene.world.color=(.23,.23,.23)
scene.view_settings.view_transform='AgX'
for name,loc,power,color,size in [
    ('Key', (4,-8,23),4300,(1,.84,.67),10),
    ('Fill',(-8,-3,12),2700,(.65,.78,1),9),
    ('Rim',(4,7,19),5200,(1,.9,.7),8)]:
    bpy.ops.object.light_add(type='AREA',location=loc)
    obj=bpy.context.object
    obj.name=name
    obj.data.energy=power
    obj.data.color=color
    obj.data.shape='DISK'
    obj.data.size=size
    obj.rotation_euler=(Vector((0,0,9))-obj.location).to_track_quat('-Z','Y').to_euler()
    move_to_studio(obj)
bpy.ops.object.camera_add(location=(24,-32,29))
cam=bpy.context.object
cam.name='Camera_Hero'
cam.rotation_euler=(Vector((0,0,8.1))-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.type='ORTHO'
cam.data.ortho_scale=21
move_to_studio(cam)
scene.camera=cam
scene.render.resolution_x=1000
scene.render.resolution_y=1200
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
bpy.ops.object.select_all(action='DESELECT')
for obj in model.objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active=next(iter(model.objects))
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA'
scene.render.filepath=str(OUT/'SkyPillar_Hero.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'SkyPillar_A.blend'))
bpy.ops.render.render(write_still=True)
cam.location=(8,-10,24)
cam.rotation_euler=(Vector((0,0,14.8))-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.ortho_scale=7.8
scene.render.resolution_x=1100
scene.render.resolution_y=1000
scene.render.filepath=str(OUT/'SkyPillar_Detail.png')
bpy.ops.render.render(write_still=True)
print('SKYPILLAR_DONE',json.dumps(stats))
