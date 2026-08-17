# Plan d’accès humain aux 717 fonctions Aurora dans Forge Studio

Date de référence : 16 août 2026.

Ce plan complète l’ADR 0045 et `STANDALONE_STUDIO_MIGRATION.md`. Il ne rouvre pas la conception
métier des 717 fonctions déjà couvertes dans le projet : il organise leur accès réel depuis
l’application autonome `Forge.Studio`.

La matrice exhaustive associée est
`tracking/studio/AURORA_STUDIO_ACCESSIBILITY.csv`. Elle est générée depuis le tracker Aurora par
`scripts/Update-StudioAuroraAccessibilityPlan.ps1` et affecte chaque fonction exactement une fois à
une page principale, une interaction humaine, un niveau de divulgation, une vague et un contrat
d’acceptation.

## Verdict de planification

La cible n’est pas de placer 717 boutons dans l’interface. Une fonction est accessible lorsque la
personne peut la trouver, la comprendre, l’utiliser ou constater honnêtement son état, puis revenir
en arrière sans connaître Godot, JSON, un UUID ou l’organisation du dépôt.

Les 717 exigences utilisent six formes d’accès cohérentes :

1. **Créer ou modifier** par manipulation directe, formulaire nommé, liste ou composition visuelle.
2. **Inspecter** une donnée, ses dépendances, ses usages et son impact.
3. **Exécuter et relire** un playtest, une validation, un build, un profil ou une simulation.
4. **Administrer en direct** une session réseau ou Live Director avec permissions et audit visibles.
5. **Constater un état automatique** pour les capacités techniques qui ne doivent pas devenir un
   réglage dangereux, par exemple déterminisme, CI, signatures, sécurité ou cache.
6. **Demander une assistance** par proposition, dry-run, diff, approbation, test et revert, sans
   retirer le parcours manuel.

Une entrée de menu, une page vide, un résumé générique ou la présence d’un service interne ne
constitue pas un accès humain.

## Critères universels d’accessibilité humaine

Chaque fonction doit respecter les critères suivants avant d’être déclarée migrée :

- **Découvrable** : présente sur la page correspondant à l’intention de l’utilisateur et trouvable
  par la recherche globale ; aucun identifiant technique n’est nécessaire.
- **Compréhensible** : libellé orienté action, phrase d’aide courte, unité visible, valeur sûre et
  conséquence annoncée avant application.
- **Actionnable** : pas de faux bouton ; un contrôle incomplet est désactivé avec une raison et une
  destination de suivi explicites.
- **Réversible** : dry-run lorsque l’impact est large, undo/redo pour les mutations locales,
  restauration dans une nouvelle révision pour les opérations destructives ou longues.
- **Non destructif** : un formulaire simplifié préserve les champs et collections qu’il ne montre
  pas ; une suppression présente les références et les remplacements possibles.
- **Observable** : progression, succès, avertissement et erreur restent visibles ; une erreur mène à
  la ressource et au champ concernés.
- **Accessible** : clavier complet, focus visible, libellés non fondés uniquement sur la couleur,
  texte jusqu’à ×1,5 dans le Studio, mouvements réduits et contraste élevé.
- **Responsive** : aucune action principale coupée à 1920×1080, 2560×1440 et 3840×2160. Le Full HD
  est le minimum supporté, le QHD la référence de conception et la 4K la borne haute qualifiée ; les
  résolutions inférieures sont hors cible. En QHD et 4K, la typographie reste compacte et l’espace
  supplémentaire revient à la 3D, aux timelines et aux documents plutôt qu’à des panneaux étirés.
- **Persistant** : sauvegarde atomique explicite, état sale visible, reprise après fermeture ou crash,
  absence d’écriture directe dans les fichiers métier.
- **Prouvé** : commande Application, test de préservation, test UI, smoke Godot et revue humaine
  proportionnée au risque.

## Grammaire commune de l’interface

### Coque globale

La fenêtre unique conserve toujours :

- en haut : module, zone ou document actif, recherche `Ctrl+K`, undo/redo, état de sauvegarde,
  **Tester ici**, tâches d’arrière-plan et action principale de la page ;
- à gauche : rail des huit rubriques, puis liste compacte des pages ou ressources ;
- au centre : vue 3D, document, timeline, matrice ou résultats selon le travail réel ;
- à droite : un seul inspecteur pour la sélection ou l’outil actif ;
- en bas : statut sur une ligne, avec diagnostics et journal dans un tiroir fermé.

Les onglets servent uniquement aux documents ouverts : zones, dialogues, scripts, quêtes ou profils.
Ils ne dupliquent ni les rubriques ni les fenêtres du système.

### Trois niveaux sans trois produits différents

- **Débutant** est le niveau par défaut. Il montre les actions indispensables, les valeurs sûres,
  un exemple et un bouton principal.
- **Standard** montre l’ensemble du travail courant d’un auteur de module.
- **Avancé** révèle événements, permissions, API, budgets, hashes et diagnostics techniques dans des
  sections repliées. Il ne change jamais de format de données.
- **Automatique** présente un état vrai et sa preuve ; il ne crée pas un interrupteur lorsque la
  capacité doit rester garantie par le système.

Le changement de niveau est global mais chaque page mémorise ses sections ouvertes. La recherche
globale trouve aussi les fonctions avancées et explique où elles se trouvent.

### Cycle uniforme des ressources

Toutes les ressources utilisent le même vocabulaire : **Nouveau**, **Dupliquer**, **Renommer**,
**Supprimer**, **Références**, **Valider**, **Tester**, **Enregistrer**. Les listes ordonnées ajoutent
**Monter**, **Descendre** et le glisser-déposer avec alternative clavier.

Un formulaire est organisé dans cet ordre :

1. Essentiel ;
2. Apparence ou présentation ;
3. Comportement ;
4. Interactions et événements ;
5. Validation ;
6. Détails avancés.

### Événements sans mur de code

Les événements `OnEnter`, `OnClick`, `OnDeath`, `Heartbeat` et équivalents sont présentés comme des
cartes : **Quand**, **Si**, **Alors**, **Sinon**. Une action courante se choisit dans une liste
nommée ; l’appel ForgeScript reste une option avancée. L’utilisateur voit la cible, l’ordre, la
condition et le résultat attendu sans écrire un identifiant à la main.

### Actions longues

Import, validation, génération, build, export, benchmark et synchronisation produisent une tâche
d’arrière-plan avec progression, étape courante, annulation, journal replié et résultat ouvrable.
Changer de page ne masque ni n’interrompt la tâche.

## Schémas précis des 36 pages

### Monde

| Page | Travail humain principal | Surface et accès essentiels | Niveau avancé |
|---|---|---|---|
| Zones & scène | Ouvrir, créer, dupliquer, redimensionner et organiser une zone | Arbre zones/calques à gauche, 3D centrale, sélection et transformateur à droite, onglets de zones ouvertes | Dimensions exactes, identifiants, dépendances, LOD et propriétés de document |
| Terrain & biomes | Sculpter, lisser, peindre, effacer et remplacer | Brosses et biomes à gauche, aperçu avant application dans la 3D, diamètre/force/chute à droite | Variantes de tuiles, groupes, règles aléatoires, budgets et données de tileset |
| Objets & interactions | Placer et configurer décor, porte, trigger et placeable | Palette recherchable, clic de placement, gizmo, menu contextuel, inspecteur Essentiel/Interaction/Événements | Verrous, pièges, sockets, physique, scripts et propriétés locales brutes repliées |
| Environnement & audio | Régler l’ambiance en regardant et en écoutant le résultat | Profils jour/nuit, lumière, ciel, météo, eau, musique et sons avec lecture A/B | Réverbération, bus, variation, occlusion, HDR, PBR et réglages de rendu |
| Navigation & transitions | Dessiner surfaces, routes, waypoints et passages | Calques marche/vol/véhicule, poignées 3D, destinations nommées et test de chemin | Navigation volumétrique, coût, occupation, ancres caméra/audio et diagnostic détaillé |
| Monde vivant | Composer rencontres et politiques propres à la zone | Cartes Rencontres, Repos, Perception, PvP, Événements et ambiance dynamique | Vagues, statistiques, nettoyage, heartbeat, renforts et règles de spawn |

### Contenu

