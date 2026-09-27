# R1 — cycle natif des quatre niveaux de végétation

9 septembre 2026, branche Rust, aucune publication. Gate 1 ouverte.

## Simplification du bois proche — étudiée, non retenue

La piste proposée ci-dessous a été exécutée sur le conifère courant, sans
changement du chêne, du feuillage, des autres LOD, des collisions ou des poses.
Blender 5.2.1 LTS, deux threads, autoexec désactivé, sorties neuves sur E:.
Réduction COLLAPSE à 50 % refusée : erreur symétrique échantillonnée 20,29 mm,
au-delà du plafond 10 mm. Plafond conservé ; candidat 75 % : 14836→11127 triangles
de bois, erreur 7,27 mm dans les coordonnées source. Ce contrôle échantillonne
sommets et centres de triangles dans les deux sens : **pas une borne de Hausdorff
continue**, et l'erreur en monde augmente avec l'échelle du placement.

Le dérivé remplace uniquement le mesh du nœud explicite LOD0 Wood. Préfixe binaire
original conservé, autres champs JSON et tous matériaux/textures conservés ; les
anciennes données non référencées restent dans le fichier (pas un gain disque).
Trois tests Python PASS : déterminisme/préservation, refus foliage/partage/transform/
perte de sémantique, refus sans réduction ou mauvais nœud. Khronos : zéro erreur,
neuf avertissements de génération de tangentes et 22 informations d'objets inutilisés.
Réimport des deux GLB, comptages concordants et paire 640×800 bois seul inspectée :
pas de différence évidente sur cette vue, ni qualification artistique globale.
Dépréciation Blender `World.use_nodes` pour Blender 6 conservée dans le journal.

Kit séparé `r1-wood-near-kit-20260909`, un asset remplacé et cinq conservés.
Quatre parcours A/B/B/A avec le **même** release
`4C4BB888936653294D420A86CDEBE7E69BC2AE0E86E7203B8D5E0EE43BFC25EC`,
même trajet 1800 ticks, Full HD, domaines High et ombres candidates, eau optique,
sans nuages et sans compilation/capture concurrente :

| Passage | FPS trajet | FPS tiers forestier | p95 forêt ms |
|---|---:|---:|---:|
| A1 courant | 73,79 | 51,89 | 22,13 |
| B1 bois réduit | 73,30 | 52,32 | 22,01 |
| B2 bois réduit | 73,31 | 52,09 | 22,02 |
| A2 courant | 73,55 | 51,77 | 22,18 |

1572 ticks communs ; écart maximal de position 7,53 mm, bras caméra 1,88 mm,
orientation constante ; mêmes projections eau/ombres. Statistiques recalculées
par l'analyseur existant, entrées épinglées inchangées, quatre sorties 0 sans
événement ERROR. PIDs 1996, 28072, 11140 et 19908 fermés ; trois Blender 4084,
20100 et 30648 également terminés, absence vérifiée.

