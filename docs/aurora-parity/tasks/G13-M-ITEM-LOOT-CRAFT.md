# G13-M — Loot contextuel, unicité, artisanat et génération

## Périmètre borné

- `ORF-ITEM-025` — tables de trésor par contexte ;
- `ORF-ITEM-026` — registre d’objets uniques ;
- `ORF-ITEM-027` — propriété de butin instancié ;
- `ORF-ITEM-028` — recettes et artisanat ;
- `ORF-ITEM-029` — génération procédurale d’objet.

Aucune autre exigence G13 n’appartient à ce lot.

## Contrat livré

Les règles de loot filtrent source, biome, faction, niveau, difficulté, tags, condition joueur et
événement. Le registre d’unicité réalise `reserve/commit/release` sous autorité explicite. La
propriété accepte public, groupe, personnel, need/greed ou maître de jeu avec expiration. Les
recettes utilisent des requêtes d’ingrédients, station, compétence, qualité, durée et sous-produits.
Le générateur choisit ses affixes par seed, budget, pools, raretés et exclusions, avec explication
reproductible.

Les transactions runtime échouent sans effet partiel. La fenêtre **Objets avancés** simule le
contexte, l’artisanat et la génération avant application canonique. ForgeScript et MCP exposent le
même profil sans voie de mutation directe.

## Preuves de fermeture

- domaine/application : `ItemAdvancedProfileTests`, `ItemAdvancedWorkflowTests` ;
- persistance : `QuestItemDepthPersistenceTests` et export/validation `.orpmod` ;
- MCP : `ForgeProjectAgentServiceTests` ;
- Toolset : `OPENRPG_FORGE_TOOLSET_G13M_ITEM_LOOT_CRAFT_PASS` ;
- runtime : `OPENRPG_FORGE_RUNTIME_G13M_ITEM_LOOT_CRAFT_PASS`.

## Limites explicites

- l’autorité serveur est consommée, pas réimplémentée ;
- need/greed fournit la décision déterministe, tandis que l’animation de vote appartient au jeu ;
- la génération ne remplace ni la provenance des assets ni la validation de licence.
