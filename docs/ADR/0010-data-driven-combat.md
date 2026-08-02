# ADR 0010 — Combat RPG data-driven minimal

- Statut : accepté
- Date : 2 août 2026

## Contexte

Le combat initial de la Crypte stockait les points de vie et dégâts directement dans le runtime.
Cette représentation suffisait à prouver la boucle jouable, mais pas à créer des créatures, pouvoirs
et récompenses réutilisables depuis un module.

## Décision

- Un `CombatProfile` optionnel appartient au blueprint et regroupe statistiques, capacités et butin.
- Les statistiques minimales sont santé, ressource, attaque, défense, faction et équipe.
- Une capacité identifiée sépare coût, cooldown, ciblage, effets de règle et clé de signal visuel.
- `CombatResolver` applique les règles dans le domaine pur avec une horloge et un jet de butin fournis
  par l’appelant ; le runtime ne fait que relier entités et rendu.
- Les objets de butin sont des slugs et quantités portables. Leur modèle d’inventaire complet pourra
  enrichir ce contrat sans rendre le résolveur dépendant du moteur.
- Le format projet courant devient `0.6.0`. Les documents `0.5.0` et antérieurs sont migrés avec des
  profils de combat absents.

## Conséquences

- Santé, ressource, factions, équipes, capacités et butin de la Crypte proviennent du projet.
- Les règles sont testables en mémoire, y compris coût, cooldown, cible invalide, défense, mort et loot.
- Les animations et particules restent des signaux visuels adressables, pas des effets de gameplay.
- Le futur serveur autoritaire pourra réutiliser exactement le même résolveur.