**Non retenu : environ +0,7 % forestier sur cette paire, pas de gain substantiel
démontré et aucun seuil atteint.** Le kit actif n'a pas été remplacé. Les quatre
scripts expérimentaux ont été retirés du dépôt après copie récupérable dans
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-wood-near-abba-20260909/tooling` ;
l'extension temporaire de l'assembleur a été retirée. Aucun code renderer ajouté.
Ne pas reprendre cette même réduction du bois comme prochaine optimisation.

Preuves locales : `r1-wood-near-pine-20260909-b` (dérivé/validation),
`r1-wood-near-pine-view-20260909` (réimports/rendus),
`r1-wood-near-abba-20260909` (mesures/analyse/outils archivés).
GLB : `7e1922707a31cb4f6bdc3bd15a1f00e9a499dd835226c955c5b584da4d96db7a`.
Comparaison : `331306D604BC2F94DB3068B0C2591E26502FA67FB987FFA3481412D43F71330E`.
La suite doit viser le feuillage/ses soumissions, pas amplifier la simplification
du bois ou répéter l'eau. Art, profils, GPU cible et Gate 1 restent ouverts.

## Déduplication exacte écartée avant implémentation — 9 septembre

Après la mesure release courante (51,72 FPS dans le tiers forestier), inspection
en lecture seule des deux GLB du kit `r1-atlas-cropped-runtime-kit-20260909`.
Script local `E:/OpenRPG_Forge/.gate1-evidence-runtime/inspect-forest-vertex-reuse-20260909.py`,
Python 3.13 standard, sans Blender, renderer ou nouvelle compilation.
Le script contrôle header/chunks, indices, strides et tuples de **tous** les
attributs, refuse morphs/sparse et vérifie les hashes du manifeste.

Résultat : sur les 20 primitives des trois LOD, chaque sommet référencé possède
un tuple d'attributs distinct. Exemple : feuillage LOD0 chêne 87360 sommets,
conifère 95016, aucun doublon complet. Les positions seules ne suffisent pas :
normales, UV et autres attributs ne doivent pas être fusionnés implicitement.
**Ne pas ajouter de passe de soudure/indexation exacte : aucun gain disponible
sur ces entrées.** Aucun seuil de tolérance introduit, aucun asset modifié.

La fusion simple des trois tons n'est pas non plus équivalente à un simple
changement de couleur : les matériaux référencent des textures PBR distinctes.
Une consolidation exigerait sa propre transformation et ses preuves.
Les anciens essais d'alpha nul/MASK sans gain restent exclus ; ce contrôle ne
réouvre pas ces pistes et ne prouve aucun gain de FPS.

Piste à étudier ensuite, non implémentée : simplification contrôlée du bois dans
un dérivé gouverné, notamment les 14836 triangles du bois LOD0 du conifère.
Conserver nombre/hauteur des arbres, feuillage, sources et retouches ; mesurer
l'écart de silhouette et les LOD avant toute promotion. Ce n'est ni un changement
automatique de densité ni une acceptation d'un asset simplifié.

## Géométrie réellement embarquée — piste LOD intermédiaire

Inventaire en lecture seule du kit courant après retrait de l'essai vertex.
Hashes des deux GLB vérifiés contre `build.json` avant comptage des accessors
des meshes `_LOD0_`, `_LOD1_`, `_LOD2_` ; collisions exclues par leur nom distinct.
Ce sont des comptes d'assets, pas de géométrie simultanément visible ou de coût GPU.

| Arbre | LOD | Triangles totaux | Triangles feuillage | Sommets feuillage |
| --- | ---: | ---: | ---: | ---: |
| Chêne | 0 | 34158 | 29120 | 87360 |
| Chêne | 1 | 8152 | 7462 | 22386 |
| Chêne | 2 | 662 | 546 | 1638 |
| Conifère | 0 | 46508 | 31672 | 95016 |
| Conifère | 1 | 7522 | 5992 | 17976 |
| Conifère | 2 | 1228 | 856 | 2568 |

Les six matériaux de feuillage sont BLEND, alpha factor 1, textures 512².
Lecture des PNG embarqués : chêne 41,99 % de texels alpha zéro, 53,90 % alpha
255, 4,10 % intermédiaires ; conifère 44,95 / 51,83 / 3,22 %. Les trois tons
de chaque arbre partagent ces proportions. Ne pas assimiler ces fractions
d'image au pourcentage de fragments GPU perdus (UV, filtrage, mips et projection).
Le générateur actuel réutilise un motif de feuille/aiguille sur des cartes plus
grandes aux LOD éloignés ; il ne bake pas un groupe de feuilles dans un atlas.

Piste de réalisation retenue pour une **étude isolée**, non encore implémentée :
baker des groupes de feuillage d'origine dans des cartes/atlas de LOD intermédiaire,
au lieu de seulement agrandir le motif d'une feuille. Conserver LOD0, bois,
collisions, positions, hauteur et volume de couronne ; garder les sources et
retouches intactes, lier les dérivés aux hashes. Comparer plusieurs angles et
les transitions avant toute promotion. Aucun objectif FPS promis avant mesure.
La stratégie de groupes feuillus avec des textures distinctes selon le LOD est
décrite dans [GPU Gems 2, Virtual Botany, §1.4](https://developer.nvidia.com/gpugems/gpugems2/part-i-geometric-complexity/chapter-1-toward-photorealism-virtual-botany).
Référence de technique seulement : aucun code, asset ou composant SpeedTree repris.

Rapport local : `E:/OpenRPG_Forge/.gate1-evidence-runtime/foliage-geometry-inspection-20260909.json`,
SHA-256 `A53219F54D6051C99760CCBB63679D4C7E6061B25DF05FBE7A441DD848F7CBE1`.
Inspection avec le parseur GLB existant et Python/Pillow/NumPy fournis, aucune
installation ni conversion Blender, aucun processus graphique lancé. Ce lot
n'est pas un import, une indexation ou une qualification d'asset.

## Rejet anticipé des triangles HLOD masqués — essai retiré

Hypothèse testée le 9 septembre : dans les lots lointains, calculer le dither
par racine avant le vent et rejeter les triangles aux seuls endpoints ±16,
déjà entièrement masqués par le fragment Bevy. Aucun changement de distances,
densité, matériaux, assets, sources ou géométrie. Test de racine constante par
triangle et garde d'ordre du shader ajoutés pour cet essai seulement.

668 tests workspace PASS / un ignoré avant correction stylistique du test ;
Clippy échoue d'abord sur `chunks_exact(3)`, corrigé en `as_chunks::<3>()` avec
reste vide vérifié. Test ciblé rejoué PASS, Clippy workspace tous targets/features,
check moteur web/WASM et build natif PASS. Logs initiaux conservés.

Deux PNG natifs 1920 × 1080, `human_bridge`, horloge figée, inspectés : aucun
défaut évident sur cette vue seulement. Ils ne sont pas identiques pixel à pixel :
hors rectangle overlay [0,0,970,356], 102439 pixels diffèrent sur 1728280, écart
maximal 34/255. Pas de preuve d'équivalence visuelle globale ni d'absence de
popping. Première analyse d'image indisponible dans le Python système (Pillow
absent), analyse réalisée ensuite avec le runtime Python fourni, sans installation.

Quatre marches A/B/B/A, mêmes 1662 arbres, 19944 primitives, kit courant,
eau optique/reflets High, autres domaines High, ombres candidates 2048/3/128 m,
RX 7800 XT, sans VSync demandée. **Builds dev instrumentés**, pas release.

| Passage | Variante | FPS trajet | p95 ms | 1 % low FPS | FPS tiers forestier |
| --- | --- | ---: | ---: | ---: | ---: |
| A1 | témoin | 56,28 | 26,48 | 35,21 | 40,53 |
| B1 | rejet vertex | 56,94 | 26,13 | 36,60 | 41,54 |
| B2 | rejet vertex | 56,67 | 26,32 | 36,22 | 41,53 |
| A2 | témoin retour | 56,83 | 26,20 | 36,45 | 41,53 |

1203 ticks communs, écarts nuls de position et de caméra. Distributions recalculées
à 1e-9, sources et entrées épinglées inchangées. La première comparaison refuse
l'égalité du compteur `animation_updates` (1926/1942), lié au nombre de frames,
pas à un paramètre d'eau. Le contrôle final exige ce compteur positif et l'égalité
de **tous les autres champs** d'eau ; aucun autre écart ignoré.

**Décision : retiré, gain non démontré.** Le témoin retour égale les candidats.
Shader et tests expérimentaux retirés par édition ciblée, trois fichiers restaurés
aux hashes antérieurs contrôlés : shader `2B3747D3…`, `vegetation.rs` `BAD99E1B…`,
`vegetation_far_geometry.rs` `4AA6529C…`. Ne pas relancer cette piste inchangée.
Le travail suivant doit porter sur la représentation géométrique/coût des
feuillages réellement visibles, pas un autre raccourci pour les fragments cachés.

Racine : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-far-vertex-cull-20260909/`.
Scripts externes `Run-FarVertexCull-20260909.ps1` et
`Compare-FarVertexCull-20260909.py` ; logs, pins, poses, captures et shader rejeté
conservés. Six sorties 0, aucun ERROR runtime ; PIDs 32828, 2932, 18056, 3428,
11740 et 32200 fermés, absence contrôlée. Aucun navigateur ou build concurrent.

