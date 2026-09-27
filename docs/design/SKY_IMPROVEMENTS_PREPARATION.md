# Ciel — préparation d'améliorations, sans remplacer le candidat accepté

10 septembre 2026. **Plan préparé, aucun changement de rendu livré dans ce lot.**
Demande propriétaire : préparer des améliorations du ciel en poursuivant Gate 1.
Le ciel accepté reste la référence ; cette étude ne devient pas un nouveau critère
implicite de clôture. Règles réutilisables pour les futures cartes, pas retouche PNG.

## Déjà présent, à conserver

- Atmosphère Bevy, milieu terrestre, soleil et éclairage d'environnement dans
  `crates/forge_engine/src/environment.rs` : ne pas ajouter un second ciel physique.
- Volume de densité Forge 3D R8 de 64³, morphologie déterministe, vent périodique,
  ray marching borné et deux échantillons d'auto-ombrage.
- Budgets de parcours Low/Medium/High/Ultra : **0/12/24/32** pas ; Low désactive
  explicitement l'aperçu nuageux. Ces plafonds ne sont pas à relever par défaut.
- Profil courant : altitude 1500 m, motif horizontal 4000 m, épaisseur dérivée
  480 m, couverture 0,52, opacité 0,85, vent [10,-5] m/s.

## Diagnostic préparatoire, pas une causalité mesurée

Les captures intégrées montrent des masses floues. Le code échantillonne le volume
64³ sans mip ; avec l'échelle actuelle cela représente environ 62,5 m par texel
horizontal. La disparition sous `ray.y <= 0.08` puis le fondu jusqu'à 0.28 sont
des approximations d'horizon explicites. L'éclairage n'a pas de terme de diffusion
directionnelle détaillé. Ces limites suggèrent les expériences ci-dessous ; elles
ne prouvent pas à elles seules quelle modification améliorera les pixels.

## Ordre proposé

1. **Formes et taille apparente** : évaluer des masses plus petites, des ouvertures
   de ciel et des contours érodés sur le volume existant. Modifier la morphologie
   ou l'échelle séparément ; seed fixe, aucune augmentation de résolution initiale.
2. **Volume lumineux** : comparer un éclairage directionnel borné et une approximation
   de diffusion multiple peu coûteuse ; éviter des nuages gris uniformes sans
   éclaircir artificiellement toute la scène. Conserver la même exposition/soleil.
3. **Horizon et cohérence** : adoucir la disparition de la couche en raccordant la
   distance et l'atmosphère existante, puis vérifier les reflets de l'eau et la
   météo. Pas d'invention de profils météo persistants ou d'éditeur Gate 2.

Le passage 64³→128³ multiplierait la mémoire brute par huit (0,25→2 Mio), mais
ne constitue pas une solution automatique au flou. Reprojection temporelle,
nouveau render target réduit et ombres nuageuses au sol sont des extensions
ultérieures à mesurer, pas des changements indispensables à cette préparation.

## Protocole limité et critère d'arrêt

- Dérivé opt-in, référence inchangée ; une variable ou un petit ensemble motivé
  par expérience, au plus deux candidats avant bilan.
- Trois cadrages : horizon, nuages au-dessus de la vallée, eau avec ciel ; mêmes
  caméra, heure, sources et qualité High, puis vérification des budgets des profils.
- Comparaison claire/pluie et court déplacement pour vérifier scintillement et
  stabilité. Un arrêt sur image ne valide pas un gain temporel.
- Mesure A/B du coût avec/sans candidat dans les mêmes conditions ; ni la somme
  des durées RenderDoc ni une vidéo à horloge imposée ne servent de FPS réel.
- Garder le dérivé seulement si le volume et l'horizon sont visiblement améliorés,
  sans erreur shader ni coût disproportionné. Sinon documenter le rejet et garder
  le ciel accepté. Pas de boucle ouverte de retouches esthétiques.

## Références techniques consultées

La documentation Epic décrit volume 3D, parcours borné, approximation de diffusion
multiple et compromis de résolution ; elle recommande notamment de limiter le
nombre d'octaves de diffusion pour les jeux. C'est une référence de méthode, pas
une dépendance Unreal ni une reprise de code ou d'assets :
[Volumetric Clouds](https://dev.epicgames.com/documentation/unreal-engine/volumetric-clouds?application_version=4.27).

La méthode de Bruneton fournit une référence physique et de validation pour
l'atmosphère précalculée ; Forge conserve toutefois son adapter Bevy existant :
[Precomputed Atmospheric Scattering](https://ebruneton.github.io/precomputed_atmospheric_scattering/).