| Page | Travail humain principal | Surface et accès essentiels | Niveau avancé |
|---|---|---|---|
| Bibliothèque & blueprints | Trouver, créer, comparer et réutiliser un blueprint | Tuiles compactes, filtres, favoris, aperçu 3D et bouton **Placer dans la zone** | Versions, dépendances, propagation aux instances, collections et budget |
| Studio créature | Construire une créature complète sans parcourir dix fenêtres | Liste à gauche, plateau tournant central, fiche Identité/Apparence/Stats/Inventaire/IA/Événements | Classes, pouvoirs, perception, loot, templates, soundset et tous les événements |
| Studio objet | Créer un objet et préserver toutes ses propriétés | Liste, aperçu 3D, fiche Identité/Apparence/Usage/Équipement/Effets/Loot | Charges, durabilité, artisanat, propriétés magiques, On Hit et génération procédurale |
| Assets & import | Choisir, vérifier et installer un asset gouverné | Recherche, petites tuiles, filtres, tri, vignette/aperçu 3D, licence puis destination | Hash, source, quarantaine, normalisation, Blender, collision, LOD et plateforme |
| Factions, marchands & compagnons | Gérer les relations et services sociaux | Trois sous-vues stables : matrice de factions, fiche marchand, fiche compagnon | Économie, réputation asymétrique, formation, loyauté, banter et comportements |

### Histoire

| Page | Travail humain principal | Surface et accès essentiels | Niveau avancé |
|---|---|---|---|
| Dialogues | Écrire ligne par ligne et tester les choix | Liste des dialogues, cartes de répliques, choix sous chaque ligne, personnage et texte visibles | Conditions/actions, graphe, voix, caméra, interjections, couverture et collaboration |
| Quêtes & journal | Décrire objectifs, états et transitions | Liste des quêtes, objectifs nommés, tableau d’états, transitions lisibles et aperçu journal | Chronologie, récompenses, groupes, variantes, tests générés et validation narrative |
| Cinématiques & caméras | Monter une séquence en voyant le cadrage | Timeline à gauche/bas, caméra 3D centrale, plan sélectionné à droite, lecture/scrub | Verrous de contrôle, interpolation, audio, scripts et événements de séquence |
| Lore & références | Retrouver un personnage, lieu ou terme et ses usages | Recherche plein texte, fiche centrale, références entrantes/sortantes et retour à l’origine | Graphe de références, incohérences, alias et contexte autorisé à l’Assistant |
| Localisation | Traduire sans perdre le contexte | Langues et filtres à gauche, table source/traduction, aperçu de l’usage à droite | Tokens, variantes, pluriels, couverture, imports/exports et validation de police |

### Gameplay

| Page | Travail humain principal | Surface et accès essentiels | Niveau avancé |
|---|---|---|---|
| Règles & difficulté | Composer des profils de règles compréhensibles | Profils, groupes de règles, valeurs avec unités, comparaison et impact | Plugins de règles, mutateurs, calendrier, variables et compatibilité de sauvegarde |
| Capacités & progression | Créer une capacité en composant ses effets | Liste, aperçu 3D/VFX, pipeline Coût/Cible/Effets/Progression | Formules, projectiles, cooldowns, hooks ForgeScript et simulation de combat |
| ForgeScript | Écrire, comprendre, compiler et tester | Fichiers à gauche, code central, problèmes dessous, API et permissions à droite | Bytecode, budgets, debugger, profiler, hot reload, version d’API et interop MCP |
| Runtime, contrôles & extensions | Régler l’expérience du joueur | Profils contrôles/caméra/HUD/sauvegarde/interaction avec test immédiat | NUI, périphériques, JSON runtime, physique, extensions et compatibilité technique |
| Réseau & Live Director | Configurer puis administrer sans mélanger les risques | Onglets **Configurer**, **Tester**, **Sessions**, **Live Director** et **Audit** | Réplication, persistance, anti-cheat, métriques, scripts GM et administration distante |

### Tester

| Page | Travail humain principal | Surface et accès essentiels | Niveau avancé |
|---|---|---|---|
| Playtest | Jouer depuis la sélection et revenir au même endroit | Scénarios à gauche, runtime isolé central, contrôles et journal à droite | Seed, replay déterministe, simulation dialogue/quête/combat et instrumentation |
| Validation & corrections | Corriger une liste de problèmes concrets | Filtres, problème central, contexte et correctif prévisualisé ; double-clic vers l’objet | Règle, chemin technique, diff, suppression de faux positifs et validation ciblée |
| Laboratoire qualité | Exécuter tests, budgets et captures | Profils nommés, lancement/annulation, résultats comparables et tendances | Benchmarks, fuzzing, soak, couverture, profiling et preuves de gate |
| Historique & récupération | Comprendre et restaurer sans écraser | Timeline des commandes/sauvegardes/crashs, impact puis restauration dans une révision | Diff sémantique, checkpoints, autosave, rollback serveur et journal complet |

### Publier

| Page | Travail humain principal | Surface et accès essentiels | Niveau avancé |
|---|---|---|---|
| Build & impact | Savoir ce qui sera construit et pourquoi | Cible, changements détectés, impact, bouton build, progression et résultat | Cache, déterminisme, CI, build incrémental, logs et artefacts |
| Packages & dépendances | Contrôler l’ordre et la provenance du contenu | Liste ordonnée, dépendances nommées, conflits, licence et suppression sûre | Namespace, signatures, migrations, delta, synchronisation et notices |
| Profils & export | Produire un `.orpmod` ou une distribution vérifiable | Profils, checklist de blocages, destination, taille et manifeste final | Plateformes, canaux, versioning, signatures, SBOM et paramètres par cible |
| Lanceur du jeu | Prévisualiser l’expérience du joueur avant export | Écrans Accueil/Configuration/Multijoueur, branding et connexion locale | Adresse distante, protocoles, rôles, version locking et diagnostic réseau |

### Projet

| Page | Travail humain principal | Surface et accès essentiels | Niveau avancé |
|---|---|---|---|
| Identité & configuration | Créer et décrire le module | Nom, description, zone et point de départ, difficulté et variables par champs nommés | Événements globaux, scripts mis en cache, cible, migration et identifiants |
| Espace de travail & raccourcis | Adapter l’outil à la personne | Mode Débutant/Standard/Avancé, panneaux, raccourcis, caméra et profils | Recherche globale, find references, multi-zones et détails de disposition |
| Sécurité & workers | Comprendre et autoriser une intégration | Carte par worker/fournisseur : accès demandé, raison, état, tester et révoquer | Quotas, sandbox, chemins, réseau, prompt injection et journal d’audit |
| Production & plateforme | Voir la santé de fabrication du produit | Git/CI/version/licences/release sous forme d’états vrais avec action applicable | ADR, artefacts, crashs, compatibilité, canaux et politiques de production |

### Assistant

| Page | Travail humain principal | Surface et accès essentiels | Niveau avancé |
|---|---|---|---|
| Assistant contextuel | Demander une proposition depuis le travail en cours | Contexte autorisé visible, objectif en langage naturel, résultat structuré et bouton **Préparer le plan** | Fournisseur, coût, confidentialité, budget, mode hors ligne et sources |
| Plans & approbation | Décider en comprenant l’impact | Étapes, diff sémantique, dry-run, avertissements, tests puis Approuver/Refuser | Autorisations par étape, audit, expiration de révision et revert |
| Création 3D assistée | Comparer avant d’importer | Étapes, variantes 3D, comparaison, licence/provenance et destination | Blender Worker, fournisseurs, paramètres, validation géométrique et coût |

## Composants transversaux à construire une seule fois

Les pages ne doivent pas réinventer leurs contrôles. Les composants suivants précèdent les grandes
vagues métier :

- `StudioNavigationRouter` : page, document, retour, lien profond et historique ;
- `StudioGlobalSearch` : pages, actions, ressources, références et diagnostics ;
- `StudioSelectionService` : sélection unique partagée entre listes, 3D, timeline et problèmes ;
- `StudioCommandCoordinator` : validation, dry-run, diff, application, undo/redo et sauvegarde ;
- `StudioResourceBrowser` : recherche, filtres, petites tuiles ou liste dense, favoris et pagination ;
- `StudioListInspector` : CRUD, réorganisation, duplication et préservation des données cachées ;
- `StudioEventCardEditor` : Quand/Si/Alors avec ForgeScript en option avancée ;
- `StudioDependencyImpactPanel` : usages, blocages, remplacements et navigation ;
- `StudioLocalizedTextEditor` : source, variantes, tokens et contexte ;
- `StudioValidationNavigator` : diagnostic vers page, document, objet et champ ;
- `StudioBackgroundJobService` : progression, annulation, résultat et reprise ;
- `StudioPlaytestBridge` : snapshot immuable, processus séparé et retour au contexte ;
- `StudioDisclosureService` : Débutant/Standard/Avancé sans perte de données ;
- `StudioAccessibilityService` : focus, contraste, texte, mouvements et annonces d’état ;
- `StudioEvidenceHarness` : smoke par page, captures et parcours clavier reproductibles.