- Témoin EXE : `6FF06DF81B0701865B17FC7EFE51B48EA90EADE1A1FD81E74A47F14C7957C626`.
- Candidat rejeté : `E70D34274B7C7EC71B1D3956810475A4057A0E1E00E5AB207CED00D881857608`.
- Comparaison : `DB86E72D0DD18FEF8232E1A39B847DDF097E8B96EA28DFACF46DA7814DBA6973`.
- PNG témoin : `BD3858901F7E3D105034FDA055E0D97541CDC6D70415CBB277B879234EDBCFE1`.
- PNG candidat : `ADE406353C3D7F3AC875A39FCE3791746A7D6E03123990C36939453ACA877FB1`.

Attention : l'exécutable dans le cache debug de compilation reste celui de
l'essai rejeté jusqu'au prochain rebuild ; ne pas le livrer comme source restaurée.
Le témoin conservé ci-dessus et le release optique antérieur ne sont pas remplacés.
Pas de build Studio ni qualification de gate, aucun commit/push, aucun asset Cadle.

## Coût vivant High/Medium — release optique actuel

Quatre processus séquentiels High/Medium/Medium/High, sans reconstruction ou
capture pendant mesure. Même binaire release optique
`2C507A416D46FD39835715DB18022CC845CAD3E190FFAC40353CD92CBFB15BCA`,
RX 7800 XT/Vulkan, 1920 × 1080, trajet vivant `1800:1:0.22`, eau claire et
reflets 48 pas/96 m actifs dans les quatre cas. Ombres candidates 2048/3/128 m
et autres demandes High constantes. Seul le domaine végétation change.

