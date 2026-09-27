# Passes d'ombre partagées avec la scène couleur

Les passes auparavant contenues dans l'exécutable headless sont maintenant
dans `tidewater_object_shadow_probe/mod.rs`. L'exécutable de qualification
et le prototype de scène appellent cette même implémentation. La variante
`tidewater_plant_probe FIXTURE NEW_PNG --scene-shadow-depth` installe les
trois cascades sur le device et le graphe de rendu de la scène Bevy, avant
le rendu caméra. Elle attend leur calcul puis sauvegarde le tableau de
profondeur dans `NEW_PNG` avec l'extension `.object-depth.bin`.

Le filtre n'est pas encore branché aux matériaux : cette étape vérifie la
coexistence des passes et fournit la texture native à raccorder. Le rendu
couleur ne doit donc pas encore changer. Aucun fichier de profondeur source
n'est chargé pour produire le rendu natif. La sortie reste statique et ne
qualifie ni les invalidations interactives ni Studio.

## Contrôles exécutés

- Build des deux exécutables et Clippy strict : réussis.
- Village : profondeur du nouvel exécutable headless et profondeur calculée
  dans la scène couleur identiques à la capture native précédente.
- Forêt : profondeur calculée dans la scène couleur identique à la capture
  native précédente, avec 361 instances de canopée.
- Village : PNG couleur et texture d'ombre du relief inchangés à l'octet.
- Les deux scènes terminent normalement après capture et lecture GPU.

Ces égalités vérifient la conservation du comportement natif pendant le
raccordement. Elles ne corrigent pas les écarts végétaux avec Tidewater
documentés dans [la comparaison précédente](2026-09-24-plant-shadow-depth.md).

## Preuves

Racine `E:\OpenRPG_Forge\.tidewater-integration` :

- `shared-shadow-build-002.log`, `shared-shadow-clippy-001.log`.
- `shared-headless-depth-001.bin` et son journal.
- `shared-scene-depth-001.png`, `.shadow.bin`, `.object-depth.bin` et journal.
- `shared-canopy-depth-001.png`, `.shadow.bin`, `.object-depth.bin` et journal.
- `shared-shadow-regression-001.json` : quatre comparaisons exactes.
- `shared-shadow-forest-001.json` : profondeur forêt exacte et hashes des
  deux exécutables utilisés.

SHA-256 profondeur village :
`68c86016224c690f6c1891dd6780eae9a3f4933940931380914b7790a1c5000d`.
SHA-256 profondeur forêt :
`1a1906f797cda5e2c7b7c0b2df503ce2ec4b300adee1f97ba73084e717b04f7d`.
SHA-256 PNG village :
`195474da178851f6e6a6c972a4c59ae9dd55effeafb2eddc4ec186354dcf8484`.

La prochaine étape est la réception PCSS/PCF dans terrain, village,
végétation et herbe, puis sa comparaison avec la scène source appariée.
Pas de verdict de parité complète ni publication.
