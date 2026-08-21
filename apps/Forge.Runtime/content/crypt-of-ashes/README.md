# Crypte des Cendres — intégration du ruleset systémique

La Crypte des Cendres est le premier module jouable d’OpenRPG Forge et la première tranche verticale du ruleset `openrpg-forge.systemic-rpg.v1`.

Le document de référence complet se trouve dans :

`docs/design/OPENRPG_FORGE_RULESET_FR.md`

Le contrat exécutable du module se trouve dans :

`apps/Forge.Runtime/content/crypt-of-ashes/systemic-gameplay.json`

## Intention

Le parcours historique du prototype est conservé :

1. rencontrer Elyra ;
2. obtenir la Clé de braise ;
3. ouvrir le passage scellé ;
4. vaincre la Sentinelle ;
5. affronter le Gardien des Cendres ;
6. atteindre la sortie.

La nouvelle tranche verticale ajoute une seconde lecture de la même zone. Le joueur peut toujours combattre directement, mais il peut aussi lire l’architecture, préparer l’arène et convertir le décor en arme.

## Contrôles ajoutés

| Touche | Action | Usage |
|---|---|---|
| `X` | Coup de pied / poussée | Repousser une cible, renverser une urne ou exploiter une surface glissante |
| `F` | Feu | Enflammer l’huile funéraire |
| `G` | Givre | Geler la rigole d’eau sépulcrale |
| `B` | Force / télékinésie | Projeter une cible, détacher le brasero ou renverser le pilier |
| `Y` | Concentration tactique | Ralentir temporairement le temps pour lire l’arène |

Ces touches évitent les liaisons déjà employées par le déplacement AZERTY, la roue d’action et le mode combat. Les contrôles sont enregistrés à l’exécution par le module et n’altèrent pas les autres modules.

## Interactions jouables

### Urne d’huile funéraire

L’urne se trouve après le passage scellé. Une poussée la renverse et répand son contenu. Le feu transforme alors la flaque en zone de combustion persistante.

Cette interaction démontre :

- objet physique ;
- substance ;
- changement d’état ;
- dégâts de zone ;
- attribution de causalité au joueur.

### Rigole d’eau sépulcrale

La rigole traverse l’approche du Gardien. Le givre la transforme en glace. Les cibles situées sur cette surface subissent une poussée plus importante.

Cette interaction démontre :

- surface ;
- réaction eau + froid ;
- réduction de friction ;
- combinaison élément + force.

### Brasero suspendu

Le brasero domine l’arène nord. Une impulsion de force rompt son ancrage. Sa chute inflige plusieurs impacts aux ennemis proches.

Cette interaction démontre :

- verticalité ;
- masse ;
- gravité ;
- impact de zone ;
- préparation avant engagement.

### Pilier fracturé

Le pilier fissuré peut être renversé. Son effondrement blesse les ennemis présents dans sa trajectoire et crée un obstacle visuel.

Cette interaction démontre :

- faiblesse structurelle ;
- force appliquée ;
- destruction lisible ;
- modification temporaire de l’arène.

### Fosse de pointes

La fosse est placée sur le côté de l’arène. Une cible poussée ou projetée dans son rayon est vaincue par le danger environnemental.

Cette interaction démontre :

- coup de pied ;
- télékinésie ;
- chaîne de causalité ;
- défaite environnementale reconnue par le combat normal.

## Approches du module

### Approche narrative

Elyra reste la source principale de contexte. Le joueur peut accepter ou refuser son aide. Les dialogues et la quête existants restent valides.

### Approche d’exploration

Le joueur cherche la clé et observe les nouveaux dispositifs. Les marqueurs tridimensionnels identifient les verbes possibles sans imposer un contour fluorescent permanent.

### Approche martiale

La sentinelle et le Gardien peuvent toujours être vaincus avec les attaques normales. Le décor n’est jamais obligatoire.

### Approche environnementale

Le joueur peut préparer une chaîne : geler la rigole, attirer une cible, la pousser, puis déclencher le brasero ou le pilier.

### Approche tactique

La concentration tactique ralentit le temps pendant que la touche est maintenue. L’échelle temporelle précédente est restaurée à la sortie du mode et lorsque le nœud est détruit.

## Architecture

`CryptSystemicGameplay.cs` est un directeur de module attaché comme enfant de `GameRoot` dans `Main.tscn`.

Le directeur :

- attend que la Crypte soit complètement initialisée ;
- se désactive pour tous les autres modules ;
- charge et valide le manifeste JSON ;
- enregistre les commandes propres au module ;
- construit les cinq dispositifs ;
- résout les interactions et réactions ;
- transmet les dégâts à l’état de combat existant.

`GameRoot.SystemicGameplay.cs` est une extension partielle, limitée et interne. Elle expose au directeur uniquement les capacités indispensables :

- lecture du joueur et de la caméra ;
- recherche des ennemis vivants ;
- poussée physique ;
- dégâts attribués au joueur ;
- défaite environnementale ;
- présentation des messages dans le HUD.

Le combat environnemental n’utilise donc pas une seconde barre de vie et ne contourne pas la logique de quête.

## Compatibilité

La modification est additive :

- aucun identifiant historique d’entité n’est remplacé ;
- le fichier `project.json` n’est pas réécrit ;
- le parcours et les sauvegardes existants restent valides ;
- le directeur n’est actif que lorsque le module chargé a le slug `crypt-of-ashes` ;
- les autres modules conservent leurs contrôles et leurs scènes.

## Limites explicites de cette première tranche

Cette intégration valide le langage de base du ruleset, mais n’achève pas encore :

- l’IA ennemie capable de déclencher chaque dispositif ;
- la sérialisation durable de tous les objets déplacés ;
- les compagnons narratifs complets de la Crypte ;
- les surfaces électriques, fumées volumétriques et fluides simulés ;
- les enquêtes de témoins et crimes appliquées à cette zone ;
- l’édition visuelle de ces interactions dans le toolset.

Ces éléments restent définis dans le ruleset et devront être convertis en composants génériques. Le module n’emploie pas de faux scripts de victoire : ses interactions passent déjà par l’état de combat existant.

## Vérification

Le test `tests/test_crypt_systemic_gameplay.py` vérifie :

- la validité du manifeste ;
- la présence des cinq interactions ;
- les chaînes huile/feu, eau/givre/force et poussée/pointes ;
- la présence de tous les contrôles ;
- l’attachement du directeur à la scène ;
- l’existence de la passerelle de combat ;
- les liens vers le ruleset.

La validation complète reste :

```powershell
.\scripts\Verify.ps1
```

La validation ciblée du contrat peut être lancée avec :

```powershell
python -m unittest tests.test_crypt_systemic_gameplay
```
