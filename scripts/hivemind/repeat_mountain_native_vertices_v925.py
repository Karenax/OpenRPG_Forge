"""Repeat the same read-only diagnostic after the verified shutdown crash."""
from pathlib import Path
recipe=Path(__file__).with_name('inspect_mountain_native_vertices_v924.py')
code=recipe.read_text('utf-8')
begin=code.index('if len(sys.argv)==1:');end=code.index("\nelse:\n    assert sys.argv[1:]==['run']",begin)
initial=r'''if len(sys.argv)==1:
    prior=W/'mountain-native-vertex-diagnostic-v924'
    failed=read(prior/'RUN-RESULT.v1.json')
    assert failed['status']=='native_vertex_diagnostic_refusal_preserved' and failed['unity_exit_code']==3221225477 and not failed['input_changes']
    assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
    prepared=read(prior/'PREPARED.v1.json');pins=dict(prepared['input_pins'])
    for p,d in pins.items():assert sha(p)==d,p
    assert not O.exists();O.mkdir();(O/'temp').mkdir()
    manifest=read(prior/'manifest.json');manifest['output']=str(O/'NATIVE-RESULT.v1.json');path=O/'manifest.json';path.write_text(json.dumps(manifest,indent=2),'utf-8')
    pins.update({str(path):sha(path),str(Path(__file__).resolve()):sha(__file__),str(prior/'RUN-RESULT.v1.json'):sha(prior/'RUN-RESULT.v1.json'),str(prior/'NATIVE-RESULT.v1.json'):sha(prior/'NATIVE-RESULT.v1.json')})
    prepared['input_pins']=pins;(O/'PREPARED.v1.json').write_text(json.dumps(prepared,indent=2),'utf-8')
    with (O/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=R,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=proc.pid)),'utf-8');print(json.dumps(dict(status='same_native_diagnostic_repeated_after_terminal_shutdown_crash',pid=proc.pid)))
'''
code=code[:begin]+initial+code[end:]
code=code.replace("O=W/'mountain-native-vertex-diagnostic-v924'", "O=W/'mountain-native-vertex-diagnostic-v925'")
exec(compile(code,str(recipe)+'#same-helper-clean-shutdown-repeat','exec'),globals())
