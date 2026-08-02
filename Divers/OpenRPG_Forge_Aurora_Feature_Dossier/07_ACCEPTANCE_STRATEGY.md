# Stratégie d’acceptation

## Statuts autorisés

```text
NotStarted
Researched
Designed
InProgress
Implemented
Tested
Deferred
Rejected
```

## Evidence obligatoire

Chaque ligne du tracker doit pouvoir référencer :

- ADR ou document de conception ;
- PR/commit ;
- test automatisé ;
- capture ou procédure manuelle reproductible ;
- benchmark si performance ;
- notice de licence si asset/dépendance.

## Tests de parité par scénario

### Scénario Builder

Créer une zone, placer blueprint, modifier instance, sauvegarder, recharger, annuler, jouer depuis ici.

### Scénario Writer

Créer un PNJ, dialogue conditionnel, quête, conséquence de faction, localisation et simuler les branches.

### Scénario Scripter

Créer événement, compiler, hot reload, provoquer erreur et boucle infinie, exécuter tests.

### Scénario GM

Se connecter, trouver joueur, créer créature, impersonner PNJ, déclencher encounter et consulter audit.

### Scénario Persistent World

Synchroniser contenu, connecter plusieurs clients, persister état, déployer package compatible et restaurer une sauvegarde.

### Scénario Agent

Lire contexte, proposer plan, dry-run, appliquer une commande bornée, tester, annuler et produire audit.
