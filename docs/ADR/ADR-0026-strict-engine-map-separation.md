# ADR-0026 — Séparation stricte du moteur et de la carte intégrée

Validation du 18 octobre : [rapport de séparation moteur/carte](../evidence/architecture/STRICT_MAP_SEPARATION_20261018.md) ;
14 contrôles réussis et 2 232 tests du workspace réussis. Les preuves de runtime
générique et des extensions optiques gardent des périmètres distincts.

Date : 18 octobre 2026. Décision propriétaire : aucune dépendance ni provenance
Tidewater dans le moteur ; la carte utilise les capacités du moteur.

Les renommages précédents ne suffisaient pas : les implémentations portées,
acquisitions, shaders et adaptateurs de carte restaient compilés dans le moteur.
Les retirer des commentaires aurait laissé les mêmes dépendances et sources.

## Décision

`forge_engine` et `forge_assets` ne contiennent plus ces sources, acquisitions,
tests ou fixtures. `forge_tidewater` possède les projections et ports de carte ;
`forge_tidewater_assets` possède ses paquets et acquisitions. Le graphe autorise
uniquement les dépendances de ces composants vers les contrats génériques.
Aucune réexportation de compatibilité ne maintient l'ancienne API dans le moteur.
Les imports des consommateurs indiquent explicitement le propriétaire de l'API.

L'audit couvre également les dépendances de domaine du moteur. Les ports CDLOD,
bornes de culling et empaquetage de végétation ont quitté `forge_world` pour
`forge_tidewater_assets`. Le domaine expose seulement le raster validé et ses
échantillons en lecture seule aux adaptateurs. Les exemples spécifiques suivent
ce nouveau propriétaire ; les tests génériques utilisent des identités neutres.

Les notices existantes suivent les fichiers portés, hors du moteur. Cette
opération n'efface ni la provenance des acquisitions ni les preuves datées.
Le moteur garde ses propres licences et celles de ses dépendances génériques.

`PlayerMotionExtension` transporte uniquement des observations éphémères :
œil projeté, attente du backend, correction verticale et contact humide local.
L'extension publie après sa résolution physique, dans `PlayerMotionPublication`,
entre Observe et Camera. Le moteur ordonne également les observations de gameplay
après cette publication. La présence de ce contrat remplace
le pilote standard ; son absence conserve le pilote normal. Le moteur ne lit
plus `AmphibiousPlayerState`. `PlayerSpawnSet` ordonne l'initialisation sans
exposer les types privés du système de création.

Les captures hors écran portent `OffscreenSceneCamera`. Le picking du terrain
ne dépend plus des types de caméra de Tidewater.

La configuration sans fonctionnalités conserve le rendu de base et les contrats
d'assets. Les projections exigeant les API de physique/authoring sont activées
avec leur fonctionnalité `physics-rapier`, également utilisée par `native` et
`web`. Sans le backend de qualité, le LOD conserve sa représentation sélectionnée
et n'invente pas de reçu GPU autorisant une transition.

## Validation et limites

Les gardes examinent la totalité des deux crates génériques, commentaires et
tests compris, et leur fermeture de dépendances locales. Les tests existants
des ports suivent leur nouveau propriétaire ; leurs assertions ne sont pas
affaiblies. Un test moteur utilise un fournisseur de mouvement indépendant,
vérifie son œil projeté et la reprise du contrôleur standard après retrait.

La qualification des fonctionnalités de chaque port, la parité visuelle de
la carte complète, les installations déjà présentes et les anciennes preuves
restent distinctes de la séparation des dépendances. Le compte nul de noms
dans le moteur ne qualifie pas à lui seul tous les effets graphiques.