## Matrice exhaustive des 717 fonctions

La matrice `AURORA_STUDIO_ACCESSIBILITY.csv` contient :

| Colonne | Sens |
|---|---|
| `feature_id`, `domain`, `title`, `priority` | Identité inchangée du tracker Aurora |
| `section_id`, `page_id` | Destination humaine principale dans les 8 rubriques et 36 pages |
| `secondary_page_id` | Lien profond utile, jamais une seconde implémentation de la fonction |
| `surface` | Globale, spatiale, hybride, documentaire, contextuelle ou live |
| `interaction` | Grammaire de contrôle attendue, par exemple manipulation directe ou cartes d’événements |
| `disclosure` | Débutant, standard, avancé ou automatique |
| `delivery_wave` | Vague de migration UX0 à UX10 |
| `acceptance_contract` | Preuves minimales exigées avant parité Studio |
| `implementation_status` | État réel : `planned`, `functional`, `partial`, `read-only`, `placeholder` ou `blocked` |
| `proof_reference` | Acceptation spécifique liée à l’identifiant, vide tant que la ligne est `planned` |
| `validated_at` | Date de la dernière qualification liée à cette preuve |

Les champs de routage restent un plan et ne prouvent pas l’implémentation. Les promotions sont
alimentées séparément par `tracking/studio/AURORA_STUDIO_PROOFS.csv`, puis recopiées de manière
déterministe dans la matrice. Une ligne non prouvée reste `planned`. Le tracker Aurora continue de
prouver la capacité projet existante ; `CREATOR_WINDOW_MIGRATION.csv` continue de décider le retrait
des 66 fenêtres historiques. Une preuve Studio doit référencer l’ID Aurora, la page, le contrôle,
la commande, le test et la capture ou procédure interactive.

Commande de régénération :

