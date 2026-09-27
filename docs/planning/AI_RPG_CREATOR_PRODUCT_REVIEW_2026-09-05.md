# Revue produit — créer un RPG avec l'IA et conserver la maîtrise du détail

Date : 5 septembre 2026. Statut : **analyse et propositions, aucune nouvelle gate autorisée**.
Demande : appliquer les améliorations de consignes approuvées, puis réexaminer la documentation
à partir du résultat final attendu : créer un RPG et son éditeur avec l'IA et permettre les retouches
manuelles précises. Ce rapport est un audit daté, pas un contexte canonique concurrent.

## 1. Conclusion produit

La direction existante est solide : documents moteur-neutres, commandes partagées, génération
déterministe, WorldSketch, provenance et séparation Studio/runtime. L'amélioration décisive consiste
à transformer ces fondations en une boucle complète dont le créateur garde la maîtrise :

**décrire → obtenir une proposition jouable → corriger à la main → demander une nouvelle variante
sans perdre ces corrections → tester une aventure → sauvegarder → distribuer.**

Les documents spécifient déjà beaucoup de briques. Le manque principal est le contrat observable
entre elles : ce qui reste identique après régénération, ce qui est invalidé après retouche, ce que
l'IA a le droit de changer, et la façon de constater qu'un RPG est jouable et intéressant.

Deux boucles doivent être distinguées. Codex développe le moteur et le Studio ; l'assistant du
produit aide le créateur à construire son module. La première ne prouve pas la seconde. Un RPG
fabriqué par des scripts de développement n'est pas une preuve qu'un utilisateur peut le créer
dans le logiciel livré. Un éditeur adapté au jeu devrait d'abord être une configuration d'un Studio
commun, avec des inspecteurs issus de schémas, plutôt qu'un nouvel exécutable généré pour chaque RPG.

## 2. Périmètre et niveau de preuve

Audit du workspace Rust local sur `f786a38aca2023f08d708687f99a47d14f34e6d4`, avec ses changements
préexistants conservés. Référence historique C#/Godot inspectée sur le checkout détaché
`a7f4a3f5dd0eeff1dc060ea1237b1d7d650b7ca0`. Aucun fetch, commit, push ou changement de branche.

Le recensement et la recherche transversale ont lu 737 fichiers Markdown/CSV/YAML, soit 74 009 lignes :
66 fichiers Rust et 671 historiques. Les axes de recherche couvrent retouches/verrous, parcours
d'auteur, gouvernance IA, reprise, identités/conflits et complétude RPG. Les documents directeurs,
contrats de monde, de règles et de création ont fait l'objet d'une lecture approfondie ; des passages
de code ont été recoupés pour distinguer objectif et implémentation. Ce recensement ne signifie pas
une revue ligne par ligne des 737 fichiers ni une réexécution de leurs anciennes preuves.

Les sources tierces, dossiers de sortie, clones de preuve, snapshots `Divers`, fichiers ignorés,
images, PDF et ledgers JSON ne font pas partie de ce total. Ils ne sont pas traités comme instructions
actives. Inventaire avec chemins, nombres de lignes et SHA-256, et résultats de recherche conservés
localement dans `C:/Users/karen/.codex/tmp/forge-docs-review-20260905/`.

Aucun comportement logiciel n'a été qualifié à nouveau pendant cet audit. Les valeurs citées dans
les anciens rapports sont des preuves historiques, pas des mesures du présent tour. La progression
reste celle du contexte et des ADR : Gate 1 active ; aucune fermeture humaine ou CI distante verte
n'est déduite de cette revue.

## 3. Ce qui existe, ce qui est prévu, ce qui reste à démontrer

