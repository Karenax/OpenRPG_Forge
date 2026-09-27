# Gate 2 — reprise du 19 septembre 2026

## Portée et état initial

Objectif actif : terminer Gate 2 dans le worktree `OpenRPG_Forge-Rust`, branche
`Rust`. La demande courante borne cette reprise à Gate 2. Aucune publication,
fusion de `main`, copie d'asset Cadle ou remise à zéro du travail existant.

Passation consultée : tâche « Créer une branche Rust », dernier verdict du
16 septembre : R2-020 terminé localement, Gate 2 ouverte. Le contrôle Hermes du
17 septembre ne remplace pas cette preuve et son test à zéro cas est exclu.

État initial : HEAD `f786a38aca2023f08d708687f99a47d14f34e6d4`, 63 chemins suivis
modifiés et 467 chemins non suivis. Inventaire initial de 1 748 fichiers de source
avec hashes, état Git et patch suivis conservés sous
`artifacts/forge-runtime/gate2-resume-20260919/`. Ce candidat modifié n'est pas une
révision propre qualifiée sous ADR-0006.

La roadmap comporte sept critères localement acquis : R2-001, R2-002, R2-003,
R2-005, R2-018, R2-020 et R2-045. Leurs preuves ne sont pas rouvertes par cette
reprise. Les 45 autres cases restent ouvertes ; code présent et qualification
sont distingués. Les critères transversaux de sortie restent également ouverts.

## Premier incrément : R2-004

Contrat conservé : ADR-0014, départ X/Z sur terrain et cap optionnel. Ni départ
sur étage ni garantie d'absence d'obstacle déduits. Les informations de zone
utilisent les champs canoniques existants de TerrainDocument ; aucun nouveau
document AreaMetadata, format d'archive ou acteur d'audit inventé.

Travail en cours, non encore validé :

- annulation de la proposition lorsque le brouillon Nom ou Altitude change ;
- cap réel de caméra en degrés dans les diagnostics du playtest ;
- présentation des métadonnées canoniques de la zone ;
- paramètres optionnels de cache et de répertoire temporaire du script de
  packaging, pour conserver les nouvelles écritures dans ce worktree.

Preuves attendues : champ modifié après aperçu => application impossible ;
renommage accepté et Undo/Redo ; choix de départ par clic ; départ 40/0/90,
Undo/Redo, sauvegarde et réouverture ; position X/Z et cap réellement observés
en simulation, retour à l'édition sans écriture. Tests ciblés de persistance et
du host, compilation native/WASM et inspection visuelle du candidat exact.

L'ancien EXE `1303A840FE90BAA2BE4AEA36D0635AD75AE48592E43D8C9ADBB53CF79E9B4D89`
a été vérifié puis copié dans le dossier de reprise pour un contrôle initial.
Il ne contient pas ces nouveaux changements et ne peut pas les qualifier.

### Candidat construit par Luna max

Le 19 septembre à 18:32 UTC, le packaging a produit
`artifacts/forge-runtime/gate2-resume-20260919/candidate-current-20260919-2030`.
Le journal termine par `Finished dev` en 2 min 37 s. Le manifeste
`build-proof.json` indique `BUILD_ONLY`, branche `Rust`, cache `target` et
répertoire temporaire dans ce worktree.

- EXE : `7D6308524C7B1968DB8A6D62DF6EE1F71325A5EB084954BDEEF26F3D5EC6B5A1`.
- WASM : `D8116620739CAAE1C869EF72CDF032592137091D2BE2FCF10F16D033E7AE7367`.

Ces identités proviennent du manifeste de construction. Elles identifient le
candidat à tester, sans prouver son comportement : `runtime_verified` et
`gate_qualified` restent `false`. Tests fonctionnels et parcours Computer Use
restent confiés exclusivement à Luna max ; aucun verdict R2-004 ajouté ici.

### Résultats ciblés transmis par Luna

