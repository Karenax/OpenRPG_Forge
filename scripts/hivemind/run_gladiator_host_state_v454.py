"""Reconcile the isolated root lock without changing resolved dependency versions."""
from pathlib import Path

parent=Path(__file__).resolve().with_name('run_gladiator_host_state_v453.py')
code=parent.read_text('utf-8').replace('gladiator-current-host-state-v453','gladiator-current-host-state-v454').replace('gladiator-host-state-v453','gladiator-host-state-v454')
anchor="state_module=R/'apps/forge_studio/src/authoring_assets/source_graph_state.rs'"
assert code.count(anchor)==1
extra='''
import tomllib
before_lock=tomllib.loads((O/'Cargo.lock').read_text('utf-8'))
lock_env=os.environ.copy();lock_env.update(PATH=str(BIN)+os.pathsep+lock_env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),TEMP=str(O/'temp'),TMP=str(O/'temp'))
with (O/'lock-reconciliation.log').open('xb') as log:
    reconciled=subprocess.run([str(BIN/'cargo.exe'),'update','--offline','--workspace','--manifest-path',str(O/'Cargo.toml')],env=lock_env,cwd=O,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
assert reconciled.returncode==0,'Offline root lock reconciliation refused'
after_lock=tomllib.loads((O/'Cargo.lock').read_text('utf-8'))
identities=lambda data:{(p['name'],p['version'],p.get('source')) for p in data['package']}
assert identities(after_lock).issubset(identities(before_lock)), 'Dependency versions changed'
(O/'LOCK-RECONCILIATION.v1.json').write_text(json.dumps(dict(existing_dependency_versions_unchanged=True,offline=True,before_packages=len(before_lock['package']),after_packages=len(after_lock['package'])),indent=2),'utf-8')
'''
code=code.replace(anchor,extra+'\n'+anchor)
code=code.replace("state=dict(status='host_state_prepared'", "pins[str(parent)]=sha(parent);pins[str(O/'LOCK-RECONCILIATION.v1.json')]=sha(O/'LOCK-RECONCILIATION.v1.json');state=dict(status='host_state_prepared'")
exec(compile(code,str(parent)+'#offline-lock-root-v454','exec'),globals())
