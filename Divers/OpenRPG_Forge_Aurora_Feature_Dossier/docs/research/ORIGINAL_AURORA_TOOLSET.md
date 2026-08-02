# Recherche — Toolset Aurora original

## Coque

Le manuel officiel présente Aurora comme le même environnement général utilisé pour construire la campagne, organisé autour de l’arborescence du module, d’une vue de zone et de palettes. Le module minimal possède une zone et un point de départ.

## Catégories d’objet

Les familles fondamentales recensées dans les manuels sont : zones, créatures, conversations, journal, objets, encounters, placeables, doors, sounds, triggers, waypoints, merchants et scripts. Le manuel communautaire détaillé complète les propriétés et événements de chaque famille.

## Blueprints et instances

La palette standard contient le contenu livré; la palette custom contient les modèles du module. Les blueprints servent de modèles de placement et certaines modifications peuvent être promues ou propagées.

## Événements

Le modèle d’extension est événementiel. Module, zone, créature, placeable, porte, encounter, trigger, merchant et conversation exposent des slots de scripts. La matrice complète se trouve dans `tracking/AURORA_EVENT_MATRIX.csv`.

## Limites historiques à ne pas recopier

- dépendance forte aux tags/resrefs ;
- modules et ressources largement binaires ;
- UI et propriétés liées au ruleset D&D/NWN ;
- tileset unique par zone ;
- nombreuses conventions magiques par variables ;
- collaboration et tests automatisés faibles ;
- sécurité et provenance non conçues pour des agents IA.
