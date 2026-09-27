# Contrôleur natif du budget d'émission

Source figée : `1438b1abfcaee3267092b75573014f4d9b4a983c`,
`src/ocean/Breakers.js::_budget`. Artefacts locaux :
`E:/OpenRPG_Forge/.tidewater-integration/`.

`forge_engine::spray_budget::SprayBudget` porte le calcul CPU original en Rust,
sans dépendance au rendu : budget initial 0,3, historique glissant de 2,5 s avec
au moins deux échantillons conservés, ajustement après une durée strictement
supérieure à une seconde, cible capacité GPU / 2, racine carrée du rapport de
débits, lissage 0,04 et bornes [0,05 ; 1]. La différence de compteurs utilise
`wrapping_sub`, conformément au calcul unsigned JavaScript.

L'appel intervient **après** l'émission. La valeur qui en résulte s'applique à
l'émission suivante. Un retour GPU absent ne change rien ; un échantillon reçu
identique au précédent continue à piloter le lissage sans grossir l'historique.
Les temps invalides ou rétrogrades et les échantillons contradictoires pour un
même instant sont refusés sans modification. Ce contrat suppose que l'appelant
associe chaque compteur à son instant de simulation et réinitialise le
contrôleur en cas de réinitialisation de cette horloge.

## Vérification

Six tests du module réel, compilé directement par `rustc --test` pour éviter
de lier tous les tests Bevy, couvrent : absence de retour et seuil exact d'une
seconde, retours répétés, wrap u32, expiration d'historique, bornes basse/haute
et refus atomique de données invalides. Tous passent
(`spray-budget-tests-002.log`).

`verify_spray_budget SPRAY_EXPORT NEW_REPORT` lit les compteurs source dont
l'intégrité est vérifiée et reconstruit l'horloge native par accumulation de
1/60. Il utilise un retour du pas précédent, comme dans l'acquisition source
qui attend sa soumission GPU. Il compare séparément le budget utilisé pour
l'émission et celui calculé après l'émission.

Les deux acquisitions de 540 pas passent :

- `spray-source-010` → `spray-budget-default-verification-001.json` ;
- `spray-scene-source-001` → `spray-budget-scene-verification-001.json`.

Toutes les valeurs f32 destinées au GPU sont identiques bit pour bit. L'écart
maximal f64 avec les nombres relus du JSON vaut 1,1102230246251565e-16.
Clippy strict et compilation réussis (`spray-budget-clippy-003.log`,
`spray-budget-build-001.log`). Les changements de ce lot n'affectent pas les
captures natives antérieures : leur code de rendu n'a pas été modifié.

## Limite explicite

Le contrôleur est vérifié sur des compteurs source appariés. Il n'est pas encore
branché aux readbacks de la simulation GPU native ; la scène rejoue toujours
le calendrier enregistré. La qualification avec délais de readback variables,
les effets sur la densité et les captures après raccordement restent à faire.
Ce lot ne qualifie donc pas encore un budget natif autonome en fonctionnement.
