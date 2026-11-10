# Backlog consommateur — OpenRPG Forge

**Extrait du backlog maître OpenNever du 7 novembre 2026.**
Le backlog maître fait foi :
`E:\OpenNever_Forge\docs\aurora-bridge\BACKLOG.md`.
En cas de divergence, corriger cet extrait sans modifier les IDs ni les critères maîtres.

## Règles locales

- M1 ajoute `forge_aurora_bridge` (lecture seule) et M2 `forge_aurora_runtime`/`forge_aurora_visual` ; aucun runtime NCS ni ruleset D&D n'existe encore dans ce dépôt.
- Ne pas modifier les quotas natifs (32 scripts, 256 pas, 128 variables narratives, 32 factions).
- Ne pas créer de dépendance Cargo `path` vers OpenNever.
- Ne pas ouvrir Gate 8/9 ni modifier le verdict d'une gate.
- Les preuves vont dans `E:\OpenRPG_Forge\Preuves-et-analyses\actives\aurora-bridge\`.
- Un import non exécuté est une prévisualisation, pas une conversion jouable.

## M0 — Cadrage et baselines

### AB-M0-01 — Baseline et inventaire du checkout
- **État :** done (instantané) · **Dépend :** —
- **Livrable :** HEAD, branche, état Git, fichiers non suivis, commandes de test.
- **Acceptation :** valeurs datées et distinctes des hypothèses.

### AB-M0-04 — ADR 0063
- **État :** done · **Dépend :** AB-M0-02
- **Livrable :** `docs/ADR/ADR-0063-aurora-bridge-import.md`.
- **Acceptation :** rôle consommateur, crates, ruleset séparé et piste hors gates actés.

### AB-M0-10 — Campagne de preuves et corpus
- **État :** done (initial) · **Dépend :** AB-M0-01
- **Livrable :** `Preuves-et-analyses/actives/aurora-bridge/{README,STATUS}.md`, `corpus/README.md`.
- **Acceptation :** aucun asset propriétaire dans Git ; politique E:/F: respectée.

### AB-M0-11 — Baselines techniques
- **État :** done (partielle) · **Dépend :** AB-M0-01
- **But :** exécuter les contrôles admissibles du workspace et noter les résultats réels.
- **Livrable :** commandes, versions, sorties, échecs.
- **Acceptation :** aucun test non lancé n'est `done`.
- **Résultat 7 nov. 2026 :** tests ciblés `forge_project`/`forge_gameplay` OK (517 réussis,
  2 ignorés) ; fmt échoue sur 11 fichiers préexistants ; Clippy ciblé sur 2 diagnostics préexistants.

### AB-M0-14 — Verrou de contrat (gelé)
- **État :** done (gelé le 7 nov. 2026) · **Dépend :** AB-M0-08
- **Livrable :** `CONTRACT.lock.json` statut `frozen` + miroir `contract/` SHA-256.
- **Acceptation :** le SHA du miroir correspond au verrou et un test refuse la dérive.
- **Résultat 7 nov. 2026 :** `frozen=true`, SHA-256
  `342b331620cd8f564b7745cf6c7593ff11762bfe90293287f0ec82806fa8a999`, test
  `validated_bundle_matches_the_contract_lock` réussi.

### AB-M0-16 — Audit local multi-zone, sauvegarde et runtime
- **État :** done (audit) · **Dépend :** AB-M0-11
- **But :** confirmer le comportement réel de `ModulePackageManifest.zones`, du runtime mono-zone,
  de `GameplayDocument.area_id`, de `save.rs`, des quotas et de la navigation.
- **Livrable :** note + tests ciblés (`CAPABILITY_AUDIT.md`).
- **Acceptation :** chaque capacité est constatée/testée/absente ; aucun merge de zones supposé.
- **Résultat 7 nov. 2026 :** 58 tests manifeste + 9 tests sauvegarde + 95 tests outil réussis ;
  runtime et sauvegarde restent mono-zone.

## M1 — Consommateur vide

**Sortie M1 :** le bundle synthétique est validé, hashé, mappé et exposé en lecture seule ; aucun
import dans le projet ni runtime.

**Résultat au 7 novembre 2026 :** M1 terminé localement. Le bundle `ab_proving` produit par
OpenNever est commité comme fixture consommateur
(`crates/forge_aurora_bridge/tests/fixtures/ab_proving.module-migration-v1/`), validé et importé
par `forge_aurora_bridge`. Le miroir du schéma est gelé par `CONTRACT.lock.json` (SHA-256
`342b331620cd8f564b7745cf6c7593ff11762bfe90293287f0ec82806fa8a999`). Le harness
`E:\OpenNever_Forge\scripts\aurora_bridge_m1_integration.ps1` prouve que l'arbre producteur et le
fixture consommateur sont byte-identiques. Aucune dépendance Cargo `path` entre les deux dépôts.

### AB-M1-17 — Crate `forge_aurora_bridge`
- **État :** done · **Dépend :** AB-M0-14, AB-M1-16 (maître)
- **But :** lecteur/validateur strict du bundle module, sans parser NWN ni dépendance OpenNever.
- **Livrables :** crate isolée, modèles, erreurs typées, limites, tests.
- **Acceptation :** version, hash, chemin, taille ou référence invalide refusé avant lecture profonde ;
  aucune dépendance Tidewater.

### AB-M1-18 — Miroir et gel du contrat
- **État :** done · **Dépend :** AB-M0-12, AB-M1-15 (maître)
- **But :** copier le schéma gelé et enregistrer version + SHA-256 + commit source.
- **Livrables :** `docs/aurora-bridge/contract/`, `CONTRACT.lock.json` statut `frozen`.
- **Acceptation :** SHA du miroir = verrou ; test de dérive.

### AB-M1-19 — Mapping des coordonnées
- **État :** done · **Dépend :** AB-M1-17
- **But :** appliquer la convention NWN → canonique `[x,z,-y]` puis le repère Forge, sans double conversion.
- **Livrables :** adaptateur + tests positions, bearings, quarts de tour, quaternions, unités.
- **Acceptation :** aller-retour stable ; cas non fini refusé.

### AB-M1-20 — Modèle importé en lecture seule
- **État :** done · **Dépend :** AB-M1-17, AB-M1-19
- **But :** exposer zones, objets, catalogues, scripts, dialogues, journal et diagnostics sans muter le projet.
- **Livrables :** `ImportedModule`/`ImportedArea` + rapport.
- **Acceptation :** 2 zones et tous les objets du module synthétique listés ; aucun import partiel.

### AB-M1-21 — Test d'intégration producteur → consommateur
- **État :** done · **Dépend :** AB-M1-05 (maître), AB-M1-20
- **But :** produire le bundle avec OpenNever puis le valider/importer avec OpenRPG.
- **Livrables :** artefact de test versionné ou généré + test automatisé.
- **Acceptation :** aucune dépendance Cargo `path` ; reproductible hors installation NWN.

### AB-M1-22 — Rapport de sortie M1
- **État :** done · **Dépend :** AB-M1-21
- **Livrables :** `PROGRESS.md`, `STATUS.md`, matrice.
- **Acceptation :** chaque critère M1 rapproché d'une preuve ou marqué non exécuté.

## M2 — Première scène jouable

**Sortie M2 :** deux zones chargées, objets projetés, collision/navigation, visite réelle, porte et
transition aller-retour. Aucun script NCS ni règle D&D complète.

**Résultat au 7 novembre 2026 :** M2 terminé localement avec réserves. `forge_aurora_runtime`
projette le bundle `ab_proving` en package Forge v2, ingère 26 assets, convertit 2800 triangles WOK
en 133 polygones `forge_navigation`, expose un `GameplayDocument` par zone et exécute deux
transitions. La preuve visuelle Bevy (`camp.png`, `crypt-after-transition.png`) et la visite headless
(285,38 m, 1175 ticks) sont dans `Preuves-et-analyses/actives/aurora-bridge/m2-10/`. Aucun modèle
NWN réel n'est chargé, aucun script NCS ni ruleset D&D n'est exécuté.

### AB-M2-02 — Assistant d'import Studio
- **État :** done (partiel : commandes natives + CLI, écran Leptos non livré) · **Dépend :** AB-M1-20
- **But :** parcours sélection → audit → aperçu → import, avec résumé des assets/scripts/blocages.
- **Livrable :** écran/commandes + diagnostics lisibles.
- **Acceptation :** un import non joué est présenté comme prévisualisation.

### AB-M2-03 — Projection vers le conteneur multi-zone
- **État :** done · **Dépend :** AB-M1-20, AB-M0-16
- **But :** créer un module Forge avec zones, objets sources, assets et références.
- **Livrable :** mapping vers `forge_project::module_package` et documents de zone.
- **Acceptation :** 2 zones conservées ; identités stables ; aucun objet perdu.

### AB-M2-04 — Ingestion des assets
- **État :** done · **Dépend :** AB-M1-20
- **But :** charger GLB, textures, sons et animations via le pipeline gouverné.
- **Livrable :** adaptateur + provenance/licence + diagnostics.
- **Acceptation :** aucun chargeur dupliqué ; asset manquant diagnostiqué.

### AB-M2-05 — Collision et navigation WOK/PWK/DWK
- **État :** done (partiel : walkmesh + forge_navigation, pas de corps Rapier dédié) · **Dépend :** AB-M1-20
- **But :** convertir la géométrie préservée en collision et navmesh utilisables.
- **Livrable :** parseur/adaptateur borné, surfaces, portes, liens inter-tuiles.
- **Acceptation :** `forge_navigation`/Rapier valident le navmesh ; preuve de déplacement.

### AB-M2-06 — Projection des objets de zone
- **État :** done · **Dépend :** AB-M2-03, AB-M2-04
- **But :** tuiles, placeables, créatures, portes, triggers, waypoints, spawns, lumières, sons.
- **Livrable :** adaptateurs par type + diagnostics.
- **Acceptation :** positions/orientations correctes ; types non supportés visibles.

### AB-M2-07 — Squelette de session multi-zone
- **État :** done · **Dépend :** AB-M0-16, AB-M2-03
- **But :** état de module multi-zone sans casser le runtime mono-zone.
- **Livrable :** `ModuleSession` ou équivalent, registre de zones, transitions.
- **Acceptation :** runtime existant intact ; aucune duplication d'état.

### AB-M2-08 — Transitions inter-zones
- **État :** done · **Dépend :** AB-M2-07
- **But :** charger/décharger une zone, conserver identité, orientation et objets.
- **Livrable :** transition aller-retour + tests.
- **Acceptation :** deux transitions ne dupliquent ni joueur ni objets.

### AB-M2-09 — `GameplayDocument` par zone
- **État :** done · **Dépend :** AB-M2-07
- **But :** représenter les objets gameplay sans gonfler les quotas natifs.
- **Livrable :** mapping documenté + tests de limites.
- **Acceptation :** quotas natifs inchangés ; dépassement NWN diagnostiqué, pas tronqué.

### AB-M2-10 — Première visite réellement exécutée
- **État :** done (partiel : preuve headless + rendu simplifié, modèles NWN non chargés) · **Dépend :** AB-M2-02..09
- **But :** lancer le runtime, visiter les deux zones, ouvrir une porte, franchir une transition.
- **Livrable :** captures/mesures, journal, hash du bundle.
- **Acceptation :** preuve d'exécution réelle ; limites consignées.

### AB-M2-11 — Régressions M2
- **État :** done · **Dépend :** AB-M2-10
- **But :** vérifier gameplay natif, assets, navigation et gates existantes.
- **Livrable :** tests + rapport.
- **Acceptation :** aucune régression non expliquée.

### AB-M2-12 — Matrice après M2
- **État :** done · **Dépend :** AB-M2-10
- **But :** requalifier `importable`, `renderable`, `navigable`.
- **Livrable :** matrice mise à jour.
- **Acceptation :** chaque case renvoie à une preuve datée.

## M3 — Quête scriptée de bout en bout

**Sortie M3 :** événement, NCS, variable, dialogue conditionnel, journal, objet, porte, combat et
sauvegarde/reprise. Les natives non supportées sont visibles.

**État M3 au 8 novembre 2026 :** terminé localement avec réserves. Le scénario `ab_proving`,
le parseur NCS borné, la VM, le registre de natives, le dialogue conditionnel, le journal, l'objet,
la porte, le combat et la reprise sont prouvés par `cargo test -p forge_nwn_runtime` et
`forge_cli aurora-scenario`. Les natives d'action, la suspension au milieu d'une exécution, le
mapping JRL/TLK complet et les événements non listés restent non supportés et visibles. Preuve :
`Preuves-et-analyses/actives/aurora-bridge/m3/README.md` ; spécification :
`docs/aurora-bridge/M3_RUNTIME.md`.

### AB-M3-01 — Spécification des événements NWN
- **État :** done · **Dépend :** AB-M2-10
- **Livrable :** spécification versionnée + table de couverture.
- **Acceptation :** chaque événement annoncé a un test ; les autres sont listés.

### AB-M3-02 — Parseur/validateur NCS
- **État :** done · **Dépend :** AB-M3-01
- **But :** lire en-tête, instructions, offsets, pile et types sans exécuter d'octets non validés.
- **Livrable :** parseur, erreurs typées, fuzz.
- **Acceptation :** bytecode invalide refusé ; aucun panic ; limites de taille.

### AB-M3-03 — VM `NcsVm` déterministe
- **État :** done · **Dépend :** AB-M3-02
- **But :** frames, sauts, types, `OBJECT_SELF`, budgets, suspension.
- **Livrable :** VM + tests opcode/stack/jumps/contextes.
- **Acceptation :** résultats reproductibles ; erreurs traçables.

### AB-M3-04 — Registre de natives minimal
- **État :** done · **Dépend :** AB-M3-03
- **But :** uniquement les natives du module synthétique puis du corpus prioritaire.
- **Livrable :** registre par ID/signature/version, journal `unsupported-native-call`.
- **Acceptation :** aucune native non implémentée silencieuse.

### AB-M3-05 — Ordonnanceur, actions et timers
- **État :** done (partiel) · **Dépend :** AB-M3-03
- **But :** `AssignCommand`, `ActionDoCommand`, `DelayCommand`, `ExecuteScript`, heartbeat, files d'action.
- **Livrable :** ordonnanceur + tests de délais/reprise.
- **Acceptation :** aucun contexte perdu ; aucun doublon après reprise.

### AB-M3-06 — Adaptateur sémantique NCS → commandes Forge
- **Avancement :** Politique d'effets `EffectPolicy`, refus atomique et reçus séquencés ; pas de bus `forge_gameplay`.
- **État :** done (partiel) · **Dépend :** AB-M3-04, AB-M3-05
- **But :** passer par les autorités de gameplay/physique/permissions, transactions et receipts.
- **Livrable :** adaptateur + tests d'intégration.
- **Acceptation :** aucune écriture arbitraire dans l'ECS.

### AB-M3-07 — Dialogue conditionnel
- **État :** done · **Dépend :** AB-M3-06
- **But :** exécuter conditions/actions DLG et afficher les branches selon les variables.
- **Livrable :** runtime de dialogue + mapping texte/TLK.
- **Acceptation :** deux branches du module synthétique prouvées.

### AB-M3-08 — Journal et quête
- **Avancement :** Statuts `QuestStatus` persistés (activation, complétion, échec) en plus du journal ; JRL complet non projeté.
- **État :** done (partiel) · **Dépend :** AB-M3-06
- **But :** projeter JRL et transitions de quête sans écraser le ruleset natif.
- **Livrable :** mapping quête/états/récompenses + tests.
- **Acceptation :** activation, completion et échec persistants.

### AB-M3-09 — Porte, objet et combat déclenchés par script
- **Avancement :** OnOpen/OnClose reliés aux portes via `SetDoorState` et scripts optionnels ; fixture sans scripts de porte dédiés.
- **État :** done (partiel) · **Dépend :** AB-M3-06
- **But :** relier OnUsed/OnDisturbed/OnDeath/OnOpen/OnClose aux objets et à la quête.
- **Livrable :** adaptateurs + scénario.
- **Acceptation :** objet ramassé, hostile vaincu, porte/transition fonctionnelle.

### AB-M3-10 — Sauvegarde/reprise VM et quête
- **Avancement :** `continuation` VM optionnelle dans `M3Save` et test de reprise JSON ; non suspendue au milieu du scénario M4.
- **État :** done (partiel) · **Dépend :** AB-M3-07..09
- **But :** persister variables, inventaire, quête, portes, timers et continuation VM.
- **Livrable :** schéma + tests de reprise.
- **Acceptation :** rechargement sans rejouer les init ni dupliquer les récompenses.

### AB-M3-11 — Diagnostics des natives/événements non supportés
- **État :** done · **Dépend :** AB-M3-04
- **Livrable :** rapport par script/événement + statuts.
- **Acceptation :** un module partiel n'est jamais annoncé complet.

### AB-M3-12 — Scénario de bout en bout et preuve
- **État :** done (partiel) · **Dépend :** AB-M3-10
- **Livrable :** session, captures, journal, hashes, rapport.
- **Acceptation :** les 6 critères GO/NO-GO sont rapprochés d'une preuve ou marqués non exécutés.

### AB-M3-13 — Matrice après M3
- **État :** done · **Dépend :** AB-M3-12
- **Livrable :** `COMPATIBILITY_MATRIX.md`.
- **Acceptation :** portée limitée au scénario prouvé.

## M4 — Compatibilité gameplay NWN

**Sortie M4 :** profil NWN séparé, combat, sorts, objets, IA, RNG et sauvegardes multi-zones, avec
écarts connus et règles natives intactes.

**État M4 au 8 novembre 2026 :** terminé localement avec réserves. `forge_nwn_rules` fournit le
profil `forge.nwn-rules-profile@1`, un RNG à graine, le combat d20 borné, les personnages, objets,
sorts/repos, factions/IA et la quête. `forge_nwn_runtime` exécute `M4Scenario` sur `ab_proving` :
le gardien attaque, blesse le combattant, est vaincu, la quête et la sauvegarde M4 sont reprises
sans doublon. Les tables 2DA, les attaques spéciales, les sorts complets, la perception, le
pathfinding de navigation et l'oracle NWN réel restent hors preuve. Preuve :
`Preuves-et-analyses/actives/aurora-bridge/m4/README.md` ; spécification :
`docs/aurora-bridge/M4_GAMEPLAY.md`.

### AB-M4-01 — Frontière `NwnRulesProfile`
- **État :** done · **Dépend :** AB-M3-12
- **But :** séparer ruleset NWN et ruleset action-RPG natif ; définir la portée.
- **Livrable :** ADR/contrat de profil + tests de non-régression.
- **Acceptation :** aucun combat natif modifié ; profil sélectionnable.

### AB-M4-02 — Combat d20 et mécaniques NWN
- **Avancement :** Attaques spéciales `power-attack`, `improved-power-attack`, `called-shot`, `knockdown`, `stunning-fist` et jets de sauvegarde.
- **État :** done (partiel) · **Dépend :** AB-M4-01
- **But :** d20, AC, BAB, attaques multiples, critiques, initiative, saves, DC, résistances, DR.
- **Livrable :** moteur de règles + tables de cas chiffrés.
- **Acceptation :** déterministe avec graine ; écarts documentés.

### AB-M4-03 — Personnages, classes, feats, compétences
- **État :** done (partiel) · **Dépend :** AB-M4-01
- **But :** statistiques, races/classes, niveaux, HP, XP, skills, feats, multiclasses, compagnons.
- **Livrable :** modèles data-driven + tests.
- **Acceptation :** aucune valeur codée en dur non sourcée.

### AB-M4-04 — Objets, équipement, inventaires, magasins
- **Avancement :** Magasin achat/vente transactionnel avec stock, or et refus atomique ; loot tables non couvertes.
- **État :** done (partiel) · **Dépend :** AB-M4-01
- **But :** slots, poids, stacks, inventaires imbriqués, propriétés magiques, stores, loot.
- **Livrable :** modèles + transactions.
- **Acceptation :** aucun item dupliqué/perdu ; propriétés inconnues visibles.

### AB-M4-05 — Sorts, effets et repos
- **Avancement :** Sort de zone `sleep`, contrôle avec jet de Volonté, plafond d'effets ; concentration/dissipation non couvertes.
- **État :** done (partiel) · **Dépend :** AB-M4-01
- **But :** slots/jour, repos, ciblage, durée, concentration, immunités, dissipation, AoE.
- **Livrable :** registre data-driven + tests.
- **Acceptation :** effets persistés correctement ; VFX non supportés diagnostiqués.

### AB-M4-06 — IA, factions et pathfinding
- **Avancement :** Garde de perception déterministe et pas de mêlée ; poursuite/fuite et pathfinding non couverts.
- **État :** done (partiel) · **Dépend :** AB-M4-01
- **But :** perception, hostilité, factions, combat, poursuite/fuite, scripts custom, traversée de zones.
- **Livrable :** IA de compatibilité + tests.
- **Acceptation :** déterministe avec graine ; aucune IA générative obligatoire.

### AB-M4-07 — Quêtes, récompenses et persistance
- **État :** done (partiel) · **Dépend :** AB-M4-01
- **But :** journal, variables locales/globales, récompenses/échecs, conséquences.
- **Livrable :** modèle de quête + migration M3.
- **Acceptation :** quêtes persistantes après transitions et rechargement.

### AB-M4-08 — RNG à graine et déterminisme
- **État :** done · **Dépend :** AB-M4-02
- **But :** jets, IA et délais reproductibles.
- **Livrable :** RNG injectable + journal de graines.
- **Acceptation :** même graine + mêmes entrées = même trace.

### AB-M4-09 — Comparaison oracle
- **Avancement :** `aurora-oracle-log` lit un log externe `forge.nwn-oracle-log@1` et le compare au rapport M4 ; oracle NWN réel non exécuté.
- **État :** done (partiel) · **Dépend :** AB-M4-08
- **But :** comparer scénarios NWN locaux autorisés et Forge.
- **Livrable :** harnais + rapport par divergence.
- **Acceptation :** aucune divergence non documentée dans les fonctions `exact`.

### AB-M4-10 — Sauvegarde multi-zone avancée
- **Avancement :** Portes, états de quête, politique d'effets et continuation optionnelle dans la sauvegarde ; factions non sérialisées séparément.
- **État :** done (partiel) · **Dépend :** AB-M4-07
- **But :** zones visitées, objets détruits/ouverts, inventaires, factions, quêtes, casts, timers, VM.
- **Livrable :** schéma versionné + tests corruption/limites.
- **Acceptation :** reprise après combat, dialogue et timer sans duplication.

### AB-M4-11 — Budgets de performance
- **Avancement :** Mesure CPU et empreinte headless (pic 9,2 Mo) ; FPS et chargement GLB non mesurés.
- **État :** done (partiel) · **Dépend :** AB-M4-10
- **But :** import froid/chaud, mémoire, FPS, changement de zone, temps VM, chargement GLB.
- **Livrable :** benchmarks + budgets.
- **Acceptation :** machine identifiée ; régression bloquante.

### AB-M4-12 — Matrice après M4
- **État :** done · **Dépend :** AB-M4-11
- **Livrable :** matrice + rapport.
- **Acceptation :** chaque `exact`/`converted` a une preuve.

## M5 — Campagne avancée et qualification

**État M5 au 8 novembre 2026 :** **partiel, non clos.** La migration de sauvegarde M4 → M5, la
trace reproductible, `aurora-diff-oracle`, `aurora-oracle-log`, les transitions répétées, le budget
VM, le panneau Studio de diagnostic et le guide utilisateur sont livrés et testés sur `ab_proving`.
Le module NWN hétérogène réel, la preuve visuelle Studio, l'oracle NWN local, les GLB du bundle dans
un viewer Khronos, les textes de licence transitifs et l'archive finale ne sont pas prouvés. Preuve : `Preuves-et-analyses/actives/aurora-bridge/m5/README.md` ; rapport :
`docs/aurora-bridge/M5_FINAL_REPORT.md`.

### AB-M5-01 — `ModuleSession` complet
- **État :** done (partiel) · **Dépend :** AB-M4-10
- **But :** registry, zones, ownership, scripts globaux, temps/round, RNG, joueurs, compagnons, journal.
- **Livrable :** session complète + tests de cycle de vie.
- **Acceptation :** chargement → init → entrée → events → transition → sauvegarde/reprise.

### AB-M5-02 — Versionnement et migration des sauvegardes
- **État :** done · **Dépend :** AB-M5-01
- **But :** schéma indépendant du hash des assets ; migration explicite des IDs.
- **Livrable :** migrations + refus explicite.
- **Acceptation :** aucune sauvegarde native Forge modifiée.

### AB-M5-03 — Chargements et transitions répétés
- **État :** done · **Dépend :** AB-M5-01
- **But :** reprise après timer, dialogue, combat ; transitions multiples.
- **Livrable :** régression + traces.
- **Acceptation :** aucun doublon item/quête/timer.

### AB-M5-04 — Diagnostics et traces dans le Studio
- **Avancement :** Panneau Leptos `AuroraM5Panel` compilé wasm, appelant `run_aurora_gameplay` ; aucune capture visuelle.
- **État :** done (partiel) · **Dépend :** AB-M4-12
- **But :** diagnostic → ressource/instance/script/table ; chaîne d'override ; conditions de dialogue.
- **Livrable :** écrans/commandes + preuves UX.
- **Acceptation :** debug hors interface métier ; diagnostics dédiés.

### AB-M5-05 — Traces reproductibles
- **État :** done · **Dépend :** AB-M5-04
- **But :** `event → script → API → mutation → receipt`, IDs stables, seeds, export expurgé.
- **Livrable :** pipeline activable.
- **Acceptation :** pas de surcharge normale ; aucun secret.

### AB-M5-06 — `diff-oracle`
- **Avancement :** `aurora-diff-oracle` et `aurora-oracle-log` ; oracle NWN réel non exécuté.
- **État :** done (partiel) · **Dépend :** AB-M4-09
- **But :** comparer NWN local autorisé et Forge avec mêmes entrées.
- **Livrable :** commande + rapport normalisé.
- **Acceptation :** reproductible ; aucun asset propriétaire.

### AB-M5-07 — Fuzz, robustesse et sécurité
- **Avancement :** NCS/save/profil/boucle VM et symlinks refusés ; archives non ingérées.
- **État :** done (partiel) · **Dépend :** AB-M1-15, AB-M3-02
- **But :** archives, chemins, symlinks, traversal, bombes, tailles, CPU/mémoire, timeouts, JSON, scripts.
- **Livrable :** corpus + fuzz + limites.
- **Acceptation :** aucun panic sur entrée utilisateur.

### AB-M5-08 — Échelle et performance
- **Avancement :** Audit structurel de 3 modules réels (42/25/29 zones) et empreinte headless ; FPS absent.
- **État :** done (partiel) · **Dépend :** AB-M4-11
- **But :** module hétérogène plus gros, plusieurs zones, assets et scripts.
- **Livrable :** benchmarks + budgets.
- **Acceptation :** limites affichées ; pas de support universel promis.

### AB-M5-09 — Validation glTF et navigation
- **État :** done (partiel) · **Dépend :** AB-M2-10
- **But :** Khronos glTF Validator + scènes de collision/navigation.
- **Livrable :** rapports + corrections bornées.
- **Acceptation :** erreurs classées ; aucune géométrie silencieusement ignorée.

### AB-M5-10 — SBOM, licences et notices
- **État :** done · **Dépend :** AB-M0-12
- **But :** SBOM, `THIRD_PARTY_NOTICES`, licences, provenance.
- **Livrable :** artefacts de livraison.
- **Acceptation :** aucun module/asset NWN redistribué sans droits.

### AB-M5-11 — Documentation utilisateur et mainteneur
- **État :** done · **Dépend :** AB-M5-10
- **But :** import local, réserves, statuts, reprise, sans IA obligatoire.
- **Livrable :** guide + documentation.
- **Acceptation :** l'utilisateur comprend jouable/partiel/non supporté.

### AB-M5-12 — Rapport final de compatibilité
- **État :** done · **Dépend :** AB-M5-01..11
- **But :** consolider preuves et limites.
- **Livrable :** matrice finale + rapport.
- **Acceptation :** aucun « module complet jouable » sans preuve.

### AB-M5-13 — Clôture et archivage
- **État :** todo · **Dépend :** AB-M5-12
- **But :** archiver selon E:/F:, mesurer le résidu.
- **Livrable :** ZIP64 vérifié + manifeste + catalogue + bilan.
- **Acceptation :** lecture intégrale vérifiée ; originaux retirés après contrôle.

## Rappel des dépendances critiques

```text
AB-M0-16 -> AB-M1-17 -> AB-M1-20 -> AB-M2-03 -> AB-M2-05 -> AB-M2-10
  -> AB-M3-02 -> AB-M3-03 -> AB-M3-12 -> AB-M4-10 -> AB-M5-01 -> AB-M5-12
```

La licence (`AB-M0-12`) ne bloque que la publication et la distribution, pas les audits et les
tests locaux.
