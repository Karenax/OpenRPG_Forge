# Sécurité du Live Director

## Menaces

- compte GM compromis ;
- action destructrice involontaire ;
- possession d’un personnage joueur ;
- mutation de quêtes/économie non auditée ;
- exécution de script arbitraire ;
- fuite d’informations cachées ;
- abus de téléportation ou spawn.

## Mesures

- RBAC/ABAC ;
- authentification forte facultative ;
- permissions par commande et zone ;
- confirmations proportionnées ;
- dry-run pour batch ;
- raisons obligatoires pour économie/progression ;
- audit immuable ;
- pas de shell ni script non signé ;
- mode spectateur distinct de possession ;
- commandes compensatoires ;
- alertes et export d’audit.
