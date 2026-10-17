# Launcher installé — L’Aube des royaumes

La présentation canonique est conservée dans `canonical-ui`, copiée depuis
`E:/OpenRPG_Forge/launcher-aube`. La coque desktop provient de
`OpenRPG_Forge-Launcher-Prototype/prototypes/forge_launcher` sans modifier ce checkout.
Le frontend embarqué conserve le guide, le bestiaire, les illustrations, la navigation
et le dialogue de sortie. Le seul raccord de distribution utilise les recettes
génériques `forge_delivery` pour le jeu installé et sa copie utilisateur du workspace.
Le menu Jouer conserve Tidewater et présente aussi les modules `.orpmod` v2 admis
dans l’installation, avec un choix explicite de carte. L’inventaire et les refus
viennent de `installed_modules::inventory` ; le lancement transmet uniquement
`ModuleId` et `AreaId` à `installed_modules::start`, qui revalide le plan et ses pins
côté natif. Aucun chemin ou argument exécutable n’est fourni par le frontend.

`Forge.exe` ouvre ce launcher. Le Hub est un gestionnaire de contenu séparé,
`Forge-ContentManager.exe`, et l’assistant NSIS reste un installateur séparé.
`--root DOSSIER` sélectionne l’installation ; sinon la racine de l’exécutable
est utilisée. Les diagnostics `--verify-distributable` et `--verify-webview RECU`
du launcher canonique restent disponibles. Aucun serveur web de développement requis.
