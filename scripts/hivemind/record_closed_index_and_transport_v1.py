"""Publish terminal receipts without promoting transport tests to editor acceptance."""
from pathlib import Path
import hashlib
import json
import re

W = Path('F:/OpenRPG_ImportWork')
T = W / 'SUIVI_LOTS_HIVEMIND'


def main():
    paths = {
        'indexed_integrity': W / 'indexed-payload-integrity-independent-reopen-v3/RESULT.v1.json',
        'native_transport': W / 'native-model-transport-installed-v1/RESULT.v2.json',
        'wasm_transport': W / 'model-large-wasm-carrier-v1/RESULT.v2.json',
    }
    if not all(p.exists() for p in paths.values()):
        return
    receipts = {k: json.loads(p.read_text('utf-8-sig')) for k, p in paths.items()}
    indexed, native, wasm = (receipts[k] for k in paths)
    assert indexed['indexed_entries'] == 26735 and indexed['refused_entries'] == 0
    assert native['actual_tests_passed'] == 45 and native['canonical_source_installed']
    assert wasm['actual_node_exit_code'] == 0 and wasm['model_bytes'] == 709944920
    assert not indexed['full_import'] and not native['actual_browser_editor_qualified']
    block = '''<!-- TERMINAL-INDEX-TRANSPORT-BEGIN -->
## Dernier bilan vérifié — lots L00, L01 et L08

**L00/L08 : audit des fichiers indexés terminé.** 26 735 fiches rapprochées : 14 219 modèles déclarés et 12 516 sources support. SHA contrôlés, 25 787 références à images vérifiées, 948 absences conservées explicitement ; 38 archives référencées contrôlées, zéro refus. Le contrôle des autres fichiers du Vault reste ouvert.

**L01 : correctif natif installé dans les sources, 45 tests réussis.** Le transport est borné à 1 Gio et conserve le tampon du modèle. Le paquet Rocks de 709 944 920 octets a aussi passé la sonde WASM sous Node : 8 contrats matériau, 86 nœuds, environ 1,32 Gio de mémoire linéaire et deux corruptions refusées.

**À faire avant disponibilité dans Studio :** lecteur Web encore borné à 512 Mio, qualification mémoire en navigateur, raccordement du renderer source_graph à l'aperçu et au placement, reconstruction puis recette réelle de l'éditeur. La sonde Node et les tests natifs ne qualifient pas le navigateur, le GPU, les LOD ou les effets.

**Sous-lot suivant : L00.VAULT.FICHIERS_NON_INDEXES**, inventaire sans suivre les jonctions, rapprochement avec les reçus existants, puis vérification par lots des fichiers non couverts. **L01.STUDIO.RENDU_WEB** suit pour débloquer la recette des scènes complètes. Aucun nouvel asset n'est compté livré sur la seule base de ces contrôles.

[Audit terminé](../indexed-payload-integrity-independent-reopen-v3/RESULT.v1.json) · [Correctif installé et tests](../native-model-transport-installed-v1/RESULT.v2.json) · [Sonde WASM](../model-large-wasm-carrier-v1/RESULT.v2.json).
<!-- TERMINAL-INDEX-TRANSPORT-END -->
'''
    for name in ('AVANCEMENT_PAR_LOTS.md', 'LOTS_ACTIFS.md'):
        path = T / name
        text = path.read_text('utf-8-sig')
        for marker in ('MODEL-WIRE-CANDIDATE', 'TERMINAL-INDEX-TRANSPORT'):
            text = re.sub(r'<!-- ' + marker + r'-BEGIN -->.*?<!-- ' + marker + r'-END -->\s*', '', text, flags=re.S)
        text = text.replace('le transport binaire de bibliothèque plafonne actuellement à **256 Mio**',
                            'le transport binaire installé dans les sources est borné à **1 Gio**')
        text = text.replace('dépasse les deux limites', 'dépasse la limite du lecteur Web')
        text = text.replace('Audit des payloads/archives référencés en cours ; ensuite réconcilier tous les fichiers actuels du Vault.',
                            'Audit des 26 735 payloads indexés et 38 archives référencées terminé ; réconcilier les fichiers non indexés du Vault.')
        heading = '# Avancement Hivemind par lots\n'
        text = text.replace(heading, heading + '\n' + block + '\n', 1) if text.startswith(heading) else block + '\n' + text
        path.write_text(text, 'utf-8')
    state_path = T / 'ETAT.v1.json'
    state = json.loads(state_path.read_text('utf-8-sig'))
    state['latest_terminal_checkpoints'] = {
        k: {'receipt': str(p), 'receipt_sha256': hashlib.sha256(p.read_bytes()).hexdigest(),
            'status': receipts[k]['status']} for k, p in paths.items()
    }
    state['indexed_payload_integrity_qualified'] = True
    state['whole_current_vault_qualified'] = False
    state['canonical_native_transport_installed'] = True
    state['actual_editor_global_qualified'] = False
    state['goal_complete'] = False
    state['active_sub_batch'] = 'L00.VAULT.FICHIERS_NON_INDEXES'
    state['next_sub_batch'] = 'L01.STUDIO.RENDU_WEB'
    boundary_root = W / 'rocks-firepit-native-lod-boundary-v1'
    boundary_run = boundary_root / 'RUN-RESULT.v1.json'
    if boundary_run.exists():
        run_boundary = json.loads(boundary_run.read_text('utf-8-sig'))
        preparation = json.loads((boundary_root / 'PREPARED.v1.json').read_text('utf-8-sig'))
        boundary_review = W / 'rocks-firepit-native-lod-boundary-reopen-v1/RESULT.v1.json'
        boundary_text = (f"Mesure native préparée pour **{preparation['expected_samples']} cas** : "
                         f"chaque seuil original, son voisin f32 inférieur et supérieur, trois angles de caméra. "
                         f"État enregistré : `{run_boundary['status']}`. Clones temporaires du prefab original ; "
                         f"aucun nouveau modèle, aucune simulation de particules, aucun rendu GPU. "
                         f"[Exécution](../rocks-firepit-native-lod-boundary-v1/RUN-RESULT.v1.json).")
        if boundary_review.exists():
            review = json.loads(boundary_review.read_text('utf-8-sig'))
            boundary_text += (f"\n\nRéouverture : **{review['samples']} mesures**, "
                              f"{review['exact_equal_boundaries']}/{review['boundaries']} seuils observés à égalité exacte. "
                              f"Règles compatibles : {', '.join(review['comparison_rule_candidates']) or 'aucune'}. "
                              f"Cela ne qualifie pas les décisions et la visibilité dans Forge. "
                              f"[Reçu](../rocks-firepit-native-lod-boundary-reopen-v1/RESULT.v1.json).")
            if not review['native_metric_comparison_rule_qualified']:
                boundary_text += ("\n\n**Candidat non activé** : aucune des comparaisons simples sur la hauteur f32 "
                                  "ne reproduit toutes les décisions. Les distances natives et les positions ont "
                                  "aussi été comparées ; le calcul de projection reste à isoler. "
                                  "[Diagnostic conservé](../rocks-firepit-native-lod-boundary-v1/DISTANCE-DIAGNOSIS.v1.json).")
                projection_run = W / 'rocks-firepit-native-lod-boundary-v2/RUN-RESULT.v1.json'
                if projection_run.exists():
                    projection = json.loads(projection_run.read_text('utf-8-sig'))
                    boundary_text += (f"\n\nCapture complémentaire des matrices de caméra et distances "
                                      f"par seuil : `{projection['status']}`. "
                                      f"[Exécution complémentaire](../rocks-firepit-native-lod-boundary-v2/RUN-RESULT.v1.json). "
                                      f"[Déclaration officielle des APIs natives mesurées](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/LODUtility.bindings.cs).")
        projection_review_path = W / 'rocks-firepit-native-lod-boundary-reopen-v2/RESULT.v1.json'
        if projection_review_path.exists():
            projection_review = json.loads(projection_review_path.read_text('utf-8-sig'))
            assert projection_review['samples'] == 549
            boundary_text += (f"\n\n**Capture complémentaire réouverte** : {projection_review['samples']} cas, "
                              f"{projection_review['projection_groups']} configurations de projection ; "
                              f"six corruptions des matrices/distances refusées, en plus des cinq contrôles des cas. "
                              f"Les huit comparaisons de distance testées restent incompatibles avec toutes les décisions natives. "
                              f"Le calcul LOD n'est pas activé dans Forge. "
                              f"[Réouverture des projections](../rocks-firepit-native-lod-boundary-reopen-v2/RESULT.v1.json).")
            state['latest_native_lod_projection_review'] = {
                'receipt': str(projection_review_path), 'samples': 549,
                'forge_lod_runtime_qualified': False,
                'negative_controls_refused': len(projection_review['projection_negative_controls_refused']),
            }
        culling_review_path = W / 'rocks-firepit-native-lod-boundary-reopen-v3/RESULT.v1.json'
        if culling_review_path.exists():
            culling_review = json.loads(culling_review_path.read_text('utf-8-sig'))
            assert culling_review['native_observations_identical_to_projection_capture'] == 549
            boundary_text += ("\n\n**Paramètres natifs de culling contrôlés** : champ de vision et position identiques "
                              "aux paramètres de caméra pour les 549 cas. Les décisions et valeurs natives de la capture "
                              "précédente se répètent exactement ; cinq corruptions supplémentaires refusées. "
                              "Cette piste ne résout pas les écarts de calcul. "
                              "[Réouverture native](../rocks-firepit-native-lod-boundary-reopen-v3/RESULT.v1.json).")
            state['latest_native_lod_culling_review'] = {
                'receipt': str(culling_review_path), 'repeated_observations': 549,
                'forge_lod_runtime_qualified': False,
            }
        arithmetic_path = W / 'lod-projection-arithmetic-diagnosis-v2/RESULT.v1.json'
        if arithmetic_path.exists():
            arithmetic = json.loads(arithmetic_path.read_text('utf-8-sig'))
            minimum = min(row['decision_mismatches'] for row in arithmetic['candidates'].values())
            boundary_text += (f"\n\nDiagnostic de {len(arithmetic['candidates'])} ordres de calcul génériques : "
                              f"au moins {minimum}/{arithmetic['samples']} décisions différentes pour chacun. "
                              f"Aucune règle n'est qualifiée ni installée. "
                              f"[Diagnostic arithmétique](../lod-projection-arithmetic-diagnosis-v2/RESULT.v1.json).")
        state['latest_native_lod_boundary_capture'] = {
            'receipt': str(boundary_run), 'recorded_status': run_boundary['status'],
            'expected_samples': preparation['expected_samples'], 'forge_lod_runtime_qualified': False,
        }
        for name in ('AVANCEMENT_PAR_LOTS.md', 'LOTS_ACTIFS.md'):
            path = T / name
            text = path.read_text('utf-8-sig')
            text = re.sub(r'<!-- NATIVE-LOD-BOUNDARY-BEGIN -->.*?<!-- NATIVE-LOD-BOUNDARY-END -->\s*', '', text, flags=re.S)
            path.write_text(text + '\n<!-- NATIVE-LOD-BOUNDARY-BEGIN -->\n## L04 — Comportement aux seuils LOD\n\n'
                            + boundary_text + '\n<!-- NATIVE-LOD-BOUNDARY-END -->\n', 'utf-8')
    history_path = W / 'vault-registry-evolution-review-v3/RESULT.v1.json'
    if history_path.exists():
        history = json.loads(history_path.read_text('utf-8-sig'))
        assert not history['errors'] and history['current_heads_verified'] == history['total_current_heads'] == 43
        state['current_import_history_chains_verified'] = {
            'receipt': str(history_path), 'receipt_sha256': hashlib.sha256(history_path.read_bytes()).hexdigest(),
            'current_heads_verified': 43, 'histories_read': history['histories_read'],
            'retouch_content_semantics_qualified': False,
        }
        history_block = '''<!-- CURRENT-IMPORT-HISTORY-BEGIN -->
## L00 — Historiques des imports réouverts

**43/43 chaînes actuelles vérifiées jusqu'à l'import initial**, 230 transactions lues, zéro refus restant. Les transactions groupées contrôlent exactement deux packs et leurs registres avant/après ; deux transitions positives et cinq corruptions ont été testées. Le refus v2 est conservé : il concernait le schéma du vérificateur. Les snapshots et fichiers de métadonnées sont rehachés ; cela ne qualifie pas les retouches visuelles ou le rendu.

[Rapprochement actuel](../vault-registry-evolution-review-v3/RESULT.v1.json) · [Refus initial conservé](../vault-registry-evolution-review-v2/RESULT.v1.json).
<!-- CURRENT-IMPORT-HISTORY-END -->
'''
        for name in ('AVANCEMENT_PAR_LOTS.md', 'LOTS_ACTIFS.md'):
            path = T / name
            text = path.read_text('utf-8-sig')
            text = re.sub(r'<!-- CURRENT-IMPORT-HISTORY-BEGIN -->.*?<!-- CURRENT-IMPORT-HISTORY-END -->\s*', '', text, flags=re.S)
            path.write_text(text + '\n' + history_block, 'utf-8')
    gate_path = W / 'model-read-gate-v1/RESULT.v1.json'
    if gate_path.exists():
        gate = json.loads(gate_path.read_text('utf-8-sig'))
        assert gate['actual_tests_passed'] == 3 and gate['canonical_sources_installed']
        state['latest_transport_concurrency_gate'] = {
            'receipt': str(gate_path), 'receipt_sha256': hashlib.sha256(gate_path.read_bytes()).hexdigest(),
            'canonical_sources_installed': True, 'actual_tests_passed': 3,
            'actual_browser_qualified': False,
        }
        gate_block = '''<!-- MODEL-READ-GATE-BEGIN -->
## L01 — Chargements binaires bornés

Garde FIFO installé dans le chemin commun de lecture Vault : un transfert actif, 128 demandes de métadonnées maximum en attente. Trois tests natifs réussis (ordre, annulation, capacité) et compilation du module WASM réussie avec sources épinglées. Le garde couvre la requête IPC, le décodage et la vérification SHA ; il ne qualifie pas la mémoire des scènes résidentes, les ressources GPU ou le rendu.

Le contrôle du frontend complet est distinct. [Tests et limites](../model-read-gate-v1/RESULT.v1.json) · [Compilation du frontend](../studio-current-source-wasm-check-v4/RUN-RESULT.v1.json).
<!-- MODEL-READ-GATE-END -->
'''
        for name in ('AVANCEMENT_PAR_LOTS.md', 'LOTS_ACTIFS.md'):
            path = T / name
            text = path.read_text('utf-8-sig')
            text = re.sub(r'<!-- MODEL-READ-GATE-BEGIN -->.*?<!-- MODEL-READ-GATE-END -->\s*', '', text, flags=re.S)
            path.write_text(text + '\n' + gate_block, 'utf-8')
    plan_path = W / 'vault-physical-batch-plan-v1/RESULT.v1.json'
    if plan_path.exists():
        plan = json.loads(plan_path.read_text('utf-8-sig'))
        state['physical_vault_batch_plan'] = {
            'receipt': str(plan_path), 'receipt_sha256': hashlib.sha256(plan_path.read_bytes()).hexdigest(),
            'status': plan['status'], 'physical_files': plan['physical_files'],
            'batches_prepared': len(plan['batches']), 'current_content_sha_verified': False,
        }
        summary = (f"\n\n### L00 — Inventaire physique et lots de contrôle\n\n"
                   f"{plan['physical_files']:,} fichiers physiques inventoriés, "
                   f"{len(plan['batches'])} groupes préparés de 512 fichiers maximum ou 2 Gio "
                   f"(un gros fichier reste entier). {plan['new_or_changed_metadata']:,} fichiers nouveaux "
                   f"ou modifiés selon les métadonnées historiques ; {len(plan['errors'])} erreurs "
                   f"d'inventaire. Cette étape ne constitue pas une nouvelle vérification SHA. "
                   f"[Inventaire et lots](../vault-physical-batch-plan-v1/RESULT.v1.json).\n")
        marker = '<!-- PHYSICAL-VAULT-PLAN-BEGIN -->'
        checks = []
        for receipt_path in sorted((plan_path.parent / 'checks').glob('*.RESULT.v1.json')):
            checked = json.loads(receipt_path.read_text('utf-8-sig'))
            if checked['status'] == 'batch_current_sha_reads_completed' and not checked['errors']:
                checks.append(checked)
        state['physical_vault_batch_plan']['sha_batches_completed'] = len(checks)
        state['physical_vault_batch_plan']['current_sha_files_checked'] = sum(c['checked_files'] for c in checks)
        if checks:
            summary += (f"\n**Contrôles SHA réalisés : {len(checks)}/{len(plan['batches'])} groupes physiques**, "
                        f"{sum(c['checked_files'] for c in checks):,} fichiers relus intégralement. "
                        f"Les groupes préparés comprennent aussi des fichiers couverts par des preuves antérieures ; "
                        f"leur couverture doit être rapprochée avant de les relire inutilement. "
                        f"[Reçus par groupe](../vault-physical-batch-plan-v1/checks).\n")
        mapping_path = W / 'vault-indexed-coverage-plan-v1/RESULT.v1.json'
        if mapping_path.exists():
            mapping = json.loads(mapping_path.read_text('utf-8-sig'))
            summary += (f"\n**Rapprochement terminé : {mapping['covered_physical_paths']:,} chemins couverts par "
                        f"les preuves conservées ; {mapping['remaining_physical_paths']:,} fichiers restants**, "
                        f"{mapping['remaining_bytes']:,} octets, {len(mapping['remaining_batches'])} lots de SHA. "
                        f"[Couverture exacte](../vault-indexed-coverage-plan-v1/RESULT.v1.json).\n")
            run_path = mapping_path.parent / ('RUN-RESULT.v2.json' if (mapping_path.parent / 'RUN-RESULT.v2.json').exists() else 'RUN-RESULT.v1.json')
            if run_path.exists():
                run = json.loads(run_path.read_text('utf-8-sig'))
                summary += (f"\nDernier checkpoint de la série : **{run['completed_batches']}/{run['batch_count']} lots**, "
                            f"{run['checked_files']:,} fichiers relus. État enregistré : `{run['status']}`. "
                            f"[Checkpoints de l'exécution](../vault-indexed-coverage-plan-v1/{run_path.name}). "
                            f"La clôture exige encore stabilité finale de l'inventaire et rapprochement historique.\n")
                state['remaining_vault_sha_checkpoint'] = {
                    'receipt': str(run_path), 'completed_batches': run['completed_batches'],
                    'batch_count': run['batch_count'], 'checked_files': run['checked_files'],
                    'recorded_status': run['status'], 'whole_current_vault_qualified': False,
                }
            independent = sorted([*(mapping_path.parent / 'independent-checkpoints').glob('*.RESULT.v1.json'), *(mapping_path.parent / 'independent-checkpoints-v2').glob('*.RESULT.v1.json')], key=lambda path: path.name)
            if independent:
                independent_path = independent[-1]
                checkpoint = json.loads(independent_path.read_text('utf-8-sig'))
                summary += (f"\nRéouverture indépendante des **{checkpoint['completed_batches']} lots terminés** : "
                            f"{checkpoint['checked_files']:,} lignes de SHA rapprochées et partition exacte de "
                            f"{checkpoint['total_inventory_paths']:,} chemins vérifiée. "
                            f"[Reçu indépendant](../vault-indexed-coverage-plan-v1/{independent_path.parent.name}/{independent_path.name}).\n")
        for name in ('AVANCEMENT_PAR_LOTS.md', 'LOTS_ACTIFS.md'):
            path = T / name
            text = path.read_text('utf-8-sig')
            text = re.sub(r'<!-- PHYSICAL-VAULT-PLAN-BEGIN -->.*?<!-- PHYSICAL-VAULT-PLAN-END -->\s*', '', text, flags=re.S)
            text += '\n' + marker + summary + '<!-- PHYSICAL-VAULT-PLAN-END -->\n'
            path.write_text(text, 'utf-8')
    final_storage_path = W / 'vault-current-final-review-v4/RESULT.v1.json'
    final_storage = json.loads(final_storage_path.read_text('utf-8-sig')) if final_storage_path.exists() else None
    if final_storage:
        state['whole_current_vault_qualified'] = bool(final_storage['whole_current_vault_qualified'])
        state['latest_final_storage_review'] = {
            'receipt': str(final_storage_path), 'recorded_status': final_storage['status'],
            'receipt_sha256': hashlib.sha256(final_storage_path.read_bytes()).hexdigest(),
            'whole_current_vault_qualified': state['whole_current_vault_qualified'], 'full_import': False,
        }
        if state['whole_current_vault_qualified']:
            state['active_sub_batch'] = 'L01.STUDIO.RENDU_WEB'
    frontend_reopen = W / 'studio-current-wasm-independent-reopen-v5/RESULT.v1.json'
    if frontend_reopen.exists():
        compile_receipt = json.loads(frontend_reopen.read_text('utf-8-sig'))
        assert compile_receipt['actual_frontend_compile_qualified'] and not compile_receipt['full_import']
        state['actual_frontend_compile_checkpoint'] = {
            'receipt': str(frontend_reopen), 'source_files': compile_receipt['source_files'],
            'compiler_check_passed': True, 'actual_editor_global_qualified': False,
        }
    execution_text = "## L00/L01/L08 — Exécutions et refus courants\n\n"
    for label, folder in (
        ('Clôture du stockage — refus initial conservé', 'vault-current-final-review-job-v1'),
        ('Clôture du stockage — reprise chemins longs', 'vault-current-final-review-job-v2'),
        ('Compilation Studio précédente', 'studio-current-source-wasm-check-v4'),
        ('Reprise de compilation Studio', 'studio-current-source-wasm-check-v5'),
    ):
        receipt_path = W / folder / 'RUN-RESULT.v1.json'
        if receipt_path.exists():
            receipt = json.loads(receipt_path.read_text('utf-8-sig'))
            execution_text += f"**{label}** : `{receipt['status']}`. [Reçu](../{folder}/RUN-RESULT.v1.json).\n\n"
            if folder == 'studio-current-source-wasm-check-v4' and receipt.get('actual_exit_code') == 0:
                execution_text += (f"Compilation terminée sans erreur ; {len(receipt.get('changed_source_files', []))} "
                                   f"sources ont changé pendant le contrôle. Le snapshot entier n'est pas qualifié. "
                                   f"Les changements Tidewater sont préservés.\n\n")
            state[folder] = {'receipt': str(receipt_path), 'recorded_status': receipt['status']}
    if frontend_reopen.exists():
        execution_text += ('**Compilation Web Studio réouverte indépendamment** : exit 0 et 1 026 fichiers '
                           'épinglés inchangés. Ce sous-lot compile est vérifié ; bundle navigateur, GPU et '
                           'recette globale de l’éditeur restent ouverts. '
                           '[Réouverture](../studio-current-wasm-independent-reopen-v5/RESULT.v1.json).\n\n')
    if final_storage:
        execution_text += (f"**Réconciliation finale du stockage** : `{final_storage['status']}` ; "
                           f"{final_storage['physical_files']:,} fichiers inventoriés. "
                           '[Reçu final](../vault-current-final-review-v4/RESULT.v1.json). '
                           'Conversion fidèle, rendu et éditeur restent à qualifier.\n\n')
    long_path_receipt = W / 'vault-long-path-repair-review-v1/RESULT.v1.json'
    if long_path_receipt.exists():
        execution_text += ('**Chemins longs réparés dans le lecteur** : 144 refus réouverts, '
                           '512/512 fichiers du lot contrôlés et les 368 SHA précédents identiques. '
                           'Aucune source renommée, déplacée ou modifiée. '
                           '[Réouverture](../vault-long-path-repair-review-v1/RESULT.v1.json).\n\n')
    route_path = W / 'studio-source-renderer-route-install-v1/RESULT.v1.json'
    if route_path.exists():
        route = json.loads(route_path.read_text('utf-8-sig'))
        state['latest_source_renderer_route'] = {'receipt': str(route_path),
            'actual_tests_passed': route['actual_tests_passed'],
            'frontend_source_files_pinned': route['source_files_pinned_for_frontend'],
            'source_renderer_integrated': False, 'actual_editor_global_qualified': False}
        execution_text += ('**Contrôle du renderer installé dans l’aperçu et le placement réels** : '
                           'cinq tests réussis, dont le GLB FirePit original ; compilation Web sur 1 027 '
                           'fichiers inchangés. Les programmes source ne peuvent plus passer silencieusement '
                           'au chargeur GLTF ordinaire. Le renderer source visible reste à raccorder. '
                           '[Reçu](../studio-source-renderer-route-install-v1/RESULT.v1.json).\n\n')
    component_path = W / 'rocks-firepit-renderer-state-glb-v1/INDEPENDENT-REOPEN.v1.json'
    if component_path.exists():
        state['latest_native_renderer_flags'] = {'receipt': str(component_path), 'nodes': 95,
            'renderers': 63, 'lod_groups': 14, 'actual_source_renderer_integrated': False}
        execution_text += ('**États natifs embarqués dans les deux scènes de travail** : 95 nœuds, '
                           '63 renderers et 14 groupes LOD réouverts ; huit corruptions refusées. '
                           'Flags force-rendering-off et identités/enabled LOD ajoutés depuis les captures natives. '
                           'Tous les chunks binaires de géométrie, textures et shaders sont inchangés. '
                           'Application dans le host, repères/horloges, visibilité LOD et rendu GPU restant. '
                           '[Réouverture](../rocks-firepit-renderer-state-glb-v1/INDEPENDENT-REOPEN.v1.json).\n\n')
    browser_path = W / 'editor-browser-observation-unavailable-v1/RESULT.v1.json'
    if browser_path.exists():
        execution_text += ("**Observation de l'éditeur** : deux démarrages du contrôleur navigateur ont échoué "
                           "dans le runtime sandbox (`helper_unknown_error`). Aucune nouvelle recette visuelle "
                           "n'est déduite. Les conversions et contrôles du stockage continuent. "
                           "[Diagnostic](../editor-browser-observation-unavailable-v1/RESULT.v1.json).\n")
    for name in ('AVANCEMENT_PAR_LOTS.md', 'LOTS_ACTIFS.md'):
        path = T / name
        text = path.read_text('utf-8-sig')
        text = re.sub(r'<!-- CURRENT-EXECUTIONS-BEGIN -->.*?<!-- CURRENT-EXECUTIONS-END -->\s*', '', text, flags=re.S)
        path.write_text(text + '\n<!-- CURRENT-EXECUTIONS-BEGIN -->\n' + execution_text + '<!-- CURRENT-EXECUTIONS-END -->\n', 'utf-8')
    state_path.write_text(json.dumps(state, ensure_ascii=False, indent=2), 'utf-8')
    journal = T / 'JOURNAL.md'
    marker = 'L00.L01.L08 — clôture audit indexé et installation transport'
    if marker not in journal.read_text('utf-8-sig'):
        with journal.open('a', encoding='utf-8') as stream:
            stream.write('\n\n## ' + marker + '\n\n' + block)
    if final_storage and final_storage['whole_current_vault_qualified']:
        for name in ('AVANCEMENT_PAR_LOTS.md', 'LOTS_ACTIFS.md'):
            path = T / name
            text = path.read_text('utf-8-sig')
            text = text.replace('Le contrôle des autres fichiers du Vault reste ouvert.',
                                'Intégrité du Vault actuel fermée : 132 961 fichiers et 603 276 106 799 octets. '
                                '[Réconciliation finale](../vault-current-final-review-v4/RESULT.v1.json).')
            text = text.replace('**Sous-lot suivant : L00.VAULT.FICHIERS_NON_INDEXES**, inventaire sans suivre les jonctions, rapprochement avec les reçus existants, puis vérification par lots des fichiers non couverts. **L01.STUDIO.RENDU_WEB** suit pour débloquer la recette des scènes complètes.',
                                '**Sous-lot suivant : L01.STUDIO.RENDU_WEB**, raccordement des scènes complètes puis recette réelle de l’éditeur. Le stockage fermé ne qualifie pas les conversions ou leur rendu.')
            path.write_text(text, 'utf-8')
    print('Terminal receipts recorded; editor remains open. Whole Vault storage qualified:', state['whole_current_vault_qualified'])



if __name__ == '__main__':
    main()
