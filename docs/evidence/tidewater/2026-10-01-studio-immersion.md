# Tidewater — raccordement du milieu sous-marin au Playtest Studio

Suite du [contact sur pente](2026-10-01-studio-slope-contact.md), dans la chaîne
du [réglage d’exposition Village](2026-09-30-studio-exposure-sweep.md).
Référence figée : `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`.

## Raccordement

Le matériau optique Studio porte désormais le masque de la surface réellement
rastérisée dans l’alpha HDR : 0,25 côté air, 0,75 côté immergé. La passe de milieu
le lit après les passes principales, avant les premiers post-traitements.
Le générateur du masque est partagé avec la promenade native.

La passe utilise le même matériau préparé, les mêmes textures FFT/ressac et le
même paquet de requêtes GPU que l’océan visible. Son shader reprend la hauteur
eulérienne, le terrain et les corrections authorées. Les coefficients d’absorption,
diffusion et éclairage viennent du paquet de surface vérifié ; aucune nouvelle
surface d’eau CPU n’est introduite.

Le marqueur est posé sur la caméra joueur existante. L’observation d’immersion
est ordonnée après propagation des transforms et avant les consommateurs
nuages, optique de surface, flare solaire et brume atmosphérique. Elle distingue
air, bande de surface et immersion complète. Le réglage « Effets sous-marins »
active ou contourne la passe de milieu, sans modifier la simulation ni supprimer
la passe de la chaîne HDR. Flare et brume restent liés au milieu de la caméra.

Le réglage est mémorisé dans les préférences visuelles locales. Une préférence
v1 existante sans ce champ le reçoit activé par défaut et conserve ses autres
valeurs. Il ne fait pas partie de `authoring.json`.

## Comparaison figée

Les diagnostics proposent un gel transitoire du joueur et des animations.
Le temps virtuel est suspendu, ainsi que les offres de nouveaux calculs de
l’océan. Une transaction déjà offerte termine son calcul ; les observations
continuent sur le temps réel. Vent et nuages gardent leur pose. La reprise
rebase l’océan sur le temps réel courant pour éviter un saut lié à la durée
de capture. Ce gel n’est pas mémorisé dans les préférences ou la carte.

Les captures actives, contournées puis actives répétées doivent conserver
exactement la matrice/projection caméra, le tick et l’ancre joueur, les
échantillons GPU, l’étape/heure océan et le temps du vent. L’échantillon caméra
et le paquet GPU doivent atteindre la dernière étape terminée de l’océan.
Le dernier échantillon consommé par le joueur appartient à son dernier tick
physique suspendu : il peut précéder cette transaction qui finit de se vider.

## Contrôles de construction

- 8 tests du milieu sous-marin, 9 tests d’horloge et 30 tests du contrôleur
  joueur passent ; la première commande avec le filtre `tidewater_player`
  n’exécutait aucun test et ne constitue pas une validation.
- La construction WASM avec `--features web` et l’empaquetage Tauri passent.
  Le premier essai WASM avait un import `bevy_ecs` manquant dans le nouveau
  module Studio ; il a été corrigé sans ajout de dépendance.
- `cargo check` de la promenade native passe après le partage du masque.
- Clippy strict reste rouge : 64 diagnostics moteur et 31 Studio. Le nouveau
  module de raccordement ne produit aucun diagnostic ; ces résultats ne
  qualifient pas une gate.

Candidat 137 : SHA-256
`bbe63589e65e4ed4af73eaf80f6d252a72314be6c834c16477c61508c6da3335`.
Frontend WASM :
`07fb9d3a7239d19b9bf253076bf34a883dd4c68a0f7c641d7d43258bd875cfed`.
[Reçu de construction](../../../../.tidewater-integration/studio-tidewater-immersion-candidate-137/build-proof.json),
[entrées du code](../../../../.tidewater-integration/studio-immersion-inputs-137.json).
`BUILD_ONLY` reste distinct du verdict d’exécution.
Le contrôle final de format passe. Rustfmt a seulement réordonné les deux
déclarations de modules dans `lib.rs` après la construction ; les corps
exécutables testés restent inchangés.
[Hashes avant/après ce formatage](../../../../.tidewater-integration/studio-immersion-inputs-final-137.json).

## Exécution

