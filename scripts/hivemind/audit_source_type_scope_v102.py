"""Count manifest source types for lot planning, without claiming logical coverage."""
from pathlib import Path
from collections import Counter, defaultdict
from datetime import datetime, timezone
import csv
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
O = W / 'collection-source-type-scope-v102'
LINEAGE = W / 'collection-archive-lineage-reconciliation-v1/RESULT.v1.json'
CANDIDATE_TYPES = {'.prefab', '.unity', '.fbx', '.obj', '.blend', '.anim',
                   '.wav', '.ogg', '.mp3', '.aiff', '.aif', '.flac'}


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


assert not O.exists()
lineage = read(LINEAGE)
parents = {edge['child_pack']: edge['parent_pack'] for edge in lineage['edges']}


def root_pack(pack):
    seen = set()
    while pack in parents:
        assert pack not in seen, 'Archive lineage cycle'
        seen.add(pack)
        pack = parents[pack]
    return pack


pins = {str(LINEAGE): sha(LINEAGE), str(Path(__file__)): sha(Path(__file__))}
packs = []
objects = []
groups = defaultdict(list)
total = Counter()
for declared in lineage['manifests']:
    path = Path(declared['manifest'])
    digest = sha(path)
    assert digest == declared['manifest_sha256'], str(path)
    pins[str(path)] = digest
    manifest = read(path)
    counts = Counter()
    seen = set()
    for item in manifest['files']:
        if item['kind'] != 'asset':
            continue
        key = (item['guid'], item['source_path'])
        assert key not in seen, (str(path), key)
        seen.add(key)
        extension = Path(item['source_path']).suffix.lower() or '(sans extension)'
        counts[extension] += 1
        if extension not in CANDIDATE_TYPES:
            continue
        stored = (path.parent / item['stored_path']).resolve()
        assert stored.is_relative_to(path.parent.resolve()), str(stored)
        row = dict(pack_id=declared['pack_id'], root_pack_id=root_pack(declared['pack_id']),
                   guid=item['guid'], source_path=item['source_path'],
                   stored_path=str(stored), extension=extension,
                   declared_sha256=item['sha256'], declared_bytes=item['bytes'],
                   source_file_present=stored.is_file(), source_payload_rehashed=False,
                   conversion_verified=False, runtime_qualified=False)
        objects.append(row)
        groups[(row['root_pack_id'], row['guid'], extension)].append(row)
    total.update(counts)
    packs.append(dict(pack_id=declared['pack_id'], root_pack_id=root_pack(declared['pack_id']),
                      primary_source_entries=sum(counts.values()),
                      counts_by_extension=dict(sorted(counts.items()))))

assert len(packs) == lineage['imported_manifests'] == 48
variant_groups = []
for (root, guid, ext), rows in sorted(groups.items()):
    hashes = sorted({row['declared_sha256'] for row in rows})
    variant_groups.append(dict(root_pack_id=root, guid=guid, extension=ext,
                               occurrences=len(rows), distinct_declared_hashes=len(hashes),
                               pack_ids=sorted({row['pack_id'] for row in rows}),
                               automatically_merged=False))
missing = [row for row in objects if not row['source_file_present']]
O.mkdir()
(O / 'SOURCE_OBJECT_CANDIDATES.v1.json').write_text(
    json.dumps(objects, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
(O / 'VARIANT_GROUPS.v1.json').write_text(
    json.dumps(variant_groups, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
with (O / 'COUNTS_BY_PACK.tsv').open('w', encoding='utf-8', newline='') as stream:
    writer = csv.writer(stream, delimiter='\t')
    extensions = sorted(total)
    writer.writerow(['pack_id', 'root_pack_id', 'primary_source_entries', *extensions])
    for pack in packs:
        writer.writerow([pack['pack_id'], pack['root_pack_id'], pack['primary_source_entries'],
                         *(pack['counts_by_extension'].get(ext, 0) for ext in extensions)])
summary = dict(status='manifest_source_types_and_candidate_scope_counted',
               observed_utc=datetime.now(timezone.utc).isoformat(), lot='L00.INVENTAIRE.TYPES.01',
               source_manifests_rehashed=len(packs), purchased_root_scopes=len({p['root_pack_id'] for p in packs}),
               primary_source_entries=sum(total.values()), counts_by_extension=dict(sorted(total.items())),
               source_object_candidate_occurrences=len(objects),
               root_guid_type_candidate_groups=len(variant_groups),
               groups_with_multiple_variants=sum(g['occurrences'] > 1 for g in variant_groups),
               groups_with_different_declared_payloads=sum(g['distinct_declared_hashes'] > 1 for g in variant_groups),
               missing_candidate_source_files=missing,
               source_payloads_rehashed=False, source_directory_current_enumeration_completed=False,
               logical_asset_denominator_reconciled=False, automatically_merged_variants=0,
               prefab_effect_and_rig_classification_completed=False,
               full_import=False, new_fully_qualified_assets=0, packs=packs, input_pins=pins)
for path in [O / 'SOURCE_OBJECT_CANDIDATES.v1.json', O / 'VARIANT_GROUPS.v1.json', O / 'COUNTS_BY_PACK.tsv']:
    summary['input_pins'][str(path)] = sha(path)
(O / 'RESULT.v1.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
lines = ['# Périmètre source par type — préparation des lots', '',
         f"48 manifestes réouverts et recontrôlés ; {summary['purchased_root_scopes']} archives racines.", '',
         '| Type source | Occurrences dans les packs et variantes |', '|---|---:|']
for ext, count in sorted(total.items()):
    lines.append(f'| {ext} | {count} |')
lines += ['', f"Candidats objets : {len(objects)} occurrences ; {len(variant_groups)} groupes archive racine/GUID/type.",
          f"Fichiers candidats absents : {len(missing)}. Les charges utiles ne sont pas rehashées par ce contrôle.", '',
          'Ces groupes servent à préparer les tranches. Ils ne constituent pas encore le total logique :',
          'les FBX peuvent contenir plusieurs meshes/clips, les prefabs peuvent assembler ou décliner les mêmes objets,',
          'et une scène ne remplace pas ses objets. Aucune variante ni aucun droit ne sont fusionnés.',
          'Les textures, shaders et autres dépendances restent à vérifier avec leurs objets.', '',
          '[Comptes par pack](COUNTS_BY_PACK.tsv) · [Candidats source](SOURCE_OBJECT_CANDIDATES.v1.json) · [Preuve](RESULT.v1.json)']
(O / 'PERIMETRE_PAR_TYPE.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')
print(json.dumps({key: value for key, value in summary.items()
                  if key not in {'packs', 'input_pins', 'missing_candidate_source_files'}}, ensure_ascii=False))
