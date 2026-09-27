# Point 7 — dossier de clôture préparé, Gate 1 ouverte

## État courant — Gate 1 clôturée le 12 septembre 2026

**PASS automatisé local sous ADR-0006, avec les dérogations acceptées.**
Dérogations saccades pendant les changements de qualité et kit : ADR-0013 ;
comparaison Godot/Cadle validée explicitement. 738 tests PASS, 2 ignorés
préexistants ; builds et replays natif/Studio PASS. Révision propre qualifiée
`7386e00a1d9e8531470b57bcd8da190636a9e390`. [Rapport de clôture](GATE1_CLOSURE_20260912.md).
CI distante NOT RUN ; aucune publication ni qualification de distribution.
Le travail Gate 2 est suivi séparément et ne fait pas partie de ce snapshot.
Les états ouverts et attentes antérieurs ci-dessous sont historiques ;
ils ne doivent pas rouvrir les décisions soldées par ce rapport.


**12 septembre : [revue unique prête](R1_CLOSURE_REVIEW_20260912.md)**, 340 fichiers
vérifiés ; liens du kit et du comparatif, décisions et limites réunis. Le propriétaire
confirme ne pas avoir effectué de retouche humaine. Aucun nouveau runtime/benchmark,
aucune clôture ou publication. Les quatre conditions ci-dessous restent distinguées.

## Décision de travail après le retour propriétaire du 11 septembre

Le propriétaire signale la répétition excessive des essais. **Ne pas relancer
les captures, cycles ou vidéos inchangés pour répondre aux conditions ci-dessous.**
Le propriétaire a ensuite autorisé le correctif temporel commun : nouveau moteur
dans `R1_TEMPORAL_LOD.md`, contrôle ciblé natif/Studio PASS. Les anciennes captures
restent historiques et aucune réserve n'est tacitement acceptée.

### Conditions résiduelles de clôture, sans nouveaux critères

| Condition | État probant | Action qui peut réellement la solder |
|---|---|---|
| Temporalité / R1-040 | Tramage LOD persistant corrigé par fondu 200 ms commun kit/forêt ; sondes natif/Studio PASS et captures inspectées (`R1_TEMPORAL_LOD.md`). Les pics historiques ~98/102 ms ne sont pas remesurés ; absence globale de saccades non prouvée. | Ne pas refaire les acquisitions inchangées. La correction ciblée est prouvée sur sa portée, pas une nouvelle qualification de fluidité ; toute réserve restante doit être jugée explicitement, sans dérogation inférée. |
| Kit / R1-049, R1-053 et retouche | Six sources éditables préparées ; contrôles structurels réalisés. Pivots, silhouettes LOD, matériaux/collisions et provenance finale pas tous approuvés humainement ; retouche humaine absente. | Revue humaine du kit et véritable retouche conservée au réimport. L'agent peut exécuter le réimport et contrôler sa fidélité après réception de la source retouchée ; ne pas fabriquer la participation humaine. |
| Jugement visuel / R1-043, 046–047, 050–052 | Profils natif/Studio comparés fonctionnellement, série fixe actuelle disponible, références historiques consultées. Acceptation de la vue intégrée et du ciel conservée. Supériorité Godot / impact comparable Cadle non démontrés au niveau requis. | Revue comparative explicite fondée sur les preuves et leurs différences de conditions ; compléter uniquement le manque précis nécessaire au comparatif. Ne pas déclarer l'équivalence à partir du nombre de fonctions ou de captures. |
| Verdict / R1-055 et ADR-0006 | Arbre Rust modifié ; hashes du candidat disponibles mais pas révision Git propre qualifiée. | Après résolution des conditions précédentes, constituer la révision exacte selon l'autorité Git disponible, sans supprimer/stasher le travail présent ; qualifier les contrôles affectés et prononcer le verdict. Aucun commit/push autorisé par ce document. |

