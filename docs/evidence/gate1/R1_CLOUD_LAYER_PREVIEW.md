# R1 — Aperçu rendu de couche nuageuse

## Candidat intégré accessible dans Studio — 9 septembre

Le nouveau lien « Candidat complet · kit, forêt, eau et ciel » combine explicitement
WorldSketch, `forge_kit=governed`, `forge_forest=batches`,
`forge_water=clear-reflections-preview` et `forge_clouds=preview`.
Les liens isolés sont conservés. Navigation avec rechargement et remise à zéro
des aperçus ; aucun document authoré, shader ou asset modifié par ce lot.

Vérifié : 55 tests Studio PASS (dont combinaison scène/kit/nuages et milieu eau),
Clippy Studio natif et Web strict PASS, Trunk et construction Tauri PASS.
Journaux locaux : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-integrated-menu-20260909`.
Avertissement amont de compatibilité future `proc-macro-error2 2.0.1` conservé.
Pas de nouvelle qualification workspace ou benchmark déduite de ces tests ciblés.

Candidat : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-integrated-menu-studio-20260909`.
EXE SHA-256 : `8B07B8FF52A67F393A2B6D8C919943B0C4752D73210DD644DA9096267FCC08E3`.
WASM SHA-256 : `1695060CC26EA2227333942F20B8216421F876993EE6FF5F805FD2939CB31638`.
Le manifeste de construction reste `BUILD_ONLY` ; les observations suivantes
sont des preuves d'exécution séparées, pas une promotion de ce manifeste.

Clic réel du menu effectué dans Tauri avec Computer Use ; chargement puis
export JSON et PNG depuis les contrôles du produit. Première tentative de clic
sans géométrie disponible échouée ; activation et nouvelle observation ont
permis le clic. Aucun lancement supplémentaire nécessaire.
JSON relu : kit 20/20, 36 colliders, 1662 arbres ; un dôme visible côté CPU,
24 pas, texture 262144 octets, couche 480 m, seed 1337 et couverture 0,52.
Eau : réflexion 48 pas/96 m. Rendu préparé : 101 pipelines, zéro pending/failed,
8 observations stables ; pic transitoire d'erreurs shader 18 conservé.
`gpu_execution_verified=false` reste explicite dans l'observation nuageuse.

Exports copiés sur E: et vérifiés contre les originaux Downloads conservés :

| Fichier dans le candidat | SHA-256 |
|---|---|
| forge-diagnostic-1788973748488.json | A06976B6115BC756E7FD6DDCA5C8E25590E7C1166FD6029AF6C4749E69FC6FE2 |
| forge-viewport-1788973757724.png | BBB710A94B824028E7CD5BAFF80920E66F70E1465489AD0398B4DFEE6B3B8B81 |

PNG 1408×966 inspecté : nuages, terrain, personnage et lisière présents ensemble.
Nuages encore flous, formes arrondies artificielles ; aucune acceptation artistique.
L'eau distante ne permet pas de qualifier ici son contact rapproché aux berges.
JSON et PNG non atomiques, caméra joueur, pas de mesure comparative du coût du ciel.
La fenêtre PID 32024 a été fermée normalement ; absence vérifiée du PID, de son
conhost 20980, de sa WebView 324 et des enfants directs suivis. Gate 1 reste ouverte.

## Répartition sans grille — 8 septembre

La recherche dans 27 cellules est remplacée par 64 centres et rayons seedés,
préparés une fois par texture dans une table bornée. Positions libres dans la
tuile horizontale, centres verticaux dans la couche et rayons variables ; distances
horizontales périodiques, altitude non répétée. Pas de génération aléatoire par
voxel, de dépendance nouvelle ou de calcul ajouté au shader. Le 2D reste inchangé.
L'érosion et la couverture sculptent cette union de volumes à tailles variables.

Capture native inspectée : moins d'alignements, masses de tailles variées et
davantage d'ouvertures bleues. Base expérimentale conservée pour la suite,
**contours encore flous/stylisés ; réalisme et revue humaine non acquis**.
La couverture n'est pas une fraction exacte du ciel et le rendu change à seed
identique lorsque l'algorithme dérivé change. Aucun profil canonique/promu.

Quatre tests volume et Clippy monde strict PASS. Le test de raccord utilise
désormais une distance normalisée par rayon : borne analytique du déplacement
`2e-7 / 0.035`, pas un seuil artistique abaissé. Périodicité exigée sur X/Z,
continuité verticale et faces vides contrôlées par les tests de champ existants.
Le build natif et le lancement ont réussi : 20 poses/36 colliders, 650 arbres,
un dôme, capture Full HD. Budget High inchangé : R8 64³, 24 pas et couche 480 m.

Rejeu workspace terminé : **613 PASS, 0 FAIL, 1 timing ignoré**. Check Studio
WASM PASS ; avertissement amont de compatibilité future `proc-macro-error2` 2.0.1
conservé. Ce contrôle de compilation ne qualifie pas un rendu navigateur.

Preuve locale `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-cloud-irregular-20260908/`.
Onze inputs non binaires comparés aux pins de `r1-cloud-lobes-20260908` et identiques.
Le temps global n'est pas gelé ; pas d'égalité pixel du vent/sol revendiquée.
Compilation concurrente/capture dev : aucune conclusion de performance.
Le Studio déployé n'est pas remplacé par ce lot natif ; Web visuel/Tauri restent dus.

SHA-256 : source volume `a99a86c318cf6781887054535e5df0a625707879cc33c8a70048d0755f458e51` ;
runtime `2ef55928916b98e904f2e08eb6377b1492a3ee4b9db7379d722c2eb65f9b84ee` ;
PNG `c721126614551c8751c2e28f3f0ab732262893a2e8fd1a41fe8574c634b62aca`.

## Morphologie cellulaire en cours — 8 septembre

Le champ 3D de `forge_world` utilise maintenant des volumes arrondis définis par
une recherche bornée dans 27 cellules voisines, une érosion 3D et le masque de
couverture. Les axes utilisent la même échelle physique ; l'enveloppe verticale
reste vide aux faces. Calcul à la préparation, pas dans le shader. API/recette,
champ 2D, texture 64³ High et 24 pas de parcours inchangés. L'essai de phase
directionnelle ci-dessous reste retiré. Aucun code/asset Cadle utilisé.

**Travail expérimental incomplet, pas un résultat artistique retenu.** Capture
native Full HD inspectée : masses davantage séparées, mais motif trop régulier
et aspect en pavés encore visible. Ne pas qualifier ce changement de ciel naturel,
ni promouvoir un profil sur cette base. Le code demeure dans le seul aperçu
opt-in pour poursuivre la correction de la répartition ; aucun environnement
par défaut n'est activé. La suite doit casser cette régularité, pas ajouter
un nouveau réglage de lumière ou répéter la capture inchangée.

