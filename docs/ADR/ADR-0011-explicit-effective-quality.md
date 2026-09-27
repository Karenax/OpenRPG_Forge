# ADR-0011 — Profils graphiques effectifs explicitement dégradés

Date : 2026-09-06. Statut : **accepté par le propriétaire**.
Décision : réponse « oui » à la demande d'accepter le compromis graphique sans valider Gate 1.
Branche Rust uniquement. Aucune autorisation de publication ou de passage de gate.

## Décision et limites

Adopter le contrat proposé dans `../planning/QUALITY_FALLBACK_CONTRACT_PROPOSAL.md` :
séparer preset demandé, plan effectif résolu, projection CPU et readiness GPU.
Les profils authorés restent inchangés. Le plan dérivé porte leurs hashes, le niveau demandé,
la signature et la génération de l'adaptateur, les paramètres effectifs et les substitutions.
`full_preset_applied` garde son sens strict ; un profil dégradé ne devient jamais High complet.

Substitutions autorisées et obligatoirement déclarées :

- réfraction écran indisponible → teinte/profondeur du matériau, sans décor réfracté ;
- volumétrie indisponible → brouillard de distance authoré, sans volume éclairé ;
- plafond métrique SSR indisponible → plafond non garanti explicitement représenté comme absent,
  sans confondre ce plafond avec le nombre de pas ou l'épaisseur ;
- aucune suppression implicite des reflets d'environnement : leur capacité doit être établie.

### Extension acceptée le 6 septembre 2026 — intensité des reflets

Le propriétaire répond explicitement « oui » à l'acceptation de cette quatrième limite,
sans considérer Gate 1 comme validée : dans les profils dégradés, l'intensité authorée des
reflets peut rester non appliquée. Elle est conservée dans la source ; la réponse physique
actuelle est utilisée et cette divergence doit être déclarée dans le plan effectif et affichée.
La réflectance fixe ne doit jamais être présentée comme un gain équivalent à l'intensité demandée.

Cet accord étend le contrat, pas les preuves d'exécution : représentation dans le résolveur,
diagnostics et qualification restent à implémenter/vérifier pour cette quatrième limite.
Aucune promotion automatique des capacités IBL/SSR, de la readiness GPU ou de
`full_preset_applied` n'en découle. Gate 1 reste ouverte avec ses validations humaines requises.
L'accord ne vaut ni publication Git ni reprise automatique de l'objectif en pause.

Les capacités inconnues/non qualifiées restent bloquantes ; une substitution ne peut utiliser
une capacité de remplacement elle-même absente/non qualifiée. Les autres paramètres sont conservés,
pas approximés silencieusement. Les exceptions terrain/forêt doivent encore être prises en compte
par l'adaptateur avant qu'il annonce un ensemble complètement installable.

## Application et qualification

### Extension acceptée le 6 septembre 2026 — protection des berges

Le propriétaire répond « ok » à la demande d'accepter l'écart de budget terrain : les
chunks traversés par des berges peuvent conserver leur pleine résolution aux trois LOD
dans le profil effectif, avec cet écart explicitement déclaré. Le cas vérifié comporte
20 chunks sur 64 ; ce nombre décrit la scène de preuve, pas une liste codée en dur.
La règle reste géométrique et réutilisable pour les cartes suivantes.

L'inventaire courant (hash terrain, IDs des chunks protégés) doit rester lié au plan.
Les distances LOD, le reste du budget, les sources authorées et les collisions sont
conservés. Cet accord n'autorise ni une protection arbitraire de tous les chunks ni une
exception pour la forêt prototypes. Le mode strict doit continuer à signaler l'écart ;
un profil effectif qui l'utilise ne devient pas un preset demandé intégralement appliqué.

L'accord lève la décision contractuelle en attente, pas les vérifications techniques :
le raccord du résolveur et de l'adaptateur à cette exception reste à implémenter et tester.
Aucune readiness GPU, performance cible, validation artistique ou Gate 1 n'est acquise,
et aucune publication Git n'est autorisée par cette réponse.

### Exigences inchangées

La résolution pure n'est pas une preuve d'application. L'acquittement futur doit lier séquence,
hash du plan et génération de l'adaptateur aux observations des sept domaines et à la readiness
GPU. Perte de capacité, identité périmée, caméra ambiguë, erreur de pipeline ou timeout interdisent
un succès. L'adaptation reste désactivée jusqu'à intégration et qualification de ce mécanisme.
Les choix manuels sont prioritaires ; le benchmark reste fixe, sans adaptation.

Studio expose les différences et l'état réellement observé. Sources, identités, retouches, seeds,
placements et collisions restent inchangés. Native et Web sont qualifiés séparément.
Cette décision ne valide ni l'esthétique, ni les performances cible, ni Studio/Tauri, ni Gate 1.
Elle n'autorise pas un fork Bevy ou un second renderer.

## Livraison

Commencer par le résolveur moteur-neutre, ses refus, round-trips et hashes déterministes.
Puis raccorder les capacités réelles, les sept projections, Studio et l'acquittement avant
d'activer l'adaptation. Chaque étape distingue prévu, implémenté et réellement vérifié.
