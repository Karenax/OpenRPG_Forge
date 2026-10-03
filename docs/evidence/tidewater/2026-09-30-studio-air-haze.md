# Tidewater — brume marine dans le Playtest Studio WebGPU

La perspective aérienne native issue de `src/post/AirHaze.js` est maintenant
appliquée dans le Playtest Studio. La caméra reprend la LUT atmosphérique déjà
préparée pour la zone importée. Le panneau graphique expose « Brume marine »
de 0 à 4 (défaut 1,60) et transmet les changements à la passe Bevy sans
redémarrer la simulation. Le commit de référence reste
`dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`.

Le [parcours isolé du candidat 058](../../../../.tidewater-integration/studio-tidewater-haze-smoke-058/fish-stand-studio-smoke-ledger.json)
est `passed` : ouverture de la bibliothèque et de la zone, placement de
l'échoppe, sauvegarde/réouverture, entrée en Playtest et marche. La source
d'auteur est intacte, aucune erreur navigateur n'a été capturée, la FFT avance
de 131 à 139 et ShoreSim de 155 à 163 pendant les manipulations graphiques.
Le parcours règle la brume à 0 puis 1,60 et revient au défaut. Le mouvement
est encore observé au tick 842. L'exécutable isolé est
`E:/OpenRPG_Forge/.tidewater-integration/studio-tidewater-haze-candidate-058/forge-studio-candidate.exe`,
SHA-256 `68a9f4f22240defe4a4923f8681dd7c7ff99f7ac8e4e5e925226362096483b8d` ;
WASM release `6b15149f99d7c84df7dcc2c4f33a4472fe2c56e049b2b5789e7b520f78019315`.

Les [captures à 0](../../../../.tidewater-integration/studio-tidewater-haze-smoke-058/fish-stand-haze-0.png)
et [à 1,60](../../../../.tidewater-integration/studio-tidewater-haze-smoke-058/fish-stand-haze-1.6.png)
font 1920 × 1080 à caméra fixe. En échantillonnant un pixel sur deux dans des
rectangles identiques, la luminance sRGB moyenne passe de 60,94 à 79,31 dans
la forêt et de 52,41 à 62,58 sur le relief ; elle passe de 217,06 à 216,98
sur le sable proche et de 115,05 à 115,09 dans le ciel. Les nuages continuent
à évoluer entre les prises : ces mesures corroborent la perspective aérienne
visible, mais ne sont pas un A/B pixel parfait.

La passe est exécutée après le post-traitement principal de Bevy et ne
reproduit pas encore exactement l'ordre HDR/temporel de la référence. Le
Playtest Studio ne publie pas encore l'observation d'immersion utilisée pour
désactiver cette brume lorsque la caméra est entièrement sous l'eau ; ce cas
reste à qualifier. Les rayons solaires, le flare très discret, les variations
de feuillage/nuages et une comparaison source/Forge à caméra, météo, heure et
exposition appariées restent ouverts. Ce candidat local n'est pas publié.

Validation : `cargo check` WASM, build WASM release, `wasm-bindgen`,
`Invoke-FishStandStudioSmoke.ps1 -SelfTest`, formatage ciblé et parcours
WebGPU réel réussis.
