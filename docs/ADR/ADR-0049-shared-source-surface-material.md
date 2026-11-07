# ADR-0049 — Matériau et géométrie de surfaces source communs

Date : 26 octobre 2026. Statut : réalisation Gate 7 en cours.

## Constat

Le matériau, la spécialisation GPU, le décodeur de maillage et les paramètres
matériels sous l'eau résidaient encore dans la crate de carte, bien que Studio et
le runtime les réutilisent. Le relais solaire dépendait d'un système de flare de
cette crate. Cette propriété empêchait de fournir ces mécanismes par le moteur seul.

## Décision

Transférer les implémentations à `forge_engine::source_surface`. Les consommateurs
utilisent directement `SourceSurfaceMaterial`, `mesh::SourceMeshBytes` et
`underwater::CausticSampleParams`. Les anciens propriétaires/types ne sont pas
maintenus en façade. Conserver l'UUID du shader, les IDs/formats des attributs,
bindings, paramètres et règles de spécialisation/alpha/culling.

Publier le soleil dans le set moteur `SurfaceSunPublication`, puis ordonner les
consommateurs après ce set. Le moteur ne dépend pas d'un système d'une carte.
Les documents canoniques ne sérialisent pas ces types Bevy ni leurs handles.

Le raccord actuel des shaders acquis et des ressources reste un adaptateur en
travail. Les transformations encore génériques de ce raccord et les autres
projections d'objets restent à transférer : cette décision ne les déclare pas
spécifiques à Tidewater. Une association de relief ne remplace pas l'acquisition
explicite et fermée des objets et ressources du module.

## Preuves et limites

Le bilan `shared-source-surfaces-validation-001.json` conserve les entrées, tests,
Clippy natif/WASM, contrôles d'architecture et scénario natif reconstruit. Les
copies historiques et échecs intermédiaires sont conservés. Le transfert ne prouve
ni la fidélité complète du Studio, ni tous les profils, ni les performances, ni la
clôture d'un critère général Gate 7. Le défaut d'héritage implicite des objets dans
Studio reste ouvert.
