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
4. lire [`docs/ADR/ADR-0002-cadle-upstream-reference.md`](docs/ADR/ADR-0002-cadle-upstream-reference.md) ;
5. lire [`docs/ADR/ADR-0003-cadle-qualification-lock-overlay.md`](docs/ADR/ADR-0003-cadle-qualification-lock-overlay.md) ;
6. lire les décisions Gate 0 [`ADR-0004`](docs/ADR/ADR-0004-gate0-draw-call-telemetry-exception.md), [`ADR-0005`](docs/ADR/ADR-0005-studio-web-confirmed-gate0.md) et [`ADR-0006`](docs/ADR/ADR-0006-remote-ci-exception-gates-0-4.md) ;
7. consulter [`docs/planning/MIGRATION_ROADMAP.md`](docs/planning/MIGRATION_ROADMAP.md) ;
8. consulter [`docs/migration/LEGACY_REUSE_MATRIX.md`](docs/migration/LEGACY_REUSE_MATRIX.md) avant de reprendre une capacité de `main`.

`PROJECT_CONTEXT.md` est la source de vérité narrative de cette branche.

## État actuel

Gate C et Gate 0 sont passées. Le workspace Rust épinglé, le runtime Bevy natif, le viewport
Bevy/WebGPU WASM et le Studio Tauri 2 + Leptos utilisent désormais la même scène Forge versionnée.
Les preuves locales reproductibles et le rapport sont dans
[`docs/evidence/gate0/LOCAL_VALIDATION.md`](docs/evidence/gate0/LOCAL_VALIDATION.md) et
[`docs/reports/GATE_0_REPORT.md`](docs/reports/GATE_0_REPORT.md).

La gate active est **Gate 2 — Éditeur de monde**, autorisée sous
[ADR-0011](docs/ADR/ADR-0011-gate2-entry-with-gate1-reservations.md). Gate 1 est acceptée comme
prototype avec réserves, pas entièrement qualifiée (43/55). Gate 2 atteint **26/43** après le
placement gouverné d’assets R2-027 ; la prochaine tranche est **R2-028, picking**. Les revues
visuelles humaines de Gate 0 restent explicitement différées par ADR-0005. La CI GitHub distante est
`NOT RUN` à cause du blocage de
facturation du compte ; ADR-0006 autorise jusqu’à Gate 4 le passage sur preuves locales sans jamais
présenter cette CI comme verte. Cadle reste une référence locale : aucun de ses assets n’est copié ou
publié dans Forge.

## Référence historique

La référence C#/Godot initiale est la branche `main` au commit :

```text
a7f4a3f5dd0eeff1dc060ea1237b1d7d650b7ca0
```

Cette référence sert à extraire les exigences et les données utiles. Elle ne devient jamais une
dépendance d’exécution de la nouvelle architecture.

## Référence exécutable

Cadle sert de base de travail locale et de donneur de techniques pour le terrain, la végétation, le
rendu et le harness. Il ne devient pas le runtime produit : Forge reste Rust + Bevy + wgpu/WebGPU et
doit compiler, tester et fonctionner sans le clone Cadle.
