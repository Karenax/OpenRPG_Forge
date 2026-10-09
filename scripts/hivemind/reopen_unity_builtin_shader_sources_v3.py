"""Pin the official archive and actual installed include closure; keep differences."""
from pathlib import Path
import datetime
import hashlib
import json
import re
import shutil

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'unity-builtin-shaders-6000.5.8f1-v3'
SOURCE=W/'unity-builtin-shaders-6000.5.8f1-v2/source/builtin'
INSTALLED=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/CGIncludes')
SHADER=SOURCE/'DefaultResourcesExtra/Particle Alpha Blend.shader'
def sha(p):
    with Path(p).open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
def main():
    assert not ROOT.exists();ROOT.mkdir();(ROOT/'installed-includes').mkdir()
    assert 'Shader "Legacy Shaders/Particles/Alpha Blended"' in SHADER.read_text('utf-8-sig')
    licenses=[p for p in SOURCE.rglob('*') if p.is_file() and p.name.lower() in {'license.txt','license.md'}]
    assert licenses
    pins={str(p):sha(p) for p in SOURCE.rglob('*') if p.is_file()}
    matches=[];differences=[]
    for p in SOURCE.rglob('*.cginc'):
        installed=INSTALLED/p.name
        if not installed.is_file():continue
        current=sha(installed);pins[str(installed)]=current
        if current==pins[str(p)]:matches.append(p.name)
        else:
            retained=ROOT/'installed-includes'/p.name
            shutil.copyfile(installed,retained);assert sha(retained)==current
            pins[str(retained)]=current
            differences.append(dict(file=p.name,official_archive_sha256=pins[str(p)],
                installed_sha256=current,installed_copy=str(retained)))
    assert matches and differences,'Retain the observed include difference rather than claiming byte equality'
    # Resolve the actual installed include dependency tree used by the shader.
    closure=[];seen=set();pending=re.findall(r'#include\s+"([^"]+)"',SHADER.read_text('utf-8-sig'))
    while pending:
        name=pending.pop()
        if name in seen:continue
        seen.add(name);path=(INSTALLED/name).resolve()
        assert path.is_file() and path.is_relative_to(INSTALLED.resolve()),'Include outside installed shader source closure'
        retained=ROOT/'installed-includes'/name
        if not retained.exists():shutil.copyfile(path,retained)
        assert sha(path)==sha(retained)
        pins[str(path)]=sha(path);pins[str(retained)]=sha(retained)
        closure.append(dict(name=name,installed=str(path),retained=str(retained),sha256=sha(path)))
        pending.extend(re.findall(r'#include\s+"([^"]+)"',path.read_text('utf-8-sig')))
    for p in [Path(__file__).resolve(),W/'unity-builtin-shaders-6000.5.8f1-v1/builtin_shaders-6000.5.8f1.zip',
              W/'unity-builtin-shaders-6000.5.8f1-v2/builtin_shaders-6000.5.8f1.zip',
              W/'unity-delta-firepit-effect-materials-v1/INDEPENDENT-REOPEN.v1.json']:
        pins[str(p)]=sha(p)
    assert all(sha(p)==d for p,d in pins.items())
    result=dict(status='official_shader_source_and_actual_installed_include_closure_reopened_with_differences',
        checked_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        release_page='https://unity.com/releases/editor/whats-new/6000.5.8f1',
        download_url='https://download.unity3d.com/download_unity/5cb7df797b7d/builtin_shaders-6000.5.8f1.zip',
        shader=str(SHADER),shader_sha256=sha(SHADER),licences=[dict(path=str(p),sha256=sha(p)) for p in licenses],
        installed_include_matches=matches,installed_include_differences=differences,
        actual_installed_include_closure=closure,input_pins=pins,
        entire_archive_equals_installed_sources=False,compiled_builtin_shader_source_equivalence_qualified=False,
        shader_ported=False,gpu_rendered=False,runtime_converted=False,full_import=False)
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps(dict(shader=str(SHADER),include_matches=len(matches),include_differences=len(differences),
        shader_include_closure=len(closure),full_import=False)))
if __name__=='__main__':main()
