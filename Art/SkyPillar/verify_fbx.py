import bpy
import json
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[2]
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(root/'Assets/CompanyWarRE/Content/Environment/SkyPillar/SM_SkyPillar_A.fbx'))
objs=[o for o in bpy.context.scene.objects if o.type=='MESH']
assert len(objs)==1, len(objs)
o=objs[0]
assert len(o.data.materials)==7
assert len(o.data.uv_layers)>0
o.data.calc_loop_triangles()
assert len(o.data.loop_triangles)==20576
corners=[o.matrix_world @ Vector(c) for c in o.bound_box]
extent=[max(c[i] for c in corners)-min(c[i] for c in corners) for i in range(3)]
assert all(abs(a-b)<.002 for a,b in zip(extent,[4.04,4.04,16.05])),extent
assert o.location.length < .001
assert abs(min(c.z for c in corners))<.001
result={'result':'PASS','mesh_count':len(objs),'materials':[m.name for m in o.data.materials],'triangles':len(o.data.loop_triangles),'world_dimensions_m':extent,'uv_layers':len(o.data.uv_layers),'bottom_center_pivot':True}
(root/'Art/SkyPillar/fbx_verification.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result))
