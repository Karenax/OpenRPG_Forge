# OpenRPG Forge

OpenRPG Forge est un projet de moteur, runtime et toolset libre pour créer des RPG 3D modulaires. Il vise une expérience de création accessible, un domaine RPG indépendant du moteur, des modules partageables et une assistance IA facultative et transactionnelle.

Le dépôt contient désormais deux **prototypes fonctionnels** : la Crypte des Cendres et la carte
extérieure Forgotten Forest. La solution C# compilable fournit
domaine versionné, application de création **Forge**, runtime 3D jouable, ForgeScript natif, archives
`.orpmod`, sauvegarde/reprise et host MCP transactionnel. La commande `scripts/Verify.ps1` valide
l’ensemble localement. Forgotten Forest ajoute terrain canonique 512 × 512 m, caméra orbitale,
4 617 instances de biome issues de 13 recettes, 17 modèles Poly Haven CC0 gouvernés, rivière
animée, navigation de surface, quête, combat et export autonome. La tranche verticale G0 à G13 est
fermée et les 717 exigences Aurora sont actuellement au statut `Tested`. Le chantier actif est la
consolidation de la CI, de l’interface, de la distribution et de la qualité visuelle.

Le lancement utilise un splash **OpenRPG_Forge** dédié, commun au runtime et à l’application de
création. La fenêtre de l’application porte également le titre **OpenRPG_Forge**. Le visuel peut
être régénéré avec `scripts/Generate-Branding.ps1`.

## Sources de vérité

Lire dans cet ordre avant toute modification :

1. [`AGENTS.md`](AGENTS.md)
2. [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md)
3. [`STATUS.md`](STATUS.md)
4. [`TASKS_MVP.md`](TASKS_MVP.md)
5. [`docs/planning/COMPLETE_FUNCTION_BACKLOG.md`](docs/planning/COMPLETE_FUNCTION_BACKLOG.md)
6. [`tracking/project/AURORA_PARITY_COVERAGE.csv`](tracking/project/AURORA_PARITY_COVERAGE.csv)
7. [`docs/audits/COMPLETE_FUNCTION_COVERAGE_AUDIT_2026-08-07.md`](docs/audits/COMPLETE_FUNCTION_COVERAGE_AUDIT_2026-08-07.md)
8. les ADR applicables dans [`docs/ADR/`](docs/ADR/)

`PROJECT_CONTEXT.md` est l’unique vision produit, architecture, IA/MCP et graphique courante. Les
anciens contextes initiaux et journaux de livraison ont été retirés pour éviter les contradictions.

## Graphe d’architecture

Le dépôt fournit un [graphe d’architecture déterministe Roslyn](docs/architecture/README.md), généré
localement sans service externe. Avant une évolution transversale, obtenir le contexte utile avec
`dotnet run --project tools/Forge.ArchitectureGraph -- query "<module ou symbole>" --mode impact --format context`.
Après une modification indexée, exécuter `generate`, puis `check --full` avant de terminer.

## Lancer le prototype

Sous Windows, depuis la racine du dépôt :

```powershell
.\scripts\Run-Prototype.ps1
```

Le lanceur compile d'abord le runtime Debug, puis ouvre en Forward+ le workspace Forge fourni avec le dépôt.
Son journal de démarrage est conservé dans `artifacts/verification/runtime-interactive.log`. Si le
chargement du module échoue, le runtime affiche désormais le diagnostic dans la fenêtre au lieu de
rester sur un écran vide.

Commandes :

- `ZQSD`, `WASD` ou flèches : déplacement ;
- `E` : parler, ramasser la clé ou ouvrir la porte ;
- `Espace` : sauter ;
- `1` : attaquer ; `2` à `6` : interactions, roue d’actions, repos et sauvegardes ;
- clic gauche : orbite libre ; clic droit : orbite et orientation du personnage ; molette : zoom ;
- `Ctrl+R` : afficher ou masquer le compteur FPS ;
- `F5` / `F9` : sauvegarder / reprendre le checkpoint rapide.

