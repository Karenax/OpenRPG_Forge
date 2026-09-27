# Cube HDR partagé avec les adaptateurs de scène

`forge_engine::tidewater_environment::cube_rgba16f` décode les six faces HDR et
leurs mipmaps sans accès fichier. Les dimensions, le nombre de niveaux, la
longueur exacte et les valeurs binary16 non finies sont contrôlés avant la
création de l'image. Les bytes acquis ne sont pas convertis ; vue Cube,
RGBA16Float, ordre par couche et sampler linéaire restent ceux du prototype.

Le prototype prépare maintenant cette image validée avant de lancer Bevy.
Le cube noir explicite des anciens modes de test sans environnement reste
local au prototype ; le chargeur partagé n'invente aucune ressource manquante.
Cette extraction permet de fournir les mêmes données d'environnement au futur
adaptateur Studio. Elle ne génère pas encore le cube depuis le ciel dynamique.

Check et Clippy strict ciblé du prototype réussissent. L'exemple
`tidewater_environment_probe` est ajouté pour vérifier les bytes du vrai cube
et les rejets de troncature, dimensions invalides, infini et NaN. Son build et
son exécution réussissent : 1048320 octets conservés exactement, six faces et
six mipmaps. Les cinq contrôles négatifs passent et Clippy strict de cet exemple
réussit également.

Le prototype est recompilé puis lancé avec le paquet 003. La capture inspectée
`native-shared-environment-001.png` est identique à
`native-village-lighting-uniform-001.png` : zéro pixel différent et deux buffers
d'ombres identiques. Hash PNG :
`243ddf9659f95afea4bbd978b2b47006983d052fe3d841958c6f45182a3c4e78`.
Le log est sans erreur ; métriques dans `shared-environment-render-proof-001.json`.

La prochaine dépendance de scène à partager est le module GPU d'atmosphère,
encore situé dans l'exemple `tidewater_atmosphere_probe`. Son installation et
ses quatre calculs doivent être séparés de la lecture filesystem et des sorties
de qualification avant utilisation dans Studio.
