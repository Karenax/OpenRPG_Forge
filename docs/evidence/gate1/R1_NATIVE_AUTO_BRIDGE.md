# Gate 1 — Pont WorldSketch automatique en natif

2026-09-05, branche Rust locale non publiée. Gate 1 reste ouverte.

## Implémenté

Le runtime conserve le document de pont non vide de la compilation WorldSketch déjà utilisée
pour le terrain et le transfère une seule fois au plugin existant. Aucun fichier intermédiaire,
seconde compilation, dépendance ou nouveau format canonique. Gate 0 et Visual Valley historique
conservent leur comportement. Une entrée explicite `--bridge-placement` reste prioritaire,
y compris en cas d'erreur : aucun fallback silencieux.

Le démarrage journalise désormais la présence effective du document, même sans chemin explicite.

## Réellement vérifié

Commande exécutée, sans `--terrain` ni `--bridge-placement` :

```text
forge_runtime --world-sketch-visual-valley --bridge-crossing-probe
```

Journal local : `E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-native-auto-bridge-20260905\probe.jsonl`.
Événement `bridge_crossing_probe_completed` du **18:48:35.539532 UTC** :

- succès, code de sortie 0, aucun ERROR et `runtime_errors_observed=false` ;
- un pont, 201 instances de mesh, un collider de tablier, deux de parapet, deux de support ;
- 42,5282 m parcourus pour 42,49999 m requis ;
- 518/518 ticks de traversée au sol, 435 en contact avec le tablier, zéro perte de contact ;
- bindings terrain, joueur et pont conservés, état final au sol ;
- hash pont `6295913b59a4627d8691bb9da5ad6ed946056b0f060c20fbddcba34eb4bf0936`,
  identique au dernier relevé Chrome ;
- hash terrain `c9000f6ab51b7d716511dd718924f5cd6403c749456620bef86bb2120f364930`.

Le harness général indique `automated_run=false` pour ce probe autonome : son verdict repose
sur son propre événement de traversée réussie, les contacts réels Rapier et le journal sans erreur,
pas sur une assimilation au harness de capture.

Validations ciblées : `cargo test -p forge_runtime --locked -q` **47 PASS** (46 unitaires,
une intégration), build runtime et Clippy runtime toutes cibles/features avec `-D warnings` PASS.
Les nouveaux tests couvrent la conservation exacte du pont compilé, sa consommation unique,
l'absence de pont historique et l'échec d'un chemin explicite absent sans fallback.
Un premier passage a signalé deux destructurations de tests incomplètes ; corrigées avec
assertion explicite de présence/absence du nouveau champ, puis tous les tests rejoués.
La suite workspace de 315 tests du lot précédent n'est pas présentée comme rejouée ici.

SHA-256 octets :

- runtime exécuté : `a7ecca1cc67683bf5d1ab236244cdabc9a3a56bde77ccea91650dd08eca7d854` ;
- journal : `eaf2cf43aaf4b2eaa9dcd06d003fa4d5d8f6fc19fa2e195c91dc4f602d363a8d`.

## Limites et suite prévue

Pas de changement de géométrie ou de matériau dans ce lot ; pas de nouvelle qualification
artistique. Traversée Chrome, navigation et validation humaine de Gate 1 restent ouvertes.
Aucune modification de source WorldSketch, aucun asset Cadle, commit ou push.
