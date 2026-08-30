# ADR-0060 — Qualification runtime et revue visuelle des cartes générées

## Statut

Accepté — 29 août 2026.

## Contexte

Le pipeline de génération savait prouver le déterminisme, les fournisseurs, les empreintes, la
collision, la navigation et la sérialisation de la scène dérivée. Ces preuves pouvaient néanmoins
valider une carte dont le résultat joueur restait manifestement insuffisant : végétation absente ou
trop diffuse, sol sans texture résolue, maisons flottantes, placeables utilisés comme personnages,
PNJ enterrés, vitesse incompatible avec l'animation et caméra régressée.

Les captures techniques du worker ne chargeaient pas nécessairement la zone complète après la
construction assistée. De plus, une chaîne de provenance libre ne prouvait pas qu'un agent ou un
humain avait réellement regardé le résultat.

## Décision

Une carte construite possède désormais deux gates successives et non substituables.

1. Le runtime charge le vrai workspace en Forward+ Vulkan et produit un audit machine versionné. Il
   mesure l'espacement d'échantillonnage, les textures réellement résolues, les couches de peinture et
   leur couverture, les strates et instances MultiMesh matérialisées, la densité locale de couverture
   du sol, les groupes de bâtiments, les fondations et leur bas visuel, les intersections entre
   végétation et bâtiments, les acteurs de patrouille, leur visuel gouverné, leur animation, leur
   mouvement, leur vitesse et leur contact au terrain sur plusieurs frames. Pour chaque entrée de
   bâtiment, il mesure les bornes réelles de la feuille fermée et de l'escalier dans le repère de
   l'encadrement, contrôle collision et interaction, puis vérifie l'ouverture et la fermeture. Les
   origines logiques ne suffisent plus. Il injecte aussi de vraies entrées orbit/zoom.
2. Le runtime produit trois captures de base liées par SHA-256 : ensemble, aérienne et niveau du sol.
   Il ajoute une capture centrée sur un patrouilleur et/ou un bâtiment dès que le brief en demande ;
   un bâtiment impose en plus deux gros plans identiques `door-closed` et `door-open`. Il refuse les
   corridors de cadrage obstrués par la végétation haute. Un réviseur inspecte toutes les
   vues obligatoires puis enregistre séparément une acceptation explicite portant sur le terrain, la
   végétation, les bâtiments, les entrées alignées et opérables, les personnages et la lisibilité des
   cadrages.

Le succès machine s'appelle explicitement `MACHINE_PASS`. Seule la revue visuelle peut émettre le
marqueur final `OPENRPG_FORGE_MAP_QUALITY_PASS`. Les nombres demandés dans le brief sont transmis au
runtime comme minima bloquants, afin qu'une omission ne devienne pas un résultat valide. La revue
finale doit nommer la baseline produit utilisée et attester explicitement qu'elle n'a pas régressé ;
cette attestation de non-régression ne remplace pas la gate distincte prouvant un gain.

## Conséquences

- Une carte ne peut plus être présentée sur la seule base de fingerprints, de nombres globaux ou de
  tests unitaires.
- Les régressions connues ont des fixtures négatives ciblées dans la politique d'audit pure.
- Les captures restent des preuves, pas une approbation automatique : une image visiblement pauvre
  ou incohérente bloque le lot.
- Les graphes déterministes ne constituent pas une preuve d'amélioration artistique. La comparaison
  avec le backend produit de référence et sa gate de gain restent obligatoires avant adoption.
- Les cartes non verdoyantes peuvent expliciter zéro strate ou zéro densité ; les cartes extérieures
  utilisent par défaut un terrain texturé, trois strates et une couverture locale minimale.
- Cette gate intervient après la construction complète. Elle complète la promotion du terrain et ne
  remplace ni les validations de navigation ni les preuves déterministes.
