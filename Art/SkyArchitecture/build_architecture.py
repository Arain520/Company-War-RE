"""Blender 4.5: build a modular architecture kit matching SkyPillar A."""
import ast
import bpy
import json
import math
import uuid
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT/'Art/SkyArchitecture'
GAME = ROOT/'Assets/CompanyWarRE/Content/Environment/SkyArchitecture'
OUT.mkdir(parents=True, exist_ok=True)
GAME.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Art/SkyPillar/SkyPillar_A.blend'))
materials={m.name.split('_',1)[1]:m for m in bpy.data.materials if m.name.startswith('Pillar_')}
# Reuse only geometry function definitions, without rerunning the original build.
tree=ast.parse((ROOT/'Art/SkyPillar/build_pillar.py').read_text(encoding='utf-8'))
defs=ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in ('finish','box','octagon','face_box')],type_ignores=[])
exec(compile(defs,'pillar_geometry','exec'),globals())
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for coll in list(bpy.data.collections):
    bpy.data.collections.remove(coll)
scene=bpy.context.scene
scene.name='Architecture_Library'
stats=[]
meshes={}
collections={}
guid=lambda name:uuid.uuid5(uuid.NAMESPACE_URL,'companywar/sky-architecture/'+name).hex

def begin(name):
    global model
    model=bpy.data.collections.new(name)
    scene.collection.children.link(model)
    bpy.context.view_layer.active_layer_collection=bpy.context.view_layer.layer_collection.children[name]
    collections[name]=model

def rod(name,a,b,r=.045,mat='Gold',vertices=8):
    a,b=Vector(a),Vector(b)
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=(b-a).length,location=(a+b)/2)
    obj=bpy.context.object
    obj.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    return finish(obj,name,mat,0)

def rail(a,b,height=.85):
    a,b=Vector(a),Vector(b)
    n=max(1,math.ceil((b-a).length/1.6))
    for i in range(n+1):
        p=a.lerp(b,i/n)
        rod('Rail_Post',p,p+Vector((0,0,height)),.04)
        box('Rail_Foot',p,(.17,.17,.055),'Steel',.01)
    rod('Handrail',a+Vector((0,0,height)),b+Vector((0,0,height)),.047)
    rod('Midrail',a+Vector((0,0,height*.48)),b+Vector((0,0,height*.48)),.027)

def deck(length,width,tiles=6):
    box('Bridge_Underside',(0,0,-.30),(length,width,.5),'Graphite',.06)
    for i in range(tiles):
        for j in [-1,1]:
            box('Walkway_Tile',(-length/2+(i+.5)*length/tiles,j*width/4,-.045),
                (length/tiles-.035,width/2-.04,.09),'Deck',.014)
    for y in [-width/2,width/2]:
        box('Edge_Beam',(0,y,-.24),(length,.16,.42),'Steel',.025)
        box('Gold_Edge_Line',(0,y,0),(length,.04,.014),'Gold',.004)
    for x in [-length/2+.13,length/2-.13]:
        box('Connection_Sill',(x,0,-.015),(.20,width,.07),'Steel',.012)

