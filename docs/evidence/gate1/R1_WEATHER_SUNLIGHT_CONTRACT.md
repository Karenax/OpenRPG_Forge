# Réponse solaire météo — contrat, 7 septembre 2026

**Implémenté dans le domaine ; pas encore raccordé au renderer. Gate 1 ouverte.**
État de cette première étape : le raccord CPU ajouté ensuite est décrit dans
`R1_WEATHER_SUNLIGHT_ENGINE.md` ; aucune preuve GPU n'est déduite du contrat.
Suite du diagnostic de `R1_024_026_WEATHER_NATIVE.md`, pas une nouvelle qualification visuelle.

## Contrat

`WeatherProfileDocument` accepte le bloc optionnel `sunlight` :

```json
{
  "sunlight": {
    "rain_illuminance_multiplier": 0.25,
    "transition_seconds": 4.0
  }
}
```

Cet extrait illustre le contrat, pas un profil complet ni un réglage adopté pour la vallée.
Le multiplicateur est fini dans [0,1] ; la durée est finie dans ]0,3600] secondes.
Le bloc est strict, validé avec son document parent et intégré au hash s'il est présent.
Absent, il n'est pas sérialisé : les anciens profils et leurs hashes sont conservés.
Les anciens lecteurs stricts refuseront un profil qui emploie ce nouveau bloc v1.
Aucun fichier de scène ou profil météo existant n'est modifié pour activer la fonction.

`WeatherSunlightProfile::advance_blend` avance un scalaire d'éclairage distinct de la
mouillure, à delta explicite, vers clair=0 ou pluie=1. Une interruption repart de la valeur
courante ; les dépassements sont bornés. `illuminance_multiplier` retourne une fraction
de l'illuminance authorée avec extrémités exactes. L'adaptateur devra toujours repartir de
cette source immuable, jamais remultiplier sa valeur projetée précédente.

Pas de dépendance renderer, horloge globale, allocation par frame, simulation de nuages,
couleur/direction du soleil, éclairage ambiant ou fonction d'édition Studio ajoutée.
Le parent conserve sa validation d'identité de projet/zone/révision.

## Preuves

Cinq tests nouveaux couvrent :

- omission du bloc et hash du profil legacy lié à la révision 3 de la preuve native ;
- round-trip, changement puis restauration de hash, refus d'un champ inconnu ;
- clair/pluie/clair exact et retour de lumière alors que le sol est encore humide ;
- interruption, delta nul et grands deltas bornés ;
- refus des paramètres/entrées invalides et acceptation des bornes autorisées.

Le premier test comparait le fichier météo révision 1 au hash runtime révision 3 : échec
conservé dans `tests-targeted.log`. Le chargeur WorldSketch lie explicitement la météo à
sa révision ; test corrigé pour reproduire cette liaison, sans changer le hash attendu
`322ee487ea89a4709bbf101d78cffecc05084d7cfc1e8778c608c2bc7d2a2115`.
Clippy a ensuite refusé les comparaisons flottantes strictes : comparaisons de bits dans
les assertions d'exactitude et borne validée dans le calcul. Pas de tolérance ajoutée.

`cargo test --workspace` : **549 PASS, 1 ignored préexistant**. Clippy workspace tous
targets/features avec `-D warnings` PASS. Les cinq tests ciblés passent aussi.
Check Studio WASM/Web PASS, fmt et diff check PASS. Warning futur `proc-macro-error2`
préexistant conservé ; aucun build graphique ou lancement GPU revendiqué.
Logs : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-weather-sunlight-contract-20260907`.

| Artefact | SHA-256 |
|---|---|
| tests-workspace.log | CBA6359AFFF5F1398160B29053D6929719D1752CB2BF9D759EDC9B0246001A83 |
| clippy-pass.log | 27EA832703C7CD5440C9E8B3E7FB562DC6085B6725BB0E81769244A2EE010827 |

## Suite requise

Raccorder un état de transition séparé au runtime météo, puis une projection du soleil
liée au document d'environnement de même identité. Vérifier l'absence/retrait de météo,
les erreurs d'identité, les changements de cible et la restauration exacte, en conservant
l'entité solaire. Exposer illuminance source et effective, puis effectuer un cycle réel
natif/Web avec capture avant de revendiquer un effet visible. Ni les valeurs illustratives
ni ces tests purs ne constituent une acceptation artistique ou une preuve GPU.

Branche Rust, worktree préservé ; aucun push, asset Cadle, passage de gate ou nouveau
benchmark. CI distante NOT RUN. Ce lot ne change pas encore la lumière de la vallée.
