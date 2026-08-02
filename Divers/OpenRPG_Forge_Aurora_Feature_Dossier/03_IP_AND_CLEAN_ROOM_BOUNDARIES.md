# Frontières juridiques et clean-room

## Principe

OpenRPG Forge peut reproduire des idées fonctionnelles générales — éditeur de zones, graphes de dialogue, scripts événementiels, client de maître de jeu — mais ne doit pas redistribuer ou copier les éléments protégés de Neverwinter Nights.

## Interdictions de projet

- aucun code BioWare, Beamdog ou composant désassemblé incorporé ;
- aucun asset officiel, texture, modèle, animation, son, portrait ou interface NWN ;
- aucun contenu CEP sans licence individuelle compatible et preuve de provenance ;
- aucune copie des textes, campagnes, personnages ou univers ;
- aucun ruleset D&D codé en dur sans licence distincte appropriée ;
- aucun nom public laissant croire à un produit officiel Aurora/NWN ;
- aucune compatibilité de format livrée sans revue clean-room et juridique.

## Ce qui est autorisé dans la conception

- décrire le comportement observé/documenté ;
- établir une matrice de capacités ;
- créer des formats, UUID, API et UI originaux ;
- utiliser des concepts généraux tels que porte, quête, dialogue, trigger, faction ou serveur ;
- créer un ruleset original ou utiliser un ruleset sous licence compatible ;
- importer des assets dont l’auteur a accordé un droit inter-moteur explicite.

## Traçabilité

Chaque feature conserve des références documentaires, mais le code doit découler des contrats Forge. Les noms d’API historiques sont présents seulement dans la matrice de recherche et ne doivent pas être recopiés comme surface publique par défaut.
