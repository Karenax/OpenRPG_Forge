# Contexte canonique — OpenRPG Forge

Dernière mise à jour : 16 août 2026.

Ce document est l’unique contexte narratif courant du projet. Les états d’exécution sont dans
`STATUS.md` et `TASKS_MVP.md`; les exigences détaillées et leurs preuves restent dans le tracker et
le dossier Aurora immuable.

## But final

OpenRPG Forge doit devenir un éditeur complet de RPG 3D modulaires permettant à une personne non
spécialiste de créer, tester, distribuer et maintenir un module entier depuis une interface claire.
Le produit doit intégrer une assistance IA et un serveur MCP gouverné, tout en restant entièrement
utilisable hors ligne et sans IA.

Un créateur doit pouvoir :

1. créer un projet et ses zones intérieures ou extérieures ;
2. importer, gouverner et placer modèles, matériaux, sons, animations et effets ;
3. créer blueprints, créatures, objets, portes, déclencheurs, rencontres et factions ;
4. écrire dialogues, quêtes, journal, règles, pouvoirs et scripts ;
5. configurer navigation, environnement, caméra, contrôles, réseau et Live Director ;
6. jouer immédiatement depuis une sélection, diagnostiquer et corriger les erreurs ;
7. exporter un module `.orpmod`, le réinstaller et le partager sans dépendance cachée ;
8. créer un lanceur Windows propre au jeu, avec branding, réglages et hébergement multijoueur local ;
9. demander à l’IA d’expliquer, proposer, construire ou réparer par des plans vérifiables et annulables.

## Produit actuel

Le dépôt contient le Creator/Toolset historique intégré à l'éditeur Godot, sa nouvelle application
autonome `Forge.Studio`, un runtime 3D séparé, ForgeScript, un format de module propre, un host MCP
et deux démonstrations jouables : Crypte des Cendres et Forgotten Forest. Les 717
exigences du catalogue Aurora sont classées `Tested` dans 187 lots clos. Cette couverture décrit les
capacités disponibles ; elle ne remplace pas la qualification d’un logiciel installable et publiable.

`Forge.Studio` possède désormais sa fondation exécutable : Module Hub, une fenêtre produit, huit
rubriques, trente-six pages, reprise du workspace et de la zone, vue 3D centrale de Forgotten Forest,
sélection partagée et pages documentaires alimentées par les vraies données. Sa première tranche de
retouche est transactionnelle : déplacement direct par gizmo X/Y/Z, inspecteur d'instance ouvert au
clic droit, tags et propriétés locales, ajout filtré, duplication exacte, suppression confirmée,
undo/redo et sauvegarde atomique. UX0-A à UX0-J qualifient maintenant quarante-deux routes humaines :
Explorateur, recherche, filtres, références, aide, dispositions et profils nommés, onglets multi-zones,
comparaison 3D protégée, raccourcis configurables, six modes de viewport, multisélection, caméra
configurable, gizmos complets, palette contextuelle, favoris et palettes nommées, placement répété ou
à la brosse avec fantômes valides/invalides, presse-papiers sémantique, assistant de création en cinq
étapes, métadonnées multilingues, identité interne stable, projet Core sans zone, point de départ
nommé avec waypoint facultatif, puis vingt liaisons d’événements de cycle de vie et de gameplay
configurées par cartes et scripts nommés, puis équipement/progression, repos, rythmes globaux,
signaux utilisateur typés, abandon sûr de cinématique, calendrier/fuseaux/repères astronomiques,
échelle de progression data-driven, variables globales typées, cache ForgeScript vérifiable et huit
pages localisées de guide joueur, renommage sûr avec migration de références et identité stable,
    puis diagnostics structurés, liens profonds, correctifs prévisualisés et export expurgé. L’abandon
