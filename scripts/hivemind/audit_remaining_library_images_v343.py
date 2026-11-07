"""Snapshot current missing images and unattempted Pirate LOD0 source cards."""
from pathlib import Path
from datetime import datetime,timezone
from collections import Counter
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault');O=W/'library-current-missing-images-v343'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
pointer=V/'metadata/asset-source-support-registry.v1.json';registry=read(pointer);catalog=V/registry['path']
assert sha(catalog)==registry['sha256']
assets=read(catalog)['assets'];assert len({a['id'] for a in assets})==len(assets)
missing=[a for a in assets if not a.get('preview')]
assert len(missing)==719
attempted=set();pins={str(p):sha(p) for p in [Path(__file__),pointer,catalog,V/'metadata/asset-import-registry.v1.json',W/'pirate-p15-thumbnail-delivery-v340/RESULT.v1.json']}
for batch in sorted(W.glob('pirate-source-thumbnail-batch-*/PREPARED.v1.json')):
    receipt=read(batch)
    if 'cases' not in receipt:continue
    attempted.update(c['id'] for c in receipt['cases']);pins[str(batch)]=sha(batch)
eligible=[a['id'] for a in missing if a['pack_id']=='hivemind.pirate-island' and a.get('original_lod_level')==0 and '/HDRP' in a.get('source_asset_path','') and a['source_asset_path'].endswith('.prefab') and a['id'] not in attempted]
assert not eligible
counts=Counter(a['pack_id'] for a in missing);lods=Counter(str(a.get('original_lod_level')) for a in missing)
broken=[a['id'] for a in assets if a.get('preview') and not (V/a['preview']).is_file()]
assert not broken
result=dict(status='current_missing_image_inventory_verified',revision=registry['revision'],source_cards=len(assets),missing_primary_images=len(missing),missing_by_pack=dict(sorted(counts.items())),missing_by_original_lod=dict(sorted(lods.items())),unattempted_pirate_hdrp_lod0_ids=eligible,missing_declared_preview_files=broken,missing_cards=[dict(id=a['id'],pack_id=a['pack_id'],original_lod_level=a.get('original_lod_level'),source_asset_path=a.get('source_asset_path')) for a in missing],input_pins=pins,full_vault_rescan=False,complete_conversion_increment=0,actual_studio_gui_verified=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
lines=['# Images restantes apres P15','',f"Catalogue : {registry['revision']}",'',f'{len(missing)} fiches source sans image principale. Aucun prefab Pirate HDRP LOD0 encore non tente ; les refus anterieurs restent ouverts. Aucun chemin de vignette declare ne manque sur disque. Cela ne qualifie ni conversion complete ni interface Studio.','','| Pack | Images manquantes |','|---|---:|']
lines.extend(f'| {p} | {n} |' for p,n in sorted(counts.items()))
lines.extend(['','| LOD source | Images manquantes |','|---|---:|'])
lines.extend(f'| {p} | {n} |' for p,n in sorted(lods.items()))
(O/'IMAGES_RESTANTES.md').write_text('\n'.join(lines)+'\n','utf-8')
print(json.dumps({k:result[k] for k in ['missing_primary_images','missing_by_pack','missing_by_original_lod','unattempted_pirate_hdrp_lod0_ids']}))
