"""Stream a compact GPU receipt; retain original pixels and large receipt untouched."""
from pathlib import Path
import json,hashlib
W=Path('F:/OpenRPG_ImportWork');I=W/'firepit-particle-mesh-gpu-v244';O=W/'firepit-particle-mesh-gpu-receipt-v249'
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 run=json.loads((I/'RUN-RESULT.v1.json').read_text('utf-8-sig'));assert run['gpu_exit_code']==0 and not O.exists()
 source=I/'gpu/RESULT.v1.json';O.mkdir();output=O/'GPU-METADATA.v1.json';skipping=False;indent=0;removed=0
 with source.open('r',encoding='utf-8') as src,output.open('x',encoding='utf-8') as dst:
  for line in src:
   if skipping:
    if line.strip() in [']','],'] and len(line)-len(line.lstrip())==indent:skipping=False
    continue
   if line.lstrip().startswith('"rgba": ['):indent=len(line)-len(line.lstrip());skipping=True;removed+=1;continue
   dst.write(line)
 assert not skipping and removed==336
 data=json.loads(output.read_text('utf-8'));assert len(data['rows'])==336 and all('rgba' not in r for r in data['rows'])
 proof=dict(status='large_gpu_receipt_streamed_without_pixel_array_duplication',original_receipt=str(source),original_receipt_sha256=sha(source),original_bytes=source.stat().st_size,metadata=str(output),metadata_sha256=sha(output),removed_redundant_decoded_rgba_arrays=removed,raw_half_and_float_pixels_preserved=True,full_import=False)
 (O/'RESULT.v1.json').write_text(json.dumps(proof,indent=2),'utf-8');print(json.dumps(proof))
if __name__=='__main__':main()
