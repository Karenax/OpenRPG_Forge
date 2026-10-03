# Tidewater — échoppe source dans la carte Studio

L'échoppe FishStand du paquet vérifié
`c739c737914261991fd9aba21c4372e7e9cafa31527f7a4139001a4db8e9b335`
est désormais projetée à son repère source `(49,9 ; terrain ; -74,6)`, avec
orientation `1,45` radian, lorsque la zone ne contient aucun placement
FishStand. Sa hauteur est échantillonnée sur le relief importé ; une nouvelle
projection du relief recalcule cette hauteur. Le rendu associe le stand, Joe et
les cinq poissons du paquet. Le Playtest attend Joe et les cinq collisions
Rapier enregistrées. Cette projection source ne modifie pas le document de zone.

Le bouton « Rendre l'échoppe Tidewater source éditable » crée une proposition
ordinaire, à accepter puis sauvegarder, qui remplace la projection source par
un placement sélectionnable. Le [parcours source](../../../../.tidewater-integration/studio-tidewater-fish-stand-source-smoke-085/boardwalk-studio-smoke-ledger.json)
est `passed` : 23 placements initiaux, aucune échoppe ajoutée au document,
Playtest actif après import des trois passerelles et marche résolue avec
collisions. La [capture vers la mer](../../../../.tidewater-integration/studio-tidewater-fish-stand-source-smoke-085/boardwalk-source-stand.png)
montre l'échoppe et Joe dans cette scène sans placement FishStand.

Le [parcours de conversion](../../../../.tidewater-integration/studio-tidewater-fish-stand-edit-smoke-086/boardwalk-studio-smoke-ledger.json)
est aussi `passed` : l'échoppe source est acceptée comme 24e placement, les
trois passerelles portent la zone à 27 placements, puis la sauvegarde, la
réouverture et le Playtest réussissent. L'archive conserve l'échoppe à
`(49,9000 ; 2,3006 ; -74,6000)` m, avec le paquet épinglé et le quaternion de
`1,45` radian autour de Y. La [capture après conversion](../../../../.tidewater-integration/studio-tidewater-fish-stand-edit-smoke-086/boardwalk-source-stand.png)
montre une seule échoppe au même emplacement. Les fixtures sources sont
restées intactes dans les deux parcours.

Le build WebGPU release, `wasm-bindgen` et le candidat Windows local ont
réussi. Exécutable :
`E:/OpenRPG_Forge/.tidewater-integration/studio-tidewater-fish-stand-source-candidate-084/forge-studio-candidate.exe`,
SHA-256 `410f8e3095163a989de389357263647c00b1b836a04c447530f37eef7751195c`.
Le candidat reste local. La mise à jour de la hauteur après une retouche de
relief n'a pas fait l'objet d'un geste UI automatisé spécifique. Le matériau
de l'échoppe et Joe restent plus sombres que dans certains cadrages source,
et le quai, son arche et ses accessoires proches restent absents de la scène
Studio ; cette étape ne qualifie pas une parité visuelle complète.
