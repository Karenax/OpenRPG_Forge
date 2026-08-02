# Impact sur l’architecture OpenRPG Forge

## 1. Le Core doit rester général

Les nouvelles fonctions ne doivent pas transformer le Core en ruleset RPG monolithique. Les types Core sont : projet, zone, entité, composant, blueprint, asset, transform, volume, interaction, mouvement, caméra, audio, script, sauvegarde, package et commande.

## 2. Le RPG Kit porte la parité Aurora métier

Créatures avec statistiques, factions, dialogues, quêtes, objets, inventaire, rencontres, marchands et compagnons sont des plugins/kits versionnés. Les interfaces publiques du Core doivent permettre d’autres kits : shooter, survival, vehicle ou space.

## 3. Les documents canoniques sont séparés des scènes Godot

```text
JSON canonique
    ↓ validation/migration
Domain objects C#
    ↓ adaptateur
Godot nodes/resources
```

Une scène Godot peut être régénérée. Un document canonique doit survivre à un changement d’adaptateur.

## 4. Les événements sont des contrats, pas des noms de scripts

Les événements Aurora deviennent des payloads typés et versionnés. Plusieurs handlers peuvent s’abonner selon priorité et autorité. Les événements avant mutation sont rares et contrôlés; les événements après mutation sont privilégiés.

## 5. Le Live Director est un client autorisé

Il ne contourne pas le serveur. Toutes ses actions utilisent des commandes administratives auditées. Cela rend possible un mode local, un serveur LAN et un persistent world avec le même contrat.

## 6. L’IA est un client de commandes

L’IA ne modifie ni JSON ni `.tscn` directement. Elle prépare un plan, appelle des commandes en dry-run, reçoit un diff, puis commite avec permission.
