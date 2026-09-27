# Corpus de recette indépendant — Gates 3 à 8

Ce dossier matérialise la préparation du 26 septembre 2026 en données de test
réutilisables. **Ce n'est ni un format de module Forge, ni un moteur de jeu, ni une
preuve de passage de gate.** Aucun fichier n'est chargé par le produit.

- `adventures.json` : deux aventures originales, références, dialogues et états
  attendus des deux approches de l'aventure Gate 3.
- `acceptance.json` : entrées adverses et oracles des recettes préparées.
- `rules_vectors.json` : 25 résultats chiffrés/de classement issus des formules
  proposées par le manuel, réutilisables comme oracles lors de leur adoption.
- `test_corpus.py` : intégrité du corpus, références, couverture et tests négatifs
  du validateur. Aucune simulation fictive n'est présentée comme test produit.

Exécution sans dépendance, réseau, build Rust ou écriture dans un module :

```powershell
python -B tools/gate_preparation/test_corpus.py
```

Les contraintes chiffrées d'un futur package, les versions de profils et les
schémas de sauvegarde restent à adopter. Les chaînes qui ressemblent à des
chemins ou instructions hostiles sont des **données**, jamais exécutées,
extraites ou transmises.

À l'ouverture de chaque gate, brancher ces entrées sur ses vraies commandes et
remplacer les oracles textuels par des assertions d'intégration. Conserver les
tests humains, graphiques, physiques et réseau comme tels ; ne pas les remplacer
par la réussite de ce validateur.

Les vecteurs numériques sont comparés aux fonctions de référence existantes de
`scripts/Test-PlayerHandbookRules.py`, sans nouvelle implémentation parallèle des
formules. Leur réussite ne démontre pas un équilibrage ni leur présence en Rust.
Les identifiants D01–D10 nomment localement les dix domaines Gate 7 du plan ; ils
ne sont pas des IDs de critères officiels ajoutés à la roadmap.

Sources : [préparation](../../docs/planning/preparation-parallele-20260926/README.md),
[Gate 3](../../docs/planning/preparation-parallele-20260926/GATE3_CONTRATS_ET_RECETTE.md),
[Gate 4](../../docs/planning/preparation-parallele-20260926/GATE4_ASSETS_ET_PACKAGE.md),
[Gate 5](../../docs/planning/preparation-parallele-20260926/GATE5_REGLES_ET_CREATOR.md),
[Gates 6–8](../../docs/planning/preparation-parallele-20260926/GATES6_8_CONTRATS_ET_PREUVES.md).
