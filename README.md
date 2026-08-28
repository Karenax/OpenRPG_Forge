# OpenRPG Forge

OpenRPG Forge est un projet de moteur, runtime et toolset à code source disponible pour créer des RPG
3D modulaires. Il vise une expérience de création accessible, un domaine RPG indépendant du moteur,
des modules partageables et une assistance IA facultative et transactionnelle.

Le dépôt contient un **module canonique** : la tranche verticale jouable Crypte des Cendres. La
solution C# compilable fournit domaine versionné,
application de création **Forge**, runtime 3D jouable, ForgeScript natif, archives `.orpmod`,
sauvegarde/reprise et host MCP transactionnel. La commande `scripts/Verify.ps1` valide l’ensemble
localement. Les outils génériques de terrain, biome forestier et gouvernance d’assets restent
disponibles sans imposer un module de démonstration extérieur. La tranche verticale G0 à G13 est
fermée et les 717 exigences Aurora sont actuellement au statut `Tested`. Le chantier actif est la
consolidation de la CI, de l’interface, de la distribution et de la qualité visuelle.

Le lancement utilise un splash **OpenRPG_Forge** dédié, commun au runtime et à l’application de
création. La fenêtre de l’application porte également le titre **OpenRPG_Forge**. Le visuel peut
être régénéré avec `scripts/Generate-Branding.ps1`.

## Licence

Le code propre à OpenRPG Forge est distribué sous
[PolyForm Noncommercial 1.0.0](LICENSE.txt), identifiant SPDX
`PolyForm-Noncommercial-1.0.0`. La licence autorise l'utilisation, la modification et la
redistribution pour les finalités non commerciales qu'elle définit ; elle n'accorde pas d'autorisation
pour un usage commercial. Les dépendances, composants tiers et assets conservent leurs licences et
notices propres.

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

## Génération procédurale gouvernée

Le host MCP peut désormais gérer des recettes versionnées, produire des previews déterministes et
mettre en file des jobs persistants pour les neuf étapes terrain → validation. Terrain3D et Godot
Flow sont des backends facultatifs derrière des adaptateurs ; un backend Godot natif conserve un
chemin fonctionnel hors ligne sans addon. Les opérations sont allowlistées, les chemins confinés au
workspace et les retouches humaines peuvent être verrouillées lors d'une régénération partielle.
Les formats persistés conservent leurs backends canoniques tandis que `providerKey` distingue
`terrain3d`, `godot-flow`, `gf-pcgodot` et `openrpg-native` pour les benchmarks et diagnostics.
Un job complet validé peut être promu depuis MCP ou la page **Génération de monde** de Studio : scène
provider-free, contrôle des révisions et empreintes, copie/manifeste atomiques, référence
`AreaWorldProfile` et inverse. Solaren exerce ce parcours jusqu'au chargement Runtime et au package
`.orpmod` avec une seconde zone de 5 904 instances, collision et navigation.

```powershell
./scripts/Install-WorldGenerationDependencies.ps1 -GodotProjectPath ./apps/Forge.Toolset -WhatIf
```

La simulation ci-dessus ne télécharge rien. Le guide complet, l'installation réelle, les limites et
le parcours Codex sont dans [`docs/world-generation/README.md`](docs/world-generation/README.md) ; la
qualification comparative est cadrée par `BACKEND_QUALIFICATION.md` et `QUALITY_BENCHMARK.md`.
La qualification locale Windows prouve le proxy comparatif et la tranche jouable Solaren sous Godot
4.7.1 avec les addons réels. Elle ne qualifie pas encore une bibliothèque artistique universelle,
les exports provider-free Linux/Windows ni la CI Linux.

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
le titre, le fond, la musique WAV facultative, les menus et la capacité multijoueur, enregistrez puis cliquez sur
**Créer le lanceur du jeu**. Le même flux est disponible en CLI :

```powershell
.\scripts\Build-GameLauncherTemplate.ps1 -Force
.\scripts\Build-GameDistribution.ps1 -Workspace E:\MonJeu -OutputDirectory E:\Exports\MonJeu-windows-x64
```

