# ADR-0031 — Relations de faction et témoins locaux

Date : 20 octobre 2026. Statut actualisé le 22 octobre 2026 : contrat de factions
et témoins locaux implémenté et qualifié dans le périmètre Gate 5, selon le
[rapport de clôture](../evidence/gate5/GATE5_CLOSURE_20261022.md).
La qualification ne s'étend pas à une simulation sociale globale.

## Besoin

La matrice Gate 5 exige des groupes, relations et conséquences dépendant de
témoins. Le scénario de preuve comporte le joueur et deux PNJ du même groupe :
une agression est visible par le premier, masquée au second ; leurs réactions
divergent et cette différence survit à la sauvegarde. Les branches de dialogue
consultent la relation personnelle du PNJ qui parle.

## Contrat retenu

- Définitions facultatives dans le document gameplay : groupes à identités
  stables, appartenances des acteurs, relations initiales entre groupes et
  règles de réaction dont le déclencheur est déclaré explicitement. Le premier
  déclencheur adopté est l'agression directe avec contact physique confirmé.
- Les variations de relation appartiennent à chaque PNJ observateur envers
  l'acteur responsable. Une variation ne se propage pas aux autres membres de
  son groupe. Le calcul part de la relation initiale déclarée et borne le résultat
  entre -100 et 100 points.
- L'adaptateur moteur fournit une observation de visibilité pour chaque témoin
  candidat, avec raycast vers l'agresseur et la victime. Le domaine contrôle
  références, état vivant, portée de perception et nombre de témoins. Les données
  de perception sont des observations physiques, comme les contacts existants.
- L'action et ses conséquences locales sont validées puis promues dans une
  seule transaction. Un retry retourne le reçu d'origine ; une observation
  ultérieure ne modifie pas rétroactivement les témoins de cette action.
- Les reçus conservent les observations admises et les causes des variations.
  La sauvegarde est vérifiée par reconstruction des relations depuis ces reçus,
  puis comparaison avec l'état courant. Les définitions restent dans le contenu
  d'auteur ; les perceptions et variations restent dans l'état de partie.
- Une condition de dialogue référence l'observateur, le sujet, la comparaison
  et le seuil en points. L'inspecteur et le runtime emploient le même calcul et
  la même unité. Les surfaces et capacités suivent le profil commun du module.

Les quotas du candidat sont 32 groupes, 16 témoins par action et 1 024 relations
personnelles modifiées, avec bornes partagées et tests de refus atomique.
L'adaptateur trie les candidats vivants et affiliés par distance à l'agresseur,
puis identité stable, avant de borner à seize ses doubles tests de visibilité.
La même fonction géométrique contrôle la portée dans le domaine et le moteur.
Les entrées historiques sans faction conservent
leur encodage et leur comportement.

## Preuves à acquérir

Le premier candidat passe dix contrats factions et les 73 tests gameplay
existants le 20 octobre à 01:31 UTC. Le journal
`factions-domain-tests-001.log`, sur F: dans le dossier de preuves Gate 5, garde
ce verdict. Le candidat suivant, avec collecteur physique, formulaires et tests
du host, reste à valider ; suivre le [plan Gate 5](../planning/GATE5_IMPLEMENTATION_PLAN.md).

Définitions et références invalides, relation initiale, témoin visible/masqué,
portée et saturation, retry, refus sans mutation, sauvegarde/reprise et
falsification isolée de relation. Ajouter les formulaires d'auteur avec aperçu,
historique et Save/Open ; prouver le raycast et les deux réponses de dialogue
dans le même Studio/runtime. Ces preuves restent distinctes de la perspective
bonus « mondes vivants », qui conserve son statut non ouvert.
