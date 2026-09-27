# Rivage simulé raccordé au rendu natif

**Correction ultérieure :** le matériau CDLOD affiché conservait une copie
des entrées provisoires du terrain. Le raccordement et les métriques corrigés
sont documentés dans [la preuve suivante](2026-09-24-shore-lod-material.md).
Les résultats ci-dessous conservent leur valeur historique avant correction.

Le matériau d'eau utilise maintenant ShoreSim et SurfFoam ; le terrain utilise
le hook original de sable mouillé et de résidus. Tous deux lisent la même Image
Bevy, produite par le calcul natif sur le GPU de la scène. Les deux vues de
référence à 2 s sont rendues, comparées et inspectées. Il subsiste des écarts
locaux : ce résultat n'est pas une qualification de parité complète.

## Calcul et transfert dans le graphe commun

L'ancien exemple numérique devient un wrapper du module partagé
`tidewater_shore_sim_probe/mod.rs`. Son installation dans une App existante
utilise le RenderDevice/RenderQueue de Bevy, sans second moteur ni device.
Quatre pas fixes sont enregistrés par passage ; textures et pipeline persistent.
Les snapshots pris en charge sont 1/120/540/1080, depuis zéro à 60 Hz.

À l'étape demandée, une copie GPU publie l'état RGBA16Float 768² dans une Image
Bevy commune aux matériaux. Le readback final relit cette Image publique : il
vérifie donc le transfert, pas uniquement la texture de travail interne.
La capture attend calcul et readbacks. Aucun état de référence n'est chargé.

Le module partagé reproduit exactement les dix sorties de l'historique
autonome de 1080 pas, puis les six sorties 0/1/120 dans la scène. Avant ajout
des nouveaux matériaux, le PNG de la scène restait identique.
Rapport `shore-shared-verification-001.json`.

## Matériaux

`prepare-water-optics.py` accepte la configuration ShoreSim/SurfFoam acquise.
Le shader de l'eau reçoit les fonctions originales, la texture d'état, les
48 octets ShoreSimParams et la dentelle. Les usages LaceNearest et SurfFoam
partagent une image de dix mips RGBA8Unorm : lectures manuelles au mip 0 pour
le premier, sampling anisotrope original pour le second. Aucun mip n'est
reconstruit ni converti en sRGB.

La fonction native `terWetFoam` appelle désormais le hook `terrainWetness`
extrait d'App.js. Le terrain reçoit le même état, la même dentelle et les mêmes
paramètres. Les formules de teinte humide, séchage apparent et résidus utilisent
ainsi la simulation calculée dans Forge. La correction d'arrondi du hash est
conservée. Avant installation, le programme contrôle l'accord des hauteurs et
des paramètres entre simulation et matériau ainsi que le mode de fixture.

Mode visible : `--water-shore-sim OCEAN_EXPORT SHORE_SIM_EXPORT`.
Le mode `--water-shore-compute` reste un diagnostic d'installation du calcul
avec l'ancien matériau ; il n'est pas présenté comme le rendu de SurfFoam.

## Comparaisons source/natif

Fixtures `water-shore-sim-seaward-001`, `water-shore-sim-island-001`.
Sources `source-water-shore-sim-002.png`,
`source-water-shore-sim-island-001.png`. Le masque mesure la région où
l'activation source de la simulation change l'image de plus de 3 niveaux,
dilatée d'un pixel ; il inclut eau et plage.

| Vue | Erreur moyenne RGB entière /255 | Moyenne région /255 | P99 région | Maximum région |
|---|---:|---:|---:|---:|
| Village vers la mer | 0,3158412 | 1,2765775 | 32 | 102 |
| Île | 0,1643514 | 0,4748566 | 17 | 62 |

Rapports `shore-material-native-comparison-001.json` et
`shore-material-native-island-comparison-001.json`. Les deux PNG natifs ont été
inspectés. Ces maxima empêchent d'affirmer une égalité exacte des effets.
Les écarts de rendu de l'écume restent à isoler ; leur cause n'est pas établie
par cette seule comparaison.

- Plage `native-water-shore-sim-001.png` :
  `b3cf31dc39728b05ec4739e3c82f9a4b504c9322a4fde2856d362d21cd308a5d`.
- Île `native-water-shore-sim-island-001.png` :
  `97e2ab1b3021aeca116096388e569f486fe9d818ff70e1036d2bc09f30031567`.

Le rapport `shore-material-state-comparison-001.json` vérifie les états source
et natif jusqu'au pas 120. À ce pas, l'écart moyen d'écume est 0,00000106581,
maximum 0,00915527. Le raccordement conserve donc les erreurs numériques déjà
mesurées, sans en introduire dans le transfert de la texture.

## Régressions et contrôles

- Répétition plage : PNG identique avec le binaire final.
- Six fichiers ShoreSim de chaque vue/répétition identiques au calcul autonome.
- 21 sorties FFT par capture identiques à `native-ocean-default-fft-frame120-003`.
- Anciens modes ShoreWaves seul et eau optique seule : PNG précédents exacts.
- Fixture simulée avec ancien mode : rejet avant capture GPU.
- Compilation finale `shore-material-native-build-002.log` et Clippy des deux
  exemples `-D warnings`, `shore-material-native-clippy-002.log`, réussis.

Rapport `shore-material-native-regression-001.json`.
Binaires finaux : scène
`8a14b4e05f62c40907bcea866a589851af5ddb4447c9e456468f458b81ebffc1`,
simulation `7257575acd70f84753a40a0d56a0cbc5508cb8976fab1acc7a5f6cd4a5601733`.

## Limites

Le graphe calcule un historique déterministe puis se fige pour la comparaison.
Une séquence interactive, des pas variables et les dépôts d'embruns ne sont
pas qualifiés. Les captures restent en 8 bits sRGB sans la chaîne HDR/temporelle
complète. La régénération du champ côtier, les autres effets d'eau/atmosphère,
Studio, WebGPU et le package autonome restent ouverts. Tous les critères TW
complets restent ouverts. Aucune publication. Artefacts sous
`E:/OpenRPG_Forge/.tidewater-integration/`.
