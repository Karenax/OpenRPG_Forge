# Dette d'architecture moteur — analyse et corrections du 17 octobre 2026

Complément du 18 octobre : la [décision de séparation stricte](../ADR/ADR-0026-strict-engine-map-separation.md)
sort les ports, acquisitions et notices de carte du moteur et de la fermeture
de ses dépendances de domaine. Les constats et compteurs de ce document restent
ceux du 17 octobre ; ils ne décrivent pas les sources après cette séparation.

## Portée et verdict

L'objectif de moteur générique reste **incomplet**. Les clôtures Gate 3 et Gate 4
qualifient leurs parcours et leurs entrées identifiées, pas l'achèvement de
l'extraction de toutes les capacités importées. Cette correction de code ne
requalifie aucun binaire déjà installé ni les preuves GPU historiques.

Le [contexte canonique](../../PROJECT_CONTEXT.md) conserve l'exigence propriétaire
du 8 octobre : les capacités moteur, leurs noms et contrats doivent être
indépendants de Tidewater et réutilisables sur une autre scène. Les mentions de
provenance/licence et les contenus de référence conservent leurs noms.

## Analyse reproductible

Le point de départ est `3c3a5efeba2473574597871f223d74aa800cf99a` plus les travaux
locaux déjà présents, préservés. Le périmètre explicite de comptage est le Rust
suivi sous `crates/forge_engine/src`, commentaires de provenance inclus :
**1 320 occurrences de tidewater dans 166 fichiers** avant cette correction.
Le chiffre de 1 449 de la première passe n'a pas de commande de comptage fournie ;
il ne sert donc pas de seuil de comparaison.

Les exemples du moteur contenaient 222 fichiers Rust et 68 912 lignes physiques,
sous-modules compris. Sept familles présentent des copies identiques à des
commentaires/lignes vides près, soit 21 entrées et **7 011 lignes de copies**.
Les autres variantes proches ne sont pas toutes identiques : certaines modifient
le format de cible GPU, le décodage sRGB, le canal ou les sorties de diagnostic.
Le chiffre « 21 k lignes dupliquées » ne permet pas de supprimer ces différences
sans analyse des protocoles et des sorties.

Une dépendance compilée directe vers `scripts/hivemind/candidates` est confirmée :
`verify_townsmith_material_layouts.rs` incluait
`SourceTownsmithBevyPipelineAdmissionV1.rs`. Une deuxième inclusion Rust n'a pas
été trouvée. Un second couplage de workflow est en revanche explicite : le
validateur commun et le builder de livraison gardaient les candidats Rust,
shaders et autres acquisitions comme entrées bloquantes de compilation parce
que l'ancien include était encore consommé. Les scripts Python seuls étaient
exclus. Ce couplage d'inventaire est distinct d'une seconde inclusion compilée.

Les corrections de zones atteignent v17. Les archives atteignent v48 parce
qu'elles combinent capacités et trois encodages : complet, compact et delta.
Les premiers numéros sont irréguliers : les chemins utilisent 8/9/7. Un passage
artificiel à « v1 » ou l'abandon des anciennes versions casserait les documents.

## Corrections apportées

### Propriété du code compilé et exemples

Les sept implémentations sont désormais dans `forge_engine/examples/support`.
Les 21 anciens noms Cargo sont conservés comme entrées minces, avec les mêmes
arguments, calculs et noms de sorties. Aucune variante de protocole différente
n'est fusionnée pour obtenir un meilleur chiffre.

La vérification des layouts utilise aussi un harness possédé par le moteur.
Le candidat historique d'orchestration reste intact, mais le build n'en dépend
plus. Le probe déplacé a aussi été découpé en initialisation GPU, choix de
layout et vérification d'un cas ; sa fonction monolithique de 225 lignes n'est
pas conservée derrière une suppression de lint.
Le [manifeste de consolidation](../evidence/architecture/EXAMPLE_CONSOLIDATION_20261017.json)
conserve les chemins, hashes et nombres de lignes avant factorisation.

Le validateur et le builder classent désormais l'ensemble de l'orchestration et
des acquisitions dans l'inventaire informatif du checkout. Avant de les exclure
de la carte de compilation, le validateur refuse les inclusions Rust explicites
vers ce dossier, y compris `concat!`/`env!`, ainsi que les chemins Cargo de
dépendances, de targets et de membres du workspace. Des cas négatifs vérifient
ces refus avant tout inventaire Git.
Les harnesses possédés par le produit et les assets statiques réellement consommés
restent épinglés. Les anciens reçus ne sont pas modifiés ou réétiquetés.

### Premières capacités génériques

- `exposure_metering` expose un `ExposureProfile` paramétrable : cible de
  luminance, adaptation et bornes. Les entrées invalides sont refusées ; les
  valeurs par défaut conservent la réponse antérieure. Studio et jeu natif
  utilisent cette API commune, sans alias `tidewater_exposure`.
  Le test du second profil indépendant a révélé une discontinuité d'arrondi
  entre segments polynomiaux, refusée par Bevy. Chaque segment commence désormais
  à l'extrémité effectivement calculée du précédent ; les coefficients du profil
  par défaut sont comparés bit à bit à la construction d'origine.
- `water_height_queries` possède le registre borné et le protocole de requêtes
  d'eau. Les consommateurs utilisent ce module sans alias `tidewater_water_query`.
  Deux scènes indépendantes exercent leurs registres sans partager slots ou poses.

