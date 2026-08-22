# Crypte des Cendres — donjon systémique de référence

La Crypte des Cendres est la première tranche verticale jouable du ruleset `openrpg-forge.systemic-rpg.v1`.

Le ruleset complet est :

`docs/design/OPENRPG_FORGE_RULESET_FR.md`

Le contrat exécutable des interactions est :

`apps/Forge.Runtime/content/crypt-of-ashes/systemic-gameplay.json`

La géométrie canonique est portée par le snapshot de zone référencé par `project.json`, et la couche artistique CC0 est construite par `Runtime/CryptArtKitDecorator.cs` à partir de KayKit Dungeon Remastered.

## Reconstruction 2026

L'ancienne Crypte était essentiellement une salle de 20 × 28 mètres. La zone canonique mesure maintenant **48 × 76 mètres** et organise le parcours en espaces distincts.

### 1. Seuil des Braises

Le joueur apparaît dans une entrée étroite et immédiatement lisible. Des escaliers et deux lignes de torches conduisent vers le sanctuaire sans exposer toute la carte d'un seul regard.

### 2. Sanctuaire d'Elyra

Elyra se trouve dans une salle plus large servant de respiration narrative. Deux colonnes structurent la composition et la sortie centrale mène vers les catacombes.

### 3. Catacombes et reliquaires

La carte s'élargit en un espace à circulation multiple. Des murs intérieurs et des nervures de pierre créent un axe central et des voies latérales. La **Clé de braise** est placée dans un reliquaire sur le côté droit : l'exploration devient donc utile avant la progression principale.

### 4. Porte scellée et salle de la Sentinelle

La porte est désormais une vraie frontière : deux murs physiques occupent le reste de la largeur et empêchent de simplement contourner la serrure. La Sentinelle attend dans une salle plus resserrée après cette transition.

### 5. Ossuaire inondé

La salle suivante est plus large et froide. Elle concentre les systèmes eau/givre, huile/feu et poussée. Une lumière froide permet d'identifier immédiatement la zone humide tandis que les torches restent chaudes sur les côtés.

### 6. Grande Nef

La Nef ouvre de nouveau les lignes de déplacement. Quatre colonnes donnent du couvert et des repères. Cette salle prépare visuellement la verticalité et les interactions structurelles du combat final.

### 7. Sanctuaire du Gardien

Le Gardien des Cendres occupe le dernier sanctuaire. Le pilier fracturé, le brasero suspendu et la fosse de pointes sont disposés autour de lui de manière à proposer plusieurs plans de combat sans supprimer le duel direct. La sortie se trouve au-delà du boss.

## Parcours principal conservé

Les identifiants historiques des éléments importants sont conservés. Le parcours narratif reste :

**Elyra → Clé de braise → porte scellée → Sentinelle → Gardien des Cendres → sortie.**

Les positions ont cependant été redistribuées dans le nouveau donjon afin que chaque étape possède son propre espace.

## Couche artistique CC0

La Crypte réutilise le pack **KayKit Dungeon Remastered 1.0** de Kay Lousberg, sous licence **CC0-1.0**.

Les modèles gouvernés présents dans le dépôt comprennent notamment :

- dalles de sol ;
- murs et murs avec passage ;
- angles ;
- colonnes ;
- escaliers ;
- coffres ;
- tonneaux décorés ;
- torches allumées.

`CryptArtKitDecorator` ne construit plus une boîte rectangulaire. Il compose les sept espaces avec des dalles, silhouettes de salles, arches, colonnes, accessoires et éclairages. Les volumes de gameplay restent séparés de cette peau artistique.

## Contrôles systémiques

| Touche | Action | Usage |
|---|---|---|
| `X` | Coup de pied / poussée | Repousser une cible ou renverser l'urne d'huile |
| `F` | Feu | Enflammer l'huile funéraire |
| `G` | Givre | Geler la rigole de l'ossuaire |
| `B` | Force / télékinésie | Projeter une cible, détacher le brasero ou renverser le pilier |
| `Y` | Concentration tactique | Ralentir temporairement le temps |

## Interactions principales

### Huile + feu

Une réserve d'huile se trouve dans l'ossuaire inondé. Une poussée répand l'huile ; le feu la transforme ensuite en zone de dégâts persistante.

### Eau + givre + force

La rigole centrale de l'ossuaire peut être gelée. Les ennemis présents sur la surface glissante sont davantage déplacés par une poussée ou la télékinésie.

### Brasero suspendu

Le brasero domine l'entrée du sanctuaire final. Une impulsion de force rompt son ancrage et produit un impact de zone.

### Pilier fracturé

Un pilier du sanctuaire final peut être renversé. Il blesse les cibles dans son rayon et modifie la lecture de l'arène.

### Fosse de pointes

La fosse se trouve sur le flanc droit du Gardien. Une cible projetée dans sa zone est vaincue par le même chemin de combat autoritaire que les attaques normales.

## Principes appliqués

La nouvelle carte applique directement plusieurs règles du ruleset :

- le lieu possède une fonction et une progression spatiale ;
- le décor sert au gameplay ;
- la voie directe reste possible ;
- l'exploration latérale apporte une ressource réelle ;
- les dangers sont placés là où ils sont crédibles ;
- les dégâts environnementaux réutilisent le combat existant ;
- la porte de progression ne peut pas être contournée par une simple erreur de géométrie ;
- les salles changent de largeur et de rythme afin d'éviter l'effet « grande boîte ».

## Architecture

`CryptSystemicGameplay.cs` reste un directeur limité au module `crypt-of-ashes`. Il valide le manifeste, enregistre les commandes, crée les dispositifs systémiques et transmet les dégâts au combat existant.

`GameRoot.SystemicGameplay.cs` expose uniquement les opérations nécessaires au directeur : joueur, ennemis, poussée, dégâts, défaite environnementale et messages HUD.

`CryptArtKitDecorator.cs` est la couche artistique runtime de cette tranche verticale. Le snapshot de zone conserve la structure de gameplay et les positions canoniques.

## Limites explicites

Cette reconstruction est substantielle mais n'est pas encore une qualification artistique finale. Restent notamment à valider sur l'environnement Windows/Godot canonique :

- navigation réelle de tous les agents dans les nouvelles salles ;
- collisions de chaque passage en playtest ;
- composition caméra dans les espaces étroits et larges ;
- budget des lumières et performances Forward+ ;
- rendu visuel 1080p/QHD ;
- sérialisation persistante des objets systémiques déplacés ;
- IA ennemie capable d'utiliser elle-même tous les dispositifs ;
- remplacement futur des colliders de sécurité runtime par des volumes entièrement éditables dans le Creator.

La généralisation masse/stabilité/surfaces/supports/causalité dans Domain/Application reste également un chantier séparé : la Crypte sert de vertical slice, pas de moteur parallèle.

## Vérification

Contrat systémique :

```powershell
python -m unittest tests.test_crypt_systemic_gameplay
```

Reconstruction du donjon :

```powershell
python -m unittest tests.test_crypt_dungeon_rebuild
```

Portail complet attendu sur Windows :

```powershell
.\scripts\Verify.ps1
```
