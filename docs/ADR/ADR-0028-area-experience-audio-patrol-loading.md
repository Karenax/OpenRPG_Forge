# ADR-0028 — Expérience de zone : audio, voix, patrouilles et chargement

## Statut

Accepté le 5 août 2026. Complété le 26 août 2026 par le parcours vocal gouverné décrit dans
`docs/DIALOGUE_VOICE.md`.

## Décision

Chaque zone porte un `AreaExperienceProfile` canonique, versionné et indépendant de la scène
moteur. Il regroupe les cues audio, émetteurs spatiaux déterministes, voix localisées, routes de
patrouille inter-zones et présentation de chargement. Le profil complète le contrat technique
`AreaLoadingProfile` : le premier décrit l'expérience du joueur, le second les ressources, budgets,
cache et contraintes de streaming.

Les ressources distribuées déclarent licence SPDX et SHA-256. Une voix sans prise enregistrée ne
peut employer un plug-in TTS que si le profil contient un consentement explicite et traçable.
ForgeScript reçoit un handle et un instantané copié ; ses mutations audio ne modifient que l'état
runtime typé. Toute mutation persistante passe par `SetAreaExperienceProfileHandler` avec dry-run,
diff, inverse et historique.

Une prise vocale de dialogue réelle utilise un blob MP3/WAV content-addressed. L’import, le package
et le runtime vérifient également structure binaire, MIME, taille, provenance et licence. La
génération synthétique reste un processus de production distinct : ses sorties exigent une
divulgation et une preuve de droits propres, sans redistribuer implicitement le générateur.

Une patrouille inter-zones référence une aire source, une aire destination, un portail et un point
de reprise. Le validateur projet contrôle ces références. Le transfert ne progresse qu'après le
chargement de la destination et sous l'autorité déclarée ; Network conserve lease et révision.

## Conséquences

- un seed et un compteur produisent la même position et le même délai d'émission sur toutes les
  plateformes ;
- les événements `Started`, `Completed` et `Interrupted` restent distincts de la ressource moteur ;
- image, vidéo, conseil localisé, progression et fallback accessible suivent le streaming réel ;
- le runtime peut instancier un lecteur audio 3D pour une ressource importée, avec fallback
  diagnostiqué si elle est absente ;
- les projets antérieurs reçoivent un profil vide sûr et une présentation de chargement accessible ;
- les nœuds audio et UI produits par le moteur restent jetables et ne deviennent jamais la source
  de vérité auteur.