| Capacité | État observé dans ce checkout | Conséquence pour le produit |
| --- | --- | --- |
| Studio Tauri/Leptos et viewport Bevy | Fondation documentée ; `StudioCommand` expose `SelectEntity` et `SceneSummary`. Le backend de ces commandes charge la preuve Gate 0. | Ne pas annoncer un éditeur de projet complet à partir de la viabilité de la coque. |
| WorldSketch et compilation | Types, validation et compilateur présents. Compilation bornée à Visual Valley 512 × 512 m. | Bon pilote ; indépendance de plusieurs vrais modules à prouver avant généralisation. |
| Verrous | `locked_feature_ids` validés pour existence/ordre et transmis à Blender. Le compilateur prend un seul sketch, sans ancienne base ni couche manuelle en entrée. | Ces fonctions ne constituent pas une preuve de fusion préservant les retouches lors d'une régénération. |
| Retouche précise | Brosses, splines, gizmos, snap, promotion/retouche et undo sont explicitement prévus en Gate 2. | Préserver ces exigences ; les réunir dans un parcours observé et dans un contrat de persistance. |
| RPG | Gate 3 définit déjà quête, dialogue, interaction environnementale, sauvegarde/reprise, authorés depuis Studio. | Excellent critère de sortie à rendre central dans le suivi produit. |
| Assistance IA du produit | Gate 6 et matrice de reprise diffèrent assistant/MCP. | Risque de découvrir tard les limites de l'édition mixte IA/manuelle. |
| Règles sans classe | Manuel et contrat détaillés ; README distingue proposition et implémentation. | Ne pas confondre livre terminé et gameplay Rust livré. |
| Export et jeu autonome | `.orpmod` v2 prévu Gate 4, distribution Gate 7. | Une démo dans l'éditeur ne démontre pas encore un jeu transmissible. |

Sources principales : [Studio](../../apps/forge_studio/README.md),
[commandes Studio](../../apps/forge_studio/src/lib.rs),
[backend Studio](../../apps/forge_studio/src/main.rs),
[WorldSketch](../../crates/forge_world/src/world_sketch.rs),
[roadmap](MIGRATION_ROADMAP.md), [matrice de reprise](../migration/LEGACY_REUSE_MATRIX.md),
[design et règles](../design/README.md).

## 4. P0 — Préserver les retouches lors de toute régénération

**Constat.** Le contexte §5/O6 prévoit couches, placements et overrides ; ADR-0008 prévoit des verrous.
Il ne suffit pas de vérifier qu'un ID verrouillé existe. Il faut définir ce qui est protégé lorsqu'une
nouvelle génération modifie sa base. Cette propriété n'est pas démontrée par le compilateur inspecté.

**Proposition.** Définir un contrat unique d'édition combinant base générée, interventions manuelles
persistantes et résultat résolu. Une sortie de compilation reste un cache ; toute donnée promue
devient explicitement authorée et sa provenance reste enregistrée. Chaque propriété a un propriétaire
et une politique : suivie par le générateur, remplacée manuellement, verrouillée, ou volontairement
détachée. Un override doit survivre à la fermeture de l'historique undo et au redémarrage.

Lors d'une régénération, comparer ancienne base, modifications humaines et nouvelle proposition.
Fusionner les modifications disjointes ; présenter les conflits concrets sans écrasement silencieux.
Un verrou peut protéger une transformation, la silhouette d'un bâtiment, une zone, un asset ou un
contenu narratif ; il ne doit pas forcément immobiliser tout l'objet et ses dépendances.

**Preuve de sortie proposée.** Déplacer et verrouiller une porte, peindre une clairière, retoucher un
toit et une réplique ; régénérer la végétation et changer la rivière. Les valeurs protégées restent
exactes après sauvegarde/réouverture. Si la nouvelle berge rend la porte inaccessible, le conflit de
gameplay est signalé plutôt que corrigé en déplaçant la porte verrouillée.

**Dépendances.** Documents/commandes/undo de Gate 2 ; ADR de persistance et de fusion avant adoption.
Les références sont [contexte §5/O6 et §8](../../PROJECT_CONTEXT.md),
[ADR-0008](../ADR/ADR-0008-world-sketch-image-to-world.md) et `locked_feature_ids` dans WorldSketch.

## 5. P0 — Stabiliser les identités au-delà du déterminisme d'une seed

**Constat.** Le contexte §8.3 propose une identité générée depuis « recette, seed, étape et index
local ». C'est reproductible à entrées identiques, mais une insertion ou un nouvel ordre peut changer
l'index des autres éléments. La formulation ne garantit donc pas la survie des liens de quête,
overrides, sauvegardes et verrous après une modification du générateur.

**Proposition.** Définir des identités persistantes de features et une filiation des objets générés.
Une insertion locale ne renumérote pas les objets existants ; suppression, remplacement, division ou
fusion produisent un remappage explicite ou un diagnostic de référence orpheline. Les indices restent
des détails de génération, pas l'unique ancrage d'une identité authorée. En cas de changement de seed,
ne pas inventer une correspondance : préserver, proposer un remappage ou demander une résolution.

