# Tidewater — occlusion et flare dans le Playtest Studio WebGPU

Les passes natives d'occlusion GTAO et de flare bloquaient le Playtest Studio
sur son premier tick, avec un viewport noir. Le point commun était leur lecture
de `ViewDepthTexture`. La caméra Playtest ne demandait `TEXTURE_BINDING` que
par effet indirect du flou de mouvement ; la voie d'océan optique suspend ce
flou et ne demandait que `COPY_SRC` pour la profondeur. Le Playtest demande
maintenant explicitement `TEXTURE_BINDING` avant que Bevy alloue la profondeur,
avec `Msaa::Off`, et conserve `COPY_SRC` quand l'océan optique en a besoin.

Le candidat isolé 053, AO actif sans curseur, a passé l'édition/réouverture,
l'entrée en jeu, FFT, ShoreSim et la marche avec un viewport visible. Le
candidat 054 ajoute le réglage « Occlusion ambiante » de 0 à 2 (défaut 1) ;
son [reçu](../../../../.tidewater-integration/studio-tidewater-gtao-smoke-054/fish-stand-studio-smoke-ledger.json)
est `passed`, avec retour à la valeur par défaut. Les [captures AO 0](../../../../.tidewater-integration/studio-tidewater-gtao-smoke-054/fish-stand-ao-0.png)
et [AO 2](../../../../.tidewater-integration/studio-tidewater-gtao-smoke-054/fish-stand-ao-2.png)
montrent un assombrissement net du relief et des bâtiments. Dans des rectangles
fixes échantillonnés tous les deux pixels, la luminance sRGB moyenne de la
colline passe de 60,19 à 42,60 et celle des maisons de 122,25 à 109,13 ; le
ciel passe de 115,23 à 115,07. Les nuages continuent entre prises, ce qui
interdit d'attribuer chaque pixel modifié à l'AO, mais la différence ciblée
relief/maisons contre ciel confirme un effet matériel important. La force 2
est volontairement une extrémité du curseur, pas une valeur recommandée.

Le candidat 055 ajoute la passe et le réglage « Flare solaire » de 0 à 2
(défaut 1). Le [parcours combiné](../../../../.tidewater-integration/studio-tidewater-flare-smoke-055/fish-stand-studio-smoke-ledger.json)
est `passed` : AO, flare et lumière rebond réglés à 0 puis 2 et réinitialisés,
FFT 131 → 139, ShoreSim 156 → 164, marche au tick 719, source inchangée et
aucune erreur navigateur capturée. Exécutable local :
`E:/OpenRPG_Forge/.tidewater-integration/studio-tidewater-flare-candidate-055/forge-studio-candidate.exe`,
SHA-256 `2a3e09ea79ae7626c1f6bf13f3cb82a6b89d535fe99d4517ac5b90b34edba501` ;
WASM `256abb3d38193333fae83011e006e30fcd85938b3bdf4cca79cc8051646a82ed`.

La première paire de flare regardait à l'opposé du soleil et ne valait pas
preuve visuelle. Une seconde prise a atteint le cap ouest et montre le soleil
dans le champ. Le [parcours de cette paire](../../../../.tidewater-integration/studio-tidewater-flare-sun-smoke-057/fish-stand-studio-smoke-ledger.json)
passe également ; les [captures à 0](../../../../.tidewater-integration/studio-tidewater-flare-sun-smoke-057/fish-stand-flare-sun-0.png)
et [à 2](../../../../.tidewater-integration/studio-tidewater-flare-sun-smoke-057/fish-stand-flare-sun-2.png)
montrent un effet très discret. La zone autour du soleil (x 780–940,
y 620–760 dans les PNG de 1920 × 1080) gagne 0,168 niveau de luminance sRGB
en moyenne ; le ciel entier gagne 0,114. Les nuages et l'adaptation d'exposition
continuent entre les prises. La stabilité GPU et la commande sont vérifiées,
mais cette paire ne mesure pas isolément la contribution du flare et ne prouve
pas la parité avec les artefacts optiques plus visibles de la référence.

Les deux passes restent après la chaîne de post-traitement Bevy et ne
reproduisent pas encore toute la composition HDR et temporelle de Tidewater.
La [brume marine Studio](2026-09-30-studio-air-haze.md) a ensuite été raccordée
et vérifiée sur le candidat 058. Les rayons solaires et la comparaison source/Forge à
caméra, heure, météo et exposition appariées restent ouverts. Compilation
WASM release, `wasm-bindgen`, self-test du parcours, formatage ciblé et
parcours WebGPU réel réussis. Aucun candidat n'a été publié.
