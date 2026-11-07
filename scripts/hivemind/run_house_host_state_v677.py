"""Resolve the isolated probe lock offline before pinning and building with --locked."""
from pathlib import Path
PARENT=Path(__file__).with_name('run_house_host_state_v676.py')
recipe=PARENT.read_text('utf-8-sig').replace('v676','v677')
anchor="code=BASE.read_text('utf-8-sig')"
addition='''
lock_step="""lock_env=os.environ.copy()
lock_env.update(PATH=str(BIN)+os.pathsep+lock_env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),TEMP=str(O/'temp'),TMP=str(O/'temp'))
with (O/'lock-generation.log').open('xb') as lock_log:
 result=subprocess.run([str(BIN/'cargo.exe'),'generate-lockfile','--offline','--manifest-path',str(O/'Cargo.toml')],env=lock_env,cwd=O,stdout=lock_log,stderr=lock_log,creationflags=subprocess.CREATE_NO_WINDOW)
assert result.returncode==0,'See lock-generation.log'
"""
needle="state_module=R/'apps/forge_studio/src/authoring_assets/source_graph_state.rs'"
assert code.count(needle)==1
code=code.replace(needle,lock_step+needle)
'''
assert recipe.count(anchor)==1
recipe=recipe.replace(anchor,anchor+addition)
exec(compile(recipe,str(PARENT)+'#offline-isolated-lock-reconciliation','exec'),globals())