```powershell
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

## Vagues de livraison

Les nombres indiquent les fonctions dont la page principale est livrée dans la vague. Ils ne sont
pas une estimation d’effort. Chaque incrément d’implémentation reste borné à une à cinq feature IDs.

| Vague | Fonctions | Pages et résultat humain attendu |
|---|---:|---|
| UX0 — Trouver et comprendre | 42 | Coque globale, Identité, Espace de travail, recherche `Ctrl+K`, liens profonds, mode Débutant/Avancé, aide et reprise |
| UX1 — Retoucher le monde | 71 | Zones et Objets : sélection, placement, transformation, CRUD, événements et dépendances sans quitter la 3D |
| UX2 — Construire un espace vivant | 98 | Terrain, environnement, audio, navigation, transitions, waypoints et rencontres avec aperçu direct |
| UX3 — Choisir et gouverner le contenu | 34 | Blueprints et Assets : recherche dense, comparaison, import, provenance, dépendances et placement |
| UX4 — Créer le contenu RPG | 140 | Créatures, objets, portes, placeables, factions, marchands et compagnons avec fiches cohérentes |
| UX5 — Écrire l’histoire | 69 | Dialogues, quêtes, cinématiques, lore et localisation avec test dans le contexte |
| UX6 — Régler le gameplay | 84 | Règles, capacités, ForgeScript et runtime avec documentation, compilation et simulation proches |
| UX7 — Tester et récupérer | 43 | Playtest isolé, diagnostics cliquables, qualité, historique, autosave et restauration |
| UX8 — Préparer et publier | 51 | Projet, build, packages, export, lanceur, sécurité et production avec états honnêtes et tâches annulables |
| UX9 — Exploiter en réseau | 68 | Configuration réseau, assurance, sessions, Live Director et audit avec séparation stricte des risques |
| UX10 — Assister sans remplacer | 17 | Contexte, plans, approbation et création 3D assistée avec parcours manuel toujours disponible |
| UX11 — Qualifier et basculer | 0 | Revue transversale des 717 routes, parité des 66 fenêtres, distributions Windows/Linux et accord utilisateur |

### Ordre interne de chaque vague

1. Reprendre un parcours réel de Forgotten Forest et un cas vide minimal.
2. Sélectionner un lot cohérent de une à cinq IDs dans la matrice.
3. Écrire le scénario humain et les états vide, chargement, succès, avertissement et erreur.
4. Brancher la commande Application et la préservation des données avant d’activer le contrôle.
5. Ajouter tests domaine/application, test du composant de présentation et smoke Godot de la page.
6. Revoir visuellement et interactivement aux résolutions cibles, avec clavier et niveau Débutant.
7. Lier les preuves, puis seulement promouvoir la ligne de migration concernée.

## Incrément livré : UX0-A

`UX0-A — Découverte et navigation humaine` est qualifié localement le 15 août 2026 :

- `ORF-SHELL-002` — Explorateur hiérarchique du projet ;
- `ORF-SHELL-008` — Filtres d’affichage ;
- `ORF-SHELL-015` — Recherche globale ;
- `ORF-SHELL-016` — Find references ;
- `ORF-SHELL-021` — Aide contextuelle.

La recherche `Ctrl+K` trouve pages, contenus, ressources, objets et actions en français ;
l’Explorateur est synchronisé avec la 3D ; les filtres ont des vues enregistrables ; les références
sont ouvrables depuis le contexte ; l’aide expose la tâche et l’action suivante. Les cinq lignes
sont `functional` et liées à `docs/acceptance/STUDIO_UX0_A.md`. Les inspections Forward+ FHD et QHD
confirment une fenêtre unique et la conservation de la vue 3D.

## Incrément livré : UX0-B

`UX0-B — Organiser son espace de travail` est qualifié localement le 15 août 2026 :

- `ORF-SHELL-001` — Disposition multi-panneaux du Toolset ;
- `ORF-SHELL-012` — Édition multi-zones par onglets ;
- `ORF-SHELL-022` — Raccourcis configurables ;
- `ORF-SHELL-023` — Profils d’éditeur ;
- `ORF-EE-003` — Plusieurs onglets de zone.

Trois zones peuvent rester ouvertes sans partager sélection ou caméra ; une comparaison 3D protégée
reste en lecture seule. Les profils et dispositions passent par dry-run, historique et sauvegarde
explicite. Les quatorze raccourcis visibles ont des presets, une recherche, une détection de conflit et
un stockage atomique. Les cinq lignes sont `functional` et liées à
`docs/acceptance/STUDIO_UX0_B.md`.

## Incrément livré : UX0-C

`UX0-C — Manipuler la scène sans quitter le Studio` est qualifié localement le 15 août 2026 :

- `ORF-SHELL-003` — Vue 3D principale ;
- `ORF-SHELL-006` — Sélection et inspection ;
- `ORF-SHELL-007` — Navigation caméra d’édition ;
- `ORF-SHELL-011` — Transformation avec gizmos ;
- `ORF-SHELL-013` — Undo et redo complets.

Le parcours réunit six modes 3D, multisélection, cadrage et verrouillage, caméra configurable avec
vues orthographiques et bookmarks, puis translation, rotation, échelle, pivots, repères, snaps,
saisie numérique et alignements. Les mutations passent par les commandes Application et leurs
historiques ; la sauvegarde des préférences reste explicite. Les cinq lignes sont `functional` et
liées à `docs/acceptance/STUDIO_UX0_C.md`.

## Incrément livré : UX0-D

`UX0-D — Placer et réutiliser sans friction` est qualifié localement le 15 août 2026 :

- `ORF-SHELL-004` — Palette contextuelle ;
- `ORF-SHELL-005` — Palettes Standard et Custom ;
- `ORF-SHELL-009` — Placement répété ;
- `ORF-SHELL-010` — Retour visuel de placement invalide ;
- `ORF-SHELL-014` — Copier, couper, coller et dupliquer.

La palette distingue sept vues, les contenus protégés et personnalisables, six familles de filtres,
les favoris, les récents et les palettes nommées. Ligne, grille, dispersion et brosse disposent d’un
aperçu vert/rouge avant application ; collision, raccord et budget sont validés sans UUID visible.
Copier, couper, coller en instance ou copie indépendante et dupliquer préservent les champs non
affichés et forment des opérations groupées annulables. Les cinq lignes sont `functional` et liées à
`docs/acceptance/STUDIO_UX0_D.md`.

## Incrément livré : UX0-E

`UX0-E — Créer un projet sans identifiants techniques` est qualifié localement le 15 août 2026 :

- `ORF-PROJ-001` — Assistant de création de projet ;
- `ORF-PROJ-002` — Métadonnées localisées du projet ;
- `ORF-PROJ-003` — Identité technique stable ;
- `ORF-PROJ-004` — Première zone obligatoire ;
- `ORF-PROJ-005` — Point de départ du joueur.

L’assistant en cinq étapes prévisualise sans écrire, crée puis vérifie un workspace, et sait produire
un RPG avec zone comme un projet Core sans zone. La page Projet modifie toutes les métadonnées
localisées, les choix techniques et le point de départ nommé par dry-run, commande, undo/redo et
sauvegarde atomique, sans exposer les identités internes. Les choix personnalisés non reconnus sont
présentés comme un profil existant et préservés octet pour octet. Les cinq lignes sont `functional`
et liées à `docs/acceptance/STUDIO_UX0_E.md`.

## Incrément livré : UX0-F

`UX0-F — Relier les événements de vie du module` est qualifié localement le 15 août 2026 :

- `ORF-PROJ-006` — Événement de chargement du module ;
- `ORF-PROJ-007` — Événements de connexion client ;
- `ORF-PROJ-008` — Événements d’objet acquis/perdu ;
- `ORF-PROJ-009` — Événements d’utilisation d’objet ;
- `ORF-PROJ-010` — Événements de mort et respawn.

La page Projet présente cinq cartes et vingt liaisons nommées, recherche les scripts ForgeScript par
nom humain, bloque les fichiers ou handlers absents, prévisualise le diff et applique la configuration
par une commande annulable. Les hooks équipement/progression non affichés et tous les profils voisins
sont préservés. Le format `0.6.35`, le runtime, le smoke Godot et les captures Forward+ prouvent le
round-trip et la résolution contextuelle. Les cinq lignes sont `functional` et liées à
`docs/acceptance/STUDIO_UX0_F.md`.

## Incrément livré : UX0-G

`UX0-G — Compléter les événements globaux du module` est qualifié localement le 15 août 2026 :

- `ORF-PROJ-011` — Événements d’équipement et niveau ;
- `ORF-PROJ-012` — Événements de repos ;
- `ORF-PROJ-013` — Événement heartbeat global ;
- `ORF-PROJ-014` — Événement utilisateur global.

Les quatre nouvelles cartes exposent équipement/progression, quatre transitions de repos, une liste
de rythmes globaux bornés et des signaux utilisateur typés créés par modèles humains. Le même
sélecteur de scripts, le dry-run et la commande atomique couvrent l’ensemble ; le format `0.6.36`,
le runtime, le smoke Godot et les captures Forward+ prouvent compilation, exécution et round-trip.
Les quatre lignes sont `functional` et liées à `docs/acceptance/STUDIO_UX0_G.md`.

## Incrément livré : UX0-H

`UX0-H — Régler le temps et les transitions globales` est qualifié localement le 15 août 2026 :

- `ORF-PROJ-015` — Abort de cutscene ;
- `ORF-PROJ-016` — Temps et calendrier du monde ;
- `ORF-PROJ-017` — Échelle de progression.

Les trois cartes partagent le formulaire humain, le sélecteur de scripts et la transaction atomique
de politiques. Le runtime restaure puis publie l’abandon, avance un calendrier configurable avec
fuseaux et événements astronomiques, puis résout une progression fondée sur courbe, groupe,
difficulté et source. Les captures Forward+ FHD, QHD et 4K exactes ont été inspectées. Les trois
lignes sont `functional` et liées à `docs/acceptance/STUDIO_UX0_H.md`. La séquence de démarrage
`ORF-PROJ-018` reste inchangée dans la page Cinématiques de UX5.

## Incrément livré : UX0-I

`UX0-I — Compléter les données globales du projet` est qualifié localement le 15 août 2026 :

- `ORF-PROJ-019` — Variables de projet ;
- `ORF-PROJ-021` — Scripts mis en cache ;
- `ORF-PROJ-022` — Description et instructions joueur.

La nouvelle section **Données et guide joueur** expose une liste/formulaire de variables typées sans
clé technique visible, un cache ForgeScript réellement compilé et invalidé par source/API/compilateur,
puis huit pages localisées directement éditables. Les deux commandes Application canoniques assurent
dry-run, diagnostics, undo/redo et persistance exacte sans perdre les profils voisins. Les captures
Forward+ Full HD, QHD et 4K exactes ont été inspectées ; les trois lignes sont `functional` et liées à
`docs/acceptance/STUDIO_UX0_I.md`.

## Incrément livré : UX0-J

`UX0-J — Fermer la coque de projet` est qualifié localement le 15 août 2026 :

- `ORF-SHELL-017` — Renommage sûr ;
- `ORF-SHELL-020` — Messages, erreurs et statut.

La recherche globale ouvre un renommage humain avec aperçu obligatoire. Les commandes canoniques
préservent identités et références, revalident la révision avant application et fournissent undo/redo
pour le contenu comme pour les ressources. La page Validation expose les diagnostics structurés,
leurs cibles nommées, liens profonds, correctifs prévisualisés, activité récente et export expurgé.
Les captures Forward+ Full HD, QHD et 4K exactes ont été inspectées ; les deux lignes sont
`functional` et liées à `docs/acceptance/STUDIO_UX0_J.md`. Les 42 routes UX0 sont donc closes sans
absorber les pages métier des vagues UX1 à UX10.

## Incrément livré : UX1-A

`UX1-A — Créer un document de zone` est qualifié localement le 16 août 2026 :

- `ORF-AREA-001` — Assistant de création de zone ;
- `ORF-AREA-002` — Document de zone indépendant.

La page Zones ouvre un assistant humain en trois étapes depuis le bouton **Nouvelle zone**, l’action
de première zone ou `Ctrl+K`. L’aperçu appelle `CreateAreaHandler` en dry-run ; l’application
revalide la révision, rejoint `ProjectCommandHistory`, active le nouvel onglet et garde la sauvegarde
explicite. Le smoke prouve gabarits vide/jouable, fondation choisie, refus humain, undo/redo,
préservation des voisins, fichier de zone unique et round-trip avec identités stables. Les captures
Forward+ Full HD, QHD et 4K exactes ont été inspectées ; l’overlay adapte son échelle aux trois
résolutions. Les deux lignes sont `functional` et liées à `docs/acceptance/STUDIO_UX1_A.md`.

## Incrément livré : UX1-B

`UX1-B — Adapter et copier une zone` est qualifié localement le 16 août 2026 :

- `ORF-AREA-011` — Redimensionnement de zone ;
- `ORF-AREA-012` — Copie de zone.

La page Zones ouvre **Dimensions & copie** dans la fenêtre unique et `Ctrl+K` route directement vers
chaque parcours. Le redimensionnement prévisualise ancrage, objets conservés, replacés ou retirés,
puis utilise `EditAreaGeometryHandler`. La copie prévisualise contenu, transitions et profils, puis
utilise `DuplicateAreaHandler` avec remappage des identités. Dry-run, révision courante, undo/redo,
préservation des voisins, document indépendant et round-trip sont prouvés. Les captures Forward+
Full HD, QHD et 4K exactes ont été inspectées ; les deux lignes sont `functional` et liées à
`docs/acceptance/STUDIO_UX1_B.md`.

## Incrément livré : UX1-C

`UX1-C — Écran de chargement de zone` est qualifié localement le 16 août 2026 :

- `ORF-AREA-028` — Écran de chargement.

La page Zones ouvre **Chargement** dans la fenêtre unique et `Ctrl+K` fournit la même route. Le
formulaire choisit image et vidéo parmi les médias importés, édite titre, astuces françaises et
anglaises, repli accessible, progression, préchargement et durée minimale sans chemin technique.
L’aperçu passe par `SetAreaExperienceProfileHandler` en dry-run, puis compare rendu normal et
mouvements réduits avec `AreaExperienceRuntimeService`. L’application rejoint l’historique projet ;
les autres données d’expérience, le monde et les zones voisines restent préservés. Le smoke charge
réellement deux ressources avec `AreaStreamingCoordinator`, couvre fallback, refus humain,
undo/redo et round-trip. Les captures Forward+ Full HD, QHD et 4K exactes ont été inspectées ; la
ligne est `functional` et liée à `docs/acceptance/STUDIO_UX1_C.md`.

## Incrément livré : UX1-D

`UX1-D — Calques et groupes d’édition` est qualifié localement le 16 août 2026 :

- `ORF-AREA-035` — Couches et groupes d’édition.

La page Zones ouvre **Calques** dans la fenêtre unique et `Ctrl+K` fournit la même route. L’arbre
hiérarchique accepte le glisser-déposer et un sélecteur parent accessible, affiche visibilité et
verrouillage hérités ainsi que le responsable, affecte les objets par noms humains et synchronise la
sélection de groupe avec la vue 3D. L’aperçu passe par `SetAreaAuthoringHandler` en dry-run, résout la
hiérarchie canonique et détaille l’export partiel avant toute mutation. L’application rejoint
l’historique projet ; les autres profils de zone et les zones voisines restent préservés. Le smoke
couvre refus humains, contrat runtime requis/facultatif, undo/redo et round-trip. Les captures
Forward+ Full HD, QHD et 4K exactes ont été inspectées ; la ligne est `functional` et liée à
`docs/acceptance/STUDIO_UX1_D.md`.

## Incrément livré : UX1-E

`UX1-E — Blueprint de placeable` qualifie `ORF-PLC-001` dans la fenêtre unique : ressource choisie
par nom et usage réel, collision, rôle, composants, scripts et transformation, aperçu canonique,
sélection des instances, undo/redo, préservation et round-trip. Les preuves sont liées à
`docs/acceptance/STUDIO_UX1_E.md`.

## Incrément livré : UX1-F

`UX1-F — Décor statique` qualifie `ORF-PLC-002` : regroupement, instanciation, occlusion, fusion sûre
et collision optionnelle avec résumé d’impact, parité commande/UI et preuves Forward+ Full HD, QHD
et 4K. La ligne est liée à `docs/acceptance/STUDIO_UX1_F.md`.

## Incrément livré : UX1-G

`UX1-G — Objet utilisable` qualifie `ORF-PLC-003` : actions ajoutables par cartes, portée, angle,
durée, focus, autorité, conditions et événement `OnUsed`, avec aperçu sec, sélection 3D, événement
runtime typé, préservation, undo/redo et round-trip. Les captures Forward+ Full HD, QHD et 4K ont été
inspectées ; la ligne est `functional` et liée à `docs/acceptance/STUDIO_UX1_G.md`.

## Incrément livré : UX1-H

`UX1-H — Conteneur` qualifie `ORF-PLC-004` : capacité et masse, propriétaire, droits d’accès,
persistance, réplication, inventaire, butin et présentation, avec aperçu sec, projection RPG Kit,
lecture ForgeScript, préservation des capacités voisines, undo/redo et round-trip. Les captures
Forward+ Full HD, QHD et 4K ont été inspectées ; la ligne est `functional` et liée à
`docs/acceptance/STUDIO_UX1_H.md`.

## Incrément livré : UX1-I

`UX1-I — Identité et apparence` qualifie `ORF-PLC-005` : nom court, noms et invitations localisés,
tags, notes, asset principal, meshes, matériaux, icône, portrait et VFX, avec aperçu sec, projection
RPG Kit, lecture ForgeScript/MCP, préservation, undo/redo et round-trip. Les captures Forward+ Full
HD, QHD et 4K ont été inspectées ; la ligne est `functional` et liée à
`docs/acceptance/STUDIO_UX1_I.md`.

## Incrément livré : UX1-J

`UX1-J — Transformation précise` qualifie `ORF-PLC-006` : position, rotation, échelle, pivot, grille,
parent humain, alignement de surface et animation, avec aperçu sec, projection RPG Kit, lecture
ForgeScript/MCP, hiérarchie validée, préservation, undo/redo et round-trip. Les captures Forward+
Full HD, QHD et 4K ont été inspectées ; la ligne est `functional` et liée à
`docs/acceptance/STUDIO_UX1_J.md`.

## Incrément livré : UX1-K

`UX1-K — Animation initiale` qualifie `ORF-PLC-007` : état chargé parmi six états humains,
animation, apparence et interaction par état, transitions ajoutables avec déclencheur, durée et
condition, puis aperçu sémantique et exécution déterministe RPG Kit. La lecture ForgeScript/MCP,
la préservation des capacités voisines, l’undo/redo et le round-trip sont prouvés. Les captures
Forward+ Full HD, QHD et 4K ont été inspectées ; la ligne est `functional` et liée à
`docs/acceptance/STUDIO_UX1_K.md`.

## Incrément livré : UX1-L

`UX1-L — Destructibilité` qualifie `ORF-PLC-008` : santé, armure, résistances typées, étapes
visuelles, effet de destruction, débris, réparation et aperçu local de dégâts, avec aperçu sec,
projection et exécution RPG Kit, lecture ForgeScript/MCP, préservation, undo/redo et round-trip. Les
captures Forward+ Full HD, QHD et 4K ont été inspectées ; la ligne est `functional` et liée à
`docs/acceptance/STUDIO_UX1_L.md`.

## Incrément livré : UX1-M

`UX1-M — Protection plot` qualifie `ORF-PLC-009` : actions protégées, droits de contournement,
feedback joueur et dérogation MJ avec motif et audit gouverné, avec aperçu sec, enforcement RPG Kit,
lecture ForgeScript/MCP, préservation, undo/redo et round-trip. Les captures Forward+ Full HD, QHD
et 4K ont été inspectées ; la ligne est `functional` et liée à
`docs/acceptance/STUDIO_UX1_M.md`.

## Incrément livré : UX1-N

`UX1-N — Faction et propriété` qualifie `ORF-PLC-010` : faction principale, appartenances et
politiques d’hostilité, crime, accès et ciblage, avec aperçu sec, projection et décisions RPG Kit,
lecture ForgeScript/MCP, préservation du champ Aurora historique, undo/redo et round-trip. Les
captures Forward+ Full HD, QHD et 4K ont été inspectées sur des factions synthétiques en mémoire ;
la ligne est `functional` et liée à `docs/acceptance/STUDIO_UX1_N.md`.

## Incrément livré : UX1-O

`UX1-O — Conversation` qualifie `ORF-PLC-011` : dialogue et point d’entrée, cinq sources de
déclenchement, priorité, usage unique et aperçu sans écriture, avec projection et décision RPG Kit,
lecture ForgeScript/MCP, préservation du dialogue Aurora historique, undo/redo et round-trip. Les
captures Forward+ Full HD, QHD et 4K ont été inspectées sur le catalogue réel de Forgotten Forest,
sans sauvegarder de liaison de preuve ; la ligne est `functional` et liée à
`docs/acceptance/STUDIO_UX1_O.md`.

## Incrément livré : UX1-P

`UX1-P — Verrouillage` qualifie `ORF-PLC-012` : quatre états, clé item réelle, justificatifs,
compétence et difficulté, code masqué, piratage, droits, reverrouillage, brouillage et aperçu sans
écriture, avec résolution RPG Kit, lecture ForgeScript/MCP sans secret, préservation, undo/redo et
round-trip. Les captures Forward+ Full HD, QHD et 4K ont été inspectées sur le catalogue réel de
Forgotten Forest, sans sauvegarder de verrou de preuve ; la ligne est `functional` et liée à
`docs/acceptance/STUDIO_UX1_P.md`.

## Incrément livré : UX1-Q

`UX1-Q — Clé consommée` qualifie `ORF-PLC-013` : conservation, consommation, charges, partage,
permission virtuelle et audit, avec aperçu sans écriture, décision RPG Kit, lecture ForgeScript/MCP,
préservation du verrou et des autres données, undo/redo et round-trip. Les captures Forward+ Full HD,
QHD et 4K ont été inspectées sur le catalogue réel de Forgotten Forest, sans sauvegarder de politique
de preuve ; la ligne est `functional` et liée à `docs/acceptance/STUDIO_UX1_Q.md`.

## Incrément livré : UX1-R

`UX1-R — Piège` qualifie `ORF-PLC-014` : déclencheur, effet, détection, désamorçage, réarmement,
propriétaire, politique alliée, VFX, état armé et usage unique, avec aperçu sans effet de scène,
projection RPG Kit, lecture ForgeScript/MCP, sous-profils de sécurité indépendants, préservation des
événements voisins, undo/redo et round-trip. Les captures Forward+ Full HD, QHD et 4K ont été
inspectées sur le catalogue réel de Forgotten Forest, sans sauvegarder de piège de preuve ; la ligne
est `functional` et liée à `docs/acceptance/STUDIO_UX1_R.md`.

## Incrément livré : UX1-S

`UX1-S — Trésor initial` qualifie `ORF-PLC-015` : objets nommés, catégories, poids, quantités,
qualité, conditions, unicité, moments chargement/ouverture/réinitialisation, graine et état généré
persistant, avec remise à zéro explicite et aperçu sans écriture. La projection RPG Kit est
indépendante du piège et des anciens événements voisins ; lecture ForgeScript/MCP, préservation,
undo/redo et round-trip sont prouvés. Les captures Forward+ Full HD, QHD et 4K ont été inspectées sur
le catalogue réel de Forgotten Forest, sans sauvegarder de trésor de preuve ; la ligne est
`functional` et liée à `docs/acceptance/STUDIO_UX1_S.md`.

## Incrément livré : UX1-T

`UX1-T — Ouverture et fermeture` qualifie `ORF-PLC-016` : version ForgeScript, liaisons OnOpen et
OnClose, acteur, résultat, état précédent et autorité, avec aperçu de dispatch sans écriture. La
projection RPG Kit est indépendante du piège, du trésor et des autres événements ; lecture
ForgeScript/MCP, validation optionnelle ou requise, préservation, undo/redo et round-trip sont
prouvés. La capture Forward+ Full HD a été inspectée sur le catalogue réel de Forgotten Forest,
sans sauvegarder d’événements de preuve ; la ligne est `functional` et liée à
`docs/acceptance/STUDIO_UX1_T.md`. Les preuves QHD et 4K sont réservées au jalon multi-lots.

## Incrément livré : UX1-U

`UX1-U — Verrouillage et déverrouillage` qualifie `ORF-PLC-017` : version ForgeScript, événements
avant et après, méthode, justificatif, acteur et veto contrôlé, avec aperçus accepté ou refusé sans
écriture. La projection RPG Kit est indépendante du piège, du trésor et des événements voisins ;
lecture ForgeScript/MCP, validation optionnelle ou requise, préservation, undo/redo et round-trip
sont prouvés. La capture Forward+ Full HD a été inspectée sur le catalogue réel de Forgotten Forest,
sans sauvegarder d’événements de preuve ; la ligne est `functional` et liée à
`docs/acceptance/STUDIO_UX1_U.md`. Les preuves QHD et 4K sont réservées au jalon multi-lots.

## Premier train parallèle livré : UX1-V et UX1-W

La reprise parallèle bornée à la vague UX1 a livré six routes le 16 août 2026. Les 42 routes UX1
restantes ne deviennent pas accessibles avant leur preuve individuelle et leur intégration dans le
checkout principal gelé. Le premier train a conservé des fichiers métier séparés par page :

- `UX1-V` : `ORF-PLC-018`, `ORF-PLC-019`, `ORF-PLC-020` sur la page Objets & interactions ;
- `UX1-W` : `ORF-EE-002`, `ORF-EE-018`, `ORF-EE-019` sur la page Zones ;
- audit en avance : premières familles Doors puis Triggers, sans promotion ni mutation centrale.

Chaque writer utilise un worktree local dédié. Le coordinateur garde seul les routeurs, la solution,
les matrices, les preuves, les documents d'état et le graphe généré. Après arrêt de toutes les
écritures, les branches sont intégrées une par une puis build, tests .NET, smoke Godot, capture
Forward+ et contrôles de matrice sont exécutés séquentiellement. Une préparation parallèle n'est
jamais une preuve `functional`. Cette séparation a été respectée : les deux branches ont été revues
statiquement, raccordées au shell par le coordinateur, puis validées l'une après l'autre.

### UX1-V — Événements de cycle de vie du placeable

`ORF-PLC-018`, `ORF-PLC-019` et `ORF-PLC-020` sont `functional`. La fiche Placeable expose
`OnUsed`, `OnDisturbed`, `OnDamaged` et `OnDeath`, sans mutation à l'ouverture. L'autorité dégâts
directe ou composée passe par un résolveur Domain commun aux validateurs, adaptateurs runtime et à
ForgeScript/MCP ; les représentations duales divergentes sont refusées. Anciens JSON, préservation,
undo/redo et round-trip sont couverts. Le portail ciblé passe 21/21 en 63,75 s et la capture
Forward+ Full HD inspectée est liée à `docs/acceptance/STUDIO_UX1_V.md`. La modification du contrat
partagé a aussi déclenché le portail complet, réussi avec 1 980 tests .NET et 39 tests Python.

### UX1-W — Outils dynamiques de zone

`ORF-EE-002`, `ORF-EE-018` et `ORF-EE-019` sont `functional`. La page Zones expose l'échelle de la
sélection explicite, la politique globale des zones dynamiques et un aperçu de copie runtime limité
aux données réellement authored. Les identités d'aperçu sont éphémères ; propriétaire et références
runtime restent déclarés indisponibles. Le portail ciblé passe 27/27 en 56,81 s et la capture
Forward+ Full HD inspectée est liée à `docs/acceptance/STUDIO_UX1_W.md`. Le train intégré passe le
même portail complet sans avertissement ni régression.

## Deuxième train parallèle livré : UX1-X, UX1-Y et UX1-Z

Le second train a livré onze routes UX1 supplémentaires le 16 août 2026. La matrice compte désormais
82 routes `functional` et 635 `planned` ; 31 routes de la vague UX1 restent à qualifier. Les trois
writers ont conservé leurs modèles, sessions et overlays dans des fichiers dédiés, puis le
coordinateur a intégré seul les hooks partagés, les preuves, la matrice et les corrections issues
des smokes réels.

### UX1-X — Fondation de porte

`ORF-DOOR-001`, `ORF-DOOR-002` et `ORF-DOOR-003` sont `functional`. La porte doit être choisie
explicitement ; placement libre/ancré, neuf états et interaction d'ouverture passent par dry-run,
commande, garde de révision, undo/redo et round-trip. Les champs Door voisins et l'ordre du
catalogue sont préservés. La Navigation exigée par le validateur reste une valeur interne non
exposée et ne promeut pas `ORF-DOOR-019`. Le portail incrémental passe 5/5 en 71,99 s et la capture
Forward+ FHD inspectée est liée à `docs/acceptance/STUDIO_UX1_X.md`.

### UX1-Y — Fondation polygonale de trigger

`ORF-TRG-001`, `ORF-TRG-002`, `ORF-TRG-006` et `ORF-TRG-007` sont `functional`. La surface édite un
polygone extrudé, ses trous, filtres, conditions, actions et événements VolumeEntered/VolumeExited.
Les scripts facultatifs restent `null` lorsqu'ils sont blancs ; Portal, Validation, Advanced et
parent caché sont préservés. Les formes 3D restent en lecture seule sans conversion. Le portail
incrémental passe 8/8 en 62,22 s ; les captures Forward+ FHD, QHD et 4K ont été inspectées après une
correction qui borne le contenu et maintient les actions visibles. La preuve est liée à
`docs/acceptance/STUDIO_UX1_Y.md`.

### UX1-Z — Réactions et physique du placeable

`ORF-PLC-021`, `ORF-PLC-022`, `ORF-PLC-023` et `ORF-PLC-024` sont `functional`. Attaques, capacités,
heartbeat, événement personnalisé et politique physique disposent de quatre aperçus sans écriture.
Un profil absent est créé sans asset inventé ; scheduler dormant et capacités voisines restent
intacts. Le Studio annonce une politique sans prétendre simuler moteur ou réseau. Le portail
incrémental passe 5/5 en 60,77 s et la capture Forward+ FHD inspectée est liée à
`docs/acceptance/STUDIO_UX1_Z.md`.

Le jalon intégré UX1-X/Y/Z passe ensuite `scripts/Verify.ps1` dans son intégralité : 1 980 tests
.NET, 39 tests Python, builds sans avertissement, graphe Roslyn déterministe à 8 959 nœuds,
59 067 relations et 0 diagnostic, puis smokes Studio, Runtime, Forward+, MCP et réseau. Les onze
routes ne sont promues qu'après ce verdict transversal.

## Troisième train parallèle livré : UX1-AA, UX1-AB et UX1-AC

Le troisième train a livré onze routes UX1 supplémentaires le 17 août 2026. La matrice compte
désormais 93 routes `functional` et 624 `planned` ; 20 routes de la vague UX1 restent à qualifier.
Les trois writers ont travaillé sur des fichiers dédiés depuis la même base, sans build concurrent.
Le coordinateur a revu puis intégré les branches une à une, raccordé seul le shell partagé et
sérialisé builds, tests, smokes, captures, matrice et graphe.

### UX1-AA — Assemblage et validation du placeable

`ORF-PLC-025`, `ORF-PLC-026` et `ORF-PLC-027` sont `functional`. La surface impose une sélection
explicite, édite socket/assemblage et actions contextuelles, puis localise les échecs collision,
références et budgets. Foundation, collision, Operations et DamageEvents manquants bloquent le
parcours produit sans synthèse. Le portail incrémental passe 8/8 en 54,91 s ; les captures Forward+
FHD/QHD/4K inspectées et la preuve isolée sont liées à `docs/acceptance/STUDIO_UX1_AA.md`.

### UX1-AB — Dégâts, chargement et événements de porte

`ORF-DOOR-007`, `ORF-DOOR-010`, `ORF-DOOR-014` et `ORF-DOOR-015` sont `functional`. Solidité,
présentation de chargement, OnDamaged/OnDeath et attaque physique passent par dry-run, garde de
révision, commande, undo/redo et round-trip. Les événements gérés sont fusionnés sous autorité
serveur ; les événements et agrégats voisins restent préservés. Le portail incrémental passe 2/2 en
55,42 s et les captures FHD/QHD/4K sont liées à `docs/acceptance/STUDIO_UX1_AB.md`.

### UX1-AC — Trigger avancé

`ORF-TRG-004`, `ORF-TRG-011`, `ORF-TRG-017` et `ORF-TRG-019` sont `functional`. Le formulaire
expose piège, filtre de cible, dix volumes obligatoires et occupation. Une Foundation absente reste
un état bloqué renvoyant vers UX1-Y ; seule la preuve emploie une session temporaire isolée. Le
portail incrémental passe 10/10 en 59,92 s et les captures FHD/QHD/4K inspectées sont liées à
`docs/acceptance/STUDIO_UX1_AC.md`.

Le jalon intégré UX1-AA/AB/AC a ensuite passé `scripts/Verify.ps1` dans son intégralité : 1 980 tests
.NET, 39 tests Python, builds sans avertissement, graphe Roslyn déterministe à 9 010 nœuds et
59 698 relations sans diagnostic, puis smokes Studio, Runtime, Forward+, MCP et réseau. Ce verdict
transversal clôt la promotion des onze routes.

## Quatrième train parallèle livré : UX1-AD, UX1-AE et UX1-AF

Le quatrième train a livré dix routes UX1 supplémentaires le 17 août 2026. La matrice compte
désormais 103 routes `functional` et 614 `planned` ; dix routes de la vague UX1 restent à qualifier.
Les trois writers ont conservé des fichiers propres à leur lot, tandis que le coordinateur a gardé
les hooks partagés, les validations séquentielles, les preuves, la matrice et le graphe.

### UX1-AD — Réactions et assemblage de porte

`ORF-DOOR-016`, `ORF-DOOR-017`, `ORF-DOOR-018` et `ORF-DOOR-020` sont `functional`. La surface
expose capacités, heartbeat/événement personnalisé, conversation/faction et jusqu'à huit panneaux
coordonnés. La fusion ne remplace que les événements explicitement gérés ; les profils hors bornes
sont refusés avant tout clamp. Le portail incrémental passe 4/4 en 68,48 s et les captures
FHD/QHD/4K sont liées à `docs/acceptance/STUDIO_UX1_AD.md`.

### UX1-AE — Découverte et accès du trigger

`ORF-TRG-005`, `ORF-TRG-008`, `ORF-TRG-012` et `ORF-TRG-013` sont `functional`. Secret,
interaction, présentation et justificatif partagent les contrats canoniques Discovery/Interaction/
Access. Une Foundation reste obligatoire et le script OnClick doit appartenir à son allowlist de
validation. Le portail incrémental passe 5/5 en 68,12 s ; les captures FHD/QHD/4K et la preuve isolée
sont liées à `docs/acceptance/STUDIO_UX1_AE.md`.

### UX1-AF — Plans de zone assistés

`ORF-AI-014` et `ORF-AI-015` sont `functional`. Le document de plan et la construction géométrique
restent deux transactions et deux historiques distincts, chacun précédé de son dry-run et lié à sa
révision. Confidentialité, provenance, coût, diff sémantique et politique réseau sont visibles. Le
portail incrémental hors ligne passe 17/17 en 75,68 s et les captures FHD/QHD/4K sont liées à
`docs/acceptance/STUDIO_UX1_AF.md`.

Le jalon intégré UX1-AD/AE/AF a ensuite passé `scripts/Verify.ps1` dans son intégralité : 1 980 tests
.NET, 39 tests Python, builds sans avertissement, graphe Roslyn déterministe à 9 059 nœuds et
60 404 relations sans diagnostic, puis smokes Studio, Runtime, Forward+, MCP et réseau. Les dix
routes ne sont promues qu'après ce verdict transversal.

## Cinquième et dernier train UX1 livré : UX1-AG, UX1-AH, UX1-AI et UX1-AJ

Le dernier train UX1 a livré dix routes supplémentaires le 17 août 2026. La matrice compte désormais
113 routes `functional` et 604 `planned`, et la vague UX1 est close à 71/71. Les quatre lots ont
conservé leurs surfaces dédiées, tandis que l'intégration des hooks, les validations .NET
séquentielles, les preuves et la promotion sont restées sérialisées.

### UX1-AG — Accès et événements de porte

`ORF-DOOR-004`, `ORF-DOOR-005`, `ORF-DOOR-006`, `ORF-DOOR-012` et `ORF-DOOR-013` sont
`functional`. Verrou/clé, refus, piège et événements terminaux partagent le profil canonique. Un
scaffold PortalUse interne satisfait uniquement l'agrégat neuf et ne promeut pas `ORF-DOOR-011`.
Le portail passe 18/18 en 75,98 s ; preuves et limites sont liées à
`docs/acceptance/STUDIO_UX1_AG.md`.

### UX1-AH — Automatisation du trigger

`ORF-TRG-009`, `ORF-TRG-010` et `ORF-TRG-018` sont `functional`. Heartbeat, namespace personnalisé
et contrôle MJ audité restent une politique bornée : aucun timer ni dispatch Live Director n'est
simulé. Le portail passe 6/6 en 69,34 s ; preuves et limites sont liées à
`docs/acceptance/STUDIO_UX1_AH.md`.

### UX1-AI — Validation canonique du trigger

`ORF-TRG-020` est `functional`. Le contexte de cibles, scripts, destinations et couches est optionnel
ou requis ; les UUID de destination sont projetés en choix humains et les références héritées
inconnues restent masquées, protégées et préservées. Le portail passe 6/6 en 64,99 s ; preuves et
limites sont liées à `docs/acceptance/STUDIO_UX1_AI.md`.

### UX1-AJ — Validation canonique de porte

`ORF-DOOR-021` est `functional`. Le même verdict canonique contrôle Studio, commande, MCP et
prébuild sur destinations, animations, collisions, credentials, scripts et liens. Foundation et
Navigation restent des prérequis non édités ; `ORF-DOOR-019` n'est pas promu. Le portail passe 18/18
en 82,17 s ; preuves et limites sont liées à `docs/acceptance/STUDIO_UX1_AJ.md`.

Les quatre surfaces ont des captures Forward+ FHD réelles et une revue corrective QHD/4K. Le jalon
intégré a ensuite passé `scripts/Verify.ps1` dans son intégralité : 1 988 tests .NET, 39 tests Python,
builds sans avertissement, graphe Roslyn déterministe à 9 132 nœuds et 61 393 relations sans
diagnostic, puis smokes Studio, Toolset, Runtime, Forward+, MCP, réseau et sauvegarde. Ce verdict
transversal clôt la promotion des dix routes et la vague UX1.

## Premier train post-UX1 livré : UX2-A, UX4-A et UX5-A

Le train parallèle suivant a livré treize routes le 17 août 2026. La matrice compte désormais
126 routes `functional` et 591 `planned`. Les trois lots sont restés dans des fichiers dédiés ;
les hooks centraux, builds, tests, smokes, captures et promotions ont été intégrés séquentiellement.

### UX2-A — Politiques de zone vivante

`ORF-AREA-023`, `ORF-AREA-025` et `ORF-AREA-026` sont `functional`. Heartbeats, modificateurs de
perception et politique de repos sont édités par collections complètes puis appliqués dans une seule
transaction area-only. Les politiques Cinématiques, Combat et Expérience restent préservées. Le
portail passe 11/11 en 75,69 s, complété par le test RpgKit de perception 1/1 ; preuves et limites
sont liées à `docs/acceptance/STUDIO_UX2_A.md`.

### UX4-A — Fondation de factions

`ORF-FAC-001` à `ORF-FAC-005` sont `functional`. Création, archive ou suppression, parents,
héritage, relations directionnelles et bandes passent par les services canoniques. Les références
externes empêchent une suppression orpheline et `ORF-FAC-006` reste `planned`. Le portail passe
13/13 en 79,97 s ; la preuve FHD du portail utilise un fallback canonique isolé et une capture
séparée qualifie les données réelles de la Crypte. Preuves et limites sont liées à
`docs/acceptance/STUDIO_UX4_A.md`.

### UX5-A — Fondation de dialogues

`ORF-DLG-001` à `ORF-DLG-005` sont `functional`. L'éditeur ligne/réponses couvre graphe, nœuds,
branches paginées, liens existants et réordonnancement réel au clavier et par payload de
glisser-déposer. Les locuteurs et sous-graphes valides mais non éditables restent protégés. Le
portail passe 16/16 en 64,03 s ; preuves et limites sont liées à
`docs/acceptance/STUDIO_UX5_A.md`.

Les trois surfaces ont des captures Forward+ FHD réelles et une revue QHD/4K sans blocage. Le jalon
intégré a ensuite passé `scripts/Verify.ps1` dans son intégralité : 1 988 tests .NET, 39 tests Python,
builds sans avertissement, graphe Roslyn déterministe sans diagnostic, puis smokes Studio, Toolset,
Runtime, Forward+, MCP, réseau, sauvegarde et distribution. Le validateur de références de faction
du trigger est resté strict ; les fixtures Crypte et Toolset ont été corrigées pour reprendre l'ID
canonique `ash-watch` au lieu de fabriquer une quatrième faction.

## Vague UX2 close : 98 routes Monde fonctionnelles

Les 95 routes UX2 encore `planned` après UX2-A ont été intégrées et qualifiées le 17 août 2026.
La matrice compte désormais 221 routes `functional` et 496 `planned`; UX2 est close à 98/98, répartie
entre Terrain (8), Environnement (34), Monde vivant (29) et Navigation (27).

- Terrain : construction modulaire, groupes, effacement/remplacement, rotation/variantes,
  ajustement compatible, relief, features et propriétés complètes de tuile ;
- Environnement : classification, cycle, lumière, brouillard, climat, eau, ciel, audio complet,
  rendu/qualité, carte/FoW, LOD/HLOD et politiques eau/AA ;
- Monde vivant : cycle de vie, événements typés, autorité de combat, triggers spécialisés et les
  vingt routes de composition/activation/scénario/lifecycle/tactique/statistiques/validation des
  rencontres ;
- Navigation : surface et volume, anchors/waypoints/locators, portails Door/Trigger, patrouilles,
  visibilité/occlusion/LOD, routes aériennes/véhicule/inter-zones et ancres caméra.

Trente-six portails incrémentaux cumulent 3 117,53 s. Chacun prouve build Studio, smoke global,
capture Forward+ Full HD et matrice ; les tests ciblés varient selon les agrégats réellement touchés.
Les preuves et limites sont liées dans `docs/acceptance/STUDIO_UX2_A.md`,
`STUDIO_UX2_TERRAIN.md`, `STUDIO_UX2_ENVIRONMENT.md`, `STUDIO_UX2_LIVING_WORLD.md` et
`STUDIO_UX2_NAVIGATION.md`. Une promotion Studio ne transforme pas un plan déterministe en runtime :
playback audio, GPU, spawn d’acteurs, pathfinding moteur, caméra live et transport réseau restent
explicitement hors claim lorsque leur bridge n’est pas exécuté.

Le portail transversal de clôture a ensuite réussi sans avertissement : 2 033 tests .NET, 39 tests
Python, graphe Roslyn déterministe à 9 968 nœuds et 71 303 relations sans diagnostic, puis smokes
Studio, Toolset, Runtime, Forward+, MCP, réseau et distribution. Cette preuve globale complète les
36 portails incrémentaux ; elle ne transforme pas leurs limites explicites en capacités live.

## UX4-B — Factions vivantes

`ORF-FAC-006` à `ORF-FAC-010` sont `functional`. La page Contenu social édite réputation
personnelle et globale, propagation par témoins, affiliations multiples et factions dynamiques dans
le catalogue canonique. Le portail incrémental passe 51/51 tests factions en 102,95 s, le smoke
global et deux captures Forward+ FHD inspectées : fallback temporaire sur Forgotten Forest et copie
des données réelles de la Crypte. La matrice atteint 226 routes `functional` et 491 `planned`.
Les limites runtime et commandes de reproduction sont liées dans
`docs/acceptance/STUDIO_UX4_B.md` ; `ORF-FAC-011` à `ORF-FAC-014` relevaient du lot UX4-C suivant.

### UX4-C — Matrice et validation factionnelles

`ORF-FAC-011` à `ORF-FAC-014` sont `functional` : tags sémantiques, matrice versionnée avec
comparaison/import-export, réaction au joueur et validation localisée. Le portail passe 51/51 tests
en 108,39 s, le smoke global et deux captures Forward+ FHD inspectées. Les 14 routes FACTION sont
closes ; la matrice atteint 230 `functional` et 487 `planned`. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX4_C.md`.

