"""Create a separate helper that captures the requested source LOD explicitly."""
from pathlib import Path
import hashlib,json
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork')
O=W/'source-lod-thumbnail-helper-v350'
base=S/'candidates/SourceLibraryThumbnailBatchV1.cs'
dest=S/'candidates/SourceLibraryLodThumbnailBatchV350.cs'
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists() and not dest.exists()
text=base.read_text('utf-8-sig')
changes={
'OpenRPGSourceLibraryThumbnailBatchV1':'OpenRPGSourceLibraryLodThumbnailBatchV350',
'public string id,prefab_path,prefab_guid,output;':'public string id,prefab_path,prefab_guid,output; public int requested_lod;',
'lod0_for_thumbnail_only=true':'lod0_for_thumbnail_only=false',
'public TextureBinding[] textures;':'public int requested_lod,source_lod_groups;public bool requested_lod_selection_verified;public string[] selected_mesh_identities;public int[] selected_mesh_vertex_counts;public string[] disabled_lod_mesh_identities;public TextureBinding[] textures;',
'foreach(var lod in fixture.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(0);':'''if(request.requested_lod<0)throw new Exception("Negative source LOD");
   var sourceLodGroups=fixture.GetComponentsInChildren<LODGroup>(true);if(sourceLodGroups.Length==0)throw new Exception("Source LOD group absent; cannot qualify a variant");
   var lodRenderers=new System.Collections.Generic.HashSet<Renderer>();var selectedLodRenderers=new System.Collections.Generic.HashSet<Renderer>();
   foreach(var lod in sourceLodGroups) {
    var levels=lod.GetLODs();if(request.requested_lod>=levels.Length)throw new Exception("Requested LOD absent in source group");
    foreach(var level in levels)foreach(var renderer in level.renderers){if(renderer==null)throw new Exception("Missing source LOD renderer");lodRenderers.Add(renderer);}
    foreach(var renderer in levels[request.requested_lod].renderers)selectedLodRenderers.Add(renderer);
    lod.ForceLOD(request.requested_lod);
   }
   if(selectedLodRenderers.Count==0)throw new Exception("Requested LOD has no renderers");
   foreach(var renderer in lodRenderers)renderer.enabled=selectedLodRenderers.Contains(renderer);
   if(selectedLodRenderers.Any(r=>!r.enabled||!r.gameObject.activeInHierarchy||r.forceRenderingOff)||lodRenderers.Except(selectedLodRenderers).Any(r=>r.enabled))throw new Exception("LOD visibility selection mismatch");
   Func<Renderer,Mesh> selectedMesh=r=>{var filter=r.GetComponent<MeshFilter>();return filter!=null?filter.sharedMesh:(r as SkinnedMeshRenderer)?.sharedMesh;};
   if(selectedLodRenderers.Any(r=>selectedMesh(r)==null))throw new Exception("Selected LOD mesh absent");''',
'new Report{id=request.id,prefab_guid=request.prefab_guid,unity_version=':'''new Report{id=request.id,prefab_guid=request.prefab_guid,requested_lod=request.requested_lod,source_lod_groups=sourceLodGroups.Length,requested_lod_selection_verified=true,selected_mesh_identities=selectedLodRenderers.Select(r=>Identity(selectedMesh(r))).ToArray(),selected_mesh_vertex_counts=selectedLodRenderers.Select(r=>selectedMesh(r).vertexCount).ToArray(),disabled_lod_mesh_identities=lodRenderers.Except(selectedLodRenderers).Select(r=>Identity(selectedMesh(r))).ToArray(),unity_version='''
}
for before,after in changes.items():
    assert text.count(before)==1,before
    text=text.replace(before,after)
dest.write_text(text,'utf-8');O.mkdir()
(O/'RESULT.v1.json').write_text(json.dumps(dict(status='requested_lod_helper_prepared_native_compilation_pending',base_sha256=sha(base),helper_sha256=sha(dest),helper=str(dest),adaptations=changes,full_import=False),indent=2),'utf-8')
print(str(dest))