High demande 24/72/180/384 m, Medium 20/60/160/320 m pour les distances
near/medium/HLOD/cull. Les deux utilisent 32 brins par patch. Le multiplicateur
de densité de la politique n'est pas appliqué par cet adapter de présentation :
les quatre journaux confirment 1662 arbres et 19944 primitives de projection.
Le fondu de détail observé passe de 24–32 à 20–28 m. Les comptes de projection
ne sont pas des nombres de draws ni des comptes d'arbres visibles.

| Passage | Végétation | FPS trajet | p95 ms | 1 % low FPS | FPS tiers forestier |
| --- | --- | ---: | ---: | ---: | ---: |
| A1 | High | 64,19 | 24,37 | 39,14 | 44,86 |
| B1 | Medium | 68,17 | 22,74 | 41,35 | 47,49 |
| B2 | Medium | 68,67 | 22,64 | 41,85 | 47,85 |
| A2 | High | 64,54 | 24,35 | 39,22 | 44,89 |

Recalcul indépendant de chaque échantillon et des tiers fixes, accord Rust à
1e-9. 1373 ticks communs ; écarts maximum de position 7,53 mm, bras caméra
1,89 mm, yaw/pitch nuls. Sources/kit épinglés inchangés, projection d'ombres
identique, budget optique confirmé, trajets complets, joueurs au sol après
freinage. Codes 0, aucun événement ERROR, PIDs 26744, 400, 15984 et 26972 fermés.
Absence d'exécutable d'essai contrôlée après les mesures.

Décision : gain répété d'environ 6 % dans la forêt, mais p95 et 1 % low toujours
insuffisants. Ni Medium ni des distances réduites ne sont promus en défaut.
Pas de qualification visuelle des transitions en mouvement par ces seuls logs.
Ne pas refaire la comparaison inchangée, ni poursuivre par une succession de
petites réductions de distance ; isoler le coût du rendu forestier actuel.
Les temps GPU de l'ancien replay RenderDoc ne sont pas ceux de ce candidat.

