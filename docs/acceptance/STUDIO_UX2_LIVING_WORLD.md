# Acceptation Studio — UX2 Monde vivant

État local au 17 août 2026 : **fonctionnel et qualifié** pour les 29 routes Monde vivant. UX2-A
reste détaillé dans `STUDIO_UX2_A.md`; les lots B–L ajoutent cycle de vie, événements typés, autorité
de combat, triggers spécialisés et composition complète des rencontres. Chaque surface exige une
sélection explicite et conserve le catalogue voisin.

## Preuves

| Lot | Routes | Evidence | Durée | Capture FHD inspectée |
|---|---|---|---:|---|
| A | AREA-023/025/026 | `ux2-a-area-living-world` | 75,69 s | `ux2-a-area-living-world-forward-plus-fhd.png` |
| B | AREA-022 | `ux2-lw-b-area-lifecycle` | 80,59 s | `ux2-lw-b-area-lifecycle-1920x1080.png` |
| C | AREA-024 | `ux2-lw-c-area-user-events` | 86,84 s | `ux2-lw-c-area-user-events-1920x1080.png` |
| D | AREA-027 | `ux2-lw-d-area-combat-authority` | 67,26 s | `ux2-lw-d-area-combat-authority-fhd.png` |
| E | TRG-014/015/016 | `ux2-lw-e-trigger-living-world` | 71,97 s | `ux2-lw-e-trigger-living-world-fhd.png` |
| F | ENC-001..005 | `ux2-lw-f-encounter-foundation` | 95,87 s | `ux2-lw-f-encounter-foundation-fhd.png` |
| G | ENC-006/007 | `ux2-lw-g-encounter-selection-activation` | 99,62 s | `ux2-lw-g-encounter-selection-activation-fhd.png` |
| H | ENC-009/011/012/013/015 | `ux2-lw-h-encounter-scenario` | 92,71 s | `ux2-lw-h-encounter-scenario-fhd.png` |
| I | ENC-008/010/014 | `ux2-lw-i-encounter-runtime-policies` | 94,53 s | `ux2-lw-i-encounter-runtime-policies-fhd.png` |
| J | ENC-016/017/019 | `ux2-lw-j-encounter-tactical` | 101,74 s | `ux2-lw-j-encounter-tactical-fhd.png` |
| K | ENC-018 | `ux2-lw-k-encounter-statistics` | 104,83 s | `ux2-lw-k-encounter-statistics-fhd.png` |
| L | ENC-020 | `ux2-lw-l-encounter-validation` | 104,94 s | `ux2-lw-l-encounter-validation-fhd.png` |

Les douze portails cumulent build Studio, tests ciblés des profils/handlers/services/sérialiseurs,
smoke global, capture Forward+ et contrôle de matrice. Les smokes contractuels vérifient invalidités,
révisions/empreintes périmées, commande canonique, undo/redo, round-trip, tails voisins, source
intacte, cleanup et restauration de surface.

Le portail transversal de clôture UX2 a ensuite réussi avec **2 033 tests .NET**, **39 tests
Python**, des builds sans avertissement, un graphe Roslyn à **9 968 nœuds / 71 303 relations / 0
diagnostic**, puis les smokes Studio, Toolset, Runtime, Forward+, MCP, réseau et distribution.

## Routes qualifiées

