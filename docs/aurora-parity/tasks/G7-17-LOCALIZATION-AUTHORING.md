# G7-17 — Localisation auteur

Date d’exécution : 3 août 2026.

## Capacité bornée

- `ORF-CONT-011` — localisation du contenu auteur.

## Résultat livré

Le module porte désormais un `LocalizationCatalog` canonique, versionné et indépendant du moteur.
Il possède l’identité stable du module, une langue par défaut, une liste de langues déclarées et des
messages ordonnés par clé. Chaque message conserve texte de repli, traductions et provenance.

L’extracteur rassemble les métadonnées du module, les textes de dialogues et les entrées de journal
de quêtes sans perdre les traductions existantes. Le résolveur applique l’ordre langue exacte,
langue générique, langue par défaut puis texte de repli. La pseudo-localisation préserve les
placeholders entre accolades. Le validateur signale les clés narratives absentes et les incohérences
de langue par défaut.

L’espace **Assistant** ouvre une surface sombre/braise dédiée à la localisation : recherche par clé,
langue active, couverture traduite, provenance, édition, aperçu résolu et pseudo-localisé. La
prévisualisation ne crée aucune révision ; l’application passe par `SetLocalizationCatalogCommand`
et rejoint l’historique annuler/rétablir commun. Le format agrégé passe à `0.6.6` et le workspace à
`1.0.0`, avec lecture des versions précédentes.

## Preuves

- `LocalizationCatalogTests` couvre validation et ordre de repli ;
- `LocalizationWorkflowTests` couvre extraction, conservation, commande dry-run, application,
  annulation/rétablissement et placeholders pseudo-localisés ;
- `LocalizationPersistenceTests` couvre l’aller-retour JSON du catalogue ;
- `OPENRPG_FORGE_TOOLSET_G717_LOCALIZATION_PASS` couvre extraction, édition, aperçu transactionnel
  et restauration de l’historique dans l’application ;
- le portail global compile en Release sans avertissement et exécute 248 tests .NET et 24 tests
  Python.

## Limites conservées

- le format ICU complet (pluriel, genre, sélection et validation fine des paramètres) reste à
  livrer ;
- l’import/export de fichiers pour traducteurs et la fusion avec détection de conflits restent à
  livrer ;
- la mesure de dépassement réel dans les widgets et captures multi-langues reste à ajouter ;
- l’éditeur modifie les langues déjà déclarées par les métadonnées ; la gestion graphique d’une
  nouvelle langue reste dans la configuration du module.

La capacité passe au statut `Implemented` partiel. La prochaine tranche unique est **G7-18 — mise à
l’échelle dans le Toolset** pour `ORF-EE-002`.
