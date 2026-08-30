# OpenRPG Forge — branche Rust/WebGPU

Cette branche est une réécriture indépendante et expérimentale d’OpenRPG Forge.

Elle a été créée le **30 août 2026** comme branche orpheline vide, sans ancêtre commun avec `main`.
Son objectif n’est pas de convertir mécaniquement le code C#/Godot existant, mais de reconstruire le
produit sur un socle **Rust + Bevy + wgpu/WebGPU**, en conservant les connaissances, les exigences,
les formats utiles et les assets gouvernés du projet historique.

## Priorité absolue

La première réussite attendue n’est pas la reproduction des centaines de fonctions du projet Godot.
La première réussite est une tranche verticale visuellement convaincante et performante :

- une vallée extérieure de 512 × 512 m ;
- terrain sculpté et texturé ;
- forêt dense et herbe animée ;
- eau, brouillard, lumière et météo ;
- personnage jouable à la troisième personne ;
- collisions ;
- édition directe de la carte ;
- sauvegarde, rechargement et passage immédiat en mode jeu ;
- cible de 60 FPS en 1080p sur un PC milieu de gamme de référence.

La reconstruction des fonctions RPG, du Creator, de ForgeScript, des modules, du MCP et du
multijoueur ne commence qu’après validation de cette base visuelle et technique.

## Documents obligatoires

Avant toute modification :

1. lire [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md) ;
2. lire [`AGENTS.md`](AGENTS.md) ;
3. lire [`docs/ADR/ADR-0001-rust-webgpu-greenfield.md`](docs/ADR/ADR-0001-rust-webgpu-greenfield.md) ;
4. consulter [`docs/planning/MIGRATION_ROADMAP.md`](docs/planning/MIGRATION_ROADMAP.md) ;
5. consulter [`docs/migration/LEGACY_REUSE_MATRIX.md`](docs/migration/LEGACY_REUSE_MATRIX.md) avant de reprendre une capacité de `main`.

`PROJECT_CONTEXT.md` est la source de vérité narrative de cette branche.

## État actuel

La branche contient uniquement sa fondation documentaire. Aucun moteur, runtime, éditeur ou format
n’est encore déclaré fonctionnel. Les commandes de compilation seront ajoutées avec le premier
workspace Cargo vérifié.

## Référence historique

La référence C#/Godot initiale est la branche `main` au commit :

```text
a7f4a3f5dd0eeff1dc060ea1237b1d7d650b7ca0
```

Cette référence sert à extraire les exigences et les données utiles. Elle ne devient jamais une
dépendance d’exécution de la nouvelle architecture.
