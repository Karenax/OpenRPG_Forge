"""Snapshot authoritative catalogue states, without promoting them to runtime proof."""
from pathlib import Path
from datetime import datetime,timezone
from collections import Counter,defaultdict
import hashlib,json,importlib.util
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault');O=W/'hivemind-remaining-work-v599'
HELPER=Path('C:/Users/karen/.codex/skills/openrpg-import-assets/scripts/source_support_import.py')
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
spec=importlib.util.spec_from_file_location('source_text_validation',HELPER);skill=importlib.util.module_from_spec(spec);spec.loader.exec_module(skill)
mp=V/'metadata/asset-import-registry.v1.json';sp=V/'metadata/asset-source-support-registry.v1.json';mr=read(mp);sr=read(sp);pins={str(p):sha(p) for p in [Path(__file__),HELPER,mp,sp]}
sourcepath=V/sr['path'];assert sha(sourcepath)==sr['sha256'];pins[str(sourcepath)]=sr['sha256'];sources=read(sourcepath)['assets']
models=[];encoding=[];packs={};states=Counter()
def inspect_text(card,path):
    for field in ['names','descriptions','tags']:
        value=card.get(field,{})
        strings=list(value.items()) if isinstance(value,dict) else list(enumerate(value))
        for key,text in strings:
            if not isinstance(text,str):continue
            try:skill.validate_text_encoding(text,card['id']+'.'+field)
            except ValueError as exc:encoding.append(dict(id=card['id'],catalogue=str(path),field=field,key=str(key),reason=str(exc)))
for pack,entry in sorted(mr['packs'].items()):
    if not pack.startswith('hivemind.'):continue
    for name,h in entry['files'].items():
        p=V/entry['path']/name;assert sha(p)==h,p;pins[str(p)]=h
    path=V/entry['path']/'details.v1.json';detail=read(path);assert detail['pack_id']==pack
    assets=detail['assets'];models.extend(assets)
    packs[pack]=dict(model_cards=len(assets),declared_states=dict(Counter(k for a in assets for k,v in a.get('readiness',{}).items() if v is True)))
    for a in assets:
        assert a['license']=='LicenseRef-Unity-Asset-Store-EULA' and a['private_only']
        states.update(k for k,v in a.get('readiness',{}).items() if v is True)
        inspect_text(a,path)
support=Counter();missing=Counter();reasons=Counter();components=Counter()
for a in sources:
    if not a['pack_id'].startswith('hivemind.'):continue
    assert a['license']=='LicenseRef-Unity-Asset-Store-EULA' and a['private_only'] and a['readiness']['converted'] is False
    support[a['pack_id']]+=1
    if not a.get('preview'):missing[a['pack_id']]+=1
    reasons[a.get('reason','unspecified')]+=1
    for c in a.get('unported_components') or []:components[str(c)]+=1
    inspect_text(a,sourcepath)
for pack in sorted(set(packs)|set(support)):
    packs.setdefault(pack,dict(model_cards=0,declared_states={}))
    packs[pack].update(source_support_cards=support[pack],source_cards_without_primary_image=missing[pack])
assert len({a['id'] for a in models})==len(models)
assert not {a['id'] for a in models}&{a['id'] for a in sources}
assert sha(mp)==pins[str(mp)] and sha(sp)==pins[str(sp)],'Catalogue changed during snapshot'
result=dict(status='current_catalogue_states_and_remaining_work_inventoried',model_cards=len(models),source_support_cards=sum(support.values()),source_cards_without_primary_image=sum(missing.values()),declared_model_states=dict(states),packs=packs,source_support_reason_counts=dict(reasons),unported_component_counts=dict(components),text_encoding_findings=encoding,text_encoding_finding_count=len(encoding),input_pins=pins,scope='Catalogue metadata SHA verified. Counts are cards including LOD and package variants, not distinct artistic objects. Model readiness is declared by existing catalogues, not newly qualified here. No GLB, runtime, GUI or full Vault transfer requalification.',full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2,ensure_ascii=True),'utf-8')
lines=['# Etat des catalogues Hivemind','',f"Fiches modele : {len(models)} ; fiches source non converties : {sum(support.values())} ; fiches source sans image principale : {sum(missing.values())}.",'','Ces compteurs incluent les variantes LOD et variantes de packs. Les etats declares des catalogues ne constituent pas une nouvelle qualification Studio/runtime.','',f"Champs texte a corriger selon le validateur du skill : {len(encoding)}.",'','| Pack | Modeles | Sources non converties | Sources sans image |','| --- | ---: | ---: | ---: |']
for pack,row in packs.items():lines.append(f"| {pack} | {row['model_cards']} | {row['source_support_cards']} | {row['source_cards_without_primary_image']} |")
lines+=['','## Raisons de conversion encore incomplete','']
for reason,count in reasons.most_common():lines.append(f'- {count} fiches : {reason}')
lines+=['','Preuves detaillees et hashes : RESULT.v1.json. Aucun asset, source ou licence modifie.']
(O/'BILAN.md').write_text('\n'.join(lines)+'\n','utf-8')
print(json.dumps({k:result[k] for k in ['model_cards','source_support_cards','source_cards_without_primary_image','declared_model_states','text_encoding_finding_count','source_support_reason_counts']}))
