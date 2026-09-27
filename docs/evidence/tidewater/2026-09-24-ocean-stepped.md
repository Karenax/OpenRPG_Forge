# FFT natif conservé entre les soumissions

Le graphe océan propose maintenant une progression d'un pas de simulation par
exécution du système de rendu. L'initialisation et le premier pas sont encodés
ensemble ; les pas suivants réutilisent les buffers, textures et temps accumulé.
Les pipelines compute sont mis en cache dans le système au lieu d'être recréés.

L'API `enable_stepping(app)` s'applique au graphe déjà installé. Le compteur
`Targets.completed_steps` indique le dernier pas encodé aux consommateurs
ordonnés après `OceanComputeSet`. Il ne remplace pas une synchronisation CPU
de fin GPU. Les images sont disponibles à chaque pas ; les readbacks finaux et
`ready` ne sont activés qu'à l'instant cible.

Commande : `tidewater_ocean_spectrum_probe OCEAN_EXPORT NEW_DIRECTORY
120 --stepped`. Les instants pris en charge restent bornés par les fixtures
existantes ; cela prépare le couplage temporel, sans fournir encore une
simulation interactive illimitée ou un contrôleur complet d'embruns.

## Vérifications

`ocean-stepped-regression-001.json` compare les résultats binaires :

- Mer normale, premier pas : 21 fichiers exactement identiques au calcul groupé.
- Mer normale, 120 pas : 21 fichiers exactement identiques.
- Mer agitée, 120 pas : 21 fichiers exactement identiques.
- Scène côtière en mode groupé après modification : FFT et ShoreSim inchangés,
  crêtes inchangées et capture avec ruban strictement identique à la précédente.

La capture de régression a le SHA-256
`18388db2aefd24477cf368151a5dadd9733be6ae27b9a304160b0487570f6a16`.
Clippy strict et compilation des exemples océan/scène réussis :
`ocean-stepped-clippy-003.log`, `ocean-stepped-build-003.log`.
Binaire océan : `82841ee1eea03900fa4bb69cd33bf9ecdc872b098d79dac1a0aad63121ac6313`.
Binaire scène : `507bf4797eb124085eb4d769aa0feb01b1bb35c79653e8deced43b525ee95aa0`.

Les résultats restent sous `E:/OpenRPG_Forge/.tidewater-integration/`.
ShoreSim, émission, intégration et dépôts doivent encore être raccordés à cette
progression, dans le même ordre que la source. Aucun critère TW complet n'est fermé.