Vérifications : quatre tests volume PASS (dont frontières de cellules/volume
sur quatre seeds), Clippy monde strict PASS, build natif PASS ; workspace
**613 PASS, 0 FAIL, 1 timing ignoré**. Les tests vérifient déterminisme, continuité
échantillonnée, périodicité, couverture monotone, faces vides et budget mémoire,
pas l'esthétique. Runtime sorti 0, kit 20/20 et 36 colliders, 650 arbres, dôme
présent, 262144 octets R8 et 24 pas observés. Capture et compilation concurrente :
pas de comparaison FPS ou de qualification du coût de génération.

Preuve : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-cloud-lobes-20260908/`, script
`Run-CloudPhase-20260907.ps1` externe relu et réutilisé. Entrées épinglées identiques
au cas sans vent `r1-cloud-phase-before-20260907`, hormis le binaire. Temps du monde
non gelé ; pas de parité pixel de la végétation. Nouveau bundle Web et exécution
Web/Tauri non acquis ; le Studio déployé conserve son ancien champ nuageux.
Check Studio WASM et fmt PASS ; avertissement amont de compatibilité future
`proc-macro-error2` 2.0.1 conservé. Ce check ne constitue pas un lancement Web.

SHA-256 : source volume `d1aa615573b8dc5bb52f0bd1c127af9bc430ee5a64f5a4c94a4bbf379d64a417` ;
runtime `01f367c10a342c26d8dc65b21b957f4024de39f994aeaa183c616358ee63d862` ;
PNG `a9f1ed11cd080f1f7b4263a3dfd06dd868bcb95ff6b9c1d8486bd1c429472a17`.

## Essai de diffusion directionnelle non retenu — 8 septembre

Une paire native a testé une variation isolée de l'éclairage direct : mélange
isotrope/Henyey–Greenstein, g=0,45, poids directionnel 0,35. Référence de méthode
(pas de code copié) : [PBR, Phase Functions](https://www.pbr-book.org/4ed/Volume_Scattering/Phase_Functions).
Gain moyen angulaire vérifié numériquement à 1,00000001238, bornes 0,74156–2,32769.
Ce contrôle arithmétique n'est ni un test WGSL ni une simulation physique complète.

Acquisition locale : `Run-CloudPhase-20260907.ps1` sous le répertoire externe de
preuves, sorties `r1-cloud-phase-before-20260907` et `r1-cloud-phase-after-20260907`.
WorldSketch/caméra de validation, kit rev.4 et réglages nuageux identiques ; vent
nuageux nul. Deux captures 1920×1080 réellement inspectées, runtime sortie 0,
kit 20/20, 36 colliders, 650 arbres et un dôme. Inputs hash-vérifiés avant/après.
Le temps global du monde n'est pas gelé : pas de parité pixel du sol/vent exigée.
Les métriques dev avec capture et compilation concurrente ne sont pas un benchmark.

**Décision : essai non retenu.** Le changement lumineux est visible mais ne corrige
pas l'aspect diffus et aplati des masses. Aucun gain convaincant de réalisme établi
sur cette vue ; ne pas multiplier les réglages de lumière pour traiter un défaut
de forme. Les ajouts WGSL de ce lot ont été retirés, shader restauré à
`2d3048164e8968eeba20ca2ede3151925c89e729a6b62b4108e4888c09c3529f`.
Les artefacts expérimentaux sont conservés, pas promus dans le Studio.

Reconstruction après retrait terminée, sortie 0 (1 min 10 s), runtime de travail
SHA-256 `263f854437e9246ef7fa8da9a9da975640e639c87aac4a6f65fec9ae416a0253`.
Ce dernier binaire n'a pas fait l'objet d'un lancement supplémentaire ; la
capture avant essai reste liée au binaire avant essai, pas à ce nouveau hash.

Build de l'essai PASS ; workspace 612 PASS, 0 FAIL, 1 timing ignoré, sortie 0.
Ces tests ne qualifient pas le réalisme du shader. Aucun nouveau test Web/Tauri,
benchmark cible ou passage de gate. La prochaine amélioration doit traiter la
morphologie nuageuse avant une nouvelle qualification du ciel.

SHA-256 avant / essai :

- runtime : `a40938c3142bb1ce822b4386112c28aa8f7fcfe1f073682fcad95fce4b44a680` /
  `857d222623ce3c10b6cddce8953464905b5d296c1256336737cb9c15d13813e0` ;
- PNG : `83e92ace6632787a7d9050c5f0cc48128da8a8577b9daaabf199041c63125fb7` /
  `566a945dfb6aef8cadd80352e7a28298fcaf2787f7143a35a248b948d0503c28`.

7 septembre 2026, Rust local. Gate 1 ouverte.

## Portée

Option native explicite `--cloud-layer-preview settings.json`, réservée aux scènes
Visual Valley. Elle ne modifie pas EnvironmentProfileDocument v1 et n'active rien
par défaut. Le fichier utilise `CloudLayerSettings`, validé avant génération.

Une texture R8 256×256 et une sphère inversée 32×16 servent de support. La sphère
suit la caméra active à 90 % de son far clip ; le shader projette le rayon sur
l'altitude logique de la couche et échantillonne le champ périodique. Vent piloté
par le temps virtuel, éclairage lié à la couleur/illuminance du soleil et exposition
caméra. Fondu près de l'horizon. Ni nouveau collider ni ombre projetée.

Le support n'est pas un volume physique ; les nuages ne sont pas inclus dans la
cubemap atmosphérique ni dans les reflets associés. Pas d'ombre sur le terrain,
pas de diffusion multiple, pas de mips générés pour ce premier aperçu, pas de
profil qualité intégré. Occlusion de géométrie située au-delà du support et
échantillonnage rasant restent à qualifier. Pas de soleil ni de caméra unique :
support masqué. Cet aperçu ne prouve pas un ciel final réaliste.

Références Bevy 0.19.1 inspectées localement : Material (enable_prepass, shader),
mesh_view_bindings et render_sky (exposition), Mesh::invert_winding. Aucun shader
Cadle copié, aucune dépendance nouvelle résolue. serde_json, déjà dans le graphe,
passe de dépendance de test à dépendance runtime pour l'entrée d'aperçu.

## Vérification en cours

Clippy strict workspace/all-targets/all-features PASS. Échecs intermédiaires
conservés dans la trace : binding mutable du matériau, type de Query complexe,
serde_json auparavant réservé aux tests, parser dépassant 100 lignes. Corrections
sans assouplissement des règles : alias de filtre et petits setters de chemins.

Suite workspace : **592 PASS / 0 FAIL / 1 timing ignoré** avant la correction
d'ordre transparent ci-dessous. Tests nouveaux moteur : suivi caméra/vent et
masquage avec deux caméras ; runtime : option explicite, scène requise et doublon
refusé. Artefacts :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-cloud-layer-20260907/`.

## Premier lancement natif — défaut visuel conservé

`first/clear.png` capturée et inspectée, SHA-256
`822dc776da84afff3bde63086f98abe0f4cd21e8d17645fc4cc8924dc2e74d23`.
1920×1080, RX 7800 XT, High, kit gouverné, vue human_bridge. Sortie 0, zéro ERROR,
118/118 pipelines prêts. Capture émise à 382, callback 385, huit observations stables.
Nuages visibles mais plats/crème ; des bandes passent devant le feuillage transparent.
Cette première image n'est pas acceptée comme amélioration réaliste.

