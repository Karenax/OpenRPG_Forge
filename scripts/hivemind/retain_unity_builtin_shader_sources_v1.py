"""Retain the official exact-version built-in shader archive, licence and pins."""
from pathlib import Path
import datetime
import hashlib
import json
import re
import stat
import urllib.request
import zipfile

ROOT=Path('F:/OpenRPG_ImportWork/unity-builtin-shaders-6000.5.8f1-v1')
URL='https://download.unity3d.com/download_unity/5cb7df797b7d/builtin_shaders-6000.5.8f1.zip'
PAGE='https://unity.com/releases/editor/whats-new/6000.5.8f1'
INSTALLED=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/CGIncludes')
def sha(path):
    with Path(path).open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()

def main():
    assert not ROOT.exists();ROOT.mkdir()
    archive=ROOT/'builtin_shaders-6000.5.8f1.zip'
    with urllib.request.urlopen(URL,timeout=60) as response, archive.open('xb') as output:
        total=0
        while block:=response.read(1024*1024):
            total+=len(block);assert total<50*1024*1024
            output.write(block)
    extracted=ROOT/'source';extracted.mkdir();pins={str(archive):sha(archive)}
    with zipfile.ZipFile(archive) as bundle:
        entries=bundle.infolist();assert len(entries)<2000 and sum(e.file_size for e in entries)<150*1024*1024
        for entry in entries:
            destination=(extracted/entry.filename).resolve()
            assert destination.is_relative_to(extracted.resolve())
            assert not stat.S_ISLNK(entry.external_attr>>16)
            if entry.is_dir():destination.mkdir(parents=True,exist_ok=True);continue
            destination.parent.mkdir(parents=True,exist_ok=True)
            with destination.open('xb') as out:out.write(bundle.read(entry))
            pins[str(destination)]=sha(destination)
    shaders=[p for p in extracted.rglob('*.shader') if re.search(
        r'Shader\s+"Legacy Shaders/Particles/Alpha Blended"',p.read_text('utf-8-sig'))]
    assert len(shaders)==1,'Exact captured shader declaration must be unique'
    licenses=[p for p in extracted.rglob('*') if p.is_file() and p.name.lower() in {'license.txt','license.md','licence.txt'}]
    assert licenses,'Retain official source licence'
    matches=[]
    for p in extracted.rglob('*.cginc'):
        installed=INSTALLED/p.name
        if installed.is_file():
            assert sha(p)==sha(installed),'Installed Unity include differs from release archive: '+p.name
            pins[str(installed)]=sha(installed);matches.append(p.name)
    assert len(matches)>20
    proof=Path('F:/OpenRPG_ImportWork/unity-delta-firepit-effect-materials-v1/INDEPENDENT-REOPEN.v1.json')
    pins[str(proof)]=sha(proof);pins[str(Path(__file__).resolve())]=sha(__file__)
    assert all(sha(p)==d for p,d in pins.items())
    result=dict(observed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        status='official_exact_version_shader_archive_and_installed_includes_reopened',
        release_page=PAGE,download_url=URL,archive_sha256=sha(archive),unity_version='6000.5.8f1',
        exact_captured_shader=str(shaders[0]),shader_sha256=sha(shaders[0]),
        licences=[dict(path=str(p),sha256=sha(p)) for p in licenses],
        installed_include_exact_matches=matches,input_pins=pins,
        shader_ported=False,gpu_rendered=False,runtime_converted=False,full_import=False)
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps(dict(shader=str(shaders[0]),installed_include_exact_matches=len(matches),full_import=False)))

if __name__=='__main__':main()
