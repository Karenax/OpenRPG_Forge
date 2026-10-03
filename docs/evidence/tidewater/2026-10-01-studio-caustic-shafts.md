# Tidewater — rayons de caustiques dans Studio

Suite de [l’immersion Studio](2026-10-01-studio-immersion.md), dans la chaîne
issue du [rapport d’exposition demandé](2026-09-30-studio-exposure-sweep.md).
Référence figée : `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`,
`src/post/Underwater.js` et `src/ocean/Caustics.js` ; attribution MIT conservée.
L’objectif complet, la comparaison source synchronisée et TW-008/TW-012 restent
ouverts. Ce rapport distingue construction, exécution et parité.

## Raccordement

Studio installe les cartes de photons déjà présentes dans le moteur : fine
512² et large 256², deux plans focaux R/G par carte et leurs mipmaps. Elles
partagent les dérivées du véritable océan FFT. Leurs quatre projections sont
ordonnées après `OceanComputeSet` et avant le pilote des caméras. Le compteur
`encoded_fft_step` indique l’étape encodée ; il n’est pas une fence GPU.

La passe de rayons travaille en `Rgba16Float` à `ceil(largeur/2)` ×
`ceil(hauteur/2)`, avec 20 pas sur au plus 22 m. Elle utilise la réfraction
solaire, les deux foyers fine/large au mip 1,5, la variation rafale/nappe du
module optique complet, l’extinction sur les trajets lumineux et de vue, ainsi
que la phase source (g = 0,85, mélange 75/25). La composition reconstruit les
rayons avec les quatre taps pondérés par profondeur de la référence. Alpha
stocke la distance de vue ; elle ne porte pas un masque de substitution.

Le shader réutilise le matériau optique partagé et son paquet de hauteur GPU.
Le bruit de marche utilise l’index de ce frame optique, actuellement l’étape
FFT dans Forge ; la suspension conserve ainsi exactement cette entrée. Il
n’y a pas de TAAU source dans ce raccordement. Le slot de lecture du shader
de rayons reçoit une texture neutre distincte de sa cible writable : aucune
alias lecture/écriture de la cible. Les bind groups et la cible restent
résidents ; la cible se recrée lors d’un changement de résolution.

« Rayons sous-marins » règle l’intensité 0–2, valeur initiale 1. Les préférences
Studio locales v1 utilisent un défaut serde pour les anciens fichiers ; aucun
champ de zone canonique n’est ajouté. À 0, hors de l’eau ou si le milieu est
désactivé, le march ne s’encode pas et la composition ne lit pas une ancienne
cible. La promenade native réutilise les mêmes cartes et propose le même
réglage ; son contrôle de compilation est séparé de sa qualification visuelle.

## Construction et contrôles

Neuf tests ciblés moteur passent sur le code final, dont validation Naga des
deux entry points, du lookup de photons et de la reconstruction par profondeur.
[Journal](../../../../.tidewater-integration/studio-shafts-engine-tests-final-141.log).
Un premier build natif a révélé un conflit d’emprunt dans le cache ; le correctif
est conservé dans le journal de réparation. Le découpage suivant de la passe a
révélé deux références de bind group à déréférencer ; le build final les corrige.
Ces échecs ne sont pas comptés comme preuves positives.

Le check de la promenade native passe.
[Journal natif](../../../../.tidewater-integration/studio-shafts-native-check-141.log).
La construction WASM avec `--no-default-features --features web --release
--locked --offline` passe en 7 min 46 s ; le packaging Tauri passe.
[Construction WASM](../../../../.tidewater-integration/studio-shafts-build-141.log),
[reçu du candidat](../../../../.tidewater-integration/studio-tidewater-shafts-candidate-141/build-proof.json),
[hashes des entrées](../../../../.tidewater-integration/studio-shafts-inputs-141.json).
Executable candidat :
`20980ee44e472c56c2722a947bf59658f3bb96b0eafbed26d964793feec21c96`.
Frontend WASM :
`c093c01be73ba492a5ec96f39bc4b52315d2fdaa933564708eb689877a886ef4`.
`BUILD_ONLY` ne prouve aucune exécution.

Clippy moteur strict conserve les **64 erreurs préexistantes**, sans erreur
dans les fichiers de ce raccordement après le découpage de la passe.
[Journal Clippy moteur](../../../../.tidewater-integration/studio-shafts-engine-clippy-final-141.log).
Clippy Studio conserve ses **31 erreurs préexistantes**, sans diagnostic dans
le raccordement.
[Journal Clippy Studio](../../../../.tidewater-integration/studio-shafts-studio-clippy-141.log).
Le formatage global rencontre l’exemple `terrain_ao.rs` absent ; le contrôle
des fichiers concernés est réalisé séparément. Aucun verdict global vert,
aucune gate ni CI distante n’est déclaré.

## Exécution et limites

Le parcours WebGPU 142 passe édition/relecture/playtest, persistance et migration
des préférences, trois triplets d’entrées figées, plongée/remontée, retour plage
et édition. Les préférences sont restaurées, les ticks et l’océan reprennent.
Le milieu reste actif pendant toutes les images ; seul le curseur des rayons
change de 1 à 0 puis à 1. Les signatures comprennent caméra, projection,
joueur, deux requêtes GPU, horloge, vent et étape des cartes de photons.
Chaque carte et chaque paquet correspond à l’étape océan drainée.

Le contrôle indépendant des pixels 142 **échoue** : à la surface, la caméra
reste légèrement au-dessus de l’eau et l’écart RGB moyen n’est que 0,016/255.
La vue immergée horizontale donne 0,098/255 ; ces deux vues n’atteignent pas le
seuil de 0,1. Les répétitions retrouvent exactement les mêmes PNG. Ces résultats
restent conservés, sans abaissement du seuil ni amplification du shader.
[Échec des pixels 142](../../../../.tidewater-integration/studio-tidewater-shafts-runtime-142/shafts-verification.json).

