# Tidewater — passerelles éditables et jouables dans Studio

Les trois passerelles du paquet source vérifié
`2d8270d51c0666b256618740a68069e79f6c1dda6c254062194717c937c734e6`
peuvent être ajoutées comme placements persistants dans la zone Tidewater. Chaque
racine conserve son identifiant, son repère édité et la liste exacte de ses
colliders Rapier ; le contrôle commun des placements valide ces collisions avant
de démarrer la simulation. Le rendu et les colliders restent issus du paquet
source épinglé. L'édition porte ici sur les placements des segments, pas sur la
géométrie des planches ou la spline de la passerelle.

Le test Rust `pinned_boardwalk_prepares_exact_walkable_source` a réussi avec le
paquet épinglé : la passerelle déplacée conserve ses trois parties visuelles et
47 volumes de collision rattachés à son placement. Le build WebGPU release,
`wasm-bindgen` et le candidat Windows local ont réussi. Le candidat est
`E:/OpenRPG_Forge/.tidewater-integration/studio-tidewater-boardwalk-edit-candidate-079/forge-studio-candidate.exe`,
SHA-256 `92212125b0faa2da42b074bfeeb3ea3cec58257ad1b299654c571c63f17f2b0a`.

Le [reçu du parcours Studio](../../../../.tidewater-integration/studio-tidewater-boardwalk-edit-smoke-080/boardwalk-studio-smoke-ledger.json)
est `passed`. Il part d'une fixture de 23 placements, ajoute les trois
passerelles, déplace la première de X = 55 m à X = 57 m, sauvegarde 26 placements,
ferme puis rouvre la zone et constate que la position éditée est conservée. La
fixture source reste inchangée. Le [Playtest](../../../../.tidewater-integration/studio-tidewater-boardwalk-edit-smoke-080/boardwalk-playtest.png)
affiche une simulation prête. Une commande de marche pendant 900 ms déplace le
personnage d'environ 3,91 m en Z ; la simulation reste active, au sol, avec
trois contacts détectés et un déplacement horizontal résolu. La
[capture après marche](../../../../.tidewater-integration/studio-tidewater-boardwalk-edit-smoke-080/boardwalk-movement.png)
montre la passerelle et le village dans la scène jouable.

La comparaison visuelle avec Three.js documentée dans
[l'étape précédente](2026-09-30-studio-boardwalk-playtest.md) demeure partielle :
les accessoires, certaines ombres et l'alignement temporel des effets ne sont
pas encore identiques. Cette preuve qualifie l'édition et le jeu sur les
passerelles ; elle ne qualifie pas une parité graphique complète ni une
distribution publique.
