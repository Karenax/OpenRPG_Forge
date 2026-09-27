# R1 — Coût du feuillage transparent, comparaison terminée sans gain de cadence

8 septembre 2026, Rust, sources existantes conservées, aucune publication.

## Complément sous canopée — mesure statique après parcours

La charge de proximité révélée par `west3.png` est distincte du cadrage héroïque
mesuré plus bas. Comparaison exécutée une fois, ordre BLEND/MASK/MASK/BLEND,
RX 7800 XT/Vulkan, High, 1920 × 1080, sans VSync demandée ni capture pendant
la mesure, même binaire natif dev (Forge opt1, dépendances opt3), mêmes 1 662 arbres.
Aucun asset ni candidat Studio modifié.

`--walk-proof 1800:1:0.22 --walk-proof-measure-frozen` utilise le contrôleur
normal, attend la fin du parcours/freinage puis 120 frames de chauffe, suspend
explicitement le temps virtuel et collecte exactement 300 durées `Time<Real>`.
Distribution calculée par `forge_core::FrameDistribution`, échantillons bruts
conservés. Positions finales autour de (-132,486 ; 2,593 ; -203,535), différences
inférieures à 5 mm, caméra yaw 3,0750246 rad, pitch -0,2094395 rad, boom 4,5 m.
Ce n'est **pas une mesure de jeu vivant** ni un gain obtenu en suspendant le jeu.

| Ordre | Alpha | FPS moyens | p95 ms | 1 % low FPS |
| --- | --- | ---: | ---: | ---: |
| 1 | BLEND | 13,026 | 78,757 | 12,083 |
| 2 | MASK | 13,209 | 77,744 | 12,575 |
| 3 | MASK | 13,126 | 77,818 | 12,143 |
| 4 | BLEND | 13,039 | 78,793 | 12,090 |

**Décision : aucun gain suffisant pour promouvoir MASK.** Ne pas répéter cette
comparaison inchangée. La simulation active seule ne suffit pas à expliquer les
13 FPS, puisqu'ils persistent temps virtuel figé. Les timings partiels du premier
BLEND (transparent GPU ~8,28 ms, SSR ~2,10 ms, opaque ~1,83 ms) ne représentent pas
le temps GPU total et n'établissent pas un goulot CPU. Coût CPU/extraction/rendu/
présentation restant à isoler ; aucun correctif de performance démontré ici.

Le premier essai vivant `blend-a` a échoué avec `walk_measurement_moved` : le
contrôle refuse une dérive > 2 cm **ou** une vitesse horizontale > 0,01 m/s.
Le journal initial ne distingue pas ces deux conditions. Aucun rapport valide
produit, exit 1 et logs conservés ; seuil non assoupli. Le mode figé est explicite
et séparé. Ce travail ne corrige ni ne qualifie la stabilité du joueur vivant.

Implémentation bornée au harnais natif (`walk_proof/measurement.rs`, options CLI,
README), sans changement partagé du moteur, du domaine ou du WASM. Test nouveau :
exclusion du mouvement/préchauffage, collecte exacte de 300 échantillons ; le
mode figé est exercé par les quatre runs, pas par un test unitaire dédié.
79 tests Runtime + un test d'intégration PASS, Clippy strict et build PASS ;
`cargo fmt --check` et `git diff --check` PASS. Pas de nouvelle qualification globale.

