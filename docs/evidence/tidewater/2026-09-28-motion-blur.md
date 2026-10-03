# Tidewater — flou de mouvement jouable

La référence `src/post/MotionBlur.js` emploie un obturateur de 0,5 image pour
180°. La promenade native expose maintenant « Flou de mouvement » dans
`H` / `F10`, de 0 à 360° par pas de 45°, avec 180° par défaut. La valeur est
sauvegardée dans `graphics.json` et transmise à la passe `MotionBlur` de Bevy
0.19 (`shutter_angle = degrés / 360`, 7 échantillons). La passe utilise les
vecteurs de mouvement GPU ; leur prépasse est maintenue lorsque le TAA est
désactivé, et retirée seulement quand TAA et flou sont tous deux coupés.

Clippy de l'exemple Tidewater passe avec `-D warnings`, après exclusion des
deux règles déjà déclenchées par `forge_project` et `walk.rs` hors de ce lot
(`needless_pass_by_value`, `too_many_lines`). La compilation native passe.
Le parcours `--walk-smoke` avec flou 180° depuis
`preview-scene-bundle-002` sort avec le code 0, `pass=true` et une capture ;
aucune erreur de validation GPU ou panique n'apparaît dans le journal. Une
session Windows interactive ouvre le menu, affiche 180° et se ferme avec le
code 0 ; capture `graphics-motion-010-default-window.png`.
Depuis le paquet `010` extrait, le même parcours passe. Un clic réel sur
« + » modifie 180° en 225° et sauvegarde `motion_blur_degrees = 225` dans un
profil neuf. L'archive contient 2 508 entrées ZIP sans erreur CRC, et le
SHA-256 de l'exécutable embarqué est identique à celui de la compilation locale.
Le lanceur répond `FORGE_LAUNCHER_AUBE_FRONTEND_READY`. SHA-256 du ZIP :
`c1f09e831bb2943954335de9d9771d33e00dd9fcbd3b6b77b07505a504c93db6` ;
SHA-256 du runtime :
`4e30a613b323e20292065f3e93aa7510067c13a755a0beb0ab009d3200730feb`.
Le collecteur de notices Rust signale encore 42 textes de licence manquants :
ce paquet reste un candidat local, sans qualification de distribution externe.

Cette passe est effective dans la promenade, mais ne reproduit pas encore
pixel pour pixel la passe du moteur WebGPU, notamment sur l'eau transparente
qui n'écrit pas tous les vecteurs de mouvement. Une comparaison d'images en
mouvement est encore nécessaire avant de qualifier sa fidélité visuelle.
