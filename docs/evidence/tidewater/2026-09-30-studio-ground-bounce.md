# Tidewater — lumière rebond source dans Studio

Le Playtest et la vue d'édition Studio utilisent maintenant la carte de lumière
rebond filtrée du commit Tidewater figé
`1438b1abfcaee3267092b75573014f4d9b4a983c`. Le paquet d'entrée
`forge.tidewater.ground-bounce-input-package` contient le descripteur, la carte
RGBA16F 512 × 512 et la notice MIT ; son manifeste porte le SHA-256
`4002ee7219943235fc725a263fbb798d9057edd25d9c8d6fbed53eadd8f86e8d`.
La bibliothèque vérifie ce pin, charge la carte en mémoire GPU Bevy et refuse
un soleil importé différent de celui qui a servi au calcul source. Le panneau
graphique Playtest expose « Lumière rebond » de 0 à 2 quand ce paquet est chargé.
Le défaut est 1 ; le curseur ne change pas le document de carte.

Un premier parcours sur le candidat 051 a échoué fermé après le chargement de
la carte et l'entrée en jeu : le curseur était masqué lors du premier rendu
Leptos, avant la disponibilité de la bibliothèque, puis ne réapparaissait pas.
Le rendu du curseur et son activation suivent désormais le statut réactif de
la bibliothèque. Le [reçu du candidat 052](../../../../.tidewater-integration/studio-tidewater-bounce-smoke-052/fish-stand-studio-smoke-ledger.json)
est `passed` : édition, sauvegarde, réouverture, entrée en jeu, changement du
rebond à 0 et 2 puis retour à 1, FFT 131 → 140, ShoreSim 157 → 165 et marche
d'environ 3,86 m. La fixture source est intacte et aucune erreur navigateur
n'a été capturée. L'exécutable isolé est
`E:/OpenRPG_Forge/.tidewater-integration/studio-tidewater-bounce-candidate-052/forge-studio-candidate.exe`,
SHA-256 `e264165f15ca2b30201fd5483efdde87b0252aa5a464aade6bcdea30e4b34c3c` ;
le WASM porte `14683ec9f6e1db918e8c4526401a3c9869dd5a69d132e394c1e077057ae6b64e`.

Les [captures à 0](../../../../.tidewater-integration/studio-tidewater-bounce-smoke-052/fish-stand-bounce-0.png)
et [à 2](../../../../.tidewater-integration/studio-tidewater-bounce-smoke-052/fish-stand-bounce-2.png)
sont très proches à l'œil ; le statut confirme que le matériau reçoit les
deux intensités, mais ces images séparées dans le temps ne mesurent pas l'effet
isolé au pixel près. La carte de rebond source est elle-même subtile. La
comparaison avec la démo publique reste défavorable à Studio pour la douceur
des ombres, le feuillage, les nuages et l'atmosphère ; les vues et leurs heures
ne sont pas appariées. La carte est calculée pour un soleil fixe et n'est pas
recalculée après une retouche du relief ou un changement d'éclairage. À ce
stade du candidat 052, AO, brume, flare et rendu temporel Studio restaient à
raccorder ; [AO et flare](2026-09-30-studio-gtao-flare.md) ont ensuite été
intégrés au Playtest, avec leurs limites visuelles propres.

Validation : test unitaire du chargeur, build WASM release, création de
l'exécutable isolé, self-test du parcours, formatage ciblé et `git diff --check`
réussis. Le `cargo fmt --all -- --check` global est bloqué par un fichier
`terrain_ao.rs` manquant dans un exemple hors de ce changement ; les fichiers
modifiés passent `rustfmt --check`. Ce candidat est local et non publié.
