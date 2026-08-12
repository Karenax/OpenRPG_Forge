# G8 QUEST-03 — Tests générés et IA contrôlée

`QuestQualityService` génère cinq scénarios déterministes : nominal, échec, répétition, multijoueur
autoritaire et sauvegarde/reprise. Chaque scénario porte seed, fixtures, étapes et assertions typées ;
le runner rend les échecs localisés et l'état final.

La proposition IA exige synopsis, états, participants, dépendances, citations et règles de style.
Elle produit une quête en preview, lint, avertissements de dépendances, violations de style et diff
sémantique. Une validation humaine nominative est obligatoire avant une application transactionnelle
unique.

Avant R073, ces capacités restaient isolées dans le RPG Kit et le workflow assistant : elles
n'étaient pas attachées au document de quête principal ni éditables et persistables depuis le
journal. R073 ferme cette limite avec `Forge.Domain.Quests.QuestQualityProfile`, son validateur,
l'onglet transactionnel, ForgeScript, le MCP et les adaptateurs moteur.

Preuves de clôture : `P1-R073-QUEST-QUALITY-ACCEPTANCE.md`, capture R073, tests Domain, Application,
ModuleFormat et MCP, smokes Toolset/runtime et portail intégré.

Le programme P1 reprend séquentiellement avec **R074 / FC-COMPANION-P1-01**.
