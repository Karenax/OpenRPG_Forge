# ADR-0016 — Autorité projet/module et sauvegarde cohérente

Date : 14 septembre 2026
Statut : accepté par le propriétaire le 14 septembre 2026 (« j'autorise ») ; implémentation partielle
Portée : Gate 2, R2-001/002/005 ; préparation des références nécessaires à Gate 4

## Constat vérifié

Le host possède une session active Terrain ou Zone, chacune avec son DocumentStore
et un authoring.json. TerrainDocument fournit ProjectId et AreaId, mais il n'existe
pas de document projet/module. Les champs « Clé projet » et « Clé zone » créent des
identités : ils ne constituent pas un assistant projet persistant. DocumentStore
assure verrou, hash attendu, staging et remplacement atomique d'un document borné
à 4 Mio ; il ne réalise pas une transaction entre deux fichiers indépendants.

## Décision acceptée

Une tête de workspace versionnée contient les projets, leurs modules et les
références aux archives de zones. Chaque référence comporte AreaId, hash des octets
de l'archive et hash de tête métier ; son appartenance à un module est unique.
ProjectId/ModuleId/AreaId sont stables et indépendants des noms affichés. Un module
est un regroupement authoré, pas encore un package distribuable .orpmod.

La tête est l'unique autorité mutable pour les projets qu'elle gère. Elle est
stockée dans un répertoire réservé distinct des dossiers historiques de zones,
par le DocumentStore existant. Les archives référencées sont immuables, adressées
par SHA-256 et validées par leurs lecteurs existants. Aucun document Bevy, chemin
libre fourni par l'interface ou cache GPU dans ces données.

Une sauvegarde procède dans cet ordre :

1. Valider les commandes, révisions, appartenance et limites de la proposition.
2. Matérialiser les nouvelles archives immuables, sans écraser un objet existant ;
   vérifier le hash, les octets et leur relecture avant toute publication de tête.
3. Préparer la nouvelle tête référant uniquement des objets présents et valides.
4. Remplacer cette seule tête atomiquement via DocumentStore et hash attendu.
5. Acquitter la révision publiée. Un échec conserve la tête précédente utilisable.

Une interruption avant l'étape 4 peut laisser des objets non référencés, jamais
une nouvelle tête pointant vers une archive absente. Aucun nettoyage automatique
des objets orphelins dans Gate 2. Une corruption ou référence manquante est un
diagnostic bloquant explicite, pas un retour silencieux à une autre zone.

## Reprise des zones existantes

Adoption explicite depuis une archive sauvegardée, session fermée, hash exact
examiné. L'archive source est conservée octet pour octet et copiée vers un objet
immuable ; l'adoption prend effet au remplacement de la tête du workspace.
Le host reconnaît les AreaId gérés et interdit ensuite leur ouverture en écriture
par la voie historique. Les fichiers historiques restent lisibles comme archives,
pas comme seconde autorité éditable. Aucun déplacement, suppression ou migration
implicite à l'ouverture. ProjectId incompatible : refus avec explication ; pas de
réattribution automatique dans cette première implémentation.

La création de projet/module et l'ajout/adoption de zone suivent Examiner/Apply,
audit et Undo/Redo ; sauvegarde explicite. Les sessions de zones réutilisent leurs
commandes et codecs. Leur persistance est fournie par l'adapter workspace, pas
par une copie de l'éditeur. Les onglets partagent ce service et indiquent les
modifications non sauvegardées ; aucun changement d'onglet ne les abandonne.

## Bornes et preuves avant qualification

Les limites de nombre de projets/modules/zones, taille de tête, taille d'archive
et budget de lecture doivent être fixées dans le contrat et les contrôles avant
allocation. Ne pas charger/rejouer toutes les archives à chaque saisie d'un nom.

- Nouvelle création, noms Unicode, doublons, identités stables, mauvais rattachement.
- Round-trip, version inconnue, référence manquante/corrompue, ancien plan et retry.
- Interruption après objet et avant tête : seule l'ancienne tête est active.
- Publication concurrente : refus du hash périmé et aucun fichier utilisateur perdu.
- Adoption : source intacte, refus de double écriture, reprise et historique exacts.
- Changement d'onglet sans perte, sauvegarde/reprise du workspace et playtest du
  snapshot exact, puis parcours Studio humain.
- Contrôles workspace ADR-0006 après raccord, provenance des objets conservée.

## Alternatives et conséquences

Deux fichiers mutables projet/zone sauvegardés successivement ne fournissent pas
l'atomicité nécessaire. Inclure toutes les géométries/journaux dans un gros fichier
projet augmente le coût de chaque sauvegarde et dépasse rapidement 4 Mio. Les
objets immuables permettent de publier une seule petite tête, au prix d'un adapter
de stockage et de règles de rétention explicites.

Cette décision est structurelle : elle ne découle pas d'un simple ajout de champ.
L'autorisation structurelle est acquise. Le premier lot implémente les documents,
commandes et sauvegardes projet/module sans zone dans le répertoire réservé
`workspace-v1`. Le fichier unique contient tête initiale, commandes et tête finale
vérifiée par rejeu : audit et Undo/Redo ne sont pas perdus à la réouverture.
Les objets immuables et l'adoption sont désormais raccordés au service natif avec
garde host contre l'édition historique. L'édition gérée réutilise désormais les
commandes/codecs de zone ; le raccord Studio reste à implémenter. Les étapes et
preuves sont suivies dans R2_WORKSPACE_FOUNDATION.md.
Aucune gate n'est validée et aucune publication Git n'est autorisée par ce texte.