Le parcours actuel permet de parler à Elyra, récupérer la Clé de braise, ouvrir la porte, combattre la Sentinelle puis le Gardien des Cendres, déclencher sa phase 2 et terminer la quête.

Pour lancer la carte extérieure **Forgotten Forest** :

```powershell
.\scripts\Run-ForgottenForest.ps1
```

Le lanceur régénère de façon idempotente le module et ses crédits depuis le Vault CC0, compile le
runtime puis ouvre la carte en Forward+. Le Vault original reste inchangé. Forgotten Forest permet
de se déplacer sur un terrain 512 × 512 m avec des contrôles MMO modernes, d’orbiter et zoomer la caméra, de suivre le sentier,
parler à Maëlys, trouver la Graine de lune, ouvrir le Passage des Ronces et affronter le Gardien des
Racines. Le ruisseau est animé, ses dix volumes appliquent nage et courant, et le chemin de
navigation relie le camp au sanctuaire en respectant la traversée du pont. Commandes caméra : clic
gauche pour orbiter librement, clic droit pour orienter caméra et personnage, molette pour zoomer,
`Début`/`Fin` pour tourner au clavier.

## Parcourir tous les modèles du Vault

```powershell
& '.\tools\asset_vault_browser\Launch-Asset-Atlas.ps1'
```

**OpenRPG Asset Atlas** ouvre une application locale séparée : 5 236 modèles logiques filtrables par
source, pack, format et type, miniatures, aperçu GLB/glTF interactif à la demande et sélection
persistante exportable en JSON ou copiable pour Codex. Le Vault reste en lecture seule.

## Ouvrir le toolset

```powershell
.\scripts\Open-Toolset.ps1
```

L’application ajoute l’écran principal **Forge**. Elle ouvre un dossier de projet Forge, affiche ses
diagnostics, reconstruit un aperçu 3D et le sauvegarde atomiquement. Une palette permet de placer,
déplacer, tourner et supprimer des entités sur la grille, avec annuler/rétablir ; la Crypte des
Cendres est ouverte par défaut. Le lanceur compile le Toolset, active Forward+ et écrit son journal
dans `artifacts/verification/toolset-interactive.log`.

Le bouton **Nouveau module** crée un workspace `1.28.0` dans un dossier dédié. L’assistant demande le
type de première zone — intérieur ou extérieur sculptable — ainsi que sa largeur et sa profondeur,
puis prépare son identité, un sol provisoire et un point de départ immédiatement éditables. Un dossier
existant non vide est toujours refusé.

Dans **Zones → Terrain & volumes**, l’onglet **Sculpture** permet de lever, abaisser, aplanir ou
lisser le heightmap, peindre localement un matériau, ainsi que d’ouvrir ou refermer des cellules de
grotte. Le rayon et l’intensité sont prévisualisés avant application ; chaque trait reconstruit
l’aperçu et reste annulable.

Le bouton **Modifier les propriétés** personnalise l’instance sélectionnée sans dupliquer ni modifier
son blueprint. L’inspecteur gère des valeurs texte, booléennes, entières et numériques, puis applique
le changement par le bus de commandes avec révision et annuler/rétablir.

