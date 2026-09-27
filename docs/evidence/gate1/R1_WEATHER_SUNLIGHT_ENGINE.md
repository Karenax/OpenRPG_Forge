# Soleil et météo — raccord moteur, 7 septembre 2026

Suite de `R1_WEATHER_SUNLIGHT_CONTRACT.md`. **Projection CPU implémentée ; rendu GPU
du réglage opt-in non qualifié.** Aucun profil existant n'est activé ou modifié.

## Implémentation

- `WeatherRuntimeState` conserve un blend lumineux distinct de la mouillure, initialisé
  à l'état météo demandé et avancé avec les deltas de simulation. Arrêter la pluie peut
  restaurer la lumière avant que le sol finisse de sécher.
- Après l'Update météo, avant l'observation de l'environnement, l'adaptateur recherche
  un seul soleil Forge et exige les mêmes projet, zone et révision. L'illuminance est
  `illuminance authorée × multiplicateur`, pas la précédente valeur projetée multipliée.
- Aucune entité solaire, géométrie, collision, direction, couleur ou ombre n'est remplacée.
  Les valeurs identiques ne réécrivent pas le composant lumière.
- Une lumière contrôlée porte son illuminance authorée pour la restituer au retrait du
  réglage/de la météo/de son état, ou au refus d'identité/topologie. Sans option, une
  lumière non contrôlée par cet adaptateur reste intacte.
- `environment.sun.weather_response` expose statut, hash météo, blend, multiplicateur,
  illuminance authorée et effective. `applied_cpu` n'est pas un verdict GPU ou artistique.
- Une réponse rejetée ou inconnue rend l'environnement incomplet et empêche son
  acquittement de budget ciel ; les autres compteurs présents ne peuvent masquer ce refus.

Le calcul utilise le contrat moteur-neutre existant ; aucune seconde représentation
canonique ou dépendance n'est ajoutée. Les profils validés restent immuables dans le produit.

## Tests et portée

Quatre tests ECS ciblent le système de projection avec les composants Bevy réels :

1. pluie puis clair : même soleil, absence d'atténuation cumulative, restitution exacte,
   sol encore humide, hashes et documents inchangés ;
2. retrait de météo : restitution ; météo d'une autre zone : refus sans atténuation ;
3. option absente : lumière non possédée inchangée ; deuxième soleil : refus et restitution
   du seul soleil précédemment possédé, lumière supplémentaire inchangée ;
4. retrait/restauration de l'état runtime puis désactivation de l'option : restitution,
   reprise et libération de la propriété de la lumière vérifiées.

Le test existant du profil effectif ciel vérifie en plus que cinq états rejetés/inconnus
interdisent `sky_budget_projected`. Ces tests ne sont pas des lectures GPU et ne prouvent
pas un cycle dans l'interface. La disparition du marqueur soleil, l'édition concurrente
par un autre adaptateur et le remplacement à chaud de documents ne sont pas qualifiés.

Les premières erreurs de compilation (helper de test masqué par une variable, fixture
de statistiques sans le nouveau champ) sont conservées dans `tests.log`. Corrections de
visibilité interne et de fixtures, pas de relaxation de contrat. Le dernier complément
refuse explicitement les acquittements d'environnement après rejet de réponse solaire.

Logs locaux : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-weather-sunlight-engine-20260907`.
Cache et environnement Cargo identiques au lot de contrat. CI distante NOT RUN.

Validation finale après le garde-fou : **553 tests workspace PASS, 1 ignored préexistant**,
Clippy workspace all-targets/all-features strict PASS, check Studio WASM/Web PASS,
fmt/diff check PASS. Avertissement futur `proc-macro-error2 v2.0.1` conservé.

| Artefact | SHA-256 |
|---|---|
| workspace-guard.log | B530343B506A88329A226CBEC4E3226D03C219835D4D73F6E67BC2FEA868CA98 |
| clippy-guard.log | 2AE0BDEBEFE0ADB43F66FFA4EED286A0755B1689C0E057B3FB5289AB01BC6EB5 |
| wasm-guard.log | C329B3B090F0C29AB4A000DA891848111E58964A0C65ABE91D2A7A3DA394D1A4 |

## Prochain raccord de qualification

Chargement externe et trois états natifs statiques vérifiés ensuite dans
`R1_WEATHER_INPUT_NATIVE.md` : effet solaire visible, facteur synthétique 0,25 trop sombre
pour être promu. Le cycle animé et le Web restent dus ; sources embarquées inchangées.

Charger un profil candidat opt-in sans remplacer les sources existantes, vérifier sa liaison
au monde puis exécuter clair/pluie/clair dans le runtime natif et le Web. Comparer les valeurs
effectives et le rendu, pas seulement l'existence du champ ou du composant. L'atténuation
solaire n'est pas une simulation de nuages et ne qualifie pas à elle seule un ciel couvert.

Les valeurs du test (multiplicateur 0,25, transition 4 s) sont synthétiques, pas un choix
artistique promu. Aucun nouveau contrôle d'auteur, capture, build graphique distribué ou
benchmark revendiqué. Sources existantes préservées ; Gate 1 ouverte, aucun push ni asset Cadle.
