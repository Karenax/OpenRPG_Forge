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

Complément du 20 août 2026 : `ORF-CRE-033` OnSpawn est désormais `functional` dans la même surface.
Le handler est préservé avec son identité et son autorité serveur ; l'aperçu émet l'événement typé
seulement après validation complète. Le portail passe 20/20 tests en 154,82 s avec smoke global et
capture Forward+ FHD inspectée. La matrice atteint 278 `functional` et 439 `planned`.

### UX4-E — Présentation de créature

`ORF-CRE-006` à `ORF-CRE-010` sont `functional` : genre et pronoms data-driven, portraits
gouvernés avec aperçu d'image réel, référence de modèle/rig, parties du corps, couleurs, ailes et
queues. Le portail passe 8/8 tests en 100,01 s, le smoke global et cinq captures Forward+ FHD
inspectées. La matrice atteint 240 routes `functional` et 477 `planned`. Preuves et limites sont
liées dans `docs/acceptance/STUDIO_UX4_E.md` ; les règles passent au lot UX4-F.

### UX4-F — Animation et règles de créature

`ORF-CRE-011` à `ORF-CRE-015` sont `functional` : graphe d'animation, état initial et pose,
ressources, attributs, défenses et sauvegardes via `SetCreatureFoundationHandler`. Le portail passe
32/32 tests en 106,73 s, le smoke global et six captures Forward+ FHD inspectées, dont cinq sur le
profil réel de la Crypte. La matrice atteint 245 routes `functional` et 472 `planned`. Preuves et
limites sont liées dans `docs/acceptance/STUDIO_UX4_F.md` ; la progression passe au lot UX4-G.

### UX4-G — Progression de créature

`ORF-CRE-016` à `ORF-CRE-020` sont `functional` : classes et niveaux, axes d'alignement libres,
compétences, talents et pouvoirs appris/préparés via `SetCreatureProgressionHandler`. Le portail
passe 10/10 tests en 102,38 s, le smoke global et six captures Forward+ FHD inspectées, dont cinq
sur le profil réel de la Crypte. La preuve conserve explicitement le jeu de pistes piloté par
l'auto-level voisin. La matrice atteint 250 routes `functional` et 467 `planned`. Preuves et limites
sont liées dans `docs/acceptance/STUDIO_UX4_G.md` ; capacités et inventaire passent au lot UX4-H.

### UX4-H — Capacités et inventaire de créature

`ORF-CRE-021` et `ORF-CRE-022` sont `functional` : capacités spéciales, conteneurs, objets réels
du catalogue et équipement via `SetCreatureFoundationHandler`. Le portail passe 18/18 tests en
107,11 s, le smoke global et quatre captures Forward+ FHD inspectées, dont trois sur le profil réel
de la Crypte. Les références d'objet et d'équipement sont validées avant application, tandis que
les règles et profils voisins restent préservés. La matrice atteint 252 routes `functional` et 465
`planned`. Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX4_H.md` ; `ORF-CRE-023` à
`ORF-CRE-049` passent aux lots suivants.

### UX4-I — Loot, pickpocket et cadavre de créature

`ORF-CRE-023`, `ORF-CRE-024` et `ORF-CRE-032` sont `functional` : objets droppables, vol à la
tire et cycle de cadavre via `SetCreatureLootHandler`. Le portail passe 11/11 tests en 119,64 s,
le smoke global et cinq captures Forward+ FHD inspectées, dont quatre sur le profil réel de la
Crypte. Les références d'objet et de compétence sont validées avant application, tandis que les
profils de fondation, présentation, règles, progression et capacités restent préservés. La matrice
atteint 255 routes `functional` et 462 `planned`. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX4_I.md` ; `ORF-CRE-025` à `ORF-CRE-031` et `ORF-CRE-033` à
`ORF-CRE-049` restent `planned`.

### UX4-J — Interactions de créature

`ORF-CRE-025`, `ORF-CRE-026`, `ORF-CRE-028` et `ORF-CRE-031` sont `functional` : factions et
appartenances, perception multi-sens, conversations associées et protections narratives via
`SetCreatureFoundationHandler`. Le portail passe 9/9 tests en 115,01 s, le smoke global et six
captures Forward+ FHD inspectées, dont cinq sur le profil réel de la Crypte. Les références aux
factions et dialogues sont résolues avant application, tandis que `OnConversation` et tous les
profils voisins restent préservés. La matrice atteint 259 routes `functional` et 458 `planned`.
Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX4_J.md` ; `ORF-CRE-027` passe au lot
UX4-K, tandis que `ORF-CRE-029`, `ORF-CRE-030` et `ORF-CRE-033` à `ORF-CRE-049` restaient
`planned` à ce jalon.

Complément du 20 août 2026 : `ORF-CRE-034` OnConversation est désormais `functional` dans la même
surface. Seul le handler du binding serveur est édité ; son identité, son schéma, sa version API,
son événement et son autorité restent préservés. Le portail incrémental passe les 9/9 tests et le
smoke `on_conversation=true` en 135,91 s ; la capture Forward+ FHD est inspectée sans clipping.
La matrice atteint 279 routes `functional` et 438 `planned` ; le résumé reste `full_gate=false`.

### UX4-K — Déplacement de créature

`ORF-CRE-027` est `functional` : les six modes de déplacement et les valeurs de vitesse,
accélération, décélération, altitude et pente sont édités via `SetCreatureFoundationHandler` sans
clamp silencieux. Le portail passe 28/28 tests en 117,80 s, le smoke global et deux captures
Forward+ FHD inspectées, dont une sur le profil réel de la Crypte. La surface préserve toutes les
compositions et tous les blueprints voisins ; son aperçu reste une projection typée en mémoire,
sans acteur ni moteur physique exécuté. La matrice atteint 260 routes `functional` et 457
`planned`. Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX4_K.md` ;
`ORF-CRE-029` passe au lot UX4-L, tandis que `ORF-CRE-030` et `ORF-CRE-033` à
`ORF-CRE-049` restaient `planned` à ce jalon.

### UX4-L — Voix et soundset de créature

`ORF-CRE-029` est `functional` : langue, banques, émotions, lipsync, variantes, licence et
provenance des droits sont édités via `SetCreaturePresentationHandler`, avec suppression optionnelle
et doublons refusés. Le portail passe 6/6 tests en 120,40 s, le smoke global et une capture
Forward+ FHD inspectée. La preuve est explicitement un fallback canonique isolé et ne revendique
aucune écoute, TTS ou animation live. La matrice atteint 261 routes `functional` et 456 `planned`.
Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX4_L.md` ; `ORF-AUD-016` reste une
transaction Area Experience distincte, tandis que `ORF-CRE-030` et `ORF-CRE-033` à
`ORF-CRE-049` restaient `planned` à ce jalon.

### UX4-M — Rôle, activité et auto-level

`ORF-CRE-030`, `ORF-CRE-047` et `ORF-CRE-048` sont `functional` via une surface unique sur
`CreatureRoleBehaviorProfile` : lore descriptif sans mécanique implicite, graphe ambiant complet et
template de progression déterministe. Le portail passe 10/10 tests en 124,60 s, le smoke global,
une capture fallback et trois captures Forward+ FHD sur le profil réel de la Crypte. Le résumé et
les cinq actions restent visibles dans les trois onglets. La matrice atteint 264 routes `functional`
et 453 `planned`. Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX4_M.md` ;
`ORF-CRE-046` reste propriété de l’agrégat IA/événements, et `ORF-CRE-033` à `ORF-CRE-046` ainsi
que `ORF-CRE-049` restent `planned`.

### UX4-N — Événements avancés de créature

`ORF-CRE-039`, `ORF-CRE-041`, `ORF-CRE-042`, `ORF-CRE-044` et `ORF-CRE-045` sont
`functional` via `CreatureEventProfile` et `SetCreatureEventsHandler` : frontière de combat,
inventaire perturbé, repos terminé, heartbeat budgété et payloads utilisateur typés. Le portail
passe 12/12 tests en 113,33 s, le smoke global, une capture fallback et une capture Forward+ FHD
sur le vrai profil Crypte. Le tail dégâts/mort est protégé et préservé. La matrice atteint
269 routes `functional` et 448 `planned`. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX4_N.md` ; les routes dégâts/mort, IA/événements, spawn et conversation
restent aux lots UX4 suivants.

### UX4-O — Dégâts, mort et validation créature

`ORF-CRE-036`, `ORF-CRE-043` et `ORF-CRE-049` sont `functional` via le tail
`CreatureEventProfile.DamageDeath`, `SetCreatureEventsHandler` et la validation projet ciblée :
BeforeDamage budgété, DamageApplied post-transaction, payload EntityDied et diagnostics complets de
la créature. Le portail passe 22/22 tests en 124,08 s, le smoke global et trois captures Forward+ FHD,
dont le vrai profil Crypte. Les bindings UX4-N sont préservés. La matrice atteint 272 routes
`functional` et 445 `planned`. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX4_O.md` ; les routes IA/événements, spawn et conversation restent aux lots
UX4 suivants.

### UX4-P — Événements et intelligence de créature

`ORF-CRE-035`, `ORF-CRE-037`, `ORF-CRE-038`, `ORF-CRE-040` et `ORF-CRE-046` sont `functional`
via `CreatureFoundationProfile.EventAiComposition` et `SetCreatureFoundationHandler` : perception,
attaque physique, capacité ciblée, mouvement bloqué avec récupération et contrôleur IA par profil.
Le portail passe 9/9 tests en 117,33 s, le smoke global et trois captures Forward+ FHD, dont le vrai
profil Crypte. Les profils UX4-D à UX4-O et les autres blueprints sont préservés. La matrice atteint
277 routes `functional` et 440 `planned`. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX4_P.md` ; OnSpawn est clos par UX4-D et OnConversation par UX4-J.

### UX4-Q — Voix localisée de créature

`ORF-AUD-016` est `functional` via `AreaExperienceProfile.Voices`,
`SetAreaExperienceProfileHandler` et `AreaExperienceRuntimeService.ResolveVoice`. La surface édite
les prises localisées complètes, sous-titres, phonèmes, gouvernance média, fallback TTS et
consentement explicite, sans charger de média ni exécuter de synthèse vocale. Le portail passe
14/14 tests en 116,00 s, le smoke global et la capture Forward+ FHD. Les profils UX4-D à UX4-P,
les autres politiques de zone et les racines projet sont préservés. La matrice atteint 280 routes
`functional` et 437 `planned`. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX4_Q.md` ; l’assistant de boss, les objets, marchands et compagnons restent
aux lots UX4 suivants.

### UX4-R — Création assistée de boss