### UX4-D — Fondation de créature

`ORF-CRE-001` à `ORF-CRE-005` sont `functional` : assistant data-driven, sélection et
initialisation explicites du blueprint, identité localisée, rôle/niveau de menace et composition
d'espèce. Le portail passe 77/77 tests en 101,74 s, le smoke global et quatre captures Forward+ FHD
inspectées, dont la vraie fondation de la Crypte et des preuves dédiées assistant/aperçu. La matrice
atteint 235 routes `functional` et 482 `planned`. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX4_D.md` ; `ORF-CRE-006` à `ORF-CRE-049` restent `planned`.

## Gates ergonomiques avant parité

Une page ne passe pas `Parity` si l’un des points suivants manque :

- un nouveau créateur ne peut pas accomplir son action principale sans explication orale ;
- une fonction de la matrice mène encore à un résumé générique ou un bouton désactivé ;
- créer, modifier, dupliquer ou supprimer exige du JSON ou un identifiant technique ;
- une erreur ne permet pas de revenir à l’objet ou au champ concerné ;
- une mutation ne passe pas par le bus de commandes ou perd des données non affichées ;
- la page ne fonctionne pas au clavier ou coupe ses actions principales sur une résolution cible ;
- une action longue bloque l’interface ou ne peut pas être annulée ;
- un statut technique est simulé, déduit ou présenté comme réussi sans preuve réelle ;
- la revue visuelle n’a porté que sur une capture automatique sans manipulation humaine.

## Sortie de migration

Le Creator historique reste disponible jusqu’à ce que :

1. les 717 lignes aient une route Studio et une preuve d’accès humain liée ;
2. les 66 lignes de `CREATOR_WINDOW_MIGRATION.csv` soient `Parity` ;
3. les neuf parcours du but final soient réalisés sans JSON dans le Studio exporté ;
4. Windows et Linux passent ouverture, édition, playtest, sauvegarde, export et reprise ;
5. un test novice, un test clavier et une revue Full HD, QHD et 4K ne trouvent aucun blocage majeur ;
6. l’utilisateur valide explicitement le remplacement du Creator historique.

Le retrait du plugin est une conséquence de cette qualification, jamais un moyen de la forcer.
