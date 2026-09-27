# Crêtes des vagues plongeantes — calcul GPU natif isolé

Le kernel original de Breakers s'exécute maintenant sur le RenderDevice Bevy.
La passe appartient au RenderGraph de l'App, avant les caméras, sans second
device. Les stations sont régénérées en Rust depuis le terrain et contrôlées
contre la référence avant transfert. Les crêtes de référence ne sont jamais
chargées comme entrées ; les trois buffers de sortie commencent à zéro.

Cet essai isole le calcul : les textures FFT aux instants 1/60 s, 2 s et 9 s
sont encore les entrées appariées de l'oracle original. Il ne constitue pas
la preuve d'une chaîne complète FFT natif → crêtes → ruban visible.
`probe.json` marque explicitement `isolated_source_fft_inputs` et enregistre
les hashes du manifeste source, des stations natives et du kernel adapté.

## Ressources et calcul

Le programme contrôle pin source, hashes, tailles, valeurs finies du terrain
et des mips FFT, limites des stations et `Frame.pad0=0`. Il conserve les
layouts originaux de Frame/samplers et les quinze bindings du kernel.
Les ressources de shading inaccessibles depuis cette entrée compute reçoivent
des textures valides provisoires. Les quatre couches et neuf mips de déplacement
sont conservés ; la lecture des crests utilise des ShaderBuffer Bevy.

La seule adaptation arithmétique reste la correction d'arrondi documentée de
`shoreHash1`. Le choix des crêtes, la recherche de phase, les positions et le
progrès de déferlement utilisent les fonctions originales.

## Comparaison

Rapport `breaker-compute-comparison-002.json`, sorties
`native-breaker-compute-002`. Pour les trois instants, aucun écart de masque
actif, d'identifiant de vague ou de masque des segments dessinables.

| Pas | Crêtes actives source/natif | Erreur moyenne par coordonnée de racine | Maximum par coordonnée de racine |
|---|---:|---:|---:|
| 1 | 471 / 471 | 0,0923 mm | 1,1978 mm |
| 120 | 690 / 690 | 0,0953 mm | 1,4305 mm |
| 540 | 731 / 731 | 0,1357 mm | 2,1484 mm |

Les erreurs des positions arrière, hauteur, creux, direction et progrès sont
également enregistrées séparément. Les attributs d'un slot dont l'identifiant
est nul sont exclus : la source y conserve des valeurs périmées, tandis que
cette évaluation indépendante commence chaque snapshot à zéro. Les identifiants
et la visibilité sont contrôlés sur tous les slots avant cette exclusion.
Ces résultats ne sont ni une égalité flottante exacte ni une qualification
visuelle du ruban.

## Répétition et contrôles

Les trois buffers et le manifeste sont identiques entre les exécutions finales
002 et 003. Les buffers restent identiques à l'essai 001, antérieur à l'ajout
du manifeste. Rapport `breaker-compute-repeat-001.json`.

- Pas 1 : `f70332b27dcdfce21bd86a330f09795657399920190f5de84fb4a2b0399badbb`.
- Pas 120 : `1e8677a71aad42bb2a4bea028acbbf8276476c962176bf12bda330080161daa5`.
- Pas 540 : `cd5f16278a03d5e2c6ed1a40fb4f2f4c2671ee834cc20630a81dfebc6c48c9c3`.

Compilation finale et Clippy `-D warnings` réussis :
`breaker-compute-build-002.log`, `breaker-compute-clippy-002.log`.
La première passe Clippy signalait une expression de plage, remplacée par
`RangeInclusive::contains` sans changement de comportement.
Binaire : `315cbecc21469a4709bb255782c084b91e6911f895e327d0495f608c905035f8`.

Les données restent sous `E:/OpenRPG_Forge/.tidewater-integration/`. Aucune
publication. Raccordement au FFT natif partagé, rubans visibles, dépôts d'embruns,
animation et qualification globale restent ouverts, ainsi que tous les TW complets.
