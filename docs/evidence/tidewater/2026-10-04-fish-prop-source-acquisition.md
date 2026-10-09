# FishProps : acquisition exacte de l'inventaire source

Référence intacte : `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`,
licence MIT, copyright DRG Software Solutions LLC. Node 22.22.3 épinglé.

## Inventaire

78 instances et 18 prototypes distincts : 28 poissons entiers, 38 poissons
ouverts/salés, une tête, un tronc, deux homards, deux lits de glace et six feuilles.
Les modèles ont deux niveaux de détail et un proxy d'ombre identique au niveau
bas. Les prototypes totalisent 51 320 triangles au niveau haut et 11 284 au
niveau bas ; les 11 284 triangles des proxies sont un inventaire distinct.
Ces totaux ne sont pas le nombre de triangles effectivement dessinés par frame.

L'acquisition reste séparée des accessoires statiques du village/de plage et
des cinq poissons de l'échoppe acquis antérieurement. Elle contient notamment
les espèces et coupes réellement employées par toute la scène.

## Méthode et preuve

`export-fish-props.mjs` construit d'abord le village source sans observation.
Il lit le buffer d'instances original sur le GPU et conserve l'empreinte de
la géométrie fusionnée. Une observation externe de `FishProps.build` copie
ensuite les items et prototypes, en appelant le même constructeur original.
La méthode est restaurée après acquisition, sans mutation de fichiers source
ni changement des appels aléatoires. Les buffers source et observés sont
strictement égaux, ainsi que scène, terrain et collisions.

L'empreinte globale retrouve l'acquisition initiale :
`e2f4799bb6f425afb4b9b800fb547528a037da15ad95f8c9d2182889c1e805f1`.
Les indices de prototype viennent directement du tableau source `kind0`,
avec contrôle contre l'ordre source des types. Les records float32 préservent
position/longueur, quaternion, pattern/seed, curl/sag/jaw et les quatre flags.
La portée source de dessin est 90 m ; celle des proxies d'ombre est 22 m.

Deux extractions finales `fish-props-source-20261004-v2` et `v3` contiennent
227 fichiers identiques. Descripteur SHA-256 :
`4a067fb63afef765b348e6f85fc6ebb6dd1ffa9b14d0751b013d03583fc3ee00`.
L'acquisition initiale v1, avant observation directe du `kind0`, reste conservée
comme étape de diagnostic ; elle n'est pas la paire finale de qualification.

`verify-fish-props.py` reconstruit indépendamment le buffer fusionné, avec les
indices locaux uint16 promus en uint32 comme dans `ReefBatch`, les normales,
`aData` et les `aKind`. Il recalcule chaque record float32 à partir des champs
source et contrôle licence, provenance, références, finitude, triangles et
proxies. Reçu `fish-props-source-proof-20261004-v2.json`, PASS.
Deux copies identiquement altérées sont quand même refusées par les pins :
`fish-props-refusal-proof-20261004-v1/result.json`, source inchangée.

## Limites

Ce contenu est une acquisition. Paquet portable, lecteur Rust, matériau dédié,
déformation de vertex, LOD/culling/proxies et placements Studio restent à porter.
Le matériau village générique ne peut pas remplacer ce contrat visuel.
Aucun rendu Rust, édition persistante, parité visuelle ou gate n'est qualifié.

## Matériau procédural acquis séparément

`export-fish-material.mjs` instancie les fabriques originales de matériau sans
changer la référence ni exporter de placements. Une géométrie de thon factice
sert uniquement à créer le batch requis par ces fabriques. Les fragments
vertex/surface/ombre principaux et de fondu, quatre modules WGSL résolus et
la table linéaire FishSkin sont exportés, avec dix fichiers de provenance.
Deux acquisitions indépendantes `fish-material-source-20261004-v1/v2`
produisent 22 fichiers identiques. Le vérificateur Python contrôle fermeture,
empreintes, provenance intacte, licence MIT, 21 patterns, 168 lignes et les
2 688 octets float32 reconstruits de la table, ainsi que les contrats matériau.
Reçu `fish-material-source-proof-20261004-v1.json`, PASS.

