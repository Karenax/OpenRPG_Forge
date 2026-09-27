# R1-040 — plan de génération végétale partagé entre profils Web

11 septembre 2026. **Implémenté, testé et mesuré localement.** Gate 1 reste
ouverte ; le cycle complet en mouvement n'est pas qualifié.

## Changement générique

Le chemin Web garde un unique `VegetationGenerationPlan` immuable, indexé par
les deux hashes terrain/végétation. Il réutilise le cache canonique déjà fourni
par ce plan, au lieu de reconstruire un plan à chaque changement de qualité.
Les règles, seeds, placements, densités par profil, meshes et collisions ne sont
pas modifiés. La préparation utilise toujours le même code géométrique que le
chemin synchrone. Le comportement des workers natifs est inchangé.

Un changement de source remplace le plan. Un résultat en cours devenu périmé
est refusé avant toute mutation des projections vivantes. Un seul plan source
est retenu, pas un jeu de meshes pour chaque profil. Ce cache retient le scatter
canonique en mémoire : son coût mémoire n'a pas été quantifié ici, et la première
génération à froid reste nécessaire. Ce n'est pas un gain sans coût mémoire.

## Vérifications

- `cargo fmt --check` et `git diff --check` PASS.
- Clippy strict workspace/all-targets/all-features et Web PASS.
- Tests workspace/all-targets/all-features : **707 PASS, deux sondes ignorées**.
- Nouveau test : identité partagée du plan et de la génération canonique,
  invalidation séparée terrain/végétation, rejet d'une source invalide.
- Test existant renforcé : rejet d'un travail périmé après édition des sources,
  puis remplacement du plan. Comparaison exacte des statistiques, attributs et
  indices des meshes avec le chemin synchrone conservée.
- Frontend release construit en 2 min 27 s, shell en 27,65 s. Avertissement
  futur `proc-macro-error2` conservé. Aucun changement de dépendances.

## Mesure avant/après

Baseline : `R1_040_FOREST_BOUNDS_REUSE.md`. Même machine, viewport 1408×966,
protocole `prewarmed_bridge_cycle`, kit, forêt, eau/nuages et parcours de 42,53 m.
Protocole et bindings source des deux enregistrements mobiles comparés : identiques.
Une paire de sessions, pas un benchmark statistique de performance globale.

| Mesure (ms) | Avant Low | Après Low | Avant retour High | Après retour High |
| --- | ---: | ---: | ---: | ---: |
| Maximum préparation végétale CPU | 175,8 | 24,5 | 183,4 | 24,2 |
| Maximum commit végétal CPU | 7,1 | 7,9 | 8,0 | 7,8 |
| Maximum forêt inclusive CPU | 24,8 | 26,3 | 24,0 | 22,9 |
| Maximum intervalle de frame de l'étape | 216,6 | 145,5 | 268,8 | 263,7 |

Environ **86–87 % de coût CPU maximal de préparation végétale en moins** dans
cette paire, pas autant de FPS gagnés. La phase forêt inclut les lots lointains ;
ne pas les additionner. Le pic High de 263,7 ms suit un travail tracé total bien
plus court : ces phases ne couvrent pas tout Bevy/navigateur/GPU.

Préparation Ready 5/5 en **61,441 s**, 134 pipelines finaux. High/Low enregistrés
en mouvement aux ticks 1768/2007. Traversée completed, 518/518 ticks au sol,
434 contacts, 42,529579 m, aucune erreur de traversée. Retour High CPU prêt à
8,5625 s et GPU prêt à **9,0811 s** après préchauffage ; premier observé à
4,7195 s, soit encore environ 4,36 s de délai. Avant : GPU à 9,151 s.
Les trois étapes contiennent respectivement 8/87/83 observations mobiles,
mais le dernier High n'a pas huit observations mobiles avec le reçu GPU prêt.
Verdict conservé : **FAILED: délai ou parcours**.

Le gain CPU ne résout donc pas encore la latence du cycle. La suite pertinente
est de borner le travail par budget temporel plutôt que d'imposer une frame
entière entre chaque petit chunk, sans augmenter silencieusement le parcours
ou assouplir ses critères. Cette suite n'est pas implémentée par ce lot.
Les 3025 frames de la dernière étape comprennent l'attente immobile du watchdog :
pas de moyenne/p95/1 % low du mouvement ni de qualification cible déduits ici.

## Artefacts et nettoyage

Racine locale : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-vegetation-plan-reuse-20260911/`.
Scripts `Validate.ps1`, `Build.ps1`, `Run.ps1`, journaux, `source-inputs.json`,
exports et `cleanup.json` conservés. **282 sources épinglées puis revérifiées**.
HEAD Rust `f786a38aca2023f08d708687f99a47d14f34e6d4`, arbre modifié non publié.

| Artefact | SHA-256 |
| --- | --- |
| EXE | `04cdb0a689a819bb2467e33c65721c5e7789bd79e78397191c949ce7fca6edd7` |
| WASM | `717e1c33f530f62e11778c7c9589594feae4eeb1b03b6ba4798e72734dea37e5` |
| studio-motion.json | `694892dc5da8ac1d0d1ac948a69422da1f3c751a2d0a60a303cfd161b58a875a` |
| studio-motion.png | `ef2eac3381a62106ef72c7d8c0a0d97d0d94b49c276e8d95457ce63135a6a874` |

Exports réels : `forge-diagnostic-1789115975801.json` et
`forge-viewport-1789115987161.png`. PNG relu : forêt, chemin, personnage et ciel
présents ; tramage de troncs encore visible, eau hors champ. Aucune nouvelle
validation artistique. La compétence Windows a servi à ouvrir les exports du
Studio et fermer sa fenêtre, sans navigateur utilisateur fermé.

Sortie 0 ; huit processus possédés terminés : 30788, 31244, 35592, 32820, 5132,
2760, 10492, 30924. Aucun survivant. Pas de suppression, commit/push ou asset
Cadle. CI distante NOT RUN (ADR-0006). Dérogations existantes inchangées ; ni
révision propre, revue humaine du kit, dossier final ou gate déclarés validés.