`ORF-AI-019` est `functional` via `AssistedBossCreationService`, `ApplyAssistedBossHandler` et une
approbation auteur liée à la révision et aux empreintes du projet/brouillon. La surface compose le
lot complet blueprint, combat, rencontre, phases, dialogue, récompenses et document runtime, puis
simule 32 exécutions déterministes sans provider réseau ni exécution live. Le portail passe 6/6
tests en 121,46 s, deux smokes globaux avec sortie native 0 et la capture Forward+ FHD. La matrice
atteint 281 routes `functional` et 436 `planned`. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX4_R.md` ; les objets, marchands et compagnons restent aux lots UX4
suivants.

### UX4-S — Auteur d’objet complet

Les 32 routes `ORF-ITEM-001..032` sont `functional` via une surface unique en cinq onglets et le
nouveau `SetItemAuthoringBundleHandler`, qui regroupe identité, fondation, création, gameplay,
opérations et profil avancé dans une seule révision annulable. Le portail passe 42/42 tests en
140,56 s, le smoke global et la capture Forward+ FHD. La matrice atteint 313 routes `functional`
et 404 `planned`. Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX4_S.md` ; aucun
asset, script, projectile, inventaire ou commerce live n’est exécuté. Les marchands et compagnons
restent aux deux derniers lots UX4.

### UX4-T — Auteur de marchand complet

Les 19 routes `ORF-MER-001..019` sont `functional` via une surface Marchands en cinq onglets et
`SetWorldFeatureCatalogHandler`. Le parcours couvre boutique, vendeurs, stock, prix, trésorerie,
restrictions, restock, événements, transactions, monnaies, négociation, marché, services,
interface et audit avec sélection explicite, dry-run, double garde, historique et persistance. Le
portail passe 27/27 tests en 150,23 s, le smoke global et la capture Forward+ FHD. La matrice
atteint 332 routes `functional` et 385 `planned`. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX4_T.md` ; aucun commerce, script, lecteur UI ou transport réseau live
n’est exécuté. Les compagnons constituent le dernier lot UX4.

### UX4-U — Auteur de compagnon complet

Les 24 routes `ORF-CMP-001..024` sont `functional` via une surface Compagnons en cinq onglets et
`SetWorldFeatureCatalogHandler`. Le parcours couvre recrutement/dialogues, groupe, propriété,
ordres, IA/formation, équipement/progression, relation/banter/quête, mort/cohésion,
pièges/capacités/contrôle direct, HUD/séparation et validation avec sélection explicite, dry-run,
double garde, historique et persistance. Le portail passe 66/66 tests en 132,09 s, le smoke global
et la capture Forward+ FHD. La matrice atteint 356 routes `functional` et 361 `planned` ; UX4 est
close à 140/140. Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX4_U.md` ; aucun
acteur, dialogue, quête, script, inventaire, caméra ou transport réseau live n’est exécuté.

### UX5-B à UX5-F — Production narrative complète

Les 64 routes restant après UX5-A sont `functional` dans un workbench à cinq onglets. Dialogue
utilise `SetDialogueHandler`, Quête `SetQuestHandler`, Localisation le catalogue projet puis
l’historique workspace de typographie, Lore `SetLoreIndexHandler`, et Cinématiques
`SetCinematicProfilesHandler`. Chaque agrégat conserve donc son dry-run, son approbation et son
historique ; aucune atomicité inter-agrégats n’est simulée. Le portail couvre 186/186 tests ciblés,
le smoke global et cinq captures Forward+ FHD, avec jalon QHD/4K. La matrice atteint 420 routes
`functional` et 297 `planned` ; UX5 est close à 69/69. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX5_COMPLETE.md` ; aucun dialogue, son, caméra, acteur, script, modèle IA ou
transport réseau live n’est exécuté par ces aperçus.

### UX6-A — Règles et temps du monde

Les routes `ORF-PROJ-023`, `ORF-GAME-017`, `ORF-GAME-023` et `ORF-GAME-024` sont `functional`
dans un workbench à quatre onglets. Difficultés, mutateurs et packages de règles passent par
`SetRulesProfilesHandler`, tandis que calendrier, horloge, échelles locales, pauses et horaires
passent séparément par `SetGameplayControlProfileHandler`. Appliquer un bloc invalide donc
l’approbation de l’autre au lieu de simuler une atomicité inter-agrégats. Le portail incrémental
passe 21/21 tests en 163,60 s, le smoke global et une capture Forward+ FHD inspectée. La matrice
atteint 424 routes `functional` et 293 `planned` ; UX6 reste ouverte à 4/84. Preuves et limites sont
liées dans `docs/acceptance/STUDIO_UX6_A.md` ; aucun package natif, événement, ForgeScript, nœud
Godot ou transport réseau n’est exécuté par les aperçus.

### UX6-B — Fondations du gameplay

Les routes `ORF-GAME-001`, `ORF-GAME-010`, `ORF-GAME-012` et `ORF-GAME-013` sont `functional`
dans un workbench à quatre onglets. Déplacement, verbes d’interaction, catalogues de caméras,
actions et liaisons sont des collections directement éditables ; une seule commande
`SetGameplayControlProfileHandler` remplace la fondation et conserve `Foundation.Save` ainsi que
tous les profils voisins. Les aperçus appellent `Step`, `ResolveFocus`, `ResolveCamera` et
`ResolveInput` sur des états copiés. Le portail incrémental passe 8/8 tests en 134,00 s, le smoke
global et une capture Forward+ FHD corrigée puis inspectée. La matrice atteint 428 routes
`functional` et 289 `planned` ; UX6 reste ouverte à 8/84. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX6_B.md` ; aucun acteur, `Camera3D`, `InputMap`, paquet, ForgeScript ou
réseau live n’est exécuté par les aperçus.

### UX6-C — Capacités gameplay

Les routes `ORF-GAME-003`, `ORF-GAME-004`, `ORF-GAME-011`, `ORF-GAME-015` et `ORF-GAME-016`
sont `functional` dans un workbench à cinq onglets. Les profils complets de nage, escalade, menu
radial, temps tactique et fournisseurs de combat passent ensemble par
`SetGameplayControlProfileHandler`, avec identités internes stables, double garde et préservation
par référence de `Foundation`, `Simulation` et `Session`. Les aperçus appellent les résolveurs de
mouvement, radial et temps sur des états copiés et lisent le fournisseur de combat par défaut.
Le portail incrémental final passe 7/7 tests en 133,93 s, le smoke global et une capture Forward+
FHD corrigée puis inspectée. La matrice atteint 433 routes `functional` et 284 `planned` ; UX6
reste ouverte à 13/84. Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX6_C.md` ; aucun
acteur, volume, roue native, horloge, combat, ForgeScript ou réseau live n’est exécuté.

### UX6-D — Simulation gameplay

Les routes `ORF-GAME-002`, `ORF-GAME-008`, `ORF-GAME-009` et `ORF-GAME-014` sont `functional`
dans un workbench à quatre onglets. Vol libre, projectiles, corps/liaisons physiques et ciblage
passent ensemble par `SetGameplayControlProfileHandler`, avec identités internes stables, double
garde et préservation de `WorldTime`, `Foundation`, des capacités UX6-C et de `Session`. Les aperçus
appellent `FlyStep`, `StepProjectile`, `SimulatePhysics` et `PreviewTargeting` sur des états copiés.
Le portail incrémental passe 11/11 tests en 126,38 s, le smoke global et une capture Forward+ FHD
inspectée. La matrice atteint 437 routes `functional` et 280 `planned` ; UX6 reste ouverte à 17/84.
Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX6_D.md` ; aucun acteur, physique Godot,
projectile natif, caméra, pathfinding, ForgeScript ou réseau live n’est exécuté. Le jalon A–D a
ensuite repassé le portail transversal complet avec 2 035 tests .NET et 39 tests Python.

### UX6-E — Production gameplay

Les routes `ORF-GAME-005`, `ORF-GAME-006`, `ORF-GAME-007` et `ORF-GAME-028` sont `functional`
dans un workbench à quatre onglets. Apesanteur, véhicules, montures et succès passent ensemble par
`SetRuntimeExtensionsHandler`, avec identités internes stables, double garde et préservation du
reste de `PostLaunch`, des packs gouvernés et des outils opérationnels. Les aperçus appellent
`ResolveMobility` et `EvaluateAchievement` sur des demandes copiées. Le portail incrémental passe
13/13 tests en 136,22 s, le smoke global et une capture Forward+ FHD inspectée. La matrice atteint 441 routes
`functional` et 276 `planned` ; UX6 reste ouverte à 21/84. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX6_E.md` ; aucune gravité, véhicule, monture, plateforme, télémétrie,
physique, ForgeScript ou réseau live n’est exécuté.

### UX6-F — Fondation ForgeScript

Les routes `ORF-SCR-001`, `ORF-SCR-002`, `ORF-SCR-003`, `ORF-SCR-006` et `ORF-SCR-007` sont
`functional` dans un atelier unique : sélection explicite, sources `.as` gouvernées, anciens
`.forge` protégés, éditeur C-like, bindings typés, `main`, compilation/cache et sandbox. Les
mutations passent par `ForgeScriptSourceHistory`, puis `ForgeScriptSourceStore` écrit atomiquement
la source et sa métadonnée ; le projet et son historique restent inchangés. Le portail incrémental
passe 34/34 tests en 284,26 s avec le graphe, le smoke global et une capture Forward+ FHD corrigée puis inspectée.
La matrice atteint 446 routes `functional` et 271 `planned` ; UX6 reste ouverte à 26/84. Preuves et
limites sont liées dans `docs/acceptance/STUDIO_UX6_F.md` ; seuls le contrat existant
`creature.damaged` et la sandbox native bornée sont qualifiés, sans migration `.forge`, hot reload,
débogage, objet gameplay, événement moteur ou réseau live.

### UX6-G — Éditeur, Wizard et APIs gameplay ForgeScript

Les routes `ORF-SCR-004`, `ORF-SCR-005`, `ORF-SCR-008`, `ORF-SCR-010` et `ORF-SCR-011` sont
`functional` dans une surface à trois colonnes : analyse/complétions/diagnostics, source canonique
et Wizard avec simulations gameplay. La page réutilise `ForgeScriptSourceHistory`, le dry-run, la
compilation, la sandbox et l’écriture atomique UX6-F ; elle n’introduit aucune autorité parallèle.
Les aperçus appellent les services canoniques sur des états copiés et refusent l’autorité cliente.
Le portail incrémental passe 25/25 tests en 251,59 s avec le graphe, le smoke global et une capture
Forward+ FHD corrigée puis inspectée. La matrice atteint 451 routes `functional` et 266 `planned` ;
UX6 reste ouverte à 31/84. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX6_G.md` ; aucun acteur, objet Godot, fichier du module, événement moteur
ou transport réseau n’est muté par les simulations. Le jalon UX6-E/F/G repasse ensuite le portail
transversal complet avec 2 035 tests .NET, 39 tests Python et tous les smokes de livraison.

