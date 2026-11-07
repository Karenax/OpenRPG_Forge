"""Version the existing source assembly with the shared WebGPU guard packager."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,subprocess,sys,traceback
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'firepit-web-assembly-controller-v216'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not O.exists();O.mkdir()
    base=S/'assemble_firepit_glb_v2.py';target=S/'assemble_firepit_glb_v3.py';normalizer=S/'normalize_source_graph_glb.py'
    assert not target.exists()
    text=base.read_text('utf-8-sig')
    replacements={
      'from assemble_native_villa_delivery import pack_glb':'from assemble_native_villa_delivery import pack_glb\nfrom normalize_source_graph_glb import normalize_glb',
      "ROOT=W/'firepit-complete-static-glb-v2'":"ROOT=W/'firepit-complete-static-glb-v3'",
      'path.write_bytes(pack_glb(doc,bytes(data)))':'raw,normal_guard_proof=normalize_glb(pack_glb(doc,bytes(data)));path.write_bytes(raw)',
      'results.append(dict(asset_id=scene[\'asset_id\'],glb=str(path)':'results.append(dict(normal_guard_packaging=normal_guard_proof,asset_id=scene[\'asset_id\'],glb=str(path)',
      'pins[str(Path(__file__))]=sha(Path(__file__))':"pins[str(Path(__file__))]=sha(Path(__file__));pins[str(Path(__file__).with_name('normalize_source_graph_glb.py'))]=sha(Path(__file__).with_name('normalize_source_graph_glb.py'));pins[str(Path(__file__).with_name('assemble_native_villa_delivery.py'))]=sha(Path(__file__).with_name('assemble_native_villa_delivery.py'))"}
    for before,after in replacements.items():
        assert text.count(before)==1,before;text=text.replace(before,after)
    target.write_text(text,'utf-8')
    pins={str(p):sha(p) for p in [Path(__file__),base,target,normalizer,S/'assemble_native_villa_delivery.py',W/'source-graph-web-guard-converter-reopen-v215/RESULT.v1.json']}
    state=dict(status='firepit_source_assembly_with_web_packager_running',full_import=False,input_pins=pins,
        new_converter_installed_in_versioned_recipe=True,original_recipe_preserved=True)
    (O/'RUN-RESULT.v1.json').write_text(json.dumps(state,indent=2),'utf-8')
    try:
        with (O/'stdout.log').open('xb') as out,(O/'stderr.log').open('xb') as err:
            p=subprocess.Popen([sys.executable,'-B',str(target)],stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            (O/'JOB.v1.json').write_text(json.dumps(dict(assembly_pid=p.pid)),'utf-8');state['assembly_exit_code']=p.wait()
        assert state['assembly_exit_code']==0,(O/'stderr.log').read_text('utf-8',errors='replace')[-2000:]
        for p,h in pins.items():assert sha(p)==h,p
        receipt=W/'firepit-complete-static-glb-v3/RESULT.v1.json';result=json.loads(receipt.read_text('utf-8-sig'))
        assert len(result['rows'])==1
        row=result['rows'][0];assert sha(Path(row['glb']))==row['sha256']
        state.update(status='firepit_source_assembly_with_shared_web_packager_completed',output_pins={str(receipt):sha(receipt),row['glb']:row['sha256']},scene_row=row,
            source_render_equivalence=False,canonical_reader_verified=False,gpu_executed=False,studio_verified=False)
    except Exception as exc:state.update(status='firepit_source_assembly_packaging_refusal_retained',error=repr(exc),traceback=traceback.format_exc())
    state['finished_utc']=datetime.now(timezone.utc).isoformat()
    (O/'RUN-RESULT.v1.json').write_text(json.dumps(state,indent=2),'utf-8');print(json.dumps({k:v for k,v in state.items() if k not in {'input_pins','scene_row','traceback'}}))
if __name__=='__main__':main()
