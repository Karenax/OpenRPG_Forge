# Tidewater — capture séparée du fond à travers l'eau

Suite des [ombres sous-marines](2026-10-01-refracted-underwater-shadows.md).
Référence figée : `dgreenheck/tidewater@1438b1ab`, `RefractionPass.js`,
`WaterMaterial.js`, `UnderwaterLighting.js` et `Caustics.js`.

## Portage

L'eau dispose désormais d'une capture couleur éclairée RGBA16F, avec alpha de
couverture, et d'une profondeur Depth32Float reversed-Z. La taille suit la
moitié du viewport réel, avec l'arrondi source pour les dimensions impaires.
Le filtre de fragments exclut ce qui dépasse la mer de 0,4 m. Les meshes
dérivés partagent les meshes, textures et buffers actuels du terrain et des
objets opaques ; les poses, remplacements, masquages et suppressions sont suivis.
Ils ne possèdent aucune donnée auteur ou collision et ne sont pas sauvegardés.
Les plantes et l'eau sont exclues de la couche de capture ; les objets opaques
entièrement au-dessus du seuil sont filtrés par leurs bounds transformés.

La caméra dérivée utilise la pose finale et la projection exacte de la vue
principale. Son jitter est redimensionné pour garder le même décalage NDC ;
elle n'hérite ni du ciel, ni des effets finaux, ni de la lumière de travail.
Elle rend avant la vue principale. Sa profondeur ne peut plus écraser la copie
opaque principale. Le matériau d'eau cherche la couleur au point d'arrivée
du rayon réfracté, exige couverture >0,5 et profondeur >0, puis déprémultiplie
la couleur. La copie opaque reste le fallback lorsque ce test échoue.

La capture conserve ses couleurs. Seule la lecture des photons du terrain
supprime leur dispersion chromatique, conformément à la branche source
`REFRACTION_CLIP`. Cette variante possède les mêmes cartes, pentes, mousse et
atténuation que l'éclairage immergé existant.

Le réglage local « Fond visible à travers l'eau » active cette passe. Il est
sérialisé dans les préférences graphiques existantes, actif par défaut pour
les anciennes préférences. Les diagnostics distinguent les poses CPU et les
commandes encodées d'une lecture effective des deux textures GPU. Cette dernière
se fait une seule fois pendant un gel de comparaison, après 40 frames ; elle
ne tourne pas en permanence pendant le jeu. Son checksum est diagnostique,
et n'est ni un SHA de provenance ni une preuve visuelle.

## Candidat 168 / parcours 170

Le check WebGPU passe après correction des erreurs initiales de visibilité de
fonction et de borrow. La construction WASM passe en 11 min 54 s, le shell
Tauri est empaqueté en `BUILD_ONLY`. Les cinq nouveaux tests natifs passent :
arrondi des dimensions et usages de textures, bounds transformés, refus d'un
shader absent/dupliqué, couverture/fallback, décodage half-float/profondeur et
padding. Les huit tests de caustiques passent également, avec validation Naga.

Binaire 168 : `dc001138cbae8d803c982c74c28c5ed517c446634cc7d608b77c23e512b19079` ;
WASM : `3dcbbab8dacca6557604b7511317d4a4580f03689025d58f9372f6b73842c0e3`.

Le parcours 170 termine édition, sauvegarde, réouverture, préférences, effets
requis et huit étapes de locomotion. Il conserve les six triplets d'ombres du
166 et ajoute un triplet activé/désactivé/répétition face à la mer depuis la
plage. Le retour à terre respecte le délai original et la source est préservée.
Le vérificateur indépendant reprend tous les critères du 166 et passe aussi
les exigences supplémentaires de capture :

| Mesure du nouveau triplet | Résultat |
|---|---:|
| Pixels changés, image entière | 435 048 |
| Pixels avec delta >2, image entière | 75 155 |
| Moyenne absolue RGB /255 | 0,286304 |
| Pixels avec delta >2, eau près du ponton | 19 608 |
| Moyenne absolue RGB /255, eau près du ponton | 1,076556 |
| Delta maximal ciel / sable sec | 1 / 1 |
| Répétition pixels et PNG | Exacte |