Protocole externe réutilisé : `Measure-Gate1OpticalRelease-20260909.ps1
-VegetationIsolation`, analyse `Compare-Gate1OpticalRelease-20260909.py --forest`.
La variante par défaut conserve le protocole eau précédent. Preuves sous
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-release-optical-forest-20260909/` :
quatre rapports, journaux, observations forestières, pins et `comparison.json`.
SHA-256 comparaison :
`404D40E71BBDC847459F196E47C875FA143E06EE98A02B51149DC41F5E08423A`.
Aucun code produit ou asset modifié, aucun navigateur lancé, aucune publication.

## Exécuté

Un seul processus, sans compilation ni modification moteur/assets : cycle existant
`--vegetation-quality high --vegetation-cycle-proof`, forêt gouvernée en lots,
kit corrigé colorimétriquement, caméra `hero_reference_match`, clair, RX 7800 XT
Vulkan, cinq PNG 1920×1080. High → Low → Medium → Ultra → High.
Les autres domaines ne changent pas : **ce n'est pas un cycle global de presets**.

Racine locale :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-forest-four-tiers-20260909/`.
`Run.ps1` conserve les entrées, refuse les anciennes captures, borne le processus
à 135 secondes (watchdog interne 120), vérifie sortie/erreurs/ordre/readbacks/hashes.
Exécution réelle 06:28:48–06:29:13 UTC, sortie 0. PID 23616 terminé et absence
vérifiée ; aucun enfant encore présent au contrôle suivant. Pas de navigateur ouvert.

## Preuves techniques acquises

- Cinq étapes après 180 observations CPU correspondantes et cinq readbacks frais.
- Cinq projections : 1662 arbres, 19944 primitives de description LOD à chaque fois.
  Ce dernier nombre n'est pas un nombre de draws ni d'arbres visibles.
- 176 lots lointains créés initialement puis réutilisés aux quatre transitions.
- Kit 20 instances / 36 colliders prêt avant la première capture. Cela ne prouve
  pas une traversée ni la stabilité de chaque collider pendant tout le cycle.
- Terrain `18bd933d37f06a6a08a34b19938a78cb8f6fe8d9667b34bd2dcd6355956fd208`
  et végétation `89ceeea8569c8940c863ca8682808baa50c3a9c0af5e2aa34b366667e02ac2d8`
  identiques aux cinq étapes. Contrôle complémentaire des comptes/lots/sources PASS.
- Zéro ERROR ; arrêt `runtime_errors_observed=false`. Avertissements Vulkan et
  shader conservés dans le journal, pas de déclaration « sans avertissement ».

## Inspection et limites

Les cinq PNG ont été ouverts et inspectés. Forêt proche, pont et terrain restent
visibles ; portée forestière nettement réduite en Low et étendue en Ultra, puis
retour de la couverture High. La silhouette lointaine n'est pas qualifiée : limites
de peuplement visibles, tramage sur certaines cimes, fines traînées sur le sol.
L'origine des traînées (géométrie/ombre/autre) n'est pas établie par ces images.
Eau standard encore striée et horizon abrupt ; aucune modification de ces effets.

Horloge animée : High initial/final ne sont pas une paire pixel-identique. Pas de
parcours franchissant les seuils LOD, de vent isolé, de vidéo de transition, de
mesure GPU complète ni de benchmark cible. Les valeurs glissantes de l'overlay
incluent chargement/transitions/captures et **ne qualifient pas les performances**.
Le garde `vegetation=Unqualified` reste intact ; ce cycle de domaine ne demande
pas d'acquittement GPU du plan global. Aucune case R1-040 ou Gate 1 promue.

Ce résultat remplace l'affirmation générale « Medium/Ultra non rejoués » pour
**cette projection native seulement**. Le cycle courant Studio correspondant et
les critères ADR-0010 restent distincts. Ne pas répéter ces cinq captures inchangées :
la prochaine observation forêt doit porter sur les transitions en déplacement,
puis les défauts effectivement localisés, pas sur un nouvel audit de chargement.

## Identité des artefacts (SHA-256)

- EXE : `3ED00CED70A722941ADECEFD95508A7E866125E4760B0D27706FCC44D8E48E10`.
- Kit build : `0F1304723BAD88F4AB01D0710DC1D2B7F1B0AD073B2745EC39B90FD6943BBD48`.
- Preview : `1156BDB6C2A0EB7D2DA5C0A6D61CB0C70CA691B8C7131B170401287CE3EC3A9D`.
- Catalogue : `E3289D7DF81D06639421037F4B9F7ECEF69D494869A1BBC6179CAD3CF2BE5884`.
- PNG High : `5E462B719B3D6B990542857A9148D4B2399F8A8D0C2BB606A79DC8185B5C7BA3`.
- PNG Low : `B2B4D5315DE0A42B2F830D06AD59A365C8F35B53E86D96D47782107ED4D53F23`.
- PNG Medium : `7F6E585EF2EE0F4255C0B3D092CB3BFAFD7CE2A8DC82B2CC6C25DB68E9282DFE`.
- PNG Ultra : `0AE2140106CCE8306618055AA18F46E41D39FDD3C206AB5882BFCA6D4946D414`.
- PNG High restauré : `5F088EB0D72F5E2D0B54DD80234B67EC27F9503FB59FE3BF021BD2BFB26DD0FB`.

