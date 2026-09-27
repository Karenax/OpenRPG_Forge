# R1-022/023 — accès Studio au candidat optique

2026-09-08, branche Rust. Gate 1 ouverte.

## Implémenté

Le menu des scènes propose « Vallée avec eau transmissive · candidat
expérimental ». Son lien conserve le kit gouverné et les batches de forêt,
avec `forge_water=depth-preview`. Le démarrage installe la ressource moteur
avant les plugins, sans modifier les sources ni le défaut. La sélection est
limitée aux vallées, unique et stricte ; valeurs inconnues et doublons échouent
explicitement par le chemin d'erreur du runtime.

L'état visible distingue la demande du candidat de sa qualification : reflets
d'environnement seuls, SSR et profils non qualifiés, sans sauvegarde. Le menu
standard permet de revenir au rendu par défaut par rechargement. Aucun deuxième
shader Studio ou chemin de mutation spécifique n'est ajouté.

## Vérifié

`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-water-optics-20260908/Verify.ps1`
et ses logs : fmt PASS, **642 tests workspace PASS, zéro échec, un ignoré**,
Clippy workspace strict PASS, check WASM Studio PASS. Test de sélection explicite,
scène, doublons et valeurs invalides inclus. Warning préexistant
`proc-macro-error2` conservé. Session de validation terminée, aucun essai graphique
ou onglet ouvert dans ce lot.

## Build et premier lancement réels

Le bundle a ensuite été construit (`Build.ps1`, logs `web-clippy.log`, `trunk.log`,
`candidate/build.log`). Clippy WASM strict PASS, Trunk release et Tauri PASS.
WASM SHA256 `477551E590FFBAB70628AC4983084E521BE0C73A43741B429213E75B0D101109` ;
EXE SHA256 `AC04E38A90FE6EC555DCE30D894D77D603D76FEFB528D2C19C1D37D3A42E0A3B`.
Pins complets dans `candidate/build-proof.json` (build seulement).

Fenêtre exacte lancée (PID 38420), rendu par défaut inspecté, menu ouvert et
lien « Vallée avec eau transmissive » cliqué avec Computer Use. Le chargement
du candidat échoue ensuite explicitement :
`projection eau invalide : observed_incomplete_components`.
Le pixel de scène derrière l'écran d'erreur n'est pas une qualification de l'eau.
PID 38420 fermé et terminaison vérifiée ; ancienne session utilisateur conservée.

Inspection : l'observateur moteur impose `transmission.steps == 0` et réutilise
l'observation des profils, incompatibles avec le candidat épinglé. Ce constat
n'autorise pas à supprimer le contrôle Studio. Correction du contrat moteur
en cours, avec tests positifs et négatifs des composants propres au candidat.

## Rejeu du bundle corrigé

`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-water-optics-observed-20260908/` :
Build.ps1, Clippy WASM strict PASS, Trunk release et Tauri PASS.
WASM SHA256 `D6B17F57A314F80E1A1A0B8DB66FF96412029956BE55F45093D31FA31E796C90` ;
EXE SHA256 `4690D19A93F962246A11A3E5C375702F43455D9E200FA7E48F0B33627E20D1EB`.

PID 19300 lancé. Menu ouvert et lien transmissif réellement cliqué avec Computer
Use ; après rechargement, scène acceptée sans l'écran de refus précédent.
Les commandes de profil d'eau sont désactivées. Exports JSON et PNG exercés :

- `forge-diagnostic-1788903890874.json`, SHA256
  `D43D7A78BDEB1DD4EA20E4B473172C6C748960CAE7EA4F36F489272C0C6D65FE` ;
- `forge-viewport-1788903906601.png`, SHA256
  `646ACBB6E777BCF8C859A1912A599550DEEE876E1366345DFAAECC9C1282005B`.

Fichiers inspectés et copiés depuis Downloads dans le répertoire de preuves.
JSON : failure_reason null, source_manifest_matched, une caméra active,
water_observation observed_main_world_components, kit 20/20, forêt 1662 arbres.
Warmup final prepared : 94 pipelines, zéro pending et zéro failed ; le pic
d'erreurs transitoires vaut 18 et reste consigné (pas de revendication « zéro
erreur pendant tout le démarrage »). GPU presentation reste not_observed dans
la télémétrie ; le PNG constitue une preuve de pixels distincte.

Mesure indicative, non benchmark, canvas 1408 × 966 : 120 frames, moyenne
29,3475 ms, p95 33,9 ms ; heap WASM 1 543 241 728 octets. Ancienne session
utilisateur préservée pendant l'essai : aucune attribution de coût isolé.
PID 19300 fermé, terminaison vérifiée, aucun enfant direct restant.

## Limites restantes

La vue initiale est trop éloignée de l'eau pour qualifier le mouvement ou les
berges. Le JSON ne sérialise pas encore le détail du mode optique : ne pas
déduire cette configuration de sa seule parité de scène. Les libellés de profil
affichent encore « Matériau d'eau non observé » au lieu de distinguer le candidat
épinglé ; c'est une limite de restitution, pas une promotion implicite du profil.
Prochaine preuve utile : vue rapprochée/animation avec détail optique exporté.
Pas de publication, pas de validation artistique, aucune case de gate promue.