**Preuve.** Ajouter un arbre, réordonner les régions et changer la version d'une recette sans casser
la cible d'une quête ni l'override d'une maison extérieure à la zone changée. Tester aussi une
suppression intentionnelle et une migration de sauvegarde. Mesurer les IDs réellement conservés.

**Impact.** Très élevé et transversal ; à décider avant le déploiement massif des outils d'édition.

## 6. P0 — Une tranche de création complète avant l'élargissement du catalogue

**Constat.** La roadmap contient déjà un bon parcours RPG en Gate 3, mais la lecture quotidienne est
dominée par les composants de rendu et les preuves de Gate 1. L'assistant produit arrive en Gate 6.
Le risque est de valider beaucoup de composants avant de mesurer la promesse de création assistée.

**Proposition.** Conserver les critères de Gate 1 et formuler dès maintenant un scénario produit
stable qui traversera les gates suivantes. Avant ouverture de Gate 2, préparer uniquement critères
et données de référence. Lorsqu'elle sera autorisée, bâtir une première boucle manuelle via le bus
partagé. À la gate autorisée pour l'IA, utiliser les mêmes commandes pour la proposition assistée.

Une anticipation d'un sous-ensemble IA en Gate 2 demanderait une décision explicite de roadmap et
un ADR ; elle n'est ni décidée ni lancée ici. Une description formelle des commandes et un jeu de
plans synthétiques peuvent d'abord évaluer le contrat sans fournisseur distant. Ces fixtures ne
doivent jamais être présentées comme une preuve d'IA réelle.

**Scénario candidat.** Une courte aventure originale dans un village, une forêt et un intérieur :
un donneur de quête, deux approches possibles, une conséquence persistante et une fin identifiable.
Le créateur modifie une porte et un dialogue, demande une variante locale de végétation, joue,
sauvegarde, reprend et produit ensuite un module installable quand Gate 4 l'autorise.

**Mesures.** Temps jusqu'au premier parcours jouable, nombre de corrections, pertes de retouches
(objectif zéro), blocages de quête, coût IA réel et réouverture sans perte. Les durées cibles sont à
calibrer par une première observation ; ne pas inscrire de promesse arbitraire de « RPG en 5 minutes ».

Sources : [roadmap Gates 2/3/6](MIGRATION_ROADMAP.md),
[matrice de reprise, assistant différé](../migration/LEGACY_REUSE_MATRIX.md).

## 7. P1 — Faire de la précision un parcours normal de l'éditeur

**Constat.** Gizmos, snap et inspecteurs sont prévus. La charte donne déjà la priorité à la manipulation
directe. Le contrat reste à détailler entre une intention telle que « plus près du chemin » et une
retouche telle que « décaler cette porte de 12 cm ».

**Proposition.** Sélection directe, lasso et sélection par région ; transformations locales/globales,
valeurs numériques en unités explicites, pivots/sockets, alignement surface/grille, filtres de
sélection et isolation d'une couche. Pour terrain/splines : masque visible, rayon, intensité, profil
et points de contrôle. L'inspecteur doit montrer la valeur résolue, sa source et le verrou associé.

Une sélection fournit aussi la cible bornée de l'IA : identifiants, région, propriétés modifiables,
caméra et dépendances utiles. « Change seulement le toit de cette maison » ne devient pas une
reconstruction du village. Les graphes techniques restent des diagnostics avancés ; dialogues et
quêtes disposent d'une édition lisible et d'une simulation du parcours.

**Preuve.** Un utilisateur corrige orientation, hauteur, porte, collision et texte sans JSON ni code,
annule une opération, puis retrouve exactement ces valeurs après régénération et redémarrage.
Sources : [charte §12](../design/DESIGN_SYSTEME.md) et [roadmap R2-007 à R2-043](MIGRATION_ROADMAP.md).

## 8. P1 — Relier intention, monde et récit avant la production détaillée

**Constat.** WorldSketch décrit efficacement l'espace et les références artistiques ; le modèle RPG
décrit dialogues, quêtes et règles. Le lien auteur entre les deux n'est pas encore un parcours prouvé.

**Proposition.** Un brief de module versionné référence intention narrative, style, durée visée,
lieux, personnages, règles choisies et contraintes de gameplay. Il n'est pas une deuxième copie du
monde : il formule des exigences liées aux IDs des documents canoniques. Une quête cible un personnage,
un objet et un lieu réels ; un chemin dessert une entrée utilisable, pas simplement un point de carte.

