"""Reopen receipts and hashes; report technical closure separately from delivery."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import subprocess
import sys

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
T = W / 'SUIVI_LOTS_HIVEMIND'
ROOT = W / 'townsmith-s-paint-material-packet-reopen-v1'


def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))


def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


def main():
    assert not ROOT.exists()
    packet_path = W / 'townsmith-s-paint-material-packet-v1/MATERIAL-PACKET.v1.json'
    packet = read(packet_path)
    pins = dict(packet['input_pins'])
    paths = [packet_path, Path(packet['original_forward_shaderlab_state']), Path(__file__)]
    for directory, passed, failed in [('townsmith-s-paint-material-packet-check-v1', 37, 0),
                                       ('normal-decode-swizzle-baseline-check-v1', 35, 2)]:
        folder = W / directory
        result = read(folder / 'RESULT.v1.json')
        assert result['compile_exit_code'] == 0
        assert (result['test_exit_code'] == 0) == (failed == 0)
        assert f'{passed} passed; {failed} failed' in (folder / 'tests.stdout.log').read_text(encoding='utf-8')
        pins.update(result['input_pins'])
        paths += [folder / 'RESULT.v1.json', folder / 'tests.stdout.log', folder / 'tests.stderr.log']
    pins.update({str(p): sha(p) for p in paths})
    for p, expected in pins.items():
        assert sha(p) == expected, p
    ROOT.mkdir()
    proof = {'status': 'material_packet_integrity_and_normal_swizzle_regression_verified',
             'finished_utc': datetime.now(timezone.utc).isoformat(), 'packet': str(packet_path),
             'schema_version': 6, 'textures': 10, 'buffer_views': len(packet['buffer_views']),
             'corrected_candidate_tests_passed': 37, 'original_validator_expected_regression_failures': 2,
             'shared_sources_modified': False, 'normal_swizzle_fix_installed': False,
             'source_pipeline_state_qualified': False, 'gpu_draw_executed': False,
             'source_unity_render_parity_qualified': False, 'forge_editor_verified': False,
             'full_import': False, 'complete_assets_delivered_increment': 0, 'input_pins': pins,
             'remaining': packet['source_state_limits']}
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(proof, indent=2), encoding='utf-8')
    subprocess.run([sys.executable, '-B', str(S / 'record_batch_progress_v1.py')], check=True, stdout=subprocess.DEVNULL)
    plan = T / 'PLAN_EXECUTION_LOTS_20261009.md'
    text = plan.read_text(encoding='utf-8')
    text = text.replace('**L03.TOWNSMITH.SHADER.S_PAINT.CONTRAT.INSTALLATION — installation et tests du contrat commun.**',
                        '**L03.TOWNSMITH.SHADER.S_PAINT.PIPELINE.BEVY — admission des vrais layouts GPU ; paquet materiau controle en parallele.**')
    text = text.replace('- Contrat dix slots : 25 tests passes ; tests moteur en compilation. Compatibilite GPU et navigateur a verifier.',
                        '- Contrat commun installe : 28 tests assets et 103 tests moteur cibles passes apres installation. Paquet complet : 37 tests isoles passes ; correction du validateur xyz preparee, non installee. Compatibilite GPU et navigateur a verifier.')
    plan.write_text(text, encoding='utf-8')
    refresh_current()
    print(json.dumps({k: v for k, v in proof.items() if k not in ('input_pins', 'remaining')}))


def refresh_current():
    bevy_root = 'townsmith-s-paint-bevy-pipeline-v3' if (W / 'townsmith-s-paint-bevy-pipeline-v3/RUN-RESULT.v1.json').exists() else 'townsmith-s-paint-bevy-pipeline-v2'
    state = read(W / bevy_root / 'RUN-RESULT.v1.json')
    normal_path = W / 'normal-decode-swizzle-common-install-v1/RUN-RESULT.v1.json'
    normal = read(normal_path) if normal_path.exists() else {'status': 'not_queued'}
    images_path = W / 'house-source-thumbnail-batch-0009-0016-v1/RUN-RESULT.v1.json'
    images = read(images_path) if images_path.exists() else {'status': 'not_started'}
    door_path = W / 'townsmith-paint-door-glb-reopen-v1/RESULT.v1.json'
    door_line = '- GLB de porte reouvert : trois LOD, 6 981 sommets, dix textures, blocs embarques byte-equal et trois submeshes sans couleur conserves. 50 tests isoles passent, dont les controles precedents. Rendu/prefab/Studio ouverts.\n' if door_path.exists() else ''
    normal_summary = 'Correction preparee, pas encore installee.'
    if (W / 'normal-decode-swizzle-common-install-v1/INSTALLED.v1.json').exists():
        normal_summary = 'Correction installee ; tests moteur a verifier.'
    if normal.get('assets_exit_code') == 0:
        normal_summary += ' Les 29 tests assets apres correction passent.'
    if normal.get('status') == 'normal_swizzle_common_installed_workspace_tests_passed':
        normal_summary = 'Correction installee : 29 tests assets et les controles moteur cibles passent dans le workspace.'
    next_step = 'Lier textures/uniformes, verifier etats source et pixels, assembler les prefabs et verifier Studio.'
    if normal.get('status') != 'normal_swizzle_common_installed_workspace_tests_passed':
        next_step = 'Terminer les controles du correctif, puis ' + next_step[0].lower() + next_step[1:]
    resource_line = ''
    resource_proof = W / 'townsmith-paint-door-native-resource-admission-reopen-v3/RESULT.v1.json'
    if resource_proof.exists():
        resource = read(resource_proof)
        if resource['status'] == 'ten_original_glb_images_and_actual_bevy_material_allocation_reopened':
            resource_line = '- Lot ressources Townsmith reouvert : dix images natives, 120 mips, 78 293 832 octets exacts ; vingt bindings texture/sampler et groupe materiau Bevy verifies, uniformes source de 336 octets. Trois meshes / 6 981 sommets importes. Aucun dessin ni rendu qualifie.\n'
            next_step = 'L03.TOWNSMITH.DESSIN : verifier les etats source, executer un dessin reel, comparer sampling et pixels a Unity, puis assembler les prefabs et verifier Studio.'
    draw_path = W / 'townsmith-paint-door-native-draw-v2/RUN-RESULT.v1.json'
    if draw_path.exists() and read(draw_path).get('gpu_draw_executed'):
        resource_line += '- Dessin reel du GLB execute : trois readbacks GPU identiques, camera et ForceEnable source mesures ; comparaison lineaire executee, fidelite refusee. Le ciel HDRP de preview est absent cote Forge ; scene et eclairage a raccorder.\n'
        next_step = 'L03.TOWNSMITH.DESSIN.SCENE : capturer la vraie sonde et le cubemap HDRP de preview, distinguer scene et materiau, puis comparer le rendu sans ajustement des seuils. Prefabs et Studio restent ouverts.'
    delivery_path = W / 'house-source-thumbnail-batch-0009-0016-delivery-reopen-v1/RESULT.v1.json'
    if (W / 'house-source-thumbnail-batch-0017-0024-delivery-reopen-v1/RESULT.v1.json').exists():
        delivery_path = W / 'house-source-thumbnail-batch-0017-0024-delivery-reopen-v1/RESULT.v1.json'
    if (W / 'house-source-thumbnail-batch-0025-0032-delivery-reopen-v1/RESULT.v1.json').exists():
        delivery_path = W / 'house-source-thumbnail-batch-0025-0032-delivery-reopen-v1/RESULT.v1.json'
    image_summary = 'Capture et revue requises ; aucune nouvelle image declaree installee.'
    if delivery_path.exists():
        delivery = read(delivery_path)
        image_summary = f"Huit fiches indexees, seize images source revues et installees. Recherche, filtres, details et lecture des PNG passent dans le lecteur natif du produit. Fiches source sans image : {delivery['missing_primary_images_before']} -> {delivery['missing_primary_images_after']}. Validation visuelle dans le Studio et usage 3D encore ouverts."
    text = f'''# Hivemind — avancement actuel

Mis a jour : {datetime.now(timezone.utc).isoformat()}

Fichiers definitifs : `F:\\OpenRPG_AssetVault`. Travail et preuves : `F:\\OpenRPG_ImportWork`.

| Lot | Portee | Etat |
|---|---|---|
| L00 | Sources, licences, transfert, hashes | Snapshot historique controle ; controle de chaque nouvelle livraison |
| L01 | Rendu commun, lumiere et ombres | Partiel ; fidelite encore refusee sur certains cas |
| L02 | Dungeon | Conversions partielles ; prefabs et rendu a terminer |
| L03 | Objets statiques Townsmith, House, Villa | Actif : materiau Townsmith S_Paint |
| L04 | Materiaux et feuillages | Partiel ; alpha, deformation, ombres et LOD a qualifier |
| L05 | Squelettes et animations | Verification des rigs, poids et clips restante |
| L06 | Effets FirePit, Blood, Dust | Captures partielles ; rendu et evolution temporelle a qualifier |
| L07 | Sprites, terrains, audio | Conversions et controles par format restants |
| L08 | Bibliotheque avec images, recherche et filtres | Indexation partielle ; parcours editeur non valide |
| L09 | Verification finale des 48 packs/variantes | Reconciliation des IDs et recettes finales restantes |

## Derniere tranche verifiee

- Contrat commun installe : 28 tests assets et 103 tests moteur cibles passes.
- Paquet Townsmith prepare : 10 textures natives, 120 mips, shaders forward/prepass, etats de rendu primaires source.
- Integrite et correction isolee du controle xyz : 37 tests passes. Deux echecs attendus sur le validateur original confirment la regression. {normal_summary}
{door_line}
{resource_line}
- Aucun nouvel asset complet livre par cette tranche. Un controle technique ne prouve pas recherche, apercu, placement, sauvegarde/reouverture et runtime dans Studio.

## Tranche suivante en cours

Sept variantes avec les vrais layouts Bevy. Etat exact : `{state['status']}`.
Ensuite : {next_step} MRT et stencil source sont conserves mais leur equivalence reste ouverte.

Correctif xyz : `{normal['status']}`. Le changement respecte la fin du controle Bevy precedent ; snapshot du fichier anterieur conserve et tests assets/moteur traces.

Derniere tranche images verifiee : huit nouveaux prefabs, 742 fichiers source controles. {image_summary}

[Plan par lots](PLAN_LOTS_A_SUIVRE.md) · [Journal](JOURNAL.md) · [Preuve du paquet](../townsmith-s-paint-material-packet-reopen-v1/RESULT.v1.json) · [Controle Bevy](../{bevy_root}/RUN-RESULT.v1.json) · [Derniere tranche images](../{delivery_path.parent.name}/RESULT.v1.json)
'''
    (T / 'AVANCEMENT_ACTUEL.md').write_text(text, encoding='utf-8')


if __name__ == '__main__':
    refresh_current() if len(sys.argv) > 1 else main()
