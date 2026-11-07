"""Reopen the actual draw and source context; keep fidelity and Studio open."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
T = W / 'SUIVI_LOTS_HIVEMIND'
OUT = W / 'townsmith-paint-door-native-draw-reopen-v1'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def write(path, data):
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')


def main():
    assert not OUT.exists(), 'Immutable closure already exists'
    source = W / 'townsmith-door-source-render-context-v6'
    draw = W / 'townsmith-paint-door-native-draw-v2'
    comparison = W / 'townsmith-paint-door-native-draw-comparison-v2'
    run = read(source / 'RUN-RESULT.v1.json')
    assert run['actual_exit_code'] == 0 and run['neutral_reference_byte_equal']
    assert run['source_inputs_unchanged'] and run['cases_captured'] == 3
    assert sha(source / 'NATIVE-RESULT.v1.json') == run['native_receipt_sha256']
    drawing = read(draw / 'RUN-RESULT.v1.json')
    assert drawing['actual_exit_code'] == 0 and drawing['gpu_draw_executed']
    assert sha(draw / 'NATIVE-RESULT.v1.json') == drawing['native_receipt_sha256']
    compared = read(comparison / 'RESULT.v1.json')
    assert compared['stable_native_readbacks'] == 3
    assert not compared['source_render_parity_qualified']
    assert not compared['pixel_fit_or_threshold_relaxation']
    prepared = read(source / 'PREPARED.v1.json')
    pins = dict(prepared['input_pins'])
    pins.update(prepared['private_before_pins'])
    for directory in (source, draw):
        for path in directory.iterdir():
            if path.is_file() and path.suffix in ('.json', '.rgba16f', '.png'):
                pins[str(path)] = sha(path)
    for path in (comparison / 'RESULT.v1.json', comparison / 'comparison.png', Path(__file__)):
        pins[str(path)] = sha(path)
    for path, expected in pins.items():
        assert sha(path) == expected, f'Changed input: {path}'
    prior = W / 'townsmith-door-source-render-context-v2'
    assert sha(source / 'door-linear.rgba16f') == sha(prior / 'door-linear.rgba16f')
    contexts = {case: read(source / f'{case}.context.json') for case in ('door-neutral', 'door-linear', 'door-fixedbackground-linear')}
    for context in contexts.values():
        assert context['actual_camera_history'] == 'CustomUserHistory0-existing'
        assert context['actual_hdrp_probe_captured']
        assert len(context['ambient_probe']) == 27
    now = datetime.now(timezone.utc).isoformat()
    limitation = {
        'status': 'corner_subtraction_silhouette_metric_unqualified',
        'comparison_receipt_sha256': sha(comparison / 'RESULT.v1.json'),
        'unqualified_fields': ['source_nonbackground_pixels', 'forge_nonbackground_pixels', 'silhouette_intersection_pixels', 'silhouette_union_pixels', 'silhouette_iou'],
        'reason': 'Unity gradient sky cannot be separated from geometry using one corner color. These fields do not measure object silhouette.',
        'required_followup': 'Independent source and Forge depth/object masks; no fitted threshold.',
        'original_comparison_preserved': True,
        'pixel_comparison_fidelity_qualified': False,
    }
    OUT.mkdir()
    write(OUT / 'METRIC-LIMITATION.v1.json', limitation)
    write(OUT / 'RESULT.v1.json', {
        'status': 'actual_camera_draw_and_existing_hdrp_history_context_reopened',
        'finished_utc': now, 'lot': 'L03.TOWNSMITH.DESSIN.01',
        'input_pins': pins, 'rehashed_pins': len(pins),
        'gpu_draw_executed': True, 'stable_native_readbacks': 3,
        'source_reference_byte_equal': True, 'source_inputs_unchanged': True,
        'source_camera_history': 'CustomUserHistory0-existing',
        'source_context': contexts['door-linear'],
        'actual_shader_gpu_ambient_binding_captured': False,
        'actual_sky_pixels_captured': False,
        'source_render_parity_qualified': False, 'forge_editor_verified': False,
        'full_import': False, 'complete_assets_delivered_increment': 0,
        'next_sub_lot': 'L03.TOWNSMITH.DESSIN.02 — éclairage, ciel et masques indépendants',
    })
    plan = T / 'PLAN_LOTS_A_SUIVRE.md'
    text = plan.read_text(encoding='utf-8')
    text = text.replace('Prochain lot de conversion : **L03.TOWNSMITH.RESSOURCES**.', 'Prochain sous-lot : **L03.TOWNSMITH.DESSIN.02**.')
    text += '''
## Townsmith : tranches de conversion et de vérification

| Sous-lot | Livrable | État et critère de clôture |
|---|---|---|
| DESSIN.01 | Dessin réel de la porte et comparaison initiale | Clos techniquement : trois captures GPU identiques, référence Unity reproduite, contexte HDRP existant vérifié. Fidélité refusée. |
| DESSIN.02 | Éclairage, ciel et masques de comparaison | En cours : capturer les ressources GPU réellement utilisées, raccorder la scène, comparer sans ajuster les seuils. |
| PREFABS.01 | Assemblage des objets associés | À suivre : transformations, slots, collisions et LOD ; conserver les variantes et IDs. |
| STUDIO.01 | Usage dans l'éditeur et runtime | À suivre : recherche, aperçu, placement, édition, sauvegarde et réouverture. |

[Clôture DESSIN.01](../townsmith-paint-door-native-draw-reopen-v1/RESULT.v1.json). Les métriques de silhouette fondées sur une couleur de coin sont non qualifiées : le ciel Unity est un gradient. Aucun asset complet supplémentaire livré.
'''
    plan.write_text(text, encoding='utf-8')
    with (T / 'JOURNAL.md').open('a', encoding='utf-8') as stream:
        stream.write(f'\n- {now} — L03.TOWNSMITH.DESSIN.01 : dessin réel et trois captures identiques réouverts ; contexte HDRP existant vérifié, référence Unity identique. Fidélité, prefabs et Studio ouverts. Suite : DESSIN.02. [Preuve](../townsmith-paint-door-native-draw-reopen-v1/RESULT.v1.json).\n')
    print(json.dumps({'status': 'technical_sub_lot_closed', 'pins': len(pins), 'full_import': False}))


if __name__ == '__main__':
    main()
