# Tidewater — passe de distance de coque

Reprise du [contrôle temporel Studio](2026-10-02-studio-temporal-controls.md),
depuis la [calibration d’exposition](2026-09-30-studio-exposure-sweep.md).
Le code ajoute la véritable entrée HULL du rendu de l’eau. Le bateau canonique
et le contrat d’inventaire de la scène éditable restent à raccorder ; le mode
temporel source de Studio conserve donc son refus explicite.

## Contrat de la référence

Le clone Tidewater est figé à
`1438b1abfcaee3267092b75573014f4d9b4a983c`. `SceneRenderer.js` rend le HULL
dans une texture **R16Float**, avec sa propre profondeur **Depth32Float**,
entre les opaques et l’eau. Le shader double face écrit la distance entre
la position monde du proxy et la caméra. La couleur est effacée à zéro,
la profondeur inversée à zéro.

La source enregistre explicitement le proxy du bateau principal. Les barques
du village et les débris ne deviennent pas des volumes HULL par leur nom ou
leur matériau. Un volume enregistré peut être inactif lorsque la caméra est
dans sa boîte locale augmentée de 0,05, ou lorsque sa sphère est hors champ.
Cette situation ne signifie pas que la scène est dépourvue de coques.

Le masque d’eau RGBA8, utilisé par le traitement temporel et l’immersion,
reste une sortie indépendante. Le shader eau élimine seulement ses faces
avant dont la distance caméra dépasse `hullDist - 0.02`, avec
`hullDist > 0.01`, lorsque le HULL de la frame courante est actif.

## Raccordement moteur

Le module `crates/forge_engine/src/tidewater_hull_mask.rs` fournit les cibles,
la géométrie explicitement enregistrée, la sélection de caméra et la passe
GPU dédiée. Il utilise l’uniforme de vue réellement préparé par Bevy et son
jitter, sans construire une caméra parallèle pour le dessin.

Le matériau optique possède les bindings 54 (distance) et 55 (activation).
Le même fragment `shadeWater` contient le discard HULL pour la beauté et
les sorties temporelles auxiliaires. Un seam absent, dupliqué ou déjà
modifié est refusé.

L’activation est remise à zéro dans RenderWorld avant toute recherche de
vue. Elle est publiée à un uniquement après l’encodage de tous les volumes
actifs et sans erreur. Une caméra supprimée, une allocation non prête ou
un changement de taille ne peuvent donc rendre un ancien masque accessible.
Cette publication atteste l’encodage CPU ; elle n’est pas une fence GPU.

`Encoding` distingue volumes enregistrés, actifs et réellement dessinés.
Il lie l’observation aux entités MainWorld/RenderWorld, au `FrameTag`, à la
taille et à la génération d’allocation. Un volume enregistré hors champ
peut réussir avec zéro dessin. Une liste vide ne fournit jamais une
assertion `NoHullsInScene`. L’adaptateur de scène reste responsable d’un
inventaire complet, distinct de cette observation du pass.

## Géométrie source préservée

La fixture 290 appelle la véritable fonction
`new BoatModel().createHullVolumeGeometry()` du clone figé, sous Node
22.22.3. Elle contient 952 positions f32 et 5 436 indices, soit 1 812
triangles. Après soudure exacte des positions, l’audit trouve **8 arêtes
ouvertes**, sans triangle dégénéré, arête non-manifold ou incohérence de
sens. Le proxy décrit comme fermé dans la source n’est donc pas entièrement
fermé au sens du contrôle topologique.

L’import de référence conserve ces octets et ces huit arêtes. Il n’ajoute
pas de triangles pour combler les trous. `Geometry::new` conserve son
contrôle de fermeture pour les nouveaux volumes. L’entrée
`from_verified_source_proxy` exige l’empreinte indépendante et le nombre
de frontières audité ; une édition des positions ou des indices invalide
cette exception.

| Donnée | SHA-256 |
|---|---|
| Positions f32 LE source | `bd7c936ad56fe9b88cefa7c70124d5660651146e50f088e0ee660c080e7e2d9d` |
| Indices u16 LE source | `0cbb33e75432c18eb5a626bacedd2a975d07471be797f0c27af085cd54c07b56` |
| Empreinte moteur, longueurs u64 LE puis positions f32 et indices u32 LE | `52b35be1ea38f49f9448ce8d8984d4d0bb3f3c3c506e73cbc1232a04d86ab92e` |

Les 42 composantes zéro négatif sont conservées dans les positions. Leur
normalisation ne sert qu’au contrôle des arêtes et ne modifie pas les
octets du proxy stocké. La fixture et sa provenance sont conservées dans
`F:/OpenRPG_Forge/.build/hull-source-fixture-290` ; elles ne constituent pas
encore un asset publié ni un bateau importé dans Studio.

## Validation et limites

Les revues statiques 286/287/289 établissent le contrat source, les
imports Bevy 0.19.1, le raccord des bindings et l’ordre du pass. La revue
delta 289 confirme le reset de l’activation avant les retours anticipés.
Elles ne constituent pas une exécution GPU.

