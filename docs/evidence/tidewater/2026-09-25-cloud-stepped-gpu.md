# Historique nuageux persistant entre les images

La scène Bevy avance désormais les nuages d'un pas à chaque invocation du
graphe de rendu, pendant ses 120 pas de préparation. Le mode headless associé
est `--native-stepped ATMOSPHERE_INPUT`. Le mode `--native-controller` conserve
le calcul en lot pour comparaison.

Les caches persistent dans l'état du système de rendu : volume et bruit bleu,
samplers, paramètres constants, pipelines ombres/trace/temporel/panorama.
Weather/bounds s'exécutent une seule fois. Les deux paires de textures
d'historique conservent leurs données et alternent selon le numéro global du
pas ; la première passe complète d'ombre et de panorama n'est pas répétée à
chaque invocation. Les buffers de paramètres de chaque pas restent immuables.

## Vérification GPU

Sous `E:/OpenRPG_Forge/.tidewater-integration/`,
`native-cloud-stepped-001/002` fonctionnent avec `cloud-controller-inputs-001`
sans Frame/Settings locaux acquis. Leurs `execution.json` attestent 120 pas
et 120 invocations du graphe, contre un seul lot précédemment.

`cloud-stepped-verification-001.json` vérifie que les dix textures finales
sont strictement identiques entre les deux essais et au calcul en lot
`native-cloud-controller-headless-001` : weather, bounds, ombres, trace,
métadonnées, les deux historiques et panorama. Cette comparaison couvre
la persistance et l'ordre des mises à jour, sans verdict de performance.

## Scène assemblée

`native-cloud-stepped-scene-001/002.png` incluent ciel/reflets/ombres nuageuses,
eau, déferlantes et embruns. La première a été inspectée visuellement.
La seconde est exactement identique au candidat calculé en lot, SHA-256
`579e78405e5d4886faaa96a79ee6042a9de3b4a3f4c412d109888255f8082d64`.
La première diffère sur 14 pixels dans [1742,453,1750,460], maximum 2/255,
moyenne scène 0,000002572/255. SHA-256 :
`2ce586ddeb16a36a3bb86126569c875b30e020a754729f82962db1c9ffffbb3a`.

Les 7 014 particules actives, appariées par identité, restent identiques au
candidat en lot ; crêtes, compteurs, dépôts et état du rivage sont inchangés.
L'ordre des emplacements de particules varie. Une différence d'ordre de
composition transparente est une explication plausible des 14 pixels,
mais elle n'est pas isolée par une preuve causale. Le rendu complet n'est
donc pas déclaré strictement reproductible.

`cloud-stepped-source-comparison-001.json` conserve la comparaison source :
moyenne 0,196815/255 dans la région d'effet, P99 4/255, maximum 109/255,
incluant les écarts préexistants. `cloud-stepped-scene-regression-001.json`
consigne la variation entre répétitions.

Clippy strict et compilation des deux exemples passent. Binaires :

- scène : `1c7e3d062c393e7032bc26ebd8e5eae2377cd29b62383115ba7b17e9586bd573` ;
- headless : `e76a4af6036beedc84365eafd2b3746dc4086607c1beb29502675c55d3861311`.

## Limites

Préparation bornée à 120 pas, caméra et éclairage fixes, puis arrêt du calcul.
Les blocs nuageux sont produits par le contrôleur Rust, mais la séquence reste
préparée avant l'exécution. La boucle interactive illimitée, les changements
de caméra/résolution sur le GPU, le Frame global et l'éclairage entièrement
natifs, les performances, Studio et navigateur restent ouverts.
La fidélité complète de la carte n'est pas clôturée ; aucune publication Git.
