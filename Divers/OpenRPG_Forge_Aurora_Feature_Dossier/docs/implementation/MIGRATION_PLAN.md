# Plan de migration depuis le contexte actuel

1. Ajouter ce dossier sous `docs/aurora-parity/`.
2. Ne pas renommer immédiatement les namespaces existants.
3. Auditer le code actuel contre le catalogue.
4. Marquer les features déjà présentes avec preuve.
5. Transformer les lacunes P0 en epics/tâches.
6. Séparer progressivement Core et RPG Kit lorsque les abstractions ont deux cas réels.
7. Ne pas extraire des microservices ou packages prématurément.
8. Conserver le vertical slice comme fixture de non-régression.