Le premier `cargo check` natif 288.0 est **interrompu et non qualifié** :
attente prolongée du cache partagé et six entrées modifiées pendant le
contrôle. Son reçu conserve l’exit `-1`, les divergences et l’interruption
des seuls processus appartenant à cette tentative. Le cache et les
processus des autres travaux sont préservés.

Le harness CPU 291 compile le module courant avec les interfaces natives
historiques explicitement reçues de Library247.6. **La tentative 291.5
passe ses 11 tests** : compilation et tests exit 0, aucune modification
des sources ou fixtures pendant l’essai, quatre copies de sources
strictement identiques. Le reçu et les logs sont conservés dans
`F:/OpenRPG_Forge/.build/hull-source-tests-291/attempt.5`.

La tentative 291.2 échouait sur la résolution des modules et l’absence
de `CARGO_MANIFEST_DIR` dans le harness direct. La tentative 291.4
compilait, avec 10/11 tests réussis ; son échec à la frontière traduite
`9.95` provenait de sa représentation f32, légèrement hors de la marge
après soustraction de 10. Le test contrôle maintenant la frontière
locale exacte sous identité et deux points traduits de part et d’autre.
La marge de 0,05 et le comportement produit restent strictement inchangés.
Ces deux échecs sont conservés.

Le fichier temporel `render.rs` diffère intentionnellement de la librairie
historique. Le reçu v3 sépare cette différence de référence du contrôle
des modifications pendant l’exécution ; le SHA est stable avant/après.
Le nouveau contrat de cet adaptateur n’est pas compilé par le harness.
Le PASS 291.5 ne qualifie donc ni le moteur courant complet, ni un rendu
Studio/WebGPU, ni la réussite des commandes GPU.

Le contrôle **298 check.2 compile ensuite les métadonnées de l’Engine
courant complet, exit 0**, avec ses 348 fichiers copiés à l’identique.
Il couvre notamment le matériau `OpticalMaterial`, ses nouveaux bindings
et le contrat typé de `HullMaskAdapter`, absents du périmètre des harnesses
291/294. Le compilateur reçu est Rust 1.98.0, commit
`88d9e12ae178fab0fb5cc050a94da85685d449ea`, LLVM 22.1.8, cible MSVC.
La sortie `libforge_engine.rmeta` mesure 12 146 658 octets, SHA-256
`d657d64d57bbb188574c70f2b6f8967641057d47e2316f20d1c4df8ce1e6d1c4`.
Les logs ne contiennent aucun diagnostic de compilation.

Son reçu brut conserve le statut `drift-unqualified` : la comparaison
lexicale des chemins attribue à tort les 348 fichiers à `source_added`
lorsque les séparateurs Windows diffèrent. L’audit indépendant 299
normalise les chemins, la casse et les séparateurs : mêmes 348 sources et
copies, aucun ajout, suppression, doublon ou changement d’empreinte.
Les contrôles avant/après des sources originales, copies et dépendances
du reçu valent également zéro divergence. Le reçu brut n’est pas réécrit ;
`dependency-metadata-audit.json` et `NOTES-delta.md` documentent séparément
le succès réel du compilateur et ce défaut du classement.

Quatre entrées hors crate sont également copiées et vérifiées : le shader
`native-shore-hash.wgsl`, utilisé par cette compilation, et trois fixtures
JSON de Visual Valley réservées aux tests. Les tentatives préliminaires
298 check.0/check.1, interrompues ou expirées avant le compilateur, restent
conservées. Une tentative d’arrêt du compilateur lent de check.2 n’a effectué
aucune action : il venait de terminer naturellement.

Le pin 299 vérifie les 12 externs reçus et les 721 fichiers directs des
cinq chemins `-L`, avec inventaires et hashes stables. Les hashes historiques
disponibles correspondent ; la bibliothèque d’import Windows de C: n’a
pas de hash historique dans ces manifestes et conserve son hash observé.
Cette preuve porte sur les sources actuelles de l’Engine **contre les
binaires historiques Library247.6**. Elle ne fournit ni nouveau rlib,
ni compilation Cargo des dépendances actuelles, ni exécution des shaders
optiques ou de Studio. Les preuves et les entrées figées sont dans
`F:/OpenRPG_Forge/.build/hull-current-engine-build-298` et
`F:/OpenRPG_Forge/.build/hull-current-engine-review-299`.

La suite **300 library.0** produit ensuite le véritable rlib complet de
l’Engine courant : compilation exit 0, statut `current-engine-library-only`,
352 entrées source (348 de la crate et quatre entrées hors crate),
721 dépendances et cinq reçus inchangés avant/après. Les ensembles de
chemins utilisent la normalisation Windows corrigée ; aucun ajout de
source ni divergence n’est relevé. Les sources originales E: correspondent
au snapshot 298 contrôlé par l’audit 299, sans nouvelle copie intermédiaire.

