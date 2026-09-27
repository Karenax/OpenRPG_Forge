# R1-040 — préparation bornée et cycle mobile Studio acquis

11 septembre 2026. **Cycle High → Low → High après préchauffage : PASS local.**
Gate 1 et qualification temporelle complète restent ouvertes.

## Correction produit

Le Web ne consacre plus obligatoirement une frame à chaque petit chunk de terrain
ou de végétation. `preparation_budget.rs` fournit un budget coopératif commun :
chaque appel d'adapter traite au plus huit étapes et s'arrête dès que 4 ms se sont
écoulées. Le temps est vérifié après chaque étape indivisible : **budget souple**,
pas préemption, plafond absolu de frame ou budget partagé entre adapters.

Le terrain prépare plusieurs chunks au besoin ; la végétation repoll sa future
aux frontières de chunks déjà existantes. Aucun microtask loop ou worker ajouté.
Les anciennes projections restent vivantes jusqu'au résultat complet. Rejet des
sources/politiques périmées, erreurs et géométrie restent inchangés. Les workers
natifs et la préparation synchrone ne changent pas. Aucun asset/carte retouché.

## Validation automatique

710 tests workspace/all-targets/all-features PASS, deux sondes ignorées. Trois
tests nouveaux vérifient de façon déterministe : huit étapes même avec une
horloge sans avancement, frontière exacte de 4 ms, arrêt après un chunk de 25 ms.
Le test terrain compare exactement les quatre profils et les berges protégées
sur seize chunks, avec progression de une à huit étapes par appel. Les cas
d'erreur et d'invalidation conservent l'absence de publication partielle.
La végétation conserve sa comparaison exacte de meshes et ses tests de rejet.

Clippy strict natif/Web, fmt et diff-check PASS. Premier Clippy refusé pour un
`else` inutile dans un test, corrigé puis relancé ; journal initial conservé.
Frontend release 2 min 25 s ; shell 27,19 s. Avertissement futur proc-macro-error2
conservé ; aucune dépendance ajoutée.

## Acquisition réelle et portée

Baseline : `R1_040_VEGETATION_PLAN_REUSE.md`. Même machine, viewport 1408×966,
protocole `prewarmed_bridge_cycle`, sources, kit/forêt, eau/nuages et trajet.
Les bindings sources de chaque enregistrement actuel ont été comparés au profil
correspondant de la baseline. Identités application/reçu GPU et déplacement
horizontal réel vérifiés pour les **trois** enregistrements, pas seulement le
libellé PASS affiché. Les critères de sonde et la longueur du parcours ne changent pas.

| Temps après préparation (s) | Avant Low | Après Low | Avant retour High | Après retour High |
| --- | ---: | ---: | ---: | ---: |
| Première observation de la demande | 0,7479 | 0,7617 | 4,7195 | 4,0481 |
| CPU prêt | 4,0627 | 3,4109 | 8,5625 | 7,8999 |
| GPU prêt | 4,4626 | 3,8069 | 9,0811 | 8,4438 |

Le gain du cycle vient principalement de Low (~0,67 s de moins pour son délai
demande/reçu GPU). Le délai propre au retour High reste ~4,40 s contre ~4,36 s ;
ne pas annoncer une accélération de cette étape isolée. Le cycle complet obtient
cependant ses huit observations stables mobiles par étape avant la fin du trajet.

Préparation Ready 5/5 en **60,1782 s**, 134 pipelines finaux. Les enregistrements
High/Low/High sont aux ticks 1703/1901/2181, vitesse horizontale ~5 m/s et
déplacements effectifs non nuls. À la terminaison de la sonde : crossing,
496/496 ticks au sol, 40,723804 m, 434 contacts, aucune erreur. Le statut de
traversée affiché au moment de l'export est ensuite completed, 518/518 ticks au
sol, 42,530 m. Ces deux instants différents ne doivent pas être confondus.

Verdict exporté : **PASS: GPU High→Low→High en déplacement**. Contrairement à
la baseline, la sonde se termine sans attendre son watchdog à l'arrêt.

| Maximum par étape (ms) | Avant Low | Après Low | Avant retour High | Après retour High |
| --- | ---: | ---: | ---: | ---: |
| Préparation terrain CPU | 10,0 | 9,8 | 13,2 | 17,2 |
| Préparation végétale CPU | 24,5 | 24,3 | 24,2 | 24,3 |
| Forêt inclusive CPU | 26,3 | 25,3 | 22,9 | 24,4 |
| Intervalle de frame | 145,5 | 147,2 | 263,7 | 271,0 |

Le pic terrain High est plus haut dans cette paire ; le budget après chunk
n'est pas une promesse de rester sous 4 ms. Les saccades de 147/271 ms restent
visibles dans les données. Une paire de sessions ne prouve ni leur amélioration
statistique, ni une absence de régression de fluidité, ni les seuils FPS/p95/1 % low.
Les maxima CPU de phase ne sont pas des timestamps GPU.

La qualification fonctionnelle de **ce cycle préchauffé** est acquise ; ne pas
répéter son ancien échec comme blocage actuel, ni le rejouer sans changement utile.
Il reste à consolider cette preuve dans le dossier final et à traiter les réserves
temporelles/visuelles au niveau de leur protocole, sans rouvrir l'esthétique
acceptée ou prétendre qu'un cycle réussi clôt tout R1-040/Gate 1.

## Preuves et nettoyage

Racine : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-preparation-budget-20260911/`.
Scripts Validate/Build/Run, journaux, source-inputs et cleanup conservés.
283 fichiers source épinglés puis revérifiés, HEAD Rust
`f786a38aca2023f08d708687f99a47d14f34e6d4`, arbre modifié non publié.

| Artefact | SHA-256 |
| --- | --- |
| EXE | `814d9f6de591c7cde6a78fd880fc52e6863393353a85c5a102d957541ca77ec9` |
| WASM | `c15bf38c10455af5d97f08d7569b3c4239263ffb87d80c928f04939be737dc2d` |
| studio-motion.json | `1973eb31480fe6848cff8ff98e06888c7d25373ecaf2463884b8ab757da87e5e` |
| studio-motion.png | `467a1fc565088e40f0268afc9fb74f82fc3cbdb612761ab34e911a5e90b4b00b` |

Exports `forge-diagnostic-1789116990560.json`, `forge-viewport-1789117000522.png`.
PNG réel inspecté : chemin, forêt, personnage et ciel présents, tramage de
troncs persistant, eau hors champ. Capture finale à l'arrêt, pas vidéo des transitions.
La compétence Windows a servi à l'observation, aux exports et à la fermeture.

Sortie 0 ; huit PIDs possédés terminés : 15024, 28640, 6368, 18096, 5736,
35280, 34156, 23796. Aucun survivant ni navigateur utilisateur arrêté.
Pas de publication, suppression ou asset Cadle. CI distante NOT RUN (ADR-0006).
Dérogations existantes inchangées ; revue humaine du kit et révision propre dues.
