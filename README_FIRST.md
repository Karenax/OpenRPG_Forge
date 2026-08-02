# OpenRPG Forge — paquet de démarrage

Ce paquet est le contexte de lancement d’un projet provisoirement nommé **OpenRPG Forge**.

L’objectif n’est pas de copier Neverwinter Nights, Aurora, D&D, leurs règles, leurs contenus ou leurs marques. L’objectif produit est de recréer, avec une identité originale et des technologies libres, la promesse qui a rendu Aurora puissant :

- un moteur de RPG 3D moderne ;
- un outil de création de modules accessible ;
- des zones, créatures, objets, dialogues, quêtes, pouvoirs et scripts personnalisables ;
- un exécutable joueur séparé ;
- du multijoueur autoritaire à terme ;
- une IA profondément intégrée, mais toujours facultative ;
- des modules partageables, versionnés et contrôlables.

## Utilisation immédiate avec Codex

1. Créer un nouveau dossier Git.
2. Copier tout ce paquet à la racine.
3. Ouvrir le dossier dans Codex.
4. Demander à Codex de lire, dans cet ordre :
   - `AGENTS.md`
   - `MEGACONTEXT.md`
   - `STATUS.md`
   - `TASKS_MVP.md`
   - les ADR de `docs/ADR/`
5. Coller le **Prompt 1 — Initialisation contrôlée** de `PROMPTS_CODEX_HERMES.md`.
6. Ne pas demander à Codex de construire tout le produit en une seule session.

Codex lit automatiquement `AGENTS.md` lorsqu’il travaille dans le dépôt. Le grand contexte reste dans `MEGACONTEXT.md` afin de ne pas gonfler les instructions persistantes.

## Utilisation avec Hermes Agent

Créer un profil Hermes dédié au projet afin de séparer sa mémoire, ses sessions et ses compétences des autres usages. Ouvrir Hermes depuis la racine du dépôt : il pourra charger `AGENTS.md`.

Hermes est recommandé pour :

- conserver la continuité architecturale ;
- tenir les décisions, le statut et le backlog à jour ;
- préparer des tâches bornées ;
- gérer le lore, les règles, les dépendances de contenu et les procédures répétables ;
- déléguer au besoin une tâche de code à Codex.

Codex est recommandé comme implémenteur principal des tâches difficiles, notamment les plugins Godot, les tests, les refactorings, l’intégration native et les corrections de compilation.

## Fichiers essentiels

- `MEGACONTEXT.md` : contexte autonome à transmettre à un nouvel agent.
- `AGENTS.md` : règles permanentes du dépôt.
- `STATUS.md` : état réel et décisions encore ouvertes.
- `TASKS_MVP.md` : backlog ordonné de la première tranche verticale.
- `PROMPTS_CODEX_HERMES.md` : prompts de lancement et de session.
- `docs/ENGINE_COMPARISON.md` : comparaison des moteurs.
- `docs/ARCHITECTURE.md` : architecture cible.
- `docs/FORGESCRIPT.md` : langage de scripts destiné aux créateurs.
- `docs/MODULE_FORMAT.md` : format de module.
- `docs/MCP_AI_ARCHITECTURE.md` : intégration IA et MCP.
- `docs/ROADMAP.md` : phases du produit.
- `docs/ADR/` : décisions architecturales verrouillées.

## Principe de lancement

La première réussite attendue n’est pas « un moteur complet ». C’est une tranche verticale appelée **Crypte des Cendres** :

- une petite zone modulaire ;
- un joueur, un PNJ, une porte, un déclencheur, un ennemi et un boss ;
- un dialogue avec condition ;
- une quête ;
- un objet ;
- un pouvoir ;
- un script de boss ForgeScript ;
- sauvegarde et chargement ;
- lancement depuis l’éditeur ;
- export en module ;
- chargement du module par un exécutable joueur séparé ;
- une modification IA appliquée par transaction avec prévisualisation et annulation ;
- un test automatisé sans interface.

Tout développement qui ne rapproche pas directement cette tranche verticale doit être justifié avant d’être entrepris.
