"""Keep the documented batch state aligned with the latest measured refusal."""
from pathlib import Path
import datetime
import hashlib
import json
import re

W = Path('F:/OpenRPG_ImportWork')
T = W / 'SUIVI_LOTS_HIVEMIND'


def main():
    proof = W / 'rocks-firepit-native-lod-metric-reopen-v1/RESULT.v1.json'
    if not proof.exists():
        return
    data = json.loads(proof.read_text('utf-8'))
    assert data['samples'] == 294 and data['native_lod_decisions_compared']
    discrepancies = data['geometry_counter_discrepancies']
    unique = {(tuple(row['case'][:2]), row['counter'], row['native'], row['previous_geometry'])
              for row in discrepancies}
    stream_proof = W / 'rocks-native-mesh-streams-reopen-v1/RESULT.v1.json'
    counter_proof = W / 'rocks-native-mesh-counter-reopen-v1/RESULT.v1.json'
    resolved = stream_proof.exists() and counter_proof.exists()
    if resolved:
        streams = json.loads(stream_proof.read_text('utf-8'))
        counters = json.loads(counter_proof.read_text('utf-8'))
        assert streams['status'] == 'native_editor_mesh_streams_exactly_match_previous_capture'
        assert streams['byte_equivalence_verified'] and streams['meshes'] == 32 and streams['stream_files'] == 431
        assert not counters['mesh_counter_differences'] and not counters['importer_setting_differences']
        diagnosis = ('**Diagnostic corrigé par mesure directe** : zéro différence de compteurs ou de paramètres '
                     "d'import sur les 57 renderers. Les 32 meshes distincts et leurs 431 flux sont identiques "
                     'octet pour octet aux captures précédentes, soit 23 334 972 octets. Les 96 divergences '
                     'de statistiques LOD sont conservées ; elles ne prouvent pas une différence des meshes. '
                     'Le refus antérieur reste disponible dans son reçu immuable.')
        next_batch = 'L01.STUDIO.TRANSPORT_WEB'
        next_action = "L01.STUDIO.TRANSPORT_WEB — permettre le chargement borné du GLB fidèle, puis raccorder son rendu source_graph à l'aperçu et au placement. Les comportements LOD restent à intégrer et qualifier ; la simulation FirePit reste dans L06."
    else:
        diagnosis = (f'**Refus géométrique conservé** : {len(discrepancies)} observations présentent des écarts '
                     f'de compteurs, soit {len(unique)} configurations distinctes. Exemple : Unity compte '
                     '7 118 sommets contre 7 021 dans une capture antérieure. Ne pas qualifier la géométrie '
                     "avant d'identifier la cause dans les imports privés.")
        next_batch = 'L04.ROCKS.GEOMETRIE.ECART_NATIF'
        next_action = "L04.ROCKS.GEOMETRIE.ECART_NATIF — comparer les identités, paramètres d'import et compteurs des meshes concernés. Puis L01.STUDIO.TRANSPORT_WEB et L04.RENDU ; la simulation FirePit reste dans L06."
    now = datetime.datetime.now(datetime.timezone.utc).isoformat()
    block = f'''<!-- NATIVE-LOD-METRIC-BEGIN -->
## L04 — Mesures LOD et vérification directe des meshes

Actualisé : {now}.

**294/294 décisions natives contrôlées**, pour 14 groupes LOD et trois angles de caméra. Valeurs f32 et références originales vérifiées ; six corruptions refusées. Les décisions concordent avec les seuils pour ces cas hors frontière.

{diagnosis}

**Composants** : les 14 groupes, 63 renderers et le composant HDRP de lumière sont identifiés dans le reçu natif séparé. Cela ne qualifie pas leurs comportements dans Forge.

**Éditeur visible encore ouvert** : le transport binaire de bibliothèque plafonne actuellement à **256 Mio**, le lecteur Web à **512 Mio**. Le GLB Rocks de 709 944 920 octets admis dans la sonde native dépasse les deux limites. L'admission native ne prouve donc pas sa disponibilité dans l'éditeur Web. L'aperçu visible utilise encore le chemin GLTF ordinaire ; le raccordement source_graph reste à terminer.

**Prochain sous-lot** : {next_action}

[Mesures et refus détaillés](../rocks-firepit-native-lod-metric-reopen-v1/RESULT.v1.json) · [Composants natifs](../rocks-firepit-native-component-state-reopen-v2/RESULT.v1.json).
{('[Compte direct des meshes](../rocks-native-mesh-counter-reopen-v1/RESULT.v1.json) · [Équivalence exacte des flux](../rocks-native-mesh-streams-reopen-v1/RESULT.v1.json).' if resolved else '')}
<!-- NATIVE-LOD-METRIC-END -->
'''
    for name in ('AVANCEMENT_PAR_LOTS.md', 'LOTS_ACTIFS.md'):
        target = T / name
        text = target.read_text('utf-8-sig')
        text = re.sub(r'<!-- NATIVE-LOD-METRIC-BEGIN -->.*?<!-- NATIVE-LOD-METRIC-END -->\s*', '', text, flags=re.S)
        heading, _, rest = text.partition('\n')
        target.write_text(heading + '\n\n' + block + '\n' + rest.lstrip(), 'utf-8')
    checkpoint = dict(updated_utc=now, active_batch='L01' if resolved else 'L04',
                      active_sub_batch=next_batch,
                      status='native_mesh_streams_verified_lod_statistics_kept_separate' if resolved else data['status'], decisions_checked=294,
                      geometry_discrepancy_observations=len(discrepancies),
                      unique_geometry_discrepancies=len(unique),
                      native_mesh_stream_equivalence_verified=resolved,
                      geometry_qualified=False, web_visible_qualified=False,
                      model_wire_budget_mib=256, web_reader_budget_mib=512,
                      full_import=False, receipt=str(proof),
                      receipt_sha256=hashlib.sha256(proof.read_bytes()).hexdigest())
    state_path = T / 'ETAT.v1.json'
    state = json.loads(state_path.read_text('utf-8'))
    state.update(updated_utc=now, active_batch=checkpoint['active_batch'],
                 active_sub_batch=checkpoint['active_sub_batch'],
                 latest_lod_metric_checkpoint=checkpoint, goal_complete=False)
    if resolved:
        checkpoint['native_stream_receipt'] = str(stream_proof)
        checkpoint['native_stream_receipt_sha256'] = hashlib.sha256(stream_proof.read_bytes()).hexdigest()
        state.setdefault('native_mesh_counter_job', {})['status_at_observation'] = 'native_mesh_identity_and_counter_comparison_complete'
        state.setdefault('native_mesh_stream_job', {})['status_at_observation'] = streams['status']
    state_path.write_text(json.dumps(state, indent=2), 'utf-8')
    (T / 'L04_LOD_MESURES_ET_REFUS.v1.json').write_text(json.dumps(checkpoint, indent=2), 'utf-8')
    journal = T / 'JOURNAL.md'
    marker = 'L04.LOD.MESURES.01 — refus de concordance géométrique'
    if marker not in journal.read_text('utf-8-sig'):
        with journal.open('a', encoding='utf-8') as stream:
            stream.write(f'\n\n## {marker}\n\n{data["finished_utc"]} : 294 décisions natives comparées, 14 groupes ; {len(discrepancies)} écarts de compteurs sur {len(unique)} configurations. Six contrôles de corruption refusés. Sources conservées. Réparation ciblée restant ; aucun asset livré supplémentaire. Transport Web 256 Mio et lecteur Web 512 Mio insuffisants pour Rocks. [Reçu](../rocks-firepit-native-lod-metric-reopen-v1/RESULT.v1.json).\n')
    if resolved:
        marker = 'L04.ROCKS.FLUX_NATIFS.01 — équivalence exacte confirmée'
        if marker not in journal.read_text('utf-8-sig'):
            with journal.open('a', encoding='utf-8') as stream:
                stream.write(f'\n\n## {marker}\n\n{streams["finished_utc"]} : 32 meshes distincts, 431 flux et 23 334 972 octets identiques aux captures précédentes. Acquisition native Editor sans réimport ni modification des modèles. Identités et configurations des 57 renderers contrôlées séparément. Les écarts de statistiques LOD ne sont pas des différences de meshes. Rendu Forge et chargement Web restent ouverts. [Reçu](../rocks-native-mesh-streams-reopen-v1/RESULT.v1.json).\n')
    print(json.dumps(checkpoint))
    import record_model_wire_checkpoint_v1
    record_model_wire_checkpoint_v1.main()


if __name__ == '__main__':
    main()
