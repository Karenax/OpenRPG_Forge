# Tidewater — comparaison visuelle et commandes graphiques Studio

La capture publique fournie par le propriétaire paraît plus aboutie que le
playtest Studio : nuages et feuillage mieux intégrés, ombres moins dures et
matières plus nuancées. Ce jugement sur les deux captures n'est pas une mesure
appariée. La [paire village HDR/ACES](2026-09-29-village-postfx-pair.md) sur le
commit source figé établit des luminances moyennes proches mais des différences
de détails, d'ombre et de traitement temporel. La démo publique a évolué depuis
ce commit. Le [README source](https://github.com/dgreenheck/tidewater) décrit
un moteur WebGPU/WGSL propre au projet, sans Three.js.

L'exécutable natif du [paquet 026](2026-09-29-windows-playtest-package-026.md)
possède déjà un panneau `H`/`F10` à quinze commandes, dont les dix effets de
la capture source. Son reset `R` et la persistance d'une valeur Bloom ont été
vérifiés sur un extrait local ; le clic physique de la souris reste non qualifié.

Le panneau de playtest Studio expose maintenant dix commandes qui agissent sur
la présentation sans modifier la carte : adaptation automatique, FXAA,
exposition EV, saturation, contraste, netteté, bloom, vignette, grain photo et
flou de mouvement. Les quatre nouvelles commandes se raccordent aux composants
et passes Bevy du viewport WebGPU ; les six commandes antérieures restent
présentes. Les valeurs par défaut suivent le profil natif. La mémorisation
locale de ce profil a été ajoutée et vérifiée ultérieurement dans le
[candidat 067](2026-09-30-studio-graphics-persistence.md).

L'adoption directe des passes natives d'occlusion, de brume et de flare dans
Studio a été refusée après essais réels. Sur le même candidat de diagnostic,
le parcours complet réussit avec les passes additionnelles neutralisées
(`mask 0`), avec le grain (`mask 1`), et avec grain + flou de mouvement
(`mask 17`). Il reste bloqué au premier tick, viewport noir, avec grain +
brume (`mask 3`), grain + occlusion (`mask 5`), grain + flare (`mask 9`),
grain + occlusion + flare (`mask 13`) et sans flou (`mask 15`). La cause GPU
précise n'est pas établie ; aucune de ces trois commandes n'apparaît comme
curseur actif dans Studio. Lumière rebond et TAA ne sont pas encore raccordés
et qualifiés dans cette vue. Leurs capacités natives restent distinctes.

Le candidat final isolé
`E:/OpenRPG_Forge/.tidewater-integration/studio-tidewater-graphics-candidate-020/forge-studio-candidate.exe`
porte le SHA-256
`85ce4b291bfb46f8755485a63ecdf348171656e286e6481c66444f942b3c5994` ;
son WASM porte
`bc797448c808fe0cb3a2f4dfb1f4339865a8ab65b941af931cd28d7d0719daf3`.
Le reçu local
`E:/OpenRPG_Forge/.tidewater-integration/studio-tidewater-graphics-smoke-029/fish-stand-studio-smoke-ledger.json`
est `passed` : bibliothèque et zone ouvertes, échoppe appliquée/sauvegardée et
rouverte, simulation active, nuages dynamiques, réglage de l'exposition à
`−1 EV` puis reset à `0,40 EV`, modification et reset des curseurs de grain
et de flou, coupure/rétablissement de l'adaptation et du FXAA sans suspendre
la simulation, puis déplacement d'environ 4,38 m sur 304 ticks. La fixture
source est intacte ; aucune erreur navigateur capturée. Le contrôle utilise
CDP pour les commandes DOM et ne qualifie ni le clic souris OS ni l'identité
visuelle avec la version publique du site.

Contrôles de construction : `cargo check` moteur natif, build WASM release,
`wasm-bindgen`, vérification syntaxique et self-test du script de parcours,
formatage ciblé et `git diff --check` réussis. Le candidat est local,
non publié. La carte éditable, l'océan visible dans Studio et la parité
visuelle complète restent des travaux séparés.

Le contrôle Clippy strict de Studio (`-D warnings`) ne passe pas dans cet arbre
de travail partagé : il rencontre d'abord `needless_pass_by_value` dans
`forge_project/zone_authoring/session.rs`, puis, en neutralisant uniquement
ce lint pour poursuivre le diagnostic, douze erreurs préexistantes dans
`forge_assets/tidewater_fish_stand.rs` (documentation, noms de champs,
comparaisons flottantes et complexité). Aucun de ces fichiers n'a été modifié
pour masquer le résultat ; la compilation et le parcours jouable ci-dessus
constituent la validation du candidat graphique.

Mise à jour du 30 septembre : une onzième commande, « Lumière rebond », est
désormais active quand la bibliothèque contient le [paquet source vérifié](2026-09-30-studio-ground-bounce.md).
Le parcours du candidat 052 vérifie 0, 2 et le retour à 1 en Playtest. La
carte est pré-calculée pour le soleil source, et l'effet visuel reste subtil.
Les autres limites d'AO, de brume et de flare Studio décrites ci-dessus restent
ouvertes sur le candidat 052.

Mise à jour suivante : l'[occlusion et le flare Studio](2026-09-30-studio-gtao-flare.md)
fonctionnent dans le Playtest WebGPU après correction de l'usage de la texture
de profondeur. Le panneau expose treize commandes actives, dont les dix effets
visibles dans la capture de référence. Leurs algorithmes et leur intensité
visuelle ne sont pas tous appariés : le flare reste très discret, la brume
marine Studio et la reconstruction temporelle restent ouvertes.

Mise à jour suivante : la [brume marine](2026-09-30-studio-air-haze.md) est
active dans le Playtest. Le panneau expose quatorze commandes, dont les dix
effets montrés par la capture source. Le candidat 058 vérifie le réglage
0 → 1,60 → 1,60, l'image, l'océan, ShoreSim et la marche. L'écart de rendu avec
la démo publique, l'immersion et la chaîne temporelle restent à qualifier.

Mise à jour suivante : le [flou de mouvement avec l'océan optique](2026-09-30-studio-motion-blur-ocean.md)
est maintenant exécuté dans le Playtest WebGPU. Le curseur n'est plus suspendu
sur la carte importée par défaut. Le candidat 066 vérifie 0°, 360° et le
retour à 180° ainsi que la marche, la FFT, ShoreSim et les autres effets.
