# Tidewater — adaptation de l'exposition dans la promenade native

Référence figée : `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`,
`src/post/PostFX.js`. La source mesure la luminance moyenne pondérée vers le
centre, vise 0,25, ne compense les scènes sombres qu'à 80 %, borne son
multiplicateur à `[0,6 ; 6,0]` et adapte à 1,6/1,1 s⁻¹. La promenade Bevy
utilise la passe `AutoExposure` de Bevy 0.19 (histogramme GPU), une courbe de
compensation qui reproduit ces bornes et un masque R8 64² calculé avec la
pondération centrale de la source. Le mode est activé par défaut et peut être
coupé dans `H` / `F10` sans modifier la compensation EV manuelle.

Deux profils isolés sur E: ne diffèrent que par `auto_exposure = 1` ou `0`.
Captures de la même pose de départ :

- activé sans masque : `E:\OpenRPG_Forge\.tidewater-integration\graphics-ab-auto-on-window.png` ;
- fixe : `E:\OpenRPG_Forge\.tidewater-integration\graphics-ab-auto-off-window.png` ;
- fixe répété : `E:\OpenRPG_Forge\.tidewater-integration\graphics-ab-auto-off-repeat.png` ;
- activé avec masque : `E:\OpenRPG_Forge\.tidewater-integration\graphics-mask-auto-on-window.png`.

Moyenne de luminance 8 bits par régions statiques, échantillonnage tous les
10 pixels : ciel 155,09 avec masque contre 149,16 fixe ; maison 92,43 contre
88,08 ; sable 148,73 contre 142,33. Deux captures du mode fixe à quelques
secondes d'intervalle ne divergent que de 0,9 à 1,2 niveau sur ces régions.
L'effet est donc visible au-delà de la variation ordinaire de la scène.
Le masque change peu cette pose ; sa valeur se jugera surtout pendant les
transitions entre zones éclairées et ombragées. Les captures ne sont pas
appariées image par image à la référence WebGPU.

Validation : `cargo check` et Clippy strict de l'exemple Tidewater PASS,
compilation native PASS, sessions A/B interactives avec fermeture code 0 et
aucune erreur de validation GPU dans les sorties. L'exécution WebGPU en
navigateur et l'étude des transitions temporelles restent à qualifier.

Distribution locale : `OpenRPG-Forge-Aube-Tidewater-Win64-20260928-007.zip`
sur E:, 2 505 entrées, 421 641 978 octets. Le lanceur Aube répond
`FORGE_LAUNCHER_AUBE_FRONTEND_READY`, le SHA-256 du runtime empaqueté est
`6af272b2960bcbe59ecbacb78ccfeab69e0fbf3c2fb2798f7356cdfdf0b87db9`,
identique au binaire compilé. Le parcours `--walk-smoke` depuis le dossier
distribué a produit `TIDEWATER_WALK_PASS` ; une promenade interactive sous un
profil neuf a affiché `Adaptation Auto` et +1,0 EV dans
`E:\OpenRPG_Forge\.tidewater-integration\graphics-package-007-menu.png`.
Un clic réel sur « − » a fait passer le menu à `Fixe` et écrit
`auto_exposure = 0` dans le profil isolé ; le clic sur « + » l'a remis à
`Auto` et à `1`. Capture du mode fixe :
`E:\OpenRPG_Forge\.tidewater-integration\graphics-package-007-fixed-menu.png`.
