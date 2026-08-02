# Core versus Kits

## Forge Core

Le Core ne connaît pas les notions de classe, quête, réputation ou marchand. Il connaît des entités, composants, interactions, événements, données, UI, mouvement, sauvegarde, packages et commandes.

Interfaces candidates :

```csharp
public interface IForgeKit;
public interface IComponentDefinition;
public interface IEntityRuntimeAdapter;
public interface IEditorWorkspaceProvider;
public interface IScriptApiModule;
public interface ICommandHandler<TCommand>;
public interface IValidationRule<TDocument>;
```

## RPG Kit

Le RPG Kit enregistre :

- composants de créature et progression ;
- inventaires, équipements et objets ;
- factions et réputation ;
- dialogues et quêtes ;
- encounters, merchants et companions ;
- modules ForgeScript et UI RPG.

## Network Kit

Le Network Kit ne doit pas modifier les contrats métier. Il ajoute autorité, réplication, identité, persistance, sessions et administration.

## Live Director

Le Live Director est un client de commandes administratives. Il ne possède pas de référence directe à la mémoire du serveur.

## AI

Forge AI est un client MCP. Les capacités de création existent d’abord dans l’interface humaine et la couche de commandes.