### UX6-H — APIs monde ForgeScript

Les routes `ORF-SCR-012` à `ORF-SCR-016` sont `functional` dans une surface à deux colonnes et
cinq onglets : inventaire, journal/quêtes, dialogues, factions/réputation et perception. La page
réutilise strictement la source, l’historique, le dry-run, la compilation, la sandbox et l’écriture
atomique UX6-F ; son bloc versionné doit être construit, relu puis inséré explicitement. Les
catalogues proviennent du projet ouvert, et l’absence de faction dans Forgotten Forest reste un
état vide honnête ; seule la copie temporaire de preuve reçoit une faction canonique. Le portail
incrémental passe 13/13 tests en 166,56 s, le smoke global et une capture Forward+ FHD corrigée puis
inspectée. La matrice atteint 456 routes `functional` et 261 `planned` ; UX6 reste ouverte à 36/84.
Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX6_H.md` ; les appels natifs sont
sandboxés, mais aucun inventaire, quête, dialogue, réputation, perception moteur ou réseau live
n’est muté par l’aperçu.

### UX6-I — Monde, mouvement, utilitaires et sandbox ForgeScript

Les routes `ORF-SCR-009`, `ORF-SCR-017`, `ORF-SCR-021`, `ORF-SCR-022` et `ORF-SCR-027` sont
`functional` dans une surface à deux colonnes et cinq onglets humains. La page réutilise strictement
la source, l’historique, le dry-run, la compilation, la sandbox et l’écriture atomique UX6-F ; le
bloc R013 versionné doit être construit, relu puis inséré explicitement. Zones, tags et blueprints
proviennent de Forgotten Forest réel, tandis que toutes les intentions monde et mouvement sont
appliquées à une copie en mémoire. Le portail incrémental passe 18/18 tests en 197,50 s, le smoke
global et une capture Forward+ FHD inspectée. La matrice atteint 461 routes `functional` et 256
`planned` ; UX6 reste ouverte à 41/84. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX6_I.md` ; aucun acteur, pathfinding, physique, sauvegarde, fichier,
processus ou transport réseau live n’est invoqué.

### UX6-J — Sécurité, quotas, déterminisme et API ForgeScript

Les routes `ORF-SCR-028`, `ORF-SCR-029`, `ORF-SCR-030`, `ORF-SCR-031` et `ORF-SCR-036` sont
`functional` dans une surface à deux colonnes et cinq onglets : handles, instructions, mémoire,
déterminisme et API. La source UX6-F reste revue et compilée sans mutation ; une politique R014
séparée passe par son propre historique, son dry-run, son empreinte et son store atomique. Les
catalogues de zone, objet et ressource proviennent de Forgotten Forest et restent affichés par
libellés humains. Le portail final passe 27/27 tests en 156,85 s, le smoke global et une capture
Forward+ FHD corrigée puis inspectée. La matrice atteint 466 routes `functional` et 251 `planned` ;
UX6 reste ouverte à 46/84. Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX6_J.md` ;
la sandbox native prouve les quotas et le replay, mais aucun acteur, serveur de partie, fichier
arbitraire, processus, réseau ou budget matériel live n’est invoqué.

### UX6-K — Permissions ForgeScript par module

`ORF-SCR-039` est `functional` dans une surface à trois onglets : Module, Kits et Liaisons.
L’allowlist explicite expose les six familles ForgeScript par libellé humain ; chaque kit est un
sous-ensemble du module et chaque liaison un sous-ensemble du kit. Le manifeste R015 séparé passe
par son propre historique, son dry-run, son empreinte et son store atomique, tandis que la source
UX6-F reste revue sans mutation. Le portail final passe 14/14 tests en 159,24 s, le smoke global et
les captures rendues des trois onglets. La matrice atteint 467 routes `functional` et 250 `planned` ;
UX6 reste ouverte à 47/84. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX6_K.md` ; la sandbox prouve autorisation, refus, quota et récupération,
mais Studio ne signe aucun package et n’exécute aucun acteur, session réseau ou runtime de jeu live.

### UX6-L — Outils runtime ForgeScript

`ORF-SCR-019`, `ORF-SCR-020` et `ORF-SCR-023` sont `functional` dans une surface à trois onglets :
Audio & musique, Horloge & timers et Événements custom. Les formulaires produisent un bloc R099
versionné à relire, qui reste dans la source et l’historique UX6-F. L’aperçu exerce les états C#
canoniques, puis compile et exécute réellement les intentions natives audio, temps et événement dans
la sandbox. Le portail final passe 15/15 tests en 156,29 s, le smoke global et trois captures Forward+ FHD
inspectées. La matrice atteint 470 routes `functional` et 247 `planned` ; UX6 reste ouverte à 50/84.
Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX6_L.md` ; aucun lecteur audio, temps
système, événement de session, acteur Godot ou transport réseau n’est exécuté par Studio.

### UX6-M — Automatisation ForgeScript

`ORF-SCR-018`, `ORF-SCR-024`, `ORF-SCR-025` et `ORF-SCR-026` sont `functional` dans une surface à
quatre onglets : Caméra & cutscenes, Routage par tags, Switches module et Switches IA. Les collections
ordonnées reconstruisent uniquement `Module.WorldFeatures.Scripts`, puis passent validation, aperçu
sec, double garde révision/empreintes, commande canonique, undo/redo et sauvegarde/réouverture. Le
resolver pur exerce plan, verrou, récupération, skip accessible, route tag/événement, valeur de flag
et fallback IA. Le portail passe 60/60 tests en 162,07 s, le smoke global et quatre captures Forward+
FHD inspectées. La matrice atteint 474 routes `functional` et 243 `planned` ; UX6 reste ouverte à
54/84. Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX6_M.md` ; aucun `Camera3D`, acteur,
comportement IA, script, moteur ou transport réseau n’est exécuté par Studio.

### UX6-N — Hot reload et débogueur ForgeScript

`ORF-SCR-032` et `ORF-SCR-033` sont `functional` dans les deux nouveaux onglets Hot reload et
Débogueur de l’unique surface R099. Le bloc versionné reste dans la source et l’historique UX6-F ;
l’aperçu exerce migration, invalidation des dépendances, rollback, reprise, breakpoint, pile,
watch, inspection et pas de débogage sur un état copié, puis compile et exécute les intentions
natives correspondantes dans la sandbox. Le portail passe 28/28 tests en 131,98 s, le smoke global
et deux captures Forward+ FHD inspectées. La matrice atteint 476 routes `functional` et 241
`planned` ; UX6 reste ouverte à 56/84. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX6_N.md` ; aucun module chargé, processus distant, acteur Godot, moteur ou
transport réseau n’est contrôlé par Studio.

### UX6-O — Qualité ForgeScript

`ORF-SCR-034`, `ORF-SCR-035`, `ORF-SCR-037`, `ORF-SCR-038` et `ORF-SCR-040` sont `functional`
dans les cinq onglets Tests, Profilage, Documentation, Analyse et État. Le profil R100 versionné
reste distinct de la source UX6-F tout en partageant sa sélection canonique ; aperçu sec,
simulation, empreintes source/profil, approbation, apply, undo/redo et sauvegarde/réouverture sont
transactionnels. Le portail passe 16/16 tests en 135,51 s, le smoke global et cinq captures
Forward+ FHD inspectées. La matrice atteint 481 routes `functional` et 236 `planned` ; UX6 reste
ouverte à 61/84. Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX6_O.md` ; tests,
mesures et état portent sur une sandbox copiée, la documentation est générée en mémoire et aucun
monde, acteur, fichier produit, processus distant ou transport réseau live n’est piloté par Studio.

### UX6-P — Interop éditeur/MCP ForgeScript

`ORF-SCR-041` est `functional` dans une surface à trois vues Profil R101, Liaison et Transactions.
La source reste l’autorité UX6-F ; le profil interop est persistant et les plans source/binding sont
exercés par le service MCP isolé avec compilation, diagnostics, tests, apply/revert/redo, snapshot,
quotas/récupération et adaptateur runtime. Le portail passe 10/10 tests en 135,87 s, le smoke global
et trois captures Forward+ FHD inspectées. La matrice atteint 482 routes `functional` et 235
`planned` ; UX6 reste ouverte à 62/84. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX6_P.md` ; aucun acteur, bus live, objet moteur, fichier arbitraire,
processus ou transport réseau n’est piloté par Studio.

### UX6-Q — Compilateur moderne et création ForgeScript assistée

`ORF-EE-031` et `ORF-AI-018` sont `functional` dans une surface à deux onglets séparant le profil
compilateur du workflow de source assistée. Le premier passe par `SetRuntimeExtensionsHandler`,
compile réellement une copie des sources autonomes et prouve cache, réutilisation, invalidation,
diagnostics stables, télémétrie et conformité. Le second produit localement une proposition
déterministe, la compile et la teste, puis écrit atomiquement le `.as` et son `.meta.json` seulement
après dry-run, approbation et permission, avec undo/redo exacts. Le portail passe 16/16 tests en
137,50 s, le smoke global et deux captures Forward+ FHD inspectées. La matrice atteint 484 routes
`functional` et 233 `planned` ; UX6 reste ouverte à 64/84. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX6_Q.md` ; aucun fournisseur distant, modèle d’IA, acteur, bus runtime,
objet Godot, distribution de bytecode ou transport réseau n’est piloté par Studio.

### UX6-R — Données et UI runtime

`ORF-EE-011`, `ORF-EE-012`, `ORF-EE-013`, `ORF-EE-015` et `ORF-EE-016` sont `functional`
dans quatre onglets Règles, Interface/événements, JSON et État sérialisable. Les collections
complètes restent dans `ModuleRuntimeExtensionProfile` et une unique commande
`SetRuntimeExtensionsHandler` porte aperçu sec, approbation, apply, undo/redo et persistance.
Les changements de sélection sauvegardent le document courant avant d’en charger un autre ; le
smoke crée deux règles, deux interfaces et deux documents JSON et rend cette conservation
bloquante. Le portail passe 24/24 tests en 140,90 s, le smoke global et quatre captures Forward+
FHD inspectées. La matrice atteint 489 routes `functional` et 228 `planned` ; UX6 reste ouverte à
69/84. Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX6_R.md` ; les services de
résolution, le bus UI et le store JSON sont exercés en mémoire, mais aucun nœud Godot, acteur,
sauvegarde produit, bus runtime live, processus ou transport réseau n’est piloté par Studio.

### UX6-S — Politiques Creator techniques

`ORF-EE-014`, `ORF-EE-017`, `ORF-EE-020` et `ORF-EE-021` sont `functional` dans quatre onglets
Périphériques, Ressources VM, Contrôle de groupe et Progression. Les quatre profils conservent leurs
identités et reconstruisent uniquement `CreatorExperience` avant un dry-run et une application par
`SetRuntimeExtensionsHandler`. Les résolveurs PostLaunch exercent filtrage, quotas, capacité du
groupe et parcours data-driven sur des entrées de simulation séparées. Le portail passe 19/19 tests
en 140,55 s, le smoke global et quatre captures Forward+ FHD inspectées. La matrice atteint 493
routes `functional` et 224 `planned` ; UX6 reste ouverte à 73/84. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX6_S.md` ; aucun périphérique, ressource, acteur, ordre IA, classe, niveau,
écran runtime ou transport réseau n’est exécuté par Studio.

