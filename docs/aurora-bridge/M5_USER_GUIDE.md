# Guide utilisateur et mainteneur - passerelle Aurora/NWN (M5)

**Version :** 8 novembre 2026.
**Public :** utilisateur local et mainteneur du pont OpenNever → OpenRPG Forge.
**Portée :** scénario de compatibilité `ab_proving`. Ce guide ne promet pas qu'un module NWN
arbitraire est jouable.

## 1. Ce que fait la passerelle

OpenNever lit un module NWN:EE en lecture seule et produit un bundle
`module-migration-bundle@1.0.0`. OpenRPG Forge valide ce bundle, projette les zones, exécute un
sous-ensemble NCS, applique un profil de règles NWN séparé et peut sauvegarder/reprendre le
scénario.

La passerelle ne remplace pas Neverwinter Nights et ne lit pas directement les MOD/HAK/TLK dans
OpenRPG. Un bundle importé sans exécution est une **prévisualisation**, pas une preuve de
jouabilité.

## 2. Commandes principales

Depuis `E:\OpenRPG_Forge\OpenRPG_Forge-Rust` :

```powershell
# Audit du bundle sans écriture
cargo run -q -p forge_cli -- aurora-audit <bundle>

# Import en package Forge (lecture seule côté bundle)
cargo run -q -p forge_cli -- aurora-import <bundle> <nouveau-dossier>

# Visite M2 simplifiée (navigation, portes, transitions)
cargo run -q -p forge_cli -- aurora-visit <bundle> --report visit.json --map visit.svg

# Scénario M3 (NCS, dialogue, journal, objet, porte, combat scripté, reprise)
cargo run -q -p forge_cli -- aurora-scenario <bundle> --report m3-report.json

# Scénario M4 (profil NWN, combat d20, sorts, objets, IA, sauvegarde)
cargo run -q -p forge_cli -- aurora-gameplay <bundle> --seed 2026 --report m4-report.json

# Qualification M5 (migration de sauvegarde, trace, diff-oracle)
cargo run -q -p forge_cli -- aurora-m5 <bundle> --seed 2026 `
  --report m5-report.json --trace m5-trace.json

# Comparer deux rapports M5
cargo run -q -p forge_cli -- aurora-diff-oracle m5-report.json replay.json `
  --report m5-diff.json

# Comparer un log oracle NWN externe au rapport M4
cargo run -q -p forge_cli -- aurora-oracle-log nwn-combat.log m4-report.json `
  --output oracle-comparison.json

# Auditer des tables 2DA locales extraites en lecture seule
cargo run -q -p forge_cli -- aurora-2da <tables-dir> --report 2da-audit.json
```

Le bundle de test est
`crates/forge_aurora_bridge/tests/fixtures/ab_proving.module-migration-v1`.

## 3. Statuts à comprendre

| Statut | Sens |
| --- | --- |
| `importable` | le bundle est structurellement valide et lisible |
| `navigable` | le WOK/PWK/DWK produit une navigation utilisable |
| `scriptable` | les NCS du sous-ensemble sont parsés et exécutés |
| `playable` | un scénario de compatibilité explicite a été exécuté |
| `saveable` | une sauvegarde a été relue et reprise sans duplication |
| `partial` | une partie seulement est prouvée ; les limites sont listées |
| `unsupported` | la fonction n'est pas exécutée et le diagnostic est visible |

Un module peut être `importable` sans être `playable`. Le rapport M5 contient `trace`, `save`,
`migration` et `resumeIdempotent` ; ces champs sont les preuves à lire avant toute annonce.

## 4. Sauvegarde et reprise

- Schéma courant : `forge.aurora-m5-save@1`.
- Il embarque le schéma M4 (`forge.aurora-m4-save@1`) et le schéma M3
  (`forge.aurora-m3-save@1`).
- La migration M4 → M5 est explicite et testée ; un schéma inconnu est refusé.
- La sauvegarde ne modifie jamais une sauvegarde native Forge ou NWN.
- Une reprise restaure session, zones visitées, portes, journal, or, personnages, inventaire,
  sorts, effets, quête et RNG ; elle refuse un PV hors bornes, un objet inconnu ou un personnage
  dupliqué.
- Les anciennes sauvegardes NWN ne sont pas promises.

## 5. Diagnostics et traces

`aurora-m5` produit :

- `m5-report.json` : rapport complet, y compris le rapport M4 embarqué ;
- `m5-trace.json` : trace expurgée `event → script → API → mutation → receipt`, avec hash stable.

La trace ne contient ni chemin absolu ni secret. `aurora-diff-oracle` compare deux rapports M5 et
écrit les divergences par champ (`gold`, `journalEntries`, `questStatus`, `spells`, `combat.*`,
`trace.entriesSha256`). Une divergence renvoie un code de sortie 2 après avoir écrit le rapport.

Les messages du scénario sont des textes de la fixture synthétique ; aucune donnée propriétaire
n'est nécessaire pour reproduire les tests.

## 6. Limites actuelles

- sous-ensemble de `ab_proving` uniquement ; pas de campagne NWN hétérogène prouvée ;
- tables 2DA, races/classes complètes, multiclasses et feats réels absents ;
- attaques spéciales, sorts complets, AoE, concentration, invocations et composants absents ;
- magasins, loot tables et propriétés magiques complètes absents ;
- perception, fuite, pathfinding de navigation et traversée de zone par IA absents ;
- pas de continuation au milieu d'une instruction VM ;
- oracle NWN réel non exécuté ; le diff compare des traces Forge et `aurora-oracle-log` attend un
  log externe au format `forge.nwn-oracle-log@1` ;
- panneau Studio `AuroraM5Panel` livré dans le code, sans capture ni vérification visuelle ;
- validation Khronos faite sur 10 GLB du Studio, pas sur les GLB du bundle ;
- audit d'échelle structurel seulement ; pas de FPS ni de chargement GLB mesurés ;
- archive finale non créée tant que M5 reste partiel.

## 7. Dépannage

| Symptôme | Cause probable | Action |
| --- | --- | --- |
| `ERROR ...` au chargement | bundle incomplet ou manifeste invalide | relancer `aurora-audit` |
| `unsupported-native` | script utilisant une native hors registre | lire le diagnostic, ne pas annoncer le module complet |
| `unsupported NCS instruction` | opcode hors sous-ensemble | utiliser le corpus vérifié ou étendre la VM |
| `oracle diff found ... divergence(s)` | rapport différent de la référence | lire `m5-diff.json` et corriger ou documenter l'écart |
| `save schema ... is not supported` | sauvegarde inconnue | utiliser M4/M5 ou ajouter une migration explicite |
| reprise non idempotente | doublon de récompense/objet | refuser la sauvegarde et inspecter les reçus |

## 8. Fichiers de référence

- `docs/aurora-bridge/M5_QUALIFICATION.md` : protocole et résultats M5.
- `docs/aurora-bridge/M5_FINAL_REPORT.md` : verdict final et risques résiduels.
- `docs/aurora-bridge/M4_GAMEPLAY.md` : profil et règles M4.
- `docs/aurora-bridge/M3_RUNTIME.md` : VM NCS et événements M3.
- `docs/aurora-bridge/m5-sbom-full.json` : SBOM Cargo complète (909 composants).
- `docs/aurora-bridge/m5-sbom.json` : vue directe simplifiée.
- `Preuves-et-analyses/actives/aurora-bridge/m5/` : rapport, trace, logs et diff.

Aucune IA n'est nécessaire pour importer, exécuter, sauvegarder, reprendre ou diagnostiquer le
scénario.