Cause de superposition : le tri Transparent3d utilise le centre de la sphère,
positionné à la caméra. Référence Bevy locale `core_3d::TransparentSortingInfo3d`
et `ViewRangefinder3d` inspectée. Le matériau est désormais trié à `-far` par son
biais de tri, sans changer le test de profondeur géométrique du shader personnalisé.
Test CPU de suivi enrichi : biais négatif contrôlé. Rejeu ciblé PASS ; nouvelle
capture requise avant d'affirmer que la superposition est corrigée à l'écran.

Premier smoke : 65,89 FPS moyens, p95 17,4522 ms, 1 % low 38,17 FPS ; capture
incluse, pas de comparaison contrôlée ni de qualification du coût ajouté.

## Rejeu après correction du tri

Test moteur ciblé PASS et Clippy moteur/all-targets/all-features strict PASS sur
la version finale. Les 592 tests workspace précèdent cette correction du biais.
`sorted/forge_runtime.exe` conservé séparément, SHA-256
`9dc6680a39f7143ad5e904cdb394ca650456218723b8080894269848c1094485`.
`sorted/clear.png` inspectée, SHA-256
`f45f813e6b7f8692c31fa0e284609a00b6530f7c893b063887248259928f96ff`.
Entrées épinglées identiques et vérifiées après exécution. Sortie 0, zéro ERROR,
118/118 pipelines prêts, capture émise à 350 et callback 353.

Le défaut de superposition sur les arbres proches n'est plus visible sur cette vue.
L'apparence reste plate/crème avec des bandes répétitives près de l'horizon :
**aperçu non retenu comme ciel final réaliste**. Cette capture fixe ne qualifie pas
le vent animé, les autres vues ou la météo. Aucun replay Web de ce shader.

Smoke final : 64,46 FPS moyens, p95 17,7427 ms, 1 % low 34,55 FPS, RX 7800 XT,
High sans VSync, capture incluse. Pas de performance cible ou de coût comparatif
qualifié. Critère ciel et Gate 1 restent ouverts. Prochain travail : donner du
relief visuel aux nuages et supprimer leur répétition rasante avant promotion,
puis raccorder le profil/qualité et les diagnostics explicites.

## Filtrage distant et épaisseur — complément exécuté

Neuf niveaux R8 précalculés par moyenne linéaire 2×2, jusqu'à 1×1 ; 87 381 octets
de données pour 256×256, filtrage trilinéaire et repeat. Une troisième lecture de
la même texture, à une autre échelle, module épaisseur/opacité/éclairage. Fondu
d'horizon élargi. Aucun document ou réglage du candidat externe changé.

Deux tests moteur ciblés PASS (suivi/tri et mips : taille, constantes, moyenne),
Clippy strict moteur/all-targets/all-features PASS. Pas de nouveau passage workspace
complet déduit. La capture native finale est `filtered/clear.png`, SHA-256
`5f260ac642dcf75cdb737e55a0c0668423737479b0d4cf1543599db4ecb8b6a8` ; binaire
conservé `filtered/forge_runtime.exe`, SHA-256
`a04a26241a03c7e04770310cc7a41fa256a0ef5cecbca659e8cc31681cba8f3f`.

Sortie 0, zéro ERROR, 118/118 pipelines prêts ; émission 356, callback 359.
Capture ouverte et inspectée : horizon moins rayé et épaisseur moins uniforme,
pas de retour du recouvrement du feuillage sur cette vue. Ciel encore stylisé,
pas d'acceptation réaliste, ni d'animation GPU qualifiée par cette image seule.
Cette évolution combine mips, détail et fondu : ne pas attribuer tout le résultat
aux seuls mips. Temps de capture différents, pas de comparaison pixel stricte.

Smoke 239 échantillons : 64,44 FPS moyens, p95 17,8469 ms, 1 % low 25,87 FPS.
Capture incluse ; pas de gain/perte attribué au shader sans mesure comparative
contrôlée. Performance cible et Gate 1 restent ouvertes. Aucun nouveau replay Web.

## Diagnostic natif — 7 septembre 2026

`CloudPreviewObservation` est exporté dans le champ optionnel `cloud_preview` des
métriques runtime. Paramètres et hash canonique, temps virtuel, décalage UV, nombre
de dômes et visibilité CPU, taille R8 et mips sont explicites. Les états indisponibles
effacent le décalage précédent ; une caméra ambiguë masque le dôme. La visibilité
CPU ne prouve pas l'exécution GPU : `gpu_execution_verified` reste toujours false.
Ce champ n'est pas encore raccordé au diagnostic Studio ; aperçu désactivé par défaut.

Deux tests moteur ciblés PASS après enrichissement : déplacement à 100 puis 300 s
(UV x = 0,25 puis 0,75), hash constant, sérialisation, masquage et reprise après
suppression de la seconde caméra. Clippy workspace strict PASS avant cet enrichissement
de test ; Clippy moteur strict PASS après. Aucun nouveau total workspace revendiqué.

Rejeu natif unique sans capture : `Run.ps1 -Attempt diagnostics -DiagnosticsOnly`,
dans le même dossier de preuve local. Sortie 0, aucun ERROR/panic trouvé dans le log,
hashes des entrées vérifiés après exécution. Binaire conservé SHA-256
`28f04b4fc6c86fc8ce13f23e5ab1a129f1c0c002089e5a3f4933d55b699c3cd2` ;
`diagnostics/clear.json` SHA-256
`f0d07d97e84c4f7e8bb7fdf442df175c75970ba8ab07f144dd37e59ba7de13ba`.
État réellement lu : `projected_cpu_not_gpu_verified`, un dôme visible, neuf mips,
87 381 octets, temps 6,798563 s et UV `[0.016996408, 0.9915018]`.
Hash des réglages : `380e97d60b8f95e7b814c81ab3aebba7926d14c45e6c79923880ce1a1d3d6182`.

Aucune nouvelle amélioration artistique ou animation GPU déduite de ce contrôle JSON.
Pas de benchmark comparatif, d'acceptation humaine ou de passage de Gate 1.

## Raccordement Studio Web — 7 septembre 2026

Implémenté : lien de menu vers une vallée avec `forge_clouds=preview`, candidat
JSON embarqué dans `examples/visual_valley/cloud-layer-preview.json`, même contrat
que le runtime. Valeur inconnue, doublon et scène Gate 0 refusés. Pas de paramètre :
aucun plugin installé. Aucun document de carte ou profil persistant modifié.
Le diagnostic Studio exporte l'observation et le statut du ciel ; absence = null.

41 tests Studio PASS, Clippy strict Web PASS, Trunk build PASS. Avertissement transitif
de compatibilité Rust future pour `proc-macro-error2 2.0.1`, pas une erreur projet.
WASM conservé sous `web/forge_studio_bg.wasm`, SHA-256
`8b4fa24f8ba1f39c66799c502fe057adb10b0248657f0f6f6aff933748f5f602`.
Serveur de preuve loopback port 1452. Même kit/vue/catalogue que la preuve routes,
réglages de nuages comparés au candidat natif et identiques.