L'IA reçoit le catalogue des capacités et assets disponibles. Une capacité absente devient une
proposition ou une limitation visible, jamais un script prétendument exécutable. Distinguer fiction
décorative et mécanisme implémenté. Prévisualiser les conséquences avant la production artistique.

**Preuve.** Déplacer un bâtiment révèle l'impact sur sa porte, la navigation, le lieu de quête et les
patrouilles. Supprimer un objet requis signale les objectifs affectés. Un agent peut proposer une
réparation, mais ne change pas un contenu verrouillé sans l'accord requis.

Sources : [vision et modèle RPG](../../PROJECT_CONTEXT.md),
[parcours historique Creator](../../../OpenRPG_Forge/docs/acceptance/CREATOR_END_TO_END.md),
[contrat IA historique](../../../OpenRPG_Forge/docs/MCP_AI_ARCHITECTURE.md).

## 9. P1 — Une seule famille de commandes pour IA, main, tests et outils

**Constat.** Le principe est déjà obligatoire, mais le Studio inspecté expose une preuve de sélection
et de résumé, pas le bus d'édition de Gate 2. Il faut empêcher la création de voies séparées à mesure
que brosses, CLI, assistant et imports arrivent.

**Proposition.** Chaque capacité authorable fournit une commande typée et documentée : schéma,
préconditions, périmètre de lecture/écriture, dry-run, diff sémantique, résultat, inverse, permissions
et validations. La commande porte une clé d'idempotence pour qu'un retry ne crée pas deux maisons.
Une révision périmée exige revalidation ; une fusion ne prolonge pas silencieusement l'approbation
d'un ancien diff. Les libellés humains doivent décrire les objets touchés et les conséquences.

Les schémas peuvent produire les formulaires simples et l'inventaire d'outils de l'IA. Les gestes
spatiaux spécialisés restent des interfaces conçues à la main. L'éditeur adapté à un module se
configure par données ; il ne nécessite pas une génération arbitraire de code exécutable.

**Preuve.** La même opération réalisée par geste, CLI ou plan synthétique produit les mêmes documents,
validations et possibilités d'annulation. Ajouter une propriété n'impose pas trois implémentations.
Source : [AGENTS, commandes du produit](../../AGENTS.md), [StudioCommand](../../apps/forge_studio/src/lib.rs).

## 10. P1 — Régénération locale et tâches longues récupérables

**Constat.** Reconstruction partielle prévue en Gate 2 ; jobs durables différés dans la matrice.
Le compilateur WorldSketch produit une nouvelle sortie et le CLI refuse l'écrasement d'un dossier.
C'est utile pour des preuves, mais ce n'est pas encore une boucle d'édition interactive.

**Proposition.** Un graphe de dépendances de données calcule les sorties réellement invalidées :
terrain → collisions → navigation → placements et audits concernés. Conserver les caches non touchés.
La clé de cache inclut toutes les entrées pertinentes, version de générateur et paramètres ; aucun
cache périmé ne doit être présenté comme final. Les résultats anciens restent visibles avec leur état
pendant un calcul, sans permettre de qualifier un playtest sur des données incohérentes.

Les jobs longs ont étapes réelles, checkpoint, annulation, reprise et sortie en staging. La promotion
revalide la révision et les permissions. Un crash ne perd ni retouche ni travail coûteux déjà produit.

**Preuve.** Annuler une génération, fermer le Studio au milieu d'un export, rouvrir et reprendre sans
doublon. Déplacer une spline ne reconstruit pas tous les assets. Comparer médiane/p95 d'attente pour
une retouche et une compilation complète, avec la même scène et le même matériel.

Sources : [roadmap Gate 2](MIGRATION_ROADMAP.md), [matrice WorldGeneration](../migration/LEGACY_REUSE_MATRIX.md),
[orchestration historique](../../../OpenRPG_Forge/docs/ADR/ADR-0050-governed-world-generation-orchestration.md).

## 11. P1 — Le playtest doit aider à corriger le jeu

**Proposition.** Lancer depuis le point sélectionné et un état de quête choisi, dans une session
isolée. À un problème observé, créer un diagnostic lié aux IDs, à la révision, au seed, aux événements
et à la position de caméra. Revenir directement à l'objet ou à la condition concernée dans l'éditeur.
Ne jamais recopier implicitement les transformations de simulation dans le monde authoré.

