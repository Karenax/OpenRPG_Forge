# Tidewater — raccordement natif de la capture de réfraction

Date : 1er octobre 2026. Référence : `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`.
Suite du [parcours Studio 181](2026-10-01-studio-refraction-motion.md).
Cette tranche poursuit la reprise du [rapport d'exposition Studio](2026-09-30-studio-exposure-sweep.md) :
la calibration Village du candidat 096 reste celle de ce rapport, sans
qualification automatique d'un rendu natif ultérieur.

## État du travail

Le raccordement est implémenté ; la tranche 182 conserve ses cinq tests du
module partagé et ses vingt tests natifs passants. L'assemblage 192 produit
un candidat identifié, avec un verdict **build-only**. Ses deux essais
runtime 193 et 195 échouent, pour les causes conservées ci-dessous.
La construction réparée 202 échoue sur l'environnement des macros du
lancement direct de rustc. La reprise 206 compile le même code, passe ses
21 tests natifs et produit le candidat 202 identifié ci-dessous. La
compatibilité de compilation WebGPU 192 passe pour le module partagé.
Le runtime 203 passe, mais son audit indépendant échoue ; la régression
de nage et d'ombres 204 passe avec ses audits. La compilation de la preuve 211
échoue sur une expression de macro JSON ; sa réparation 215 compile, passe
24 tests natifs et produit un candidat identifié avec le verdict build-only.
Le runtime 216 et son audit indépendant passent ; la régression de nage et
d'ombres 217 passe avec ses deux audits inchangés.
**Le raccordement natif est qualifié localement par le triplet statique 216
et la régression 217, sur le candidat 215 identifié.**
L'objectif complet reste actif ; cette tranche ne clôt pas la parité de l'île.

## Raccordement

Le natif réutilise la passe Bevy partagée, ses miroirs du terrain et du village
opaque, la capture RGBA16F à demi-résolution et la copie Depth32Float reversed-Z.
Le shader de l'eau reçoit les mêmes textures et indicateur aux bindings 51–53.
Le choix de caméra et la copie de profondeur principale excluent `CaptureCamera`.
Le nouvel ordre explicite sélectionne la caméra avant `Synchronize`, puis
publie la disponibilité de la capture vers le matériau natif. Le couplage
inutile au résident océan propre à Studio est supprimé ; les exigences de
caméra, terrain, shaders et cibles restent vérifiées par la passe partagée.

Le panneau graphique natif expose « Capture de réfraction », activée par
défaut. La sauvegarde, le rechargement, la réapplication et le reset sont
couverts par un test du vrai chemin de contrôle. Un ancien profil sans ce
champ conserve ses autres valeurs et active le nouveau réglage par défaut.
Ces contrôles headless ne constituent pas une manipulation utilisateur OS.

## Preuve additionnelle préparée

Le module natif de preuve est opt-in, réservé à une projection statique.
L'essai initial emploie une copie isolée de la scène claire 004 avec la pose
côtière sèche du Studio 181 : seule la métadonnée `fixture.json` change,
sans mutation des assets ou de la source de l'île. Cet essai 193 échoue
sur l'oracle d'herbe de la fixture originale. La reprise 195 utilise donc
la scène 004 originale, sans changer cet oracle ; elle échoue ensuite sur
l'enregistrement du callback. Aucun de ces essais ne valide le triplet.
La projection native conserve son contrat near/far ; ni cette préparation
ni ces essais ne prouvent la projection source complète.
Le profil graphique de comparaison désactive adaptation d'exposition, grain,
TAA et flou ; le réglage de capture est seul à varier 1/0/1 dans le processus.

Chaque variante doit enregistrer les matrices réelles de la caméra principale
et de la caméra dérivée, la projection, les textures liées et les compteurs.
Les phases actives doivent produire des lectures HDR/profondeur réelles,
avec de nouveaux fichiers bruts pour la répétition. L'audit indépendant décode
les PNG et les textures, contrôle couverture/profondeur à chaque pixel et
exige une répétition exacte. Les seuils d'effet .01 RGB / 1 000 pixels avec
delta supérieur à 2 sont ceux d'une preuve de visibilité, sans seuil de parité.

Le parcours de nage antérieur est conservé dans une exécution séparée sur le
même binaire : sept étapes, trois paires d'immersion et triplet d'ombres Neutral.
Les anciens audits restent inchangés. Les nouveaux essais auront leurs reçus
et leur verdict propre, y compris sur échec, avant toute qualification.

[Tests partagés](../../../../.tidewater-integration/native-refraction-unit-tests-182.log),
[vingt tests natifs](../../../../.tidewater-integration/native-refraction-example-tests-182.log),
[49 entrées bornées avant construction](../../../../.tidewater-integration/native-refraction-final-inputs-182.json),
[provenance de la vue](../../../../.tidewater-integration/native-refraction-fixture-182.json),
[protocole et limites](../../../../.tidewater-integration/native-refraction-proof-design-182.md).

Les entrées bornées ne représentent pas un snapshot intégral de l'arbre partagé.
L'identité du candidat 192 est enregistrée ci-dessous ; la compatibilité
WebGPU est enregistrée ci-dessous ; l'échec de capture 203 reste conservé,
distinct du triplet statique 216 et des régressions GPU 204 passantes.
La saccade initiale, le TAAU, les cas
d'occlusion, les autres contenus/comportements et la comparaison avec horloge
source identique gardent leurs critères ouverts.

## Suite de fidélité identifiée pendant la construction

L'analyse du snapshot source propre et figé distingue le TAAU source du TAA
Bevy expérimental. Le resolve source attend Halton32, historique HDR et de
profondeur, vitesses sans jitter, profondeur finale après eau et masque d'eau.
Les matériaux Tidewater désactivent actuellement les prépasses ; la profondeur
opaque seule ne prouve pas ces entrées. Le placement actuel de l'occlusion et
de la brume après tonemapping diffère aussi du chemin HDR source.

La prochaine tranche devra établir ces entrées et leur ordre sur le renderer
Bevy existant, puis porter le resolve exact, avec lectures et comparaisons.
Cette inspection n'est ni une implémentation ni une qualification TAAU.
[Analyse source et chemins vérifiés](../../../../.tidewater-integration/tidewater-taau-next-design-184.md).
Le contrat proposé pour les entrées temporelles fait l'objet d'une note de
conception distincte ; cette note ne constitue ni code intégré ni preuve GPU.
[Conception des entrées temporelles](2026-10-01-temporal-auxiliary-design.md).

Le module pur `tidewater_temporal_frame.rs` prépare Halton32 et les matrices
courantes/précédentes sans système ECS ni passe GPU. Les 32 paires attendues
ont été vérifiées par un calcul JavaScript indépendant de la référence.
Le module reste **non exporté** : il n'intervient pas dans le candidat 185.
Son formatage et ses cinq tests purs passent dans le harness 187. Le shim
réexporte les véritables types glam 0.32.1 utilisés par bevy_math 0.19.1,
avec le fingerprint de cette dépendance vérifié. Cette preuve isolée ne
couvre pas l'export Cargo, ECS, les entrées GPU ou le resolve. Aucun TAAU
produit n'est déclaré implémenté ou qualifié à partir de cette préparation.
[Tests purs](../../../../.tidewater-integration/temporal-frame-cpu-187/tests.log),
[provenance et commandes](../../../../.tidewater-integration/temporal-frame-cpu-187/qualification.json),
[identité des types réels](../../../../.tidewater-integration/temporal-frame-cpu-187/math-types-provenance.json),
[état sans export ni processus restant](../../../../.tidewater-integration/temporal-frame-cpu-187/final-scope-check.json).

## Construction interrompue et reprise

La première construction 182 est interrompue pendant la copie du cache
incrémental après un échec de hardlinks. Le processus compilateur a cumulé
seulement 6,59 s CPU en plus de vingt minutes, avec 3,28 Go lus et 3,57 Go
écrits puis presque plus de progression des compteurs pendant les observations
suivantes. Son journal et reçu conservent l'échec/interruption ; aucun binaire
natif 182 n'est qualifié. Les vingt tests déjà terminés restent valides.

Seule la chaîne de processus appartenant à cette construction est arrêtée,
après vérification de ses commandes et parents. Les sources et fichiers de
cache restent préservés. La reprise 185 désactive `CARGO_INCREMENTAL` pour ce
processus uniquement, avec les mêmes sources, features et optimisation native.
Aucun gain de rendu n'est déduit de ce changement de construction.
[Interruption et compteurs](../../../../.tidewater-integration/native-refraction-build-interruption-182.json).

## Stockage physique 191 et assemblage 192

Le contrôle 191/192 corrige une présomption fondée sur le préfixe `E:` :
le cache logique `E:/OpenRPG_Forge/.build/cargo-target` résolvait vers `F:`.
Le manifeste conserve cet alias et sélectionne des copies isolées sur le
stockage physique `E:/OpenRPG_Forge/.tidewater-integration/native-refraction-ssd-inputs-191`.
Il recense 331 entrées totalisant 6 166 506 036 octets ; 300 copies isolées
représentent 5 826 200 738 octets. La vérification finale confirme les SHA256,
le stockage physique E et l'absence de références F dans les arguments du lien.
Le reçu de préparation reste `ready_verified_not_linked` : il ne crée pas
d'exécutable et ne qualifie aucun rendu. Le chemin logique E figurant dans
le harness CPU 187 ne constitue pas non plus une preuve de stockage SSD ;
ses empreintes et son identité mathématique restent la portée de cette preuve.
[Manifeste et résolution des chemins](../../../../.tidewater-integration/native-refraction-ssd-inputs-191/manifest-192.json),
[vérification finale avant lien](../../../../.tidewater-integration/native-refraction-ssd-inputs-191/verify-192.json).

L'assemblage 192 réemploie le codegen natif 185 terminé, avec les mêmes
octets d'entrée et `/DEBUG:NONE`, sans recompilation. Les 49 entrées bornées
ne présentent aucune dérive. Le lien sort avec le code 0 en 15,89 s et
produit 126 347 776 octets, SHA256
`cad033a84b44953b4c3cc51f59c8df63f15ad7383c0e4bf2dffce09a64676272`.
Le reçu garde son verdict **build-only** ; la durée de construction ne
mesure ni la performance du jeu ni la qualité visuelle.
[Assemblage](../../../../.tidewater-integration/native-refraction-assembly-192/assembly.json),
[identité du candidat](../../../../.tidewater-integration/native-refraction-candidate-192/candidate.json).

## Échecs runtime 193 et 195 conservés

Le runtime 193 utilise ce candidat et la fixture dérivée côtière. Il sort
avec le code 101 sur `native grass cells differ from original oracle` :
la sélection produit zéro cellule, alors que l'oracle original en attend
sept à ce niveau. Modifier seulement la caméra ne rend donc pas cette
fixture compatible avec le contrôle statique d'herbe. Le reçu garde
`failed`, `process_closed = true` et `source_preserved = true`.
[Reçu 193](../../../../.tidewater-integration/native-refraction-static-runtime-193/run.json),
[assertion d'herbe](../../../../.tidewater-integration/native-refraction-static-runtime-193/stderr.log).

Le runtime 195 reprend la fixture 004 originale : ses deux empreintes de
fixture sont identiques. Il sort aussi avec le code 101, cette fois à
l'enregistrement de l'observer de capture d'écran. Bevy 0.19.1 refuse
l'observer exclusif qui demande `&mut World` avec `On<ScreenshotCaptured>`.
Les avertissements de readback vers un canal fermé suivent cette interruption ;
ils ne constituent pas une validation de textures. Son reçu reste `failed`,
avec processus fermé et source préservée. Ces deux échecs ne sont ni effacés
ni relabellisés par la réparation suivante.
[Reçu 195](../../../../.tidewater-integration/native-refraction-static-runtime-195/run.json),
[refus de l'observer exclusif](../../../../.tidewater-integration/native-refraction-static-runtime-195/stderr.log).

## Réparation callback 202 et construction 206

La réparation remplace l'observer exclusif par un callback
`On<ScreenshotCaptured>, Commands`. Il conserve les données possédées de
l'image et de la phase, puis applique les mêmes contrôles de signature,
pixels, fichier et disponibilité via `commands.queue` dans le monde.
Les observers de readback conservent leurs paramètres typés. Un nouveau
test headless enregistre le vrai callback et appelle `world.flush()` ;
il doit détecter le refus d'observer sans événement, écriture PNG ni GPU.
Ce test passe dans les 21 tests de la reprise 206 ; il qualifie
l'enregistrement de l'observer headless, sans exécution du callback GPU.

Les 49 entrées bornées de 202 diffèrent de celles de 182 uniquement pour
`refraction_proof.rs`, SHA256 réparé
`38ce3dd1c25722b0a9764a1519b6ac73e2eb42d8109dcfd9e40a416dc0daf32b`.
Le script réutilise les dépendances vérifiées de 191, conserve l'optimisation
native 1 et les assertions, désactive les informations de debug, et compile
seulement le programme natif et ses tests sans Cargo. Le premier garde-fou
de préparation attendait treize `--extern`, alors que la commande 185 en
possède douze : ce reçu `failed-precompile` est conservé ; le script préparé
vérifie désormais les douze dépendances réelles.
[Entrées de la réparation](../../../../.tidewater-integration/native-refraction-final-inputs-202.json),
[échec du garde-fou initial](../../../../.tidewater-integration/native-refraction-repaired-build-preparation-202.json),
[protocole de construction réparée](../../../../.tidewater-integration/build-native-refraction-repaired-202.ps1).

La compilation 202 échoue ensuite avant les tests : les macros de dérivation
signalent `CARGO_MANIFEST_DIR is not defined`. Le lancement direct de rustc
n'avait pas reproduit les variables fournies normalement par Cargo ; les
1 592 erreurs finales sont une cascade issue de ce défaut d'outillage,
sans verdict sur le produit ou le callback. Le reçu `failed`, son code de
compilation 1 et le journal sont conservés.
[Échec d'outillage 202](../../../../.tidewater-integration/native-refraction-repaired-build-202/build.json),
[journal des macros](../../../../.tidewater-integration/native-refraction-repaired-build-202/compile-tests.log).

La reprise 206 conserve les mêmes 49 sources et les douze archives natives.
Elle emploie les mêmes octets des 36 DLL de macros dans le répertoire isolé
physique E de 205, distinct du répertoire de copie complet. Elle fournit
explicitement `CARGO_MANIFEST_DIR`, le manifest et les variables du paquet,
et limite `TEMP`/`TMP`/`TMPDIR` au répertoire temporaire de cette construction.
Ces variables sont restaurées à la sortie ; aucune modification globale
d'environnement ou de source n'est déduite de cette reprise.
[Snapshot des macros prioritaires](../../../../.tidewater-integration/native-refraction-host-macros-205/ready-priority-isolated.json),
[commande et environnement 206](../../../../.tidewater-integration/build-native-refraction-repaired-206.ps1),
[49 entrées inchangées de 206](../../../../.tidewater-integration/native-refraction-final-inputs-206.json).

Les codes de compilation des tests, d'exécution des tests et de compilation
du binaire sont tous 0. Le journal compte **21 tests natifs passants**, dont
l'enregistrement du vrai observer. Le reçu conserve **build-and-tests-only**.
Le candidat figé dans `native-refraction-candidate-202` provient de cette
construction 206 : 126 390 784 octets, SHA256
`2db2c70a75e1799407c426d0b7f6ad815e8c55d3277ba2f2b1c0f5ffd21138a8`.
Son reçu garde **build-only**, avec les empreintes du reçu de construction
et des entrées 206 ; ce nom de candidat ne transforme pas l'échec 202 en succès.
[Résultat de construction 206](../../../../.tidewater-integration/native-refraction-repaired-build-206/build.json),
[21 tests natifs](../../../../.tidewater-integration/native-refraction-repaired-build-206/tests.log),
[identité du candidat réparé](../../../../.tidewater-integration/native-refraction-candidate-202/candidate.json).

| Travail | État de ce rapport |
|---|---|
| Construction directe 202 | Échec d'outillage conservé ; tests non exécutés |
| Reprise 206 du callback réparé | 21 tests PASS et binaire compilé ; build-and-tests-only |
| Triplet natif actif / arrêté / répété 203 | Runtime PASS, audit indépendant FAILED conservé |
| Régression de nage et d'ombres 204 | Runtime et audits 159/156 PASS, sur le candidat 202 uniquement |
| Stabilisation de la preuve 211 | Compilation des tests FAILED, avant tout runtime |
| Réparation 215 | 24 tests PASS et candidat identifié ; build-and-tests-only |
| Triplet statique 216 du candidat 215 | Runtime et audit indépendant 182 PASS, sans retry |
| Nage et ombres 217 du candidat 215 | Runtime et audits indépendants 159/156 PASS |
| Compatibilité de compilation WebGPU 192 | PASS de compilation du module partagé ; aucune exécution WebGPU déduite |
| TAAU produit | Non intégré ; cinq tests CPU purs 187 seulement |

Le contrôle WebGPU compile le module partagé pour `wasm32-unknown-unknown`
avec les features `web,qualification` : son reçu est `passed`, avec le code
Cargo 0. Ce contrôle ne prouve pas une exécution GPU dans Studio.
[Commande de compatibilité](../../../../.tidewater-integration/check-refraction-webgpu-192.ps1),
[reçu de compilation WebGPU](../../../../.tidewater-integration/native-refraction-webgpu-check-192.json).

## Runtime 203 réussi, audit indépendant échoué

Le runtime 203 du candidat 202 sort avec le code 0, ferme son processus et
préserve la fixture originale. Son reçu et le triplet produit sont `passed`.
L'auditeur indépendant 182 reste toutefois **failed** : à la ligne 107,
la moyenne RGB du readback partagé antérieur ne correspond pas exactement
à celle du fichier RAW effectivement reçu plus tard. L'échec initial est
conservé séparément dans `refraction-capture-verification-initial-failure-210.json`.
Il ne doit pas être relabellisé à partir du succès du runtime.
[Runtime 203](../../../../.tidewater-integration/native-refraction-static-runtime-203/run.json),
[triplet et fichiers réellement reçus](../../../../.tidewater-integration/native-refraction-static-runtime-203/probe.refraction.json),
[audit échoué](../../../../.tidewater-integration/native-refraction-static-runtime-203/refraction-capture-verification.json),
[échec initial conservé](../../../../.tidewater-integration/native-refraction-static-runtime-203/refraction-capture-verification-initial-failure-210.json).

Le diagnostic donne, sur le canal bleu, `0.06456512847984279` dans le sample
partagé et `0.06456512845040839` dans le RAW, soit environ `2,94344e-11`.
La différence de checksum est réelle. Cet écart est compatible avec le
changement d'un pixel f16 `(509, 211)`, de `0.0220336914` à `0.0220489502` ;
sa cause source reste inconnue. La tolérance RGB de l'audit reste `1e-12`,
avec `rtol = 0` : aucun élargissement n'est utilisé pour faire passer 203.
[Diagnostic indépendant conservé](../../../../.tidewater-integration/native-refraction-static-runtime-203/readback-consistency-diagnostic-211.json).

Les autres constats sont conservés avec leur portée : PNG actif/répété,
pixels RGB décodés et RAW couleur/profondeur actif/répété sont identiques.
L'effet actif/arrêté atteint une moyenne absolue RGB de `0,305046457` et
127 608 pixels avec un delta supérieur à 2. La capture active a été inspectée
visuellement. Ces constats ne lèvent pas l'échec de cohérence du readback
partagé et ne prouvent pas la parité avec Three.js.

## Régression de nage et d'ombres 204 passante

Le runtime 204, sur le même candidat 202, sort avec le code 0 et ferme son
processus. L'audit 159 des cartes d'éclairage passe les sept étapes et les
trois paires Float/Dive/Neutral. Il conserve un retry de ligne d'eau dans
la bande de 2 cm, avec les invariants de locomotion, dérive, caméra et délais
inchangés ; aucun critère de l'audit n'est assoupli.
[Runtime 204](../../../../.tidewater-integration/native-refraction-swim-runtime-204/run.json),
[audit des cartes et de la nage](../../../../.tidewater-integration/native-refraction-swim-runtime-204/light-maps-verification.json).

L'audit 156 du point d'entrée des ombres passe également : 5 533 pixels
présentent un delta supérieur à 2, avec une moyenne absolue RGB non arrondie
de `0,0211377958`. Le retour actif/répété retrouve exactement le PNG et les
pixels décodés ; le triplet d'ombres a été inspecté visuellement. Cette
régression locale de l'effet additif ne qualifie ni la capture 203 ni le TAAU.
[Audit indépendant des ombres](../../../../.tidewater-integration/native-refraction-swim-runtime-204/refracted-shadow-verification.json).

## Stabilisation de la preuve 211 et réparation 215

La correction préparée est limitée au programme de preuve natif : elle demande
les readbacks partagé et RAW ensemble, sur la frame figée `hold + 40`.
Le PNG attend plus de 40 frames principales et encodages de capture, la validité
GPU et les deux callbacks. Les checksums FNV64 du payload sans padding doivent
correspondre exactement avant le PNG ; ce contrôle n'est pas une comparaison
directe des deux buffers octet par octet. L'auditeur conserve ses comparaisons
RAW et PNG exactes entre les phases active et répétée.
Si la stabilité manque, au maximum deux retries restent permis dans le délai
total de 180 s, avec conservation des preuves et noms RAW distincts par
tentative. Aucun seuil d'effet, de couverture ou de comparaison n'est réduit.
Les callbacks tardifs d'une phase ou tentative antérieure sont ignorés.
La source antérieure est sauvegardée ; le module Engine et `lib.rs` restent
ceux du candidat précédent.
[Source de preuve avant correction](../../../../.tidewater-integration/refraction-proof-before-stability-211.json),
[contrat et formatage de la correction](../../../../.tidewater-integration/refraction-proof-stability-211.json).

Le garde-fou de préparation 211 constate également un ajout d'exemple dans
`Cargo.toml`. Le reçu vérifie que seul `source_fragment_capture_probe_v3` est
ajouté, sans changement du paquet, des features, des dépendances ou des lints ;
le manifest antérieur est préservé. Le snapshot borné garde 49 entrées : la
preuve et ce manifest diffèrent de 206, les 47 autres entrées restent identiques.
Les douze archives natives partagées restent celles de 191 ; ce snapshot ne
prouve pas une recompilation de la bibliothèque Engine.
[Refus initial du garde-fou](../../../../.tidewater-integration/native-refraction-preparation-drift-211.json),
[vérification de l'ajout seul](../../../../.tidewater-integration/native-refraction-cargo-drift-verification-211.json),
[49 entrées 211](../../../../.tidewater-integration/native-refraction-final-inputs-211.json).

La compilation des tests 211 échoue ensuite avec le code 1 : la macro
`serde_json::json!` interprète une expression de tableau suivie d'un index
comme un tableau JSON. La réparation 215 ajoute seulement les parenthèses
requises autour de cette expression Rust ; elle ne change ni shader, seuil,
contrôle de stabilité, délai ou nombre de retries. La source avant réparation,
le journal et le reçu 211 échoué restent conservés. La source réparée porte
le SHA256 `21e8c18c92b16f79ff7102b705235db98a6f2b120f9bbd637ae9653a67ca36e0`.
[Échec de compilation 211](../../../../.tidewater-integration/native-refraction-repaired-build-211/build.json),
[journal conservé](../../../../.tidewater-integration/native-refraction-repaired-build-211/compile-tests.log),
[réparation bornée 215](../../../../.tidewater-integration/native-refraction-macro-fix-215.json),
[source avant réparation](../../../../.tidewater-integration/refraction-proof-before-macro-fix-215.rs),
[49 entrées 215](../../../../.tidewater-integration/native-refraction-final-inputs-215.json).

La construction 215 passe la compilation des tests, leur exécution et la
compilation du binaire, avec trois codes 0 et **24 tests natifs passants**.
Les trois nouveaux tests vérifient le checksum sans padding, la borne de deux
retries et la mise en file simultanée des demandes shared/RAW après 40 frames.
Le candidat 215 conserve **build-only** : 126 399 488 octets, SHA256
`286fd821d55fb8bbf609ca71a2c217139fc80022c5598ddbf20f2a6459f7d7e6`.
Ses 49 entrées portent le SHA256
`a9c994a83851d1bee0ee85a6988d909f59bc3057593cfc84c4de76e12ea6d9b2`.
Le runtime 216 et son audit passent ; le runtime 217 et ses audits passent
sur ce même candidat 215. Les PASS 206/204 et l'échec 203 restent attachés
au candidat 202.
[Construction 215](../../../../.tidewater-integration/native-refraction-repaired-build-215/build.json),
[24 tests natifs](../../../../.tidewater-integration/native-refraction-repaired-build-215/tests.log),
[identité du candidat 215](../../../../.tidewater-integration/native-refraction-candidate-215/candidate.json).

Les générateurs préparés 209 et 214 restent sans exécution ; le générateur 218
emploie la nouvelle numérotation et les identités finales du candidat et des
49 entrées. Son gel est limité aux suites explicitement nommées, aux archives
sélectionnées physiquement sur E, aux scripts de contrôle et aux rapports/plan.
Il refuse une sortie existante, exige l'absence de dérive et la confirmation
des résultats et de leur réconciliation documentaire, puis conserve un état
`evidence-snapshot-only` avec l'objectif complet actif. Le manifeste constitue
le reçu du gel ; sa présence et son verdict doivent être lus directement.
Les runtimes 212/213 abandonnés n'ont jamais été exécutés et ne sont pas créés.
[Générateur borné 218](../../../../.tidewater-integration/write-native-refraction-artifacts-218.py),
[manifeste d'artefacts 218](../../../../.tidewater-integration/native-refraction-artifacts-218.json).

La suite de fidélité reste de raccorder le contrat temporel pur aux vues Bevy
sans second renderer.
Elle devra prouver les positions sans jitter, les matrices courantes et
précédentes, les vitesses, la profondeur finale et le masque d'eau avant
le resolve source. La saccade initiale, la même horloge que la source,
les cas d'occlusion et la fidélité complète des contenus/effets restent ouverts.

## Triplet statique 216 qualifié localement

Le runtime 216 du candidat 215 sort avec le code 0, ferme son processus et
préserve la fixture originale. La preuve se termine en `8,9735866 s`, sans
retry ni mismatch de readback. L'auditeur 182 inchangé retourne **passed** :
PNG, pixels RGB décodés et RAW couleur/profondeur sont exacts entre les phases
active et répétée. La capture active a pour SHA256 PNG
`6136df133fcf8a1f35b76bd461f517e82d047ceea3c609958f97f6e58b112dbd`.
L'image active a été inspectée visuellement.
[Runtime 216](../../../../.tidewater-integration/native-refraction-static-runtime-216/run.json),
[triplet et reçus de readback](../../../../.tidewater-integration/native-refraction-static-runtime-216/probe.refraction.json),
[audit indépendant 182](../../../../.tidewater-integration/native-refraction-static-runtime-216/refraction-capture-verification.json).

Les requêtes RAW actives sont émises à la frame 522, soit `482 + 40`, et celles
de la répétition à la frame 619, soit `579 + 40`. Les readbacks partagé et RAW
ont des checksums FNV64 et des statistiques concordants, avec les signatures
réelles de caméra et des captures ; cela ne remplace pas une comparaison
directe des deux buffers. La comparaison cryptographique et des pixels
décodés active/répétée est contrôlée séparément par l'auditeur.

L'effet actif/arrêté atteint `0,3050464570473251` de moyenne absolue RGB,
364 182 pixels modifiés et 127 609 pixels avec un delta supérieur à 2,
avec un delta maximal de 127. La couverture comprend 118 023 pixels et aucun
échantillon non fini ; le masque alpha correspond à la profondeur sur chaque
pixel. La scène de capture comprend une partie de terrain et 140 parties
de village. Cette preuve statique locale ne prouve ni horloge source identique,
parité complète Three.js, TAAU, performance ou exécutable redistribuable.

## Régression de nage et d'ombres 217 qualifiée localement

Le runtime 217 sort avec le code 0 et ferme son processus sur le même candidat
215. L'audit indépendant 159 passe les sept étapes et les trois paires
Float/Dive/Neutral, avec 3 820 ticks et aucun retry de ligne d'eau. Les critères
de dérive, caméra, délai, retour à la plage et paires d'immersion restent ceux
de l'auditeur. Les treize captures finales sont conservées avec le reçu.
[Runtime 217](../../../../.tidewater-integration/native-refraction-swim-runtime-217/run.json),
[audit des cartes et de la nage](../../../../.tidewater-integration/native-refraction-swim-runtime-217/light-maps-verification.json).

L'audit indépendant 156 des ombres passe : 4 111 pixels ont un delta supérieur
à 2 parmi 7 022 pixels modifiés, avec une moyenne absolue RGB de
`0,01827594521604938` et un delta maximal de 46. Le PNG et les pixels décodés
actifs/répétés sont exacts. Ces résultats qualifient la régression native
bornée du raccordement ; le triplet d'ombres a été inspecté visuellement.
Ils ne remplacent ni les preuves TAAU ni la parité
visuelle et comportementale complète avec la source.
[Audit des ombres 217](../../../../.tidewater-integration/native-refraction-swim-runtime-217/refracted-shadow-verification.json).

## Revue du contrôle indépendant

La revue préalable corrige un faux échec possible dans l'auditeur : les bornes
de profondeur GPU sont des `f32`, sérialisés sous leur décimale JSON courte,
alors que Python lit cette décimale en `f64`. La comparaison vérifie désormais
les bits `f32` après conversion, comme les données brutes. Une valeur voisine
en `f32` est toujours rejetée ; aucun seuil de couverture, d'effet ou de
répétition n'est élargi. La version avant correction reste conservée.
Ce contrôle ponctuel et la revue du protocole ne qualifient pas l'exécution GPU.
