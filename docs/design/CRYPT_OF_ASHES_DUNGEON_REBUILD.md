# Crypte des Cendres — reconstruction du donjon

## Objectif

Transformer la Crypte des Cendres d'une salle-prototype en un petit donjon jouable qui démontre réellement le ruleset systémique d'OpenRPG Forge.

La reconstruction ne change pas la quête principale ni les identifiants critiques. Elle change l'espace dans lequel cette quête se joue.

## Dimensions

- ancienne zone : 20 × 28 m ;
- nouvelle zone : 48 × 76 m ;
- structure : sept espaces principaux + reliquaires latéraux ;
- format de zone : snapshot canonique OpenRPG Forge ;
- art : KayKit Dungeon Remastered CC0-1.0 déjà gouverné dans le dépôt.

## Séquence spatiale

### A. Seuil des Braises

Fonction : introduction, spawn, cadrage du ton.

Le volume est volontairement étroit. Le joueur ne voit pas immédiatement le boss ni la totalité de la Crypte. Les escaliers et torches créent un premier axe.

### B. Sanctuaire d'Elyra

Fonction : narration et préparation.

Le volume s'élargit. Elyra est décalée sur le côté pour que la conversation ne bloque pas l'axe principal. Les colonnes créent une composition de sanctuaire plutôt qu'une zone vide.

### C. Catacombes et reliquaires

Fonction : exploration et lecture du monde.

Le joueur dispose d'un axe central et de voies latérales créées par des nervures de murs. La Clé de braise n'est plus posée presque sur le chemin principal : elle se trouve dans un reliquaire latéral à droite. Le côté opposé fournit une exploration miroir et prépare le joueur à rechercher les bords d'une salle.

### D. Porte scellée

Fonction : verrou narratif et spatial.

Deux murs physiques encadrent la porte. La clé est donc nécessaire : il n'est plus possible de résoudre le verrou en marchant simplement autour du modèle.

### E. Salle de la Sentinelle

Fonction : premier combat contrôlé.

La salle resserrée limite les angles et donne plus de valeur aux poussées. Elle sert de transition entre l'exploration des catacombes et les salles franchement systémiques.

### F. Ossuaire inondé

Fonction : démonstration eau/givre/huile/force.

La salle est plus large. Une lumière froide signale la zone humide tandis que les torches latérales conservent le langage chaud de la Crypte. L'huile est disposée dans le même espace afin que le joueur puisse créer des interactions opposées et choisir sa préparation.

### G. Grande Nef

Fonction : respiration, verticalité et préparation du boss.

La largeur augmente à nouveau. Quatre colonnes servent de repères, de couvert et de préparation visuelle aux structures destructibles. L'arche finale cadre l'entrée du sanctuaire.

### H. Sanctuaire du Gardien

Fonction : combat final systémique.

Le Gardien se situe au centre d'une arène contenant :

- pilier fracturé à gauche ;
- brasero suspendu près de l'entrée ;
- fosse de pointes sur le flanc droit ;
- colonnes non destructibles servant de lecture et de couverture ;
- sortie au-delà du boss.

Le duel direct reste possible. Les dispositifs permettent cependant de préparer une victoire par le monde.

## Règles appliquées

La reconstruction applique les règles suivantes de `OPENRPG_FORGE_RULESET_FR.md` :

1. un lieu doit avoir une fonction, pas seulement une surface ;
2. la progression alterne compression et ouverture ;
3. l'exploration latérale doit produire une conséquence utile ;
4. les objets systémiques sont placés dans un contexte crédible ;
5. le décor ne remplace pas le combat direct ;
6. le verrou narratif doit être cohérent avec la géométrie ;
7. les dangers sont lisibles avant d'être déclenchés ;
8. les dégâts environnementaux passent par la causalité et le combat autoritaire ;
9. la carte ne révèle pas immédiatement toutes ses solutions ;
10. la composition artistique et la collision sont séparées.

## Art CC0

Le lot `apps/Forge.Runtime/art/kaykit-dungeon-remastered/` est utilisé sans nouvelle acquisition externe.

La disposition `art-kits/kaykit-dungeon-remastered.layout.json`, interprétée par `GodotArtKitBuilder`, place les sols, murs, passages, colonnes, escaliers, coffres, tonneaux et torches selon la nouvelle géométrie. Les pièces sont répétées comme kit modulaire plutôt que comme props isolés.

## Limites avant qualification

Cette reconstruction doit encore être ouverte dans Godot 4.7.1 .NET sur l'environnement Windows canonique afin de valider :

- build complet ;
- navigation des agents ;
- collisions et absence de blocage ;
- caméra ;
- lisibilité des prompts systémiques ;
- performances de la densité de modèles ;
- budget de lumières ;
- captures Forward+ 1080p et QHD ;
- sauvegarde/reprise sur le nouveau placement des acteurs.

La PR reste donc en brouillon jusqu'à ces validations.
