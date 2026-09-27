# Calcul océanique partagé avec la scène

Le module `examples/tidewater_ocean_spectrum_probe/mod.rs` contient désormais
l'implémentation commune. L'exemple numérique est un wrapper. L'entrée
`install` installe les mêmes ressources et passes sur le RenderApp de la scène,
sans créer de device ou d'application supplémentaires. Elle expose les handles
des textures de déplacement/dérivées et les signaux de disponibilité/lecture.
Les samplers utilisent Repeat et interpolation linéaire, y compris les mips.

Le prototype de scène accepte :

```text
tidewater_plant_probe SKY_FIXTURE NEW_PNG --sky-ocean-compute OCEAN_EXPORT
```

Il exécute l'historique de 120 pas, écrit les 21 sorties dans le répertoire
frère `.ocean`, et attend leur acquittement avant la capture. Le callback de
lecture n'arrête pas l'application de scène après réussite ; le wrapper
numérique conserve sa sortie automatique. Les lectures restent consommées
une seule fois, y compris lorsque plusieurs soumissions sont en vol.

## Preuves

Artifacts sous `E:/OpenRPG_Forge/.tidewater-integration/`.

- Scène : `sky-island-002` + `ocean-source-003` →
  `native-ocean-shared-island-001.png` et son répertoire `.ocean`, code 0.
- Les 21 sorties correspondent bit à bit à
  `native-ocean-default-fft-frame120-003`.
- Le PNG est identique à `native-sky-island-003.png`, zéro pixel différent,
  SHA-256 `74a1aad1ea6c5d2f0afbc8b80f5d2a2d795037709618ffdd05ff1a7e5a133d83`.
- La régression numérique tempête `native-ocean-shared-headless-001` termine
  avec code 0 et reproduit les 21 sorties de
  `native-ocean-storm-fft-frame120-003`.
- Rapport : `ocean-shared-regression-001.json`.
- Compilation `ocean-shared-build-final.log` et Clippy avec `-D warnings`
  `ocean-shared-clippy-002.log` réussis pour les deux exemples.

Binaire scène SHA-256 :
`d9df78f6b0334b0229526d8228ef92124383d2ee10c2abe26f4211ae52a871c8`.
Binaire numérique SHA-256 :
`4a4729c708c897971a4caf3639648c8ecc47f9ac0d07f29cc24cbc46b7ab439d`.

## Limites

Ce test établit la coexistence sur le même device et la conservation du calcul.
Il ne dessine pas de surface d'eau, n'exerce pas encore l'échantillonnage des
textures dans WaterSurface/WaterMaterial et ne mesure aucune performance.
Le calcul reste un historique fixe enregistré une fois. Le maillage océanique,
le matériau optique, le rivage et la simulation interactive constituent la
suite. Aucun critère de parité complète n'est clôturé, aucune publication.
