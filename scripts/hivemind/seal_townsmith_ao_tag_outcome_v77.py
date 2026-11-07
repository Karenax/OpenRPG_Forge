from pathlib import Path
import json,hashlib
from datetime import datetime,timezone
W=Path('F:/OpenRPG_ImportWork');O=W/'townsmith-ao-tag-deferred-outcome-v77';assert not O.exists();pins={}
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def pin(p,h=None):
 v=hashlib.sha256(p.read_bytes()).hexdigest();assert h is None or h==v,str(p);pins[str(p)]=v;return v
paths=[W/'townsmith-door-source-declared-gbuffer3-reopen-v75/RESULT.v1.json',W/'townsmith-gbuffer3-ao-tag-control-v76/RESULT.v1.json',W/'townsmith-ao-tag-deferred-comparison-v77/RESULT.v1.json']
for p in paths:
 r=read(p);pin(p)
 for key,h in r['input_pins'].items():pin(Path(key),h)
r=read(paths[2]);rows=r['rows'];before=rows[1]['mean_rgb_error'];after=rows[2]['mean_rgb_error'];inst=W/'townsmith-declared-gbuffer3-instrumentation-v75/RESULT.v1.json';i=read(inst);pin(inst);assert i['sdk_restored_byte_equal'] and not i['sdk_restore_pending'];pin(Path(i['sdk_path']),i['sdk_original_sha256']);q=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust/scripts/hivemind/queue_source_glb_ao_tag_deferred_v77b.py');pin(q);fix=W/'townsmith-ao-tag-launcher-sha-correction-v77b/RESULT.v1.json';pin(fix);pin(Path(__file__))
res=dict(status='actual_source_ao_tag_qualified_private_candidate_does_not_improve_full_material',finished_utc=datetime.now(timezone.utc).isoformat(),lot='L03.TOWNSMITH.ECLAIRAGE.AO.GBUFFER3',source_ao_tag_pixels=34445,source_graph_floor_uf11_equal_pixels=34445,source_reference_byte_unchanged=True,sdk_restored_byte_equal=True,consecutive_equal_gpu_readbacks=3,full_material_error_before=before,full_material_error_after=after,increase_percent=(after/before-1)*100,max_error=rows[2]['max_rgb_error'],pixels_over_001_before=rows[1]['pixels_over_001'],pixels_over_001_after=rows[2]['pixels_over_001'],rehashed_pins=len(pins),input_pins=pins,candidate_retained_not_installed=True,source_pixels_applied_to_native=False,pixel_fit_or_threshold_relaxation=False,forge_editor_verified=False,full_material_fidelity_qualified=False,full_import=False,complete_assets_delivered_increment=0)
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(res,indent=2)+'\n');print(json.dumps({k:v for k,v in res.items() if k!='input_pins'}))