La série 143 utilise le même candidat et tourne la caméra avec de vrais
événements souris CDP vers le soleil réfracté de `water-frame.bin`, sans déplacer
artificiellement le joueur. La direction est calculée depuis les octets source
puis vérifiée dans la matrice de caméra ; la visée d’origine est restaurée avant
le retour plage. Ce contrôle cible les rayons solaires, donc séparément la vue
immergée orientée vers le soleil et l’absence d’effet depuis la plage. Il ne
requalifie pas la vue de surface refusée dans 142.
[Reçu de la visée](../../../../.tidewater-integration/studio-tidewater-shafts-runtime-143/shaft-camera-aim.json).

| Vue 143, 1920 × 1080 | Écart RGB moyen 1/0, sur 255 | Pixels avec un canal différent de plus de 2 | Écart 1/répétition |
|---|---:|---:|---:|
| Air | 0,000 | 0 % | 0,000 |
| Immergée vers le soleil | 0,243 | 3,768 % | 0,000 |

Le contrôle indépendant 143 passe avec le seuil inchangé. L’effet reste subtil
à sa valeur source 1 ; les différences dessinent les rayons autour de la zone
solaire, pas une hausse uniforme de l’exposition. Les PNG activé, désactivé et
répété ont été inspectés. Le PNG actif et sa répétition ont le SHA-256
`40ef12cdad7adc012bdf6ce19ad13bd97b0138e00c642dc51c8d6408680b146f`.
La caméra est à 1,934 m sous la hauteur locale ; l’étape figée est 6537.
[Vérification 143](../../../../.tidewater-integration/studio-tidewater-shafts-runtime-143/shafts-sun-verification.json),
[triplet solaire](../../../../.tidewater-integration/studio-tidewater-shafts-runtime-143/shafts-sunward-pair.json),
[parcours et sources](../../../../.tidewater-integration/studio-tidewater-shafts-runtime-143/fish-stand-studio-smoke-ledger.json).

Les deux parcours atteignent ensuite le clamp de nage près du fond : ils ne
remplacent pas la preuve de neutralité libre 139. Aucune image source synchronisée
n’est comparée dans ces séries ; le verdict acquis est celui du raccordement
visible WebGPU à entrées identiques. La qualification native des pixels de
rayons seuls reste distincte.

### Régression native

Le binaire natif est construit et exécuté avec les nouvelles cartes et le shader
de rayons. La série 144 **échoue** avant la plongée complète : après drainage de
l’océan, la caméra est à −2,297 cm de la surface, hors de la tolérance stricte
de 2 cm. La paire Float avait des signatures et PNG identiques ; l’échec de
préparation Dive est conservé.
[Reçu 144](../../../../.tidewater-integration/native-caustic-shafts-144/probe.walk.json).

Le protocole natif peut désormais enregistrer une suspension Dive refusée,
reprendre puis rejoindre la bande avec les commandes source de nage avant de
réessayer, au plus 40 fois. Le seuil, les signatures et les contrôles de pixels
ne changent pas ; aucun déplacement direct du joueur ni changement du moteur
de nage. Le correctif porte seulement `swim_smoke.rs` ; les entrées du candidat
Studio 141 restent identiques. Le nouveau build natif 145 passe en 51,64 s.
[Entrées natives](../../../../.tidewater-integration/native-shafts-inputs-145.json).

La série 145 passe les sept étapes de marche/nage/plongée/remontée/retour :
3808 ticks, backend stable, neutralité libre sans dérive et sources intactes.
Les trois paires actives/contournées ont les mêmes signatures. Le gel Dive est
à +0,765 cm de la hauteur locale et la vue Neutral à −1,316 m. Ce run accepte
le premier gel Dive ; le chemin de rejet/reprise ajouté n’est donc pas exercé
par une reprise effectivement rejetée dans cette exécution.
[Reçu 145](../../../../.tidewater-integration/native-caustic-shafts-145/probe.walk.json),
[identité du binaire et fermeture](../../../../.tidewater-integration/native-caustic-shafts-145/run.json).
Le décodage indépendant donne un écart RGB moyen de 0 dans Float, 10,644/255
dans Dive et 16,691/255 dans Neutral. Les images ont été inspectées : vue mixte,
fond et piles immergés sont rendus sans erreur de pipeline. Ces paires désactivent
le milieu entier ; elles ne qualifient pas séparément les rayons natifs.
[Métriques natives](../../../../.tidewater-integration/native-caustic-shafts-145/immersion-pixel-metrics.json).

Le source auteur conserve le SHA-256
`50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720`.
Les processus d’essai et compilateurs possédés sont terminés. Les captures,
reçus et scripts restent locaux dans `.tidewater-integration`.

Ce lot porte les rayons de caustiques. Il ne porte pas la torche, les deux
cartes d’éclairage sous-marin centrées sur la caméra ni leur raccordement
complet aux receveurs immergés. Les caustiques du terrain natif restent le
premier raccordement décrit dans
[la preuve de photons](2026-09-28-caustic-photon-stage.md). Les autres
comportements, le package Aube et la parité de toute l’île restent ouverts.

Suite : [cartes d'éclairage sous-marin et raccordements](2026-10-01-underwater-light-maps.md),
avec les reçus GPU de centrage et la correction de caméra du probe natif.
Les limites ci-dessus décrivent le candidat de ce rapport ; la suite qualifie
séparément les nouvelles cartes.
