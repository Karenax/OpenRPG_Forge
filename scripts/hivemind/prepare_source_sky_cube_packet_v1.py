"""Pack exact original half floats in declared mip-major order for admission."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,struct
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'source-sky-cube-packet-v1';CAP=W/'firepit-source-sky-reference-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    proof_path=W/'firepit-source-sky-reopen-v1/RESULT.v1.json';proof=read(proof_path)
    for p,h in proof['input_pins'].items():assert sha(p)==h,p
    payload=bytearray();mips=[];inputs=[]
    for row in proof['rows']:
        p=CAP/f'source-sky-0-mip-{row["mip"]}.rgba32f';assert sha(p)==row['rgba32f_sha256']
        original=p.read_bytes();values=struct.unpack('<'+str(len(original)//4)+'f',original)
        half=struct.pack('<'+str(len(values))+'e',*values)
        assert struct.unpack('<'+str(len(values))+'e',half)==values
        mips.append(dict(mip=row['mip'],width=row['width'],height=row['width'],layers=6,offset=len(payload),bytes=len(half),sha256=hashlib.sha256(half).hexdigest(),source_rgba32f_sha256=sha(p)))
        payload.extend(half);inputs.append(p)
    ROOT.mkdir();raw=ROOT/'source-sky.rgba16f';raw.write_bytes(payload)
    result=dict(schema='openrpg.source-prefiltered-sky-packet.v1',status='lossless_native_half_cube_packet_prepared_for_target_admission',
        created_utc=datetime.now(timezone.utc).isoformat(),path=str(raw),sha256=sha(raw),bytes=len(payload),
        format='Rgba16Float',dimension='D2',view_dimension='CubeArray',width=256,height=256,array_layers=6,cube_count=1,mip_count=9,
        data_order='MipMajor',face_order=['PositiveX','NegativeX','PositiveY','NegativeY','PositiveZ','NegativeZ'],mips=mips,
        default_source_lod_scale=6,native_values_preserved_exactly=True,reencoding_loss=False,
        source_context='Actual HDRP CustomUserHistory0 reference sky reflection; source-render acquisition, not a new authored model',
        target_storage_admitted=False,target_gpu_sampling_qualified=False,full_import=False,editor_visual_verified=False,
        input_pins={str(p):sha(p) for p in [Path(__file__).resolve(),proof_path,*inputs]},
        next_action='Implement strict shared cube admission and fail-closed scene dependency; validate storage and actual target GPU against the 182 source sampling cases before using in the original scene.')
    (ROOT/'PACKET.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','mips']}))
if __name__=='__main__':main()
