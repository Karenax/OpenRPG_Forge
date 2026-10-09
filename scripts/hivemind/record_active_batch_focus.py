"""Write a concise batch focus from actual receipts, without closing delivery."""
from pathlib import Path
import datetime
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
T = W / 'SUIVI_LOTS_HIVEMIND'

def main():
    checkpoint = max((W / 'library-checkpointed-native-audit-v2').glob('INDEPENDENT-CHECKPOINT-REOPEN.v*.json'),
        key=lambda p: int(p.name.split('.v')[1].split('.')[0]))
    checkpoint_count = json.loads(checkpoint.read_text('utf-8-sig'))['details_checked']
    names = [
        'detail-original-render-contracts-v1/RESULT.v1.json',
        'detail-native-surface-interfaces-v1/RESULT.v1.json',
        'detail-native-surface-interfaces-v1/INDEPENDENT-REOPEN.v1.json',
        'detail-native-lit-fragments-v1/RESULT.v1.json',
        'detail-native-lit-fragments-v1/INDEPENDENT-REOPEN.v1.json',
        'source-graph-scalar-transport-native-v5/RUN-RESULT.v1.json',
        'detail-native-material-state-v1/PREPARED.v1.json',
        'detail-native-material-state-v1/RUN-RESULT.v1.json',
        'detail-source-raw-gpu-parity-v1/RESULT.v1.json',
        'detail-texture-gpu-v1/RUN-RESULT.v1.json',
        'detail-native-sampling-reference-v1/RUN-RESULT.v1.json',
        'detail-normal-mode2-bevy-candidate-v1/RESULT.v1.json',
        'detail-normal-decoder-gpu-v1/RUN-RESULT.v1.json',
        'detail-normal-decoder-gpu-v1/DECODER-PARITY.v1.json',
        'detail-normal-decoder-independent-reopen-v1/RESULT.v1.json',
        'detail-default-native-rg-binding-v2/RESULT.v1.json',
        'detail-native-default-bevy-vertex-v2/RESULT.v1.json',
        'detail-native-default-bevy-compositions-v2/RESULT.v1.json',
        'detail-native-normal-loop-aov-v16/PREPARED.v1.json',
        'detail-native-normal-loop-aov-v16/RUN-RESULT.v1.json',
        'detail-native-normal-loop-aov-v16/INDEPENDENT-REOPEN.v1.json',
        'detail-native-texture-storage-v1/PREPARED.v1.json',
        'detail-native-texture-storage-v1/RUN-RESULT.v1.json',
        'detail-native-texture-storage-v1/TEXTURE-REOPEN.v1.json',
        'detail-aov-private-input-compatibility-v1/REPAIR.v1.json',
        'detail-aov-private-material-context-v4/RESULT.v1.json',
        'detail-native-normal-loop-aov-v15/PREPARED.v1.json',
        'detail-native-normal-loop-aov-v15/RUN-RESULT.v1.json',
        'detail-native-normal-loop-aov-v15/INDEPENDENT-REOPEN.v1.json',
        'detail-native-normal-loop-aov-v14/PREPARED.v1.json',
        'detail-native-normal-loop-aov-v14/RUN-RESULT.v1.json',
        'detail-native-normal-loop-aov-v14/INDEPENDENT-REOPEN.v1.json',
        'library-all-records-independent-reconciliation-v4/RESULT.v1.json',
        'library-checkpointed-native-audit-v2/RUN-RESULT.v2.json',
        'detail-native-normal-loop-aov-v13/PREPARED.v1.json',
        'detail-native-normal-loop-aov-v13/RUN-RESULT.v1.json',
        'detail-native-normal-loop-aov-v13/INDEPENDENT-REOPEN.v1.json',
        'detail-native-aov-api-review-v1/RESULT.v1.json',
        'detail-native-normal-loop-aov-v12/PREPARED.v1.json',
        'detail-native-normal-loop-aov-v12/RUN-RESULT.v1.json',
        'detail-native-normal-loop-aov-v12/INDEPENDENT-REOPEN.v1.json',
        'library-incremental-pages-reopen-v3/RESULT.v1.json',
        'detail-debug-output-encoding-diagnosis-v1/RESULT.v1.json',
        'detail-shader-program-licenses-v1/RESULT.v1.json',
        'detail-default-native-gain-v11/PREPARED.v1.json',
        'detail-default-native-gain-v11/RUN-RESULT.v1.json',
        'detail-default-native-rg-binding-v1/RESULT.v1.json',
        'detail-native-default-kernel-v1/RESULT.v1.json',
        'detail-native-default-stages-v1/RESULT.v1.json',
        'detail-native-default-stages-negative-controls-v1/RESULT.v1.json',
        'library-page-count-refusal-v1/RESULT.v1.json',
        'library-available-images-independent-reopen-v2/RESULT.v1.json',
        'unity-source-null-mask-surface-v10/PREPARED.v1.json',
        'unity-source-null-mask-surface-v10/RUN-RESULT.v1.json',
        'detail-mask-surface-independent-reopen-v3/RESULT.v1.json',
        'unity-source-null-mask-surface-v9/PREPARED.v1.json',
        'unity-source-null-mask-surface-v9/RUN-RESULT.v1.json',
        'detail-mask-surface-independent-reopen-v2/RESULT.v1.json',
        'unity-source-null-mask-surface-v8/PREPARED.v1.json',
        'unity-source-null-mask-surface-v8/RUN-RESULT.v1.json',
        'detail-mask-surface-independent-reopen-v1/RESULT.v1.json',
        'source-unlit-forward-install-v1/INSTALL.v1.json',
        'source-unlit-targeted-native-check-v2/PREPARED.v1.json',
        'source-unlit-targeted-native-check-v2/RUN-RESULT.v1.json',
        'unity-source-null-mask-hdrp-v7/PREPARED.v1.json',
        'unity-source-null-mask-hdrp-v7/RUN-RESULT.v1.json',
        'detail-mask-original-graph-paths-v1/RESULT.v1.json',
        'asset-library-indexed-payload-integrity-resume-v2/PREPARED.v1.json',
        'asset-library-indexed-payload-integrity-resume-v2/STOPPED-PREDECESSOR.v1.json',
        'asset-library-indexed-payload-integrity-resume-v2/RUN-RESULT.v1.json',
        'source-unlit-program-native-check-v1/QUEUED.v1.json',
        'source-unlit-program-native-check-v1/PREPARED.v1.json',
        'source-unlit-program-native-check-v1/RUN-RESULT.v1.json',
        'source-unlit-canonical-native-check-v1/PREPARED.v1.json',
        'source-unlit-canonical-native-check-v1/RUN-RESULT.v1.json',
        'source-graph-production-color-mask-install-v1/PREPARED.v1.json',
        'source-graph-production-color-mask-install-v1/INSTALL.v1.json',
        'unity-source-null-mask-hdrp-v2/PREPARED.v1.json',
        'unity-source-null-mask-hdrp-v2/PRIVATE-CONTEXT.v1.json',
        'unity-source-null-mask-hdrp-v2/RUN-RESULT.v1.json',
        'unity-source-null-mask-hdrp-v2/COMPARISON.v1.json',
        'unity-source-null-mask-hdrp-v2/REPEATABILITY-DIAGNOSIS.v1.json',
        'unity-source-null-mask-hdrp-v3/TARGET-MIGRATION-DIAGNOSIS.v1.json',
        'unity-source-null-mask-hdrp-v4/PREPARED.v1.json',
        'unity-source-null-mask-hdrp-v4/RUN-RESULT.v1.json',
        'unity-source-null-mask-hdrp-v5/PREPARED.v1.json',
        'unity-source-null-mask-hdrp-v5/RUN-RESULT.v1.json',
        'unity-source-null-mask-hdrp-v6/PREPARED.v1.json',
        'unity-source-null-mask-hdrp-v6/RUN-RESULT.v1.json',
        'source-graph-production-color-mask-install-queue-v1/QUEUED.v1.json',
        'source-graph-production-color-mask-install-queue-v1/RUN-RESULT.v1.json',
        'source-unlit-program-native-check-v1/LOCK-ROOT-DEPENDENCY.v1.json',
        'firepit-native-age-arithmetic-diagnosis-v1/RESULT.v1.json',
        'firepit-gradient-boundaries-independent-reopen-v3/RUN-RESULT.v1.json',
        'firepit-gradient-boundaries-independent-reopen-v4/RUN-RESULT.v1.json',
        'firepit-gradient-boundaries-independent-reopen-v4/INDEPENDENT-REOPEN.v1.json',
        'firepit-gradient-boundary-color-paths-v1/PREDECLARED.v1.json',
        'firepit-gradient-boundary-color-paths-v1/RESULT.v1.json',
        'source-unlit-forward-native-check-v2/QUEUED.v1.json',
        'source-unlit-forward-native-check-v2/PREPARED.v1.json',
        'source-unlit-forward-native-check-v2/RUN-RESULT.v1.json',
        'source-graph-production-color-mask-check-v4/JOB.v1.json',
        'source-graph-production-color-mask-check-v4/RUN-RESULT.v1.json',
        'source-graph-production-color-mask-check-v4/PREPARED.v1.json',
        'source-graph-production-color-mask-check-v4/LOCK-RESOLUTION.v1.json',
        'delta-firepit-ra-shader-gpu-v2/QUEUED.v1.json',
        'delta-firepit-ra-shader-gpu-v2/PREPARED.v1.json',
        'delta-firepit-ra-shader-gpu-v2/RUN-RESULT.v1.json',
        'delta-firepit-ra-shader-gpu-v2/COMPARISON.v1.json',
        'unity-delta-firepit-gradient-boundaries-v2/PREPARED.v1.json',
        'unity-delta-firepit-gradient-boundaries-v2/RUN-RESULT.v1.json',
        'unity-delta-firepit-gradient-boundaries-v2/INDEPENDENT-REOPEN.v1.json',
        'unity-delta-firepit-particle-behavior-v7/RUN-RESULT.v1.json',
        'unity-delta-firepit-effect-materials-v1/RUN-RESULT.v1.json',
        'unity-builtin-shaders-6000.5.8f1-v4/RESULT.v1.json',
        'unity-builtin-shaders-6000.5.8f1-v4/SOURCE-CODE-COMPARISON.v1.json',
        'delta-firepit-alpha-shader-port-v1/RESULT.v1.json',
        'unity-delta-firepit-particle-geometry-v1/RUN-RESULT.v1.json',
        'delta-firepit-geometry-buffers-v2/RESULT.v1.json',
        'delta-firepit-baked-geometry-semantics-v1/RESULT.v1.json',
        'unity-delta-firepit-bake-state-probe-v2/RUN-RESULT.v1.json',
        'unity-delta-firepit-legacy-shader-gpu-v1/RUN-RESULT.v1.json',
        'unity-delta-firepit-legacy-shader-gpu-builtin-v2/RUN-RESULT.v1.json',
        'unity-delta-firepit-legacy-shader-gpu-builtin-v2/PRIOR-API-ADMISSION.v1.json',
        'delta-firepit-native-tint-gpu-v3/RESULT.v1.json',
        'delta-firepit-legacy-material-bindings-v1/RESULT.v1.json',
        'unity-delta-firepit-active-texture-storage-v1/RUN-RESULT.v1.json',
        'firepit-active-texture-gpu-probe-v1/PREPARED.v1.json',
        'firepit-active-texture-gpu-run-v2/PREPARED.v1.json',
        'firepit-active-texture-gpu-run-v2/RUN-RESULT.v1.json',
        'firepit-active-texture-gpu-run-v2/INDEPENDENT-REOPEN.v1.json',
        'unity-delta-firepit-ra-sampling-v1/RUN-RESULT.v1.json',
        'delta-firepit-native-sampling-reopen-v2/RUN-RESULT.v1.json',
        'delta-firepit-native-sampling-reopen-v2/INDEPENDENT-REOPEN.v1.json',
        'delta-firepit-ra-sampling-parity-v1/PREDECLARED.v1.json',
        'delta-firepit-ra-sampling-parity-v1/RUN-RESULT.v1.json',
        'delta-firepit-ra-sampling-parity-v1/COMPARISON.v1.json',
        'source-graph-production-color-mask-v1/PREPARED.v1.json',
        'source-graph-production-color-mask-check-v1/PREPARED.v1.json',
        'source-graph-production-color-mask-check-v1/RUN-RESULT.v1.json',
        'source-graph-production-color-mask-check-v2/PREPARED.v1.json',
        'source-graph-production-color-mask-check-v2/RUN-RESULT.v1.json',
        'source-graph-production-color-mask-v2/PREPARED.v1.json',
        'source-graph-production-color-mask-check-v3/PREPARED.v1.json',
        'source-graph-production-color-mask-check-v3/RUN-RESULT.v1.json',
        'delta-firepit-alpha-port-gpu-v1/PREPARED.v1.json',
        'delta-firepit-alpha-port-gpu-v1/RUN-RESULT.v1.json',
        'delta-firepit-alpha-port-gpu-v1/COMPARISON.v1.json',
        'unity-delta-firepit-legacy-shader-gpu-half-v3/RUN-RESULT.v1.json',
        'unity-delta-firepit-legacy-shader-gpu-half-v3/INDEPENDENT-REOPEN.v1.json',
        'delta-firepit-alpha-port-gpu-half-queue-v2/JOB.v1.json',
        'delta-firepit-alpha-port-gpu-half-v2/PREPARED.v1.json',
        'delta-firepit-alpha-port-gpu-half-v2/RUN-RESULT.v1.json',
        'delta-firepit-alpha-port-gpu-half-v2/COMPARISON.v1.json',
        'delta-firepit-vertex-color-hypotheses-v1/RESULT.v1.json',
        'delta-firepit-render-velocity-drift-v1/RESULT.v1.json',
        'delta-firepit-simulation-residual-v1/RESULT.v1.json',
        'delta-firepit-gradient-lookup-v1/PREDECLARED.v1.json',
        'delta-firepit-gradient-lookup-v1/RESULT.v1.json',
        'unity-delta-firepit-gradient-boundaries-v1/PREPARED.v1.json',
        'unity-delta-firepit-gradient-boundaries-v1/RUN-RESULT.v1.json',
        'unity-delta-firepit-gradient-boundaries-v1/INDEPENDENT-REOPEN.v1.json',
        'delta-firepit-single-pass-requirements-v1/RESULT.v1.json',
        'source-unlit-forward-native-check-v1/QUEUED.v1.json',
        'source-unlit-forward-native-check-v1/PREPARED.v1.json',
        'source-unlit-forward-native-check-v1/RUN-RESULT.v1.json',
        'unity-delta-firepit-native-ra-shader-v1/PREPARED.v1.json',
        'unity-delta-firepit-native-ra-shader-v1/RUN-RESULT.v1.json',
        'unity-delta-firepit-native-ra-shader-v1/INDEPENDENT-REOPEN.v1.json',
        'delta-firepit-ra-shader-gpu-v1/QUEUED.v1.json',
        'delta-firepit-ra-shader-gpu-v1/PREPARED.v1.json',
        'delta-firepit-ra-shader-gpu-v1/RUN-RESULT.v1.json',
        'delta-firepit-ra-shader-gpu-v1/COMPARISON.v1.json',
        'source-graph-color-write-mask-candidate-v1/PREPARED.v1.json',
        'source-graph-color-write-mask-native-check-v1/RUN-RESULT.v1.json',
        'source-graph-color-write-mask-native-check-v1/CONTROLLED-STOP.v1.json',
        'source-graph-color-write-mask-native-check-v2/RUN-RESULT.v1.json',
        'delta-rgb8-canonical-loader-native-v2/RUN-RESULT.v1.json',
        'delta-rgb8-frozen-canonical-loader-v5/RUN-RESULT.v1.json',
        'delta-rgb8-frozen-canonical-loader-v7/RUN-RESULT.v1.json',
        checkpoint.relative_to(W).as_posix(),
    ]
    evidence = []
    for name in names:
        path = W / name
        if path.is_file():
            data = path.read_bytes()
            evidence.append(dict(path=str(path), sha256=hashlib.sha256(data).hexdigest(),
                receipt=json.loads(data.decode('utf-8-sig'))))
    now = datetime.datetime.now(datetime.timezone.utc).isoformat()
    rgb_state = json.loads((W / 'delta-rgb8-frozen-canonical-loader-v7/RUN-RESULT.v1.json').read_text('utf-8-sig'))
    rgb_text = 'Trois modules installés ; 860 sources copiées et figées ; build v7 en cours.'
    if rgb_state.get('actual_host_tests_passed') == 17 and rgb_state.get('actual_exit_code') == 0:
        rgb_text = 'Trois modules RGB8 installés ; moteur réel compilé sur 860 sources figées, 17 tests du chargeur/scène réussis, tous les hashes du snapshot inchangés. Qualification sur ce snapshot précis ; GPU et Studio restant.'
    elif rgb_state.get('finished_utc'):
        rgb_text = 'Build v7 terminal non qualifié ; refus et sources figées conservés, diagnostic requis.'
    geometry = json.loads((W / 'unity-delta-firepit-particle-geometry-v1/RUN-RESULT.v1.json').read_text('utf-8-sig'))
    if geometry.get('native_geometry_verified'):
        geometry_text = f"{geometry['geometry_observations_verified']} observations natives réouvertes ; répétabilité exacte et 112 références temporelles inchangées."
    elif geometry.get('status') == 'refused_or_unverified':
        geometry_text = 'Capture non qualifiée : refus conservé dans le reçu ; corriger la cause avant toute admission.'
    else:
        geometry_text = 'Capture native en cours : 112 observations temporelles et trois caméras de mesure ; 336 observations attendues.'
    buffers_path = W / 'delta-firepit-geometry-buffers-v2/RESULT.v1.json'
    if buffers_path.is_file():
        buffers = json.loads(buffers_path.read_text('utf-8-sig'))
        geometry_text += f" Contrôle indépendant : {buffers['observations_verified']} buffers/matrices, {len(buffers['negative_controls'])} corruptions refusées."
    semantics_path = W / 'delta-firepit-baked-geometry-semantics-v1/RESULT.v1.json'
    semantics_text = 'Mesure des centres, tailles et couleurs en attente.'
    if semantics_path.is_file():
        semantics = json.loads(semantics_path.read_text('utf-8-sig'))
        semantics_text = (f"336 mesures : écart maximum entre centre de quad et position de particule {semantics['maximum_center_error']:.8f} unité source ; "
            f"écart de côté {semantics['maximum_side_error']:.8f} ; {semantics['color_difference_observations']} observations de couleur diffèrent de GetCurrentColor au-delà d’un pas de 8 bits. "
            'La cause reste non qualifiée ; les valeurs réelles BakeMesh sont conservées. Ne pas reconstruire le rendu à partir des seules valeurs GetParticles/GetCurrentColor.')
    bake_state = json.loads((W / 'unity-delta-firepit-bake-state-probe-v2/RUN-RESULT.v1.json').read_text('utf-8-sig'))
    bake_text = 'Mesure native v2 en cours : état vivant et clocks après chaque bake.'
    if bake_state.get('native_bake_state_probe_verified'):
        bake_text = (f"336 bakes et snapshots après bake réouverts : {bake_state['live_snapshot_change_observations']} changement d’état vivant. "
            'Les 112 références temporelles et 336 géométries précédentes restent identiques. La mutation par le bake est écartée dans cette portée ; interpolation/couleur restent à mesurer.')
    gpu_text = 'Capture GPU v2 lancée dans le contexte Built-in pris en charge : 16 fixtures avec shader original, couleurs BakeMesh originales, texture blanche temporaire sur clone, deux fonds d’alpha distinct. Contexte précédent HDRP restauré après mesure. Observations v1 conservées mais API de matrices non qualifiée sous HDRP.'
    gpu_path = W / 'unity-delta-firepit-legacy-shader-gpu-builtin-v2/RUN-RESULT.v1.json'
    if gpu_path.is_file():
        gpu_state = json.loads(gpu_path.read_text('utf-8-sig'))
        if gpu_state.get('native_shader_fixture_gpu_verified'):
            gpu_text = '16 fixtures du shader original réouvertes sur GPU Unity dans le contexte Built-in pris en charge ; alpha du fond préservé, réglages de pipeline restaurés. Texture blanche de mesure distincte de la texture source ; aucun matériau source modifié. Tentative HDRP précédente conservée séparément.'
        elif gpu_state.get('status') == 'refused_or_unverified':
            gpu_text = 'Capture GPU non qualifiée ; refus terminal conservé dans son reçu, à diagnostiquer.'
    tint_text = 'Comparaison des hypothèses de teinte en attente.'
    tint_path = W / 'delta-firepit-native-tint-gpu-v3/RESULT.v1.json'
    if tint_path.is_file():
        tint = json.loads(tint_path.read_text('utf-8-sig'))
        tint_text = (f"16 pixels natifs comparés : hypothèses admises {', '.join(tint['matching_hypotheses']) or 'aucune'}. "
            f"Écart maximal teinte brute {tint['maximum_rgb_errors']['raw_get_color']:.8f} ; teinte linéaire {tint['maximum_rgb_errors']['linearized_get_color']:.10f}. "
            'Deux bindings de matériau et deux buffers uniformes f32 de 48 octets préparés/réouverts avec texture active originale, documents sauvegardés et licences conservés.')
    texture_text = 'Capture native lancée : une texture RA active partagée par deux matériaux, PNG et .meta copiés exactement dans le projet de mesure. Helper et vérificateur existants réutilisés ; copie privée de métadonnées conservée avant/après import.'
    texture_path = W / 'unity-delta-firepit-active-texture-storage-v1/RUN-RESULT.v1.json'
    if texture_path.is_file():
        texture_state = json.loads(texture_path.read_text('utf-8-sig'))
        if texture_state.get('textures_verified') == 1:
            texture_text = (f"Une texture RA active et {texture_state['mips_verified']} mipmaps natifs réouverts ; format {texture_state['graphics_format']}. "
                f"Sources originales inchangées ; changement de la seule métadonnée de copie privée : {texture_state['changed_owned_private_importer_meta']}. Avant/après conservés.")
        elif texture_state.get('status') == 'refused_or_unverified':
            texture_text = 'Capture de texture non qualifiée ; refus et diagnostic conservés, à résoudre avant admission.'
    texture_gpu_text = 'Projet préparé pour une vraie texture RA et onze mips ; modules historiques de stockage/sampler réutilisés, cœur figé, Cargo/Rustc 1.98.0. Exécution en file après libération du cache par le build masque RGB ; aucun résultat GPU déclaré avant réouverture.'
    texture_gpu_path = W / 'firepit-active-texture-gpu-run-v2/RUN-RESULT.v1.json'
    if texture_gpu_path.is_file():
        texture_gpu_state = json.loads(texture_gpu_path.read_text('utf-8-sig'))
        if texture_gpu_state.get('mips_verified') == 11:
            texture_gpu_text = 'Une vraie texture RA et onze mips échantillonnés sur GPU DX12 puis réouverts : 45 056 texels, hashes/identités inchangés, valeurs finies et deux corruptions refusées. Modules historiques de stockage/sampler et cœur figé ; parité de sampling Unity et renderer actuel non qualifiés.'
        elif texture_gpu_state.get('finished_utc'):
            texture_gpu_text = 'Sous-lot GPU terminal non qualifié : refus et logs conservés. ' + texture_gpu_state.get('error', '')
        else:
            texture_gpu_text += ' État mesuré : ' + texture_gpu_state['status'] + '.'
    native_sampling_text = 'Capture réelle Unity D3D11 lancée : une texture originale RA, onze mips, grille 64×64, sampler de la texture native et importer inchangé. Réouverture v2 prévue : noms des enums de capture comparés aux entiers du reçu de stockage, sans changer les références ni les pixels.'
    native_sampling_path = W / 'delta-firepit-native-sampling-reopen-v2/RUN-RESULT.v1.json'
    if native_sampling_path.is_file():
        native_sampling_state = json.loads(native_sampling_path.read_text('utf-8-sig'))
        if native_sampling_state.get('mips_verified') == 11:
            native_sampling_text = 'Onze mips réels échantillonnés sur GPU Unity D3D11, 45 056 texels réouverts ; texture, importer et sampler inchangés. Représentation des enums normalisée dans un reçu indépendant, première vérification conservée. Comparaison avec Forge encore requise.'
        elif native_sampling_state.get('finished_utc'):
            native_sampling_text = 'Référence native de sampling non qualifiée ; refus conservé : ' + native_sampling_state.get('error', '')
    sampling_comparison_text = 'Comparaison en file après réouverture des deux captures : tous les 45 056 texels / 180 224 canaux, onze mips. Tolérance 0,00001 déclarée avant résultat ; hypothèses coordonnées directes et retournement vertical séparées, aucun retournement silencieux.'
    sampling_comparison_path = W / 'delta-firepit-ra-sampling-parity-v1/RUN-RESULT.v1.json'
    if sampling_comparison_path.is_file():
        comparison_state = json.loads(sampling_comparison_path.read_text('utf-8-sig'))
        if comparison_state.get('source_sampling_qualified'):
            sampling_comparison_text = (f"45 056 texels GPU Unity/Forge comparés, écart maximal direct {comparison_state['direct_maximum_error']:.10f}. "
                f"Orientation qualifiée dans cette portée : {comparison_state['source_orientation_qualified']}. Modules de sampler historiques ; shader, renderer complet et prefab non qualifiés.")
        elif comparison_state.get('finished_utc'):
            sampling_comparison_text = 'Comparaison refusée/non qualifiée ; diagnostic conservé : ' + comparison_state.get('error', '')
    production_mask_text = 'Six fichiers préparés isolément avec préimages : contrat schema 4 à masque obligatoire, transport GLB, programme, clé Eq/Hash et spécialisation réelle du forward. Schemas 2/3 gardent leur ABI ALL existante ; canaux inconnus et mauvais schema refusés. Moteur figé de 861 fichiers et tests réels en file après le build GPU ; aucune installation produit.'
    production_mask_path = W / 'source-graph-production-color-mask-check-v2/RUN-RESULT.v1.json'
    if (W / 'source-graph-production-color-mask-check-v3/RUN-RESULT.v1.json').is_file():
        production_mask_path = W / 'source-graph-production-color-mask-check-v3/RUN-RESULT.v1.json'
        production_mask_text = 'Sept fichiers préparés isolément : raccordement du masque RGB et constructeur du test Studio conservant ALL=15. Snapshot v3 indépendant ; 17 tests Studio et tests renderer relancés, aucune assertion retirée. Installation produit restant.'
    if (W / 'source-graph-production-color-mask-check-v4/RUN-RESULT.v1.json').is_file():
        production_mask_path = W / 'source-graph-production-color-mask-check-v4/RUN-RESULT.v1.json'
        production_mask_text = '17 tests Studio réussis et conservés. Refus du verrou de dépendances v3 conservé ; snapshot v4 séparé, résolution hors ligne journalisée avant tests moteur --locked. Aucun asset livré par ce contrôle ; installation restant.'
    if production_mask_path.is_file():
        production_mask_state = json.loads(production_mask_path.read_text('utf-8-sig'))
        if production_mask_state.get('actual_renderer_tests_passed'):
            production_mask_text = (f"17 tests Studio host et {production_mask_state['actual_renderer_tests_passed']} tests renderer source_graph réussis sur le raccordement figé. "
                'Installation produit et comparaison du port shader sur GPU restant.')
        elif production_mask_state.get('finished_utc'):
            production_mask_text = 'Raccordement non qualifié ; refus de compilation/contrôle conservé : ' + production_mask_state.get('error', '')
    if ((W / 'source-graph-production-color-mask-v2/PREPARED.v1.json').is_file()
            and not (W / 'source-graph-production-color-mask-check-v3/RUN-RESULT.v1.json').is_file()):
        production_mask_text = 'Sept fichiers corrigés et préparés isolément. Copie du snapshot v3 en préparation avant les 17 tests Studio et les tests renderer ; test Studio conservant son ancien masque ALL=15, aucune assertion retirée. Installation produit restant.'
    production_mask_text += ' Refus v1 de workspace et v2 de champ absent dans le test Studio conservés ; correction v3 isolée, mêmes assertions.'
    alpha_port_text = 'Rendu GPU du port lancé/en file : deux vrais buffers matériau de 48 octets, seize fixtures natives conservées, cible float32, blend SrcAlpha/OneMinusSrcAlpha et masque RGB matériel. Texture blanche linéaire de mesure ; texture RA, billboards, soft particles et brouillard exclus de cette portée.'
    alpha_port_path = W / 'delta-firepit-alpha-port-gpu-v1/RUN-RESULT.v1.json'
    if alpha_port_path.is_file():
        alpha_port_state = json.loads(alpha_port_path.read_text('utf-8-sig'))
        if alpha_port_state.get('fixtures_verified') == 16:
            alpha_port_text = (f"16 fixtures du shader original comparées au port réellement rendu sur GPU DX12 ; écart maximal {alpha_port_state['maximum_rgba_error']:.10f}, "
                'alpha du fond préservé. Uniformes originaux utilisés ; texture blanche linéaire de mesure. Pas de qualification du prefab ni de la texture RA dans le shader.')
        elif alpha_port_state.get('finished_utc'):
            alpha_port_text = 'Port GPU non qualifié : ' + alpha_port_state.get('error', '') + ' Refus et comparaison conservés.'
    alpha_port_text += ' Refus RGBA32Float conservé : FLOAT32_BLENDABLE absent sur DX12. Nouvelle référence Unity ARGBHalf lancée, format HDR RGBA16 réellement utilisé par Forge ; port v2 en file, mêmes shaders/fixtures et tolérance 0,00001, readback brut f16 conservé et décodage f32 contrôlé indépendamment.'
    alpha_half_path = W / 'delta-firepit-alpha-port-gpu-half-v2/RUN-RESULT.v1.json'
    if alpha_half_path.is_file():
        alpha_half_state = json.loads(alpha_half_path.read_text('utf-8-sig'))
        if alpha_half_state.get('fixtures_verified') == 16:
            alpha_port_text = (f"Port RGBA16 réellement rendu sur GPU DX12 et comparé à Unity ARGBHalf : 16 fixtures réouvertes, écart maximal {alpha_half_state['maximum_rgba_error']:.10f}, alpha du fond préservé. "
                'Buffers matériau originaux, readback brut f16 conservé et décodage contrôlé indépendamment. Texture blanche linéaire de mesure ; vraie texture RA dans le shader, billboards, variantes et effet complet restant. Refus RGBA32Float précédent conservé.')
        elif alpha_half_state.get('finished_utc'):
            alpha_port_text += ' Refus HDR16 conservé : ' + alpha_half_state.get('error', '')
    native_half_path = W / 'unity-delta-firepit-legacy-shader-gpu-half-v3/RUN-RESULT.v1.json'
    if native_half_path.is_file():
        native_half_state = json.loads(native_half_path.read_text('utf-8-sig'))
        if native_half_state.get('native_shader_fixture_gpu_verified'):
            alpha_port_text += ' Référence Unity ARGBHalf vérifiée : 16 fixtures réouvertes, contexte Built-in et restauration des réglages confirmés.'
    render_time_text = 'Mesures de couleur et de positions natives en cours.'
    ra_shader_text = 'Capture Unity du shader original avec la vraie texture RA liée, seize images HDR de 16×16 pixels, puis comparaison GPU DX12 en file après les contrôles matériau. Tous les 4 096 pixels et buffers f16 seront réouverts ; tolérance 0,00001 inchangée, hypothèses coordonnées directes et retournement vertical séparées. Modules de stockage/sampler historiques, aucun remplacement de texture ni qualification de l’effet complet.'
    native_ra_path = W / 'unity-delta-firepit-native-ra-shader-v1/RUN-RESULT.v1.json'
    if native_ra_path.is_file():
        native_ra = json.loads(native_ra_path.read_text('utf-8-sig'))
        if native_ra.get('native_shader_fixture_gpu_verified'):
            ra_shader_text += ' Référence native réouverte avec texture, sampler, identités et alpha du fond vérifiés.'
        elif native_ra.get('finished_utc'):
            ra_shader_text += ' Capture native refusée ; preuve conservée : ' + native_ra.get('error', '')
    ra_path = W / 'delta-firepit-ra-shader-gpu-v1/RUN-RESULT.v1.json'
    if (W / 'delta-firepit-ra-shader-gpu-v2/RUN-RESULT.v1.json').is_file():
        ra_path = W / 'delta-firepit-ra-shader-gpu-v2/RUN-RESULT.v1.json'
        ra_shader_text += ' Correction v2 des deux commentaires Rust après le refus de build v1, conservé ; shader, vraie texture et tolérance inchangés.'
    if ra_path.is_file():
        ra = json.loads(ra_path.read_text('utf-8-sig'))
        if ra.get('fixture_shader_actual_ra_gpu_parity_verified'):
            ra_shader_text = (f"Shader original avec texture RA comparé sur 4 096 pixels GPU Unity/DX12 ; écart maximal {ra['maximum_rgba_error']:.10f}, "
                f"correspondance de coordonnées des fixtures : {ra['fixture_coordinate_mapping']}. Alpha préservé ; modules de transport historiques. Matériau actuel, billboards, variantes, effet complet et Studio restant.")
        elif ra.get('finished_utc'):
            ra_shader_text += ' Comparaison refusée ; diagnostic conservé : ' + ra.get('error', '')
    program_text = 'Constructeur générique du programme forward-only préparé : admission SHA atomique, masque RGB, phase opaque/cutout/transparente et déclarations de textures conservés. Trois tests supplémentaires en file après les quatre tests matériau et les 17 tests Studio ; 24 assertions attendues au total. Installation et raccordement du chargeur restant.'
    program_path = W / 'source-unlit-program-native-check-v1/RUN-RESULT.v1.json'
    if program_path.is_file():
        program_state = json.loads(program_path.read_text('utf-8-sig'))
        if program_state.get('actual_program_tests_passed') == 3:
            program_text = 'Constructeur forward-only : trois tests programme, quatre tests matériau et 17 tests Studio réussis. Enregistrement des vrais Assets<Shader>, reimport et refus sans mutation contrôlés. Sources figées ; installation, chargeur et rendu réel restant.'
        elif program_state.get('finished_utc'):
            program_text += ' Contrôle refusé ; diagnostic conservé : ' + program_state.get('error', '')
    color_hypotheses_path = W / 'delta-firepit-vertex-color-hypotheses-v1/RESULT.v1.json'
    residual_path = W / 'delta-firepit-simulation-residual-v1/RESULT.v1.json'
    if color_hypotheses_path.is_file() and residual_path.is_file():
        color_hypotheses = json.loads(color_hypotheses_path.read_text('utf-8-sig'))
        residual = json.loads(residual_path.read_text('utf-8-sig'))
        render_time_text = (f"336 buffers et {color_hypotheses['quad_color_samples']} couleurs de quad mesurés. "
            'Ni couleur brute ni seule conversion sRGB→linéaire ne correspondent à tous les buffers natifs. '
            + '; '.join(f"{row['node_path']} : prédiction par reliquat de temps, écart maximal de position {row['maximum_residual_prediction_error']:.10f}" for row in residual['node_measurements'])
            + '. La cause, les forces et le clock interne restent non qualifiés ; aucune correction runtime installée.')
    gradient_path = W / 'delta-firepit-gradient-lookup-v1/RESULT.v1.json'
    if gradient_path.is_file():
        measured_gradient = json.loads(gradient_path.read_text('utf-8-sig'))
        best_name = measured_gradient['ranked_hypotheses'][0]
        best = measured_gradient['hypotheses'][best_name]
        render_time_text += (f" Gradients hérités testés sur {measured_gradient['quad_color_samples']} couleurs natives : "
            f"meilleure hypothèse {best_name}, {best['within_one_byte_quads']} valeurs à un octet près, écart maximal {best['maximum_byte_error']} octets. "
            'Aucune hypothèse ne couvre tous les cas au seuil déclaré ; tables/couleurs et runtime non qualifiés.')
    boundary_prepared_path = W / 'unity-delta-firepit-gradient-boundaries-v1/PREPARED.v1.json'
    if (W / 'unity-delta-firepit-gradient-boundaries-v2/PREPARED.v1.json').is_file():
        boundary_prepared_path = W / 'unity-delta-firepit-gradient-boundaries-v2/PREPARED.v1.json'
    if boundary_prepared_path.is_file():
        boundary_prepared = json.loads(boundary_prepared_path.read_text('utf-8-sig'))
        render_time_text += (f" Mesure native ciblée des bornes du gradient lancée : {boundary_prepared['expected_frames']} observations attendues, "
            'une particule fournie explicitement par clone du prefab, paramètres/courbes d’origine inchangés, deux répétitions et âges f32 aux bornes. Aucun remplacement de la simulation autonome.')
    boundary_path = W / 'unity-delta-firepit-gradient-boundaries-v1/RUN-RESULT.v1.json'
    if (W / 'unity-delta-firepit-gradient-boundaries-v2/RUN-RESULT.v1.json').is_file():
        boundary_path = W / 'unity-delta-firepit-gradient-boundaries-v2/RUN-RESULT.v1.json'
        render_time_text += ' Refus v1 conservé ; v2 initialise le clone natif par Simulate(0), garde le contrôle d’une particule et ajoute node/âge/compte au diagnostic.'
    if (W / 'firepit-gradient-boundaries-independent-reopen-v4/RUN-RESULT.v1.json').is_file():
        boundary_path = W / 'firepit-gradient-boundaries-independent-reopen-v4/RUN-RESULT.v1.json'
        render_time_text += ' Capture v2 conservée : réouverture v4 avec arrondi final exactement mesuré sur 4096 âges ; contrôle de l’ordre des âges et corruption d’un ULP conservés. Refus des calculs intermédiaires v2/v3 conservés, cause interne non qualifiée.'
    if boundary_path.is_file():
        boundary = json.loads(boundary_path.read_text('utf-8-sig'))
        if boundary.get('native_gradient_lookup_probe_verified'):
            render_time_text += f" Bornes natives réouvertes : {boundary['boundary_frames_verified']} observations répétables ; comparaison et conversion restant."
        elif boundary.get('finished_utc'):
            render_time_text += ' Mesure de bornes refusée ; preuve conservée : ' + boundary.get('error', '')
    color_path = W / 'firepit-gradient-boundary-color-paths-v1/RESULT.v1.json'
    if color_path.is_file():
        color_state = json.loads(color_path.read_text('utf-8-sig'))
        best_name = color_state['ranked_hypotheses'][0]
        best = color_state['hypotheses'][best_name]
        render_time_text += (f" Chemins de couleur mesurés sur {color_state['native_quads']} quads de bornes : meilleure hypothèse {best_name}, "
            f"{best['exact']} correspondances exactes, maximum {best['maximum_byte_error']} octets. Aucune correction runtime installée ; simulation autonome restant.")
    single_pass_text = 'Règles de passes originales à vérifier avant admission du matériau legacy.'
    single_pass_path = W / 'delta-firepit-single-pass-requirements-v1/RESULT.v1.json'
    if single_pass_path.is_file():
        single_pass_text = 'Deux renderers natifs vérifiés : une passe shader originale, aucun Fallback/UsePass, shadow_casting=0 et receive_shadows=false. Le contrat Graph actuel exige une prépasse ; admission générique forward-only distincte nécessaire, sans inventer de prépasse ni traiter les particules comme un mesh statique. Type matériau et runtime pas encore implémentés.'
    unlit_path = W / 'source-unlit-forward-native-check-v1/RUN-RESULT.v1.json'
    if (W / 'source-unlit-forward-native-check-v2/RUN-RESULT.v1.json').is_file():
        unlit_path = W / 'source-unlit-forward-native-check-v2/RUN-RESULT.v1.json'
    if unlit_path.is_file():
        unlit = json.loads(unlit_path.read_text('utf-8-sig'))
        single_pass_text = ('Candidat générique forward-only préparé : aucune prépasse/ombre, UV0 float4 et couleurs sources exigées, aucune tangente inventée. '
            'Bindings GPU réutilisés, masque RGB dans la clé, culling des vues réfléchies conservé. ')
        if unlit.get('actual_material_tests_passed') == 4:
            single_pass_text += 'Quatre tests du matériau Bevy réel et les 17 tests Studio réussis sur le moteur figé ; installation, shader complet et runtime restant.'
        elif unlit.get('finished_utc'):
            single_pass_text += 'Contrôle refusé, preuves conservées : ' + unlit.get('error', '')
        else:
            single_pass_text += 'Quatre contrôles matériau et les 17 tests Studio conservés sont en file après le masque moteur v3 ; aucune installation ni qualification runtime.'
    mask_text = 'Compilation v2 réelle lancée avec Cargo/Rustc 1.98.0 épinglés, même moteur figé et dépendances déjà testés. La tentative stable v1 a été arrêtée après vérification de propriété des seuls PID du build ; logs/fichiers et reçu terminal conservés. Deux contrôles RGB et quatre contrôles du forward-state original attendus. Code produit conservé.'
    mask_path = W / 'source-graph-color-write-mask-native-check-v2/RUN-RESULT.v1.json'
    if mask_path.is_file():
        mask_state = json.loads(mask_path.read_text('utf-8-sig'))
        if mask_state.get('tests_passed') == 6:
            mask_text = 'Six tests du forward-state original et du candidat masque RGB réussis sur le moteur figé ; sources épinglées inchangées. GPU et spécialisation réelle restent à qualifier ; code produit conservé.'
        elif mask_state.get('status') == 'refused_or_unverified':
            mask_text = 'Compilation/contrôles non qualifiés : refus terminal conservé, correction du candidat requise avant admission.'
    install_mask_path = W / 'source-graph-production-color-mask-install-v1/PREPARED.v1.json'
    if install_mask_path.is_file():
        production_mask_text += ' Installation préparée sur sept fichiers : préimages et candidats vérifiés, sauvegardes prévues ; application conditionnée aux tests moteur réels. Aucun code produit remplacé par cette préparation.'
    installed_mask_path = W / 'source-graph-production-color-mask-install-v1/INSTALL.v1.json'
    if installed_mask_path.is_file():
        installed_mask = json.loads(installed_mask_path.read_text('utf-8-sig'))
        if installed_mask.get('installed_files') == 7:
            production_mask_text += ' Sept fichiers testés installés dans le produit avec sauvegardes et gardes de préimages ; build du produit courant, GPU et assets complets restant à vérifier.'
        else:
            production_mask_text += ' Installation non fermée ; journal de chaque fichier conservé.'
    elif (W / 'source-graph-production-color-mask-install-queue-v1/RUN-RESULT.v1.json').is_file():
        production_mask_text += ' Application en file après fermeture des deux consommateurs des anciennes préimages ; aucune installation anticipée.'
    null_mask_text = 'Préparation du contrôle natif Detail : sources originales conservées et matériaux privés migrés conservés séparément avec leurs différences. Aucun remplacement de la texture source absente.'
    null_mask_path = W / 'unity-source-null-mask-hdrp-v2/RUN-RESULT.v1.json'
    if null_mask_path.is_file():
        null_mask_state = json.loads(null_mask_path.read_text('utf-8-sig'))
        if null_mask_state.get('native_default_render_behavior_verified'):
            null_mask_text = f"Comportement du masque nul mesuré sur {null_mask_state['images_verified']} images natives ; comparaison blanc/noir réouverte. Matériaux privés migrés conservés avec leurs différences ; contexte complet du prefab non qualifié."
        elif null_mask_state.get('finished_utc'):
            null_mask_text = 'Contrôle natif refusé, sources et diagnostic conservés : ' + null_mask_state.get('error', '')
        else:
            null_mask_text += ' Capture Unity en cours ; aucune parité de rendu encore annoncée.'
    if (W / 'unity-source-null-mask-hdrp-v3/TARGET-MIGRATION-DIAGNOSIS.v1.json').is_file():
        null_mask_text += ' Douze images natives réouvertes : répétabilité refusée. Préparation v3 arrêtée après migration native de la copie privée Detail ; versions auteur/native et différences conservées sans écrasement. Sept différences de propriétés scalaires documentées ; aucun binding ou défaut de pixels qualifié.'
    latest_null_mask_path = W / 'unity-source-null-mask-hdrp-v5/RUN-RESULT.v1.json'
    if latest_null_mask_path.is_file():
        latest_null_mask = json.loads(latest_null_mask_path.read_text('utf-8-sig'))
        null_mask_text += ' Refus v4 sans exposition/post-traitement conservé. Mesure v5 dans une scène de prévisualisation isolée, scène active conservée et paramètres de temps HDRP relevés ; matériau effectif précisément épinglé, migration du matériau auteur et prefab complet non qualifiés.'
        if latest_null_mask.get('native_default_render_behavior_verified'):
            null_mask_text += ' Douze images répétables et contrôle blanc/noir réussis uniquement pour ce binding effectif ; raccordement Forge restant.'
        elif latest_null_mask.get('finished_utc'):
            null_mask_text += ' Mesure isolée refusée : ' + latest_null_mask.get('error', '')
        else:
            null_mask_text += ' Capture isolée en cours.'
    clock_mask_path = W / 'unity-source-null-mask-hdrp-v6/RUN-RESULT.v1.json'
    if clock_mask_path.is_file():
        clock_mask = json.loads(clock_mask_path.read_text('utf-8-sig'))
        null_mask_text += ' Refus v5 conservé. HDRP transporte le temps dans un constant buffer de caméra : les globals Shader lus à zéro ne qualifient pas ce clock. V6 utilise la vraie caméra CustomUserHistory0 du StandardRequest, fixe son clock diagnostique à zéro et vérifie son identité/clock effectif après chaque rendu.'
        if clock_mask.get('native_default_render_behavior_verified'):
            null_mask_text += ' Douze images répétables et comparaison blanc/noir réussies dans cette portée effective ; migration, rendu original complet et binding Forge restant.'
        elif clock_mask.get('finished_utc'):
            null_mask_text += ' Contrôle du clock/rendu refusé : ' + clock_mask.get('error', '')
        else:
            null_mask_text += ' Contrôle natif du clock en cours.'
    canonical_unlit_path = W / 'source-unlit-canonical-native-check-v1/RUN-RESULT.v1.json'
    if canonical_unlit_path.is_file():
        canonical_unlit = json.loads(canonical_unlit_path.read_text('utf-8-sig'))
        canonical_text = 'Contrôle matériau et constructeur dans le vrai workspace moteur : les 68 tests source_graph sont conservés, sept contrôles matériau/programme ajoutés ; reçu séparé des 17 tests Studio conservé. Refus Serde du host séparé conservé. Aucun module unlit installé ni asset qualifié par cette préparation.'
        if canonical_unlit.get('actual_program_tests_passed') == 3:
            canonical_text = f"Quatre tests matériau, trois tests constructeur et les 68 anciens tests moteur conservés ont réussi ; {canonical_unlit['actual_engine_tests_passed']} tests source au total. Reçu séparé des 17 tests Studio conservé. Installation du forward-only et usage réel du chargeur restant."
        elif canonical_unlit.get('finished_utc'):
            canonical_text += ' Contrôle canonique refusé : ' + canonical_unlit.get('error', '')
        else:
            canonical_text += ' Build canonique en cours.'
        single_pass_text = canonical_text
        program_text = canonical_text
    targeted_path = W / 'source-unlit-targeted-native-check-v2/RUN-RESULT.v1.json'
    if targeted_path.is_file():
        targeted = json.loads(targeted_path.read_text('utf-8-sig'))
        canonical_text = 'Binaire du vrai moteur compilé ; refus précédent conservé : cinq tests Tidewater inclus par le filtre large manquaient de données natives. Reprise ciblée Hivemind sur le même binaire et les mêmes sources épinglées, sans nouvelle compilation ni modification de Tidewater.'
        if targeted.get('status') == 'actual_engine_material_and_factory_tests_passed':
            canonical_text = f"{targeted['actual_engine_tests_passed']} tests Hivemind du vrai moteur réussis : les 68 anciens contrôles et les sept nouveaux contrôles matériau/constructeur conservés. Binaire et sources épinglés inchangés ; 17 tests Studio réutilisés depuis leur reçu séparé. Refus du filtre large conservé. Installation forward-only et usage réel du chargeur restant."
        elif targeted.get('finished_utc'):
            canonical_text += ' Reprise refusée : ' + targeted.get('error', '')
        else:
            canonical_text += ' Contrôle ciblé en cours.'
        single_pass_text = program_text = canonical_text
    forward_install_path = W / 'source-unlit-forward-install-v1/INSTALL.v1.json'
    if forward_install_path.is_file():
        forward_install = json.loads(forward_install_path.read_text('utf-8-sig'))
        if forward_install.get('installed_files') == 3:
            single_pass_text = single_pass_text.replace('Installation forward-only et usage réel du chargeur restant.', 'Usage réel du chargeur restant.')
            program_text = program_text.replace('Installation forward-only et usage réel du chargeur restant.', 'Usage réel du chargeur restant.')
            installed_text = ' Les deux modules forward-only et leur raccordement plugin testés sont installés dans le produit ; sauvegarde de la préimage, gardes SHA et reçu conservés. Prochaine étape : build du produit courant, transport/chargeur et rendu réel de FirePit. Aucun effet complet livré par cette installation.'
            single_pass_text += installed_text
            program_text += installed_text
    if installed_mask_path.is_file() and installed_mask.get('installed_files') == 7:
        production_mask_text = 'Sept fichiers testés installés avec sauvegardes et gardes de préimages. Référence figée : 68 tests renderer et 17 tests Studio réussis. Build du produit courant, rendu GPU et assets complets restent à vérifier.'
    if (W / 'unity-source-null-mask-hdrp-v7/RUN-RESULT.v1.json').is_file():
        null_mask_text = 'V7 : 18 captures natives conservées, dont six préchauffages ; répétabilité refusée malgré le clock contrôlé. Aucun défaut de pixels qualifié. Versions auteur et matériau privé migré conservées séparément. Analyse du graphe original : sept nœuds accessibles depuis _Mask, sorties Occlusion et Smoothness ; portée statique seulement. Mesure native ciblée de ces données matérielles à préparer.'
    surface_path = W / 'unity-source-null-mask-surface-v8/RUN-RESULT.v1.json'
    if surface_path.is_file():
        surface = json.loads(surface_path.read_text('utf-8-sig'))
        null_mask_text = 'Mesure native V8 des deux sorties réelles HDRP Smoothness/Occlusion (1005/1006) : 36 captures attendues, 12 préchauffages conservés et 24 comparaisons null/blanc/noir/répétition. État : ' + surface['status'] + '. Matériau effectif migré épinglé ; versions auteur conservées, contexte éclairé et prefab complet non qualifiés.'
        independent_path = W / 'detail-mask-surface-independent-reopen-v1/RESULT.v1.json'
        if independent_path.is_file():
            independent = json.loads(independent_path.read_text('utf-8-sig'))
            if independent.get('native_surface_default_qualified'):
                null_mask_text = 'V8 : 36 captures réouvertes indépendamment, répétition exacte et défaut nul équivalent au blanc sur Smoothness/Occlusion ; couverture noire limitée à un pixel par sortie. Sources épinglées inchangées et debug restauré. Qualification limitée aux données de surface mesurées ; migration auteur, rendu éclairé, binding Forge et prefab complet restant.'
            else:
                null_mask_text += ' Réouverture indépendante refusée : ' + independent.get('error', '')
    focused_surface_path = W / 'unity-source-null-mask-surface-v9/RUN-RESULT.v1.json'
    if focused_surface_path.is_file():
        focused = json.loads(focused_surface_path.read_text('utf-8-sig'))
        null_mask_text += ' V9 : caméra cadrée sur les indices du sous-mesh original, géométrie et slots intacts ; 36 captures attendues et minimum indépendant de 32 pixels noirs positifs par sortie. État : ' + focused['status'] + '.'
        focused_reopen_path = W / 'detail-mask-surface-independent-reopen-v2/RESULT.v1.json'
        if focused_reopen_path.is_file():
            focused_reopen = json.loads(focused_reopen_path.read_text('utf-8-sig'))
            if focused_reopen.get('native_surface_default_qualified'):
                counts = focused_reopen['modes']
                null_mask_text += f" Réouverture ciblée réussie : {counts['smoothness']['black_control_changed_pixels']} pixels Smoothness et {counts['occlusion']['black_control_changed_pixels']} pixels Occlusion ; répétition exacte, nul/blanc identiques. Rendu éclairé et binding Forge non qualifiés."
            else:
                null_mask_text += ' Réouverture ciblée refusée : ' + focused_reopen.get('error', '')
    high_surface_path = W / 'unity-source-null-mask-surface-v10/RUN-RESULT.v1.json'
    if high_surface_path.is_file():
        high_surface = json.loads(high_surface_path.read_text('utf-8-sig'))
        null_mask_text += ' V10 : même sous-mesh original cadré, readback 128×128 ; le minimum de 32 pixels positifs par sortie est conservé. État : ' + high_surface['status'] + '.'
        high_reopen_path = W / 'detail-mask-surface-independent-reopen-v3/RESULT.v1.json'
        if high_reopen_path.is_file():
            high_reopen = json.loads(high_reopen_path.read_text('utf-8-sig'))
            if high_reopen.get('native_surface_default_qualified'):
                counts = high_reopen['modes']
                null_mask_text += f" 36 images/589824 pixels réouverts : {counts['smoothness']['black_control_changed_pixels']} pixels positifs Smoothness et {counts['occlusion']['black_control_changed_pixels']} Occlusion ; répétition exacte et nul/blanc identiques. Binding Forge et prefab complet restant."
            else:
                null_mask_text += ' Réouverture haute résolution refusée : ' + high_reopen.get('error', '')
    integrity_path = W / 'asset-library-indexed-payload-integrity-resume-v2/RUN-RESULT.v1.json'
    integrity_text = 'Contrôle des payloads et archives indexés à reprendre ; preuves de transfert historiques conservées.'
    if integrity_path.is_file():
        integrity = json.loads(integrity_path.read_text('utf-8-sig'))
        integrity_text = 'Audit précédent arrêté sans reçu final ; reprise du binaire épinglé après contrôle des sources, sans écriture du Vault. État : ' + integrity['status'] + '. Ce contrôle ne qualifie pas à lui seul tous les fichiers actuels du Vault.'
    detail_kernel_text = 'Conversion du kernel Detail restante.'
    detail_kernel_path = W / 'detail-native-default-kernel-v1/RESULT.v1.json'
    if detail_kernel_path.is_file():
        detail_kernel = json.loads(detail_kernel_path.read_text('utf-8-sig'))
        detail_kernel_text = f"Kernel original : {detail_kernel['active_nodes']} nœuds/{detail_kernel['original_terminals']} sorties ; Naga exit={detail_kernel['actual_naga_exit_code']}. Entrée explicite native_default_mask_rg, deux textures sources conservées et référence manquante retenue ; aucune valeur RG inventée ni texture substituée."
        if (W / 'detail-native-default-stages-v1/RESULT.v1.json').is_file():
            detail_stages = json.loads((W / 'detail-native-default-stages-v1/RESULT.v1.json').read_text('utf-8-sig'))
            detail_kernel_text += f" Stages vertex/surface : Naga exit={detail_stages['actual_naga_exit_code']}, 23 dépendances de déplacement conservées. Binding du défaut, stockage/décodage normal et compositions Bevy complètes restant."
        if (W / 'detail-native-default-stages-negative-controls-v1/RESULT.v1.json').is_file():
            detail_kernel_text += ' Deux mutations supprimant/renommant le champ de défaut refusées par le vrai compilateur Naga.'
        if (W / 'detail-shader-program-licenses-v1/RESULT.v1.json').is_file():
            detail_kernel_text += ' Licences exactes des helpers Unity jointes aux trois dossiers de dérivés ; droits achetés conservés séparément.'
        gain_path = W / 'detail-default-native-gain-v11/RUN-RESULT.v1.json'
        if gain_path.is_file():
            gain = json.loads(gain_path.read_text('utf-8-sig'))
            detail_kernel_text += ' Mesure RG non clampée : clones diagnostiques Roughness=0.125 et AOIntensity=0.25, matériau auteur inchangé. État : ' + gain['status'] + '.'
        gain_binding_path = W / 'detail-default-native-rg-binding-v1/RESULT.v1.json'
        if gain_binding_path.is_file():
            gain_binding = json.loads(gain_binding_path.read_text('utf-8-sig'))
            if gain_binding.get('native_default_rg_binding_qualified'):
                detail_kernel_text += ' RG natifs mesurés par réponse non clampée : [1,1], packet f32 conservé ; intégration du packet au renderer et autres bindings restant.'
            else:
                detail_kernel_text += ' Mesure RG refusée : ' + gain_binding.get('error', '')
        if (W / 'detail-debug-output-encoding-diagnosis-v1/RESULT.v1.json').is_file():
            detail_kernel_text += ' Cause partiellement identifiée dans le SDK primaire : la sortie de debug applique SRGBToLinear ; le readback ne donne pas directement la valeur matérielle. Prochaine mesure : voie AOV native, qui évite cette conversion. Aucun packet RG utilisé. Adaptateur Bevy/binding 120 préparé et conditionné au packet réellement qualifié.'
    library_text = f'{checkpoint_count:,} fiches et images réouvertes indépendamment sur 26 735 ; audit global actif.'
    available_images_path = W / 'library-available-images-independent-reopen-v2/RESULT.v1.json'
    if available_images_path.is_file():
        available = json.loads(available_images_path.read_text('utf-8-sig'))
        if available.get('cards_checked'):
            library_text = f"{available['cards_checked']} fiches réouvertes, {available['images_checked']} références à images vérifiées ({available['distinct_images_checked']} fichiers distincts), {available['missing_images']} fiches sans image conservées avec leurs IDs. Couverture attendue : 26 735 fiches. Rendu pertinent et livraison complète non qualifiés."
        else:
            library_text += ' Réouverture séparant les images disponibles des fiches sans aperçu en cours ; aucune absence comptée comme image.'
    incremental_path = W / 'library-incremental-pages-reopen-v3/RESULT.v1.json'
    aov_path = W / 'detail-native-normal-loop-aov-v12/RUN-RESULT.v1.json'
    if (W / 'detail-native-normal-loop-aov-v13/RUN-RESULT.v1.json').is_file():
        aov_path = W / 'detail-native-normal-loop-aov-v13/RUN-RESULT.v1.json'
    if (W / 'detail-native-normal-loop-aov-v14/RUN-RESULT.v1.json').is_file():
        aov_path = W / 'detail-native-normal-loop-aov-v14/RUN-RESULT.v1.json'
    if (W / 'detail-native-normal-loop-aov-v15/RUN-RESULT.v1.json').is_file():
        aov_path = W / 'detail-native-normal-loop-aov-v15/RUN-RESULT.v1.json'
    if (W / 'detail-native-normal-loop-aov-v16/RUN-RESULT.v1.json').is_file():
        aov_path = W / 'detail-native-normal-loop-aov-v16/RUN-RESULT.v1.json'
    if aov_path.is_file():
        aov_run = json.loads(aov_path.read_text('utf-8'))
        detail_kernel_text += ' Capture AOV dans la boucle native normale, sortie flottante et scène privée ; état enregistré : ' + aov_run['status'] + '. Refus de compilation v12 conservé, namespace corrigé en v13 ; refus de scène initiale sans nom conservé, garde de scène vide ajoutée en v14. Aucun succès déduit du seul démarrage Unity.'
        detail_kernel_text += ' V14 arrêté uniquement après identification de l’exception HDRP Input.mousePosition répétée, zéro image ; reçu de contrôle conservé. V15 utilise les deux API d’entrée dans le seul projet privé avec préimage et restauration gardée. Payload auteur Detail inchangé octet par octet, seule version interne HDRP 12→13 ; autre copie matérielle privée retenue comme contexte non qualifié.'
        if (W / 'detail-native-normal-loop-aov-v16/PREPARED.v1.json').is_file():
            detail_kernel_text += ' V15 : 36 images produites mais reçu final refusé après lecture du pipeline démonté ; captures conservées, configuration privée restaurée. V16 conserve l’identité réelle du pipeline avant démontage et rejoue les mêmes contrôles.'
    measured_binding_path = W / 'detail-default-native-rg-binding-v2/RESULT.v1.json'
    if measured_binding_path.is_file():
        measured_binding = json.loads(measured_binding_path.read_text('utf-8'))
        if measured_binding.get('native_default_rg_binding_qualified'):
            detail_kernel_text += ' Mesure AOV linéaire v16 réussie puis réouverte indépendamment : 36 images, répétition exacte et nul/blanc identiques ; 83/55/93 pixels positifs par caméra sur chacune des deux sorties. Défaut RG mesuré [1,1], packet f32 admis uniquement pour Detail ; configuration privée restaurée. Refus debug antérieur conservé ; prefab et parité Forge non qualifiés.'
    detail_bevy_path = W / 'detail-native-default-bevy-vertex-v2/RESULT.v1.json'
    if detail_bevy_path.is_file():
        detail_bevy = json.loads(detail_bevy_path.read_text('utf-8'))
        if detail_bevy['status'] == 'detail_measured_default_abi_and_bevy_vertex_composed':
            detail_kernel_text += f" {detail_bevy['actual_positive_compositions']} compositions réelles Bevy réussies ; binding 120/256 octets préparé avec les RG mesurés, UV1 et déplacements sources conservés. Fragment, textures/GPU, installation renderer et prefab complets restant."
    detail_textures_path = W / 'detail-native-texture-storage-v1/TEXTURE-REOPEN.v1.json'
    if detail_textures_path.is_file():
        detail_textures = json.loads(detail_textures_path.read_text('utf-8'))
        detail_kernel_text += f" Deux textures originales réouvertes indépendamment : {detail_textures['source_texture_storage_captures_verified']}/2 textures, {detail_textures['mips_verified']}/24 mips ; formats DXT5 sRGB/UNorm et blocs vérifiés, sources inchangées. Sampling, décodeur normal, rendu et prefab restant."
    if incremental_path.is_file():
        incremental = json.loads(incremental_path.read_text('utf-8'))
        if incremental.get('status') == 'new_library_pages_and_thumbnail_bytes_verified':
            library_text = f"{incremental['cumulative_cards']} fiches couvertes par les reçus indépendants ; {incremental['cumulative_image_references']} références à images contrôlées ; {incremental['cumulative_missing_images']} fiches sans image conservées avec leurs IDs. Dernier sous-lot : {incremental['new_cards']} nouvelles fiches et {incremental['new_image_references']} références, {incremental['new_distinct_images_hashed']} fichiers image distincts rehachés. Anciennes pages rehachées, anciennes images héritées de leur reçu. Couverture attendue : 26 735 fiches. Rendu pertinent et livraison complète non qualifiés."
    final_library_path = W / 'library-all-records-independent-reconciliation-v4/RESULT.v1.json'
    if final_library_path.is_file():
        final_library = json.loads(final_library_path.read_text('utf-8'))
        if final_library.get('status') == 'all_native_library_records_pages_filters_and_image_receipts_reconciled':
            library_text = '26 735/26 735 fiches réconciliées indépendamment avec le reçu natif final ; 25 787 références à images contrôlées, 948 absences explicites. Les 479 dernières fiches et images contrôlées dans le dernier sous-lot ; contrôles antérieurs hérités de leurs reçus, toutes les pages rehachées. Filtres pack/disponibilité, présence des licences et refus de placement des sources contrôlés. Géométrie/rendu 3D et recette globale Studio/runtime non qualifiés.'
    normal_text = 'En attente de preuves GPU ; aucun résultat anticipé.'
    normal_path = W / 'detail-normal-decoder-independent-reopen-v1/RESULT.v1.json'
    if normal_path.is_file():
        normal = json.loads(normal_path.read_text('utf-8'))
        if normal.get('status') == 'detail_exact_candidate_normal_gpu_parity_reopened':
            normal_text = ('Mise à jour : sampling de 2 textures/24 mipmaps/98304 points comparé Unity/Forge, écarts RGBA nuls ; '
                'décodage de la seule normale réelle comparé sur 12 mipmaps/49152 points avec les références Unity RGorAG et UnpackNormal. '
                'Quatre compositions Bevy, cinq contrôles de refus et SHA/licences réouverts ; helpers identiques au candidat Detail. '
                'Matériau complet, renderer installé, prefab et recette Studio non qualifiés.')
    lit_path = W / 'detail-native-lit-fragments-v1/INDEPENDENT-REOPEN.v1.json'
    if lit_path.is_file():
        lit = json.loads(lit_path.read_text('utf-8'))
        if lit.get('actual_positive_compositions_reopened') == 8:
            normal_text += (' Ensuite : dix sorties de surface originales réouvertes, huit compositions complètes '
                'éclairage/prépasses et six refus ciblés ; alpha-test/couverture/deux faces conservés. '
                'Le transport GLB de binding 120 était absent : réparation générique schema 5 en cours de tests réels ; '
                'aucune qualification du rendu complet déduite.')
    text = f'''# Lots actifs et ordre de livraison

Vue synthétique : [Avancement par lots](AVANCEMENT_PAR_LOTS.md).

Actualisé : {now}. Les compteurs ci-dessous ne comptent pas des assets livrés.

| Sous-lot | Travail actuel | Critère pour fermer cette étape |
|---|---|---|
| L00.Integrite | {integrity_text} | Tous les payloads et archives référencées contrôlés, puis réconciliation complète du Vault. |
| L01/L04.RGB8 | {rgb_text} Le build mutable précédent reste refusé après changement de sources ; sa preuve n’est pas remplacée par une qualification globale. | Admission et rendu réels du prefab complet dans le moteur et Studio. Les tests du chargeur ne qualifient pas le rendu. |
| L06.FirePit.01 | Étape vérifiée : deux effets, 112 observations, transformations, cinq modifications du parent et répétabilité. Temps f32 comparés exactement, corruption d’un ULP refusée. Cinq changements de métadonnées privées de dossiers restent documentés ; le refus SHA global est conservé. | Référence native fermée dans cette portée ; comportement autonome, version Unity originale, conversion Forge et rendu restent à qualifier. |
| L06.FirePit.Materiaux | Deux identités et propriétés natives réouvertes : shader Alpha Blended original et texture active RA conservés. Trois fonctions WGSL portées et contrôlées par Naga ; une mauvaise signature refusée. Sources/licence officielles conservées, 55 écarts d’includes installés documentés et fermeture réelle de cinq includes retenue. | Comparaison GPU Unity/Forge, billboard et UV d’animation, masque RGB, profondeur/brouillard, ordre transparent et usage dans le renderer. Les alias de textures sauvegardés inactifs restent conservés sans les sélectionner. |
| L06.FirePit.Geometrie | {geometry_text} Billboards, couleurs, UV d’animation et matrices retenus ; aucun nouveau modèle 3D auteur. | Conversion des repères et rendu dans Forge. Une capture et ses contrôles ne ferment pas la livraison de l’effet. |
| L06.FirePit.Semantique | {semantics_text} | Mesurer l’interpolation temporelle et la conversion de couleur natives, puis comparer le shader et les billboards sur GPU avant admission runtime. |
| L06.FirePit.EtatApresBake | {bake_text} | Ce diagnostic ne remplace ni une conversion autonome ni le rendu dans Forge. |
| L06.FirePit.ShaderGPU | {gpu_text} | Mesurer la teinte réellement utilisée par le GPU et son blend/masque RGB ; comparer le port Forge. Texture source, soft particles, brouillard et effet complet restent distincts de ces fixtures. |
| L06.FirePit.Bindings | {tint_text} | Capturer le stockage natif de la texture RA active, comparer son sampling et le port sur GPU Forge, puis intégrer masque RGB et billboards. Bindings préparés ne signifie pas matériau rendu dans Forge. |
| L06.FirePit.TextureRA | {texture_text} | Comparaison du sampling Unity/Forge, format/sampler natifs, orientation et usage de la vraie texture dans le shader. Stockage capturé ne signifie pas rendu complet qualifié. |
| L06.FirePit.TextureGPU | {texture_gpu_text} | Comparaison indépendante avec le sampling Unity natif, puis port du shader avec texture réelle et admission dans le renderer. |
| L06.FirePit.SamplingUnity | {native_sampling_text} | Comparaison numérique mip par mip et qualification explicite de l’orientation ; aucune substitution de texture. |
| L06.FirePit.PariteSampling | {sampling_comparison_text} | Parité dans cette portée puis validation du shader original porté et des billboards ; ne pas qualifier le prefab à partir de la texture seule. |
| L01/L06.MasqueRGB | {mask_text} État de pass hashable incluant le masque de canaux, refus préalable des bits inconnus et conservation des autres attachments. | Comparer le port sur GPU, puis enregistrer le masque dans la spécialisation réelle du matériau. Tests et préparation seuls ne prouvent pas l’admission renderer. |
| L01/L06.MasqueProduction | {production_mask_text} | Build/tests réels puis installation avec contrôle des préimages, rendu GPU et usage complet dans Studio. |
| L06.FirePit.PortShaderGPU | {alpha_port_text} | Rendu avec vraie texture RA, géométrie et UV/billboards sources, variantes applicables, puis admission dans le matériau et runtime de Forge. |
| L06.FirePit.ShaderTextureRA | {ra_shader_text} | Parité du shader avec texture réelle, puis raccordement aux buffers de particules, variantes applicables, comportement autonome et usage dans Studio/runtime. |
| L01/L06.ProgrammeForward | {program_text} | Enregistrer le programme et ses bindings dans le renderer et le chargeur, puis vérifier son usage réel sur les données sources. |
| L06.FirePit.TempsRenderer | {render_time_text} | Qualifier le reliquat de simulation, les forces et l’évaluation de couleurs/tailles dans le temps de rendu ; simulation autonome et version Unity originale restant distinctes. |
| L01/L06.LegacyUnePasse | {single_pass_text} | Admission renderer générique fidèle aux capacités natives, clock et billboards, sans modifier la route des matériaux Graph déjà préparés. |
| L04.FirePit.02 | Après la référence native : assemblage des neuf nœuds, quatre dessins LOD, matériau, lumière, collider et deux effets. | Dérivé complet réouvert, rendu et comportement comparés, sans déclarer les seuls tests de shaders comme une livraison. |
| L04.Rocks.01 | Sept matériaux préparés ; référence de texture Detail absente des 48 inventaires contrôlés. | Mesurer le défaut natif, puis assembler les 86 nœuds et 57 dessins avec les huit matériaux et le feuillage. |
| L04.Rocks.Detail | {null_mask_text} | Comparer le rendu natif du masque absent aux contrôles explicites, puis qualifier le binding Forge ; aucune qualification implicite du prefab complet. |
| L04.Rocks.Detail.Kernel | {detail_kernel_text} | Binding natif et rendu complets, puis assemblage de Rocks et recette dans Studio. |
| L04.Rocks.Detail.Normale | {normal_text} | Terminer fragments/material complet et renderer, puis assemblage Rocks et recette Studio/runtime. |
| L08.Bibliotheque | {library_text} | Réconciliation complète, images pertinentes, recherche/filtres, aperçu, édition et persistance dans Studio. |

Après ces dépendances communes : Dungeon, groupes statiques par pack, rigs/animations, Blood/Dust et autres effets, sprites/terrains, puis réconciliation finale. Les tailles et IDs détaillés sont dans [le découpage](DECOUPAGE_OPERATIONNEL.md) et [les sous-lots](PROCHAINS_SOUS_LOTS.md).

Chaque livraison suit **conversion → vérification → rendu/comportement → fiche/image → éditeur/runtime**. Le suivi conserve séparément : attendu, étape vérifiée, asset livré, refus et action suivante. Les lots déjà vérifiés ne sont pas refaits.

Fichiers livrés : `F:\\OpenRPG_AssetVault`. Travail et preuves : `F:\\OpenRPG_ImportWork`.
'''
    T.mkdir(exist_ok=True)
    stamp = now.replace(':', '-').replace('+', '_')
    retained = T / 'historique-focus'
    retained.mkdir(exist_ok=True)
    manifest = dict(observed_utc=now, evidence=evidence, full_import=False,
        all_assets_delivered=False, source_assets_created=False)
    (retained / (stamp + '.json')).write_text(json.dumps(manifest, indent=2), 'utf-8')
    for name, content in [('LOTS_ACTIFS.md', text),
                          ('LOTS_ACTIFS.v1.json', json.dumps(manifest, indent=2))]:
        destination = T / name
        temporary = T / (name + '.tmp')
        temporary.write_text(content, 'utf-8')
        temporary.replace(destination)
    print(json.dumps(dict(document=str(T / 'LOTS_ACTIFS.md'), receipts=len(evidence), full_import=False)))

if __name__ == '__main__':
    main()
    # Preserve the newer assembled-scene checkpoint when regenerating historical views.
    from record_firepit_assembly_batch_v1 import main as record_scene_assembly
    record_scene_assembly()