publie réellement le ForgeScript résolu après la restauration ; le cache compile par
source/API/compilateur et toutes les mutations qualifiées gardent un historique annulable. Les
    vingt et un incréments UX1 qualifiés ajoutent l’assistant humain de zone, ses gabarits
    vide/jouable et le document indépendant, puis redimensionnement avec pertes explicites, copie
    complète ou gabarit avec nouvelles identités, et écran de chargement avec médias importés,
    astuces, progression réelle et repli accessible, puis calques imbriqués, états hérités,
    sélection de groupe et export partiel, et enfin blueprint de décor avec ressource humaine,
    collision, rôle, interaction, transform et scripts, puis accès direct au décor statique avec
    regroupement, instanciation, occlusion, fusion sûre et collision optionnelle, puis objet utilisable
    avec actions nommées, portée, focus, autorité, conditions et événement `OnUsed`, puis conteneur
    avec capacité, masse, propriétaire, droits, persistance, réplication, inventaire, butin et
    présentation, puis identité et apparence avec noms localisés, tags, notes, meshes, matériaux,
    icône, portrait et VFX, puis transformation précise avec coordonnées, rotation, échelle, pivot,
    grille, parent, surface et animation, puis état visuel initial parmi six états, animations,
    apparences, interaction et transitions prévisualisées, puis destructibilité avec santé, armure,
    résistances, étapes visuelles, destruction, débris et réparation, puis protection plot avec
    actions, droits, feedback et dérogation MJ auditée, puis faction principale et appartenances
    avec effets d’hostilité, crime, accès et ciblage prévisualisés, puis conversations liées aux
    interactions, états, utilisateurs, objets et événements ; aperçu, undo/redo et round-trip
    exacts, puis verrouillage avec états, clé, justificatifs, défi, code masqué, piratage, droits,
    reverrouillage et brouillage prévisualisés, puis politique de clé conservée, consommée, partagée,
    à charges ou virtuelle avec audit et aperçu, puis piège avec déclencheur, effet, détection,
    désamorçage, réarmement, propriété, alliés, VFX et usage unique, puis trésor initial avec table
    d’objets, moment, graine et état généré persistant, puis événements d’ouverture et de fermeture
    avec bindings ForgeScript et payload canonique, enfin événements avant/après du verrou avec
    méthode, justificatif, acteur et veto contrôlé portent à 65 les routes Studio qualifiées.
La vague UX2 qualifie désormais les 98 routes Monde de Terrain, Environnement, Monde vivant et
Navigation : édition directe des tuiles et profils, audio authoré, événements/rencontres, surfaces,
waypoints, transitions, visibilité, routes et ancres caméra. Chaque lot possède commande canonique,
dry-run, undo/redo, round-trip, smoke et preuve Forward+ FHD ; les aperçus distinguent explicitement
plans déterministes, services de décision et effets moteur réellement exécutés. La matrice Studio
compte ainsi 221 routes `functional` et 496 `planned`. Le portail transversal de clôture UX2 passe
avec 2 033 tests .NET, 39 tests Python, des builds sans avertissement, un graphe Roslyn déterministe
à 9 968 nœuds et 71 303 relations sans diagnostic, puis les smokes Studio, Toolset, Runtime,
Forward+, MCP, réseau et distribution. Playtest, distribution Windows/Linux et remplacement du
lanceur Creator historique ne sont pas déclarés terminés avant leur propre migration et
qualification.

Le Creator sait aussi configurer un profil de lanceur par jeu et générer une distribution Windows x64
atomique. Le lanceur WPF possède une présentation RPG cinématique animée, vérifie ses fichiers, expose
Accueil, Configuration et Multijoueur, applique les préférences au runtime et démarre une session
locale autoritaire. La connexion distante reste un aperçu désactivé jusqu'au branchement d'un
transport réseau inter-processus. Les restaurations `win-x64` propres aux exports utilisent des
verrous NuGet temporaires et ne réécrivent pas les `packages.lock.json` canoniques du dépôt.

Le parcours Creator normal ouvre la démonstration Forgotten Forest et privilégie l'édition directe :
choisir un objet ou une brosse de terrain, cliquer dans la vue 3D, cadrer la zone active et conserver
les détails techniques en second niveau. Les aperçus d'assets externes restent gouvernés ; un asset
indisponible reçoit un placeholder explicite sans autoriser d'import implicite depuis le réseau.

Le runtime possède une interface de combat dynamique fondée sur les résultats autoritaires du
domaine : barres animées, cible, plaques ennemies, nombres flottants, recharge réelle, toasts,
pings, impacts, caméra, micro hit-stop et audio SFX. Cette présentation reste générique, ne modifie
ni équilibrage ni contenu de module, et se replie proprement avec mouvements réduits, contraste
élevé, modes de vision des couleurs et texte jusqu'à ×2.

Le chantier courant n’est plus la création de nouvelles familles fonctionnelles. Il est la migration
progressive des capacités déjà couvertes vers `Forge.Studio`, suivie de la qualification de livraison,
de la CI distante reproductible et de la finition visuelle. Le plugin historique reste disponible tant
que la matrice de parité n'est pas close.