Déjà réglé sur sa portée : profils fonctionnels, acquisition fixe, acquisition
vidéo, préchauffage et mesures locales documentées. Performance de référence
R1-041 et mesures GPU Studio restent WAIVED/ACCEPTED, CI distante NOT RUN,
pas des succès mesurés. Ne pas rouvrir le ciel accepté ni ajouter VRAM résidente
exhaustive, équivalence pixel ou timing GPU Studio comme nouvelles conditions.

**Verdict actuel : Gate 1 non close ; aucun passage Gate 2.** Les revues humaines
sont un travail du propriétaire, pas un processus automatique toujours en cours.
La plainte sur les répétitions n'est pas une dérogation aux critères restants.

### Paire vidéo déjà acquise, sans nouvelle campagne

`r1-overlap-video-20260911/clear/bridge.mp4` et `rain/bridge.mp4` :
deux acquisitions terminées code 0, 271 images chacune, 1920×1080, 30 images/s
**offline_manual_time**, durée 9,033333 s. Même profil High et mêmes huit hashes
d'entrées, vérifiés par `verify_weather_pair`. Chaque acquisition a contrôlé les
images, le franchissement physique, les ressources/pipelines prêts, l'encodage
et la sortie normale. Six tests de l'outil PASS, dont huit altérations de la paire
rejetées. Aucune revue temporelle humaine ou agent de ces MP4 n'est revendiquée
ici ; une validation de structure vidéo n'est pas sa lecture visuelle.

SHA-256 clair : `5ff43c84bbcb069ca8f6e1c0c9dd61f67faf75b1b60261c36c052394a5d98930`.
SHA-256 pluie : `316accc02232545d3555d28813047bb84522d5ba511bfe5ea4dc41467e0b2372`.
Racine locale : `E:/OpenRPG_Forge/.gate1-evidence-runtime/`.
Contrôle Win32 après acquisition : aucun runtime du chemin candidat ou parent
PID 25448 restant. Aucun navigateur lancé. Les index antérieurs sont conservés
et ne sont pas présentés comme incluant cette paire ajoutée après leur création.

## Série native courante clair/pluie — 11 septembre, après consolidation

