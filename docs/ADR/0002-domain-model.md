# ADR 0002 — Domaine versionné indépendant de Godot

- Statut : accepté
- Date : 1 août 2026

## Contexte

Des scènes Godot utilisées comme source de vérité coupleraient les modules au moteur, compliqueraient validation, migration, exécution headless et inspection par les outils IA.

## Décision

La source de vérité d’un projet est un modèle de domaine C# pur, persistant et versionné.

- `Forge.Domain` et `Forge.Application` ne référencent aucun type Godot.
- Les liens persistants utilisent des identifiants opaques et stables, jamais des noms lisibles.
- Les valeurs spatiales sont représentées par des types propres (`Vector3Value`, `QuaternionValue`, `TransformValue`).
- Les définitions réutilisables sont séparées des instances placées.
- Godot reconstruit ses nœuds et scènes à partir du modèle ; ces nœuds ne deviennent pas l’état métier.
- Toute sérialisation persistante possède une version et des tests de round-trip ou de migration.

## Conséquences

- Le domaine peut être validé et testé sans lancer Godot.
- L’éditeur, le runtime, le serveur et MCP peuvent partager les mêmes contrats.
- Les conversions vers Godot sont explicites et localisées dans des adapters.
- La discipline initiale est plus stricte, mais elle évite un couplage difficile à retirer plus tard.
