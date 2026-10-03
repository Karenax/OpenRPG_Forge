# Tidewater — passerelles source dans le Playtest Studio

Les trois passerelles de `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`
sont maintenant chargées depuis le paquet vérifié
`2d8270d51c0666b256618740a68069e79f6c1dda6c254062194717c937c734e6`
avec la bibliothèque de bâtiments correspondante. La scène Bevy projette les
5 parties visibles (7 564 triangles) et les 69 boîtes de marche plus les
3 cylindres des poteaux dans Rapier, sous les trois repères source. Le paquet
complet du site a été reconstruit avec `prepare-building-library-site.py` ; le
chargement refuse une divergence du paquet ou de la bibliothèque de matériaux.

Le build WebGPU release et `wasm-bindgen` ont réussi. Le candidat Windows local
est `E:/OpenRPG_Forge/.tidewater-integration/studio-tidewater-boardwalk-candidate-073/forge-studio-candidate.exe`,
SHA-256 `254ea838efdb5e5188c714ba26b4b2c727475aacee2e72599163e8a19b45e83d`.
La [capture Village](../../../../.tidewater-integration/studio-tidewater-boardwalk-smoke-073/tidewater-studio-village-calibrated-040.png)
montre la passerelle et les chemins latéraux au même cadrage 1920 × 1080 que
la [référence source](../../../../.tidewater-integration/baseline-003/village.png).
Le [reçu du parcours Village](../../../../.tidewater-integration/studio-tidewater-boardwalk-smoke-073/tidewater-studio-village-reference.json)
indique une simulation prête ; cette disponibilité dépend aussi de la
synchronisation des 72 colliders Rapier de la passerelle.

À 0,40 EV Studio, le [comparateur apparié](../../../../.tidewater-integration/studio-tidewater-boardwalk-smoke-073/comparison-source-studio-040.json)
donne les écarts moyens de luminance suivants, avant puis après la passerelle :

| Région | Avant | Après |
|---|---:|---:|
| Colline | 8,90 | 8,72 |
| Maisons | 19,27 | 18,91 |
| Premier plan | 16,71 | 12,88 |
| Image entière | 14,02 | 13,10 |

Le [parcours jouable](../../../../.tidewater-integration/studio-tidewater-boardwalk-playtest-074/fish-stand-studio-smoke-ledger.json)
est `passed` : 23 placements de départ, 24 après sauvegarde et réouverture,
fixture source intacte, entrée en Playtest, marche d'environ 3,71 m, effets
océaniques et graphiques actifs, aucune erreur navigateur. À la fin de la
marche, le centre de la capsule est `(53,60 ; 3,46 ; -73,29)` m, au sol. Sa
demi-hauteur est `0,90` m, donc sa base est à `2,56` m ; la boîte de passerelle
source voisine à `z=-73,70` a son sommet à `2,55` m. Ces positions concordent
avec une marche sur la passerelle, en plus de l'enregistrement Rapier.

La parité visuelle reste partielle : la référence comporte encore des bancs,
barils, cordages, accessoires et ombres absents ou différents. Les temps source
et Studio ne sont pas identiques (2,00 s contre environ 3,09 s sur la capture
Village), et la météo/les effets temporels ne sont pas appariés image par image.
Les segments sont pour l'instant des objets source immuables projetés dans la
carte ; leur déplacement et la modification de leur spline ne sont pas encore
des opérations d'édition persistées dans Studio. Ce candidat local n'est ni
publié ni qualifié comme parité finale.
