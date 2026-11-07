# ADR-0057 — Reconstruction temporelle et passages optiques communs

Date : 27 octobre 2026. Statut : réalisation Gate 7, qualification en cours.

## Décision

Le contrat de frame et la reconstruction temporelle existants deviennent
`forge_engine::source_temporal_frame` et `source_temporal`. Les passages
auxiliaires, leur couverture réelle, les séquences de jitter, historiques,
générations, acquittements et captures diagnostiques gardent leur algorithme.
AO et grain deviennent également des modules moteur. Les masques de coque
explicites rejoignent `source_hull_mask`, sans inventaire implicite des scènes.

`source_refraction` possède les identités transitoires de caméra de capture,
leur ordre de synchronisation et les transformations de shaders existantes
(clip de réfraction, couleur réfractée et masque du milieu visible). Les
branchements aux ressources de la scène restent dans l'adapter de réfraction.
Les deux programmes de surface de stand, utilisés par le runtime et ses tests
de conformité, rejoignent `source_surface::stall_programs` sans changer leurs
octets. Le moteur ne compile aucun fichier depuis la crate de carte.

Les consommateurs référence et Studio utilisent directement ces propriétaires.
La crate de carte ne conserve ni copie des passages transférés ni réexport
de leurs modules. Les noms internes WGSL/types et libellés diagnostiques de ces
passages utilisent le préfixe `source`. Les noms d'options diagnostiques de ce
code passent de `FORGE_TIDEWATER_*` à `FORGE_SOURCE_*`. Ce changement ne modifie
aucun document canonique, schéma de package, UUID de shader ou numéro de binding.
Les commentaires SPDX, copyright et provenance restent conservés.

## Vérification

Le plan central `shared-temporal-plan-001.json` et son application
`shared-temporal-transfer-001.json` épinglent 85 originaux (1 557 152 octets),
40 déplacements et 87 fichiers destinataires. Les sauvegardes restent dans
la même campagne ; aucun checkout parallèle n'est compilé. Le premier contrôle
de compilation révèle trois constantes de réfraction comprises par erreur dans
la plage de déplacement des identités : couche 17 et deux UUID sont rétablis
exactement depuis l'original. Le refus initial reste conservé.

Rejouer Clippy natif/Web, les tests transférés du contrat de frame, propriété
des historiques, couverture, masques, shaders et captures bornées, puis les
sondes GPU. Les contrôles de frontières suivent les propriétaires moteur et
interdisent toujours une dépendance compilée sur la carte ; leurs premières
erreurs de chemins restent conservées. Comparer les fonctions et assertions
aux originaux en distinguant explicitement les changements de noms internes.

Sur les sources 023 inchangées, Clippy strict natif et Web, le format et les
25 frontières passent. `shared-temporal-root-tests-001.log.receipt.json`
termine avec 2 780 tests réussis, aucun échec et 68 ignorés selon leurs
protocoles ; les 1 048 tests moteur incluent désormais les tests transférés.
L'audit `shared-temporal-source-equivalence-001.json` compare les 40 fichiers
aux sauvegardes après les transformations déclarées ; UUID conservés. Les
sondes GPU et l'activation dans les modules restent à prouver séparément.

La sonde optique native suivante passe sur deux scènes indépendantes, avec
dix PNG identiques au précédent essai 019 : AO, grain et flare transférés sont
exécutés sur le GPU Radeon/Vulkan. Cette preuve ne couvre pas encore TAA,
masques de coque ou leur activation dans les modules.

## Portée restante

Le transfert ne constitue pas l'activation de cette chaîne dans le viewport
canonique ou le jeu de modules. Le raccord de leurs matériaux réels, la
préparation/completion GPU de chaque passage, leurs profils, les déplacements
et les comparaisons restent dus. Le défaut visuel Medium constaté sur les
sources 018 demeure un refus ; déplacer le code ne le résout pas.