URL effectivement ouverte dans Chrome :
`?forge_scene=world-sketch-visual-valley&forge_view=human_bridge&forge_view_input=candidate&forge_kit=governed&forge_forest=batches&forge_clouds=preview`.
Après un pas de temps de 250 ms : kit 20/20, 36 colliders prêts, 650 arbres.
Aucune erreur console lors du relevé. Un clic automatisé sur le menu résumé n'a
pas trouvé sa cible ; aucun succès de navigation par ce clic n'est revendiqué.
L'accès direct, le pas de temps et les deux exports ont réellement fonctionné.

PNG `forge-viewport-1788794458104.png` ouvert et inspecté : 1408×741,
nuages visibles sans recouvrement des arbres sur cette vue, encore plats/crème.
SHA-256 `7404f6b2456186fdcad08083c1958fa770a053a3f182724d69d980b5ce63cde1`.
JSON `forge-diagnostic-1788794460617.json` relu, SHA-256
`f917ea683477b35a4d204c4180a6bb1dc1790155c43ad2dfa34ee41eac0138f4`.
Il contient le même hash de réglages que le natif, temps 0,25 s, UV
`[0.000625, 0.9996875]`, un dôme visible et `gpu_execution_verified=false`.
`gate_passed` et `human_acceptance` restent false. Copies locales conservées dans
le dossier de preuve ci-dessus ; ce ne sont pas des captures Full HD qualifiantes.

Premier rendu du shader dans Chrome vérifié, pas sa fluidité animée, sa qualité
artistique finale, le coût comparatif, les profils ou Tauri/WebView2. Gate 1 ouverte.

## Essai de traversée volumique bornée — rejeté

Une variante locale a parcouru une couche logique en huit étapes (24 lectures R8
maximum au lieu de trois), avec absorption exponentielle et atténuation solaire
approximée. Même texture, mêmes entrées épinglées et même scène native. Deux tests
moteur et Clippy strict moteur PASS pour cet essai ; ils ne qualifient pas son rendu.

Lancement `Run.ps1 -Attempt slab`, sortie 0, sans ERROR/panic trouvé dans le log.
Capture native `slab/clear.png` ouverte et inspectée : **strates répétées très visibles
dans les nuages**, résultat rejeté. SHA-256
`7c3398d4938534490f6ab28e78f362ef4b5b0d4feee3ad3bceae4f0a6af91c28`.
Binaire de l'essai conservé seulement dans `slab/forge_runtime.exe`, SHA-256
`dc2d09cb08998ab2d8cff9d66e7e006704242c9d455f18e6ecf3ec4b26e2f510`.
Smoke 239 échantillons avec capture : moyenne 65,04 FPS, p95 17,9618 ms,
1 % low 31,04 FPS. Ce n'est pas une mesure comparative de surcoût.

Le shader, l'uniforme supplémentaire et les compteurs spécifiques de cette variante
ont été retirés du code actif, en préservant tout le travail antérieur. L'aperçu à
trois lectures vérifié dans Chrome reste la base, toujours non accepté artistiquement.
Pas de campagne de performance sur une variante déjà rejetée visuellement. Ne pas
reprendre cette discrétisation verticale à huit tranches comme solution qualifiée,
ni augmenter aveuglément son coût pour masquer le défaut. Aucun passage de gate.

Après retrait : `cargo fmt --all --check`, `git diff --check` et
`cargo build -p forge_runtime` PASS. L'exécutable courant du cache E: a donc été
reconstruit sans la variante rejetée. Shader actif SHA-256
`e13b1788ad3dc39cfe322358b72175ea3a9db944049e31cb252a87cf1548443b`.
Pas de nouvelle capture de la version antérieure pour ce simple rétablissement.

## Essai de relief par normale de hauteur — rejeté, 7 septembre

Essai borné à quatre lectures R8 par fragment (au lieu de trois) : normale par
différences finies, direction solaire complète et opacité exponentielle. Aucun
changement des textures générées, du JSON de réglages ou des sources de carte.
Deux tests moteur PASS, Clippy moteur strict PASS et build natif dev PASS.
Une première erreur Clippy sur la longueur du test a été corrigée en extrayant
ses assertions, sans assouplissement de règle.

