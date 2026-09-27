# R1 — Vue de preuve WorldSketch figée dans Studio

7 septembre 2026, branche Rust, travail local non publié. Gate 1 ouverte.

## Besoin et implémentation

La caméra d'exploration laisse l'eau trop éloignée pour isoler une petite variation de
teinte ; un changement de bouton observé côté CPU ne constitue pas une preuve optique.
Le mode explicite `forge_view=<id>` sélectionne donc une caméra **déjà authorée** dans
le WorldSketch chargé. Aucun déplacement n'est écrit dans la carte.

Le plugin partagé `FixedProofViewPlugin`, sous feature `qualification`, valide le document,
l'identifiant et la direction, réutilise l'unique caméra perspective active, puis applique
pose/FOV/clips au démarrage. Temps virtuel figé depuis Startup ; temps réel et rendu continuent.
Le Studio désactive l'échantillonnage du joueur dans ce mode. Une URL sans ce paramètre
garde le fonctionnement antérieur. Paramètre vide/répété, scène incompatible, caméra inconnue
et combinaison avec `forge_probe` refusés explicitement.

Le statut de caméra fixe et les états de reflets/teinte sont inclus dans l'export diagnostique
existant. L'interface signale « sans sauvegarde, hors benchmark ». Cette installation caméra
n'est ni un acquittement GPU ni une acceptation artistique. Pas de capacité Web promue.

## Protocole

Compiler Studio Web puis servir le bundle local. Exemple :
`?forge_scene=world-sketch-visual-valley&forge_view=human_bridge`.
Comparer profil, reflets d'environnement seuls, sans reflets, retour au profil ; teinte
activée/désactivée/restaurée. Exporter observations et images sur la même vue et le même
bundle. Ne pas utiliser les timings de la scène figée comme benchmark.

Les tests ECS ciblés initiaux ont vérifié la conservation de l'entité caméra, des sources,
de la pose et de la perspective, le temps virtuel nul/pausé et le refus des topologies 0/2.
Le test de parsing refuse les URL ambiguës. Un premier Clippy a refusé deux comparaisons
directes de flottants dans les tests et une convention de documentation : corrigés sans
désactiver de règle. Les résultats finaux et observations Web sont consignés ci-dessous
après exécution ; ce protocole seul ne vaut pas succès.

