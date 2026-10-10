# Consolidation des points 4 puis 1 — 10 octobre 2026

## Demande et périmètre

Le propriétaire a demandé de corriger d'abord la cohérence des documents d'avancement, puis l'intégration du candidat RPG et les erreurs de CI. Cette correction conserve les travaux concurrentement présents dans le checkout Rust. Elle ne reprend pas le chantier de généralisation, ne clôture pas Gate 3 et ne publie aucun changement.

## Documentation

`PROJECT_CONTEXT.md`, la roadmap, le plan préparatoire Gate 3, le bilan Gate 3, ADR-0006, ADR-0018 et le paragraphe d'état d'`AGENTS.md` distinguent désormais : Gate 2 clôturée par décision propriétaire ; Gate 3 ouverte ; acceptation humaine limitée au correctif 005 du 8 octobre ; preuves Source075 limitées à leur snapshot ; preview installable distincte de l'aventure RPG et de la release ; CI du 9 octobre effectivement exécutée mais native rouge à Clippy. Les acquisitions historiques restent intactes et datées. Avant publication, le remplacement du candidat installable initial rejeté par la correction R3 est aussi répercuté ; ces binaires moteur du 9 octobre ne comprennent pas cette intégration moteur/CI.

## Intégration et corrections

Les ports sont sélectionnés à partir de Source075 `7a0c770f1eac0afcc8fb67accaae539c4da10747`, comparés aux sources courantes basées sur `ae999c2f3395e9472301baebcfcb3d1b2ec0ec98`. Les manifests, forks et fichiers de rendu du candidat ne sont pas remplacés mécaniquement. Les contrats récents des scalaires, textures, BRDF, Resources et Readiness sont conservés.

Le test natif de plancher a revele que Rapier pouvait supprimer la translation horizontale apres un contact dont la normale verticale valait 0,99999994. Le fork Rapier est reconstruit depuis les 217 fichiers de l archive verrouillee, SHA `d9961dfc6cabf508c2db7bc7a73b9d82800de898ccaed0bbaab58290c9620fc0`, puis normalise cette normale dans `compute_hit_info`. Aucun seuil de mouvement n est relache ; ni version ni transitive ne change. Le fork Bevy du candidat n est pas adopte. Le premier echec natif reste archive dans `workspace-tests-001.log`. Le stub de validation du shader sous-marin est complete avec les champs lumineux deja utilises par le shader reel ; Naga conserve toutes ses validations.

Les corrections fonctionnelles portent sur la conservation d'un appui bref entre deux ticks fixes, la détection de l'appui sous la capsule au bord d'une marche et l'absence de notifications matérielles inutiles pour un objet immobile. Les tests ajoutés couvrent aussi les clés opposées, les poses relâchées, les réactions au feu, la conservation de la couleur et de la rugosité et les handles absents. Les ports ciblés conservent les vérifications de provenance et d'ordre des callbacks Ocean/Spray.

Les erreurs de style sont corrigées dans les Assets, le moteur, le Studio et les probes versionnés : conversions bornées ou vérifiées, tailles de tableaux GPU, imports explicites, fixtures empruntées, initialisations directes, comparaisons exactes de bits lorsque le contrat l'exige. Les exceptions locales restantes ont une raison attachée à l'item concerné, notamment les SystemParams Bevy et les séquences GPU dont l'ordre doit rester visible. Aucune désactivation globale de Clippy n'est ajoutée.

Treize tests exigeant des fichiers privés restent compilés et contrôlés par Clippy, puis exécutés explicitement avec les entrées authentifiées. Ils ne sont pas revendiqués comme exécutés par la CI publique. Le test GameFish déjà ignoré conserve son protocole séparé. Voir le [guide des replays privés](../../testing/PRIVATE_SOURCE_REPLAYS.md).

Les changements concurrents de rendu ont été réconciliés : la base `SourceGraphPhaseMaterial` reste celle du checkout courant. La phase ne réserve aucun sampler ; ses tests et la spécialisation du matériau sont conservés. La validation finale correspond à cette réconciliation, et non au précédent snapshot natif 002, conservé séparément.