Le diagnostic ajouté au refus Nature conserve ses conditions de validité et
son message utilisateur ; il précise dans les journaux quelle condition périme
le readback. Il ne retire pas le refus et ne qualifie pas une sélection.
Le candidat de diagnostic 007 confirme que seule la disponibilité du résident
est fausse lors du refus, avec génération et séquence identiques. La correction
suivante attend son état prêt avant de demander le readback ; la réception et
ses gardes restent identiques octet pour octet, ainsi que le délai de 120 s.
Clippy Web strict passe ; le rejeu du candidat 008 reste requis.
Ce rejeu expire au délai inchangé : l'inventaire GPU compte lui aussi la caméra
ID hors écran et invalide son reçu. Les sources 025 corrigent ce filtre en
réutilisant `OffscreenSceneCamera`, avec un test qui conserve le refus de deux
caméras de scène ordinaires. La version 026 passe neuf tests ciblés et Clippy
natif/Web ; la sélection réelle du candidat 009 réussit ensuite. La retouche
révèle un second refus de projection : un quaternion auteur f32 valide,
promu sans normalisation, dépasse la tolérance stricte f64 du streaming.
Les sources 029 normalisent uniquement cette copie dérivée et conservent
la tolérance, les données canoniques et les gardes. Le test de régression
échoue avant correction puis passe ; 314 tests monde et Clippy strict passent.
Le candidat 010 vérifie ensuite l'affichage, annuler/rétablir et réouverture
de cette retouche (`studio-nature-assessment-006.json`), avec un refus shader
intermittent conservé et une qualification finale de sources encore due.

## Matériaux végétaux monde — 28 octobre

La famille auxiliaire `WorldPlant` dérive maintenant le shader réellement
utilisé par le Studio courant et les modules, en complément du matériau
capturé historique. Elle conserve les fonctions de déformation et de surface,
les masques de rejet et le vertex monde ; seule l'entrée de beauté est remplacée
par la reprojection statique des plantes acquises. Aucun varying supplémentaire
n'est ajouté. L'import PBR inutilisé est retiré de cette dérivée afin de réserver
le groupe 1 au seul paquet temporel. Les variantes de prépass restent conservées.
Le registre du mode source du Studio ajoute le matériau monde réel.

`shared-world-plant-temporal-validation-001.json` qualifie les sources 062 :
Clippy strict natif/Web, 91 tests temporels courants et 26 frontières passent.
Le test explicite compose le shader du module immuable contre les bibliothèques
Bevy installées : quatre familles végétales, deux modes d'éclairage et deux
étages, soit 16 modules Naga validés. Il vérifie les fonctions source conservées,
l'exclusivité du groupe 1 et le refus de sources déjà dérivées ou d'un nouveau
rejet dans l'entrée de beauté. Les deux premiers appels Clippy, refusés pour
l'API du cache de test puis un point-virgule, sont conservés ; les 2 538 entrées
restent inchangées pendant les validations finales.

Ce résultat est une validation de composition CPU et d'enregistrement Studio.
L'activation dans le jeu de modules, la couverture auxiliaire réelle sur GPU,
les historiques et l'ordre complet AO/brume/sous-marin/resolve/final restent
à qualifier. La brume volumétrique consomme encore les sorties du milieu et
des cartes optiques du même frame ; leur reprise commune reste nécessaire.

Les vues sélectionnées préparent ensuite leurs usages de profondeur au même
endroit que leurs paquets temporels : `COPY_SRC | TEXTURE_BINDING`, avec les
usages précédents conservés. Le test 066 couvre aussi les vues non sélectionnées
et les captures qui doivent rester intactes. Les 92 tests courants, Clippy
natif/Web et les 26 frontières passent. L'audit natif ajouté utilise le passage
auxiliaire existant sans rendu pipeliné, sans masquer le contrat de coque
incomplet ni activer le resolve. L'encodage de géométrie, les lectures GPU et
la completion complète restent des verdicts distincts.

Les audits natifs 001 à 003 identifient ensuite les matériaux de modèle source
non enregistrés, puis trois racines de gameplay dont le Mesh3d a été retiré
sans retirer sa classe de visibilité. L'enregistrement explicite du modèle
réutilise le shader auxiliaire rigide commun et les gardes de sa clé de matériau ;
il ne crée pas un autre renderer. Le retrait commun d'un Mesh3d nettoie seulement
sa propre classe, en conservant les autres classes et composants. Les régressions
072 couvrent le remplacement gameplay et le retrait/rétablissement de maillage ;
les tests végétation sont rejoués pour les autres consommateurs. Le rejeu natif
072 passe huit encodages de géométrie avec 18 981 commandes, sans entrée en
attente ni non reconnue. Il conserve les historiques à zéro et le refus de
coque requis ; il ne qualifie pas le resolve ou les lectures GPU. Les gardes de couverture et le contrat de coque restent inchangés.


## Requêtes de hauteur et cartes photoniques communes — 28 octobre

