# Stabilisation de la frontière atmosphérique Vulkan

Le diagnostic ajoute une sortie indépendante contenant la distance de
l'intersection (`tMax`), la distance imposée par les coordonnées de la LUT
(`d`), le terme `dot(ro,ro)-RT²` et la hauteur de vue. Les dix sorties
originales de l'acquisition restent inchangées ; les deux fichiers de
diagnostic sont supplémentaires.

Sur la ligne supérieure de la LUT (y=63), la référence donne un terme
sphérique nul. Vulkan donne environ 0,2903 dans la mesure half-float et
sélectionne une petite intersection positive. Au pixel x=200, par exemple,
la référence traverse environ 1 776 km tandis que le calcul Vulkan brut
retient environ 0,000166 km. Le diagnostic établit le changement de racine
retenue ; il ne prouve pas quelle transformation interne du compilateur
produit le résidu.

## Correction et résultats

L'adapter natif conserve le kernel source, sauf pour `tMax` à la frontière :
si y=63, d>0 et le terme sphérique est positif, il utilise la distance `d`
déjà définie par la paramétrisation. Le cas d=0 et les autres lignes
conservent le calcul original. La correction dépend de la condition
numérique observée, sans forcer un backend graphique.

La première variante utilisait `d` sur toute la frontière et modifiait un
texel Dx12. La variante finale exige le résidu positif ; ses quatre sorties
Dx12 sont identiques à celles précédant la correction.

| Transmission Vulkan | Avant | Après |
|---|---:|---:|
| Erreur maximale absolue | 1,0 | 0,01220703125 |
| Erreur moyenne absolue par scalaire | 0,00167266739 | 0,000000512106 |
| Scalaires différents de la référence | 899 | 157 |
| Pixels avec erreur >0,01 | 94 | 1 |

Le texel restant est (164,63). Le diagnostic source y indique lui-même une
intersection presque nulle, 0,000061 km au lieu des quelque 1 456 km de la
paramétrisation. La référence y donne une transmission 1, contre environ
0,988 dans le canal le plus différent du candidat. Cette différence reste
explicitement ouverte ; aucune égalité à l'octet avec Tidewater n'est revendiquée.

La luminance du ciel et les trois vecteurs d'irradiance/transmission solaire/
horizon restent identiques aux sorties natives précédentes aux deux hauteurs
testées. Leurs petits écarts antérieurs avec la référence ne sont donc pas
corrigés par cette stabilisation de frontière.

## Vérifications et preuves

Racine `E:\OpenRPG_Forge\.tidewater-integration` :

- `atmosphere-source-004` : sorties source plus diagnostic indépendant.
- `atmosphere-boundary-diagnostic-001`, `native-atmosphere-boundary-001` :
  diagnostic avant correction. Seule la sortie de transmission y est utile ;
  les sorties aval calculées avec cette texture de diagnostic ne représentent
  pas une atmosphère physique et sont exclues des comparaisons de rendu.
- `atmosphere-boundary-diagnostic-001.json` : échantillons des racines/distances.
- `native-atmosphere-stable-002`, `native-atmosphere-stable-island-001`,
  `native-atmosphere-stable-dx12-002` : variante finale.
- `atmosphere-stable-comparison-001.json` mesure la première variante Vulkan ;
  la transmission de la variante finale est identique.
- `native-atmosphere-raw-regression-001` : `--raw-source` reproduit les quatre
  sorties Vulkan antérieures à l'octet.
- `atmosphere-boundary-regression-001.json` : régressions, source préservée,
  localisation du texel restant et hash du binaire final.
- `atmosphere-boundary-build-002.log`, `atmosphere-boundary-clippy-001.log` : réussis.
- Binaire SHA-256
  `532c4d03f0b37432fc9c917e0a9367b30994e5fbc271d27767167bb113ffa93c`.

Le programme et la référence bruts restent disponibles avec `--raw-source`
pour les diagnostics. La stabilisation ne vaut pas qualification visuelle :
ciel visible, soleil/lune/étoiles, nuages, environnement natif, temporel et
Studio restent à raccorder. Aucune publication.
