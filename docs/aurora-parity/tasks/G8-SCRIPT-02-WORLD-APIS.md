# G8 SCRIPT-02 — APIs monde du RPG Kit

`ScriptWorldService` expose cinq surfaces autoritaires et déterministes aux scripts : transferts
d'inventaire transactionnels avec capacité et rollback, transitions de quête scoped avec historique,
sessions de dialogue liées à un graphe et à leurs participants, adhésions/réputations de faction avec
cause et scope, et requêtes de perception combinant sens, ligne de vue, portée, stimulus et confiance.

Toutes les mutations portent un identifiant, une autorité, une révision ou une trace sémantique. Les
refus conservent l'état original. `ScriptWorldServiceTests` couvre nominal, rollback, interruption,
scope et répétabilité seedée.

La prochaine tranche unique est **G8 SCRIPT-03**.
