# ADR 0006 — Nommage produit indépendant du moteur

Statut : accepté — 2 août 2026.

## Contexte

OpenRPG Forge doit présenter sa propre identité. Répéter le nom de la technologie sous-jacente dans
les écrans, guides et noms d’assemblies donne l’impression que le produit n’est qu’un habillage de
son moteur 3D.

## Décision

- Les surfaces créateur et la documentation produit utilisent **OpenRPG Forge**, **Forge Editor**,
  **runtime 3D** et **adaptateur moteur**.
- Le splash de lancement affiche **OpenRPG_Forge** dans le runtime comme à l’ouverture du projet
  d’édition.
- L’assembly de conversion est nommé `Forge.EngineAdapter`.
- Le nom du moteur reste autorisé dans les fichiers de dépendances, la configuration, les rapports
  d’environnement, les ADR techniques et les diagnostics d’installation qui exigent une référence
  exacte.
- Ce changement de terminologie n’autorise ni fork ni rebranding du moteur pour le MVP.

## Conséquences

L’identité visible est celle d’OpenRPG Forge et la frontière technique demeure explicite. Les
créateurs ne dépendent pas du vocabulaire interne du moteur, tandis que les mainteneurs conservent
les informations exactes nécessaires au build et au diagnostic.
