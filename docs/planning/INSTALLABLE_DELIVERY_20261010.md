# Installateur Windows classique — 10 octobre 2026

**Candidat `.1` rejeté par le propriétaire : remplacement non demandé du launcher.**
Les preuves historiques ci-dessous concernent un Hub utilisé à tort comme launcher.
Elles ne valident pas L’Aube des royaumes. La correction `.2` restaure le frontend
canonique dans `Forge.exe` et sépare `Forge-ContentManager.exe` ; ses preuves sont
conservées dans `classic-build-004-aube` et un nouveau dossier de test.
Ne pas distribuer l’ancien `OpenRPG-Forge-Setup-20261010.exe`.
La [livraison corrigée R3](INSTALLABLE_DELIVERY_20261010_R3.md), avec deux lancements
natifs Tidewater réellement vérifiés, et ses nouvelles preuves
remplacent ce candidat ; le détail ci-dessous reste historique.

Livraison locale `0.1.0-preview.20261010.1` : un seul fichier
`E:/OpenRPG_Forge/OpenRPG-Forge-Setup-20261010.exe` (737 426 850 octets).

SHA-256 : `d0dbc9d9cbfa343a5852aa426d0570bc6d851c0ef7ec1777b25e63cd5662ec8c`.

L’assistant français reprend les couleurs et le décor « L’Aube des royaumes ».
Il propose le dossier de destination, extrait un payload temporaire privé, installe
les applications et contenus, ajoute les raccourcis et l’entrée de désinstallation
Windows. WebView2 est inclus hors ligne; la signature Microsoft est contrôlée avant
son exécution si le runtime manque. Aucun outil de développement n’est requis côté client.

Le hub dispose aussi d’une destination modifiable, d’un dialogue de dossier, d’une
relecture explicitant le chemin et d’une invalidation du plan lorsque ce chemin change.
Le décor original est copié avec provenance; les titres externes sont rendus comme
texte et le mouvement respecte la préférence système.

## Composition et provenance

Le Hub et le gestionnaire de livraison sont recompilés et testés depuis les sources
figées de `classic-build-001`. Les essais d’emballage suivants réutilisent ces binaires
uniquement après vérification de l’égalité exacte des inventaires et hashes.
Le jeu et le Studio restent les binaires reconstruits le 9 octobre, conservés octet
pour octet avec leurs preuves de build. Les travaux simultanés du moteur ne sont
pas inclus dans cette révision de l’installateur.

| Pack embarqué | Version | SHA-256 du manifeste |
| --- | --- | --- |
| Applications | `0.1.0-preview.20261010.1` | `e76ebeb687bae77023d4da53400a2a0aa256b252983627faf53eb33b7a5d9510` |
| Assets de référence | `0.1.0-preview.20261009.1` | `ba8a76a25de26f0c80249e3dddea457ebf4ce90303c0d4a316fc73968ad857b6` |
| Module de référence | `0.1.0-preview.20261009.1` | `734a21d7e7a84a1af5a81574202c96013b86049487924a6ba32a66131004978c` |

Les archives restent indépendantes dans le payload de développement et le protocole
`.forgepack` reste disponible pour le futur client. La bibliothèque privée complète
n’a pas été exportée ni envoyée dans cet EXE : il contient les contenus nécessaires
au jeu de référence et le module auteur, avec leurs licences et provenance.

## Validation locale

- Gestionnaire de livraison : 8 tests existants et 4 tests ajoutés passent.
- Hub : 3 tests de chemin/racine/invalidation passent.
- Présentation : navigateur isolé avec fixture IPC explicite, destination Unicode,
  blocage d’un ancien plan, relecture du chemin, feedback de vérification, rendu texte
  des titres et fenêtre 900×650 contrôlés; captures conservées.
- EXE exact : installation dans `Dossier installé Élan`, vérification intégrale des
  fichiers, réinstallation, puis désinstallation passent.
- Intégration Windows : registre et trois raccourcis créés puis retirés. Les deux
  raccourcis Forge préexistants restent identiques.
- 28 fichiers utilisateur, une retouche auteur et des fichiers étrangers dans la
  racine et un package sont conservés après réinstallation/désinstallation.
- Launcher et Studio : processus natifs démarrés et vivants pendant les sondes,
  avec profils WebView2 isolés. Recette de lancement du jeu contrôlée; aucun nouveau
  diagnostic GPU du jeu n’est revendiqué dans ce chantier d’installateur.
- Ancienne livraison → nouvelle → rollback : vérification complète et conservation
  des 28 fichiers utilisateur passent avec les packages finaux exacts.

La désinstallation conserve les fichiers de packages modifiés/étrangers et un verrou
neutre pour éviter une course entre installateurs. Elle ne supprime jamais récursivement
le dossier choisi. Si des retouches rendent un package conservé partiel, une nouvelle
destination évite d’effacer ces retouches lors d’une installation neuve.

Preuves : `E:/OpenRPG_Forge/.delivery-evidence/20261010/classic-build-003/`,
`classic-install-002/`, `upgrade-final/` et `hub-ui-final/`.
Les premiers essais d’emballage sont conservés séparément et ne sont pas la livraison
retenue : encodage ANSI corrigé, puis initialisation du répertoire temporaire corrigée.

La signature du prérequis réel a été vérifiée localement. L’installation du prérequis
sur Windows vierge et l’acceptation humaine des fenêtres natives restent à effectuer.
Le jeu fourni reste une promenade native de référence. Gate 3 reste ouverte et cette
livraison locale ne constitue pas une qualification RPG ou de release.