Ajouter des simulations ciblées : objectif inaccessible, PNJ absent, précondition impossible,
récompense dupliquée, verrou de progression, sauvegarde/reprise au milieu d'une quête. Un parcours
automatisé prouve les états et l'accessibilité ; l'intérêt, le rythme et la lisibilité demandent encore
une partie observée. Un replay exact de physique n'est pas supposé acquis entre backends : documenter
son périmètre et distinguer replay d'événements et nouvelle simulation.

**Preuve.** Une quête volontairement cassée pointe vers la référence fautive ; sa correction est
testable depuis le même état. La session rejouée ne modifie pas le projet sans commande explicite.
Sources : [contexte §8.8 et Gate 3](../../PROJECT_CONTEXT.md),
[contrat de règles, chaîne causale](../design/player-handbook/parts/05_implementation_contract.md).

## 12. P2 — Construire moins d'assets, mais de meilleurs kits authorables

**Proposition.** Prioriser un kit cohérent avec éléments paramétriques, variantes, sockets, échelles,
collisions, LOD et provenance. Le créateur doit pouvoir remplacer un candidat tout en conservant
les liens gameplay et les réglages locaux. L'IA choisit dans un catalogue vérifié avant de commander
une nouvelle génération ; les candidats externes restent en staging et les sélections humaines
explicitement requises par ADR-0008 restent obligatoires.

La retouche Blender doit elle aussi avoir un propriétaire persistant : recette reproductible ou
source d'asset authorée et versionnée, reliée aux exports par provenance. Une correction faite
uniquement dans un `.blend` de cache serait perdue au prochain build. Promouvoir explicitement
l'asset corrigé ou retranscrire sa modification dans la recette ; un `.blend` de monde complet ne
devient pas pour autant la source canonique de la carte.

**Preuve.** Une maison peut changer de matériau, toit et porte sans casser son intérieur ou sa quête.
Un module exporté retrouve ses assets et notices sur une machine sans Asset Vault ni fournisseur IA.
Les obligations de distribution et de CI restent applicables à leur gate.

