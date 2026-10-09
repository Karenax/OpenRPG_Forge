"""Independently verify native frames; retain the refused whole-project SHA gate."""
from pathlib import Path
import datetime
import hashlib
import importlib.util
import json
import re
import shutil
import struct

W=Path('F:/OpenRPG_ImportWork')
OLD=W/'unity-delta-firepit-particle-behavior-v4'
ROOT=W/'unity-delta-firepit-particle-behavior-v5'
BASE=Path(__file__).with_name('queue_delta_firepit_particle_behavior_v1.py')

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest()
def read(path):return json.loads(Path(path).read_text('utf-8-sig'))
def save(name,value):
    (ROOT/name).write_text(json.dumps(value,indent=2,allow_nan=False),'utf-8')
def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result)
    return result

def main():
    assert not ROOT.exists()
    refusal=read(OLD/'RUN-RESULT.v1.json')
    assert refusal['status']=='refused_or_unverified' and refusal['unity_exit_code']==0
    assert refusal['error'].startswith('AssertionError: Changed input:')
    prepared=read(OLD/'PREPARED.v1.json')
    project=Path(prepared['project'])
    exact_names={
        'Assets/HIVEMIND/NativeAmericanVillage/HDRP(Default)/Art/StaticMeshes.meta',
        'Assets/HIVEMIND/NativeAmericanVillage/HDRP(Default)/Art/Prefabs.meta',
        'Assets/HIVEMIND/NativeAmericanVillage/HDRP(Default)/Art/Textures/Masks.meta',
        'Assets/HIVEMIND/NativeAmericanVillage/HDRP(Default)/Art/Textures/fabric.meta',
        'Assets/HIVEMIND/NativeAmericanVillage/HDRP(Default)/Art/StaticMeshes/Props.meta',
    }
    observed={p:sha(p) for p in prepared['input_pins']}
    changed=[p for p,d in prepared['input_pins'].items() if observed[p]!=d]
    assert len(changed)==5
    assert {Path(p).relative_to(project).as_posix() for p in changed}==exact_names
    # A narrowly scoped native behavior proof is distinct from the refused
    # unchanged-project assertion. No asset/source/code pin is exempted.
    for p in changed:
        meta=Path(p);assert meta.with_suffix('').is_dir()
        text=meta.read_text('utf-8-sig')
        assert re.fullmatch(r'fileFormatVersion: 2\nguid: [0-9a-f]{32}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: \{\}\n  userData: *\n  assetBundleName: *\n  assetBundleVariant: *\n',text)
    ROOT.mkdir()
    state=dict(status='independently_verifying_captured_frames_with_private_folder_metadata_changes',
        native_behavior_verified=False,gpu_rendered=False,studio_verified=False,full_import=False,
        unchanged_private_project_qualified=False,prior_refusal_preserved=True)
    save('RUN-RESULT.v1.json',state)
    try:
        for name in ['BEHAVIOR.v1.json','manifest.json','verify_frames.py']:
            shutil.copyfile(OLD/name,ROOT/name)
            assert sha(OLD/name)==sha(ROOT/name)
            observed[str(OLD/name)]=sha(OLD/name)
        for p in [BASE,Path(__file__).resolve(),OLD/'RUN-RESULT.v1.json',OLD/'unity.log',OLD/'PREPARED.v1.json']:
            observed[str(p)]=sha(p)
        source=module('firepit_sources',BASE)
        verifier=module('firepit_verifier',ROOT/'verify_frames.py')
        report=read(ROOT/'BEHAVIOR.v1.json');request=read(ROOT/'manifest.json')
        assert len(report['cases'])==2 and not report['full_import']
        controls=verifier.controls();summaries=[]
        for case in prepared['source_cases']:
            result=next(r for r in report['cases'] if r['node_path']==case['node_path'])
            particle,applied=source.effective_particle(read(case['original_source_ir']))
            assert applied==case['expected_particle_overrides']
            native=verifier.load_native(result['native_particle_json'],'ParticleSystem')
            for edit in applied:
                value=native
                for key in edit['property_path'].split('.'):value=value[key]
                expected=edit['after']
                if type(expected) is float:assert struct.pack('<f',value)==struct.pack('<f',expected)
                else:assert value==expected
            scoped=dict(request,particle_file_id=result['particle_file_id'],
                renderer_file_id=case['renderer_file_id'],material_guid=case['material_guid'])
            summary=verifier.check(result,scoped,particle);assert summary['rows']==56
            for row in result['rows']:
                for key,value in case['source_local_to_world'].items():
                    assert abs(row['local_to_world'][key]-value)<=1e-6
            summaries.append(dict(node_path=case['node_path'],native_particle_overrides_reopened=applied,**summary))
        assert all(sha(p)==d for p,d in observed.items())
        changes=[]
        for index,p in enumerate(changed):
            retained=ROOT/f'private-folder-{index:02d}.meta'
            shutil.copyfile(p,retained);assert sha(retained)==observed[p]
            changes.append(dict(path=p,before_sha256=prepared['input_pins'][p],
                after_sha256=observed[p],retained_after=str(retained),
                before_bytes_retained_in_this_receipt=False,guid_equivalence_not_claimed=True))
        proof=dict(input_pins=observed,behavior_sha256=sha(ROOT/'BEHAVIOR.v1.json'),
            summaries=summaries,controls=controls,frames_verified=112,effect_systems_verified=2,
            source_effective_overrides_and_transforms_retained=True,seeded_native_reference_verified=True,
            private_folder_metadata_changes=changes,unchanged_private_project_qualified=False,
            original_project_version_qualified=False,autonomous_play_or_prewarm_parity_qualified=False,
            gpu_rendered=False,runtime_converted=False,full_import=False)
        save('INDEPENDENT-REOPEN.v1.json',proof)
        state.update(status='two_effects_and_112_seeded_native_frames_reopened_with_private_folder_changes',
            native_behavior_verified=True,effect_systems_verified=2,frames_verified=112,
            independent_receipt_sha256=sha(ROOT/'INDEPENDENT-REOPEN.v1.json'))
    except Exception as error:state.update(status='refused_or_unverified',error=type(error).__name__+': '+str(error))
    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json',state)
    print(json.dumps(state))

if __name__=='__main__':main()
