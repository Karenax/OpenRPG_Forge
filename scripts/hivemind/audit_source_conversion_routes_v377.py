"""Reconcile source prefab identities with converted catalogs and retain route blockers."""
from pathlib import Path
from collections import Counter,defaultdict
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault');O=W/'source-conversion-route-audit-v377'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
sourcepointer=V/'metadata/asset-source-support-registry.v1.json';sr=read(sourcepointer);sourcepath=V/sr['path'];assert sha(sourcepath)==sr['sha256']
assets=read(sourcepath)['assets'];mp=V/'metadata/asset-import-registry.v1.json';models=read(mp)
pins={str(p):sha(p) for p in [Path(__file__),sourcepointer,sourcepath,mp]}
converted=defaultdict(list);catalogs={};unavailable_catalogs=[]
for pack_id,revision in sorted(models['packs'].items()):
    details_path=V/revision['path']/'details.v1.json';assert sha(details_path)==revision['files']['details.v1.json'];pins[str(details_path)]=sha(details_path)
    for model in read(details_path)['assets']:
        prefab=model.get('source_prefab')
        if not isinstance(prefab,dict) or not prefab.get('sha256'):continue
        path=Path(model['source_catalog'])
        if str(path) not in catalogs:
            if not path.is_file():
                unavailable_catalogs.append(str(path));catalogs[str(path)]=None
            else:
                data=read(path);pins[str(path)]=sha(path)
                catalogs[str(path)]={a['id']:a for a in data['assets']},data.get('archive_sha256')
        catalog=catalogs[str(path)]
        if catalog is None:continue
        rows,archive=catalog;assert model['id'] in rows
        native=rows[model['id']]
        level=native.get('original_lod_level')
        converted[(pack_id,prefab['path'],prefab['sha256'],archive)].append(dict(model_id=model['id'],model_path=model['model_path'],original_lod_level=level,lod_explicitly_declared=level is not None,source_catalog=str(path)))
routes=Counter(a['support_kind'] for a in assets);matches=[];unmatched=[];mirror_by_pack=Counter();route_by_pack=defaultdict(Counter)
for asset in assets:
    route_by_pack[asset['pack_id']][asset['support_kind']]+=1
    identity=tuple(asset.get(field) for field in ['pack_id','source_asset_path','source_sha256','source_archive_sha256']);candidates=converted.get(identity,[]) if all(identity) else []
    if candidates:
        level=asset.get('original_lod_level')
        strict=[c for c in candidates if level is not None and c['lod_explicitly_declared'] and c['original_lod_level']==level]
        matches.append(dict(source_id=asset['id'],source_lod_level=level,same_pack_prefab_hash_and_archive_candidates=candidates,explicit_lod_matches=strict,conversion_or_readiness_changed=False))
    else:unmatched.append(asset['id'])
    if asset['support_kind'] in {'source_support_hdrp_mirror_normals','source_support_hdrp_double_sided_mirror_normal_material'}:mirror_by_pack[asset['pack_id']]+=1
engine=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_engine/src/source_fragment.rs');shader=engine.parent/'shaders/source_fragment_common.wgsl'
assert 'SourceBackfaceMode::Mirror => 2.0' in engine.read_text('utf-8-sig')
pins[str(engine)]=sha(engine);pins[str(shader)]=sha(shader)
for p,h in pins.items():assert sha(Path(p))==h,p
result=dict(status='source_conversion_routes_and_identity_candidates_audited',source_cards=len(assets),support_route_counts=dict(sorted(routes.items())),route_counts_by_pack={p:dict(c) for p,c in sorted(route_by_pack.items())},same_pack_prefab_hash_archive_candidate_source_cards=len(matches),explicit_same_lod_candidate_source_cards=sum(bool(m['explicit_lod_matches']) for m in matches),identity_matches=matches,unmatched_source_ids=unmatched,unavailable_model_catalogs=sorted(set(unavailable_catalogs)),mirror_source_cards_by_pack=dict(mirror_by_pack),engine_declares_mirror_mode=True,mirror_source_asset_qualification=False,mirror_additional_requirements='Medieval cases also declare active BasicTextured mask/opacity and include skinned cloth; mirror mode alone does not qualify the complete source object.',input_pins=pins,vault_modified=False,conversion_increment=0,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
lines=['# Lots de conversion restant a qualifier','',f"{len(assets)} fiches source support. {len(matches)} fiches ont un candidat avec meme pack, prefab SHA et archive SHA ; {result['explicit_same_lod_candidate_source_cards']} ont aussi un LOD explicitement concordant. Aucun candidat n'est promu sans validation de son objet complet et de ses materiaux.",'','| Route source | Fiches |','|---|---:|']
lines.extend(f'| {k} | {v} |' for k,v in sorted(routes.items()))
lines.extend(['','## Normales doubles faces miroir','',f"{sum(mirror_by_pack.values())} fiches : "+', '.join(f'{p} ({n})' for p,n in sorted(mirror_by_pack.items()))+'.','Le moteur declare deja le mode Mirror, mais cela ne qualifie pas ces objets. Les cas Medieval mentionnent aussi masque/opacite actifs et comprennent des cloth skinnes ; ils ne doivent pas etre aplatis en geometrie statique. La correction doit verifier les branches et le profil complet de chaque cas.','','Les 14 219 fiches modele declarees converties et les 12 517 fiches source support sont deux etats distincts. Ce rapport ne revalide pas les GLB, ne remplace pas le controle dans Studio et ne modifie aucun etat de conversion.'])
(O/'LOTS_CONVERSION.md').write_text('\n'.join(lines)+'\n','utf-8')
print(json.dumps({k:result[k] for k in ['source_cards','same_pack_prefab_hash_archive_candidate_source_cards','explicit_same_lod_candidate_source_cards','mirror_source_cards_by_pack','unavailable_model_catalogs']}))
