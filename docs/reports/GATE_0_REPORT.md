# Rapport Gate 0 — Fondation native, WASM et Studio

Date : 30 août 2026
Branche : `Rust`
Commit de code qualifié : `528a909738bde92a8831e5b41ba185ab35ffef7f`
Verdict : **NON PASSÉE — ARRÊT AVANT GATE 1**

## Résultat exécutif

La fondation Gate 0 fonctionne localement et depuis un clone propre Windows : huit packages Cargo,
runtime Bevy natif, viewport Bevy/WASM WebGPU, frontend Leptos, shell Tauri, scène JSON partagée,
sélection typée et preuves reproductibles. Le replay final a passé 27 tests, formatage, Clippy strict,
WASM, Trunk, Tauri, `cargo-deny`, RustSec et le runtime automatisé sans `upstream/cadle/` ni correction
manuelle.

Deux défauts de reproductibilité découverts pendant le premier clone propre ont été corrigés avant la
qualification finale :

- Tauri normalise désormais un `NO_COLOR=1` hérité vers la valeur booléenne acceptée par Trunk ;
- le runtime crée les parents de sortie absents, supprime les preuves périmées et refuse un succès si
  la capture demandée n'existe pas ou est vide.

Ces résultats ne suffisent pas à passer la gate. Les blocages formels restants sont :

- aucun run CI distant Windows, Linux ou WASM : le workflow et les commits Rust ne sont pas publiés ;
- aucune revue visuelle humaine consignée ;
- aucune décision formelle « Studio web confirmé » ou « fallback natif » ;
- aucun vrai compteur d'appels GPU, distinct des deux draws logiques de la scène ;
- aucune fenêtre statistique qualifiée pour Chrome, Edge et le Studio ;
- le chemin WebGPU indisponible et le refus effectif d'une permission Tauri non accordée ne sont pas
  exécutés.

**Gate 1, Gate 2, Gate 3 et Gate 4 n'ont pas commencé.** L'ordre imposé par `AGENTS.md` interdit de
commencer Gate 1 tant que Gate 0 n'est pas passée.

## Commits du lot

```text
7d529a862aba7f5cf32d2b4111042c73debff02d
feat(gate0): establish Rust runtime and Studio foundation

528a909738bde92a8831e5b41ba185ab35ffef7f
fix(gate0): harden reproducible proof outputs
```

Les documents et l'enveloppe de preuve suivis sont :

- `docs/evidence/gate0/LOCAL_VALIDATION.md` ;
- `docs/evidence/gate0/BROWSER_STUDIO_VALIDATION.md` ;
- `docs/evidence/gate0/NATIVE_RUNTIME.visual-proof.json` ;
- `docs/evidence/gate0/VISUAL_PROOF.schema.json` ;
- `docs/evidence/gate0/SCENE_PROOF.schema.json` ;
- `docs/evidence/gate0/SUPPLY_CHAIN.md`.

Les médias et ledgers bruts restent locaux et ignorés. Aucun asset Cadle n'est copié dans Forge.

## Qualification en clone propre

Le clone `--no-local --single-branch --branch Rust` a été placé exactement à `528a9097`, avec un
statut Git vide avant et après qualification. `upstream/cadle/` était absent et zéro fichier
`upstream/cadle/**` était suivi.

| Contrôle | Résultat |
|---|---|
| `CARGO_BUILD_JOBS=1; ./scripts/Test-Rust.ps1` | PASS |
| formatage et Clippy strict | PASS |
| tests workspace | 27 passés, 0 échec |
| WASM, Clippy WASM et Trunk release | PASS |
| `cargo deny` | PASS : advisories, bans, licences, sources |
| politique RustSec | PASS : 0 vulnérabilité, 20 avertissements, 1 blocage distribution |
| `NO_COLOR=1; cargo tauri build --no-bundle` | PASS sans correction externe |
| runtime release automatisé | PASS, sortie 0 |
| création automatique du dossier de preuve absent | PASS |
| capture et métriques non vides | PASS |
| statut Git final | vide |

Marqueurs exacts :

```text
FORGE_RUSTSEC_POLICY_PASS vulnerabilities=0 warnings=20 distribution_blockers=1
FORGE_RUST_VALIDATION_PASS wasm=True supply_chain=True
```

Le hash du document partagé est identique en natif et en WASM :
`0ea53d539067f399e7e76678532144955f35e5a15e95d11becf3a174843c67df`.

## Preuve runtime native propre

La commande release a créé elle-même le dossier `artifacts/gate-0/final-528a909`, auparavant absent,
puis a capturé la frame 180 et quitté après la frame 240.

| Artefact local ignoré | Taille | SHA-256 |
|---|---:|---|
| `forge_runtime.exe` | 73 513 984 octets | `b5654c52a27920ffec7516b36231b0ddab6ad04b1d6b47b4e452329634a5184e` |
| `native-runtime.png` | 1 027 592 octets, 1920 × 1080 | `0e3a206d2f984c0b42927801df3b2d54664a6ef13da76852a6008dcb5a332061` |
| `native-runtime-metrics.json` | 3 581 octets | `225bb327f1311376d84d62c97723ebe0857b6371e6d67bc9c4b1c453563af950` |

