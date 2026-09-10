import bpy
import json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Art/SkyArchitecture'
GAME=ROOT/'Assets/CompanyWarRE/Content/Environment/SkyArchitecture'
manifest=json.loads((OUT/'asset_manifest.json').read_text(encoding='utf-8'))
results=[]
for asset in manifest:
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(GAME/(asset['name']+'.fbx')))
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    assert len(objects)==1,(asset['name'],len(objects))
    obj=objects[0]
    obj.data.calc_loop_triangles()
    assert len(obj.data.loop_triangles)==asset['triangles'],asset['name']
    assert obj.data.uv_layers,asset['name']
    assert obj.location.length<.001,asset['name']
    corners=[obj.matrix_world@Vector(c) for c in obj.bound_box]
    extent=[max(c[i] for c in corners)-min(c[i] for c in corners) for i in range(3)]
    assert all(abs(a-b)<.002 for a,b in zip(extent,asset['dimensions_m'])),(asset['name'],extent)
    # Signed volume must be positive for every closed connected mesh component.
    # This detects inverted surfaces that disappear under Unity backface culling.
    mesh=obj.data
    parent=list(range(len(mesh.vertices)))
    def find(x):
        while parent[x]!=x:
            parent[x]=parent[parent[x]]
            x=parent[x]
        return x
    for edge in mesh.edges:
        a,b=edge.vertices
        parent[find(a)]=find(b)
    volumes={}
    for tri in mesh.loop_triangles:
        a,b,c=(mesh.vertices[i].co for i in tri.vertices)
        key=find(tri.vertices[0])
        volumes[key]=volumes.get(key,0)+a.dot(b.cross(c))/6
    inverted=[v for v in volumes.values() if v < -1e-6]
    assert not inverted,(asset['name'],'inverted shells',inverted)
    results.append({'name':asset['name'],'result':'PASS','triangles':asset['triangles'],'shell_count':len(volumes),'uv0':True,'dimensions_m':extent})
(OUT/'verification.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
print('VERIFIED',len(results),'FBX assets')
