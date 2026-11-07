# Reprendre OpenRPG Forge hors Codex

**Gate 9 planifiée, non ouverte — 7 novembre.** Tidewater doit être un jeu créé
avec Studio, reposant sur des capacités génériques de l'éditeur et du moteur,
reproductibles dans d'autres jeux. L'inventaire et les critères R9-01 à R9-06
figurent dans la [roadmap](planning/MIGRATION_ROADMAP.md#gate-9--tidewater-reproductible-par-les-capacités-communes).
Cette décision ne lance pas la gate et ne change pas les verdicts de Gate 7.

<a id="gate-7-etat-courant"></a>

## Gate 7 — état courant au 7 novembre 2026

**Gate 7 reste ouverte.** R7-01 à R7-06 disposent de verdicts locaux Windows
datés. Le Hub a été retiré du checkout au lot 048. Le produit suit le parcours
Studio → export du jeu et de son launcher → jeu autonome.

- **R7-07, R7-09 et R7-10 sous Windows.** Le candidat privé 049 couvre le
  Studio installé et l'export de Tidewater et du Grand Plateau dans un
  Sandbox hors ligne sans SDK. Un invité joueur séparé a installé, lancé et
  repris les deux jeux sans Studio. Les cycles de mise à jour, restauration
  et désinstallation du Studio et de Tidewater préservent les données
  contrôlées. Le bilan Windows 049 rapproche 43 pièces.
- **R7-07, R7-09 et R7-10 sous Linux.** Le build privé 0837 et les essais en
  conteneur Debian 12 sans SDK couvrent l'ouverture de la fenêtre Studio
  et, avec le kit 0837, le lancement de diagnostic et la sauvegarde du
  Grand Plateau. Restent à qualifier un build sur les sources courantes,
  le parcours auteur/export, la reprise via le launcher, la mise à jour,
  la restauration et le parcours complet sur un OS propre.
- **R7-08.** Le service d'export natif a produit les crédits de la
  référence Tidewater. L'attribution des dépendances du Studio, dont
  `drain_filter_polyfill` identifié dans le WASM livré, et les droits de
  redistribution des assets et composants restent à établir.
- **Performance du moteur commun.** Dernier trajet Tidewater mesuré sur
  l'export 049 au profil Moyen : 18,85 ms en moyenne, 31,37 ms au p95.
  La cible moyenne de 16,67 ms et la comparaison appariée avec la
  référence restent à vérifier. Une exécution par variante ne permet
  pas d'attribuer l'écart mesuré à la dernière modification.

Voir le [plan](planning/GATE7_IMPLEMENTATION_PLAN.md) et la
[matrice d'acceptation](evidence/gate7/GATE7_PROGRESS_20261023.md#matrice-dacceptation).
Les reçus détaillés par candidat suivent.

## Historique des qualifications

Le Studio 018 est assemblé sur les sources communes. Il intègre l'ouverture
du navigateur sur un espace vide, le runtime avec diagnostics de disponibilité
avant fermeture et le bundle de notices 015. Reçus dans
`first-run-resume-observations-20261105-018`. Installateur privé Windows
`ForgeStudio-0.1.0-preview.20261105.018.exe`, SHA-256
`bece3b6866eea268f041b0673957cdc95361339b4595a3a850d655066e4ba540`.
Reçu `studio-installer-result.json`, SHA-256
`7fdd2d830caf73dde45973af383d8b050dfc17d3112449c8c5c0fce33bdf03a8`.
Le chemin source de la référence Rust est toujours présent dans le WASM 018.
Le manifeste Studio inclut les trois documents Rust et leur preuve.
Pour `erased 0.1.2`, 38 noms de fonctions du WASM avant optimisation
référencent le type `ErasedBox` dans les fermetures de Tachys. Le WASM livré
conserve le chemin du code Tachys et omet la table des noms de fonctions.
L'archive Cargo verrouillée correspond aux sources locales et déclare MIT.
Cette analyse conserve la réserve runtime du Studio et ne tranche pas
l'attribution du code optimisé. Reçu `R7-08-studio-erased-binary-scope-018.json`,
SHA-256 `329980ab288d4f0a254c18eb0d34a2ce906fbe3d058e63835a8e1321e3a84329`.
Le scénario Windows propre 018 est prêt dans `studio-clean-20261105-018`.
Il monte en lecture seule l'installateur du Studio 018 et l'export privé des
Trois Cairns 017, déjà vérifié sur l'hôte. Reçu `windows-sandbox-plan-018.json`,
SHA-256 `e91b349d7d183ad022411142c61512d26d8798a42818e7275171d69a1f759fa4`.
L'activation de Sandbox a réussi avec l'option sans redémarrage automatique.
Windows a demandé le redémarrage effectué depuis. Reçu
`windows-sandbox-activation.json`, SHA-256
`e88d12c4db8a59506f61b27b8b88e3d2f34695907040aab2ba4ef672606cd25e`.
Après redémarrage, le scénario 018 a ouvert Windows Sandbox. Le relevé
invité Windows 11 Entreprise 26100 confirme l'absence de SDK et des cinq
commandes de développement recherchées, avec WebView2 déjà présent.
Reçu `windows-sandbox-guest-preflight-018.json`, SHA-256
`c031d965c62d2d1abf171105e9a03f0a5bac50a6d05124ff508f5e3d3999dbc4`.
L'installation du Studio 018 autorisée a échoué avant la création du plan.
`forge-delivery.exe plan-file` sort avec `0xC0000135`. L'invité ne contient
ni `VCRUNTIME140.dll` ni `VCRUNTIME140_1.dll`. Les binaires natifs 018 les
importent selon leur rôle, alors que l'installateur ne livre que WebView2.
Reçu `R7-10-clean-windows-studio-crt-refusal-018.json`, SHA-256
`9336a83ea9f466f4343ef0471d3d3156ae895824ea414a58454f574137935c13`.
La construction Windows de distribution passe à la liaison statique du CRT.
Le premier outil de livraison recompilé a inspecté le paquet des Trois Cairns
dans le même invité dépourvu de ces DLL. Reçu
`clean-windows-static-crt-20261106-019/R7-10-clean-windows-delivery-cli-static-crt-019.json`,
SHA-256 `19d60fb458c594de9e40c18dd84d035af039c8e90dbc3e65b756dd622486b9fe`.
Le candidat 020 regroupe le Studio, le moteur de jeu exporté, le launcher
autonome et l'outil de livraison. Leurs tables d'imports PE ne contiennent
aucune DLL Visual C++ redistribuable. Reçus dans
`windows-static-crt-candidate-20261106-020` sous la campagne Gate 7.
L'installateur `ForgeStudio-0.1.0-preview.20261106.020.exe` a pour SHA-256
`47bb5b031173a4aaffce26920487ba4e7bb18ef598a7688cbb2bf988c3e01bb8`.
Le premier Sandbox 020, ID `25216f97-0c0c-451a-8409-086b336cca03`, a confirmé
Windows 11 Entreprise 26100 sans SDK, compilateurs ni DLL redistribuables.
Reçu `windows-sandbox-guest-preflight-020.json`, SHA-256
`64d773bcf8fd085e4b48c90d437d13a7f331893416e7045fe90412c0563dd3ef`.
Le bureau invité est resté noir dans Computer Use, malgré Explorer et DWM
actifs dans la session invitée et une reconnexion. Ce Sandbox est arrêté.
Le scénario en rendu logiciel montre le même écran noir, avec un nouveau
précontrôle réussi. Sandbox actif `072bcbc8-2aba-4dd9-9105-7e255ffcbf45`,
fenêtre `41618762`. Reçu
`windows-sandbox-guest-preflight-020-software-display.json`, SHA-256
`5c387199120edbd956561b8090a947349d0aa88824e7cd64772d1ef1d48153b8`.
Après reconnexion dans la fenêtre active, Computer Use voit le bureau invité.
L'assistant du Studio 020 a été affiché, puis l'invité s'est arrêté avant
l'accord au bouton « Installer ». À la reprise, deux nouveaux invités avec
les mêmes entrées épinglées ont refusé l'ouverture de session (nombre
maximal de sessions dépassé et `0x80070520`), y compris après fermeture
des clients résiduels de qualification. Les invités ont été arrêtés.
Reçu `windows-sandbox-resume-connection-refusal-020.json`, SHA-256
`f14acf285da7379f4090a5aa7c9fbe7d427fd57b5818b8b3208b8bc7a015b75c`.
Sans redémarrage hôte, la reprise en rendu logiciel a brièvement ouvert
l'invité `6b2e2216-f61e-44be-be0c-78eb64308703`. Précontrôle invité
`windows-sandbox-guest-preflight-020-recovered-software.json`, SHA-256
`84d8d8c7592b50dbc587e95dafece53231f08626971f845df6f3b584787fb213`.
Le client a affiché une déconnexion et l'ID a disparu après la fermeture
du client précédent. Aucune installation 020 n'a eu lieu dans cet invité.
Le processus Sandbox résiduel a disparu sans redémarrage du PC. Le nouvel
invité en rendu logiciel `d08cc619-035d-40f5-993e-ec421753c876` a installé
le Studio 020 avec l'accord de l'utilisateur. L'installateur a terminé et
le Studio installé a affiché son écran d'accueil. Reçus
`windows-sandbox-guest-preflight-020-software-20261106-165420.json` (SHA-256
`2f1fd22e9058e409179dc5f1c98b199d4de7ad780a3fd74eb9389703791150fe`) et
`R7-10-clean-windows-studio-install-020.json` (SHA-256
`230123a794e9ae3955f1e976e2d5406a5b0f26ff265ae002bc811605ca193d7e`).
Le projet vierge « Mon projet », sa carte et le joueur initial ont été créés
et sauvegardés dans le Studio installé. La vue 3D est restée en chargement
jusqu'à la fermeture de la carte. L'export privé 1.0.0 a produit quatre
fichiers vérifiés par les deux lecteurs natifs. Reçu
`R7-10-clean-windows-studio-export-020.json`, SHA-256
`11a493df2c2bd3ef59072ecf7fdbf4b011b8fbe1f5bf0442315e8a78e866e0f7`.
La copie de reprise du jeu reste sous `windows-clean-evidence-software-20261106-165420/mon-projet-export-020`.
Les trois fichiers auteur sont archivés dans `mon-projet-workspace-v1-020.zip`,
SHA-256 `9f68d36ce00f3c628647a86da6eeb5be9f69cead36094918650c1d409e9add49`.
Installation du jeu et ouverture de `Jouer.exe` réussies dans le Sandbox
en rendu logiciel. Premier démarrage, profil graphique Faible : Bevy a
sélectionné Microsoft Basic Render Driver (CPU/DX12) et reçu `Out of Memory`
avant la première projection prête. Aucune reprise de partie prouvée.
Reçus `R7-10-clean-windows-game-install-020.txt` (SHA-256
`fa135443cc5a32cecc94a42670640547b11bbf36c3b25cb2bf8c82d2d35295d9`) et
`R7-10-clean-windows-game-runtime-report-020.json` (SHA-256
`d23b43b909e9293453267522e218c7a5076ea53c7acc12efb829609724e8501f`).
L'invité logiciel est arrêté. Réutiliser l'export scellé dans un invité
avec GPU et sans Studio pour la partie et la reprise.
Le Sandbox neuf `63051335-5a29-4630-b68f-9f8d8a2c6c19` expose les deux
adaptateurs AMD dont la Radeon RX 7800 XT. Précontrôle
`windows-sandbox-game-only-vgpu-preflight-020.json`, SHA-256
`218b18cd168a6e94f2fc87911db28179046cb43a815b64a1a356190c374cf174`.
Il contient le jeu seul, sans Studio installé ni SDK. À la racine du dossier
partagé, le launcher refuse le chemin Windows canonique qui se termine par
une barre oblique inverse. La copie locale invitée `Desktop/MonProjet020`,
vérifiée aux quatre empreintes, affiche et vérifie le plan d'installation.
Le code de livraison applique désormais sa normalisation de répertoire à
la source du candidat Windows avant et après résolution, avec le même contrôle
des liens. Le test ciblé accepte un dossier canonique terminé par `\` et
vérifie toujours l'intégrité du candidat et le rollback. Reçu
`r7-09-installer-shared-root-manifest-20261106.log.receipt.json` à la racine de
la campagne. Le binaire 020 reste celui d'avant la correction.
L'installation a été autorisée et vérifiée dans ce Sandbox GPU.
`Jouer.exe` a lancé la partie, F5 a créé la sauvegarde et « Reprendre »
a affiché « Sauvegarde reprise » dans un deuxième processus. La Radeon RX
7800 XT a été utilisée en Vulkan pour les deux lancements. La sauvegarde
est restée identique octet pour octet. Les deux sorties réussissent après
une première projection prête à la trame 15. Le second rapport relève
une dernière projection prête à la trame 680 et un état final indisponible
à la fermeture.
Reçu `R7-10-clean-windows-game-vgpu-resume-020.json`, SHA-256
`99881b4005ac8e754cdf33f52219501eab390ef74ca044d67ef8f153e3dc9ce3`.
Le Studio 020 installé dans le Sandbox GPU `eeb47575-98e3-4192-921f-b0a2f50beb9f`
a rouvert `Mon module / Ma carte` depuis les trois fichiers auteur épinglés.
Le terrain s'affiche et la commande « Rapprocher » change le cadrage. Les
diagnostics affichent une vue 3D active et des allocations WebGPU. Le journal
reste sur « Terrain chargé · préparation de l'affichage ». Les trois fichiers
auteur sont inchangés et le binaire lancé correspond à la preuve de build 020.
Reçu `R7-10-clean-windows-studio-viewport-vgpu-020.json`, SHA-256
`a5bf57f92985913a30799ae8b39daca838c62a408692e8d7ec293965d287541f`.
Après accord, la désinstallation NSIS du Studio 020 a terminé dans ce même
Sandbox. Elle a retiré les fichiers des paquets, le bootstrap, le raccourci
et l'enregistrement Windows. Le répertoire des paquets vide et le fichier
de verrouillage de zéro octet subsistent. Les trois fichiers auteur ont les
mêmes SHA-256 avant et après. Reçu `R7-09-clean-windows-studio-uninstall-020.json`,
SHA-256 `270a81cc18bb69f2fb981514f2acc3fc95b251905a60a834121834b4d40e1678`.
L'invité a été arrêté après la vérification. Les exigences globales
R7-07/08/09/10 restent ouvertes.
R7-09 : l'ancien garde NSIS refusait une réinstallation dans ce dossier
résiduel sans marqueur. Le contrôle natif utilisé par NSIS accepte maintenant
uniquement un verrou vide et un répertoire de paquets vide dans un dossier
sans marqueur. Le test ciblé vérifie la désinstallation, le refus d'un fichier
étranger, la réinstallation et la conservation du projet. Les deux recettes
NSIS compilent avec les charges inertes du test. Reçu
`r7-09-studio-reinstall-root-20261106.log.receipt.json` à la racine de la
campagne. Le contrôle depuis un installateur Windows reconstruit reste dû.
Le lot `windows-reinstall-correction-20261106-021` regroupe cette correction
et celle du chemin d'export Windows. Son inventaire de build diffère du 020
sur six fichiers du service de livraison et de sa recette NSIS. Le CLI,
le launcher, le moteur et le Studio ont compilé en release Windows depuis
le cache partagé. Le kit et l'installateur sont assemblés. Reçu
`studio-installer-result.json`, SHA-256
`11a4463ca7ee5ab5232b9ed0faa3ffb826a39d3ced189ae3e8b089a42ebfb390`.
Les quatre binaires Windows 021 sont x64. Les noms de DLL importées
correspondent à ceux du 020, sans import du runtime C++ redistribuable.
Reçu `windows-pe-imports-021.json`, SHA-256
`b81734ba419ef9358c358ca87b8e10fa57a9a8caf38c655603f7b4edd5bd91eb`.
R7-08 : les quatre reçus 021 confirment `+crt-static`. Les Build Tools 2022
17.14, MSVC 14.44 et le SDK 10.0.26100.0 présents sur l'hôte sont recensés
avec les empreintes des trois bibliothèques CRT statiques de référence.
Reçu `R7-08-windows-static-crt-scope-021.json`, SHA-256
`9da9a896f72ef9d687658514e86d0d50281aae9dc53129cffebad219d81343f1`.
Les PDB ont ensuite été appariés aux quatre exécutables livrés par GUID et
âge CodeView. Leurs enregistrements de modules d'édition des liens identifient
des objets de `libcmt.lib`, `libvcruntime.lib` et `libucrt.lib` dans les quatre
rôles, depuis les chemins MSVC et SDK recensés. Reçu
`R7-08-windows-static-crt-linked-modules-021.json`, SHA-256
`729f980f058ae640848cc69287957d946208d48fbd75ef56c261fd9137305f4f`.
La relecture du paquet Studio 021 et de l'export « Mon projet » vérifie
respectivement 472 et 471 entrées de licence. Les deux index des
contributions originales Forge référencent les mêmes textes MIT et Apache-2.0,
identiques à ceux du dépôt. Le module de jeu ne référence aucun asset du
catalogue. Les trois réserves Cargo demeurent et les paquets déclarent un
usage local privé. Reçu `R7-08-windows-021-packaged-notices.json`, SHA-256
`175d1f1645c76c59d2e5caa60914b08d5acf8f3f458281625526bf054da2417e`.
Le catalogue des Build Tools installés identifie les conditions applicables
à cet outil. Le SDK installé renvoie à sa liste de redistribution, qui ne
nomme pas `libucrt.lib`. Le droit de redistribution des objets CRT liés
dans les quatre exécutables reste à établir.
[Conditions des Build Tools 2022](https://visualstudio.microsoft.com/license-terms/vs2022-ga-diagnosticbuildtools/),
[liste Visual Studio 2022](https://learn.microsoft.com/en-us/visualstudio/releases/2022/redistribution)
et [liste du SDK Windows](https://learn.microsoft.com/en-us/legal/windows-sdk/redist).
L'installateur 021, SHA-256
`74691eb1dfe1519d0b405439d20f9c7e77d7a99b99a51b6546b8b3448dc3755e`,
est ouvert dans le Sandbox GPU `97ab4d22-6f69-40d2-8f90-85ac931a0cd3`.
Le précontrôle `windows-sandbox-reinstall-preflight-021.json` (SHA-256
`fd16cd06f270e2979e7f6af08568e0ea8822df405271ae09e4fff635c3773ca5`)
vérifie le binaire et reproduit le dossier résiduel du retrait 020 à côté
du projet auteur épinglé. Le Studio 021 a été installé dans ce dossier et
« Ma carte » s'affiche dans le projet rouvert. Le binaire du Studio et le CLI
installés correspondent aux preuves de build 021. Les trois fichiers auteur
sont inchangés. Reçu `R7-09-clean-windows-studio-reinstall-021.json`, SHA-256
`65099a009bf5346280d124feea88bc65b6c50509630d1a96a0383caa9d19c504`.
Cet essai couvre la réinstallation après retrait, sur un résidu reproduit,
et l'ouverture des données conservées. Dans un autre invité propre, le
Studio 020 a été installé, mis à jour vers le 021 par NSIS en mode silencieux,
puis restauré au 020 par le CLI de livraison. Les trois états ont été vérifiés
et le Studio restauré a ouvert sa fenêtre. Les trois fichiers auteur sont
restés identiques. Le bootstrap Windows conserve sa version 021 tandis que
le paquet actif revient au 020. Reçu `R7-09-windows-studio-update-rollback-021.json`,
SHA-256 `97e01abca94ea0914a70c156a5d942ac1cca10a99208af3fd78cc58048c3a40c`.
Le cycle Windows du jeu exporté est vérifié dans un Sandbox avec vGPU.
L'installateur 021, lancé à la racine d'un partage en lecture seule, a mis
« Mon projet » 020 à jour vers le 021. La sauvegarde 020 a repris dans le jeu
mis à jour, puis dans le 020 restauré et rouvert depuis `Jouer.exe`. Après
désinstallation, les paquets, `Jouer.exe` et le manifeste d'installation
sont retirés, et le SHA-256 de la sauvegarde est identique. Reçu
`R7-09-windows-game-lifecycle-021.json`, SHA-256
`dc4084e4eba8c1c2670466e65c58c42ec73fd42ae070744344fecd8bcdc77cb6`.
La qualification des distributions Linux reste ouverte.

G7-D5 : relancer une mise à jour Studio déjà activée avec le même paquet
et le même dossier utilisateur conserve désormais la révision active et
la cible du rollback. Le service vérifie de nouveau le paquet et l'état
installé sous verrou. Le test Windows refait le plan après activation,
reprend la mise à jour et revient à la révision précédente avec les
paquets et le workspace préservés. Reçu central
`studio-update-retry-preserves-rollback-20261106-001.log.receipt.json`,
SHA-256 `0573fa09709c529dc9d28d592e1a7dcc41e71f8d9ce31d3c312d534eeba8c75a`.
Cette correction du service est postérieure au 021.
Dans un Sandbox Windows, le paquet 021 a été activé par son CLI sans
copier les exécutables d'amorçage ni créer les raccourcis. Un installateur
de qualification avec le CLI et le script NSIS corrigés a repris cet état.
Il vérifie que le paquet actif correspond au sien, termine l'intégration
Windows et conserve à l'octet près la tête d'installation et le fichier
de projet. Reçu `R7-09-windows-nsis-first-activation-recovery-20261106-001.json`,
SHA-256 `cf2b87a1672fe149e51777baf01df5f2936802c501b36b8ebfa2c84a813370bf`.
L'essai reproduit l'interruption après activation de la première révision.
L'installateur Studio appelle désormais une variante de l'application du
plan qui enregistre un marqueur de reprise sous verrou avant la copie des
paquets. Le test Windows provoque un échec pendant la copie, vérifie
l'absence de tête active, puis reprend avec le paquet corrigé. La révision 1
est vérifiée et le dossier de copie incomplète est préservé. Un dossier
étranger sans marqueur est refusé. Les dix tests Rust ciblés passent, ainsi
que les huit tests NSIS, dont la compilation des deux profils.
Reçu `studio-resume-before-activation-20261106-001.log.receipt.json`,
SHA-256 `601f6e50e59fe6249d9f539781f3a6790fd742b6b974ef4c2d33bc3bf3bd1bb7`.
Ce premier contrôle couvre le service appelé par NSIS.
L'installateur historique Gate 4 `OpenRPG-Forge-Setup.exe`
à la racine du workspace a été rapproché de son reçu 019 par SHA-256
`4573abba6f8471991b913855d85f9122dcf04029d04853c71e64d3f57fb6d127`.
Dans un Sandbox, ce 019 s'arrête sur le Windows vierge avant de publier
sa tête. L'installation aboutit après mise à disposition du runtime
Microsoft Visual C++ x64 dans l'invité. Le Studio 021 installé à côté
réutilise le `UserData` par défaut. Les 89 fichiers de l'espace auteur
historique (144 150 023 octets), un fichier de sauvegarde témoin et les
trois manifestes de paquets privés conservent leurs empreintes. Le Studio
ouvre sa fenêtre et le verrou du workspace historique est occupé.
Reçu `G7-D5-windows-legacy-019-studio-021-coexistence-20261106-001.json`,
SHA-256 `94258ad6481d968b63fe73f9e112c005e5cd7cb7a363fe1581db77e4e9e158b1`.
Cette preuve couvre la coexistence et la reprise de l'espace auteur par
défaut. La reprise en jeu d'une sauvegarde historique, la réouverture
visuelle d'une carte, la reprise des ressources de référence et le retrait
du Hub restent à qualifier.
La source de l'installateur Studio reprend désormais, à sa première
installation, le dossier utilisateur d'une installation Forge historique
valide enregistrée dans Windows. Une destination fournie explicitement
est prioritaire à la première installation. Les mises à jour conservent
le dossier déjà épinglé et une entrée historique orpheline permet le
parcours neuf. Le test Windows appelle le CLI de l'installateur et applique
le plan sur une installation historique isolée. Le projet et la tête
historique conservent leurs empreintes. Onze tests Rust et huit tests
NSIS passent, dont la compilation des deux profils.
Reçu `studio-migrate-custom-user-root-20261106-001.log.receipt.json`,
SHA-256 `571195fa7575d3e457c0ef5aab9de10a2f1a52a452c8666662280eabb777e495`.
Un essai combiné du 6 novembre valide le parcours NSIS complet dans un
Sandbox. Une petite fixture historique v1 enregistre un dossier
utilisateur personnalisé. L'application d'un paquet de test corrompu
échoue après avoir créé le marqueur et le dossier de copie, avant la
publication de la tête. L'installateur de qualification, qui réutilise le
paquet Studio 021 avec le CLI et le script NSIS corrigés, reprend cet état.
Il lit l'enregistrement Windows, active la révision 1, conserve le
dossier personnalisé et laisse les empreintes de la tête historique,
du fichier auteur et de la sauvegarde témoin identiques.
Reçu `G7-D5-windows-nsis-custom-root-before-activation-20261106-001.json`,
SHA-256 `bde9c87b624397ee161c113cccbf833084ee9b274e058e9f3dde4fd5fb0f26d8`.
Le test de dossier personnalisé emploie une installation historique
synthétique. Le reçu 019/021 couvre séparément la coexistence réelle
avec le dossier par défaut.
Les sauvegardes réellement produites par le runtime 019 pour Les Trois
Cairns 1.0.1 et Le Relais des Lanternes 1.0.0 ont été relues depuis
l'archive Gate 4. Le décodeur actuel accepte les deux sauvegardes avec
leurs modules exacts en mode strict et les réencode à l'octet près.
Reçu `G7-D5-legacy-019-native-save-readback-20261106-001.json`,
SHA-256 `f8c388cf5ab361bae9983b59bb75ecbd2d16fe7e24935a012be957d16b027dad`.
Ces enveloppes identifient le module, la zone et les empreintes source
et fonctionnelle. Le lien avec l'identité du jeu exporté doit être
vérifié via le module avant de transférer les données de `UserData/saves`
vers l'espace de sauvegarde du jeu. Ce contrôle CPU établit la
compatibilité des octets.
La commande native `import-legacy-save` relie les deux têtes
d'installation au projet, au module et à la version exacte de la carte.
Elle copie le document validé vers un emplacement libre du jeu exporté,
sous les verrous de livraison et de sauvegarde. Une relance avec la même
sauvegarde conserve l'empreinte et une sauvegarde cible différente est
préservée. Le test Windows confirme l'isolation des projets et l'état
`Ready` dans la vue native utilisée par le launcher. Reçus
`legacy-save-import-project-isolation-20261106-002.log.receipt.json`
(`445ea752b4d5fb3087a718f639595a3b64fbd7655e3721ca8c344fdd071af93c`) et
`legacy-save-import-cli-build-20261106-001.log.receipt.json`
(`59c3a9967d6f3ba7801647147edcc9e6206f50bf8c609687f5400d94414abd4a`).
Le launcher Windows propose aussi la recherche dans « Réglages » lorsque
l'ancienne installation Forge est enregistrée. Le joueur choisit sa
carte et l'action d'import apparaît après vérification de la sauvegarde.
L'empreinte vérifiée est contrôlée à nouveau lors de la copie. La vue
« Reprendre » est actualisée après l'import. Le test du frontend couvre
le refus, la sélection et le focus, puis l'activation de « Reprendre »
sur réponses simulées. Le contrôle UX heuristique relève zéro
avertissement. Le test Rust vérifie le refus d'une empreinte changée et
la vue native après copie. Le contrôle Windows du launcher
`--features standalone` passe.
Reçus `launcher-legacy-save-pinned-review-20261106-001.log.receipt.json`
(`550f2ce8fbb73efd4d0c758e69c303f4e570eba098197f07e1783e3c28cd383c`) et
`launcher-legacy-save-windows-standalone-check-20261106-001.log.receipt.json`
(`79fb890cb9cc06ce2e1db19a26a78e6ba7ae8f254f42b261da9f2830050b1b31`).
Le launcher installé des Trois Cairns a importé la sauvegarde 019 archivée
depuis le dossier historique, puis le jeu a repris la quête terminée, la
récompense et l'endurance à 90 dans Windows Sandbox avec GPU. L'original et
la copie importée ont le même SHA-256. Le candidat privé a été assemblé avec
le module archivé et une structure d'installation historique reconstituée.
Reçu `G7-D5-windows-launcher-legacy-save-resume-20261106-001.json`,
SHA-256 `29dd56506db6f1005b49b8a9d206431327c342ec40f9e749b306f9ac94009726`.
Pour les cartes historiques, le nouvel installateur Studio reprend le
paquet `reference-assets` de l'installation v1 enregistrée lorsque le
même dossier utilisateur est utilisé. La copie transitoire est contrôlée
par le plan de livraison. Le paquet installé conserve son manifeste et
ses droits privés. Le Studio utilise `data/scene/static-assets.json`
avec l'empreinte du manifeste et résout les modèles avec le lecteur
existant, y compris après retrait de l'ancienne installation. La copie
de ressources appartient à l'installation Studio. Sa désinstallation
la retire et préserve le workspace utilisateur.
La lecture du paquet original 019 a vérifié 8 138 fichiers, 496 953 319
octets et les quatre entrées du catalogue. Reçu
`G7-D5-legacy-019-reference-assets-readback-20261106-001.json`, SHA-256
`26e35cb4f4b210bbdce65154365735c0ff6a63ad8942eac4e94672f312aa1db4`.
Le test natif Windows sur fixture couvre la mise à jour, la relance après
retrait de l'installation v1, le retour arrière et la remise à niveau,
le refus d'un catalogue altéré et la désinstallation avec conservation du
workspace. La suite donne 108 tests réussis et deux ignorés. Reçu
`studio-reference-models-migration-20261106-004.log.receipt.json`, SHA-256
`1bfe99abfda74ad2d2a9d8b8e1c1175016aa438e439147d8b58a6aa3d6145174`.
Les paquets d'origine du 019 donnent 21 placements actifs et 10 empreintes
distinctes sur `Tidewater island` (`e620dd19-ead9-5913-a6d6-0f93109148c0`).
Quatre épinglent les octets des GLB du catalogue de scène. Six épinglent
des descripteurs du catalogue `data/buildings/catalog.json`, dont la lecture
relève du lecteur commun `forge_assets::source_surface_package`.
Inventaire `G7-D5-legacy-019-tidewater-model-pins-20261106-001.json`, SHA-256
`076196ac461bc58f2dc5f944aeeb790e1465cd72231129a1faf01802004c77bd`.
Dans un Sandbox avec GPU, le service d'installation a repris les paquets
exacts du 019. Le candidat réutilise le binaire Studio 021 et ajoute le
paquet de référence aux paquets autorisés. Les deux documents auteur
conservent leurs empreintes après migration et désinstallation du 019.
Reçu `G7-D5-windows-studio-021-real-019-migration-20261106-001.json`, SHA-256
`fccc43b6647ff9d5e015d6a25501db9c34a1af3aa6c2b843a3e921cc79e80e11`.
La sélection de `Tidewater island` ouvre le document et sa hiérarchie, mais
la vue 3D reste vide. Le journal signale `The module omitted its authored
vegetation companions`. Le module géré historique n'a pas de pin d'origine
`.orpmod`. Le service de ressources répond sans description dans ce cas,
alors que la carte déclare une source végétale importée. Les dix blocs de
65 383 instances sont présents aux empreintes attendues dans les données
auteur du paquet 019. Il faut raccorder les instances et leurs prototypes
à la description de ressources du lecteur commun. Reçu et capture associés
`G7-D5-windows-studio-021-real-019-open-20261106-001.json`, SHA-256
`e177ef5dd68b8f1ca60639bd1ffd3b502283a6d6e1f56207d9004a701da`.
Le Studio peut désormais décrire les ressources végétales d'une carte
gérée sans origine `.orpmod` depuis le paquet de référence installé.
Le lecteur commun reconstruit l'index depuis le profil historique,
vérifie que son relief correspond à la carte et contrôle chaque ressource
contre les empreintes du profil et du paquet. Le test avec les paquets
originaux du 019 a transmis 84 ressources (46 603 250 octets, index
compris) et décodé les six groupes de géométrie. Reçu
`G7-D5-real-019-legacy-plant-acquisition-20261106-001.json`, SHA-256
`2cc28fbb2e3e5fa73cdef547efece9dc318b61da6bd8e8f87b4b3b7a3b4d2809`.
Les 24 contrôles ciblés passent, dont le refus d'utiliser la voie
historique pour un module qui déclare déjà une origine.
Le Studio 022 et son CLI ont été reconstruits sur les mêmes sources
stables. L'essai réemploie le paquet d'application 021 avec le binaire
Studio remplacé et le paquet `reference-assets` autorisé. Dans le Sandbox
avec GPU, les documents historiques conservent leurs empreintes après
migration et désinstallation du 019. `Tidewater island` ouvre sa
hiérarchie. À 188 s, la vue attend toujours « Chargement des ressources
du module… » et aucune première image 3D n'a été observée. Les relevés
de processus bornent l'observation, sans établir la cause.
Reçu `legacy-019-studio-vegetation-20261106-022/reprise/G7-D5-windows-studio-022-real-019-viewport-20261106-001.json`,
SHA-256 `77ca83264425209aaecb5ff515145c2317a262c036782cdec1cd5c61dfadfa2c`.
Le Sandbox est arrêté. Le paquet temporaire 022 de 342 691 022 octets
a été retiré après conservation du reçu. Le chargement WebView, le
rendu, le réexport complet et le retrait du Hub restent à qualifier.
Le diagnostic du binaire 022 dans un Sandbox hors ligne a ouvert le
Studio, mais WebView2 n'a pas exposé le port de diagnostic local attendu.
La cause exacte de l'attente de 188 s reste donc indéterminée. Le Studio
transporte désormais les blocs de ressources via l'IPC binaire Tauri,
comme les modèles GLB. Il conserve les contrôles de tête, de paquet,
d'ordre et d'empreinte. Une réponse attendue plus de 30 s produit une
erreur identifiant la description ou le bloc et son offset. La lecture
JSON reste disponible pour les consommateurs existants de la commande.
Les 22 tests natifs ciblés et les compilations WebAssembly et Studio
Windows passent. Reçus `studio-module-resource-binary-native-20261106-002.log.receipt.json`,
`studio-module-resource-binary-wasm-20261106-001.log.receipt.json` et
`studio-module-resource-binary-desktop-20261106-001.log.receipt.json`.
Le Studio 024 intègre ce transport. Lancé directement dans un Sandbox Windows
hors ligne avec vGPU et les octets vérifiés du 019, il a affiché la hiérarchie
de Tidewater (32 entrées). À 206 s, la vue affichait encore « Chargement des
ressources du module… », sans première image ni erreur visible. Les deux
empreintes des documents auteur ont été vérifiées après l'ouverture. Reçu
`legacy-019-studio-binary-ipc-20261106-024/G7-D5-windows-studio-024-real-019-viewport-20261106-001.json`,
SHA-256 `0c570e5532e3d1059d2a625aeb477437bc3156e8fc8c14937cab84a4f625f697`.
Le Sandbox a été arrêté et la copie d'essai de 317 819 376 octets retirée.
Une acquisition terminée en erreur réveille désormais aussi la vue en attente
afin qu'elle affiche l'erreur et masque le chargement. Cette correction passe
la compilation WebAssembly (`studio-module-resource-failure-viewport-wasm-20261106-001.log.receipt.json`).
Le Studio 025 intègre cette remontée. Dans un Sandbox avec vGPU, le même
document affiche encore « Chargement des ressources du module… » à 91 s,
sans première image ni erreur de la vue. Une relance du même binaire avec
le journal WebView2 demandé n'a produit ni fichier de journal ni argument
de journalisation observé sur ses processus. Les documents auteur ont
conservé leurs empreintes. Reçu
`legacy-019-studio-loading-error-20261106-025/G7-D5-windows-studio-025-real-019-viewport-20261106-001.json`,
SHA-256 `4e289a5d001ff30150ebf7a2a777e4b3ab7c86765bfcba494a0a892647305f2b`.
Le Sandbox a été arrêté et la copie d'essai de 317 801 395 octets retirée.
La vue affiche désormais des étapes distinctes pour l'inventaire, le transfert,
la préparation et la remise des ressources à la projection. Compilation WebAssembly
vérifiée (`studio-module-resource-milestones-wasm-20261106-001.log.receipt.json`).
Ces étapes restent à observer dans le Studio pour situer l'arrêt du 019.
Les six empreintes historiques du catalogue `data/buildings` sont maintenant
raccordées au paquet de surfaces commun. Le service natif lie ce paquet à
une carte historique qui référence au moins une de ses identités exactes,
contrôle sa fermeture contre l'inventaire installé et conserve la licence
du paquet. Le test avec le workspace et les ressources originaux du 019
décode 906 fichiers (102 543 563 octets), les six groupes de végétation
et les six modèles épinglés par les placements courants. Reçu
`studio-legacy-019-surfaces-native-20261106-001.log.receipt.json`,
SHA-256 `9ec078ea845f017bf48dc1812cc1e08835328f7fc035e08d7187d779d5681227`.
La compilation WebAssembly passe (`studio-legacy-019-surfaces-wasm-20261106-001.log.receipt.json`).
Le chemin de projection utilise le même hôte de surfaces que les modules
reçus. La preuve de rendu et la capture de ces ressources dans le réexport
autonome restent à réaliser.
Le Studio 026, reconstruit sur entrées stables, ouvre les 32 entrées de
Tidewater avec les paquets vérifiés du 019. Le chargement se termine par
« Source objects require their admitted live environment and surface host »
dans la vue et le journal. Le moteur refuse d'installer les surfaces avant
leur hôte d'éclairage. Les deux documents auteur conservent leurs empreintes.
Reçu `legacy-019-studio-surfaces-20261106-026/G7-D5-windows-studio-026-real-019-viewport-20261106-001.json`,
SHA-256 `3bb194722ff162a2a5e7963b2b4dcc15c576e0e1a7ac4e760e92e0844359593a`.
Le Sandbox est arrêté. La copie vérifiée sur E: reste utilisée pour le contrôle
de l'environnement historique, construit à partir de `data/scene` et
`data/clouds` et transmis au décodeur commun.
Le contrôle natif avec `scene-host-tests` passe sur les fichiers originaux du
019. Il lit 1 173 fichiers (105 252 515 octets) et exécute le décodeur du moteur
sur l'environnement, dont les 120 étapes des nuages. Reçu
`studio-legacy-019-environment-native-20261106-001.log.receipt.json`, SHA-256
`fd9209b397ac7a7694f1d832c6a3b4caa501750701c1fd3eafe4bda0ba934545`.
Le Studio 027 est reconstruit sur entrées stables. Dans le Sandbox avec vGPU,
« Ressources du module prêtes pour la projection » apparaît au plus tard à
47 s. À 170 s, le canevas reste vide et le journal affiche toujours ce statut.
Les deux documents auteur conservent leurs empreintes. Reçu
`legacy-019-studio-environment-20261106-027/G7-D5-windows-studio-027-real-019-viewport-20261107-001.json`,
SHA-256 `3a36dd76bf699285229addc9370603ae27a858d786107e7d9c034809473a92f6`.
Le Sandbox est arrêté. La copie vérifiée reste en usage pour le diagnostic
du démarrage de la projection avec le même frontend. Le réexport autonome
reste à qualifier séparément.
Le même frontend 027 a été revérifié puis embarqué dans un hôte de diagnostic
avec DevTools. WebGPU sélectionne `BrowserWebGpu`. La première erreur moteur
est une validation ECS Bevy 0.19.1, `Resource does not exist`. Les noms du
système et du paramètre sont absents de ce build. Les erreurs `unreachable`
et `RefCell already borrowed` suivent ce premier arrêt. Reçu
`legacy-019-studio-environment-20261106-027/G7-D5-windows-studio-027-webgpu-diagnostic-20261107-001.json`,
SHA-256 `c304b163a41a1f3e3cfdadde3a0f01f33e17ecec542213651c9c076706f85804`.
L'invité de diagnostic a été arrêté. Les sections exécutables du WASM avec
symboles correspondent octet pour octet à celles du 027. La pile situe
la validation dans le cycle ECS de `Main::run_main`, à l'appel des étapes
récurrentes. Elle ne donne pas le nom du système qui échoue avant exécution.
Carte d'adresses `legacy-019-studio-environment-20261106-027/webgpu-panic-symbol-map.txt`,
SHA-256 `c9c37b5cbb29762160f4d1c7c0e419252a67764ca49c025dc5ba65e02609b6ac`.
Les 307 737 672 octets temporaires de symbolisation ont été retirés.
Le Studio 028 installe le plugin moteur des matériaux source dans la vue
terrain échantillonnée et enregistre les systèmes GPU des effets après la
création de la vue de rendu. Les builds WASM et Windows passent sur entrées
stables. Dans le Sandbox avec vGPU, les 32 entrées sont visibles et les
ressources annoncées prêtes, mais la première image manque encore à 182 s.
Les empreintes des deux documents auteur correspondent aux originaux.
Reçu `legacy-019-studio-source-graph-20261107-028/G7-D5-windows-studio-028-real-019-viewport-20261107-001.json`,
SHA-256 `a8f7b60f49fd12c7a63168c6f68a43eeee8f6be676e6d0219270e360d9941eb6`.
Le Studio 029 avec `web,bevy/debug` identifie la première erreur à 84 s
dans le Sandbox vGPU. Le système moteur `vegetation_prototypes::wind::prepare_wind_cache`
échoue avant exécution sur `Res<VegetationPresentation>` absent. Le
chargeur des prototypes était déjà installé pour permettre une sélection
future dans le Studio, mais le profil, le champ d'interaction et les
matériaux associés n'étaient installés qu'en présence de modèles déjà
sélectionnés. Reçu `legacy-019-studio-bevy-diagnostic-20261107-029/G7-D5-windows-studio-029-named-ecs-20261107-001.json`,
SHA-256 `deb60e592f7e6eed102c651018368590ce9d7a9d2738dd0903c94b1c5334ac68`.
Les deux documents auteur gardent leurs empreintes. Le Sandbox est arrêté.
Les trois fichiers temporaires du frontend de diagnostic (176 361 646 octets)
ont été retirés après conservation de la preuve. Le candidat 030 fournit au
chargeur le profil de végétation déjà enregistré dans la carte importée,
via l'initialisation commune du moteur. Le Studio 030 est reconstruit sur
entrées stables. Dans le Sandbox avec vGPU, Tidewater affiche le terrain,
la végétation, les bâtiments et les ombres avant le relevé à 104 s. Le
bouton « Rapprocher » déplace la caméra. Les deux documents auteur gardent
leurs empreintes. Reçu
`legacy-019-studio-authored-wind-20261107-030/G7-D5-windows-studio-030-real-019-viewport-20261107-001.json`,
SHA-256 `38a385ffee891f5a6a2e6684e23e980539b8114b58cf39d9a7f0302452308d7e`.
Ce relevé couvre l'ouverture visuelle et la caméra. Le Sandbox est arrêté.
L'export privé des cartes gérées sans origine `.orpmod` utilise désormais
le même lecteur des ressources historiques que le Studio. Il prépare les
liaisons par carte, vérifie chaque octet avant assemblage et conserve
l'inventaire de provenance et les notices. Les GLB historiques de collision
triangulée suivent le catalogue gouverné du jeu exporté avec les mêmes
identités et empreintes. Le fichier original `PROVENANCE.md` du paquet 019
(350 octets, SHA-256 `6e510e0ff2d16cb130ef086af67f10a17c6a4fbabdb76bfeed3c607445699b79`)
a été ajouté à la fixture active sans extraire une seconde copie du paquet.
La bibliothèque native de livraison compile avec ce raccordement. Reçu
`studio-029-private-reference-delivery-native.log.receipt.json`,
SHA-256 `87132480462b32d4668aa571b6acd08138bb8a6b5536ae0242da092968ccfa13`.
L'hôte de diagnostic 029 compile également avec l'export raccordé. Dans un
Sandbox séparé, la carte possède déjà ses règles de jeu. Le bouton
« Créer le fichier .orpmod » en mode privé refuse deux empreintes pour
l'identifiant `1aedd6b3-b7d3-5430-a3ca-6cd4808e1eaa`. Le journal auteur
référence six anciennes révisions de quatre GLB. Les six fichiers
(31 272 396 octets) ont été retrouvés et vérifiés par SHA-256 dans deux
archives de qualification, sans extraire de copie. Aucun paquet n'a été
produit et les empreintes des documents auteur sont identiques. Reçu
`legacy-019-studio-bevy-diagnostic-20261107-029/G7-D5-windows-studio-029-legacy-private-export-20261107-001.json`,
SHA-256 `166c53c99a2acd48dcff1648a63d8bb4f5768176d2aeb28c3b4b22e547b7a4d4`.
L'API 14 du paquet privé ajoute un inventaire de modèles d'historique
par couple identifiant et SHA-256, avec fichier GLB et licence épinglés.
Le catalogue de jeu continue à exiger la révision utilisée par la carte
active. Le constructeur sait transporter les anciens GLB admis. Le Studio
sait les lire par épingle depuis le paquet d'origine du module ouvert.
Les 58 tests du contrat passent, dont la distinction entre révision active
et historique. Reçu `studio-031-historical-project-contract.log.receipt.json`,
SHA-256 `7e9abf643f4d9aa25bb20d5fb636b866e2291943a092ad4b7193d61124650c9c`.
Les 31 tests de transport exécutés passent, avec deux fixtures externes
ignorées. Reçu `studio-031-historical-delivery-contract-r3.log.receipt.json`,
SHA-256 `f1678373633d5a24ea957cb79cd64f845d1d0db78cfda141c7d5dca22875755b`.
Le lecteur Studio compile. Reçu `studio-031-historical-studio-reader-check-r2.log.receipt.json`,
SHA-256 `02cc41b4934f2c323e463f0f60c6b7f36672f130e873f86bf23a4c9e8834237f`.
L'export Studio distingue désormais les modèles de la carte active des
révisions présentes dans le journal auteur. La qualification privée du 019
utilise les six GLB (31 272 396 octets) extraits par empreinte des deux
archives déjà vérifiées. Le manifeste du complément est épinglé au
SHA-256 `24a6fd25663be262916dedc2a0e2e38041b357922716662c7a5f23ca3fe86192`
et lié au manifeste `reference-assets` du 019. Le contrôle de compilation
du Studio passe. Reçu `studio-032-historical-export-studio-check.log.receipt.json`,
SHA-256 `3ba5a5b91b9ce7a586c27bdb8e15070a9bd1d8f2aac30e5a3a31d3cc60123bea`.
Dans Windows Sandbox avec GPU, le Studio natif 032 et l'interface 030
ont exporté la carte du 019 en `.orpmod` privé. L'inspection du paquet
confirme l'API 14, le gameplay d'origine, les quatre modèles actifs et
les six révisions historiques par identifiant et SHA-256. Les deux
documents auteur conservent leurs empreintes. Le paquet de 189 146 663
octets reste disponible sur F: pour la qualification du jeu. Reçu
`legacy-019-model-history-20261107-032/G7-D5-windows-studio-032-legacy-private-export-20261107-001.json`,
SHA-256 `64c18aa9e6d8946119c0f2939c36ad387b8947567f1d5ddf83cf05d16b697bae`.
Le chargeur commun `forge_delivery module-check` a admis ce paquet et ses
onze sources de zone. Les empreintes de la tête et de l'archive correspondent
au reçu Studio. Reçu `editor-launcher-windows-candidate-20261107-033/G7-D5-windows-tidewater-032-runtime-admission-033.json`,
SHA-256 `feefa188cfc5e6e00f2244b10c2f3fe512b9b8736c90097d258d5715e36bfa6f`.
Le runtime Windows 033 construit sur des entrées stables a lancé ce paquet
sur la Radeon RX 7800 XT. La zone, les bâtiments et la végétation ont été
projetés. Le contrôle de scène échoue dans le HLOD statique sur une primitive
transparente et la disponibilité du gameplay alterne pendant l'observation.
Reçu `editor-launcher-windows-candidate-20261107-033/G7-D5-windows-tidewater-032-runtime-033.json`,
SHA-256 `8c22b6096052e7b917e8ae3a523d39049fc0bbe3c585fcb400adb80075babb77`.
Le moteur conserve désormais toutes les primitives d'un objet transparent
dans son rendu détaillé et autorise l'agrégation des autres objets éligibles.
Le test de régression passe avec un objet à primitives opaques et transparentes
à côté d'un objet agrégeable. Reçu `engine-034-static-hlod-retain-blended.log.receipt.json`,
SHA-256 `08a503c6438de76bfbf3f39073042bc8b4b4a9a992efc290fe9793a5cff256fc`.
Le runtime et le launcher Windows 034 ont été construits sur le même
inventaire. Le contrôle des notices retrouve la fermeture Cargo du runtime
et les 298 dépendances du launcher. Reçu
`editor-launcher-windows-candidate-20261107-034/R7-08-windows-notice-preflight-034.json`,
SHA-256 `89a63283d6b8a7bc85ce8d84fc0268295b81a33a0f745669b2ffdbb07dd5db60`.
Le runtime 034 capture le village de Tidewater et couvre ses 407 primitives
statiques sans erreur. Après la trame 200, seules 99 trames sur 400 présentent
une projection prête, alors que le joueur reste au même endroit. L'attente
de la simulation reste intermittente. Reçu
`editor-launcher-windows-candidate-20261107-034/G7-D5-windows-tidewater-032-runtime-034.json`,
SHA-256 `c84d0ef983b00939ad2ea4b0bd54f8a53db95eac2ac4a1bf81b72a18e9284353`.
Un objet mobile conserve maintenant la réservation du terrain quand sa
projection physique est temporairement fixe. Cela évite de retirer les
collisions nécessaires à sa propre réactivation. Les deux tests ciblés
passent, dont la conservation du sol d'un objet distant pendant le blocage
et après la reprise du gameplay. Reçu
`engine-035-streaming-reserved-ground-regression.log.receipt.json`,
SHA-256 `15ca7e92d7ef934d0b51e5df3d7540c8546e99551d5530634a88b27652103da1`.
Le runtime Windows 035 a joué le paquet du Studio 032, sauvegardé puis
repris la partie par son empreinte. Les deux sorties et les deux projections
finales réussissent. Le démarrage repris retrouve l'état sauvegardé exact,
et la sauvegarde conserve le SHA-256
`22d90f78e67697520bc2c4fd1630ad2619c5c389fa760166ab0a962c51f60a5e`.
Les traces conservent neuf trames indisponibles après la première projection
prête dans chaque session. La performance et le parcours depuis le launcher
ne sont pas qualifiés par cet essai natif. Reçu
`editor-launcher-windows-candidate-20261107-035/G7-D5-windows-tidewater-032-native-save-resume-035.json`,
SHA-256 `b577adf3c8816a3b4bf3ce6f1905f6968ae77a4ddd0808943fd001991a0473cd`.
Le kit Windows 035 rassemble 473 fichiers et 141 736 607 octets. Son reçu
`editor-launcher-windows-candidate-20261107-035/game-kit-assembly-receipt.json`
a le SHA-256 `c14164577ce25d6fc92abdbc81aeb23b9fa2e8ea2e9501cc80118fb37ca4af54`.
Le Studio 035 est construit avec ce kit dans le checkout commun, SHA-256
`8469bf3bded03a1a2355dcba6341a19d13ccb3b74a2d01ae650f51cd917c6873`.
Dans le Sandbox `d28da61f-aa68-415e-9ae9-da0314a99891`, son interface a
configuré puis sauvegardé le projet Tidewater historique (carte d'origine,
qualité Élevée) et créé le jeu privé avec son installateur. Les quatre
fichiers exportés totalisent 350 782 604 octets, conservés une seule fois
sur F: dans `editor-launcher-windows-candidate-20261107-035/tidewater-game-export-035`.
Le reçu `studio-tidewater-export-035.json` conserve leurs SHA-256 et celui
du workspace sauvegardé. L'installation dans le dossier utilisateur de
l'invité a terminé. Le launcher a lancé la carte d'origine, F5 a sauvegardé
la partie, puis le bouton « Reprendre » l'a rouverte après fermeture propre.
Les deux rapports gameplay et runtime réussissent, avec 21 objets manuels,
399 primitives enregistrées et zéro pipeline en échec ou en attente en fin
de session. La sauvegarde conserve le SHA-256
`56715da329f5d2437636eb603365cb2a0148734e78c69771a007ad28d5c14f46`.
Reçu `G7-D5-windows-studio-tidewater-launcher-save-resume-035.json`, SHA-256
`f6846b282993de3cd96f9f60d09fbe770cfb0d074f040d7cdc24048c2378db6b`.
Ce contrôle valide le parcours produit privé dans cet invité, qui contient
aussi le Studio et les montages source en lecture seule. Il ne qualifie
ni un invité joueur seul, ni les performances, ni les visuels de présentation.
Le retrait du Hub reste à qualifier.
Le lot `presentation-20261107-036` raccorde les trois visuels du projet à
la collection d'imports gouvernée. Le paquet conserve les images exactes,
leur provenance, leurs licences et les sources/parents nécessaires aux
retouches ; il reste privé. Le launcher affiche ces images après admission
du paquet et applique l'icône à sa fenêtre. Sept tests ciblés et le check
WASM passent. Reçu `implementation-checks-036.json`, SHA-256
`440cc93a8b9a440a55e7040291c998bdd7f661d5e77e1d37fe3d66b911a97804`.
Le launcher, le runtime, l'outil de livraison et le frontend Studio 036 sont
construits sur ces mêmes sources stables. Le kit privé 036 est assemblé
dans `E:/OpenRPG_Forge/.build/tmp/gate7-windows-game-kit-presentation-036`
(473 fichiers, 141 908 127 octets). Les quatre graphes launcher affectés
ont été recalculés hors ligne : leurs ensembles de dépendances restent
identiques au bundle de notices 015 réutilisé sans copie. Les nouveaux reçus
`notice-graph-036.json` et `notice-candidate-036.json` conservent la filiation
des preuves historiques ; ils ne qualifient pas une redistribution publique.
Manifeste candidat `presentation-20261107-036/source-inputs.json`, SHA-256
`fed311ec736c65128c0a64d0c6b7817006e58db449798a089c815239ef659618`.
Le snapshot diagnostic précoce `source-inputs-036.json`, remplacé après les
corrections, a été retiré : 2 030 989 octets. Les manifestes canoniques et
leurs reçus sont conservés. Le Studio 036 est construit avec ce kit,
SHA-256 `bd2d276711b27cd02b4e2afc5a978d0e6a6a808113b6de7daf821937ec83aae0`.
Son interface a sélectionné les trois visuels, examiné/appliqué la
modification puis sauvegardé et réexporté le projet Tidewater en 1.0.1.
Le workspace conserve les mêmes références de carte ; seuls icône, logo
et fond ont changé. L'installateur a mis à jour le jeu 1.0.0 existant.
Le launcher affiche les trois images et son icône native. Les sources de
retouche, le parent de la variante et la preuve de licence sont présents
avec leurs octets exacts dans le paquet installé. « Reprendre » lance la
sauvegarde conservée ; les rapports gameplay et projection réussissent
et le runtime se ferme proprement. Le SHA-256 de la sauvegarde reste
`56715da329f5d2437636eb603365cb2a0148734e78c69771a007ad28d5c14f46`.
Reçu `G7-D4-windows-presentation-update-resume-036.json`, SHA-256
`ee8a578f147c819b51de33dce109d9182c56a412bbd5d7e11de1c5c41ca488e0`.
Les quatre fichiers exportés sont conservés une seule fois sur F: dans
`presentation-20261107-036/tidewater-game-export-036`, 351 130 232 octets.
La copie de travail de l'invité a été retirée après vérification SHA-256
et contrôle des processus : `export-copy-retired-036.json`.
Ce contrôle qualifie la présentation et la reprise après mise à jour du
jeu privé. La préparation du binaire Studio dans cet invité n'est pas une
qualification de son installateur. L'invité joueur seul, les performances,
les licences de redistribution, Linux et le retrait du Hub restent ouverts.
Le même Sandbox reste disponible ; le Studio 036 (PID 7276) est ouvert
sur le projet sauvegardé, le jeu et l'installateur sont fermés.
L'installateur autonome du Studio 036 est disponible dans la même campagne.
`OpenRPG-Forge-Studio-036-Setup.exe` fait 444 344 632 octets et porte le
SHA-256 `7385eeb067fc584214869d771166ac091bd6f9199f0e3205dfe1e82332ffae3e`.
La recette `studio-only` reprend les binaires 036 et leur kit vérifié. Les
reçus, l'index et le manifeste sont dans `studio-installer-036/`.
`studio-notice-scope-audit-036.json` retrouve 336 dépendances natives et 596
Web avec les notices existantes. Trois cas de notice Web restent ouverts.
La préparation porte le verdict `BUILD_ONLY`. La migration depuis une
installation historique et la qualification en environnement propre restent
à vérifier sur le candidat final. L'assemblage temporaire sur E: a été
retiré après contrôle du Setup, des empreintes et des processus. Le reçu
`studio-installer-036/staging-retirement.json` totalise 356 130 420 octets
retirés. Le kit reste disponible pour le Studio de qualification.

Pour le retrait du Hub, reprendre la matrice D1 avec les preuves 035/036/039.
Le launcher joue les contenus déclarés et l'installateur du jeu utilise
le service de livraison commun. L'import `.orpmod` est disponible dans
« Projet et jeu ». Le Studio 039 a ajouté Tidewater à l'espace où Le Grand
Plateau était déjà sauvegardé. Les deux modules réapparaissent après relance,
avec les origines exactes et le premier projet inchangé. Le reçu Windows
`module-import-20261107-038/G7-D3-windows-studio-module-import-039.json`
porte le SHA-256 `22e15c3c4726459894ed46ba1e38bf7886a252b0802cea4450c8584e808b4147`.
L'espace auteur de cet essai reste sous
`E:/OpenRPG_Forge/.build/qualification-data/gate7/module-import-20261107-038/workspace`
pour le prochain export de qualification. Les recettes
historiques `build_delivery.py`, `build_classic_installer.py` et
`repair_installed_launcher.py` portent encore l'ancien Hub. Les suites
Linux `hub`/`shells` et les scripts Hivemind de mise à jour d'une ancienne
installation restent à rapprocher des chemins cibles avant retrait.
Le Setup Studio appelle `recover-legacy-modules` après activation et avant
l'inscription des raccourcis, si l'ancien produit enregistré et le Studio
utilisent le même dossier utilisateur. Les modules actifs et de retour
arrière sont vérifiés puis conservés sous `recovered-modules`. La relance
réutilise les archives valides. Les contrôles Windows et NSIS ciblés passent
sur fixtures. Reçu `G7-D5-legacy-module-recovery-040.json`, SHA-256
`5f9938216a23f4093e1c83eb6305319cab2e753abae4e612323f645dae54cb41`.
Le launcher peut maintenant vérifier les sauvegardes historiques avec les
modules récupérés après le retrait. Le CLI `import-recovered-save` accepte
aussi un profil utilisateur conservé fourni explicitement. Le contrôle
Windows reprend les octets réels de la sauvegarde des Trois Cairns 019
après retrait d'une installation historique reconstruite. Le module et
la sauvegarde source conservent leurs empreintes. Reçu
`G7-D5-windows-recovered-cairns-save-041.json`, SHA-256
`7e6c6dfed57ce51de8a8db3e08e22694c428e4033c0a29b24a3d99fc4261b1a5`.
Le launcher installé a ensuite trouvé, importé et repris cette sauvegarde
dans un Sandbox Windows joueur seul, sans Studio ni SDK. La quête terminée,
la marque de la gardienne et les valeurs 100/90 sont revenues en jeu. Les
deux empreintes restent identiques après fermeture. Reçu
`windows-recovered-save-20261107-041/G7-D5-windows-clean-player-recovered-save-041.json`,
SHA-256 `c5427f3e547c55bfb92e2c16313d0bd6040ba3e62939d04abb25cdb62b4a3b4b`.
L'installation du jeu passe par le service natif et la tête historique
est reconstruite avec les vrais octets 019.

La migration réelle entre les Setup NSIS 019 et Studio 042 a ensuite
réussi en Sandbox Windows hors ligne. Le CRT Microsoft signé a été
installé dans l'invité pour le 019. Le Setup Studio reprend le profil et
la révision originale de `reference-assets`, puis récupère le module
Cairns et l'ancien module de référence avec leurs droits privés.
Après désinstallation du 019, la tête, l'enregistrement et le raccourci
du Hub sont absents. Les 90 fichiers auteur de la fixture 036, la
sauvegarde originale et les deux archives récupérées sont inchangés.
Le binaire Studio 042 démarre depuis le lanceur installé et son empreinte
correspond au build. La réouverture visuelle de la carte 042 reste à couvrir.
Reçu
`windows-recovered-save-20261107-041/studio-migration/G7-D5-windows-nsis-019-studio-042-migration.json`,
SHA-256 `0b8ee629361c69a67db4a167209a5a3e04d511c7affcbfa2c0cff5f6cd882c5f`.
Le Sandbox est arrêté. Le retrait de la cible et des recettes est consigné
dans le lot 048 ci-dessous.

Le 043 exige le mode `legacy` pour reproduire l'ancienne livraison
avec `build_delivery.py` ou `build_classic_installer.py`. NSIS exige
`LEGACY_WITH_HUB` pour ce profil et `STUDIO_ONLY` pour le Studio. Les
20 contrôles ciblés passent, dont la compilation NSIS des deux profils
avec des données inertes. Reçu `G7-D5-windows-explicit-delivery-profiles-043.json`,
SHA-256 `ffbc4c776b47fad5de714d1230a38b3c397d327c3a39160973bbd37391ebeee4`.
La reproduction explicite et les anciens outils de vérification restent
des consommateurs identifiés. Les outils Hivemind versionnés v975,
v978 et v1020 appellent ou vérifient encore la livraison historique.
Le reçu v1020 est historique. Sa racine enregistrée `F:/instal test/Installation`
ne contient plus de tête au relevé du 7 novembre.

Le 044 raccorde le paquet optionnel `asset-library` au plan de migration
Studio qui transférait déjà `reference-assets`. Les deux révisions, les
descripteurs et les droits sont conservés. Une relance réutilise le contenu
installé. Le test natif Windows relit la bibliothèque après désinstallation
de la fixture historique. Le contrôle de réouverture du catalogue de
référence et la garde de désinstallation du Studio passent également.
Reçu `G7-D5-studio-legacy-asset-library-migration-044.json`, SHA-256
`e10d25b70eff915a048f8686b2d8bdfbca98af940c2d353f2cebc445ea83e335`.
Le 045 retire le Studio et laisse les révisions `asset-library` en place.
Le reçu local relie le profil auteur à la révision à reprendre. La
réinstallation réutilise les octets déjà présents et refuse leur
réactivation s'ils ont changé. Onze tests natifs Windows passent, dont
la reprise de désinstallation après verrouillage du binaire et le refus
des révisions étrangères. Reçu
`G7-D5-studio-retained-library-lifecycle-045.json`, SHA-256
`19366b169f573182d58dc042026155b6c036324231fc3f6c5cce92d3abe2c112`.
Le 046 exerce le Setup 019 d'origine et un Setup NSIS de qualification
qui assemble le paquet Studio 042 inchangé avec le CLI de livraison
recompilé. Dans Windows Sandbox sans SDK, le Setup Studio a transféré
les deux paquets d'assets. Après retrait du 019 puis désinstallation et
réinstallation du Studio, `asset-library` conserve son emplacement, ses
droits et les empreintes de ses trois fichiers. Le profil et le fichier
auteur témoin sont conservés. Le paquet `reference-assets` est absent du
Studio réinstallé. La reprise de ce paquet reste nécessaire pour les
projets historiques qui en dépendent.
Reçu
`windows-library-lifecycle-20261107-046/G7-D5-windows-nsis-019-046-library-lifecycle.json`,
SHA-256 `0c36c82f7ff109f45d5d8844ff324f219e418d1929334bd0b366ddd0ccac298c`.
Le Sandbox est arrêté. Les copies et intermédiaires retirés représentent
2 436 808 237 octets sur E: et 4 066 682 octets de manifestes identiques
sur F:. Les Setup originaux 019 et Studio 042 sont disponibles sur E:.

Le 047 garde aussi `reference-assets` sur place lors du retrait du
Studio. Le reçu v2 raccorde les deux paquets au même profil. Le lecteur
accepte les reçus v1 pour `asset-library`. Les trois tests natifs Windows
incluent la relecture du catalogue par le service de lancement Studio.
Dans un Sandbox propre hors ligne, le Setup de qualification réutilise
le paquet Studio 042 avec le CLI 047. Le CLI installe le paquet original
`reference-assets` 019 (8 138 entrées) et la bibliothèque témoin 046.
Le désinstalleur retire l'application, puis le Setup réinstalle le Studio
avec le même profil et les deux révisions inchangées. Le vérificateur
relit les paquets installés et le catalogue. Ce scénario part du Studio
avec les deux paquets installés. Le Setup historique est couvert par le
046. Reçu
`windows-library-lifecycle-20261107-047/G7-D5-windows-nsis-047-retained-studio-assets.json`,
SHA-256 `91775e26283551ac0216c5499d2fd240cf6d5ad6dc098043d2fa86254de58c61`.
Le Sandbox est arrêté. Les temporaires retirés représentent 787 965 792
octets sur E: et les manifestes identiques 4 066 682 octets sur F:.

Le 048 retire `apps/forge_hub`, le profil NSIS groupé, les anciennes
recettes d'assemblage Windows et les commandes Linux et Hivemind qui
recompilaient le Hub. Le Setup Studio conserve la récupération du 019.
Les vérificateurs et outils de maintenance des livraisons déjà
installées continuent de lire leurs artefacts historiques épinglés.
L'archive cataloguée `gate7-hub-retirement-20261107-048.zip` contient les
sources retirées, les modifications locales et les recettes antérieures,
avec un manifeste SHA-256 et relecture intégrale. Elle occupe 6 981 889
octets sur F:. Le retrait des 22 sources archivées et des quatre schémas
générés libère 8 118 618 octets sur E:.
Les contrôles de livraison exécutent 73 tests, dont un ignoré. Ils
couvrent la compilation NSIS avec un payload inerte, le refus des
anciennes recettes, le paquet Studio et le kit du jeu autonome.
Reçu `G7-D5-hub-source-retirement-048.json`, SHA-256
`31d09890ab6876214c14e41301bd32e7893c6eb96a7e85c67a26e8f108bd39df`.
Le candidat Windows réunifié 049 assemble le Studio, le CLI de livraison,
le launcher autonome et le moteur de modules depuis le même inventaire
de sources. L'installateur NSIS privé correspond à ces binaires.
Reçu `unified-windows-20261107-049/G7-D6-unified-windows-candidate-049.json`,
SHA-256 `873b6d9e44f676dd0dd29a6032d07697a7ba24ab64cce981d9aff3291c628141`.
L'installation et le démarrage du Studio 049 sont constatés dans un
Windows Sandbox hors ligne sans SDK. Le CLI et le Studio installés
correspondent aux empreintes du build. Le premier script n'a pas rendu
son reçu final. Une relecture dans le même invité a constaté la fenêtre
du Studio et ses processus WebView2 sans relancer l'installation.
L'invité est arrêté. Reçu
`unified-windows-20261107-049/clean-windows/G7-D6-clean-windows-studio-049.json`,
SHA-256 `d123f6a059253e4cdf3f249f4b597dc455e93de0959c56702586bcf956a3b393`.
Le Setup conservé sur E: occupe 444 465 159 octets. Le kit de jeu reste
disponible pour la qualification avec 141 964 447 octets de contenu.
Le bilan `unified-windows-20261107-049/staging-retirement.json` consigne
356 484 567 octets de temporaires retirés sur E: et 20 297 030 octets
d'inventaires identiques retirés sur F:.

Dans un nouveau Sandbox hors ligne, le Studio 049 installé a enregistré
les configurations du Grand Plateau et de Tidewater dans une copie du
workspace 039, dont l'original était monté en lecture seule. Les deux exports privés ont des
identités de jeu et de module distinctes. La relecture intégrale de
leurs paquets confirme le même launcher et le même moteur 049.
Reçu `unified-windows-20261107-049/editor-export/G7-D6-two-studio-exports-049.json`,
SHA-256 `15df0ad9036f3a758b79eaf16f5e70b04ea469dc7901af70796fa6d50498c0e6`.

Un Sandbox joueur neuf, hors ligne et sans Studio ni SDK, a installé
les deux jeux depuis leurs `Installer.exe`. Chaque launcher a démarré
une nouvelle partie, reconnu la sauvegarde créée dans le jeu et permis
de la reprendre. Les quatre rapports constatent une projection prête et
une fermeture réussie. Les deux sauvegardes et les deux états
d'installation gardent leurs empreintes au contrôle final, dont
l'installation du Grand Plateau après le parcours Tidewater.
Reçu `unified-windows-20261107-049/player-only/G7-D6-clean-windows-two-games-049.json`,
SHA-256 `abee236f8f02a0f63d6f77ee3d4895795a40b9f9f5e062e3b918e60ceb478984`.
Un troisième Sandbox hors ligne a installé Tidewater 035, activé le 049,
restauré le 035, réappliqué le 049 puis désinstallé le jeu depuis son
launcher. Le moteur actif a repris la sauvegarde 035 après la mise à jour
et après la restauration. Les empreintes des deux sauvegardes ont été
conservées à chaque étape. Le Grand Plateau était représenté par sa
sauvegarde dans cet invité. Le contrôle final confirme le retrait de
`Jouer.exe`, de l'état d'installation et des paquets de Tidewater.
Reçu `unified-windows-20261107-049/maintenance/G7-D6-windows-game-maintenance-049.json`,
SHA-256 `3571f9cbed5272ce69f4b36edd0949f25903b1d64f8a49b72cb1373bf31bc4a3`.
Les trois invités sont arrêtés. Les deux exports occupent 540 065 135
octets sur E: dans `.build/qualification-data/gate7/unified-windows-049-exports`.
Ils restent les entrées exactes pour le rapprochement des droits et des
performances sur ce candidat. Les reçus ci-dessus couvrent le parcours
privé Windows. La qualification Linux et les droits de redistribution
restent ouverts.

Le candidat Linux privé 0837 du 3 novembre reste exploitable pour la
reprise du parcours d'installation. Le reçu de build comporte 15 étapes
réussies. Le volume Docker existant a été monté en lecture seule hors
réseau. Le paquet Studio de 385 439 325 octets et les 470 fichiers du
kit correspondent aux empreintes du build. Les quatre ELF et le ZIP
source ont aussi été vérifiés sur E:. Le checkout actuel diffère sur
355 entrées de la comparaison des sources et manifests produit. Ce
candidat garde son verdict `PASS_BUILD_ONLY` et sa révision.
`editor-launcher-linux-qualification-20261102-001/runs/candidate-20261103-0837/linux-retained-artifacts-recheck-20261107.json`,
SHA-256 `ed715678bb30bc43ff26601fa21e594446149639b6925f23fe04008f902f021a`.
Les copies host du moteur, du launcher et du CLI ont été retirées
après vérification des originaux conservés dans le kit et le dossier
Studio du volume. Retrait de 196 591 536 octets sur E:. Le Studio ELF
reste sur E: pour le préflight de bureau sans extraction du paquet. Le
ZIP permet de relire le snapshot exact. Docker a été arrêté après lecture,
avec zéro conteneur résiduel. La lecture n'a lancé ni build ni produit.
Le contrôleur `scripts/gate7/linux/run_clean_delivery.py` accepte
désormais le vrai paquet Studio privé en plus du scénario historique
avec fixture. Le mode Studio relie le plan et l'état installé aux
empreintes de l'éditeur, puis vérifie la désinstallation et le fichier
témoin. Le contrat a été vérifié localement avec un CLI simulé. L'essai
suivant a utilisé le vrai paquet 0837 et son CLI dans le conteneur SDK
existant, hors réseau, volume source en lecture seule et installation
sur tmpfs. Les cinq commandes `inspect`, `plan-file`, `apply`, `verify`
et `uninstall-studio` se sont terminées avec le code 0. L'éditeur installé
correspond au SHA-256 attendu, est exécutable, puis est retiré. Le fichier
témoin a conservé son empreinte. Le tmpfs et le conteneur ont été libérés
et Docker arrêté. Aucun nouveau paquet conservé, un reçu de 3 897 octets.
`editor-launcher-linux-qualification-20261102-001/runs/candidate-20261103-0837/linux-studio-install-sdk-0837.json`,
SHA-256 `20f80eac00623de3fe695dbd1c218a93c00f0a7c7ea0a48b832441b80283ded8`.
Le Studio n'a pas été démarré. La machine sans SDK et le parcours auteur
restent ouverts. La session hôte ordinaire a reçu un refus d'accès pour
`Get-VM` sur la VM Ubuntu existante.

Un conteneur Debian 12 distinct du SDK a ensuite installé le même paquet
0837 à l'empreinte et résolu toutes les dépendances ELF du Studio. Les
bibliothèques de runtime sont inventoriées, les compilateurs et SDK sont
absents, le volume de paquet est en lecture seule et le réseau a été
déconnecté avant l'installation. Le CLI a lancé l'éditeur sous Xvfb avec
le verrou d'installation. La fenêtre « OpenRPG Forge Studio » est mappée
et son PID appartient à l'éditeur vérifié. Le log signale un bus AT-SPI
absent dans le conteneur. Le Studio a ensuite été désinstallé en conservant
le fichier utilisateur témoin. La fenêtre seule ne qualifie ni le contenu
du WebView, ni le parcours auteur/export, ni la machine propre. L'image
Debian et le conteneur d'essai ont été retirés et Docker arrêté. Le reçu
de 14 568 octets contient le protocole et les observations.
`editor-launcher-linux-qualification-20261102-001/runs/candidate-20261103-0837/linux-studio-installed-sdk-free-container-0837.json`,
SHA-256 `2e2d45dd7256e8f7918e67ac5190b78fa19d8ff6b8432dae1f871e7d782e71d7`.

Un autre conteneur Debian 12 sans SDK a exercé le parcours joueur du
Grand Plateau. Les 470 fichiers du kit Linux 0837 ont été repris à leurs
empreintes depuis le paquet Studio et associés à la configuration et au
module 049 inchangés. Le CLI a assemblé et installé l'application privée.
Le diagnostic natif `--verify-game-launch` du launcher installé a démarré
le runtime du kit et sa fenêtre X11 sous llvmpipe/Vulkan. Après
réinstallation du même paquet, F5 a créé une sauvegarde `forge.game-save`
dont les identités de module, zone, source et jeu correspondent au reçu
Windows. L'empreinte de la sauvegarde est conservée après désinstallation.
Le premier relevé avait compté `graphics.json` comme sauvegarde. Le
contrôle final porte sur `authoring.json` et son enveloppe de jeu. La
reprise par le launcher, l'export depuis le Studio Linux et la qualification
sur OS propre restent à faire. Le conteneur et l'image d'essai ont été
retirés, Docker arrêté, et aucun nouveau paquet n'est conservé.
Reçus `linux-plateau-from-studio-package-container-0837.json` et
`linux-plateau-gameplay-save-container-0837.json` dans
`editor-launcher-linux-qualification-20261102-001/runs/candidate-20261103-0837/`.
Ils occupent 31 800 octets sur F:. SHA-256 du reçu de sauvegarde
`7d25a478358ae676f449eccd7f9f6b7f2d766e932093484fc0c971fd4680e217`.

Le binaire 049 extrait du paquet vérifié a exécuté les deux modules sur
Windows natif, Radeon RX 7800 XT/Vulkan, à 1280 × 800 avec Fifo demandé.
Au profil Moyen, sur les images 300 à 599 avec projection et simulation
prêtes, les intervalles moyens/p95 sont de 6,94/7,41 ms au Grand Plateau
et de 22,72/25,05 ms à Tidewater. Les deux scènes utilisent le même
moteur et des contenus différents. Le profilage séparé de Tidewater
retrouve 22,90/25,52 ms. Entre ses deux derniers relevés cumulés,
`render.Render` mesure 12,24 ms par appel, dont la cause reste à isoler.
L'horodatage GPU asynchrone des ombres source vaut 0,41 à 0,45 ms aux
deux observations de régime. Les intervalles CPU imbriqués ne s'additionnent
pas. Cette mesure à caméra fixe reste distincte d'une mesure en mouvement
ou d'une comparaison avec l'ancien exécutable Tidewater.
Reçu `unified-windows-20261107-049/native-profile/G7-D6-windows-native-profile-049.json`,
SHA-256 `5ca7429b8a0c9cbbe89b29b24c05b861a4f17d3ef8a10062388cf2f5520ca8f4`.
Les 334 388 752 octets de caches, journaux et binaire extrait de ce lot
ont été retirés d'E: après les essais. Les preuves de cadence restent sur F:.

Le runtime commun déduplique maintenant les maillages et matériaux actifs
par identifiant dans l'inventaire graphique. L'ordre d'admission et les
contrôles des dépendances restent identiques. Les 32 tests graphiques
existants passent dans le checkout partagé.
Reçu `unified-049-graphics-inventory-dedup.log.receipt.json`, SHA-256
`1acb5d18767410ac763ab50e249d9695af6bc5aaac1cfbb27f463ccb68fd08f6`.
Le rejeu A/B du 7 novembre a reconstruit le moteur seul. Son inventaire
de compilation ne diffère du 049 que par le fichier de l'inventaire
graphique. Le même export Tidewater, à caméra fixe en Moyen et à
1280 × 800, passe de 23,26 à 22,89 ms en moyenne et de 25,84 à 25,33 ms
au p95. La famille `graphics.inventory.other` passe de 0,113 à 0,108 ms
par appel. L'écart global observé sur un passage par binaire reste faible.
`render.Render` mesure encore 12,43 ms par appel. Les relevés asynchrones
de la bande caméra donnent 6,79 à 9,16 ms côté CPU et 4,72 à 5,56 ms côté
GPU sur le moteur modifié. Ces bandes se recouvrent. Il reste à isoler
le coût d'encodage et de soumission avant d'attribuer les 12,43 ms.
Reçu `unified-windows-20261107-049/native-profile/inventory-dedup/G7-D6-inventory-dedup-comparison.json`,
SHA-256 `fce4d734cd08035fb0c4d8ff1034ca4d85b17f058f3e4bf57783179605609357`.
Relevés CPU/GPU `render-diagnostics-readback.json`, SHA-256
`853d7624495eea937b71ec490014ead2efd07737a2b757020111e1a4c0b36c55`.
Les 541 728 368 octets temporaires sur E: ont été retirés. Les deux
inventaires avant/après sur F: étaient identiques au `source-inputs.json`
conservé à l'empreinte `e9f92a83fdc81a8da59b770c4405d13f335ff3e3c3e072ff14eed6138cd3ce46`.

Les bornes CPU du graphe utilisées par la référence Tidewater sont
maintenant enregistrées dans le moteur commun. La session native sur
le même export Tidewater au profil Moyen mesure 23,02 ms en moyenne
et 25,43 ms au p95 sur 300 images prêtes. Entre les deux derniers relevés
cumulés, `render.Render` mesure 12,52 ms par appel. Les bandes CPU
comprennent 4,25 ms jusqu'à la fin du système atmosphère, 0,13 ms
jusqu'à la fin du système nuages, 4,57 ms jusqu'à la fin du pilote caméra,
1,39 ms pour la soumission et la finalisation du graphe, puis 0,20 ms
après le graphe. La première bande comprend les systèmes qui s'exécutent
avant la fin de l'atmosphère. Le système atmosphère lui-même peut sortir
immédiatement quand sa révision est déjà encodée. La première bande et
celle de la caméra restent les priorités pour isoler le travail CPU.
Reçu `unified-windows-20261107-049/native-profile/render-bands/G7-D6-tidewater-common-render-bands.json`,
SHA-256 `a54755a02c5d5bb3421dac203211dc53df4d8f26373447a1486ec0e3466759db`.
Le profilage est activé à la demande. Les 211 641 266 octets temporaires
ont été retirés d'E:. Les deux inventaires avant/après étaient identiques
au `source-inputs.json` conservé à l'empreinte
`1f212d5f010971141878dc8df4af71833413be2e0570294691ae3f0b812e7d96`.

Le moteur commun partage désormais la matrice de projection de chaque
cascade entre les objets opaques. Chaque objet conserve sa propre pose
par couche. Les matrices inchangées restent résidentes. Le rendu conserve
les opérations modèle puis projection et la cadence des cascades.
Le nouvel essai reprend l'export, le profil et la caméra du relevé
précédent. Sur 300 intervalles prêts, la moyenne passe de 23,02 à
19,38 ms et le p95 de 25,43 à 21,02 ms. `render.Render` passe de
12,52 à 10,17 ms par appel. Les 707 casters opaques, les 715 récepteurs
et les cinq lots végétaux sont retrouvés. Les rapports ne signalent
aucune erreur de pipeline. La comparaison porte sur une vue fixe et
ne comporte pas de relecture directe de l'image de profondeur.
Reçu `unified-windows-20261107-049/native-profile/opaque-shadow-cascades/G7-D6-opaque-shadow-cascade-comparison.json`,
SHA-256 `15cd10e2801f046951b1dddf4029770455a030088e34143f358a21524436b506`.
Les 211 638 534 octets temporaires sur E: ont été retirés. Le
`source-inputs.json` conservé remplace les deux inventaires identiques
avant/après, à l'empreinte
`e0d87063e3266e68d7901ac3daee787b737ee644c54b28ae203127c82d055d19`.

Le moteur conserve les flux d'ombres végétales tant que la sélection
ordonnée reste identique. La position horizontale identique réutilise
la sélection. Une position différente applique le même sélecteur radial
et réutilise les octets si l'ordre des casters n'a pas changé. Les
remplacements invalident le cache. Le vent et les matrices de cascade
restent actualisés par le graphe existant.
Sur le même export Tidewater au profil Moyen et à caméra fixe, la
moyenne passe de 19,38 à 18,97 ms et le p95 de 21,02 à 20,66 ms. Les
deux relevés de régime comptent le même téléversement initial et aucun
nouveau téléversement, avec 259 appels de sélection entre les relevés.
Les cinq lots végétaux, les 707 casters opaques, les 715 récepteurs et
la concordance du vent sont retrouvés.
Reçu `unified-windows-20261107-049/native-profile/resident-plant-shadow-streams/G7-D6-resident-plant-shadow-streams-comparison.json`,
SHA-256 `de8f695ac637e6c791c95865c51646799d34ddcd3d65076640969786445538f5`.
Les 211 629 470 octets temporaires sur E: ont été retirés. Le
`source-inputs.json` conservé remplace les deux inventaires identiques
avant/après, à l'empreinte
`c28320287256f97164d32f39a806227574221cf3e3b31554463181a459fb606e`.

Le scénario natif a ensuite rejoué le même trajet d'environ 41 m sur
l'export Tidewater, avec le runtime distribué du 049 puis le runtime
optimisé déjà compilé. L'écart maximal aux bornes des deux déplacements
est de 5 cm. Sur RX 7800 XT, Vulkan, 1280 × 800 au profil Moyen,
l'intervalle moyen en mouvement passe de 24,53 à 20,61 ms, le p95 de
35,02 à 34,31 ms et le maximum de 69,56 à 46,12 ms. Les 558 et 665
intervalles retenus correspondent tous à des images déclarées prêtes.
Le budget moyen de 16,67 ms reste ouvert. Le relevé couvre un seul trajet
par binaire et ne compare pas le moteur au lecteur Tidewater historique.
Reçu `unified-windows-20261107-049/native-profile/moving-route/G7-D6-tidewater-moving-route-comparison.json`,
SHA-256 `e7fd65ab23ea440fc196be7e28b2b7c377b14a7038a53210558a90d73037eba5`.
Les preuves vérifiées sur F: totalisent 4 908 939 octets. Les caches,
copies et journaux de travail totalisant 541 657 981 octets ont été retirés d'E:.

Le moteur commun enregistre désormais les commandes d'ombres opaques une
fois par cascade dans un `RenderBundle`. Les passes de profondeur
réexécutent les mêmes commandes et les mêmes liaisons. Les matrices de
projection et les poses conservent les écritures précédant chaque passe.
Le nouveau binaire release reprend les mêmes déplacements sur le même
export Tidewater. Les bornes de trajet diffèrent de 4,5 cm au maximum.
La moyenne passe de 20,61 à 19,18 ms, le p95 de 34,31 à 33,99 ms sur
665 et 714 intervalles prêts. Aucun échec de pipeline. La capture finale
montre les ombres des bâtiments et de la végétation. Elle a été prise
après la fenêtre mesurée. Un trajet par binaire, aucune comparaison
pixel à pixel. Le budget moyen de 16,67 ms et le lecteur historique
restent à qualifier.
Reçu `unified-windows-20261107-049/native-profile/opaque-shadow-bundles/G7-D6-opaque-shadow-bundles-comparison.json`,
SHA-256 `f12d9bf84dc1ba64f92d4df1f256a81baeab28499d05a97085f61967c367658d`.
Le lot conserve 14 fichiers de preuve, soit 6 082 447 octets sur F:.
Retrait de 211 601 354 octets de temporaires sur E: et de 4 059 406
octets de copies d'inventaire identiques sur F: après vérification.

La mesure historique Release 012 du 15 octobre a été retrouvée dans
l'archive Gate 3 et vérifiée aux empreintes. Les rapports retrouvent les
mêmes identités de document, d'archive de zone et de données de jeu que
l'export Tidewater 049. Sur la RX 7800 XT et Vulkan, le 012 mesure 15,19
puis 10,51 ms sur les phases fixes et 10,50 puis 10,51 ms en rotation sur
place, à 2560 × 1440 et avec le pilote 26.8.1. Le trajet récent est mesuré
à 1280 × 800 avec le pilote 26.10.1. Les ressources visuelles du lecteur
historique sont chargées séparément, et les réglages diffèrent.
Le profil fixe du moteur commun avant les `RenderBundle` mesure 18,45 ms
pour l'ordonnanceur de rendu, dont 10,13 ms pour `Render`. La seconde phase
fixe historique mesure respectivement 10,03 et 6,55 ms. La préparation
hors du graphe de rendu mérite donc aussi d'être isolée dans un prochain
relevé apparié. Ces valeurs n'établissent pas la cause de l'écart.
Reçu de 6 701 octets obtenu sans relancer les binaires
`unified-windows-20261107-049/native-profile/opaque-shadow-bundles/G7-D6-tidewater-historical-reference-scope.json`,
SHA-256 `24ea6972fca82708bba72dfdccc0676031476885d1faa3d7575157dca2d3b8ba`.

Le binaire avec réutilisation des commandes d'ombres a ensuite repris le
trajet au profil Moyen avec les diagnostics CPU activés. Sur trois
fenêtres de cinq secondes après démarrage, `Render` coûte 8,66 à 8,87 ms
par image principale, `Prepare` 3,91 à 4,03 ms et `PrepareViews`
2,44 à 2,48 ms. Les contrôles végétaux avant et après le mouvement
prennent ensemble 1,46 à 1,90 ms. Ces portées incluent l'ordonnancement
et des appels imbriqués. Les fenêtres comprennent des déplacements et
des pauses. Le parcours instrumenté a terminé, toutes les images
retenues étaient prêtes.
Reçu `unified-windows-20261107-049/native-profile/cpu-moving-route/G7-D6-tidewater-movement-cpu-scope.json`,
SHA-256 `5d45cbc67a49f6186e847166d32b64615829db3d1a9033007522557886ad512d`.
Sept pièces vérifiées, 2 505 430 octets sur F:. Les temporaires du
profil sur E: ont été retirés, soit 211 671 752 octets.

Dans chaque garde végétale, la simulation peut réutiliser le contrôle de
projection réussi du rendu. L'observation se fait après l'actualisation
de l'herbe, puis les accès au monde sont en lecture jusqu'au contrôle de
simulation. Le contrôle de secours de la projection, la validation du
jeton GPU et les vérifications avant et après le déplacement restent
actifs. Les 29 tests de végétation streamée passent, dont les mutations
dans le même tick et le refus du mouvement tardif.
Reçu `tidewater-plant-guard-shared-audit.log.receipt.json`,
SHA-256 `ef3833d01c1d46cec56dae2a5934fe68dde66e5cf90dd3cd699fe8551b4dd416`.
La modification est postérieure au binaire profilé ci-dessus.

Le runtime indexe aussi les signatures d'inventaire par famille. Les
vecteurs de clés triées sont transférés sans copie et chaque famille est
lue directement. Les entrées utilisées pour les empreintes de publication
gardent le même format. Le nouveau binaire release regroupe les deux
modifications. Sur le trajet Tidewater identique, la moyenne passe de
19,18 à 18,89 ms et le p95 de 33,99 à 31,65 ms. Les 714 et 726
intervalles retenus sont prêts. L'écart maximal aux bornes du trajet est
de 4,5 cm. Les comptes des inventaires et des surfaces d'ombre
correspondent. La mesure couvre un trajet par binaire et ne sépare pas
les deux modifications. Le budget moyen de 16,67 ms reste ouvert.
Reçu `unified-windows-20261107-049/native-profile/graphics-inventory-keys/G7-D6-graphics-inventory-keys-comparison.json`,
SHA-256 `630ea851ccf89a889ce48c2b2939a587f4777fb5b01bf7d162f40c9ff6c17ccc`.
Douze pièces, 4 527 786 octets sur F:. Retrait de 211 601 096 octets
de temporaires sur E: et de 4 059 406 octets de copies d'inventaire
identiques sur F: après vérification.

Le chemin de l'exécutable Release 012 pointe désormais sur le cache
Cargo commun. Le lecteur conservé du 27 octobre a une autre empreinte.
Sur la RX 7800 XT en Vulkan avec le pilote 26.10.1, ce lecteur ouvre
l'instantané archivé et retrouve l'empreinte source attendue. Les
préférences et les 13 fichiers de l'instantané sont vérifiés. L'audit
des 8 165 autres fichiers retrouve une différence dans
`data/workspace/workspace-v1/authoring.json`. Le lecteur s'arrête sur
`Invalid source camera pose or field of view` dans `terrain_lod.rs`
(code 101) avant de produire un profil. La comparaison appariée reste
ouverte. Le runtime actuel n'a pas été relancé pour cet essai.
Reçu de 3 641 octets sur F:
`unified-windows-20261107-049/native-profile/G7-D6-tidewater-retained-probe-replay.json`,
SHA-256 `776e5f206153baf592af27c4f741cd5fe32e7b2304ea1d64e68f0c042534ba67`.
Les 54 382 216 octets de temporaires de cet essai restent sous
`.build/tmp/gate7-tidewater-aligned-20261107` sur E: après refus du retrait
par la politique d'exécution.

Les deux contrôles de végétation du moteur comparent la partition
terrain et le HLOD empruntés au contrôleur commun. Ils ne copient leurs
cartes que si la source, la politique, les cellules ou le HLOD ont
changé. Les 29 tests de végétation streamée passent sous Windows.
Le gain sur le trajet Tidewater reste à mesurer sur le prochain binaire
commun. Le journal et le reçu `tidewater-plant-partition-borrowed-audit.log`
et `tidewater-plant-partition-borrowed-audit.log.receipt.json` occupent
5 045 octets sur F:. SHA-256 du reçu
`9a87cb056537b2f618eab1f0ab46593ee0148789073b06e885f285fd784a8924`.

Le runtime release construit sur entrées stables reprend le trajet du
049 sur 40,80 m, au profil Moyen en 1280 × 800, avec Vulkan et FIFO.
Sur les deux fenêtres en mouvement, la moyenne passe de 18,89 à
18,85 ms et le p95 de 31,65 à 31,37 ms. Les 726 puis 727 intervalles
retenus sont prêts. Les bornes du trajet, les inventaires et les comptes
de surfaces d'ombre correspondent. Un passage par version et l'écart
observé ne permettent pas d'attribuer un gain à la modification. Le
budget de 16,67 ms reste ouvert. Le lecteur de notices et sa fixture
ainsi que deux fichiers du protocole Linux ont aussi changé depuis le
build précédent.
Reçu `unified-windows-20261107-049/native-profile/plant-partition-borrowed/G7-D6-plant-partition-borrowed-comparison.json`,
SHA-256 `7395b30a91ede1a87d1363cba1072158ef54dc7648e7d5a2084498418694f049`.
Douze fichiers, 4 525 977 octets sur F: après retrait de 4 059 408
octets de manifestes identiques. Le dossier temporaire E: a été retiré,
dont 190 017 122 octets après reprise du nettoyage en chemins longs.

Le contrôleur terrain commun conserve l'identifiant logique de chaque
cellule et tuile HLOD dès sa publication. Les contrôles comparent encore
le marqueur vivant à cette identité et à la source attendue. Le dernier
trajet Tidewater compte 169 cellules et 144 tuiles HLOD, soit 313 calculs
d'identifiant évités à chaque contrôle terrain complet dans cette scène.
Les 50 tests de streaming passent sous Windows, dont les refus d'un
marqueur de cellule ou de tuile altéré. Le gain de cadence reste à mesurer
sur un build commun ultérieur. Journal et reçu
`terrain-residency-identity-audit.log` et `.log.receipt.json`, 6 920
octets sur F:. SHA-256 du reçu
`f533eccd8e0bcc3480b8eb669fc2991cd37971d418337495832cd2355f72d18b`.

R7-09, cycle du Studio 049. Un Sandbox Windows hors ligne et sans SDK
a installé le 042 puis le 049 sur la même installation. Deux plans natifs
ont réactivé le 042 puis le 049. Le programme de désinstallation NSIS
a retiré le Studio, puis le même Setup 049 l'a réinstallé et son exécutable
exact a ouvert une fenêtre Studio. Les 42 fichiers du workspace de
qualification (271 032 968 octets) et le catalogue de référence ont gardé
leurs empreintes. Le workspace préservé est un contrôle de fichiers.
L'essai d'ouverture et d'export depuis le Studio relève du parcours
distinct déjà consigné. Le Sandbox est arrêté.
Reçu `unified-windows-20261107-049/studio-maintenance/G7-D6-windows-studio-maintenance-049.json`,
SHA-256 `537a15d552539c006fc4c77416dc1e7da75c006fb3cc0d5bbfac31703c6e31b2`.

Le rapprochement Windows privé 049 vérifie 43 pièces et relie les
empreintes des builds aux binaires observés dans les invités. Il couvre le
volet Windows de R7-07, le cycle d'installation et de maintenance Windows
de R7-09 et le parcours en Sandbox Windows 11, hors ligne et avec vGPU,
de R7-10. Le Studio a exporté depuis une copie du workspace auteur. L'invité
joueur a installé et repris les deux jeux sans Studio ni SDK. Les étapes de
maintenance ont leurs propres invités et preuves. Les reçus historiques
gardent leur portée. Ce rapprochement de 6 334 octets a été réalisé sans
nouveau build ou essai en invité.
Reçu `unified-windows-20261107-049/R7-07-09-10-windows-049-assessment.json`,
SHA-256 `a8cc186a3cf6630ed9a63f2300d7f51e41fa366716f51c1cff16082346afb5d4`.

R7-08, périmètre des deux exports 049. Les manifestes limitent les deux
jeux à l'usage privé et déclarent les mêmes 471 entrées de notices pour
l'application Windows. Le catalogue embarqué du Grand Plateau contient deux
maillages procéduraux avec preuve de redistribution. Les quatre bâtiments
Quaternius sélectionnés dans Tidewater ont un libellé CC0-1.0 et un droit
de redistribution déclaré `forbidden`, sans preuve de redistribution
pour les fichiers livrés. R7-08 reste ouvert pour les autres contenus et
composants ainsi que la validation des droits.
Reçu `unified-windows-20261107-049/R7-08-windows-game-export-scope-049.json`,
SHA-256 `a7c58d52a46bc4607e8719b61fbeb8958eefb00c0ff7cf10a2f2bb99115973eb`.
Les archives des lots 017, 018 et 020 d'octobre établissent maintenant la
filiation exacte des quatre GLB. Les 52 entrées sources des recettes
correspondent aux octets de l'archive Quaternius conservée. Les empreintes
relient les assemblages, les retouches de textures et de supports et les
quatre fichiers embarqués. L'éditeur du pack annonce la licence CC0.
[Medieval Village MegaKit](https://quaternius.com/packs/medievalvillagemegakit.html)
La preuve complète le dossier d'import gouverné des modèles. Le chemin de
secours qui les reprend d'une installation de référence produit par
construction un catalogue d'usage privé. Le 049 conserve ses droits déclarés.
Reçu `unified-windows-20261107-049/R7-08-tidewater-quaternius-provenance-049.json`,
SHA-256 `83f8b6501e9fcc901e9d58c35e0ae6cdb8b666521f7284029e27d3a938b6de7e`.
Les quatre modèles ont ensuite passé la préparation, l'application, la
relecture et la sélection pour l'export privé dans une collection Studio
isolée. Le service natif a conservé les quatre identifiants et SHA-256
utilisés par les placements. Les douze fichiers de l'instantané couvrent
les modèles, les recettes de dérivation et les notices. Les fichiers source
du pack sont déclarés CC0 par Quaternius. Les entrées d'essai conservent
le droit de redistribution inconnu pour les assemblages retouchés et le
service refuse leur sélection publique. [Licence du pack](https://quaternius.com/packs/medievalvillagemegakit.html)
L'essai utilise la bibliothèque native Studio du cache commun. Il couvre
ses commandes de collection et l'instantané, sans rejouer l'UI ou
réassembler un module. Le reçu conserve les entrées de reprise et leurs
hashes, soit 31 920 octets sur F:. La collection d'essai, les paquets de
transport et le programme de contrôle ont été retirés d'E:, soit
88 791 984 octets.
Reçu `unified-windows-20261107-049/R7-08-tidewater-governed-reimport-049.json`,
SHA-256 `210c319292b2bb1a3f94f9fea6e077c8501d34fb530da83a5eab055bf7288b22`.

La lecture intégrale des deux transports 049 rapproche leurs descripteurs,
leurs conditions privées d'origine et leurs 5 et 13 entrées de licence.
Dans Tidewater, les manifestes des surfaces, de l'environnement et de la
végétation épinglent la même révision amont et déclarent respectivement
821, 266 et 83 fichiers. Le paquet reprend la licence MIT amont aux
empreintes, mais ne contient pas son fichier de crédits complet. L'amont
distingue la licence de son code des notices des ressources tierces.
La portée des droits doit donc être rapprochée des médias effectivement
embarqués, y compris les six modèles historiques distincts du catalogue
actif. [Crédits amont](https://raw.githubusercontent.com/dgreenheck/tidewater/1438b1abfcaee3267092b75573014f4d9b4a983c/CREDITS.md)

Le même inventaire relève 21 597 175 octets de répétitions dans le module
Tidewater. Quatre paires modèle livré/source de retouche en représentent
21 579 540. Les chemins ont des rôles distincts. Cette mesure délimite le
lot à mutualiser et les chemins à préserver dans une future distribution.
Les paquets vérifiés sont conservés aux empreintes. Le reçu de 11 554
octets sur F: est
`unified-windows-20261107-049/R7-08-windows-module-media-notice-scope-049.json`,
SHA-256 `d999e072b1884f430d84ffb7a5167684341e8e6f5308042310c1532f0da6ef6b`.

Le manifeste du paquet d'origine 019 épingle
`data/scene/CREDITS-Tidewater.md` au SHA-256
`45fbe42bf547cea34cb6606e9571b59045d0b9d0c6a55321615b9ca791510325`,
mais sa liste de licences ne désigne que `PROVENANCE.md`. Le lecteur
commun transmet désormais aussi les crédits épinglés à la racine de
la scène lors d'un export depuis une référence installée. Le test
Windows de migration vérifie leur relecture après réinstallation et
le refus des crédits altérés. Les exports issus d'un ancien paquet
importé reprennent ses avis existants. Pour vérifier l'ajout dans un
jeu complet, il reste à repartir de la référence installée avec le
prochain Studio. Le statut privé et les réserves sur les médias restent
applicables. Le journal et le reçu `r7-08-legacy-reference-scene-credits`
occupent 1 394 octets sur F:. SHA-256 du reçu
`a10af91f589df8ca92581dbc716bfe18e1eaaf490adc085ea0f1ba6956acb530`.

Le service natif d'export a ensuite assemblé et relu un module privé de
210 733 850 octets depuis la référence 019. L'archive et les blobs
canoniques de Tidewater ont été repris aux empreintes, avec le catalogue
et les modèles historiques du 049. Le paquet embarque les 4 892 octets
des crédits amont sous `_forge-export/reference/notices/data/scene/`,
avec la même empreinte et une entrée de licence. La zone, la végétation,
l'environnement, les surfaces et les six révisions historiques
correspondent au 049. Le contrôle réutilise les bibliothèques du cache
release, sans lancer l'interface Studio ni installer le jeu. Les droits
déclarés restent privés. Les 437 421 640 octets de fichiers de travail
ont été retirés. Reçu et programme de contrôle, 7 878 octets sur F:
`unified-windows-20261107-049/R7-08-reference-credits-export-049-data.json`,
SHA-256 `cd4626880ef6478e75e1a264861fdac122ab27b99edaada67d89087e36279478`.

L'inventaire du Setup 049 relit ses neuf composants et les 475 fichiers
du paquet Studio, dont 472 entrées de notices et de provenance. Les
plugins NSIS et le bitmap correspondent aux fichiers du toolset local.
L'empreinte du programme WebView2 embarqué correspond au paquet Microsoft
dont le runtime interne 155.0.4283.45 a déjà été vérifié x64. Les trois
identités de dépendances Web encore en réserve et les conditions CRT
restent consignées dans le reçu. Les fichiers historiques à l'origine
du module Tidewater imposent toujours l'usage privé du réexport.
Reçu `unified-windows-20261107-049/R7-08-windows-studio-setup-scope-049.json`,
SHA-256 `e9218f5329d233916350c5c73e6acd90e6203aa5dee8994b2023952365d9ef16`.
[Licence NSIS](https://nsis.sourceforge.io/License) et
[déploiement hors ligne WebView2](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution#offline-deployment).

Le préparateur Windows recalcule les identités sans texte dans les trois
index du kit. Il refuse un audit qui omet une identité, même avec une
empreinte d'audit valide. Les 13 tests de préparation Studio passent.
La relecture des index aux empreintes du Setup 049 retrouve les trois
réserves WASM et aucune dans la portée Cargo native Windows du Studio.
Le reçu de 1 718 octets sur F: consigne la relecture sans réassemblage du 049.
`unified-windows-20261107-049/R7-08-windows-studio-notice-gaps-replay-049.json`,
SHA-256 `f954274f38d430e81750780f728fa27383b10d9149ef02fd67a4d70b882ae650`.

Le WASM aux empreintes du Setup 049 contient sept fonctions qui passent
la même adresse de localisation issue de `drain_filter_polyfill 0.1.3`.
Elle désigne la ligne 148 de `src/copypasted_impl.rs`, où l'implémentation
indexe le vecteur à compacter. Le désassemblage établit la présence de
ce chemin dans le WASM livré. La comparaison avec la source Rust du
supplément retrouve 167 lignes identiques dans l'ordre. La révision
nightly exacte et la notice propre au paquet restent à établir.
[Polyfill publié](https://github.com/whentze/drain_filter_polyfill/blob/9dc585f3eec048bfc3faf8656e07c4a19273c38f/src/copypasted_impl.rs)
et [référence Rust](https://github.com/rust-lang/rust/blob/69f9c33d71c871fc16ac445211281c6e7a340943/library/alloc/src/vec/drain_filter.rs).

Le graphe verrouillé et les deux sites d'appel confirment que
`convert_case_extras` teste la casse des identifiants dans la macro hôte.
Pour `erased`, le WASM 049 conserve le chemin et le diagnostic de son
appelant Tachys. Les instructions optimisées propres à `erased` restent
à attribuer. Les trois réserves de notices restent dans le reçu de
distribution. Reçu de 4 580 octets sur F:
`unified-windows-20261107-049/R7-08-studio-wasm-instruction-scope-049.json`,
SHA-256 `06c812528fae7e234b66b9c73098803aef117674c605b1780d24d150c3f136d8`.

Dans l'invité 036, la collection du Studio a reçu les deux images originales
de qualification : catalogue révision 2, SHA-256
`2e8181767b9c16c995763604391ad158392ae0b4c14cbd96d0535bdf9bead547`.
La variante verte conserve son parent bleu, la preuve de licence privée et
sa source de retouche. Voir `import-catalog-036.json` et
`import-variant-applied-ui-036.jpg`. Le montage inscriptible
`C:/ForgeQualification/PresentationResults` vise le lot 036 ; `Results`
continue de viser le lot 035. Aucun nouvel invité ni redémarrage du poste.
Consigne propriétaire : les essais Gate 7 déjà autorisés se poursuivent
sans redemander une confirmation à chaque installation ou étape de test.
Conserver cette autorisation dans ce périmètre de qualification isolée ;
respecter les nouveaux ordres d'arrêt, sans en déduire de publication Git.
Stockage du lot Cairns 019 terminé. Les fichiers `qualify.exe`, `.pdb`,
objet intermédiaire et `qualify.rs` ont été retirés de la qualification E:
après contrôle de l'absence de processus actif et comparaison exacte du code
avec le champ `reconstruction_harness` du reçu 019 conservé. Retrait de
15 977 658 octets. Le module, la sauvegarde et le candidat autonome restent
disponibles pour les contrôles de coexistence et de migration.
Stockage 020 : la copie d'assemblage `forge-studio.forgepack` a été retirée
de `.build/tmp/gate7-studio-delivery-20261106-020` après lecture intégrale du
même membre dans l'installateur 020 conservé. Les 342 631 099 octets ont le
SHA-256 `8ac24223905d90c7679875d2ca3575fc536442aa7ccafc415921b02c6f33d110`.
L'installateur 020 reste disponible pour un retour arrière. Le paquet 021
reste en usage pour la qualification en cours.
Pour partir du terrain plat, ouvrir la carte créée et utiliser « Activer les
règles de jeu dans cette carte » avant la sauvegarde. Le Studio crée ainsi
le joueur initial exigé pour l'export. Rouvrir « Projet et jeu » carte
fermée, choisir cette carte comme départ et sauvegarder la configuration.

R7-08 : le WASM livré par le 020 a la même empreinte que le WASM 018 audité.
Les archives verrouillées `erased 0.1.2` et `convert_case_extras 0.2.0`
correspondent aux manifests d'origine et aux sources de leurs commits
de publication respectifs. Les deux manifests déclarent MIT et leurs arbres
amont ne contiennent aucun fichier de licence ou d'attribution distinct.
Reçu `R7-08-upstream-source-reconciliation-020.json`, SHA-256
`92ebd87fe64ced3ce17e32ff90fb5929e4e2c2e49764555ff65d9d2f2b224fc2`.
Les réserves d'attribution et de portée binaire restent ouvertes. Les droits
sur le code CRT désormais lié aux binaires Windows relèvent des conditions
Microsoft applicables au toolset, séparément des notices Cargo.
[Conditions Build Tools 2022](https://visualstudio.microsoft.com/license-terms/vs2022-ga-diagnosticbuildtools/)
et [liste de redistribution 2022](https://learn.microsoft.com/en-us/visualstudio/releases/2022/redistribution).
Les deux paquets effectivement qualifiés pour le lancement et la reprise de
« Mon projet » ont été relus et vérifiés aux empreintes. Le paquet de jeu
embarque 471 fichiers de licence et le module ne référence aucun asset du
catalogue. Les index Cargo de base et des assets correspondent à ceux du
candidat Trois Cairns 017. Les deux index complémentaires diffèrent.
Reçu `R7-08-clean-windows-game-020-notice-scope.json`, SHA-256
`839374309f33c82946eb34aee508ba80930b1b9908a0689a75f694e444330a57`.
Les graphes Cargo Windows du moteur de jeu et du launcher conservent les
mêmes 613 identités cumulées et les mêmes notices candidates que le 017.
Les deux index modifiés conservent leurs entrées de paquets. Le complément
ajoute les pièces de provenance de `drain_filter_polyfill 0.1.3`, avec
l'origine exacte encore en réserve. La police Fira Mono du moteur 020 a été
retrouvée octet pour octet et sa notice vérifiée dans le paquet. Reçu
`R7-08-clean-windows-game-020-notice-delta.json`, SHA-256
`d9ae594e853666c6df1e5cfd5a4de4873ebc89c27c6ac7973015763ac6b36f00`.
Le jeu est en usage local privé. La portée des notices pour les binaires et
les ressources intégrées au moteur reste à établir.
L'inventaire extrait de l'installateur 020 contient neuf entrées. Les trois
plugins et le bitmap correspondent aux fichiers NSIS 3.12, avec compression
zlib, et les trois fichiers Forge au dossier d'assemblage. L'exécutable
WebView2 embarqué correspond au fichier Microsoft vérifié au build.
Reçu `R7-08-studio-installer-components-020.json`, SHA-256
`89cd0185a238ef8a7a893d0ed614db96ea8bb12d92ef196230f2f8fc46b8c630`.
Le stub WebView2 est x86. L'extraction hors exécution du fichier épinglé
confirme que le setup interne et `msedgewebview2.exe` 155.0.4283.45 sont
x64. Reçu `R7-08-webview2-payload-x64-020.json`, SHA-256
`9315d40aa20c6fb939cd88dc9e2f9dafdd6c9ed6dbc4087865bf05a363ade954`.
Le Sandbox Windows 11 du 6 novembre conserve WebView2 156.0.4314.58
après la tentative de retrait via le programme Microsoft signé, qui
retourne le code 93. L'installation hors ligne sur un système dépourvu
de WebView2 reste à vérifier.
La source NSIS traite désormais la valeur `0.0.0.0` comme une absence et
poursuit la recherche dans les autres emplacements. Le détecteur extrait
de cette source passe quatre cas dans le Sandbox avec des clés de test
distinctes. Les huit tests de l'installateur passent, dont la compilation
des deux profils NSIS. Le binaire 021 précède la correction.
Reçu `R7-10-windows-webview2-detection-021.json`, SHA-256
`45463e8abb13ee4102f3dcac4c135cc06fe17e7aad88e9924e0b72ba1560da99`.
[Licence NSIS](https://nsis.sourceforge.io/License) et
[déploiement WebView2 hors ligne](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution#offline-deployment).

R7-06 rejoint R7-01/R7-03/R7-04/R7-05 : 45 preuves et 11 sources revérifiées,
32 tests actuels du launcher standalone, frontend/sources 011–015 identiques
et parcours réel 011 conservé. Reçu `R7-06-game-launcher-assessment-015.json`,
SHA-256 `decfb4a2c863f3304040240a4bdbc6f5a65d96288a70b4b5df9088626f6025e1`.
Ne pas refaire le cycle du launcher sans nouveau changement ; 015 non installé.
Les réglages indiqués non appliqués restent hors qualification fonctionnelle.
R7-04 rejoint R7-01/R7-03/R7-05 : 49 preuves et chemins de ressources fermés
revérifiés, 25 tests actuels des refus après changement des services. Reçu 015
`R7-04-runtime-packaging-assessment-015.json`, SHA-256
`5ef6f45265c58aeb924523d1b1578e315a71aa5c75792a440eda80b68bb8a2be`.
Ni OS propre, ni licence/redistribution, ni installation 015 déduits de ce verdict.
R7-05 rejoint R7-01/R7-03 après rapprochement vérifié de 45 preuves : reçu 015
`R7-05-graphics-profiles-assessment-015.json`, SHA-256
`cf9dc52a2f100dc62fa3284e60183307d59c4050d728801c47a6729c97e83b38`.
Le refus Medium historique est conservé ; ancienne et nouvelle captures vues,
motif de halos non observé dans le 015. Mesures par profil de 39–43 échantillons
sur environ 1,1 s ; aucune acceptation FPS/parité Tidewater n'en est déduite.
R7-02 est qualifié localement Windows. Le Studio 015 a sauvegardé deux copies du cairn existant
à X=80/86 m, Z=-89 m hors cellules des PNJ, puis exporté le jeu privé 1.0.2
`game-8496-1793895505129626800` (workspace révision 14, aucun autre contenu changé).
Le parcours 015 a préparé un agrégat, puis refusé la qualification à l'étape 0 :
l'observateur GPU de HLOD comptait aussi la caméra hors écran de réfraction.
Preuves et export rapproché dans `profile-prepass-lifecycle-20261105-015/hlod-complete-001`.
Correction dans `static_hlod/gpu.rs` sur le marqueur commun `OffscreenSceneCamera` ;
15 tests ciblés passent, dont refus de deux caméras de scène. Runtime 016
construit ; le parcours s'arrête encore à l'étape 0 après 123 activations
d'une image. À chaque activation les casters passent de 28 à 26, l'atlas
est recréé et invalide l'agrégat qui le lit. Reçu `hlod-shadow-feedback-refusal.json`
dans 016, SHA-256 `018ad10e1fff5edfac7cb64aa00ea11aa4eef5591e1a037dee7e566dc446fd4c`.
Le moteur 017 conserve les casters source avec leur visibilité logique dans
le collecteur d'ombres existant ; les contrôles d'admission GPU sont conservés.
Build runtime 017 terminé sur cache partagé ; PID 26692 sorti normalement après
les 18 étapes du même parcours 1.0.2. Lot prêt sur les quatre profils, détail
restauré, puis lot réactivé ; 28 casters dans les fenêtres agrégées mesurées.
Trois captures revues, 21 preuves épinglées dans `R7-02-hlod-assessment-017.json`
(dossier 017), SHA-256 `e8878ffdf90deb114011fa627a47d56dbda5dad9c54d8b000219ebc5688212e2`.
Studio 8496 fermé, 1.0.2 non installé. Le kit privé 017 est assemblé sur les
mêmes sources compilées : 471 fichiers / 141 196 895 octets, runtime
`82f6aa364a2fcf1262298ccccc0915fbdf95a3bae4915334b2134ac66a2398e7`.
Reçu dans 017, `game-kit-assembly-receipt.json`, SHA-256
`b36afaba250a3f4d2c080d4b3a0f46247d6d014e24c390a5dba018d68e0b6a6c`.
Kit sous `.build/tmp/gate7-windows-game-kit-engine-20261105-017`.
Frontend, Studio et CLI 017 terminés. Studio `441127607758195ae8a4d5f0f808c32241c1e1ac4ef4f49be214cfc36170eb31`.
Kit adjacent vérifié dans le `bin` de qualification 017. Export réel
`exports/game-4328-1793901617923867300` depuis le workspace révision 14.
Tous les payloads sont vérifiés et le module C6 est identique au module de
l'essai HLOD. Reçu `studio-ui-export-verification.json` SHA-256
`f27611839c79a1bfd4116b318aa67f122b0ead212ae85232cd6326eb21b06bf2`.
Studio fermé. L'installation neuve 017 autorisée par le propriétaire est en
révision 1. `Jouer.exe`, partie neuve, F5 et reprise via le launcher observés.
Reçu `installed-fresh-resume-cycle.json` SHA-256
`a7ac594a162e6d8978c6ba79bbd76b4c6b8b26db26cf48524e51de76c58db227`.
Le rapport final de reprise indique une projection indisponible à la fermeture,
malgré la sauvegarde reprise, 2 008 ticks et une sortie native réussie.
Après installation des Trois Cairns, une autre reprise du Camp 017 termine
avec la projection prête, sans reproduire l'indisponibilité. Pour dater un
éventuel retour, le runtime commun rapporte les première et dernière images
prêtes et le moteur date l'événement de fermeture. Vérification Cargo ciblée
réussie (`r7-09-resume-projection-frames-check-20261105.log`, SHA-256
`17670cf73950abaf1f75f2e9731772424dc852fc0145654197f7901c1c18b197`).
Les deux jeux installés précèdent cette instrumentation.
Les cycles 008/011 restent les preuves du changement de runtime à carte identique.
Le rapprochement 011→017 relève 19 différences d'entrées compilées hors
launcher, services de distribution et sauvegarde. Reçu
`R7-09-install-path-reconciliation-017.json` SHA-256
`b6054dd6632f16e7c196b191a49cacb5941a73ea334205877c2de58606f9df5b`.
Le même Studio 017 a exporté un second jeu, Les Trois Cairns, depuis le
workspace séparé `second-game-workspace`. L'outil de qualification a préparé
le workspace à partir du module procédural existant. Le titre, la carte de
départ et la configuration ont été enregistrés dans le Studio avant l'export.
Les deux paquets embarquent le même moteur. Leurs identités, modules et
emplacements de sauvegarde sont distincts. Les quatre fichiers du second
export totalisent 160 901 709 octets et leurs payloads ont été vérifiés.
Reçu `second-game-export-verification.json` SHA-256
`615d60876a16110900126f0fb50b9160accdb0b8139f3023ce9bd0555fd31e46`.
L'installateur des Trois Cairns a refusé la racine du Camp 017 avec le
motif « A different game is installed here ». L'installation et la sauvegarde
du Camp ont été vérifiées par SHA-256 après ce refus. Le plan d'installation
neuve des Trois Cairns a ciblé `second-game-install`.
Reçu `second-game-installer-plan-ui.json` SHA-256
`ceefd097dae724222447f5f40b9b142ce1552d86877102479fecad6868969dd5`.
Installation autorisée et appliquée. Nouvelle partie puis reprise des
Trois Cairns, puis reprise du Camp, toutes trois terminées avec projection
prête. Les deux jeux utilisent le même binaire et le même dossier de données
de qualification, avec des sauvegardes séparées par identité de jeu.
L'installation et la sauvegarde du Camp sont identiques au relevé préalable.
Reçu `R7-09-installed-games-isolation-017.json`, SHA-256
`1a20761e1ae34cec51685f80f59033e0ca8ca1e5b4e6593829114f6a2d36fce1`.
Le Studio du second jeu est fermé. Le retrait du cache temporaire
`second-game-studio/webview` a été bloqué avant exécution par le contrôle
automatique. Le dossier contient 305 fichiers et 73 610 970 octets. Ne pas
retenter ce retrait par un autre moyen. Le workspace auteur et son export
sont dans le dossier frère `second-game-workspace`.
Le Camp 017 a été désinstallé par le programme prévu après autorisation.
Les deux sauvegardes et le manifeste d'installation des Trois Cairns
correspondent au reçu de coexistence. Les 2 271 fichiers déclarés des deux
paquets du Camp (412 090 735 octets) et le programme temporaire sont retirés.
Le dossier `packages` vide et le verrou de zéro octet subsistent. Reçu
`R7-09-camp-uninstall-saves-017.json`, SHA-256
`acd8567fb5e23dd304ca6b5371f180b91e25577d783c5f5b96a02b831bebe326`.
Reçu `uninstall-plan-ui.json` SHA-256
`9e089a8992ee222b657fe21ffef0c1d4375f55f8c0b5bafc3180ad3334096675`.
Installateur privé du Studio assemblé sur les mêmes sources. Reçu
`studio-installer-result.json` SHA-256
`86c2a7df15d89432e99ee769ee614176c782cd9676af0a20dfa58291c07ed54f`.
R7-10 : le journal Panther de l'invité propre situe l'échec dans la phase
Windows `Sysprep_Online_Specialize_Bcd` (`0xC0000098`). Le relevé ciblé et
les révisions de reprise sont épinglés dans `final-qualification-preflight.json`,
SHA-256 `c4cd14abdec9538dcf049e79fddd1d5c2110a3da2bc8d1c86d63523cda0fb333`.
Windows Sandbox est disponible comme fonctionnalité hôte désactivée.
Configuration 017 préparée dans le dossier de qualification, accès en lecture
seule aux deux distributions, réseau désactivé, vGPU activé. Le démarrage
prévu relève l'OS, les outils absents ou présents et les empreintes des entrées.
Reçu `windows-sandbox-plan.json` SHA-256
`1b11d4effd400cffa67218d2d06946fe54163464a875e490d8b2ef656f2928d0`.
Le manifeste embarqué et l'installateur 017 correspondent aux reçus de
construction. Le Studio prépare une racine auteur vide et la bibliothèque
d'assets est facultative. Sur l'invité, ouvrir « Projet et jeu », initialiser
l'espace, créer un projet, un module et une carte, puis activer et sauvegarder
les règles de jeu de la carte. Configurer cette carte comme entrée du jeu et
créer les paquets privés. Le parcours est rapproché de huit sources du 017
dans `R7-10-studio-first-run-preflight.json`, SHA-256
`29a7e49e45057b701163f4d2810ac2721309462954df93cf498dcac15c9f8c66`.
L'installation, l'export et la reprise sur l'invité restent à observer.
Dans le checkout commun, le navigateur de modules accepte désormais une
racine auteur vide. L'initialisation reste explicite. L'ouverture demandée
explicitement et les autorités existantes invalides gardent leurs refus.
Deux tests ciblés passent, dont le parcours sur workspace existant, journal
`r7-10-first-run-workspace-host-20261105.log` (racine de la campagne), SHA-256
`8893a6a65d1cc2e739f8632e6ff43c9232ea1222b22307c269452d922501b2ae`.
Le Studio 017 précède cette correction. Sur le 017, l'action d'initialisation
reste accessible dans « Projet et jeu » après le message de fichier absent.
R7-08 : le catalogue réellement embarqué du module 1.0.2 autorise les deux
géométries originales, mais déclare `redistribution: forbidden` pour
`vv-tree-pine-a` et `vv-tree-oak-a`. Le module et le kit restent privés.
Reçu `R7-08-private-candidate-content-scope.json` dans 017, SHA-256
`4698d1430840986a301a27aab41db40ed919f1290c528e0288a5fef25ea02d57`.
Établir leurs droits ou remplacer les modèles avant redistribution ; la licence
du code Forge ne couvre pas ces déclarations d'assets.
Les Trois Cairns offre un périmètre distinct. Les 489 fichiers déclarés dans
ses deux paquets installés correspondent à leurs manifestes. Ses deux GLB
et recettes déclarent CC0-1.0, sans les arbres du Camp ni texte historique
importé. Le contenu RPG correspond au module source, déclaré privé. Les
613 identités des graphes Cargo Windows cumulés du moteur de jeu et de son
launcher ont chacune un texte de notice candidat. La vérification couvre aussi
la notice de la police, les textes et les archives des neuf composants Symphonia.
Reçu `R7-08-trois-cairns-installed-notice-scope-017.json`, SHA-256
`10c06c9ffb476b5ccf446fb953913321c7be80e814bbd65918d967d91b31a07c`.
Le candidat reste privé. L'inventaire Cargo conserve sa portée de graphe
de build et les réserves du Studio sont traitées séparément.
La police Fira Mono a été retrouvée octet pour octet dans le runtime du kit 017.
Le texte OFL embarqué correspond à la notice amont épinglée. Reçu
`R7-08-font-kit-017.json`, SHA-256
`8f411e12754711adda3e39d61cdc740f24aaa157fbb16498aa16dad89b235b46`.
Le WASM épinglé du Studio 017 contient le chemin source de
`drain_filter_polyfill 0.1.3` dans sa section de données. Le fichier adapté
de l'archive Cargo verrouillée correspond sur 87 lignes de code alignées à
la référence Rust 1.66.0. Le reçu rapproche le texte Apache, le texte MIT
et le fichier COPYRIGHT du même commit Rust, sans déterminer le point de
fork exact. Candidat de notice `R7-08-studio-wasm-rust-attribution.json`,
SHA-256 `6b24127a8f8730d293b730780b9da7f9d711b8ec434f1f7bcfa63b923f6f25b4`.
Le bundle privé `E:/OpenRPG_Forge/.build/tmp/gate7-notice-bundle-015`
intègre ce rapprochement comme référence de provenance transitive. Le
collecteur a conservé les entrées précédentes et le préparateur Windows a
vérifié les trois documents Rust et la preuve 017. Reçu
`R7-08-notice-bundle-015-candidate.json`, SHA-256
`c4ab930c86a7cf4b70fb79e42c851cbe9142fd490423d476ee7e4a08ed8779ab`.
Le Studio 018 réutilise le graphe 014 avec ce reçu. Le bundle 015
contient 437 fichiers et 2 923 790 octets. Le 014 reste la référence des
paquets déjà vérifiés. Les trois identités restent en réserve et le Studio
installable 017 utilise toujours les notices 014.
R7-01 rejoint R7-03 après rapprochement vérifié : reçu 015
`R7-01-streaming-assessment-015.json`, SHA-256
`e4e7c0b2cc98fb6849e9f26b5d67c907d23f46028988a9605d3c6b8819341569`.
Le Studio 015 a exporté `game-33412-1793890314656093300` : quatre fichiers /
431 789 877 octets, tous les payloads et deux inspections natives vérifiés.
Reçu `studio-ui-export.json`, SHA-256
`0434e332d728edc7230305298f48956eaca8f8cc34050a989859c12b60a1529f`.
Studio 33412 fermé après cet export. Source auteur alors en révision 12.
L'installation est désormais 008 révision 3 : rollback UI 011 → 008 autorisé,
réouverture via `Jouer.exe`, reprise et sortie du jeu vérifiées. Le SHA du fichier
de sauvegarde et son état restent identiques. Reçu `installed-rollback-resume-cycle.json`
dans 011, SHA-256 `b80cbbbcd6f47fffbb6d913bf9adaac3de1d53e2ffa5471e81d487276d5f76a3`.
La désinstallation du Camp 017 est vérifiée ci-dessus. Le parcours installé
du Studio 018 reste à qualifier.
Ne pas refaire les cycles de profils ou le trajet streaming acquis.
Le retrait des replis 014 a été refusé avant exécution (`blocked by policy`) :
341 912 159 octets conservés, zéro retiré, reçu `replaced-014-retirement-result.json`.
Ces deux nouvelles cibles sont désormais refusées aussi ; ne pas les retenter
par un autre moyen. Les refus antérieurs 005/008/011 restent préservés.

**Profil 015 : correction GPU vérifiée, consolidation terminée.** Runtime 015
construit sur source stable `a0f90d61…`, binaire SHA-256
`41939b16a6c506de4205d4fb2f02dd3549254e0e47f8e48dc1b9a4cadf4f81c7`.
Cycle natif PID 29108 terminé : 14 étapes, Low/Medium/Ultra/High appliqués,
persistés et pleinement prêts. Redémarrage PID 24816 terminé : High relu malgré
le défaut CLI Ultra. Aucun défaut de validation GPU ; quatre captures inspectées.
Préférences isolées ; sauvegarde réelle joueur `223dc3c4…` inchangée. Reçu 015
`qualification-assessment.json`, SHA-256
`f218506709e3dbc37a0a7678805bbd51c9c99899e2ffd91ef67333b1140e328e`.
Ne pas rejouer ce cycle sans nouvelle cause. Sources compilées désormais figées.
Les cinq stages sont terminés sur sources stables. Suivi functions 2870,
frontend 41786 et Studio natif 81801 clos. Binaire Studio SHA-256
`445145909f3a9ba486d7b665b749fdabd54a08a5b7beed4c37c74f3acd42cd0b`.
Le frontend WASM/JS/HTML/CSS est byte-identique au 014 ; seuls deux manifestes
de pins changent. Reçu `frontend-byte-comparison.json`, SHA-256
`b1260fa6215ee21ad946133f0f6b6963c81a9827b87c02df76db11fc69e417aa`.
Kit assemblé (8501 terminé), version `0.1.0-preview.20261105.015`, 471 fichiers /
141 195 871 octets sous `.build/tmp/gate7-windows-game-kit-engine-20261105-015`.
Reçu d'assemblage copié à l'identique dans les preuves 015, SHA-256
`4350e7ab06375cc40cedc1f960d23ea081aea97188e9c648ed97ba84464c0620` ;
manifest kit `39465b28e28f7ae87a11ec113f62437fea6893c8b11899cb261f21e33570fdde`.
Le stage Studio terminé du pipeline existant utilisait
`--studio-game-kit-root E:/OpenRPG_Forge/.build/tmp/gate7-windows-game-kit-engine-20261105-015`
et `--studio-game-kit-receipt-sha256 4350e7ab06375cc40cedc1f960d23ea081aea97188e9c648ed97ba84464c0620`.
Même evidence 015, cache/cargo-home/temp partagés et `--shared-jobs 16`.
Export 014 conservé pour le rapprochement R7-01 ; export 015 vérifié ci-dessus.
Le kit 015 est privé, pas qualifié pour redistribution ; 015 n'est pas installé.

Notice indépendante préparée pendant ce build : `drain_filter_polyfill 0.1.3`
déclare MIT OR Apache-2.0 au commit publié `9dc585f3…`. Les trois blobs source
de l'archive Cargo correspondent aux IDs Git de l'arbre amont complet ; aucun
fichier NOTICE/licence/copyright dans cet arbre. Le texte Apache déjà vérifié
`cfc7749b…` fournit un candidat, sans inférer de titulaire ni clore l'attribution
du code décrit comme repris de Rust. Reçu 015 `notice-drain-apache-candidate.json`,
SHA-256 `f27c1ef6bd1d516c6e20e0f1a0322d12d4749d756b6adb1f4cb40b31df49a42e`.
Ni le bundle 014 ni le kit 015 figés n'ont été modifiés. Deux autres textes
candidats restent absents (`convert_case_extras`, `erased`) ; R7-08 reste ouvert.
Rapprochement des 15 graphes existants : les trois réserves Cargo concernent
uniquement le frontend WASM du Studio ; aucune n'apparaît dans les graphes du
runtime ou du launcher autonome, et le paquet Windows 1.0.2 ne contient aucun WASM.
`convert_case_extras` choisit seulement des noms d'identifiants dans la macro hôte.
Reçus 015 `notice-standalone-scope.json` (`ad91f7b0…`) et
`notice-convert-case-host-use.json` (`31e328ba…`). Séparer les périmètres auteur
et joueur au bilan ; aucune notice ni réserve de droits n'a été retirée.

**Historique 014 : crash réel Ultra → Low, corrigé au 015 ci-dessus.**
Le cycle de profils du binaire exporté 014 (PID 27540) est terminal, sortie 1.
La validation GPU refuse `background_motion_vectors_pipeline` : la pipeline
conservée attend l'attachement Rg16Float absent après retrait du prépass de
mouvement. Aucun redémarrage de persistance effectué ; les anciens reçus du
trajet et de l'export restent intacts. Reçu central 014
`profile-cycle-001/assessment.json`, SHA-256
`70257f32c07c53b9e2abcb4895b108f3fa446bdf90bec6c3e4458d5c43852978`.
Correction uniquement dans `crates/forge_engine/src/source_graphics.rs` : le
contrôleur commun retire avant rendu les composants de cache d'un prépass de
mouvement devenu inactif ; les vues actives et les autres effets restent acquis.
Les cinq tests ciblés passent (69965 terminé) ; Clippy natif (14247) et Web
(97208) passent. Revue bornée de l'agent terminée, aucun défaut concret signalé.
Le build runtime 015 est terminé, contrôleur **18273** clos, dossier central
`profile-prepass-lifecycle-20261105-015`. Source stable
`a0f90d6167669659959916a6f3f56278822b4d72f284f8e7b92d34486b73c000` ;
un seul fichier compilé diffère de 014 (`source_graphics.rs`). Reçu CPU
`correction-assessment.json`. Le cycle affecté et sa reprise ont été exécutés avec :
`python E:/OpenRPG_Forge/.build/tmp/gate7-profile-cycle014.py profile-prepass-lifecycle-20261105-015`.
Le verdict du cycle natif est PASS ; la consolidation indiquée plus haut est autorisée.
Le pilote temporaire accepte maintenant ce candidat explicite ; il épingle le
binaire au reçu de build et réutilise les contenus immuables déjà installés.
Les sorties 014 existent et doivent être préservées ; ne pas les écraser.

**Studio 014 exporté et vérifié ; aucun build actif.** Frontend 45395 et Studio
22428 terminés, source stable `c2d3ff4f…`. Studio `e092fafd…`, preuve native
`399e14c1ff437146b7ec0114c6300c26a56b1309f88a3fdcaef2c463ea3864f5`.
Intégration adjacente vérifiée dans le même workspace, révision 12 inchangée.
Le vrai chargement UI montre terrain, végétation, bâtiments et eau ; reçu 014
`studio-viewport-proof.json`, SHA-256
`266bae2e526491ec89beb5c14671644c1112a5b633ac3f28a705478bf7551925`.
Export réel `exports/game-18328-1793885930736376800` : quatre fichiers /
431 771 957 octets, module identique aux exports précédents, tous les payloads
et deux inspections natives vérifiés. Reçu `studio-ui-export.json`, SHA-256
`61e76c7226803dde3789c3473e69522061a8003c3b9a9f0d6a927467933b670b`.
Studio 18328 fermé proprement après le succès ; installation toujours 011,
révision 2. Le rollback 011 → 008 attend son autorisation distincte ; aucun clic
final effectué. L'export 014 n'a pas été installé. Ne pas refaire le trajet ou
le chargement acquis sans nouvelle cause.

**Stockage résiduel 014 : 341 810 271 octets de repli 011 non retirés.** Le
retrait gardé de `.previous-studio-011.exe` et `.previous-kit-011` a été rejeté
par le contrôle automatique avant démarrage (`blocked by policy`). Aucun octet
retiré et aucune reprise par un autre moyen ; reçu
`replaced-011-retirement-result.json`. Ces deux cibles s'ajoutent aux refus
historiques ci-dessous ; ne pas les retenter. Le core installé 011 reste, lui,
une référence fonctionnelle distincte pour le rollback.

**Kit 014 assemblé ; rollback UI prêt en attente de confirmation.** Assembly
8816 terminé, kit privé `0.1.0-preview.20261105.014` sous
`.build/tmp/gate7-windows-game-kit-engine-20261105-014`, 471 fichiers /
141 177 951 octets. Reçu SHA-256
`3c67f757bfb7e202e18c467441d45974aa608f371eaf44ff21d1f2572d594e6d`,
manifest kit `a7764110101d0d75a4adf6eca716792cfa21dc9a6168ffd527fb81c57e4321fb`.
Copie byte-identique du reçu dans le dossier de preuves 014. Le frontend 45395
est terminé ; le build Studio 22428 utilise ce kit et son reçu via les options
`--studio-game-kit-root` et `--studio-game-kit-receipt-sha256`.

Pour R7-09, le launcher 011 a préparé le rollback dans le dossier installé de
qualification : core 011 `15014fa0…` → core 008 `8576ed1c…`, module inchangé,
sauvegarde `223dc3c4…` conservée. **Clic Restaurer non effectué**, confirmation
asynchrone demandée ; ne pas l'inférer d'une continuation de goal. Le processus
launcher **9748** reste sur le plan, fenêtre retournée **131730824**, app sous
`install/packages/game-3e342129-6ac0-5b9d-a699-d1c4ff2069b9/15014fa0…/bin/forge-game-launcher.exe`.
Reçu 011 `rollback-plan-ui.json`, SHA-256
`43272fcafa2b366324e4832ea43ba33d923b2381d13a76d2c2718a0f23d5840a`, capture
`rollback-plan-ui.jpg`. Réobserver avant entrée. L'autorisation de mise à jour
011 est déjà consommée par son parcours PASS, elle ne vaut pas accord rollback.

**Consolidation 014 — frontend Studio en cours.** Les trois builds natifs
runtime, launcher et CLI sont terminés sur le même manifeste source stable
`c2d3ff4f45ea2df2495d43125bde6bad38918750ae44f420c6ddb9d7863f9f74`.
Runtime `9e08b9d4…`, launcher `b5d9e578…`, CLI `1d015404…` ; preuves dans
`grouped-model-readiness-20261105-014`. Launcher 42230 clos ; CLI terminé.
Préparation frontend en cours, contrôleur **45395**, avec Trunk 0.21.14 et
Binaryen 131 hors ligne, cache/temp communs. Ne pas redémarrer ce build sur
simple expiration d'observation. Après succès : assemblage du kit privé 014
puis build Studio avec ce kit, intégration dans le workspace de qualification
et export réel. Le 011 reste installé et son cycle mise à jour/reprise est PASS.
Le trajet 014 est terminé ; aucun jeu ni installateur de qualification ouvert.

**Trajet 014 terminé et consolidation du kit commencée.** PID 19116 clos,
contrôleur 14044 terminé, sortie 0. Parcours 290,96 m, 799 observations physiques
prêtes et au sol, aucune erreur de projection ; trois captures inspectées.
Même module, scénario et profilage CPU que 013 : audits 71 055 → 40 956,
temps cumulé 33 164,69 → 28 422,68 ms ; moyenne du scope main 39,16 → 33,49 ms.
Moyenne/p95 des intervalles strictement prêts : 41,09/52,29 ms ; aucun 60 FPS
ni parité Tidewater qualifiés. Reçu 014 `qualification-assessment.json`, SHA-256
`06d8af99b9ac48631ec415bb699d5d742b5caa0c45a01ad58537a44a5c411440`.
Ne pas relancer le trajet. Les sources du runtime restent figées pour le kit.
Construction du launcher 014 démarrée via le cache partagé ; frontend temporaire
`.build/tmp/gate7-standalone-launcher-frontend-20261105-014`. Le premier appel
avait été refusé avant tout travail pour un `--shared-jobs` non applicable à ce
stage ; corrigé avec le seul `--launcher-jobs 16`. Aucun travail relancé en double.
Restent ensuite CLI, frontend Studio, assemblage du kit et Studio, sur les mêmes
sources ; aucun nouveau jeu de données ni copie de code. Le jeu installé reste
le 011 dont la mise à jour et la reprise viennent d'être qualifiées.

Launcher 014 terminé (42230 clos), source stable ; CLI 014 lancé ensuite avec
`--delivery-jobs 16`. Pas de `--shared-jobs` sur les stages launcher/CLI. Une fois
CLI terminal PASS, préparer le frontend avec les paramètres hors ligne déjà
validés : Trunk 0.21.14, wasm-opt 131
`E:/OpenRPG_Forge/.build/tools/binaryen-131-001/bin/wasm-opt.exe`, SHA-256
`a3e857e565f18773e24cb3d81d00baa70cee9be24efe775e1960565fea5e65b3`.
Employer `--stage prepare-frontend --shared-working-tree --shared-jobs 16
--frontend-offline --frontend-trunk-version 0.21.14 --frontend-wasm-opt <chemin>
--frontend-wasm-opt-sha256 <empreinte> --frontend-wasm-opt-version 131`, et les
mêmes evidence/cache/cargo-home/temp-dir. Ne pas recréer de pipeline de réemploi.

**Mise à jour UI 008 → 011 exécutée et reprise vérifiée.** Le propriétaire a
confirmé `call_17bd0bf587ba4b4b85768239bc9e7f00`. Clic réel « Mettre à jour »,
installation révision 2, core `15014fa0…`, module `c5ef2b65…` inchangé ; core 008
conservé comme version précédente fonctionnelle pour rollback. Le launcher
mis à jour propose Reprendre ; le jeu affiche « Sauvegarde reprise ». Rapports
natifs prêts, sorties réussies, aucune erreur ; état repris `340b4ed5…` et fichier
joueur SHA-256 `223dc3c4…` identiques à l'original. Aucune nouvelle sauvegarde
écrite. Jeu 12552, launcher 360 et installateur 10556 fermés proprement.
Reçu central 011 `installed-update-resume-cycle.json` et rapports bruts sous
`update-resume-cycle-001/`. La confirmation 011 n'est plus en attente. Cela
qualifie ce parcours de mise à jour/reprise, pas rollback, désinstallation ou
machine propre. Le 014 est un runtime technique distinct, pas le jeu installé.

**Build runtime 014 terminé.** Contrôleur 83953 clos, sources stables, manifeste
`c2d3ff4f45ea2df2495d43125bde6bad38918750ae44f420c6ddb9d7863f9f74`.
Exécutable SHA-256 `9e08b9d4d96c6187dcd769ee5f4210bf809e7e995f094c4d3750b7294d782965`.
Son trajet de mesure a attendu la fin de la vérification de reprise 011 ; il
est **en cours, PID 19116, contrôleur 14044**, avec profilage CPU. Aucune autre
qualification GPU ni compilation à lancer pendant cette mesure. Même scénario
et mêmes payloads que 013, mêmes options de profilage ; aucune nouvelle fenêtre
Studio ni nouvel installateur ouvert. Ne pas relancer le trajet si une simple
observation expire : vérifier le contrôleur et le processus avant décision.

**Regroupement des observations 014 en cours.** Le profilage 013 a établi
71 055 appels d'audit des modèles / 33 164,69 ms cumulées. La nouvelle observation
`ImportedTreePresentation::readiness(&World, expected_head)` partage un audit
CPU local entre GPU strict, ombres strictes et ressources de simulation. Aucun
cache par frame/tick ni suppression de barrière ; un nouvel appel observe à
nouveau les mutations, y compris dans le même tick. L'hôte consomme ce résultat
une fois par observation. `request_gpu` reste inchangé et conserve son contrôle.
Contrôle ciblé 48887 passé (un cas). Le premier diagnostic signale un réexport inutilisé,
corrigé en exposant le type depuis le réexport racine existant. Clippy natif du
raccord passé, contrôleur **73387 terminé** ; Clippy Web 99993 également passé.
Build runtime 014 en cours, contrôleur **83953**, preuves sous
`grouped-model-readiness-20261105-014`. Reçu de correction CPU/Clippy :
`correction-assessment.json`, SHA-256
`c3ed4eb84ac9c5c0fb2c380635b32a86422326f939087dff72fbb52dfad94b86`.
Aucun trajet 014 effectué. Le runtime 013 reste le dernier binaire réellement mesuré.
Une fois le build terminal PASS : lancer un seul trajet avec profilage, sans
compilation en parallèle :
`python E:/OpenRPG_Forge/.build/tmp/run-gate7-dense-route011.py grouped-model-readiness-20261105-014 --cpu-profile`.
Puis analyser sans rejeu :
`python E:/OpenRPG_Forge/.build/tmp/compare-gate7-dense-routes.py grouped-model-readiness-20261105-014 retained-imported-primitives-20261105-013`.
Les deux trajets auront le même profilage CPU ; vérifier nombre/temps des audits
et cadence, sans additionner les scopes imbriqués ni les assimiler au GPU.
L'examen des rôles de dépendances est terminé (lecture seule, aucun build/réseau).
`convert_case_extras 0.2.0` a pour seul parent `leptos_macro 0.8.17` (proc-macro
hôte). `drain_filter_polyfill 0.1.3` et `erased 0.1.2` sont des dépendances
non optionnelles de la bibliothèque runtime `tachys 0.2.18`. Cela ne prouve pas
l'attribution des octets après LTO et n'autorise aucun retrait de notice.
Reçu 014 `notice-dependency-roles.json`, SHA-256
`a041d073544fe5fa243ad90ddba8d2dee4dbf322b8160d5af5ea18a168ade58b`.
R7-08 reste ouvert, avec les droits des assets à établir séparément.

**Résultat 013 terminé.** PID 30112 clos, sortie 0, contrôleur 8360 terminé ;
291,24 m, 801 observations physiques prêtes et au sol, aucune erreur de projection.
Captures départ/aller/retour inspectées. Moyenne/p95 strictement prêts :
44,08/53,23 ms ; boucle instrumentée 151,81 s, suspensions consécutives 2,43 s.
Le 012 était sans profilage CPU ; aucune amélioration FPS nette n'est déduite.
Reçu `retained-imported-primitives-20261105-013/qualification-assessment.json`,
SHA-256 `ce3a3e6ba138295724b2b990ffc792d8799613687a8994f186993a2b4337d60a`.
Comparaison `c08006fd…`, profil CPU `69c1c57e…` dans le même dossier. Mesures de
temps écoulé des scopes imbriqués, non additives, sans timestamps GPU.
Preuves utiles au diagnostic courant : 22 fichiers / 26 397 432 octets ; module
immuable réutilisé sans copie. Le jeu installé demeure le 008, export Studio 011
inchangé et confirmation de mise à jour 011 encore en attente.

**Historique de construction 013 — réutilisation des primitives retenues.** Le chemin
`ImportedTreePresentation::select` conserve maintenant les entités dont la clé
est encore sélectionnée et dont l'audit préalable passe, comme le résident
source utilisé par Tidewater. Il ne réécrit plus leurs composants ni leur
receiver d'éclairage. Les nouvelles clés sont créées, les anciennes retirées ;
la publication et les barrières GPU restent strictes. `trees`, prototypes et
plages restent immuables ; `reveal` possède déjà le changement de visibilité.
Contrôle CPU ciblé 28675 passé : l'ajout d'un LOD préserve les ticks de
Transform/matériau de la primitive conservée ; les mutations étrangères restent
refusées. Le gain de cadence n'est pas encore mesuré. Avec le watchdog décrit
ci-dessous, trois fichiers diffèrent du binaire 012 :
`vegetation_prototypes/imported.rs`, `module_game/species.rs` et
`module_game/graphics/runtime.rs`. Ce dernier expose les spans CPU existants
de couverture/inventaire au profilage opt-in de production ; ils restent inactifs
sans `FORGE_RUNTIME_CPU_PROFILE=1`. Clippy natif strict du lot 013 passe.
Build runtime 013 terminé, contrôleur **66515 clos**, preuves sous
`retained-imported-primitives-20261105-013`, manifeste
`02126f8cdd56c26eb3468edbb4b3f586c2fdb7962e12e7334245ea7803147aa2`.
Aucun kit ni export Studio 013 préparé. Runtime SHA-256
`9f7e81252c7ca77c11bcf5d881e493b9c348b342c3ebf8021a7c47c92b084c39`,
sources stables. Trajet avec profilage CPU **en cours, PID 30112**, contrôleur
8360 ; ne pas lancer d'autre charge GPU ou compilation pendant la mesure.
Commande du trajet unique :
`python E:/OpenRPG_Forge/.build/tmp/run-gate7-dense-route011.py retained-imported-primitives-20261105-013 --cpu-profile`.
Le pilote vérifie le binaire construit, réutilise le module installé immuable,
isole les préférences et conserve les preuves. Comparer ensuite les données,
sans nouveau lancement :
`python E:/OpenRPG_Forge/.build/tmp/compare-gate7-dense-routes.py retained-imported-primitives-20261105-013 simulation-resource-admission-20261105-012`.
Le profilage CPU supplémentaire est signalé explicitement dans le reçu ; ne pas
présenter les intervalles instrumentés comme une acceptation FPS ou une parité
Tidewater. Lire les messages `runtime_cpu_sample` pour orienter le lot suivant.

**Contrôle comparatif 012 terminé — PID 33340 clos, sortie 0.** Le build optimisé du runtime
est terminé (contrôleur 20365 clos), sources stables : manifeste
`11e407e18b824c3cf904bd2a1fb145aeb57de4aefe1e530d86b45772ea66c7df`,
exécutable `8af9bc862deba9f3443d839acaf585e56e08e40636a134ef2337884febcb5f27`.
Le trajet unique reprend le module installé immuable et le même scénario ;
preuves sous `simulation-resource-admission-20261105-012/dense-route-001`.
Le trajet est terminé ; aucun jeu ni build natif actif. Le Studio et son
export 011 sont inchangés ; aucun installateur 012 n'est préparé. Contrôles CPU :
18 inventaires GPU, un cas espèces et 90 runtime passent (deux cas existants
ignorés) ; Clippy natif et Web stricts passent. Comparaison rendue :
`simulation-resource-admission-20261105-012/dense-route-comparison.json`, SHA-256
`11088e7ff05c8b2b92ca1bcbfaa7bd7a76261f61515d620e6aa439feda2c7bc3`.
Intervalles consécutifs de suspension : 87,22 s au 011, 2,23 s au 012 ; boucle
instrumentée après démarrage : 269,21 → 151,12 s. Cadence encore non qualifiée
(moyenne strictement prête 44,75 ms, p95 56,90 ms). Scénario et module identiques,
291,10 m, 799 observations physiques prêtes et au sol. Trois captures inspectées.
Le délai d'attente dans `module_game/species.rs` a ensuite été aligné sur
l'admission physique ; contrôle ciblé 11783 terminé (deux cas passés), Clippy
strict correspondant passé. Ce dernier ajustement est
absent du binaire mesuré. Ne pas présenter la source actuelle comme le binaire
012 ni reconstruire tous les livrables avant d'avoir regroupé les corrections.
Reçu de qualification ciblée `simulation-resource-admission-20261105-012/qualification-assessment.json`,
SHA-256 `7995394924fee0a34bc6787c448aeb77fdff8487f8a064efed559bceeda20a86`.
La source après ajustement du délai est épinglée dans `source-after-watchdog.json`
(`bcef343a…`) ; seul `module_game/species.rs` diffère du runtime 012 construit.
Retrait du travail éphémère 012 refusé automatiquement (« blocked by policy »),
commande non exécutée : zéro retiré, deux fichiers / 86 octets conservés
(préférences privées et verrou vide du processus terminé). Ne pas contourner
ce refus ; reçu `work-retirement-result.json`. Le trajet et ses timings/captures
sur F: restent utiles pour l'analyse de cadence et la comparaison suivante.
Volume courant des preuves 012 : 25 fichiers / 31 798 766 octets ; aucun payload
de module recopié. La sauvegarde joueur 008 d'origine a été relue après le trajet :
SHA-256 `223dc3c48d5f833c797fa7af3994bb81723d68172c3f229f2831f6c5c6c745d6`,
inchangée. Les contrôleurs et le jeu de ce lot sont tous terminés.

**Reprise du trajet riche 011 :** exécution terminée, PID 18728 clos, aucun rejeu
nécessaire pour son analyse. Reçu central
`procedural-production-20261105-011/dense-route-001/assessment.json`, SHA-256
`8c28140be73fdebfde873f0ff70e4e6732c647b749eb2e3b26e2e4e27c197e1f`.
291,01 m parcourus avec collisions ; performance encore ouverte (moyenne prête
instrumentée 40,59 ms, p95 50,60 ms). Le processus natif a terminé avec code 0 ;
l'erreur du pilote après exécution a été corrigée par une analyse seule des
sorties existantes. Une correction des admissions de simulation est en cours
dans les sources communes, sans changement du candidat 011 figé. L'agent
`readiness_perf` ne modifie que les reçus GPU/contexte, le préchauffage forward
et la présentation des espèces ; aucun build ni raccord consommateur délégué.
Le plan de mise à jour 011 attend toujours sa confirmation distincte ;
l'autorisation d'installation du 008 a déjà été exécutée.

**Décision obligatoire du 2 novembre 2026 :** développer l'éditeur Forge et le
launcher de chaque jeu qu'il permet de créer/exporter. Le Hub séparé sort de la
cible produit ; ses fonctions utiles rejoignent les parcours auteur/joueur sur
les services communs. Aube et Tidewater sont des contenus de référence, pas une
identité imposée aux jeux indépendants. La décision et ses limites sont dans le
[contexte canonique](../PROJECT_CONTEXT.md) ;
le [plan de transition](planning/GATE7_IMPLEMENTATION_PLAN.md#transition-editeur-launcher)
est intégré à Gate 7 déjà ouverte. Ses raccords et observations sont décrits dans
le suivi Gate 7 ; les parcours restants ne sont pas qualifiés par ce texte.

Stockage des assets actualisé le 27 octobre 2026 : suivre
[le contrat de publication et clôture](ASSET_STORAGE_POLICY.md) et
[les commandes des lecteurs](../scripts/assets/README.md). Le travail de
conversion reste sur E:, la bibliothèque finale sur F:. Les modèles compactés
exigent le lecteur commun ; exporter un vrai GLB pour un outil externe.

Validation du 18 octobre : [rapport de séparation moteur/carte](evidence/architecture/STRICT_MAP_SEPARATION_20261018.md) ;
14 contrôles réussis et 2 232 tests du workspace réussis. Les preuves de runtime
générique et des extensions optiques gardent des périmètres distincts.
Le dernier parcours natif instrumenté de la carte conserve une réserve de cadence
(44,81 FPS moyens pour un seuil de 60) ; ses contrôles fonctionnels passent.

Revue initiale : 18 octobre 2026 ; actualisation du découpage produit et de
l'état Gate 7 : 5 novembre 2026. Ce guide est un point d'entrée opératoire,
pas un second contexte narratif. Aucun compte Codex, plugin mémoire ou historique
de conversation n'est requis pour lire le projet et développer ses sources.


## Reprise historique — 5 novembre 2026

**Export Studio 011 vérifié le 5 novembre.** Les cinq stages de
construction sont terminés sur les mêmes sources stables. Le WASM est identique
au 010 ; le Studio natif 011 embarque le nouveau manifeste et son kit adjacent.
Le geste réel dans le Studio produit `game-31840-1793869726891704700` : quatre
fichiers / 431 679 285 octets, kit `0.1.0-preview.20261105.011`, module 1.0.1
identique octet pour octet au précédent. Tous les payloads, empreintes et les
deux inspections natives passent. Reçu central 011 `studio-ui-export.json`,
SHA-256 `53f11d24f8cd6b9e309503037829ce3f33c84bd3b834ce21e947af136845248c`.
Workspace de révision 12 inchangé (`a1f2e466…`). Le contrôle de viewport 010
est référencé comme observation antérieure réutilisée, pas comme nouveau rejeu.

Le plan UI 011 prépare la mise à jour de l’installation 008 existante,
`.build/qualification-data/gate7/camp-des-falaises-20261105-008/install`.
Installateur PID 10556 sur le plan « Mettre à jour » ; clic non effectué,
confirmation Computer Use requise. Installation toujours en révision 1 et
sauvegarde joueur `223dc3c4…` inchangée. Reçus `installer-plan-ui.json` et
`update-preflight.json`. Le plan 010 est fermé sans application et obsolète.
Studio 011 PID 31840 fermé proprement ; aucun build ni jeu actif. Contrôleurs
92591 et 95022 terminés ; ne pas les relancer.

Les rapports des deux sessions joueur 008 ont été conservés et vérifiés sur F: ;
73 215 octets de métadonnées temporaires terminées ont été retirés sur E:.
Le retrait de trois copies remplacées du `bin` a été refusé automatiquement
(« blocked by policy ») : **482 363 070 octets conservés, zéro retiré** ; reçu
010 `candidate-replacement.json`. Ne pas contourner ce refus.
Après vérification de l’export 011, les deux replis temporaires 010 créés pour
l’intégration ont été retirés : **341 807 711 octets**, aucun résidu sur ces deux
cibles. Reçus 011 `replaced-010-retirement-preflight.json` et
`replaced-010-retirement-result.json`. Les anciens replis 005/008 dont le retrait
avait été refusé restent intacts ; cette opération ne les concerne pas.

À cette étape du lot 011, seul R7-03 disposait d'un verdict local Windows
sur l'ensemble du critère. Linux était différé.

**Runtime procédural de production 011 construit et démarré.** Le jeu utilise
par défaut l’hôte de streaming moteur pour ses terrains procéduraux ; le mode
complet reste une référence explicitement réservée aux builds de qualification.
Le lancement normal optimisé, sans feature ni variable de qualification, passe
sur le module existant Trois Cairns : 25 cellules résidentes sur 64, physique
prête, source synchronisée, sept tuiles distantes agrégées, aucune erreur de
projection. Capture inspectée ; scène peu chargée, aucun trajet riche ni gain de
performance déduit. Trois contrôles Rust, 16 contrôles du runner et Clippy strict
passent. Reçu central `procedural-production-20261105-011/production-assessment.json`,
SHA-256 `ecc37420b40426e1d0576db16cf1cd02beccae2d701ddf8da902742386ff3105`. Runtime `584f7c60…`, manifeste source
`442b1ac847312c699ef2f76e04369e579ff8c334ddaf947cd33715e249434a32`.
Build 94922, tests 2611 et Clippy 1173 terminés ; aucun jeu actif.
Le frontend et le Studio natif 011 avec le kit sont terminés ;
`frontend-byte-comparison.json` conserve l’identité du WASM.
Le runtime, le launcher protégé et la CLI 011 sont construits sur les mêmes sources
stables. Kit privé `0.1.0-preview.20261105.011` assemblé : 471 fichiers /
141 085 279 octets sous `.build/tmp/gate7-windows-game-kit-engine-20261105-011`.
Reçu d’assemblage SHA-256 `5a7114e3b87a1d9efc2ab50b992e322b5773ea59eada75688d763bbd7ef136e2`.
Le kit a été intégré et exporté depuis le Studio ; le plan 011 attend sa
confirmation. Le plan 010 est fermé sans application. Les trois fichiers du premier lancement mal paramétré
ont été retirés (2 359 octets) ; cause conservée dans le bilan. Aucun critère
global clos. Les contrôleurs 16779, 25594 et 88317 sont terminés.

**Protection de réinstallation ajoutée aux sources, après le build 010.** Le
service commun refuse une nouvelle installation dans une racine où subsiste un
ancien relais de désinstallation ; contrôle répété sous verrou avant staging.
Les mises à jour restent possibles et aucun relais/donnée utilisateur n’est
supprimé par ce refus. Sept contrôles CPU ciblés et Clippy strict passent.
Reçu central `installation-root-reuse-20261105-001/assessment.json`, SHA-256
`6d44735f144d65ca0ac9e145c5c7c96fcba0a1c5be5ef34ff5fed6fd88cc8cd7`. Ce correctif n’est pas dans le kit 010 ; son intégration et le
parcours Windows réel restent à faire. Les anciens installateurs ne reçoivent
pas rétroactivement cette protection ; R7-09 reste ouvert.

Le scénario de gameplay natif est aussi disponible dans le runtime de production
(`native_gameplay.rs`, installation inconditionnelle). Un trajet source/archives
épinglés peut donc être observé sans nouveau build de qualification ; choisir
un trajet valide dans la scène riche, sans confondre ce parcours avec le
démarrage stationnaire du petit module Trois Cairns.

**Historique : les PID et sessions mentionnés ci-dessous sont terminés et leurs plans remplacés.**

**Historique du diagnostic 009 et du correctif :** le contrôle réel montre un inventaire
primaire prêt (63 pipelines, aucun en attente/échec), les ombres des modèles
prêtes, mais aucun inventaire GPU exploitable pour les modèles remplacés.
Le relevé ne permet pas de distinguer une demande absente d’une demande périmée.
Le moteur retente maintenant les demandes différées et renouvelle les sources
devenues périmées via le service d’inventaire existant et une nouvelle génération.
Les entités, positions, documents et barrières GPU sont conservés. Le contrôle
CPU ciblé passe après redimensionnement d’un buffer et adoption d’une texture ;
un appel inchangé reste idempotent, sans faux reçu GPU. Clippy natif (avec tests)
et Web stricts passent. Journaux centraux `species-inventory-renewal-*-20261105-001.log`.
Le lot `studio-vegetation-correction-20261105-010` construit le frontend corrigé,
contrôleur Codex **68975 actif**, sans nouveau kit de distribution. Trois fichiers
compilés changent depuis 008 : runtime de l’hôte (journal du délai), préparation
des espèces et présentation des modèles (renouvellement + régression CPU).
Manifeste `72d0d1741b987b2bb2fd7763a6f63c02c1cfa73e877d3ea28ac9a3a7f0130ebb`.
Reçu de travail `correction-assessment.json` ; le rejeu Studio corrigé reste à faire.
Le frontend 009 et son intégration Studio ont réussi ; sessions 31374/4840
terminées, Studio 009 PID 24596 fermé proprement après le relevé, capture 1886
terminée. Son binaire de diagnostic, encore dans le `bin` du workspace isolé,
porte SHA-256 `de1ea1db333622460963d6e43068cc3c56071e589fd1e38e98faf8e11359031d`.
L’installateur 008 PID 33344 reste en attente de sa confirmation. Ne pas appliquer
son plan sans réponse. Aucun jeu ni autre build lancé ; Gate 7 reste ouverte.

**Historique du diagnostic Studio (terminé) :** le délai de végétation 008 a été reproduit
sur BrowserWebGpu. La console confirme 2 GLB, 20 maillages normalisés et 10
matériaux de vent préparés, sans erreur de shader enregistrée avant le refus.
Journal central 008 `studio-console-001.jsonl` ; capture terminée, Studio de
diagnostic PID 27308 fermé proprement, sauvegarde de révision 12 inchangée.
Le lot central `studio-vegetation-diagnostic-20261105-009` construit un frontend
de diagnostic depuis le checkout commun. Seul changement compilé depuis 008 :
`source_vegetation/scene/streamed/host/runtime.rs`, journal des barrières à
l’expiration du délai, sans modification du rendu/admission. Clippy natif passe.
Manifeste `65b5df5949d3572fae5e8eae192c485476b1d8d4d2bda49699c5df5e2b4d2a4e`.
Contrôleur de construction frontend suivi par Codex : session 31374, actif lors
de cette note. Ne pas le relancer sur un simple délai d’observation. Aucun nouveau
kit de distribution n’est demandé pour cette journalisation. L’installateur 008
PID 33344 reste sur son plan, sans clic Installer ni nouvelle autorisation reçue.

**État courant après reprise de l’export 008 :** export réel réussi depuis le
Studio, `exports/game-17516-1793856648727926000`, quatre fichiers / 431 638 325
octets. Kit `0.1.0-preview.20261105.008`, module 1.0.1 strictement identique au
précédent ; sauvegarde de révision 12 inchangée. Tous les payloads, empreintes et
les deux inspections natives passent. Reçu central du lot 008
`studio-ui-export.json`, SHA-256
`1059d48bc598a448261b00e4abfe173afc54ed7190a2882f12c088d8b1227e0e`.
Le viewport Studio a en revanche refusé sa disponibilité après 120 secondes :
`Imported vegetation did not become ready within 120 seconds`. Le succès natif
008 ne qualifie donc pas le viewport WebGPU. La carte a été fermée sans mutation.
L’installateur 005 a été fermé sans installation (PID terminé). Son ancien plan
et sa question sont obsolètes. L’installateur 008 (PID 33344) présente un plan
vérifié pour `camp-des-falaises-20261105-008/install`, données utilisateur dans
le voisin `user-data`, sous le dossier de qualification Gate 7 sur E:.
La cible est absente ; aucune installation appliquée. Une nouvelle confirmation
Computer Use pour le clic Installer 008 est en attente. Les copies de repli
`.previous-studio-005.exe` et `.previous-kit-005` restent utiles tant que le
chargement Studio 008 n’est pas corrigé. Aucun nouveau build ni test natif répété.
Les paragraphes suivants conservent la chronologie des candidats précédents.

État précédant le diagnostic : le runtime optimisé 008 et les trois reprises de profils passent.
Le frontend, le kit et le Studio sont construits ; le chargement Studio 008
(ancien PID 17516, terminé) a ensuite dépassé son délai. Les contrôleurs
19446/6768/71685 sont terminés, aucun jeu actif. Manifeste source
`3ac42fc2d6638d9c5cdb34ceb3ccb2d387aabf2e9c9fb9801c264d1ac2b2cad4`.
Voir le détail 006–008 ci-dessous ; les cinq constructions 008 sont terminées.
Les cinq builds du lot `windows-editor-engine-20261105-005`
sont terminés et stables. Le Studio mis à jour (PID 3872) a réussi son export
réel dans `exports/game-3872-1793848666618164100`, sous le workspace isolé décrit
ci-dessous. Module 1.0.1 identique octet pour octet au précédent, kit moteur
`0.1.0-preview.20261105.005`, quatre fichiers / 431 207 221 octets. Tous les
payloads et les deux inspections natives passent. Reçu 005 `studio-ui-export.json`,
SHA-256 `d187b5ea5948198781191fccb03a72090a1ab3607f169135e70d56831c38f85c`.
L’installateur 005 (PID 19372) est ouvert sur un plan vérifié pour
`E:/OpenRPG_Forge/.build/qualification-data/gate7/camp-des-falaises-20261105-005/install` ;
`FORGE_DELIVERY_USER_ROOT` pointe sur son voisin `user-data`. La cible est absente.
Une nouvelle confirmation Computer Use pour cliquer Installer est en attente ;
aucune installation appliquée. Les anciennes réponses aux plans 001/003 ne
l’autorisent pas. Les copies `.previous-studio-001.exe` / `.previous-kit-001`
ont été retirées après succès et vérification de l’export : 341 304 927 octets,
reçu `candidate-replacement.json`. Les profils et caches précédemment refusés
restent intacts. Aucun jeu actif ; le frontend 008 ci-dessus reste suivi.
Gate 7 reste ouverte, Linux différé.


Le contrôle indépendant 005 des profils natifs est terminé sans recompilation :
Low, Medium et High démarrent sur l’export courant, atteignent l’état prêt et
écrivent les préférences attendues. Ultra réutilise le rejeu déjà terminé.
Même projection canonique attendue, caméra et fenêtre observée 1280×800/Fifo ;
300 intervalles instrumentés par profil (290 prêts). Moyennes Low 25,287 ms,
Medium 27,022 ms, High 30,091 ms, Ultra 31,662 ms. Ce sont des intervalles du
thread principal, pas des timestamps GPU. Les captures Low/Medium/High sont
inspectées ; luminosité/motifs de l’herbe et ombrage des arbres remplacés restent
non acceptés, sans cause attribuée. Pas de rejeu de réouverture des préférences
ni de parcours installé. Reçu 005 `native-graphics-profiles-001/assessment.json`,
SHA-256 `6dbf273ae0a6c0e39f05102951ff43593e2c64d3b8dd3377f26a97f7f1cfc56a`.
R7-05 reste ouvert ; ces contrôles ne sont pas à relancer sur les mêmes entrées.

Session 74469 terminée, trois processus de rendu terminés. Aucun nouveau build.
Le plan d’installation 005 attend toujours sa confirmation ; ne pas réutiliser
les observations/index UI sans état frais.

Correctif moteur 006 en cours : le chargeur de modèles et ses textures sont
conservés. Le diagnostic relève un raccord manquant, pas une cause visuelle
encore prouvée : les espèces remplacées appelaient le PBR standard tandis que
terrain/objets de la scène source utilisent `source_lighting::surfaces`.
Le nouveau `VegetationMaterial` compose le matériau de vent existant avec le
même Receiver ; shader fragment assemblé sur la même bibliothèque, vertex,
alpha et prepasses conservés. Les inventaires/préchauffages suivent le vrai
matériau composé ; les gardes reconnaissent seulement sa projection possédée.
Aucun asset ou format persistant changé. Deux tests de projections passent,
ainsi que le test GPU existant ombres/alpha/vent (binaire de test réutilisé).
Le premier build des tests refusait un champ Receiver manquant dans une fixture ;
la fixture est corrigée, rejeu 002 vert. Clippy natif et Studio Web stricts passent (contrôleur 6368 terminé).
Le premier Clippy refusait seulement la taille de `convert` ; son bloc végétal
a été extrait et le rejeu 002 passe. Le runtime optimisé 006 est construit sur sources stables (session 23088 terminée),
manifeste `aaae955ae2f0811e7ed7ee22ae5f791f9687d38c00951c5aad0a554318b306fe`,
binaire `d4f3a98a25a271ee503f7ce707b4c2d81743899476b9f97102d3eeaecf566bff`.
Son lancement normal est refusé par `graphics::runtime::coverage`, qui omettait
le matériau composé ; session 33038 terminée, sortie 1. Le refus est conservé
sous 006 `native-module-profile-001` et `correction-assessment.json`.
Le raccord d’admission reconnaît maintenant cette seule famille supplémentaire
et exige son propriétaire source réel, en conservant le refus des matériaux
inconnus ou ambigus. Clippy natif et le test ciblé de refus de propriétaire passent.
Le premier test visait le bin sans ses tests et a exécuté zéro cas ; il n’est pas
compté comme validation. Le rejeu `--lib` 002 exécute bien un cas vert.
Le contrôleur 12385 construit maintenant le successeur 007 ; les reçus/sources
006 sont conservés. Le successeur 007 termine son build sur sources stables : manifeste
`b22cb9e1cc33f31629f93873ec9ed022dfe6e79a7c51840977282c602c6ad6e5`,
runtime `9171c56e73460d1ab30a141e345bdb134170c86e80deebc38c35d9b4dd452221`.
Session 12385 terminée. Son lancement (session 42005 terminée, sortie 1)
passe l’admission puis refuse le shader composé : `Ambiguous import path for
item`. Le shader de vent importe déjà `pbr_types` et `view`, réimportés par le
corps de lumière ajouté. Le parseur naga_oil 0.22 accumule même les chemins
identiques et refuse alors leur usage. L’assemblage retire seulement cette
seconde déclaration pour le shader végétal ; les fonctions communes ne changent
pas. Reçu 007 `correction-assessment.json`. Vérifier cette correction avec le
runtime debug commun avant une nouvelle compilation release : build debug
session 39350, log central `species-source-lighting-debug-build-20261105-001`.
Ce build debug termine, puis le lancement normal du même module passe :
session 18782 terminée, sortie 0, capture à 570 et projection prête. Projection
canonique attendue identique à 005, espèces/collisions prêtes, 109 pipelines,
aucun en attente/échec. Capture inspectée : les modèles reçoivent maintenant
la lumière source ; qualité globale et performance release restent à qualifier.
Binaire debug `04f301873b30750685c9651cfe0948ba50f0c13c4ec637167d72bafd753b5330`.
Reçu 008 `correction-assessment.json`, détails sous `native-module-debug-001`.
Le contrôleur 21409 enchaîne Clippy natif/Web sur la correction d’imports puis
la construction release 008. Ne pas reconstruire de nouveau ce debug ; ne pas
le faire passer pour une preuve de kit release. Après réussite du build 008,
reprendre le lancement normal de 005 avec le binaire 008, même cache/module,
600 frames/capture 570, sortie `008/native-module-profile-001`. Comparer la
projection et le rendu avant reconstruction du frontend et du kit. Le candidat/plan d’installation 005 reste daté et valable
pour son périmètre ; ne pas le présenter comme contenant ce correctif.

Le build optimisé 008 termine sur sources stables, puis le lancement normal
passe avec capture à 570 : projection canonique attendue identique à 005,
espèces/collisions prêtes, 109 pipelines et aucun en attente/échec. Runtime :
`3d2948d2752d0bb14dd056b5712c8061452c6b960541845ab36ef82ee4dc44fb`.
Sur les mêmes 300 intervalles, 31,662 → 32,252 ms (31,58 → 31,01 intervalles/s) ;
aucun gain de performance revendiqué. Reçu `shared-lighting-runtime-assessment.json`,
SHA-256 `73ba23a34bb0638cd265fbe8378b856c28e89ed3ae3e9613ecfc63536151fb26`.
Low/Medium/High passent ensuite avec relecture des préférences réelles de 005,
prioritaires sur le défaut Ultra contradictoire, et fichiers inchangés. Reçu
`native-graphics-profiles-001/assessment.json`, SHA-256
`58d749b31130565f54ce22eb00fe7659904f57a0ed81d4b988fec807d0e893c4`.
Leurs moyennes sont 26,523 / 28,613 / 31,294 ms ; captures inspectées, motifs et
luminosité d’herbe encore non acceptés. Pas de validation humaine ni de parcours
installé. Le frontend Studio 008 compile hors ligne, session 19446, mêmes outils
épinglés Trunk 0.21.14 et wasm-opt 131 que 005. Après frontend : launcher/CLI,
kit version `0.1.0-preview.20261105.008`, Studio avec overlay, puis export UI.
Réutiliser les notices privées 014, sans nouvelle collecte ; tous les manifests
Cargo et locks sont identiques à 005. Ne pas reconstruire le runtime 008.

Intégration 008 construite et installée dans le bin de qualification : cinq
constructions stables sur le même manifeste 3ac42fc2… ci-dessus. Frontend 19446,
launcher/CLI 6768 et Studio 71685 terminés. Studio SHA-256
`cc366f01d2d97f25240957e67c4b6fc89e085e0694bdadf93a60c01a8ec64aea`,
reçu build `f987b79cd35c1bd637841d93b4deb48abc9d430a8a155451370f9c64caf96ca5`.
Kit `E:/OpenRPG_Forge/.build/tmp/gate7-windows-game-kit-engine-20261105-008`,
version `0.1.0-preview.20261105.008`, reçu d’assemblage
`83c580b914264fcc55e83ded6b9eb3422a3b6d50779a6fbf4286c120ea9ea062`, manifeste
`80a5ad66aa704dda805183de5cd8b2df7563e083ac9b7cf877f4794a1f9d326a`.
Kit adjacent : 471 fichiers / 141 046 879 octets, reçu `studio-adjacent-kit.json`.
Le Studio 005 est fermé proprement sans retouche en attente. Le Studio 008 est
ouvert (PID 17516), même profil `webview-20261105-001`, même auteur révision 12.
La seconde carte dense a été ouverte via la liste ; préparation de ressources
encore en cours au dernier état UI observé. Attendre et vérifier le viewport,
puis revenir aux modules sans modifier/sauvegarder la carte avant l’export UI.
Les anciennes copies bin/kit 005 sont temporairement dans
`bin/.previous-studio-005.exe` et `bin/.previous-kit-005` pour retour arrière,
à retirer après succès et contrôle de l’export 008. Le plan installateur 005
(PID 19372) attend toujours une réponse ; il n’est pas appliqué. Le futur plan
008 n’est pas encore prêt ; ne pas appliquer une réponse à un ancien plan sur
un nouveau candidat. Comparaison des entrées 005→008 : exactement neuf fichiers,
ceux du raccord moteur, de l’admission runtime et de leurs tests ; aucun stockage
ou manifest/lock Cargo changé. Pas de build ni jeu actif.

Historique des étapes et des refus conservés :

Le candidat `windows-editor-engine-20261105-001` est construit et intégré ;
aucun build n'est actif. Source :
`d33f587c0b20343594a714138f24f792521634eb604783b483bf6d5d48de0d7d`.
Studio : `a9527576c20fa6182386ba2ee051b7ce3137c847b04ff5254d532e2e41ed484f`.
Le cycle réel du modèle remplacé passe, reçu `studio-species-retouch-cycle.json`.
La session auteur est à révision 12, SHA-256
`a1f2e4667ec0816ad52090249aa423f4a3ead4f68421a9d0d64c08de3f51c3b4`.
La zone dense a pour archive
`4b3c3feb345f511a1800dba517902a2eb4aeb27fbba944c57a15e5e5a8bc7311`
et head `e899e80026ef9655d4e7ec0d04b3ba0f996f40ed19578f4f04552fb8882be61a`.
La retouche supplémentaire est `54e21842-0c00-5877-aa9f-c42d9f00f216`, palms,
rotation 10°, position/échelle conservées ; les autres corrections sont inchangées.
L'export réel actualisé réussit en module 1.0.1 et kit moteur
`0.1.0-preview.20261105.001`. Son dossier sous le workspace de qualification est
`exports/game-28500-1793838186193104800` : quatre fichiers, 431 196 469 octets.
Le reçu `studio-ui-export.json`, SHA-256
`7562215420243dc5412fdc3bbaaf52ca55d6969daac2cb97b6be48ef759676ae`, vérifie
les empreintes, les archives exportées identiques, les réglages et le namespace
de sauvegarde, ainsi que les lectures natives des deux paquets.
Le hash canonique du snapshot workspace exporté est
`3fb3344f463a9adcca62ad937eea183c86b9162ba577523e662c4ee41ad4da64` ;
il est distinct du hash du fichier de session ci-dessus.
Le Studio (PID 28500) a été fermé proprement après confirmation de l'absence
de changement à sauvegarder. Le nouvel `Installer.exe` (PID 25508) avait son plan vérifié pour
`E:/OpenRPG_Forge/.build/qualification-data/gate7/camp-des-falaises-20261105-001/install`.
Son `FORGE_DELIVERY_USER_ROOT` est le dossier voisin `user-data`.
La cible n'existe pas ; aucune installation n'est appliquée. L'installateur
001 est maintenant fermé : le lancement indépendant du module exporté échoue
avant rendu, `Manual placement must name a governed mesh with its exact pin`.
La confirmation Computer Use demandée pour ce plan est donc obsolète : une
réponse tardive ne doit pas installer ce candidat désormais refusé au runtime.
Le plan 003 est remplacé : son installateur a été fermé sans installation.
Son ancienne confirmation ne doit pas déclencher l'application du nouveau plan.
Le correctif dans `apps/forge_runtime/src/module_game/manual.rs` exclut les
retouches végétales reconnues par leur identité et pin de bloc exact de la
résolution des maillages manuels. L'hôte végétal commun les applique déjà.
Le lot `windows-editor-engine-20261105-002` a construit seulement le runtime
affecté après Clippy strict. Source :
`6dd082f84229d5d6391d623279f29df1ed04cea282fcc577fe1a1038b1b91e31` ; runtime :
`147d4c6634015073d7b87ad2d0664aa160eb15f603fa404b4505cbb10045f196`.
La tentative `native-module-export-001` atteint une projection prête mais sa
capture à frame 5970 dépasse le watchdog de 120 s : échec conservé. Le même
binaire, même module et même cache réussissent ensuite `native-module-export-002`
sur 900 frames, capture à 870, sortie 0. 65 383 instances et 13 434 collisions
canoniques ; espèces, gameplay et inventaires GPU prêts, aucune erreur de
projection. Reçu `normal-module-runtime-assessment.json`, SHA-256
`51b25d94028c9d835c1c66f78591772d233cc5d285ec8cbae4d832840684f17a`.
La caméra fixe en Ultra reste à 48,39 ms moyens (20,67 intervalles/s), p95
51,91 ms, sur 591 samples prêts entre frames 300 et 900. Ce ne sont ni des
timestamps GPU ni une comparaison avec Tidewater. Un profilage borné du même
binaire utilise `FORGE_RUNTIME_CPU_PROFILE=1`, sous `native-module-profile-001`.
Le profilage finit avec succès : coût CPU cumulé de `render.Specialize` autour
de 15,95 ms/appel, source-shadow GPU asynchrone autour de 14,29 ms à sa dernière
observation. Reçu `runtime-cost-diagnosis.json` ; les durées imbriquées ne
s'additionnent pas et les valeurs GPU gardent leur âge.
Le correctif en cours réutilise les globals temporels Bevy pour les matériaux
de prototypes chargés, conserve leur instant pour les paquets d'ombres, et
n'émet plus de modification d'asset pour le seul passage du temps. Il touche
`vegetation.rs`, `vegetation_prototypes/wind.rs`, `vegetation_prototypes/imported.rs`,
le shader partagé et `source_shadows/casters/governed.rs`. Les deux tests ciblés
`governed_material_clock` passent, ainsi que Clippy natif strict. Le runtime
optimisé du lot `windows-editor-engine-20261105-003` est construit sur sources
stables, mais refusé au rendu : la passe `pbr_prepass_pipeline` n'a pas le
binding Globals 0:11 utilisé par le shader. Reçu `shared-clock-gpu-refusal.json`.
Ne pas installer ce candidat. Le correctif conserve le transport temporel
existant : le slot zéro inactif du `ShaderBuffer` LOD à binding matériau 101
porte le snapshot Time courant/précédent. Son propriétaire existant
`temporal_lod/render.rs` l'actualise sans modifier l'asset matériau ni ajouter
de buffer ou de format persistant. Le test ciblé de bouclage passe, ainsi que
les deux tests GPU existants réutilisés sans recompilation (picking des modèles
remplacés et cascades d'ombres, alpha et vent). Clippy natif et Studio Web stricts
passent également. Le runtime optimisé du lot 004 est construit et son lancement
de scène complète passe avec capture à 570, projection prête et sortie 0.
Source : `866283acda95e49d41bd9d1b75fe6c3686e2a5831b15f295fa523eac09b8f04e` ;
runtime : `d88abb1da3ca20c6841306e7951e925c83bde1495856b074cf13cc0bdd82cea8`.
Le reçu 004 `shared-clock-runtime-comparison.json`, SHA-256
`f2917ef9f8c18bc3688ec2d42f80eba4b5a4b16d3b0310b78b36a52a977cccf1`,
compare les frames communes 300–599 (300 samples, 290 prêts de chaque côté),
avec état canonique attendu, caméra, résolution 1280×800 et Fifo identiques.
La moyenne passe de 49,414 à 38,641 ms (20,237 à 25,880 intervalles/s, +27,88 %),
p95 de 51,984 à 42,110 ms. Le CPU `render.Specialize` passe de 15,95 à 0,88 ms ;
`render.Queue` de 8,45 à 0,40 ms. Les observations cumulées/imbriquées ne
s'additionnent pas. La projection reste à 65 383 instances, 13 434 colliders
canoniques, 110 pipelines prêts et aucune erreur de rendu.
La performance reste insuffisante : les nouvelles observations situent un coût
dans l'intervalle CPU `render.PrepareViews` (~20,5 ms) et dans les ombres source
GPU asynchrones (~10,84 ms au dernier échantillon, avec âge). Diagnostiquer leur
cause avant une autre modification, sans rejouer les contrôles déjà terminés.
Le code Bevy de `prepare_windows` situe l'attente de surface dans cet intervalle :
ce n'est pas la preuve d'une fonction CPU lente. La référence et le runtime
demandent tous deux la synchronisation verticale par défaut. Le test GPU existant
confirme une Radeon RX 7800 XT discrète/Vulkan. Les anciens casters végétaux
remplacés sont retirés de leur sélection avant quota ; pas de doublon trouvé.
Le lot 005 prépare le tri des modèles par volume de cascade avec le frustum OBB
Bevy et les bornes de déformation existantes. Un buffer d'instances garde trois
plages et une sélection mise en cache ; ni modèles ni distances ne sont réduits.
Les deux tests CPU de sélection passent, session 88887 terminée. Le test GPU
existant d'ombres passe aussi avec une empreinte de profondeur inchangée après
activation du tri. Clippy natif et Studio Web stricts passent. La construction
optimisée du lot 005 termine sur sources stables ; session 59682 terminée.
Source : `5cce335a751eefc7e11197827924b8768a33d1a2cf77a31848619ba43b44127e` ;
runtime : `9b47825ad5d65a8f38b25bc85dbc35caa23285782009926fac77ad144edd2371`.
Le contrôle de scène complète `native-module-profile-001` passe avec capture à
570, sortie 0 et projection prête (session 97814 terminée). Le reçu
`cascade-runtime-comparison.json`, SHA-256
`99c0779db7af91e13291e02c23bb1121c00dc5df8bc3ab989a3d4ef8539faf19`,
vérifie état canonique attendu, caméra, arguments, 1280×800/Fifo/focus identiques.
Sur 300 frames communes 300–599 (290 prêtes de chaque côté), 38,641 → 31,662 ms
en moyenne (25,880 → 31,584 intervalles/s), p95 42,110 → 33,762 ms : +22,04 %
depuis 004 et +56,07 % depuis 002. Instances/colliders canoniques inchangés,
110 pipelines prêts, aucune erreur de rendu. Les valeurs GPU restent datées
et asynchrones ; aucune parité Tidewater ou acceptation visuelle n'est déduite.
Le frontend Studio 005 est construit sur sources stables, session 72938 terminée.
Le launcher autonome et la CLI sont également construits, contrôleur 10792 terminé.
Tous les reçus portent le même manifeste source 5cce335a… ci-dessus.
Le kit `E:/OpenRPG_Forge/.build/tmp/gate7-windows-game-kit-engine-20261105-005`
est assemblé, version `0.1.0-preview.20261105.005`. Reçu d’assemblage SHA-256
`2301800d76fc65748aa75b2097486807091ac2e41bbcca18cb28f68afda2036e`, manifeste
`a25ca83bddbf9a191f7f52f2e430bb69dce0411119fbdf7d5d511e593cc0c594`.
Le Studio natif est construit sur sources stables, session 88029 terminée.
Studio SHA-256 `05c848516c4263b13d7c361a59efa0a26cd5ca15d82f2f7df565a1baa5469897` ;
reçu de build `3fa4976b607e7a10a882cdbd420babb4ebfa93c7345e3a2b56ecd2a25ce80d5f`.
Le bin et son kit adjacent ont été remplacés après contrôle intégral de leurs
empreintes ; reçu 005 `studio-adjacent-kit.json`, 471 fichiers / 140 615 775 octets.
La session auteur révision 12 est inchangée. Le Studio est ouvert (PID 3872),
profil réutilisé `webview-20261105-001`. L’export UI réussit ; son plan 005
attend une nouvelle confirmation au clic Installer (voir état courant ci-dessus).
Les copies de retour arrière du remplacement ont été retirées après validation.
Ne pas reconstruire le runtime ni le frontend sans nouveau changement.
Réutiliser les notices privées existantes, sans relancer leur recherche :
bundle `E:/OpenRPG_Forge/.build/tmp/gate7-notice-bundle-014`, reçu candidat
`notices-winsafe-20261103-001/delivery-candidate.json` (SHA-256
`49581cdaa745ad4a943be41f2bbb0ee47bff6f556334583293e457cdead79d19`),
graphe voisin `graph.json`, attendu
`153c0e49c71cde685e7ff5d2abcf8969b244fcd7c955a0e590e61c115a3d403e`.
Vérifier le fichier de graphe et laisser l'assembleur revalider le graphe Cargo
courant. Le cache de registre est `E:/OpenRPG_Forge/.build/cargo-home/registry`.
Ces notices sont des candidats privés ; leurs limites légales restent inchangées.
Le runtime 004 validé est temporairement conservé dans
`camp-des-falaises-20261105-001/runtime-baseline-004/forge-module.exe`,
117 621 248 octets, empreinte d88abb… ci-dessus : conservation temporaire pour
la comparaison et le remplacement du cache partagé (reçu 005
`baseline-retention.json`). Cette copie est maintenant retirée : comparaison
004/005 terminée, kit 005 et export Studio 005 vérifiés avec le runtime 9b4782…
encore disponible. Reçu 008 `baseline-retirement.json`, retrait après hash,
contrôle de contenu unique, chemin physique et absence de processus actif.
Le dossier `runtime-baseline-004` n’existe plus ; ne pas le recréer.
Sessions 44998, 56544, 59682 et 97814 terminées ; aucun jeu de qualification n'est actif.
Le Studio/kit et l'export conservent le runtime antérieur : ne pas les présenter
comme mis à jour par ce lancement direct. Reprendre sur le même module, cache,
caméra et instrumentation que `native-module-profile-001` du lot 002, avec
600 frames (capture à 570), pas 6000 à cause du watchdog de 120 s.
Traiter le coût identifié avant de réassembler un kit et de demander
l'installation d'un nouveau candidat effectivement prêt. L'ancien game.forgepack
garde le runtime refusé et ne doit pas être présenté comme corrigé par ce run.
La tentative refusée et ses journaux sont dans le sous-dossier 001
`native-module-export-001`. Ne pas la transformer en qualification du jeu.
Ne pas retenter les retraits refusés de `module-cache`, `webview`, `webview-004`
par un autre outil. Les deux profils refusés gardent 972 286 819 octets ; les
profils suivants restent provisoirement associés aux parcours en cours.
Voir le reçu 004 `studio-webview-retirement.json`. Gate 7 reste ouverte, Linux différé.

## Lire et choisir le périmètre

Reprise opératoire du 4 novembre, lot Windows 003 : le workspace isolé
`E:/OpenRPG_Forge/.build/qualification-data/gate7/studio-imported-species-20261104-001`
est sauvegardé ; le candidat Studio corrigé est ouvert. La source courante est
`workspace-v1/authoring.json`, révision 10 : ne pas remplacer les données
gérées par les anciens fichiers de zone du bootstrap. Le jeu configuré est
« Le Camp des Falaises » ; le palmier retouché et les 453 corrections de relief
sont dans l’archive `9dec325cc9c4231f81eb79e7f96f23a6062f4f13f3061c8f78384d509a634737`.
Les reçus réels sont sous la campagne centrale, lot
`windows-editor-engine-20261104-002` : `studio-authoring-cycle.json` et
`studio-relief-export-assessment.json`. Le premier export a échoué avant livraison.
La correction distingue les références de blocs végétaux des GLB externes ;
son test ciblé passe. Les cinq constructions sur les sources
`c794931c4e87a83a1fd55ca7b9829ecde495e4382c06cf44dc7e29f49c4e82ea`
ont terminé sous `windows-editor-engine-20261104-003`, cache commun uniquement.
Le kit adjacent est vérifié et le candidat remplacé dans le même `bin`.
L’export corrigé depuis le Studio a réussi dans
`exports/game-20172-1793826379287132000` du workspace : quatre fichiers,
430 955 200 octets, dont le module à deux cartes, le jeu et `Installer.exe`.
Le reçu `windows-editor-engine-20261104-003/studio-ui-export.json` vérifie les
empreintes, les retouches et la configuration exportées. L’installateur est
ouvert, son plan vérifié pour
`E:/OpenRPG_Forge/.build/qualification-data/gate7/camp-des-falaises-20261104-003/install` ;
le clic final Installer attend la confirmation Computer Use au moment de l’action.
Ne pas appliquer ce plan avant sa réponse. Ensuite qualifier l’installation et
le jeu autonome. Les critères Linux restent
différés, sans nouveau verdict de clôture. Le cache redondant dont le retrait
automatique a été refusé reste préservé ; ne pas relancer cette suppression.
Les sources ont ensuite reçu un correctif de trois lignes dans
`apps/forge_studio/src/authoring_brush_pointer.rs` : conserver l’outil choisi
après un point refusé, puis nettoyer le geste par le `suspend` existant.
Le contrôle Studio WebGPU passe ; ce changement est postérieur au candidat 003
et ne modifie pas ses paquets ni le plan d’installation en attente. Ne pas
annoncer son intégration ou son geste réel comme qualifiés ; le prochain lot
groupé devra les vérifier. Le cas du terrain visible hors cellules physiques
détaillées reste distinct et ouvert. Le reçu est
`studio-brush-retry-assessment-20261104-001.json` dans la campagne centrale.
Un lot groupé supplémentaire de huit fichiers, décrit par
`terrain-and-companion-corrections-20261104-001.json`, raccorde le pointage des
maillages terrain/HLOD résidents à l’intersection Bevy existante, avec contrôle
de source et de visibilité, sans collider ajouté. Deux tests CPU passent et
le contrôle strict moteur/Studio WebGPU passe après réorganisation des fonctions
végétales. La livraison laisse aussi passer les compagnons épinglés par l’entrée
dédiée `module-game`, au plan et avant le spawn ; le chemin ancien refuse
toujours ces paquets. Le test sur installation de fixture et Clippy passent.
Aucun exécutable de fixture n’est lancé. Ces sources doivent rejoindre le
prochain candidat groupé et les parcours réels affectés ; le candidat 003 et
son installation graphique en attente restent inchangés.
La reconstruction groupée sous `windows-editor-engine-20261104-004` est terminée :
les cinq étapes portent le manifeste stable
`9dd50220c6e7191c2caad387ba1c8115877383af5d62db8392de7b48f03f8018`.
Le Studio et son kit adjacent sont remplacés et vérifiés dans le même `bin`.
Le reçu `candidate-replacement.json` confirme la session sauvegardée inchangée,
le retrait des 140 605 535 octets du kit adjacent 003 et les 140 605 023 octets
du nouveau kit. Le binaire Studio est épinglé à
`172bd2c1228667d076b43d0c88ecdc0ebddd8c32af65ef80ef15ec7a5e50d256`.
Le processus Studio 004 ouvert (PID 33864 au lancement) emploie le même
`FORGE_AUTHORING_ROOT` et le profil temporaire neuf `webview-004`.
La seconde carte du module est sélectionnée et charge ses ressources ; reprendre
les gestes de brosse après refus et de sélection des espèces remplacées.
L'installateur 003 reste ouvert, sa cible isolée encore absente au contrôle ;
sa confirmation est en attente. Ne pas appliquer ce plan sans réponse.
Les exports 003 sont inchangés et indépendants du remplacement Studio.


Le parcours réel du candidat 004 confirme le maintien de la brosse après un
clic dans le ciel, mais refuse encore la plage. Le rendu source masque les
cellules techniques de la même partition ; le filtre de pointage les écartait.
Le marqueur propriétaire `PhysicsProjection` et son empreinte autorisent
maintenant leur réemploi sans nouvelle géométrie, raster ou collider.
Trois tests CPU passent, puis le contrôle strict moteur/Studio WebGPU.
Le même essai sélectionne un arbre `trees`, prépare/applique/sauvegarde une
rotation de 15° sans modifier position/échelle : archive
`faca92c12c03f92398b65937bda361348fc9ed716285a738cc3437a2dce7242e`,
head `b196c8ddbf6f9a98fb6997ccf7bbcaf574a27f48b11b4556678f9874fa09afb9`.
Ce n'est pas une preuve de modèle remplacé : le viewport des modules omettait
l'appel `PreparedHost.with_species`, déjà employé par le jeu normal.
Il est raccordé au même chargeur de prototypes et au même catalogue épinglé ;
aucune population procédurale supplémentaire n'est générée. Le contrôle strict
moteur/Studio WebGPU passe sur ces quatre fichiers modifiés. Les gestes de
remplacement et de brosse restent à vérifier après construction.
La séquence unique du candidat 005 est lancée sous
`windows-editor-engine-20261104-005`, manifeste
`483b3cca9f97028e81210ff0c41721fb989dfac57768e5a7b1bf26f7ae8b4ed8`.
Elle enchaîne frontend, jeu, launcher, livraison, kit puis Studio sur le cache
commun avec 8 jobs ; conserver ses sources stables. Le reçu groupé est
`source-terrain-and-module-species-corrections-20261104-001.json`, le constat UI
`windows-editor-engine-20261104-004/studio-ui-observations.json`.
L'installateur 003 attend toujours la confirmation déjà demandée ; le Studio
004 reste ouvert sur sa retouche sauvegardée. Aucun nouveau critère Gate 7
n'est déclaré PASS et aucun contrôle Linux n'est lancé.

Le 5 novembre, le Studio 004 est fermé proprement après confirmation des
retouches enregistrées. La session conserve exactement le SHA-256
`3d4cda396b1b60a29fdff85660c191688f91585a68c2fffcfbe5940319553e99`.
Reçu : `windows-editor-engine-20261104-004/studio-clean-close.json`. Le build
005 a terminé ses cinq constructions ; les sessions 51685 et 13457 sont terminées. Le kit adjacent 004 est à conserver pour le remplacement vérifié
par 005. L’installateur 003 reste ouvert et n’est pas appliqué.


Le retrait des profils temporaires fermés `webview` et `webview-004` du même
workspace est ensuite refusé par le contrôle d'approbation automatique
(`blocked by policy`), avant exécution. Ils conservent 844 fichiers,
972 286 819 octets. Ne pas relancer cette suppression par un autre shell ou
outil ; le reçu `windows-editor-engine-20261104-004/studio-webview-retirement.json`
porte le statut `NOT_EXECUTED_AUTOMATIC_APPROVAL_REJECTED`. La session auteur,
les exports et le cache `module-cache` précédemment protégé sont préservés.


Le frontend et le jeu natif 005 terminent en `BUILD_ONLY`, sources stables.
La séquence initiale 51685 s'arrête ensuite au contrôle des arguments du
launcher, avant construction : `--shared-jobs` n'est pas admis pour ce rôle.
L'appel est corrigé sans modification du produit ; les deux étapes réussies
sont préservées et la reprise 13457 part du launcher, puis livraison, kit et
Studio. `orchestrator-resume.json` conserve cet arrêt et sa reprise dans le
lot 005. Ne pas recommencer le frontend ou le jeu natif.


Les cinq constructions du candidat 005 sont maintenant terminées en
`BUILD_ONLY`, sur le manifeste stable
`483b3cca9f97028e81210ff0c41721fb989dfac57768e5a7b1bf26f7ae8b4ed8`.
Le kit adjacent et le Studio sont remplacés par le lecteur canonique, session
auteur inchangée à `3d4cda396b1b60a29fdff85660c191688f91585a68c2fffcfbe5940319553e99`.
Le Studio est épinglé à `e675daa5dcd4a0b2df0ad09a0cce0342de2a486752b5600893d877b99ee78e2f`.
Le remplacement retire les 140 605 023 octets du kit adjacent 004 vérifié ;
le nouveau kit occupe également 140 605 023 octets. Les refus de suppression
`module-cache`, `webview` et `webview-004` sont respectés et ces chemins restent
intacts. Reçus 005 : `studio-adjacent-kit.json` et `candidate-replacement.json`.
Le Studio 005 est ouvert (PID 19704 au lancement) sur le même workspace,
profil neuf `webview-005`. La seconde carte dense est sélectionnée et charge
ses ressources ; reprendre les gestes de brosse et de modèles remplacés.
Les sessions de build 51685 et 13457 sont terminées ; ne pas les relancer.
L'installateur exporté 003 reste ouvert, sans application de son plan ni
réponse présumée à la confirmation demandée. Aucun nouveau verdict Gate 7.



Depuis la séparation du 18 octobre, développer le moteur dans `crates/forge_engine`
et les contrats génériques d'assets dans `crates/forge_assets`. La carte, ses
ports et ses données résident dans `crates/forge_tidewater` et
`crates/forge_tidewater_assets`. Aucun alias des anciennes API de carte ne reste
dans le moteur. Les exemples de qualification se lancent avec `-p forge_tidewater`
et les vérificateurs de données avec `-p forge_tidewater_assets` ; leurs noms et
arguments restent les mêmes. Lire [ADR-0026](ADR/ADR-0026-strict-engine-map-separation.md)
et les README des composants avant de reprendre un ancien script.

`scripts/Test-GenericEngine.ps1 -PrepareBrowser` contrôle aussi le moteur seul,
sans fonctionnalités par défaut et en WASM, puis le workspace et les ports.
Les scripts historiques de `scripts/hivemind` ne constituent pas des outils
produit et ne sont pas réécrits par cette séparation.

1. Lire [PROJECT_CONTEXT.md](../PROJECT_CONTEXT.md), qui porte l'état canonique.
2. Lire [AGENTS.md](../AGENTS.md) : architecture, gouvernance et règles de travail,
   applicables aussi à un développeur humain ou à un autre assistant.
3. Consulter les [ADR](ADR), en particulier ADR-0001 à ADR-0006, puis les décisions
   propres au sous-système et [ADR-0034](ADR/ADR-0034-github-backup-local-gate-validation.md).
4. Lire l'état courant de la [roadmap](planning/MIGRATION_ROADMAP.md), puis le
   README du sous-système concerné.

La branche active est `Rust`, dans `E:\OpenRPG_Forge\OpenRPG_Forge-Rust` sur le
poste actuel. `main` est la référence historique C#/Godot, sans dépendance
d'exécution du produit Rust. Sur un autre poste, utiliser un clone de `Rust` et
adapter les chemins ; sur ce poste, conserver le checkout commun.

Gate 3 est clôturée le 15 octobre ; Gate 4 est **clôturée localement** le
16 octobre, lot `0.1.0-preview.20261016.019`. Les
[rapports Gate 3](evidence/gate3/GATE3_CLOSURE_20261015.md) et
[Gate 4](evidence/gate4/GATE4_CLOSURE_20261016.md) portent les critères et limites.
Les anciennes sections « reste ouvert » ou « à acquérir » sont des constats datés,
pas des instructions de recommencer les gates. Distribution, Windows vierge,
Linux, réserve GLib et résultats distants historiques gardent des verdicts séparés.

**État au 22 octobre : Gate 5 clôturée localement**, selon son
[rapport final](evidence/gate5/GATE5_CLOSURE_20261022.md) : treize domaines et
R5-P01/P02 validés. Il s'agit de fondations pour créer des quêtes ensuite, sans
livraison d'aventure. Gate 6 est ensuite ouverte le 22 octobre et **clôturée
localement le 23 octobre**, selon son
[rapport final](evidence/gate6/GATE6_CLOSURE_20261023.md) : 21 critères, deux
solutions jouées/reprises pour chaque parcours et archive vérifiée. Le
[bilan comparatif](evidence/gate6/GATE6_COMPARISON_20261023.md) conserve ses limites,
sans gain général d'efficacité démontré. **Gate 7 est ouverte depuis le 23 octobre**
avec objectif de clôture documentée ; suivre son
[plan](planning/GATE7_IMPLEMENTATION_PLAN.md) et son
[suivi](evidence/gate7/GATE7_PROGRESS_20261023.md). Les verdicts à jour
figurent dans l'[état courant](#gate-7-etat-courant). Gate 8 et la gate bonus
restent non ouvertes. GitHub sert
de sauvegarde distante ; ses contrôles sont arrêtés et ne conditionnent plus
les gates, selon ADR-0034. Les validations locales restent exigées. La clôture
n'atteste pas un commit ou un push des modifications locales.

**Reprise du 19 octobre :** le propriétaire ouvre Gate 5 avec objectif de clôture,
selon le [plan de réalisation](planning/GATE5_IMPLEMENTATION_PLAN.md). Pour reprendre
un chantier déjà autorisé, établir son objectif, ses fichiers et ses preuves
actuelles avant toute modification ; une ancienne conversation n'est pas requise
pour comprendre les règles du dépôt.

La répartition propriétaire du 20 octobre remplace celle de l'ouverture : E:
porte les sources, outils, téléchargements, compilations, caches et temporaires ;
F: conserve les preuves, l'historique et la bibliothèque d'assets convertis.
Compiler le checkout commun avec `scripts/Invoke-ForgeCargo.ps1`, la cible
`E:/OpenRPG_Forge/.build/cargo-target`, `CARGO_HOME` sous `.build/cargo-home`,
`CARGO_INCREMENTAL=0` et `TEMP`/`TMP` sous `.build/tmp` sur E:.
Neutraliser les anciennes variables pointant vers F:, sans recopier les caches
historiques. Les preuves sont centralisées sous
`E:/OpenRPG_Forge/Preuves-et-analyses/actives/gate5/20261019/`, physiquement sur F:.
L'archive vérifiée est `F:/OpenRPG_Forge/Preuves-et-analyses/archives/gate5-20261022-e-001.zip` ;
son catalogue est `E:/OpenRPG_Forge/Preuves-et-analyses/rapports/gate5-closure-20261022-e-001.json`.
Les anciens chemins et reçus restent historiques. Vérifier les processus actifs
avant toute nouvelle compilation et consulter le rapport final avant de rejouer
une qualification déjà acquise.

## Préserver le travail local avant la reprise

Le commit de référence de l'inventaire initial du 17 octobre est
`2b0d60fa` (`feat: close Gate 4 governed assets and standalone module delivery`).
Cela identifie un état Git, pas tous les fichiers locaux ni tous les octets
qualifiés : le rapport Gate 4 référence aussi un gel de sources et ses preuves.

La séparation du 18 octobre travaille depuis
`3c3a5efeba2473574597871f223d74aa800cf99a`, toujours sur le checkout partagé
modifié. Les nouveaux composants de carte et les contrats neutres doivent être
conservés avec les suppressions de leurs anciens emplacements : un clone du
commit seul ne reproduit pas ce lot local avant son commit et sa publication.

L'[inventaire local daté](handoff/LOCAL_WORKTREE_20261017.json) liste les états
Git et les chemins visibles pendant cette passation, hors fichiers de cette édition,
avec taille et SHA-256 des fichiers texte de travail lisibles. Les binaires
locaux sont seulement recensés par chemin et taille. Les suppressions restent
des suppressions ; un inventaire n'est ni une sauvegarde ni un commit.
Les fichiers ignorés et les données hors checkout ne sont pas inventoriés par Git.
Le travail concurrent peut évoluer après cette capture.

Le lot local comprend notamment les scripts d'import `scripts/hivemind/`, leurs
deux documents de suivi, des suppressions de configuration Codex et d'anciens
artefacts d'installateur. Ne pas tout ajouter à Git automatiquement. Les suivis
locaux `docs/planning/HIVEMIND_SAVED_PHYSICS_REFERENCE.md` et
`docs/planning/HIVEMIND_SOURCE_LOD_HIERARCHY.md` font partie de ce travail non
publié ; leurs preuves privées peuvent être sur `F:`. Ils sont recensés dans
l'inventaire et doivent être récupérés avec la sauvegarde locale si nécessaires.

Depuis la racine du checkout, ces commandes de lecture permettent de réévaluer
la situation (exécutées pendant la revue) :

```powershell
git branch --show-current
git rev-parse HEAD
git status --short
git diff --name-status
git ls-files --others --exclude-standard
```

Avant de changer de machine, conserver une sauvegarde privée du checkout avec
ses fichiers locaux, des données ci-dessous et des cibles de jonctions utilisées.
Vérifier la relecture et les empreintes de cette sauvegarde. Un clone Git ne
contient pas les fichiers non suivis, les fichiers ignorés ou les données privées.
Ne pas exécuter `reset --hard`, `clean` ou un nettoyage global pour reprendre.
Ne pas restaurer les anciennes configurations d'agents supprimées. Aucun commit
ou push n'est autorisé par ce guide.

## Environnement de développement

La procédure actuellement documentée vise Windows x64. Préparer Git, PowerShell
7.2 ou plus récent (`pwsh`, utilisé par les hooks), Rustup avec MSVC/Windows SDK,
WebView2 pour le shell Studio, Python 3.11 ou plus récent pour les outils et Node.js pour le
frontend Aube et les tests navigateur. Ces outils de développement ne sont pas
des prérequis du client installé. Linux n'est pas qualifié par cette passation.

La [toolchain](../rust-toolchain.toml) épingle Rust **1.98.0**, rustfmt, Clippy,
rust-src et `wasm32-unknown-unknown`. Le
[bootstrap](../scripts/Bootstrap-Rust.ps1) épingle cargo-deny 0.20.2,
cargo-audit 0.22.2, Trunk 0.21.14, Tauri CLI 2.11.4 et wasm-bindgen-cli 0.2.127.
Le script télécharge des dépendances et refuse une branche autre que `Rust`.
Les dépendances locales sous `third_party/` sont nécessaires : garder le clone
complet et `Cargo.lock`. Cadle n'est pas requis pour compiler Forge.

Procédures déjà présentes dans le dépôt, à exécuter sur le poste de reprise après
installation des prérequis (non relancées lors de cette édition documentaire) :

```powershell
.\scripts\Bootstrap-Rust.ps1 -InstallTools
.\scripts\Test-Rust.ps1
```

Le second script lance formatage, Clippy, tests natifs, contrôles WASM, Trunk et
supply-chain. Sélectionner les validations selon le changement et le protocole,
sans assimiler compilation, tests, GPU, acceptation humaine et qualification.
La supply-chain conserve une réserve GLib/Linux ; lire le résultat effectif.

## Données et lancement du poste actuel

Le point d'entrée du propriétaire reste `E:\OpenRPG_Forge\Lancer-Editeur.cmd`.
Il lance le Studio commun et définit `OPENRPG_ASSET_VAULT`. Préserver ce lanceur
et son profil ; changer d'éditeur de code ne nécessite pas de les remplacer.
Les valeurs suivantes ont été lues dans le lanceur, le profil et les scripts le
17 octobre ; elles sont des chemins du poste actuel, pas des valeurs universelles.

| Élément à conserver ou remapper | Emplacement actuel / rôle |
|---|---|
| Studio et profil côte à côte | `E:\OpenRPG_Forge\.gate3-ntfs-cargo-20261002\release\forge-studio.exe` et `forge-studio.local.json` |
| Projets et cartes auteur | champ `authoring_root` du profil : `E:\OpenRPG_Forge\.gate3-evidence\20261011\p05-human-review-shared019-rpg007-001\authoring-data` |
| Vault privé | `F:\OpenRPG_AssetVault`, référencé par le lanceur et `asset_vault` |
| Assets du frontend | `E:\OpenRPG_Forge\.forge-local\studio-assets`, avec `frontend-static-inputs.json` |
| Données du jeu et des scènes | `E:\OpenRPG_Forge\.forge-local\runtime-data` ; `module_assets.root` vise `data\scene` et son catalogue épinglé |
| Jeu natif séparé | `game.executable` : `E:\OpenRPG_Forge\.gate3-ntfs-cargo-20261002\release\examples\tidewater_plant_probe.exe` |
| Rapports des parties | `game.session_root` : `E:\OpenRPG_Forge\.forge-local\game-sessions` |
| Preuves et livraisons locales | dossiers voisins `.gate3-evidence`, `.gate4-evidence`, installateur et contenus référencés par les rapports |
| Travaux privés d'import | `F:\OpenRPG_ImportWork` et les emplacements indiqués par les suivis d'import |

Sur une autre machine, rétablir les données nécessaires puis préparer un profil
pour cette machine avec ses chemins. Les chemins relatifs du profil se résolvent
par rapport à l'EXE. Ne pas retirer les vérifications de catalogue/hash pour
compenser une donnée absente. Les projets, sauvegardes, sources privées et leurs
licences doivent être transférés de manière autorisée ; ils ne sont pas inclus
automatiquement dans Git. Les liens des rapports vers les preuves voisines ne
seront utilisables qu'après restauration de ces dossiers.

## Compiler le Studio et préparer une livraison

Le hook [Trunk](../apps/forge_studio/Trunk.toml) compile le worker de navigation
et prépare les assets. Il exige `CARGO_TARGET_DIR` explicitement ; le staging
utilise `FORGE_STUDIO_LOCAL_ASSETS` ou, par défaut, le dossier voisin
`.forge-local/studio-assets`. Il contrôle le manifeste et refuse certaines
jonctions. Un répertoire vide ne remplace pas les données authentifiées.
La présence du manifeste frontend a été constatée pendant la revue ; son contenu
n'a pas été requalifié. L'accès au Vault `F:` a été refusé par l'environnement
de cette session : sa disponibilité et son contenu restent à vérifier sur le
poste de reprise. Ne pas confondre refus d'accès et absence du dossier.

Sur le poste actuel, la consigne propriétaire du 20 octobre remplace celle de
l'ouverture de Gate 5 : développer et compiler sur E:, conserver preuves,
historique et bibliothèque d'assets sur F:. Les chemins installés déjà constatés
plus haut restent des références de l'installation existante.

```powershell
$env:CARGO_TARGET_DIR = 'E:\OpenRPG_Forge\.build\cargo-target'
$env:CARGO_HOME = 'E:\OpenRPG_Forge\.build\cargo-home'
$env:CARGO_INCREMENTAL = '0'
$env:TEMP = 'E:\OpenRPG_Forge\.build\tmp'
$env:TMP = $env:TEMP
$env:FORGE_STUDIO_LOCAL_ASSETS = 'E:\OpenRPG_Forge\.forge-local\studio-assets'
```

Pour le frontend Gate 5, utiliser une destination de travail sur E: avant
d'appeler le wrapper Trunk, puis conserver les preuves utiles sur F:. Le hook
navigation utilise `TRUNK_STAGING_DIR` et hérite du cache Cargo et des temporaires E:.

```powershell
$env:TRUNK_BUILD_DIST = 'E:\OpenRPG_Forge\.build\frontend\gate5-candidat-unique'
$env:TRUNK_OFFLINE = 'true'
$env:TRUNK_SKIP_VERSION_CHECK = 'true'
$env:CARGO_NET_OFFLINE = 'true'
$env:PATH = 'E:\OpenRPG_Forge\.build\tools\binaryen-131-001\bin;' + $env:PATH
```

Trunk 0.21.14 et wasm-bindgen 0.2.127 sont disponibles sur le poste ; ce dernier
correspond au verrou courant. Les exécutables existants sur C: peuvent être lus,
mais aucun nouvel outil ou cache n'y est installé. Le mode hors ligne refuse un
outil absent avant d'ouvrir le cache automatique de Trunk : vérifier les outils
dans `PATH` et préparer les compléments éventuels sous `.build/tools/` sur E:. Voir le
[code de résolution des outils de Trunk 0.21.14](https://github.com/trunk-rs/trunk/blob/v0.21.14/src/tools.rs#L265).
Cette vérification ne constitue pas un build frontend. Le packager
`Build-StudioCandidate.ps1` accepte désormais une sortie neuve sous
`F:/OpenRPG_Forge/` ; lui fournir explicitement les dossiers de cache et de
temporaires F:. Ses anciens défauts ne sont pas les paramètres de Gate 5.
Le fichier d'entrée demande l'optimisation WASM `z`. Binaryen 131 a donc été
recopié depuis l'outil existant sur E: vers le dossier F: ajouté au `PATH`
ci-dessus, avec vérification d'empreinte, version exécutée et licence Apache 2.0.
Le reçu `wasm-opt-tool-001.json`, sous les preuves Gate 5 sur F:, indique qu'aucun
téléchargement ni build frontend n'a été exécuté par cette préparation.
Le hook Trunk peut déjà avoir copié `forge-kit`. Fournir tout de même
`-GovernedKit` au packager : celui-ci exige alors la même liste de fichiers et
les mêmes empreintes, sans remplacement ni duplication des pins. Les tests
d'entrée `candidate-kit-input-tests-002` du 20 octobre passent sur F:, y compris
le refus d'un fichier caché supplémentaire ; ils ne
constituent ni un build du Studio ni une qualification graphique.
Avant tout lancement graphique du nouveau binaire, fixer aussi :

```powershell
$env:FORGE_WEBVIEW_DATA_ROOT = 'E:\OpenRPG_Forge\.build\webview\studio'
```

Le raccordement natif `native_webview.rs` diffère alors la création automatique
des fenêtres et fournit ce chemin absolu au constructeur Tauri avant toute
création de WebView ; la fenêtre Atelier emploie le même dossier. Une valeur
vide ou relative est refusée au démarrage. Ce réglage est nécessaire car le
gestionnaire Tauri peut créer son dossier local par défaut avant l'appel à
WebView2. Les constats préparatoires du 20 octobre sont remplacés, pour l'état
courant, par les validations du candidat final et les parcours graphiques acquis
dans le [rapport Gate 5](evidence/gate5/GATE5_CLOSURE_20261022.md).
Vérifier le chemin effectif dans les processus WebView2 lors d'un nouveau
lancement et conserver les preuves nécessaires dans le catalogue central sur F:.

Le wrapper [Invoke-StudioTrunk.ps1](../scripts/Invoke-StudioTrunk.ps1) gère le
build/serve et normalise `NO_COLOR`. Pour une reconstruction livrable, suivre
[scripts/delivery/README.md](../scripts/delivery/README.md), le validateur commun
et ses reçus, plutôt que d'assembler d'anciens binaires. Le target générique
`forge_runtime` et le jeu livré `tidewater_plant_probe` ont des rôles distincts.
Sérialiser les builds lourds et préserver les sessions du propriétaire.

Le lot historique distingue le launcher Aube, le gestionnaire de contenus,
Studio, le jeu natif et les packs. Cette organisation décrit les livraisons
existantes, pas la cible obligatoire du 2 novembre : éditeur et launcher du jeu
exporté. Conserver les installations utilisables pendant la transition et ne
retirer le Hub qu'après transfert et qualification de ses fonctions utiles. Le
[rapport Gate 4](evidence/gate4/GATE4_CLOSURE_20261016.md) identifie l'installateur
unique et les contenus séparés du lot 019, avec empreintes et limites.

## Fin d'une session de reprise

Consigner le périmètre modifié, les commandes réellement exécutées et leurs
résultats, les preuves et limites, puis la prochaine action concrète. Actualiser
le contexte ou la roadmap seulement si leur état change. Ne pas stocker une
décision indispensable uniquement dans la mémoire d'un assistant. Une mémoire
ancienne ne remplace jamais les fichiers et preuves actuels du projet.

Cette passation a été vérifiée par lecture des scripts, contrôle des liens
documentaires et `git diff --check`. Aucun bootstrap, build, test produit,
lancement de jeu ou transfert de données n'a été effectué pour cette édition.
Le propriétaire demande ensuite la publication du lot documentaire sur `Rust` ;
elle conserve les autres travaux locaux hors de son périmètre.
La reprise depuis un clone neuf sur un autre PC reste à éprouver.

## Contrôles des entrées moteur communes — 18 octobre 2026

Le [bilan d’architecture](planning/ENGINE_RESERVES_20261018.md) distingue les
corrections réalisées de la généricité globale encore ouverte. Le runner local
reproduit les garde-fous, Clippy natif/WASM, tests workspace et le probe optique :

```powershell
pwsh -NoProfile -File scripts/Test-GenericEngine.ps1 -PrepareBrowser
```

Il écrit dans un nouveau dossier `output/generic-engine-<date>`, déjà ignoré par
Git. Il exige une toolchain et les dépendances Rust disponibles localement
(`--locked --offline`) et le CLI `wasm-bindgen` pour préparer le navigateur.
`-TargetDirectory` permet de choisir le cache Cargo. L’étape navigateur doit être
exécutée sur localhost avec WebGPU : la préparation ne vaut pas preuve de rendu.
Le [guide des API](../crates/forge_tidewater/COMMON_SCENE_INPUTS.md) précise les
conventions et limites. Ce runner n’installe ni ne publie le produit et ne clôture
aucune gate.

Pour le probe navigateur, servir le dossier de résultat affiché par le runner :

```powershell
python -m http.server 8769 --bind 127.0.0.1 --directory "output/generic-engine-<date>"
```

Ouvrir `http://127.0.0.1:8769/index.html` dans un navigateur WebGPU. La page doit
afficher PASS après les dix readbacks, puis arrêter le serveur avec Ctrl+C.
Les outils vérifiés ici sont Rust 1.98.0 et `wasm-bindgen` 0.2.127, ce dernier
devant correspondre au lockfile. Les dépendances et le CLI doivent être disponibles
localement avant les commandes `--offline`.

## Assistant IA et MCP — Gate 6 clôturée localement

Les frontières du sous-système sont consignées dans
[ADR-0035](ADR/ADR-0035-assistant-governed-proposals.md). Le domaine de proposition
est sous `crates/forge_project/src/assistant/` ; les protocoles, coordinateurs
natifs et panneaux sont sous `apps/forge_studio/src/assistant*` et `mcp*`.
Le [rapport courant](evidence/gate6/GATE6_PROGRESS_20261022.md) distingue les
preuves métier, fournisseur, Studio et jeu ; ne pas déduire une clôture de la
seule réussite d’un test ou d’un build.

Ajouter la section `assistant` au profil `forge-studio.local.json` adjacent à
l’exécutable. **Conserver les autres sections du profil existant**, notamment
les chemins d’auteur, de Vault et du jeu. Exemple correspondant au serveur local
fourni pour cette qualification :

```json
{
  "assistant": {
    "base_url": "http://192.168.1.41:8080/v1",
    "model": "qwen3.8-flash-next-iq4-xs",
    "api_kind": "chat_completions",
    "enable_reasoning": true,
    "api_key_environment": null,
    "limits": {
      "maximum_attempts": 2,
      "maximum_input_tokens": 100000,
      "maximum_output_tokens": 16384,
      "maximum_micro_usd": 0,
      "prices": {"input_per_million": 0, "output_per_million": 0}
    }
  }
}
```

Les tarifs sont des entiers en micro-USD par million de tokens. Les zéros de cet
exemple décrivent l’absence de facturation fournisseur déclarée pour le serveur
local, sans mesurer son coût matériel ou électrique. Pour une clé, inscrire
seulement le nom de sa variable native dans `api_key_environment`, jamais sa
valeur dans le profil, les sources ou les preuves. Une adresse HTTP exige une
IP locale explicitement configurée ; une destination distante exige HTTPS.
`FORGE_ASSISTANT_CONFIGURATION` permet une surcharge native limitée au processus
de qualification ; elle ne modifie pas le profil du propriétaire.

Sauvegarder puis relancer Studio pour charger la configuration. Dans **Créer et
raconter → Assistant**, vérifier destination, modèle et plafonds. Sélectionner
les propriétés, cibles et région ; les références d’assets se recherchent
localement puis se cochent, avec un droit de placement séparé. La création de
contenu est un choix explicite et exige les capacités Creator adéquates. Préparer
et relire les données exactes, autoriser l’envoi, attendre le résultat, examiner
le diff puis approuver l’application et sauvegarder la carte. Le délai affiché
varie de 2 à 16 minutes avec le plafond de sortie ; une attente n’entraîne aucun
nouvel appel automatique. Le raisonnement est demandé explicitement sans être
affiché ou enregistré.

Les jobs restent sous `assistant-jobs` de la racine d’auteur. Après interruption,
inspecter le panneau de reprise et les empreintes saved/pending avant toute
promotion. Réutiliser le résultat reçu avec une nouvelle revue quand le contexte
le permet ; un Started incertain ne prouve pas un échec serveur. Un résultat
valide dépassant le budget reste conservé et bloqué. Ne pas effacer un journal
pour contourner ses quotas. Après une application non sauvegardée perdue, seul
le contexte original permet de retrouver le résultat Staged ; un reçu canonique
présent est réconcilié sans nouvelle application.

Pour MCP, le client lance ce même exécutable avec `--mcp-stdio`. L’interface
Studio doit d’abord préparer, afficher et autoriser le contexte. Les outils
`forge_inspect_selection` et `forge_propose_edits` ne peuvent pas approuver ou
appliquer un diff. L’approbation reste dans Studio. Les propositions persistées
sous `assistant-mcp-proposals` n’accordent aucun droit au redémarrage : autoriser
à nouveau le contexte puis relire le nouveau diff. Une ancienne approbation est
invalide. Révoquer ou déconnecter retire l’autorité sans purger les preuves.

La campagne de qualification reste consultable sous
`Preuves-et-analyses/actives/gate6-ia-mcp/` au hub commun, physiquement sur F:.
Elle est aussi archivée dans `archives/gate6-ia-mcp-20261023.zip`, lecture et
SHA-256 intégralement vérifiés, avec catalogue central et reçu
`rapports/gate6-archive-20261023.json`. Les originaux sont conservés. Le candidat
Studio qualifié est le binaire commun `f23467af…` ; le Studio déjà ouvert du
propriétaire n'est pas remplacé. Le dernier contrôle Qwen est injoignable :
vérifier l'endpoint avant tout nouvel envoi, sans inventer de résultat réussi.
Les scripts `scripts/gate6/` compilent le produit commun et préparent des données
isolées. Le pilote UI observe les sources sauvegardées ; il ne remplace pas les
clics de consentement et d’application. Le lancement canonique du propriétaire
et son profil ne doivent pas être remplacés par ces données de test.
