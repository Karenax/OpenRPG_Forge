"""Refresh the user-facing sequential batch plan from actual receipts."""
import datetime
import ctypes
from ctypes import wintypes
import hashlib
import json
from pathlib import Path

W = Path('F:/OpenRPG_ImportWork')
T = W / 'SUIVI_LOTS_HIVEMIND'
HISTORICAL_SOURCE_TRANSITIONS = {}


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def evidence_pin_matches(path, expected):
    """Reopen old receipts against exact retained backups after the guarded RGB8 transition."""
    current = sha(path)
    if current == expected:
        return True
    root = W / 'delta-rgb8-canonical-loader-native-v2'
    transition_path = root / 'SOURCE-TRANSITION-INSTALLED.v1.json'
    if not transition_path.is_file():
        return False
    status = read(root / 'RUN-RESULT.v1.json')
    assert sha(transition_path) == status['source_transition_sha256']
    for row in read(transition_path)['files']:
        if Path(row['path']) == Path(path) and expected == row['before_sha256']:
            assert current == row['after_sha256']
            assert sha(row['candidate']) == current and sha(row['backup']) == expected
            HISTORICAL_SOURCE_TRANSITIONS[str(path)] = dict(**row,
                historical_proof_only=True, current_renderer_qualified=False)
            return True
    return False


def replace_checked(path, old, new):
    assert path.read_bytes() == old, 'Concurrent tracking change; preserve it'
    temporary = path.with_suffix(path.suffix + '.followup.tmp')
    temporary.write_bytes(new)
    temporary.replace(path)