| Route | Lot | Qualification Studio |
|---|---|---|
| <a id="orf-area-022"></a>`ORF-AREA-022` | B | Entrée/sortie typée et binding observé ; aucun script prétendument exécuté. |
| <a id="orf-area-024"></a>`ORF-AREA-024` | C | Événement utilisateur versionné, payload typé, compilation sèche et dispatch ForgeScript réel via le runtime. |
| <a id="orf-area-027"></a>`ORF-AREA-027` | D | PvP, alliés et aggro authorés ; autorité serveur appliquée avant coût ou dégâts. |
| <a id="orf-trg-014"></a>`ORF-TRG-014` | E | Repos sûr évalué avec danger et composition réelle du groupe. |
| <a id="orf-trg-015"></a>`ORF-TRG-015` | E | Piste évaluée avec compétence, difficulté, faux indice et contexte de quête. |
| <a id="orf-trg-016"></a>`ORF-TRG-016` | E | Interjection avec participants, priorité, once/cooldown et autorité bornée. |
| <a id="orf-enc-001"></a>`ORF-ENC-001` | F | Assistant de rencontre sur profil canonique existant ou création explicite. |
| <a id="orf-enc-002"></a>`ORF-ENC-002` | F | Volume d’activation complet et valeurs non finies refusées. |
| <a id="orf-enc-003"></a>`ORF-ENC-003` | F | Candidats créature complets, rôles, poids et filtres. |
| <a id="orf-enc-004"></a>`ORF-ENC-004` | F | Difficulté data-driven, budget et aperçu de composition. |
| <a id="orf-enc-005"></a>`ORF-ENC-005` | F | Min/max, groupes et plafonds validés. |
| <a id="orf-enc-006"></a>`ORF-ENC-006` | G | Sélection selon challenge via stratégie persistée et valeurs party de preview. |
| <a id="orf-enc-007"></a>`ORF-ENC-007` | G | Once/Repeat/Timed/Wave/Persistent/PopulationMaintained/Script authorés. |
| <a id="orf-enc-008"></a>`ORF-ENC-008` | I | Respawn, reset, persistance et anti-farm résolus par contexte explicite. |
| <a id="orf-enc-009"></a>`ORF-ENC-009` | H | Ancres manuelles, transform, capacité, navigation, visibilité et distribution. |
| <a id="orf-enc-010"></a>`ORF-ENC-010` | I | Faction active réelle sélectionnée sans réassigner un acteur. |
| <a id="orf-enc-011"></a>`ORF-ENC-011` | H | ActivatorQuery et autorité requis authorés. |
| <a id="orf-enc-012"></a>`ORF-ENC-012` | H | OnEnter/OnExit persistés et projetés dans le cycle de vie. |
| <a id="orf-enc-013"></a>`ORF-ENC-013` | H | OnExhausted et action terminale authorés. |
| <a id="orf-enc-014"></a>`ORF-ENC-014` | I | Heartbeat/UserDefined, cadence, budget, namespace et autorité. |
| <a id="orf-enc-015"></a>`ORF-ENC-015` | H | Phases et vagues complètes avec politiques de fin. |
| <a id="orf-enc-016"></a>`ORF-ENC-016` | J | Renforts avec alerte, faction et route réelles, cap, délai et annulation. |
| <a id="orf-enc-017"></a>`ORF-ENC-017` | J | Hors-vue selon distance, visibilité caméra et signal LOS explicite. |
| <a id="orf-enc-018"></a>`ORF-ENC-018` | K | Simulation statistique offline déterministe, variances et percentiles. |
| <a id="orf-enc-019"></a>`ORF-ENC-019` | J | Décision de nettoyage dead/escaped/abandoned/unload/persistent. |
| <a id="orf-enc-020"></a>`ORF-ENC-020` | L | Validation agrégée et corrections bornées, couverture inconnue affichée « Non évalué ». |

`ORF-AREA-023`, `ORF-AREA-025` et `ORF-AREA-026` conservent leurs ancrages et preuves détaillées
dans `docs/acceptance/STUDIO_UX2_A.md`.

## Limites honnêtes

- les rencontres exposent authoring, validation et simulations/services de décision ; le Studio ne
  spawn aucun acteur, n’exécute aucun timer moteur, ne raycaste pas et ne transporte rien en réseau ;
- la validation ENC-020 ne déclare pas conformes les récompenses, conditions ou plafonds globaux
  faute de catalogues projet canoniques : ces lignes restent explicitement « Non évalué » ;
- les scripts ne sont exécutés que dans les bridges réellement branchés ; ailleurs, le Studio
  annonce compilation, projection ou binding observé ;
- Forgotten Forest reste vide pour les familles absentes ; les rencontres riches vivent uniquement
  dans une session temporaire isolée.

## Reproduction

```powershell
# Exemple ; chaque mode de la table est reproductible de la même manière.
.\scripts\Verify-StudioIncrement.ps1 -Page living-world `
  -Evidence ux2-lw-l-encounter-validation `
  -Capture artifacts\verification\studio-increment\ux2-lw-l-encounter-validation-fhd.png
```
