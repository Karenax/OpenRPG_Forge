"""Inventory current missing source previews; never count them as conversions."""
from pathlib import Path
from collections import Counter
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault');O=W/'remaining-source-images-v949'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    pointer=V/'metadata/asset-source-support-registry.v1.json';pin=sha(pointer);state=read(pointer);catalog_path=V/state['path'];catalog_sha=sha(catalog_path);assert catalog_sha==state['sha256'];catalog=read(catalog_path)
    assets=catalog['assets'];assert len(assets)==12517
    missing=[a for a in assets if not a.get('preview')];assert len(missing)==515
    rows=[{k:a.get(k) for k in ['id','pack_id','support_kind','source_asset_path','source_guid','original_lod_level','source_file','source_sha256','source_meta_file','source_meta_sha256','source_archive','source_archive_sha256','source_manifest_path','source_manifest_sha256']} for a in missing]
    counts=Counter(a['pack_id'] for a in missing);groups=[]
    for pack,count in counts.most_common():
        group=[a for a in rows if a['pack_id']==pack]
        groups.append(dict(pack_id=pack,cards_missing=count,support_kinds=dict(Counter(a['support_kind'] for a in group)),cards=group))
    assert sha(pointer)==pin and sha(catalog_path)==catalog_sha
    O.mkdir(exist_ok=False);result=dict(status='current_515_missing_source_images_inventoried',finished_utc=datetime.now(timezone.utc).isoformat(),source_cards=12517,source_cards_with_preview=12517-515,source_cards_missing_preview=515,groups=groups,input_pins={str(pointer):pin,str(catalog_path):catalog_sha},scope='Source-support primary preview presence only; image fidelity, full model image coverage, conversion, GUI and runtime remain separate.',full_import=False)
    (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2,ensure_ascii=False),'utf-8')
    print(json.dumps(dict(source_cards_missing_preview=515,groups=[{k:g[k] for k in ['pack_id','cards_missing','support_kinds']} for g in groups]),ensure_ascii=False))
if __name__=='__main__':main()
