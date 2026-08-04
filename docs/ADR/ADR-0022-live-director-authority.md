# ADR-0022 — Live Director séparé et autoritaire

## Statut

Accepté pour G10 Live Director.

## Décision

Le Live Director est un kit .NET séparé, `Forge.LiveDirector`, qui dépend de `Forge.Network` et ne dépend pas de l'adaptateur graphique. Son client possède une session distincte, expirante, révocable et attribuée à un compte autorisé. L'authentification multifacteur reste un adaptateur optionnel; l'autorité et le moindre privilège sont obligatoires.

Toutes les actions live utilisent les permissions et révisions du serveur. La création à chaud est une transaction prévisualisable, bornée et auditée; le catalogue ne montre que les blueprints explicitement autorisés par le serveur. Le chooser est une projection de lecture recherchable et non une seconde source de vérité.

## Conséquences

- aucune mutation directe depuis l'interface GM;
- aucune confiance implicite accordée au client séparé;
- mêmes identités, permissions, révisions et journaux que G9;
- tests déterministes sans moteur graphique;
- l'interface spécialisée sera branchée après stabilisation des contrats du kit.
