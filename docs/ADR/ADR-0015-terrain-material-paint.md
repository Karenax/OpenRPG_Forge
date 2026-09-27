# ADR-0015 — Peinture persistante des matériaux du terrain

Date : 12 septembre 2026
Statut : **accepté pour implémentation par le propriétaire le 13 septembre 2026 ; qualification non acquise**
Portée : Gate 2, R2-012 et R2-014. Ne vaut pas acceptation globale d'ADR-0014.

Accord explicite « j'autorise » en réponse à la demande conjointe ADR-0014/0015.
L'accord ADR-0014 est consigné séparément ; aucune publication Git autorisée.

## Constat vérifié

TerrainDocument v2 contient les deltas de hauteur et les identifiants des couches,
pas de masque spatial authoré. Le générateur calcule les poids herbe/terre/roche
à partir du relief. Une couleur écrite seulement dans le mesh disparaîtrait lors
du recalcul et ne fournirait pas une correction sauvegardable.
La session actuelle archive le document initial et les reçus réversibles avec
un hash final ; les archives sont bornées à 4 Mio et les commandes à 1 024.

## Décision proposée

1. Étendre le **même TerrainDocument** vers une version 3, avec corrections
   matérielles clairsemées sur sa grille. Aucune seconde carte canonique,
   aucun stockage dans l'ECS, aucune nouvelle dépendance.
2. Chaque correction porte sample_x/sample_z et une liste de poids entiers
   u16 référencés par **ID de couche**, pas par son rang dans le tableau. Poids
   strictement positifs, somme exacte 65 535, IDs uniques et existants. Ordre
   canonique Z/X puis ID. Une case absente utilise les poids procéduraux.
   Une case corrigée conserve son mélange absolu lors du recalcul du relief.
3. Borner le nombre de cases au nombre de sommets de la grille supportée,
   au maximum 66 049 dans le générateur actuel 512 m / pas 2 m. La limite
   d'archive reste applicable : préparation refusée avant toute mutation si
   le résultat ou son historique ne peuvent pas être sauvegardés.
4. Dans le générateur actuel, exposer seulement les trois couches effectivement
   prises en charge. Refuser les autres explicitement ; ne pas présenter la
   palette 32 couches du document comme 32 matériaux rendus.
5. Fournir peinture et effacement local de correction, rayon/intensité et trait.
   Peindre mélange le poids effectif courant avec la couche choisie ; quantifier
   avec distribution déterministe du reliquat pour conserver la somme exacte.
   Effacer est une commande explicite distincte qui restaure la règle procédurale.
6. Utiliser Preview/Apply/Undo/Redo, révision/hash/command_id, inverse, audit et
   Save existants. Un geste ne produit qu'une retouche. Préparer une projection
   couleur visible avant Apply ; ne pas modifier le terrain pendant le dry-run.
7. Reconstruire seulement les attributs de matériaux des chunks touchés et leurs
   frontières communes. Aucun remplacement de collision si les hauteurs et la
   géométrie restent identiques. Les consommateurs de poids (ex. végétation)
   doivent lire le même mélange effectif, pas les anciens poids procéduraux seuls.

## Compatibilité et migration

- Continuer à lire les documents v1/v2 et archives historiques en vérifiant leurs
  hashes **avant** migration. Leur représentation et leur hash restent inchangés.
- V1/v2 refusent les corrections de matériaux ; une ouverture ne migre ni ne
  sauvegarde implicitement. Pas de modification des fixtures de Gate 1.
- Proposer une commande explicite de migration vers v3 avec aperçu, inverse et
  révision. Elle conserve toutes les hauteurs, IDs, seed et couches existantes.
- Versionner l'archive destinée aux nouveaux types de commandes. Conserver la
  lecture/relecture des anciens reçus et leur sens original. Un retour à une
  version antérieure ne peut pas supprimer silencieusement une peinture.
- Conserver l'historique existant ou refuser explicitement une migration qui ne
  peut pas le représenter ; ne pas jeter l'historique pour faire passer un test.

## Conservation des retouches

La peinture appartient aux corrections du document, pas au journal Undo ni au
cache GPU. Recalcul du relief ou nouvelle seed ne l'effacent pas. Un changement
de grille, d'étendue ou la suppression d'une couche référencée crée un conflit
explicite, pas un remappage approximatif. Le verrouillage des objets, les portes,
les biomes et la fusion générale restent le chantier distinct d'ADR-0014.

## Preuves attendues pour la livraison (pas acquises par ce plan)

- Migration/relecture d'archives v1 existantes sans changement préalable de hash,
  Undo de migration et refus de rétrogradation destructrice.
- Peinture et effacement déterministes ; rejet coordonnées/IDs/poids invalides,
  dépassement d'archive sans mutation et retry idempotent.
- Même résultat en génération complète/partielle, frontières sans couture,
  collisions inchangées après peinture seule.
- Peindre, modifier le relief puis régénérer, Undo/Redo, Save, fermer/rouvrir :
  poids authorés conservés. Vérifier hors zone peinte également.
- Parcours Studio sans JSON manuel, aperçu/application réellement visibles,
  geste continu et mesures d'attente auteur. Puis qualification des contrats
  partagés selon ADR-0006, sans réexécuter les anciens audits graphiques Gate 1.

## Approbation demandée

Autoriser cette évolution versionnée et compatible du format terrain/archive
pour implémenter R2-012. Cela n'autorise ni publication Git, ni nouveau moteur,
ni saut de gate, ni validation anticipée de Gate 2 ou d'ADR-0014.
