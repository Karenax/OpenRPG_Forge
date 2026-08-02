# Direction UX — « Aurora 2026 »

« Aurora 2026 » est le nom interne de l’intention fonctionnelle, pas une marque publique. OpenRPG
Forge doit retrouver l’immédiateté d’un grand toolset de modules tout en proposant une identité,
des données, des règles et des interactions originales.

## Promesse créateur

Une personne qui ne connaît pas le moteur sous-jacent doit pouvoir partir d’un dossier vide et :

1. créer un module et ses zones ;
2. définir et placer décors, portes, créatures, objets, déclencheurs et rencontres ;
3. écrire dialogues, quêtes, journal, règles, pouvoirs et scripts ;
4. importer et valider ses assets ;
5. tester immédiatement depuis le point choisi ;
6. corriger les diagnostics sans quitter Forge ;
7. exporter, recharger et partager un `.orpmod` sûr.

L’IA accélère ces opérations mais aucune fonction de création ne doit en dépendre.

## Architecture de l’interface

Les espaces produit restent stables :

- **Monde** — zones, kits modulaires, placement et rencontres ;
- **Blueprints** — définitions réutilisables, propriétés et variantes ;
- **Dialogues** — embranchements, conditions, actions et localisation ;
- **Quêtes** — états, transitions et journal ;
- **Scripts** — ForgeScript, diagnostics, tests et API disponible ;
- **Assets** — import, provenance, licence, collision et LOD ;
- **Validation** — références, performances, sécurité et préparation à l’export ;
- **Playtests** — points de départ, scénarios, logs et artefacts ;
- **Assistant** — plans prévisualisables, diff, application et annulation.

Ces espaces partagent le même projet versionné, le même bus de commandes, la même sélection et le
même vocabulaire. Ils ne deviennent pas une collection d’outils indépendants.

## Principes visuels et d’interaction

- hiérarchie sombre, calme et lisible, avec accent braise pour l’action principale ;
- une action primaire évidente par contexte ;
- informations techniques détaillées dans des panneaux repliables, pas dans le parcours principal ;
- états explicites : prêt, modification non sauvegardée, validation, playtest, erreur ;
- diagnostics reliés à l’objet concerné et formulés avec une correction possible ;
- raccourcis clavier et manipulation directe, sans rendre les boutons incompréhensibles ;
- aperçu et test toujours proches de l’édition ;
- aucune marque du moteur sous-jacent dans les surfaces produit contrôlées par Forge.

## État actuel

Le premier écran Monde permet déjà de créer un module, ouvrir un projet, éditer, sauvegarder, exporter et
reconstruire une zone. L’inspecteur de propriétés locales distingue clairement ce qui appartient à
l’instance de ce qui reste hérité du blueprint, avec types lisibles et action principale unique.
T3.2 ajoute la sélection du point de départ, le snapshot temporaire, le lancement immédiat et le
retour des logs. L’espace Dialogues fournit maintenant liste de nœuds, choix branchés, conditions,
actions, textes localisables et aperçu dans la même identité sombre et braise. Le runtime offre une
sauvegarde/reprise F5/F9 et l'Assistant peut préparer, appliquer et annuler un plan de dialogue par
MCP. Le MVP est fermé ; l’espace Quêtes visuel, Blueprints, Scripts, Assets, Validation et la
manipulation directe du viewport forment désormais le gate **Creator Core G7**, et non une prétendue
parité Aurora déjà atteinte.
