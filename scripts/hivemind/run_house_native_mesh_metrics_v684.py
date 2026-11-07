"""Capture Unity mesh queries on disposable clones; never replace concave geometry with a hull."""
from pathlib import Path
import ast

S = Path(__file__).resolve().parent
base = S / 'run_gladiator_native_box_metric_v471.py'
tree = ast.parse(base.read_text('utf-8-sig'))
assignment = next(n for n in tree.body if isinstance(n, ast.Assign)
                  and any(isinstance(t, ast.Name) and t.id == 'CODE' for t in n.targets))
CODE = ast.literal_eval(assignment.value)
replacements = {
    'OpenRPGGladiatorNativeBoxMetricV471': 'OpenRPGHouseNativeMeshMetricV684',
    'GetComponentsInChildren<BoxCollider>': 'GetComponentsInChildren<MeshCollider>',
    'public string prefab_path;': 'public bool convex;public string mesh_guid;public string mesh_file_id;public int cooking_options;public string prefab_path;',
    'var points=new List<Point>();var rays=new List<RaySample>();var center=box.center;var half=box.size*0.5f;':
        'if(box.sharedMesh==null)continue;string meshGuid;long meshFileId;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source.sharedMesh,out meshGuid,out meshFileId))throw new Exception("Mesh identity absent");var points=new List<Point>();var rays=new List<RaySample>();var center=box.sharedMesh.bounds.center;var size=box.sharedMesh.bounds.size;var half=size*0.5f;if(half.x<=0||half.y<=0||half.z<=0)throw new Exception("Degenerate mesh bounds refused");',
    'for(int axis=0;axis<3;axis++)foreach(float factor':
        'if(box.convex)for(int axis=0;axis<3;axis++)foreach(float factor',
    'center+box.size*2f': 'center+size*2f',
    'var disabledClosest=box.ClosestPoint(disabledPoint);':
        'var disabledClosest=box.convex?Vec(box.ClosestPoint(disabledPoint)):new float[0];',
    'local_size=Vec(box.size)': 'local_size=Vec(size)',
    'disabled_closest=Vec(disabledClosest)': 'disabled_closest=disabledClosest',
    'new Box{prefab_path=path':
        'new Box{convex=source.convex,mesh_guid=meshGuid,mesh_file_id=meshFileId.ToString(),cooking_options=(int)source.cookingOptions,prefab_path=path',
}
for old, new in replacements.items():
    expected = 2 if old == 'GetComponentsInChildren<BoxCollider>' else 1
    assert CODE.count(old) == expected, (old, CODE.count(old))
    CODE = CODE.replace(old, new)
# Receipt retains the historical array field "boxes", but every row is explicitly
# a mesh sample with mesh identity, convexity and source cooking policy.
parent = S / 'run_house_native_box_metrics_v681.py'
recipe = parent.read_text('utf-8-sig')
begin = recipe.index('CODE=ast.literal_eval(')
end = recipe.index('\nPARENT=', begin)
recipe = recipe[:begin] + 'CODE=OWNED_MESH_CODE' + recipe[end:]
recipe = recipe.replace(".replace('v672','v681').replace('V672','V681')",
                        ".replace('v672','v684').replace('V672','V684')")
recipe = recipe.replace('house-native-component-state-v673/HELPER-OWNERSHIP.v1.json',
                        'house-native-component-state-v681/HELPER-OWNERSHIP.v1.json')
recipe = recipe.replace("len(audit['helpers'])==39", "len(audit['helpers'])==40")
recipe = recipe.replace('V681', 'V684')
recipe = recipe.replace('OpenRPGHouseNativeBoxMetricV684', 'OpenRPGHouseNativeMeshMetricV684')
recipe = recipe.replace("assert len(native['boxes'])==3 and sum(len(b['points']) for b in native['boxes'])==63 and sum(len(b['rays']) for b in native['boxes'])==18",
    "assert len(native['boxes'])==19 and sum(b['convex'] for b in native['boxes'])==17 and sum(len(b['points']) for b in native['boxes'])==357 and sum(len(b['rays']) for b in native['boxes'])==114")
recipe = recipe.replace('three_house_native_box_metrics_captured_review_pending',
                        'nineteen_house_native_mesh_queries_captured_review_pending')
OWNED_MESH_CODE = CODE
exec(compile(recipe, str(parent) + '#house-native-mesh-metric-v684', 'exec'), globals())