def export_asset(name,description,pivot):
    originals=list(model.objects)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in originals: obj.select_set(True)
    bpy.context.view_layer.objects.active=originals[0]
    bpy.ops.object.duplicate()
    dup=list(bpy.context.selected_objects)
    for obj in dup:
        bpy.context.view_layer.objects.active=obj
        for modifier in list(obj.modifiers): bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.context.view_layer.objects.active=dup[0]
    bpy.ops.object.join()
    obj=bpy.context.object
    obj.name=name
    bpy.context.scene.cursor.location=(0,0,0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(island_margin=.015)
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.data.calc_loop_triangles()
    corners=[obj.matrix_world@Vector(c) for c in obj.bound_box]
    stats.append({'name':name,'description':description,'pivot':pivot,'triangles':len(obj.data.loop_triangles),
        'dimensions_m':[round(v,4) for v in obj.dimensions],
        'bounds_min':[min(c[i] for c in corners) for i in range(3)],
        'bounds_max':[max(c[i] for c in corners) for i in range(3)],
        'materials':[m.name for m in obj.data.materials]})
    bpy.ops.export_scene.fbx(filepath=str(GAME/(name+'.fbx')),use_selection=True,object_types={'MESH'},
        axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
        bake_anim=False,add_leaf_bones=False)
    meta=(ROOT/'Assets/CompanyWarRE/Content/Environment/SkyPillar/SM_SkyPillar_A.fbx.meta').read_text(encoding='utf-8')
    meta=meta.replace('d72ae69107315a239fefcad7f344f620',guid(name))
    (GAME/(name+'.fbx.meta')).write_text(meta,encoding='utf-8')
    meshes[name]=obj.data
    obj.data.use_fake_user=True
    bpy.data.objects.remove(obj,do_unlink=True)
    root=bpy.data.objects.new(name+'_Root',None)
    model.objects.link(root)
    for obj in originals: obj.parent=root
    # Presentation layout; roots are the reusable local origins.
    idx=len(stats)-1
    root.location=((idx%4)*12,(idx//4)*15,0)
    return root

begin('SM_SkyBridge_8m')
deck(8,3,8)
for y in [-1.48,1.48]:
    rail((-4,y,0),(4,y,0))
    for x in [-3,-1,1,3]:
        rod('Underdeck_Diagonal',(x-1,y,-.48),(x+1,y,-1.1),.065,'Steel')
        rod('Underdeck_Diagonal',(x-1,y,-1.1),(x+1,y,-.48),.065,'Steel')
    box('Truss_Chord',(0,y,-1.1),(8,.13,.13),'Graphite',.015)
export_asset('SM_SkyBridge_8m','8 m main bridge, 3 m wide, gold rails and underdeck truss','walkway center; ends X +/-4; deck Z=0')

begin('SM_SkyCatwalk_8m')
deck(8,1.25,8)
for y in [-.61,.61]: rail((-4,y,0),(4,y,0),.75)
export_asset('SM_SkyCatwalk_8m','8 m narrow service catwalk','walkway center; ends X +/-4; deck Z=0')

begin('SM_SkyLanding_4m')
octagon('Landing_Structure',4,4,-.6,-.1,.23,'Graphite',.035)
for x in [-1.47,-.49,.49,1.47]:
    for y in [-1.47,-.49,.49,1.47]:
        box('Landing_Tile',(x,y,-.05),(.955,.955,.1),'Deck',.018)
for y in [-1.9,1.9]:
    rail((-1.8,y,0),(1.8,y,0))
for x in [-1.8,1.8]:
    for y in [-1.8,1.8]: box('Corner_Gold_Cap',(x,y,-.18),(.3,.3,.38),'Gold',.045)
export_asset('SM_SkyLanding_4m','4 m endpoint landing, open on X ends','walkway center; deck Z=0')

begin('SM_SkyStairs_1m')
for i in range(6):
    h=(i+1)/6
    box('Stair_Riser',(-1.25+i*.5,0,(h-.05)/2),(.5,2.4,h-.05),'Armor',.015)
    box('Tread',(-1.25+i*.5,0,h-.025),(.48,2.4,.05),'Deck',.008)
    box('Tread_Gold_Nose',(-1.475+i*.5,0,h+.005),(.03,2.38,.01),'Gold',.002)
for y in [-1.18,1.18]: rail((-1.48,y,1/6),(1.48,y,1),.8)
export_asset('SM_SkyStairs_1m','6 steps, 1 m rise, 3 m run, 2.4 m width','lower mounting plane; ascends in +X; top Z=1')

begin('SM_SkyCommandRoom')
octagon('Room_Base',3.25,3.05,0,.18,.18,'Steel')
box('Room_Dark_Core',(0,0,1.22),(2.8,2.6,2.12),'Graphite',.1)
for side in range(4):
    face_box('Window_Recess',side,0,1.34,1.22,1.98,.08,1.20,'Recess')
    face_box('Interior_Light',side,0,1.39,1.32,.62,.018,.57,'Light',.035)
    for u in [-1.12,1.12]:
        face_box('White_Frame_Post',side,u,1.43,1.23,.25,.26,2.14,'Deck',.035)
    for z in [.32,2.14]: face_box('White_Frame_Lintel',side,0,1.43,z,2.48,.26,.24,'Deck',.035)
    face_box('Window_Sill',side,0,1.5,.72,1.98,.22,.11,'Gold',.015)
    face_box('Window_Mullion',side,.38,1.45,1.45,.075,.10,1.1,'Steel',.009)
octagon('Roof_Coping',3.25,3.25,2.28,2.47,.17,'Deck',.045)
box('Roof_Inset',(0,0,2.48),(2.68,2.68,.035),'Armor',.012)
box('Roof_Hatch',(0,0,2.55),(.85,.85,.13),'Deck',.025)
box('Roof_Status',(0,0,2.63),(.43,.43,.025),'Gold',.012)
for x in [-1.41,1.41]:
    for y in [-1.41,1.41]: box('Gold_Frame_Corner',(x,y,2.3),(.2,.2,.35),'Gold',.025)
export_asset('SM_SkyCommandRoom','white observation / command room with illuminated inset windows','roof mounting base Z=0; place at pillar height 16')

begin('SM_SkyServiceRoom')
box('Service_Base',(0,0,.10),(2.9,2.6,.2),'Steel',.04)
box('White_Service_Shell',(0,0,.88),(2.6,2.3,1.5),'Deck',.06)
box('Door_Frame',(0,-1.18,.78),(.85,.11,1.30),'Steel',.02)
box('Door',(0,-1.247,.78),(.68,.035,1.12),'Graphite',.015)
box('Door_Gold_Latch',(.22,-1.28,.78),(.05,.04,.2),'Gold',.006)
for x in [-.85,.85]: box('Front_Window',(x,-1.174,1.11),(.49,.035,.38),'Recess',.025)
for x in [-1.315,1.315]:
    for y in [-.66,0,.66]: box('Side_Vent',(x,y,.85),(.035,.40,.64),'Armor',.012)
box('Roof_Slab',(0,0,1.7),(2.8,2.5,.16),'Deck',.03)
box('Roof_Machine',(0,.18,1.89),(.85,.78,.23),'Steel',.04)
box('Roof_Machine_Gold',(0,.18,2.02),(.5,.4,.035),'Gold',.008)
rail((-1.23,1.06,1.79),(1.23,1.06,1.79),.58)
for y in [-.42,.42]: rod('Side_Ladder_Rail',(1.43,y,.18),(1.43,y,1.82),.035)
for z in [.35,.65,.95,1.25,1.55]: rod('Ladder_Rung',(1.43,-.42,z),(1.43,.42,z),.027,'Steel')
export_asset('SM_SkyServiceRoom','white service hut with ladder, vents and roof safety rail','roof mounting base Z=0')

begin('SM_SkyAntenna')
octagon('Mast_Foot',1.35,1.35,0,.18,.2,'Steel')
box('Mast_Base',(0,0,.56),(.63,.63,.85),'Deck',.045)
rod('Main_Mast',(0,0,.9),(0,0,4.55),.15,'Graphite',12)
for z in [1.22,2.4,3.7,4.4]:
    rod('Gold_Collar',(0,0,z-.055),(0,0,z+.055),.22,'Gold',12)
for angle in [0,2*math.pi/3,4*math.pi/3]:
    x,y=math.cos(angle),math.sin(angle)
    rod('Foot_Brace',(x*.58,y*.58,.18),(0,0,1.1),.055,'Steel')
    for z in [3.4,4.0]:
        rod('Antenna_Arm',(0,0,z),(.78*x,.78*y,z+.12),.045,'Gold')
        panel=box('Antenna_Blade',(.82*x,.82*y,z+.16),(.65,.20,.08),'Deck',.025)
        panel.rotation_euler=(0,-.22,angle)
    box('Mast_Status_Light',(.13*x,.13*y,2.86),(.055,.055,.45),'Light',.008)
export_asset('SM_SkyAntenna','three-way antenna mast with two tiers of blades','roof mounting base Z=0')

begin('SM_SkyMachinery')
box('Equipment_Skid',(0,0,.09),(2.8,2.4,.18),'Steel',.03)
for x in [-.7,.7]:
    box('HVAC_Housing',(x,.18,.55),(1.05,1.3,.8),'Armor',.04)
    bpy.ops.mesh.primitive_cylinder_add(vertices=24,radius=.40,depth=.05,location=(x,.18,.98))
    finish(bpy.context.object,'Fan_Recess','Recess',.008)
    for angle in [0,math.pi/2,math.pi,3*math.pi/2]:
        blade=box('Fan_Blade',(x+math.cos(angle)*.16,.18+math.sin(angle)*.16,1.02),(.34,.105,.035),'Steel',.008)
        blade.rotation_euler.z=angle+.35
    rod('Fan_Hub',(x,.18,.98),(x,.18,1.05),.09,'Gold',12)
    for z in [.38,.5,.62,.74]: box('HVAC_Louver',(x,-.481,z),(.77,.025,.044),'Recess',.006)
rod('Utility_Pipe',(-1.12,.9,.35),(1.12,.9,.35),.07,'Gold')
for x in [-1.12,1.12]: rod('Pipe_Elbow',(x,.9,.35),(x,.9,.84),.07,'Gold')
box('Control_Box',(0,-.91,.32),(.5,.37,.43),'Deck',.025)
export_asset('SM_SkyMachinery','twin rooftop fan units, pipework and controls','roof mounting base Z=0')

def tower(width,height):
    octagon('Tower_Core',width,width,0,height,.2,'Graphite',.035)
    for side in range(4):
        for u in [-width*.36,0,width*.36]:
            face_box('Tower_Vertical_Fin',side,u,width/2,height/2,width*.10,.14,height,'Armor',.025)
        for z in [height*.22,height*.49,height*.74]:
            face_box('Tower_Horizontal_Seam',side,0,width/2+.075,z,width*.82,.04,.045,'Recess',.005)
        for u in [-width*.29,width*.29]:
            face_box('Tower_Window_Slot',side,u,width/2+.08,height-1.25,.09,.035,.9,'Gold',.008)
    octagon('Tower_Cap',width+.15,width+.15,height,height+.15,.2,'Steel')
    box('Rooftop_Block',(-width*.18,0,height+.48),(width*.38,width*.5,.65),'Armor',.03)
    rod('Tower_Aerial',(width*.24,width*.24,height+.15),(width*.24,width*.24,height+1.35),.025,'Steel')

begin('SM_SkyTower_Slim24m')
tower(2.9,24)
export_asset('SM_SkyTower_Slim24m','slender 24 m background tower','bottom center Z=0')

begin('SM_SkyTower_Monolith36m')
tower(5.6,36)
for side in range(4):
    for u in [-1.9,1.9]: face_box('Monolith_Buttress',side,u,2.88,16.5,.42,.32,33,'Steel',.04)
export_asset('SM_SkyTower_Monolith36m','36 m broad background monolith with buttresses','bottom center Z=0')

begin('SM_SkyTower_Split28m')
tower(3.8,24)
for x,z in [(-.97,28),(.97,26.4)]:
    box('Stepped_Crown',(x,0,(24+z)/2),(1.68,3.65,z-24),'Armor',.06)
    box('Crown_Top',(x,0,z+.05),(1.75,3.72,.1),'Steel',.025)
    box('Crown_Window',(x,-1.837,z-.62),(.15,.025,.5),'Gold',.008)
export_asset('SM_SkyTower_Split28m','stepped twin crown background tower','bottom center Z=0')

begin('SM_SkyRing_Quarter_R7m')
# True continuous annular deck, with flush radial ends for four-way assembly.
segments=24
r0,r1=6.4,7.6
verts=[]
for i in range(segments+1):
    a=i*math.pi/2/segments
    verts += [(r*math.cos(a),r*math.sin(a),z) for z in [-.24,0] for r in [r0,r1]]
faces=[]
for i in range(segments):
    a=i*4;b=a+4
    faces.extend([(a,b,b+1,a+1),(a+2,a+3,b+3,b+2),(a,a+2,b+2,b),(a+1,b+1,b+3,a+3)])
faces += [(0,1,3,2),(segments*4,segments*4+2,segments*4+3,segments*4+1)]
mesh=bpy.data.meshes.new('Ring_Deck')
mesh.from_pydata(verts,[],faces);mesh.update()
obj=bpy.data.objects.new('Ring_Deck',mesh);model.objects.link(obj)
finish(obj,'Ring_Deck','Deck',.007)
for i in range(12):
    a=i*math.pi/24;b=(i+1)*math.pi/24
    for r in [r0+.04,r1-.04]:
        rail((r*math.cos(a),r*math.sin(a),0),(r*math.cos(b),r*math.sin(b),0),.67)
    rod('Radial_Deck_Joint',(r0*math.cos(a),r0*math.sin(a),.003),(r1*math.cos(a),r1*math.sin(a),.003),.009,'Graphite')
for a in [.2,.75,1.3]:
    rod('Ring_Support',(2.8*math.cos(a),2.8*math.sin(a),-2),(6.65*math.cos(a),6.65*math.sin(a),-.25),.085,'Steel')
export_asset('SM_SkyRing_Quarter_R7m','90 degree ring walkway, center radius 7 m, clear deck width 1.2 m','ring center, walkway Z=0; rotate 0/90/180/270 degrees to complete ring')

(OUT/'asset_manifest.json').write_text(json.dumps(stats,indent=2),encoding='utf-8')

def studio(target,camloc,scale,res):
    target.render.engine='CYCLES'
    target.cycles.samples=32
    target.cycles.use_denoising=True
    target.world=bpy.data.worlds.new(target.name+'_World')
    target.world.use_nodes=True
    target.world.node_tree.nodes['Background'].inputs[0].default_value=(.25,.32,.43,1)
    target.world.node_tree.nodes['Background'].inputs[1].default_value=.45
    target.view_settings.view_transform='AgX'
    target.render.resolution_x=res[0];target.render.resolution_y=res[1]
    target.render.resolution_percentage=100
    target.render.image_settings.file_format='PNG'
    camdata=bpy.data.cameras.new(target.name+'_Camera')
    cam=bpy.data.objects.new(target.name+'_Camera',camdata)
    target.collection.objects.link(cam)
    cam.location=camloc[0]
    cam.rotation_euler=(Vector(camloc[1])-cam.location).to_track_quat('-Z','Y').to_euler()
    camdata.type='ORTHO';camdata.ortho_scale=scale
    target.camera=cam
    for name,loc,energy,color,size in [('Key',(-20,-20,65),20000,(1,.83,.65),30),('Fill',(40,-10,45),15000,(.65,.8,1),30)]:
        data=bpy.data.lights.new(target.name+name,'AREA');data.energy=energy;data.color=color;data.shape='DISK';data.size=size
        obj=bpy.data.objects.new(data.name,data);target.collection.objects.link(obj);obj.location=loc
        obj.rotation_euler=(Vector((10,8,10))-obj.location).to_track_quat('-Z','Y').to_euler()
    data=bpy.data.lights.new(target.name+'_Sun','SUN');data.energy=2;data.angle=.12
    obj=bpy.data.objects.new(data.name,data);target.collection.objects.link(obj);obj.rotation_euler=(.4,-.5,-.6)
    return cam

studio(scene,((70,-85,75),(18,14,10)),77,(1600,1200))
bpy.context.view_layer.active_layer_collection=bpy.context.view_layer.layer_collection
floor=box('Library_Ground',(18,15,-.85),(150,150,.3),'Graphite',0)
scene.render.filepath=str(OUT/'Architecture_Library.png')
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D': area.spaces.active.region_3d.view_perspective='CAMERA'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'SkyArchitecture_Kit.blend'))
# Separate scene with linked mesh instances arranged as a reference-style board.
demo=bpy.data.scenes.new('SkyBattlefield_Assembly')
bpy.context.window.scene=demo
demo.unit_settings.system='METRIC'
placements=[]
def place(name,pos,rotation=0):
    obj=bpy.data.objects.new(name,meshes[name]);demo.collection.objects.link(obj)
    obj.location=pos;obj.rotation_euler.z=rotation
    placements.append({'asset':name,'position_blender':list(pos),'rotation_z_degrees':math.degrees(rotation)})
    return obj

bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/CompanyWarRE/Content/Environment/SkyPillar/SM_SkyPillar_A.fbx'))
main=next(o for o in bpy.context.selected_objects if o.type=='MESH')
meshes['SM_SkyPillar_A']=main.data
meshes['SM_SkyPillar_A'].use_fake_user=True
bpy.data.objects.remove(main,do_unlink=True)
for ix in range(6):
    for iy in range(6):
        place('SM_SkyPillar_A',((ix-2.5)*5.6,(iy-2.5)*5.6,0))
for name,pos in [
    ('SM_SkyCommandRoom',(14,14,16)),('SM_SkyServiceRoom',(14,-8.4,16)),
    ('SM_SkyMachinery',(-14,8.4,16)),('SM_SkyMachinery',(8.4,14,16)),
    ('SM_SkyAntenna',(-8.4,14,16)),('SM_SkyAntenna',(14,2.8,16))]: place(name,pos)
for x in [-12,0,12]:
    place('SM_SkyPillar_A',(x,-22,0))
    place('SM_SkyLanding_4m',(x,-22,16.65))
for x in [-6,6]: place('SM_SkyBridge_8m',(x,-22,16.65))
place('SM_SkyCommandRoom',(-12,-22,16.65))
for x,y,name in [(-31,18,'SM_SkyTower_Monolith36m'),(0,39,'SM_SkyTower_Slim24m'),
    (30,30,'SM_SkyTower_Monolith36m'),(-27,-8,'SM_SkyTower_Split28m'),
    (29,-10,'SM_SkyTower_Slim24m'),(-19,35,'SM_SkyTower_Split28m'),(40,10,'SM_SkyTower_Split28m')]:
    place(name,(x,y,-5))
for angle in [0,math.pi/2,math.pi,math.pi*1.5]: place('SM_SkyRing_Quarter_R7m',(-31,18,19),angle)
# Ground is a neutral blue depth cue; clouds and characters are outside this building kit.
bpy.context.view_layer.active_layer_collection=bpy.context.view_layer.layer_collection
floor=box('Backdrop',(0,0,-6),(260,260,.2),'Graphite',0)
studio(demo,((64,-89,78),(0,7,12)),100,(1600,1200))
demo.render.filepath=str(OUT/'SkyBattlefield_Assembly.png')
(OUT/'assembly_layout.json').write_text(json.dumps(placements,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'SkyBattlefield_Assembly.blend'))
bpy.ops.render.render(write_still=True)
# Close view of the small modules, to show architectural detail clearly.
bpy.context.window.scene=scene
scene.camera.location=(35,-42,39)
scene.camera.rotation_euler=(Vector((18,7,0))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
scene.camera.data.ortho_scale=52
for name,coll in collections.items():
    if 'Tower' in name or 'Ring' in name: coll.hide_render=True
scene.render.filepath=str(OUT/'Architecture_Modules.png')
bpy.ops.render.render(write_still=True)
print('ARCHITECTURE_DONE',len(stats),'assets')