**Acquisition et inspection agent des douze vues terminées**, sans rebuild :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-overlap-twelve-views-20260911/`.
Runtime actuel `f7b39f7536dbcfcc8180daf6ac614e96b9804633f4cd2f5524fc5258ad27bd5d`,
profil High, 1920×1080, six caméras × clair/pluie, forêt en batches, eau forward
avec réflexion/réfraction et nuages. Commandes exactes dans `manifest.json`.
Kit build et catalogue identiques par hash au candidat Studio ; aucune retouche
de carte ou d'asset. Vérification directe du snapshot : hors `validation_cameras`,
le WorldSketch est identique au document canonique. Les 284 sources du build
correspondent toujours au workspace.

`Capture-WorldSketch.py` terminé avec code 0 ; chaque lancement a été attendu
jusqu'à sa sortie, avec délai maximal 120 s. Douze captures prêtes et arrêts
propres contrôlés dans les logs, sans erreur runtime. Vérification finale Win32 :
aucun processus du runtime à ce chemin ni parent d'acquisition PID 1860 restant.
Pas de navigateur ouvert. Pas de benchmark déduit des overlays.

Inspection des douze PNG : pluie visible dans chaque paire, variation de mouillage,
pont/berge/eau lisibles dans la vue rapprochée dédiée. Limites conservées :
bords de terrain visibles en vue générale, architecture prototype, répétition
végétale, tramage sur silhouettes/architecture et sous-bois peu détaillé.
La caméra `forest_detail` reste extérieure à la forêt ; elle ne prouve pas une
inspection rapprochée de tous les assets. Le ciel accepté n'est pas remis en chantier.
Ces observations agent ne sont pas une acceptation humaine ni une revue temporelle.

Hashes : manifeste
`844749e6ec7cba4ef8fddb276b25195577a47322c20ae1b9a27fcb50dc4a2323` ;
rapport local `verification.json`
`bd50bd008146640dc6b644f6abea74ccda6a86c24f78bbccc2a97fcd62fa9704`.
Le script local `Verify.py` a revérifié les douze sorties, les entrées et les sources.

Intégration au collecteur final par `--include-current-native-views`, uniquement
avec `--include-studio-motion --studio-candidate preparation-overlap`.
Index neuf `r1-final-dossier-overlap-20260911/index-twelve-views.json` :
**447 fichiers contrôlés**, `partial_not_gate_pass`, SHA-256
`60f995bc9a291a6af564d15cee22a392f5359e258e55e9ec4250908136ef58f1`.
Les nouvelles captures sont dans `current_native_views`, les douze anciennes
restent historiques. **15 tests du collecteur PASS**, dont refus d'un ancien
binaire, d'une série incomplète et de mauvais protocole/profil. Diff check PASS.

La série fixe du candidat actuel n'est donc plus manquante. Restent la vidéo
actuelle appariée, les réserves temporelles, la comparaison finale et les revues
humaines, ainsi que la révision propre. Aucun verdict de gate ou publication.

## Candidat avec chevauchement — preuves actuelles du 11 septembre

Les index antérieurs restent historiques. Candidat actuel : sources identiques
dans `r1-mesh-preparation-overlap-20260911/` (Studio) et
`r1-overlap-native-parity-20260911/` (natif), 284 entrées vérifiées sans dérive.
717 tests PASS/deux ignorés et Clippy natif/Web PASS. Cycle Studio mobile
High/Low/High PASS, cycle natif cinq étapes PASS. Compléments Medium/Ultra Studio
acquis dans `r1-overlap-studio-medium-ultra-20260911/` ; comparaison fonctionnelle
des quatre profils PASS. Détails, hashes, inspections et nettoyage dans
`R1_040_MESH_STAGING.md` et `R1_043_CURRENT_NATIVE_STUDIO.md`.

Ces acquisitions n'apportent pas de nouvelle vidéo/pluie appariée, d'équivalence
de pixels, de benchmark final, de retouche/revue humaine ni de révision propre.
La consolidation automatique intègre maintenant explicitement ce candidat via
`--include-studio-motion --studio-candidate preparation-overlap`.
Commande exécutée le 11 septembre, sans nouveau processus graphique :

```text
python scripts/Verify-Gate1FinalEvidence.py --evidence-root E:/OpenRPG_Forge/.gate1-evidence-runtime --output E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-final-dossier-overlap-20260911/index.json --include-studio-motion --studio-candidate preparation-overlap
```

Résultat : **408 fichiers contrôlés**, 284 sources du candidat identiques au
workspace, **14 tests du collecteur PASS**, `git diff --check` PASS.
SHA-256 de l'index :
`120e1d0400e16e879bef44df563b8dbd2eae7930801a19549ea4a8f82ed41321`.
EXE/WASM, build-proof, frontend servi, diagnostic mobile, PNG, nettoyage et
rapports de parité natif/Studio avec leurs payloads sont revérifiés par hash.
La vérification sémantique du cycle mobile est rejouée ; les comparateurs natif
et Medium/Ultra ne sont pas réexécutés : leurs rapports épinglés et leurs entrées
de preuve sont conservés et contrôlés, sans exécuter de script externe.
Les douze captures et deux vidéos historiques restent séparées, pas promues au
candidat actuel. Aucune dérive documentaire n'est autorisée pour ce candidat.

Le reçu de nettoyage Studio prouve la terminaison enregistrée des huit PID,
mais ne contient pas de code de sortie : le collecteur ne réaffirme donc pas
une sortie normale à partir de ce seul reçu, malgré la sortie 0 observée pendant
l'acquisition et documentée dans R1-040. Aucun inventaire actuel de la machine
n'est déduit de ces anciens PID.

Statut conservé : **partial_not_gate_pass**. Fluidité/LOD temporel, nouvelle série
finale appariée pluie/vidéo et comparaison finale, retouche et revues humaines,
révision propre restent distincts et ouverts. Le fichier attendu de retouche
`r1-kit-human-ready-20260910/vv-rock-moss-a-human.blend` est toujours absent.
Aucune compilation Rust, modification de rendu ou d'asset, publication ou
ouverture de Gate 2 dans ce lot. Les anciens index restent intacts.

10 septembre 2026 — **PARTIAL, aucun verdict de passage**.

## Complément du 11 septembre — candidat berges, sans nouvelle acquisition

Le collecteur accepte un choix explicite `--studio-candidate bank-reuse` avec
`--include-studio-motion`. L'ancien choix reste disponible et strict : il ne
devient pas automatiquement le nouveau candidat. Commande réellement exécutée :

```text
python scripts/Verify-Gate1FinalEvidence.py --evidence-root E:/OpenRPG_Forge/.gate1-evidence-runtime --output E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-final-dossier-bank-reuse-20260911/index.json --include-studio-motion --studio-candidate bank-reuse
```

**377 fichiers contrôlés**, douze captures et deux vidéos historiques revérifiées.
Nouveau Studio EXE/WASM, build-proof, snapshot de sources, JSON mobile et nettoyage
épinglés ; aucun PNG nouveau substitué par une image historique. Le supplément
signale `viewport_png_verified=false`, `normal_exit_verified=false`,
`fluidity_qualified=false`, `clean_revision_verified=false`.
282/283 fichiers source correspondent au build ; déplacement du paragraphe README
explicitement identifié par chemin et paire de hashes. Aucun autre changement de
source n'est autorisé par cette exception documentaire ; snapshot original intact.

Neuf tests du collecteur PASS, dont quatre refus supplémentaires : autre candidat,
fichier moteur à la place du README, ancien hash différent, nouveau hash différent.
La commande complète termine avec `partial_not_gate_pass`. Ce lot n'a lancé aucun
renderer, compilation Rust, import d'asset ni publication. Fichier de retouche
humaine toujours absent à cette vérification. SHA-256 de l'index :
`8baeda2448d5f33c11df1e95469aba01b4ed4f1f88ba835cda1c160a72b356a7`.

Cet index remplace l'index de préparation bornée comme regroupement du dernier
diagnostic, pas comme qualification finale : parité native du nouveau binaire,
réserves temporelles, retouche/revues humaines et révision propre restent distinctes.
Les anciens index sont conservés et ne sont pas réécrits.

## Actualisation du 11 septembre — portée du collecteur corrigée

**Cycle Studio intégré au nouvel index, sans relancer le renderer.**
`Verify-Gate1FinalEvidence.py --include-studio-motion` réexécuté : **378 fichiers
hashés**, 12 captures et deux vidéos historiques revérifiées. Index courant :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-final-dossier-motion-20260911/index-reviewed.json`,
SHA-256 `fb7e0e87ad125fa7fd08a0e6f5bb208a4d7fc32e88a0bbd4f67ce61295738fde`.
Le premier `index.json` du même dossier est conservé ; `index-reviewed.json`
corrige la description de portée du préchauffage désormais examiné structurellement.

