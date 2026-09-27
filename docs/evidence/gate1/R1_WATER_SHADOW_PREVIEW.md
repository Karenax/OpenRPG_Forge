# R1-022/023 — ombre opaque du candidat transmissif

2026-09-08, branche Rust. Suite de `R1_WATER_FLOW_PREVIEW.md`.
**Gate 1 ouverte ; candidat non promu dans le Studio.**

## Implémentation

La surface d'eau du candidat reçoit `bevy::light::NotShadowCaster` au spawn.
Le lit transmis ne doit pas être obscurci par une ombre opaque de l'eau elle-même.
La réception d'ombres est conservée ; berge, arbres, pont et rendu opaque par
défaut ne changent pas. Aucun changement de shader, carte, géométrie ou profil.

Le shadow atlas standard ne représente pas la transmission colorée de l'eau.
L'absorption dans le candidat reste calculée sur le rayon de vue, pas sur le
trajet de lumière solaire : ne pas revendiquer caustiques ou transport complet.

Test de non-régression : les deux modes sont instanciés ; l'eau seule cesse de
projeter en mode candidat, continue de recevoir, et les berges gardent leurs
deux comportements. Vérification CPU ECS distincte de la preuve visuelle.

## Vérification

Répertoire de preuves :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-shadow-preview-20260908/`.
`Verify.ps1` rejoue fmt, tests workspace, Clippy strict, check WASM et build natif.
641 tests PASS, zéro échec, un ignoré. fmt, Clippy workspace strict,
check WASM Studio et build natif PASS. Warning préexistant de compatibilité future
`proc-macro-error2` conservé. Aucun changement de dépendance.

- `water.rs` SHA256 :
  `470F3C48884A3134E7F2B87C7F7F66BBBDDA9F3250AEC2F384F80FE50EB7DC3F`.
- Binaire SHA256 :
  `BB9A0D649E9C06C11DA73F9AB4DD5A2D192A29F6492C1C2782042E0A2782C273`.
- `hero_reference_match.png` SHA256 :
  `DA2D759CBA419271221EF1197D4520E62D2694C855B431BFC9B9CB21E3BDABD1`.

Capture native inspectée, même caméra, temps figé, kit et configuration d'ombres
que le témoin `r1-water-flow-preview-20260908/hero_reference_match.png`.
Shader principal inchangé, SHA256
`C13048B9CEEE749B9BF353FFEA70957A9DC4FCB5CABD834C36550D1AFE59DE1B`.
La rupture sombre vers y=740 disparaît ; le fond demeure visible sans cette
frontière de cascade. Une ombre reste visible sous le pont. Cela confirme le
défaut de projection opaque de l'eau dans ce cadrage ; cela ne qualifie pas tous
les angles, toutes les ombres reçues, les profils ni l'animation.

Overlay non benchmark : moyenne 19,78 ms, p95 26,08 ms, 1 % low 33,9 FPS.
Aucun gain de performance revendiqué. PID 9288 terminé, exit 0, capture vérifiée,
aucun ERROR. Aucun onglet ouvert ; processus de validation terminé.

## Limites et suite

Correctif du candidat seulement, pas encore livré dans le Studio. Reflets SSR,
profils complets et mouvement restent non qualifiés. L'apparence des berges et
du fond n'est pas déclarée réaliste sur cette seule image. Intégration Studio
et revue du candidat en mouvement restent nécessaires ; Gate 1 ouverte,
aucune publication Git ni promotion de case de qualification.