### UX6-T — Indicateur AOE et NUI 2.0

`ORF-EE-022` et `ORF-EE-024` sont `functional` dans deux onglets Zone d’effet et NUI 2.0. Le
premier conserve l’identité du profil et édite formes, portée, hauteur, couverture, confirmation et
palettes avant une résolution réelle par `AreaEffectPreviewService` sur des candidats temporaires.
Le second déclare les sept capacités du toolkit 2.0 en laissant les documents, widgets et bindings
strictement inchangés. Les deux passent par `SetRuntimeExtensionsHandler`, double garde, undo/redo
et round-trip. Le portail passe 15/15 tests en 147,77 s, le smoke global et deux captures Forward+
FHD inspectées. La matrice atteint 495 routes `functional` et 222 `planned` ; UX6 reste ouverte à
75/84. Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX6_T.md` ; aucun mesh Godot,
ciblage serveur, document NUI, événement GUI ou transport réseau n’est exécuté par Studio.

### UX6-U — Variables et blackboard

`ORF-GAME-022` est `functional` dans une surface à deux onglets Définition gouvernée et Blackboard
en mémoire. La première remplace seulement valeur par défaut, réplication, visibilité debug et
exigence runtime de la variable sélectionnée via `SetModuleConfigurationHandler`; l’éditeur complet
de création, clé, type et portée reste l’autorité UX0-I. La seconde exerce réellement
`ProjectBlackboardScriptService` sur une copie : handle typé, mutation normalisée, vue debug,
delta par audience, snapshot/reload par portée, refus d’un handle périmé et frontière legacy.
Le portail passe 56/56 tests en 144,09 s, le smoke global et une capture Forward+ FHD inspectée.
La matrice atteint 496 routes `functional` et 221 `planned` ; UX6 reste ouverte à 76/84. Preuves et
limites sont liées dans `docs/acceptance/STUDIO_UX6_U.md` ; aucun serveur, client, `GameRoot`,
ForgeScript, fichier de sauvegarde produit ou transport réseau n’est exécuté par Studio.

### UX6-V — Pathfinding amélioré

`ORF-EE-028` est `functional` dans une surface à trois onglets Surface & agent, Laboratoire A* et
Adaptation moteur. La première reconstruit uniquement le profil sélectionné et le pas de cellule
avant `SetNavigationTopologyHandler`. La seconde exécute `NavigationPathfinder` sur une surface
copiée avec porte, obstacle, coût, récupération et budget. La troisième expose les plans de mise à
jour dynamique, rebake et `SurfaceNavigationPlanBuilder` sans les présenter comme un mouvement
live. Le portail passe 10/10 tests en 144,21 s, le smoke global et une capture Forward+ FHD
inspectée. La matrice atteint 497 routes `functional` et 220 `planned` ; UX6 reste ouverte à 77/84.
Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX6_V.md` ; aucun `NavigationAgent3D`,
acteur, évitement local, déplacement, serveur ou transport réseau n’est exécuté par Studio.

### UX6-W — Transformations runtime

`ORF-EE-001` et `ORF-EE-027` sont `functional` dans une surface à trois onglets Overrides,
Tweens et Aperçu runtime copié. Les collections canoniques du profil cinématique conservent leurs
identités, parents, transforms, canaux, durées, easing, autorité, sauvegarde et réplication. Une
unique commande `SetCinematicProfilesHandler` porte dry-run, approbation, apply, undo/redo et
round-trip, tandis que `TransformOverrideRuntime` et `AnimationTweenRuntime` exercent hiérarchie,
transform, matériau, état et annulation sur une copie. Le portail final passe 10/10 tests en
146,60 s, le smoke global et trois captures Forward+ FHD inspectées après correction responsive.
La matrice atteint 499 routes `functional` et 218 `planned` ; UX6 reste ouverte à 79/84. Preuves
et limites sont liées dans `docs/acceptance/STUDIO_UX6_W.md` ; aucun nœud Godot, matériau, état
d’acteur, scène, serveur ou transport réseau live n’est modifié par Studio.

### UX6-X — HUD runtime et typographie

`ORF-GAME-026` et `ORF-EE-026` sont `functional` dans deux onglets Carte, journal et HUD puis
Police et échelles. Le shell conserve l’autorité `ModuleGameplayControlProfile.Session.Shell` et
passe par `SetGameplayControlProfileHandler`; les échelles et la chaîne typographique conservent
l’autorité `EditorWorkspaceProfile` et son historique distinct. L’application effectue donc deux
transactions explicites avec rollback de la première si la seconde échoue. Les résolveurs opèrent
sur des copies et l’échantillon Godot reste local. Le portail final repasse 22/22 tests en 162,88 s, le smoke
global et deux captures Forward+ FHD inspectées après correction de la représentation numérique.
La matrice atteint 501 routes `functional` et 216 `planned` ; UX6 reste ouverte à 81/84. Preuves
et limites sont liées dans `docs/acceptance/STUDIO_UX6_X.md` ; aucun HUD, thème global,
périphérique, viewport, nœud runtime ou session de jeu live n’est modifié par Studio.

### UX6-Y — Météo runtime et streaming de zone

`ORF-GAME-018` et `ORF-EE-023` sont `functional` dans deux onglets Météo runtime et Streaming de
zone. Le premier reconstruit uniquement la politique du profil météo avant `SetAreaEnvironment` et
exécute machine déterministe, autorité et événement typé sur copie. Le second reconstruit uniquement
`Area.World.Loading` avant `SetAreaWorldProfile` et exerce dépendances, progression, cache,
annulation et benchmark sur un chargeur mémoire déterministe. Les deux commandes sont explicites,
annulables et protégées par révision et empreintes. Le portail final passe 33/33 tests en 163,69 s,
le smoke global et deux captures Forward+ FHD inspectées après correction du libellé d’isolation.
La matrice atteint 503 routes `functional` et 214 `planned` ; UX6 reste ouverte à 83/84. Preuves
et limites sont liées dans `docs/acceptance/STUDIO_UX6_Y.md` ; aucun réseau, ForgeScript, chunk,
scène Godot ou chargement moteur live n’est exécuté par Studio.

### UX6-Z — Création assistée de capacité

`ORF-AI-020` est `functional` dans une surface à trois onglets Design de combat, Dépendances &
gouvernance et Tests & équilibrage. Le porteur est explicite ; effets ordonnés, script réel, objet
optionnel, tags IA, intention d'équilibrage et gouvernance alimentent le fournisseur local
déterministe. La proposition traverse cinq scénarios `CombatResolver`, l'équilibrage et le document
runtime stable avant approbation, dry-run puis `ApplyAssistedAbilityHandler`. Le portail passe 6/6
tests en 146,31 s, le smoke global et une capture Forward+ FHD inspectée. La matrice atteint 504
routes `functional` et 213 `planned` ; UX6 est close à 84/84. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX6_Z.md` ; aucun fournisseur distant, ForgeScript, objet, VFX, audio,
combat moteur, acteur Godot, serveur ou transport réseau live n’est exécuté par Studio.

### UX3-A — Fondation des blueprints et palettes

`ORF-PAL-001`, `ORF-PAL-002`, `ORF-PAL-003`, `ORF-PAL-006` et `ORF-PAL-007` sont
`functional` dans une surface à trois onglets Définition réutilisable, Instance et dérivation,
puis Palettes Standard et Projet. L'édition Projet passe par `UpsertBlueprintHandler` ; la
dérivation retargete uniquement l'instance sélectionnée via `CreateBlueprintFromEntityHandler`.
La palette Standard reste protégée en lecture seule et les deux inventaires réutilisent l'unique
catalogue canonique. Le portail final passe 22/22 tests en 144,41 s, le smoke global, trois captures
Forward+ FHD et le jalon QHD/4K inspecté. La matrice atteint 509 routes `functional` et 208
`planned` ; UX3 reste ouverte à 5/34. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX3_A.md` ; aucune instance, ressource, scène, asset, script, moteur ou
transport réseau live n'est exécuté par Studio.

### UX3-B — Propagation, migration et prefabs

`ORF-PAL-004`, `ORF-PAL-005`, `ORF-PAL-008`, `ORF-PAL-013` et `ORF-PAL-014` sont
`functional` dans une bibliothèque avancée à deux transactions indépendantes : propagation et
migration sélectives d'une instance réelle, puis variantes et compositions. Les catégories sont
hiérarchiques, les transforms de nœuds sont complets, les références restent humaines et chaque
apply refait son dry-run sous garde de révision et d'empreintes. Une exclusion de migration élargit
sa contrainte à la version précédente plutôt que de conserver une définition cachée
impossible à sérialiser. Le portail passe 17/17 tests en 146,04 s, le smoke global et deux captures
Forward+ FHD inspectées. La matrice atteint 514 routes `functional` et 203 `planned` ; UX3 reste
ouverte à 10/34. Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX3_B.md` ; aucun asset,
prefab, scène, script, moteur ou transport réseau live n'est chargé ou exécuté par Studio.

### UX3-C — Découverte, miniatures et dépôt

`ORF-PAL-009`, `ORF-PAL-010`, `ORF-PAL-011` et `ORF-PAL-012` sont `functional` dans une
surface de découverte qui sépare préférences personnelles et placement projet. Favoris/récents,
recherche combinée et miniature locale gouvernée restent liés au catalogue canonique ; le vrai
payload Godot est validé par `BlueprintDropPlanner` avant le dry-run et l'application via
`PlaceRepeatedEntities`. Les deux transactions gardent leurs approbations, historiques et
persistances indépendants. Le portail passe 23/23 tests en 153,00 s, le smoke global et une capture
Forward+ FHD inspectée. La matrice atteint 518 routes `functional` et 199 `planned` ; UX3 reste
ouverte à 14/34. Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX3_C.md` ; aucun asset,
prefab, scène, script, acteur, moteur ou transport réseau live n'est chargé ou exécuté par Studio.

### UX3-D — Dépendances, provenance et licence

