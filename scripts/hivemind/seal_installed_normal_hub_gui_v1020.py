"""Seal normal installed Hub -> Studio -> filtered source-library observations."""
from pathlib import Path
import json
from assemble_native_villa_delivery import read, sha, require, now

W = Path('F:/OpenRPG_ImportWork')
V = Path('F:/OpenRPG_AssetVault')
I = Path('F:/instal test/Installation')
GUI = W / 'installed-normal-hub-library-gui-v1019'
O = W / 'installed-normal-hub-library-verification-v1020'


def main():
    require(not O.exists(), 'Existing verification preserved')
    observed = read(GUI / 'OBSERVED.v1.json')
    tree = (GUI / 'source-lod5.accessibility.txt').read_text('utf-8')
    for token in ('26736 assets indexés', '6 résultats', 'Disponibilité Value: Source conservée',
                  'Pack Value: pirate island', observed['source_asset_id'],
                  'LicenseRef-Unity-Asset-Store-EULA', 'Bouton (disabled) Aperçu 3D',
                  'Bouton (disabled) Placer sur la carte'):
        require(token in tree, 'Native accessibility observation incomplete: ' + token)
    require((GUI / 'source-lod5.jpg').read_bytes()[:3] == bytes((255, 216, 255)), 'Native JPEG screenshot missing')
    state = read(I / 'installation.json')
    package = I / 'packages' / state['active']['core']['id'] / state['active']['core']['revision']
    exe = package / 'bin/forge-studio.exe'
    require(str(exe).lower() in observed['studio_window']['app'].lower(), 'Observed Studio differs from active installed core')
    preference = Path(state['user_root']) / 'asset-vault.v1.json'
    require(read(preference)['path'] == 'F:/OpenRPG_AssetVault', 'Installed private Vault preference changed')
    pointer = V / 'metadata/asset-source-support-registry.v1.json'
    revision = read(pointer)
    catalogue_path = V / revision['path']
    require(sha(catalogue_path) == revision['sha256'], 'Source catalogue revision mismatch')
    catalogue = read(catalogue_path)['assets']
    card = next(r for r in catalogue if r['id'] == observed['source_asset_id'])
    require(card['availability'] == 'source_support' and
            card['readiness']['converted'] is False and card['readiness']['placement_qualified'] is False and
            card['readiness']['runtime_qualified'] is False and 'not-placeable' in card['tags'] and
            card['license'] == 'LicenseRef-Unity-Asset-Store-EULA', 'Source status or licence differs')
    pins = {str(p): sha(p) for p in (GUI / 'OBSERVED.v1.json', GUI / 'source-lod5.accessibility.txt',
             GUI / 'source-lod5.jpg', I / 'installation.json', I / 'Forge-ContentManager.exe',
             exe, package / 'package-manifest.json', preference, pointer, catalogue_path,
             V / 'metadata/asset-import-registry.v1.json', Path(__file__).resolve())}
    for field in ('source_file', 'source_meta_file', 'preview'):
        file = V / card[field]
        require(file.resolve().is_relative_to(V.resolve()), 'Vault path escape')
        expected = card[{'source_file': 'source_sha256', 'source_meta_file': 'source_meta_sha256',
                         'preview': 'preview_sha256'}[field]]
        require(sha(file) == expected, 'Observed source or preview bytes changed: ' + field)
        require(str(file).replace('/', '\\') in tree, 'Observed path differs: ' + field) if field != 'source_meta_file' else None
        pins[str(file)] = expected
    O.mkdir()
    (O / 'SOURCE-CARD.v1.json').write_text(json.dumps(card, indent=2), 'utf-8')
    result = dict(status='NORMAL_INSTALLED_HUB_STUDIO_SOURCE_LIBRARY_OBSERVED', finished_utc=now(),
                  installation_revision=state['revision'], core_revision=state['active']['core']['revision'],
                  installed_studio_sha256=sha(exe), input_pins=pins,
                  normal_hub_launch_verified=True, editor_library_access_observed=True,
                  catalogue_count_observed=26736, filtered_source_results_observed=6,
                  source_asset_observed=card['id'], native_source_image_observed=True,
                  pack_and_availability_filters_observed=True, source_license_and_files_observed=True,
                  nonplaceable_source_controls_refused_as_expected=True,
                  all_catalogue_images_visually_verified=False, source_material_render_parity_qualified=False,
                  placement_save_reopen_runtime_verified=False, current_common_studio_rebuilt=False,
                  new_fully_qualified_assets=0, registry_modified=False, full_import=False)
    (O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    text = '''## Bibliothèque accessible par le lancement normal installé — v1020

Le Content Manager de l'installation 7 s'est ouvert ; son bouton Ouvrir l'éditeur a lancé le Studio du core actif 4c2dcebf. La bibliothèque y expose 26736 entrées indexées et son emplacement F:/OpenRPG_AssetVault. Recherche SM_ShackSideShed3x3_01 + filtre Pack pirate island + Disponibilité Source conservée : six résultats avec images visibles. La fiche du LOD5 montre sa licence Unity Asset Store, son identifiant, son prefab conservé sur F: et sa vignette native Unity sur F:. Aperçu 3D et Placer restent désactivés pour cette source non convertie, conformément à son statut. Le chemin normal Hub -> Studio est maintenant observé ; l'ancien timeout GUI reste une preuve historique, pas le blocage actuel.

Preuves : F:/OpenRPG_ImportWork/installed-normal-hub-library-gui-v1019 et installed-normal-hub-library-verification-v1020. Cette vérification porte sur l'installation existante et cette recherche source ; elle ne reconstruit pas Studio depuis le checkout courant, ne valide pas toutes les images, et ne qualifie ni la conversion de la source ni placement/runtime. Le lot Villa natif de 19 scènes reste distinct ; son lecteur courant v1017 a compilé mais été refusé pour une entrée de vérificateur modifiée pendant le build. Les corrections concurrentes de casts/Clippy sont conservées ; v1021 reprend la validation fraîche sans reconversion des GLB.

'''
    for name in ('TABLEAU_DE_BORD_LOTS.md', 'SUIVI_LISIBLE.md', 'POINT_DE_REPRISE.md'):
        target = W / 'SUIVI_LOTS_HIVEMIND' / name
        before = target.read_bytes()
        (O / (name + '.before')).write_bytes(before)
        pending = target.with_name(name + '.v1020.pending')
        with pending.open('xb') as stream:
            stream.write(text.encode('utf-8') + before)
        require(target.read_bytes() == before, 'Concurrent tracking edit')
        pending.replace(target)
    print(json.dumps({k: v for k, v in result.items() if k != 'input_pins'}))


if __name__ == '__main__':
    main()
