# Correction du launcher dans l’installateur — 10 octobre 2026

**Historique, remplacé par [R3](INSTALLABLE_DELIVERY_20261010_R3.md).**
Le vrai lancement a ensuite révélé une collision entre les sorties de sessions
Tidewater. R3 corrige cette panne et vérifie deux sessions interactives réelles
sur l'installation F:. L'EXE R2 est archivé dans
`classic-build-004-aube/installer-superseded-game-collision.exe`.

Le candidat `.1` utilisait le gestionnaire de contenu à la place du launcher,
ce qui dépassait la demande du propriétaire. Il est rejeté pour cette raison.
Son EXE est conservé uniquement dans `classic-build-003/installer-rejected-launcher-replacement.exe`.

La révision `0.1.0-preview.20261010.2` fournit un EXE autonome :
`E:/OpenRPG_Forge/OpenRPG-Forge-Setup-20261010-r2.exe` (1 188 342 472 octets).
SHA-256 : `7672f78ae7c8c35ddaab451521580eea9557da61b36720287d51b6072fc97c27`.

## Rôles restaurés

- `Forge.exe` contient le véritable launcher L’Aube des royaumes : accueil,
  navigation, guide du joueur, bestiaire, illustrations et dialogue de sortie.
- `Forge-ContentManager.exe` est le Hub séparé pour ajouter et vérifier les packages.
- L’assistant NSIS reste séparé : thème Aube, choix du dossier, installation hors
  ligne, raccourcis launcher/Studio/contenus et désinstallation Windows.

La coque canonique est copiée sans modifier le checkout Launcher-Prototype ni
`launcher-aube`. L’adaptateur installé utilise les recettes de `forge_delivery` :
racine choisie, packs séparés et workspace utilisateur. Aucun serveur de développement
n’est requis. Jeu et Studio restent les binaires reconstruits le 9 octobre ;
leurs hashes sont vérifiés avant réemballage. Les modifications concurrentes du moteur
ne sont pas intégrées par ce chantier de correction du launcher.

## Preuves locales

- `classic-build-004-aube` : sources figées des trois workspaces et build de production.
  Tests du launcher (3 bibliothèque + 6 desktop), du Hub (3) et de livraison (12) PASS.
- `aube-presentation-parity.json` : 86 fichiers canoniques identiques ; styles,
  app/codex et illustrations inchangés. Le HTML ajoute seulement le bridge desktop
  original. Le retrait des chemins documentaires locaux suit le builder canonique.
- `aube-ui-restored` : accueil, guide, recherche Carillon de bronze et bouton Jouer
  contrôlés dans un navigateur isolé avec une fixture IPC déclarée.
- `classic-install-004-aube` : EXE exact, installation dans un chemin Unicode,
  vérification intégrale, réinstallation et désinstallation PASS ; 28 fichiers
  utilisateur et les fichiers étrangers sont conservés. `Forge-ContentManager.exe`
  est également absent après désinstallation.
- Le vrai WebView embarqué et le vrai Snapshot IPC passent et reconnaissent le jeu
  installé. Launcher et Studio démarrent avec des profils isolés. Le jeu est contrôlé
  par sa recette de lancement ; aucun nouveau essai GPU n’est revendiqué ici.
- `content-manager-ui-separated` : destination, invalidation/relecture des plans,
  titres rendus comme texte et présentation minimale contrôlés sur le Hub séparé.

Les raccourcis existants dans l’installation utilisateur sont conservés jusqu’à la
correction en place, avec vérification de leur propriétaire. Les preuves de cette
correction sont dans `repair-user-installation-aube` ; leur résultat est distinct des
essais isolés. La bibliothèque privée complète reste un transfert séparé ; les trois
archives de contenu sont conservées dans le payload de développement pour intégration
au futur client.

Correction en place **PASS** dans `F:/instal test/Installation` : vérification
intégrale après mise à jour, vrai WebView/IPC Aube connecté au jeu, 61 fichiers
personnels identiques, cinq raccourcis enregistrés et deux anciens raccourcis
étrangers conservés. L’ancien core reste le snapshot précédent. Les cibles et
arguments des raccourcis sont conservés dans `shortcut-targets.json`.

Ces résultats ne constituent ni acceptation visuelle humaine du nouvel installateur,
ni qualification de Windows vierge, ni clôture de Gate 3.
