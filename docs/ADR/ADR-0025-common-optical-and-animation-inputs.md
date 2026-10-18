# ADR-0025 — Entrées communes pour animation, locomotion et effets optiques

Date : 18 octobre 2026
Statut : appliquée et validée localement sur les périmètres indiqués ; généricité globale ouverte
Déclencheur : demande propriétaire de traiter les deux réserves d’architecture du 17 octobre.

## Décision

Les capacités réutilisables possèdent leurs modules et contrats communs. Les
acquisitions figées, identités et politiques d’import appartiennent aux adaptateurs
de contenu. Aucune nouvelle scène canonique ni nouvelle pile de rendu n’est créée.

- `scene_wind` reçoit des horloges explicites et une borne de pas de temps choisie
  par la scène. L’acquisition de référence et sa vérification de provenance restent
  dans `forge_assets::tidewater_environment`, sans inclusion de ce JSON par le moteur.
- `pendulum_motion` et `amphibious_locomotion` portent les calculs communs.
  Un profil de locomotion est validé avant installation ; son type validé est
  conservé par le contrôleur lors de l’initialisation de la session. Il s’agit
  d’entrées dérivées du renderer, sans nouveau format persistant ; la gravité
  y est une magnitude descendante positive, explicitement nommée.
- `film_grain`, `ambient_occlusion` et `lens_flare` possèdent leurs shaders et API.
  Les UUID de shaders et les noyaux WGSL sont conservés. Aucun alias des anciennes
  API moteur n’est ajouté.
- Le flare lit un `SceneSun` explicite ou les lumières directionnelles Bevy.
  L’adaptateur de matériaux importés possède un relay opt-in, installé par Studio
  et le probe natif existant. Le flare fonctionne sans bibliothèque de village.
- `water_medium` porte l’observation du milieu et sa frontière d’ordonnancement.
  Le haze en espace d’affichage expose un set commun ; un effet optique ne dépend
  plus de la fonction de rendu privée du backend de référence pour son ordre.

Les effets qui lisent la profondeur déclarent un besoin commun de texture
échantillonnable. Le flag est ajouté à la caméra extraite avant l’allocation Bevy,
avec conservation de ses autres usages. Le renderer ne dépend plus d’un réglage
implicite fourni seulement par la scène de référence.

Le flare déclare aussi son exécution avant l'upscaling et l'UI. Un ordre après
`PostProcess` et avant un set de haze vide ne suffit pas : la copie finale peut
précéder le flare. La présence du haze importé ne doit pas assurer ce contrat
à la place de l'effet commun.

## Compatibilité et preuves

Les schémas persistants, identités de contenu et acquisitions historiques ne sont
pas renommés. Les profils par défaut conservent les constantes et l’ordre des
opérations antérieurs. Les nouveaux tests contrôlent des profils indépendants,
le refus d’entrées invalides, la conservation du pas d’animation et l’absence de
bibliothèque importée lors de l’installation des effets communs.

Le probe optique procédural compare séparément chaque effet avec une capture sans
effet, sur les mêmes scènes natives et WebGPU. Ses résultats ne sont disponibles
qu’après exécution et readback GPU ; une compilation seule ne constitue pas cette
preuve. Ce probe ne qualifie pas l’eau, le ciel, les imports de packages ou la
parité complète de Tidewater.

## Limite

Cette décision fixe la frontière des extractions livrées. Elle ne clôture pas la
généricité globale : les autres capacités et adaptateurs de contenu encore couplés
restent recensés dans l’audit d’architecture. Aucun changement de gate, de release,
de distribution ou de publication Git n’est implicite.