Le rapport `artifacts/forge-runtime/gate2-resume-20260919/validation-luna-max-20260919.md`
consigne `zone_start` 2/2, `zone_host` 1/1, formatage et vérification du moteur
WASM réussis. Clippy Studio WASM reste en échec : `too_many_lines` dans
`AuthoringControls` et trois assertions `float_cmp` du test de revue d'habitat.
Ces diagnostics restent à corriger puis à revérifier par Luna.

La première tentative Computer Use n'a produit aucune preuve graphique : elle
utilisait l'interface `cua`, sans accès aux applications natives. Reprise confiée
à Luna avec le point d'entrée Windows `@oai/sky` prescrit par le skill Computer
Use disponible. Cette erreur de sélection d'outil ne prouve pas une
indisponibilité du plugin natif.

Reprise Sky : Luna confirme l'import réussi de `@oai/sky`, l'identification
du candidat PID 16172 et la création par l'interface du workspace/projet/module
et de la zone de test « R2-004 Luna » (seed 4004). Le parcours a produit des
captures inline ; leur archivage et les scénarios R2-004 restants sont en cours.
Ce point opérationnel ne constitue pas encore le verdict visuel du critère.
Les captures sont maintenant archivées sous `cu/` dans le dossier de reprise.
`03-metadata-identities.jpg`, produite par Luna et relue par le parent, montre
le nom accepté « R2-004 Luna », 512 × 512 m, seed 4004, pas 2 m, IDs projet/zone
et révision 1. Cette capture établit l'affichage de ces champs ; elle ne prouve
pas à elle seule les transitions Preview/Apply ou la persistance.
Point suivant transmis par Luna : scénarios Nom et Altitude (aperçu, modification
du brouillon annulant l'aperçu, puis nouvelle application), Undo/Redo et Save
observés. Captures `04-name-A-preview.jpg` à `12-zone-save.jpg` archivées dans
`cu/`. Le compte rendu final doit préciser l'ordre des raccourcis et les états
acceptés ; départ numérique/picking, playtest et réouverture restent en cours.
Complément Luna : picking terrain observé à X -57.7745 / Z 57.37874, puis
départ accepté restauré à 40/0/90. `18-reopen-metadata-start.jpg` documente la
réouverture avec métadonnées et départ conservés. Le diagnostic runtime de
`19-playtest-runtime-diagnostics.jpg` indique position (40.00, 7.91, 0.00),
au sol `true`, cap caméra 90.0°, ThirdPerson, simulation prête `true` et sources
CPU concordantes. Restent à consolider dans le rapport final : Undo/Redo du
départ, raccourci après invalidation, état sans zone, archive inchangée autour
du playtest et régression du candidat des sources finales.
Luna a ensuite observé le départ accepté 40→50, Undo→40, Redo→50 puis Undo
et Save→40 (captures 20–22), ainsi que la fermeture de zone (23).
Le champ X restant à 50 après Undo correspond au brouillon indépendant :
`PlaytestControls` ne le recharge automatiquement qu'au changement de zone,
et propose « Reprendre les valeurs du départ accepté ». L'état accepté et
l'objet sauvegardé restent à 40 ; ce constat ne prouve pas une perte de données.
Le bouton de reprise du brouillon doit encore être observé par Luna sur le
candidat final, avec comparaison directe des empreintes autour du playtest.
Le second Luna confirme après corrections : formatage PASS, Clippy Studio WASM
strict `--all-targets --locked --offline -- -D warnings` exit 0 en 5 min 53 s,
et test natif ciblé
`authoring_regeneration_review::tests::habitat_review_shows_units_and_effective_preview_without_application`
exit 0. Ce module est inclus nativement sous `cfg(test)` ; aucune exécution de
test WASM n'est déduite. Journaux dans le sous-dossier `clippy-recheck` des
artefacts de reprise. Le candidat graphique 7D630852 précède l'extraction des
composants de contrôles ; la validation de cette source finale doit rester
distincte de son parcours graphique.

### Construction des sources finales

Le WASM final compile (13 min pour le parcours Trunk), identité
`6AD185185CD9BB6AF0F95EBFC926F16B9D1C978D35BA91E9EF50170A9E089E2F`.
Le packaging `candidate-final-20260919` échoue dans rustc sur `tauri` avec
`0xc0000374, STATUS_HEAP_CORRUPTION` ; aucun EXE final ni manifeste de succès.
Le journal d'échec est conservé. Une tentative supplémentaire de packaging,
sans reconstruire le WASM, est confiée à Luna avec `CARGO_BUILD_JOBS=1` dans
un dossier distinct `candidate-final-retry-20260919`. Aucun succès présumé.
Le cache de cette construction se trouve dans le sous-dossier `worktree/target`
du dépôt Rust (et non le `target` du premier build), toujours dans le périmètre
autorisé ; aucun nettoyage ou déplacement effectué.

La tentative ciblée à un job réussit : manifeste du 19 septembre 19:27 UTC,
`candidate-final-retry-20260919/build-proof.json`, EXE
`9317EE50FD209B8C32D41F17C95E45AEDBD71792DE5CFD147BB325B6E4A649EF`,
WASM final 6AD18518 ci-dessus. Statut conservé `BUILD_ONLY` ; contrôle graphique
des composants extraits confié à Luna sur cet EXE avant conclusion R2-004.
Le répertoire temporaire réel est également sous le sous-dossier `worktree/`
du dépôt, comme le précise le manifeste.
Ce sous-dossier apparaît non suivi dans Git : notamment des caches Node sous
`worktree/artifacts/forge-runtime/gate2-resume-20260919/tmp`. Il est conservé
sans nettoyage et ne fait pas partie des sources à qualifier ou à publier.
Les constructions suivantes utilisent les chemins absolus racine `target` et
`artifacts/.../tmp` pour éviter de reproduire cette ambiguïté.

### R2-006 : changement de zone

Audit Terra de R2-006 : `SourceControls` conserve les sélections de chemin,
rivière et bâtiment au changement de zone. La réconciliation des placements
conserve aussi un ID si celui-ci existe dans la nouvelle zone. Une réutilisation
d'ID peut donc transporter une sélection entre deux `AreaId` distincts.
La végétation possède déjà sa garde de changement de zone.

Correction implémentée par Terra dans `authoring_zone.rs`, validation en cours :
effacer ces sélections au changement
de l'`AreaId` accepté, annuler le geste bâtiment et utiliser le `clear` des
placements pour effacer également son état de picking. Conserver les sélections
indépendantes par catégorie et leur continuité lors d'une révision dans la même
zone. Scénarios à confier à Luna : A→B avec IDs réutilisés, sélection indépendante
dans B, conservation au changement de révision sans changement de zone.
Cet audit statique ne qualifie ni le reset graphique, ni la pagination >40,
ni les autres cas limites de R2-006.
Le candidat R2-004 9317EE50 est immuable et ne contient pas ce correctif.
Une seconde instance Luna max vérifie le nouveau lot sans manipuler l'interface
occupée par la qualification R2-004. Aucun verdict R2-006 acquis à ce stade.
Résultat local transmis par Luna : formatage et Clippy Studio WASM strict
`--all-targets --locked --offline -- -D warnings` PASS (10,88 s). Aucun test
d'effet Leptos exécuté : ce module est conditionné à WASM/web et aucun harness
applicable n'a été trouvé. Journaux `r2-006-check`. La compilation ne remplace
pas les scénarios graphiques multizones requis ; candidat dédié en préparation.

## Organisation et conditions de clôture

### Préparation R2-B après la sélection

Lecture des preuves existantes : le replay 442d3065 du correctif de collecte
DOM a déjà montré une bande continue de 618 échantillons sur un glisser droit
rapide, application et sauvegarde. Ce défaut n'est pas à rouvrir comme absent
(`R2_AUTHORING_BRUSH_WORKSPACE.md`, section Replay du correctif).
Les sept tests du domaine de brosse et les tests du cache partiel sont acquis
dans leur portée historique ; ils ne remplacent pas les gestes Studio.

Qualification restante à grouper : autres opérations (abaisser, lisser,
aplatir, bruit, pente), cercle/rayon/intensité, annulation après changement de
réglage (correctif du 15/09), Undo/Redo du trait et reprise exacte sauvegardée.
Courbes rapides et grandes limites restent distinctes du simple trait droit.
Le recalcul partiel nécessite sa preuve de chunks affectés, pas une déduction
depuis une image. Aucune nouvelle validation R2-B revendiquée ici.

Audit complémentaire Luna en lecture seule : aucun manque d'implémentation
bloquant trouvé pour R2-007..014. Parcours à grouper sur une zone : cercle et
réarmement après réglage, cinq opérations, un trait entier avec Undo/Redo puis
Save/Close/Open ; peinture puis effacement, annulation après changement de
couche/rayon/intensité, Undo/Redo et reprise ; enfin relief après peinture.
Le replay matériau historique précède les corrections d'annulation et le patch
material-only : il ne suffit pas à qualifier ces changements actuels.
Les tests natifs de patch partiel préservent entités, handles meshes et composants
Rapier ; conserver leur portée distincte d'une preuve GPU. Aucun test existant
n'a été relancé par cet audit, et aucune case R2-B n'est cochée.

À la demande explicite du propriétaire, tous les tests, y compris Computer Use,
sont confiés à Luna avec effort max. Terra medium réalise les changements bornés ;
le parent coordonne et relit. Deux sous-agents maximum, aucun sous-agent récursif.
Les modèles/efforts ont été explicitement demandés à l'outil. Le service Standard
est configuré sur disque ; l'outil de cette session n'expose pas de sélection de
service, donc son application effective n'est pas attestée. Le propriétaire a
demandé de poursuivre avec le système de sous-agents après signalement.

Le verdict final exige le parcours auteur complet, ses mesures, la qualification
sur révision propre sous ADR-0006 et les revues humaines applicables. Aucun test
automatisé ne remplace une approbation humaine. CI distante : état documentaire
`NOT RUN — infrastructure billing`, sans nouveau run distant dans cette reprise.

## Verdict R2-004

Qualification locale acquise par le parcours 7D630852 et la régression ciblée
9317EE50. Rapport final :
`artifacts/forge-runtime/gate2-resume-20260919/validation-luna-max-final-20260919.md`.
Preuve structurée : `cu-final-9317EE50/r2-004-final-regression-evidence.json`.
La sauvegarde `authoring.json` est identique avant/après playtest :
`89E0DCB892A81B9589A604459DE1AA1F97690CD502737F81ECAE0782E84118D7`.
Les métadonnées, la retouche Nom/Altitude, le départ sauvegardé, le picking,
Undo/Redo du départ, la réouverture et le diagnostic runtime sont couverts par
ces deux parcours ; le refactoring intermédiaire ne touche pas le picking ni
les commandes de départ. Le diagnostic GPU reste explicitement non confirmé.
Le manifeste de construction conserve BUILD_ONLY ; la preuve runtime est dans
le rapport séparé. Les clés de création affichées par défaut sont distinctes des
identités acceptées ; aucune mutation canonique induite n'est observée.

## Qualification R2-006 — 20 septembre 2026

Le candidat apparié EXE 4DB02C62 / WASM 8FE20704 qualifie localement R2-006 :
transport Tauri u64 lossless, ouverture A/B, pagination 44 objets, sélections
des quatre types, reset interzone, continuité intrazone, filtre/retouche,
picking/annulation et suppression/Undo. Le rapport détaillé, les captures 12–42
et les limites sont sous `r2-006-cu/r2-006-v4-validation-20260920.md`.

Les premiers jeux synthétiques ont reproduit l'arrondi JavaScript de seed puis
deux budgets rivière insuffisants. Ils sont conservés comme preuves d'échec.
La fixture v4 utilise le profil de surface exigé et un budget 21 m dérivé d'une
borne complète de 20,941 m ; génération, tests et AuthoringHost A/B passent.

## Qualification R2-007 à R2-014 — 20 septembre 2026

Luna max a rejoué sur le candidat 4DB02C62 les cycles complets de relief,
peinture et effacement, les annulations de réglage, le picking/cercle et la
réouverture finale. L'archive v3 rouverte est à la révision 64, avec 422 deltas
de hauteur et zéro override matériau après l'effacement final attendu. Tests
world 28/28, engine terrain_update 10/10, tests projet/Studio ciblés et Clippy
pertinents passent ; le premier Clippy moteur a subi un crash rustc externe,
puis la reprise séquentielle a passé. Rapport et captures 01–92 sous
`r2-007-014-cu-20260920/`.

## Qualification R2-015 — 20 septembre 2026

Luna max qualifie localement le pinceau de densité sur candidat 4DB02C62 :
déverrouillage, point/trait, rayon/intensité, annulation après réglage,
réarmement, Apply/Undo/Redo, Save/Close/Open et inspection des deux masques.
Tests densité 5/5, inspection 2/2, editor_vegetation 13/13 et suites
authoring/historique passent. Rapport et captures 01–12 sous
`r2-015-validation-20260920/`.

## Qualification R2-016 — 20 septembre 2026

Luna max qualifie localement le catalogue d'espèces sur candidat 4DB02C62 :
provenance et GLB gouvernés, mélange/retrait, propagation oak sur deux couches
Tree, Preview/Apply, Undo/Redo et Save/Close/Open. La tête finale révision 5
contient quatre reçus et deux bindings exacts, sans duplication. Les tests
domaine couvrent le retry/idempotence ; la faute sidecar testée n'atteint pas le
catalogue embarqué et reste explicitement non revendiquée comme preuve UI.

## Qualification R2-017 — 20 septembre 2026

R2-017 ajoute un override auteur d'exclusions sans seconde source : tête v9,
archives v28/v29/v30, commande typée et inverse. Tests projet 3/3, world 1/1,
suites complètes, Studio natif/WASM et Clippy ciblés passent. Sur candidat
DA859D53 / WASM 5BF46B40, Luna max a observé héritage recette, Circle,
CorridorSegment, annulation sur champ, Apply, Undo/Redo, suppression et
Save/Close/Open. Rapport et captures sous `r2-017-validation-20260920/`.

## Qualification R2-019 — 20 septembre 2026

Sur candidat DA859D53, Luna max a observé Preview sans écriture, Abandon et
restauration, second Preview prêt en 9,1 ms, Apply, Undo/Redo et Save/Close/Open
au même hash. Tests reprojection, abandon, readiness et absence de doublons
passent. La présentation GPU reste explicitement non qualifiée. Rapport et
captures 16–28 sous `r2-019-validation-20260920/`.

## Qualification R2-021 — 20 septembre 2026

Sur candidat DA859D53, Luna max a validé la création multipoint, Preview sans
mutation, Apply, Undo/Redo, refus invalide, Save/Close/Open et la relecture du
chemin. Le même chemin accepté a ensuite été édité (largeur 3 → 4 m, dernier
point Z 5 → 10 m), prévisualisé, appliqué, annulé/rétabli et sauvegardé. Archive
finale B0D7B11F vérifiée sur disque. La seconde réouverture UI a rencontré un
sélecteur désactivé malgré une archive annoncée disponible ; cette limite ne
retire pas la preuve de création/édition et de persistance, mais reste ouverte
comme défaut d'interface. Rapport et captures sous
`artifacts/forge-runtime/gate2-resume-20260919/r2-021-validation-20260920/`.

## Qualification R2-022 — 20 septembre 2026

Le lot ajoute un profil vertical optionnel rétrocompatible aux chemins :
altitudes absolues par point, pente et dévers bornés, limite cut/fill vérifiée
sur le terrain effectif et collider Rapier du ruban profilé accepté. Sur candidat
6C1AB803 / WASM FADECB49, Luna max a acquis Preview sans mutation, Apply avec
acquittement CPU/Rapier, Undo/Redo, Save/Close/Open, relecture exacte et refus
d'un profil hors cut/fill sans mutation de l'archive DE2F8C5E. GPU et
intersections profilées restent non qualifiés. Rapport sous
`artifacts/forge-runtime/gate2-resume-20260919/r2-022-validation-20260920/`.

## Qualification R2-023 — 21 septembre 2026

Sur candidat 6C1AB803, Luna max a validé une seconde rivière quatre points :
Preview sans écriture, Apply avec terrain creusé/eau concordants et CPU/Rapier,
Undo/Redo, Save/Close/Open et archive 3EF87C85 persistante. Le noir observé
immédiatement après reprise était le warmup WebGPU : après ~11 à 14,5 s, terrain
et deux rivières sont visibles et stables, y compris en playtest puis au retour
édition. GPU, optique, navigation et physique de l'eau restent non qualifiés.
Rapports et captures textuelles sous
`artifacts/forge-runtime/gate2-resume-20260919/r2-023-validation-20260920/`.

**Statut : R2-004 et R2-006 à R2-023 qualifiés localement ; R2-024 est le premier critère ouvert ; Gate 2 ouverte.**

## Consolidation de la reprise — 22 septembre 2026

Le roadmap compte désormais 42 critères R2 clos localement sur 52. Les replays
automatisés natifs v14 placements et v3 bâtiment/playtest sur le candidat exact
`268B824946C53323C899276BC918BF5348F87E1A8CC2F6B50CB7A7C99B3804F5`
ont clos R2-028 à R2-033. Leur composition avec les contrats typés et preuves
antérieures clôt aussi R2-034 (bus), R2-035 (Preview/diff), R2-036 (Undo/Redo),
R2-040 (réouverture exacte) et R2-041 (Jouer/Retour).

La campagne de persistance fraîche
`E:/OpenRPG_Forge/.gate2-evidence/persistence-failure-20260922-v1/` a réussi
13/13 tests exacts et clôt localement R2-038. Avec le replay Studio explicite de
première sauvegarde interrompue, elle clôt R2-039 par composition. ADR-0014 a été
consolidé pour l'autorité unique, les overrides typés, les verrous et les conflits,
ce qui clôt localement le critère documentaire R2-044.

Les dix critères encore ouverts sont R2-037, R2-042, R2-043, R2-046 à R2-052.
Aucun de ces constats ne constitue une revue humaine, un PASS ADR-0006 ou une
qualification de Gate 2 : `gate_qualified=false`. CI distante :
`NOT RUN — infrastructure billing`. Aucun travail Gate 3 ni publication Git.

## Candidat local v8 — 22 septembre 2026

Le frontend release et le shell natif v8 ont été reconstruits avec caches et
temporaires sur E:, offline/locked. Les 468 entrées sont restées au manifeste
`AEF0DE612696A1B6052D57DC09B9AFE64F1CDACC024649829DC3B4A68768E6A8`.
Le candidat exact est `126ED7C7A96095132D78E590FC1208E826D0E5C9E293B0A187D73C96652D23AE`,
avec WASM `8DC3E06581536180E0241226D0154A169C27178F076823EC37E4269FAB515E5A`.
Il reste `BUILD_ONLY` jusqu'aux replays CDP ; détails et warning natif borné dans
`R2_CANDIDATE_V8_BUILD.md`.
