"""Audit current model metadata separately from source thumbnail progress."""
from pathlib import Path
from collections import Counter
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault');O=W/'current-model-readiness-audit-v375'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
pointer=V/'metadata/asset-import-registry.v1.json';registry=read(pointer);pins={str(pointer):sha(pointer)}
totals=Counter();pack_rows=[];missing=[];ids=set();unqualified=[]
for pack_id,revision in sorted(registry['packs'].items()):
    base=V/revision['path'];assert base.resolve().is_relative_to(V.resolve())
    for name,digest in revision['files'].items():
        p=base/name;assert sha(p)==digest,p;pins[str(p)]=digest
    details=read(base/'details.v1.json');assert details['pack_id']==pack_id
    declared=Counter()
    for asset in details['assets']:
        assert asset['id'] not in ids;ids.add(asset['id'])
        assert asset.get('model_path') and asset.get('model_sha256')
        path=V/asset['model_path'];assert path.resolve().is_relative_to(V.resolve())
        if not path.is_file():missing.append(dict(id=asset['id'],path=str(path)))
        readiness=asset.get('readiness',{})
        for field,value in readiness.items():
            if value is True:declared[field]+=1;totals[field]+=1
        if readiness.get('full_import') is not True:unqualified.append(dict(id=asset['id'],pack_id=pack_id,declared_readiness=readiness,missing=asset.get('missing',[])))
    pack_rows.append(dict(pack_id=pack_id,model_cards=len(details['assets']),declared_readiness_true=dict(declared)))
sourcepointer=V/'metadata/asset-source-support-registry.v1.json';source_registry=read(sourcepointer);sourcepath=V/source_registry['path'];assert sha(sourcepath)==source_registry['sha256']
source=read(sourcepath)['assets'];pins[str(sourcepointer)]=sha(sourcepointer);pins[str(sourcepath)]=sha(sourcepath);pins[str(Path(__file__))]=sha(Path(__file__))
for p,h in pins.items():assert sha(Path(p))==h,p
result=dict(status='current_model_metadata_and_file_presence_audited',model_cards=len(ids),declared_readiness_true=dict(totals),pack_rows=pack_rows,missing_declared_model_files=missing,model_cards_without_declared_full_import=len(unqualified),unqualified_model_cards=unqualified,source_support_cards=len(source),source_cards_declared_converted=sum(a.get('readiness',{}).get('converted') is True for a in source),source_primary_images_missing=sum(not a.get('preview') for a in source),input_pins=pins,model_content_rehashed=False,full_vault_rescan=False,actual_studio_gui_verified=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
lines=['# Etat des conversions : metadonnees courantes','',f"{len(ids)} fiches de modeles, {len(source)} fiches source support.",f"{len(missing)} chemins de modele declares absents. {result['source_primary_images_missing']} fiches source sans image.",'','Les nombres ci-dessous sont des etats declares dans les metadonnees ; ce controle ne revalide pas le contenu GLB, le runtime ou la GUI. Il ne prouve pas une conversion complete.','','| Etat declare vrai | Fiches modele |','|---|---:|']
lines.extend(f'| {k} | {v} |' for k,v in sorted(totals.items()))
(O/'ETAT_CONVERSIONS.md').write_text('\n'.join(lines)+'\n','utf-8')
print(json.dumps({k:result[k] for k in ['model_cards','declared_readiness_true','model_cards_without_declared_full_import','source_support_cards','source_cards_declared_converted','source_primary_images_missing']}))
