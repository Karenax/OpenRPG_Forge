# Prompt Codex — revue de parité

Effectue une revue, sans coder, des feature IDs fournis.

Pour chaque feature :

- compare requirement, code, tests et documentation ;
- vérifie Core versus Kit ;
- vérifie indépendance de Godot du domaine ;
- vérifie commandes/undo ;
- vérifie sécurité ForgeScript/MCP/réseau selon le cas ;
- vérifie licences/provenance ;
- classe Complete, Partial, Missing ou Contradictory ;
- cite les chemins et lignes ;
- propose le plus petit correctif.

Ne marque pas Tested sur la seule présence de code.