def main():
    now = datetime.datetime.now(datetime.timezone.utc).isoformat()
    ui_path = W / 'studio-actual-native-library-ui-v1' / 'RESULT.v1.json'
    if ui_path.with_name('RESULT.v2.json').is_file():
        ui_path = ui_path.with_name('RESULT.v2.json')
    if ui_path.with_name('RESULT.v3.json').is_file():
        ui_path = ui_path.with_name('RESULT.v3.json')
    if ui_path.with_name('RESULT.v4.json').is_file():
        ui_path = ui_path.with_name('RESULT.v4.json')
    if ui_path.with_name('RESULT.v5.json').is_file():
        ui_path = ui_path.with_name('RESULT.v5.json')
    if ui_path.with_name('RESULT.v6.json').is_file():
        ui_path = ui_path.with_name('RESULT.v6.json')
    ui_evidence = None
    ui_note = None
    if ui_path.is_file():
        ui = read(ui_path)
        for pin in ui['evidence_files']:
            assert sha(ui_path.parent / pin['file']) == pin['sha256']
        assert not ui['full_import'] and not ui['human_approval']
        ui_evidence = dict(receipt=str(ui_path), receipt_sha256=sha(ui_path), **ui)
        ui_note = (f"L08.STUDIO.UI : recette réelle du candidat natif sur F ; "
            f"recherche Axe : {ui['unfiltered_query_results']} résultats, puis "
            f"{ui['source_preserved_filter_results']} avec Source conservée. "
            f"{ui['detail_reviews']} fiches examinées : dimensions, triangles, licence, "
            "fichiers et vignettes sur F ; aperçu 3D et placement désactivés pour la source non plaçable. "
            "Des vignettes génériques restent visibles. La couverture de toutes les images et de tous les assets, "
            "la parité de rendu et le runtime restent à qualifier ; aucun nouvel asset entièrement validé. "
            f"Reçu : studio-actual-native-library-ui-v1/{ui_path.name}.")
        if ui.get('save_reopen_verified'):
            ui_note += (" Paper Cluster : aperçu 3D texturé observé, une instance appliquée et sauvegardée, "
                "zone fermée puis réouverte depuis la liste native. Même UUID, transformation et SHA "
                "du document sauvegardé ; inspecteur commun relu. Le prérequis de zone est résolu. "
                "Cette preuve porte sur une instance ; couverture globale et parité Unity restent à qualifier.")
            if ui.get('placement_visual_render_observed'):
                ui_note += (" Instance texturée observée après cadrage dans la carte réouverte. "
                    "Entrée/sortie du playtest réalisée sans changer la sauvegarde ; asset non observé "
                    "dans la vue joueur, donc usage runtime encore non qualifié.")
            if ui.get('edit_save_reopen_verified'):
                ui_note += (" Retouche appliquée puis sauvegardée : même UUID, X=2 m, Y=0, Z=0, "
                    "rotation Y=45°, révision 3. Fermeture/réouverture native et SHA du document "
                    "édité concordants ; playtest sans changement implicite de sauvegarde. Même "
                    "essai X=2/Z=0 ne prouve pas un défaut runtime : départ réel X=16/Z=8. "
                    "Ancienne interprétation corrigée dans RESULT.v6.json ; refaire le test au départ réel.")
        elif ui.get('preview_attempted'):
            ui_note += (" Essai réel d'aperçu du GLB Paper Cluster : refus avant rendu, "
                "« Convertir la zone avant de placer des objets ». Préparer une zone auteur "
                "dans le workspace isolé, puis rejouer aperçu/placement/persistance/runtime.")
    census_progress_path = W / 'embedded-effect-source-census-v1/PROGRESS.v1.json'
    census_final_path = census_progress_path.with_name('RESULT.v1.json')
    if census_final_path.is_file():
        census_progress_path = census_final_path
    census_evidence = None
    census_note = None
    if census_progress_path.is_file():
        census = read(census_progress_path)
        if census_progress_path == census_final_path:
            assert census['packs_checked'] == len(census['packs']) == 48
            for pack in census['packs']:
                assert sha(pack['receipt']) == pack['sha256']
        census_evidence = {**census, 'progress_receipt': str(census_progress_path),
            'full_effect_coverage_verified': False, 'new_assets_converted': 0, 'full_import': False}
        census_note = (f"L06.EMBEDDED.CENSUS : {census['packs_checked']}/48 packs ; "
            f"{census['source_files_checked']} prefabs/scènes contrôlés, "
            f"{census.get('direct_effect_sources', 0)} sources avec composants d'effets directs, "
            f"dont {census.get('newly_discovered_direct_effect_sources', 0)} absentes de l'ancien IR. "
            "Sources et .meta réouverts par SHA avant/après chaque pack ; IR complet retenu "
            "pour les effets directs. Recensement en cours ; héritage des prefabs, dépendances, "
            "conversion et recette restent requis. Preuves : embedded-effect-source-census-v1/packs.")
        if census_progress_path == census_final_path:
            census_note = census_note.replace('Recensement en cours ;',
                f"Recensement sérialisé terminé ; {census['scan_errors']} refus de lecture conservés ;")
    audit_root = W / 'library-locked-integrity-reader-integration-v1'
    audit_progress = None
    audit_log = audit_root / 'audit.stderr.log'
    if audit_log.is_file():
        lines = audit_log.read_text('utf-8-sig').splitlines()
        for line in reversed(lines):
            try:
                progress = json.loads(line)
            except json.JSONDecodeError:
                continue
            if {'details_checked', 'images_read', 'total'} <= progress.keys():
                assert 0 <= progress['images_read'] <= progress['details_checked'] <= progress['total']
                kernel = ctypes.WinDLL('kernel32', use_last_error=True)
                kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
                kernel.OpenProcess.restype = wintypes.HANDLE
                kernel.GetExitCodeProcess.argtypes = [wintypes.HANDLE, ctypes.POINTER(wintypes.DWORD)]
                kernel.QueryFullProcessImageNameW.argtypes = [wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD)]
                kernel.CloseHandle.argtypes = [wintypes.HANDLE]
                handle = kernel.OpenProcess(0x1000, False, 37560)
                alive = False
                if handle:
                    try:
                        code = wintypes.DWORD()
                        size = wintypes.DWORD(32768)
                        name = ctypes.create_unicode_buffer(size.value)
                        alive = bool(kernel.GetExitCodeProcess(handle, ctypes.byref(code)) and code.value == 259
                            and kernel.QueryFullProcessImageNameW(handle, 0, name, ctypes.byref(size))
                            and Path(name.value).name == 'library-audit.locked.immutable.v1.exe')
                    finally:
                        kernel.CloseHandle(handle)
                audit_progress = dict(observed_utc=now, **progress, process_id=37560,
                    exact_executable_confirmed_live=alive, source_log=str(audit_log),
                    last_progress_line=line, terminal_result=False,
                    converted_assets_validated=0, actual_ui_visual_review=False, full_import=False)
                break
    audit_note = (f"{audit_progress['details_checked']}/{audit_progress['total']} fiches et "
                  f"{audit_progress['images_read']} images lues ; processus exact vivant : "
                  f"{audit_progress['exact_executable_confirmed_live']}. Audit non terminal, "
                  "aucun nouvel asset converti ou rendu qualifié."
                  if audit_progress else 'Aucun compteur de progression lisible.')
    checkpoint_path = W / 'library-checkpointed-native-audit-v2/INDEPENDENT-CHECKPOINT-REOPEN.v1.json'
    if checkpoint_path.with_name('INDEPENDENT-CHECKPOINT-REOPEN.v2.json').is_file():
        checkpoint_path = checkpoint_path.with_name('INDEPENDENT-CHECKPOINT-REOPEN.v2.json')
    if checkpoint_path.with_name('INDEPENDENT-CHECKPOINT-REOPEN.v3.json').is_file():
        checkpoint_path = checkpoint_path.with_name('INDEPENDENT-CHECKPOINT-REOPEN.v3.json')
    checkpoint_versions = list(checkpoint_path.parent.glob('INDEPENDENT-CHECKPOINT-REOPEN.v*.json'))
    if checkpoint_versions:
        checkpoint_path = max(checkpoint_versions, key=lambda p: int(p.name.split('.v')[1].split('.')[0]))
    checkpoint_evidence = None
    if checkpoint_path.is_file():
        checkpoint_evidence = read(checkpoint_path)
        for batch in checkpoint_evidence['batches']:
            assert sha(batch['file']) == batch['sha256']
        audit_note += (f" Ancien audit incomplet conservé. Reprise v2 : {checkpoint_evidence['details_checked']}/26 735 fiches et images dans des groupes durables, réouverts indépendamment. Audit global toujours non terminal.")
    urp_v2_path = W / 'unity-villa-prepared-native-urp-v2/RUN-RESULT.v2.json'
    urp_v2 = read(urp_v2_path) if urp_v2_path.is_file() else None
    delta_defaults_path = W / 'delta-native-defaults-independent-reopen-v2/RESULT.v2.json'
    delta_vectors_path = W / 'delta-vector-node-converter-v3/INDEPENDENT-REOPEN.v1.json'
    delta_defaults = read(delta_defaults_path) if delta_defaults_path.is_file() else None
    delta_vectors = read(delta_vectors_path) if delta_vectors_path.is_file() else None
    if delta_defaults:
        assert delta_defaults['native_materials_reopened'] == 13 and not delta_defaults['full_import']
        for p, expected in delta_defaults['input_pins'].items():
            assert evidence_pin_matches(p, expected)
    if delta_vectors:
        assert delta_vectors['converted_shader_kernels_reopened'] == len(delta_vectors['rows']) == 6
        for row in delta_vectors['rows']:
            assert sha(row['wgsl']) == row['sha256']
    delta_stages_path = W / 'delta-vector-native-stages-v1/RESULT.v1.json'
    delta_uv1_path = W / 'delta-vector-uv1-binding-reopen-v1/RESULT.v1.json'
    position_frame_path = W / 'source-graph-position-frame-native-v1/RUN-RESULT.v1.json'
    if (W / 'source-graph-position-frame-native-v2/RUN-RESULT.v1.json').is_file():
        position_frame_path = W / 'source-graph-position-frame-native-v2/RUN-RESULT.v1.json'
    delta_stages = read(delta_stages_path) if delta_stages_path.is_file() else None
    delta_uv1 = read(delta_uv1_path) if delta_uv1_path.is_file() else None
    position_frame = read(position_frame_path) if position_frame_path.is_file() else None
    uv_binding_path = W / 'source-graph-uv-bindings-native-v1/RUN-RESULT.v1.json'
    for version in [2, 3]:
        candidate = W / f'source-graph-uv-bindings-native-v{version}/RUN-RESULT.v1.json'
        if candidate.is_file():
            uv_binding_path = candidate
    uv_binding = read(uv_binding_path) if uv_binding_path.is_file() else None
    uv_requirement_path = W / 'source-graph-uv-requirements-native-v2/RUN-RESULT.v1.json'
    uv_pipeline_path = W / 'source-graph-uv-pipeline-native-v1/RUN-RESULT.v1.json'
    uv_requirement = read(uv_requirement_path) if uv_requirement_path.is_file() else None
    uv_pipeline = read(uv_pipeline_path) if uv_pipeline_path.is_file() else None
    for evidence in [position_frame, uv_binding, uv_requirement, uv_pipeline]:
        if evidence and evidence.get('actual_exit_code') == 0:
            for p, expected in evidence['input_pins'].items():
                assert evidence_pin_matches(p, expected)
            log = W / ({'four_actual_position_frame_tests_passed':'source-graph-position-frame-native-v2',
                'three_actual_uv_binding_tests_passed':'source-graph-uv-bindings-native-v3',
                'ten_actual_uv_requirement_tests_passed':'source-graph-uv-requirements-native-v2',
                'thirteen_actual_uv_pipeline_tests_passed':'source-graph-uv-pipeline-native-v1'}[evidence['status']]) / 'native.log'
            assert sha(log) == evidence['native_log_sha256']
    delta_bevy_vertex_path = W / 'delta-vector-bevy-vertex-stages-v2/INDEPENDENT-REOPEN.v1.json'
    delta_bevy_vertex = read(delta_bevy_vertex_path) if delta_bevy_vertex_path.is_file() else None
    if delta_bevy_vertex:
        assert delta_bevy_vertex['native_vertex_programs_reopened'] == len(delta_bevy_vertex['rows']) == 6
        assert delta_bevy_vertex['actual_positive_compositions_reopened'] == 24
        assert len(delta_bevy_vertex['scoped_negative_compositions']) == 5
        assert not delta_bevy_vertex['renderer_installed'] and not delta_bevy_vertex['full_import']
        for p, expected in delta_bevy_vertex['input_pins'].items():
            assert evidence_pin_matches(p, expected)
    delta_surface_path = W / 'delta-native-surface-interfaces-v1/INDEPENDENT-REOPEN.v1.json'
    delta_surface = read(delta_surface_path) if delta_surface_path.is_file() else None
    if delta_surface:
        assert delta_surface['native_surface_interfaces_reopened'] == len(delta_surface['rows']) == 6
        assert delta_surface['actual_positive_compositions_reopened'] == 24
        assert delta_surface['qualifier_must_never_be_installed']
        assert not delta_surface['fragment_lighting_adapter_present']
        assert not delta_surface['renderer_installed'] and not delta_surface['full_import']
        for p, expected in delta_surface['input_pins'].items():
            assert evidence_pin_matches(p, expected)
    delta_lit_path = W / 'delta-native-lit-fragments-v1/INDEPENDENT-REOPEN.v1.json'
    delta_lit = read(delta_lit_path) if delta_lit_path.is_file() else None
    if delta_lit:
        assert delta_lit['native_lit_programs_reopened'] == len(delta_lit['rows']) == 6
        assert delta_lit['actual_positive_compositions_reopened'] == 46
        assert len(delta_lit['scoped_negative_compositions']) == 6
        assert delta_lit['source_alpha_cutout_material_count'] == delta_lit['source_alpha_to_mask_material_count'] == 4
        assert delta_lit['native_lighting_and_prepass_fragments_present']
        assert not delta_lit['renderer_installed'] and not delta_lit['full_import']
        for p, expected in delta_lit['input_pins'].items():
            assert evidence_pin_matches(p, expected)
    frame_admission_path = W / 'studio-source-frame-admission-native-v2/RUN-RESULT.v2.json'
    frame_admission = read(frame_admission_path) if frame_admission_path.is_file() else None
    if frame_admission:
        admission_prepared = read(frame_admission_path.parent / 'PREPARED.v2.json')
        assert admission_prepared['expected_tests'] == 17 and admission_prepared['new_frame_admission_tests'] == 3
        if frame_admission.get('status') == 'seventeen_actual_host_frame_admission_tests_passed':
            assert frame_admission.get('actual_exit_code') == 0
            assert frame_admission['tests_passed'] == 17 and frame_admission['all_input_pins_unchanged']
            for p, expected in frame_admission['input_pins'].items():
                assert evidence_pin_matches(p, expected)
            assert sha(frame_admission_path.parent / 'native.stdout.log') == frame_admission['stdout_sha256']
            assert sha(frame_admission_path.parent / 'native.stderr.log') == frame_admission['stderr_sha256']
        elif frame_admission.get('actual_exit_code') == 0:
            assert frame_admission['status'] == 'refused_or_unverified' and frame_admission['tests_passed'] == 0
            assert frame_admission['error'].startswith('Native input changed:')
            assert 'test result: ok. 17 passed; 0 failed;' in (frame_admission_path.parent / 'native.stdout.log').read_text('utf-8-sig')
            assert any(sha(p) != d for p,d in admission_prepared['input_pins'].items())
    frozen_rgb8_path = W / 'delta-rgb8-frozen-canonical-loader-v3/RUN-RESULT.v1.json'
    if (W / 'delta-rgb8-frozen-canonical-loader-v4/RUN-RESULT.v1.json').is_file():
        frozen_rgb8_path = W / 'delta-rgb8-frozen-canonical-loader-v4/RUN-RESULT.v1.json'
    if (W / 'delta-rgb8-frozen-canonical-loader-v5/RUN-RESULT.v1.json').is_file():
        frozen_rgb8_path = W / 'delta-rgb8-frozen-canonical-loader-v5/RUN-RESULT.v1.json'
    if (W / 'delta-rgb8-frozen-canonical-loader-v6/RUN-RESULT.v1.json').is_file():
        frozen_rgb8_path = W / 'delta-rgb8-frozen-canonical-loader-v6/RUN-RESULT.v1.json'
    if (W / 'delta-rgb8-frozen-canonical-loader-v7/RUN-RESULT.v1.json').is_file():
        frozen_rgb8_path = W / 'delta-rgb8-frozen-canonical-loader-v7/RUN-RESULT.v1.json'
    frozen_rgb8 = read(frozen_rgb8_path) if frozen_rgb8_path.is_file() else None
    canonical_rgb8_path = W / 'delta-rgb8-canonical-loader-native-v2/RUN-RESULT.v1.json'
    canonical_rgb8 = read(canonical_rgb8_path) if canonical_rgb8_path.is_file() else None
    complete_prefab_source_path = W / 'delta-complete-prefab-source-reopen-v1/RESULT.v1.json'
    complete_prefab_source = read(complete_prefab_source_path) if complete_prefab_source_path.is_file() else None
    if complete_prefab_source:
        assert complete_prefab_source['effect_sources_reopened'] == 2 and complete_prefab_source['manifests_searched'] == 48
        assert complete_prefab_source['complete_parent_overrides_retained'] and not complete_prefab_source['full_import']
        for group in ['input_pins','output_pins']:
            for p,d in complete_prefab_source[group].items():
                assert sha(p) == d
    firepit_behavior_path = W / 'unity-delta-firepit-particle-behavior-v1/RUN-RESULT.v1.json'
    if (W / 'unity-delta-firepit-particle-behavior-v2/RUN-RESULT.v1.json').is_file():
        firepit_behavior_path = W / 'unity-delta-firepit-particle-behavior-v2/RUN-RESULT.v1.json'
    for version in [3, 4, 5, 6, 7]:
        candidate = W / f'unity-delta-firepit-particle-behavior-v{version}/RUN-RESULT.v1.json'
        if candidate.is_file():
            firepit_behavior_path = candidate
    firepit_behavior = read(firepit_behavior_path) if firepit_behavior_path.is_file() else None
    firepit_hdrp_repair_path = W / 'delta-firepit-hdrp-closure-repair-v2/RESULT.v1.json'
    firepit_hdrp_repair = read(firepit_hdrp_repair_path) if firepit_hdrp_repair_path.is_file() else None
    if firepit_hdrp_repair:
        assert firepit_hdrp_repair['missing_component_guid_verified'] and firepit_hdrp_repair['original_sources_unchanged']
        assert firepit_hdrp_repair['prior_terminal_refusal_preserved'] and not firepit_hdrp_repair['full_import']
        repair_plan_path = firepit_hdrp_repair_path.parent / 'PLAN.v1.json'
        assert sha(repair_plan_path) == firepit_hdrp_repair['plan_sha256']
        repair_plan = read(repair_plan_path)
        assert repair_plan['verified_native_component_guid'] == '7a68c43fe1f2a47cfa234b5eeaa98012'
        # Source closure staging is historical; later Unity serialization does
        # not become a current unchanged-project assertion here.
        for p,d in firepit_hdrp_repair['input_pins'].items():
            if '/unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Assets/' not in p.replace('\\','/'):
                assert sha(p) == d
    if firepit_behavior and firepit_behavior.get('native_behavior_verified'):
        proof_path = firepit_behavior_path.parent / 'INDEPENDENT-REOPEN.v1.json'
        assert sha(proof_path) == firepit_behavior['independent_receipt_sha256']
        proof = read(proof_path)
        assert proof['frames_verified'] == 112 and proof['effect_systems_verified'] == 2
        assert sha(firepit_behavior_path.parent / 'BEHAVIOR.v1.json') == proof['behavior_sha256']
        for p,d in proof['input_pins'].items():
            assert sha(p) == d
    if canonical_rgb8 and canonical_rgb8.get('canonical_renderer_installed'):
        transition_path = canonical_rgb8_path.parent / 'SOURCE-TRANSITION-INSTALLED.v1.json'
        assert sha(transition_path) == canonical_rgb8['source_transition_sha256']
        transition = read(transition_path)
        assert len(transition['files']) == 3
        for row in transition['files']:
            assert sha(row['path']) == sha(row['candidate']) == row['after_sha256']
            assert sha(row['backup']) == row['before_sha256']
        if canonical_rgb8.get('actual_host_tests_passed') == 17:
            assert canonical_rgb8['actual_exit_code'] == 0 and canonical_rgb8['all_current_input_pins_unchanged']
            for p,d in transition['input_pins'].items():
                assert sha(p) == d
            assert sha(canonical_rgb8_path.parent / 'native.stdout.log') == canonical_rgb8['stdout_sha256']
            assert sha(canonical_rgb8_path.parent / 'native.stderr.log') == canonical_rgb8['stderr_sha256']
    rgb8_closure_path = W / 'delta-rgb8-inclusion-closure-queue-v1/RUN-RESULT.v1.json'
    rgb8_closure = read(rgb8_closure_path) if rgb8_closure_path.is_file() else None
    if frozen_rgb8 and frozen_rgb8.get('actual_host_tests_passed') == 17:
        assert frozen_rgb8['actual_exit_code'] == 0 and frozen_rgb8['all_frozen_input_pins_unchanged']
        assert sha(frozen_rgb8_path.parent / 'SNAPSHOT.v1.json') == frozen_rgb8['snapshot_sha256']
        assert sha(frozen_rgb8_path.parent / 'native.stdout.log') == frozen_rgb8['stdout_sha256']
        assert sha(frozen_rgb8_path.parent / 'native.stderr.log') == frozen_rgb8['stderr_sha256']
        for p,d in read(frozen_rgb8_path.parent / 'PREPARED.v1.json')['input_pins'].items():
            assert evidence_pin_matches(p, d)
    remaining_delta_path = W / 'unity-delta-remaining-native-materials-v1/RUN-RESULT.v1.json'
    if (W / 'unity-delta-remaining-native-materials-v2/RUN-RESULT.v1.json').is_file():
        remaining_delta_path = W / 'unity-delta-remaining-native-materials-v2/RUN-RESULT.v1.json'
    remaining_delta = read(remaining_delta_path) if remaining_delta_path.is_file() else None
    rocks_path = W / 'delta-rockstemperate-native-independent-reopen-v1/RESULT.v1.json'
    rocks = read(rocks_path) if rocks_path.is_file() else None
    rocks_later_private_changes = []
    if rocks:
        assert rocks['native_materials_verified'] == 1 and rocks['active_texture_bindings_verified'] == 7
        assert not rocks['source_and_native_saved_float_color_differences']
        assert len(rocks['refused_mutations']) == 4 and not rocks['full_import']
        for p,d in rocks['input_pins'].items():
            current = sha(p)
            if current != d:
                private_firepit = W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Assets/Hivemind/NativeAmericanVillage/HDRP(Default)/Art/Materials/Instances/M_FirePit_A_01.mat'
                assert Path(p) == private_firepit
                later = read(W / 'unity-delta-firepit-closure-retry-v3/RUN-RESULT.v1.json')
                assert later['actual_unity_exit_code'] == 0 and later['status'] == 'refused_or_unverified'
                assert p in later['error']
                rocks_later_private_changes.append(dict(path=p, original_sha256=d,
                    observed_sha256=current, reason='Later FirePit import changed its private copy; historical Rocks receipt preserved'))
    firepit_path = W / 'unity-delta-firepit-closure-retry-v3/RUN-RESULT.v1.json'
    firepit = read(firepit_path) if firepit_path.is_file() else None
    firepit_reopen_path = W / 'delta-firepit-native-serialization-reopen-v1/RESULT.v1.json'
    firepit_reopen = read(firepit_reopen_path) if firepit_reopen_path.is_file() else None
    rocks_texture_path = W / 'unity-delta-rocks-firepit-texture-storage-v1/TEXTURE-REOPEN.v1.json'
    rocks_texture = read(rocks_texture_path) if rocks_texture_path.is_file() else None
    if rocks_texture:
        assert rocks_texture['source_texture_storage_captures_verified'] == 10 and rocks_texture['mips_verified'] == 120
        assert rocks_texture['format_block_layout_checked'] and not rocks_texture['full_import']
        for p,d in rocks_texture['input_pins'].items():
            assert evidence_pin_matches(p, d)
    rocks_effective_path = W / 'delta-rocks-firepit-effective-state-v1/RESULT.v1.json'
    rocks_effective = read(rocks_effective_path) if rocks_effective_path.is_file() else None
    if rocks_effective:
        assert rocks_effective['materials_compared'] == 2 and not rocks_effective['original_render_state_parity_qualified']
        for p,d in rocks_effective['input_pins'].items():
            assert evidence_pin_matches(p, d)
    rocks_gpu_path = W / 'delta-rocks-firepit-texture-gpu-v1/RUN-RESULT.v1.json'
    rocks_gpu = read(rocks_gpu_path) if rocks_gpu_path.is_file() else None
    rocks_normal_path = W / 'unity-delta-rocks-firepit-normal-reference-v1/RUN-RESULT.v1.json'
    rocks_normal = read(rocks_normal_path) if rocks_normal_path.is_file() else None
    rocks_raw_path = W / 'delta-rocks-firepit-normal-raw-gpu-parity-v1/RESULT.v1.json'
    rocks_raw = read(rocks_raw_path) if rocks_raw_path.is_file() else None
    if rocks_raw:
        assert rocks_raw['normal_texture_raw_sampling_verified'] and rocks_raw['normal_textures_compared'] == 4
        assert rocks_raw['mip_levels_compared'] == 48 and rocks_raw['rgba_tolerance'] == 1e-6
        assert rocks_raw['common_measured_coordinate_mappings'] == ['identity']
        for p,d in rocks_raw['source_proof_pins'].items():
            assert evidence_pin_matches(p, d)
    rocks_lit_path = W / 'delta-rocks-firepit-native-lit-v1/INDEPENDENT-REOPEN.v1.json'
    rocks_lit = read(rocks_lit_path) if rocks_lit_path.is_file() else None
    if rocks_lit:
        assert rocks_lit['native_lit_programs_reopened'] == 2 and rocks_lit['actual_positive_compositions_reopened'] == 14
        assert len(rocks_lit['scoped_negative_compositions']) == 6 and not rocks_lit['renderer_installed']
        for p,d in rocks_lit['input_pins'].items():
            assert evidence_pin_matches(p, d)
    rocks_decoder_path = W / 'delta-rocks-firepit-normal-decoder-gpu-v1/RUN-RESULT.v1.json'
    rocks_decoder = read(rocks_decoder_path) if rocks_decoder_path.is_file() else None
    if rocks_decoder and rocks_decoder.get('native_normal_textures_verified') == 4:
        assert rocks_decoder['normal_mips_verified'] == 48
        assert rocks_decoder['execution_exit_code'] == rocks_decoder['comparison_exit_code'] == 0
        assert sha(rocks_decoder_path.parent / 'DECODER-PARITY.v1.json') == rocks_decoder['parity_receipt_sha256']
        for p,d in read(rocks_decoder_path.parent / 'PREPARED.v1.json')['input_pins'].items():
            assert evidence_pin_matches(p, d)
    rocks_license_path = W / 'delta-rocks-firepit-program-licenses-v1/RESULT.v1.json'
    rocks_license = read(rocks_license_path) if rocks_license_path.is_file() else None
    if rocks_license:
        assert rocks_license['copied_files'] == 5 and not rocks_license['full_import']
        for p,d in rocks_license['input_pins'].items():
            assert evidence_pin_matches(p, d)
    if rocks_normal and rocks_normal.get('native_normal_textures_verified') == 4:
        assert rocks_normal['unity_exit_code'] == rocks_normal['verifier_exit_code'] == 0
        assert rocks_normal['reference_mips_verified'] == 48
        assert sha(rocks_normal_path.parent / 'GPU-REFERENCE-REOPEN.v1.json') == rocks_normal['native_reference_sha256']
        for p,d in read(rocks_normal_path.parent / 'PREPARED.v1.json')['input_proofs'].items():
            assert evidence_pin_matches(p, d)
    if rocks_gpu and rocks_gpu.get('gpu_textures_verified') == 10:
        assert rocks_gpu['gpu_mips_verified'] == 120 and rocks_gpu['execution_exit_code'] == 0
        assert sha(rocks_gpu_path.parent / 'GPU-REOPEN.v1.json') == rocks_gpu['gpu_reopen_sha256']
        for p,d in read(rocks_gpu_path.parent / 'PREPARED.v1.json')['input_pins'].items():
            assert evidence_pin_matches(p, d)
    if firepit_reopen:
        assert firepit_reopen['native_materials_reopened'] == 1
        assert firepit_reopen['active_texture_bindings_verified'] == 3
        assert not firepit_reopen['source_and_native_saved_float_color_differences']
        assert len(firepit_reopen['refused_mutations']) == 4
        assert not firepit_reopen['full_import'] and not firepit_reopen['private_serialization_unchanged']
        for p,d in firepit_reopen['input_pins'].items():
            assert evidence_pin_matches(p, d)
    if firepit and firepit.get('native_materials_verified') == 1:
        assert firepit['actual_unity_exit_code'] == 0 and firepit['active_texture_bindings_verified'] == 3
        assert sha(firepit_path.parent / 'INDEPENDENT-REOPEN.v1.json') == firepit['independent_receipt_sha256']
        assert sha(firepit_path.parent / 'NATIVE-RESULT.v1.json') == firepit['native_result_sha256']
        for p,d in read(firepit_path.parent / 'PREPARED.v1.json')['input_pins'].items():
            assert evidence_pin_matches(p, d)
    if remaining_delta and remaining_delta.get('native_materials_verified') == 2:
        remaining_proof_path = remaining_delta_path.parent / 'INDEPENDENT-REOPEN.v1.json'
        remaining_proof = read(remaining_proof_path)
        assert remaining_proof['active_texture_bindings_verified'] == 10 and len(remaining_proof['refused_mutations']) == 4
        assert sha(remaining_proof_path) == remaining_delta['independent_receipt_sha256']
        assert sha(remaining_delta_path.parent / 'NATIVE-RESULT.v1.json') == remaining_delta['native_result_sha256']
    foliage_texture_root = W / 'unity-delta-foliage-texture-storage-v1'
    if (W / 'unity-delta-foliage-texture-storage-v2/RUN-RESULT.v1.json').is_file():
        foliage_texture_root = W / 'unity-delta-foliage-texture-storage-v2'
    foliage_texture_path = foliage_texture_root / 'RUN-RESULT.v1.json'
    foliage_texture = read(foliage_texture_path) if foliage_texture_path.is_file() else None
    if foliage_texture:
        prepared = read(foliage_texture_root / 'PREPARED.v1.json')
        assert prepared['texture_cases'] == 18 and prepared['original_materials'] == 6
        assert prepared['api_compile_exit_code'] == 0
        assert sha(foliage_texture_root / 'manifest.json') == prepared['manifest_sha256']
        for p, expected in prepared['input_pins'].items():
            assert evidence_pin_matches(p, expected)
    if delta_stages:
        assert delta_stages['vertex_and_fragment_stage_probes_validated'] == len(delta_stages['rows']) == 6 and not delta_stages['full_import']
        for row in delta_stages['rows']:
            assert sha(row['program']) == row['program_sha256'] and sha(row['probe']) == row['probe_sha256']
    if delta_uv1:
        assert delta_uv1['renderer_nodes_checked'] == len(delta_uv1['rows']) == 52
        assert delta_uv1['unique_geometry_streams_checked'] == len(delta_uv1['geometries']) == 27
        for p, h in delta_uv1['input_pins'].items():
            assert evidence_pin_matches(p, h)
    migration_path = W / 'hdrp-v4-material-migration-fields-v1/RESULT.v1.json'
    dust_path = W / 'dust-exact-resource-identity-reopen-v2/RESULT.v2.json'
    dust_evidence = None
    dust_note = ''
    if dust_path.is_file():
        dust = read(dust_path)
        assert dust['status'] == 'exact_source_shader_and_texture_resource_identities_reopened'
        assert dust['prior_refusal_preserved'] and dust['source_and_copy_unchanged']
        assert not dust['passes_compiled_verified'] and not dust['gpu_render_parity_qualified']
        assert not dust['runtime_converted'] and not dust['full_import']
        dust_evidence = dict(receipt=str(dust_path), receipt_sha256=sha(dust_path),
            checked_utc=dust['checked_utc'], resource_identities_reopened=3,
            properties_observed=dust['properties_observed'],
            prior_refusal_preserved=True, passes_compiled_verified=False,
            texture_pixels_captured=False, runtime_converted=False, full_import=False)
        dust_note = ("L06.DUST.IDENTITY : 3/3 identités exactes matériau/shader/texture réouvertes ; "
            "30 propriétés natives retenues ; 7 contrôles négatifs refusés. Sources et copies "
            "réouvertes par SHA avant/après. Le refus antérieur reste conservé : nom vide pour "
            "la passe 0. Ce reçu sépare l'identité des ressources de la qualification des passes. "
            "Version d'origine, compilation des passes, pixels, rendu GPU, conversion runtime "
            "et recette Studio restent non qualifiés. "
            "Reçu : `dust-exact-resource-identity-reopen-v2/RESULT.v2.json`.")
    effects_path = W / 'effect-execution-batches-v1/RESULT.v1.json'
    embedded_batches_path = W / 'embedded-effect-execution-batches-v1/RESULT.v1.json'
    embedded_batches_evidence = None
    embedded_batches_note = None
    if embedded_batches_path.is_file():
        extension = read(embedded_batches_path)
        assert extension['newly_partitioned_sources'] == 89
        assert sum(b['expected'] for b in extension['batches']) == 89
        assert len(extension['scan_refusals']) == 6 and not extension['full_import']
        embedded_batches_evidence = dict(receipt=str(embedded_batches_path),
            receipt_sha256=sha(embedded_batches_path), batch_count=extension['batch_count'],
            newly_partitioned_sources=89, scan_refusals=6, full_import=False)
        embedded_batches_note = (f"L06.EMBEDDED.BATCHES : 89 nouveaux cas répartis en "
            f"{extension['batch_count']} sous-lots de cinq sources maximum, par pack et format ; "
            "les 85 cas existants et 139 graphes restent dans le plan antérieur. Six refus de lecture "
            "ont une action native explicite. Sources, .meta et IR des nouveaux cas relus par SHA avant/après. "
            "Aucune conversion runtime acquise. Liste : "
            "[nouveaux sous-lots](../embedded-effect-execution-batches-v1/SOUS_LOTS.md).")
    effects_evidence = None
    effects_note = ''
    if effects_path.is_file():
        effects = read(effects_path)
        assert effects['entry_counts'] == {'unresolved_effect_prefab': 85, 'vfx_graph_source_file': 139}
        assert effects['source_inventory_verified'] == 224 and effects['new_assets_converted'] == 0
        assert not effects['full_collection_effect_coverage_verified'] and not effects['full_import']
        ids = [entry['id'] for batch in effects['batches'] for entry in batch['entries']]
        assert len(ids) == len(set(ids)) == 224
        effects_evidence = dict(receipt=str(effects_path), receipt_sha256=sha(effects_path),
            checked_utc=effects['checked_utc'], batch_count=effects['batch_count'],
            source_inventory_verified=224, new_assets_converted=0,
            full_collection_effect_coverage_verified=False, full_import=False)
        effects_note = (f"L06.EFFECTS.BATCHES : {effects['batch_count']} sous-lots exacts couvrent "
            "85 prefabs et 139 fichiers de graphes ; sources, .meta et IR réouverts par SHA "
            "avant/après lors du reçu. IDs, composants, flags de modules, modes de rendu, "
            "licences et hashes conservés. Aucun effet converti ou rendu qualifié par ce découpage. "
            "Le recensement complémentaire terminé et ses 29 nouveaux sous-lots sont indiqués ci-dessus ; "
            "héritage des prefabs et six scènes binaires restent à inspecter. "
            "Liste : [sous-lots des effets](../effect-execution-batches-v1/SOUS_LOTS.md). "
            "Reçu : `effect-execution-batches-v1/RESULT.v1.json`.")
    medieval_refusal_path = W / 'medieval-three-material-refusal-analysis-v1/RESULT.v1.json'
    medieval_refusal_evidence = None
    medieval_refusal_note = ''
    if medieval_refusal_path.is_file():
        refusal = read(medieval_refusal_path)
        assert refusal['status'] == 'three_medieval_original_materials_preserved_native_capture_refused'
        assert refusal['materials'] == refusal['failed'] == 3 and refusal['captured'] == 0
        assert refusal['input_pins_unchanged'] and not refusal['migration_accepted']
        medieval_refusal_evidence = dict(receipt=str(medieval_refusal_path),
            receipt_sha256=sha(medieval_refusal_path), original_sources_reopened=refusal['original_source_files_reopened'],
            original_materials_unchanged=refusal['original_materials_unchanged'],
            private_copies_changed=refusal['private_material_copies_changed'],
            original_serialized_properties_retained=refusal['original_serialized_properties_retained'],
            captured=0, failed=3, native_materials_verified=0, migration_accepted=False, gpu_verified=False)
        medieval_refusal_note = (f"L04.MEDIEVAL.MATERIAL.FAILURE : 0/3 captures acceptées ; "
            f"{medieval_refusal_evidence['original_sources_reopened']} fichiers sources réouverts intacts, "
            "dont les trois matériaux. Trois copies privées modifiées par Unity ; propriétés sérialisées "
            "d'origine conservées. Différences retenues : version 12→13, champs de parent et de verrouillage. "
            "Le résultat natif refuse aussi les trois sous-shaders pour un tag HDRP vide. "
            "Mesurer et corriger le contexte natif avant toute règle explicite de migration des copies. "
            "Reçu : `medieval-three-material-refusal-analysis-v1/RESULT.v1.json`. Sources et contrôleurs actifs inchangés.")
    migration_evidence = None
    migration_note = ''
    if migration_path.is_file():
        migration = read(migration_path)
        assert migration['status'] == '42_source_and_private_material_field_comparisons_retained'
        assert migration['materials'] == 42 and migration['input_pins_unchanged']
        assert not migration['migration_accepted'] and not migration['full_import']
        migration_evidence = dict(receipt=str(migration_path), receipt_sha256=sha(migration_path),
            compared=migration['materials'], byte_identical=migration['byte_identical'],
            original_properties_retained=migration['materials_retaining_original_serialized_properties'],
            changed_copies=migration['materials_with_field_differences'],
            added_property_occurrences=migration['added_property_occurrences'],
            migration_accepted=False, native_materials_verified=0, gpu_verified=False)
        migration_note = (f"L02.HDRP.MIGRATION.FIELDS : {migration_evidence['compared']}/42 matériaux "
            f"comparés champ par champ ; {migration_evidence['original_properties_retained']}/42 conservent "
            f"leurs propriétés sérialisées d'origine. {migration_evidence['changed_copies']} copies privées "
            f"présentent des différences, dont {migration_evidence['added_property_occurrences']} propriétés "
            "ajoutées. Relevé historique avec sources et hashes conservés ; aucun changement des contrôleurs "
            "actifs, aucune migration acceptée, aucune parité native/GPU qualifiée. Observer les valeurs et "
            "effets des ajouts et des keywords avant toute règle de migration explicite. "
            "Reçu : `hdrp-v4-material-migration-fields-v1/RESULT.v1.json`.")
    frame_path = W / 'studio-source-scene-frame-native-v1/RUN-RESULT.v1.json'
    host_path = W / 'studio-source-scene-host-native-v1/RUN-RESULT.v1.json'
    frame, host = read(frame_path), read(host_path)
    assert frame['tests_passed'] == 12 and frame['all_input_pins_unchanged']
    assert host['status'] == '14_actual_shared_scene_host_module_tests_verified'
    assert host['tests_passed'] == 14 and host['all_input_pins_unchanged']
    host_changes = [path for path, expected in host['input_pins'].items() if sha(path) != expected]
    host_note = ('14/14 tests réussis ; entrées réouvertes sans modification.' if not host_changes
                 else f'14/14 tests réussis sur le snapshot antérieur ; {len(host_changes)} entrées ont changé depuis. Contrôle WASM actuel requis.')
    textures = W / 'nested-texture-all-four-reconciliation-v1/RESULT.v1.json'
    assert textures.is_file()
    skin_path = W / 'medieval-native-skin-khronos-validation-v1/RESULT.v1.json'
    skin_status = 'Positions et normales de 1 024 vertices sur 2 poses réconciliées ; conversion finale et rendu restent à faire.'
    skin_evidence = None
    if skin_path.exists():
        skin = read(skin_path)
        assert skin['status'] == 'native_skin_glb_khronos_validated'
        assert skin['issues']['numErrors'] == 0 and skin['input_pins_unchanged']
        skin_evidence = dict(receipt=str(skin_path), receipt_sha256=sha(skin_path),
                             errors=0, warnings=skin['issues']['numWarnings'],
                             deformation_receipt=str(W / 'medieval-native-skin-glb-reopen-v1/RESULT.v1.json'),
                             material_qualification_pending=True, gpu_verified=False, studio_verified=False)
        skin_status = 'GLB riggé réouvert : 1 024 vertices, 1 os, 3 nœuds, 2 poses ; Khronos 0 erreur / 1 avertissement conservé. Matériaux, GPU et Studio à qualifier.'
    recovery = W / 'villa-hierarchy-version-normalization-recovery-v1/RESULT.v1.json'
    reopened = W / 'villa-native-prefab-hierarchy-independent-reopen-v1/RESULT.v1.json'
    hierarchy = '76 captures produites ; récupération du réglage de version privé en contrôle, puis réouverture indépendante de 226 IDs.'
    if reopened.exists():
        native = read(reopened)
        hierarchy = f"{native['logical_scenes_reconciled']}/226 IDs réconciliés ; {len(native['refusals'])} refus conservés. Rendu et Studio restent à vérifier."
    elif recovery.exists():
        hierarchy = '76/76 captures réouvertes après normalisation documentée de la version privée ; vérification indépendante des 226 IDs en cours.'
    jobs = [
        ('L02.HDRP.PASSES.V4', W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4/RUN-RESULT.v4.json', 42),
        ('L04.MEDIEVAL.MATERIAL', W / 'unity-medieval-cloth-material-source-v1/RUN-RESULT.v1.json', 3),
        ('L04.VILLA.DEFAULTS.V3', W / 'unity-villa-normal-default-api-observation-v3/RUN-RESULT.v1.json', 4),
        ('L04.DELTA.MATERIALS.V2', W / 'unity-delta-null-material-defaults-v2/RUN-RESULT.v1.json', 13),
    ]
    prepared_capture_path = W / 'unity-dungeon-native-prepared-pipeline-v6/RUN-RESULT.v6.json'
    if prepared_capture_path.is_file():
        jobs.append(('L02.HDRP.PASSES.V6', prepared_capture_path, 42))
    delta_current_path = W / 'unity-delta-warm-prepared-native-v3/RUN-RESULT.v3.json'
    if delta_current_path.is_file():
        jobs.append(('L04.DELTA.MATERIALS.V3', delta_current_path, 13))
    pirate_path = W / 'unity-pirate-exact-subgraph-native-v4/RUN-RESULT.v4.json'
    if pirate_path.is_file():
        jobs.append(('L04.PIRATE.MATERIALS.V4', pirate_path, 12))
    medieval_current = W / 'unity-medieval-warm-native-hdrp-v2/RUN-RESULT.v2.json'
    if medieval_current.is_file():
        jobs.append(('L04.MEDIEVAL.MATERIAL.V2', medieval_current, 3))
    medieval_reopened_path = medieval_current.with_name('INDEPENDENT-REOPEN.v1.json')
    medieval_reopened_evidence = None
    if medieval_reopened_path.is_file():
        medieval_reopened = read(medieval_reopened_path)
        assert medieval_reopened['native_materials_verified'] == len(medieval_reopened['rows']) == 3
        for path, expected in medieval_reopened['input_pins'].items():
            assert evidence_pin_matches(path, expected)
        medieval_reopened_evidence = dict(receipt=str(medieval_reopened_path),
            receipt_sha256=sha(medieval_reopened_path), **medieval_reopened)
    delta_analysis_path = pirate_path.with_name('DELTA-REFUSAL-ANALYSIS.v1.json')
    delta_analysis_evidence, delta_analysis_note = None, None
    if delta_analysis_path.is_file():
        analysis = read(delta_analysis_path)
        assert analysis['original_requested'] == 13 and analysis['captured'] == 1 and analysis['refused'] == 12
        assert len(analysis['independently_reopened_successful_subset']) == 1
        assert not analysis['original_graph_semantics_rewritten'] and not analysis['fallback_used']
        delta_analysis_evidence = dict(receipt=str(delta_analysis_path),
            receipt_sha256=sha(delta_analysis_path), **analysis)
        delta_analysis_note = ("L04.DELTA.REFUSAL : 1/13 capture réussie réouverte indépendamment ; "
            "12/13 refus conservés. Cause mesurée : sous-graphe authored absent du projet privé ; "
            "Unity échoue dans GraphData.CollectShaderKeywords sur node.asset.keywords. "
            "Sous-graphe exact et .meta retrouvés dans l'inventaire, relus par SHA et ajoutés "
            "au projet privé sans changer le graphe ni utiliser de shader de remplacement. "
            "Reprise limitée aux 12 matériaux Pirate Island, sans refaire le cas Native American. "
            "État courant dans le tableau ; réouverture indépendante puis rendu restent requis. "
            "Reçu : unity-pirate-exact-subgraph-native-v4/DELTA-REFUSAL-ANALYSIS.v1.json.")
    pirate_reopened_path = pirate_path.with_name('INDEPENDENT-REOPEN.v1.json')
    pirate_reopened_evidence = None
    if pirate_reopened_path.is_file():
        reopened_pirate = read(pirate_reopened_path)
        assert reopened_pirate['native_materials_verified'] == len(reopened_pirate['rows']) == 12
        for path, expected in reopened_pirate['input_pins'].items():
            assert evidence_pin_matches(path, expected)
        pirate_reopened_evidence = dict(receipt=str(pirate_reopened_path),
            receipt_sha256=sha(pirate_reopened_path), **reopened_pirate)
        delta_analysis_note += (" Reprise terminée : 12/12 captures et 12/12 réouvertures "
            "indépendantes réussies. Avec le cas conservé, 13/13 observations Delta ont une "
            "preuve de réouverture ; il s'agit de matériaux capturés, pas de 13 assets livrés. "
            "Parité du rendu et usage dans Forge restent requis. Reçu : "
            "unity-pirate-exact-subgraph-native-v4/INDEPENDENT-REOPEN.v1.json.")
    hdrp_reopened_path = prepared_capture_path.with_name('INDEPENDENT-REOPEN.v1.json')
    hdrp_reopened_note = None
    if hdrp_reopened_path.is_file():
        hdrp_reopened = read(hdrp_reopened_path)
        assert hdrp_reopened['native_materials_verified'] == len(hdrp_reopened['rows']) == 42
        for path, expected in hdrp_reopened['input_pins'].items():
            assert evidence_pin_matches(path, expected)
        hdrp_reopened_note = ("L02.HDRP.PASSES.V6.REOPEN : 42/42 captures réouvertes indépendamment ; "
            "identités matériau/shader, propriétés, files de rendu, keywords et passes nommées compilées "
            "contrôlés. Sources originales inchangées selon la capture ; ce vérificateur relit les snapshots, "
            "il ne qualifie ni pixels ni version Unity d'origine. Reçu : "
            "unity-dungeon-native-prepared-pipeline-v6/INDEPENDENT-REOPEN.v1.json. "
            "Suite : 13 captures Delta dans le projet initialisé, puis Medieval/URP et vérifications différées.")
    observed = [dict(id=identifier, expected=count, receipt=str(path), receipt_sha256=sha(path),
                     status=read(path)['status']) for identifier, path, count in jobs]
    reopen_path = W / 'queued-native-material-independent-reopens-v1/RUN-RESULT.v1.json'
    reopen_evidence = None
    if reopen_path.exists():
        reopen = read(reopen_path)
        reopen_evidence = dict(receipt=str(reopen_path), status=reopen['status'],
                               expected=58, verified=reopen['native_materials_verified'],
                               job=read(reopen_path.parent / 'JOB.v1.json'))
        observed.append(dict(id='L02/L04.MATERIAL.REOPEN', expected=58,
                             receipt=str(reopen_path), receipt_sha256=sha(reopen_path), status=reopen['status']))
    cache_path = W / 'queued-unity-package-cache-seeding-v1/RUN-RESULT.v1.json'
    cache_evidence = None
    if cache_path.exists():
        cache = read(cache_path)
        cache_evidence = dict(receipt=str(cache_path), status=cache['status'],
                              expected=4, verified=cache['caches_verified'],
                              job=read(cache_path.parent / 'JOB.v1.json'),
                              asset_sources_modified=False, full_import=False)
    wasm_path = W / 'studio-current-source-wasm-check-v1/RUN-RESULT.v1.json'
    wasm_v2 = W / 'studio-current-source-wasm-check-v2/RUN-RESULT.v1.json'
    if wasm_v2.exists():
        wasm_path = wasm_v2
    wasm_v3 = W / 'studio-current-source-wasm-check-v3/RUN-RESULT.v1.json'
    if wasm_v3.exists():
        wasm_path = wasm_v3
    wasm_evidence = None
    if wasm_path.exists():
        wasm = read(wasm_path)
        wasm_evidence = dict(receipt=str(wasm_path), status=wasm['status'],
                             job=read(wasm_path.parent / 'JOB.v1.json'),
                             gpu_rendered=False, studio_verified=False, full_import=False)
    lineage_path = W / 'collection-archive-lineage-reconciliation-v1/RESULT.v1.json'
    frontend_path = W / 'studio-actual-frontend-build-v2/RUN-RESULT.v1.json'
    frontend_evidence = None
    if frontend_path.exists():
        frontend = read(frontend_path)
        frontend_evidence = dict(receipt=str(frontend_path), status=frontend['status'],
                                 job=read(frontend_path.parent / 'JOB.v1.json'),
                                 gpu_rendered=False, studio_verified=False, full_import=False)
    candidate_path = W / 'studio-candidate-F-output-support-v1/RESULT.v1.json'
    no_png_path = W / 'library-no-png-source-batches-v1/RESULT.v1.json'
    urp_path = W / 'villa-urp-no-png-source-equivalence-v1/RESULT.v1.json'
    urp_geometry_path = W / 'villa-urp-counterpart-geometry-reopen-v1/RESULT.v1.json'
    urp_geometry_evidence = None
    if urp_geometry_path.exists():
        geometry = read(urp_geometry_path)
        assert geometry['status'] == 'nine_villa_counterpart_geometry_derivatives_reopened'
        assert not geometry['changed_inputs']
        urp_geometry_evidence = dict(receipt=str(urp_geometry_path), receipt_sha256=sha(urp_geometry_path),
                                     geometry_derivatives_reopened=geometry['geometry_derivatives_reopened'],
                                     validator_errors=sum(r['issues']['numErrors'] for r in geometry['rows']),
                                     validator_warnings=sum(r['issues']['numWarnings'] for r in geometry['rows']),
                                     hdrp_material_reuse_authorized=False, assets_delivered=0)
    urp_material_path = W / 'villa-urp-six-material-source-bindings-v1/RESULT.v1.json'
    urp_material_evidence = None
    if urp_material_path.exists():
        material = read(urp_material_path)
        assert material['status'] == 'six_villa_urp_materials_and_two_active_graphs_bound'
        urp_material_evidence = dict(receipt=str(urp_material_path), receipt_sha256=sha(urp_material_path),
                                    materials=6, active_graphs=2,
                                    unresolved_authored_texture_bindings=material['unresolved_authored_texture_bindings'],
                                    native_default_observations_required=material['native_default_observations_required'],
                                    assets_delivered=0, gpu_rendered=False)
    urp_capture_path = W / 'villa-urp-native-capture-inputs-v1/RESULT.v1.json'
    urp_capture_evidence = None
    if urp_capture_path.exists():
        capture_inputs = read(urp_capture_path)
        assert capture_inputs['status'] == 'six_original_urp_capture_requests_and_dependencies_pinned'
        urp_capture_evidence = dict(receipt=str(urp_capture_path), receipt_sha256=sha(urp_capture_path),
                                    requests=len(capture_inputs['requests']),
                                    source_files=len(capture_inputs['source_files']),
                                    unresolved_graph_references=len(capture_inputs['unresolved_graph_references']),
                                    native_capture_executed=False, assets_delivered=0)
    urp_api_path = W / 'villa-urp-native-helper-api-v1/RESULT.v1.json'
    urp_api_evidence = None
    if urp_api_path.exists():
        api = read(urp_api_path)
        assert api['status'] == 'urp_bootstrap_and_capture_helpers_api_compiled'
        assert not api['changed_inputs'] and all(r['actual_exit_code'] == 0 for r in api['helpers'])
        for helper in api['helpers']:
            assert sha(helper['helper']) == helper['sha256']
        urp_api_evidence = dict(receipt=str(urp_api_path), receipt_sha256=sha(urp_api_path),
                               actual_exit_codes=[r['actual_exit_code'] for r in api['helpers']],
                               native_capture_executed=False, assets_delivered=0)
    urp_queue_path = W / 'unity-villa-six-original-urp-materials-v1/RUN-RESULT.v1.json'
    if (W / 'unity-villa-prepared-native-urp-v2/RUN-RESULT.v2.json').is_file():
        urp_queue_path = W / 'unity-villa-prepared-native-urp-v2/RUN-RESULT.v2.json'
    urp_queue_evidence = None
    if urp_queue_path.exists():
        queue = read(urp_queue_path)
        urp_queue_evidence = dict(receipt=str(urp_queue_path), receipt_sha256=sha(urp_queue_path),
                                 status=queue['status'], native_materials_verified=queue['native_materials_verified'],
                                 expected_materials=6, assets_delivered=0)
    urp_reopen_path = W / 'villa-urp-six-material-independent-reopen-v1/RUN-RESULT.v1.json'
    urp_reopen_evidence = None
    if urp_reopen_path.exists():
        reopened = read(urp_reopen_path)
        control_path = urp_reopen_path.parent / 'CONTROLS.v1.json'
        control = read(control_path)
        urp_reopen_evidence = dict(receipt=str(urp_reopen_path), receipt_sha256=sha(urp_reopen_path),
                                  status=reopened['status'], native_materials_verified=reopened['native_materials_verified'],
                                  native_defaults_verified=reopened['native_defaults_verified'],
                                  controls_receipt=str(control_path), controls_sha256=sha(control_path),
                                  positive_fixture_controls=sum(control[k]['positive_fixture_controls'] for k in ['shared_controls', 'urp_controls']),
                                  negative_fixture_controls=sum(control[k]['negative_fixture_controls'] for k in ['shared_controls', 'urp_controls']),
                                  assets_delivered=0)
    native_candidate_path = W / 'studio-native-library-candidate-queue-v1/RUN-RESULT.v1.json'
    current_urp_reopen = W / 'unity-villa-prepared-native-urp-v2/INDEPENDENT-REOPEN.v1.json'
    if current_urp_reopen.is_file():
        reopened = read(current_urp_reopen)
        assert reopened['native_materials_verified'] == 6 and reopened['native_defaults_verified'] == 10
        urp_reopen_evidence = dict(receipt=str(current_urp_reopen), receipt_sha256=sha(current_urp_reopen), **reopened)
    foliage_material_path = W / 'unity-delta-foliage-native-materials-v2/RUN-RESULT.v1.json'
    foliage_material = read(foliage_material_path) if foliage_material_path.is_file() else None
    foliage_material_reopen_path = foliage_material_path.parent / 'INDEPENDENT-REOPEN.v1.json'
    foliage_material_reopen = read(foliage_material_reopen_path) if foliage_material_reopen_path.is_file() else None
    foliage_effective_path = foliage_material_path.parent / 'EFFECTIVE-PROPERTY-COMPARISON.v1.json'
    foliage_effective = read(foliage_effective_path) if foliage_effective_path.is_file() else None
    if foliage_material_reopen:
        assert foliage_material_reopen['native_materials_verified'] == 6
        assert foliage_material_reopen['texture_bindings_verified'] == 18
        assert sha(foliage_material_path.parent / 'NATIVE-RESULT.v1.json') == foliage_material_reopen['native_result_sha256']
    if foliage_effective:
        assert foliage_effective['materials_compared'] == 6 and not foliage_effective['original_render_state_parity_qualified']
    texture_independent_path = foliage_texture_root / 'TEXTURE-REOPEN.v1.json'
    texture_independent = read(texture_independent_path) if texture_independent_path.is_file() else None
    foliage_gpu_path = W / 'delta-foliage-texture-gpu-v2/RUN-RESULT.v1.json'
    if (W / 'delta-foliage-texture-gpu-v3/RUN-RESULT.v1.json').is_file():
        foliage_gpu_path = W / 'delta-foliage-texture-gpu-v3/RUN-RESULT.v1.json'
    foliage_gpu = read(foliage_gpu_path) if foliage_gpu_path.is_file() else None
    foliage_normal_path = W / 'unity-delta-foliage-normal-reference-v1/RUN-RESULT.v1.json'
    foliage_normal = read(foliage_normal_path) if foliage_normal_path.is_file() else None
    foliage_raw_parity_path = W / 'delta-foliage-normal-raw-gpu-parity-v1/RESULT.v1.json'
    foliage_raw_parity = read(foliage_raw_parity_path) if foliage_raw_parity_path.is_file() else None
    foliage_rgorag_path = W / 'delta-native-lit-fragments-rgorag-v2/INDEPENDENT-REOPEN.v1.json'
    foliage_rgorag = read(foliage_rgorag_path) if foliage_rgorag_path.is_file() else None
    foliage_decoder_path = W / 'delta-foliage-normal-decoder-gpu-v1/RUN-RESULT.v1.json'
    foliage_decoder = read(foliage_decoder_path) if foliage_decoder_path.is_file() else None
    foliage_rgb_reference_path = W / 'unity-delta-foliage-rgb24-reference-v1/RUN-RESULT.v1.json'
    foliage_rgb_reference = read(foliage_rgb_reference_path) if foliage_rgb_reference_path.is_file() else None
    foliage_rgb_parity_path = W / 'delta-foliage-rgb24-expansion-gpu-parity-v1/RESULT.v1.json'
    foliage_rgb_parity = read(foliage_rgb_parity_path) if foliage_rgb_parity_path.is_file() else None
    rgb8_transport_path = W / 'delta-rgb8-native-transport-candidate-v3/RUN-RESULT.v1.json'
    if (W / 'delta-rgb8-native-transport-candidate-v4/RUN-RESULT.v1.json').is_file():
        rgb8_transport_path = W / 'delta-rgb8-native-transport-candidate-v4/RUN-RESULT.v1.json'
    rgb8_transport = read(rgb8_transport_path) if rgb8_transport_path.is_file() else None
    rgb8_regression_path = W / 'delta-rgb8-native-regressions-v1/RUN-RESULT.v1.json'
    rgb8_regression = read(rgb8_regression_path) if rgb8_regression_path.is_file() else None
    if rgb8_regression:
        assert rgb8_regression['actual_exit_code'] == 0 and rgb8_regression['tests_passed'] == 15
        assert sha(rgb8_regression_path.parent / 'native.log') == rgb8_regression['native_log_sha256']
    if rgb8_transport and rgb8_transport.get('native_rgb24_textures_verified') == 3:
        rgb8_proof_path = rgb8_transport_path.parent / 'INDEPENDENT-GPU-REOPEN.v1.json'
        rgb8_proof = read(rgb8_proof_path)
        assert sha(rgb8_proof_path) == rgb8_transport['independent_receipt_sha256']
        assert rgb8_proof['mips_verified'] == 37 and rgb8_proof['coordinate_mapping'] == 'identity'
    if foliage_rgb_parity:
        assert foliage_rgb_parity['rgb24_textures_compared'] == 3 and foliage_rgb_parity['mip_levels_compared'] == 37
        assert foliage_rgb_parity['rgb24_expansion_raw_sampling_verified']
        assert foliage_rgb_parity['common_measured_coordinate_mappings'] == ['identity']
    if foliage_raw_parity:
        assert foliage_raw_parity['normal_textures_compared'] == 6 and foliage_raw_parity['mip_levels_compared'] == 75
        assert foliage_raw_parity['normal_texture_raw_sampling_verified']
        assert foliage_raw_parity['common_measured_coordinate_mappings'] == ['identity']
    if foliage_rgorag:
        assert foliage_rgorag['native_lit_programs_reopened'] == 6
        assert foliage_rgorag['actual_positive_compositions_reopened'] == 46
        assert len(foliage_rgorag['scoped_negative_compositions']) == 6
    if foliage_decoder and foliage_decoder.get('native_normal_textures_verified') == 6:
        decoder_parity_path = foliage_decoder_path.parent / 'DECODER-PARITY.v1.json'
        decoder_parity = read(decoder_parity_path)
        assert sha(decoder_parity_path) == foliage_decoder['parity_receipt_sha256']
        assert decoder_parity['exact_normal_textures_compared'] == 6 and decoder_parity['mip_levels_compared'] == 75
        assert decoder_parity['candidate_mode2_matches_float_rgorag_gpu_reference']
    if foliage_gpu and foliage_gpu.get('gpu_textures_verified') == 18:
        gpu_report_path = foliage_gpu_path.parent / 'GPU-REOPEN.v1.json'
        gpu_report = read(gpu_report_path)
        assert gpu_report['verified_gpu_textures'] == 18 and gpu_report['verified_mip_levels'] == 225
        assert sha(gpu_report_path) == foliage_gpu['gpu_reopen_sha256']
    if texture_independent:
        assert texture_independent['source_texture_storage_captures_verified'] == 18
        assert texture_independent['original_sources_and_materials_unchanged']
    native_candidate_evidence = None
    if native_candidate_path.exists():
        native_candidate = read(native_candidate_path)
        native_candidate_evidence = dict(receipt=str(native_candidate_path), receipt_sha256=sha(native_candidate_path),
                                         status=native_candidate['status'], candidate_built=native_candidate['candidate_built'],
                                         studio_verified=False, full_import=False)
    villa_tracking_path = W / 'villa-batch-tracking-reconciliation-v1/RESULT.v1.json'
    villa_tracking_evidence = None
    if villa_tracking_path.exists():
        villa_tracking = read(villa_tracking_path)
        assert villa_tracking['status'] == 'eleven_villa_batch_tracking_states_reconciled'
        assert len(villa_tracking['batches']) == 11 and villa_tracking['historical_receipt_ids_reconciled'] == 207
        villa_tracking_evidence = dict(receipt=str(villa_tracking_path), receipt_sha256=sha(villa_tracking_path),
                                      batches_reconciled=11, historical_derivative_ids_reconciled=207,
                                      derivative_payloads_reopened_now=False, new_assets_validated=0)
    hdrp_failure_path = W / 'hdrp-v4-failure-and-material-copy-analysis-v1/RESULT.v1.json'
    hdrp_failure_evidence = None
    if hdrp_failure_path.exists():
        failure = read(hdrp_failure_path)
        assert failure['status'] == 'hdrp_v4_refusals_and_original_materials_reopened'
        hdrp_failure_evidence = dict(receipt=str(hdrp_failure_path), receipt_sha256=sha(hdrp_failure_path),
                                    original_materials_unchanged=failure['original_materials_unchanged'],
                                    private_material_copies_changed=failure['private_material_copies_changed'],
                                    captured=0, failed=42, native_materials_verified=0)
    warm_path = W / 'delta-warm-hdrp-project-compatibility-v1/RESULT.v1.json'
    warm_evidence = None
    warm_note = ''
    if warm_path.is_file():
        warm = read(warm_path)
        assert warm['requested_materials'] == 13 and warm['project_count'] == 2
        assert not warm['assets_copied'] and not warm['native_context_verified'] and not warm['full_import']
        warm_evidence = dict(receipt=str(warm_path), receipt_sha256=sha(warm_path),
            checked_utc=warm['checked_utc'], status=warm['status'],
            checked_prepared_files=warm['checked_prepared_files'],
            indexed_warm_guids=warm['indexed_warm_guids'], conflict_count=len(warm['conflicts']),
            assets_copied=False, native_context_verified=False, full_import=False)
        warm_note = (f"L04.DELTA.WARM.PREFLIGHT : {warm['checked_prepared_files']} fichiers préparés "
            f"comparés aux chemins et {warm['indexed_warm_guids']} GUID du projet HDRP initialisé ; "
            f"{len(warm['conflicts'])} collision de chemin/GUID/contenu. Sources, copies et "
            "entrées relues par SHA avant/après lors du reçu. Réutilisation envisageable après "
            "diagnostic et correction du contexte natif ; aucune copie ni capture lancée. "
            "Les 13 matériaux restent à capturer, réouvrir et qualifier. "
            "Reçu : `delta-warm-hdrp-project-compatibility-v1/RESULT.v1.json`.")
    priority_path = W / 'hdrp-diagnostic-queue-priority-v1/RUN-RESULT.v1.json'
    priority_evidence = None
    priority_note = ''
    if priority_path.is_file():
        priority = read(priority_path)
        controlled_path = W / 'hdrp-diagnostic-queue-priority-v1/CONTROLLED-DEFER.v1.json'
        controlled = read(controlled_path)
        assert len(controlled['stopped_waiting_controllers']) == 5
        assert not controlled['native_unity_stopped'] and not controlled['sources_modified']
        assert controlled['all_requested_captures_retained']
        priority_evidence = dict(receipt=str(priority_path), receipt_sha256=sha(priority_path),
            status=priority['status'], controlled_defer_receipt=str(controlled_path),
            controlled_defer_sha256=sha(controlled_path), deferred_waiting_controllers=5,
            native_unity_stopped=False, sources_modified=False, captures_abandoned=False)
        priority_note = (f"L02.HDRP.QUEUE.PRIORITY : `{priority['status']}`. "
            "Ordre corrigé après les 42 refus Dungeon et 3 refus Medieval : observation Villa "
            "active, puis diagnostic HDRP, résolution du contexte, puis reprise des captures "
            "Delta/URP et vérifications différées. Cinq contrôleurs strictement en attente "
            "différés ; identités de processus contrôlées et états/entrées sauvegardés. "
            "Aucun Unity actif interrompu, aucune capture ni source abandonnée. "
            "Reçu : `hdrp-diagnostic-queue-priority-v1/CONTROLLED-DEFER.v1.json`.")
    hdrp_diagnostic_path = W / 'hdrp-native-context-diagnostic-v1/RUN-RESULT.v1.json'
    hdrp_diagnostic_evidence = None
    hdrp_resolution_note = None
    if hdrp_diagnostic_path.exists():
        diagnostic = read(hdrp_diagnostic_path)
        hdrp_diagnostic_evidence = dict(receipt=str(hdrp_diagnostic_path), receipt_sha256=sha(hdrp_diagnostic_path),
                                       status=diagnostic['status'], native_diagnostic_executed=diagnostic['native_diagnostic_executed'],
                                       native_materials_verified=0, full_import=False)
        native_path = hdrp_diagnostic_path.with_name('NATIVE-DIAGNOSTIC.v1.json')
        if diagnostic['status'] == 'native_hdrp_context_and_subshader_observations_retained':
            assert sha(native_path) == diagnostic['native_result_sha256']
            native = read(native_path)
            assert len(native['before']['materials']) == len(native['after']['materials']) == 42
            assert native['prepare_returned_true'] and not native['prepare_error']
            assert native['after']['pipeline_instance_type'] == 'UnityEngine.Rendering.HighDefinition.HDRenderPipeline'
            assert all(m['selected_tag'] == 'HDRenderPipeline' and not m['error'] and
                       not m['shader_has_errors'] for m in native['after']['materials'])
            hdrp_resolution_note = ("L02.HDRP.CONTEXT.RESOLUTION : pipeline sélectionné mais absent avant "
                "initialisation ; préparation native mesurée réussie, 42/42 tags HDRP et 462 noms de passes "
                "observés après préparation. Capture v5 refusée pour collision de noms entre helpers ; "
                "helper défectueux conservé hors compilation. V6 utilise un namespace distinct et le projet "
                "privé initialisé, compilation API réussie ; état de capture V6 dans le tableau. Aucune parité "
                "de pixels acquise. Après capture et réouverture, reprendre les 13 Delta, 3 Medieval, 6 URP "
                "et les vérifications différées. Reçus : hdrp-native-context-diagnostic-v1, "
                "unity-dungeon-native-prepared-pipeline-v5/HELPER-REPAIR.v1.json, "
                "unity-dungeon-native-prepared-pipeline-v6.")
    urp_evidence = None
    if urp_path.exists():
        urp = read(urp_path)
        assert urp['status'] == 'villa_urp_source_equivalence_reopened'
        urp_evidence = dict(receipt=str(urp_path), receipt_sha256=sha(urp_path),
                            expected_cards=9, source_equivalent_cards=urp['source_equivalent_cards'],
                            distinct_materials=len(urp['material_comparisons']),
                            geometry_derivative_reuse_verified=False, assets_delivered=0)
    no_png_evidence = None
    no_png_rows = ''
    if no_png_path.exists():
        no_png = read(no_png_path)
        assert no_png['status'] == '71_no_png_source_cards_pinned_into_six_batches'
        assert no_png['source_cards'] == 71 and sum(b['expected_cards'] for b in no_png['batches']) == 71
        for batch in no_png['batches']:
            assert sha(batch['receipt']) == batch['sha256']
        no_png_evidence = dict(receipt=str(no_png_path), receipt_sha256=sha(no_png_path),
                               source_cards=71, distinct_source_prefabs=no_png['distinct_source_prefabs'],
                               batches=no_png['batches'], images_generated=0, assets_delivered=0)
        no_png_rows = '\n'.join(f"| {b['id']} | {b['pack_id']} | {b['expected_cards']} fiches / {b['distinct_source_prefabs']} prefabs sources ; sources et .meta épinglés. Géométrie complète, matériaux, image et recette restent à produire. |" for b in no_png['batches'])
    kit_path = W / 'studio-candidate-input-preparation-v1/RESULT.v1.json'
    kit_evidence = None
    if kit_path.exists():
        kit = read(kit_path)
        assert kit['status'] == 'existing_governed_kit_candidate_inputs_reopened'
        kit_evidence = dict(receipt=str(kit_path), receipt_sha256=sha(kit_path),
                            kit_root=kit['kit_root'], assets_reopened=kit['assets_reopened'],
                            new_hivemind_assets_validated=0, candidate_built=False)
    candidate_evidence = None
    if candidate_path.exists():
        candidate_evidence = read(candidate_path)
        assert sha(candidate_evidence['script']) == candidate_evidence['script_sha256']
        candidate_evidence = dict(receipt=str(candidate_path), receipt_sha256=sha(candidate_path),
                                 status=candidate_evidence['status'], candidate_built=False,
                                 studio_verified=False, full_import=False)
    lineage_evidence = None
    if lineage_path.exists():
        lineage = read(lineage_path)
        assert lineage['status'] == 'archive_lineage_and_batch_scope_reconciled' and not lineage['refusals']
        lineage_evidence = dict(receipt=str(lineage_path), receipt_sha256=sha(lineage_path),
                                roots=24, nested_variants=24, tracked_packs=48, source_entries=98612,
                                archive_payloads_rehashed_now=False,
                                historical_hash_finished=lineage['historical_archive_hash_finished'])
    rows = '\n'.join(f"| {r['id']} | {r['expected']} cas attendus | `{r['status']}` |" for r in observed)
    body = f"""# Prochains sous-lots Hivemind

Actualisé : {now}. Les reçus font foi ; les étapes techniques ne comptent pas des assets prêts dans l'éditeur.

| Ordre / sous-lot | Périmètre | État / critère de fin |
|---|---|---|
| L01.SCENE.FRAME | Matrices, temps, ressources et attachement | 12/12 tests réussis. GPU et Studio à contrôler. |
| L01.SCENE.HOST | Chargement commun et refus atomiques | {host_note} |
| L04.VILLA.HIERARCHY | 76 racines sources / 226 IDs logiques | {hierarchy} |
| L04.VILLA.BATCHES | Suivi des onze sous-lots physiques | 11 reçus terminaux / 207 IDs réconciliés avec les listes exactes ; anciens états corrigés dans `SOUS_LOTS_VILLA.v1.json`, préimage conservée. Reçu : `villa-batch-tracking-reconciliation-v1/RESULT.v1.json`. Réconciliation de suivi uniquement, aucun nouveau contrôle de rendu ni de livraison. |
{rows}
| L02.HDRP.PASSES.V4.FAILURE | 42 refus de capture | 0/42 captures acceptées ; tag de pipeline du sous-shader vide pour les 42 matériaux. Sources : 42/42 matériaux originaux et .meta réouverts intacts ; 18 copies privées modifiées par Unity, différences conservées. Mesurer instance de pipeline, tag global et sous-shaders avant correction. Reçu : `hdrp-v4-failure-and-material-copy-analysis-v1/RESULT.v1.json`. Aucun asset qualifié par cet essai. |
| L02.HDRP.CONTEXT.DIAGNOSTIC | Pipeline et 42 identités de sous-shaders | `{hdrp_diagnostic_evidence['status'] if hdrp_diagnostic_evidence else 'non démarré'}`. Helper compilé contre Unity installé, sortie séparée ; mesure avant/après la préparation native du pipeline. Réutilise le projet privé et ses caches ; ordre effectif indiqué dans la section de priorité de la file. Reçu : `hdrp-native-context-diagnostic-v1/RUN-RESULT.v1.json`. Aucun shader remplacé, aucun verdict de parité de rendu. |
| L04.TEXTURE.RENDER | 46 textures / 544 mips dans 4 packs | Stockage réouvert ; décodage, orientation et rendu à comparer. |
| L03.TOWNSMITH.DRAW | 893 géométries / 8 prefabs | Géométries et poses vérifiées ; 6 bindings de dessin à observer, matériaux et rendu à terminer. |
| L01.STUDIO | Aperçu et placement communs | Raccordement réel puis rendu, édition, sauvegarde, réouverture et runtime. |
| L01.STUDIO.RUNTIME | Instance Paper Cluster sauvegardée | Édition/sauvegarde/réouverture contrôlées. Essai X=2/Z=0 inconclusif : départ réel X=16/Z=8, épinglé dans RESULT.v6.json. Refaire la recette au départ réel avant tout diagnostic de panne. |
| L01.STUDIO.WASM | Frontend complet actuel | V1 refusé : import Component ; v2 refusé : API de diagnostics GPU optionnels. Corrections appliquées ; v3 sous Rust 1.98, 965 fichiers épinglés et cache incrémental désactivé. État actuel : `{wasm_evidence['status'] if wasm_evidence else 'non disponible'}`. Reçu : `studio-current-source-wasm-check-v3/RUN-RESULT.v1.json`. Ce contrôle ne qualifie pas le rendu. |
| L01.STUDIO.FRONTEND | Construction du frontend réel et du worker de navigation | `{frontend_evidence['status'] if frontend_evidence else 'non disponible'}`. Attend la fin et le succès du contrôleur WASM exact ; mêmes sources vérifiées avant/après. Sorties isolées sur F dans `studio-actual-frontend-build-v2/dist`. V1 arrêté avant tout build pour corriger les noms de sorties attendus, preuve STOPPED conservée. Build frontend debug ; aucune recette UI ou GPU encore qualifiée. |
| L01.STUDIO.CANDIDATE | Candidat natif sur F | `{candidate_evidence['status'] if candidate_evidence else 'non disponible'}`. Script adapté à F:/OpenRPG_ImportWork et syntaxe PowerShell contrôlée ; intégration frontend par la variante Files de Tauri, avec préfixe de répertoire conservé. Reçu : `studio-candidate-F-output-support-v1/RESULT.v1.json`. Candidat à construire après le frontend ; aucune recette produit encore qualifiée. |
| L01.STUDIO.INPUTS | Kit existant nécessaire au candidat | {kit_evidence['assets_reopened'] if kit_evidence else 0}/8 GLB réouverts et hashes attendus vérifiés, documents kit concordants. Reçu : `studio-candidate-input-preparation-v1/RESULT.v1.json`. Aucun nouvel asset Hivemind validé ni nouveau modèle créé ; utiliser ce kit existant lors de la construction du candidat. |
| L01.STUDIO.CANDIDATE.RUN | Candidat natif réel | `{native_candidate_evidence['status'] if native_candidate_evidence else 'non démarré'}` ; construction séquentielle après le frontend exact, validation préalable et réouverture du binaire et du bundle. Sortie : `studio-native-library-candidate-v1`. Reçu : `studio-native-library-candidate-queue-v1/RUN-RESULT.v1.json`. Recherche, images, aperçu, placement et runtime restent à contrôler dans le produit. |
| L05.MEDIEVAL | Rig et 3 variantes de tissu | {skin_status} |
| L06.BLOOD | Un effet complet, puis les autres | Émission, trajectoires, durée, transparence et comparaison temporelle. |
| L06.DUST | Un effet complet, puis les autres | Même recette, avec preuve propre à chaque effet. |
| L07.SPRITES puis L07.TERRAIN | Formats spéciaux par pack | Dépendances, conversion et contrôles adaptés au type. |
| L08, après chaque livraison | Images, fiches, recherche et filtres | Usage réel dans l'éditeur ; 948 images historiques à traiter. |
{no_png_rows}
| L08.NO_PNG.05.SOURCES | Villa URP : comparaison aux preuves natives Villa | {urp_evidence['source_equivalent_cards'] if urp_evidence else 0}/9 entrées sources de pose/géométrie équivalentes ; prefabs, FBX et .meta réouverts et identiques. Six matériaux URP distincts conservés. Réouvrir les dérivés de géométrie avant réutilisation ; aucune réutilisation de matériau HDRP autorisée. Reçu : `villa-urp-no-png-source-equivalence-v1/RESULT.v1.json`. |
| L08.NO_PNG.05.GEOMETRY | Villa URP : dérivés correspondants | {urp_geometry_evidence['geometry_derivatives_reopened'] if urp_geometry_evidence else 0}/9 dérivés de géométrie et repères source réouverts ; hashes/BIN concordants. Khronos : 0 erreur / 0 avertissement. Reçu : `villa-urp-counterpart-geometry-reopen-v1/RESULT.v1.json`. Ces fichiers correspondent à HDRP : assembler séparément les six matériaux URP avant les images ; aucun nouvel asset livré. |
| L08.NO_PNG.05.MATERIALS | Villa URP : six matériaux et deux graphes actifs | {urp_material_evidence['materials'] if urp_material_evidence else 0}/6 matériaux sources liés ; {urp_material_evidence['unresolved_authored_texture_bindings'] if urp_material_evidence else 'inconnu'} référence de texture authored non résolue ; {urp_material_evidence['native_default_observations_required'] if urp_material_evidence else 'inconnu'} valeurs par défaut à observer nativement. Reçu : `villa-urp-six-material-source-bindings-v1/RESULT.v1.json`. Conversion des matériaux et rendu restent à qualifier. |
| L08.NO_PNG.05.CAPTURE.INPUTS | Villa URP : entrées natives | {urp_capture_evidence['requests'] if urp_capture_evidence else 0}/6 requêtes préparées ; {urp_capture_evidence['source_files'] if urp_capture_evidence else 0} fichiers sources et leurs .meta vérifiés ; {urp_capture_evidence['unresolved_graph_references'] if urp_capture_evidence else 'inconnu'} références de graphe sans source conservées explicitement. Préparer le contexte URP isolé et compiler le helper, puis mettre en file après les captures Unity existantes. Aucune capture exécutée. Reçu : `villa-urp-native-capture-inputs-v1/RESULT.v1.json`. |
| L08.NO_PNG.05.CAPTURE.API | Villa URP : helpers natifs | {urp_api_evidence['actual_exit_codes'] if urp_api_evidence else 'en attente'} : compilation réelle du bootstrap URP et du relevé des matériaux. Projet isolé et file séquentielle en préparation ; aucune capture exécutée. Reçu : `villa-urp-native-helper-api-v1/RESULT.v1.json`. |
| L08.NO_PNG.05.CAPTURE.RUN | Villa URP : six matériaux | `{urp_queue_evidence['status'] if urp_queue_evidence else 'non démarré'}` ; {urp_queue_evidence['native_materials_verified'] if urp_queue_evidence else 0}/6 observations natives vérifiées. Contexte URP séparé puis capture des sources ; anciennes tentatives conservées. Reçu courant : `{urp_queue_path}`. Rendu Forge restant. |
| L08.NO_PNG.05.CAPTURE.REOPEN | Villa URP : vérification indépendante | `{urp_reopen_evidence['status'] if urp_reopen_evidence else 'non démarré'}` ; {urp_reopen_evidence['native_materials_verified'] if urp_reopen_evidence else 0}/6 matériaux et {urp_reopen_evidence['native_defaults_verified'] if urp_reopen_evidence else 0}/10 valeurs par défaut. Quatre mutations du relevé réel refusées ; les contrôles de fixtures restent distincts des assets. Reçu courant : `{urp_reopen_evidence['receipt'] if urp_reopen_evidence else 'en attente'}`. Rendu et livraison restant. |
| L09, après chaque pack | IDs attendus / livrés / refusés | Réconciliation complète ; tous les packs restent couverts. |

Les captures Unity restent séquentielles. L'ordre courant et les captures différées sont indiqués dans la section de priorité de la file lorsqu'elle existe ; l'ancien ordre ne vaut plus après une réorganisation documentée. Les contrôles indépendants peuvent avancer pendant les captures. Les lots déjà vérifiés ne sont pas reconvertis sans motif documenté.

Optimisation des caches : état `{cache_evidence['status'] if cache_evidence else 'non disponible'}`, {cache_evidence['verified'] if cache_evidence else 0}/4 caches vérifiés. Le premier projet est devenu actif et Unity a créé son propre PackageCache : la copie préparatoire ne pouvait plus être publiée. Son worker seul a été arrêté, les fichiers intermédiaires sont conservés, les autres optimisations sont différées ; les captures Unity continuent. Preuve : `queued-unity-package-cache-seeding-v1/CONTROLLED-STOP.v1.json`. Aucun asset ni capture supprimé. Une préparation de cache n'est pas une validation d'asset.

Chaque livraison suit cinq étapes : conversion, vérification des fichiers, contrôle du comportement (matériaux/rig/effets), fiche et image, puis recette dans l'éditeur et le runtime. Son reçu contient les IDs exacts, attendu/vérifié, fichiers, hashes, refus et prochaine action.

Les 207 dérivés physiques Villa sont vérifiés ; les 19 scènes supplémentaires restent séparées. Ni ces compteurs ni les tests ci-dessus ne prouvent une livraison complète dans Studio.

Périmètre des archives réconcilié : 24 archives d'origine et 24 variantes imbriquées correspondent aux 48 packs suivis et aux 98 612 entrées de source déclarées. Reçu : `collection-archive-lineage-reconciliation-v1/RESULT.v1.json`. Les manifests et tailles ont été relus ; les octets des archives n'ont pas été rehachés dans ce contrôle. La preuve SHA historique date du 3 octobre. L'ancien dossier d'origine sur E: est absent ; ce contrôle ne recense pas de nouveaux achats éventuels.

Bibliothèque : `F:\\OpenRPG_AssetVault`.
Travail, caches et preuves : `F:\\OpenRPG_ImportWork`.
Découpage complet : `DECOUPAGE_OPERATIONNEL.md`. Packs : `INVENTAIRE_PAR_PACK.csv`. Historique : `JOURNAL.md`.
"""
    state_path = T / 'ETAT.v1.json'
    old_state = state_path.read_bytes()
    state = json.loads(old_state.decode('utf-8-sig'))
    state['updated_utc'] = now
    if census_evidence:
        state['embedded_effect_source_census_progress'] = census_evidence
    if embedded_batches_evidence:
        state['embedded_effect_execution_batches'] = embedded_batches_evidence
    if ui_evidence:
        state['actual_native_library_ui_partial_review'] = ui_evidence
    if delta_analysis_evidence:
        state['delta_material_refusal_and_exact_dependency_repair'] = delta_analysis_evidence
    if pirate_reopened_evidence:
        state['pirate_exact_source_subgraph_material_independent_reopening'] = pirate_reopened_evidence
    if medieval_reopened_evidence:
        state['medieval_warm_material_independent_reopening'] = medieval_reopened_evidence
    if delta_stages:
        state['delta_vector_vertex_fragment_stages_v1'] = dict(receipt=str(delta_stages_path), receipt_sha256=sha(delta_stages_path), **delta_stages)
    if delta_uv1:
        state['delta_vector_uv1_binding_reopening_v1'] = dict(receipt=str(delta_uv1_path), receipt_sha256=sha(delta_uv1_path), **delta_uv1)
    if position_frame:
        state['source_position_frame_native_tests_v1'] = dict(receipt=str(position_frame_path), receipt_sha256=sha(position_frame_path), **position_frame)
    if uv_binding:
        state['source_uv_binding_native_tests_v1'] = dict(receipt=str(uv_binding_path), receipt_sha256=sha(uv_binding_path), **uv_binding)
    if uv_requirement:
        state['source_uv_requirements_native_tests_v2'] = dict(receipt=str(uv_requirement_path), receipt_sha256=sha(uv_requirement_path), **uv_requirement)
    if uv_pipeline:
        state['source_uv_pipeline_function_native_tests_v1'] = dict(receipt=str(uv_pipeline_path), receipt_sha256=sha(uv_pipeline_path), **uv_pipeline)
    if delta_bevy_vertex:
        state['delta_bevy_vertex_programs_reopened_v2'] = dict(receipt=str(delta_bevy_vertex_path), receipt_sha256=sha(delta_bevy_vertex_path), **delta_bevy_vertex)
    if delta_surface:
        state['delta_native_surface_interfaces_v1'] = dict(receipt=str(delta_surface_path), receipt_sha256=sha(delta_surface_path), **delta_surface)
    if delta_lit:
        state['delta_native_lit_fragments_v1'] = dict(receipt=str(delta_lit_path), receipt_sha256=sha(delta_lit_path), **delta_lit)
    if frame_admission:
        state['studio_source_frame_admission_native_v2'] = dict(receipt=str(frame_admission_path), receipt_sha256=sha(frame_admission_path), **frame_admission)
    if frozen_rgb8:
        state['delta_rgb8_frozen_canonical_loader_v3'] = dict(receipt=str(frozen_rgb8_path), receipt_sha256=sha(frozen_rgb8_path), **frozen_rgb8)
    if canonical_rgb8:
        state['delta_rgb8_current_canonical_loader_v2'] = dict(receipt=str(canonical_rgb8_path), receipt_sha256=sha(canonical_rgb8_path), **canonical_rgb8)
    state['historical_rgb8_source_transitions'] = list(HISTORICAL_SOURCE_TRANSITIONS.values())
    if complete_prefab_source:
        state['delta_complete_prefab_source_reopen_v1'] = dict(receipt=str(complete_prefab_source_path), receipt_sha256=sha(complete_prefab_source_path), **complete_prefab_source)
    if firepit_behavior:
        state['delta_firepit_particle_behavior_v1'] = dict(receipt=str(firepit_behavior_path), receipt_sha256=sha(firepit_behavior_path), **firepit_behavior)
    if firepit_hdrp_repair:
        state['delta_firepit_hdrp_closure_repair_v2'] = dict(receipt=str(firepit_hdrp_repair_path), receipt_sha256=sha(firepit_hdrp_repair_path), historical_private_project_staging_only=True, **firepit_hdrp_repair)
    if rgb8_closure:
        state['delta_rgb8_inclusion_closure_queue_v1'] = dict(receipt=str(rgb8_closure_path), receipt_sha256=sha(rgb8_closure_path), **rgb8_closure)
    if remaining_delta:
        state['delta_remaining_native_materials_v2'] = dict(receipt=str(remaining_delta_path), receipt_sha256=sha(remaining_delta_path), **remaining_delta)
    if rocks:
        state['delta_rockstemperate_independent_reopen_v1'] = dict(receipt=str(rocks_path), receipt_sha256=sha(rocks_path),
            historical_receipt_preserved=True, later_unrelated_private_changes=rocks_later_private_changes, **rocks)
    if firepit:
        state['delta_firepit_closure_retry_v3'] = dict(receipt=str(firepit_path), receipt_sha256=sha(firepit_path), **firepit)
    if firepit_reopen:
        state['delta_firepit_serialization_reopen_v1'] = dict(receipt=str(firepit_reopen_path), receipt_sha256=sha(firepit_reopen_path), **firepit_reopen)
    for key,path,evidence in [('delta_rocks_firepit_texture_storage_v1',rocks_texture_path,rocks_texture),
            ('delta_rocks_firepit_effective_state_v1',rocks_effective_path,rocks_effective),
            ('delta_rocks_firepit_texture_gpu_v1',rocks_gpu_path,rocks_gpu)]:
        if evidence:
            state[key] = dict(receipt=str(path),receipt_sha256=sha(path),**evidence)
    if rocks_normal:
        state['delta_rocks_firepit_normal_reference_v1'] = dict(receipt=str(rocks_normal_path),receipt_sha256=sha(rocks_normal_path),**rocks_normal)
    for key,path,evidence in [('delta_rocks_firepit_normal_raw_gpu_parity_v1',rocks_raw_path,rocks_raw),
            ('delta_rocks_firepit_native_lit_v1',rocks_lit_path,rocks_lit),
            ('delta_rocks_firepit_normal_decoder_gpu_v1',rocks_decoder_path,rocks_decoder),
            ('delta_rocks_firepit_program_licenses_v1',rocks_license_path,rocks_license)]:
        if evidence:
            state[key] = dict(receipt=str(path),receipt_sha256=sha(path),**evidence)
    if foliage_texture:
        state['delta_foliage_texture_storage_job_v1'] = dict(receipt=str(foliage_texture_path), receipt_sha256=sha(foliage_texture_path), expected_textures=18, **foliage_texture)
    if foliage_material:
        state['delta_foliage_native_materials_v2'] = dict(receipt=str(foliage_material_path), receipt_sha256=sha(foliage_material_path), **foliage_material)
    if foliage_material_reopen:
        state['delta_foliage_native_materials_independent_reopen_v1'] = dict(receipt=str(foliage_material_reopen_path), receipt_sha256=sha(foliage_material_reopen_path), **foliage_material_reopen)
    if foliage_effective:
        state['delta_foliage_effective_properties_v1'] = dict(receipt=str(foliage_effective_path), receipt_sha256=sha(foliage_effective_path), **foliage_effective)
    if texture_independent:
        state['delta_foliage_texture_independent_reopen_v1'] = dict(receipt=str(texture_independent_path), receipt_sha256=sha(texture_independent_path), **texture_independent)
    if foliage_gpu:
        state['delta_foliage_texture_gpu_v2'] = dict(receipt=str(foliage_gpu_path), receipt_sha256=sha(foliage_gpu_path), **foliage_gpu)
    if foliage_normal:
        state['delta_foliage_normal_reference_v1'] = dict(receipt=str(foliage_normal_path), receipt_sha256=sha(foliage_normal_path), **foliage_normal)
    if foliage_raw_parity:
        state['delta_foliage_normal_raw_gpu_parity_v1'] = dict(receipt=str(foliage_raw_parity_path), receipt_sha256=sha(foliage_raw_parity_path), **foliage_raw_parity)
    if foliage_rgorag:
        state['delta_native_lit_rgorag_v2'] = dict(receipt=str(foliage_rgorag_path), receipt_sha256=sha(foliage_rgorag_path), **foliage_rgorag)
    if foliage_decoder:
        state['delta_foliage_normal_decoder_gpu_v1'] = dict(receipt=str(foliage_decoder_path), receipt_sha256=sha(foliage_decoder_path), **foliage_decoder)
    if foliage_rgb_reference:
        state['delta_foliage_rgb24_reference_v1'] = dict(receipt=str(foliage_rgb_reference_path), receipt_sha256=sha(foliage_rgb_reference_path), **foliage_rgb_reference)
    if foliage_rgb_parity:
        state['delta_foliage_rgb24_expansion_gpu_parity_v1'] = dict(receipt=str(foliage_rgb_parity_path), receipt_sha256=sha(foliage_rgb_parity_path), **foliage_rgb_parity)
    if rgb8_transport:
        state['delta_rgb8_native_transport_candidate_v3'] = dict(receipt=str(rgb8_transport_path), receipt_sha256=sha(rgb8_transport_path), **rgb8_transport)
    if rgb8_regression:
        state['delta_rgb8_native_regressions_v1'] = dict(receipt=str(rgb8_regression_path), receipt_sha256=sha(rgb8_regression_path), **rgb8_regression)
    if delta_defaults:
        state['delta_default_bindings_reopened_v2'] = dict(receipt=str(delta_defaults_path), receipt_sha256=sha(delta_defaults_path), **delta_defaults)
    if delta_vectors:
        state['delta_vector_kernels_reopened_v3'] = dict(**delta_vectors, independent_receipt=str(delta_vectors_path), independent_receipt_sha256=sha(delta_vectors_path))
    if checkpoint_evidence:
        state['library_checkpointed_audit_independent_reopening'] = checkpoint_evidence
    if urp_v2:
        state['villa_urp_v2_current_job'] = dict(receipt=str(urp_v2_path), receipt_sha256=sha(urp_v2_path), **urp_v2)
    if audit_progress:
        state['library_integrity_audit_progress'] = audit_progress
    if migration_evidence:
        state['hdrp_material_migration_field_analysis'] = migration_evidence
    if warm_evidence:
        state['delta_warm_hdrp_project_compatibility'] = warm_evidence
    if priority_evidence:
        state['hdrp_diagnostic_queue_priority'] = priority_evidence
    if dust_evidence:
        state['dust_exact_resource_identity_reopen'] = dust_evidence
    if effects_evidence:
        state['effect_execution_batches'] = effects_evidence
    if medieval_refusal_evidence:
        state['medieval_material_native_refusal_analysis'] = medieval_refusal_evidence
    state['sequential_batch_followup_oct07'] = dict(updated_utc=now, jobs=observed,
        frame_tests=12, host_tests=14, host_receipt=str(host_path), host_receipt_sha256=sha(host_path),
        host_input_pins_reopened=not host_changes, host_inputs_changed_since_tests=host_changes,
        textures_receipt=str(textures), hierarchy_status=hierarchy,
        gpu_rendered=False, studio_verified=False, full_import=False)
    if skin_evidence:
        state['sequential_batch_followup_oct07']['medieval_skin_glb'] = skin_evidence
    if reopen_evidence:
        state['sequential_batch_followup_oct07']['independent_material_reopening_queue'] = reopen_evidence
    if cache_evidence:
        state['sequential_batch_followup_oct07']['queued_official_package_cache_seeding'] = cache_evidence
    if wasm_evidence:
        state['sequential_batch_followup_oct07']['current_studio_wasm_check'] = wasm_evidence
    if lineage_evidence:
        state['sequential_batch_followup_oct07']['archive_lineage_reconciliation'] = lineage_evidence
    if frontend_evidence:
        state['sequential_batch_followup_oct07']['actual_studio_frontend_build'] = frontend_evidence
    if candidate_evidence:
        state['sequential_batch_followup_oct07']['studio_candidate_F_output_support'] = candidate_evidence
    if kit_evidence:
        state['sequential_batch_followup_oct07']['studio_candidate_existing_kit_inputs'] = kit_evidence
    if no_png_evidence:
        state['sequential_batch_followup_oct07']['no_png_source_batches'] = no_png_evidence
    if urp_evidence:
        state['sequential_batch_followup_oct07']['villa_urp_no_png_source_equivalence'] = urp_evidence
    if urp_geometry_evidence:
        state['sequential_batch_followup_oct07']['villa_urp_counterpart_geometry_reopen'] = urp_geometry_evidence
    if urp_material_evidence:
        state['sequential_batch_followup_oct07']['villa_urp_material_source_bindings'] = urp_material_evidence
    if urp_capture_evidence:
        state['sequential_batch_followup_oct07']['villa_urp_native_capture_inputs'] = urp_capture_evidence
    if urp_api_evidence:
        state['sequential_batch_followup_oct07']['villa_urp_native_helper_api'] = urp_api_evidence
    if urp_queue_evidence:
        state['sequential_batch_followup_oct07']['villa_urp_native_capture_queue'] = urp_queue_evidence
    if urp_reopen_evidence:
        state['sequential_batch_followup_oct07']['villa_urp_independent_reopen_queue'] = urp_reopen_evidence
    if native_candidate_evidence:
        state['sequential_batch_followup_oct07']['actual_native_studio_candidate_queue'] = native_candidate_evidence
    if villa_tracking_evidence:
        state['sequential_batch_followup_oct07']['villa_batch_tracking_reconciliation'] = villa_tracking_evidence
    if hdrp_failure_evidence:
        state['sequential_batch_followup_oct07']['hdrp_v4_failure_analysis'] = hdrp_failure_evidence
    if hdrp_diagnostic_evidence:
        state['sequential_batch_followup_oct07']['hdrp_native_context_diagnostic_queue'] = hdrp_diagnostic_evidence
    replace_checked(state_path, old_state, json.dumps(state, indent=2, ensure_ascii=False).encode('utf-8'))
    target = T / 'PROCHAINS_SOUS_LOTS.md'
    if census_note:
        body += f"\n## Effets intégrés aux prefabs et scènes\n\n{census_note}\n"
    if embedded_batches_note:
        body += f"\n## Nouveaux sous-lots d’effets\n\n{embedded_batches_note}\n"
    if hdrp_reopened_note:
        body += f"\n## Captures Dungeon vérifiées\n\n{hdrp_reopened_note}\n"
    if delta_analysis_note:
        body += f"\n## Delta — refus et réparation des dépendances exactes\n\n{delta_analysis_note}\n"
    if medieval_reopened_evidence:
        body += ("\n## Medieval — reprise vérifiée\n\nL04.MEDIEVAL.MATERIAL.V2 : 3/3 captures et 3/3 "
            "réouvertures indépendantes réussies dans le contexte HDRP initialisé. Matériaux, shaders, "
            "textures attendues et passes contrôlés ; 26 originaux réouverts intacts. Copie privée "
            "vide laissée par l'interruption conservée dans medieval-empty-copy-repair-v1, puis "
            "octets sources exacts restaurés. Rendu et usage Forge restent à qualifier. Reçu : "
            "unity-medieval-warm-native-hdrp-v2/INDEPENDENT-REOPEN.v1.json.\n")
    if ui_note:
        body += f"\n## Recette partielle de la bibliothèque native\n\n{ui_note}\n"
    if delta_defaults and delta_vectors:
        body += ("\n## L04 Delta — nouveaux sous-lots vérifiés\n\n13/13 bindings natifs réouverts indépendamment, 1 contrôle positif et 11 incohérences refusées. Reçu : `delta-native-defaults-independent-reopen-v2/RESULT.v2.json`. Six kernels refusés pour Normalize/Transform/UV1 convertis puis validés Naga et réouverts : `delta-vector-node-converter-v3/INDEPENDENT-REOPEN.v1.json`. Les six succès antérieurs sont conservés ; 13 kernels liés aux textures restent à convertir. Entrées hôte UV1, matrice world-to-object et origine relative caméra obligatoires ; aucune parité GPU ni livraison globale acquise.\n")
    if delta_stages and delta_uv1:
        body += ("\n## L04 Delta — stages et géométries UV1\n\n6/6 stages vertex/fragment construits avec le helper existant et validés Naga. UV1.y réouvert sur 52 nœuds de rendu correspondant à 27 géométries ; GUID/fileID, octets natifs et dérivés concordants. Seule la composante Y est lue dans ces DAGs ; le padding shader des autres composantes reste non qualifié. Reçus : `delta-vector-native-stages-v1/RESULT.v1.json` et `delta-vector-uv1-binding-reopen-v1/RESULT.v1.json`. Raccordement au pipeline et rendu encore requis.\n")
    if position_frame:
        body += f"\n## L01 — entrées des transformations de position\n\nModule réel `source_graph_position_frame.rs` : matrice monde→objet et origine relative caméra dérivées des repères explicites et de la transformation courante. État des 4 tests natifs : `{position_frame['status']}` ; {position_frame.get('tests_passed',0)}/4. L'échec v1 sur rotation/échelle (ligne affine calculée par inversion générale) reste conservé avec sa source exacte ; v2 inverse la représentation affine après le contrôle strict des entrées. Aucun relâchement pour matrices projectives ou invalides. Source et harness épinglés, caches/preuves sur F. Reçu : `{position_frame_path}`. Shader hôte et rendu non qualifiés.\n"
    if uv_binding:
        body += f"\n## L04 — raccordement des UV source\n\nSélection explicite des canaux 1 à 7 avec attributs Vec4 source distincts et emplacements sans collision. Test du module réel : `{uv_binding['status']}` ; {uv_binding.get('tests_passed',0)}/3. Les refus v1/v2 du suivi PowerShell d'un processus terminé restent conservés ; v3 termine ce contrôle avec un code de succès explicite. Tests du refus des substituts UV standard/autres canaux et des emplacements. Reçu : `{uv_binding_path}`. Pipeline complet et comportement GPU encore requis.\n"
    if uv_requirement and uv_pipeline:
        body += f"\n## L04 — déclaration UV et layout moteur\n\nIntake : {uv_requirement.get('tests_passed',0)}/10 tests, six vrais shaders épinglés et régression de la ligne ambiguë UV0/UV1 contre les anciennes et nouvelles sources compilées. Exigences inscrites dans la clé du pipeline ; forward/prepass doivent déclarer les mêmes canaux. Fonction réelle de layout et tests moteur compilés : {uv_pipeline.get('tests_passed',0)}/13. UV1 de lightmap et UV2 source ne remplacent pas un UV1 source manquant. La définition standard VERTEX_UVS_B est retirée du chemin source. Reçus : `{uv_requirement_path}` et `{uv_pipeline_path}`. Cette compilation ciblée ne compile pas tout le moteur et ne qualifie pas le GPU, les fragments ni la livraison.\n"
    if delta_bevy_vertex:
        body += "\n## L04 — stages vertex Bevy du feuillage\n\n6/6 programmes composés avec les 13 modules Bevy réels : 24 succès (forward, profondeur, normales, orthographique) réouverts, 5 variantes non prises en charge refusées pour leurs raisons spécifiques. UV1 source à l'emplacement 12 ; matrice monde→objet dérivée de la transformation d'instance. La tentative v1 (division matricielle interdite en WGSL) et ses six refus sont conservés ; v2 utilise la multiplication par l'inverse du scalaire. Reçu : `delta-vector-bevy-vertex-stages-v2/INDEPENDENT-REOPEN.v1.json`. Fragment, admission des transformations, installation du pipeline, textures et rendu restent à vérifier.\n"
    if foliage_texture:
        body += f"\n## L04 — textures natives du feuillage Delta\n\n18 textures distinctes pour les six shaders. Sources et importeurs SHA vérifiés ; helper natif compilé. État : `{foliage_texture['status']}` ; {foliage_texture.get('textures_verified',0)}/18. V2 réutilise les copies et le helper, attend le contrôleur précédent puis URP, et corrige le défaut PowerShell de fin normale confirmé par un test de sortie. Un éventuel succès v1 est conservé sans recapture ; un export partiel impose sa réconciliation. Aucun job Unity vivant n'est arrêté. Projet privé Linear réutilisé, originaux conservés. Reçu : `{foliage_texture_path}`. Échantillonnage GPU, normales, rendu et usage Studio restent à vérifier.\n"
    if foliage_material_reopen:
        body += f"\n## L04 — six instances natives de feuillage\n\n6/6 matériaux et 18 références de textures réouverts indépendamment ; quatre mutations du relevé réel refusées. Reçu : `{foliage_material_reopen_path}`. Les sources restent intactes. La comparaison distingue valeurs sauvegardées et valeurs effectives : aucune différence sauvegardée ; 18 écarts de stencil effectif, keywords ajoutés et désactivation native de MotionVectors conservés dans `{foliage_effective_path}`. Ces observations ne qualifient ni la version Unity d'origine ni la parité du rendu.\n"
    if foliage_gpu:
        body += f"\n## L04 — échantillonnage GPU des textures de feuillage\n\nÉtat : `{foliage_gpu['status']}` ; {foliage_gpu.get('gpu_textures_verified',0)}/18 textures. Trois RGB24 converties en RGBA8 avec RGB exact sur tous les mips et alpha 255 identique à l'oracle natif CPU au niveau de base ; originaux conservés. Compilation du probe avec les vrais modules moteur actuels, cache indépendant sur F. Refus v1 de pin historique conservé : le moteur a ajouté BC4 depuis le probe précédent. Reçu courant : `{foliage_gpu_path}`. Échantillonnage Unity comparatif, décodage des normales et rendu complet restent distincts.\n"
        if foliage_gpu_path.parent.name.endswith('-v3'):
            body += "\nReprise v3 sans recompilation : seul le nom du paquet racine du lock Cargo a changé. Toutes les versions, sources et checksums de dépendances sont identiques ; binaire compilé et trois dérivés RGB24 réutilisés. Refus v2 et réconciliation conservés dans `delta-foliage-texture-gpu-v3/LOCK-RECONCILIATION.v1.json`.\n"
    if foliage_normal:
        body += f"\n## L04 — références natives des normales de feuillage\n\nÉtat : `{foliage_normal['status']}` ; {foliage_normal.get('native_normal_textures_verified',0)}/6 textures de normale exactes, sélectionnées par les lectures actives du graphe. Images sources déjà importées et projet Linear réutilisés ; mesure des RGBA, du décodeur Unity et de la formule RG-or-AG sur une grille identifiée pour chaque mip. Le contrôleur attend la fin du probe GPU exact. Reçu : `{foliage_normal_path}`. Comparaison avec Forge, rendu complet et usage éditeur restent requis.\n"
    if foliage_raw_parity:
        body += f"\n## L04 — comparaison RGBA des six normales\n\n6/6 textures, 75 mips et 307 200 coordonnées mesurées concordent entre Unity D3D11 et Forge DX12 à tolérance RGBA fixe 1e-6. L'unique mapping commun mesuré est identity ; aucun retournement supposé. Reçu : `{foliage_raw_parity_path}`. Cette preuve porte sur la grille et les LOD mesurés, pas tous les pixels ni le rendu du matériau.\n"
    if foliage_rgorag:
        body += f"\n## L04 — six programmes avec mode RG-or-AG explicite\n\nSix programmes complets avant/prépass et leur changement de helper réouverts : 46 compositions réelles et six refus spécifiques vérifiés. Les modes RGB existants restent identiques ; le mode 2 lit RG-or-AG et conserve alpha. Il doit être admis explicitement par l'hôte sur des images qualifiées. Reçu : `{foliage_rgorag_path}`. Installation moteur et rendu restent requis.\n"
    if foliage_decoder:
        body += f"\n## L04 — GPU du décodeur réel des six programmes\n\nÉtat : `{foliage_decoder['status']}` ; {foliage_decoder.get('native_normal_textures_verified',0)}/6. Le shader de mesure contient exactement le helper commun aux douze stages avant/prépass candidats. Comparaison aux références Unity : XY/alpha 1e-6, tolérance Z de conditionnement sqrt 5e-4 conservée. Reçu : `{foliage_decoder_path}`. Une réussite ne prouve pas le rendu final ni l'usage dans l'éditeur.\n"
    if foliage_rgb_parity:
        body += f"\n## L04 — trois RGB24, expansion et transport\n\n3/3 textures, 37 mips et 151 552 coordonnées concordent entre RGB24 Unity et RGBA8 Forge à tolérance fixe 1e-6 ; mapping identity mesuré. RGB est conservé sur tous les octets/mips, alpha ajouté vaut 255 et concorde avec les captures natives. Reçu : `{foliage_rgb_parity_path}`. Le contrat courant ImportedTextureStorage ne représente pas R8G8B8_UNorm : ne pas rebaptiser le capture JSON ni l'attribuer au format RGBA8. Prochaine adaptation : conserver RGB8 natif dans le transport, vérifier ses tailles/hashes/capture, puis convertir explicitement vers Image RGBA8 lors de la préparation moteur. Les 17 tests hôte compilent sur leurs sources épinglées actuelles ; préserver cette exécution avant mutation. Assemblage GLB, rendu et éditeur restent requis.\n"
    if delta_surface:
        body += "\n## L04 — interfaces natives de surface Delta\n\n6/6 interfaces réouvertes indépendamment, 24 compositions réelles réussies. UV0 et UV1 source transmis depuis le vertex déplacé vers les stages avant/profondeur/normales ; fermeture des dix sorties de surface et identité des instructions sources vérifiées. Les calculs du vent restent dans le vertex. Reçu : `delta-native-surface-interfaces-v1/INDEPENDENT-REOPEN.v1.json`. La sortie couleur de validation ne doit jamais être installée comme matériau. Adaptateur d’éclairage, transparence, admission, GPU et usage Forge restent à terminer.\n"
    if delta_lit:
        body += "\n## L04 — éclairage et prépasses Delta\n\n6/6 programmes natifs avant et prépass assemblés, 46 compositions réelles et 6 refus précis contrôlés indépendamment. Les six contrats sont relus sur matériaux et objets actifs du ShaderGraph : quatre découpes alpha/couverture, deux opaques, constantes de faces arrière propres à chacun. Les fragments utilisent les normales, rugosité, AO et émission sources dans le transport Bevy ; prépass de profondeur, normales, ombre et couverture présents. Reçus : `delta-foliage-render-contracts-v1/RESULT.v1.json` et `delta-native-lit-fragments-v1/INDEPENDENT-REOPEN.v1.json`. L'exposition caméra est une politique native ; parité Unity, captures des instances, textures, admission du pipeline, installation moteur, GPU et usage Forge restent à qualifier.\n"
    if frame_admission:
        body += f"\n## L01/L04 — admission des repères dans l'hôte réel\n\nMatrices exactement affines et mondes relatif/absolu de même base exigés. Avant la mise à jour des matériaux, chaque instance de caméra et d'ombre doit avoir une transformation globale composée, inversible et finie ; un refus conserve les anciens paramètres et masque l'hôte par son chemin existant. Trois nouveaux tests : refus projectif/base différente, refus d'instance singulière sans mutation, conservation rotation/échelle/origine caméra. État des 17 tests natifs du véritable hôte avec dépendance moteur : `{frame_admission['status']}` ; {frame_admission.get('tests_passed',0)}/17. Compilation préparée derrière le build natif déjà actif, cache F réutilisé ; aucune qualification GPU acquise. Reçu : `{frame_admission_path}`.\n"
    body += f"\n## Audit du Vault — observation courante\n\n{audit_note}\n"
    if migration_note:
        body += f"\n## Réparation des copies de matériaux Dungeon\n\n{migration_note}\n"
    if medieval_refusal_note:
        body += f"\n## Refus natif des trois matériaux Medieval\n\n{medieval_refusal_note}\n"
    if warm_note:
        body += f"\n## Delta — réutilisation du projet HDRP initialisé\n\n{warm_note}\n"
    if priority_note:
        body += f"\n## Priorité de la file Unity\n\n{priority_note}\n"
    if hdrp_resolution_note:
        body += f"\n## Résolution du contexte HDRP et reprise\n\n{hdrp_resolution_note}\n"
    if dust_note:
        body += f"\n## Dust — identités des ressources\n\n{dust_note}\n"
    if effects_note:
        body += f"\n## Sous-lots des effets\n\n{effects_note}\n"
    replace_checked(target, target.read_bytes(), body.encode('utf-8'))
    update = f"\n\n## Point courant des sous-lots — {now}\n\nL01 : FRAME 12/12 et HOST : {host_note} Textures : 46/46 et 544 mips réconciliés pour le stockage. Villa : {hierarchy} Medieval : {skin_status} États actuels des captures et prochaines actions dans PROCHAINS_SOUS_LOTS.md. GPU, Studio et livraison globale non qualifiés.\n\nL08 : {audit_note}\n"
    if migration_note:
        update += f"\n{migration_note}\n"
    if medieval_refusal_note:
        update += f"\n{medieval_refusal_note}\n"
    if warm_note:
        update += f"\n{warm_note}\n"
    if priority_note:
        update += f"\n{priority_note}\n"
    if hdrp_resolution_note:
        update += f"\n{hdrp_resolution_note}\n"
    if dust_note:
        update += f"\n{dust_note}\n"
    if effects_note:
        update += f"\n{effects_note}\n"
    if ui_note:
        update += f"\n{ui_note}\n"
    if census_note:
        update += f"\n{census_note}\n"
    if embedded_batches_note:
        update += f"\n{embedded_batches_note}\n"
    if hdrp_reopened_note:
        update += f"\n{hdrp_reopened_note}\n"
    if delta_analysis_note:
        update += f"\n{delta_analysis_note}\n"
    for name in ['TABLEAU_DE_BORD_LOTS.md', 'PLAN_ET_SUIVI.md', 'JOURNAL.md']:
        path = T / name
        old = path.read_bytes()
        replace_checked(path, old, old + update.encode('utf-8'))
    pirate_status = read(pirate_path)['status'] if pirate_path.is_file() else 'non démarré'
    digest = f"""# Avancement Hivemind par lots

Mis à jour : {now}. Périmètre : **48 packs et variantes**.

Bibliothèque : `F:\\OpenRPG_AssetVault`. Travail, caches et preuves : `F:\\OpenRPG_ImportWork`.

## Point actuel

| Sous-lot | Vérifié / attendu | Résultat et prochaine étape |
|---|---:|---|
| L02 — Dungeon, captures de matériaux | 42 / 42 | Captures et réouverture indépendante réussies ; rendu fidèle et livraison restent requis. |
| L04 — Delta, première capture | 1 / 13 | Un cas réussi réouvert ; 12 refus dus au sous-graphe absent du projet privé. Refus conservés. |
| L04 — Pirate Island, reprise des refus | {'12 / 12' if pirate_reopened_evidence else '12 cas à traiter'} | {'Captures et réouverture indépendante réussies ; rendu dans Forge restant' if pirate_reopened_evidence else pirate_status}. Sous-graphe source exact ajouté ; le cas déjà réussi n’est pas refait. |
| L04 — Delta, bindings de textures manquantes | {delta_defaults['native_materials_reopened'] if delta_defaults else 0} / 13 | Réouverture native indépendante ; aucun remplacement des références manquantes, pixels et rendu restant. |
| L04 — Delta, reprise des opérations vectorielles | {delta_vectors['converted_shader_kernels_reopened'] if delta_vectors else 0} / 6 | Shaders WGSL convertis, Naga PASS, réouverts ; UV1 et matrices explicites à raccorder au moteur avant qualification du rendu. |
| L04 — Delta, stages vertex/fragment | {delta_stages['vertex_and_fragment_stage_probes_validated'] if delta_stages else 0} / 6 | Stages de validation Naga réussis ; assemblage et installation dans le renderer restant. |
| L04 — Delta, stages vertex Bevy | {delta_bevy_vertex['native_vertex_programs_reopened'] if delta_bevy_vertex else 0} / 6 | 24 compositions réelles et 5 refus vérifiés ; UV1 et repères d’instance raccordés dans le shader. Fragment, admission, pipeline et rendu restant. |
| L04 — Delta, interfaces natives de surface | {delta_surface['native_surface_interfaces_reopened'] if delta_surface else 0} / 6 | 24 compositions et instructions sources réouvertes ; UV0/UV1 transmis aux surfaces déplacées. Éclairage final, installation et GPU restant. |
| L04 — Delta, éclairage et prépasses | {delta_lit['native_lit_programs_reopened'] if delta_lit else 0} / 6 | 46 compositions et 6 refus réouverts ; 4 matériaux alpha/couverture et 2 opaques. Textures, admission, installation moteur et rendu GPU restant. |
| L04 — Delta, données UV1 du feuillage | {delta_uv1['renderer_nodes_checked'] if delta_uv1 else 0} nœuds / {delta_uv1['unique_geometry_streams_checked'] if delta_uv1 else 0} géométries | UV1.y source réouvert, identité et octets natifs concordants ; transmission au shader et rendu restant. |
| L04 — raccordement des UV source | {uv_binding.get('tests_passed',0) if uv_binding else 0} / 3 tests | État : `{uv_binding['status'] if uv_binding else 'non démarré'}` ; sélection des attributs source exacts. Pipeline et rendu restant. |
| L04 — intake des déclarations UV | {uv_requirement.get('tests_passed',0) if uv_requirement else 0} / 10 tests | Déclarations réelles des six shaders et régression UV0/UV1 vérifiées ; ambiguïtés et mauvais formats refusés. |
| L04 — fonction de layout du pipeline | {uv_pipeline.get('tests_passed',0) if uv_pipeline else 0} / 13 tests | Fonction réelle et tests moteur compilés ; UV1 source exigé, substituts refusés. Moteur complet, fragment et GPU restant. |
| L01 — matrices de position source | {position_frame.get('tests_passed',0) if position_frame else 0} / 4 tests | État : `{position_frame['status'] if position_frame else 'non démarré'}`. Module réel de translation, rotation, échelle et origine caméra ; qualification GPU distincte. |
| L01/L04 — admission réelle des repères | {frame_admission.get('tests_passed',0) if frame_admission else 0} / 17 tests | État : `{frame_admission['status'] if frame_admission else 'non démarré'}`. Contrôles atomiques ajoutés ; validation native en file, GPU restant. |
| L04 — Medieval, matériaux de tissu | {'3 / 3' if medieval_reopened_evidence else '3 cas à traiter'} | {'Captures et réouverture indépendante réussies, avec les textures attendues ; rendu dans Forge restant.' if medieval_reopened_evidence else 'Voir le reçu courant du sous-lot.'} |
| L06 — recensement des effets directs | 48 / 48 packs | 8 830 fichiers examinés, 174 sources d’effets directs ; 6 scènes binaires refusées à inspecter nativement. |
| L06 — nouveaux sous-lots d’effets | 89 cas / 29 sous-lots | Liste exacte, sources, métadonnées, licences et SHA conservés. Conversion et comportement temporel restent à faire. |
| L08 — recette réelle Studio | 1 instance, 2 fiches | Recherche/filtres et fiches observés ; aperçu texturé, placement, retouche, sauvegarde et réouverture contrôlés pour Paper Cluster. |
| L01/L08 — vue joueur | 0 / 1 | Essai précédent inconclusif : objet X=2/Z=0, départ joueur X=16/Z=8. Retest au départ réel requis ; aucun défaut de chargement démontré. |
| L00 — ancien audit interrompu | {audit_progress['details_checked'] if audit_progress else '?'} / 26 735 lectures historiques | {'Processus exact vivant, audit non terminal.' if audit_progress and audit_progress['exact_executable_confirmed_live'] else 'Ancien processus absent : résultat incomplet, reprise avec preuves persistantes requise.'} Aucun résultat global ni rendu qualifié. |
| L00 — reprise avec preuves durables | {checkpoint_evidence['details_checked'] if checkpoint_evidence else 0} / 26 735 | Fiches et octets des images réouverts indépendamment ; audit global en cours. |
| L04 — Villa URP | {urp_v2.get('native_materials_verified', 0) if urp_v2 else 0} / 6 | {'6 matériaux et 10 valeurs par défaut réouverts indépendamment ; quatre incohérences réelles refusées. Rendu Forge restant.' if current_urp_reopen.is_file() else 'Capture native en cours ; réouverture indépendante restant.'} |
| L04 — textures du feuillage Delta | {foliage_texture.get('textures_verified',0) if foliage_texture else 0} / 18 | État : `{foliage_texture['status'] if foliage_texture else 'non démarré'}` ; {foliage_texture.get('mips_verified',0) if foliage_texture else 0} mips. {'Réouverture séparée des identités, octets, importeurs et sources réussie.' if texture_independent else 'Réouverture séparée restant.'} Décodage des normales et GPU restant. |
| L04 — instances natives du feuillage | {foliage_material.get('native_materials_verified',0) if foliage_material else 0} / 6 | {'Six matériaux et 18 bindings réouverts indépendamment ; quatre mutations refusées.' if foliage_material_reopen else 'Capture native en cours.'} {str(foliage_effective['effective_property_differences']) + ' écarts de valeurs effectives, keywords ajoutés et MotionVectors désactivée conservés ; valeurs sauvegardées inchangées.' if foliage_effective else 'Comparaison des valeurs effectives restant.'} Parité du rendu, installation et éditeur restant. |
| L04 — textures du feuillage sur GPU | {foliage_gpu.get('gpu_textures_verified',0) if foliage_gpu else 0} / 18 | État : `{foliage_gpu['status'] if foliage_gpu else 'non démarré'}` ; trois dérivés RGB24 → RGBA8 vérifiés contre les pixels natifs CPU. Comparaison Unity, décodage normal et rendu restant. |
| L04 — normales du feuillage, référence Unity | {foliage_normal.get('native_normal_textures_verified',0) if foliage_normal else 0} / 6 | État : `{foliage_normal['status'] if foliage_normal else 'non démarré'}` ; grille et mips identifiés. Comparaison avec le décodeur Forge et rendu restant. |
| L04 — comparaison RGBA des normales | {'6 / 6, 75 mips' if foliage_raw_parity else '0 / 6'} | {'307 200 positions concordantes à 1e-6 ; mapping identity mesuré.' if foliage_raw_parity else 'À faire.'} Décodage et rendu restent distincts. |
| L04 — programmes RG-or-AG | {'6 / 6' if foliage_rgorag else '0 / 6'} | 46 compositions et six refus réouverts pour le candidat à mode 2 explicite ; modes existants conservés. Admission et installation restant. |
| L04 — décodeur de normales sur GPU | {foliage_decoder.get('native_normal_textures_verified',0) if foliage_decoder else 0} / 6 | État : `{foliage_decoder['status'] if foliage_decoder else 'non démarré'}` ; helper identique aux six programmes. Raccordement hôte, rendu et éditeur restant. |
| L04 — RGB24 originales et expansion GPU | {'3 / 3, 37 mips' if foliage_rgb_parity else '0 / 3'} | {'151 552 positions concordantes à 1e-6 ; RGB exact et alpha natif préservés.' if foliage_rgb_parity else 'En cours.'} Ajouter le transport RGB8 natif puis l’expansion RGBA8 au chargement avant assemblage GLB. |
| L04 — transport natif RGB8 | {rgb8_transport.get('native_rgb24_textures_verified',0) if rgb8_transport else 0} / 3, 37 mips | État : `{rgb8_transport['status'] if rgb8_transport else 'non démarré'}`. Candidat sur F ; octets et capture RGB24 d’origine conservés, expansion dans le moteur, corruptions refusées. Installation, assemblage GLB et rendu restant. |
| L04 — régressions du transport RGB8 | {rgb8_regression.get('tests_passed',0) if rgb8_regression else 0} / 15 tests | Formats BC, capture native, mips et samplers existants vérifiés avec le candidat. Compilation et raccordement du moteur complet restent distincts. |
| L01/L04 — chargeur RGB8 et hôte figés sur F | {frozen_rgb8.get('actual_host_tests_passed',0) if frozen_rgb8 else 0} / 17 tests | État : `{frozen_rgb8['status'] if frozen_rgb8 else 'non démarré'}`. Vraies sources et fork rerecast copiés ; versions du registre verrouillées comme l’application. Le premier essai hôte a exécuté 17 tests PASS, mais sa qualification a refusé trois sources Tidewater modifiées. Installation et rendu restant. |
| L01/L04 — raccordement RGB8 au moteur courant | {canonical_rgb8.get('actual_host_tests_passed',0) if canonical_rgb8 else 0} / 17 tests | État : `{canonical_rgb8['status'] if canonical_rgb8 else 'non démarré'}`. Trois modules installés avec sauvegardes SHA sur F ; compilation du checkout courant distincte du succès figé. Rendu complet et recette restant. |
| L04 — texture de détail Rocks manquante | {complete_prefab_source['manifests_searched'] if complete_prefab_source else 0} / 48 inventaires | {complete_prefab_source['matching_manifests'] if complete_prefab_source else 0} inventaire contient le GUID déclaré ; contrôle des sources indexées, pas nouvel audit des archives. Aucun remplacement de texture appliqué. |
| L06 — FirePit, héritage des effets | {complete_prefab_source['effect_sources_reopened'] if complete_prefab_source else 0} / 2 sources | Feu et fumée, sources, matériaux et modifications du parent réouverts ; cinq overrides de particules conservés dans la capture suivante. Comportement et rendu restant. |
| L06 — FirePit, références temporelles natives | {firepit_behavior.get('frames_verified',0) if firepit_behavior else 0} / 112 observations | État : `{firepit_behavior['status'] if firepit_behavior else 'non démarré'}`. Clones du parent complet de neuf nœuds, deux graines, deux répétitions et 14 temps par effet. {'Transformations et cinq overrides vérifiés ; temps f32 exacts et répétabilité contrôlés. Cinq changements de métadonnées privées de dossiers et refus SHA global conservés.' if firepit_behavior and firepit_behavior.get('native_behavior_verified') else 'Transformations et cinq overrides à comparer nativement.'} Lecture autonome, conversion Forge et rendu distincts. |
| L06 — FirePit, réparation du composant HDRP | {1 if firepit_hdrp_repair else 0} / 1 GUID de composant | {'Fournisseur HDAdditionalLightData 17.5.0 vérifié, fermeture des dépendances préparée dans le projet HDRP et refus initial conservé' if firepit_hdrp_repair else 'Composant manquant dans le projet minimal à résoudre'}. Capture effective et conversion restant. |
| L01/L04 — fermeture des inclusions moteur | — | État : `{rgb8_closure['status'] if rgb8_closure else 'non démarré'}`. Quatre inclusions littérales restantes recensées ; leur reprise attend la fin du contrôleur exact, conserve les refus et réutilise le cache. Aucun job actif interrompu. |
| L04 — matériaux RocksTemperate et FirePit | {(1 if rocks else 0)+(1 if firepit_reopen else 0)} / 2, {(7 if rocks else 0)+(3 if firepit_reopen else 0)} / 10 bindings | Réouvertures indépendantes séparées ; refus historiques conservés. État effectif, textures GPU, programmes complets et rendu restant. |
| L04 — RocksTemperate, réussite isolée | {rocks.get('native_materials_verified',0) if rocks else 0} / 1, 7 bindings | Capture réussie du lot partiel réouverte indépendamment ; propriétés sauvegardées identiques, quatre corruptions refusées. Rendu et livraison du prefab complet restent requis. |
| L04 — FirePit, fermeture des dépendances | {firepit_reopen.get('native_materials_reopened',0) if firepit_reopen else 0} / 1, 3 bindings | État : `{firepit_reopen['status'] if firepit_reopen else (firepit['status'] if firepit else 'non démarré')}`. Deux sous-graphes originaux vérifiés contre l’inventaire ; propriétés sauvegardées identiques. Réécriture privée Unity conservée ; parité effective et deux effets restent à vérifier. |
| L04 — Rocks/FirePit, stockage des textures | {rocks_texture.get('source_texture_storage_captures_verified',0) if rocks_texture else 0} / 10, {rocks_texture.get('mips_verified',0) if rocks_texture else 0} / 120 mips | Réouverture indépendante des identités, sources, importeurs, formats, mips et samplers. Quatre normales ; comparaison GPU et décodage restant. |
| L04 — Rocks/FirePit, état effectif HDRP | {rocks_effective.get('materials_compared',0) if rocks_effective else 0} / 2 | {rocks_effective.get('effective_property_differences',0) if rocks_effective else 0} écarts de masques de stencil mesurés, MotionVectors désactivée ; keywords ajoutés conservés. Parité du rendu non qualifiée. |
| L04 — Rocks/FirePit, textures GPU | {rocks_gpu.get('gpu_textures_verified',0) if rocks_gpu else 0} / 10 | État : `{rocks_gpu['status'] if rocks_gpu else 'non démarré'}`. 120 mips attendus ; quatre BC1 sRGB et six BC3 linéaires natifs, sans expansion RGB24. Comparaison Unity et rendu restent distincts. |
| L04 — Rocks/FirePit, normales Unity | {rocks_normal.get('native_normal_textures_verified',0) if rocks_normal else 0} / 4 | État : `{rocks_normal['status'] if rocks_normal else 'non démarré'}`. 48 mips attendus ; capture après le contrôleur GPU exact, images sources existantes réutilisées. Comparaison avec le décodeur Forge restant. |
| L04 — Rocks/FirePit, comparaison RGBA native | {rocks_raw.get('normal_textures_compared',0) if rocks_raw else 0} / 4, 48 mips | 196 608 positions à tolérance fixe 1e-6 ; mapping identity mesuré. Décodeur et rendu restent des contrôles distincts. |
| L04 — Rocks/FirePit, programmes éclairés | {rocks_lit.get('native_lit_programs_reopened',0) if rocks_lit else 0} / 2 | 14 compositions Bevy et six refus réouverts ; calculs source, matrices et UV0 conservés. Installation, admission et rendu des prefabs complets restent requis. |
| L04 — Rocks/FirePit, décodeur GPU | {rocks_decoder.get('native_normal_textures_verified',0) if rocks_decoder else 0} / 4 | État : `{rocks_decoder['status'] if rocks_decoder else 'non démarré'}`. Helper exactement extrait des deux programmes ; 48 mips à comparer aux références Unity. |
| L04 — licences des helpers portés | {rocks_license.get('copied_files',0) if rocks_license else 0} / 5 copies | Licences Unity exactes jointes aux nouveaux dérivés privés ; licences des assets achetés conservées séparément. |

Ces nombres comptent des **étapes techniques**. Ils ne constituent pas un total d’assets utilisables et ne ferment pas l’import complet.

## Découpage à suivre

1. **L00 — sources et stockage** : intégrité du Vault, archives, licences, provenance.
2. **L01 — chargement et usage communs** : ressources, aperçu, placement, édition et runtime.
3. **L02 — Dungeon** : prefabs complets, matériaux et rendu.
4. **L03 — conversions statiques** : lots de géométries, puis assemblage et contrôle des prefabs.
5. **L04 — textures, matériaux et feuillage** : couleurs, normales, transparence, mips et déformation.
6. **L05 — rigs et animations** : os, poids, poses, clips et rendu animé.
7. **L06 — effets** : dépendances, émission, trajectoires, durée et transparence.
8. **L07 — sprites et terrains** : conversion et usage adaptés au format.
9. **L08 — bibliothèque** : images propres aux assets, fiches, recherche, filtres et recette réelle.
10. **L09 — réconciliation finale** : chaque ID attendu livré ou refus explicite, couverture complète.

Chaque sous-lot suit : **conversion → vérification → comportement/rendu → fiche et image → usage éditeur/runtime**. Une étape refusée reste visible et ne fait pas avancer le compteur de livraison.

[Découpage et critères de fin](DECOUPAGE_OPERATIONNEL.md) · [Sous-lots et reçus courants](PROCHAINS_SOUS_LOTS.md) · [Nouveaux effets](../embedded-effect-execution-batches-v1/SOUS_LOTS.md) · [Effets déjà recensés](../effect-execution-batches-v1/SOUS_LOTS.md) · [Inventaire des 48 packs](INVENTAIRE_PAR_PACK.csv) · [Historique](JOURNAL.md).
"""
    digest_path = T / 'AVANCEMENT_ACTUEL.md'
    if digest_path.is_file():
        replace_checked(digest_path, digest_path.read_bytes(), digest.encode('utf-8'))
    else:
        with digest_path.open('x', encoding='utf-8') as stream:
            stream.write(digest)
    plan_v1 = T / 'DECOUPAGE_OPERATIONNEL.v1.json'
    plan_v2 = T / 'DECOUPAGE_OPERATIONNEL.v2.json'
    plan = read(plan_v1)
    assert plan['pack_rows'] == 48 and not plan['complete']
    assert sha(plan['inventory_path']) == plan['inventory_sha256']
    actions = {
        'L01': "Frontend et candidat natif construits. Une instance GLB : aperçu, placement, retouche et réouverture contrôlés. Rejouer le playtest au départ réel X=16/Z=8 ; terminer aussi le chargement et le rendu communs des scènes à matériaux source.",
        'L02': "42/42 captures HDRP et réouverture indépendante réussies. Qualifier le rendu et les copies de matériaux migrées avant assemblage/livraison des prefabs complets.",
        'L04': "Captures Pirate Island 12/12, Medieval 3/3 et Villa URP 6/6 avec 10 valeurs par défaut réouvertes. Delta : 6 kernels, 24 compositions vertex et 24 interfaces de surface, UV1.y sur 52 nœuds/27 géométries. Six programmes d'éclairage/prépass assemblés : 46 compositions et 6 refus réouverts. Six instances natives et 18 bindings réouverts ; 18 différences de stencil effectif conservées. Les 18 textures et 225 mips sont réouverts ; trois RGB24 converties exactement en RGBA8. Terminer le contrôle GPU, le décodage des normales, l'admission des 17 tests hôte et le rendu moteur. Treize kernels liés aux textures manquantes restent à convertir sans inventer de référence. Villa : 207 dérivés vérifiés, 19 scènes séparées ; livraison complète encore requise.",
        'L06': "FirePit en cours : deux effets hérités, cinq overrides de particules et 112 observations natives attendues dans le parent complet ; convertir ensuite leurs matériaux et comportements dans Forge. Continuer Blood et Dust, puis les 44 sous-lots initiaux (85 prefabs, 139 graphes) et 29 nouveaux sous-lots (89 sources directes). Six scènes binaires et héritage restant à inspecter ; émission, trajectoires, durée et transparence à qualifier par effet.",
        'L08': "Recherche/filtres et deux fiches examinés dans le candidat natif. Paper Cluster : aperçu texturé, placement, retouche, sauvegarde et réouverture contrôlés ; essai runtime inconclusif à reprendre au départ réel X=16/Z=8. Terminer la couverture globale et les 948 images historiques.",
    }
    if foliage_decoder and foliage_decoder.get('native_normal_textures_verified') == 6 and foliage_rgb_parity:
        actions['L04'] = ("Six programmes complets RG-or-AG réouverts : 46 compositions et six refus. "
            "18 textures/225 mips échantillonnés sur GPU. Six normales/75 mips : RGBA et décodeur "
            "concordent avec Unity sur 307 200 positions ; trois RGB24/37 mips concordent après "
            "expansion RGBA8 sur 151 552 positions. Ajouter RGB8 natif au transport et à la préparation "
            "moteur, en gardant le capture JSON original ; préparer ensuite les GLB et leurs bindings. "
            "Préserver l'exécution des 17 tests hôte déjà en compilation avant cette mutation. "
            "Rendu, installation et recette éditeur/runtime restent requis. Treize kernels de textures "
            "manquantes, les autres packs et 19 scènes Villa restent couverts par leurs sous-lots.")
    if rgb8_transport and rgb8_transport.get('native_rgb24_textures_verified') == 3 and rgb8_regression:
        actions['L04'] = ("Candidat RGB8 vérifié : trois textures, 37 mips et 151 552 positions "
            "concordent avec Unity ; quinze régressions de stockage/mips/samplers réussies. "
            "Préserver la fin des 17 tests hôte en cours, puis installer les trois modules exacts "
            "et vérifier le chargeur complet. Assembler ensuite les prefabs complets avec les six "
            "matériaux natifs et leurs 18 textures, sans oublier les matériaux et comportements "
            "non encore couverts. Rendu, images, éditeur/runtime, autres packs et 19 scènes Villa "
            "restent à vérifier ; aucune livraison complète n'est déclarée.")
    if frozen_rgb8:
        actions['L04'] = ("RGB8 : trois textures/37 mips et quinze régressions vérifiés. Les 17 tests "
            "initiaux ont exécuté PASS mais leur qualification a refusé trois sources Tidewater "
            "concurrentes. La vraie copie figée sur F est en contrôle, avec le fork rerecast et les "
            "versions verrouillées de l’application. Finir ce contrôle, puis installer les trois "
            "modules après vérification de leurs hashes. RocksTemperate et FirePit : captures "
            "natives de deux matériaux/10 bindings en reprise après échec UPM avant export. "
            "Le prefab Rocks a 86 nœuds/57 renderers/8 matériaux ; FirePit a 9 nœuds/4 renderers "
            "et deux effets. Préserver les trois matériaux et les comportements encore incomplets "
            "pendant l’assemblage complet. Rendu, images, éditeur/runtime et autres packs restent requis.")
    if rocks_texture and firepit_reopen:
        actions['L04'] = ("RocksTemperate et FirePit : deux captures de matériaux/10 bindings réouverts, "
            "propriétés sauvegardées identiques ; six écarts de masques de stencil et MotionVectors "
            "désactivée conservés, parité effective non qualifiée. Dix textures/120 mips réouverts "
            "indépendamment ; finir leur contrôle GPU puis références Unity et décodeurs des quatre normales. "
            "Finir les 17 tests du chargeur RGB8 figé, compléter ses inclusions exactes après la fin "
            "du contrôleur actif, puis intégrer les trois modules avec sauvegardes et contrôle SHA. "
            "Terminer les programmes des huit matériaux du prefab Rocks (86 nœuds/57 renderers), "
            "et celui de FirePit (9 nœuds/4 renderers). Convertir aussi ses deux effets ; assembler "
            "les prefabs complets, comparer le rendu, produire leurs images et vérifier placement/édition/" 
            "sauvegarde/réouverture/runtime. Autres packs et 19 scènes Villa restent au périmètre.")
    if rocks_decoder and rocks_decoder.get('native_normal_textures_verified') == 4 and rocks_lit:
        actions['L04'] = ("RocksTemperate et FirePit : deux programmes complets éclairés/prépasses "
            "réouverts, 14 compositions Bevy et six refus ; calculs et UV0 sources conservés. "
            "Dix textures/120 mips validés sur GPU ; quatre normales/48 mips concordent avec Unity "
            "sur 196 608 positions, et leur décodeur exact a réussi le contrôle GPU. Licences Unity "
            "jointes aux cinq dérivés. Finir les 17 tests du chargeur RGB8 figé puis intégrer les trois "
            "modules exacts avec sauvegardes, garde SHA et contrôle canonique. Résoudre et mesurer "
            "la texture par défaut du matériau Detail restant avant l’assemblage du prefab Rocks "
            "complet (86 nœuds/57 renderers/8 matériaux). Assembler aussi FirePit (9 nœuds/4 renderers) "
            "en conservant ses deux effets à convertir. Les six écarts HDRP de stencil et MotionVectors "
            "désactivée restent visibles. Rendu des prefabs, images, édition/sauvegarde/réouverture/runtime, "
            "autres packs et 19 scènes Villa restent au périmètre ; aucune livraison complète annoncée.")
    old_plan = {lot['id']: lot['next'] for lot in plan['lots']}
    for lot in plan['lots']:
        if lot['id'] in actions:
            lot['next'] = actions[lot['id']]
    plan.update(schema='hivemind.execution-batches.v2', updated_utc=now,
        previous_plan=str(plan_v1), previous_plan_sha256=sha(plan_v1),
        current_progress=str(digest_path), current_receipts=str(target), complete=False)
    plan_data = json.dumps(plan, indent=2, ensure_ascii=False).encode('utf-8')
    if plan_v2.is_file():
        replace_checked(plan_v2, plan_v2.read_bytes(), plan_data)
    else:
        with plan_v2.open('xb') as stream:
            stream.write(plan_data)
    plan_md = T / 'DECOUPAGE_OPERATIONNEL.md'
    before_md = plan_md.read_bytes()
    previous_md = T / f'DECOUPAGE_HISTORIQUE_{hashlib.sha256(before_md).hexdigest()[:16]}.md'
    if not previous_md.exists():
        with previous_md.open('xb') as stream:
            stream.write(before_md)
    contents = before_md.decode('utf-8-sig')
    import re
    contents = re.sub(r'Actualisation : [^\r\n]+', f'Actualisation : {now}.', contents, count=1)
    for identifier, action in actions.items():
        contents = contents.replace(old_plan[identifier], action)
    # Refresh the authoritative action column even after earlier updates changed
    # its text. Matching the immutable v1 prose alone leaves stale current rows.
    refreshed_lines = []
    for line in contents.splitlines(keepends=True):
        for identifier, action in actions.items():
            if line.startswith(f'| {identifier} |'):
                ending = line[len(line.rstrip('\r\n')):]
                cells = line.rstrip('\r\n').split('|')
                assert len(cells) == 6
                cells[4] = ' ' + action + ' '
                line = '|'.join(cells) + ending
                break
        refreshed_lines.append(line)
    contents = ''.join(refreshed_lines)
    progress_link = '\nSuivi courant lisible : [AVANCEMENT_ACTUEL.md](AVANCEMENT_ACTUEL.md). Les reçus détaillés et états des exécutions sont dans [PROCHAINS_SOUS_LOTS.md](PROCHAINS_SOUS_LOTS.md). Plan JSON actualisé : `DECOUPAGE_OPERATIONNEL.v2.json` ; version initiale conservée.\n'
    if 'Suivi courant lisible :' not in contents:
        contents = contents.replace('# Travail Hivemind par lots\r\n', '# Travail Hivemind par lots\r\n' + progress_link, 1)
        if 'Suivi courant lisible :' not in contents:
            contents = contents.replace('# Travail Hivemind par lots\n', '# Travail Hivemind par lots\n' + progress_link, 1)
    replace_checked(plan_md, before_md, contents.encode('utf-8'))
    print(json.dumps(dict(tracking_updated=True, current_frame_tests_verified=frame_admission.get('tests_passed',0) if frame_admission else 0, rgb8_transport_verified=rgb8_transport.get('native_rgb24_textures_verified',0) if rgb8_transport else 0, full_import=False)))


if __name__ == '__main__':
    main()