Le mode observé dans cette référence figée est `FISH_SPECULAR='intended'`.
La source documente aussi le mode `reference` correspondant au comportement
historique three.js. Cette différence doit être traitée explicitement lors de
la comparaison visuelle ; le reçu d'acquisition ne prouve pas la parité avec
l'application three.js. Shader Rust, adaptation Bevy et exécution GPU restent
à réaliser. L'échec initial d'extraction appelait une fabrique non exportée
`iceGeometry`; corrigé avant toute création du dossier de sortie.
## Paquet portable et lecteur Rust

Deux assemblages indépendants du paquet FishProps contiennent 250 payloads
plus `package.json`, avec géométrie et matériau dans leurs espaces de fichiers
séparés. Les descriptors source restent byte-identiques. Le constructeur
rejoue les deux audits d'acquisition avant de créer le dossier du paquet.
SHA-256 du manifeste :
`e578b44e93d18d79ec06c875b76dae005f71c17ecf1d0ff09aa3ecddd651330a`.
Les assemblages v1/v2 concordent ; `fish-props-package-proof-20261004-v1.json`
est PASS. Une table matériau altérée, une géométrie absente et un fichier
supplémentaire sont refusés, sans altérer le paquet source :
`fish-props-package-refusal-20261004-v1/result.json`.

`crates/forge_assets/src/tidewater_fish_props.rs` ajoute le transport synchrone
et asynchrone épinglé, les limites de taille/chemins, les références contrôlées,
l'inventaire de 78 instances/18 prototypes, les distances source 90 m/22 m,
le contrat matériau dédié, la licence et les 78 identités de catalogue.
Le lecteur ne prépare aucun maillage GPU et ne transpose pas les shaders.
Deux tests natifs réels sont PASS, sans ignoré/filtré, ainsi que Clippy
all/pedantic avec warnings refusés. Ils couvrent catalogue, identités,
parité des transports, absence/altération et manifeste altéré.
Reçu courant `fish-reader-validation-20261004-v3/result.json`, inputs inchangés.
Le rejeu v2 avait seulement un avertissement Clippy sur le waker de test,
remplacé par `Waker::noop()` avant la validation v3 complète.

La compilation en métadonnées WASM passe aussi code 0, inputs inchangés :
`fish-reader-wasm-check-20261004-v1/result.json`. Le lecteur est enregistré
dans le registre assets avec les octets antérieurs préservés et sauvegardés :
`fish-reader-registration-20261004-v1/result.json`. Il n'est pas dans le
snapshot de 1 338 fichiers du candidat plage en cours de construction.
Projection moteur, matériau Bevy, LOD/culling/ombres, connexion Studio et
preuve visuelle restent à réaliser ; disponibilité dans l'éditeur non prouvée.
## Référence GPU des poses

`export-fish-pose-reference.mjs` exécute le corps vertex source acquis,
non réécrit, dans un adaptateur compute de validation. Les entrées viennent
du paquet épinglé ; le lookup des records reçoit directement l'indice source
au lieu d'un buffer de draw issu du culling. Les sorties sont positions et
normales mondiales, avec quaternion, longueur, mâchoire, curl et sag source.

Exécution WebGPU réussie : 169 709 sommets, 234 cas (78 instances × 3 LOD),
compilation WGSL sans erreur, sorties finies, référence source intacte.
`fish-pose-reference-20261004-v1/reference.json`, SHA-256
`bb8182a80d303b55d535df5e27688712c56b6275fd114633cd861b576b005f52`.
Les buffers d'entrée/sortie, records, table FishSkin et kernel sont conservés
avec leurs empreintes. Il s'agit de la référence numérique pour le portage,
non d'une comparaison avec une implémentation Rust ni d'un rendu complet.
Matériau fragment, culling, fondu de LOD et parité visuelle restent non prouvés.
## Portage des poses et préparation des maillages

`crates/forge_engine/src/tidewater_fish_props/pose.rs` porte la mâchoire,
la courbure latérale et le sag source en float32 local. Quaternion, longueur
et translation restent dans le transform commun de la racine éditable.
Le test compare toutes les positions/normales mondiales après ce transform
aux sorties de la référence GPU épinglée : 169 709 sommets, 234 cas.
Erreur maximale de position 7,6293945e-6 m ; normale 4,172325e-7.
Le cas non fini est refusé. Deux tests PASS et Clippy all/pedantic PASS :
`fish-pose-validation-20261004-v3/result.json`, inputs inchangés.
Les v1/v2 conservent leurs avertissements Clippy corrigés, sans changement
aux calculs ou tolérances (position 3e-5 m, normale 2e-5).

