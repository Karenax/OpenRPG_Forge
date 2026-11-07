# ADR-0046 — Exécution commune de l'environnement source

Date : 25 octobre 2026. Statut : réalisation Gate 7 en cours.

## Écart constaté

Les LUT d'atmosphère, les passes de nuages, le ciel et le calcul de l'environnement
HDR étaient exécutés dans le composant de carte. Le contrôleur de nuages et les
uniformes de lumière communs ne suffisaient pas à rendre ces capacités disponibles
au moteur et aux modules autonomes.

## Décision

Transférer les implémentations existantes dans `forge_engine::source_atmosphere`,
`source_clouds`, `source_sky` et `source_environment`. Le Studio et la référence
emploient directement ces propriétaires. Conserver les ressources, les layouts,
les identités des shaders et l'ordre atmosphère → nuages → environnement → caméra.
Les mises à jour et lectures GPU conservent leurs générations, leurs barrières et
la distinction entre encodage, publication et achèvement GPU.

Le moteur reçoit des entrées explicites. Le décodage des acquisitions, les pins,
la révision source, les gardes du dataset et les métadonnées historiques restent
dans l'intégration et ses codecs. Le constructeur commun des programmes valide
les kernels, les dispatchs, les groupes et l'ABI avant leur préparation GPU ; il
n'infère aucune ressource ni scène de référence. Les agrégats préparés et résidents
appartiennent au moteur, sans lecteur de paquet ni accès au système de fichiers.

Les vues hors écran utilisent le marqueur commun. Les captures de réfraction
possèdent déjà ce marqueur par composant requis ; la sélection commune n'a donc
pas à connaître le type de caméra du composant de carte.

## Vérification et suite obligatoire

Contrôler les consommateurs natifs et WebGPU, les tests transférés et les refus
d'entrées GPU mal formées. Rejouer les graphes réels avec des données vérifiées.
Conserver les reçus d'échec ; une compilation seule ne prouve pas leur exécution.

Ce transfert ne raccorde pas à lui seul les récepteurs PCSS/PCF et l'éclairage
source au matériau PBR des modules. L'admission des ressources d'environnement
dans leur paquet fermé, leur configuration canonique et leur consommation par
le runtime restent obligatoires. Aucun verdict de parité ou de clôture de Gate 7
n'est déduit de cette extraction.
