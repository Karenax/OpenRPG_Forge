# Émission GPU native des embruns

L'exemple `tidewater_spray_probe SPRAY_EXPORT NEW_DIRECTORY` exécute le kernel
original Breakers avec émission sur le RenderDevice Bevy. Les stations sont
reconstruites en Rust. Les buffers de particules et leur compteur commencent
à zéro ; aucun résultat de particules ou de crêtes source n'est chargé.
Le premier pas utilise encore les textures FFT source appariées pour isoler
l'émission. L'intégration des particules, les dépôts et le rendu ne sont pas
encore exécutés par cet exemple.

## Piège d'acquisition corrigé

Les deux passes originales partagent le même UniformBlock SprayParams.
Les uploads utilisent `queue.writeBuffer` avant soumission du command buffer.
La valeur effective de frameSeed est donc celle après `Spray.update`, même
pour la passe d'émission enregistrée auparavant. Au premier pas, elle vaut 1.

L'acquisition expérimentale `spray-source-004` introduisait une soumission et
une lecture entre émission et intégration. Elle utilisait alors la graine 0
pour l'émission et modifiait la simulation. Elle est exclue, ainsi que le
premier résultat natif `native-spray-emission-001` qui lui était apparié.

Les exports corrigés `spray-source-005/006` copient les buffers d'émission vers
des buffers dédiés dans le même command encoder. Leur lecture intervient
après la soumission normale, sans séparation des deux passes. Le manifeste
distingue la valeur CPU avant émission et `effective_gpu_seed`.

`spray-emission-regression-001.json` prouve la conservation de 92 fichiers
non particulaires, des multiensembles de particules aux pas 1/120/540 et du
budget original par rapport à `spray-source-002`. L'émission copiée est
reproductible modulo permutation des emplacements. Le contrôle couplé est
également rejoué dans `spray-reference-verification-002.json`.

## Résultat natif

`spray-emission-comparison-001.json` apparie les particules par type et tag,
après vérification de l'unicité de cette clé sur l'échantillon :

- 47 particules dans chaque moteur : 28 gouttes, neuf brumes, dix gerbes.
- Types et identifiants exactement concordants ; âge initial nul.
- Position : erreur moyenne par coordonnée 0,084168 mm ; maximum 0,728607 mm.
- Vitesse : erreur maximale par composante 0,00009704 m/s.
- Rayon : erreur maximale 0,00000915 m ; durée de vie : 0,000000239 s.

Les exécutions `native-spray-emission-002/003` produisent des enregistrements
actifs strictement identiques modulo permutation atomique. Le multiensemble
natif normalisé a le SHA-256
`02fd8e30993a21990c4c6f35642512d39a3dab90fe8d0708b2f0c42e6fdd00be`.

Compilation et Clippy strict réussis : `spray-emission-build-003.log`,
`spray-emission-clippy-003.log`. Binaire :
`c62abe34d2b2cc79603f560b639fbc8c225e4ad641e79ca1be30dc0c25db5b13`.
Les acquisitions et résultats sont sous `E:/OpenRPG_Forge/.tidewater-integration/`.

Ce contrôle du premier pas ne qualifie pas l'évolution temporelle, le budget
asynchrone natif, les dépôts, les sprites ni la chaîne FFT native partagée.
Tous les critères TW complets restent ouverts.

## Suite — première intégration native des particules

L'option `--integrate` enchaîne maintenant le kernel original Spray Update
après l'émission dans le même command encoder et sur les mêmes buffers.
Les paramètres de surface, de rivage et de particules sont contrôlés ; le
buffer atomique des dépôts commence à zéro. Les fonctions originales appliquent
gravité, traînée, turbulence, croissance et hauteur d'eau Eulerienne. Les
émissions CPU et le corps mobile restent désactivés dans cette acquisition.

`spray-integration-comparison-001.json`, exécutions
`native-spray-integration-001/002` : 47 particules avec types et identifiants
concordants, répétition exacte modulo emplacements. Erreur moyenne par
coordonnée de position 0,084280 mm, maximum 0,726700 mm ; erreur maximale de
hauteur d'eau 0,870742 mm. L'âge correspond exactement au premier pas de 1/60 s.
Le multiensemble natif normalisé a le SHA-256
`79023b9b08557b650e4fac8a5c0f7f7efdca0531d721de406c0779918225aa0a`.

Le buffer de dépôt est identique à la source mais entièrement nul à ce pas.
Cela vérifie le branchement, pas les collisions ni des retombées effectives.
La qualification des dépôts nécessite des états plus tardifs et le raccordement
temporel avec ShoreSim. Les textures FFT restent des entrées source appariées.

Clippy strict et compilation réussis (`spray-integration-clippy-001.log`,
`spray-integration-build-001.log`). Le mode émission seul conserve exactement
ses particules précédentes (`spray-integration-emission-regression-001.json`).
Binaire : `c6defa1e37b19d7449b03d5240664b7f87e01cb6328e25448edc93c120c038b8`.
Rendu, historique complet et qualification globale restent ouverts.

## Suite — retombées natives sur états appariés à 2 s et 9 s

`--replay-update 120|540` charge explicitement l'état de particules source
juste avant l'intégration et les textures FFT correspondantes, puis exécute
uniquement Spray Update. Le manifeste marque ces entrées source, distingue
les résultats attendus qui ne sont pas chargés et indique que le kernel
d'émission n'est pas exécuté. Aucun faux résultat de crêtes n'est exporté.

Les exports `spray-source-007/008` copient maintenant les états avant intégration
aux trois instants, toujours dans le même command encoder. Les multiensembles
avant/après sont reproductibles ; 95 fichiers non permutés sont identiques.
Rapports `spray-reference-verification-003.json` et `spray-impact-regression-001.json`.

Les rapports `spray-impact-120-comparison-001.json` et
`spray-impact-540-comparison-001.json` comparent les mêmes emplacements :

| Pas | Actives avant → après | Particules mortes au pas | Dépôt pondéré | Texels touchés |
|---|---:|---:|---:|---:|
| 120 | 7 078 → 6 886 | 192 | 349 | 169 |
| 540 | 12 746 → 12 462 | 284 | 448 | 222 |

Les décisions de vie/mort, durées de vie, identités et chaque texel du dépôt
sont exactement concordants. Erreur maximale de position : 0,075221 mm et
0,029445 mm ; la hauteur d'eau calculée diffère au maximum de 4,460312 mm et
2,198935 mm. Les écarts de hauteur sont conservés dans les rapports, même
lorsqu'ils ne changent pas les décisions de collision de ces échantillons.

Les sorties natives finales `native-spray-impact-120-002` et
`native-spray-impact-540-002` reproduisent exactement les cinq buffers utiles
des essais précédents. Les deux modes du premier pas restent inchangés.
Clippy et compilation réussis (`spray-impact-clippy-003.log`,
`spray-impact-build-003.log`). Binaire :
`04eb5d0afa7761cdab588c5ed4df6542b7d1d2120a6042470393a91923e6da5b`.

Cette preuve valide des mises à jour isolées à partir d'états source. Elle ne
prouve pas encore un historique natif complet ni la consommation des dépôts
par ShoreSim au pas suivant. Le rendu des sprites reste ouvert.
