# ADR-0029 — Extensions runtime, UI déclarative, JSON sûr et état d’entité

## Statut

Accepté le 5 août 2026.

## Décision

Le module porte un `ModuleRuntimeExtensionProfile` canonique, versionné et indépendant des nœuds
du moteur. Il réunit cinq familles de schémas de règles (`Class`, `Progression`, `Weapon`, `Vfx`,
`Casting`), les documents d’interface déclarative, les documents JSON runtime et la politique de
snapshot d’entité. Chaque ressource possède UUID, version et provenance.

Les documents UI utilisent une arborescence de widgets, des bindings de valeur et des événements
typés. Un événement déclare commande, version d’API ForgeScript, contexte d’entrée et permission.
Le runtime reconstruit les contrôles Godot ; ces contrôles restent jetables. Les mutations auteur
passent exclusivement par `SetRuntimeExtensionsHandler` avec dry-run, diff, inverse et historique.

`ForgeScriptJsonService` ne donne aucun accès au système de fichiers. Il fournit des handles,
révisions optimistes, JSON Pointer, permissions et quotas d’octets/profondeur. La validation v1
garantit la syntaxe et le sous-ensemble de schéma nécessaire (`type` et `required`) ; son extension
future devra rester rétrocompatible et bornée.

La sauvegarde `.orpsave` 1.3 ajoute des snapshots sélectionnés : composants JSON, références UUID,
inventaire et blackboard. Les sauvegardes 1.1/1.2 migrent vers une liste vide sûre. Le module décide
des composants et plafonds autorisés ; le runtime ne sérialise jamais un nœud moteur brut.

## Conséquences

- une extension optionnelle absente est ignorée ; un consommateur qui la déclare requise reçoit un
  diagnostic stable et localisé ;
- Creator, runtime, ForgeScript et MCP lisent le même document canonique ;
- l’UI de démonstration de la Crypte prouve le rendu déclaratif et les bindings JSON sans devenir
  une source de vérité secondaire ;
- le MCP expose lecture, plan, application et réversion transactionnelle ;
- les formats projet `0.6.17`, workspace `1.11.0` et sauvegarde `1.3.0` portent les migrations.