Preuve conservée : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-cloud-relief-20260907/`.
`clear.png` SHA-256 `ee577a32172ae00827b0b8420b19e9a7e463c9f04a189afe5f146202bc557288`.
Sortie 0, aucun ERROR/panic, entrées conservées, 20 instances/36 colliders,
650 arbres, 1080p ; 118/118 pipelines prêts, capture 1200, callback 1203.
Image inspectée : couverture plus opaque mais formes encore plates/crème ;
pas de gain suffisant pour retenir la variante. Vue/kit plus récents que l'ancien
smoke filtré : aucune comparaison pixel ni performance causale déduite.
1799 échantillons avec capture, build dev : 61,177 FPS, p95 17,264 ms,
1 % low 51,844 FPS. Ce n'est pas une mesure release ni un benchmark comparatif.

Variante retirée après inspection : shader et `cloud_preview.rs` restaurés
**aux hashes exacts du candidat release épinglé**, comparaison SHA-256 PASS,
`git diff --check` PASS. Binaire d'essai conservé en preuve ; le binaire debug du
cache contient encore cet essai et doit être reconstruit avant réutilisation.
Le binaire release immuable reste utilisable avec les sources rétablies.

Décision : ne pas poursuivre les ajustements d'éclairage seuls. La prochaine
amélioration du ciel doit porter sur la morphologie/épaisseur des nuages ; ni les
huit tranches verticales rejetées ni cette normale de surface ne constituent la
solution. Pas d'augmentation de coût conservée, pas de promotion Web/profil,
pas de qualification artistique ou passage de gate.

## Morphologie 3D préparée, pas encore rendue

`forge_world::generate_cloud_density_volume` produit maintenant un champ R8
continu en trois dimensions, moteur-neutre, seedé, périodique horizontalement,
avec faces supérieure/inférieure vides. Bruit de couverture horizontal plus
érosion 3D interpolée et enveloppe verticale ; aucune copie de tranches 2D.
Résolution bornée 32–128, puissance de deux ; maximum réellement généré en test :
2 097 152 octets. Wind/altitude/échelle/opacité ne régénèrent pas la morphologie.
L'ancienne fonction 2D et son usage actif restent inchangés.

Inspection préalable du brouillard existant : sa texture 32³ ne contient qu'une
enveloppe de bord, et sa projection lumineuse est bornée par les ombres proches.
Elle n'est pas réutilisée abusivement comme preuve de nuages à 1500 m.
Le nouveau champ ne crée ni renderer, ni dépendance, ni document persistant.

Vérifié : six tests nuages (dont trois nouveaux), puis **118 tests forge_world
PASS / 0 FAIL**, Clippy strict forge_world/all-targets PASS, format et diff PASS.
Contrôles : déterminisme/seed, budget maximal, entrées invalides, tranches distinctes,
continuité verticale, périodicité horizontale, couverture monotone et indépendance
des paramètres de présentation. Pas de total workspace ou de parité WASM déduit.

**Non implémenté à ce stade** : upload de ce volume, parcours et éclairage GPU,
diagnostics associés, profils/qualité et raccordement Studio. Aucun nouveau ciel
affiché ni coût GPU qualifié. Prochaine étape : raccordement borné dans l'aperçu,
puis un contrôle visuel décisif ; ne pas promouvoir le champ sur ses tests seuls.

## Volume 3D raccordé à l'aperçu natif

L'option explicite existante utilise maintenant un R8 64³ (262 144 octets),
échantillonné linéairement, périodique en X/Z et borné en Y. Le parcours a 24 étapes
maximum et deux lectures d'ombrage solaire par étape, soit au plus 72 lectures
de densité par fragment. Arrêt anticipé à faible transmittance. Épaisseur dérivée
du motif (`pattern_scale_m × 0,12`), soit 480 m pour le candidat. Aucun changement
des réglages source ou de la densité 2D indépendante. Pas d'activation par défaut.

Le diagnostic expose dimension, octets, profondeur et budget de parcours ;
`gpu_execution_verified=false` reste honnête : ce compteur est une observation
CPU, la capture est la preuve GPU distincte. Ni ombres de nuages sur le terrain,
ni inclusion dans l'IBL, ni diffusion multiple, ni profils qualité raccordés.
Un seul mip actuellement ; distance, mouvement et scintillement restent à qualifier.

Deux tests moteur ciblés PASS (upload/axes et projection/topologie/diagnostic),
Clippy strict moteur/all-targets/all-features PASS, build natif dev PASS.
Suite workspace terminée après le smoke : **600 PASS / 0 FAIL / 1 probe timing
préexistant ignoré**, code 0 ; log `workspace-tests.log` dans la preuve ci-dessous.

Preuve `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-cloud-volume-20260907/` :
binaire `400dfc492325aaaa201125c7dcb8647d463a41645f27d31017f7b154dc4ce0b2` ;
PNG `0c56f3f508ec6ffdd411c28195ce0fbc3d375c30540a256eb9e85c2cd2bca0cb`.
Sortie 0, aucun ERROR/panic, entrées épinglées conservées, 1080p, 20 instances,
36 colliders et 650 arbres. Pipelines 118/118, capture 1200, retour GPU 1203.
PNG inspecté : davantage d'épaisseur apparente, pas de strates répétées visibles
sur cette vue, mais masses trop lisses/uniformes et teinte crème. **Aperçu conservé
pour travail, pas accepté artistiquement ni promu comme ciel final.**

Smoke dev avec capture : 1799 échantillons, 59,605 FPS moyens, p95 17,807 ms,
1 % low 48,460 FPS ; un hitch >33,3 ms. Aucune comparaison de coût release déduite.
Les trois mesures release précédentes portent sur le candidat sans cet aperçu,
pas sur ce nouveau shader. Pas de lancement Web/Tauri de cette version.
Gate 1 reste ouverte ; ne pas rejouer les benchmarks avant de décider le rendu final.

## Morphologie affinée à budget GPU inchangé

Génération CPU seule modifiée : déformation périodique des coordonnées, couverture
sur huit cellules au lieu de quatre, davantage d'érosion 3D et transition de densité
plus courte. Texture toujours R8 64³, shader inchangé (24 étapes maximum).
Pas de nouvelle lecture GPU ou allocation ajoutée. Le coût CPU de génération
n'est pas annoncé inchangé ; aucun benchmark causal de coût n'a été effectué.

Six tests nuages PASS, Clippy strict forge_world/all-targets PASS, build natif dev
PASS, format/diff PASS. Les 600 tests workspace ci-dessus précèdent cette évolution
de la morphologie ; ne pas les présenter comme un nouveau passage complet.

Un smoke unique exécuté et inspecté, preuve
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-cloud-volume-shape-20260907/`.
PNG SHA-256 `bfb9a8cfbc79e9147c50b3963d03dd949487d225af51459fddb6a20f87e03714`.
Entrées et code épinglés par `pins.json`, code de sortie 0, aucun ERROR/panic,
kit 20/20, 36 colliders, 650 arbres, 1080p. 118/118 pipelines prêts,
capture 1200 / callback 1203. Paramètres source nuages inchangés.

Observation : masses plus fragmentées et moins lisses ; teinte crème uniforme
et couverture encore marquée. Conservé dans l'aperçu expérimental uniquement,
sans acceptation artistique. La comparaison n'est pas pixel à pixel : temps
virtuels des captures différents. Aucun ajout de détail à une carte particulière.
Smoke avec capture/dev : 1799 échantillons, 59,993 FPS moyens, p95 17,642 ms,
1 % low 49,884 FPS, aucun hitch >33,3 ms. Pas de qualification release/Web/Tauri.

## Même volume vérifié dans Chrome WebGPU

Clippy strict Studio/WASM PASS, Trunk offline/locked **profil dev** PASS, sources
épinglées inchangées après compilation. Avertissement transitif de compatibilité
future `proc-macro-error2 2.0.1` conservé ; pas une erreur projet.
Preuve : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-cloud-volume-web-20260907/`.
WASM SHA-256 `56795d97829fa6431578936e84e00ca53bea5350fb5faf8e6317666cbf040a0d`.
Bundle et six GLB copiés dans un nouveau dossier ; poses identiques au candidat
natif rev.4. Liaison WorldSketch/caméra Web préexistante conservée ; preview
Web rev.3→4 uniquement. Sources, copies et assemblage épinglés/vérifiés.

URL loopback port 1456, paramètres réellement ouverts dans Chrome :
`?forge_scene=world-sketch-visual-valley&forge_view=human_bridge&forge_view_input=candidate&forge_kit=governed&forge_forest=batches&forge_clouds=preview`.
Un clic initial a expiré pendant le chargement, temps encore 0 s vérifié avant
reprise. Le clic suivant fonctionne : 0,250 s, kit 20/20, 36 colliders, 650 arbres,
7800 primitives LOD. Console d'erreurs relevée vide. Aucun geste d'édition source.

Exports archivés après comparaison des hashes avec les téléchargements :
- PNG `forge-viewport-1788802321250.png`,
  `32e13a91f6257603ef1da900d8e96aa273ff720758eb62f2b90a5a9cbe7b690f` ;
- JSON `forge-diagnostic-1788802323281.json`,
  `2678400546a27f9d30f2c5bede18aad1e7f7db504743c6841e9e810d7d52818c`.

PNG 2048×1157 ouvert et inspecté : volume nuageux visible, même aspect crème
et masses encore lisses. JSON `observations.cloud_preview` relu : dimension 3D,
262144 octets, un mip, profondeur 480 m, 24 étapes, un dôme visible,
settings hash identique au natif, UV `[0.000625,0.9996875]`, temps 0,25 s.
Le compteur CPU conserve `gpu_execution_verified=false` ; la capture constitue
la preuve visuelle séparée. Ce pas de temps ne prouve pas la fluidité du vent.

Raccordement du nouveau shader Chrome exécuté ; ni Tauri/WebView2, ni benchmark
Web, ni comparaison optique synchronisée avec le natif, ni acceptation artistique
déduits. Aperçu toujours explicite, Gate 1 ouverte, aucune publication Git.

## Éclairage indirect du ciel dans les nuages — natif

Le shader remplace le remplissage constant teinté par le soleil par une lecture
de l'environnement diffus de la vue. Rotation et intensité de la cubemap respectées,
une lecture hors de la boucle volumique ; maximum théorique 72 lectures de densité
plus une lecture d'environnement par fragment. Pas de nouvelle texture ni de
couleur de carte ajoutée. API WGSL Bevy 0.19.1 inspectée localement
(`mesh_view_bindings`, `environment_map::quat_rotate`) ; aucun fork/copie d'asset.
Les nuages consomment l'IBL, mais ne sont toujours pas inclus dans son contenu.

Premier lancement échoué (`r1-cloud-sky-fill-20260907/`) : import d'environnement
non gardé lorsque la variante est compilée sans `ENVIRONMENT_MAP`. Log et binaire
conservés, aucune capture qualifiée. Import et fonction désormais conditionnels,
remplissage noir sans environnement ; aucune désactivation silencieuse du ciel.

Build dev corrigé PASS. Smoke final
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-cloud-sky-fill-fixed-20260907/` :
sortie 0, aucun ERROR/panic, 118/118 pipelines prêts, capture 1200, callback 1203.
Kit complet, 36 colliders, 650 arbres, 1080p, entrées épinglées conservées.
PNG `f02498569dcf93cbf2147bea54bda6efafd4d28903b08343c5e959690ec0bb3a`
ouvert et inspecté : zones ombrées moins jaunes et contraste accru ; masses
encore lisses. Conservé dans l'aperçu, pas accepté comme rendu final.

