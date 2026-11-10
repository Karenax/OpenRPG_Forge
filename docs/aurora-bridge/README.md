# Passerelle Aurora/NWN → OpenRPG Forge — contexte consommateur

**Statut au 8 novembre 2026 : M5 partiel, non clos ; `forge_aurora_runtime`
projette le bundle en package Forge, convertit les WOK en navigation et exécute une visite à deux
zones ; `forge_nwn_runtime` exécute le sous-ensemble NCS de `ab_proving` jusqu'à la reprise
idempotente ; `forge_nwn_rules` exécute un combat d20 déterministe, des sorts, des objets, des
factions/IA et une sauvegarde M4 ; M5 ajoute la migration M4 → M5, une trace reproductible,
`aurora-diff-oracle`, `aurora-oracle-log` et un panneau Studio de diagnostic. Le module NWN
hétérogène réel, la preuve visuelle Studio, l'oracle NWN local, les GLB du bundle et l'archive
restent absents.**
Ce dossier est le point d'entrée consommateur. Il ne remplace ni `PROJECT_CONTEXT.md`, ni `AGENTS.md`,
ni `docs/HANDOFF.md` : il organise le chantier et renvoie aux sources canoniques.

## 1. Rôle

OpenRPG Forge est **consommateur** de la passerelle :

- il valide et importe les bundles produits par OpenNever ;
- il projette les zones, objets, assets et références vers ses documents Forge ;
- il fournit un runtime de compatibilité séparé pour les scripts NCS, les événements et le ruleset NWN ;
- il ne lit pas directement les MOD/HAK/TLK et ne duplique pas les parsers Aurora.

OpenNever reste producteur et propriétaire du contrat. Le contrat de référence est
`module-migration-bundle@1.0.0`, documenté dans
`E:\OpenNever_Forge\docs\aurora-bridge\MODULE_MIGRATION_BUNDLE_V1.md`.

## 2. Documents à lire dans cet ordre

1. [`BACKLOG.md`](BACKLOG.md) — extrait consommateur du backlog maître M0 à M5.
2. [`PROGRESS.md`](PROGRESS.md) - état réel, blocages, preuves, prochaine action.
3. [`M3_RUNTIME.md`](M3_RUNTIME.md) - spécification M3 : événements, NCS, natives, limites.
4. [`M4_GAMEPLAY.md`](M4_GAMEPLAY.md) - spécification M4 : profil NWN, combat, sorts, objets, IA, sauvegarde.
5. [`M5_QUALIFICATION.md`](M5_QUALIFICATION.md) - qualification M5 : migration, trace, diff-oracle, limites.
6. [`M5_USER_GUIDE.md`](M5_USER_GUIDE.md) - guide utilisateur et mainteneur.
7. [`M5_FINAL_REPORT.md`](M5_FINAL_REPORT.md) - verdict M5 et risques résiduels.
8. [`CAPABILITY_AUDIT.md`](CAPABILITY_AUDIT.md) - audit multi-zone, runtime mono-zone, sauvegarde et quotas.
9. [`CONTRACT.lock.json`](CONTRACT.lock.json) - verrou du contrat (gelé) et
   [`contract/`](contract/) - miroir du schéma SHA-256.
10. [`../../docs/ADR/ADR-0063-aurora-bridge-import.md`](../ADR/ADR-0063-aurora-bridge-import.md) - ADR consommateur.
11. [`../../docs/ADR/ADR-0064-nwn-rules-profile.md`](../ADR/ADR-0064-nwn-rules-profile.md) - ADR du profil NWN.
12. Backlog maître :
   `E:\OpenNever_Forge\docs\aurora-bridge\BACKLOG.md`.
13. Campagne de preuves :
   `E:\OpenRPG_Forge\Preuves-et-analyses\actives\aurora-bridge\STATUS.md`.

## 3. Décisions structurantes

- **Aucune dépendance Cargo `path`** entre les deux checkouts. Le contrat est un format JSON versionné.
- **Aucun code Rust partagé** entre OpenNever et OpenRPG.
- Le contrat est mirroiré localement et verrouillé par version + SHA-256.
- Les zones NWN sont importées comme **objets sources** dans le conteneur multi-zone
  (`forge_project::module_package::objects`), pas converties en terrain Forge.
