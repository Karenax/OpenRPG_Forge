# Commandes, undo/redo et transactions

## Une seule voie de mutation

```text
UI / CLI / MCP / tests
          ↓
       Command
          ↓
   validation + dry-run
          ↓
     Domain handler
          ↓
  documents canoniques
          ↓
   adaptateur Godot/cache
```

## Enveloppe de commande

```json
{
  "commandType": "area.addEntity",
  "commandVersion": 1,
  "commandId": "uuid",
  "transactionId": "uuid",
  "author": { "kind": "human|agent|server", "id": "..." },
  "expectedRevision": 42,
  "payload": {}
}
```

## Résultat

- validation ;
- diagnostics ;
- semantic diff ;
- documents affectés ;
- nouvelle révision ;
- undo payload ou snapshot ;
- événements produits ;
- preuves de tests facultatives.

## Commandes runtime

Les commandes runtime serveur ne sont pas toutes annulables. Elles doivent être compensables lorsque possible et toujours auditées pour les actions GM/IA sensibles.
