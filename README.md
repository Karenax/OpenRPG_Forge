# OpenRPG Forge

OpenRPG Forge est un projet de moteur, runtime et toolset libre pour créer des RPG 3D modulaires. Il vise une expérience de création accessible, un domaine RPG indépendant du moteur, des modules partageables et une assistance IA facultative et transactionnelle.

Le dépôt contient désormais deux **prototypes fonctionnels** : la Crypte des Cendres et la carte
extérieure Forgotten Forest. La solution C# compilable fournit
domaine versionné, application de création **Forge**, runtime 3D jouable, ForgeScript natif, archives
`.orpmod`, sauvegarde/reprise et host MCP transactionnel. La commande `scripts/Verify.ps1` valide
l’ensemble localement. Forgotten Forest ajoute terrain canonique, caméra orbitale, 357 instances de
biome, 22 modèles GLB CC0 gouvernés, rivière animée, navigation de surface, quête, combat et export autonome. Les espaces auteur avancés
décrits dans la cible Aurora 2026 restent la phase suivante du produit.

Le lancement utilise un splash **OpenRPG_Forge** dédié, commun au runtime et à l’application de
création. La fenêtre de l’application porte également le titre **OpenRPG_Forge**. Le visuel peut
être régénéré avec `scripts/Generate-Branding.ps1`.

## Sources de vérité

Lire dans cet ordre avant toute modification :

1. [`AGENTS.md`](AGENTS.md)
2. [`MEGACONTEXT.md`](MEGACONTEXT.md)
3. [`STATUS.md`](STATUS.md)
4. [`TASKS_MVP.md`](TASKS_MVP.md)
5. les ADR applicables dans [`docs/ADR/`](docs/ADR/)

Le fichier [`README_FIRST.md`](README_FIRST.md) est le document d’accueil du paquet initial. Le méga-contexte reste la référence complète pour la vision et l’architecture.

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

Le bouton **Nouveau module** crée un workspace `1.16.0` dans un dossier dédié. L’assistant demande le
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
[`docs/UX_AURORA_2026.md`](docs/UX_AURORA_2026.md).

L’espace **Dialogues** édite les nœuds, les choix, leurs conditions typées, leurs actions et leurs
textes localisables, avec aperçu immédiat. Dialogues et quêtes sont stockés dans le workspace `1.16.0`,
mutés par commandes transactionnelles et consommés directement par le runtime de la Crypte.

L’espace **Validation** configure Editor, Client, DedicatedServer, Test et Shipping, gère le registre
des dépendances et prévisualise les licences ou provenances qui bloqueraient une distribution. Le
bouton **Exporter .orpmod** sauvegarde, construit le profil choisi, génère le NOTICE puis valide
l'archive partageable. Le host MCP
officiel fournit résumé, révision, diagnostics, plans de dialogue prévisualisables/réversibles,
compilation ForgeScript et agent de test sans exposer de shell. Le parcours complet est détaillé dans
le [guide créateur](docs/CREATOR_GUIDE.md).

Le kit procédural de la Crypte suit les [conventions d’assets](docs/ASSET_CONVENTIONS.md) : unités,
axes, pivots, nommage, formes de collision simples et traçabilité des licences.

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