Racine locale : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-understory-alpha-20260908`.
Reproduction : `Compare.ps1 -Frozen`, sortie fraîche obligatoire, quatre processus
séquentiels avec deadline 110 s et terminaison en `finally`. Pins sources et dérivé
vérifiés par le script ; origine des GLB détaillée plus bas.

| Fichier | SHA-256 |
| --- | --- |
| forge_runtime-frozen.exe | `F07052E404AF6775449475B97464C107648050D393439E0ADD55E2440D395E65` |
| frozen-blend-a.json | `12B9EBCBE1E1BFFE5695025B399B591BE1367F219E272F312E735B50BCF9960D` |
| frozen-mask-a.json | `A72360FE60F655215F0F12D850ED7465947FFFAE316DCA08F733A00A41903599` |
| frozen-mask-b.json | `B254FF103CF0AEC36408B3A57426C140E8DC6C30BDBBCB4705D73F10F280777E` |
| frozen-blend-b.json | `137EC74D6B37D5221DD9BF82AA44B3F22FF2AD87BD46BCA512273DCF0D43D347` |

PIDs 39052, 10384, 30824 et 35388 terminés, quatre sorties 0 ; contrôle final
aucun processus depuis cette racine. Gate 1 ouverte : ni acceptation artistique,
ni qualification du matériel cible, ni nouvelle capture de MASK dans ce cadrage.

## Diagnostic établi

Lecture des mesures du candidat forestier `r1-forest-scale-density-20260908` :
dernier relevé natif `main_transparent_pass_3d/elapsed_gpu` 6,11476 ms,
2 422 486 primitives, 8 029 696 fragments ; passe opaque 0,86924 ms.
Ce sont des mesures de passes, pas le temps GPU total ni la preuve que toute
la baisse de cadence vient des feuilles. Le Studio n'expose pas ces mêmes
timestamps ; son coût précis reste à isoler.

Les deux GLB actuellement chargés contiennent six matériaux de feuilles/aiguilles
en `BLEND`, double face. Le générateur `bind_pbr_texture_set` raccordait directement
la texture alpha et imposait `BLENDED`. `foliage_alpha_and_detail` génère un contour
avec bord adouci, pas un matériau de verre. Un découpage est donc un candidat
pertinent, mais changera les pixels des bords et exige une comparaison visuelle.

## Implémenté et vérifié

Le worker accepte une politique explicite `blend` ou `mask` depuis le manifeste.
Défaut historique et manifeste actuel restent `blend` : aucune réinterprétation
silencieuse d'un ancien kit. `mask` introduit un nœud Round sur l'alpha, reconnu
par l'exporteur glTF Blender comme `MASK`, cutoff 0,5, double face conservée.
Les textures sources ne changent pas ; le moteur conserve l'alpha du GLB.
Le validateur de rapport accepte les deux politiques déclarées, pas un mode inconnu.

`Test-FoliageAlpha.py`, trois exports réels sous **Blender 5.2.1 LTS**, PASS :
MASK 0,5, contrôle BLEND, contrôle opaque, albédo/normale et images autonomes,
mode inconnu refusé avant mutation. Sortie :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-foliage-alpha-20260908/export-policy-test`.
Log : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-foliage-alpha-policy-20260908.log`.
Warning exporteur de plusieurs nœuds image pour un sampler conservé sur les
deux fixtures alpha ; aucun échec. Processus headless terminé, pas de rendu
interactif laissé ouvert. `git diff --check` PASS.

Le skill d'import d'assets a imposé la séparation source/dérivé et qualification :
ce test de matériau ne constitue pas un nouvel import complet ou une revue humaine.
Le worker complet garde sa qualification historique Blender 4.5 ; le test du
helper sous 5.2 ne vaut pas qualification/migration du worker complet.

## Plan initial, exécuté dans la continuation ci-dessous

Produire un dérivé des arbres courants sans modifier géométrie, LOD, textures,
échelle, poses ou retouches authorées ; prouver ces invariants et le mode MASK
sur les octets exportés, puis comparer avec les originaux sur même binaire,
scène et paramètres. Les vieux GLB, pins et candidat Studio restent intacts.
Au moment de ce plan, benchmark A/B et capture du kit MASK n'étaient pas exécutés.
Ils le sont désormais (résultats ci-dessous), sans gain justifiant une promotion.
Le candidat MASK n'est **pas déployé dans le Studio**. Gate 1 reste ouverte.

## Dérivé réel et décision — continuation du 8 septembre

**Piste testée, aucun gain de cadence établi : ne pas promouvoir au titre de la
performance, ni répéter ces essais inchangés.** Le candidat MASK reste disponible
pour une éventuelle décision artistique. Le Studio livré reste en BLEND.

`scripts/Derive-FoliageAlpha.cjs` produit un kit externe neuf à partir du kit
runtime courant. Il ne transforme que les matériaux explicitement marqués comme
feuillage BLEND ; les arbres authorés sont refusés. Les deux fichiers GLB ne
changent que leurs champs alpha et métadonnée associée, vérifiés après relecture.
Tous les autres champs JSON et **l'intégralité du bloc binaire** restent identiques
(géométrie, LOD, indices, UV, normales, images). Quatre assets non arborés, provenance,
20 placements du kit, WorldSketch, terrain, scatter, pivots et hauteurs préservés.
Kit dérivé d'identité distincte, qualification humaine/publication toujours fausses.
L'export technique indique les invariants structurels et glTF, pas un rebuild Blender.

Racine : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-foliage-alpha-20260908`.
Sortie `runtime-kit/` ; commande : `node scripts/Derive-FoliageAlpha.cjs <kit-source>
<sortie-fraîche> <répertoire-gltf-validator>`. Test ciblé
`node --test scripts/Test-DeriveFoliageAlpha.cjs` : **5 PASS**, couvrant conservation,
déterminisme, verre non ciblé, re-conversion, dépendance externe, absence de texture
ou de balise, GLB corrompu/tronqué. Aucun nouveau test Rust : code Rust inchangé.