`ORF-PAL-015` et `ORF-PAL-016` sont `functional` dans une surface de gouvernance qui sépare le
manifeste de dépendances de la provenance distribuable. `WorkspaceBuildGraphBuilder` ferme les
ressources transitives et refuse avant packaging toute dépendance absente d'un blueprint utilisé ;
`BlueprintDropPlanner` et `DistributionComplianceValidator` vérifient source, auteur, SPDX,
permission et SHA-256. Les deux transactions gardent leurs approbations et historiques distincts.
Le portail passe 45/45 tests en 191,90 s, le smoke global et deux captures Forward+ FHD inspectées.
La matrice atteint 520 routes `functional` et 197 `planned` ; UX3 reste ouverte à 16/34. Preuves et
limites sont liées dans `docs/acceptance/STUDIO_UX3_D.md` ; aucun asset, prefab, package, script,
acteur, moteur ou transport réseau live n'est chargé, créé ou exécuté par Studio.

### UX3-E — Compatibilité artistique et budgets d’asset

`ORF-PAL-017` et `ORF-PAL-018` sont `functional` dans une surface à deux transactions. La première
mesure les bornes `POSITION` d’un GLB et la résolution d’un PNG, puis localise les six écarts
artistiques canoniques. La seconde gouverne triangles, matériaux, textures, mémoire, animations,
LOD et collisions par plateforme, avec avertissement ou refus pré-build explicite. Les deux
approbations sont indépendantes et protégées par révision et empreintes. Le portail passe 11/11
tests en 156,86 s, le smoke global et deux captures Forward+ FHD inspectées. La matrice atteint 522
routes `functional` et 195 `planned` ; UX3 reste ouverte à 18/34. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX3_E.md` ; aucun asset, matériau, GPU, package, script, acteur, moteur ou
transport réseau live n'est instancié, mesuré, créé ou exécuté par Studio.

### UX3-F — Navigateur, import et aller-retour Blender

`ORF-CONT-013`, `ORF-CONT-014`, `ORF-CONT-020`, `ORF-CONT-021` et `ORF-CONT-022` sont
`functional` dans une surface qui fusionne ressources module et catalogue, classe réellement les
textures et modèles, puis sépare profil d’import et édition externe. `AssetImportPlanner` produit un
plan sec KTX2/GLB ; la configuration Blender ne contient que des opérations typées allowlistées et
un worker request confiné. Les deux approbations, révisions, empreintes, historiques et stores sont
indépendants. Le portail passe 32/32 tests en 157,96 s, le smoke global et trois captures Forward+
FHD inspectées. La matrice atteint 527 routes `functional` et 190 `planned` ; UX3 reste ouverte à
23/34. Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX3_F.md` ; aucun asset n’est
téléchargé, transformé ou réimporté et aucun processus Blender, package, script, acteur, moteur ou
transport réseau live n’est exécuté par Studio.

### UX3-G — Manifestes d’assets de production

`ORF-CONT-008`, `ORF-CONT-015`, `ORF-CONT-016` et `ORF-CONT-017` sont `functional` dans une
surface qui conserve le manifeste complet : gouvernance, budgets plateforme, chunks ordonnés,
collision/navigation, clips d’animation, VFX et LOD. Les fichiers réels et leurs hashes sont
résolus avant l’approbation ; le plan VFX demeure une décision pure. Révision, empreintes,
dry-run, historique, undo/redo, save/reopen et tails cachés sont préservés. Le portail passe 15/15
tests en 160,44 s, le smoke global et quatre captures Forward+ FHD inspectées. La matrice atteint
531 routes `functional` et 186 `planned` ; UX3 reste ouverte à 27/34. Preuves et limites sont liées
dans `docs/acceptance/STUDIO_UX3_G.md` ; aucun mesh, bake, playback, rendu VFX, LOD, benchmark GPU,
package, script, acteur, moteur ou transport réseau live n’est produit ou exécuté par Studio.

### UX3-H — Données et transferts de contenu

`ORF-CONT-004`, `ORF-CONT-005`, `ORF-CONT-006` et `ORF-CONT-018` sont `functional` dans un
workbench à quatre vues : import borné de fichiers ouverts, export déterministe des blueprints et
de leurs dépendances, catalogue de sources textuelles gouvernées et tables typées. L’import local
et l’export possèdent chacun leur rollback de fichiers ; la configuration projet conserve sa
commande, son dry-run et son historique propres. Révision, empreintes, undo/redo, save/reopen,
voisins et source produit sont préservés. Le portail passe 18/18 tests en 173,10 s, le smoke global
et cinq captures Forward+ FHD inspectées. La matrice atteint 535 routes `functional` et 182
`planned` ; UX3 reste ouverte à 31/34. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX3_H.md` ; l’étiquette ERF historique ne signifie pas que le format
propriétaire est décodé, et aucun package, script, acteur, moteur ou transport réseau live n’est
installé ou exécuté par Studio.

### UX3-I — Provenance, quarantaine et génération média gouvernée

`ORF-CONT-023`, `ORF-CONT-024` et `ORF-AI-024` sont `functional` dans un workbench à deux
historiques indépendants. Le premier choisit un asset réel, vérifie provenance, droits, budget,
hash, format et quarantaine avant une décision humaine ; le scanner produit est fail-closed et la
preuve déterministe reste confinée à l’isolat. Le second édite la politique projet et planifie un
candidat Image/Audio/VFX sans appeler de fournisseur. Révision, empreintes, dry-run, undo/redo,
save/reopen, voisins et source produit sont préservés. Le portail passe 12/12 tests en 171,71 s,
le smoke global et quatre captures Forward+ FHD inspectées. La matrice atteint 538 routes
`functional` et 179 `planned` ; UX3 est close à 34/34. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX3_I.md` ; aucun téléchargement, scan antivirus simulé dans le produit,
génération, import, rendu, playback, publication ou appel distant n’est exécuté par Studio.

### UX7-A — Validation canonique et diagnostics cliquables

`ORF-AREA-037`, `ORF-AUD-020`, `ORF-QA-001` à `ORF-QA-007` et `ORF-QA-021` sont `functional`
dans l'atelier Validation. Dix cartes humaines agrègent les autorités canoniques existantes pour
schéma, références, graphes, géométrie, navigation, localisation, licences, préparation de zone et
audio ; chaque diagnostic conserve son code et son chemin, expose une cible cliquable et peut
prévisualiser puis appliquer uniquement un correctif déjà gouverné. Révision, empreintes,
undo/redo, save/reopen, voisins et source produit sont préservés dans l'isolat. Le portail passe
33/33 tests en 300,73 s, le smoke global et une capture Forward+ FHD inspectée. La matrice atteint
548 routes `functional` et 169 `planned` ; UX7 reste ouverte à 10/43. Preuves et limites sont liées
dans `docs/acceptance/STUDIO_UX7_A.md` ; aucun build, package, runtime gameplay, playback, acteur,
moteur ou transport réseau live n’est exécuté par l'atelier.

### UX7-B — Laboratoire qualité isolé

`ORF-QA-008`, `ORF-QA-010`, `ORF-QA-015` et `ORF-QA-020` sont `functional` dans la page Qualité.
Le profil est choisi explicitement, prévisualisé puis appliqué par sa commande canonique. Le run
copie le workspace, permet une annulation réelle entre familles et expose quatre résultats
navigables : validation Domaine, replay headless, six scénarios de sauvegarde sur fichiers
`.orpsave` et journal JSON confidentiel. Révision, empreintes, licences, validation, undo/redo,
save/reopen, source et ressources temporaires sont préservés. Le portail passe 25/25 tests en
245,20 s, le smoke global et une capture Forward+ FHD corrigée puis inspectée. La matrice atteint
552 routes `functional` et 165 `planned` ; UX7 reste ouverte à 14/43. Preuves et limites sont liées
dans `docs/acceptance/STUDIO_UX7_B.md` ; aucun runner xUnit embarqué, runtime Godot de jeu, acteur,
script, playback, journal joueur ou transport réseau live n'est exécuté par le laboratoire.

### UX7-I — Observabilité Tracy et OpenTelemetry

`ORF-EE-029` est `functional` dans la page Qualité. Le profil canonique autorise explicitement
Tracy et/ou OpenTelemetry, impose une durée bornée et choisit l'inclusion ForgeScript ; son aperçu
et son application passent par `SetRuntimeExtensionsHandler`. Le run copie le workspace, produit
une vraie trace JSON OpenTelemetry confinée couvrant Editor, Runtime, Serveur et ForgeScript,
expose six résultats navigables, prouve l'annulation et supprime l'isolat. Tracy natif n'est jamais
simulé : sans Godot instrumenté et capture server compatible, un diagnostic stable explique le
prérequis. Le portail passe 14/14 tests en 420,22 s, le smoke global et une capture Forward+ FHD
corrigée puis inspectée. La matrice atteint 624 routes `functional` et 93 `planned` ; UX7 est close
à 43/43 et le graphe Roslyn frais compte 11 194 nœuds / 86 220 relations sans diagnostic. Preuves
et limites sont liées dans `docs/acceptance/STUDIO_UX7_I.md` ; aucun build Godot
personnalisé, capture Tracy native, compteur GPU constructeur, réseau, commit ou publication n'est
revendiqué.

### UX8-A — Build réel, déterministe et incrémental

`ORF-SHELL-019`, `ORF-EE-004` et `ORF-BUILD-003` à `ORF-BUILD-005` sont `functional` dans la page
Build. Après sélection explicite, Studio calcule le graphe canonique, copie le workspace dans un
isolat, lance réellement `Forge.Cli build` deux fois, compare les deux `.orpmod` octet par octet et
prouve que le second passage ne reconstruit aucune ressource. L'annulation tue l'arbre du processus
et toute fermeture libère l'isolat ; projet, historique, sélection et Git restent inchangés. Le
portail passe 61/61 tests en 624,28 s, le smoke global, une capture Forward+ FHD inspectée, la
matrice à 585 `functional` / 132 `planned` et le graphe Roslyn à 11 032 nœuds / 84 434 relations
sans diagnostic. Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX8_A.md` ; aucune
signature, publication, installation ni qualification de distribution n'est revendiquée.

### UX8-B — Maintenance collaborative confinée du build

`ORF-BUILD-006` et `ORF-BUILD-008` à `ORF-BUILD-011` sont `functional` dans la page Build. Après
sélection explicite, Studio copie le workspace dans un isolat jetable, inventorie et purge le cache,
normalise les documents, applique les migrations avec sauvegarde et second passage idempotent,
reconstruit les références avant/arrière puis produit un diff sémantique sans UUID visible. Les
opérations canoniques sont annulables entre étapes ; le module source, son historique, sa sélection
et Git restent inchangés. Le portail passe 25/25 tests en 549,47 s, le smoke global et une capture
Forward+ FHD inspectée ; la matrice atteint 590 `functional` / 127 `planned` et le graphe Roslyn
11 043 nœuds / 84 530 relations sans diagnostic. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX8_B.md` ; aucun undo multi-fichiers sur la source, shell arbitraire,
réseau, commit ou publication Git n'est revendiqué.

