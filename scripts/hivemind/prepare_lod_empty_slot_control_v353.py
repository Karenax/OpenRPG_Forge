"""Distinguish source-declared empty renderer slots from broken references."""
from pathlib import Path
import hashlib,json
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'source-lod-empty-slot-control-v353'
src=S/'candidates/SourceLibraryLodThumbnailBatchV350.cs';dest=S/'candidates/SourceLibraryLodThumbnailBatchV353.cs'
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists() and not dest.exists()
text=src.read_text('utf-8-sig').replace('OpenRPGSourceLibraryLodThumbnailBatchV350','OpenRPGSourceLibraryLodThumbnailBatchV353')
text=text.replace('public int requested_lod;','public int requested_lod,expected_empty_lod_slots;')
text=text.replace('public int requested_lod,source_lod_groups;','public int requested_lod,source_lod_groups,source_empty_lod_slots;')
anchor='var lodRenderers=new System.Collections.Generic.HashSet<Renderer>();'
assert text.count(anchor)==1
text=text.replace(anchor,'''int emptyLodSlots=sourceLodGroups.Sum(g=>g.GetLODs().Sum(level=>level.renderers.Count(r=>r==null)));
   if(emptyLodSlots!=request.expected_empty_lod_slots)throw new Exception("Native empty LOD slots differ from explicit source fileID zero slots");
   '''+anchor)
anchor='foreach(var level in levels)foreach(var renderer in level.renderers){if(renderer==null)throw new Exception("Missing source LOD renderer");lodRenderers.Add(renderer);}'
assert text.count(anchor)==1
text=text.replace(anchor,'foreach(var level in levels)foreach(var renderer in level.renderers)if(renderer!=null)lodRenderers.Add(renderer);')
anchor='foreach(var renderer in levels[request.requested_lod].renderers)selectedLodRenderers.Add(renderer);'
assert text.count(anchor)==1
text=text.replace(anchor,'''if(!levels[request.requested_lod].renderers.Any(r=>r!=null))throw new Exception("Requested source LOD contains only empty slots");
    foreach(var renderer in levels[request.requested_lod].renderers)if(renderer!=null)selectedLodRenderers.Add(renderer);''')
text=text.replace('source_lod_groups=sourceLodGroups.Length,','source_lod_groups=sourceLodGroups.Length,source_empty_lod_slots=emptyLodSlots,')
dest.write_text(text,'utf-8');O.mkdir()
(O/'RESULT.v1.json').write_text(json.dumps(dict(status='explicit_empty_slot_control_prepared_native_check_pending',input_pins={str(src):sha(src),str(dest):sha(dest),str(Path(__file__)):sha(Path(__file__))},source_references_must_resolve=True,native_empty_slot_count_must_match_declared_zeros=True,every_selected_group_must_have_renderer=True,full_import=False),indent=2),'utf-8')
print(str(dest))
