# Calcul atmosphérique natif — première comparaison

`tidewater_atmosphere_probe` exécute sur le device Bevy les quatre kernels
WGSL originaux : transmission 256×64, diffusion multiple 32×32, luminance
du ciel 192×108 et intégration de l'irradiance. Les trois textures RGBA16Float
et le buffer d'irradiance sont entièrement produits côté natif. Les sorties
source ne sont pas chargées comme entrées de remplacement.

Les paramètres sont construits en Rust depuis la position verticale et
la direction du soleil : quantification de hauteur par pas de 2 m sous
100 m, puis de 2 %, altitude planétaire, coefficients de diffusion et
illumination source. Les 64 octets de paramètres correspondent exactement
à la référence pour les deux hauteurs testées : 6360,014 km et
6360,436769929756 km avant conversion float32.

L'outil `export-atmosphere.mjs` exécute les kernels d'un checkout propre et
épinglé. Il enregistre programmes, paramètres, sorties et hashes. Une copie
GPU identité permet de lire les textures source dépourvues de COPY_SRC.
Le premier essai d'acquisition a échoué faute d'enregistrement du bloc Frame ;
l'import correspondant a été ajouté avant création de l'atmosphère.

## Résultats et anomalie ouverte

Deux acquisitions source successives sont identiques, ainsi que deux
exécutions natives Vulkan. Le même exécutable a aussi été exécuté en Dx12
sur la même Radeon RX 7800 XT, ce qui change les écarts mesurés.

| Sortie, caméra village | Maximum absolu Vulkan | Maximum absolu Dx12 |
|---|---:|---:|
| Transmission | 1,0 | 0,00048828125 |
| Diffusion multiple | 0,0000762939453 | 0,00000762939453 |
| Luminance du ciel | 0,018310546875 | 0,0009765625 |
| Irradiance / transmission solaire / horizon | 0,000127077103 | 0,000026345253 |

Les 94 pixels de transmission dont l'erreur dépasse 0,01 sont tous sur la
dernière ligne (y=63), à la limite supérieure de l'atmosphère. Plusieurs
valeurs natives Vulkan restent à 1 alors que la référence est atténuée.
Cet écart est reproductible et disparaît sous cette forme importante en
Dx12. Le changement de backend circonscrit le problème à la chaîne
graphique, mais ne prouve pas encore quelle opération numérique en est
responsable. Il reste à diagnostiquer/corriger ; ce n'est pas une parité
atmosphérique acquise ni une raison de déclarer Vulkan qualifié.

La caméra élevée donne, sous Vulkan, une erreur maximale de luminance
0,0126953125 et une erreur maximale d'irradiance 0,000088989735. Les LUTs
statiques sont identiques à celles de la caméra basse, comme attendu.
Les rapports conservent les nombres de scalaires différents, percentiles,
hashes et valeurs d'irradiance complets.

## Vérifications et preuves

Racine `E:\OpenRPG_Forge\.tidewater-integration` :

- `atmosphere-source-002`, `atmosphere-source-003` : acquisitions identiques.
- `atmosphere-island-source-001` : caméra élevée.
- `native-atmosphere-001`, `native-atmosphere-002` : calcul Vulkan répété.
- `native-atmosphere-island-001`, `native-atmosphere-dx12-001`.
- `atmosphere-comparison-001.json`, `atmosphere-island-comparison-001.json`,
  `atmosphere-dx12-comparison-001.json`.
- `atmosphere-regression-001.json` : répétitions, localisation des erreurs
  fortes et refus d'une direction solaire nulle avant GPU, sans sortie créée.
- `atmosphere-build-002.log`, `atmosphere-clippy-002.log` : réussis.
- Journaux `native-atmosphere-002.log` et `native-atmosphere-dx12-001.log` :
  backend et adaptateur explicitement enregistrés.
- Binaire SHA-256
  `841abf77e1e685182fef97fbfabe3372db93a9d1bdae27343b1f13c2e582e4fc`.

## Portée

Ce calcul reste indépendant du prototype de scène : le ciel visible, les
disques solaire/lunaire, les étoiles, les nuages, la mise à jour interactive
et le remplacement du cube d'environnement acquis restent à raccorder.
L'acquisition actuelle refuse la nuit, car la direction de lumière de son
entrée environnement serait alors celle de la lune plutôt que du soleil
réel. Les variantes nocturnes et WebGPU navigateur ne sont pas qualifiées.
Aucun verdict de reproduction complète ni publication.