La série 138 conserve son échec : le contrôle exigeait aussi que le dernier
échantillon consommé par la physique suspendue atteigne la transaction océan
qui venait de se vider. Aucun triplet d’images n’a été accepté.
[Reçu d’échec](../../../../.tidewater-integration/studio-tidewater-immersion-swimming-138/immersion-air-pair.json).
La série 139 passe le smoke complet et les huit étapes de nage, retour plage et
édition compris. Les trois triplets conservent les entrées figées exactement.
Les préférences sont restaurées et les ticks/étapes reprennent après chaque
triplet ; les sources restent intactes. La caméra et le paquet GPU utilisent
la dernière étape terminée. Le joueur avait consommé une étape antérieure de
4 à 5 étapes selon la capture, avant suspension de son tick physique.
[Reçu du parcours](../../../../.tidewater-integration/studio-tidewater-immersion-swimming-139/tidewater-studio-swimming.json).

L’image immergée active diffère du contournement de 28,986 niveaux RGB moyens
par canal sur 255 ; sa répétition retrouve exactement le même PNG. Le fond
et les piles du ponton prennent la teinte et l’atténuation du milieu. La
capture de bande de surface reste en revanche 4,49 cm au-dessus de la hauteur
locale : son plan proche reste dans l’air et les PNG actif/contourné sont
identiques. Le contrôle de pixels refuse donc de qualifier une vraie vue mixte
avec cette capture. La série 140 cherche cette vue par les seules touches de
nage et le gel de diagnostics, sans déplacer artificiellement le joueur.

### Verdict visuel de la série 140

Le smoke d’édition/relecture/playtest et le contrôle indépendant des pixels
passent. Les six essais de gel de la surface sont conservés dans le reçu ;
le sixième place la caméra 1,485 cm sous la hauteur locale. Les captures sont
1920 × 1080, avec entrée capturée et deux requêtes GPU sans rejet. Pour comparer
le milieu seul, exposition automatique, grain et flou de mouvement sont
désactivés par leurs contrôles UI puis restaurés après chaque triplet.

| Vue | Écart RGB moyen actif/contourné, sur 255 | Pixels avec un canal différent de plus de 2 | Écart actif/répétition |
|---|---:|---:|---:|
| Air | 0,000 | 0 % | 0,000 |
| Vue mixte | 34,265 | 46,170 % | 0,000 |
| Immergée | 23,488 | 99,422 % | 0,000 |

Les PNG actifs répétés ont le même SHA-256 que leur première capture dans
les trois cas. Les signatures exactes des entrées restent identiques ; les
branches active et contournée sont encodées, et la reprise des ticks joueur
et des étapes océan est observée. Ces compteurs indiquent du travail encodé,
pas une fence GPU ; les PNG fournissent la preuve des pixels présentés.

[Vérification des signatures et pixels](../../../../.tidewater-integration/studio-tidewater-immersion-swimming-140/immersion-verification.json),
[reçu de vue mixte](../../../../.tidewater-integration/studio-tidewater-immersion-swimming-140/immersion-surface-pair.json),
[reçu immergé](../../../../.tidewater-integration/studio-tidewater-immersion-swimming-140/immersion-immersed-pair.json).
Inspection des images effectuée : la vue mixte conserve le ciel et le relief
côté air, colore le fond côté eau et montre le liseré du ménisque ; la vue
immergée atténue le fond et les piles du ponton. Aucun oracle source synchronisé
n’est photographié dans cette série.

L’audit complet de nage 139 passe, y compris la neutralité loin du fond.
L’audit de nage 140 **ne passe pas** ce dernier critère : sa plongée arrive
sur la limite source à 25 cm du fond. Il n’est donc pas utilisé comme preuve
de neutralité libre. Les contrôles de pixels 140 restent distincts et ne
relâchent aucun seuil de cet audit physique.
[Audit physique 139](../../../../.tidewater-integration/studio-tidewater-immersion-swimming-139/swimming-verification.json),
[échec physique 140](../../../../.tidewater-integration/studio-immersion-swimming-140.verification.log).

Le snapshot source conserve le SHA-256
`50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720`.
Les processus possédés par les essais sont arrêtés ; aucun processus candidat
ou compilateur n’est conservé. La série ne qualifie pas le package du jeu via
le launcher Aube, les échelles du ponton dans Studio ou toute l’île.

## Limites

La passe partagée est le sous-ensemble natif déjà documenté : extinction,
diffusion, vue mixte et ménisque. Les shafts volumétriques, la diffusion de
torche, les caustiques complètes et TAAU source ne sont pas qualifiés par ce
raccordement. L’occlusion ambiante Studio reste ordonnée différemment de la
chaîne source. Une paire Studio active/contournée ne prouve pas la parité
avec une capture Three.js. L’objectif complet et TW-008/TW-012 restent ouverts.

Suite : [rayons de caustiques et comparaisons Studio](2026-10-01-studio-caustic-shafts.md).