La région d'eau est X=0–850/Y=405–620. Les régions témoins sont
ciel X=920–1680/Y=0–285 et sable sec X=900–1720/Y=840–1040 ; elles ne présentent
aucun delta supérieur à un niveau. L'inspection des images confirme un effet
local dans l'eau, sans changement visible de la vue d'ensemble. Elle ne permet
pas encore de conclure sur tous les cas d'occlusion derrière une rambarde.

Les lectures GPU trouvent 358 075 pixels couverts dans chacune des textures
960×540, sur 518 400 pixels, sans valeur non finie. L'alpha varie de 0 à 1,
la profondeur de 0 à 0,030288175. Les moyennes HDR RGB sont
[0,436037 ; 0,354412 ; 0,218507] : la capture n'est pas grise. La caméra de
capture et la caméra principale concordent ; les données GPU des ombres et
les horloges restent figées. Le contrôle HTML et la préférence sauvegardée
suivent chacun les trois phases du réglage. La simulation reprend ensuite.

[Parcours 170](../../../../.tidewater-integration/studio-tidewater-refraction-capture-runtime-170/fish-stand-studio-smoke-ledger.json),
[audit indépendant et textures GPU](../../../../.tidewater-integration/studio-tidewater-refraction-capture-runtime-170/refraction-capture-verification.json).

## Audit et limites

L'audit Clippy 168 signale six warnings dans les nouveaux modules ; ils sont
corrigés dans le 169 sans changement du calcul ou des shaders produits.
L'audit warning 169 termine avec les 65 warnings Engine préexistants, dont
celui du cache, aucun dans ces modules. Clippy strict 169 reste en échec sur
les deux `needless_pass_by_value` préexistants de `forge_project::session`.
L'audit warning n'est pas un Clippy strict PASS. La construction WASM 169
passe en 8 min 28 s ; sa qualification runtime possède son propre reçu.

### Régression du candidat final 169 — parcours 171

Le binaire `f558a4dc09d0bcfc7c701861e4bbd05d74cc17090ca3d0b8b68d31e825b105c9`
et le WASM `957c3caf1fd143c5f135c7e5af7987ff19574e87e1b329590b08289e5ac2119e`
passent le même parcours complet dans une nouvelle copie isolée. Les cinq tests
natifs du module repassent après les corrections de lint. Les six comparaisons
d'ombres et le triplet du nouveau réglage passent sans assouplir les seuils.
Le processus termine et ferme ; l'archive source reste préservée.

La nouvelle comparaison trouve 412 365 pixels changés, dont 86 219 avec delta
>2, moyenne RGB 0,288288/255. Dans l'eau près du ponton : 23 165 pixels avec
delta >2, moyenne 1,070716/255. Les témoins ciel et sable sec restent à un delta
maximal de 1/255 ; la répétition des pixels et du PNG est exacte. La capture
GPU couvre encore 358 075 pixels en couleur et en profondeur, sans valeur
non finie. L'inspection des images du 171 confirme la portée locale du résultat.
Les métriques 170 et 171 ne constituent pas une comparaison entre candidats
à la même horloge ; chaque triplet a sa propre horloge figée.

[Parcours 171](../../../../.tidewater-integration/studio-tidewater-refraction-capture-runtime-171/fish-stand-studio-smoke-ledger.json),
[audit final](../../../../.tidewater-integration/studio-tidewater-refraction-capture-runtime-171/refraction-capture-verification.json),
[manifeste et 37 entrées sans dérive](../../../../.tidewater-integration/refraction-capture-artifacts-171.json).

Restent ouverts : preuve native GPU de cette passe partagée, cas d'occlusion
distincts, dispersion des caustiques isolée, redimensionnement/jitter en runtime,
performance en mouvement et absence de croissance des ressources après éditions
répétées. Les poissons transparents, rochers/récifs, TAAU, torche, autres
comportements, package final et comparaison à horloge source identique restent
séparés. L'objectif complet est actif ; ce rapport ne clôture aucune gate.
