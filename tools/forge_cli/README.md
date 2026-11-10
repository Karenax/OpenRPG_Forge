# forge_cli

Outil reproductible des documents Forge et de la passerelle Aurora/NWN.

## Documents Forge

```text
forge_cli validate <scene-proof.json>
forge_cli hash <scene-proof.json>
forge_cli generate-proof <new-scene-proof.json>
forge_cli validate-world-sketch <world-sketch.json>
forge_cli compile-world-sketch <world-sketch.json> <new-output-directory>
```

`validate` parse et valide le schéma métier avant d'afficher son empreinte canonique. `hash` refuse
également un document invalide : une empreinte Forge ne certifie jamais des données non validées.
`generate-proof` produit l'exemple procédural canonique et refuse d'écraser un fichier existant.

## Passerelle Aurora/NWN

```text
forge_cli aurora-audit <bundle>
forge_cli aurora-import <bundle> <new-package-directory>
forge_cli aurora-visit <bundle> [--report visit.json] [--map visit.svg]
forge_cli aurora-scenario <bundle> [--report m3-report.json]
forge_cli aurora-gameplay <bundle> [--seed 2026] [--profile profile.json] [--report m4-report.json]
forge_cli aurora-m5 <bundle> [--seed 2026] [--report m5-report.json] [--trace m5-trace.json]
forge_cli aurora-diff-oracle <expected-m5.json> <actual-m5.json> [--report diff.json]
```

- `aurora-audit` valide le bundle sans écrire.
- `aurora-import` projette le bundle en package Forge.
- `aurora-visit` exécute la visite M2 simplifiée.
- `aurora-scenario` exécute le scénario NCS M3.
- `aurora-gameplay` exécute le profil NWN et le combat d20 M4.
- `aurora-m5` exécute la qualification M5, migre la sauvegarde M4 → M5 et écrit une trace
  reproductible.
- `aurora-diff-oracle` compare deux rapports M5 et renvoie un code 2 si une divergence est détectée.

Le bundle de test est
`crates/forge_aurora_bridge/tests/fixtures/ab_proving.module-migration-v1`.
Les limites et guides sont dans `docs/aurora-bridge/M3_RUNTIME.md`,
`docs/aurora-bridge/M4_GAMEPLAY.md`, `docs/aurora-bridge/M5_QUALIFICATION.md` et
`docs/aurora-bridge/M5_USER_GUIDE.md`.
