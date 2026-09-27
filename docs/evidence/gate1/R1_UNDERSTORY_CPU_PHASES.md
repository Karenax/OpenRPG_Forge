# R1 — Localisation du coût sous canopée, 8 septembre 2026

Complément 9 septembre : `R1_RENDERDOC_GPU_DURATIONS.md` mesure les événements
GPU d'une capture historique via RenderDoc, sans patch Bevy. Les ombres et les
cartes d'éclairage y sont séparées ; la passe transparente domine ce replay.
Ce n'est ni la charge stationnaire ci-dessous ni le candidat courant : ne pas
soustraire ou comparer directement leurs temps, ni réactiver les essais écartés.

## Paramètres isolés et projection effective exportée

Racine : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-shadow-resolution-20260908`.
`Measure.ps1 -Resolution 4096`, puis `2048`, puis `96m` : même binaire, mêmes
sources BLEND, parcours normal, fenêtre Full HD, temps virtuel figé et 300 frames.
Tous les domaines sont demandés High séparément. Le profil externe est lié à la
révision 3 du WorldSketch ; il ne modifie ni le dépôt ni le Studio.

Le Runtime accepte désormais `--walk-shadow-profile` uniquement en mesure de
parcours avec qualité d'ombres explicite, sans `--quality` global. Identité et
révision externes strictement validées, aucun rebinding silencieux. Le rapport
exporte `shadow_projection` : soleil unique, atlas effectif, bornes des cascades,
distance et présence des contact shadows. Ces observations CPU ne sont pas des
timestamps GPU. Tests des défauts, restrictions CLI, doublon, round-trip de profil,
révision incompatible et profil invalide ; **82 tests Runtime + une intégration
PASS**, Clippy all-targets strict, build natif, fmt et diff-check PASS.

| Paramètre demandé et observé | FPS moyens | p95 ms | 1 % low FPS | Frame moyenne ms |
| --- | ---: | ---: | ---: | ---: |
| Témoin : 4096, 128 m | 13,000 | 78,648 | 12,259 | 76,922 |
| Résolution seule : 2048, 128 m | 18,425 | 56,376 | 16,659 | 54,275 |
| Portée seule : 4096, 96 m | 12,819 | 79,506 | 12,184 | 78,009 |

Quatre cascades et contact shadows présents dans les trois rapports (16 pas).
Entre témoin et 2048, bornes exactement identiques : 24 / 41,931866 / 73,26172 /
128 m. Le diff JSON des deux profils, après remise à 4096 du seul champ de
résolution, est vide. Coût externe moyen 73,465 → 50,731 ms. Pose différente de
moins de 8 mm après parcours, caméra inchangée. Les budgets 4096/96 m sont aussi
confirmés dans leur rapport, coût externe 74,533 ms, sans gain établi.

Un premier profil High à 64 m a été **refusé** (sortie 2) par la monotonie des
paliers : Medium demande déjà 96 m. Logs et profil `64m` conservés, aucun rapport
de performance valide. La règle n'est pas assouplie ; le contrôle suivant à 96 m
est une entrée différente, valide. PIDs 38208, 29256, 36024 (échec) et 42132 tous
terminés ; absence de processus depuis la racine vérifiée.

**Décision : résolution identifiée comme levier significatif, portée 128 → 96 m
sans intérêt démontré pour cette charge.** Ne pas répéter ces mêmes paires.
2048 reste un candidat de diagnostic, pas un preset livré ni une qualité visuelle
acceptée ; 18,4 FPS reste insuffisant. Nombre de cascades et contact shadows ne
sont pas encore isolés. Aucun gain cumulé ne peut être extrapolé depuis les essais.

Limites : une fenêtre par variante, pas A/B/B/A de qualification ; poses proches
mais non identiques et phase temporelle des animations au gel non normalisée.
Pas de capture du candidat 2048, pas de qualification du jeu vivant ou du matériel
cible. Paramètres source inchangés et Gate 1 toujours ouverte.

| Artefact | SHA-256 |
| --- | --- |
| forge_runtime.exe | `D01FB4564DE5B3F053CF6EAAFC8743F6BD2253A6CA5F75B9DB02E5BDD459FBE3` |
| 4096.json | `F54B62BE6D4DC708F8FBB40EC9864A425D38C2B93DC40D489C27CB36BD67988A` |
| 2048.json | `A747D18F8C90FA86C7DA9F7456295DACE24914EAD8E907CFF2CFA7CFFB271DCD` |
| 96m.json | `46D05C7A5F0E25824776F0436EA07B9B1C4FF3678B1B0DED53EFADFD8B50F94B` |
| 4096.profile.json | `80E5C998B5F061BD9EECADA94D608F8CA8361D83B70DC069CE38C95D149A4252` |
| 2048.profile.json | `5BAF6248041DBF39209A0B140A283D9ADABCB57D70182C5D90D338AAF97CF119` |
| 96m.profile.json | `E7B5F8CEC877A75D41A8FA0FF2893F476A554D340A43600BC482D113F2EAAA44` |

## Contrôle du profil d'ombres : écart majeur observé

Après la paire de compteurs ci-dessous, même exécutable `D9F8BEEB…`, même kit,
parcours, dimensions, 300 échantillons statiques et compteurs GPU activés :
`Measure.ps1 -Label shadow-high` puis `-Label shadow-low` dans la même racine.
Les deux commandes fixent séparément terrain, post-process, eau, environnement,
végétation et pluie à High ; seul `--shadow-quality` change. `--quality` global
n'est pas utilisé, conformément au refus des overrides mixtes par le CLI.

| Profil d'ombres demandé | FPS moyens | p95 ms | 1 % low FPS | Hors First–PostUpdate moyen ms |
| --- | ---: | ---: | ---: | ---: |
| High | 13,057 | 78,711 | 12,292 | 73,106 |
| Low | 29,891 | 34,699 | 25,911 | 29,921 |

Les sources de profil définissent High = quatre cascades, atlas 4096, distance
128 m et contact shadows ; Low = deux cascades, atlas 1024, distance 64 m sans
contact shadows. Plusieurs paramètres changent : ce contrôle **ne permet pas**
d'attribuer les ~43 ms d'écart à la résolution seule. Le rapport actuel ne
sérialise pas l'observation effective du profil d'ombres ; ces valeurs sont
celles du document demandé, pas un relevé GPU des cascades.

Pose High (-132,4841 ; 2,5931 ; -203,5350), Low
(-132,4916 ; 2,5970 ; -203,5237) : écart d'environ 14 mm après contrôleur normal,
pas une position bit-identique. Caméra, taille du personnage et forêt inchangées.
Une paire courte exploratoire, pas un A/B/B/A de qualification ni un jeu vivant.

**Décision : prioriser les paramètres et le chemin d'ombres**, avec observation
effective exportée pour le prochain contrôle. Ne pas remplacer High par Low pour
faire passer la gate, ni prétendre avoir conservé la qualité visuelle : aucune
capture Low n'a été produite ici. Isoler les paramètres puis inspecter le candidat
avant promotion. Le défaut High, le Studio, les assets et les documents restent
intacts ; 29,9 FPS ne satisfait pas non plus l'objectif 60 FPS.

| Artefact | SHA-256 |
| --- | --- |
| shadow-high.json | `98FF338EB517FA323BD0D40E36B904830693B8222BDB35043CCEE58558BDE86C` |
| shadow-low.json | `404386B6F4744795EA57CDAFD9A61131E615E95369FA1A6D0B67F2500A7AB36E` |

PIDs 9208 et 28532 terminés, sorties 0, aucun ERROR Runtime ; absence de processus
depuis la racine revérifiée après les quatre essais. Aucun rebuild supplémentaire
pour la paire d'ombres (CLI existant), fmt et diff-check PASS. Gate 1 reste ouverte.

## Contrôle ultérieur des compteurs GPU

Deux exécutions séquentielles, même nouveau binaire et même charge statique :
`Measure.ps1 -Label on-a`, puis `-Label off-a`, racine
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-pipeline-query-cost-20260908`.
Le second ajoute uniquement `--no-pipeline-statistics`, option autorisée seulement
dans une mesure de parcours bornée. Aucun changement des réglages ordinaires du
Runtime ou du Studio. Le rapport lit les capacités effectives du RenderDevice :
PIPELINE_STATISTICS_QUERY vrai puis faux, TIMESTAMP_QUERY vrai dans les deux cas.
62 diagnostics de rendu puis 28 : l'absence des compteurs est explicite, pas zéro.

