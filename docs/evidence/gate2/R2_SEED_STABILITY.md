# R2-018 — stabilité de la seed végétale

15 septembre 2026, branche Rust, qualification fonctionnelle locale.

## Verdict

R2-018 satisfait : à documents et seed identiques, la génération reproduit les
chunks et instances ; changer la seed change la distribution sans supprimer les
corrections authorées. Le réglage est accessible par le Studio et sauvegardable.
Ce verdict ne clôture ni R2-C ni Gate 2 et n'ajoute aucune validation humaine.
Il ne promet pas le même hash de reçu/cache après changement de révision, ni une
identité binaire GPU entre machines ou versions futures du générateur.

## Implémentation inspectée

`forge_world::vegetation` calcule l'entropie depuis seed, ID stable de couche,
cellule et tentative. Les IDs utilisent projet/zone, seeds terrain/végétation,
résolution et coordonnées, pas l'ordre du journal ni sa révision. Filtres de
masques, exclusions, pente/altitude et matériaux restent actifs.
`editor_vegetation::prepare_request` ne change que la seed végétale, via la
régénération de zone ; refus sans changement, aperçu, reçu/inverse et audit
restent ceux du bus partagé. `authoring_zone` expose cette demande dans le Studio.

## Preuve actuelle ciblée

`authored_seed_round_trip_restores_exact_instances_after_archive_reopen`, dans
`crates/forge_world/tests/editor_vegetation.rs` : couche peinte et verrouillée,
génération 42 répétée exactement, seed 43 différente, relecture d'archive,
retour à 42 puis seconde relecture. Comparaison intégrale des chunks et instances
réussie malgré la nouvelle révision ; terrain, recette finale et corrections
strictement conservés. Ce sont des générations CPU réelles, pas une simple
comparaison de nombres de seed ou de tailles de tableaux.

Test ciblé PASS : compilation 1,32 s, exécution 0,08 s. Clippy test strict PASS,
0,42 s. Premier essai : erreur de compilation du test (déplacement depuis Arc),
corrigée par clonage du résultat pour la comparaison ; aucun code produit changé.

## Parcours Studio et fichiers recoupés

Les gestes de seed/Save et conservation de clairière/porte sont ceux décrits dans
`R2_ZONE_REGENERATION.md` (A64D5ABA), suivis de la réouverture F1C5DB66.
Le replay densité 975b9033 de `R2_VEGETATION_AUTHORING.md` apporte aussi le cycle
peinture/verrou/seed 43→44/Save/Close/Open, sans JSON manuel.

Recontrôle actuel des fichiers du parcours A64/F1, sous
`E:/OpenRPG_Forge/.gate2-evidence/` :

- `retention-ui-20260914-0900/before-regeneration.json` :
  CF67FD4C6FD406E704701EC4622880B55413220E736AE50213A915ABAC6659F1.
- `retention-ui-20260914-0900/81de8d42-89b9-58b5-b090-406cf4c63d06/authoring.json`
  et même chemin de zone dans `scene-index-ui-20260914` :
  5269A9B0F430785FE9A307928B615E716EF0CD89C33F204A61142CD1A0BE7D20.

Le décodeur produit `inspect_zone_archive summary` relit avant/après : seeds
42/43, hashes corrections et terrain de base identiques, neuf reçus après,
tête 7b962cbc3496082108d65deda6894503151e796b75083cc55ba6feaec38f5f54.
Aucun fichier modifié par cette relecture, aucun nouveau lancement graphique,
aucune publication. CI distante : NOT RUN — infrastructure billing.
