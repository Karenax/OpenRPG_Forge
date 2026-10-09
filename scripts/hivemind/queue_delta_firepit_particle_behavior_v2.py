"""Retry the terminal missing-component refusal using the verified installed HDRP package."""
from pathlib import Path
import datetime
import hashlib
import importlib.util
import json
import re
import shutil
import sys

W=Path('F:/OpenRPG_ImportWork')
BASE=Path(__file__).with_name('queue_delta_firepit_particle_behavior_v1.py')
PROJECT=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
REPAIR=W/'delta-firepit-hdrp-closure-repair-v2'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def write(path,value):
    with Path(path).open('x',encoding='utf-8') as stream:
        json.dump(value,stream,indent=2,allow_nan=False)


def stage():
    assert not REPAIR.exists(),'Keep existing repair evidence'
    prior_root=W/'unity-delta-firepit-particle-behavior-v1'
    prior=read(prior_root/'RUN-RESULT.v1.json')
    assert prior['status']=='refused_or_unverified' and prior['unity_exit_code']==1
    assert 'Exception: Missing source component' in (prior_root/'unity.log').read_text('utf-8-sig')
    spec=importlib.util.spec_from_file_location('old_native_firepit',BASE)
    old=importlib.util.module_from_spec(spec);spec.loader.exec_module(old);old.unity_idle()
    package_meta=PROJECT/'Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/Lighting/Light/HDAdditionalLightData.cs.meta'
    package_source=package_meta.with_suffix('')
    package_manifest=PROJECT/'Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/package.json'
    assert re.search(r'^guid: 7a68c43fe1f2a47cfa234b5eeaa98012\s*$',package_meta.read_text('utf-8-sig'),re.M)
    assert read(package_manifest)['version']=='17.5.0'
    assert read(PROJECT/'Packages/manifest.json')['dependencies']['com.unity.render-pipelines.high-definition']=='17.5.0'
    source_manifest=W/'hivemind-run/native-american-village/source.v1.json'
    source=read(source_manifest);source_root=Path(source['source_root'])
    index={(f['source_path'],f['kind']):f for f in source['files']}
    pins={str(p):sha(p) for p in [Path(__file__).resolve(),BASE,source_manifest,
        prior_root/'RUN-RESULT.v1.json',prior_root/'unity.log',package_meta,package_source,
        package_manifest,PROJECT/'Packages/manifest.json']}
    pending=['abc00000000014341674918090329798'];seen=set();copies=[];retained=[];unindexed=set()
    reference=re.compile(r'guid:\s*([0-9a-f]{32})|"guid"\s*:\s*"([0-9a-f]{32})"')
    serialized_receipt=W/'delta-firepit-native-serialization-reopen-v1/RESULT.v1.json'
    assert serialized_receipt.is_file()
    pins[str(serialized_receipt)]=sha(serialized_receipt)
    while pending:
        guid=pending.pop()
        if guid in seen:continue
        seen.add(guid);assert len(seen)<=4096,'Dependency closure exceeds bounded source budget'
        relative=source['guid_paths'][guid]
        original=source_root/relative
        assert original.resolve().is_relative_to(source_root.resolve())
        assert original.suffix.lower() not in {'.cs','.dll','.exe','.unitypackage'},'Bought code/archive execution forbidden'
        asset=index[(relative,'asset')];meta=index[(relative,'asset.meta')]
        assert asset['guid']==meta['guid']==guid
        for path,entry in [(original,asset),(Path(str(original)+'.meta'),meta)]:
            assert sha(path)==entry['sha256'];pins[str(path)]=entry['sha256']
            destination=PROJECT/relative
            if path.suffix=='.meta':destination=Path(str(destination)+'.meta')
            assert destination.resolve().is_relative_to(PROJECT.resolve())
            if destination.exists():
                current=sha(destination)
                if current!=entry['sha256']:
                    if destination.suffix=='.meta':
                        assert re.search(r'^guid: '+guid+r'\s*$',destination.read_text('utf-8-sig'),re.M)
                    else:
                        # Only the already independently reopened Unity serialization
                        # of this private FirePit material can differ. Never overwrite it.
                        assert destination.name=='M_FirePit_A_01.mat'
                        assert entry['sha256']=='4e535ddaf4b0f679c528f1f4d258e989996f37b3c8bca4b6ec76bf8b1dbe71ec'
                        assert current=='67ef9aebfa0de3138b3ebf7aa5e1efb23be75ef4e04ab141b7cfe7b1f4c46006'
                retained.append(dict(original=str(path),original_sha256=entry['sha256'],
                    project_copy=str(destination),project_copy_sha256=current,
                    original_and_private_bytes_identical=current==entry['sha256']))
                pins[str(destination)]=current
            else:
                copies.append(dict(original=str(path),destination=str(destination),sha256=entry['sha256']))
        if original.suffix.lower() in {'.prefab','.mat','.asset','.shadergraph','.shadersubgraph'}:
            text=original.read_text('utf-8-sig').replace('\\"','"')
            for pair in reference.findall(text):
                child=pair[0] or pair[1]
                if child in source['guid_paths']:pending.append(child)
                else:unindexed.add(child)
    assert '7a68c43fe1f2a47cfa234b5eeaa98012' in unindexed
    REPAIR.mkdir()
    write(REPAIR/'PLAN.v1.json',dict(input_pins=pins,original_dependency_assets=len(seen),
        copies=copies,existing_private_assets_preserved=retained,
        unindexed_references=sorted(unindexed),verified_native_component_guid='7a68c43fe1f2a47cfa234b5eeaa98012',
        verified_package_version='17.5.0',original_authoring_version_qualified=False,full_import=False))
    for row in copies:
        destination=Path(row['destination']);assert not destination.exists()
        assert sha(row['original'])==row['sha256'];destination.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(row['original'],destination)
        assert sha(destination)==row['sha256'];pins[str(destination)]=row['sha256']
    for p,d in pins.items():assert sha(p)==d
    write(REPAIR/'RESULT.v1.json',dict(status='exact_complete_prefab_dependencies_prepared_in_verified_hdrp_project',
        checked_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),input_pins=pins,
        plan_sha256=sha(REPAIR/'PLAN.v1.json'),source_assets_in_closure=len(seen),
        exact_new_files_copied=len(copies),existing_files_preserved=len(retained),
        original_sources_unchanged=True,prior_terminal_refusal_preserved=True,
        missing_component_guid_verified=True,native_capture_verified=False,full_import=False))


if len(sys.argv)==1:
    stage()

SCRIPT=BASE.read_text('utf-8-sig')
for original,replacement in [
    ("ROOT = W / 'unity-delta-firepit-particle-behavior-v1'","ROOT = W / 'unity-delta-firepit-particle-behavior-v2'"),
    ("PROJECT = W / 'hivemind-prefab-instance-source-closure-v1/projects/native-american-village'",
     "PROJECT = W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'")]:
    assert SCRIPT.count(original)==1;SCRIPT=SCRIPT.replace(original,replacement)
needle="pins.update(source['input_pins']);pins.update(source['output_pins'])"
assert SCRIPT.count(needle)==1
SCRIPT=SCRIPT.replace(needle,needle+"\n    repair_root=W/'delta-firepit-hdrp-closure-repair-v2'\n    repair=read(repair_root/'RESULT.v1.json');check(repair['input_pins'])\n    pins.update(repair['input_pins']);pins[str(repair_root/'RESULT.v1.json')]=sha(repair_root/'RESULT.v1.json')")
exec(compile(SCRIPT,str(BASE),'exec'),{'__name__':'__main__','__file__':str(Path(__file__).resolve())})