| Compteurs pipeline | FPS moyens | p95 ms | 1 % low FPS | Hors First–PostUpdate moyen ms |
| --- | ---: | ---: | ---: | ---: |
| Activés | 13,065 | 78,011 | 12,374 | 73,026 |
| Désactivés | 12,901 | 80,547 | 11,925 | 73,686 |

300 échantillons par essai, poses à moins de 5 mm, Full HD High, même kit BLEND
épinglé. **Aucun gain établi : ne pas désactiver par défaut ni répéter cette paire
inchangée.** Deux runs exploratoires, pas une preuve de régression de 1 % ni une
qualification globale. Timings GPU conservés, aucun compteur indisponible promu
au titre de R1-037. PIDs 10136 et 16952 terminés, sorties 0 et aucun ERROR Runtime.
81 tests Runtime + un test d'intégration, Clippy strict et build natif PASS.
La validation CLI couvre le défaut inchangé et le refus hors mesure bornée.

| Artefact | SHA-256 |
| --- | --- |
| forge_runtime.exe | `D9F8BEEBF3617E68ADACA3A7168DB5DE334F9FABF50A82DB40699A22EF28AD6B` |
| on-a.json | `20644D3095150EFDE7C3DE540843C72DEEBA79F2383DD76F689C2A265B92D5D7` |
| off-a.json | `24386A217E9389E85CCBD906E7FDB7CE8374DABCE1668738DC97D36B97066FCC` |

