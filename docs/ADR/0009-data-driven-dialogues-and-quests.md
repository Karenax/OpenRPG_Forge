# ADR 0009 — Dialogues et quêtes data-driven

- Statut : accepté
- Date : 2 août 2026

## Contexte

La première version jouable de la Crypte pilotait la conversation d’Elyra, la progression de quête
et l’ouverture de la porte avec des branches propres au runtime. Cette logique empêchait un auteur
de créer un autre module uniquement depuis les données et rendait l’édition, la simulation et les
futurs outils MCP incohérents.

## Décision

- Les dialogues sont des graphes immuables identifiés, composés de nœuds, choix, conditions et
  actions typés, avec texte localisable et fallback lisible.
- Les quêtes sont des machines à états identifiées, avec transitions conditionnelles, actions et
  entrées de journal localisables.
- Le domaine valide les références, l’accessibilité des nœuds et états, les terminaux et les
  transitions ; il peut simuler les parcours sans dépendance au moteur.
- Toute mutation passe par `set_dialogue` ou `set_quest`, avec révision, dry-run, patch, inverse,
  audit et historique.
- Le format projet courant devient `0.5.0`. `0.4.0` introduit les dialogues et `0.5.0` les quêtes ;
  les versions antérieures sont migrées avec des collections vides.
- Le runtime consomme ces agrégats par des évaluateurs de conditions et des exécuteurs d’actions
  génériques. La Crypte ne contient plus de progression narrative spécifique hors de ses données.

## Conséquences

- Les mêmes documents alimentent l’éditeur, la validation, le playtest et le runtime.
- L’ajout de nouveaux types de conditions ou d’actions exige un contrat explicite et des tests.
- Les extensions ForgeScript pourront compléter ce vocabulaire après stabilisation de la sandbox,
  sans être nécessaires aux dialogues et quêtes ordinaires.
- L’éditeur Quêtes complet reste une surface produit à ajouter ; le contrat persistant et la machine
  à états sont déjà disponibles pour cette interface.