### UX8-C — Merge assisté et gouvernance du dépôt

`ORF-BUILD-012` à `ORF-BUILD-015` sont `functional` dans la page Build. Le merge assisté exerce
réellement les autorités listes et graphes sur deux variantes du module, expose les conflits et
conserve les deux variantes par identités déterministes sans écrire la source. Un onglet séparé
prévisualise puis persiste uniquement le profil de gouvernance : préfixe et propriété des worktrees,
sept contrôles CI et catalogue de cinq fixtures. La commande est protégée par révision et empreintes,
alimente undo/redo/sauvegarde dans un isolat et laisse projet, historique, sélection, approbation et
Git inchangés. Le portail passe 23/23 tests en 538,11 s, le smoke global et deux captures Forward+
FHD inspectées ; la matrice atteint 594 `functional` / 123 `planned` et le graphe Roslyn 11 056
nœuds / 84 680 relations sans diagnostic. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX8_C.md` ; aucun merge Git, agent, runner CI distant, shell arbitraire,
commit ou publication n'est revendiqué.

### UX8-D — État du dépôt, versions et décisions

`ORF-BUILD-001`, `ORF-BUILD-002`, `ORF-BUILD-018` et `ORF-BUILD-019` sont `functional` dans la page
Production. Après sélection explicite, Studio inspecte le profil et le dépôt local sans lancer Git :
garanties de dépôt, huit emplacements standards, versions distinctes Core/kits/package/schémas,
compatibilité, changelog, dossier/modèle/statuts ADR et preuves de tests/benchmarks. Les dossiers
absents restent annoncés comme configurés mais non matérialisés. L'inspection est annulable, ses
quatre résultats sont navigables et le journal reste replié. Le portail passe 19/19 tests en
535,79 s, le smoke global et une capture Forward+ FHD corrigée puis inspectée ; la matrice atteint
598 `functional` / 119 `planned` et le graphe Roslyn 11 066 nœuds / 84 755 relations sans diagnostic.
Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX8_D.md` ; aucune commande Git,
génération, écriture d'ADR, commit ou publication n'est revendiquée.

### UX8-E — Documentation, tâches, canaux et diagnostics

`ORF-BUILD-020`, `ORF-BUILD-021`, `ORF-BUILD-024` et `ORF-BUILD-025` sont `functional` dans la page
Production. Après sélection explicite, Studio reconstruit les six catalogues documentaires en mode
contrôle, valide les manifestes de tâches, prévisualise la transition du canal de release puis crée,
inspecte et supprime une archive de crash temporaire assainie. Les quatre résultats sont navigables,
l'inspection est annulable et les journaux restent repliés à la demande. Le portail passe 30/30
tests en 531,26 s, le smoke global et une capture Forward+ FHD inspectée ; la matrice atteint 602
`functional` / 115 `planned` et le graphe Roslyn 11 076 nœuds / 84 866 relations sans diagnostic.
Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX8_E.md` ; aucun agent, écriture
documentaire, bundle, signature, publisher, publication, dump natif ou collecte de processus n'est
revendiqué.

### UX8-F — Dépendances, notices, crédits et frontière IP

`ORF-BUILD-022`, `ORF-BUILD-023`, `ORF-CONT-030` et `ORF-CONT-031` sont `functional` dans la page
Production. L’inspection conserve l’état réel vide de Forgotten Forest ; une approbation complète est
créée uniquement dans l’isolat de preuve. Le formulaire passe par `DistributionComplianceValidator`
puis `SetModuleDistributionHandler`, lie l’approbation à la révision et aux empreintes exactes, et
prouve dry-run, application, undo/redo puis save/reopen. La notice Shipping est générée deux fois et
comparée octet par octet ; crédits, provenances et frontières IP restent en lecture canonique. Le
portail passe 20/20 tests en 534,72 s, le smoke global et une capture Forward+ FHD inspectée ; la
matrice atteint 606 `functional` / 111 `planned` et le graphe Roslyn reste frais sans diagnostic.
Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX8_F.md` ; aucun téléchargement, audit
externe, package, signature, écriture du registre d’assets ou publication n'est revendiqué.

### UX8-G — Packages versionnés, priorité et dépendances

`ORF-PROJ-020` et `ORF-CONT-001`, `ORF-CONT-002`, `ORF-CONT-003`, `ORF-CONT-007` sont
`functional` dans la page Packages. Forgotten Forest conserve son catalogue vide ; la preuve riche
reste un brouillon local et une transaction dans un workspace temporaire. La surface édite les
packages et leurs dépendances avec identités stables, recalcule les hashes, prévisualise ordre et
conflits, puis passe par `SetContentWorkspaceHandler` avant undo/redo et save/reopen.
`ContentPackageCompiler` produit lock, manifeste et ressources résolues en mémoire. Le portail passe
47/47 tests en 544,55 s, le smoke global et une capture Forward+ FHD corrigée puis inspectée ; la
matrice atteint 611 `functional` / 106 `planned` et le graphe Roslyn final 11 105 nœuds / 85 168
relations sans diagnostic. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX8_G.md` ; aucune installation, écriture de package, exécution de script,
signature, opération réseau ou publication n'est revendiquée.

### UX8-H — Distribution multi-plateforme, signatures et deltas

`ORF-PROJ-024`, `ORF-BUILD-016`, `ORF-BUILD-017`, `ORF-CONT-028` et `ORF-CONT-029` sont
`functional` dans la page Export. La surface édite séparément les cinq profils de cible, trois
plateformes, le packaging Windows, les empreintes publiques de signature et la politique delta,
puis passe par `SetModuleDistributionHandler` avec dry-run, approbation liée aux empreintes,
undo/redo et save/reopen. La vérification optionnelle construit des bundles déterministes et exerce
signature, application et rollback delta uniquement dans un workspace temporaire. Le portail passe
22/22 tests en 422,07 s, le smoke global et une capture Forward+ FHD inspectée ; la matrice atteint
616 `functional` / 101 `planned` et le graphe Roslyn final 11 120 nœuds / 85 368 relations sans
diagnostic. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX8_H.md` ; aucune clé privée, installation, publication, notarisation,
téléversement ou opération réseau n'est exécuté.

### UX8-I — Compatibilité projet, migrations et plateformes

`ORF-PROJ-025` et `ORF-CONT-025` sont `functional` dans la page Production. Le profil versionné
préserve toutes les queues de `WorkspaceBuildProfile`, passe par `SetWorkspaceBuildProfileHandler`
et prévisualise la migration réelle du workspace sans l’appliquer. L’analyse des manifests choisit
une variante exacte ou `Any`, puis vérifie rôles requis, budget, présence des fichiers, SHA-256 et
métadonnées compatibles sans dupliquer l’éditeur UX3-G. Le portail passe 64/64 tests en 423,41 s,
le smoke global et une capture Forward+ FHD inspectée ; la matrice atteint 618 `functional` / 99
`planned` et le graphe Roslyn final 11 133 nœuds / 85 504 relations sans diagnostic. Preuves et
limites sont liées dans `docs/acceptance/STUDIO_UX8_I.md` ; aucune migration du module source,
modification de manifest, mesure matérielle, import runtime, benchmark, commande Git ou publication
n'est exécuté.

### UX8-J — Sécurité des workers et protection anti-injection

`ORF-AI-023` et `ORF-AI-032` sont `functional` dans la page Sécurité. La politique worker conserve
Python libre, shell, eval, réseau et chemins non confinés sur refus, tout en exposant une liste
blanche Blender typée. Le profil Assistant traite le projet comme donnée non fiable et la sonde
canonique refuse instruction hostile, outil hors liste et élévation de permission. Deux
consentements, plans, historiques et fichiers distincts restent visibles. Le portail passe 35/35
tests en 472,65 s, le smoke global et une capture Forward+ FHD inspectée ; la matrice atteint 620
`functional` / 97 `planned`. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX8_J.md` ; aucun worker, Python, Blender, shell, réseau, fournisseur IA,
outil sémantique, commande Git ou publication n'est exécuté.

### UX8-K — Import/export en masse confiné

`ORF-BUILD-007` est `functional` dans la page Export. L’atelier sélectionne explicitement le sens,
les chemins relatifs, le glob, les extensions, la collision, la provenance et la licence, puis
affiche fichiers, tailles, SHA-256, diff et diagnostics avant consentement. Le transfert passe par
`ContentBatchTransferService`, la mutation par `SetContentWorkspaceHandler`, et la session coordonne
rollback, undo/redo, sauvegarde et réouverture sans prétendre fournir une transaction distribuée.
Le portail passe 51/51 tests en 423,12 s, le smoke global et une capture Forward+ FHD corrigée puis
inspectée ; la matrice atteint 621 `functional` / 96 `planned`. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX8_K.md` ; aucun téléchargement, source externe, watcher, synchronisation,
publication, commande Git ou mutation du module source n'est exécuté.

### UX8-L — Synchronisation de dépôt et métadonnées NWSync

`ORF-CONT-009` et `ORF-EE-025` sont `functional` dans la page Packages. La surface sélectionne le
module, configure reprise, déduplication et vérification SHA-256, confiance signataire, cache,
quota, licences, canal et compatibilité NWSync, puis affiche le manifeste et le diff avant
consentement. La mutation passe par `SetRuntimeExtensionsHandler`; la vérification réelle utilise
uniquement un dépôt local et un cache temporaire via `ContentRepositorySyncService`, avec second
passage entièrement réutilisé. Le portail passe 61/61 tests en 419,84 s, le smoke global et une
capture Forward+ FHD inspectée ; la matrice atteint 623 `functional` / 94 `planned` et UX8 43/51.
Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX8_L.md` ; aucun réseau, téléchargement
distant, serveur NWSync, watcher, installation, commande Git ou publication n'est exécuté.

### UX8-M — Expérience joueur multijoueur

`ORF-MP-003` à `ORF-MP-006` sont `functional` dans la page Lanceur. La surface sélectionne le
module, configure découverte, lobby, création et autorité des personnages, puis passe par
`NetworkPlayerExperienceHistory` avec dry-run, gardes de révision et d'empreintes, undo/redo et
sauvegarde atomique dans `.forge/network/player-experience.json`. Le profil combat reste strictement
préservé. Le portail passe 9/9 tests en 443,52 s, le smoke global et quatre captures Forward+ FHD
corrigées puis inspectées ; la matrice atteint 628 `functional` / 89 `planned`, UX8 47/51 et le
graphe Roslyn 11 213 nœuds / 86 391 relations sans diagnostic.
Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX8_M.md` ; aucun transport, serveur,
annuaire distant, client, compte, monde persistant, commande Git ou publication n'est exécuté.