Lecture du renderer local Bevy 0.19.1 : `bevy_pbr/src/render/light.rs`,
`view_shadow_pass` crée sa passe avec `timestamp_writes: None`, sans span de
diagnostic GPU, contrairement aux passes listées dans le rapport. Aucun timing
d'ombre n'apparaît dans le relevé. Cette lacune explique pourquoi les timings
partiels ne permettent pas d'exclure les ombres ; elle ne prouve pas leur coût.

## Résultat et prochaine décision

La majorité du temps de cette charge statique est **hors First–PostUpdate** :
72,260 ms, contre 3,559 ms dans les phases principales mesurées. Ne pas cibler
les règles de génération ou le compteur de végétation comme explication des
~76 ms sans preuve supplémentaire. Le compteur s'exécute dans PostUpdate, dont
la durée totale moyenne est ici inférieure à 3 ms.

Le segment externe comprend Last, extraction/rendu, présentation, attente du
runner et toute attente entre les bornes. Il ne permet **pas encore** d'attribuer
le coût au GPU, au pilote, à la synchronisation, aux diagnostics de rendu ou à
une fonction précise. Prochaine mesure utile : subdiviser ce segment, notamment
le chemin de rendu et ses attentes. Pas de répétition alpha ni d'optimisation
du domaine à l'aveugle. Aucun gain produit revendiqué.

## Implémentation bornée

`apps/forge_runtime/src/walk_proof/cpu_phases.rs` ajoute cinq schedules de borne
uniquement lorsque la mesure de parcours est activée : avant First, après
PreUpdate, après RunFixedMainLoop, après Update et après PostUpdate. `Instant`
reste indépendant de la suspension du temps virtuel. Le rapport de mesure
exporte les cinq noms et 300 lignes brutes correspondantes.

L'intervalle externe commence à la borne PostUpdate précédente et finit à la
borne First courante ; les autres intervalles suivent jusqu'à PostUpdate courant.
Les lignes ne sont pas exactement alignées aux bornes de `Time<Real>` : comparer
les distributions, pas soustraire chaque ligne au frame time pour fabriquer un
temps GPU. Coût des marqueurs inclus et non quantifié séparément. Aucun changement
du moteur partagé, des assets, de la simulation ou du Studio ; pas de capture
requise pour cette instrumentation non visuelle.

## Exécution réelle

Racine : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-understory-cpu-phases-20260908`.
Commande reproductible : `Measure.ps1`, qui vérifie les pins du binaire et du
kit original, refuse les sorties existantes et termine son processus en finally.
Parcours normal `1800:1:0.22`, freinage, 120 frames de stabilisation puis temps
virtuel figé et 300 échantillons, BLEND original, 1 662 arbres, High Full HD,
VSync demandée désactivée. RX 7800 XT/Vulkan, build dev Forge opt1/dépendances opt3.

Pose finale (-132,4864 ; 2,5933 ; -203,5358), grounded vrai, yaw 3,0750246 rad,
pitch -0,2094395 rad, boom 4,5 m. Même charge locale que le comparatif précédent,
pas une qualification RTX 3060/RX 6600, du Studio ou du jeu vivant.

| Segment mural | Moyenne ms | p95 ms |
| --- | ---: | ---: |
| Hors First–PostUpdate | 72,260 | 73,96 |
| First et PreUpdate | 0,362 | 1,16 |
| Boucle fixe | 0,037 | 0,05 |
| Update | 0,305 | 0,48 |
| SpawnScene et PostUpdate | 2,854 | 3,27 |

Frame time : moyenne 75,827 ms, **13,188 FPS**, p95 77,592 ms, 1 % low 12,164 FPS.
300 échantillons pour chaque segment et pour les frames ; p95 des segments par
rang supérieur (285e valeur triée sur 300). Une seule exécution de localisation,
pas un benchmark A/B d'optimisation. Warning wgpu sur position sans `@invariant`
avec comparaison Equal conservé dans le journal, aucune erreur Runtime.

| Artefact | SHA-256 |
| --- | --- |
| forge_runtime.exe | `122B16E64785E1F0CF1D0D8AC3699ED85729D3BB2B211DDE2E7DB13B12A863F8` |
| phases.json | `F535D38BBDD64110F5471EF2935C6295AAC5DEE34E62620F19C4277A76B848EF` |

PID 11100, sortie 0, terminaison et absence de processus depuis la racine vérifiées.
81 tests Runtime + un test d'intégration PASS ; deux tests nouveaux vérifient la
partition temporelle déterministe et l'exécution des bornes dans Main. Clippy
Runtime all-targets strict, build natif, fmt et diff-check PASS. Les tests ciblés
ne constituent pas une qualification globale. **Gate 1 reste ouverte.**