1799 échantillons avec capture/dev : 59,823 FPS, p95 17,840 ms, 1 % low
39,523 FPS. Ce smoke ne passe pas les seuils locaux et ne mesure pas causalement
le coût du changement. Pas de campagne release déclenchée. La preuve Chrome
précédente ne comprend pas ce dernier shader ; contrôle optique IBL isolé,
Web/Tauri et qualification artistique restent dus. Format/diff contrôlés,
aucun nouveau passage de tests CPU revendiqué pour cette modification WGSL seule.

## Raccordement IBL confirmé visuellement dans le Studio

Bundle dev Web reconstruit offline/locked, Clippy Web PASS, sources inchangées.
WASM `ccb5c09d140f1bdf3facde4dbfc9eae0e400f7d7348de30b15c8ed229f0e3390`.
Nouvel assemblage rev.4, copies et poses vérifiées ; aucun ancien bundle écrasé.
Preuve `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-cloud-sky-fill-web-20260907/`,
serveur loopback 1457, mêmes paramètres de vue/kit/nuages que la preuve Chrome précédente.

Cycle réellement exécuté par les boutons du Studio : IBL 1→0→1, sans avancer
le temps après le pas initial de 250 ms. Trois diagnostics relus : temps 0,25 s,
caméra human_bridge, mêmes hashes de caméra et de nuages, kit 20/20, 36 colliders,
650 arbres, même réflectance et teinte d'eau. PNG 2048×1157 tous ouverts/inspectés.
Pas de changement de profil qualité ou de carte. Erreurs console : liste vide.

| État IBL | PNG | JSON |
|---|---|---|
| 1 initial | forge-viewport-1788803127167.png | forge-diagnostic-1788803129383.json |
| 0 coupée | forge-viewport-1788803152376.png | forge-diagnostic-1788803154405.json |
| 1 restaurée | forge-viewport-1788803181627.png | forge-diagnostic-1788803184657.json |

Copies archivées et comparées aux hashes des téléchargements. PNG respectifs :
`9cdc4204e840de82bf7caf1e8ee5a32af4393705872ad6ab4795a816c9a7ca1e`,
`936fd6d9c6c11931062880f970cedbb26a73c17399a0c185ece8677a235a8852`,
`27d3d8690984d5968b9b619909c2b29f6f01873720f4441591a864147ebd9105`.

Observation : assombrissement net des zones nuageuses ombrées à IBL 0, retour
visuel à l'éclairage initial après restauration. L'eau et les objets changent
aussi : la commande coupe l'IBL de la scène, pas seulement celle des nuages.
Ce contrôle confirme le raccordement visible, pas une invariance pixel à pixel
ni une qualification photométrique. Une capture par état, donc **pas** le protocole
statistique à trois captures par état de `R1_WEB_OPTICAL_SAMPLE_PROTOCOL.md`.
Pas de campagne supplémentaire pour ce raccordement déjà observé ; fluidité,
profils, rendu final, performance release et Tauri demeurent des critères séparés.
`gate_passed=false` et `human_acceptance=false` dans les trois exports.

## Budget provisoire par niveau demandé

Le budget de l'aperçu suit désormais `EnvironmentQualityRequest` : Low désactive
le dôme, Medium 12 étapes, High 24, Ultra 32. Sans demande de qualité, le budget
par défaut reste High ; le diagnostic indique alors `requested_quality_tier=null`.
Table moteur-neutre `forge_project::cloud_preview_view_steps`, non persistée et
explicitement provisoire : ce n'est pas une extension silencieuse du contrat de
preset effectif. Aucun acquittement de preset complet ni activation par défaut.

La texture 64³ reste allouée en Low ; seule sa présentation est désactivée,
avec statut `disabled_by_preview_quality_budget` et zéro étape annoncé.
Réactivation sur les mêmes handles, aucune régénération ni mutation des réglages.
Le shader borne le parcours à 32, puis au budget uniforme demandé. Maximum Ultra :
96 lectures de densité et une d'environnement ; High conserve 72+1.

Deux tests moteur ciblés PASS, dont cycle Low/Medium/Ultra/High enrichi : valeurs,
visibilité, hash des réglages constant, un matériau conservé et pas de preuve GPU
inventée. Clippy moteur strict/all-targets/all-features PASS, build natif dev PASS.
Smoke High `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-cloud-quality-20260907/` :
sortie 0, aucun ERROR/panic, 118/118 pipelines prêts, capture 1200/callback 1203.
PNG `f1e55a73bfae1126c0dd237ce1543ffe0d6ddf67cce33b8488cf4b5f9e047d35`
inspecté, kit complet, 1080p, diagnostic High/24. Image cohérente avec l'aperçu,
pas de nouvelle acceptation artistique. 1799 échantillons avec capture/dev :
60,578 FPS moyens, p95 18,240 ms, 1 % low 37,335 FPS ; pas de seuils qualifiés.

