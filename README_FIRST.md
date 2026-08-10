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
   - `docs/planning/COMPLETE_FUNCTION_BACKLOG.md`
   - `tracking/project/AURORA_PARITY_COVERAGE.csv`
   - les ADR de `docs/ADR/`
5. Dans le dépôt existant, suivre le gate et la prochaine tâche de `STATUS.md` ; le **Prompt 1 —
   Initialisation contrôlée** de `PROMPTS_CODEX_HERMES.md` ne sert qu’à recréer un dépôt vide.
6. Ne pas demander à Codex de construire tout le produit en une seule session.

Codex lit automatiquement `AGENTS.md` lorsqu’il travaille dans le dépôt. Le grand contexte reste dans `MEGACONTEXT.md` afin de ne pas gonfler les instructions persistantes.

## État courant du prototype

Le paquet de démarrage a maintenant produit un MVP solo fonctionnel et les tranches verticales G0 à
G13 sont fermées. Cette fermeture ne vaut pas parité fonctionnelle complète : le tracker compte
207 exigences `Tested` sur 717. Les 510 exigences restantes sont couvertes une seule fois par le
plan exhaustif, réparti en 187 lots `FC-*` de une à cinq fonctions. Le programme P0 est explicitement
actif : 191 fonctions non closes dans les 50 lots `R001` à `R050`, toujours exécutés un par un avec
audit sémantique et preuves avant d’ouvrir le suivant.

La source de vérité de l’avancement est `STATUS.md`. La direction d’interface et le contexte
graphique sont dans `docs/UX_AURORA_2026.md`. Le chantier Asset Vault/Forgotten Forest, sa cible
visuelle et l’état du prototype extérieur sont documentés dans `docs/assets/` et `docs/progress/`.

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
- `docs/planning/COMPLETE_FUNCTION_BACKLOG.md` : couverture exhaustive en petits lots numérotés.
- `tracking/project/AURORA_PARITY_COVERAGE.csv` : statut et preuve de chacune des 717 exigences.
- `PROMPTS_CODEX_HERMES.md` : prompts de lancement et de session.
- `docs/ENGINE_COMPARISON.md` : comparaison des moteurs.
- `docs/ARCHITECTURE.md` : architecture cible.
- `docs/FORGESCRIPT.md` : langage de scripts destiné aux créateurs.
- `docs/MODULE_FORMAT.md` : format de module.
- `docs/MCP_AI_ARCHITECTURE.md` : intégration IA et MCP.
- `docs/ROADMAP.md` : phases du produit.
- `docs/AURORA_CAPABILITY_DELIVERY_PLAN.md` : gates et ordre de livraison post-MVP.
- `docs/UX_AURORA_2026.md` : promesse créateur, identité visuelle et interactions validées.
- `docs/assets/OPENRPG_FORGE_BIBLIOTHEQUE_ASSETS_ET_MAP_REELLE.md` : contexte graphique, Asset Vault et carte Forgotten Forest.
- `docs/progress/ASSET_AND_MAP_IMPLEMENTATION_STATUS.md` : état prouvé du pipeline et du prototype extérieur.
- `docs/ADR/` : décisions architecturales verrouillées.

## Tranche verticale de lancement — atteinte

La première réussite attendue n’était pas « un moteur complet », mais une tranche verticale appelée
**Crypte des Cendres**. Elle est maintenant validée ; cette liste reste le rappel de son périmètre :

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

La suite est pilotée par les gates post-MVP de `STATUS.md` et
`docs/AURORA_CAPABILITY_DELIVERY_PLAN.md`, sans annoncer une parité complète avant ses preuves.
