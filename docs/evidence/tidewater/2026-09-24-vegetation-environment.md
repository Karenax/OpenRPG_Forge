# Tidewater — éclairage du feuillage par le ciel acquis

Incrément local sur source `1438b1abfcaee3267092b75573014f4d9b4a983c` intacte.
Il raccorde l'environnement diffus/spéculaire à la végétation native, sans
clôturer la fidélité de scène, TW-006, TW-009 ou TW-010.

## Acquisition et raccordement

`export-environment.mjs` exécute les programmes originaux Atmosphere,
SkyProClouds, Sky et Environment pendant 120 pas de 1/60 s, à la caméra et
à l'heure de référence. Les deux fichiers de bruit originaux sont servis au
chargeur et hashés. Soleil/irradiance proviennent du calcul atmosphérique,
avec les formules App ; le premier readback est explicitement attendu.
Le vent des nuages reste celui du source (7 m/s, direction normalisée
(0.35, 0.94)). Les végétaux du diagnostic gardent leur vent de contrôle
antérieur (6 m/s, (1, 0)) : la temporalité de la scène complète reste ouverte.

Sont acquis 9 coefficients SH vec4 f32 et les six faces × six niveaux
128 à 4 px du cube spéculaire rgba16float. Une copie textureLoad/store
rend le cube lisible sans modifier ses flags source. Deux acquisitions
indépendantes finales ont les mêmes 37 fichiers et le même manifeste.

Le natif charge le cube en ordre face puis mip (1 048 320 octets), vérifie sa
taille et refuse NaN/Inf. Les SH sont injectés dans le programme vérifié de
la fixture. Le shader reprend l'évaluation SH, DFGApprox et multiscatter
source, avec occlusion du matériau et direction/couleur réelles du soleil.
Le cube est partagé entre matériaux. Les anciennes fixtures reçoivent un
cube neutre sans changer leur programme.

L'oracle indépendant utilise le shadeSurface et les hooks Environment
originaux, alimentés par les mêmes données acquises. Il qualifie leur
consommation native ; il ne prouve pas un port natif de la génération du ciel.

## Comparaison isolée, 1920×1080 sRGB

| Mesure sur l'union des pixels non noirs | Village | Forêt |
| --- | ---: | ---: |
| Pixels de l'union | 583646 | 1321336 |
| RGB exactement identiques | 572164 | 833118 |
| Erreur moyenne par canal /255 | 0.056246 | 1.011835 |
| P99 du maximum par canal /255 | 1 | 61 |
| Maximum /255 | 255 | 200 |
| Désaccord noir/non-noir | 152 | 2794 |

Images source/natif inspectées. Les écarts de forêt persistent ; pas de
revendication d'identité. Le masque non noir inclut l'effet de l'éclairage,
il n'est pas un verdict indépendant de couverture géométrique.

## Preuves locales

Racine : `E:\OpenRPG_Forge\.tidewater-integration`.

- Exports définitifs village : `environment-village-003` et `004` ;
  `environment-reexport.json` : 37 fichiers, aucun écart, manifeste identique.
  Exports 001/002 remplacés (vent diagnostique et compteur de frame anciens).
- Fixtures : `environment-lighting-004`, `environment-lighting-canopy-001`.
- Village : `source-environment-final.png`, `native-environment-final.png`,
  `environment-comparison-final.json`. Image native SHA-256
  `a50afea5754b6940f76cbcbdd17ee90114b07deb58b76819640e0d5e593b1129`.
- Forêt : `source-environment-canopy-001.png`, `native-environment-canopy-001.png`,
  `environment-canopy-comparison-001.json`.
- Build et Clippy strict : `environment-lighting-build-002.log`,
  `environment-lighting-clippy-002.log`, réussis. Binaire final SHA-256
  `6b427dd981f80023cc51fd36f672b1f1056b8c16d7146b8afd7a82b246e1536e`.
- Régression lumière unitaire : `environment-direct-regression.png`, identique
  au précédent `217fb1d9eb88e5750ef6d9631872e783bed0beba7ef5f0e3dec08a823f7733cd`.
- Mode environnement sans cube : code 1 avant GPU, `environment-missing.log`.

Premiers essais corrigés : initialisation headless omise, upload de première
face traitée comme cube complet par le chargement différé source, extraction
de signature dans un commentaire au lieu de la fonction WGSL. Les essais
échoués sont conservés ; seules les captures réussies ci-dessus sont utilisées.

## Limites restantes

L'environnement est figé, calculé depuis un état froid explicite. Il ne rejoue
pas les précompilations ni les readbacks asynchrones de la baseline App.
Le ciel visible, ses nuages dynamiques, les ombres, rebonds, lumières locales,
exposition et traitements temporels restent à porter/raccorder. Terrain et
village conservent leur éclairage provisoire. Pas de validation Studio,
navigateur, performance, packaging final ou publication Git.
