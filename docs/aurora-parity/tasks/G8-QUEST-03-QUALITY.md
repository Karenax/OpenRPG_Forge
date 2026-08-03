# G8 QUEST-03 — Tests générés et IA contrôlée

`QuestQualityService` génère cinq scénarios déterministes : nominal, échec, répétition, multijoueur
autoritaire et sauvegarde/reprise. Chaque scénario porte seed, fixtures, étapes et assertions typées ;
le runner rend les échecs localisés et l'état final.

La proposition IA exige synopsis, états, participants, dépendances, citations et règles de style.
Elle produit une quête en preview, lint, avertissements de dépendances, violations de style et diff
sémantique. Une validation humaine nominative est obligatoire avant une application transactionnelle
unique.

Preuves : `QuestQualityServiceTests`, suite RPG Kit et solution globale.

La famille Quest sélectionnée pour G8 est complète. La prochaine tranche unique est
**G8 SCRIPT-01**.
