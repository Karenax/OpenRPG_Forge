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

`Forge.exe` ouvre ce launcher dans la livraison historique qui comprenait
aussi `Forge-ContentManager.exe` et son assistant NSIS.
`--root DOSSIER` sélectionne l’installation ; sinon la racine de l’exécutable
est utilisée. Les diagnostics `--verify-distributable` et `--verify-webview RECU`
du launcher canonique restent disponibles. Aucun serveur web de développement requis.


## Transition Gate 7 vers un launcher par jeu (2 novembre 2026)

Le launcher décrit ci-dessus est l'existant Aube. La cible est un launcher propre à
chaque jeu exporté par le Studio, configuré depuis son projet et limité à ses contenus
admis ; Tidewater et les contenus Aube restent des références. Cette migration n'est
pas encore réalisée. Les préférences Rust sont conservées pour la session seulement ;
le frontend Aube possède séparément ses préférences WebView et ne les applique pas au
moteur. La matrice des commandes, dépendances et preuves à rejouer figure dans la
campagne Gate 7 `editor-launcher-d1-inventory-001.md` et l'addendum ADR-0021.

Le shell commun propose désormais un profil de jeu autonome :
`FORGE_LAUNCHER_PROFILE=standalone` et `FORGE_LAUNCHER_DIST` produisent le frontend
sans contenus Aube dans un nouveau dossier temporaire. `FORGE_LAUNCHER_DIST` doit
être un chemin absolu, inexistant et directement sous `.build/tmp` du workspace ;
une autre racine temporaire absolue, existante et hors checkout peut être fournie
par `FORGE_LAUNCHER_TEMP_ROOT`. Les liens et jonctions dans cette racine sont
refusés. Le dossier `apps/forge_launcher/dist` reste réservé au profil Aube.
Le build Cargo de production sélectionne `--features standalone` et passe dans
`TAURI_CONFIG` un chemin vers ce frontend relatif à `apps/forge_launcher` sous
`build.frontendDist`, par exemple `../../../.build/tmp/mon-frontend`. Sur Windows,
un chemin absolu peut être interprété comme une URL et laisser le frontend vide.
La feature sélectionne le contrôle du frontend
attendu et le marqueur `FORGE_LAUNCHER_STANDALONE_FRONTEND_READY` pour
`--verify-distributable` ; le profil Aube reste le comportement par défaut et garde
son propre marqueur. Le nom du produit et le titre initial générique sont vérifiés
avant d'ouvrir la fenêtre. Les builds utilisent le même cache Cargo et le même nom
de binaire : leurs compilations doivent être coordonnées et chaque binaire doit
être conservé et épinglé avant le build suivant.

```json
{"productName":"OpenRPG Forge","build":{"frontendDist":"../../../.build/tmp/mon-frontend"},"app":{"windows":[{"label":"main","title":"Jeu","width":1600,"height":960,"minWidth":1280,"minHeight":720,"resizable":true,"maximized":true,"fullscreen":false,"decorations":true,"transparent":false,"shadow":true,"backgroundColor":"#071b1b"}]}}
```

Le frontend autonome affiche l'identité et les cartes du manifeste vérifié et
demande au service natif de lancer l'entrée ou une carte admise. Une sauvegarde
existante impose la reprise tant que les slots de nouvelle partie ne sont pas
disponibles ; une demande de reprise préexistante est conservée. Les réglages
audio, d'affichage et d'accessibilité ne sont pas encore appliqués depuis ce
launcher. Les contrôles de build et les fixtures ne qualifient pas l'interface
visuelle, l'export complet ni l'installation sur machine propre.
