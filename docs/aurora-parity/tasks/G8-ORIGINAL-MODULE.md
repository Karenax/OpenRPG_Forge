# G8 — Second module original : La Veillée des Éclats

`OriginalModuleAuthoringService` construit le module exclusivement avec `NewModuleHandler`,
`UpsertBlueprintHandler`, `PlaceEntityHandler`, `SetDialogueHandler`, `SetQuestHandler` et
`ProjectCommandHistory`. Le CLI `forge author-original` transmet ensuite l'agrégat à
`ProjectSession.Create` : aucun JSON canonique n'est écrit par le générateur.

Le module comprend le Parvis des Astres, deux créatures originales, un item, un brasero, une porte,
un trigger, un dialogue à embranchement et une quête à trois états. La génération applique 17
commandes et termine à la révision 16, profils de combat inclus. Le test `VeilleeDesEclatsSampleTests` ouvre le workspace,
vérifie le contenu, exporte le `.orpmod`, le recharge et confirme son identité distincte.

Le smoke runtime reconstruit 8 entités, 3 collisions, 2 combattants, le dialogue et la quête.

La prochaine tranche unique est la **revue visuelle G8 nominale/maximisée**.