- Le runtime natif et ses quotas (32 scripts, 256 pas, 128 variables narratives, 32 factions) ne sont
  pas modifiés pour absorber un module NWN. Le runtime de compatibilité est distinct.
- Le ruleset NWN (`NwnRulesProfile`) est séparé du ruleset action-RPG natif. Aucune régression du
  combat de contact physique natif n'est acceptée.
- Les statuts `importable`, `renderable`, `navigable`, `scriptable`, `playable`, `saveable` restent
  séparés ; un import non joué est une prévisualisation.

## 4. Crates prévues

| Crate / composant | Jalon | Rôle | État |
| --- | --- | --- | --- |
| `forge_aurora_bridge` | M1 | lecteur, validateur, mapping et import du bundle | créée, import lecture seule |
| `forge_aurora_runtime` | M2 | projection Forge, walkmesh, session, visite | créée, visite locale |
| `forge_aurora_visual` | M2 | rendu Bevy simplifié de la visite | créée, capture locale |
| `forge_nwn_runtime` | M3 | VM NCS, événements, natives, ordonnanceur | créée ; sous-ensemble `ab_proving`, 27 tests |
| `forge_nwn_rules` | M4 | profil NWN, combat d20, objets, sorts, factions, quête | créée ; 35 tests, sous-ensemble `ab_proving` |
| `ModuleSession` | M2/M5 | état multi-zone, transitions, persistance | créée (M2) ; snapshot M4 |

Le placement exact de `ModuleSession` est fixé dans `forge_aurora_runtime` pour M2 ; la persistance
multi-zone reste à concevoir en M5. Ne pas créer de crate vide par anticipation.

## 5. Baseline observée le 7 novembre 2026

| Élément | Valeur |
| --- | --- |
| Checkout commun | `E:\OpenRPG_Forge\OpenRPG_Forge-Rust` |
| Branche | `Rust` |
| HEAD observé | `97176c19` |
| Audit du 26 octobre | citait `fc75b8e8`, révision antérieure |
| Fichiers non suivis hors périmètre | `scripts/hivemind/audit_gladiator_material_migration_v397.py`, `scripts/hivemind/verify_palm_scalar_vertex_v562.py` |
| Code NWN/Aurora | `forge_aurora_bridge`, `forge_aurora_runtime`, `forge_nwn_runtime`, `forge_nwn_rules`, `forge_aurora_visual` |
| Contrat importé | `module-migration-bundle@1.0.0`, gelé, SHA-256 `342b3316…` |

Ne pas modifier les fichiers non suivis ci-dessus. Ne pas nettoyer, réinitialiser ou committer le
travail préexistant pour obtenir un arbre propre.

## 6. État des gates

La passerelle est une **piste parallèle hors numérotation des gates**. Elle n'ouvre ni Gate 8, ni
Gate 9, ni une nouvelle gate. Gate 7 reste ouverte selon son plan ; Gate 8 et le bonus restent non
ouverts. Aucun verdict de gate n'est modifié par ce chantier.

Les travaux de la passerelle ne doivent pas détourner les priorités de la gate active sans décision
explicite du propriétaire. Le présent cadrage est autorisé ; l'implémentation suit le backlog M0–M5.

## 7. Comment reprendre sans lire la conversation

1. Lire ce fichier, puis `PROGRESS.md`.
2. Lire `ADR-0063`, puis le backlog maître OpenNever.
3. Vérifier `git status`, HEAD et les fichiers non suivis.
4. Prendre la première tâche `todo` ou `in-progress` dont les dépendances sont satisfaites.
5. Mettre à jour `PROGRESS.md` et `STATUS.md` de la campagne après chaque tranche.
6. Ne jamais marquer `done` un contrôle non exécuté.

## 8. Prochaine action

Poursuivre **M5** : module NWN hétérogène réel, oracle NWN local, preuve visuelle Studio, GLB du
bundle, textes de licence transitifs et archivage.
M5 est partiel : migration, trace, diff-oracle et panneau Studio sont prouvés, mais le module
hétérogène et les preuves visuelles/licences/archive manquent. Voir [`PROGRESS.md`](PROGRESS.md),
[`M5_FINAL_REPORT.md`](M5_FINAL_REPORT.md) et les preuves
`Preuves-et-analyses/actives/aurora-bridge/m5/`.
