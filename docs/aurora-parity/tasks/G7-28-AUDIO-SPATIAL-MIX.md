# G7-28 — Spatialisation et mixage audio

Le profil audio canonique couvre son global 2D ou spatial 3D, rayons interne/externe, courbe
d’atténuation et absorption de l’air. Il porte aussi l’état initial, politique single/restart/
overlap, autorité locale/serveur, fondu et callbacks namespacés.

Les couches adaptatives filtrent heure (y compris plage traversant minuit), météo, tag de lieu,
danger et intérieur. `AreaAudioMixer` produit atténuation, couches actives et politique runtime sans
dépendance moteur. L’écran Environnement expose les réglages essentiels.

Preuves : `AreaAudioMixerTests`, tests environnement/sérialisation et suite globale.

La prochaine tranche unique est **G7-29 — reverb, occlusion et streaming audio**.
