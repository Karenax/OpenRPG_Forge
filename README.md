# OpenRPG Forge

OpenRPG Forge est un projet de moteur, runtime et toolset libre pour créer des RPG 3D modulaires. Il vise une expérience de création accessible, un domaine RPG indépendant du moteur, des modules partageables et une assistance IA facultative et transactionnelle.

Le dépôt contient désormais un **MVP fonctionnel de la Crypte des Cendres** : solution C# compilable,
domaine versionné, application de création **Forge**, runtime 3D jouable, ForgeScript natif, archives
`.orpmod`, sauvegarde/reprise et host MCP transactionnel. La commande `scripts/Verify.ps1` valide
l’ensemble localement. Les espaces auteur avancés décrits dans la cible Aurora 2026 restent la phase
suivante du produit.

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

Le dépôt fournit un [graphe d’architecture déterministe](docs/architecture/README.md), généré
localement sans dépendance externe. Avant une évolution transversale, obtenir les fichiers utiles
avec `python scripts/architecture_graph.py query "<module ou symbole>" --format paths`. Après une
modification indexée, exécuter `generate`, puis `check` avant de terminer.

## Lancer le prototype

Sous Windows, depuis la racine du dépôt :

```powershell
.\scripts\Run-Prototype.ps1
```

Commandes :

- `ZQSD`, `WASD` ou flèches : déplacement ;
- `E` : parler, ramasser la clé ou ouvrir la porte ;
- `Espace` : attaquer.
- `F5` / `F9` : sauvegarder / reprendre le checkpoint rapide.

Le parcours actuel permet de parler à Elyra, récupérer la Clé de braise, ouvrir la porte, combattre la Sentinelle puis le Gardien des Cendres, déclencher sa phase 2 et terminer la quête.

## Ouvrir le toolset

```powershell
.\scripts\Open-Toolset.ps1
```

L’application ajoute l’écran principal **Forge**. Elle ouvre un dossier de projet Forge, affiche ses
diagnostics, reconstruit un aperçu 3D et le sauvegarde atomiquement. Une palette permet de placer,
déplacer, tourner et supprimer des entités sur la grille, avec annuler/rétablir ; la Crypte des
Cendres est ouverte par défaut.

Le bouton **Nouveau module** crée un workspace `0.9.0` dans un dossier dédié avec son identité, une
première zone, un sol provisoire et un point de départ immédiatement éditables. Un dossier existant
non vide est toujours refusé.

Le bouton **Modifier les propriétés** personnalise l’instance sélectionnée sans dupliquer ni modifier
son blueprint. L’inspecteur gère des valeurs texte, booléennes, entières et numériques, puis applique
le changement par le bus de commandes avec révision et annuler/rétablir.

Le bloc **Test du module** permet de choisir un point de départ et de lancer l’état courant sans le
sauvegarder : Forge produit un snapshot temporaire, ouvre le runtime, affiche son rapport puis
supprime le snapshot. La direction de l’interface complète est décrite dans
[`docs/UX_AURORA_2026.md`](docs/UX_AURORA_2026.md).

L’espace **Dialogues** édite les nœuds, les choix, leurs conditions typées, leurs actions et leurs
textes localisables, avec aperçu immédiat. Dialogues et quêtes sont stockés dans le projet `0.6.4`,
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
- sauvegarde dans un premier processus puis reprise/fin de quête dans un second.

Voir [`docs/ENVIRONMENT.md`](docs/ENVIRONMENT.md) pour les versions locales utilisées.

## Premier objectif produit

La première tranche verticale est **Crypte des Cendres** : une petite zone jouable et exportable qui prouve le domaine versionné, l’édition 3D, le runtime séparé, ForgeScript, le format `.orpmod`, un scénario headless et une mutation IA prévisualisable puis annulable.

Tout chantier qui ne rapproche pas directement cette tranche verticale doit être justifié avant d’être entrepris.
