# Instructions agents — dossier Aurora Parity

Ce fichier complète `AGENTS.md`. Il ne remplace pas les décisions architecturales existantes.

## Ordre de lecture

1. `AGENTS.md` du dépôt ;
2. MEGACONTEXT OpenRPG Forge ;
3. ce dossier `README_FIRST.md` ;
4. requirements du domaine concerné ;
5. tracker de couverture.

## Interprétation

- Une feature ORF décrit une capacité cible, pas une obligation de copie.
- Les références Aurora expliquent le besoin historique.
- Les noms publics, formats, règles et assets doivent être originaux.
- Une feature P2/P3 ne doit pas détourner une tâche P0/P1.
- Ne pas créer de classe, interface ou projet vide uniquement pour cocher le catalogue.

## Processus par feature

1. sélectionner un ensemble cohérent de 1 à 5 features ;
2. auditer le code et les tests existants ;
3. produire ou mettre à jour un ADR si l’architecture change ;
4. définir le modèle canonique et la commande ;
5. implémenter le cas nominal ;
6. ajouter validation et erreur structurée ;
7. ajouter undo/transaction si édition ;
8. ajouter ForgeScript/MCP seulement si le contrat le demande ;
9. tester ;
10. mettre à jour `tracking/AURORA_PARITY_COVERAGE.csv` avec preuves.

## Interdictions

- pas de copie de code ou d’assets NWN ;
- pas de dépendance à un format propriétaire comme source canonique ;
- pas de règles D&D dans Core ;
- pas de Godot dans Domain ;
- pas de SQL brut dans les scripts de module par défaut ;
- pas de shell/Python arbitraire dans MCP ;
- pas de mutation directe de JSON par l’IA ;
- pas de fork Godot sans ADR et prototype démontrant le blocage ;
- pas d’implémentation multijoueur avant contrats locaux testés ;
- pas de statut Tested sans evidence.

## Réponse de fin de tâche

- features traitées ;
- fichiers modifiés ;
- décisions ;
- tests exécutés et résultats ;
- tracker mis à jour ;
- limites ;
- prochaine tâche unique.
