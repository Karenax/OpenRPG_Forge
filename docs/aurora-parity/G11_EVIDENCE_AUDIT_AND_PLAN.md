# G11 — Audit de preuves et plan de livraison

## Verdict initial

L'audit du 4 août 2026 couvre les 99 exigences affectées à G11 dans la matrice canonique :

- 3 étaient déjà `Implemented` (`ORF-BUILD-022`, `ORF-BUILD-023`, `ORF-QA-007`) ;
- 96 restaient `Researched` sans preuve projet ;
- le gate contient 6 P0, 52 P1 et 41 P2 ;
- les domaines les plus larges sont `EE` (20), `CONTENT` (18), `QA` (16) et `AREA` (12).

La simple présence du catalogue ou d'un asset dans le Vault n'est pas une preuve d'import de production. Chaque lot ci-dessous doit fournir code canonique, diagnostics stables, tests et limites explicites. Une promesse visuelle ou de performance exige respectivement une capture ou un benchmark.

## Lots bornés

Chaque lot contient cinq IDs au maximum et possède un résultat observable unique.

| Lot | Feature IDs | Résultat attendu |
|---|---|---|
| G11-A | ORF-CONT-013, ORF-CONT-014, ORF-EE-004, ORF-BUILD-023, ORF-QA-007 | Profils textures/modèles gouvernés, tâches et cache déterministes, NOTICE et blocage licence. |
| G11-B | ORF-CONT-008, ORF-CONT-015, ORF-CONT-016, ORF-CONT-017, ORF-EE-010 | Manifeste d'assets avec collisions, animations, VFX et LOD. |
| G11-C | ORF-CONT-021, ORF-CONT-022, ORF-CONT-025, ORF-CONT-026, ORF-CONT-027 | Round-trip externe/Blender optionnel, migration et désinstallation sûre. |
| G11-D | ORF-CONT-028, ORF-CONT-029, ORF-BUILD-016, ORF-BUILD-017, ORF-BUILD-024 | Signatures, deltas et distributions Windows/multi-plateformes par canal. |
| G11-E | ORF-CONT-004, ORF-CONT-005, ORF-CONT-018, ORF-BUILD-007, ORF-BUILD-012 | Imports/exports en masse, tables de données et fusion assistée. |
| G11-F | ORF-CONT-012, ORF-EE-026, ORF-EE-003, ORF-PAL-009, ORF-PAL-017 | UI scalable, onglets de zone, favoris et compatibilité artistique. |
| G11-G | ORF-EE-005, ORF-EE-006, ORF-EE-007, ORF-EE-008, ORF-AREA-019 | PBR, HDR, éclairage et ciel de référence. |
| G11-H | ORF-AREA-017, ORF-AREA-018, ORF-AREA-020, ORF-GAME-018, ORF-AUD-015 | Environnement vent/météo/eau et réverbération runtime. |
| G11-I | ORF-AREA-004, ORF-AREA-005, ORF-AREA-032, ORF-WPT-011, ORF-EE-023 | Terrain multi-biome, features et navigation volumétrique. |
| G11-J | ORF-PROJ-012, ORF-PROJ-013, ORF-AREA-023, ORF-AREA-026, ORF-PROJ-015 | Événements de repos/heartbeat, interdiction et abort contrôlé. |
| G11-K | ORF-PROJ-018, ORF-GAME-019, ORF-WPT-013, ORF-EE-001, ORF-EE-027 | Séquences/cutscenes, ancres caméra et transforms interpolés. |
| G11-L | ORF-GAME-003, ORF-GAME-004, ORF-GAME-011, ORF-GAME-015, ORF-GAME-016 | Déplacements, menu radial, pause tactique et modes de combat. |
| G11-M | ORF-PROJ-017, ORF-PROJ-023, ORF-GAME-024, ORF-AREA-025, ORF-AREA-027 | Progression, difficulté, perception et règles de zone. |
| G11-N | ORF-AUD-010, ORF-AUD-016, ORF-AUD-017, ORF-WPT-010, ORF-AREA-028 | Audio spatial/événementiel, patrouilles inter-zones et chargement. |
| G11-O | ORF-EE-011, ORF-EE-012, ORF-EE-013, ORF-EE-015, ORF-EE-016 | Règles data-driven, UI scriptable et sérialisation runtime. |
| G11-P | ORF-EE-019, ORF-EE-022, ORF-EE-024, ORF-EE-031, ORF-BUILD-020 | Copie runtime, indicateurs, NUI, compilateur et documentation générée. |
| G11-Q | ORF-BUILD-021, ORF-BUILD-025, ORF-QA-022, ORF-QA-028, ORF-CONT-009 | Manifeste de tâches, artefacts de crash, santé, récupération et synchronisation. |
| G11-R | ORF-QA-006, ORF-QA-009, ORF-QA-011, ORF-QA-012, ORF-QA-013 | Validation localisation et tests ForgeScript/narration/rencontres. |
| G11-S | ORF-QA-014, ORF-QA-016, ORF-QA-017, ORF-QA-018, ORF-QA-019 | Tests réseau/visuels, benchmarks, budgets et profilage intégré. |
| G11-T | ORF-QA-023, ORF-QA-024, ORF-QA-025, ORF-BUILD-022 | Accessibilité éditeur/runtime, fuzzing sécurité et gouvernance finale. |

## Critères de passage communs

- formats et schémas publics versionnés, migrations testées ;
- aucune dépendance à Blender ou à un service distant au runtime ;
- build et cache déterministes entre cultures Windows ;
- licence, provenance, hash et budget présents pour chaque asset distribué ;
- mutations d'auteur via commandes transactionnelles avec dry-run et undo/redo ;
- validation globale, graphe frais, captures et benchmarks attachés aux lots concernés.

## Avancement

**G11-A est fermé le 4 août 2026.** Les profils texture/modèle sont persistés canoniquement,
modifiés par une commande transactionnelle avec dry-run et undo/redo, puis consommés par un
exécuteur confiné au workspace. Un GLB Kenney et une texture ambientCG du Vault ont été traités de
façon reproductible avec contrôle des hashes source avant/après. La preuve détaillée est dans
[`tasks/G11-A-ASSET-PROCESSING-PROFILES.md`](tasks/G11-A-ASSET-PROCESSING-PROFILES.md).

**G11-B est fermé le 4 août 2026.** Les sorties d'import portent désormais un manifeste
content-addressed avec plateformes, collisions, animations, VFX et LOD ; elles sont sauvegardées,
exportées et vérifiées par le runtime sans dépendre d'une scène moteur. La preuve détaillée est dans
[`tasks/G11-B-ASSET-PACKAGE-MANIFESTS.md`](tasks/G11-B-ASSET-PACKAGE-MANIFESTS.md).

**G11-C est fermé le 5 août 2026.** Le round-trip externe est canonique, persistant et
transactionnel ; un worker Blender optionnel consomme uniquement des opérations typées et confinées.
La parité de plateforme, les migrations de confiance et les désinstallations avec analyse d’impact
sont couvertes par le domaine. La preuve détaillée est dans
[`tasks/G11-C-EXTERNAL-ROUNDTRIP-AND-LIFECYCLE.md`](tasks/G11-C-EXTERNAL-ROUNDTRIP-AND-LIFECYCLE.md).

## Prochaine tâche unique

Démarrer **G11-D** : signatures, mises à jour delta et distribution multi-plateforme gouvernée.
