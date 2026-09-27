# Éclairage de scène transmis aux bâtiments

Le shader de village portait encore en constantes la direction/couleur solaire
et les neuf coefficients SH de l'environnement de la capture. Ces valeurs
empêchaient les bâtiments de suivre un autre éclairage de scène.

`SceneLighting` expose ces paramètres via le binding uniforme 18 du matériau.
La construction vérifie direction unitaire, valeurs finies et couleur solaire
positive ou nulle. Le cube spéculaire et les cartes d'ombres restent fournis
séparément par le renderer. `LightingBindings` transmet maintenant cet uniforme.
Le script `prepare-village-lighting-uniform.py` extrait les coefficients acquis,
remplace les constantes du shader et écrit `village-lighting.json` dans la
fixture vérifiée. Les fixtures historiques gardent leurs paramètres acquis,
embarqués uniquement dans le prototype de qualification.

Le paquet `building-package-003` conserve les 23 identités et leurs géométries ;
son shader et son manifeste changent. Pin du paquet :
`a53b6987563b0b0d5bb5b9113e1c3f27142baf53658a9e1a78338854f99599e6`.
Le lecteur synchrone/asynchrone et ses contrôles négatifs réussissent sur ce
paquet. Clippy strict ciblé du prototype réussit également.

Deux fixtures sont préparées pour la validation GPU :
`package-only-buildings-scene-002` conserve les valeurs acquises et
`package-only-buildings-no-sun-001` met seulement la couleur solaire des
bâtiments à zéro. Elles conservent les mêmes géométries et occlusions.
Compilation native réussie (8 min 20 s). Les deux captures ont réussi :

- `native-village-lighting-uniform-001.png` diffère de la capture précédente
  sur deux pixels, aux coordonnées (x=769,y=480) et (x=770,y=500), d'au plus
  un niveau par canal. Les deux buffers d'ombres sont identiques. L'image a
  été inspectée ; l'identité bit à bit de l'image n'est pas revendiquée.
  Hash : `243ddf9659f95afea4bbd978b2b47006983d052fe3d841958c6f45182a3c4e78`.
- `native-village-lighting-no-sun-001.png` confirme l'effet de la couleur
  solaire : 142840 pixels changés, écart maximal 90/255, les deux buffers
  d'ombres toujours identiques. Les surfaces du village (bâtiments, ponton,
  accessoires utilisant ces matériaux) s'assombrissent comme attendu.
  Hash : `e7f5dc484c586ffd42f09487738ad0bd65b4e68821d90b07681761f88b146df5`.

Les rapports `village-lighting-uniform-proof-001.json` et
`village-lighting-no-sun-proof-001.json` consignent les métriques. Les logs ne
contiennent aucune erreur. Ce sont des variations contrôlées de l'éclairage
des surfaces de village ; terrain, eau et cartes d'ombres gardent leur soleil
initial pour isoler le test. Cela ne raccorde pas encore l'horloge, les lumières
Studio ni l'environnement dynamique global.

Contrôle indépendant de l'ambiance diffuse : la troisième fixture ne met que
les neuf coefficients SH à zéro, tout en conservant le soleil acquis et le
cube spéculaire. `native-village-lighting-no-diffuse-001.png` montre les faces
à l'ombre nettement assombries ; 199321 pixels changés, écart maximal 113/255.
Les deux buffers d'ombres restent identiques et le log est sans erreur.
Hash : `db25f033ff765ed2733ab8152e8c10402ca9bbc3f747c6c41c3dee563a8f6986`.
Métriques dans `village-lighting-no-diffuse-proof-001.json`. Les trois captures
utilisent le même binaire et le même shader de paquet : seuls les paramètres
de scène changent. Aucun cycle temporel continu n'a été testé par ces captures.