## Expérience Creator cible

L’interface autonome est organisée autour de huit rubriques stables et trente-six pages : Monde,
Contenu, Histoire, Gameplay, Tester, Publier, Projet et Assistant. Une seule fenêtre produit contient
la navigation, la page de travail et un inspecteur contextuel unique. Les onglets éventuels
représentent des documents ouverts, jamais des familles de fonctions ou des fenêtres système.

Les pages spatiales conservent la vue 3D centrale, la zone active, la caméra et la sélection quand
l'utilisateur change de rubrique. Les pages Dialogues, Quêtes, Scripts, Validation et configuration
emploient une surface documentaire dédiée sans conserver artificiellement la 3D. Les dialogues sont
présentés ligne par ligne ; leur graphe reste une vue avancée.

Dans une page spatiale, l'objet est manipulé là où il est visible : clic gauche pour sélectionner,
flèches X/Y/Z pour déplacer, clic droit pour choisir la catégorie de réglages, puis inspecteur unique
pour la transformation, les tags et les propriétés locales. Le même clic droit permet d'ajouter à
l'endroit visé, de dupliquer à côté ou de supprimer après confirmation. Ces actions partagent le même
historique transactionnel et n'ouvrent pas de sous-fenêtre.

Les sélecteurs de fichiers et confirmations système sont les seules fenêtres natives ordinaires.
Aucun outil secondaire ne s’ouvre au démarrage. La sélection, l’historique, les diagnostics, la
navigation et le vocabulaire sont partagés par toute la session Studio.

La plage d’affichage officiellement prise en charge par le Studio va de **1920×1080 (Full HD)** à
**3840×2160 (4K)**, avec **2560×1440 (QHD)** comme résolution de conception et de revue principale.
Les résolutions inférieures à 1920×1080 ne font plus partie de la cible produit ni des critères
d’acceptation. En 4K, les pixels supplémentaires agrandissent d’abord la vue 3D, les timelines et les
listes de travail : les panneaux gardent des largeurs bornées et la typographie ne double pas
artificiellement. La mise à l’échelle du système reste prise en charge sans construire l’interface
autour d’une toile fixe ou étirée.

Le mode Débutant présente d’abord les actions indispensables, des valeurs sûres, des explications
contextuelles et un parcours guidé. Le mode avancé révèle les réglages techniques sans changer le
document canonique ni perdre de données.

Toute action importante possède un libellé explicite, un état visible et, lorsqu’elle est fréquente,
une commande avec raccourci configurable. La couleur n’est jamais l’unique indicateur. Les erreurs
doivent mener à l’objet et à la propriété concernés et proposer une correction prévisualisable quand
cela est sûr.

## Édition manuelle et non destructive

Chaque élément métier doit rester créable et modifiable sans IA. Les agrégats et collections
principaux sont présentés comme une liste et un inspecteur à champs nommés, avec ajout, duplication,
suppression et réorganisation lorsque l’ordre est significatif. Les syntaxes compactes à séparateurs,
le JSON, les UUID, les hashes et les vues techniques restent des outils avancés, jamais le parcours
principal.

Un formulaire rapide ne peut pas effacer silencieusement les localisations, propriétés ou entrées
qu’il n’affiche pas. Une suppression de zone, blueprint, dialogue ou quête passe par une
prévisualisation transactionnelle, la validation des dépendances, puis l’historique undo/redo. L’IA
et le MCP accélèrent ce même modèle de commandes ; ils ne constituent pas une voie d’édition séparée.

## Direction graphique

L’identité 2026 est sombre, calme et lisible : fonds graphite ou violet très profond, surfaces
légèrement séparées, texte clair, accent braise pour l’action primaire, vert pour la réussite et
rouge réservé aux blocages. L’interface évite les murs de texte, les bordures excessives et les
contrôles techniques permanents.

La hiérarchie attendue est : titre et contexte, action principale, contenu de travail, inspecteur,
diagnostics. Les détails UUID, hash, provenance, schéma et logs restent accessibles mais repliés par
défaut. L’aperçu et le playtest restent proches de l’édition.

Le produit porte exclusivement la marque OpenRPG Forge dans ses surfaces contrôlées. Le moteur 3D
sous-jacent est une dépendance technique interne, pas l’identité du logiciel.

