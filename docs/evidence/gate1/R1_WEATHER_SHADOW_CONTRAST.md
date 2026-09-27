# R1 — contraste météo sans écrasement soustractif des ombres

9 septembre 2026, branche Rust, aucune publication. Gate 1 ouverte.

## Cause et portée

Le complément de `R1_WATER_LIT_SCATTERING.md` isole les aplats noirs avec
une paire à variable unique : contraste pluie 1,08 puis 1,00. La rivière et
le pont retrouvent leurs détails sans changer le shader d'eau. Le contraste
linéaire de Bevy soustrait de la lumière sous son pivot 0,5, puis écrête.
Le problème est commun à la scène, pas une couleur de rivière à retoucher.

## Implémentation

`weather.rs::color_grading_component` adapte le contraste renforcé à une
courbe positive autour du gris linéaire 0,18 :
`(0.18 * (x / 0.18)^contrast)^(1 / gamma)`.
Pour contraste > 1, les champs Bevy deviennent contraste 1,
gain `0.18^(1/contrast - 1)` et gamma `authored_gamma/contrast`.
Ces opérations existent déjà dans le pipeline : aucune nouvelle passe,
dépendance ou copie de shader Bevy. Contraste <= 1 conserve la réponse
linéaire précédente, notamment le résultat plat intentionnel à zéro.

Ce changement concerne tous les profils météo utilisant cet adapter,
y compris le ciel clair ; il n'est ni un réglage de carte, ni une hausse
artificielle de l'éclairage. Les documents, leurs hashes, les matériaux,
l'eau et l'exposition authorée ne changent pas. Les champs diagnostiques
`mapped_contrast` / `mapped_gamma` restent les valeurs moteur effectives.
Le gain dérivé est documenté ici mais n'a pas de champ diagnostique dédié.

## Validation exécutée

Racine locale :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-weather-shadow-contrast-20260909/`.
`Validate.ps1` conserve tests workspace, Clippy moteur, check WASM Studio et
build natif. `Capture.ps1` réutilise le profil soleil réduit original
`weather-dim.json` (contraste pluie toujours 1,08), la caméra `water_close`
et la frame 300 ; sortie bornée, journaux, hashes et fermeture vérifiée.

Le premier test CPU incluait x=0,001 avec gamma 0,1 et contraste 2 : résultat
sous la plage représentable f32, assertion stricte positive en échec.
L'échantillonnage commence désormais à 0,01 pour cette grille extrême.
Le test vérifie positivité, monotonie, gris pivot, réponse gamma et noir nul,
ainsi que la conservation exacte des paramètres pour contraste <= 1.
Il ne prétend pas supprimer les limites de quantification GPU/f32.

649 tests workspace PASS, zéro échec, un ignoré préexistant. Clippy moteur
all-targets/all-features strict PASS et check Studio WASM
`--no-default-features --features web --lib` PASS. Build natif PASS.
Deux erreurs de procédure conservées : comparaisons float exactes refusées par
Clippy (corrigées en comparaisons de bits) et première commande WASM avec les
features desktop par défaut (corrigée sans toucher au code Tauri).
L'avertissement de future incompatibilité `proc-macro-error2 2.0.1` subsiste.

SHA256 source `weather.rs` :
`4CC8BE3C32E5EF11EF95FD8A99DCBD0C86A7BC71AA6108119FF3D6EB8BD8BA67`.
SHA256 EXE : `FCDBD29107C2AFA320877D63C71436240E2EA2CC3BF474143D930F0CF550B0D9`.
Le shader eau conserve son hash
`A77D5448998A00BDB318C5A20A730503EA466D589A79878655E03B8B35468B54`.

Deux captures natives du même EXE acquises et inspectées :

| Cas | PNG SHA256 | moyenne / p95 / 1 % low affichés |
| --- | --- | --- |
| Soleil réduit, pluie, contraste 1,08 | `591F385FB61DFF410490C2172E80F9E53D7888A8A261D8AA45F82A1694F5CB94` | 9,77 ms / 15,67 ms / 55,0 FPS |
| Ciel clair embarqué, contraste 1,06 | `4A1EC1775AAC446070F8F095399B267EFDC473DF88B3413D7C2A14E25433C4F3` | 10,39 ms / 17,74 ms / 52,3 FPS |

Journaux `dim.*.log` et `clear.*.log` : sorties zéro, un `capture_verified`
chacun, aucun ERROR. PIDs 40256 et 23344 fermés puis absence revérifiée.
Les relevés courts ne qualifient pas la performance cible.

Inspection : les grands aplats noirs du pont et de l'eau ont disparu dans le
cas sombre, sans neutralisation du contraste authoré. Le ciel clair conserve
les détails du pont et du lit. Ombres encore présentes, eau toujours trop
uniforme/brune selon les portions ; pas de qualification artistique globale.
La correction générique est conservée. Aucun asset, profil de carte ou source
persistante modifié. Le check WASM ne prouve pas les pixels Studio : bundle et
rejeu WebGPU restent à faire, avec la contribution lumineuse du lot précédent.
Temporalité et profils complets restent ouverts. Aucun passage de gate.