Le supplément `current_studio_motion` épingle séparément EXE, WASM, diagnostic,
PNG, manifeste de 283 sources, build-proof et reçu de nettoyage du candidat
`r1-preparation-budget-20260911`. Les sources sont revérifiées dans le workspace
Rust, avec refus des chemins hors dépôt. Trois enregistrements mobiles dans
l'ordre High/Low/High, sources constantes, identités application/reçu GPU,
absence de pipelines en attente/échec et préchauffage Ready 5/5 sont contrôlés.
Les commandes contenues dans les artefacts ne sont pas exécutées.

**Huit tests du collecteur PASS**, dont 14 corruptions du supplément Studio
(immobilité, valeurs non finies, ordre, sources, reçu périmé, GPU incomplet,
cycle/préchauffage/trace incomplets). Ce n'est pas une nouvelle preuve visuelle
ou un benchmark. La terminaison des huit PIDs est un reçu historique vérifié,
pas une observation de tous les processus actuellement sur la machine.

Statut maintenu `partial_not_gate_pass`, révision propre false, fluidité false,
mesures GPU Studio non qualifiées (dérogation ADR-0012 distincte). Les acquisitions
natives historiques ne sont pas relabellisées comme sorties du nouvel EXE Studio.
Les mentions d'index sans cycle mobile ci-dessous décrivent les étapes antérieures.
Aucun build Rust nécessaire pour ce changement de collecteur ; aucun renderer,
navigateur, suppression ou publication lancé.

