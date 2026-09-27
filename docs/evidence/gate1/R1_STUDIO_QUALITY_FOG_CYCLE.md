# R1-040 — Cycle Web courant et brume expérimentale

2026-09-06, Rust local non publié. **Smoke partiel, pas de passage Gate 1.**

## Exécution observée

Build Studio du lot `r1-studio-proof-export-20260906/dist`, sans recompilation
concurrente, servi sur `127.0.0.1:14643` avec
`?forge_scene=world-sketch-visual-valley&forge_kit=governed`.
WASM SHA-256 : `e2db23d1ac0f15d327fec6d720ef487a54db0cdaa3053eb198c2dfd5ef445e1d`.

Clics réels dans le même onglet : **High → Low → Medium → Ultra → High**.
Chaque niveau rejoint les sept observations attendues : cinq `cpu_projected`,
environnement/eau `partial`, `available_budgets_projected=true`,
`full_preset_applied=false`. Kit conservé à 20/20 instances et 36 colliders,
un seul monde initialisé affiché. Le manifeste de parité reste aligné ; c'est
un relevé de démarrage, pas un rehash continu après chaque clic.

Low a été capturé avant maîtrise du premier plan : overlay autour d'une frame
par seconde, à ne pas utiliser comme mesure de performance. Ensuite
`Page.bringToFront` et lecture réelle `document.visibilityState=visible`,
`document.hasFocus()=true`, sans rechargement. Après le clic Ultra, une lecture
de statut expire à trois secondes ; la lecture suivante retrouve Ultra appliqué.
Pas de cause attribuée ni de fluidité de transition qualifiée. Aucun délai
produit n'a été augmenté et aucun nouveau processus n'a remplacé le précédent.

## Volumétrie WebGPU

Après retour High, clic d'activation de la brume expérimentale :

- observation `applied=true`, 64 étapes demandées, borne effective 128 m ;
- rendu réel capturé et inspecté : voile plus chaud et moins de contraste dans
  les lointains, sans écran noir ni frontière rectangulaire dure visible sur cette vue ;
- passage Low : `applied=false`, borne effective absente, brouillard de distance conservé ;
- retour High : `applied=true`, 64 étapes, 128 m ;
- désactivation explicite : `applied=false`, capture inspectée sans le voile précédent.

Canvas 1408 × 741, page capturée 1920 × 855 : **pas une preuve Full HD du viewport**.
Les captures sont visibles dans les résultats de l'outil, sans PNG archivé localement.
Les overlays de cadence glissante ne sont pas un benchmark. La scène reste
prototypale (capsule, végétation angulaire et terrain très vert) ; aucune acceptation
artistique, preuve de rayons lumineux, mouvement continu ou qualification Tauri.

Lecture warning/error : liste vide. La lecture CDP des événements signale
`truncated=true` : elle ne prouve donc pas l'absence d'erreurs sur tout le cycle.
Onglet et serveur temporaires fermés après observation. Aucun téléchargement
interne au navigateur, réglage de sécurité ou publication effectué.

## Régression détectée et correction

Avant correction, Low retire bien le volume mais le ledger conserve
`requested_step_count=64` du document authoré, alors que la qualité demande zéro.
Le moteur actualise maintenant ce champ depuis la politique même sans volume
actif (Low, aperçu désactivé, topologie incompatible). Sans politique, le budget
authoré demeure la référence. Ce champ n'est jamais un compteur de pas GPU exécutés.

Test ECS ajouté : High/Low/Medium/Ultra/High donne exactement 64/0/32/96/64,
volume non appliqué, borne absente, preset complet toujours faux, hash source
inchangé. Test ciblé PASS. Le correctif change l'observation, pas le rendu ;
les captures ci-dessus précèdent ce correctif et ne prouvent pas son exécution Web.

Journaux post-correction :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-quality-fog-cycle-20260906`.
Résultat final : **413 tests workspace PASS**, Clippy workspace tous targets/features
avec warnings projet en erreurs PASS, check Studio `wasm32-unknown-unknown` / `web`
PASS, formatage et `git diff --check` PASS. Avertissement de compatibilité future
`proc-macro-error2` 2.0.1 conservé. Pas de rebuild Trunk ni nouveau lancement Web
après ce correctif de diagnostic. Les sources de projet, assets et géométrie sont inchangés.

SHA-256 des journaux post-correction :

- `tests.log` : `82e4a105b2d2f5451378a95d4975eee7fdfde052576d414a11528432e1e400a7` ;
- `clippy.log` : `c746629906fd8544a1f9f20ec3b93b7cc9a3c46290c8e741af033035c529ee96` ;
- `wasm.log` : `5aebf073cc589cc24e58ee08f3b2eb817ce0bc64f100fd97b80cf668d05f7467`.

## Reste ouvert

Budgets optiques non supportés, qualification de la volumétrie, fluidité des
transitions, adaptation automatique, capture normalisée, comparaison humaine et
performances finales. CI distante NOT RUN sous ADR-0006. Aucun critère de gate
n'est fermé par ce smoke ou par la correction de télémétrie.
