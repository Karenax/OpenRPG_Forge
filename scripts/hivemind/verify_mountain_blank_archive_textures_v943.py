"""Verify two uniform textures against actual original unitypackage payloads."""
from pathlib import Path
from datetime import datetime,timezone
import gzip,hashlib,json,re,tarfile
import numpy as np
from PIL import Image
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault');O=W/'mountain-blank-source-archive-proof-v943'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def main():
    manifest_path=W/'hivemind-run/mountain-ancient-mountain/source.v1.json';manifest=read(manifest_path)
    pointer_path=V/'metadata/asset-source-support-registry.v1.json';pointer=read(pointer_path);catalog=read(V/pointer['path'])
    card=next(c for c in catalog['assets'] if c['id']=='hivemind.mountain-ancient-mountain.prefab-sm-mountaintree-02-abc00000000006886554338819280193')
    assert sha(manifest_path)==card['source_manifest_sha256']
    archive=V/card['source_archive'];before=archive.stat();assert before.st_size==card['source_archive_bytes']
    archive_sha=sha(archive);assert archive_sha==card['source_archive_sha256']==manifest['archive_sha256']
    source_root=Path(manifest['source_root']);texture_relative=Path('Assets/HIVEMIND/HDRP/TheMessengerMountain/Art/Textures');wanted={}
    for name in ['T_FantasyTree01_BaseColor.png','T_Bark01_ARH.png']:
        p=source_root/texture_relative/name;meta=Path(str(p)+'.meta');guid=re.search(r'^guid: (\w+)$',meta.read_text('utf-8-sig'),re.M)[1]
        assert manifest['guid_paths'][guid]==(texture_relative/name).as_posix()
        wanted[guid]=dict(source=str(p),source_meta=str(meta),source_sha256=sha(p),source_meta_sha256=sha(meta),archived_sha256=None)
    with gzip.open(archive,'rb') as compressed,tarfile.open(fileobj=compressed,mode='r|') as tar:
        for member in tar:
            name=member.name.removeprefix('./');parts=name.split('/')
            if len(parts)!=2 or parts[0] not in wanted or parts[1]!='asset':continue
            row=wanted[parts[0]];assert row['archived_sha256'] is None and member.isfile() and member.size<=50*1024*1024
            stream=tar.extractfile(member);assert stream is not None
            with stream:row['archived_sha256']=hashlib.file_digest(stream,'sha256').hexdigest()
            row['archive_member']=member.name;row['archive_bytes']=member.size
    after=archive.stat();assert before.st_size==after.st_size and before.st_mtime_ns==after.st_mtime_ns
    for row in wanted.values():
        assert row['archived_sha256']==row['source_sha256'] and sha(row['source'])==row['source_sha256']
        with Image.open(row['source']) as image:
            pixels=np.asarray(image.convert('RGBA'));assert pixels.shape==(2048,2048,4) and not np.any(pixels)
        row['rgba_all_zero']=True;row['pixels_verified']=2048*2048
    O.mkdir(exist_ok=False);result=dict(status='two_zero_rgba_textures_match_original_unitypackage_payloads',finished_utc=datetime.now(timezone.utc).isoformat(),archive=str(archive),archive_sha256=archive_sha,archive_bytes=before.st_size,rows=list(wanted.values()),source_manifest_sha256=sha(manifest_path),input_pins={str(manifest_path):sha(manifest_path),str(pointer_path):sha(pointer_path),str(Path(__file__).resolve()):sha(__file__)},source_mutations=0,repair_applied=False,root_cause_qualified=False,full_import=False)
    (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(dict(status=result['status'],textures_verified=2,archive_sha256=archive_sha)))
if __name__=='__main__':main()