Suite workspace terminée : 600 PASS, 0 FAIL, 1 ignoré. Clippy Web et build dev
terminés dans `r1-cloud-quality-web-20260907/`. WASM SHA-256
`5f13f50f10a24440395bf5edd8096149ad171337d7c511efe40b97f04a17460f` ;
pins des sources revérifiés inchangés après lancement. Avertissement de compatibilité
future de proc-macro-error2 2.0.1 conservé, pas une erreur Clippy du projet.

Assemblage kit rev.4 réussi sans mutation des placements. Chrome, port local 1458 :
Low demandé à temps 0,250 s ; capture `forge-viewport-1788804302353.png`
(2048×1157) inspectée, nuages absents, kit 20/20 et 36 colliders annoncé prêt.
Retour High demandé ; aucune erreur console observée. Les demandes suivantes de
téléchargement JSON puis PNG High n'ont produit aucun nouveau fichier dans Downloads.
La cause n'est pas établie : ne pas confondre le message « téléchargement demandé »
avec un fichier acquis. Cycle visuel Low/High incomplet, budgets Web non confirmés
par JSON ; seuls les tests ECS prouvent encore le cycle numérique des quatre niveaux.
Pas de nouvelle campagne de rendu ni de promotion de gate déduite.

Reprise du diagnostic d'export : l'UI annonce High avec budgets disponibles appliqués,
kit 20/20, 36 colliders et « Capture GPU reçue 2048 × 1157 » pour
`forge-viewport-1788804356463.png`. Ce fichier reste absent de Downloads lors du
contrôle suivant. Le PNG utilise le callback Bevy `save_to_disk`, le JSON un lien
Blob propre au Studio : un défaut du seul sérialiseur JSON n'expliquerait pas les
deux absences. Aucune cause navigateur confirmée. L'inspection de
`chrome://downloads/` a été refusée par la politique de l'outil ; aucun contournement
ni changement des permissions du navigateur. Vérification humaine du téléchargement
requise pour poursuivre cette preuve via l'export actuel. Rendu inchangé.

### Essai du cycle global natif : échec conservé

