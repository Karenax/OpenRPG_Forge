# Tidewater — ombres de la végétation proche dans le Playtest Studio

Le Studio ne transmettait auparavant que le relief et les bâtiments à sa
passe d'ombres importée. Les plantes visibles avaient un matériau récepteur,
mais n'étaient pas des émetteurs dans cette passe. Le candidat local 096 ajoute
les cinq groupes émetteurs de la source épinglée : palmiers, bananiers,
monsteras, larges feuillages et arbres de canopée. Le sous-bois et l'herbe
restent exclus, comme dans les indicateurs `castShadow` de la source pour ces
groupes. Les ombres prennent les mêmes instances validées que le rendu visible,
y compris les décalages enregistrés dans le document de zone.

La sélection des émetteurs suit les rayons de requête des niveaux proches du
paquet végétal, avec une limite par groupe : environ 130 m pour palmiers et
bananiers, 115 m pour monstera et larges feuillages, 75 m pour la canopée.
Les végétaux lointains ne remplissent donc pas la dernière cascade de 400 m.
Les masques des feuilles sont utilisés dans la carte de profondeur ; il ne
s'agit pas de simples taches circulaires sous les troncs.

La [capture du village avec ombres](../../../../.tidewater-integration/studio-tidewater-shadows-village-096/tidewater-studio-village-calibrated-040.png)
montre les silhouettes des feuillages sur la pelouse et le chemin près des
maisons. Elle se compare à la [capture sans ces émetteurs](../../../../.tidewater-integration/studio-tidewater-village-pier-pair-094/tidewater-studio-village-calibrated-040.png)
et à la [source épinglée](../../../../.tidewater-integration/baseline-003/village.png).
Le [reçu de capture](../../../../.tidewater-integration/studio-tidewater-shadows-village-096/tidewater-studio-village-reference.json)
confirme la caméra, la résolution, les effets et l'océan actifs. Son exécutable
isolé a pour SHA-256
`4e61bec1158bb863c965e56a38fe6b157a80d4ed62601702dabc0a6dea36532f`.

La [comparaison chiffrée](../../../../.tidewater-integration/studio-tidewater-shadows-village-096/paired-village.json)
donne une erreur absolue moyenne de luminance de 13,40/255 pour l'image entière,
contre 13,10/255 avant ces ombres. Les petites différences de pose animée et de
temps entre les captures interdisent d'attribuer ce chiffre aux seules ombres.
Il ne prouve pas une amélioration globale de fidélité. L'exposition Studio
reste plus élevée que celle de la source (118,42 contre 109,23 de luminance
moyenne).

Le test `edited_near_tree_is_selected_as_a_shadow_caster` passe : un
arbre situé dans le rayon de 50 m entre dans le lot, à 400 m il en sort, et il réapparaît après
déplacement de la caméra. La compilation WASM et la construction release
passent. Le [parcours Playtest](../../../../.tidewater-integration/studio-tidewater-shadows-smoke-096/boardwalk-studio-smoke-ledger.json)
est `passed` : réouverture de zone, 32 objets placés, personnage déplacé
d'environ 3,14 m, FFT océan et ShoreSim actifs.

Limites : la capture ne prouve pas la synchronisation du vent entre les feuilles
et leurs ombres, ni la stabilité des FPS lors d'une longue traversée de forêt.
L'herbe proche ne projette toujours pas d'ombre géométrique individuelle ; elle
reçoit les ombres. Les différences de lumière, d'exposition et de traitement
temporel avec la démo source restent à qualifier avant de parler de parité.
