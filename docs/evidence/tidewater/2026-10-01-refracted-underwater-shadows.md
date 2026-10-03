# Tidewater — point d'entrée des ombres sous l'eau

Suite des [cartes d'éclairage immergé](2026-10-01-underwater-light-maps.md).
Référence figée : `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`,
`src/ocean/UnderwaterLighting.js`, `hookShadowPosition`, et
`src/engine/render/wgsl/lighting.js`.

**Réfraction vérifiée en natif et dans Studio sur les piles du ponton.**
La comparaison Studio 166 vérifie aussi le terrain immergé sous l'ombre du ponton.
Ce rapport ne constitue pas une qualification de gate ou de parité de l'île.

## Raccordement

La lecture des ombres d'objets dans l'éclairage du terrain et des bâtiments
est déplacée au point où la lumière pénètre dans l'eau. Le terrain utilise
la hauteur et la pente locales des cartes GPU, les objets légers le niveau
moyen et une normale verticale. Le rayon est réfracté selon l'indice 1,333 ;
sa remontée vers la surface reprend le seuil de 0,15 sur la composante Y.
Les lectures des ombres de relief et de nuages restent celles de la source.
Les receveurs au-dessus de l'eau conservent leur point initial.

Le disque de déplacement dépendant de la profondeur reprend les constantes
source : rayon `min(depth * 0.05, 0.5)`, bruit intercalé et cycle de 64 images.
Son index vient du véritable buffer GPU des cartes optiques. Le portage du
calcul ne prouve pas le flou temporel : la résolution temporelle source et
le TAAU restent à qualifier.

Le composant W inutilisé du vecteur d'absorption porte le contrôle de cette
réfraction ; XYZ gardent leurs coefficients optiques. Aucune liaison GPU,
taille d'uniforme, texture ou donnée auteur supplémentaire. Le réglage local
« Réfraction des ombres » est activé par défaut, indépendant de la force des
photons ; désactiver l'éclairage immergé rend aussi ce hook neutre. Studio
conserve sa sérialisation booléenne et les anciennes préférences obtiennent
le nouvel état activé par défaut. Le menu natif dispose du même contrôle.

La transformation du shader exige exactement une lecture d'ombre au site
d'éclairage prévu. Une absence, une duplication ou une seconde adaptation
échoue explicitement ; les anciens shaders de diagnostic sans ce modèle
d'éclairage restent inchangés.

## Contrôles

- 8 tests caustiques : PASS, dont parsing/validation Naga des deux variantes
  complètes de receveurs et rejet des ancres de shader absentes/dupliquées.
- Build natif 152 : PASS en 2 min 28 s.
- Clippy strict : échecs préexistants conservés, 64 diagnostics moteur et
  31 Studio ; aucun nouveau diagnostic dans les fichiers du calcul ajouté.
- Run natif 153 : PASS sur les sept étapes et les trois paires de milieu.
  Le vérificateur indépendant des origines GPU/horloges passe aussi.
  Ce premier run ne désactive pas isolément la réfraction des ombres.
  [Parcours](../../../../.tidewater-integration/native-refracted-shadows-153/probe.walk.json),
  [origine GPU et signature](../../../../.tidewater-integration/native-refracted-shadows-153/light-maps-verification.json),
  [binaire et fermeture](../../../../.tidewater-integration/native-refracted-shadows-153/run.json).

Les captures Studio activé/désactivé/répété fixent caméra, joueur, vent et
horloge source ; le milieu reste activé, les photons et rayons sont à zéro.
Les reçus vérifient la commande et les uniformes des matériaux effectivement
raccordés aux cartes. La capture native additionnelle s'insère après la
paire Neutral existante, avec les mêmes entrées drainées, sans supprimer ni
modifier les trois paires de régression et sans déplacer le joueur.
Une absence d'effet visible ou une répétition différente ne constitue pas une
preuve positive. Les compteurs d'encodage ne sont pas des barrières GPU.

### Comparaison native 156

Les trois paires de milieu et les sept étapes de déplacement passent encore :
3 808 ticks, 791 ticks au sol, une entrée en nage et un retour à la marche,
dérive Neutral nulle. Le contrôle indépendant des origines GPU et des horloges
passe. La capture additionnelle conserve le milieu actif, photons et rayons à
zéro, et ne change que le contrôle de réfraction des ombres.

Les PNG activé et répété sont identiques, de même que leurs pixels RGB décodés.
Entre activé et désactivé, 7 089 pixels changent, dont 4 059 de plus de deux
niveaux ; le maximum est de 45/255 et la moyenne absolue des canaux sur l'image
entière de 0,017727/255. Le changement se situe auprès des piles visibles à
gauche : il prouve un effet local sur ces receveurs, pas toute la couverture
du fond marin. Les contrôles des matériaux raccordés sont confirmés sur un
receveur terrain et sept receveurs légers.

[Régression conservée](../../../../.tidewater-integration/native-refracted-shadows-156/light-maps-verification.json),
[audit des pixels et des uniformes](../../../../.tidewater-integration/native-refracted-shadows-156/refracted-shadow-verification.json),
[capture activée](../../../../.tidewater-integration/native-refracted-shadows-156/probe.shadow.on.png),
[capture désactivée](../../../../.tidewater-integration/native-refracted-shadows-156/probe.shadow.off.png).

### Première comparaison Studio 153 : effet non validé

Le parcours Studio, l'édition/sauvegarde/réouverture, la conservation de la
source et les commandes des receveurs passent. Les entrées des triplets restent
figées et les répétitions sont identiques. Cependant la vue à X=64, Z=25,
inclinée vers le fond, ne montre qu'un seul pixel changé de 1/255. Aucun pixel
ne dépasse deux niveaux ; cette vue ne prouve donc pas le déplacement d'ombre.
Les autres vues montrent zéro à trois pixels changés de 1/255 seulement.

Le premier vérificateur utilisait à tort une inégalité de hash PNG comme preuve
d'effet visible. Son résultat est conservé comme
`refracted-shadow-verification-attempt-02-hash-only.json`, mais remplacé par le
[verdict corrigé FAILED](../../../../.tidewater-integration/studio-tidewater-refracted-shadows-runtime-153/refracted-shadow-verification.json).
Le vérificateur exige désormais une moyenne RGB non arrondie supérieure à
0,01/255 et au moins 1 000 pixels changés de plus de deux niveaux, ainsi qu'une
répétition RGB exacte. Le contrôle natif additionnel reprend ces mêmes critères
sur les RGB8 réellement livrés par le callback de capture.

La reprise Studio rejoint X=40, Z=29 par la nage et vise horizontalement les
faces éclairées des piles, comme la capture native. Le run 154 a été interrompu
avant son ledger final : handle absent et aucun wrapper/candidat restant dans
l'inventaire des processus. Ses fichiers sont conservés ; le run 155 repart
dans un autre dossier. Ce changement de cadrage ne modifie ni les shaders ni
les critères de différence de pixels.

Le build natif 154 avait échoué sur le chemin du nouveau sous-module de preuve ;
le chemin explicite corrige cet échec, et le build 155 passe. Le build 157 avec
le contrôle RGB8 renforcé passe en 1 min 26 s. Son Clippy strict s'arrête sur
deux diagnostics préexistants de `forge_project::zone_authoring::session`
(`needless_pass_by_value`), avant d'auditer l'exemple ; cela n'est pas un PASS.
L'audit séparé à sévérité warning a permis de corriger les nouveaux diagnostics
du sous-module de preuve (import global, longueur de méthode, découpage RGB).
Le build 158 passe en 53,98 s ; cet audit laisse 20 warnings de l'exemple, aucun
dans `swim_smoke/shadow_proof.rs`, sans rendre le Clippy strict vert.

Le run Studio 155 échoue avant la nage sur la capture des entrées : deux refus
`WrongDocumentError` de verrouillage du pointeur. La simulation CPU active ne
prouve pas que la vue accepte les commandes ; l'essai demeure `failed_closed`.
La reprise 156 remet le document au premier plan via CDP avant le clic réel et
enrichit le diagnostic de focus, visibilité et rattachement du canvas. Elle
conserve l'exigence de verrouillage réel, le délai de 15 s et tous les contrôles
graphiques du parcours.

### Comparaison Studio 156 et suite du diagnostic

La remise au premier plan permet la capture réelle et le déplacement jusqu'à
X=39,93, Z=28,56, en vue horizontale des piles. Le triplet retrouve les mêmes
entrées GPU et la répétition exacte, mais seuls cinq pixels changent de 1/255 :
le [vérificateur conserve FAILED](../../../../.tidewater-integration/studio-tidewater-refracted-shadows-runtime-156/refracted-shadow-verification.json).
Le défaut ne peut donc plus être attribué uniquement au premier cadrage.
Le parcours complet 156 échoue aussi au retour : le personnage est revenu en
marche et au sol, mais reste à Z=-43,46 à l'échéance de 55 s, au lieu de franchir
Z=-47. Ce critère reste exigé ; le run n'est pas requalifié en PASS.

Une observation GPU est ajoutée pour lire les 464 octets du véritable buffer
des matrices de réception, uniquement pendant une comparaison tenue. Elle
publie contrôle d'adoption, activation, caméra, direction, matrices, cascades
et index de trame. Ce reçu est asynchrone : il doit rejoindre la caméra tenue
avant de servir au diagnostic et ne remplace pas une comparaison d'images.
Le reset de ce buffer lors d'une nouvelle génération et les bindings des
matériaux restent des pistes à vérifier ; aucune cause n'est encore prouvée.
Cette instrumentation est une nouvelle tranche, non couverte par le binaire
et le WASM 152 des captures ci-dessus.
Les trois tests de layout/adaptation/décodage du receveur passent. Le décodeur
rejette un buffer tronqué ou une caméra non finie. Le readback s'arrête et son
dernier échantillon est effacé à la reprise. La compilation WASM 160 passe
en 8 min 44 s et le package 160 est construit en `BUILD_ONLY`. Le parcours
de diagnostic s'exécute dans une nouvelle copie auteur isolée. Le parcours 160
vise désormais le point de départ mesuré après le
détour, avec de vrais événements souris/clavier et les mêmes critères de
retour. Ces deux modifications attendent leur preuve d'exécution.
Le run 160 s'est arrêté avant le premier triplet sur un `$name:` non délimité
dans le helper PowerShell. Son échec est conservé ; le helper 161 corrige
`${name}:`, et le runner et son helper sont tous deux reparsés avant la relance.
Le run 161 utilise le même binaire
`96eff49485616b86c478da35e1a15993ec5c0ce785e3f2d5a01c859af070fba7`,
WASM `ded094cad1e86da5cc7ad388506de17ad889ba06d7d1a85d60ac6f8ec2834132`,
et attend le reçu GPU d'activation/caméra avant les captures isolées.

Le run 161 s'arrête ensuite à la surface : le buffer GPU est activé et adopté,
mais sa caméra reste 6,672 mm sous la caméra finale tenue. Le reçu Air concorde
à moins de 0,008 mm. Un dernier paquet de l'océan peut encore ajuster la caméra
flottante après la pause, alors que les ombres cessaient immédiatement leur
mise à jour. Ce décalage est mesuré ; il ne prouve pas à lui seul la cause de
l'absence d'effet visible des runs 153/156. L'échec et les deux reçus sont
conservés dans
[le dossier 161](../../../../.tidewater-integration/studio-tidewater-shadow-receiver-observation-runtime-161).

La correction 162 lit la transformation globale après sa propagation et
permet de transmettre cette dernière pose lorsque l'océan est drainé. Elle
réutilise la génération résidente, sans avancer l'horloge du vent pendant la
pause et sans accepter de modification auteur. Le seuil de concordance GPU
reste de 1 mm et les critères RGB restent inchangés. La compilation WASM passe
en 3 min 56 s ; le package 162 est `BUILD_ONLY`, binaire
`24f859149c82ebe08709de9108535e77542eaffc33c3c33045e128bda949a84a`,
WASM `2f7497bcd0668a28b437e40bf2ca09b57c095df1985851ef03db46fbb9de13c5`.
Le parcours 163 reprend tous les contrôles dans une nouvelle copie isolée ;
il passe, termine et ferme son processus. Les cinq triplets retrouvent une
caméra GPU exactement égale à la transformation globale, des matrices
inchangées entre ON/OFF/répétition et une répétition RGB exacte. Le retour
dirigé vers le départ observé franchit Z=-47 en marche au sol dans le délai
initial. L'archive source est préservée.

La vérification RGB indépendante 163 reste cependant `FAILED` : sous le ponton,
huit pixels changent de 1/255, aucun de plus de deux niveaux, moyenne absolue
0,000001286/255. Air et fond ne changent pas ; surface et plongée ne changent
que de 1/255. La correction de caméra ne suffit donc pas à qualifier l'effet
Studio. Le nouveau vérificateur exige aussi l'activation et la concordance de
la caméra GPU, puis l'égalité du reçu GPU complet pendant chaque triplet.
[Parcours 163](../../../../.tidewater-integration/studio-tidewater-shadow-receiver-drain-runtime-163/fish-stand-studio-smoke-ledger.json),
[comparaison indépendante 163](../../../../.tidewater-integration/studio-tidewater-shadow-receiver-drain-runtime-163/refracted-shadow-verification.json).

La lecture de `bevy_render-0.19.1/src/storage.rs` confirme que la réinsertion
du buffer, de même taille, usage et label avec `COPY_DST`, conserve son
allocation GPU. Le simple reset du handle n'établit donc pas une désynchronisation
des bindings. La suite doit observer les paramètres des matériaux et leurs
échantillons de réception, au-delà du buffer global déjà validé.
L'audit Clippy strict Engine 160 conserve 64 erreurs préexistantes et aucun
diagnostic dans les deux fichiers du décodeur ; il ne constitue pas un PASS.

### Oubli des accessoires dans les casters Studio — correction 164

La lecture de `scene::buildings` révèle un défaut indépendant du buffer : elle
parcourt exclusivement `ResidentScene`. Les passerelles, le ponton et les
accessoires de place sont projetés dans `ResidentBoardwalk`, `ResidentPier`
et `ResidentPlaza`, sans être transmis à cette collecte. Le probe natif utilise
les lots opaques complets de la source. La présence du ponton à l'image ne
prouvait donc pas sa présence dans le pass des ombres de Studio.

La collecte partagée inclut désormais les pièces opaques de ces trois
ressources. Elle compose les poses locales jusqu'à la racine, y compris les
pivots de l'enseigne et des lanternes. Elle continue d'exclure les matériaux
5/6 de filet/poisson. La clé de génération Studio contient les entités et les
matrices de cette même collecte pour suivre déplacement, suppression et
reprojection des accessoires. Aucun placement auteur n'est modifié.

Le test de hiérarchie imbriquée vérifie une pose courante malgré un
`GlobalTransform` périmé. Le test du paquet de ponton figé est étendu pour
exiger 27 casters opaques, même sans `ResidentScene`, et leur déplacement
de 8 m avec la racine auteur. Les deux tests passent : le ponton est chargé
depuis `pier-package-001` avec `FORGE_TIDEWATER_PIER_PACKAGE` explicitement
fourni, et non ignoré faute de fixture. Ses 27 pièces produisent 27 draws
validés sans `ResidentScene` ; déplacer la racine conserve les entités et
déplace toutes leurs matrices de 8 m. La compilation WebGPU 164 passe en
14 min 23 s et son package est construit en `BUILD_ONLY`. Le parcours 165
reprend tous les contrôles du 163 dans une nouvelle copie isolée, avec les
mêmes cinq triplets et le retour à pied. La présence effective des ombres et
la comparaison RGB attendent sa fin.

Clippy strict 164 s'arrête avant Engine sur les deux diagnostics préexistants
`needless_pass_by_value` de `forge_project::zone_authoring::session`.
L'audit séparé à sévérité warning termine : 65 warnings Engine, dont celui du
cache de compilation, aucun dans `scene.rs` ni les trois méthodes ajoutées.
Cet audit ne constitue pas un Clippy strict PASS.

### Validation Studio 165 après ajout des casters

Le binaire `bda531a02cf8bed5c7a04d93752d7fccaffd001e932589d06c9f7e9eed3b4a15`
et le WASM `70587123fbef3bcac65ec3b55608ec03f888502da8eda6a0230c3b0477b17e80`
terminent le parcours complet et ferment le processus d'essai. Édition,
sauvegarde, réouverture, préférences, effets requis et huit étapes de
locomotion passent. Le personnage revient en marche, au sol et au-delà de
Z=-47 dans le délai original de 55 s. Les 31 fichiers d'entrée figés ne
présentent aucune dérive après le run ; l'archive auteur reste inchangée.

La génération adoptée contient 148 pièces opaques. Pour chacun des cinq
triplets, le reçu GPU de réception est activé, sa caméra correspond à la vue
réelle, et son contenu complet reste identique entre ON/OFF/répétition.
Les matrices, caméra, horloges océan/vent, cartes et paramètres physiques sont
contrôlés ; les rayons et caustiques sont à zéro. Seule la réfraction des
ombres change. Le vérificateur PNG indépendant passe avec les critères
originaux, sans assouplissement :

| Vue | Pixels changés | Pixels avec delta > 2 | Delta maximal /255 | Moyenne absolue RGB /255 |
|---|---:|---:|---:|---:|
| Air | 0 | 0 | 0 | 0 |
| Surface | 16 400 | 9 231 | 75 | 0,043763 |
| Plongée | 3 351 | 1 297 | 16 | 0,002981 |
| Fond | 0 | 0 | 0 | 0 |
| Piles du ponton | 13 587 | 5 973 | 71 | 0,026755 |

Chaque répétition retrouve exactement les PNG et les pixels RGB initiaux.
L'inspection des deux captures sous le ponton situe l'effet sur les piles et
leurs traverses à gauche de l'image. Ce test qualifie un effet local sur les
objets légers ; la vue Fond ne prouve toujours pas séparément le déplacement
des ombres du terrain par les vagues. Elle reste une limite explicite.
L'ajout des casters produit l'effet absent du 163, sans changement du shader
de réfraction entre ces deux candidats. Les échecs précédents restent conservés.

[Parcours 165](../../../../.tidewater-integration/studio-tidewater-opaque-props-shadows-runtime-165/fish-stand-studio-smoke-ledger.json),
[comparaison RGB et reçus GPU 165](../../../../.tidewater-integration/studio-tidewater-opaque-props-shadows-runtime-165/refracted-shadow-verification.json),
[réfraction activée](../../../../.tidewater-integration/studio-tidewater-opaque-props-shadows-runtime-165/shadow-underpier-on.png),
[réfraction désactivée](../../../../.tidewater-integration/studio-tidewater-opaque-props-shadows-runtime-165/shadow-underpier-off.png).

Les sources figées, binaires, scripts, reçus, captures et vérificateurs des
tranches 160–165 sont référencés dans le
[manifeste additionnel](../../../../.tidewater-integration/refracted-shadows-artifacts-165.json).

### Nouveau contrôle natif 159 avec lecture RGB8 dans le callback

Le binaire `66acfca4a4de44994a7ac36024964115e7428d7cb070846d21928b05a7497f16`
termine et ferme son processus. Les sept étapes et les trois paires passent,
ainsi que le triplet additionnel avec le critère RGB8 renforcé. Le décodeur
PNG indépendant retrouve exactement les hashes RGB et les métriques calculées
dans le callback : 8 939 pixels changés, dont 5 532 de plus de deux niveaux,
maximum 46/255, moyenne absolue RGB 0,021295/255. La répétition est exacte.

L'audit historique 151 supposait un total fixe de 3 808 ticks et a rejeté ce
run à 3 848 ticks, malgré le parcours complet. Son échec est conservé dans
`light-maps-verification-attempt-01-fixed-ticks.json`. Le nouvel audit vérifie
le total contre la somme des sept reçus d'étapes, leurs modes et le retour réel
à la plage, puis conserve tous les contrôles GPU, les trois paires, la dérive
Neutral nulle, le seuil de pas caméra 0,3 m et le délai des paires de 10 s.
Il passe avec une reprise de plongée à la ligne d'eau déjà prévue par le probe,
752 ticks au sol, une entrée en nage et un retour à la marche.

[Régression 159](../../../../.tidewater-integration/native-refracted-shadows-159/light-maps-verification.json),
[RGB8 et décodeur indépendant](../../../../.tidewater-integration/native-refracted-shadows-159/refracted-shadow-verification.json).

Les hashes des captures, reçus, sources de compilation et vérificateurs du lot
152–159 sont conservés dans le
[manifeste](../../../../.tidewater-integration/refracted-shadows-artifacts-159.json).

### Validation séparée du terrain — Studio 166

Le candidat 164 est conservé sans recompilation. Le parcours 166 ajoute une
sixième comparaison en rejoignant à la nage le côté est du ponton, cible
X=64/Z=29, puis en dirigeant la caméra vers le sable. Il conserve les cinq
comparaisons et tous les contrôles précédents, ainsi que le retour à pied dans
le délai original. Le parcours termine, le processus est fermé et l'archive
auteur est préservée.

Le vérificateur indépendant reprend tous les critères du 165 et exige en plus
un effet sur une région centrale de sable, X pixels 480–1440 / Y 250–1030,
qui exclut les piles visibles en haut de l'image. Il passe : 544 765 pixels
changent, dont 420 292 de plus de deux niveaux ; le delta RGB moyen est
30,269930/255 et le maximum 141/255. La répétition retrouve exactement les
pixels et le PNG initiaux. Air demeure strictement identique. La caméra GPU
est [63,539890 ; −1,794159 ; 28,986637], les paramètres et matrices restent
figés ; seuls les contrôles de réfraction des ombres changent. L'inspection
des images montre le déplacement de l'ombre du ponton sur le fond sableux.
Cette preuve qualifie le hook complet sur ce terrain immergé, à cette vue et
ce soleil ; elle ne qualifie pas toutes les heures ou l'accumulation temporelle.

[Parcours 166](../../../../.tidewater-integration/studio-tidewater-terrain-shadow-runtime-166/fish-stand-studio-smoke-ledger.json),
[récepteurs GPU, six comparaisons et région sable](../../../../.tidewater-integration/studio-tidewater-terrain-shadow-runtime-166/terrain-refracted-shadow-verification.json).
[Manifeste des 65 artefacts et absence de dérive des 31 entrées au run 166](../../../../.tidewater-integration/terrain-refracted-shadows-artifacts-166.json).

La lecture de la source précise la prochaine étape : `RefractionPass.js`
produit une **couleur éclairée RGBA16F** à demi-résolution, alpha de couverture,
et une profondeur reversed-Z. Il exclut les fragments au-dessus de mer +0,4 m.
La variante monochrome concerne exclusivement l'échantillonnage des caustiques
dans cette passe (`UnderwaterLighting.js`, lignes 235–236), sans dispersion ;
elle ne signifie pas une capture en niveaux de gris. Cette passe séparée reste
à raccorder : la copie opaque actuelle ne révèle pas le fond caché par le ponton.

## Limites

Restent ouverts : accumulation temporelle de ce flou, passe séparée de
réfraction et échantillonnage des caustiques sans dispersion dans cette passe,
couverture séparée des rochers/récifs, torche, autres comportements,
package Aube final et comparaison à la source avec la même horloge. L'objectif
complet reste actif.

Suite du run 166 : [capture sous-marine couleur/profondeur/couverture](2026-10-01-studio-refraction-capture.md).
Les reçus de cette passe possèdent leur propre candidat ; les succès 164/166
restent attachés à leurs hashes d'origine. Le TAAU source
demande ensuite un historique HDR, une profondeur précédente, la vélocité
des surfaces et de l'eau, et la séquence Halton ; activer le TAA natif Bevy
ne suffirait pas à qualifier cette chaîne dans Studio. Aucun processus de
ce lot ne reste ouvert et aucune publication n'a été réalisée.
