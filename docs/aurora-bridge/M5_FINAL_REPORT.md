# Rapport final M5 - passerelle Aurora/NWN

**Date :** 8 novembre 2026.
**Verdict :** **M5 partiel - non clos localement.**
**Portée prouvée :** scénario synthétique `ab_proving` importé, exécuté, sauvegardé, migré et
repris ; traces reproductibles et diff-oracle entre deux exécutions Forge.
**Portée non prouvée :** module NWN hétérogène réel, preuve visuelle Studio, oracle NWN local, GLB
du bundle dans un viewer Khronos, textes de licence transitifs, FPS et archive finale.

## 1. Ce qui est prouvé

| Capacité | Preuve |
| --- | --- |
| Import et validation du bundle | `forge_aurora_bridge`, manifeste `bfc43867…` |
| Navigation, portes et transitions | M2, visite 285,38 m, 2 transitions |
| NCS, dialogue, journal, objet, porte | M3, 21 tests |
| Profil NWN, combat d20, sorts, objets, IA | M4, 46 tests rules |
| Sauvegarde M5 et migration M4 → M5 | `m5-report.json`, `accepted=true`, `restored=true` |
| Trace reproductible | 54 entrées, SHA-256 `283a5af7…` |
| Diff-oracle Forge | replay identique `matches=true` ; mutation `gold` détectée |
| Harnais oracle externe | `aurora-oracle-log`, format testé ; aucun log NWN réel |
| Transitions répétées | 3 cycles, 6 transitions, restauration sans fuite |
| Robustesse VM/sauvegarde/profil | budget infini, corruption, schémas inconnus |
| Panneau Studio M5 | `AuroraM5Panel` appelle `run_aurora_gameplay` ; pas de capture |
| Validation Khronos glTF | 10 GLB, 0 erreur, 69 warnings classés |
| SBOM/licences | `m5-sbom-full.json`, 909 composants avec licence |
| Échelle structurelle | 3 modules réels audités (42/25/29 zones), export refusé |
| Documentation utilisateur | `M5_USER_GUIDE.md` |

## 2. Ce qui n'est pas prouvé

- **Module hétérogène réel :** aucun module NWN communautaire ou officiel n'a été importé, joué et
  sauvegardé dans cette campagne. Le corpus reste `ab_proving`.
- **Studio M5 :** panneau Leptos livré et compilé wasm, mais aucune capture ni vérification
  visuelle automatisée ; les diagnostics restent aussi disponibles en CLI.
- **Oracle NWN :** `aurora-diff-oracle` compare deux traces Forge ; `aurora-oracle-log` sait lire un
  log externe, mais aucun scénario NWN local n'a été exécuté comme oracle.
- **glTF :** 10 GLB du Studio validés sans erreur, mais aucun GLB du bundle `ab_proving` ni scène
  de navigation dans un viewer Khronos.
- **SBOM :** `m5-sbom-full.json` couvre 909 composants du graphe Cargo ; les textes de licence
  transitifs ne sont pas agrégés.
- **Performance :** mesure CPU du scénario (`11,723 ms` de moyenne, p95 `15,526 ms`) et empreinte
  headless (pic 9,2 Mo) ; aucun FPS ni chargement GLB mesurés.
- **Archivage :** aucune archive ZIP64, aucun retrait de preuve ; la campagne reste active.

## 3. Risques résiduels

| Risque | Impact | Réponse |
| --- | --- | --- |
| corpus synthétique seulement | généralisation abusive | matrice et rapport limitent explicitement la portée |
| tables 2DA absentes | personnages/classes non conformes | marquer `partial`, ne pas annoncer `exact` |
| oracle non exécuté | règles non comparées à NWN | conserver `oracleStatus=not-run` |
| sauvegardes M3/M4 anciennes | migration future cassée | refus explicite des schémas inconnus |
| assets propriétaires | redistribution illégale | aucun asset NWN dans Git ; SBOM local-only |
| panneau Studio sans capture | découverte partielle | guide CLI + rapport diagnostics + panneau à vérifier visuellement |
| archive non faite | preuves dispersées | garder `actives/`, archiver seulement après clôture réelle |

## 4. Critères de sortie M5

| Critère | Verdict |
| --- | --- |
| module hétérogène importé, joué, sauvegardé, repris | **NO-GO** (corpus synthétique seulement) |
| limites affichées | **PASS** |
| preuves de sécurité/performance/licences publiables | **PARTIEL** |
| campagne archivée | **NO-GO** (M5 non clos) |
| support universel | **NO-GO** (non revendiqué) |

## 5. Décision

M5 reste **partiel**. Le travail technique de migration, trace, diff-oracle, robustesse et
documentation est livré et reproductible, mais il ne satisfait pas le critère de module hétérogène
ni les preuves UX/glTF/archive. Aucun commit, tag, push ou publication n'est effectué.

## 6. Prochaines actions pour clore M5

1. Choisir un module NWN local autorisé, l'exporter avec OpenNever et exécuter le scénario M5.
2. Exécuter un oracle NWN local sur les mêmes entrées et normaliser les divergences.
3. Vérifier visuellement le panneau Studio `AuroraM5Panel` et ajouter une capture UX réelle.
4. Valider les GLB du bundle et la scène de navigation dans un viewer Khronos.
5. Agréger les textes de licence et les notices de distribution.
6. Mesurer FPS, mémoire, chargement GLB et changement de zone.
7. Archiver la campagne avec ZIP64, manifeste et bilan de stockage après clôture réelle.