Preuves locales : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-fixed-view-20260907`.
La première erreur est conservée dans `clippy.log`.

## Résultats exécutés

- Workspace : **540 tests PASS, 0 échec, 1 timing préexistant ignoré** ; Clippy strict
  workspace/all-targets/all-features et check Studio WASM PASS. Les dernières modifications
  sont le masquage de l'aide interactive, le refus de pointer-lock et le libellé d'attente
  dans le chemin WASM ; bundle final recompilé, tests de domaine/parser inchangés.
- Bundle **dev**, pas release/Tauri : `web-final2/forge_studio_bg.wasm`.
- Navigateur intégré : `human_bridge` appliquée, monde initialisé une fois, caméra 1/1,
  six sources de scène alignées, cinq sources du plan identiques après retour aux réglages.
  L'aide interactive n'est plus affichée dans cette vue fixe. Aucun error console retourné
  pour le lancement valide. Caméra inconnue testée sur le premier bundle : refus explicite,
  monde non initialisé, sans substitution silencieuse par une caméra par défaut.
- Neuf readbacks GPU PNG **768 × 606** téléchargés, ouverts et inspectés. Les fichiers locaux
  sont vérifiés, contrairement au simple message de téléchargement demandé dans le produit.
  Copies conservées dans la racine de preuve, originaux laissés dans Downloads.
- JSON `forge-diagnostic-1788740565119.json` relu : pose de preuve nommée, état reflets/teinte,
  sources présentes ; `gpu_capture_pair_atomic=false`, `gate_passed=false`.

La désactivation complète de réflectance assombrit nettement l'eau ; la restauration retrouve
son aspect clair. Cela établit une réponse optique Web à ce contrôle, **pas une isolation IBL** :
la réflectance agit aussi sur l'éclairage direct. Les autres variations sont insuffisantes
pour conclure : un résidu entre images restaurées persiste, malgré le temps virtuel figé.
Sa cause n'est pas qualifiée ici et aucune identité pixel-perfect n'est revendiquée.

Mesure reproductible en lecture seule : `Measure-Optics.ps1`, RGB 8 bits, somme absolue
des trois canaux, pas de 2 pixels. Région eau avec quelques berges : x=[230,550[, y=[330,375[,
3680 échantillons ; témoin sol : x=[240,550[, y=[420,550[, 10075 échantillons.

| Image comparée à 1788740489800 | Écart moyen eau | Écart moyen sol |
| --- | ---: | ---: |
| 1788740514916 — environnement seul | 5.778804 | 0.050720 |
| 1788740522905 — réflectance zéro | 56.564130 | 0.066998 |
| 1788740533360 — profil restauré | 6.765761 | 0.051414 |
| 1788740555005 — teinte zéro | 3.833696 | 0.047643 |
| 1788740564840 — teinte restaurée | 3.672554 | 0.045757 |

Contrôle supplémentaire sans SSR, référence 1788740671445 : teinte zéro (1788740679101)
écart eau 4.082880, restauration (1788740689117) 3.549457 ; témoins 0.050223 et 0.000496.
La teinte reste donc non concluante dans cette vue, même sans SSR. Ne pas la promouvoir
sur ce signal, ni déduire une réfraction écran. Le cadrage reste assez éloigné et la
présentation visuelle reste prototypale. Ce lot n'a pas modifié les shaders ni les sources.

## Suite et limites

La pose figée réutilisable et son export sont maintenant disponibles pour une comparaison
Web contrôlée. Il reste à isoler l'IBL et à obtenir une preuve SSR/teinte distinguable du
résidu de restauration, avec un cadrage d'eau adéquat. Ne pas gonfler les paramètres authorés
pour rendre le test artificiellement visible. Aucune capacité Web déclarée Supported,
aucune adaptation activée. Pas de benchmark, acceptation artistique, Tauri, matériel cible
ou passage de Gate 1. CI distante **NOT RUN** ; aucun commit/push ni asset Cadle.

## Diagnostic du résidu — mécanismes temporels indépendants du temps virtuel

Lecture du code **Bevy 0.19.1 épinglé localement**, sans modification de dépendance :

- `bevy_pbr/src/ssr/ssr.wgsl`, lignes 190–217 : le bruit SSR dépend de
  `globals.frame_count` (couche de texture STBN ou graine procédurale), pas de l'horloge
  virtuelle. Figer les vagues ne fige donc pas cet échantillonnage.
- `bevy_anti_alias/src/taa/mod.rs`, `prepare_taa_jitter`, lignes 353–381 : décalage
  suivant une séquence Halton de huit positions indexée par `FrameCount`.
- `water/quality.rs::apply_water_quality` retire SSR en mode environnement mais conserve
  explicitement TAA/prépasses, car leur usage est partagé. Le contrôle sans SSR n'isole
  donc pas tous les effets temporels. Ne pas supprimer TAA dans le produit pour obtenir
  artificiellement un test passant.
- `bevy_time::update_virtual_time` copie bien le temps virtuel vers le `Time` générique
  utilisé par l'animation Forge. Les horloges réelle et de frames doivent continuer.
- `ScreenshotCaptured` ne fournit que l'entité et l'image ; la réception asynchrone ne
  donne pas directement le numéro de frame GPU échantillonné. Le compter à la réception
  ne constitue pas une preuve de phase identique.

Ces chemins démontrent que « temps virtuel figé » n'implique pas « pixels déterministes ».
Ils ne quantifient pas la part respective TAA/SSR/autres effets dans le résidu Web mesuré.
La prochaine comparaison doit d'abord mesurer plusieurs restaurations/témoins à état
identique et tenir compte de cette dispersion, ou porter une véritable identité de capture
côté renderer. Une seule paire A/B ne suffit pas pour les différences faibles. Ne pas figer
le compteur global de frames : les preuves GPU fraîches et les deadlines ne doivent pas
être faussées. Pas de nouvelle série de captures tant que le protocole ne distingue pas
ce bruit de la contribution recherchée.

Sources locales consultées (SHA-256) : SSR
`DF112162A9302FCAD97AA1430A13EE2C306637FA4D93FE439822992167A869A9`, TAA
`6B359518A77E3754057582AEF3729EA6554745917CE5CBE676EF1ACC6FE5CFEB`.
Le test de vue fixe est renforcé avec une horloge réelle synthétique de 16 ms : il contrôle
séparément le temps virtuel, son alias générique, l'avancement réel et celui du compteur de
frames. Aucun code de production ni shader changé par ce complément.
Validation du complément : les deux tests ciblés `fixed_proof_view` PASS (dont le test
renforcé), Clippy moteur all-targets/all-features avec `-D warnings` PASS, codes de sortie 0.
Pas de nouveau verdict workspace ou navigateur : les 540 tests et le bundle ci-dessous
restent la preuve du lot précédent, dont le code de production est inchangé.
`clock-tests.log` SHA-256 : `315D9D489371CB68ADFE31D16E3EE98D73C51D3CE165EAEF911377C4DCA8D358` ;
`clock-clippy.log` : `895C9788B77D1DAB9E2212B55CAFC2246A210D84F32962DB64BBB1CB89257452`.

## SHA-256 des artefacts du cycle Web

Les noms numériques ci-dessous désignent `forge-viewport-<nombre>.png`.

| Artefact | SHA-256 |
| --- | --- |
| web-final2/forge_studio_bg.wasm | 0FCB6E4EFBFA4783940939E7DB301B68ED23E8A45E01545F48CBA1C759992301 |
| tests.log | A41B71E344B4756CE2437E766D27377E4FF684B1E6B941CEE6A24202C8DF45ED |
| clippy-final.log | 0E29B328C8192B2E436B6C208F3B0B3620AD9275C4C3125D8E21AD2A13E54893 |
| wasm-final.log | D780A722BE9895B25D3B472F53264F835E87D3528E71AE05D0C0B41418978E12 |
| web-build-final2.log | 6176935680CD534A02C30394C0CCC4611293057B03B871B395F3E72F7AF50B8B |
| forge-diagnostic-1788740565119.json | CF0A6E264F8223E08CA8B6B595CD609FE0AE661DAB2F6B319579CEC87CF7C29A |
| 1788740489800 | C4B49B53FEB14230229CB7C70EE3AC84859861FC0F5A606957AB4E617CC17A49 |
| 1788740514916 | 0533195B61BCD8B91335020FE0AABD8F4A76C7EF0C9D751A21E04585BAD6A0D1 |
| 1788740522905 | 2B6D31A8B04F9FB05EC8951579DC89C57A55FFF813742BA07766F3235061B976 |
| 1788740533360 | A3BB2C046F70416382B8FD7241700DBB9064872840AFA75486EF26963A139015 |
| 1788740555005 | 4C68CA958431C56E1C10E551D28EA68B399B8A2D0BCAFD8F50F1235C3FB42403 |
| 1788740564840 | AFC318C184F7BA1FC9F2DC0E5B019C35560FA6ACF65D0E0861DEB98BA09842E6 |
| 1788740671445 | FB735077169A2B5EAD8922EA8B4FABE1B5DD60687EF7635F09F61C51EE741CC3 |
| 1788740679101 | 32A6143333D93C8B8AD3A20BF8E8B1416D52C4FCC4C1DADB4E492F1F5ABBE7A8 |
| 1788740689117 | BA99A383132D8285C5633A11D056732435BA24AAB51CD9052002C6E4B5C10CAB |