Les attributions d'origine sont conservées. Ces tests CPU prouvent le contrat
et l'indépendance des entrées, pas le rendu ou le readback GPU sur une autre carte.

### Compatibilité des archives

Une table interne unique dans `zone_authoring/session/versions.rs` décrit les
familles et leurs codecs. Le dispatcher et les codecs compact/delta l'utilisent
pour choisir et vérifier les versions, sans changer les formats ni les données.
Le choix des capacités depuis les reçus reste dans le noyau de session.

Le contrat d'archives teste la conservation des numéros historiques irréguliers,
la réouverture, l'égalité des octets réémis, l'audit, Undo et les refus de versions
futures ou de codecs incompatibles. Les 48 numéros sont contrôlés pour leur
routage univoque. Ces contrôles ne remplacent pas les autres tests de domaines.

### Prévention des régressions

`scripts/test_engine_boundaries.py` contrôle les entrées compilées directes vers
l'orchestration, les grandes copies d'exemples et la réintroduction des deux
anciennes API extraites. Il est appelé par `Test-Rust.ps1` et le workflow natif.
`scripts/audit_engine_architecture.py` produit un inventaire statique versionné
avec un périmètre explicite ; le nombre d'occurrences inclut les attributions
et ne constitue pas une preuve de généricité.

## Travail restant pour satisfaire l'objectif complet

| Lot | Frontière à extraire | Résultat attendu avant clôture |
|---|---|---|
| Recettes et données | `tidewater_assets`, terrain importé, composition de scène | Les identités, chemins, hashes et assemblages spécifiques sont des données de module ; le moteur reçoit des contrats typés. |
| Atmosphère | ciel, nuages, vent, haze, éclairage et post-traitement | Paramètres et plugins communs, sans dépendance à la carte ; régression du rendu et seconde scène indépendante. L'exposition n'est que la première extraction de cette famille. |
| Eau et surface | océan, rivage, underwater, refraction et requêtes de surface | Graphe GPU et paramètres communs, ABI/shaders vérifiés, entrée terrain/eau injectée ; le registre de requêtes extrait ne solde pas cette famille. |
| Objets et matériaux | maillages, matériaux, végétation, bâtiments et props importés | Chargeurs et projections réutilisables ; les sélections de contenus restent dans le module, avec provenance conservée. |
| Contrôleur et locomotion | contrôleur importé, nage, contacts et accès aux surfaces | Comportements paramétrables et fournisseurs de surface indépendants ; aucune sélection de carte par nom ou slug. |
| Qualification et exemples | variantes de probes encore proches et scènes de référence | Harnesses par protocole explicite, références historiques conservées et entrées compatibles ; aucune confusion HDR/sRGB/profondeur. |
| Formats | heads, archives, compaction et reprise | Compatibilité byte/hash/replay et refus inchangés pour les données historiques ; toute nouvelle migration est spécifiée et testée avant activation. |

Cette décomposition décrit le reste du travail autorisé, pas des capacités déjà
livrées. Les lots moteur nécessitent chacun leur preuve native et WebGPU ;
ils doivent être intégrés sans copies de produit par conversation.

1. Classer les capacités restantes : atmosphère/post-traitement, eau et surface,
   matériaux/maillages, locomotion et composition d'assets. Pour chacune, séparer
   les paramètres moteur des recettes et identités de la carte importée.
2. Extraire chaque capacité par contrat, puis migrer Studio, runtime, shaders et
   diagnostics ensemble. Un renommage global ne supprime pas les hypothèses de
   contenu, d'échelle ou de composition ; les données historiques restent lisibles.
3. Acquérir une preuve d'usage indépendante pour chaque famille sur natif et
   WebGPU, puis une régression de la scène Tidewater, avec nouveaux reçus et captures.
4. Étudier les variantes d'exemples encore proches par protocole. Mutualiser les
   parties réellement communes sans fusionner les formats de capture ni les verdicts.
   Séparer aussi les cibles produit de l'outillage de qualification : le test
   global lie aujourd'hui de nombreux exécutables Bevy de preuve. Cette séparation
   doit migrer leurs appels et conserver les vérifications explicites ; désactiver
   discrètement des cibles ou supprimer des tests n'est pas une correction.
5. Continuer la stabilisation des schémas avec fixtures historiques et matrice de
   migrations ; toute future simplification de format exige un contrat de migration
   explicite, pas une renumérotation.

La réserve de généricité reste ouverte tant que ces étapes ne sont pas prouvées.
Les décisions de gates et les résultats historiques ne sont pas réécrits pour
masquer cette dette.

## Validation de cette correction

Les résultats et limites de l'exécution sont conservés dans le
[bilan de validation](../evidence/architecture/VALIDATION_20261017.md).

La compilation WASM de Studio passe, mais son Clippy strict expose 80 diagnostics
dans des fichiers inchangés. Cette dette distincte reste ouverte ; les emplacements
et hashes sont consignés dans le bilan, sans suppression des lints.

## Reprise du 18 octobre

Le [bilan de reprise](ENGINE_RESERVES_20261018.md) détaille la correction des
diagnostics Studio et les extractions supplémentaires. Les résultats du 17 restent
datés ; leur réserve Clippy ne remplace pas les contrôles plus récents.
