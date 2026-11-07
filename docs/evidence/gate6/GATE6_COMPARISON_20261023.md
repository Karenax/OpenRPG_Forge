# Gate 6 — comparaison observée des parcours Studio

Le même état initial `d66d4114…` et les quatre commandes de workspace antérieures
servent aux deux parcours. Un opérateur Codex pilote les commandes ordinaires
du Studio ; le résultat cible est déjà connu lors du parcours manuel. Il s'agit
d'une comparaison de recette locale, pas d'une étude de vitesse humaine.
Les données exactes, contrôles de signatures fonctionnelles et limites sont
conservés dans `assistant-comparison-review-001.json`, campagne centrale
`Preuves-et-analyses/actives/gate6-ia-mcp/` (F:).

## Temps et travail constatés

| Mesure observée | Parcours avec assistant Qwen | Parcours manuel dans Studio |
| --- | --- | --- |
| Début de session | 22 octobre, 16:56:18 UTC | 22 octobre, 22:18:07 UTC |
| Jusqu'au décor complet sauvegardé | 13 030,305 s, environ 3 h 37 min | 4 550,633 s, environ 1 h 16 min |
| Jusqu'aux deux reprises natives vérifiées | 14 794,361 s, environ 4 h 07 min | 6 442,844 s, environ 1 h 47 min |
| Commandes de workspace depuis l'état commun | 23, dont annulations | 41 |
| Nouveaux reçus présents dans le head final | 2 IA + 15 humains | 41 humains |
| Appels du fournisseur produit | 3 résultats stagés, 1 job interrompu | Aucun |

Les durées incluent les inspections, latences d'outils, réparations, builds,
validations et travaux intercalés. Le parcours manuel inclut aussi l'interruption
pour diagnostiquer le serveur local. La comparaison ne permet donc ni ratio de
productivité, ni moyenne/p95, ni attribution causale des écarts à l'assistant.
Le temps plus court observé du manuel ne démontre pas un avantage général du
manuel : les réparations d'intégration pèsent fortement dans le premier parcours.

La première création IA sauvegardée seule prend 768,539 s depuis le lancement,
dont 318,634 s entre le clic Générer et l'observation du résultat stagé. Elle
comprend la quête, personnages, dialogues, trois lieux liés et un arbre ; les
retouches, variante et décor complet sont ultérieurs. Les trois salles et la
forêt finales demandent ensuite respectivement 262,714 et 534,563 s de commandes
ordinaires. Ces sous-phases ne remplacent pas la durée complète du tableau.

## Corrections et coût

Le parcours IA a nécessité une correction de représentation des identités et
liens de lieux avant création réussie. La porte et la réplique ont été retouchées
manuellement, puis une vraie variante Qwen a déplacé uniquement l'arbre sans
perdre ces retouches. Les maisons proposées ensuite ont été refusées à l'export
pour incompatibilité média, puis annulées. Le décor final est construit avec
trois pièces canoniques et six arbres par les outils ordinaires. L'export privé
du Vault et la présentation éclairée des intérieurs ont aussi été raccordés et
qualifiés pendant cette campagne ; ces travaux ne sont pas une durée fournisseur.

Le réglage manuel de récompense manquant est ajouté et qualifié **avant** le
chronométrage manuel. Pendant ce parcours, le choix initial de chêne est corrigé
par remplacement groupé des six références. Le lancement refuse d'abord le pin
du kit livré ; l'exporteur natif est corrigé, testé et rejoué sur le même head,
sans changer le décor pour contourner le refus. Ce travail intervient après la
mesure de sauvegarde complète et dans l'intervalle jusqu'aux reprises natives.
Les refus historiques restent conservés.

Les trois résultats fournisseur comptabilisés déclarent **8 197 tokens d'entrée
et 12 835 de sortie**. Le tarif local configuré est zéro, donc le coût produit
déclaré est zéro ; les appels sans résultat comptabilisé peuvent avoir consommé
des tokens inconnus. Les sondes et recettes séparées ne sont pas intégralement
mesurées par ces quatre journaux. Électricité, matériel et coût de l'opérateur
Codex ne sont mesurés pour aucun des deux parcours. « Aucun appel fournisseur »
du parcours manuel ne signifie donc pas « travail économiquement gratuit ».

## Qualité fonctionnelle et différences

Les deux résultats jouent les deux solutions de la même quête, attribuent un
seul Insigne, consomment Preuve seulement pour la collecte et ouvrent seulement
la nouvelle porte. Ils ont trois lieux avec quatre liaisons, trois pièces de
8 × 6 × 3 m aux mêmes positions et six transformations d'arbres identiques.
Les deux recettes entrent réellement dans le refuge par le KCC, sauvegardent
et reprennent exactement à l'intérieur, sans nouvelle action ou récompense.
Les scénarios natifs emploient les commandes normales ; ils sont distincts des
clics UI qui prouvent la création, les retouches et le lancement depuis Studio.

Les modèles d'arbres diffèrent : Vault `9bbec162…` / `04471efb…`, 9 419 256 octets,
contre kit livré `68534a54…` / `c5c0ffe8…`, 10 177 164 octets. Les noms manuels
de pièces restent « Pièce » et ceux des arbres gardent le libellé du chêne après
remplacement ; la référence IA possède des noms de salles explicites. Les PNJ
manuels sont centrés à Y=0,97 ; les définitions IA utilisent Y=0. Les valeurs
physiques du ramassage et de la porte, la limite de pile de Preuve (24 contre 1)
et la visibilité du retour par témoignage diffèrent aussi. La validation métier
de solution exige toujours la quête active. Aucune identité de géométrie, de
qualité artistique ou de l'ensemble des champs d'auteur n'est revendiquée.

Les quatre captures manuelles d'intérieur/reprise et celles de la référence
sont relues pour leur lisibilité fonctionnelle : quête terminée, Insigne ×1,
porte ouverte, sauvegarde reprise et pièce visible. Les matériaux, personnages
de recette et éclairages restent provisoires. Ce bilan satisfait la comparaison
temps/corrections/coût/qualité de R6-P07 ; il ne démontre pas un gain général
d'efficacité ni une acceptation esthétique du propriétaire.

Preuves principales : `assistant-comparison-provider-inputs-001.json`,
`assistant-manual-authoring-source-001.json`, `assistant-kit-export-review-001.json`,
`assistant-reference-native-review-001.json`,
`native-manual-{collection,witness}-001/{verification,resume/verification}.json`.
