# G8 DIALOGUE-03 — Modularité, impact, couverture, collaboration et IA

Les sous-dialogues déclarent version, paramètres d'entrée/sortie et contrat de variables, validables
isolément. Les conséquences typées Quest/Faction/Item/World alimentent une analyse d'impact par
dépendances inverses. La couverture énumère chemins, conditions, cycles, nœuds couverts/non couverts
et ratio.

La collaboration repose sur JSON canonique par nœud, IDs stables, révisions, locks expirables,
commentaires, diff sémantique et résolution de conflits. La génération IA exige synopsis, citations
et règles de style ; elle retourne preview, lint et violations avant une application transactionnelle
explicite et non répétable.

Preuves : `DialogueAuthoringServiceTests`, suite RPG Kit et solution globale.

La famille Dialogue sélectionnée pour G8 est complète. La prochaine tranche unique est
**G8 QUEST-01**.