Le bloc **Test du module** permet de choisir un point de départ et de lancer l’état courant sans le
sauvegarder : Forge produit un snapshot temporaire, ouvre le runtime, affiche son rapport puis
supprime le snapshot. La direction de l’interface complète est décrite dans
[`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md).

L’espace **Dialogues** édite les nœuds, les choix, leurs conditions typées, leurs actions et leurs
textes localisables, avec aperçu immédiat. Dialogues et quêtes sont stockés dans le workspace `1.28.0`,
mutés par commandes transactionnelles et consommés directement par le runtime de la Crypte.

L’espace **Validation** configure Editor, Client, DedicatedServer, Test et Shipping, gère le registre
des dépendances et prévisualise les licences ou provenances qui bloqueraient une distribution. Le
bouton **Exporter .orpmod** sauvegarde, construit le profil choisi, génère le NOTICE puis valide
l'archive partageable. Le host MCP
officiel fournit résumé, révision, diagnostics, plans de dialogue prévisualisables/réversibles,
compilation ForgeScript et agent de test sans exposer de shell. Le parcours complet est détaillé dans
le [guide créateur](docs/CREATOR_GUIDE.md).

La navigation principale contient exactement neuf espaces : **Monde**, **Blueprints**, **Dialogues**,
**Quêtes**, **Scripts**, **Assets**, **Validation**, **Playtests** et **Assistant**. Données, Réseau et
Live Director restent disponibles sous **Outils avancés**. Le profil Débutant commence avec les quatre
espaces essentiels et l'aide contextuelle. La [revue Creator de bout en bout](docs/acceptance/CREATOR_END_TO_END.md)
sert de procédure de qualification humaine.

## Construire le runtime Windows autonome

```powershell
.\scripts\Build-RuntimeDistribution.ps1 -Force
.\scripts\Verify-RuntimeDistribution.ps1 -RunSmoke
```

La distribution produite sous `artifacts/distribution/` se lance avec
`OpenRPG_Forge_Runtime.exe`, contient ses modules inspectables et un manifeste SHA-256. Les détails et
la création d'une archive ZIP sont dans [`docs/DISTRIBUTION.md`](docs/DISTRIBUTION.md).

## Créer le lanceur Windows d'un jeu

Dans **Distribution et conformité**, activez le profil **Lanceur du jeu · Windows x64**, choisissez
le titre, le branding, les menus et la capacité multijoueur, enregistrez puis cliquez sur
**Créer le lanceur du jeu**. Le même flux est disponible en CLI :

```powershell
.\scripts\Build-GameLauncherTemplate.ps1 -Force
.\scripts\Build-GameDistribution.ps1 -Workspace E:\MonJeu -OutputDirectory E:\Exports\MonJeu-windows-x64
```

Le dossier obtenu possède son propre `.exe` WPF single-file et une interface RPG cinématique : décor
personnalisable, brumes et braises animées, parallaxe, transitions, Accueil, Configuration et
Multijoueur. Le runtime, le `.orpmod` Client et chaque fichier restent couverts par le manifeste
SHA-256. Le solo et l'hébergement local autoritaire fonctionnent ; la connexion distante reste
volontairement désactivée tant que le transport réseau inter-processus n'est pas branché. Voir
[`docs/DISTRIBUTION.md`](docs/DISTRIBUTION.md).

Le kit procédural de la Crypte suit les [conventions d’assets](docs/ASSET_CONVENTIONS.md) : unités,
axes, pivots, nommage, formes de collision simples et traçabilité des licences.

## Objets sonores 3D

L'écran **Environnement de zone**, onglet **Objets sonores**, édite un émetteur canonique placé :
jeu de clips ordonné ou pondéré, tags, langue, loudness, aperçu, licence SPDX, provenance, hash,
politique one-shot/boucle/région/compte/événement, scripts et portée 3D. Un objet vide est sûr ; un
objet incomplet affiche un diagnostic stable avant application ou distribution.

Le runtime reconstruit les lecteurs 3D depuis ce document, sans stocker de nœud moteur dans le
domaine. Le host MCP expose `forge_get_audio_emitters`, filtrable par zone, UUID ou recherche, puis
un plan dry-run/apply/revert. ForgeScript expose uniquement `AudioEmitterRef`,
`audio_is_active` et la demande validée `audio_set_active`.

## Profil et commandes de build

Chaque workspace peut conserver un `build.profile.json` textuel et versionnable. L'absence du fichier
reste compatible et charge le profil sûr par défaut. L'écran **Scripts & build incrémental** permet de
prévisualiser puis d'appliquer ce profil par la même commande de domaine que les intégrations ; la
sauvegarde est atomique et les actions **Annuler / Rétablir** persistent l'état restauré.

Le profil décrit les emplacements stables `data/`, `scripts/`, `assets/`, `localization/`, `tests/`,
`packages/`, `docs/` et `.forge/`. Le cache `.forge/` est jetable et ignoré par Git ; les documents
canoniques restent textuels et les archives sont toujours produites vers une destination explicite.

L'onglet **Gouvernance** du même écran configure aussi le workflow branche/worktree et son ownership,
la CI, le catalogue versionné des cinq fixtures de référence, les versions SemVer Core/Kits/packages,
les versions de schéma séparées, le changelog et les règles ADR. Ces valeurs utilisent le même
`build.profile.json`, la même validation, le même dry-run et le même historique que le profil de
build. Les politiques détaillées sont dans `docs/governance/`, sans donner de shell arbitraire aux
outils MCP.

L'écran **Extensions runtime & UI**, onglet **QA Acceptance**, configure sans éditer de JSON les
profils/catégories de diagnostics, les sept familles de fixtures malveillantes, le tracker des 717
fonctions Aurora et les gates G0–G13. Ces valeurs appartiennent au profil canonique du module : la
même commande assure prévisualisation, validation localisée, persistance, annuler/rétablir et plans
MCP. L'audit ci-dessous est strictement en lecture seule sur le dépôt et le workspace :

```powershell
dotnet run --project tools/Forge.Cli -- quality acceptance <repository> <workspace>
```

```powershell
dotnet run --project tools/Forge.Cli -- build-profile set-default <workspace>
dotnet run --project tools/Forge.Cli -- validate <workspace>
dotnet run --project tools/Forge.Cli -- diff <workspace>
dotnet run --project tools/Forge.Cli -- references <workspace>
dotnet run --project tools/Forge.Cli -- test <workspace>
dotnet run --project tools/Forge.Cli -- build <workspace> <destination.orpmod>
dotnet run --project tools/Forge.Cli -- pack <workspace> <destination.orpmod>
dotnet run --project tools/Forge.Cli -- migrate <workspace>
```

Toutes ces commandes rendent une sortie structurée et des codes de sortie stables : `0` pour succès,
`1` pour validation ou exécution refusée, `2` pour une utilisation invalide. `build` effectue en plus
un second assemblage isolé et compare les octets. Un export inchangé réutilise l'archive existante au
lieu de la réémettre. Le host MCP expose les opérations bornées équivalentes
`GetBuildProfile`, `ValidateBuild`, `PlanBuild`, `BuildModule` et `TestBuild`, sans shell arbitraire.

## Vérifier tout le dépôt

```powershell
.\scripts\Verify.ps1
```

Ce script exécute :

- restore NuGet verrouillé ;
- vérification de format ;
- build Release ;
- tests .NET ;
- tests Python et handshake MCP JSON-RPC réel ;
- smoke test de l’application de création dans le moteur 3D ;
- export/validation `.orpmod` ;
- scénario runtime headless complet piloté par l'agent MCP ;
- export, validation et rechargement headless de Forgotten Forest avec ses assets gouvernés ;
- sauvegarde dans un premier processus puis reprise/fin de quête dans un second.

Voir [`docs/ENVIRONMENT.md`](docs/ENVIRONMENT.md) pour les versions locales utilisées.

## Premier objectif produit

La première tranche verticale est **Crypte des Cendres** : une petite zone jouable et exportable qui prouve le domaine versionné, l’édition 3D, le runtime séparé, ForgeScript, le format `.orpmod`, un scénario headless et une mutation IA prévisualisable puis annulable.

Tout chantier qui ne rapproche pas directement cette tranche verticale doit être justifié avant d’être entrepris.