Le 7 septembre, le binaire High déjà épinglé
`4a1c245bb5a0a2aa45237f565ff55ce3fcb2ceeb2ae17de155e3386535f91040`
a été lancé avec le même WorldSketch, kit rev.4, catalogue, forêt regroupée et
réglages nuageux, mais `--quality high --quality-cycle-proof` au lieu du smoke
à nombre de frames fixe. Journal dans
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-cloud-quality-native-cycle-20260907/runtime.log`.
Runtime sortie **1**, `quality_cycle_failed`, délai de 120 secondes expiré,
aucune capture d'étape acquise. Les pins du smoke High concordent encore avec
leurs fichiers pendant cet essai ; aucun rendu ou paramètre source modifié.

Obstacle identifié dans le code : `global_quality/effective.rs` classe la végétation
`Unqualified` dès que `prototype_preview` est présent. Le cycle global exige
un plan sans blockers et `gpu_ready` dans `global_application_matches`.
Cette incompatibilité empêche de qualifier le cycle du candidat expérimental
par ce protocole, indépendamment de l'export Chrome. Pas de diagnostic détaillé
de tous les blockers runtime déduit du seul timeout. Ne pas relancer ce cycle
inchangé, ni supprimer son garde-fou pour obtenir une capture. Qualification de
la forêt et preuve ciblée des budgets restent distinctes ; aucune promotion.

### Refus immédiat du scénario incompatible

Le CLI refuse désormais `--quality-cycle-proof` avec l'une des deux options de
forêt expérimentale dès la validation des arguments, avant lecture des assets et
création des sorties. Le message explicite `prototype vegetation is Unqualified`
et distingue les captures ordinaires, toujours autorisées, d'une qualification globale.
Préparer un catalogue sans activer la forêt n'est pas interdit. Aucun critère moteur
ou profil effectif modifié ; aucun contournement du refus GPU.

74 tests runtime PASS, Clippy runtime/all-targets strict PASS, build dev PASS.
Le nouveau test couvre les deux variantes, l'ordre des arguments, la préparation
seule et les captures ordinaires. Premier test négatif corrigé : la fixture plaçait
initialement la sortie à la frame de capture par défaut, contrairement à la règle
existante des six frames de marge ; règle conservée, capture du test fixée à 60.

Exécutable testé SHA-256
`6278f4ebc3e4dd1a953d173d044d515390e45cee40c8ce547590f9e2d87c3a2c` :
invocation avec chemins d'assets inexistants et forêt regroupée refusée en phase
`arguments`, sortie 2, 408 ms observées, aucun répertoire de preuve créé.
Ce temps est une observation du refus CLI, pas un benchmark moteur.
Le cycle normal sans forêt reste couvert par les tests ; pas de nouveau rendu requis
pour cette validation d'arguments. Le bundle Web précédent n'est pas reconstruit.

### Ressources nuageuses dans l'inventaire GPU

Autre manque identifié : `gpu_submission/assets.rs` ne reconnaissait pas
`CloudMaterial`, donc toute entité de ce matériau était comptée inconnue.
L'inventaire déclare maintenant son matériau et son image de densité 3D, via les
mêmes contrôles de préparation/bind group que les autres matériaux. Aucun compteur
de ressources préparées n'est inventé. Les images IBL restent collectées depuis
les composants d'environnement existants. Les ressources conservées en Low restent
dans cet inventaire, même lorsque le dôme est masqué.

Le remplacement du handle de densité change les exigences collectées : le mécanisme
existant de remise à zéro de la preuve de queue s'applique. Le test couvre la
dépendance exacte, son remplacement, la disparition du matériau et l'absence de
preuve GPU dans un World CPU. Les uniformes restent privés ; seul le type et son
handle de densité sont accessibles à l'intérieur de la crate. Un Default réservé
aux tests permet de construire la fixture sans ouvrir les uniformes de production.
Premier essai de compilation du test refusé pour accès aux champs privés ; corrigé
dans la fixture, sans élargir leur visibilité.

16 tests GPU-submission ciblés PASS ; Clippy moteur all-targets/all-features strict
PASS ; suite workspace **602 PASS, 0 FAIL, 1 ignoré** (21+3+4+11+262+63+74+1+41+4+118).
Pas de changement shader/recette/rendu, pas de nouvelle capture ou préparation GPU
réelle déduite de ces tests. Le bundle Web et les captures antérieures restent liés
à leur ancien code d'inventaire. La forêt demeure `Unqualified` ; le refus CLI et
les critères de gate sont conservés. Intégration GPU courante à observer séparément.

### Cycle GPU natif isolé exécuté après le raccordement

Script `E:/OpenRPG_Forge/.gate1-evidence-runtime/Run-CloudGpuInventoryCycle-20260907.ps1`.
Même kit rev.4 et WorldSketch, **sans** activation du remplacement expérimental
de forêt : arbres procéduraux de scatter, modèles du kit toujours présents.
Cette différence isole le matériau nuageux ; ce n'est pas le candidat complet.
Build dev PASS, exécutable SHA-256
`31ded77447e66768311d67d5e09625e56bde5993615e443d26f609a00bd84b65`.

Premier essai `r1-cloud-gpu-inventory-cycle-20260907` : runtime sortie 0 et cinq
PNG, mais contrôle du journal en échec (événement de fin absent). Cause identifiée :
`RUST_LOG=warn` hérité masquait les événements info de preuve. Artefacts conservés,
pas de succès rétrospectif attribué à ce contrôle.

Rejeu `r1-cloud-gpu-inventory-cycle-logged-20260907`, avec
`RUST_LOG=warn,forge::proof=info,forge::quality=info` dans le processus de script :
sortie 0, cinq étapes capturées et événement de fin acquis, aucun ERROR, pins
inchangés. High/Low/Medium/Ultra/High : `gpu_ready=true` à chaque étape,
**`full_preset_applied=false` conservé**. Journal SHA-256
`a65d4402e47dbde7665506225b977bc221d92d96e454551dfdbe3236e655054c`.

Les cinq `captures/stage-0.png` à `stage-4.png` (1920×1080) ont été inspectés :
nuages présents en High, absents en Low, présents en Medium/Ultra et au retour High.
Pas d'invariance pixel à pixel (temps/vent évolutifs). Silhouettes de nuages encore
lisses ; la cadence glissante affiche des hitches de transition, notamment 1 % low
1,6–1,8 FPS en Ultra/retour High : ni fluidité ni performance qualifiées par ce cycle.
Le champ `resolution=0` du journal Global n'est pas une mesure du viewport ; les
dimensions rapportées ici sont celles des PNG. Aucun résultat Web/Tauri ou art déduit.
Ne pas rejouer ce contrôle isolé sans modification pertinente.

## Échelle verticale de morphologie — 7 septembre

Le bruit 3D utilisait une hauteur normalisée complète alors que la couche physique
mesure 12 % de l'échelle horizontale : lobes comprimés verticalement. Le calcul
échantillonne maintenant la hauteur en unités de l'échelle horizontale. Le ratio
`CLOUD_LAYER_DEPTH_RATIO` est partagé par le générateur, le diagnostic et l'uniforme
du shader. Texture 64³ R8, nombre de pas et sources authorées inchangés. Aperçu seul.

Six tests nuages PASS ; Clippy monde/moteur strict, build natif et check Studio WASM
PASS (avertissement futur proc-macro-error2 conservé). Pas de nouveau total workspace.
Paire native avec forêt regroupée, kit rev4, High, vent nuageux nul dans un fichier
de preuve distinct : `E:/OpenRPG_Forge/.gate1-evidence-runtime/cloud-aspect-20260907/`.
`fixed-0` ancien et `fixed-1` nouveau : sorties 0, PNG 1920×1080 inspectés, hashes
terrain/végétation identiques et réglages nuages communs
`45807775f61f38959b65c4fe5727dfdd1d6c5175203cfb46684bea9b52ecbfe5`.
Premier `run-0` échoué sur redimensionnement non confirmé, journal conservé ; la
paire utilise ensuite la taille initiale et vérifie la résolution finale sans resize.

Relecture du protocole : `ensure_resize_confirmed` exige intentionnellement un
événement backend postérieur à la demande. Une taille déjà active n'est pas une
preuve de redimensionnement ; le contrôle reste inchangé. L'échec `run-0` n'est
donc pas reclassé PASS et n'autorise pas de qualifier une transition de fenêtre.
Les essais `fixed-*` qualifient uniquement leurs dimensions finales et leurs captures.

Observation : moins de petites strates, masses plus continues ; résultat toujours
flou/stylisé, pas un ciel réaliste accepté. Les binaires diffèrent aussi par les
correctifs de cache forêt précédents ; aucune attribution de gain de performance,
parité pixel du sol ou qualification des frames intermédiaires. Pas de preuve Web
visuelle ni raccord météo/profil complet déduit du check WASM.

SHA-256 avant/après :
- binaires : `1322b92345cd889c72e69d36cfec76fdafc3913523bf2979959aaa8c2115d3bd` /
  `aacc4450a483f29377932915adfa5371546dc52ac0978b884a72cf5d84dfca9f` ;
- PNG : `042933413211fdcad36c0d1b0e6817e6ccc1f8d2ff8c2a68936ba5abdfd375c2` /
  `c2fb4da9e4acccb749c06ad1071237aaf2ffa5fdf4f288733c3d657c515e026f` ;
- journaux : `a008ab1cda7de9a3bf435de9d25676a8fa3c677b99735fe55ad55d4361ed3726` /
  `ec1dbdbaf17278bbc4959c00ae2ba3436a7801c5135a66fd717200460b5530ad`.

## Rejeu Studio actualisé — 7 septembre

Bundle isolé `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-current-studio-20260907-b/`,
Trunk dev PASS, sources épinglées inchangées, kit rev4 assemblé avec copies vérifiées.
WASM SHA-256 `30828382077622ad656d9fd2fd0dbb31be94ccb57920cfcb9c375c9c162e3e2b`.
Premier build `r1-current-studio-20260907` refusé par Clippy Web : les retours Option
des préparations terrain/végétation sont toujours Some sur ce backend synchrone.
Deux `cfg_attr` WASM ciblés documentent ce contrat partagé avec le polling natif ;
aucun test fonctionnel supprimé. Clippy Web strict puis Trunk PASS sur le second essai.
Ce changement d'annotation n'ajoute aucun worker navigateur.

URL locale : port 1459, `forge_scene=world-sketch-visual-valley&forge_kit=governed&forge_forest=batches&forge_clouds=preview`.
Première URL sans scène refusée correctement ; clic sur le résumé de menu sans match,
puis ouverture avec scène explicite. Les onglets personnels n'ont pas été modifiés.
Studio observé : monde initialisé une fois, hashes alignés, une caméra, kit 20/20,
36 colliders prêts, 650 arbres/7800 primitives, forêt toujours non qualifiée.

PNG téléchargé et inspecté 2048×1157 : terrain/forêt/personnage/nuages présents,
nuages encore flous ; caméra libre différente de la paire native, aucune équivalence
pixel revendiquée. PNG et diagnostic effectivement reçus, copies hash-vérifiées.
L'ancien échec d'export sur le port 1458 n'est pas diagnostiqué ni rétroactivement résolu.
Diagnostic nuage : 64³ R8, 262144 octets, profondeur 480 m, 24 pas High,
hash réglages `380e97d60b8f95e7b814c81ab3aebba7926d14c45e6c79923880ce1a1d3d6182`.
`gpu_execution_verified=false` reste un diagnostic CPU correct ; la capture séparée
ne le transforme pas en preuve atomique. Ni cycle qualité complet, Tauri, animation
isolée, performance cible ni acceptation artistique acquis par ce smoke.

PNG source `forge-viewport-1788811177102.png`, copie `viewport.png`, SHA-256
`a0312978567631bb1b3f200d12cc665ef4583cba43eafab02c6fc517ae088e69`.
JSON source `forge-diagnostic-1788811211495.json`, copie `diagnostic.json`, SHA-256
`3435919f0bdc501ad22536f043c7ac1f416e99f0db9e11765b2d727d387a2b1a`.
