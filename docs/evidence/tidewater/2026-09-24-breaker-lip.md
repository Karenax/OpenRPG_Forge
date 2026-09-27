# Ruban visible des déferlantes dans Bevy

Le ruban original utilise maintenant les crêtes du FFT/calcul natifs partagés.
La topologie est reconstruite en Rust depuis le nombre de stations ; le shader
lit directement le ShaderBuffer des crêtes. Le matériau conserve le profil
balistique original, Fresnel, reflets du ciel, éclairage transmis, dentelle et
écume. Il est double face, à alpha prémultiplié, testé contre la profondeur
sans écrire dans celle-ci. Le rendu transparent suit l'océan.

`prepare-breaker-lip.py` contrôle les acquisitions et prépare la fixture.
Les adaptations WGSL concernent les bindings Bevy, interfaces vertex/fragment
et matrices caméra ; les fonctions du ruban et ses textures restent originales.
Le champ Frame.view est renommé sans modification du layout mémoire.

## Image et comparaison

Fixture `water-breaker-lip-seaward-001`, invocation native avec
`--water-breaker-compute OCEAN_EXPORT SHORE_EXPORT BREAKERS_EXPORT`.
La présence du shader préparé active le matériau ; l'ancienne fixture sans
shader conserve son mode compute seulement.

Capture native `native-breaker-lip-001.png` inspectée, SHA-256
`18388db2aefd24477cf368151a5dadd9733be6ae27b9a304160b0487570f6a16`.
Référence `source-water-breakers-002.png`, caméra côtière à 2 s.
`breaker-lip-comparison-001.json` compare les captures avec/sans ruban de chaque
moteur, puis les couleurs dans la région modifiée par la source, dilatée d'un pixel :

- Source : 1 485 pixels modifiés ; natif : 1 484 ; intersection : 1 484.
- Région comparée : 2 391 pixels ; erreur moyenne par canal 0,115154/255.
- Percentile 99 du maximum par pixel : 2/255 ; maximum absolu : 13/255.
- Moyenne sur toute l'image : 0,174457/255, incluant les différences antérieures.

Ce résultat ne prouve pas une égalité pixel pour pixel ni une parité temporelle.
L'effet reste qualifié sur cette seule pose et cet instant.

## Répétition et régressions

`breaker-lip-regression-001.json` confirme une deuxième capture identique,
la capture sans ruban inchangée, et pour chacune des trois exécutions :
21 fichiers FFT, six fichiers ShoreSim et buffer de crêtes identiques aux
références natives précédentes. Aucun résultat FFT/crêtes source n'est injecté.

Compilation réussie (`breaker-lip-build-001.log`) et Clippy strict des deux
exemples réussi (`breaker-lip-clippy-002.log`). Binaire scène exécuté :
`977fbdcde7424e3413fb5fa080f5e7b97072dd28ba60685d41a9b4322592af62`.
Après compilation, seul un attribut de lint pour le champ utilisé par la scène
a été ajouté au module partagé ; aucun changement de calcul.

Résultats sous `E:/OpenRPG_Forge/.tidewater-integration/`. Embruns, dépôts couplés,
animation interactive, autres vues/temps, Studio et qualification globale restent
ouverts. Aucun critère TW complet n'est fermé.
