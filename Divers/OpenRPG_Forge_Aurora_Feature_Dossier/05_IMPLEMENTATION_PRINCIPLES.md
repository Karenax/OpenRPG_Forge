# Principes d’implémentation obligatoires

1. Une feature possède un ID stable `ORF-*`.
2. Une feature est affectée à Core, un Kit ou Tooling avant code.
3. Le domaine ne référence pas Godot.
4. Les instances ont un UUID stable; les noms peuvent changer.
5. Les références sont typées et validées.
6. Les commandes sont atomiques et testées avec undo lorsque pertinent.
7. Les listes et graphes sont sérialisés dans un ordre stable.
8. Les scripts utilisent des handles opaques et des services validés.
9. Les mutations réseau sont autoritaires côté serveur.
10. Les imports externes passent par une quarantaine.
11. Les builds de distribution échouent sur licence inconnue.
12. Les outils MCP ne fournissent pas de shell ou de Python arbitraire.
13. Les add-ons Godot sont encapsulés derrière une interface Forge.
14. Aucun framework générique n’est créé avant deux cas d’usage réels.
15. Une feature n’est `Tested` qu’avec une preuve automatisée ou une procédure reproductible.
