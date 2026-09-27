# R1-040 — préparation terrain Studio répartie par chunks

11 septembre 2026, branche Rust existante, sans publication ni changement d'asset.

## Implémentation

Après la végétation, le chemin Web du terrain prépare désormais un chunk par
appel de frame, avec un seul snapshot possédé. Le calcul LOD/morph existant est
réutilisé, y compris les chunks protégés par les berges. Le résultat reste privé
jusqu'à la préparation complète ; les bindings et la source sont contrôlés avant
application. Source, politique, niveau ou ensemble de berges modifié : abandon
du travail périmé. Un chunk invalide ne publie aucune géométrie partielle.

Le chemin natif en tâche de fond reste inchangé. Ni document auteur, collider,
algorithme géométrique, budget de profil, shader, matériau ou dépendance modifiés.
Pas de cache simultané des quatre profils. Les meshes précédents restent visibles
pendant l'attente, avec statut non qualifié pour la nouvelle demande.

## Vérification acquise

Clippy strict workspace/all-targets/all-features et Web/WASM PASS.
Premier refus Clippy : système dépassant 100 lignes ; helpers de statut/nettoyage
extraits, sans désactiver le contrôle. Trois tests ajoutés : égalité exacte des
quatre profils et berges protégées avec le calcul précédent, un chunk par appel,
abandon source/politique/niveau/berges périmés et refus d'un chunk tardif invalide.
700 tests PASS / un timing préexistant ignoré, dont ces trois tests. Builds
frontend Rust release et bundle Studio PASS. Contrôle frontend avec wasm-opt=0,
comme le candidat précédent ; HTML de distribution inchangé. Aucun nouveau
build ou essai graphique natif revendiqué : calcul natif inchangé.

Essai Studio réel du 11 septembre, exports à 08:10–08:11 : Ready 5/5 en
41,4071 s, Low/Medium/High/Ultra/High, High restauré, sept domaines appliqués.
Identité de soumission GPU identique à l'application, queue terminée,
140 pipelines, zéro pending/failed. Ce temps total de préparation n'est pas
un benchmark de cadence, ni une comparaison contrôlée avec l'ancien candidat.
64 chunks canoniques et 20 chunks de berges protégés dans le plan final.
Six hashes de scène identiques au smoke clair précédent. PNG 1408 × 966 relu
et inspecté : terrain, végétation, forêt lointaine et personnage présents.
Image fixe, pas qualification temporelle ou revue humaine du kit.

278 fichiers source épinglés dans `source-inputs.json`, tous revérifiés sans
écart. HEAD f786a38aca2023f08d708687f99a47d14f34e6d4, worktree modifié :
pas une révision Git propre. Kit externe épinglé par `studio/build-proof.json`.
Pins SHA-256 :

- `studio-clear.json` : `47fc0b776c96104da63fcc05ba624426f57bb9be3026c099971a86629d95e1e0`.
- `studio-clear.png` : `e72dcf26bcb32d2bb5b850858242a503d8bf6607b68f1d4fc725499f812b22c3`.
- `web/forge_studio_bg.wasm` : `c4b52e83439245134bc41ea46eb24501f37ba2aed69d4ccac891f76810ec5044`.
- `studio/forge-studio-candidate.exe` : `89d23b0525714ea5e95d341459d6563f7327f5bcca2f9d51152939b805addbc8`.

Téléchargements originaux conservés (`1789107056084.json`, `1789107073945.png`).
Instance fermée normalement ; absence revérifiée des huit PID possédés :
15172, 27604, 30556, 15072, 2916, 604, 33676, 10804.

Journaux et nouveaux builds sous
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-terrain-staged-20260911/`.
Le candidat du 10 septembre reste conservé, pas recyclé comme preuve du nouveau code.

## Limites

Copie initiale, recherche des contraintes de berges, coût d'un chunk et application
finale des meshes ne sont pas garantis sous 16,7 ms. La préparation peut durer plus
longtemps au total. Aucun gain FPS, suppression de toutes les saccades, correction
du tramage des troncs ou passage de Gate 1 n'est déduit du découpage seul.