Dernière preuve indépendante : `R1_040_PREPARATION_BUDGET.md` acquiert le cycle
Studio High/Low/High après préchauffage en déplacement (trois reçus GPU concordants).
710 tests PASS ; exports et pins de ce nouveau candidat dans son dossier propre.
Cette preuve n'est pas encore intégrée à l'index historique ci-dessous ; les
douze captures anciennes ne deviennent pas celles de ce nouveau binaire.
Fluidité complète, revues humaines et révision finale propre restent dues.

Dernier complément : `R1_052_FOREST_OPTICAL_COMPARISON.md` contient une comparaison
des intérieurs forestiers avec FOV vertical proche de 60° et coûts vivants des
deux candidats. Acquisition descriptive réalisée ; budgets et première/troisième
personne encore différents, pas de benchmark équivalent ni verdict humain.
Les paragraphes suivants relatent les étapes antérieures et l'index historique,
qui ne contient pas ces nouvelles preuves. Aucun fichier historique remplacé.

Complément postérieur : deux fenêtres de coût Cadle réellement acquises dans
un nouveau dossier, sans modifier les anciens rapports à `stats=[]` ni l'index
historique. Voir `R1_052_CADLE_COST_20260911.md`. Partie Forge appariée encore due.

Le collecteur ne déclare plus à tort le préchauffage « incomplet » : il ne
l'évalue pas. Sa preuve indépendante est `R1_PROFILE_PREWARM.md`. La dérogation
GPU Studio ADR-0012 est distinguée d'une mesure ; aucun PASS GPU fabriqué.
Six tests du collecteur PASS, dont métadonnées manquantes refusées et présence
de statistiques insuffisante pour promouvoir un benchmark apparié.