`crates/forge_engine/src/tidewater_fish_props.rs` prépare les 18 prototypes,
78 templates et 234 maillages locaux, avec indices uint16 source, attributs
`FishRestPosition` et `FishData` et paramètres de peau séparés. Les positions
posées servent au rendu ; la position de repos reste disponible au shader
procédural. Les flags peau conservent leur ordre source, sans matériau de
substitution ni publication d'entités/GPU. Longueur et quaternion source
restent dans le transform Forge, avec les identités du lecteur commun.

Validation native complète du préparateur : trois tests PASS, zéro
ignoré/filtré, Clippy all/pedantic PASS, inputs inchangés :
`fish-engine-preparation-validation-20261004-v2/result.json`. Ils contrôlent
l'inventaire, les attributs de repos/données, l'égalité du modèle bas/shadow
et incluent la comparaison numérique intégrale des poses. Le premier
harness avait un chemin de sous-module incorrect ; corrigé explicitement.

Le préparateur et le lecteur compilent aussi en métadonnées WASM avec les
artefacts exacts du candidat plage :
`fish-engine-preparation-wasm-check-20261004-v3/result.json`, code 0,
inputs inchangés. Les v1/v2 gardent les diagnostics du helper (sélection
incorrecte des artefacts hôtes puis nom de rmeta sans préfixe lib), non des
défauts du produit. Le registre moteur inclut maintenant le préparateur
sous la feature physique existante, octets antérieurs préservés :
`fish-engine-registration-20261004-v1/result.json`.

Ce préparateur n'installe pas encore de matériau FishProps ni de résident
rendu. LOD/culling/fondu, proxy d'ombre avec masque de nageoires, projection
commune des instances et transport Studio restent ouverts. Ces sources
ne sont pas dans le candidat plage figé déjà construit.
## Référence raster complète du matériau

`export-fish-surface-reference.mjs` exécute le corps `main-surface.wgsl`
acquis intégralement, sans réécriture, dans un vrai pipeline fragment WebGPU.
Les trois helpers communs (luminance, bruit de masque, perturbation de normale)
et le module fish proviennent des payloads épinglés. Les dérivées écran,
le relief procédural et les discard des nageoires sont réellement exécutés.
Le paquet entier est vérifié avant lancement ; la source figée reste intacte.

Deux exécutions indépendantes produisent sept fichiers byte-identiques dans
`fish-surface-reference-20261004-v1/v2`. Descripteur SHA-256 :
`3d717578fefdbca9a0460fec2d9dec5157c19488ee2ca4f4c9bf63561c68cb9b`.
Les 1 560 cas couvrent les paramètres des 78 instances sur 20 domaines de
partie diagnostiques chacun ; atlas 1 280 × 1 248, temps fixé 0,625.
Quatre plans float32 conservent albedo/roughness, normale/metalness,
translucency/specular et clearcoat/coverage. Ces domaines sont des plans
paramétriques, pas les maillages réels ni une capture de la carte.

`verify-fish-surface-reference.py` contrôle indépendamment les hashes,
le corps complet présent exactement une fois, les records et la table peau
identiques au paquet, la reproductibilité de tous les octets, la finitude,
les normales unitaires, le clearcoat wet source et les masques identiques
entre les deux passes. PASS : 1 486 733 pixels couverts et 110 707 pixels
de nageoires écartés ; reçu `fish-surface-reference-proof-20261004-v1.json`.
Les premiers diagnostics du harness étaient uniquement des noms de variables
communs à l'adaptateur et au corps source ; noms d'adaptateur corrigés sans
modifier le matériau source. Aucun résultat incomplet n'est qualifié.

Le mode acquis reste `intended`, explicitement consigné ; la différence
historique avec la lecture specular zéro de Three.js reste à comparer.
Cette référence sert au raccordement du matériau Rust dédié. Elle ne prouve
ni éclairage commun, ni ombres, ni LOD, ni rendu dans Studio, ni parité visuelle.