Khronos glTF Validator 2.0.0-dev.3.10 : **0 erreur par GLB**. Les douze warnings
par arbre `MESH_PRIMITIVE_GENERATED_TANGENT_SPACE` sont également présents sur les
originaux, vérifiés séparément ; tangentes reconstruites au runtime, portabilité
non acquise. Infos `UNUSED_OBJECT` conservées, pas silencieusement filtrées.

Réimport réel Blender 5.2.1 LTS via `Test-FoliageAlphaReimport.py` : deux arbres
PASS, 10/7 objets, comparaison exacte après import des positions, normales,
polygones, slots, UV et matrices. Les sources et dérivés restent hash-identiques
avant/après. Aucun retouchage du `.blend` original ni indexation Vault implicite.

### Comparaison de cadence exécutée

Même binaire `16011A7D…` du candidat forestier, RX 7800 XT/Vulkan, High, clair,
caméra du preview inchangée, 1920 × 1080, VSync demandée désactivée. Quatre
exécutions **séquentielles**, ordre A/B/B/A, 300 frames de chauffe, 1 200 frames
totales et 899 échantillons retenus chacune. Aucune capture durant les mesures.
Le contexte Windows reste celui du poste, pas un laboratoire isolé.

| Ordre | Alpha | FPS moyens | p95 ms | 1 % low FPS |
| --- | --- | ---: | ---: | ---: |
| 1 | BLEND | 40,446 | 26,204 | 33,350 |
| 2 | MASK | 39,735 | 26,444 | 35,131 |
| 3 | MASK | 39,938 | 26,317 | 35,009 |
| 4 | BLEND | 39,984 | 26,565 | 34,492 |

La passe transparente n'apparaît plus dans les diagnostics MASK, le coût passe
dans l'opaque (derniers relevés 4,96/4,92 ms, contre opaque 0,87/0,88 et transparent
5,88/5,85 ms en BLEND). Cela ne prouve pas un temps GPU total ni un gain FPS :
les moyennes MASK sont légèrement inférieures aux témoins. Ne pas déduire une
cause CPU précise de ce seul résultat. Seuil de 60 FPS toujours non acquis.

Les quatre sorties sont 0, aucun événement ERROR. Hash du terrain/végétation,
profil High, taille et 1 662 arbres contrôlés ; sources épinglées intactes après
essais. `Compare.ps1`, `blend-a/mask-a/mask-b/blend-b.json` et journaux conservés.

