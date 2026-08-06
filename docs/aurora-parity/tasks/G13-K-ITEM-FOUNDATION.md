# G13-K — Fondation avancée des objets

## Périmètre borné

- `ORF-ITEM-007` — valeur et coût ;
- `ORF-ITEM-010` — prérequis de niveau, lore, compétences, traits et factions ;
- `ORF-ITEM-011` — identification progressive ;
- `ORF-ITEM-012` — propriété, vol, preuve et juridiction ;
- `ORF-ITEM-017` — effets On Hit déterministes.

Aucune autre exigence G13 n’appartient à ce lot.

## Contrat livré

Un blueprint d’objet peut porter un `ItemAdvancedProfile` optionnel. La fondation décrit monnaie,
marchés, qualité, rareté, usure et demande ; prérequis data-driven ; niveaux de connaissance et
propriétés révélées ; historique de propriété ; puis procs avec trigger, chance, cooldown, condition,
effet et attribution. Les validateurs refusent les valeurs hors bornes, identités dupliquées et
contrats incomplets.

`ItemAdvancedRuntimeResolver` calcule la valeur, les prérequis, la connaissance et les procs à partir
d’un contexte explicite et d’un seed stable. La fenêtre **Objets avancés** offre diagnostics,
simulation et transaction identifiée. ForgeScript expose un handle typé et MCP reste en lecture ou
dry-run.

Les formats projet `0.6.31`, workspace `1.25.0` et `.orpmod` conservent les données ; l’absence du
profil garde exactement le comportement historique.

## Preuves de fermeture

- domaine/application : `ItemAdvancedProfileTests`, `ItemAdvancedWorkflowTests` ;
- persistance : `QuestItemDepthPersistenceTests` et package réel du portail ;
- MCP : `ForgeProjectAgentServiceTests` ;
- Toolset : `OPENRPG_FORGE_TOOLSET_G13K_ITEM_FOUNDATION_PASS` ;
- runtime : `OPENRPG_FORGE_RUNTIME_G13K_ITEM_FOUNDATION_PASS`.

## Limites explicites

- le lot fournit les décisions gameplay et données d’auteur, pas une nouvelle boutique ;
- les conditions scriptées sont des références ForgeScript confinées, jamais du code arbitraire ;
- la preuve de propriété est canonique, mais son habillage visuel dépend du module.
