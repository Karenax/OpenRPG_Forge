# G13-J — Opérations avancées de quête

## Périmètre borné

- `ORF-QST-009` — priorité, importance et épinglage du journal ;
- `ORF-QST-016` — répétition, cooldown, limite, reset et décroissance des récompenses ;
- `ORF-QST-017` — partage individuel, groupe, opt-in, leader ou unanime ;
- `ORF-QST-018` — transitions exclusives, confirmation et conséquences ;
- `ORF-QST-021` — notifications localisées et accessibles.

Aucune autre exigence G13 n’appartient à ce lot.

## Contrat livré

`QuestDefinition` porte un `QuestOperationsProfile` optionnel. Le validateur protège les bornes,
références de transitions et d’états, identités, groupes mutex, textes localisés et provenance.
`QuestOperationsRuntimeResolver` calcule l’ordre du journal, la prochaine répétition, la progression
partagée, les choix exclusifs et les notifications sans dépendre du moteur. L’autorité réseau est une
entrée explicite ; le lot ne crée aucun protocole parallèle.

La fenêtre **Quêtes avancées** expose catalogue, trois onglets, diagnostics, simulation, dry-run,
application et undo/redo. Elle appelle `SetQuestOperationsHandler`, le handle ForgeScript est typé et
MCP ne propose qu’une requête filtrée et une prévisualisation transactionnelle.

Les formats projet `0.6.31`, workspace `1.25.0` et `.orpmod` conservent le profil. Les versions
précédentes le relisent comme absent. La Crypte des Cendres contient un exemple complet exécuté par
le runtime.

## Preuves de fermeture

- domaine et application : `QuestOperationsTests`, `QuestOperationsWorkflowTests` ;
- persistance : `QuestItemDepthPersistenceTests` et export/validation `.orpmod` du portail ;
- MCP : `ForgeProjectAgentServiceTests` ;
- Toolset : `OPENRPG_FORGE_TOOLSET_G13J_QUEST_OPERATIONS_PASS` ;
- runtime : `OPENRPG_FORGE_RUNTIME_G13J_QUEST_OPERATIONS_PASS` ;
- fermeture : `OPENRPG_FORGE_VERIFICATION=PASS` et graphe déterministe frais.

## Limites explicites

- le rendu plein écran des notifications reste à la couche de présentation du jeu ;
- le resolver consomme l’autorité G9 existante et n’implémente pas un transport réseau ;
- la décroissance produit une valeur déterministe, mais l’attribution effective reste au système de
  récompenses du module.
