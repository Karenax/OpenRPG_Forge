"""Close only the inspected thumbnail capture scope; do not install or qualify Forge."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,struct
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'house-source-thumbnail-reopen-v1';CAPTURE=W/'unity-library-source-thumbnail-house-bench-v8'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def main():
    assert not ROOT.exists();ROOT.mkdir()
    native=read(CAPTURE/'NATIVE-RESULT.v1.json');run=read(CAPTURE/'RUN-RESULT.v1.json')
    assert run['actual_exit_code']==0 and run['source_inputs_unchanged']
    assert native['source_object_unchanged'] and native['presentation_only']
    assert native['presentation_fixed_ev']==8 and native['presentation_tonemapping']=='Neutral'
    assert len(native['images'])==2 and len(native['compiled_passes'])==8 and all(p['compiled'] for p in native['compiled_passes'])
    pins=read(CAPTURE/'PREPARED.v1.json')['input_pins']
    for p,digest in pins.items():assert sha(p)==digest,p
    for row in native['images']:
        p=Path(row['path']);assert p.resolve().parent== (CAPTURE/'images').resolve()
        data=p.read_bytes();assert data[:8]==b'\x89PNG\r\n\x1a\n' and data[12:16]==b'IHDR'
        assert struct.unpack('>II',data[16:24])==(512,512)
        assert sha(p)==row['sha256'] and row['clipped_rgb_pixels']==0
        pins[str(p)]=row['sha256']
    for p in [Path(__file__).resolve(),CAPTURE/'NATIVE-RESULT.v1.json',CAPTURE/'RUN-RESULT.v1.json',CAPTURE/'PREPARED.v1.json']:pins[str(p)]=sha(p)
    review=dict(status='source_thumbnail_candidates_accepted_by_agent_for_indexing',reviewed_utc=datetime.now(timezone.utc).isoformat(),agent_review=True,human_review=False,observed='Both inspected views show the complete blue painted bench, individual slats and surface wear. No all-white saturation or generic placeholder.',images=native['images'],native_receipt_sha256=sha(CAPTURE/'NATIVE-RESULT.v1.json'),presentation_only=True,original_unity_version_parity=False,images_installed_in_vault=False,forge_editor_qualified=False,full_import=False)
    review_path=CAPTURE/'VISUAL-REVIEW.v1.json';assert not review_path.exists();review_path.write_text(json.dumps(review,indent=2),'utf-8');pins[str(review_path)]=sha(review_path)
    result=dict(status='two_house_native_source_thumbnails_independently_reopened_agent_reviewed',finished_utc=datetime.now(timezone.utc).isoformat(),sub_batch='L08.IMAGE.HOUSE.01',images_verified=2,agent_review=True,human_review=False,input_pins=pins,source_inputs_unchanged=True,presentation_only=True,images_installed_in_vault=False,geometry_converted=False,forge_editor_qualified=False,full_import=False)
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