### Rendu et nettoyage

Cinquième exécution distincte : `Capture.ps1`, vue figée `human_bridge`, frame
400 à 600. PNG 1920 × 1080 inspecté : pont, grande lisière et texture découpée
visibles, pas de disparition de forêt ; changement des bords perceptible mais
pas d'amélioration artistique décisive démontrée. Pas de qualification en mouvement,
de vent ou de Tauri par ce PNG. Le contrôle compare le même cadrage que
`r1-forest-scale-density-20260908/forest-human.png`, temps virtuel figé.

PIDs des quatre mesures 43600, 20140, 10480, 34696 terminés ; capture PID 29584
terminée, zéro processus de cet exécutable au contrôle final. Blender terminé ;
aucun navigateur ouvert. Aucune relance prévue sur ce candidat inchangé.

| Preuve | SHA-256 |
| --- | --- |
| runtime-kit/alpha-derivative-proof.json | `6539483DCCF19FAF8BB92C51F0418308F767E05A461BAFBEE4DA68A8150D2419` |
| reimport-proof.json | `CECBD4F6AC1990A6717D351EF2FFEB2753321023235CB5995C42C00BFB96901F` |
| mask-human.png | `086A4A6004AF3A398E76E8F9987DFA3F6526F5898E47BCC8FBC58FDC6FD4A608` |

## 9 septembre — rejet du raccourci alpha exactement nul

Essai distinct du passage BLEND vers MASK : abandon du fragment avant éclairage
PBR uniquement pour les prototypes BLEND dont l'alpha échantillonné vaut exactement
zéro. Aucun seuil positif, matériau, arbre, profil ou source de carte modifié.
Quatre parcours vivants A/B/B/A, même kit courant, RX 7800 XT, 1920 × 1080,
ombres candidates 2048/trois cascades, commande `1800:1:0.22`.

| Passage | FPS moyens | p95 ms | 1 % low FPS | FPS dernier tiers |
| --- | ---: | ---: | ---: | ---: |
| A témoin | 54,66 | 27,39 | 33,70 | 39,54 |
| B alpha nul | 54,82 | 26,98 | 34,63 | 39,89 |
| B alpha nul | 55,11 | 26,96 | 31,17 | 39,60 |
| A témoin | 55,45 | 27,05 | 35,06 | 40,05 |

**Aucun gain reproductible : modification et test dédié retirés.** Ne pas relancer
cette paire inchangée ni essayer une succession de seuils pour sauver cette piste.
Les statistiques recalculées depuis chaque échantillon concordent avec Rust à
1e-9 ; 1139 ticks communs, projections d'ombres identiques. Les deux captures
natives figées `human_bridge` ont été inspectées : pas de changement évident de
silhouette, mais images non identiques, donc aucune parité pixel ou acceptation
artistique déduite. Aucune exécution Studio de ce shader expérimental.

Preuves externes conservées dans
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-zero-alpha-20260909/` : `Run.ps1`,
`Compare.py`, `comparison.json`, quatre rapports, journaux, deux PNG, pins,
EXE et shaders avant/après. `measure.log` confirme les quatre terminaisons.
Les tests workspace de l'expérience ont passé (662 PASS, un ignored), ainsi que
Clippy ciblé, build natif et check WASM ; ils ne démontrent pas un gain de rendu.
Après retrait, fmt/diff-check PASS et shader restauré à son hash exact
`2B3747D3270E84D306DBB76338A582D5FD91C47FD7F00C52C6EEE6671EC0A3A9`.
Rebuild Runtime restauré `cargo build --locked --offline -p forge_runtime` PASS
(`restored-build.log`), sans nouvelle exécution graphique ni rebuild Studio.
Aucun processus dont l'exécutable appartient au répertoire de cet essai ne reste
au contrôle final. Les améliorations antérieures sont conservées. Gate 1 reste ouverte.