### UX8-N — Cycle de vie des packages

`ORF-CONT-010`, `ORF-CONT-019`, `ORF-CONT-026` et `ORF-CONT-027` sont `functional` dans la page
Packages. La surface conserve trois autorités distinctes : `SetRuntimeExtensionsHandler` pour les
packs curatés, `SetContentWorkspaceHandler` et le registre canonique pour les namespaces, puis les
stores dédiés pour migration et désinstallation. Les quatre historiques imposent sélection,
dry-run, consentement, gardes stale, undo/redo et save/reopen ; migration et retrait externe
produisent des backups restaurables et la collecte ne supprime que les blobs orphelins. Les 22/22
tests ciblés, le smoke global, quatre captures Forward+ FHD inspectées et le portail en 581,21 s
ferment UX8 à 51/51 et portent la matrice à 632 `functional` / 85 `planned`. Le graphe Roslyn atteint
11 234 nœuds / 86 636 relations sans diagnostic. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX8_N.md` ; aucun script de package, agent, shell, réseau, téléchargement,
commande Git, mutation du module source ou publication n'est exécuté.

### UX9-A — Session réseau locale autoritaire

`ORF-MP-002`, `ORF-MP-007`, `ORF-MP-011` et `ORF-MP-012` sont `functional` dans la page Directeur
réseau. La surface persiste `NetworkFoundationProfile`, passe par `NetworkFoundationHistory` avec
dry-run et double garde, puis ouvre après permission et confirmation une session autoritaire locale.
La preuve connecte un opérateur, refuse une mutation cliente, crée un groupe, calcule un delta
répliqué sous les six règles d’intérêt, audite chaque décision puis déconnecte réellement le membre.
Les 17/17 tests ciblés, le smoke global et la capture Forward+ FHD corrigée puis inspectée portent la
matrice à 636 `functional` / 81 `planned` et UX9 à 4/68. Le portail passe en 643,09 s et le graphe
Roslyn atteint 11 253 nœuds / 86 841 relations sans diagnostic. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX9_A.md` ; aucun port public, transport distant, serveur dédié durable,
compte réel, commande Git ou publication n'est utilisé. `ORF-MP-001` reste explicitement planifié.

### UX9-B — Opérations réseau locales gouvernées

`ORF-MP-013`, `ORF-MP-015`, `ORF-MP-017`, `ORF-MP-020` et `ORF-MP-027` sont `functional` dans la
page Directeur réseau. La surface persiste `NetworkOperationsProfile`, sépare cinq workflows
humains et passe par `NetworkOperationsHistory` avec sélection, dry-run, permission Administrateur,
approbation liée à la révision, gardes stale, undo/redo et save/reopen. Les sondes exercent reprise,
hash et cache adressé, négociation de version, grants configurés, canaux/langues/accessibilité du
chat et dispatch priorisé avec trace et anti-rejeu. Les 25/25 tests ciblés, le smoke global et cinq
captures Forward+ FHD inspectées portent la matrice à 641 `functional` / 76 `planned` et UX9 à 9/68.
Le portail passe en 532,65 s et le graphe Roslyn atteint 11 276 nœuds / 87 071 relations sans
diagnostic. Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX9_B.md` ; caches et
workspaces temporaires sont libérés, sans transport distant, serveur public, dépôt de contenu
distant, compte réel, bridge externe, commande Git ou publication.

### UX9-C — Monde réseau et administration gouvernée

`ORF-MP-009`, `ORF-MP-010`, `ORF-MP-016`, `ORF-MP-018` et `ORF-MP-019` sont `functional` dans la
page Directeur réseau. La surface persiste `NetworkWorldAdministrationProfile`, sépare cinq
workflows humains et passe par `NetworkWorldAdministrationHistory` avec sélection, dry-run,
permission Administrateur, approbation liée à la révision, gardes stale, undo/redo et save/reopen.
Les sondes exercent instances/entrée de groupe, création dynamique bornée, compte local et erreurs
non énumérables, commandes sûres/rate limit et modération auditée ; le service MCP existant couvre
lecture filtrée, plan, apply et revert sous rôle et portée serveur. Les 17/17 tests ciblés, le smoke
global et cinq captures Forward+ FHD inspectées portent la matrice à 646 `functional` / 71 `planned`
et UX9 à 14/68. Le portail passe en 548,58 s et le graphe Roslyn atteint 11 299 nœuds / 87 300
relations sans diagnostic. Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX9_C.md` ;
Studio n'ouvre aucun listener, socket, compte externe, serveur public, commande Git ou publication.

### UX9-D — Persistance réseau et observabilité locale

`ORF-MP-022`, `ORF-MP-023`, `ORF-MP-028`, `ORF-MP-030` et `ORF-MP-032` sont `functional` dans la
page Directeur réseau. La surface persiste `NetworkPersistenceObservabilityProfile`, sépare cinq
workflows humains et passe par `NetworkPersistenceObservabilityHistory` avec sélection, dry-run,
permission Administrateur, approbation liée à la révision, gardes stale, undo/redo et save/reopen.
Les sondes exercent snapshot révisionné, opérations `world_flag` allowlistées, métriques et santé
filtrées, capture bornée du tick et ticket de reconnexion à usage unique avec refus du replay. Les
12/12 tests ciblés, le smoke global et cinq captures Forward+ FHD inspectées portent la matrice à
651 `functional` / 66 `planned` et UX9 à 19/68. Le portail passe en 534,22 s et le graphe Roslyn
final est frais, déterministe et sans diagnostic. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX9_D.md` ; Studio n'ouvre aucun endpoint externe, exporter, socket, serveur
public, base SQL distante, commande Git ou publication.

### UX9-E — Résilience et capacité réseau

`ORF-MP-033` et `ORF-MP-034` sont `functional` dans la page Directeur réseau. La surface persiste
`NetworkResilienceCapacityProfile`, sépare sauvegarde/rollback et capacité/file, puis passe par
`NetworkResilienceCapacityHistory` avec sélection, dry-run, permission Administrateur, approbation
liée à la révision, gardes stale, undo/redo et save/reopen. Les sondes exercent un vrai snapshot et
`RollbackService` sur copie mémoire, puis remplissent `ConnectionQueue`, observent les positions 1
et 2 et promeuvent la première réservation. Les 11/11 tests ciblés, le smoke global et deux captures
Forward+ FHD inspectées portent la matrice à 653 `functional` / 64 `planned` et UX9 à 21/68. Le
portail passe en 546,18 s et le graphe Roslyn final est frais, déterministe et sans diagnostic.
Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX9_E.md` ; Studio n'ouvre aucun stockage
distant, socket, serveur public, rollback du module réel, commande Git ou publication.

### UX9-F — Espace Live Director

`ORF-DM-001`, `ORF-DM-003`, `ORF-DM-004`, `ORF-DM-005` et `ORF-DM-006` sont `functional` dans la
page Directeur réseau. La surface persiste `LiveDirectorWorkspaceProfile`, sépare client/Creator,
spawn spatial et Chooser/focus, puis passe par `LiveDirectorWorkspaceHistory` avec sélection,
dry-run, permission Administrateur, approbation liée à la révision, gardes stale, undo/redo et
save/reopen. La sandbox exerce les vrais `DirectorAuthenticationService`, `LiveDirectorClient`,
`LiveCatalogService`, `LiveSpawnService`, `LiveWorldChooser` et `LiveWorldActionService`, conserve
les IDs de preview, applique puis compense le spawn, filtre le privé et prouve un focus sans mutation.
Les 12/12 tests ciblés, 54/54 régressions Live Director, le smoke global et trois captures Forward+
FHD inspectées portent la matrice à 658 `functional` / 59 `planned` et UX9 à 26/68. Le portail passe
en 549,52 s et le graphe Roslyn atteint 11 367 nœuds / 87 994 relations sans diagnostic. Preuves et
limites sont liées dans `docs/acceptance/STUDIO_UX9_F.md` ; Studio n'ouvre aucun socket, compte
externe, serveur réel, commande Git ou publication.

### UX9-G — Actions Live Director sûres

`ORF-DM-007`, `ORF-DM-008`, `ORF-DM-009`, `ORF-DM-010` et `ORF-DM-012` sont `functional` dans la
page Directeur réseau. La surface persiste `LiveDirectorActionProfile`, sépare
téléportation/destruction, soin/limbo et possession/restitution, puis passe par
`LiveDirectorActionHistory` avec sélection, dry-run, permission Administrateur, approbation liée à
la révision, gardes stale, undo/redo et save/reopen. La sandbox exerce les vrais
`AuthorizationService`, `LiveWorldChooser`, `LiveWorldActionService` et
`LiveDirectorControlService`, conserve l'orientation et l'identité, applique puis compense les
actions et restitue la possession à expiration. Les 18/18 tests ciblés, 56/56 régressions Live
Director, le smoke global et trois captures Forward+ FHD corrigées puis inspectées portent la
matrice à 663 `functional` / 54 `planned` et UX9 à 31/68. Le portail passe en 514,28 s et le graphe
Roslyn atteint 11 391 nœuds / 88 214 relations sans diagnostic. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX9_G.md` ; Studio n'ouvre aucun socket, compte externe, serveur réel,
commande Git ou publication.

### UX9-H — Autorité Live Director gouvernée

`ORF-DM-013`, `ORF-DM-014`, `ORF-DM-016`, `ORF-DM-017` et `ORF-DM-019` sont `functional` dans la
page Directeur réseau. La surface persiste `LiveDirectorAuthorityProfile`, sépare
incarnation/protection, or/expérience et portes/portails, puis passe par
`LiveDirectorAuthorityHistory` avec sélection, dry-run, permission Administrateur, approbation liée
à la révision, gardes stale, undo/redo et save/reopen. La sandbox exerce les vraies autorités de
contrôle, dégâts, économie, progression et monde, vérifie expiration, round-trip et projection de
niveau, applique puis compense les ajustements et restaure collision/portail. Les 18/18 tests
ciblés, 58/58 régressions Live Director, le smoke global et trois captures Forward+ FHD corrigées
puis inspectées portent la matrice à 668 `functional` / 49 `planned` et UX9 à 36/68. Le portail
passe en 512,27 s et le graphe Roslyn atteint 11 416 nœuds / 88 452 relations sans diagnostic.
Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX9_H.md` ; Studio n’ouvre aucun socket,
compte, serveur ou portefeuille externe, commande Git ou publication.

### UX9-I — Groupes Live Director gouvernés