Source : [pipeline artistique et fabrique d'assets](../world-generation/README.md).

## 13. P2 — Règles cohérentes, budget IA explicite et fonctionnement sans fournisseur

Le manuel sans classe fournit un contenu détaillé, mais reste une proposition distincte du ruleset
historique. Avant de générer dialogues, objets et rencontres, sélectionner un profil de règles
versionné ; afficher sa provenance et ses capacités implémentées. Commencer le prototype par le
sous-ensemble nécessaire au parcours, puis étendre. L'IA ne doit pas inventer une statistique ou un
effet manquant dans le moteur. Les mêmes références alimentent inspecteurs, diagnostics et aide.

Pour une production IA, afficher provider, données nécessaires à transmettre, plafond de coût,
nombre de candidats, durée et politique de retry. Arrêter au plafond et conserver les résultats.
Le contrôle s'effectue à la frontière de la tâche autorisée, avec les confirmations explicites requises,
pas par des questions répétées pour chaque opération interne. Une autonomie plus large que la politique
produit actuelle demanderait son propre ADR ; ce rapport ne la rend pas effective.

Le module généré doit rester éditable et jouable sans connexion au modèle qui l'a créé. Ne pas confondre
assistant d'auteur, comportement des PNJ et modèle génératif au runtime. Une IA générative pendant la
partie serait une fonctionnalité supplémentaire avec ses coûts, permissions et limites propres.

Sources : [README design](../design/README.md), [manuel](../design/OPENRPG_FORGE_PLAYER_HANDBOOK_FR.md),
[contexte O9 et Gate 6](../../PROJECT_CONTEXT.md).

## 14. Améliorer le pilotage documentaire sans affaiblir les preuves

Trois problèmes documentaires concrets persistent au-delà des onze corrections approuvées :

1. [README design](../design/README.md) annonce encore « Gate 0 reste la gate active » alors que le
   contexte annonce Gate 1. Remplacer ces copies d'état par un lien vers une seule source de statut.
2. [contexte §8.3](../../PROJECT_CONTEXT.md) doit préciser la stabilité des identités après modification,
   pas seulement à seed identique ; un ADR est préférable à une correction silencieuse du contrat.
3. Le contexte fait 1 836 lignes au recensement. Déplacer à terme protocoles détaillés et procédure
   historique vers leurs documents propriétaires, en conservant les liens. Garder un contexte court
   sur objectif, état, décisions, frontières et routes de lecture. Les preuves ne doivent pas être
   supprimées pour alléger le contexte.

Les descriptions déjà présentes de couches, undo, provenance et parcours ne doivent pas être
réécrites dans un nouveau méga-plan. Compléter leurs critères d'acceptation et leur traçabilité.
Pour chaque capacité, distinguer : spécifiée, présente dans le code, exercée localement, inspectée
visuellement, qualifiée pour un périmètre. Une case cochée n'établit pas tous ces niveaux.

Le benchmark historique [effort d'auteur](../../../OpenRPG_Forge/docs/world-generation/QUALITY_BENCHMARK.md)
mesure déjà temps humain, actions manuelles et régénérations. Reprendre ces critères dans les preuves
Rust, sans reprendre le renderer historique. La métrique directrice proposée est le temps pour
obtenir une aventure jouable satisfaisante avec zéro perte de retouche, complété par qualité, coût et
fiabilité ; ni le nombre de fichiers produits, ni les 717 routes, ni les FPS seuls ne le remplacent.

## 15. Ordre proposé et décisions nécessaires

| Étape | Travail proposé | Dépendance et décision |
| --- | --- | --- |
| Maintenant, conception | Fixer le scénario auteur de référence, définir verrous/fusion/identités et leurs cas d'échec, cartographier les commandes nécessaires. | Documents proposés ; ne modifie ni le statut ni les critères des gates. |
| Gate 1 courante | Terminer les preuves restantes selon ses protocoles, y compris les revues humaines requises. | Aucun élargissement ni assouplissement demandé par cette revue. |
| Gate 2 autorisée | Réaliser le plus petit parcours manuel complet, persistant et retouchable, avec commandes communes et recalcul local. | ADR de fusion/persistance/identités avant adoption des nouveaux contrats. |
| Gate 3 autorisée | Réaliser l'aventure de référence depuis le Studio et sa boucle diagnostic/correction. | Mesurer l'expérience de création et de jeu, pas seulement les fonctions isolées. |
| Gates 4 puis 5 | Vérifier package indépendant, second module, kits et profils de règles avant élargissement du catalogue. | Préserver les exigences de sécurité, de licence et de distribution. |
| Gate 6 | Brancher l'assistant sur les commandes stabilisées et mesurer le gain face au parcours manuel. | Une anticipation partielle demanderait explicitement une révision de roadmap/ADR. |
| Gates 7/8 | Distribution complète puis réseau. | Ne pas utiliser cette revue comme autorisation de les démarrer. |

Les changements de logiciel ci-dessus sont des propositions. Aucun code produit, schéma de données,
profil de règles ou protocole de gate n'a été modifié pendant cette intervention.

## 16. Livraison de l'amélioration des instructions

Les onze propositions approuvées sont appliquées aux deux `AGENTS.md`, au contexte Rust, aux six
skills historiques, aux métadonnées du skill de publication, au protocole de validation Studio et
à la catégorie 3 de la politique Computer Use : 12 fichiers modifiés dans ces périmètres.
Les ambiguïtés supplémentaires audit/statut et commit seul relevées par une revue indépendante ont
été corrigées dans les mêmes fichiers. Les confirmations de catégorie 2 sont conservées.

La mémoire utilise le mécanisme prescrit : note de correction sous
`C:/Users/karen/.codex/memories/extensions/ad_hoc/notes/2026-09-05T120000-instructions-autonomie-forge.md`.
Cette note ne prouve pas la réindexation du service distant. La modification Computer Use est locale
à la version en cache `26.901.31953` ; elle devra être contrôlée lors d'une mise à jour du plugin.

Validation : six skills passent `quick_validate.py` avec PyYAML 6.0.3 installé uniquement dans un
répertoire temporaire de validation. Revue indépendante de quatre scénarios en lecture seule :
commit local sans push, deux lots avec premier test rouge, audit sans mutation, vérification Rust
sans transposition Godot. Deux réserves corrigées puis levées par relecture. Ces scénarios sont une
revue comportementale des consignes, pas une exécution réelle de builds ou de publications.

Les deux dépôts passent `git diff --check`. Le contrôle statique complémentaire passe 85 assertions
sur les sauvegardes avant édition, les frontières d'approbation conservées, les métadonnées des
skills, les corrections de portée et les 30 liens internes du rapport. Ces assertions ne remplacent
pas une validation du logiciel.

Les changements préexistants sont conservés ; aucune publication n'est effectuée. Ce rapport ne
revendique aucune nouvelle validation runtime ou fermeture de gate.
