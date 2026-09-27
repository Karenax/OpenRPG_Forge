# Asset integrity — Hivemind Dark Fantasy Weapons

Date de l'audit : 2026-09-26
Périmètre : contrôle local, non mutant, du pack privé `hivemind.dark-fantasy-weapons` dans `E:\OpenRPG_AssetVault`. Aucun téléchargement, installation, conversion, copie d'asset, build Cargo, Studio ou runtime n'a été lancé.

## Référentiel inspecté

- registre : `E:\OpenRPG_AssetVault\metadata\asset-import-registry.v1.json` ;
- révision pointée : `7f17f8784df586a725925bbbc316d18fac8c7cc642fe790b9e7c57b052666f0e` ;
- catalogue : `normalized/hivemind/dark-fantasy-weapons-v1/catalog.v1.json` ;
- dérivés : `normalized/hivemind/dark-fantasy-weapons-v1/models/*.glb` ;
- détails/profils : `metadata/asset-import-revisions/hivemind.dark-fantasy-weapons/7f17f8784df586a725925bbbc316d18fac8c7cc642fe790b9e7c57b052666f0e/{details,profiles}.v1.json` ;
- rapport glTF adjacent : `normalized/hivemind/dark-fantasy-weapons-v1/validation.khronos.json`.

Le registre contient 17 assets dans chacun des quatre ensembles : catalogue, détails, profils et rapports glTF. Les trois ensembles d'identifiants sont sans doublon et leurs différences d'ensemble sont nulles. Les hashes du registre sont également intacts :

| Fichier de révision | Hash attendu par le registre | Hash physique | Résultat |
| --- | --- | --- | --- |
| `details.v1.json` | `36f6f0884e146c6c938214fd5dc7e4a1d823c5027445c25ee7e9f508bd3b699b` | `36f6f0884e146c6c938214fd5dc7e4a1d823c5027445c25ee7e9f508bd3b699b` | PASS |
| `profiles.v1.json` | `88c56f6e74173298926b463685aa84471ee561ee657b69bd981f974f5fcbb996` | `88c56f6e74173298926b463685aa84471ee561ee657b69bd981f974f5fcbb996` | PASS |

Le hash du catalogue est `8b16aa4dd7042ecdbd76d4ae1020d09ae2ca93e325f16bf500d3853c37b97005`. Le rapport glTF adjacent référence exactement ce hash.

## Hash des 17 GLB

Pour chaque ligne, le hash SHA-256 physique a été recalculé avec `Get-FileHash -Algorithm SHA256`. Il est identique à `catalog.assets[].sha256`, `details.assets[].model_sha256`, `profiles[].model_content_sha256`, `profiles[].dependency_closure_sha256` et `validation.khronos.reports[].sha256`. Le `model_path` de `details.v1.json` résout, sans sortie de pack, vers le fichier physique correspondant au chemin relatif indiqué.

| Asset | `catalog.model` / `details.model_path` | SHA-256 physique = toutes les références | Résultat |
| --- | --- | --- | --- |
| `arrow-var1` | `models/arrow-var1.glb` | `ea2d5049a02bb9617b235d98f8faf8f4cc0d2e284082cb0d6bfd73f8b2993f11` | PASS |
| `arrow-var2` | `models/arrow-var2.glb` | `f0b322b03b4ad6aedf7f86468ffecf5e94adab9159ad47704ffaaa66185a8867` | PASS |
| `axe` | `models/axe.glb` | `1aefe52f981f11ebac6502b514c6d36830a049dc36ef4ab9fae1ea605f1672b9` | PASS |
| `bow` | `models/bow.glb` | `d7041dfbcd1644eb441a14507b8938e80b5c091581e5dcb05e3b9fdaf703eab4` | PASS |
| `crossbow` | `models/crossbow.glb` | `a800877b7aad5b053dde2f7dbb2e8e28e31e98a449518f39772f70a67fbca7ee` | PASS |
| `dagger` | `models/dagger.glb` | `26ca15953c3ed785aa1fb40a15c9d5c3bba4ba4e438872fdafcc18f0f9fa5d23` | PASS |
| `mace` | `models/mace.glb` | `07fd40ca1ba219758d2bfc1ae003d8a4f1cd3c58366aaec6d1628c260e0c20bc` | PASS |
| `scythe` | `models/scythe.glb` | `8520db6f470eba1998d4ab440e33540ace200ef2864ecbb81c0ae24b1262aa34` | PASS |
| `shield-1` | `models/shield-1.glb` | `12eaea9795e4cf6e0d5ccefae7597add9bccd1846626947773619b9a74f4a77f` | PASS |
| `shield-2` | `models/shield-2.glb` | `041497414e7d057fb45c8a5f795cb7cdf38ec102fa7036eebd1386182d0f8633` | PASS |
| `shield-3` | `models/shield-3.glb` | `79d73e8acdc45a174926a76e008fae0ba27a58acc86b9279e26d0ad4ee830a76` | PASS |
| `spear` | `models/spear.glb` | `a2d8530cec527003fa21bf8ed65c5e92435d0b65ebcc18bc1243fc32e01507eb` | PASS |
| `staff` | `models/staff.glb` | `64cc7e6f5baadb8c1ea0d72349ea121ff11a22517496ec2a62ee32a39d4c19bf` | PASS |
| `sword-1` | `models/sword-1.glb` | `f10d2cff9f0ba8b1f4bc46276fefc29ebb1b1cd7cee863b64eea6d2854bdb77e` | PASS |
| `sword-2` | `models/sword-2.glb` | `3785196030806b6d2b7755a498bfa59bcaf32e3a9d499e828221341aa52b4a3a` | PASS |
| `sword-3` | `models/sword-3.glb` | `b4c114878bb0efd9335d0434b62ef43dc491e757f51b687bf35a287c9c8fd406` | PASS |
| `throwingaxe` | `models/throwingaxe.glb` | `891c128327bd89434f7f5fad25b30defdba6d209f2f2f473dd9e40011d99d109` | PASS |