`libforge_engine.rlib` mesure 220 087 788 octets, SHA-256
`d494ac20ab8305b69a70056c8d13f9741c54ef932d5a1fc9706b63ae045eb57d`.
Le dep-info mesure 57 571 octets, SHA-256
`4c33831856c53aa8e6c4c02483e8746bae839e5896220729d46a40fd5ce706b2`.
Le compilateur est le même Rust 1.98.0 reçu ; les externs et chemins `-L`
restent les binaires historiques Library247.6. Le reçu et les logs sont
dans `F:/OpenRPG_Forge/.build/hull-current-engine-live-300/library.0`.
Ce rlib contient le `PreparedOptics` courant et le pass HULL ; sa compilation
ne constitue pas encore une exécution GPU du fragment eau.

La sonde hors écran **294 build.0/run.0 passe ses 11 états**, avec
compilation et exécution exit 0, sources et dépendances inchangées.
Elle compile le véritable module courant, avec les dépendances natives
reçues de Library247.6. Le binaire de sonde mesure 105 021 952 octets,
SHA-256 `f4e78b9344f202e4a679d10903b507e86023863f74438b3d14cb52ba7bc6cef9`.
Les reçus, logs et lectures binaires sont conservés dans
`F:/OpenRPG_Forge/.build/hull-headless-probe-294`.

| État | Taille HULL | Pixels non nuls | Activation GPU |
|---|---|---:|---:|
| Proxy source actif | 128 × 96 | 270 | 1 |
| Hors champ | 128 × 96 | 0 | 0 |
| Retour dans le champ | 128 × 96 | 270 | 1 |
| Caméra dans la boîte source | 128 × 96 | 0 | 0 |
| Caméra à nouveau à l’extérieur | 128 × 96 | 270 | 1 |
| Redimensionnement impair | 129 × 97 | 275 | 1 |
| Caméra inactive | 1 × 1 | 0 | 0 |
| Caméra réactivée | 129 × 97 | 275 | 1 |
| Coque supprimée | 129 × 97 | 0 | 0 |
| Caméra désélectionnée | 1 × 1 | 0 | 0 |
| Coque recréée et déplacée | 129 × 97 | 293 | 1 |

La sonde compare 64 pixels de chacun des six états actifs à une
intersection rayon/triangles de la géométrie source, soit **384 contrôles**.
L’écart maximal vaut 0,0164052 unité. Le seuil appliqué est explicitement
`abs(distance_ray) / 1024 + 0.002`, lié à la précision float16 et au calcul
du rayon f32 ; il n’est pas le seuil strict du resolve couleur source.
Le padding de lecture R16Float est conservé : 129 × 97 produit des lignes
de 512 octets. Les lectures d’activation portent sur les 16 octets réels
du buffer utilisé par le pass.

La scène de sonde reste immobile jusqu’à la réception des deux lectures
asynchrones. Les états à caméra inactive/désélectionnée vérifient
l’activation neutre et la cible minimale ; ils ne prétendent pas qu’une
passe de caméra a été dessinée ou effacée. Cet essai utilise une seule
vue et aucun jitter. Il ne vérifie pas le fragment eau réel, les bindings
dans ses pipelines, un `FrameTag` corrélé octet par octet, le traitement
temporel complet, une seconde vue d’eau, Studio ou un navigateur.

La revue indépendante 295 confirme les reçus, les tailles, les hashes
des 22 lectures brutes et les zéros des états inactifs, padding compris.
Les retours 0/2/4 sont identiques octet par octet, comme 5/7 ; la distance
change lors de la recréation déplacée. Aucun diagnostic d’échec GPU n’est
présent dans les logs ; l’identité du pilote/adaptateur n’y est pas
consignée. La revue conserve toutes les limites ci-dessus et n’attribue
pas les résultats aux shaders couleur source.

Restent : le raccord GPU au matériau eau réel et au traitement temporel,
le bateau canonique éditable avec son proxy gouverné, l’inventaire des parts
effectivement soumises et son invalidation lors des éditions, puis le
raccord `HullMaskAdapter` Studio et une reconstruction native/WASM.
La recette 297 utilise le véritable `PreparedOptics::new/stage`, avec les
paquets vérifiés d’environnement, terrain et océan. Elle prévoit des lectures
GPU eau/coque/activation et les contrôles face avant, face arrière et fond
sans eau ; l’ancien matériau local du probe ne couvre pas les bindings 54/55.
Cette recette préparée n’est pas un essai exécuté.
La suite [304 — matériau eau réel](2026-10-02-real-water-hull-probe.md)
exécute ensuite le `PreparedOptics` courant lié à Engine300 : huit phases
GPU, 267 pixels retrouvant exactement le fond, répétition sans coque et
contrôle de vue inférieure identiques. Ses reçus, avertissement de fermeture
et limites restent séparés de cette preuve HULL-only.
Les candidats 279/283/284 précèdent cette modification ; leurs preuves
ne sont pas réutilisées pour qualifier ce nouveau pass. La calibration
d’exposition 096 et la parité complète de l’île restent distinctes.
