# OpenRPG Forge — branche Rust/WebGPU

## État courant — Gate 1 clôturée le 12 septembre 2026

**PASS automatisé local sous ADR-0006, avec les dérogations acceptées.**
Dérogations saccades pendant les changements de qualité et kit : ADR-0013 ;
comparaison Godot/Cadle validée explicitement. 738 tests PASS, 2 ignorés
préexistants ; builds et replays natif/Studio PASS. Révision propre qualifiée
`7386e00a1d9e8531470b57bcd8da190636a9e390`. [Rapport de clôture](docs/evidence/gate1/GATE1_CLOSURE_20260912.md).
CI distante NOT RUN ; aucune publication ni qualification de distribution.
Le travail Gate 2 est suivi séparément et ne fait pas partie de ce snapshot.
Les états ouverts et attentes antérieurs ci-dessous sont historiques ;
ils ne doivent pas rouvrir les décisions soldées par ce rapport.


Cette branche est une réécriture indépendante et expérimentale d’OpenRPG Forge.

Le résultat final visé est de **créer un RPG avec l'IA puis retoucher précisément son monde, ses
personnages et son histoire dans le Studio, sans perdre ces corrections lors des régénérations**.
Le parcours complet va jusqu'au jeu, à la sauvegarde et à la distribution ; l'édition reste possible
sans IA. Les priorités et preuves attendues sont définies dans la vision de
[`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md) et déclinées dans la
[`roadmap`](docs/planning/MIGRATION_ROADMAP.md). Cette cible n'est pas une déclaration de capacités livrées.

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

La gate active est **Gate 1 — Visual Valley**. Les revues visuelles humaines de Gate 0 restent
explicitement différées par ADR-0005. La CI GitHub distante est `NOT RUN` à cause du blocage de
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
