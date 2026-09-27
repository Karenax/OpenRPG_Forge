# forge_project

Matériaux authorés (ADR-0015) : TerrainDocument v3 opt-in conserve des mélanges
absolus u16 par ID de couche ; v1/v2 et le constructeur historique restent
inchangés tant qu'une migration explicite n'est pas appliquée. SetSchemaVersion
et SetMaterialOverrides utilisent le journal réversible, archive v2. Le format
v1 d'archive reste réservé aux anciens documents/commandes. Pas de downgrade
effaçant la peinture ni de nouvelle commande qui dépasse la limite de sauvegarde.
Voir R2_MATERIAL_PAINT.md pour preuves et limites Studio/GPU actuelles.

## Gate 2 terrain authoring

`terrain_edit` provides typed preview/apply/undo/redo and a verified versioned
session journal. Native-only `terrain_edit::file_session` connects it to the
core byte store, with stale-save detection and dirty state. It requires an
application-authorized directory; no frontend filesystem permission is implied.
See `docs/evidence/gate2/R2_FILE_SESSION.md` from the repository root for tests
and the unfinished interruption recovery / Studio integration.

## Optional optical medium

`WaterMediumProfile` separates RGB absorption and scattering coefficients in
inverse metres, bounded to finite `[0, 100]`. Its validated Beer-Lambert
`transmittance` accepts distances in `[0, 100000]` metres. Values are linear
optical coefficients, not sRGB palette colours or measured spectral data.
`WaterDocument.medium` is optional, omitted when absent: legacy JSON and hashes
are preserved. This is an additive v1 reader capability; older strict readers
reject documents that opt in. The engine selects its explicitly unqualified
transmissive candidate when present, projecting both coefficients without sRGB
conversion. No reference map opts in and no Studio authoring control is delivered.
See `docs/evidence/gate1/R1_WATER_MEDIUM_INTEGRATION.md` for the actual proof scope.

## Optional weather sunlight response

`WeatherProfileDocument.sunlight` is an optional renderer-neutral contract with a
`rain_illuminance_multiplier` in `[0, 1]` and a finite `transition_seconds` in `(0, 3600]`.
When absent it is omitted from serialization, preserving existing profile hashes and behavior.
It is an additive v1 reader capability: older strict readers reject profiles that use this field.
No existing example opts in automatically.

`WeatherSunlightProfile::advance_blend` advances a separate normalized clear/rain lighting blend
from an explicit delta. It does not consume material wetness or make sunshine depend on drying.
`illuminance_multiplier` returns the fraction of the immutable authored sun illuminance;
adapters must not multiply the previous projected value repeatedly. Exact endpoints are retained.
These pure methods do not simulate clouds, ambient sky changes, sun direction/color changes or
sheltered rain. The optional engine adapter now consumes them with source-identity and
removal/restoration checks; actual GPU/visual qualification remains separate from its CPU tests.

`effective_quality` résout des plans dérivés sous ADR-0011, sans type renderer ni mutation
des presets. Il conserve les sept domaines, explicite les trois substitutions autorisées
et refuse les capacités inconnues ou les remplacements indisponibles. Le plafond SSR absent
est `null`, pas zéro. Les hashes lient politique, niveau, capacités et génération ; `verify`
recalcule le plan avant consommation d'un JSON importé. Un plan sans blocage n'est ni une
projection CPU, ni une preuve GPU, ni un acquittement adaptatif. Le raccord aux capacités
réelles du moteur et à Studio expose désormais un diagnostic candidat ; installation,
qualification GPU et acquittement restent à réaliser.

Définit les documents JSON persistants et moteur-neutres de Forge :

- `forge.scene-proof` version 1 pour la preuve native/WASM de Gate 0 ;
- `forge.terrain` version 1 pour la source de vérité terrain de Visual Valley ;
- `forge.visual-valley-quality-profile` version 1 pour les presets Low/Medium/High/Ultra, le mode
  benchmark High fixe et la politique d'adaptation bornée.

`TerrainDocument` conserve les identités projet/zone, la révision, les coordonnées en mètres Y-up,
la seed, l'étendue, les paramètres logiques de height sampling et les identifiants des couches
matériau. La désérialisation refuse les champs inconnus. La validation exige des étendues, un pas et
une fréquence finis et strictement positifs, une élévation finie, une amplitude finie positive ou
nulle, 1 à 12 octaves, une lacunarité finie supérieure à 1 et une persistance dans `]0, 1]`. Un
document contient entre 1 et 32 couches ; leurs identifiants uniques contiennent 1 à 64 caractères
ASCII minuscules, chiffres ou `_`, en commençant par une lettre. Les noms non vides sont limités à
128 octets. L'étendue doit être un multiple du pas d'échantillonnage dans la tolérance flottante
documentée par le code, et chaque axe est limité à 8 192 intervalles. Le round-trip et l'empreinte JSON
canonique sont testés.

Ces données ne contiennent aucun type Bevy, handle GPU, mesh, texture ou chemin d'asset. Les caches et
liaisons renderer sont produits par les crates d'exécution.

Le profil qualité global est également moteur-neutre. Son preset High s'aligne sur les budgets déjà
authorés lorsqu'ils existent dans les documents végétation, environnement, eau et météo de Visual
Valley ; les distances LOD terrain et les budgets de shadow map restent des cibles documentaires. Il
impose une progression monotone des coûts entre Low et Ultra. Le benchmark est toujours High avec
adaptation explicitement désactivée ; les seuils d'hystérésis décrivent une politique future et ne
sont encore consommés par aucun runtime ou viewport Studio.

`effective_quality` dérive les plans candidats ADR-0011 sans modifier ces sources.
La capacité `reflection_intensity` distingue le gain authoré de la présence de reflets :
seule son indisponibilité explicite autorise `reflection_intensity_not_applied` sous
ADR-0011. Une capacité manquante/non qualifiée reste bloquante, comme le gain indisponible
en mode strict. Cette limite ne qualifie ni IBL ni SSR, ne vaut pas application GPU et ne
remplace pas la valeur authorée par la réflectance du matériau. Le plan conserve la limite
dans son hash et la vérification par recalcul refuse sa suppression.

Les contraintes terrain dérivées peuvent accompagner les capacités et le plan : empreinte
du terrain, nombre de chunks canoniques et liste strictement triée des identifiants conservés
au stride 1 sur les trois LOD. Elles sont validées et liées au hash. L'extension terrain
explicitement acceptée d'ADR-0011 permet `terrain_banks_full_resolution` uniquement avec
une projection qualifiée ; le mode strict, les capacités inconnues et les inventaires
incohérents restent bloquants. Le plan conserve ses distances et budgets authorés ainsi
que l'inventaire exact de cette exception, sans revendiquer le preset strict complet.
Les champs optionnels absents se lisent comme absence d'inventaire, jamais comme preuve que
le budget est respecté ; un plan importé doit toujours être revérifié avec les capacités actuelles.

`QualitySourceBindings` lie aussi les sources terrain, végétation, environnement, eau et
météo, en complément du hash du profil de budgets commun. Une modification d'une de ces
empreintes change le hash du plan. Le terrain lié doit correspondre à celui de l'inventaire
des berges. Un ancien plan sans ces champs reste lisible pour diagnostic, mais ne peut pas
être utilisé par le garde-fou d'application tant que les liaisons n'ont pas été établies.
