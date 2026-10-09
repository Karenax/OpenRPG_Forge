from pathlib import Path
S=Path(__file__).parent
p=S/'check_native_capture_candidate_v1.py'
t=p.read_text().replace('native-texture-capture-contract-check-v1','native-texture-capture-contract-check-v2')
a="""    deps={}
    for name in ['forge_core','serde','serde_json']:
        p=max(DEPS.glob('lib'+name+'-*.rlib'),key=lambda p:p.stat().st_mtime);deps[str(p)]=sha(p);command+=['--extern',name+'='+str(p)]"""
b="""    core=max(DEPS.glob('libforge_core-*.rlib'),key=lambda p:p.stat().st_mtime)
    fingerprint=DEPS.parent/'.fingerprint'
    coreinfo=json.loads((fingerprint/core.stem.removeprefix('lib')/'lib-forge_core.json').read_text())
    expected={row[1]:row[3] for row in coreinfo['deps']}
    deps={}
    for name in ['forge_core','serde','serde_json']:
        if name=='forge_core':p=core
        else:
            matches=[p for p in DEPS.glob('lib'+name+'-*.rlib') if int.from_bytes(bytes.fromhex((fingerprint/p.stem.removeprefix('lib')/('lib-'+name)).read_text().strip()),'little')==expected[name]]
            assert len(matches)==1,(name,matches)
            p=matches[0]
        deps[str(p)]=sha(p);command+=['--extern',name+'='+str(p)]"""
assert a in t
(S/'check_native_capture_candidate_v2.py').write_text(t.replace(a,b),encoding='utf-8')
