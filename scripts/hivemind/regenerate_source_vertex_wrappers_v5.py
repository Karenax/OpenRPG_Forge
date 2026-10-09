"""Regenerate 48 stage variants in three declared batches; retain v4 verbatim."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,subprocess
from source_graph_instance_index_transport_v2 import repair_vertex_instance_transport
W=Path('F:/OpenRPG_ImportWork');OLD=W/'native-hdrp-nine-source-programs-v1/source-graph-bevy-vertex-wrapper-v4';ROOT=OLD.with_name('source-graph-bevy-vertex-wrapper-v5')
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def write(p,v):p.write_text(json.dumps(v,indent=2),'utf-8')
def main():
    assert not ROOT.exists()
    parent=read(OLD/'RESULT.v1.json');assert len(parent['rows'])==48 and parent['actual_positive_compositions']==192 and not parent['errors']
    validator=OLD/'validate_bevy_vertex.exe';modules=OLD/'module-list.txt';assert sha(validator)==parent['validator_sha256']
    inputs=[Path(__file__).resolve(),Path(__file__).with_name('source_graph_instance_index_transport_v1.py'),Path(__file__).with_name('source_graph_instance_index_transport_v2.py'),OLD/'RESULT.v1.json',validator,modules,OLD/'LICENSE.Bevy.MIT.txt',OLD/'source_graph_bevy_vertex_and_prepass.wgsl']
    for pin in parent['bevy_primary_module_pins']:
        assert sha(Path(pin['copy']))==pin['sha256'];inputs.append(Path(pin['copy']))
    for row in parent['rows']:
        p=Path(row['forward_vertex']);assert sha(p)==row['sha256'];inputs.append(p)
    pins={str(p):sha(p) for p in inputs};ROOT.mkdir();(ROOT/'LICENSE.Bevy.MIT.txt').write_bytes((OLD/'LICENSE.Bevy.MIT.txt').read_bytes())
    (ROOT/'source_graph_bevy_vertex_and_prepass.wgsl').write_text(repair_vertex_instance_transport((OLD/'source_graph_bevy_vertex_and_prepass.wgsl').read_text('utf-8')),'utf-8')
    manifests=[]
    for batch_index in range(3):
        selected=parent['rows'][batch_index*16:(batch_index+1)*16]
        manifest=dict(batch=f'L01.INSTANCE.WRAPPERS.{batch_index+1:02}',expected_material_variants=16,expected_positive_compositions=64,ids=[dict(group=r['group'],material_guid=r['material_guid'],source_program=r['forward_vertex'],source_sha256=r['sha256'],output=str(ROOT/r['group']/r['material_guid']/'source_graph_vertex_and_prepass.wgsl')) for r in selected],qualification_scope='shader stage variants; no complete assets',full_import=False)
        p=ROOT/f'BATCH-{batch_index+1:02}.PLAN.v1.json';write(p,manifest);manifests.append((p,manifest,selected))
    rows=[];failures=[];batch_receipts=[]
    for plan_path,plan,selected in manifests:
        completed=[]
        for row in selected:
            folder=ROOT/row['group']/row['material_guid'];folder.mkdir(parents=True);output=folder/'source_graph_vertex_and_prepass.wgsl'
            try:
                patched=repair_vertex_instance_transport(Path(row['forward_vertex']).read_text('utf-8'));assert repair_vertex_instance_transport(patched)==patched
                output.write_text(patched,'utf-8');permutations=[]
                for permutation in row['actual_stage_permutations']:
                    name=permutation['permutation'];out=folder/f'{name}.stdout.log';err=folder/f'{name}.stderr.log'
                    with out.open('xb') as stdout,err.open('xb') as stderr:
                        proc=subprocess.run([str(validator),str(modules),str(output),*permutation['shader_defines']],stdout=stdout,stderr=stderr,creationflags=subprocess.CREATE_NO_WINDOW)
                    assert proc.returncode==0,(name,proc.returncode)
                    permutations.append(dict(permutation=name,actual_exit_code=proc.returncode,shader_defines=permutation['shader_defines']))
                actual=dict(group=row['group'],material_guid=row['material_guid'],source=row['forward_vertex'],source_sha256=row['sha256'],forward_vertex=str(output),sha256=sha(output),explicit_entrypoint_instance_transport=True,actual_stage_permutations=permutations,source_vertex_data_qualified=False,renderer_installed=False,gpu_qualified=False)
                completed.append(actual);rows.append(actual)
            except Exception as error:failures.append(dict(group=row['group'],material_guid=row['material_guid'],error=repr(error)))
        receipt=ROOT/plan_path.name.replace('PLAN','RESULT');write(receipt,dict(status='declared_vertex_stage_batch_verified' if len(completed)==16 else 'declared_vertex_stage_batch_refused',plan_sha256=sha(plan_path),expected_variants=16,variants_validated=len(completed),positive_compositions=sum(len(r['actual_stage_permutations']) for r in completed),full_import=False,rows=completed));batch_receipts.append(receipt)
    negatives=[];test_program=Path(rows[0]['forward_vertex'])
    for original in parent['unsupported_stage_refusal_tests']:
        out=ROOT/f'negative-{original["test"]}.stdout.log';err=ROOT/f'negative-{original["test"]}.stderr.log'
        with out.open('xb') as stdout,err.open('xb') as stderr:
            proc=subprocess.run([str(validator),str(modules),str(test_program),*original['shader_defines']],stdout=stdout,stderr=stderr,creationflags=subprocess.CREATE_NO_WINDOW)
        reason=original['reason'] in err.read_text('utf-8',errors='replace') or original['reason'] in out.read_text('utf-8',errors='replace')
        assert proc.returncode==original['expected_exit_code'] and reason,original['test']
        negatives.append(dict(test=original['test'],actual_exit_code=proc.returncode,source_specific_reason_present=reason,reason=original['reason']))
    for p,h in pins.items():assert sha(p)==h,p
    result=dict(status='shared_explicit_instance_transport_48_stage_variants_composed' if not failures and len(rows)==48 else 'shared_instance_transport_stage_generation_refused',finished_utc=datetime.now(timezone.utc).isoformat(),rows=rows,failures=failures,expected_stage_variants=48,stage_variants_validated=len(rows),positive_compositions=sum(len(r['actual_stage_permutations']) for r in rows),unsupported_stage_refusal_tests=negatives,batch_receipts=[dict(path=str(p),sha256=sha(p)) for p in batch_receipts],historical_snapshot_unchanged=True,common_generation_recipe_applied=True,renderer_installed=False,gpu_qualified=False,vault_assets_updated=0,complete_assets_delivered_increment=0,full_import=False,input_pins=pins,output_pins={r['forward_vertex']:r['sha256'] for r in rows})
    write(ROOT/'RESULT.v1.json',result);print(json.dumps({k:v for k,v in result.items() if k not in ['rows','input_pins','output_pins','batch_receipts','unsupported_stage_refusal_tests']}))
if __name__=='__main__':main()
