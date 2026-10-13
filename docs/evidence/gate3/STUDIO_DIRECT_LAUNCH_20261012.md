# Studio — lancement direct configuré, 12 octobre 2026

Les hashes et essais de cette note décrivent le correctif de démarrage du
12 octobre. L'identité actuelle du même EXE commun et ses corrections suivantes
sont dans [le suivi du chargement, de la mémoire et du jeu séparé](STUDIO_LOADING_NATIVE_GAME_20261012.md).

L'EXE commun attendait FORGE_AUTHORING_ROOT et les paramètres du catalogue dans
son environnement. Lancé directement, il ne pouvait donc pas charger le module.
La commande pwsh fournie utilisait le runtime injecté par Codex, pas un point
d'entrée vérifié dans l'environnement ordinaire du propriétaire.

Le backend natif lit désormais `forge-studio.local.json` près de son EXE.
Cette configuration versionnée sélectionne explicitement le workspace, le
Vault et le catalogue de modèles avec son SHA-256. Les chemins relatifs sont
résolus contre le dossier de l'EXE, indépendamment du dossier du shell.
Une configuration malformée ou partielle est refusée ; les contrôles des racines,
du catalogue et des modèles restent en vigueur. Les variables explicites des
installateurs et outils restent prioritaires et ne sont pas modifiées par le
Studio. Un override partiel du catalogue n'est pas complété avec un autre profil.

Le même EXE commun reste à
`E:/OpenRPG_Forge/.gate3-ntfs-cargo-20261002/release/forge-studio.exe`.
Le point d'entrée à double-clic est `E:/OpenRPG_Forge/Lancer-Editeur.cmd`.
L'ancien script PowerShell devient un simple wrapper compatible PowerShell 5 ;
le lancement normal n'a plus besoin de PowerShell. Aucun autre EXE ou checkout.

Validation : 256 tests Studio PASS, 3 ignorés, format et Clippy natif strict PASS,
build natif PASS. Le frontend et le renderer ne sont pas modifiés par ce correctif
de configuration native. La retouche du plafond de source-support de la bibliothèque
présente dans le checkout commun est préservée ; elle appartient à l'autre chantier.

Les trois essais passent avec le PATH Machine + User ordinaire, sans variables
Forge/Vault/WebView héritées : EXE direct, raccourci `.cmd` et wrapper sous
Windows PowerShell 5.1. Chaque session ouvre Tidewater et affiche ses bâtiments,
son ponton et son terrain. Elles sont fermées par l'interface, sans arrêt forcé
ni retouche. Le wrapper est testé avec `-ExecutionPolicy Bypass` ; aucune
politique PowerShell de la machine n'est modifiée. Le double-clic `.cmd` ne
dépend pas de cette politique.

EXE SHA-256 : `8db598d1a2e3ebb16d9d8fad7b7772d3fd0a0b872f3ae8e1850e1d256699e276`.
Empreinte des 2540 entrées de compilation :
`8cbc516fdb014f78d54ee274f33309ad4e342b9f2559b13d9fa9ef183cd2a087`,
inchangée entre compilation et essais UI. La session directe ferme avec exit0 ;
les deux wrappers ferment avec exit0, mais le code de sortie de leurs enfants
Studio n'est pas disponible dans les handles d'observation : seule leur
fermeture normale par l'interface est attestée.
Preuves : `E:/OpenRPG_Forge/.gate3-evidence/20261012/studio-direct-launch-fix-001`.
Le retrait du test intégré reste en vigueur. Aucune qualification P05, Gate 3,
CI distante ou performance n'est déduite de ce correctif de démarrage.
