# ADR-0032 — Horloge, capacités et rencontres bornées

Date : 20 octobre 2026. Statut actualisé le 22 octobre 2026 : profil borné du
lot D implémenté, disponible et qualifié localement dans le périmètre Gate 5.
Voir le [rapport de clôture](../evidence/gate5/GATE5_CLOSURE_20261022.md) et le
[rapprochement des preuves](../evidence/gate5/GATE5_LOCAL_QUALIFICATION_20261021.md).

## Besoin et périmètre

Le Creator doit permettre de définir des capacités avec coûts et effets, un
calendrier et des rencontres uniques ou récurrentes. Les refus préservent l'état,
les expirations arrivent une fois et la reprise ne déclenche pas une seconde
fois les mêmes échéances. Ce lot adopte un profil minimal explicite ; le manuel
complet et les comportements autonomes de la perspective bonus restent hors lot.

## Autorités et temps

Un bloc facultatif `SimulationDefinition`, versionné, regroupe l'horloge, les
capacités, effets temporaires, affectations aux acteurs, événements du calendrier
et rencontres. Son état de partie est distinct des définitions authorées.
Le domaine reste indépendant de Bevy, Rapier et des horloges système.

L'adaptateur fournit la durée du pas dans une observation physique, à partir de
`Time<Fixed>`. Le domaine accumule des nanosecondes entières et dérive le jour,
l'heure et la minute depuis une date de départ et une durée de minute de jeu.
Les durées d'effet restent en temps de simulation ; le calendrier peut avoir
une échelle différente, déclarée dans la source et expliquée dans l'inspecteur.
Une pause de simulation suspend cette horloge. Les observations historiques sans
ce bloc conservent leur encodage et leur comportement.

L'observation est monotone et idempotente selon le tick déjà utilisé par la
physique. Le même tick avec des paramètres différents est refusé. La sauvegarde
passe à v5 uniquement pour les documents utilisant ce nouveau bloc.

## Capacités et effets

Une capacité déclare son identité, nom, acteurs autorisés, cible, portée,
condition, coûts d'endurance/items et effets typés. Le premier profil fournit
le soin borné à la vitalité maximale et le bonus temporaire de protection contre
les frappes. Les coûts et tous les effets appartiennent à la même transaction
que le reçu de commande ; un refus ou un quota dépassé n'en conserve aucune part.

Chaque effet actif possède une identité et une cause stables. Les politiques
de cumul sont explicites : refus, rafraîchissement ou nombre de piles borné.
Les incompatibilités déclarées entre effets sont vérifiées dans les deux sens ;
elles font refuser la capacité entière sans consommer ses coûts.
Le calcul de protection consulte les effets actifs sans modifier la définition
de l'acteur. Expiration et annulation retirent une instance une seule fois ;
leurs calculs et les durées utilisent la même horloge injectée.

## Calendrier et rencontres

Les événements planifiés déclarent un premier instant et une périodicité
facultative. Le domaine traite l'intervalle de temps nouvellement franchi, avec
un budget partagé avec les conséquences physiques ; la reprise conserve la
borne déjà franchie. L'énumération des échéances est elle-même bornée.

Une rencontre référence un événement et des acteurs déjà définis, avec condition
et règle de réarmement. Ces acteurs sont absents du jeu avant leur activation.
Leur identité de domaine reste stable ; présence, collision et présentation
sont projetées par l'adaptateur. Un acteur ne participe qu'à une rencontre.
Le premier profil propose une activation unique ou un réarmement après défaite
de tous les participants et délai. Les activations retiennent leur cycle et
leur cause pour vérifier les compteurs et refuser les doublons après reprise.

Bornes initiales proposées : 128 capacités et 128 définitions d'effet, 16
capacités par acteur, 128 instances actives, 64 événements et 64 rencontres,
16 participants par rencontre et 128 reçus d'activation. Leur adoption exige
des bornes partagées, validation des références et tests de refus atomique.

## Exigences de qualification — acquises localement

Coûts insuffisants et effets incompatibles sans consommation ; cumul et
expiration unique ; temps injecté, événement unique/récurrent et overflow ;
participants absents avant activation, réarmement conforme et reprise sans
duplication. Ajouter les parcours normaux du Creator, aperçu, Undo/Redo,
Save/Open et scénario physique. Les tests de domaine, builds, preuves du moteur
et acceptation du parcours gardent des verdicts distincts dans le
[plan Gate 5](../planning/GATE5_IMPLEMENTATION_PLAN.md).
