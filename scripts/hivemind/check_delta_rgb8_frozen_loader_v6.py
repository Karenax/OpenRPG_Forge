"""Snapshot current installed RGB8 and all real engine sources; preserve live-build drift."""
from pathlib import Path
import datetime
import hashlib
import importlib.util
import json
import os
import re
import shutil
import subprocess
import sys
import tomllib

W=Path('F:/OpenRPG_ImportWork')
REPO=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
ROOT=W/'delta-rgb8-frozen-canonical-loader-v6'
SNAPSHOT=ROOT/'snapshot'
BASE=Path(__file__).with_name('check_delta_rgb8_frozen_loader_v1.py')
PREVIOUS=W/'delta-rgb8-canonical-loader-native-v2'
PROVEN=W/'delta-rgb8-frozen-canonical-loader-v5'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def write(name,value):
    with (ROOT/name).open('x',encoding='utf-8') as stream:
        json.dump(value,stream,indent=2,allow_nan=False)


def prepare():
    assert not ROOT.exists(),'Preserve prior snapshot and logs'
    prior=read(PREVIOUS/'RUN-RESULT.v1.json')
    assert prior['status']=='refused_or_unverified' and prior['actual_exit_code']==0
    assert 'test result: ok. 17 passed; 0 failed;' in (PREVIOUS/'native.stdout.log').read_text('utf-8-sig')
    assert prior['error'].startswith('AssertionError: Changed input:')
    transition=read(PREVIOUS/'SOURCE-TRANSITION-INSTALLED.v1.json')
    assert sha(PREVIOUS/'SOURCE-TRANSITION-INSTALLED.v1.json')==prior['source_transition_sha256']
    for row in transition['files']:
        assert sha(row['path'])==sha(row['candidate'])==row['after_sha256']
        assert sha(row['backup'])==row['before_sha256']
    assert read(PROVEN/'RUN-RESULT.v1.json')['actual_host_tests_passed']==17
    files={p for folder in [REPO/'crates',REPO/'third_party/rerecast-0.4.0']
        for p in folder.rglob('*') if p.is_file() and not any(part in {'target','.git','node_modules'} for part in p.parts)}
    files.update(REPO/'apps/forge_studio/src/authoring_assets'/(name+'.rs') for name in
        ['source_graph_scene','source_graph_resources','source_graph_frame','source_graph_intake','source_graph_host'])
    files.update([REPO/'Cargo.toml',REPO/'Cargo.lock'])
    # Reuse the previously proven literal-resource inclusion list, copying the
    # current originals, then discover any new direct includes in current Rust.
    old_snapshot=W/'delta-rgb8-frozen-canonical-loader-v1/snapshot'
    for value in read(PROVEN/'PREPARED.v1.json')['input_pins']:
        p=Path(value)
        if p.is_relative_to(old_snapshot):
            original=REPO/p.relative_to(old_snapshot)
            if original.is_file():files.add(original)
    literal=re.compile(r'include_(?:str|bytes)!\(\s*"([^"\r\n]+)"\s*\)')
    for path in tuple(files):
        if path.suffix!='.rs':continue
        for relative in literal.findall(path.read_text('utf-8-sig')):
            included=(path.parent/relative).resolve()
            assert included.is_file() and included.is_relative_to(REPO.resolve()), 'Literal include unavailable: '+str(included)
            files.add(included)
    assert 500<len(files)<1800 and sum(p.stat().st_size for p in files)<80*1024*1024
    ROOT.mkdir();(ROOT/'src').mkdir();(ROOT/'temp').mkdir()
    originals={};frozen={};rows=[]
    for source in sorted(files):
        assert not source.is_symlink()
        digest=sha(source);target=SNAPSHOT/source.relative_to(REPO)
        target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,target)
        assert sha(target)==digest==sha(source),'Concurrent source copy changed: '+str(source)
        originals[str(source)]=digest;frozen[str(target)]=digest
        rows.append(dict(original=str(source),copied=str(target),sha256=digest))
    assert all(sha(p)==d for p,d in originals.items()),'Concurrent source snapshot incoherent'
    root_manifest=SNAPSHOT/'Cargo.toml';text=root_manifest.read_text('utf-8-sig')
    original_toml=tomllib.loads(text)
    excluded=['apps/forge_runtime','apps/forge_studio','tools/forge_cli']
    for member in excluded:
        line=f'    "{member}",\n';assert text.count(line)==1;text=text.replace(line,'')
    root_manifest.write_text(text,'utf-8');restored=tomllib.loads(text)
    restored['workspace']['members']=original_toml['workspace']['members']
    assert restored==original_toml
    frozen[str(root_manifest)]=sha(root_manifest)
    for name in ['Cargo.toml','src/main.rs']:
        text=(PROVEN/name).read_text('utf-8-sig').replace(old_snapshot.as_posix(),SNAPSHOT.as_posix())
        assert old_snapshot.as_posix() not in text
        (ROOT/name).write_text(text,'utf-8')
    shutil.copyfile(PROVEN/'Cargo.lock',ROOT/'Cargo.lock')
    # Identical registry versions/checksums; never resolve a fallback fork.
    actual_lock=tomllib.loads((REPO/'Cargo.lock').read_text('utf-8-sig'))
    probe_lock=tomllib.loads((ROOT/'Cargo.lock').read_text('utf-8-sig'))
    identities={(p['name'],p['version'],p.get('source'),p.get('checksum')) for p in actual_lock['package']}
    assert all(not p.get('source') or (p['name'],p['version'],p.get('source'),p.get('checksum')) in identities for p in probe_lock['package'])
    changed=[]
    for p,d in transition['input_pins'].items():
        current=sha(p)
        if current!=d:changed.append(dict(path=p,before_sha256=d,current_sha256=current))
    assert changed
    for path in [Path(__file__).resolve(),BASE,ROOT/'Cargo.toml',ROOT/'Cargo.lock',ROOT/'src/main.rs',
        PREVIOUS/'RUN-RESULT.v1.json',PREVIOUS/'native.stdout.log',PREVIOUS/'native.stderr.log',
        PREVIOUS/'SOURCE-TRANSITION-INSTALLED.v1.json',PROVEN/'RUN-RESULT.v1.json']:
        frozen[str(path)]=sha(path)
    write('SNAPSHOT.v1.json',dict(original_sources=rows,original_input_pins=originals,frozen_input_pins=frozen,
        prior_current_build_changed_inputs=changed,prior_seventeen_tests_executed=True,
        prior_global_unchanged_source_qualification_refused=True,excluded_workspace_members_only=excluded,
        installed_rgb8_source_modules_frozen=3,registry_dependencies_match_actual_application_lock=True,
        local_rerecast_fork_preserved=True,current_checkout_unchanged=True,full_import=False))
    write('PREPARED.v1.json',dict(input_pins=frozen,target=read(PREVIOUS/'PREPARED.v1.json')['target'],
        wait_pid=prior['cargo_pid'],wait_birth_ticks='0',expected_tests=17,
        actual_source_files_copied=len(rows),canonical_checkout_unchanged=True,full_import=False))
    with (ROOT/'controller.log').open('xb') as log:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,
            stdin=subprocess.DEVNULL,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
    write('JOB.v1.json',dict(controller_pid=child.pid,created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()))
    print(json.dumps(dict(controller_pid=child.pid,frozen_current_sources=len(rows),prior_changed_inputs=len(changed),
        expected_tests=17,current_checkout_unchanged=True,full_import=False)))


if __name__=='__main__':
    if len(sys.argv)>1 and sys.argv[1]=='run':
        spec=importlib.util.spec_from_file_location('frozen_loader',BASE)
        native=importlib.util.module_from_spec(spec);spec.loader.exec_module(native)
        native.ROOT=ROOT;native.SNAPSHOT=SNAPSHOT
        native.run()
    else:prepare()