## Identité des preuves et limites

La copie isolée `integrated-source-002` contient les cibles versionnées du workspace et les dépendances courantes réconciliées. Cinquante et un exemples de diagnostic locaux non versionnés sont conservés à part dans cette copie et restent intacts dans le primaire ; ils ne sont pas inclus dans son contrôle CI. Le lanceur et l’outil `tools/forge_delivery`, chacun dans un workspace séparé et modifiés par une autre tâche, ne sont pas qualifiés par ces résultats. Le probe navigateur optionnel `verify_browser_source_glb_v141.rs` a aussi évolué concurremment ; sa nouvelle acquisition ne fait pas partie des cibles par défaut ou du check Engine WebGPU. Ces changements sont conservés sans remplacement.

Le manifeste de 4495 fichiers `validated-source-003.manifest.json` identifie la source contrôlée, SHA-256 `e16566af6ee00e3ce2ecb3908098825b4db4df5858f2efd80b3cde4c754b1867`. Les entrées privées ont été relues : 259 fichiers, 21246539 octets, SHA conformes au manifeste d'acquisition ; le paquet Scanned ajoute 83 fichiers et 68848404 octets verifies separement. Les contrôles utilisent Rust 1.98.0, le lock courant, Windows, mode hors ligne et un seul job Cargo. Les logs sont conservés sous `E:\OpenRPG_Forge\.tmp\progress-repair-20261010`.

L’application protégée a intégré 337 fichiers avec sauvegarde des versions précédentes. Le contrôle initial après copie retrouve les SHA attendus de tous ces fichiers et de 1421 entrées Rust/WGSL/Cargo du périmètre testé. Les fins de ligne des fichiers Rust copiés hors vendor sont ensuite normalisées de CRLF à LF pour le diff Git ; leurs contenus restent identiques après cette seule normalisation. Le vendor du registre reste inchangé. Le reçu `application-lf-normalization.json` conserve les SHA avant/après. Les espaces de travail concurrents et le probe optionnel mentionnés ci-dessus restent hors de cette parité. Le reçu est `post-application-source-parity.json`.

## Résultats

| Contrôle local Windows | Résultat |
| --- | --- |
| `cargo fmt --all --check` | PASS |
| Clippy workspace/all-targets, `-D warnings` | PASS |
| Tests workspace/all-targets | 2003 PASS, 0 échec, 55 ignorés, 164 binaires |
| Replays privés, mêmes binaires natifs | 13 PASS (Assets 1, Fish 9, Scanned 3) |
| Engine WebGPU `wasm32-unknown-unknown`, sans features par défaut, `web` | PASS ; 2 avertissements d’imports inutilisés sur cette cible |
| `cargo deny` | PASS : advisories, bans, licenses, sources |
| Audit selon politique Forge | PASS : 0 vulnérabilité, 10 avertissements ; bloqueur de distribution GLib RUSTSEC-2024-0429 conservé |

Les builds release natifs, le packaging Studio et le build Trunk release de la CI distante ne sont pas revendiqués par ces contrôles. Aucun nouveau rendu GPU ni verdict humain n'est acquis.

La suite finale est `workspace-tests-003.log`, SHA-256 `6083af958ef85e3df961bb3f39793dd9439963673d317071a3f19725a505e3fe`. Les replays finaux sont `private-replay-assets-002.log`, `private-replay-fish-002.log` et `private-replay-scanned-002.log` ; leurs reçus conservent SHA des logs et des exécutables. `workspace-strict-015.log`, `format-check-002.log` et `wasm-check-003.log` constituent les contrôles associés. Les premiers échecs et le snapshot natif 002 restent archivés.


## Suite soumise aux preuves

Une CI distante verte nécessite une publication explicitement autorisée, puis l'exécution des jobs Windows/Linux/WASM/supply-chain sur cette nouvelle révision. Les anciens PASS Source075 et les résultats Windows locaux ne remplacent pas ces jobs. L'acceptation visuelle courante et la décision de clôture Gate 3 restent distinctes.