Nouvel index exécuté :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-final-dossier-20260911/index.json`.
89 fichiers, 12 captures retenues et deux vidéos revérifiés. Ce sont les acquisitions
historiques du 10 septembre, pas des captures du nouveau build terrain Studio.
Le smoke de ce build est séparé dans `R1_TERRAIN_STAGED_PREPARATION.md`.

Les rapports Cadle forêt/vallée lus sur place déclarent 1920×1080, label High,
RX 7800 XT via ANGLE/D3D11 et **zéro enregistrement dans `stats`**. Les chaînes
de renderer sont conservées telles quelles ; ni WebGPU ni équivalence de budgets
avec High Forge ne sont déduits du nom de la référence. L'index expose désormais
ces conditions et conserve `matched_benchmark_verified=false` même si un rapport
contient des statistiques. R1-052 exige encore la paire de coûts contrôlée.

Inspection complémentaire : `shot-in.png` forêt Cadle et
`forest_detail-clear/capture.png` Forge montrent respectivement un sous-bois
depuis l'intérieur et une lisière depuis une pente extérieure. Cette paire ne
permet pas de conclure que les densités intérieures sont équivalentes ou différentes
à cadrage constant. Ne pas améliorer la carte pour compenser ce seul écart de caméra.
Pour l'acquisition comparative restante, utiliser une vue intérieure à hauteur
humaine dans chaque moteur ; conserver la météo, FOV et budgets effectifs, séparer
mesures et jugement artistique. Aucun asset ou image Cadle copié, aucun renderer
lancé dans ce lot. Aucun critère artistique, temporel ou humain coché.

Les sections suivantes décrivent l'acquisition initiale.

Actualisation : point 5 soldé localement (`R1_PROFILE_PREWARM.md`), sans nouveau
verdict artistique. Les douze captures retenues et deux vidéos ont été revérifiées
par le collecteur (89 fichiers épinglés), sans lancer de renderer. R1-044/045
soldés pour leur acquisition seule ; les revues humaines restent séparées.
La comparaison fonctionnelle native/Studio du point 4 est également revérifiée
PASS, sans promouvoir l'équivalence pixel, la performance ou une session unique.
Sorties dans `r1-profile-prewarm-20260910/dossier-recheck` et `parity-recheck.json`.

Décision ultérieure aux acquisitions : mesures GPU Studio uniquement
`WAIVED / ACCEPTED` par
[ADR-0012](../../ADR/ADR-0012-gate1-studio-gpu-measurements-waiver.md).
Conserver les résultats bruts non qualifiés ; cette exception ne vaut ni revue
humaine ni clôture de Gate 1. Index complémentaire de 89 fichiers et limites :
`R1_CLOSURE_PROGRESS_20260910.md` (l'index initial de 62 fichiers reste historique).

Complément postérieur à l'index de 62 fichiers : `R1_CLOSURE_PROGRESS_20260910.md`.
Capture/replay intégré courant, copies Blender pour revue et comparaison Godot
historique réalisés. L'ancien index reste intact, il ne couvre pas ces nouvelles
preuves. La comparaison Godot n'est plus entièrement NOT RUN : une référence
historique a été inspectée, sans qualifier la comparaison finale appariée.

## Identité et acquisition

Index local vérifié :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-final-dossier-20260910/index.json`.
62 fichiers hashés ; relecture reproductible via `scripts/Verify-Gate1FinalEvidence.py`.

- HEAD local : `f786a38aca2023f08d708687f99a47d14f34e6d4`, branche Rust, arbre modifié.
  Ce HEAD **n'identifie pas à lui seul** le candidat complet non committé.
- Runtime réutilisé : `r1-motion-final-20260910/runtime-shadow.exe`, SHA-256
  `50991d48b8b00716847f805aa821f72fdd5ded9e9bb52b5d2dfba3f4d69e5e8f`.
- Même kit, eau forward, nuages et forêt en batches épinglés par chaque manifeste.
- Pas de build, commit, push, modification des assets ou passage de gate.

## Douze vues retenues et inspectées

Sous `E:/OpenRPG_Forge/.gate1-evidence-runtime/` :

- `r1-final-twelve-views-20260910-b` : six caméras × clair/pluie, 1920×1080,
  High, douze arrêts propres. Hero, hauteur humaine, repère architectural,
  vue générale et forêt conservées dans les deux météos.
- La paire `water_detail` de ce premier ensemble est **écartée de la sélection** :
  forêt agrandie masquant l'eau. Fichiers originaux conservés, pas retouchés.
- `r1-final-water-views-20260910` : deux captures de remplacement, même candidat.
  Caméra placée au-dessus du pont pour voir eau et berges ; **uniquement la caméra
  de preuve** change. `r1-final-water-camera-20260910` prouve que les documents de
  géométrie compilés sont identiques ; seule la lignée liée aux caméras change.
  Le fichier de référence de caméras est actualisé : l'ancien hash de cette entrée
  dans la préparation initiale n'est plus courant. Ne pas déclarer cette ancienne
  préparation entièrement fraîche après ce changement.

