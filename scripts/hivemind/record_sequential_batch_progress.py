"""Publish only terminal subset receipts; keep physical and runtime counts separate."""
import datetime
import hashlib
import json
from pathlib import Path

WORK = Path('F:/OpenRPG_ImportWork')
TRACK = WORK / 'SUIVI_LOTS_HIVEMIND'


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def write(path, data):
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', 'utf-8')
    temporary.replace(path)


def main():
    state_path = TRACK / 'ETAT.v1.json'
    state = read(state_path)
    recorded = state.setdefault('verified_sequential_subsets', {'townsmith': [], 'villa': []})
    new_events = []
    for kind, relative in [('townsmith', 'townsmith-sequential-remaining-v1/RUN-RESULT.v1.json'),
                           ('villa', 'villa-native-sequential-delivery-v2/RUN-RESULT.v1.json')]:
        if kind == 'townsmith' and (WORK / 'townsmith-sequential-remaining-v2/RUN-RESULT.v1.json').exists():
            relative = 'townsmith-sequential-remaining-v2/RUN-RESULT.v1.json'
        run_path = WORK / relative
        run = read(run_path)
        seen = {row['key'] for row in recorded[kind]}
        for row in run['closed_batches']:
            key = str(row['start_ordinal']) if kind == 'townsmith' else row['batch_id']
            if key in seen:
                continue
            first_name, second_name = ('capture', 'geometry') if kind == 'townsmith' else ('assembly', 'frame')
            first_path, second_path = Path(row[first_name + '_receipt']), Path(row[second_name + '_receipt'])
            assert sha(first_path) == row[first_name + '_receipt_sha256'], 'First receipt changed'
            assert sha(second_path) == row[second_name + '_receipt_sha256'], 'Second receipt changed'
            first, second = read(first_path), read(second_path)
            if kind == 'townsmith':
                count = row['count']
                assert first['status'] == 'closed_native_townsmith_cases_independently_reopened'
                assert second['status'] == 'verified_townsmith_geometry_derivatives_reopened'
                assert first['verified_native_mesh_cases'] == second['verified_meshes'] == count
                assert [r['capture_ordinal'] for r in second['rows']] == list(range(row['start_ordinal'], row['start_ordinal'] + count))
                assert {r['asset_id'] for r in first['rows']} == {r['asset_id'] for r in second['rows']}
                for mesh in second['rows']:
                    target = Path(mesh['derivative'])
                    geometry_path = target / 'GEOMETRY.v1.json'
                    assert sha(geometry_path) == mesh['geometry_receipt_sha256']
                    for name, stream in read(geometry_path)['derived_streams'].items():
                        assert sha(target / name) == stream['sha256'], 'Derived stream changed'
                extra = {'captured_buffers': first['captured_buffers_rehashed'],
                         'derived_streams': second['derived_streams_reopened']}
                description = f'Townsmith cas {row["start_ordinal"]} à {row["start_ordinal"] + count - 1} : {count}/{count} géométries intermédiaires vérifiées'
            else:
                count = row['verified_scenes']
                assert second['status'] == 'native_original_source_frames_bound_and_reopened'
                assert first['verified_derivatives'] == second['verified_scenes'] == count
                assert len({r['asset_id'] for r in second['rows']}) == count
                for scene in second['rows']:
                    assert sha(scene['glb']) == scene['sha256'], 'Frame-bound GLB changed'
                extra = {}
                description = f'{key} : {count}/{count} dérivés physiques et repères source vérifiés'
            assert not second['full_import'], 'Unexpected promotion into completed import'
            now = datetime.datetime.now(datetime.timezone.utc).isoformat()
            recorded[kind].append({'key': key, 'count': count, 'verified_utc': now,
                                  'receipts': dict(row), 'studio_verified': False,
                                  'full_import': False, **extra})
            seen.add(key)
            new_events.append(f'\n\n## Sous-lot vérifié — {now}\n{description}. '
                              f'Reçus : {first_path} et {second_path}. '
                              'GPU, bibliothèque et usage Studio/runtime restent à qualifier.\n')
        state.setdefault('sequential_jobs_observed', {})[kind] = {
            'status': run['status'], 'progress_receipt': str(run_path),
            'active_batch': run.get('active_batch'), 'start_ordinal': run.get('start_ordinal'),
            'error': run.get('error'), 'observation_is_not_process_liveness': True,
        }
    townsmith = 80 + sum(r['count'] for r in recorded['townsmith'])
    villa = 60 + sum(r['count'] for r in recorded['villa'])
    assert townsmith <= 893 and villa <= 207
    state['updated_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    state['goal_complete'] = False
    recorded_closures = len(new_events)
    admission_root = WORK / 'villa-remaining-canonical-admission-v1'
    if (admission_root / 'RUN-RESULT.v1.json').exists():
        admission = read(admission_root / 'RUN-RESULT.v1.json')
        initial_admission_record = 'remaining_villa_canonical_admission' not in state
        state.setdefault('remaining_villa_canonical_admission', {}).update({
            'progress_receipt': str(admission_root / 'RUN-RESULT.v1.json'),
            'job_receipt': str(admission_root / 'JOB.v1.json'),
            'status_at_observation': admission['status'],
            'expected_remaining_scenes': 187, 'canonical_scenes': admission['canonical_scenes'],
            'gpu_rendered': False, 'studio_verified': False, 'full_import': False,
        })
        admitted = state['remaining_villa_canonical_admission'].setdefault(
            'verified_native_admission_batches', [])
        seen_admissions = {row['batch_id'] for row in admitted}
        for batch in admission['closed_batches']:
            if batch['batch_id'] in seen_admissions:
                continue
            stdout_path = Path(batch['stdout'])
            assert sha(stdout_path) == batch['stdout_sha256'], 'Native admission log changed'
            for path, expected in batch['input_pins'].items():
                assert sha(path) == expected, 'Native admission input changed'
            inputs_paths = [Path(path) for path in batch['input_pins']
                            if Path(path).name == 'INPUTS.v1.json']
            assert len(inputs_paths) == 1, 'Native admission input identity is ambiguous'
            inputs = read(inputs_paths[0])
            output = [json.loads(line) for line in stdout_path.read_text('utf-8').splitlines()
                      if line.startswith('{')]
            summaries = [row for row in output if 'canonical_scenes' in row]
            actual_ids = [row['asset_id'] for row in output if 'asset_id' in row]
            expected_ids = [row['asset_id'] for row in inputs]
            assert len(summaries) == 1 and summaries[0] == batch['summary']
            assert summaries[0]['canonical_scenes'] == len(expected_ids) == batch['canonical_scenes']
            assert summaries[0]['negative_controls_passed'] == batch['negative_controls_passed'] == 2
            assert len(actual_ids) == len(set(actual_ids)) == len(expected_ids)
            assert set(actual_ids) == set(expected_ids), 'Native admitted IDs differ'
            admitted.append({'batch_id': batch['batch_id'],
                             'canonical_scenes': batch['canonical_scenes'],
                             'negative_controls_passed': 2,
                             'stdout': str(stdout_path),
                             'stdout_sha256': batch['stdout_sha256'],
                             'inputs_sha256': sha(inputs_paths[0]),
                             'all_batch_input_pins_reopened': True,
                             'verified_utc': state['updated_utc'],
                             'gpu_rendered': False, 'studio_verified': False})
            seen_admissions.add(batch['batch_id'])
            new_events.append(
                f'\n\n## {batch["batch_id"]} — admission native réouverte — {state["updated_utc"]}\n'
                f'{batch["canonical_scenes"]} scènes : IDs exacts, entrées épinglées et log natif '
                f'réouverts ; deux contrôles négatifs réussis. Reçu : {stdout_path}. '
                'Le rendu GPU et l’usage dans Studio restent à vérifier.\n')
        state['remaining_villa_canonical_admission']['independently_recorded_remaining_scenes'] = sum(
            row['canonical_scenes'] for row in admitted)
        if initial_admission_record:
            new_events.append(
                f'\n\n## L01.VILLA — admission canonique des lots suivants préparée — {state["updated_utc"]}\n'
                'Contrôleur lancé pour les 187 scènes des lots 02 à 11. Il attend le contrôleur '
                'exact de reprise et exige vingt admissions natives et deux contrôles négatifs '
                'réellement réussis, avec sources inchangées. Ensuite, copie immuable du lecteur '
                'compilé, réouverture de chaque lot physique fermé, IDs exacts, hashes des fichiers '
                'avant/après et contrôle du compte réellement admis par l’exécutable. Aucun '
                'rebuild par sous-lot, aucune reconversion des géométries, aucune promotion GPU '
                'ou Studio/runtime. Les dix-neuf scènes non résolues restent hors de ce parcours '
                'et doivent être traitées séparément. '
                f'Suivi : {admission_root / "RUN-RESULT.v1.json"}.\n')
    recovery_root = WORK / 'native-scene-check-queue-recovery-v2'
    first_native_admissions = 0
    if (recovery_root / 'RUN-RESULT.v1.json').exists():
        recovery = read(recovery_root / 'RUN-RESULT.v1.json')
        if recovery['status'] == 'actual_module_tests_and_20_native_scene_admissions_verified':
            assert recovery['canonical_scenes'] == 20 and recovery['negative_controls_passed'] == 2
            assert recovery['all_input_pins_unchanged']
            first_native_admissions = 20
        initial_recovery_record = 'native_scene_queue_recovery' not in state
        state.setdefault('native_scene_queue_recovery', {}).update({
            'progress_receipt': str(recovery_root / 'RUN-RESULT.v1.json'),
            'job_receipt': str(recovery_root / 'JOB.v1.json'),
            'status_at_observation': recovery['status'],
            'tests_passed': recovery['tests_passed'], 'canonical_scenes': recovery['canonical_scenes'],
            'gpu_rendered': False, 'studio_verified': False, 'full_import': False,
            'preserved_prior_attempt': str(WORK / 'native-scene-check-queue-recovery-v1'),
        })
        if initial_recovery_record:
            new_events.append(
                f'\n\n## L01 — reprise de la surveillance native — {state["updated_utc"]}\n'
                'Surveillance par handles Windows et date exacte de création, après comparaison '
                'FILETIME/.NET sur un processus vivant ; contrôle du timeout vivant et des PID '
                'absents/réutilisés réussi. La tentative v1 refusée avant toute exécution des tests '
                'est conservée avec son code exact. La reprise v2 attend la fin des quatre '
                'contrôleurs existants, exige le succès réel des six tests de l’API canonique, '
                'puis réutilise les projets natifs et sources épinglées pour les six tests '
                'd’attachement, les neuf tests de ressources et les vingt admissions natives avec '
                'deux contrôles négatifs. Les étapes déjà prouvées sont conservées. Aucun test '
                'supplémentaire, admission canonique, GPU ou Studio qualifié au lancement. '
                f'Suivi : {recovery_root / "RUN-RESULT.v1.json"}.\n')
    native_admissions = first_native_admissions + sum(
        row['canonical_scenes'] for row in state.get('remaining_villa_canonical_admission', {}).get(
            'verified_native_admission_batches', []))
    assert native_admissions <= 207
    pose_queue = WORK / 'townsmith-remaining-prefab-pose-queue-v1'
    if (pose_queue / 'JOB.v1.json').exists() and 'townsmith_prefab_pose_queue' not in state:
        job_path = pose_queue / 'JOB.v1.json'
        state['townsmith_prefab_pose_queue'] = {
            'job_receipt': str(job_path), 'job_sha256': sha(job_path),
            'pid_at_launch': read(job_path)['pid'], 'expected_new_prefabs': 3,
            'progress_receipt': str(pose_queue / 'RUN-RESULT.v1.json'),
            'observation_is_not_process_liveness': True,
            'studio_verified': False, 'full_import': False,
        }
        new_events.append(
            f'\n\n## L03.TOWNSMITH — contrôle indépendant des poses — {state["updated_utc"]}\n'
            'Les cinq premiers prefabs passent un contrôle indépendant des matrices parentes '
            'et des identités de rendu. Les trois suivants ont un vérificateur en file derrière '
            'le contrôleur exact d’assemblage ; aucun nouveau Unity ni répétition des cinq '
            f'premiers. Suivi : {pose_queue / "RUN-RESULT.v1.json"}. '
            'Cette préparation ne valide ni pose ni rendu.\n')
    poses = state.setdefault('verified_townsmith_prefab_poses', [])
    for folder, expected in [('townsmith-first-five-prefab-pose-reopen-v1', 5),
                             ('townsmith-remaining-three-prefab-pose-reopen-v1', 3)]:
        pose_path = WORK / folder / 'RESULT.v1.json'
        if not pose_path.exists() or any(row['receipt'] == str(pose_path) for row in poses):
            continue
        pose = read(pose_path)
        if pose['status'] != 'native_prefab_pose_and_renderer_identities_reopened':
            state.setdefault('townsmith_pose_review_failures', {})[folder] = {
                'receipt': str(pose_path), 'error': pose.get('error'), 'status': pose['status']}
            continue
        assert pose['prefabs_verified'] == expected and pose['all_input_pins_reopened_before_after']
        ids = [row['asset_id'] for row in pose['rows']]
        assert len(ids) == len(set(ids)) == expected
        assert not set(ids).intersection(asset for row in poses for asset in row['asset_ids'])
        for path, digest in pose['input_pins'].items():
            if Path(path).name in ['verify_townsmith_prefab_pose.py', 'townsmith.HIERARCHY-RESULT.v1.json']:
                assert sha(path) == digest, 'Independent pose reviewer/native capture changed'
        poses.append({'receipt': str(pose_path), 'receipt_sha256': sha(pose_path),
                      'asset_ids': ids, 'prefabs_verified': expected,
                      'nodes_verified': pose['nodes_verified'],
                      'renderer_bindings_verified': pose['renderer_bindings_verified'],
                      'gpu_rendered': False, 'studio_verified': False})
        new_events.append(
            f'\n\n## L03.TOWNSMITH — poses natives réouvertes — {state["updated_utc"]}\n'
            f'{expected} prefabs, {pose["nodes_verified"]} nœuds et '
            f'{pose["renderer_bindings_verified"]} bindings contrôlés indépendamment ; '
            'sources épinglées avant/après, états et LOD conservés. '
            f'Reçu : {pose_path}. Matériaux, dessins et Studio restent à qualifier.\n')
    townsmith_poses = sum(row['prefabs_verified'] for row in poses)
    assert townsmith_poses <= 8
    gpu_scope_path = WORK / 'villa-gpu-profile-current-native-scope-v1/RESULT.v1.json'
    if gpu_scope_path.exists() and 'villa_gpu_profile_scope' not in state:
        scope = read(gpu_scope_path)
        assert scope['status'] == 'older_profiles_not_applicable_to_current_native_scene_identities'
        assert not scope['exact_mesh_identity_intersections'] and not scope['measured_defaults_transplanted']
        assert scope['current_deferred_scenes'] == scope['current_deferred_declared_tangents_present'] == 19
        state['villa_gpu_profile_scope'] = {
            'receipt': str(gpu_scope_path), 'receipt_sha256': sha(gpu_scope_path),
            'older_profiles_not_applicable_to_current_226_native_ids': True,
            'current_deferred_scenes_have_declared_captured_tangents': 19,
            'full_glb_payloads_rehashed_this_scope_review': False,
            'material_bindings_still_unresolved': True, 'full_import': False}
        new_events.append(
            f'\n\n## L04.VILLA — portée des profils GPU corrigée — {state["updated_utc"]}\n'
            'Les quatorze anciens profils GPU et leurs dix-neuf dérivés ne partagent aucune '
            'identité mesh GUID/fileID ni asset ID avec les 226 scènes natives actuelles. '
            'Aucune transplantation de valeurs mesurées. Les dix-neuf scènes actuellement '
            'différées déclarent des tangentes capturées ; leur binding matériel reste à '
            'résoudre. Contrôle des documents et en-têtes, sans nouvelle réouverture du BIN '
            f'complet. Reçu : {gpu_scope_path}.\n')
    checkpoint_path = WORK / 'villa-207-canonical-source-checkpoint-v1/RESULT.v1.json'
    if checkpoint_path.exists() and 'villa_native_admission_source_checkpoint' not in state:
        checkpoint = read(checkpoint_path)
        assert checkpoint['status'] == '207_native_admissions_and_exact_code_checkpoint_retained'
        assert checkpoint['native_admitted_scenes'] == 207
        assert checkpoint['all_selected_source_hashes_reopened_before_after']
        state['villa_native_admission_source_checkpoint'] = {
            'receipt': str(checkpoint_path), 'receipt_sha256': sha(checkpoint_path),
            'source_files_preserved': checkpoint['source_files_preserved'],
            'future_viewport_edits_not_qualified_by_this_checkpoint': True}
        new_events.append(
            f'\n\n## L01.VILLA — admission complète conservée — {state["updated_utc"]}\n'
            f'207 scènes admises par le lecteur natif ; {checkpoint["source_files_preserved"]} '
            'fichiers de code exacts conservés avec hashes avant/après. Les modifications '
            'du viewport doivent recevoir leurs propres preuves. '
            f'Reçu : {checkpoint_path}.\n')
    frame_root = WORK / 'studio-source-scene-frame-native-v1'
    if (frame_root / 'RUN-RESULT.v1.json').exists():
        frame = read(frame_root / 'RUN-RESULT.v1.json')
        first_frame_observation = 'source_scene_frame_native_check' not in state
        state.setdefault('source_scene_frame_native_check', {}).update({
            'progress_receipt': str(frame_root / 'RUN-RESULT.v1.json'),
            'job_receipt': str(frame_root / 'JOB.v1.json'),
            'status_at_observation': frame['status'], 'tests_passed': frame['tests_passed'],
            'expected_tests': 12, 'gpu_rendered': False, 'studio_verified': False,
            'observation_is_not_process_liveness': True})
        if first_frame_observation:
            new_events.append(
                f'\n\n## L01 — mise à jour explicite des frames — {state["updated_utc"]}\n'
                'Module Studio ajouté pour appliquer le même contexte aux matériaux caméra '
                'et ombre, après validation complète des destinations, rôles et ownership. '
                'Modes de normales capturés conservés ; aucun frame, clock ou mip deviné. '
                'Douze tests des modules réels, dont trois nouveaux contrôles transactionnels, '
                'en cours ; aucun raccordement actif au viewport ni rendu qualifié. '
                f'Suivi : {frame_root / "RUN-RESULT.v1.json"}.\n')
    host_root = WORK / 'studio-source-scene-host-native-v1'
    if (host_root / 'RUN-RESULT.v1.json').exists():
        host = read(host_root / 'RUN-RESULT.v1.json')
        first_host_observation = 'source_scene_host_native_check' not in state
        state.setdefault('source_scene_host_native_check', {}).update({
            'progress_receipt': str(host_root / 'RUN-RESULT.v1.json'),
            'job_receipt': str(host_root / 'JOB.v1.json'),
            'status_at_observation': host['status'], 'tests_passed': host['tests_passed'],
            'expected_tests': 14, 'gpu_rendered': False, 'studio_verified': False,
            'viewport_call_sites_wired': False,
            'observation_is_not_process_liveness': True})
        if first_host_observation:
            new_events.append(
                f'\n\n## L01 — montage commun des scènes source — {state["updated_utc"]}\n'
                'Fonction Studio commune ajoutée : intake typé, ressources, hiérarchie complète '
                'sous la racine existante, paramètres de frame explicites, passes originales '
                'retenues et nettoyage des allocations propres si attachement refusé. Une '
                'déclaration source invalide ne devient jamais un glTF ordinaire. Quatorze '
                'tests des modules réels, dont deux contrôles de sélection/refus, en file '
                'après le contrôleur exact des frames. Les appels aperçu/placement et la '
                f'qualification GPU restent ouverts. Suivi : {host_root / "RUN-RESULT.v1.json"}.\n')
    pass_diagnosis_path = WORK / 'dungeon-native-pass-snapshot-diagnosis-v1/RESULT.v1.json'
    if pass_diagnosis_path.exists() and 'dungeon_native_pass_snapshot_failure' not in state:
        diagnosis = read(pass_diagnosis_path)
        assert diagnosis['requested_materials'] == 42 and diagnosis['capture_report_failed_rows'] == 39
        assert not diagnosis['unnamed_pass_guard_relaxed']
        state['dungeon_native_pass_snapshot_failure'] = {
            'receipt': str(pass_diagnosis_path), 'receipt_sha256': sha(pass_diagnosis_path),
            'materials_requested': 42, 'unnamed_pass_refusals': 39,
            'capture_report_successful_rows': 3, 'hdrp_render_parity_qualified': False,
            'original_pipeline_compatibility_proven': False, 'full_import': False}
        new_events.append(
            f'\n\n## L02 — capture de passes HDRP refusée — {state["updated_utc"]}\n'
            'Unity exit 1 : 39/42 matériaux refusés sur des passes sans nom, répartis entre '
            'cinq shaders. Les trois lignes sans erreur déclarent des passes BuiltIn ; elles '
            'ne prouvent pas le rendu HDRP. Les graphes originaux déclarent pourtant des '
            'cibles HDTarget ; sélection effective des subshaders/passes à diagnostiquer. '
            'Aucun guard retiré, aucune source modifiée, aucun succès global de capture. '
            f'Reçu : {pass_diagnosis_path}.\n')
    write(state_path, state)
    for name in ['TABLEAU_DE_BORD_LOTS.md', 'PLAN_ET_SUIVI.md', 'JOURNAL.md']:
        with (TRACK / name).open('a', encoding='utf-8') as stream:
            stream.write(''.join(new_events))
    dashboard_path = TRACK / 'TABLEAU_DE_BORD_LOTS.md'
    dashboard = dashboard_path.read_text('utf-8')
    start_marker, end_marker = '<!-- CURRENT_BATCH_CHECKPOINT_START -->', '<!-- CURRENT_BATCH_CHECKPOINT_END -->'
    before, rest = dashboard.split(start_marker, 1)
    _, after = rest.split(end_marker, 1)
    checkpoint = (
        f'{start_marker}\n## Point vérifié : {state["updated_utc"]}\n\n'
        '| Parcours | Compteur et portée | Reçus courants |\n|---|---|---|\n'
        f'| Villa | {villa}/207 dérivés physiques et repères source ; 19 autres scènes à résoudre séparément | '
        '[Chaîne Villa](../villa-native-sequential-delivery-v2/RUN-RESULT.v1.json) |\n'
        f'| Townsmith | {townsmith}/893 géométries intermédiaires | '
        '[Chaîne Townsmith](../townsmith-sequential-remaining-v2/RUN-RESULT.v1.json) |\n'
        f'| Villa : lecteur natif | {native_admissions}/207 scènes admises ; rendu GPU non vérifié | '
        '[Admissions natives](../villa-remaining-canonical-admission-v1/RUN-RESULT.v1.json) |\n'
        f'| Townsmith : poses de prefabs | {townsmith_poses}/8 contrôlées indépendamment ; rendu non vérifié | '
        '[Contrôle des poses restantes](../townsmith-remaining-prefab-pose-queue-v1/RUN-RESULT.v1.json) |\n\n'
        'Ces compteurs ne qualifient pas le rendu GPU, les prefabs finaux, les images/index '
        'ou l’usage dans Studio/runtime. Chaque nouveau sous-lot consigné a ses reçus et ses '
        'fichiers dérivés réouverts ; les groupes déjà consignés ne sont pas refaits.\n'
        f'{end_marker}\n\n'
    )
    temporary = dashboard_path.with_suffix('.md.tmp')
    temporary.write_text(before + checkpoint + after.lstrip('\n'), 'utf-8')
    temporary.replace(dashboard_path)
    print(json.dumps({'new_closed_subsets_recorded': recorded_closures,
                      'townsmith_intermediate_geometries': townsmith,
                      'villa_canonical_admissions': native_admissions,
                      'townsmith_prefab_poses': townsmith_poses,
                      'villa_physical_derivatives': villa, 'goal_complete': False}))


if __name__ == '__main__':
    main()