## Bibliothèque de surface Rust comparée sur GPU

`crates/forge_engine/src/tidewater_fish_props/surface.rs`, enregistré dans le
préparateur, assemble une bibliothèque fragment dédiée à partir du paquet
vérifié. Le corps surface reste intégral, les trois helpers communs et le
module fish aussi. Le consommateur devra fournir `frame.view`, `frame.time`
et la liaison de stockage `fishSkin`. Aucun matériau générique de substitution.

Deux modes explicites : `Intended` conserve les paramètres acquis ;
`ThreeReference` applique la valeur specular zéro historique après le corps
source, sans changer ses autres calculs. Compilation native, export des deux
bibliothèques et Clippy all/pedantic PASS ; inputs inchangés :
`fish-surface-adapter-validation-20261004-v2/result.json`. Le premier harness
échouait seulement sur un import Rust placé après les statements, corrigé.
Le préparateur entier, avec ce nouveau sous-module, passe aussi le contrôle
WASM en métadonnées : `fish-engine-preparation-wasm-check-20261004-v4/result.json`.

Le rasteriseur peut recevoir la bibliothèque réellement exportée par Rust.
Les deux modes ont exécuté les 1 560 cas, sans erreur WebGPU ni sortie non finie :
`fish-surface-adapter-gpu-intended-20261004-v1` et
`fish-surface-adapter-gpu-reference-20261004-v1`.
`verify-fish-surface-adapter.py` confirme tous les octets des quatre plans
identiques à l'oracle en mode intended. En mode reference, les seuls changements
sont les 1 486 733 valeurs specular des pixels couverts, toutes mises à zéro ;
albedo, relief, normales, metalness, translucency, clearcoat et discard restent
identiques. Reçu PASS : `fish-surface-adapter-gpu-proof-20261004-v1.json`,
avec sources de compilation épinglées et revérifiées.

Portée : bibliothèque de surface testée en raster GPU sur domaines plans.
Les liaisons Bevy du matériau, l'éclairage complet, les ombres, le résident
LOD, la projection commune et le chargement dans Studio restent à terminer.
Les paramètres specular comparés ne sont pas une preuve de reflets dans la carte.

## Matériau Bevy dédié et fermeture d'éclairage

`crates/forge_engine/src/tidewater_fish_props/material.rs` définit FishMaterial,
les paramètres source, la table peau en storage, les attributs rest/data et
le constructeur de shader. Les ressources sont celles de la scène : soleil/SH,
environnement, ombres live, nuages, lampes, lumière renvoyée par le sol et
atténuation sous l'eau. Le clearcoat suit la composition source, avec normale
géométrique séparée de la normale de relief. Le culling reste front/back source.
Les poses restent préparées dans les meshes CPU ; le shader conserve le repos.

Le constructeur commun de `tidewater_material.rs` expose désormais la même
chaîne de receivers au matériau dédié. Le trajet Village précédent appelle
la même chaîne, dans le même ordre. Les ressources partagées ne sont pas
réinitialisées implicitement. Un consumer doit encore installer le plugin,
publier les handles, régler le mode de reflet et actualiser le temps.

Validation native isolée : `fish-material-native-validation-20261004-v6/result.json`,
14 tests PASS, sources inchangées. Trois nouveaux tests contrôlent la table
GPU byte-identique aux 168 lignes acquises, les paramètres des 78 instances,
les liaisons de scène et la construction de la fermeture de shader. Le harness
utilise le graphe natif cached exact, les nouvelles sources FishProps et les
constructeurs communs extraits intacts. Il inclut les receivers scene-buffer
courants et le constructeur neutre de lampe, sans recompiler tout le plugin
GPU de collecte. Ce n'est pas un build Cargo/application complet.
Clippy all/pedantic est strict sur le nouveau module FishProps ; les modules
communs préexistants du harness sont hors scope de cette vérification Clippy.
Les premiers diagnostics concernaient le harness (chemins de sous-modules,
environnement des derives, dépendances récentes absentes du cache ancien).
Le constructeur peau a aussi été corrigé pour utiliser des tableaux de bytes
fixes sans unwrap. Les preuves de runs précédents restent conservées.