`ORF-DM-020`, `ORF-DM-021`, `ORF-DM-023`, `ORF-DM-024` et `ORF-DM-026` sont `functional` dans la
page Directeur réseau. La surface persiste `LiveDirectorGroupProfile`, sépare triggers/sélection,
déplacement/attaque et voyage joueur, puis passe par `LiveDirectorGroupHistory` avec sélection,
dry-run, permission Administrateur, consentement du groupe, approbation liée à la révision, gardes
stale, undo/redo et save/reopen. La sandbox exerce les vrais services de progression, commandes de
groupe et joueurs, vérifie identifiant stable, plafond, échecs partiels, avertissement de faction et
transition caméra, puis restaure l’état synthétique. Les 13/13 tests ciblés, 58/58 régressions Live
Director, le smoke global et trois captures Forward+ FHD inspectées portent la matrice à 673
`functional` / 44 `planned` et UX9 à 41/68. Le portail passe en 517,61 s et le graphe Roslyn atteint
11 438 nœuds / 88 669 relations sans diagnostic. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX9_I.md` ; Studio n’ouvre aucun socket, serveur ou joueur réel, commande Git
ou publication.

### UX9-J — Opérations Live Director gouvernées

`ORF-DM-027`, `ORF-DM-028`, `ORF-DM-029`, `ORF-DM-032` et `ORF-DM-033` sont `functional` dans la
page Directeur réseau. La surface persiste `LiveDirectorOperationsProfile`, sépare
recherche/ForgeScript, variables/scène et visibilité MJ, puis passe par
`LiveDirectorOperationsHistory` avec sélection, dry-run, permission Administrateur, approbation
liée à la révision, gardes stale, undo/redo et save/reopen. La sandbox exerce les vrais services de
recherche, scripts, variables, scènes et visibilité, vérifie focus visuel, adaptateur serveur, état
persistant, cinq facettes, rollback et overlay viewport, puis restaure l’état synthétique. Les 16/16
tests ciblés, 58/58 régressions Live Director, le smoke global et trois captures Forward+ FHD
inspectées portent la matrice à 678 `functional` / 39 `planned` et UX9 à 46/68. Le portail passe en
519,91 s et le graphe Roslyn atteint 11 461 nœuds / 88 901 relations sans diagnostic. Preuves et
limites sont liées dans `docs/acceptance/STUDIO_UX9_J.md` ; Studio n’ouvre aucun socket, serveur ou
joueur réel, commande Git ou publication.

### UX9-K — Narration Live Director gouvernée

`ORF-DM-035` et `ORF-DM-036` sont `functional` dans la page Directeur réseau. La surface persiste
`LiveDirectorNarrativeProfile`, sépare quête et rencontre, puis passe par
`LiveDirectorNarrativeHistory` avec sélection, dry-run, permission Administrateur, approbation liée
à la révision, gardes stale, undo/redo et save/reopen. La sandbox exerce les vrais services de quête
et rencontre : portée tous joueurs ou sélection, quatre conséquences, projection RPG, cinq
commandes, scaling borné, adaptateurs spawn/IA et rollbacks complets. Les 10/10 tests ciblés, 58/58
régressions Live Director, le smoke global et trois captures Forward+ FHD inspectées portent la
matrice à 680 `functional` / 37 `planned` et UX9 à 48/68. Le portail passe en 525,88 s et le graphe
Roslyn atteint 11 484 nœuds / 89 114 relations sans diagnostic. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX9_K.md` ; Studio n’ouvre aucun socket, serveur ou joueur réel, commande Git
ou publication.

### UX9-L — Accès et supervision Live Director

`ORF-DM-002`, `ORF-DM-011`, `ORF-DM-025`, `ORF-DM-031` et `ORF-DM-034` sont `functional` dans la
page Directeur réseau. La surface persiste `LiveDirectorAccessProfile` et expose cinq politiques
humaines bornées, puis passe par `LiveDirectorAccessHistory` avec sélection, dry-run, permission
Administrateur, approbation liée à la révision, gardes stale, undo/redo et save/reopen. La sandbox
exerce les vraies autorités d’authentification, inspection, joueurs, audit et chat sur un scénario
synthétique, vérifie les cinq handles ForgeScript puis libère les ressources. Les 13/13 tests
ciblés, 58/58 régressions Live Director, le smoke global et la capture Forward+ FHD corrigée puis
inspectée portent la matrice à 685 `functional` / 32 `planned` et UX9 à 53/68. Le portail passe en
503,26 s et le graphe Roslyn atteint 11 502 nœuds / 89 296 relations sans diagnostic. Preuves et
limites sont liées dans `docs/acceptance/STUDIO_UX9_L.md` ; Studio n’ouvre aucun socket, compte,
serveur ou canal externe, commande Git ou publication.

### UX9-M — Interventions et enregistrement Live Director

`ORF-DM-015`, `ORF-DM-018`, `ORF-DM-022`, `ORF-DM-030` et `ORF-DM-037` sont `functional` dans la
page Directeur réseau. La surface édite le `LiveDirectorProductionProfile` canonique du module par
`SetRuntimeExtensionsHandler` et expose cinq cartes humaines pour repos forcé, niveau avec
snapshot, apparence bornée, groupe sur raccourci et enregistrement expurgé. Sélection, dry-run,
permission Administrateur, motif, confirmation, approbation liée à la révision, gardes stale,
undo/redo, save/reopen et préservation exacte des profils voisins sont vérifiés. La sandbox exerce
`LiveProductionService` et `PostLaunchCapabilityResolver` sur des identités et un monde synthétiques,
puis libère les ressources. Les 7/7 tests ciblés, 60/60 régressions Live Director, le smoke global
et la capture Forward+ FHD corrigée puis inspectée portent la matrice à 690 `functional` / 27
`planned` et UX9 à 58/68. Le portail passe en 507,90 s et le graphe Roslyn final est frais,
déterministe et sans diagnostic. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX9_M.md` ; Studio n’ouvre aucun socket, serveur ou joueur réel, commande
Git ou publication.

### UX9-N — Production réseau avancée

`ORF-MP-014`, `ORF-MP-021`, `ORF-MP-024`, `ORF-MP-025`, `ORF-MP-026` et `ORF-MP-035` sont
`functional` dans la page Directeur réseau. La surface édite les profils canoniques gameplay et
réseau du module par `SetRuntimeExtensionsHandler` et expose six cartes humaines pour contenu
curaté, voix, shard, hot reload, extensions serveur et protocole cross-platform. Sélection, dry-run,
permission Administrateur, confirmation, approbation liée à la révision, gardes stale, undo/redo,
save/reopen et préservation exacte des profils voisins sont vérifiés. La sandbox exerce
`PostLaunchCapabilityResolver` et `ServerProductionService` sur un projet synthétique, puis libère
les ressources. Les 8/8 tests ciblés, 99/99 régressions réseau, le smoke global et la capture
Forward+ FHD inspectée portent la matrice à 696 `functional` / 21 `planned` et UX9 à 64/68. Le
portail passe en 507,54 s et son graphe Roslyn atteint 11 528 nœuds / 89 644 relations sans
diagnostic, puis est régénéré après promotion. Preuves et limites sont liées dans
`docs/acceptance/STUDIO_UX9_N.md` ; Studio n’ouvre aucun socket, serveur, audio, extension externe,
commande Git ou publication.

### UX9-O — Clôture réseau

`ORF-MP-001`, `ORF-MP-008`, `ORF-MP-029` et `ORF-MP-031` sont `functional` dans la page Directeur
réseau. Quatre cartes humaines coordonnent les profils canoniques de fondation, expérience joueur
et assurance pour le serveur dédié headless, le PvP/tir ami, les logs structurés privés et
l’anti-triche autoritaire. Sélection, trois dry-runs canoniques, permission Administrateur,
confirmation distincte, approbations liées aux trois révisions et empreintes, gardes stale,
compensation, undo/redo, sauvegarde atomique par document, réouverture et préservation sémantique
des voisins sont vérifiés. La sandbox exerce la configuration et la session réseau, `PartyService`,
`CombatAuthority`, `StructuredLogRouter` et `AntiCheatService` sur des identités et sinks mémoire,
puis libère toutes les ressources. Les 37/37 tests ciblés, 99/99 régressions réseau, 385/385
régressions ModuleFormat, le smoke global et la capture Forward+ FHD corrigée puis inspectée portent
la matrice à 700 `functional` / 17 `planned` et closent UX9 à 68/68. Le portail passe en 515,32 s et
le graphe Roslyn final atteint 11 548 nœuds / 89 923 relations sans diagnostic, frais et
déterministe. Preuves et limites sont liées dans `docs/acceptance/STUDIO_UX9_O.md` ; Studio n’ouvre aucun
port, socket, service Windows, serveur persistant, sink distant, commande Git ou publication.

### UX10-A — Contexte, outils et fournisseurs

`ORF-AI-001/002/005/012/013` sont `functional` dans une surface Assistant unifiée accessible depuis
Contexte. Le module est sélectionné explicitement ; les dix surfaces de contexte, le graphe borné,
les cinq outils sémantiques, les fournisseurs hors ligne/OpenAI local/Ollama local et les politiques
de budget, classification et consentement sont visibles avant approbation. La sandbox utilise une
copie confinée et restaure profil, historique, sélection et surface. Preuve :
`docs/acceptance/STUDIO_UX10_A.md`.

### UX10-B — Plans, transactions et autorisations

`ORF-AI-006/007/008/009/010` sont `functional` dans Plans. Le plan structuré incomplet est refusé,
le dry-run conserve révision et empreinte, puis `begin → preview → commit → rollback` produit un diff
typé et restaure exactement les variables initiales. Les niveaux d’autorisation restent distincts
et une confirmation absente est refusée. Preuve : `docs/acceptance/STUDIO_UX10_B.md`.

### UX10-C — Audit, prompts et hors ligne

`ORF-AI-011/030/031/033` sont `functional` dans Plans. Prompt versionné et hashé, validation JSON
Schema, réparation bornée, refus d’élévation de permission/outils et audit expurgé sont exercés sans
réseau. L’édition manuelle, le build et le runtime restent indépendants des métadonnées IA. Preuve :
`docs/acceptance/STUDIO_UX10_C.md`.

### UX10-D — 3D assistée et cohérence narrative

`ORF-AI-021/022/029` sont `functional` dans 3D assistée. Les recettes Blender typées et le
fournisseur text/image-to-3D déterministe imposent provenance CC0, staging, LOD, collision,
acceptation humaine et rollback ; le contrôle narratif retourne des constats sourcés sans réécrire
le contenu. Preuve : `docs/acceptance/STUDIO_UX10_D.md`.

Les quatre lots cumulent 66/66 tests ciblés, un smoke global, quatre captures Forward+ FHD
corrigées puis inspectées et un portail incrémental en 524,12 s. Le graphe Roslyn est frais à
11 566 nœuds / 90 143 relations / 0 diagnostic et la matrice atteint 717 `functional` / 0
`planned`. La qualification n’exécute aucun modèle externe ni Blender, ne mute pas le module source
et ne réalise aucune opération Git ou publication.

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