## Cohérence géométrique et taxonomique

- 17 chemins `catalog.model` uniques, 17 GLB présents, 17 entrées de détails et 17 profils ; aucune entrée manquante ou excédentaire.
- Chaque `id` du catalogue est le `details.id` et le `profiles.external_id` correspondant ; références FBX et prefab concordantes pour les 17 assets.
- `catalog.dimensions_m` et `details.measurement.dimensions` concordent avec un écart absolu maximal de `3.85459413720923e-07 m`. Les bounds concordent avec un écart maximal de `9.68960214620118e-07 m` ; les tolérances d'audit sont `1e-6`.
- Triangles, vertices et listes de matériaux sont cohérents entre catalogue, détails et profils pour les 17 assets.
- Les 17 profils sont `family=equipment`. Le couple catalogue (`weapon`/`shield`), taxonomie détails et (`assembly_role`, `semantic_type`) profils ne présente aucun désaccord.
- Le manifeste source et l'archive conservée sont eux-mêmes intacts : `source_manifest_sha256 = 69196f0443fad88d4801d26933b69cc6436f81eff8dd05a89e791efb958df116` et archive SHA-256 `1af804b5b69a528f68c45e2a5815df495dd75721d4fd0c1489d08cf9ce34c0ed`.

## Validation glTF

Le validateur existant a été relancé sans réseau ni installation, vers un fichier temporaire hors Vault. Commande exécutée :

```powershell
node C:\Users\karen\.codex\skills\openrpg-import-assets\scripts\validate_glb.cjs `
  E:\OpenRPG_AssetVault\normalized\hivemind\dark-fantasy-weapons-v1\catalog.v1.json `
  E:\OpenRPG_AssetVault\cache\import-tools\hivemind-dark-fantasy-weapons-v1\validator\node_modules\gltf-validator `
  C:\Users\karen\AppData\Local\Temp\asset-integrity-rerun-38fc2c423a544437806247c5b8181514.json
