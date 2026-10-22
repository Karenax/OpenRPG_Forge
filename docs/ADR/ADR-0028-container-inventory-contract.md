# ADR-0028 — Conteneurs et conservation des inventaires

Date : 19 octobre 2026. Statut actualisé le 22 octobre 2026 : contrat implémenté
et qualifié localement dans le périmètre Gate 5, selon le
[rapport de clôture](../evidence/gate5/GATE5_CLOSURE_20261022.md).

## Besoin

Le lot B doit fournir des conteneurs authorables, des transferts atomiques et une
reprise sans perte ni duplication. Un champ de contenu éditable seul ne suffit
pas : les commandes doivent être jouables dans le Studio/runtime partagé.

## Décision

- Le conteneur est une variante d'`ObjectKind`, avec la position et les faits
  physiques communs. Les items restent ceux du catalogue `GameplayDocument.items`.
- Sa définition contient capacité, contenu initial, filtre des items admis et
  clé facultative. Le filtre vide accepte tout item déclaré. La clé n'est pas
  consommée. Sans clé, le conteneur reste non verrouillable.
- Le profil minimal mesure la capacité en **unités d'items**, comme l'inventaire
  actuel des acteurs. Aucune règle de poids n'est annoncée implicitement.
- Une pile est strictement positive et bornée par `ItemDefinition.max_stack` ;
  une capacité est bornée à 10 000 unités. Les quantités sont additionnées en
  `u64`, sans saturation silencieuse.
- Les transferts acteur/conteneur et les changements de verrou utilisent
  `GameSession::execute`, ses identités de commande, sa séquence, son observation
  de ligne de vue, la portée du profil et ses quotas. Un objet détruit, consommé
  ou en mouvement refuse le transfert. La simulation physique n'est pas doublée.
- Source, destination, filtre, pile et capacité sont vérifiés avant mutation.
  L'opération complète et ses événements restent dans le candidat atomique
  existant ; tout refus conserve les deux inventaires et les journaux.
- Le gameplay étendu exige le schéma Creator v2 et la capacité `Containers`
  réellement exécutée. Les documents v1 et leurs octets sans nouveaux champs
  restent inchangés. La capacité n'est pas déclarée disponible sur les seules
  structures préparatoires.

## Sauvegarde et preuves

Les états des conteneurs sont attachés aux objets canoniques, pas à un second
état du jeu. Les sauvegardes contenant ces états portent une version explicite
étendue ; les sauvegardes sans conteneur conservent leur format historique.
Le hash fonctionnel inclut définition, contenu initial, filtres et clé.

La reprise doit vérifier les références, bornes et formes d'état, puis la
conservation des items par un registre dérivé du journal complet conservé :
contenus initiaux, ramassages uniques, consommation/récompense de quête et
transferts. Les événements d'items doivent correspondre à leurs commandes ;
modifier les inventaires et recalculer le hash d'état ne suffit pas à fabriquer
une sauvegarde valide. Ce registre vérifie les quantités ; il ne remplace pas
les observations physiques produites par l'adaptateur.

Oracles : transfert dans les deux sens ; source absente ; quantité nulle ; pile
ou inventaire plein ; filtre ; verrou/clé ; portée/occlusion ; quota ; répétition
idempotente ; réouverture ; falsification d'inventaire et de journal ; auteur
preview/apply/Undo/Redo ; parcours réel de restitution dans le second module.

Cette décision fixe la réalisation ; aucune clôture de lot ou de gate n'est
déduite de sa présence. Les critères de P07/P08 restent applicables.