Les propriétaires `source_water_queries` et `source_caustics` reprennent les
requêtes bornées et les cartes photoniques déjà utilisées. Leurs quatorze
consommateurs utilisent directement le moteur, sans réexport dans la crate de
carte. La constante du shader de requête devient publique pour l'adapter de
surface qui prépare le même matériau optique ; le protocole et ses refus sont
conservés. Les chemins de compilation et libellés diagnostiques sont adaptés,
sans modification des UUID, bindings ou du WGSL photonique.

`shared-optical-primitives-transfer-001.json` vérifie les quatre fichiers
(40 172 octets d'origine) contre leurs empreintes 072 : inverser uniquement les
transformations déclarées restitue les SHA-256 originaux. Aucune copie des
anciens modules n'est conservée. Les 27 frontières et Clippy strict natif passent
sur 073, ainsi que les neuf tests transférés et Clippy strict Web, sans dérive
des entrées (`shared-optical-primitives-validation-001.json`). Cette extraction
ne qualifie pas les cartes en jeu ni la chaîne brume/sous-marin/resolve.


## Brume, milieu sous-marin et éclairage optique communs — 28 octobre

Les 24 fichiers des passages aériens, sous-marins et de leurs cartes d'éclairage
(184 284 octets d'origine) rejoignent `source_air_haze`, `source_underwater` et
`source_underwater_lighting`. Les consommateurs Studio et référence utilisent
ces propriétaires directement, sans façade dans la crate de carte. Les UUID,
protocole de requête, comptages d'encodage et refus restent conservés. Les noms
de modules, imports WGSL, options diagnostiques et définition HDR utilisent le
préfixe source ; les mentions de provenance et droits restent intactes.

`source_water_schedule` possède les identités FFT/rivage utilisées par les
producteurs et les consommateurs optiques. Les sondes qui gardent leur propre
identité FFT publient aussi le marqueur commun ; leur calcul de rivage attend
celui-ci. Il s'agit d'un raccord réel de l'ordre des calculs, à qualifier sur GPU,
pas d'un acquittement ajouté pour faire passer le contrat de frame.

`shared-optical-chain-transfer-001.json` épingle les sources 073 avant transfert
et les transformations déclarées. Le contrôle indépendant
`shared-optical-chain-shader-equivalence-001.json` inverse les seuls imports et
la définition HDR dans les onze WGSL : les empreintes originales sont toutes
retrouvées. Aucune copie d'ancien module n'est conservée. Les premiers refus
Clippy concernent un commentaire de déclaration déplacée puis deux imports
groupés restés dans la crate de carte ; ils sont corrigés sans façade de
compatibilité. Clippy strict natif de toutes les cibles moteur, Studio et carte
passe sur 076. Les 18 tests optiques transférés, 92 tests temporels courants,
Clippy Web, le format et les 28 frontières passent sur 2 541 entrées inchangées
(`shared-optical-chain-validation-001.json`). Le candidat 017/076 retrouve les
huit encodages et 18 981 commandes du module, sans attente ni type non reconnu,
avec refus de coque et historiques à zéro conservés. Le rejeu pipeliné de la
chaîne sur la scène de référence refuse avant encodage valide : l'essai n'avait
pas activé la chaîne statique HDR existante et le matériau de poisson utilise
maintenant l'horloge commune, absente de l'ancienne reconnaissance auxiliaire.
Le lanceur est corrigé et la reconnaissance 077 accepte uniquement le binding
entier, le bitcast du temps et la sélection exacte de frame observés, en
conservant le préfixe de surface et de masque. Les 94 tests temporels courants,
deux rejeux explicites du matériau et du shader réels (quatre modules Naga),
Clippy natif/Web et les entrées 077 inchangées passent. La référence 002 n'a plus
de refus de dérivation et encode les sept rôles HDR avec le même numéro de frame
sur 3 855 observations. Deux maillages StandardMaterial skinnés/morphés restent
hors du contrat rigide ; le renderer conserve zéro commit et termine en refus.
Les fonctions et buffers de skinning courant/précédent du renderer existant
sont à reprendre avec les règles de remise à zéro de l'historique source ;
ni une géométrie rigide substituée ni une exclusion de ces maillages ne prouvent
ce contrat. La vue aérienne et les commandes ne qualifient pas les textures,
l'immersion, la parité visuelle ou les performances. Aucun verdict de chaîne
complète ou de clôture ne découle du transfert seul.

## Historique auxiliaire des maillages animés — 28 octobre

La passe auxiliaire utilise les matrices articulaires courantes et précédentes
du renderer Bevy pour les matériaux opaques `StandardMaterial` et
`ModelMaterial` enregistrés. La disposition de la caméra reste celle de la vue
effective ; seule la disposition du maillage est étendue aux matrices précédentes.
Une allocation inchangée, deux frames Render consécutives, l'absence de changement
de visibilité, de skin et de bindposes, ainsi que la capacité des deux buffers
sont requises. Sinon, la couverture attend et l'historique est réinitialisé.
Le propriétaire temporel Render est obligatoire pour ces maillages ; les morph
targets et contrats non reconnus restent refusés.

Sur les sources 089 inchangées, 97 tests temporels, huit compositions du shader
réel, Clippy strict de l'exemple natif et Clippy Web passent. Le rejeu de référence 006 utilise la
scène et son personnage animé : la couverture observée atteint 19 081 maillages
sans attente ni contrat inconnu, et l'encodage reprend après trois frames Render
sans caméra. Le précédent rejeu 004 avait mis en évidence l'arrêt des adaptateurs
de l'étal et des poissons pendant cette absence ; ils attendent désormais la
réactivation tout en refusant les vues ambiguës. Le détail et les empreintes sont
dans le suivi Gate 7. Ce résultat ne qualifie ni les vitesses par sommet lues sur
GPU, ni les morph targets, ni la parité visuelle ou les performances ; le raccord
général aux modules et la qualification de distribution restent ouverts.

## Raccord temporel du masque de coque — 28 octobre

Le moteur peut raccorder le masque de distance déjà encodé pour la scène à la
passe auxiliaire, sans deuxième dessin. Un propriétaire de scène doit fournir
un inventaire complet indépendant des volumes enregistrés ; le moteur le vérifie
à chaque synchronisation, refuse les doublons, les omissions et les volumes non
déclarés, puis lie le résultat à la vue, la frame, la révision, la génération,
la taille et au nombre de volumes effectivement encodés. Une liste vide ne
vaut pas attestation d'absence de coque. Cette API reste inactive si la scène ne
fournit pas ce contrat ; l'inventaire du seul `HullVolume` ne suffit pas.

Les sources 094 passent onze tests du masque, 97 tests temporels, huit variantes
du shader de skinning, Clippy strict de l'exemple ainsi que Clippy Web. Clippy
strict du moteur passe sur 092, dont les sources moteur sont identiques à 094.
Sur la scène de référence, une coque synthétique isolée et explicitement
identifiée produit un dessin de masque sur les frames validées avant et après
mouvement, reset, redimensionnement et interruption de caméra ; les empreintes
contrôlées restent stables. Le diagnostic module 009 conserve huit encodages de
18 981 maillages et le refus de coque sans inventaire, avec historiques à zéro.
Ces preuves portent sur l'encodage des commandes : le bateau réel, l'exclusion
visuelle de l'eau, les lectures GPU, le coût de l'inventaire et le raccord complet
aux scènes de modules restent à qualifier.

Le rejeu 009 sur les sources 096 remplace le volume synthétique par la géométrie
du bateau source, sous matériaux de diagnostic : le propriétaire vérifie sa
résidence et fournit ses exclusions d'eau. Les 154 fichiers du paquet et le
manifeste gardent leurs empreintes. Les 32 phases et la commande de coque sont
observées avec 19 093 maillages, avant et après mouvement, reset, redimensionnement
et interruption de caméra. Le rejet d'une frame au redimensionnement dans le
rejeu 008 a précédé une reprise avec reset ; la sonde 096 vérifie désormais ce
cas sans considérer la frame refusée comme un commit. Sur le rejeu 009, la frame
de redimensionnement a directement réamorcé l'historique. Le contrôle module 010
continue à refuser la coque et à bloquer l'historique sans intégration. Ni le
matériau source du bateau, ni l'exclusion visible de l'eau, ni une lecture GPU,
ni la performance ou un module utilisateur ne sont qualifiés par cet essai.

## Profondeur opaque et caméra de contact communes — 28 octobre

La copie de profondeur opaque, les métadonnées de caméra et leur validation de
contact sont déplacées vers `forge_engine::source_scene_depth`. Les consommateurs
Tidewater, Studio et sonde utilisent ce propriétaire directement ; aucune copie
ni façade dans la crate de carte ne subsiste. Le rapprochement indépendant
`shared-scene-depth-equivalence-001.json` retrouve exactement les trois hashes
initiaux après inversion des changements de chemins, visibilité et ordre
d'imports. Le garde ajouté ensuite rend l'installation de la caméra idempotente
et refuse l'absence de renderer ; il est couvert par un test dédié.

Sur les 2 541 entrées 099 inchangées, les six tests de profondeur, huit tests
de l'étal (un test de fixture GPU ignoré), onze de coque, 97 temporels, huit
variantes du shader et le test de reprise passent ; Clippy strict natif et Web
passe, ainsi que les 28 contrôles de frontières. Le rejeu de référence 010
encode la coque du bateau source et les contacts de l'étal avec la même identité
de caméra et l'historique correspondant sur les frames valides. Il observe aussi
une frame refusée au redimensionnement suivie d'un reset et d'un réamorçage.
L'audit module 011 retrouve huit encodages et 18 981 maillages, avec refus de
coque maintenu. Le rejeu atteste l'encodage des commandes, pas une lecture GPU,
la parité visuelle, la performance ou le raccord complet aux modules.

Le producteur GPU des contacts et du temps de surface rejoint à son tour
`source_scene_depth::contact_gpu`, directement utilisé par le Studio, le
matériau et la sonde. L'identité du buffer et le calcul ne changent pas :
`shared-contact-publisher-equivalence-001.json` retrouve l'empreinte 100 après
inversion des seuls chemins, visibilité et ordre d'imports. Sur les sources 101
inchangées, les huit tests du propriétaire commun, six tests de l'étal (un GPU
explicite ignoré), les contrôles coque/temporel, Clippy strict natif/Web et 28
frontières passent. Le rejeu de référence 011 retrouve les contacts liés à la
caméra dans 51 observations valides, avec la géométrie source du bateau et une
reprise après refus au redimensionnement ; le paquet et le binaire sont inchangés.
L'audit module 012 conserve huit encodages de 18 981 maillages et refuse toujours
le contrat de coque absent. Ces résultats ne qualifient pas les pixels, les
performances ni la chaîne complète des modules.

## Simulation FFT d'océan commune — 28 octobre

Le calcul FFT résident et son horloge sont transférés dans
`forge_engine::source_ocean`. Tidewater, le Studio et les sondes utilisent ce
propriétaire ; les shaders, les buffers et le séquencement restent les mêmes.
Le contrat `forge_assets::source_ocean::OceanInputSource` reçoit les données
admises par leur lecteur : le lecteur Tidewater conserve ses contrôles de
provenance, d'empreintes et de fermeture des fichiers. Le moteur vérifie en plus
les dimensions et limites de l'ABI avant allocation, sans prétendre vérifier
les droits ou l'authenticité d'un autre lecteur. Les entrées historiques à 120
étapes et les acquisitions explicites de 1 à 256 étapes restent admises sous
leurs conditions respectives. Les libellés diagnostiques deviennent génériques.

Avant les gardes d'entrée supplémentaires, la comparaison indépendante
`shared-ocean-transfer-equivalence-001.json` retrouve le hash initial du calcul
après inversion des changements déclarés ; l'horloge est identique octet pour
octet. Sur les sources 106 inchangées, 15 tests moteur et quatre tests courants
du lecteur passent ; un test historique requiert un autre paquet et reste ignoré.
Clippy strict natif/Web et 29 contrôles de frontière passent. Sur la même machine
et le paquet source vérifié à 124 étapes, les lectures GPU avant/après du calcul
résident donnent 72 textures et trois buffers identiques, 15 029 568 octets par
exécution, sans composante non finie (`shared-ocean-gpu-comparison-002.json`).
L'audit module 013 conserve huit encodages de 18 981 maillages et son refus de
coque. La comparaison GPU ne qualifie pas la surface d'eau complète, la parité
visuelle, la performance ou l'intégration des données d'océan dans les modules.

## Géométrie d'océan commune — 28 octobre

La géométrie CDLOD du matériau optique rejoint `forge_engine::source_ocean_geometry`.
Le Studio transmet le shader et les paramètres admis par le lecteur de surface
existant, qui garde les vérifications de provenance et d'empreintes. Les limites
du shader et des corrections restent explicites : raster 2 048 carré, origine
(-1 024, -1 024), espacement 1 m et hauteur extérieure -90 m. Le moteur refuse
les domaines incompatibles au lieu de rendre une surface mal alignée.

Sur les sources 109 inchangées, le test de ces limites, Clippy strict natif et Web
et les 29 contrôles de frontière passent. Les six tests de stockage mutualisé et
les deux tests de paquets d'effets passent aussi sur ce même état. Ces résultats
ne qualifient pas le rendu complet, la performance, la migration de stockage
encore en cours ni l'utilisation de l'océan par d'autres modules.

Sur les sources 111, les préparations de ressac et d'optique exigent la même
révision de terrain que le plan raster utilisé pour calculer les dérivés. Le
registre de requêtes d'eau refuse une réutilisation de nom avec une taille de
plage différente, afin d'éviter un chevauchement entre consommateurs. Douze tests
de terrain éditable, cinq de requêtes d'eau, Clippy natif/Web et 29 frontières
passent sur ces entrées inchangées ; aucun rendu ou module n'en est déduit.

Le lecteur d'entrées FFT rejoint ensuite `forge_assets::source_ocean_package`,
directement consommé par Studio et les sondes. Les noms, formats et contrôles
d'admission du paquet acquis sont conservés ; l'index peut être contrôlé avant
le transport des ressources. Sur les sources 112, cinq tests passent, un test
historique reste ignoré, Clippy natif/Web et 29 frontières passent. La sonde
du calcul résident retrouve 75 sorties GPU identiques à la référence 106 sur
le même paquet à 124 étapes. Cela ne qualifie ni le rendu d'eau complet ni le
transport et l'activation dans les modules.

## Champ de rivage et identité du terrain — 28 octobre

Le calcul de propagation source est déplacé octet pour octet vers
`forge_engine::source_shore_field` (`shared-shore-transfer-equivalence-001.json`).
Le ressac conserve le champ capturé uniquement pour le raster source identique et
sans retouche ; sinon il calcule ce champ sur le plan de terrain vérifié. Le
matériau optique consomme ce même champ, lié à la révision de terrain et au hash
du manifeste complet de surface. Sans simulation, il ne réutilise le champ
capturé que si un paquet de ressac admis atteste le raster, le champ et les
paramètres correspondants ; sinon il recalcule. Le domaine de shader reste le
carré centré de 2 048 m à pas de 1 m : aucune autre grille n'est revendiquée.

L'essai privé `shared-shore-private-tests-005.log` utilise les paquets réels
épinglés : raster de référence, second raster, retouche, absence de référence,
refus d'un champ périmé et refus d'une surface différente avec même champ source.
La sonde native `shared-shore-resident-assessment-003.json` lit le ressac
résident au pas 124 sur le terrain source et sur une côte synthétique ; les deux
textures RGBA16 contiennent des valeurs finies, sont non nulles et diffèrent.
Elles sont identiques aux deux lectures du précédent rejeu après le renforcement
des identités. Cet essai ne prouve pas des pixels visibles de l'eau, une parité
visuelle ou de performance, ni le rendu dans un module. Les sources 133 ne
diffèrent des sources 132 du rejeu GPU que par l'ordre d'un import Studio ;
Clippy natif/Web, format et 29 frontières passent sur les sources 133 stables.

La simulation résidente de ressac rejoint ensuite
`forge_engine::source_shore_sim`. Studio, le matériau de la carte et les sondes
utilisent ce même composant ; le champ expose sa révision de terrain et son
identité de surface par des accesseurs en lecture seule. Le shader d'arithmétique
est déplacé vers le moteur sans changement d'octets, SHA-256
`ddc9dba7ec42131730129d088dbd585c2fcf9294c21d57e01431a2098cb1c432` ;
les sondes et l'outil de préparation emploient ce fichier unique. Le manifeste
des sources 143 l'inclut à son nouvel emplacement. Clippy strict natif et Web et
les 29 frontières passent. La compilation Web conserve l'avertissement connu
d'incompatibilité future de `proc-macro-error2`. Ce transfert ne raccorde pas
encore l'optique, la réfraction ni le rendu des modules.

L'essai privé `shared-shore-engine-private-tests-001.log` passe sur les paquets
réels, y compris pour un raster distinct et les contrôles de révision. La sonde
résidente reconstruite produit sur les deux terrains les mêmes textures que
les lectures 003, octet pour octet (`shared-shore-resident-assessment-005.json`).
Les sources 143 restent inchangées jusqu'à l'inventaire 144. Les deux nouvelles
copies redondantes, 9 437 184 octets, sont retirées après contrôle de leur
identité ; les lectures 003, de même volume, restent comme référence
(`shared-shore-repeat-retirement-005.json`). Aucune parité visuelle, performance
ou qualification de module n'est déduite de cette lecture de simulation.

## Optique, réfraction et requêtes de surface — 28 octobre

Le matériau optique, la capture de réfraction et le producteur de requêtes GPU
rejoignent ensemble `forge_engine` ; Studio et les sondes appellent ces mêmes
composants. Les contrôles d'identité terrain/surface restent dans la préparation
existante. La simulation de ressac et son champ demeurent les mêmes objets moteur.
Le calcul des positions de gameplay est fourni par des callbacks nommés, exécutés
après la position caméra et avant la capture du paquet. Un nom vide ou déjà
utilisé est refusé ; une erreur de position interrompt la publication du paquet
de cette trame. Le joueur amphibie fournit sa position par ce mécanisme sans
introduire de dépendance du moteur vers la crate de la carte.

Sur les sources 149 vérifiées inchangées jusqu'à 151, Clippy strict natif/Web,
le format des 26 fichiers concernés et 29 frontières passent. Le test du paquet
de requêtes vérifie l'ordre réel et l'absence de nouvelle publication après un
point non fini ; les 36 tests de locomotion amphibie passent, de même que la
préparation du ressac et de l'optique sur les paquets réels pour deux terrains.
Les six tests ciblés du stockage mutualisé et les deux tests d'effets passent ;
ils ne qualifient pas la migration de stockage parallèle. Le premier Clippy du
lot a refusé des assertions du nouveau test ; il est conservé comme échec et le
rejeu corrigé passe. Le Web conserve l'avertissement connu d'incompatibilité
future de `proc-macro-error2`.

Aucune lecture GPU de l'optique ni de la réfraction n'a été rejouée sur ce
transfert. Le parcours de module refuse toujours l'océan jusqu'au raccordement
de ses ressources et à la qualification de la chaîne complète ; ni la parité
visuelle, ni les performances, ni Gate 7 ne sont déduites de ces tests.

## Préparation commune de l'eau des modules — 28 octobre

`source_water_host::PreparedHost` prépare la FFT, la géométrie, le ressac et
l'optique depuis les paquets fournis, le document et son plan raster vérifié.
Son installation exige les hôtes de terrain et d'environnement de la même
révision ; elle compose les producteurs communs et attend leur disponibilité et
une taille de vue valide avant de mettre en place le matériau. Son indicateur
`staged` décrit une publication CPU, pas une preuve de complétion GPU. Le jeu de
module possède l'adaptateur qui résout les ressources admises et appelle cet
hôte, mais le garde de lancement reste actif.

Le ressac résident porte désormais les identités de terrain et de surface. Le
terrain lie ses textures uniquement à la révision correspondante, refuse une
ancienne révision et conserve les textures neutres en l'absence de ressac.
L'hôte d'environnement transmet aussi les publications solaires à l'optique
résidente, comme le fait le parcours Studio existant.

Sur les sources 156, les cinq tests d'hôte terrain passent, dont ces contrôles
de liaison. Sur les sources 158 vérifiées jusqu'à 159, deux essais privés avec
les trois paquets réels passent : préparation complète, refus d'un plan de
terrain ancien et refus d'installation sans ses hôtes. Les 38 tests du jeu de
module, six du stockage mutualisé et deux des effets passent. Clippy strict
natif passe sur les sources 157, puis le test ajouté passe aussi Clippy ciblé ;
Clippy Web passe sur 158 avec l'avertissement connu `proc-macro-error2`.
Les essais stricts précédents ont refusé une signature complexe et une fonction
trop longue ; leurs reçus restent des échecs, corrigés par les rejeux. Les
sources 160 ne changent que le format d'une annotation et restent stables à 161.

Il reste à éprouver l'installation depuis un module réellement installé et le
rendu GPU de cette chaîne, connecter la locomotion et une barrière de complétion
appropriée avant de lever le garde, puis qualifier Studio, images et performances.
La préparation et `staged` ne constituent ni un résultat visuel ni une clôture.

## Inventaire de l'eau et vues dérivées — 28 octobre

Le contrôle graphique des modules reconnaît l'optique uniquement si l'hôte
possède encore la projection et le ressac de la bonne révision. Un état CPU
`staged` seul ne peut pas lever la barrière. Le matériau optique déclare ses
23 dépendances images/buffers à l'inventaire typé ; la famille d'eau dispose de
son propre reçu GPU. La capacité bornée passe de six à sept propriétaires pour
accueillir cette cinquième famille d'affichage avec le ciel et l'herbe existants.
Les changements de ressources continuent à révoquer les anciens reçus.

La sélection graphique ignore les caméras hors écran et exclut de l'inventaire
principal uniquement les miroirs de réfraction effectivement marqués par leur
hôte et encore placés sur sa couche de capture. Le contrôle est ordonné après
la synchronisation de ces miroirs et la mise à jour de l'eau.

Sur les sources 163, Clippy strict natif et Web et la suite moteur passent :
1 120 tests réussis, sept ignorés. Les nouveaux tests vérifient les 23 dépendances,
la capacité d'inventaire et l'identité des miroirs. Les sources 165 ne changent
que deux fichiers du contrôle graphique ; Clippy ciblé et les 40 tests de module
passent, notamment caméra hors écran, eau sans propriétaire et projection
manquante malgré un indicateur CPU. Les sources restent inchangées jusqu'à 166.
Le premier Clippy ciblé refusait la longueur d'une fonction ; le rejeu après
extraction du contrôle d'eau passe. Les six tests du stockage mutualisé et les
deux tests d'effets passent, sans qualifier leur migration parallèle.

Ces essais ne prouvent pas encore la capture de réfraction ni le rendu de l'eau
sur GPU dans un module installé. Il faut aussi vérifier le coût des changements
de géométrie d'eau et le maintien de la locomotion ; le garde reste actif.
Le diagnostic temporel de module exclut ensuite lui aussi les caméras hors
écran. Les sources 167 ne changent que ce fichier depuis 165 ; Clippy ciblé et
les 40 tests de module passent, avec sources stables jusqu'à 168.

### Essai de composition en module installé — 28 octobre

L'hôte d'eau publie ses programmes de requête et d'éclairage sous-marin avant
l'attente de l'environnement, afin de ne pas laisser des pipelines attendre
des shaders publiés seulement après cette même attente. La préparation des
ressources et le garde du lancement ordinaire restent inchangés. Un scénario
réservé à la qualification, sur un module explicitement épinglé, a obtenu un
reçu GPU courant pour les ressources d'eau ; il échoue encore sur la validation
CPU graphique avant le démarrage de la physique. La capture complète, la
locomotion et les performances restent à prouver. Références et limites :
`docs/evidence/gate7/GATE7_PROGRESS_20261023.md`, campagne
`module-water-runtime-native-001/` et `module-water-runtime-native-002/`.

Le système commun de qualité des ombres exclut désormais la caméra de capture
hors écran de la sélection de la caméra principale, tout en refusant plusieurs
caméras principales. Sur le module épinglé, le rejeu `module-water-runtime-native-003/`
atteint la physique et exécute le scénario ; ses captures ne montrent pas l'océan
et ne qualifient pas le rendu visuel, la réfraction ou la locomotion aquatique.

Le calcul indépendant de locomotion amphibie et son profil sont désormais dans
`forge_engine::amphibious_locomotion`, réexportés pour Tidewater sans copie
d'implémentation. Leur transfert est identique octet pour octet et neuf tests
passent. Le contrôleur Rapier et l'intégration module restent à transférer et
qualifier avec de véritables lectures de surface.

Le contrôleur Rapier amphibie est partagé dans `forge_engine` et réexporté par
Tidewater. Ses régressions de carte conservent leurs fixtures sur place et
compilent la même source moteur dans un harnais de test. Les modules possédant
une eau explicitement admise installent ce contrôleur et attendent un échantillon
GPU initial associé à la position du joueur ; cette observation ne modifie pas
la simulation tant qu'elle est figée. Les 38 tests ciblés passent après
correction. La nage réelle en module reste à vérifier.

L'hôte d'eau sélectionne la caméra principale pour le passage sous-marin,
préserve les usages de profondeur existants et ajoute ceux nécessaires à cette
lecture ; il écarte les caméras hors écran et refuse une sélection étrangère.
Le scénario installé `module-water-runtime-swim-003/` observe une immersion,
une entrée en nage et le retour à la marche. L'encodage du passage n'est pas
une preuve de complétion GPU ou de fidélité des pixels ; voir le suivi Gate 7.