```

Résultat du relancement : exit `0`, `assets=17`, `errors=0`, `warnings=0`, validateur `2.0.0-dev.3.10`, `catalog_sha256=8b16aa4dd7042ecdbd76d4ae1020d09ae2ca93e325f16bf500d3853c37b97005`. La preuve est conservée durablement sous `E:\OpenRPG_Forge\OpenRPG_Forge-Rust\output\gate-preparation-20260926\asset-integrity-rerun.json` : `61211` octets, SHA-256 `8964996e133a4cbd14df26f30a8a3d44ef1dabe4ef5ced979a938f5bba27942f`, 17 rapports, 0 erreur et 0 warning. La copie temporaire C sous `C:\Users\karen\AppData\Local\Temp\asset-integrity-rerun-38fc2c423a544437806247c5b8181514.json` est également conservée ; aucun asset n'a été copié. Le rapport adjacent donne le même résultat pour les 17 hashes actuels. Tous les rapports sont glTF `2.0`, sans animation, skin ou morph target, avec scène par défaut, sept ressources embarquées et aucune ressource externe détectée.

La version Blender prescrite a été vérifiée : `Blender 5.2.1 LTS` (`9e2066aef7ef`). Aucun fichier n'a été réouvert ou rendu par Blender pendant cet audit ; il n'y a donc pas de nouvelle preuve de rendu visuel.

## Droits et qualification

Le pack reste privé et non redistribuable comme bibliothèque :

- licence déclarée : `LicenseRef-Unity-Asset-Store-EULA` ;
- `private_only=true` pour le pack et les 17 détails ;
- `redistribution_as_asset_library=false` ;
- `license_verified=false` ; l'achat est déclaré par le propriétaire, mais le reçu et les exceptions juridiques spécifiques au pack ne sont pas audités.

La cohérence de fichiers est donc un **PASS d'intégrité locale**, pas une autorisation de publication. Les états sont indépendants : `converted`, `described`, `classified`, `geometry_measured`, `gltf_validated` et `metadata_ready` sont vrais pour 17/17 ; `studio_visible`, `placement_qualified`, `runtime_qualified` et `full_import` sont faux pour 17/17. Les adaptateurs indiqués dans les détails sont `vault_adapter=available`, `studio_adapter=unavailable` et `runtime_adapter=unavailable`.

Les 20 entrées d'ancres sont toutes des propositions `agent_review` (`position_status=proposed`, `orientation_status=unverified`, `human_review=0`). Les champs `collisions`, `lods`, `grip-orientation` et `gameplay` restent `not_configured` pour chacun des 17 assets. Les previews présentes dans le Vault ne sont pas utilisées comme preuve de rendu actuel.

**Verdict :** `asset-integrity=PASS` pour les octets, les pointeurs, la cohérence catalogue/détails/profils et la validation glTF ; `full-import=NOT QUALIFIED` volontairement. Les métadonnées et la politique privée observées imposent ici une conservation privée, et l'absence de preuve Studio, placement, équipement, collision, orientation, rendu actuel ou runtime interdit toute conclusion plus large.

## Contrôles indépendants demandés en parallèle (hors qualification du pack)

- P07 : `python -B scripts/Test-PlayerHandbookRules.py` — exit `0`, `13` tests exécutés, `OK`.
- P08 source JSON bestiaire uniquement : les quatre contrôles `schema JSON`, `roster et autorités`, `règles communes` et `parité des doubles pages` ont donné `4/4`. Aucun PDF, visionneur, génération ou UI n'a été lancé.
- Corpus de préparation : première exécution à `18/18`, puis deux contrôles négatifs ont été ajoutés pour les deux lacunes signalées ; exécution finale `python -B tools/gate_preparation/test_corpus.py` — exit `0`, `20` tests exécutés, `OK`. `test_outgoing_link_without_type_or_target_is_rejected` et `test_untrusted_content_cannot_invert_refusal_policy` passent. Les `65` cas d'acceptation restent des oracles de données : `product_tests_executed=false` et `gate_qualified=false`; aucun des cas produit n'a été exécuté.

Hashes SHA-256 exacts des cinq fichiers du corpus final :

| Fichier | Octets | SHA-256 |
| --- | ---: | --- |
| `tools/gate_preparation/README.md` | 2380 | `27d8ef661acdf040df7400d5b5bff343c5885c6f387bcf08bd9900702395b314` |
| `tools/gate_preparation/adventures.json` | 5621 | `889633ad494769dfc07459c1cae3cccf1ed9a3647762aa30f8f9c6505e038ab7` |
| `tools/gate_preparation/acceptance.json` | 13430 | `d455c41eda43f374b1e2fd497a76d66c36829c6b883c1ac141d26ab8ad30d53e` |
| `tools/gate_preparation/rules_vectors.json` | 1641 | `fa596d455e89a98c53112a11e29f4af7a93d3be03b8cc9d97652ae98e8ca8a4a` |
| `tools/gate_preparation/test_corpus.py` | 15626 | `d5e03a567ca8bc2eb8594295cf4768ebfb560f31b8edf134097b5df2e32417e4` |

Les deux lacunes précédentes sont donc levées uniquement pour ces invariants précis et avec preuve par les deux tests négatifs ; le corpus reste un contrôle de données de préparation, pas un validateur de sécurité complet ni une preuve produit.

## Commandes et limites

- Lecture initiale du worktree Rust (`git status --short --branch`) : arbre déjà fortement dirty, branche `Rust` ; les changements préexistants ont été conservés.
- Hashes physiques : `Get-FileHash -Algorithm SHA256` sur les 17 fichiers sous `normalized/.../models`.
- Validateur : commande ci-dessus, sortie temporaire hors Vault ; aucun manifeste Vault n'a été réécrit.
- Blender : `E:\Program Files\Blender 5.2\blender.exe --version`.
- Les tests synthétiques du skill d'import et `prepare/apply` n'ont pas été lancés afin de conserver l'audit du Vault strictement non mutant.
- Ce contrôle ne constitue ni un test Cargo, ni un test Studio, ni un test runtime, ni une revue humaine des ancres ou des matériaux.

Ce fichier est le seul livrable écrit par cette mission dans le worktree Rust ; aucun fichier du Vault, de ses manifests ou de la révision immuable n'a été modifié.