Le matériau et ses dépendances courantes passent le contrôle WASM en métadonnées :
`fish-material-wasm-check-20261004-v1/result.json`, assets/engine code 0,
inputs inchangés, application non liée.

`validate-fish-material-shader.mjs` crée les pipelines WebGPU des variantes
uniform et shared-scene, avec la fermeture complète du shader exporté par Rust.
Deux variantes sans message de compilation ni erreur de validation :
`fish-material-shader-gpu-check-20261004-v1/result.json`.
Shader exporté SHA-256 :
30a24805585963c27accbc74a7177b38de5915c9072c52b84c63807484d15db2.
Les imports Bevy caméra/mesh sont explicitement remplacés par des stubs de
sonde ; aucun draw, binding de scène réel ou rendu Studio n'est prouvé par
ce contrôle. Les sorties surface comparées au tour précédent gardent leur
portée propre. Projection commune, résident/LOD, proxy d'ombre masqué,
publication Studio et comparaisons de carte restent ouverts.

## Suite — placements communs FishProps et fondu de distance

Le raccord moteur `tidewater_fish_props/resident.rs` accepte uniquement les
placements communs fournis, sans republier les instances source supprimées.
Les copies partagent les trois géométries mais possèdent deux matériaux visuels
indépendants ; racines/projections et parties utilisent les marqueurs communs.
Les prototypes source non solides portent le contrat collision non requise.
La collecte d'ombres conserve le proxy source et exige encore une passe masquée.

Preuve native isolée : `.tidewater-integration/fish-material-native-validation-20261004-v14/result.json`.
Compilation, 18 tests et Clippy ciblé passent ; entrées inchangées. Les nouveaux
cas couvrent copie indépendante, sélection hors champ d'une ombre proche,
masquage au-delà de 90 m, temps/frame, nettoyage des ressources possédées,
inventaire vide et refus atomique des identités dupliquées. Ce harnais reprend les
marqueurs communs actuels et les dépendances Bevy natives existantes ; ce n'est
pas une construction Cargo complète de Studio. Les échecs v10 à v13 restent
conservés (harnais/imports, mutabilité, assertion d'entités internes, lint float).

Le culling Rust passe aussi les 3 432 cas exécutés par le culling source dans
`fish-lod-reference-20261004-v2/reference.json` : ombres à moins de 22 m,
transition de détail entre 0,88*28L et 28L, fondu entre 81 et 90 m. Les distances
exactes sont conservées en bits f64 aux frontières. L'oracle v1 avait une
observation de distance Math.hypot différente de Math.sqrt source ; v2 corrige
l'observation sans modifier le culling acquis.

Le shader courant inclut le Bayer/fondu source. Preuve
`fish-material-shader-gpu-check-20261004-v2/result.json` : deux pipelines WebGPU
créés sans erreur, source intacte, shader SHA256
`3e457ffeaf08f4c4cb02c7df12aa357f116dd5912ebac74bbfae7298710a0062`.
Imports caméra/mesh remplacés explicitement par la sonde : aucun draw Bevy,
aucune validation des bindgroups produit et aucune parité visuelle affirmée.
WASM du nouveau résident, MaterialPlugin et transport/catalogue Studio,
projection en scène et passe d'ombre masquée restent ouverts.

Le nouveau résident et le culling passent aussi le contrôle de métadonnées
WASM : `fish-material-wasm-check-20261004-v2/result.json`, lecteur et moteur
retour 0, entrées inchangées. Dépendances WASM du candidat plage et harnais natif
v14 ; pas de lien d'application ni de dessin Studio dans cette preuve.

Suite : [raccord bibliothèque et inventaire Studio](2026-10-04-fish-prop-studio-library.md).
Le paquet poissons et ses identités/poses rejoignent les placements communs
via le binding existant. Cargo WASM Studio PASS sur 1 347 sources figées ;
contrôle natif moteur 18 tests/Clippy PASS. Bibliothèque indépendante vérifiée,
non déployée ; projection viewport/matériau/ombre et qualification visibles
restent ouvertes. Aucun rendu Studio des poissons annoncé.
