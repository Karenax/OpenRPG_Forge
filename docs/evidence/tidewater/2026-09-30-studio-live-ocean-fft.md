# Tidewater — FFT océan vivante dans le Playtest Studio

Le paquet d'entrées océaniques acquis localement reste épinglé au manifeste
`ba9da93d9012341d180712e1ddcf8143f7878770e19ce7ba477269aa24e9b879`
et à la référence `1438b1abfcaee3267092b75573014f4d9b4a983c`.
`OceanPackage` valide les hashes et les métadonnées ; `PreparedOcean` vérifie
les uniformes initiaux et ceux du pas 120. Le test du paquet réel
`acquired_fft_inputs_match_both_manifests` passe.

Au démarrage du Playtest importé, Studio prépare ce paquet et monte
`tidewater_ocean` dans le renderer Bevy existant. Les quatre cascades et neuf
kernels WGSL soumettent le bootstrap de 120 pas puis des pas pilotés par
l'horloge réelle. Le statut du Playtest expose la phase et le dernier pas soumis.
Cette donnée est un accusé de soumission CPU/render graph, pas une lecture des
pixels GPU.

Le candidat isolé
`E:/OpenRPG_Forge/.tidewater-integration/studio-tidewater-ocean-candidate-021/forge-studio-candidate.exe`
a le SHA-256
`6c0e833291c092d4c07bfa99256bba2fb971c434e261a16a153abc2aea7fb1ab` ;
son WASM a le SHA-256
`68cf1cd8a33446f416e090bc8d29c4b3fd9b51d5f999e47e33fb09f8e1922187`.
`cargo check` WASM, build WASM release et `wasm-bindgen` passent.

Le parcours Studio complet sur copie isolée a réussi deux fois. Le second
reçu, avec `-RequireOceanFft`, est
`E:/OpenRPG_Forge/.tidewater-integration/studio-tidewater-ocean-smoke-022/fish-stand-studio-smoke-ledger.json`.
Il vérifie le bootstrap, puis une progression de l'étape 131 à 139 sans
suspendre la simulation. Après déplacement, le statut montre l'étape 237,
333 ticks de jeu et 119 images de nuages dynamiques. La fixture source est
inchangée et aucune erreur navigateur n'a été capturée. Le parcours vérifie
aussi pose, sauvegarde, réouverture, réglages graphiques et retour édition.

Il manque encore la consommation des textures FFT par le maillage océanique,
le matériau optique, les interactions de rive/nage dans Studio et la
comparaison d'images animées avec la référence. La mer n'est pas visible dans
ce candidat ; la progression du compteur ne prouve pas à elle seule les
valeurs GPU ni la parité visuelle.