Le WorldSketch embarqué a été accepté par les vérifications source du runtime ;
le hash brut du fichier courant relevé par le script n'est pas le hash canonique
du document ni une attestation de compilation de ce fichier dans l'EXE.

## Parcours natif du candidat — complément du 9 septembre

Exécution indépendante du cycle, même EXE et mêmes trois descripteurs du kit
(hashes revérifiés après). Racine
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-forest-walk-20260909/` : `Run.ps1`,
`stderr.jsonl`, `stdout.log`, `final.png`. Aucun changement moteur ou authoré.
Profil global High demandé, forêt en lots, caméra joueur, marche normale Rapier
`1800:1:0.22`, capture à partir de la frame 2400, sortie à 2500, deadline externe
110 secondes. Pas de réglage équilibré d'ombres injecté dans cet essai.

Rejeu commencé 06:32:26 UTC, terminé 06:32:58 : position
(14,999651 ; 5,150231 ; -224,99998) → (-132,49252 ; 2,5985398 ; -203,51808),
soit environ 149 m de déplacement horizontal net. Au terme des 120 ticks de
freinage : `grounded=true`, vitesse horizontale nulle. Ce n'est pas une distance
de chemin mesurée ni une preuve de navigation automatique.

Deux observations de fenêtre par Computer Use montrent la progression vers les
arbres puis l'arrivée dans le peuplement. Le PNG final Full HD a ensuite été
ouvert et inspecté. Une troisième observation UI a échoué après l'arrêt normal
de l'application (`foreground window did not report a process id`) ; aucun
processus n'a été relancé pour cela. Le journal confirme capture vérifiée puis
arrêt succès le 06:33:30 UTC, sans ERROR runtime. PID 10796 terminé, absence du
processus et de ses enfants vérifiée. Aucun onglet navigateur créé.

La scène reste lisible, arbres proches imposants et peuplement dense ; aucun
écran vide ou disparition globale dans ces observations. En revanche, tramage
visible sur des troncs/cimes intermédiaires et rendu encore artificiel. Les deux
instantanés et le PNG final **ne prouvent pas la continuité temporelle des LOD**,
ni l'absence de popping entre les observations. Pas de vidéo acquise ou de
franchissement de chaque seuil identifié par arbre. R1-014/040 restent ouverts.

La cadence finale glissante affichée est 36,35 ms moyens, p95 37,74 ms,
1 % low 25,3 FPS ; elle signale un coût perceptible à l'intérieur de la forêt,
mais ce n'est pas un benchmark ni une comparaison d'optimisation. La statistique
GPU de passe opaque (environ 1,5 ms) n'est pas le temps GPU total : ne pas en
déduire que la scène est CPU-bound. Les isolations d'ombres antérieures dans
`R1_SHADOW_BALANCED_CANDIDATE.md` doivent être prises en compte avant toute
nouvelle campagne générale ; ce passage n'a ni appliqué ni requalifié ce candidat.

SHA-256 PNG : `59A5311AA0716DDFF6B13E32F09000BF733E65A7F688AD4B2E4DB07032BCF922`.
SHA-256 journal : `1E08FAE8F1EE6D8FAB188087AE95E5899FCD9E753EE8D8E39D0ABEA4F72019D9`.
Résultat acquis : parcours du contrôleur et arrivée observée dans la forêt avec
le candidat courant. Résultat encore manquant : transitions temporelles,
performance acceptable, parité Studio et acceptation humaine. Ne pas rejouer
ce parcours inchangé pour seulement reconfirmer son chargement ou son arrivée.