Le dossier obtenu possède son propre `.exe` WPF single-file et une interface RPG cinématique : décor
personnalisable, brumes et particules animées, parallaxe, transitions, musique avec Play/Stop et volume,
Accueil, Configuration et Multijoueur. Le runtime, le `.orpmod` Client et chaque fichier restent couverts par le manifeste
SHA-256. Le solo, l'hébergement autoritaire et la connexion Internet directe par invitation privée
`openrpg2:` fonctionnent avec TLS épinglé, jeton authentifié, contrôle du module/protocole et
réplication des avatars. Une tranche serveur exerce combat, inventaire, quête et checkpoint sans
accepter l'état calculé par le client. L'hôte doit toujours autoriser et rediriger le port TCP choisi ;
reconnexion, relais/NAT automatique et projection de toute une campagne restent ouverts. Voir
[`docs/DISTRIBUTION.md`](docs/DISTRIBUTION.md) pour l'export et
[`docs/MULTIPLAYER_INTERNET.md`](docs/MULTIPLAYER_INTERNET.md) pour l'hébergement, la connexion,
le serveur headless, les diagnostics et les limites de sécurité.

La distribution Windows versionnée sous `exe/` contient 515 fichiers et trois exécutables racine :
`OpenRPG_Forge_Studio.exe` pour créer les modules, `Solaren_Les_Vallees_Libres.exe` pour jouer au
package Client Solaren et `Cendres.exe` pour la régression interne de la Crypte. Leurs manifestes
couvrent respectivement 281, 227 et 224 fichiers. Les deux lanceurs utilisent des couples de manifestes
distincts afin de ne jamais croiser leurs modules ; la Crypte reste volontairement une fixture Editor,
pas une livraison Client/Publication. Les sous-dossiers `branding/`, `game/` et `runtime/` font partie
du produit et doivent rester à côté des exécutables. Cette distribution locale reste non signée.

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

Les voix de dialogue suivent un flux distinct mais les mêmes garanties : Studio et MCP importent un
fichier local MP3/WAV avec licence SPDX ou `LicenseRef`, provenance et SHA-256, puis appliquent un plan
recalculé vers un blob immuable. Workspace, package, conformité et runtime revalident chemin, hash et
structure ; le sous-titre reste le fallback accessible. Les quatre voix Solaren courantes sont des WAV
CC0-1.0 synthétiques et reproductibles, accompagnés d’un manifeste de génération. Le workflow auteur,
les trois outils MCP et la frontière de sécurité sont détaillés dans le
[guide des voix de dialogue](docs/DIALOGUE_VOICE.md).

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

## Vérifier au bon niveau

Pour une modification courante, produire puis exécuter un plan ciblé :

```powershell
.\scripts\Get-VerificationPlan.ps1 -ChangedPath <fichiers> -Stage Development
.\scripts\Invoke-TargetedVerification.ps1 -ChangedPath <fichiers> -Stage Development
```

Les profils `Core`, `Studio`, `Runtime`, `Assets`, `Network` et `Distribution` couvrent les jalons de
sous-système. Le portail transversal historique reste le profil `Full` et le comportement par défaut :

```powershell
.\scripts\Verify.ps1 -Profile Studio -Evidence ux2-nav-l -StudioSmokeScope Route
.\scripts\Verify.ps1 -Profile Full
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
- export, validation et rechargement headless de la Crypte des Cendres avec ses assets gouvernés ;
- sauvegarde dans un premier processus puis reprise/fin de quête dans un second.

Voir [`docs/ENVIRONMENT.md`](docs/ENVIRONMENT.md) pour les versions locales utilisées.
La politique de sélection et les seuils d'élargissement sont décrits dans
[`docs/planning/TARGETED_VERIFICATION.md`](docs/planning/TARGETED_VERIFICATION.md).

## Premier objectif produit

La première tranche verticale est **Crypte des Cendres** : une petite zone jouable et exportable qui prouve le domaine versionné, l’édition 3D, le runtime séparé, ForgeScript, le format `.orpmod`, un scénario headless et une mutation IA prévisualisable puis annulable.

Tout chantier qui ne rapproche pas directement cette tranche verticale doit être justifié avant d’être entrepris.
