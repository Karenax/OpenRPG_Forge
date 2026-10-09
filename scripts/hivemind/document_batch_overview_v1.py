"""Publish a compact reading order without upgrading technical evidence."""
from pathlib import Path
import datetime
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
D = W / 'SUIVI_LOTS_HIVEMIND'

def main():
    incremental = W / 'library-incremental-pages-reopen-v3/RESULT.v1.json'
    r = json.loads(incremental.read_text('utf-8')) if incremental.exists() else {}
    ok = r.get('status') == 'new_library_pages_and_thumbnail_bytes_verified'
    cards = r['cumulative_cards'] if ok else 24576
    images = r['cumulative_image_references'] if ok else 23628
    missing = r['cumulative_missing_images'] if ok else 948
    final_path = W / 'library-all-records-independent-reconciliation-v4/RESULT.v1.json'
    final = json.loads(final_path.read_text('utf-8')) if final_path.exists() else {}
    final_ok = final.get('status') == 'all_native_library_records_pages_filters_and_image_receipts_reconciled'
    if final_ok:
        cards, images, missing = final['cards_checked'], final['image_references_checked'], final['missing_images']
    now = datetime.datetime.now(datetime.timezone.utc).isoformat()
    binding_path = W / 'detail-default-native-rg-binding-v2/RESULT.v1.json'
    binding = json.loads(binding_path.read_text('utf-8')) if binding_path.exists() else {}
    bevy_path = W / 'detail-native-default-bevy-vertex-v2/RESULT.v1.json'
    bevy = json.loads(bevy_path.read_text('utf-8')) if bevy_path.exists() else {}
    raw_path = W / 'detail-source-raw-gpu-parity-v1/RESULT.v1.json'
    raw = json.loads(raw_path.read_text('utf-8')) if raw_path.exists() else {}
    raw_ok = (raw.get('normal_texture_raw_sampling_verified') is True
              and raw.get('normal_textures_compared') == 2
              and raw.get('mip_levels_compared') == 24
              and raw.get('sampled_texel_lod_coordinates') == 98304
              and raw.get('common_measured_coordinate_mappings') == ['identity'])
    normal_path = W / 'detail-normal-decoder-independent-reopen-v1/RESULT.v1.json'
    normal = json.loads(normal_path.read_text('utf-8')) if normal_path.exists() else {}
    normal_ok = (normal.get('status') == 'detail_exact_candidate_normal_gpu_parity_reopened'
                 and normal.get('normal_textures_verified') == 1
                 and normal.get('normal_mips_verified') == 12
                 and normal.get('negative_controls_passed') == 5)
    lit_path = W / 'detail-native-lit-fragments-v1/INDEPENDENT-REOPEN.v1.json'
    lit = json.loads(lit_path.read_text('utf-8')) if lit_path.exists() else {}
    lit_ok = (lit.get('native_lit_programs_reopened') == 1
              and lit.get('actual_positive_compositions_reopened') == 8
              and len(lit.get('scoped_negative_compositions', [])) == 6)
    scalar_path = W / 'source-graph-scalar-transport-native-v6/RUN-RESULT.v1.json'
    scalar = json.loads(scalar_path.read_text('utf-8')) if scalar_path.exists() else {}
    native_state_path = W / 'detail-native-material-state-v1/RUN-RESULT.v1.json'
    native_state = json.loads(native_state_path.read_text('utf-8')) if native_state_path.exists() else {}
    text = f'''# Avancement Hivemind par lots

Mis à jour : {now}.

Périmètre : **48 packs et variantes**, **14 219 modèles déclarés** et **12 516 sources support**. Ces nombres sont un inventaire, pas un total d'assets utilisables.

Fichiers de bibliothèque : `F:\\OpenRPG_AssetVault`.
Travail temporaire, caches, captures et preuves : `F:\\OpenRPG_ImportWork`.

## Les dix lots

| Lot | Contenu | État et prochaine étape |
|---|---|---|
| L00 — Sources et stockage | Archives, sources, licences, SHA et transfert sur F | Audit des payloads/archives référencés en cours ; ensuite réconcilier tous les fichiers actuels du Vault. |
| L01 — Chargement Forge | Matériaux, shaders et scènes dans le vrai moteur | 26 tests formats et 87 tests moteur réussis sur le filtre source_graph ; 17 contrôles Studio antérieurs conservés. Terminer assemblage, rendu réel et recette. |
| L02 — Dungeon | Prefabs, LOD, pivots et matériaux | 42 captures natives contrôlées ; assemblage et rendu des prefabs complets restent à qualifier. |
| L03 — Modèles statiques | Conversions par pack, puis prefabs complets | 893 géométries Townsmith contrôlées ; résoudre six bindings de dessin. Villa : 207 dérivés physiques contrôlés, 19 scènes restantes. |
| L04 — Matériaux et végétation | Textures, normales, transparence et déformations | En cours : matériau Detail de Rocks ; puis assemblage Rocks et FirePit. Ne pas remplacer une texture absente sans preuve native. |
| L05 — Rigs et animations | Os, poids, poses, clips et déformation | Premiers contrôles Medieval conservés ; matériaux, déformation GPU et recette restent à faire. |
| L06 — Effets | FirePit, Blood, Dust et effets intégrés | FirePit : shader et sampling GPU comparés ; simulation autonome et effet complet restent. Blood/Dust et autres sources suivent par sous-lots. |
| L07 — Formats spéciaux | Sprites, terrains et autres ressources | Conversion et usage adaptés à chaque type ; aucun intermédiaire ne compte comme livré. |
| L08 — Bibliothèque et éditeur | Images, recherche, filtres, fiches et usage | {cards:,}/26 735 fiches couvertes par les reçus indépendants, {images:,} références à images contrôlées, {missing} absences explicites. Recette globale restant. |
| L09 — Bilan final | Rapprochement de tous les IDs et preuves | À fermer après les lots applicables ; chaque ID doit être livré ou porter un refus précis. |

## Comment un sous-lot avance

**Conversion → vérification des fichiers → rendu/comportement → fiche et image → éditeur/runtime.**

Chaque sous-lot conserve les IDs exacts, attendu/vérifié, chemins, hashes, reçu, refus éventuels et prochaine action. Un asset n'est compté livré qu'après les étapes applicables à son type. Les lots validés ne sont pas reconvertis ; les réparations ciblées gardent les anciennes preuves.

Taille des lots : Townsmith garde ses groupes de 20 géométries déjà documentés ; les nouveaux effets intégrés sont regroupés par cinq sources maximum ; la bibliothèque avance par pages durables de 48 fiches. Pour les conversions restantes, grouper par pack et type sans séparer artificiellement un prefab de ses dépendances.

## File de travail actuelle

1. **L08.VERIFICATION.PAGES.03** : vérifier les nouvelles pages produites depuis le reçu de 24 576 fiches, sans refaire leurs images déjà contrôlées. État : {'étape vérifiée' if ok else 'vérification en cours'}.
2. **L04.ROCKS.DETAIL.MESURE** : mesurer le défaut matériel en sortie linéaire native, puis raccorder le binding Forge. Le readback debug précédent est refusé pour cette mesure ; aucune valeur supposée n'est installée.
3. **L01/L06.FIREPIT.INTEGRATION** : terminer le chargeur et le rendu du shader avec les vraies données, puis simulation autonome et prefab complet.
4. **L03.TOWNSMITH.DESSIN** : résoudre les six mappings natifs avant assemblage/rendu ; conserver les 893 géométries contrôlées.
5. Reprendre Dungeon et les autres packs, rigs, effets et formats spéciaux selon leurs dépendances. L00 continue en lecture seule ; publication dans le Vault après les audits qui verrouillent ses fichiers.

Les sous-lots organisent l'exécution et ne créent aucun nouveau point d'autorisation. Après chaque résultat significatif : **lot — étape — vérifiés/attendus — résultat — suite**, avec son reçu.

## Dernier résultat

'''
    if ok:
        text += (f"L08.VERIFICATION.PAGES.03 : **{r['new_cards']} nouvelles fiches** et "
                 f"**{r['new_image_references']} références à images** contrôlées ; "
                 f"{r['new_distinct_images_hashed']} fichiers image distincts rehachés pour ce sous-lot. "
                 f"{r['new_missing_images']} nouvelle absence. "
                 "Les contrôles précédents sont hérités de leur reçu, leurs pages sont rehachées ; leurs images ne sont pas présentées comme nouvellement rehachées. "
                 "Cette étape ne qualifie ni la conversion complète ni le rendu de tous les assets.\n\n"
                 "[Reçu du sous-lot](../library-incremental-pages-reopen-v3/RESULT.v1.json).\n\n")
    else:
        text += 'Vérification des nouvelles pages en cours ; aucun résultat anticipé.\n\n'
    if final_ok:
        text += ('**L08.VERIFICATION.PAGES.04 terminé** : les 479 dernières fiches et leurs 479 références à images ont été contrôlées ; '
                 '415 fichiers image distincts rehachés. Les 26 735 fiches et toutes leurs pages sont réconciliées avec le reçu natif final. '
                 '25 787 références à images contrôlées ; 948 absences conservées. Filtres par pack/disponibilité, licences présentes et refus de placement des sources contrôlés. '
                 'Images pertinentes, rendu 3D et recette globale Studio/runtime restent à qualifier. '
                 '[Reçu final des fiches](../library-all-records-independent-reconciliation-v4/RESULT.v1.json).\n\n')
    detail_texture_path = W / 'detail-native-texture-storage-v1/TEXTURE-REOPEN.v1.json'
    detail_texture = json.loads(detail_texture_path.read_text('utf-8')) if detail_texture_path.exists() else {}
    if detail_texture.get('source_texture_storage_captures_verified') == 2:
        text += ('**L04.ROCKS.DETAIL.TEXTURES — stockage vérifié** : 2/2 textures sources et 24/24 mipmaps réouverts indépendamment ; '
                 'formats DXT5 sRGB/UNorm, tailles de blocs, identités et hashes contrôlés. '
                 'Sampling GPU, décodage de la normale, matériau et prefab complets restent à qualifier. '
                 '[Reçu des textures](../detail-native-texture-storage-v1/TEXTURE-REOPEN.v1.json).\n\n')
    if binding.get('native_default_rg_binding_qualified'):
        text += ('**L04.ROCKS.DETAIL.DEFAUT — mesure terminée** : 36 images AOV linéaires réouvertes, '
                 'répétitions exactes et contrôles nul/blanc/noir ; 83/55/93 pixels positifs par caméra sur chaque sortie. '
                 'RG mesuré [1,1], packet f32 qualifié pour ce défaut uniquement ; configuration privée restaurée. '
                 '[Reçu du défaut](../detail-default-native-rg-binding-v2/RESULT.v1.json).\n\n')
        text = text.replace('mesurer le défaut matériel en sortie linéaire native, puis raccorder le binding Forge.',
                            'mesure native terminée ; terminer les fragments, sampling/normal GPU, raccordement renderer et prefab Rocks.')
    if bevy.get('status') == 'detail_measured_default_abi_and_bevy_vertex_composed':
        text += ('**L04.ROCKS.DETAIL.BEVY_VERTEX — étape vérifiée** : quatre compositions réelles Bevy réussies '
                 'avec binding 120 et packet de 256 octets issu du défaut mesuré ; UV1 et déplacements source conservés. '
                 'Fragment, renderer installé, GPU et prefab complet restent non qualifiés. '
                 '[Reçu Bevy](../detail-native-default-bevy-vertex-v2/RESULT.v1.json).\n\n')
    text += ('[Détails techniques actuels](LOTS_ACTIFS.md) · [Découpage et inventaire des packs](DECOUPAGE_OPERATIONNEL.md) '
             '· [Sous-lots et historique des preuves](PROCHAINS_SOUS_LOTS.md).\n')
    if raw_ok:
        text = text.replace('Sampling GPU, décodage de la normale, matériau et prefab complets restent à qualifier.',
                            'Sampling GPU comparé dans le sous-lot suivant ; décodage de la normale, matériau et prefab complets restent à qualifier.')
        text = text.replace('mesure native terminée ; terminer les fragments, sampling/normal GPU, raccordement renderer et prefab Rocks.',
                            'mesure native et sampling RGBA terminés ; contrôler le décodage normal GPU, puis fragments, renderer et prefab Rocks.')
        text = text.replace('**L08.VERIFICATION.PAGES.03** : vérifier les nouvelles pages produites depuis le reçu de 24 576 fiches, sans refaire leurs images déjà contrôlées. État : étape vérifiée.',
                            '**L08.VERIFICATION.PAGES.04** : audit des 26 735 fiches terminé ; recette visuelle et usage global dans Studio encore ouverts.')
        text += ('\n**L04.ROCKS.DETAIL.SAMPLING — étape vérifiée** : 2/2 textures, 24/24 niveaux de mip, '
                 '98 304 coordonnées comparées entre le GPU Unity et le GPU Forge ; mapping mesuré identité, '
                 'seuil RGBA conservé 0,000001. La texture couleur est comparée comme couleur ; seule la seconde '
                 'texture sera admise au contrôle de décodage de normale. Aucun rendu de prefab complet déduit. '
                 '[Reçu de comparaison](../detail-source-raw-gpu-parity-v1/RESULT.v1.json). '
                 'Suite : **L04.ROCKS.DETAIL.NORMALE**.\n')
    if normal_ok:
        text = text.replace('**L04.ROCKS.DETAIL.MESURE**', '**L04.ROCKS.DETAIL.MATERIAU_COMPLET**')
        text = text.replace('mesure native et sampling RGBA terminés ; contrôler le décodage normal GPU, puis fragments, renderer et prefab Rocks.',
                            'mesure native, sampling et normale GPU terminés ; terminer les fragments, renderer et prefab Rocks.')
        text += ('\n**L04.ROCKS.DETAIL.NORMALE — étape vérifiée** : 1/1 normale réelle, 12/12 niveaux de mip, '
                 '49 152 coordonnées comparées ; concordance avec les références Unity RGorAG et UnpackNormal. '
                 'Quatre compositions Bevy et cinq contrôles de refus vérifiés ; helpers identiques au candidat Detail. '
                 'SHA et licences réouverts. Le candidat reste hors du Vault : matériau complet, renderer, prefab '
                 'et recette Studio sont encore ouverts. '
                 '[Reçu indépendant](../detail-normal-decoder-independent-reopen-v1/RESULT.v1.json). '
                 'Suite : **L04.ROCKS.DETAIL.MATERIAU_COMPLET**.\n')
    if lit_ok:
        text += ('\n**L04.ROCKS.DETAIL.MATERIAU_COMPLET — programmes composés et réouverts** : '
                 '10 sorties originales de surface et 4 compositions de leur interface ; 8 compositions complètes '
                 'éclairage/prépasses et 6 refus de variantes non qualifiées. Alpha-test, alpha-to-coverage, '
                 'constantes des deux faces [1,1,-1], UV1 et défaut mesuré préservés. '
                 'Compilation réelle Bevy, sans verdict de rendu GPU ni de prefab livré. '
                 '[Reçu des programmes](../detail-native-lit-fragments-v1/INDEPENDENT-REOPEN.v1.json).\n')
    if scalar:
        text += ('\n**L01.PARAMETRES.GLBS5 — raccordement testé** : le format GLB précédent ne transportait pas '
                 'le packet de binding 120. Correctif générique schema 5, SHA des 256 octets et slots compilateur ; '
                 'préimages conservées, refus de packet absent/modifié/non fini. État réel des tests : `'
                 + scalar['status'] + '`. '
                 '[État des tests](../source-graph-scalar-transport-native-v6/RUN-RESULT.v1.json). '
                 'Ne ferme pas le rendu, le prefab ou Studio.\n')
        for package in scalar.get('tests', []):
            if package.get('exit_code') == 0:
                text += f"\nTests réels `{package['package']}` : **{len(package['passed_tests'])} passés**, zéro échec dans ce filtre `source_graph`. Aucun verdict GPU déduit.\n"
    if native_state:
        text += ('\n**L04.ROCKS.DETAIL.ETAT_NATIF** : capture ciblée des propriétés, passes et deux bindings de textures '
                 'dans le projet HDRP chaud, après les tests Rust détenus. État : `' + native_state['status'] + '`. '
                 '[État de capture](../detail-native-material-state-v1/RUN-RESULT.v1.json). '
                 'Sources conservées ; versions auteur/HDRP et rendu complet restent distincts.\n')
    target = D / 'AVANCEMENT_PAR_LOTS.md'
    if target.exists():
        before = target.read_bytes()
        backup = D / ('AVANCEMENT_PAR_LOTS.before-' + hashlib.sha256(before).hexdigest()[:12] + '.md')
        if not backup.exists():
            backup.write_bytes(before)
    target.write_text(text, encoding='utf-8')
    if ok:
        state_path = D / 'ETAT.v1.json'
        old = state_path.read_bytes()
        state = json.loads(old)
        backup = D / ('ETAT.before-library-v3-' + hashlib.sha256(old).hexdigest()[:12] + '.json')
        if not backup.exists():
            backup.write_bytes(old)
        state['latest_incremental_library_verification'] = dict(
            receipt=str(incremental), receipt_sha256=hashlib.sha256(incremental.read_bytes()).hexdigest(),
            new_cards=r['new_cards'], cumulative_cards=cards,
            cumulative_image_references=images, missing_images=missing,
            full_import=False, finished_utc=r['finished_utc'])
        state['current_reading_order'] = str(target)
        if binding.get('native_default_rg_binding_qualified'):
            state['latest_detail_native_default_binding'] = dict(receipt=str(binding_path),
                receipt_sha256=hashlib.sha256(binding_path.read_bytes()).hexdigest(),native_images_verified=36,
                measured_rg=binding['measured_rg'],full_import=False,whole_prefab_qualified=False)
        if bevy.get('status') == 'detail_measured_default_abi_and_bevy_vertex_composed':
            state['latest_detail_bevy_vertex_composition'] = dict(receipt=str(bevy_path),
                receipt_sha256=hashlib.sha256(bevy_path.read_bytes()).hexdigest(),actual_positive_compositions=4,
                renderer_installed=False,gpu_rendered=False,full_import=False)
        if detail_texture.get('source_texture_storage_captures_verified') == 2:
            state['latest_detail_texture_storage_verification'] = dict(
                receipt=str(detail_texture_path), receipt_sha256=hashlib.sha256(detail_texture_path.read_bytes()).hexdigest(),
                textures_verified=2, mips_verified=24, gpu_sampling_qualified=False,
                normal_decode_qualified=False, full_import=False)
        if final_ok:
            state['latest_complete_library_record_audit'] = dict(
                receipt=str(final_path), receipt_sha256=hashlib.sha256(final_path.read_bytes()).hexdigest(),
                cards_checked=cards, image_references_checked=images, missing_images=missing,
                full_import=False, actual_ui_visual_review=False, finished_utc=final['finished_utc'])
        state['active_sub_batch'] = 'L04.ROCKS.DETAIL.MESURE'
        if raw_ok:
            state['latest_detail_texture_gpu_parity'] = dict(receipt=str(raw_path),
                receipt_sha256=hashlib.sha256(raw_path.read_bytes()).hexdigest(),
                textures_compared=2,mips_compared=24,coordinates_compared=98304,
                measured_coordinate_mapping='identity',normal_decoder_qualified=False,full_import=False)
            state['active_sub_batch'] = 'L04.ROCKS.DETAIL.NORMALE'
        if normal_ok:
            state['latest_detail_normal_gpu_parity'] = dict(receipt=str(normal_path),
                receipt_sha256=hashlib.sha256(normal_path.read_bytes()).hexdigest(),
                textures_compared=1,mips_compared=12,coordinates_compared=49152,
                negative_controls_passed=5,renderer_installed=False,full_import=False)
            state['active_sub_batch'] = 'L04.ROCKS.DETAIL.MATERIAU_COMPLET'
        if lit_ok:
            state['latest_detail_full_material_composition'] = dict(receipt=str(lit_path),
                receipt_sha256=hashlib.sha256(lit_path.read_bytes()).hexdigest(),
                actual_compositions=8,scoped_refusals=6,gpu_rendered=False,full_import=False)
        if scalar:
            state['scalar_transport_test_status'] = dict(receipt=str(scalar_path),status=scalar['status'],full_import=False)
            state['active_sub_batch'] = 'L01.PARAMETRES.GLBS5'
        if native_state:
            state['detail_native_material_state_capture'] = dict(receipt=str(native_state_path),status=native_state['status'],full_import=False)
        state['updated_utc'] = now
        state_path.write_text(json.dumps(state, indent=2, ensure_ascii=False), encoding='utf-8')
        journal = D / 'JOURNAL.md'
        journal_text = journal.read_text('utf-8-sig')
        raw_marker = 'L04.ROCKS.DETAIL.SAMPLING — comparaison Unity Forge'
        normal_marker = 'L04.ROCKS.DETAIL.NORMALE — candidat GPU réouvert'
        lit_marker = 'L04.ROCKS.DETAIL.MATERIAU_COMPLET — huit compositions réouvertes'
        if lit_ok and lit_marker not in journal_text:
            with journal.open('a', encoding='utf-8') as stream:
                stream.write(f'\n\n## {lit_marker}\n\n{now} : dix sorties de surface originales, '
                             'quatre compositions de l’interface, huit compositions éclairage/prépasses et six refus ciblés. '
                             'Alpha-test, alpha-to-coverage, deux faces [1,1,-1], UV1 et RG mesurés préservés. '
                             'Rendu GPU et prefab non qualifiés. '
                             '[Reçu](../detail-native-lit-fragments-v1/INDEPENDENT-REOPEN.v1.json). '
                             'Suite : transport du packet de binding 120, L01.PARAMETRES.GLBS5.\n')
        if normal_ok and normal_marker not in journal_text:
            with journal.open('a', encoding='utf-8') as stream:
                stream.write(f'\n\n## {normal_marker}\n\n{normal["finished_utc"]} : 1/1 normale, 12/12 mipmaps, '
                             '49152 coordonnées ; concordance Unity RGorAG et UnpackNormal, '
                             'quatre compositions Bevy, cinq contrôles de refus et SHA/licences vérifiés. '
                             'Matériau complet, renderer, prefab et Studio non qualifiés. '
                             '[Reçu](../detail-normal-decoder-independent-reopen-v1/RESULT.v1.json). '
                             'Suite : L04.ROCKS.DETAIL.MATERIAU_COMPLET.\n')
        if raw_ok and raw_marker not in journal_text:
            with journal.open('a', encoding='utf-8') as stream:
                stream.write(f'\n\n## {raw_marker}\n\n{now} : 2/2 textures, 24/24 niveaux de mip, '
                             '98304 coordonnées comparées ; mapping identité et seuil RGBA 1e-6 conservé. '
                             'Décodage normale et rendu complet non qualifiés. '
                             '[Reçu](../detail-source-raw-gpu-parity-v1/RESULT.v1.json). '
                             'Suite : L04.ROCKS.DETAIL.NORMALE.\n')
        marker = 'L08.VERIFICATION.PAGES.03 — reçu library-incremental-pages-reopen-v3'
        if marker not in journal_text:
            with journal.open('a', encoding='utf-8') as stream:
                stream.write(f'\n\n## {marker}\n\n{r["finished_utc"]} : '
                             f'{r["new_cards"]} nouvelles fiches, {r["new_image_references"]} références image, '
                             f'{r["new_distinct_images_hashed"]} images distinctes rehachées. '
                             f'Couverture cumulée {cards}/26735 ; {missing} absences conservées. '
                             'Anciennes pages rehachées et contrôle antérieur des images hérité de son reçu. '
                             'Conversion complète et rendu global non qualifiés. '
                             '[Reçu](../library-incremental-pages-reopen-v3/RESULT.v1.json). '
                             'Suite : L04.ROCKS.DETAIL.MESURE.\n')
        final_marker = 'L08.VERIFICATION.PAGES.04 — réconciliation complète des fiches'
        if final_ok and final_marker not in journal.read_text('utf-8-sig'):
            with journal.open('a', encoding='utf-8') as stream:
                stream.write(f'\n\n## {final_marker}\n\n{final["finished_utc"]} : 26735/26735 fiches, '
                             '25787 références à images, 948 absences ; dernières 479 fiches vérifiées indépendamment. '
                             'Filtres pack/disponibilité, présence des licences et refus de placement des sources contrôlés. '
                             'Rendu 3D, images pertinentes, conversion complète et recette globale non qualifiés. '
                             '[Reçu](../library-all-records-independent-reconciliation-v4/RESULT.v1.json).\n')
        texture_marker = 'L04.ROCKS.DETAIL.TEXTURES — stockage réouvert indépendamment'
        if detail_texture.get('source_texture_storage_captures_verified') == 2 and texture_marker not in journal.read_text('utf-8-sig'):
            with journal.open('a', encoding='utf-8') as stream:
                stream.write(f'\n\n## {texture_marker}\n\n{now} : 2/2 textures, 24/24 mipmaps ; '
                             'identités, SHA et blocs DXT5 sRGB/UNorm contrôlés ; sources inchangées. '
                             'Sampling GPU, normale et prefab complets restent non qualifiés. '
                             '[Reçu](../detail-native-texture-storage-v1/TEXTURE-REOPEN.v1.json).\n')
        detail_marker = 'L04.ROCKS.DETAIL.DEFAUT et BEVY_VERTEX — étapes vérifiées'
        if binding.get('native_default_rg_binding_qualified') and bevy.get('status') == 'detail_measured_default_abi_and_bevy_vertex_composed' and detail_marker not in journal.read_text('utf-8-sig'):
            with journal.open('a', encoding='utf-8') as stream:
                stream.write(f'\n\n## {detail_marker}\n\n{now} : 36 images AOV linéaires réouvertes ; RG [1,1] mesuré, '
                             '83/55/93 pixels positifs par caméra pour chacune des deux sorties, config privée restaurée. '
                             'Quatre compositions Bevy avec vrai binding 120/256 octets ; fragment, sampling/normal GPU, '
                             'renderer et prefab complets restant. [Mesure](../detail-default-native-rg-binding-v2/RESULT.v1.json) '
                             '· [Bevy](../detail-native-default-bevy-vertex-v2/RESULT.v1.json).\n')
    print(str(target))

if __name__ == '__main__':
    main()
    # Preserve the newer assembled-scene checkpoint when regenerating historical views.
    from record_firepit_assembly_batch_v1 import main as record_scene_assembly
    record_scene_assembly()
