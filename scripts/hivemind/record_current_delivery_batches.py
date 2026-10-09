"""Record closed batch receipts without promoting physical checks to runtime delivery."""
import datetime
import hashlib
import json
from pathlib import Path

WORK = Path('F:/OpenRPG_ImportWork')
TRACK = WORK / 'SUIVI_LOTS_HIVEMIND'


def read(relative):
    path = WORK / relative
    raw = path.read_bytes()
    return path, json.loads(raw.decode('utf-8-sig')), hashlib.sha256(raw).hexdigest()


def main():
    state_path, state, _ = read('SUIVI_LOTS_HIVEMIND/ETAT.v1.json')
    now = datetime.datetime.now(datetime.timezone.utc).isoformat()
    batches = {b['id']: b for b in state['batches']}
    native_path, native, native_sha = read('townsmith-native-partial-reopen-0040-0059-v1/RESULT.v1.json')
    geom_path, geom, geom_sha = read('townsmith-native-geometry-0040-0059-v1/RESULT.v1.json')
    assert native['status'] == 'closed_native_townsmith_cases_independently_reopened'
    assert geom['status'] == '20_verified_townsmith_geometry_derivatives_reopened'
    assert native['verified_native_mesh_cases'] == geom['verified_meshes'] == 20
    assert native['original_selected_sources_unchanged'] and not geom['full_import']
    entry = {
        'batch_id': 'L03.TOWNSMITH.03', 'capture_ordinals': '40-59',
        'native_cases_verified': 20,
        'captured_buffers_rehashed': native['captured_buffers_rehashed'],
        'geometry_derivatives_verified': 20,
        'derived_streams_reopened': geom['derived_streams_reopened'],
        'native_receipt': str(native_path), 'native_receipt_sha256': native_sha,
        'derivative_receipt': str(geom_path), 'derivative_receipt_sha256': geom_sha,
        'whole_capture_expected_cases': 893, 'whole_capture_closed': False,
        'glb_created': False, 'full_import': False,
    }
    first_record = 'townsmith_third_partial_batch' not in batches['L03']
    batches['L03']['townsmith_third_partial_batch'] = entry
    fourth_native_path, fourth_native, fourth_native_sha = read('townsmith-native-partial-reopen-0060-0079-v1/RESULT.v1.json')
    fourth_geom_path, fourth_geom, fourth_geom_sha = read('townsmith-native-geometry-0060-0079-v1/RESULT.v1.json')
    assert fourth_native['verified_native_mesh_cases'] == fourth_geom['verified_meshes'] == 20
    assert fourth_native['status'] == 'closed_native_townsmith_cases_independently_reopened'
    assert fourth_geom['status'] == '20_verified_townsmith_geometry_derivatives_reopened'
    assert fourth_native['original_selected_sources_unchanged'] and not fourth_geom['full_import']
    new_fourth_record = 'townsmith_fourth_partial_batch' not in batches['L03']
    batches['L03']['townsmith_fourth_partial_batch'] = {
        'batch_id': 'L03.TOWNSMITH.04', 'capture_ordinals': '60-79',
        'native_cases_verified': 20, 'captured_buffers_rehashed': fourth_native['captured_buffers_rehashed'],
        'geometry_derivatives_verified': 20, 'derived_streams_reopened': fourth_geom['derived_streams_reopened'],
        'native_receipt': str(fourth_native_path), 'native_receipt_sha256': fourth_native_sha,
        'derivative_receipt': str(fourth_geom_path), 'derivative_receipt_sha256': fourth_geom_sha,
        'whole_capture_closed': False, 'glb_created': False, 'studio_verified': False, 'full_import': False,
    }
    state['sequential_townsmith_conversion'] = {
        'first_ordinal': 80, 'expected_remaining_cases': 813, 'planned_batches': 41,
        'progress_receipt': str(WORK / 'townsmith-sequential-remaining-v1/RUN-RESULT.v1.json'),
        'job_receipt': str(WORK / 'townsmith-sequential-remaining-v1/JOB.v1.json'),
        'studio_verified': False, 'full_import': False,
    }
    delivery = batches['L01']['native_scene_delivery']
    third = delivery['third_delivery_batch']
    new_villa_record = third.get('source_frames_verified', 0) != 20
    assembly_path, assembled, assembly_sha = read('villa-native-scene-delivery-l04-villa-03-v2/RUN-RESULT.v1.json')
    frame_path, frame, frame_sha = read('villa-native-source-frame-bound-l04-villa-03-v1/RUN-RESULT.v1.json')
    assert assembled['verified_derivatives'] == frame['verified_scenes'] == 20
    assert frame['status'] == 'native_original_source_frames_bound_and_reopened'
    third.update(status='20_native_glb_storage_normals_and_original_source_frames_reopened_canonical_pending',
                 verified_derivatives=20, source_frames_verified=20,
                 receipt=str(assembly_path), receipt_sha256=assembly_sha,
                 source_frame_receipt=str(frame_path), source_frame_receipt_sha256=frame_sha,
                 canonical_scenes_verified=0, gpu_rendered=False, studio_verified=False)
    state['updated_utc'] = now
    state['goal_complete'] = False
    state['sequential_villa_delivery'] = {
        'batch_ids': [f'L04.VILLA.{i:02d}' for i in range(4, 12)],
        'expected_physical_scenes': 147,
        'progress_receipt': str(WORK / 'villa-native-sequential-delivery-v2/RUN-RESULT.v1.json'),
        'preserved_prior_failure': str(WORK / 'villa-native-sequential-delivery-v1/RUN-RESULT.v1.json'),
        'studio_verified': False, 'full_import': False,
    }
    tmp = state_path.with_suffix('.json.tmp')
    tmp.write_text(json.dumps(state, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    tmp.replace(state_path)
    dashboard_path = TRACK / 'TABLEAU_DE_BORD_LOTS.md'
    dashboard = dashboard_path.read_text('utf-8')
    start_marker = '<!-- CURRENT_BATCH_CHECKPOINT_START -->'
    end_marker = '<!-- CURRENT_BATCH_CHECKPOINT_END -->'
    checkpoint = (
        f'{start_marker}\n## Point vérifié : {now}\n\n'
        '| Parcours | Sous-lots clos | Compteur et portée | Suite en cours |\n'
        '|---|---|---|---|\n'
        '| Villa | 01 à 03 | 60/207 dérivés physiques et bases source ; Studio/runtime non qualifiés | '
        '[Lots 04 à 11](../villa-native-sequential-delivery-v2/RUN-RESULT.v1.json) |\n'
        '| Townsmith | 01 à 04 | 80 géométries intermédiaires ; prefab final et Studio/runtime non qualifiés | '
        '[41 sous-lots restants](../townsmith-sequential-remaining-v1/RUN-RESULT.v1.json) |\n\n'
        'Les liens de suite donnent les reçus courants, le lot actif et les nouveaux sous-lots clos. '
        'Ils restent distincts des preuves d’usage dans l’éditeur.\n'
        f'{end_marker}\n\n'
    )
    if start_marker in dashboard:
        before, rest = dashboard.split(start_marker, 1)
        _, after = rest.split(end_marker, 1)
        dashboard = before + checkpoint + after.lstrip('\n')
    else:
        lines = dashboard.splitlines(keepends=True)
        dashboard = ''.join(lines[:2]) + checkpoint + ''.join(lines[2:])
    dashboard_tmp = dashboard_path.with_suffix('.md.tmp')
    dashboard_tmp.write_text(dashboard, 'utf-8')
    dashboard_tmp.replace(dashboard_path)
    if first_record:
        text = (
            f'\n\n## L03.TOWNSMITH.03 — sous-lot clos — {now}\n'
            'Cas 40 à 59 : 20/20 captures natives et 269 buffers réouverts indépendamment ; '
            '20/20 géométries intermédiaires et 189 streams dérivés vérifiés. '
            'Les trois sous-lots totalisent 60 cas natifs, 806 buffers et 566 streams dérivés. '
            'La capture globale de 893 meshes reste ouverte. Aucun prefab GLB final, matériau GPU '
            'ou asset Studio/runtime supplémentaire qualifié. Sources et identités conservées ; '
            'aucun cas des deux sous-lots précédents refait. '
            f'Reçus : {native_path} et {geom_path}.\n'
            'Villa 04 à 11 : 147 scènes physiques prévues dans une chaîne séquentielle, '
            'qui attend la clôture des repères source de Villa 03. Chaque sous-lot exige ses reçus '
            'terminaux avant le suivant ; les étapes moteur, GPU, index/images et Studio/runtime '
            'restent séparées. Progression : villa-native-sequential-delivery-v1/RUN-RESULT.v1.json.\n'
        )
        for name in ['TABLEAU_DE_BORD_LOTS.md', 'PLAN_ET_SUIVI.md', 'JOURNAL.md']:
            with (TRACK / name).open('a', encoding='utf-8') as stream:
                stream.write(text)
    if new_villa_record:
        text = (
            f'\n\n## L04.VILLA.03 — sous-lot clos ; chaîne suivante reprise — {now}\n'
            '20/20 dérivés physiques et repères source réouverts ; total des trois sous-lots : '
            '60/207 scènes éligibles à ce parcours. Les 19 scènes avec lectures de normales '
            'non résolues restent dans un traitement distinct. Aucun contrôle GPU ou Studio/runtime '
            'nouveau qualifié. '
            f'Reçus : {assembly_path} et {frame_path}.\n'
            'Le contrôleur séquentiel v1 a échoué dans son attente du processus terminé, avant '
            'la création du lot 04. Son reçu est conservé. La chaîne v2 vérifie directement le '
            'reçu terminal de Villa 03 et son hash, puis traite les lots 04 à 11 (147 scènes) '
            'sans refaire les trois premiers. Contrôleur lancé : PID 18340. '
            'Suivi : villa-native-sequential-delivery-v2/RUN-RESULT.v1.json. '
            'Townsmith 04 (cas 60 à 79) : vérification indépendante puis conversion en cours ; '
            'aucun nouveau compteur terminal ajouté avant les reçus.\n'
        )
        for name in ['TABLEAU_DE_BORD_LOTS.md', 'PLAN_ET_SUIVI.md', 'JOURNAL.md']:
            with (TRACK / name).open('a', encoding='utf-8') as stream:
                stream.write(text)
    if new_fourth_record:
        text = (
            f'\n\n## L03.TOWNSMITH.04 — sous-lot clos ; suite lancée — {now}\n'
            'Cas 60 à 79 : 20/20 captures natives et 264 buffers réouverts indépendamment ; '
            '20/20 géométries intermédiaires et 192 streams dérivés vérifiés. Total des quatre '
            'sous-lots : 80 géométries, 1070 buffers capturés et 758 streams dérivés. '
            f'Reçus : {fourth_native_path} et {fourth_geom_path}.\n'
            'Chaîne séquentielle lancée (PID 31732) pour les cas 80 à 892 : 40 sous-lots de 20 '
            'puis un de 13. Chaque sous-lot attend les captures existantes, vérifie les identités '
            'et sources, réutilise le convertisseur et le reopener existants puis contrôle les '
            'hashes des streams livrés. Aucun redémarrage Unity ni répétition des 80 cas clos. '
            'Les sources de l’adaptateur de taille variable sont épinglées avec les autres helpers. '
            'Suivi vivant : townsmith-sequential-remaining-v1/RUN-RESULT.v1.json. '
            'Il s’agit de géométries intermédiaires : prefabs complets, matériaux, images/index '
            'et Studio/runtime restent à qualifier.\n'
        )
        for name in ['TABLEAU_DE_BORD_LOTS.md', 'PLAN_ET_SUIVI.md', 'JOURNAL.md']:
            with (TRACK / name).open('a', encoding='utf-8') as stream:
                stream.write(text)
    print(json.dumps({'recorded': 'L03.TOWNSMITH.04', 'verified_intermediate_geometries': 80,
                      'source_capture_buffers': 1070, 'derived_streams': 758,
                      'new_studio_runtime_assets_qualified': 0}))


if __name__ == '__main__':
    main()
