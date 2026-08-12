# Licences et provenance des assets du MVP

## Périmètre de la Crypte des Cendres

La couche artistique de la Crypte utilise neuf modèles issus de **KayKit Dungeon Remastered 1.0**
par Kay Lousberg, sous licence **CC0-1.0**. Le détail, la source et le hash de l'archive sont conservés
dans `apps/Forge.Runtime/art/crypt-kaykit/CREDITS.md`. La composition est originale OpenRPG Forge.

La couche de gameplay utilise encore :

- des primitives géométriques intégrées au moteur 3D comme volumes physiques invisibles et effets ;
- des matériaux, couleurs, lumières et collisions créés par le code original OpenRPG Forge ;
- `openrpg_forge_splash.png`, visuel original généré spécifiquement pour ce dépôt et détenu par le
  projet OpenRPG Forge ;
- les scripts `ash_warden_phase_two.forge` et `ash_warden_death.forge`, code original du projet.

Ces éléments peuvent être distribués sous la même licence que le code du dépôt dès que celle-ci est
formellement ajoutée. En attendant ce choix, aucun droit de redistribution tiers n'est implicitement
accordé.

## Composants tiers non artistiques

- AngelScript 2.38.0 est intégré au host natif sous licence zlib. Le texte complet et les hashes sont
  conservés dans `native/ForgeScript/THIRD_PARTY_NOTICES.md`.
- Le SDK C# officiel Model Context Protocol 1.4.1 est consommé comme paquet NuGet sous licence
  Apache-2.0 ; il n'est pas incorporé dans les archives créateur `.orpmod`.
- Zig 0.16.0 sert uniquement de toolchain locale au build natif et n'est pas redistribué dans les
  modules.
- Le moteur 3D .NET est une dépendance de développement/runtime séparée ; ses fichiers ne sont pas
  des assets du module de démonstration.

## Règle pour les futurs modules

Tout asset importé devra déclarer source, auteur, licence, URL ou preuve d'achat, modifications et
compatibilité de redistribution avant export public. Un asset sans provenance vérifiable reste un
brouillon local et ne doit pas entrer dans une release.

Depuis G7-13, cette règle est exécutable pour les métadonnées actuellement canoniques. Les profils
Client, DedicatedServer et Shipping refusent avant écriture un module, un blueprint ou une
dépendance requise dont la licence SPDX ou la provenance est inconnue. L’export inscrit les
dépendances approuvées dans le manifest et génère `THIRD-PARTY-NOTICES.txt`, revérifié au chargement.
Les futurs documents d’assets importés devront rejoindre le même validateur dès leur introduction.