| Mesure post-warm-up | Valeur |
|---|---:|
| échantillons | 120 |
| durée | 454,8433 ms |
| moyenne | 3,7904 ms / 263,83 FPS |
| p50 / p95 / p99 | 2,3067 / 8,0712 / 8,7186 ms |
| maximum | 10,5704 ms |
| 1 % low | 103,69 FPS |
| hitches > 16,7 ms / > 33,3 ms | 0 / 0 |

GPU observé : AMD Radeon RX 7800 XT, Vulkan, pilote `26.8.1 (LLPC)`. La capture montre le sol vert,
le cube bleu centré et l'overlay lisible, sans corruption. Cette inspection est automatisée ;
`NATIVE_RUNTIME.visual-proof.json` conserve donc honnêtement :

```text
review.status = uninspected
review.method = automated_then_human
validation.overall = partial
```

## Studio et navigateurs

Le bundle 528 a été chargé dans Chrome et Edge. Chrome a confirmé WebGPU, le document partagé, le
monde initialisé une fois et la surface Studio ; la reprise de contrôle pour la capture et la
sélection a expiré. Edge a confirmé en plus la capture visuelle, la sélection du cube, l'EntityId
`ae91d914-98e9-52d4-80d1-4fddc482988d`, un rerender sans reconstruction du monde et l'absence de
warning ou d'erreur console.

Le shell Tauri exact a été reconstruit dans le clone propre avec le wrapper Trunk :

| Artefact | Taille | SHA-256 |
|---|---:|---|
| `forge-studio.exe` | 16 304 640 octets | `4bb12eef7841b9bc4247b6cc12ea4012aa241d75c31f75dea1d43edc2a77efb0` |
| `forge_studio_bg.wasm` | 34 879 512 octets | `d307e835f6aede2fb8cdc5b0939d2219a54354b79def6fb09f65805779086a28` |
| `forge_studio.js` | 181 150 octets | `4751935be971a12ee33840bb93f9e39a1e92bce10d964a0e6b5c23d864399ecb` |

La validation Tauri interactive complète avait été réalisée sur le commit de fondation `7d529a86` :
WebGPU, sélection hiérarchie et picking, IPC Rust typé et trois rerenders sans reconstruction. Le fix
`528a9097` ne change pas ces chemins fonctionnels, mais la fenêtre Tauri finale n'a pas reçu une
nouvelle capture commit-bound. `R0-059` reste donc ouvert.

## Chaîne d'approvisionnement

`cargo deny` passe. RustSec ne signale aucune vulnérabilité connue au lockfile, mais conserve vingt
avertissements documentés. `RUSTSEC-2024-0429` (`glib 0.18.5`, `unsound`) demeure un blocage explicite
de distribution Linux.

La licence du code Forge doit aussi être tranchée par le propriétaire : les manifests déclarent
`MIT OR Apache-2.0`, tandis que `PROJECT_CONTEXT.md` exprime une intention PolyForm Noncommercial.
Ce conflit et `glib` bloquent la distribution, sans être à eux seuls la cause technique du refus de
passage Gate 0.

## Conditions de passage Gate 0

Toutes les conditions sont nécessaires.

| Condition | Verdict | Motif |
|---|---|---|
| clone propre compile sans correction manuelle | PASS LOCAL | Test-Rust, Tauri et runtime passent à `528a9097`, arbre propre |
| runtime natif lancé | PASS LOCAL | capture et ledger propres, schéma valide |
| build WASM WebGPU lancé | PASS LOCAL | check, Clippy et bundle Trunk passent |
| Studio Tauri/Leptos lancé avec canvas | PASS LOCAL | session Tauri fonctionnelle du socle et build final propre |
| même document source visible sur les deux cibles | PASS LOCAL | hash partagé exact |
| sélection simple partagée | PASS LOCAL | hiérarchie/picking vers inspecteur et IPC Rust |
| tests et CI verts | **ÉCHEC GATE** | tests locaux verts, zéro run GitHub Actions Rust |
| aucune règle métier en JavaScript | PASS STATIQUE | aucune source JS/TS métier suivie |
| aucune dépendance à `main` à l'exécution | PASS STATIQUE/DYNAMIQUE | branche orpheline, clone propre sans `main` ni Cadle |
| capture et rapport inspectés | **ÉCHEC GATE** | inspection automatisée seulement, revue humaine absente |
| décision Studio web ou fallback natif | **OUVERT** | Studio web viable localement, décision formelle non enregistrée |

## Verdict et arrêt

Gate 0 reste **NON PASSÉE**. `origin/Rust` est encore au commit
`3f0e9e2d7575c53f9f57ee20d51cdfb580cbf526`, le workflow Rust n'y existe pas et GitHub Actions
retourne zéro run pour cette branche. Aucun push n'a été effectué sans ordre explicite.

La prochaine progression autorisée reste dans Gate 0 : publication explicitement autorisée pour
obtenir la CI, revue humaine des captures et décision Studio. Jusqu'à leur résolution, commencer
Gate 1–4 violerait la roadmap.
