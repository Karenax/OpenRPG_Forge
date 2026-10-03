# Tidewater — flou de mouvement avec océan optique dans Studio

Le curseur « Flou de mouvement » existait dans le Playtest Studio, mais la
passe était suspendue lorsque l'océan optique était actif, ce qui est le cas
par défaut pour la carte importée. Le curseur commande maintenant le composant
et la passe Bevy en présence de l'eau : 0° retire l'effet, 360° l'active, et
« Réinitialiser les effets » revient à 180°. Le statut Playtest indique la
valeur effectivement portée par la caméra.

Le shader Bevy 0.19 du flou emploie quatre `textureSample` à LOD implicite.
Deux se trouvent dans une boucle à contrôle de flux divergent ; WebGPU refuse
alors le pipeline `motion_blur_pipeline` et le Playtest reste noir au premier
tick. Le correctif WebGPU du frontend reconnaît exactement ces quatre appels
dans le module composé et les convertit en `textureSampleLevel(..., 0.0)`.
Les textures d'entrée n'ont pas de chaîne mip ; l'échantillonnage de niveau 0
préserve donc celui attendu. Une modification de la forme du shader est
signalée et n'est pas réécrite à l'aveugle.

Le [parcours isolé du candidat 066](../../../../.tidewater-integration/studio-tidewater-motion-blur-smoke-066/fish-stand-studio-smoke-ledger.json)
est `passed`. Il ouvre et édite la zone, place l'échoppe, sauvegarde et rouvre,
entre en Playtest, vérifie les réglages 0° → 360° → 180°, puis marche jusqu'au
tick 909. La FFT atteint l'étape 486, ShoreSim aussi, les nuages soumettent
367 images et le navigateur ne relève aucune erreur. Le parcours vérifie
également l'océan optique, la lumière rebond, l'occlusion, le flare et la brume.
Le candidat local est
`E:/OpenRPG_Forge/.tidewater-integration/studio-tidewater-motion-blur-candidate-066/forge-studio-candidate.exe`,
SHA-256 `61bdee9abc8de58e2b3cce62f62772241b086c4b1b901c167a5e3f2c9eed423f` ;
le WASM release porte le SHA-256
`bcb2e69900a924744b93fcebf570db899b68a62c63917178ef817f0abc8a5530`.

Ce test démontre une passe active et un Playtest jouable avec l'océan. Il ne
mesure pas la ressemblance perceptuelle du flou à celui de la démo publique,
ni sa qualité pendant des rotations rapides de caméra. La comparaison visuelle
appariée, l'immersion et la chaîne temporelle restent ouvertes. Ce candidat
local n'est pas publié.