Les 14 images ont été inspectées ; les 12 retenues couvrent bien les six vues
en clair/pluie. Eau et berges visibles dans la paire corrigée, pluie visible et
sol mouillé dans les vues pluie. Restent perceptibles : prototype architectural,
limites du terrain sur vue générale, répétitions de végétation et tramage distant.
Le ciel déjà accepté n'est pas remis en chantier. L'overlay masque une partie de
l'image : ce sont des preuves de diagnostic, pas des visuels promotionnels.
Pas de déduction de performance des cadences affichées en capture fixe.

## Vidéos réutilisées, pas de nouvelle campagne

`r1-motion-final-20260910/bridge-shadow/bridge.mp4` (High clair) et
`bridge-shadow-ultra-rain/bridge.mp4` (Ultra pluie) : inputs, MP4, séquences PNG,
logs, préparation GPU et franchissement physique revérifiés. Deux vidéos offline
à pas manuel ; elles **ne prouvent pas 30 FPS temps réel**, ni une comparaison
météo High strictement appariée. La revue antérieure est décrite dans le point 3 ;
aucune nouvelle acceptation humaine n'est déduite de leurs hashes.

## Comparaison de référence, portée limitée

Cadle : références locales `artifacts/cadle-gate-c/qualification-script-c/tools/out/`
`r1-ab-forest-20260906` et `r1-ab-vale-20260906`. `report.json` et
`shot-ground.png` hashés, deux images inspectées **sur place**, sans copie d'asset
ou d'image Cadle dans le dossier Forge. Référence historique épinglée par Gate C,
pas une nouvelle exécution Cadle ni un benchmark contemporain apparié.

Observation : la référence forêt Cadle montre un sous-bois nettement plus continu,
des troncs/textures plus détaillés et des faisceaux lumineux plus structurés.
La référence vallée possède une strate herbacée très dense. Forge présente
désormais une forêt à grande échelle et une eau/berge lisible, mais ses sols et
son sous-bois restent plus simples. **Équivalence d'impact non démontrée** ;
ne pas déduire une supériorité de la seule présence des mêmes fonctions.

Godot : recherche bornée des dossiers de preuve actuels et des noms de documents
historiques sans trouver une paire finale qualifiée correspondant à ce candidat.
Comparaison finale appariée **NOT RUN** ; la consultation historique Godot a depuis
été réalisée dans `R1_CLOSURE_PROGRESS_20260910.md`, pas à refaire comme absente.
La matrice de reprise a été consultée, aucun code/asset historique importé.

## Validations du lot

- Audit six GLB : 0 erreur, 38 avertissements explicités dans le point 6.
- Tests caméra corrigée : 4 PASS.
- Tests collection optique : 2 PASS, comprenant six corruptions rejetées.
- Tests audit : résultat courant et quatre cas négatifs PASS ; syntaxe Node PASS.
- Consolidation : 12 vues sélectionnées, 2 vidéos revérifiées, 62 fichiers hashés.
- Deux essais initiaux échoués conservés hors sélection : racine de compilation
  incorrecte, puis contrat incompatible metrics/capture optique. Corrigés dans
  les outils sans desserrer le garde moteur, nouvelles destinations distinctes.
- Tous les runtimes de cette acquisition sont terminés ; aucun navigateur ouvert.

## Verdict et conditions restantes

Point 5 soldé localement avec les dérogations explicites et le préchauffage acquis.
Point 6 reste PARTIAL. Point 7 préparé mais non clos : comparaison finale Godot
(consultation historique déjà réalisée), jugement final Cadle et revue humaine manquants ;
preuve d'une révision source propre exacte non produite (hashes d'artefacts ≠
révision clean). Réserves temporelles du point 3 maintenues. Performance point 2
WAIVED/ACCEPTED, pas benchmark PASS. Aucun critère non couvert n'est coché.
