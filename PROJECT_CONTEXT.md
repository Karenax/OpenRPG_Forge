# Contexte canonique — OpenRPG Forge

Dernière mise à jour : 24 août 2026.

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

`Forge.Studio` est désormais l'application Creator autonome qualifiée : Module Hub, une fenêtre
produit, huit rubriques, trente-six pages, vue 3D centrale lorsque le contexte est spatial et ateliers
pleine surface pour les dialogues, scripts, données, validation et opérations réseau. La matrice
d'accès humain contient 717 routes uniques au statut `functional`, de UX0 à UX10, et les 66 fenêtres
historiques possèdent chacune une destination Studio au statut `Parity`.

Les parcours couvrent l'édition directe du monde, les blueprints et assets gouvernés, la narration,
ForgeScript, le gameplay, le playtest isolé, la validation, la préparation de distribution, le réseau
et l'Assistant. Les mutations qualifiées passent par les commandes Application avec validation,
dry-run, diff, garde de révision, audit, undo/redo, sauvegarde atomique et préservation des données
voisines ; les aperçus distinguent les plans déterministes des effets moteur réellement exécutés.

La revue corrective UX11-B a inspecté les trente-six pages en Full HD, QHD et 4K. Les distributions
Studio Windows x64 et Linux x64 embarquent le workspace Forgotten Forest et la bibliothèque
ForgeScript native de leur plateforme ; le smoke Linux a été rejoué sur Debian 13 x86_64 sans
éditeur Godot, ce qui ferme `STUDIO-043`. Le portail transversal final passe 2 117 tests .NET,
44 tests Python et tous ses smokes ; le graphe Roslyn reste frais et déterministe. Cette qualification
locale ne constitue pas une release publique signée.

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

Le chantier courant n’est plus la migration des routes Studio. Il porte sur la consolidation Git, la
CI distante reproductible et les prérequis de release : licence du code, signature Authenticode,
revue physique multi-écrans, fournisseur IA réel et autorisation explicite de publication. Le plugin
historique reste disponible jusqu'à une décision utilisateur explicite de basculer le lanceur Creator.

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

## Direction gameplay systémique

Le ruleset RPG canonique est `docs/design/OPENRPG_FORGE_RULESET_FR.md`. Il définit un RPG d’action
systémique en temps réel avec concentration tactique, un groupe pouvant atteindre quatre personnages,
une progression ouverte mais bornée, des quêtes par états et un monde dont les objets, surfaces,
structures et mécanismes peuvent devenir des outils ou des armes. Les actions physiques et les
conséquences narratives doivent partager une même chaîne de causalité.

La Crypte des Cendres est la première tranche verticale jouable de cette direction. Son snapshot
canonique n’est plus la salle historique de 20 × 28 m : la branche de qualification reconstruit une
zone de 48 × 76 m organisée en Seuil des Braises, sanctuaire d’Elyra, catacombes et reliquaires,
salle de la Sentinelle, Ossuaire inondé, Grande Nef et sanctuaire du Gardien. Les identifiants de la
quête principale sont conservés, mais les acteurs et les interactions sont redistribués dans ces
espaces. La Clé de braise se trouve dans un reliquaire latéral et la porte scellée possède désormais
une frontière physique qui empêche de la contourner par la géométrie.

La couche visuelle de cette reconstruction réemploie le lot gouverné KayKit Dungeon Remastered
CC0-1.0 présent dans `apps/Forge.Runtime/art/crypt-kaykit/` : sols, murs, passages, colonnes,
escaliers, coffres, tonneaux et torches composent les mêmes sept espaces que le snapshot de zone.
Le document spécialisé `docs/design/CRYPT_OF_ASHES_DUNGEON_REBUILD.md` décrit cette composition et
ses critères de qualification.

Le manifeste `apps/Forge.Runtime/content/crypt-of-ashes/systemic-gameplay.json` conserve cinq
démonstrateurs : huile inflammable et eau gelable dans l’Ossuaire, brasero suspendu, pilier fracturé
et fosse de pointes dans le sanctuaire final. Les dégâts environnementaux réutilisent l’état de
combat autoritaire existant afin de préserver phases de boss, événements, butin, progression et
transitions de quête. Les prompts d’interaction sont limités à la proximité du joueur pour ne pas
traverser visuellement l’ensemble du donjon.

Cette tranche prouve un premier parcours de « monde comme arme » ; elle ne vaut pas implémentation
générique de toutes les propriétés, substances, structures, compagnons et conséquences décrites par
le ruleset. La reconstruction spatiale est intégrée à `main` et qualifiée par compilation
Godot/.NET, navigation, collisions, sauvegarde/reprise, performances et revue Forward+. Ces preuves
restent à rejouer après toute modification qui touche ce parcours. La généralisation dans le
domaine, le Creator et les formats de module reste un chantier séparé.

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
zone, proposer un dialogue, construire une quête, préparer un import, compiler un script ou lancer un test. Il n’expose ni shell arbitraire ni écriture générique de fichiers.

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
