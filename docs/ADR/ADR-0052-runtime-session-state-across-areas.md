# ADR-0052 — État de session runtime multi-zone

Date : 28 août 2026
Statut : accepté
UUID : `a63363e7-7b81-4264-b2c0-25483f3e8846`
Schéma : v1
Provenance : `author:openrpg-forge`

## Contexte

Le runtime reconstruisait entièrement la scène Godot lors d'un passage de portail. Seuls l'identité
du point d'arrivée et un libellé traversaient ce rechargement ; inventaire, variables, quêtes,
progression et états locaux étaient recréés. Le format de sauvegarde exigeait aussi un composant de
combat joueur et ne pouvait pas représenter les protagonistes pacifiques de Solaren. Enfin, la
reprise choisissait toujours la zone d'entrée avant de lire la sauvegarde, ce qui rendait une partie
enregistrée dans une autre zone impossible à charger normalement.

## Décision

- Le format `.orpsave` passe en `1.8.0`. Le composant `Player` devient optionnel : position,
  inventaire, variables, quêtes et progression restent obligatoirement indépendants du combat.
- `AreaStates` contient des états locaux identifiés par UUID de zone : hostiles, météo, snapshots
  d'entités et exploration de carte. Les doublons, références invalides et sous-états mal formés sont
  rejetés par le validateur canonique.
- Les sauvegardes `1.7.0` et antérieures sont migrées en plaçant leur état local historique dans une
  collection contenant la zone courante. Les champs historiques restent lisibles pendant cette
  migration.
- Le runtime capture la zone quittée avant le rechargement de scène. L'état global traverse le
  portail et l'état local est restauré uniquement lorsque sa zone redevient active ; une zone visitée
  pour la première fois conserve donc son état initial authored.
- La position d'arrivée du portail reste prioritaire sur un ancien snapshot du joueur. Les snapshots
  locaux ne réappliquent pas le transform joueur, déjà porté explicitement par `PlayerPosition`.
- Avant de construire la scène lors d'une reprise, le runtime lit l'UUID de zone de la sauvegarde et
  sélectionne l'unique spawn joueur de cette zone. Le chargement n'est plus limité à la zone d'entrée.
- Les transitions continuent d'utiliser les liens et le cycle de vie canoniques ; la collection de
  session est un transport d'état et non une seconde autorité de navigation.

## Preuves

- Tests : `tests/Forge.ModuleFormat.Tests/Saves/RuntimeSaveStoreTests.cs`
- Suite : `tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj` (458 tests)
- Build : `apps/Forge.Runtime/Forge.Runtime.csproj` (Debug, zéro warning/erreur)
- Godot 4.7.1 : `OPENRPG_FORGE_SOLAREN_SAVE_PASS` avec `combat=optional-none`
- Godot 4.7.1 : `OPENRPG_FORGE_RUNTIME_AREA_STATE_SMOKE_PASS` pour Ember Inn vers Clairval
- Godot 4.7.1 : `OPENRPG_FORGE_RUNTIME_AREA_SAVE_RESUME_PASS` après une relance séparée
- Studio UX7-B : le harness construit lui aussi une sauvegarde `1.8.0` canonique avec `AreaStates` ;
  `OPENRPG_FORGE_STUDIO_UX7_B_PASS` confirme `saves=true`, écriture partielle bloquée, rollback,
  migration d'ancienne version, octets déterministes et round-trip.
- Journaux : `artifacts/verification/consolidation-lot2/`

## Conséquences

Solaren peut sauvegarder et reprendre normalement sans profil de combat. Un portail ne perd plus
l'état global de partie, et les états propres aux zones déjà visitées survivent aux allers-retours et
aux sauvegardes disque. Une sauvegarde créée en dehors de la zone d'entrée reprend dans sa vraie zone.

La collection reste bornée par les validateurs existants de snapshots et d'exploration, mais sa taille
croît avec le nombre de zones visitées. La persistance réseau autoritaire et la fusion de snapshots de
plusieurs joueurs restent des responsabilités du lot multijoueur, pas du format local de session.

## Remplacement

Cette décision ne remplace aucun ADR. Elle étend le contrat de sauvegarde runtime existant en
préservant ses migrations.