La cible 3D est un réalisme stylisé lisible : échelle métrique cohérente, matériaux PBR normalisés,
silhouettes distinctes, éclairage orientant le joueur, collisions et navigation fiables, LOD et
budgets par profil de qualité. Crypte des Cendres utilise désormais un kit artistique CC0 gouverné et
Forgotten Forest une composition stylisée avec modèles réels, terrain, navigation, eau et éclairage
jouables. La passe restante avant démonstration publique est une finition artistique exhaustive et
une revue humaine sur les résolutions et profils matériels supportés, pas un retour aux primitives.

Le rendu Forward+ applique une qualité visuelle gouvernée par profil : ciel procédural cinématique,
occlusion et lumière indirecte d'écran, glow et brouillard volumétrique lorsque le backend les prend
en charge. Les chemins de validation en compatibilité GL conservent un repli sûr et une colorimétrie
neutre lorsqu'une LUT déclarée manque.

En combat, la lisibilité prime sur l'ornement : l'état réel du joueur, de la cible, des ressources et
des capacités est visible sans dépendre uniquement de la couleur. Les animations et effets restent
brefs, bornés par des pools stables et entièrement supprimables par la politique de mouvements réduits.

## Architecture à préserver

Flux de mutation officiel :

`Studio / Creator historique / Runtime / MCP -> commandes Application -> Domain -> persistance et adaptateurs`

- Domain et Application restent indépendants du moteur 3D.
- Les données métier versionnées sont la source de vérité ; les scènes sont reconstruites.
- Toute mutation est transactionnelle, validée, auditée et annulable.
- Les formats persistants conservent identité, schéma et provenance.
- Les modules communautaires ne peuvent pas charger de code natif ni remplacer le runtime.
- Le Vault externe conserve originaux, licences et hashes ; Git ne reçoit pas les acquisitions lourdes.
- Le graphe Roslyn est généré depuis les sources et ne se modifie jamais à la main.

## IA et MCP intégrés

L’Assistant est un espace natif du Creator. Il peut utiliser des fournisseurs locaux ou distants
interchangeables. Le projet reste fonctionnel si aucun fournisseur n’est configuré.

Le serveur MCP expose des outils métier de haut niveau, par exemple lire le contexte borné d’une
zone, proposer un dialogue, construire une quête, préparer un import, compiler un script ou lancer
un test. Il n’expose ni shell arbitraire ni écriture générique de fichiers.

Une mutation assistée suit toujours :

`contexte borné -> proposition -> validation -> dry-run -> diff sémantique -> approbation -> application -> test -> audit -> revert`

Chaque plan est lié à la révision du projet et expire si cette révision change. Les autorisations,
coûts, confidentialité, fournisseurs et outils utilisables sont visibles. Les sorties non fiables
sont traitées comme des données à valider, jamais comme des instructions privilégiées.

## Définition du produit consolidé

La première version publiable est atteinte lorsque :

- les CI Windows et Linux sont vertes de façon répétée sur un checkout propre ;
- le Creator et le runtime se distribuent sous la marque OpenRPG Forge sans script de développement ;
- un jeu peut être livré avec son propre lanceur vérifiable sans ouvrir le Creator ;
- installation, mise à jour, désinstallation et lancement sont validés sur une machine propre ;
- un utilisateur peut réaliser les neuf étapes du but final sans éditer de JSON ;
- chaque élément nécessaire à un module peut être créé, inspecté, modifié et supprimé manuellement
  sans perte silencieuse des données non affichées ;
- les huit rubriques et trente-six pages du Studio sont cohérentes, navigables au clavier et
  utilisables sans connaissance de l'éditeur Godot ;
- Crypte et Forgotten Forest passent une revue visuelle interactive aux résolutions supportées ;
- un module créé, exporté, installé, sauvegardé, repris et mis à niveau passe un test de bout en bout ;
- l’IA/MCP réussit un parcours plan/dry-run/approbation/test/revert sans accès arbitraire ;
- version, changelog, licences, SBOM, signatures disponibles et limites connues sont publiés.

## Sources de vérité

Lire dans cet ordre :

1. `AGENTS.md` pour les contraintes de contribution ;
2. ce document pour le produit et le but final ;
3. `STATUS.md` pour l’état vérifié ;
4. `TASKS_MVP.md` pour la seule file active ;
5. `docs/planning/COMPLETE_FUNCTION_BACKLOG.md` et
   `tracking/project/AURORA_PARITY_COVERAGE.csv` pour les 717 exigences ;
6. les ADR et documents spécialisés applicables.

`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/` est un snapshot clean-room immuable. Il sert à
auditer les exigences, pas à décrire l’état courant du produit.
