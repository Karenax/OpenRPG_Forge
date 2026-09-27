# Embruns et écume — boucle GPU native sur 120 pas

Source : `1438b1abfcaee3267092b75573014f4d9b4a983c`.
Artefacts : `E:/OpenRPG_Forge/.tidewater-integration/`.

Le mode `--native-history OCEAN_EXPORT --coupled-shore SHORE_EXPORT` de
`tidewater_spray_probe` exécute l'ordre original : FFT, ShoreSim, émission,
déplacement des particules. Le kernel ShoreSim lit puis efface les dépôts du
pas précédent dans le même buffer GPU que celui alimenté par les particules.
Les dépôts du dernier pas restent disponibles pour le suivant. Aucun transfert
CPU des états intermédiaires et aucune remise à zéro externe des dépôts dans
ce mode. Le budget d'émission reste le calendrier source rejoué.

Le module ShoreSim partage désormais sa fonction d'encodage entre son mode
isolé et le mode couplé. Terrain, champ de propagation et paramètres rivage,
terrain et simulation sont comparés avant démarrage. Le manifeste du probe
identifie l'acquisition ShoreSim et l'absence d'état source importé.

## Exécutions et preuves

- `native-spray-coupled-001` : première exécution, acquisition rivage complète.
- `native-spray-coupled-002` : binaire final, entrées réduites uniquement :
  `spray-native-history-inputs-001` (14 fichiers), `ocean-native-inputs-001`
  (13 fichiers), `shore-native-inputs-001` (18 fichiers). Ces répertoires ne
  contiennent aucun résultat de particules, FFT ou état de rivage.
- `spray-coupled-comparison-001.json` : comparaison à `spray-source-010`,
  répétition native et effets relatifs aux simulations sans embruns.
- `spray-coupled-regression-001.json` : les 21 buffers FFT sont inchangés,
  ainsi que les crêtes, dépôts, compteurs et particules actives modulo ordre
  atomique par rapport à l'historique non couplé. La simulation isolée du rivage
  reproduit ses six buffers aux pas 0, 1 et 120.

L'état de rivage des deux exécutions couplées est exactement identique :
`f62a27bb64eea8d215f7e9307acd9e7e62cdbed4b2762fa0a684f47a60dd5246`.

| Effet des embruns au pas 120 | Source | Natif |
|---|---:|---:|
| Texels d'écume modifiés | 5 504 | 5 478 |
| Texels d'humidité modifiés | 0 | 0 |
| Texels de résidu modifiés | 1 | 0 |
| Texels de flux modifiés | 0 | 0 |

L'erreur moyenne du canal écume source/natif est 0,000002440 ; son maximum
local est 0,080811. En soustrayant les baselines respectives sans embruns,
l'erreur moyenne de l'effet vaut 0,000001703, avec le même maximum. Le canal
est une quantité de simulation, pas une couleur d'image. Les écarts hérités du
rivage et des particules ne sont pas masqués par une déclaration de parité.
Les 13 identités manquantes/supplémentaires et le dépôt 353 contre 349 du
[rapport historique](2026-09-24-spray-history.md) restent inchangés.

Clippy strict sur spray, rivage et scène principale et compilation des deux
probes réussis (`spray-coupled-clippy-002.log`, `spray-coupled-build-002.log`).
Un jeu de paramètres modifié avec hash de manifeste cohérent est refusé avant
création des sorties (`spray-coupled-invalid-001.log`).
Binaire spray final :
`81d4a396b39d130316ef9717fda509cd7430985453712c68553f781468e5b255`.

## Limites restantes

Ce lot établit la consommation GPU des retombées et l'évolution de l'écume,
pas le rendu des sprites, la publication de cet état dans la scène complète,
le contrôleur adaptatif natif du budget ou la parité temporelle globale.
Les divergences d'émission dès le pas 8 restent à diagnostiquer. L'édition
Studio et la qualification de la carte complète restent ouvertes.
