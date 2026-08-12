# G8 SCRIPT-02 — APIs monde du RPG Kit

`R098 / FC-SCRIPT-P1-02` est clos. `ScriptWorldService` expose cinq surfaces autoritaires et
déterministes aux scripts : transferts d'inventaire transactionnels avec capacité, requête, rollback
et audit, transitions de quête scoped avec objectifs et historique, sessions de dialogue liées à un
graphe et à leurs participants, adhésions/réputations de faction avec cause et scope, et requêtes de
perception combinant sens, ligne de vue, portée, stimulus et confiance.

Toutes les mutations portent un identifiant, une autorité, une révision ou une trace sémantique. Les
refus conservent l'état original. Le même contrat passe par l'adaptateur applicatif, les handles
opaques de la sandbox native, MCP, le Toolset et un panneau runtime accessible. Les tests couvrent
nominal, rollback, handles périmés, interruption, scope et répétabilité seedée.

La preuve de clôture est `P1-R098-SCRIPT-WORLD-APIS-ACCEPTANCE.md`. La prochaine tranche canonique
est `R099 / FC-SCRIPT-P1-03`.
