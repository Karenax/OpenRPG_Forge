# Gate 3 — version commune, 11 octobre 2026 (actualisée le 13 octobre)

## Studio courant : conservation de la carte pendant le jeu, 13 octobre

Le seul EXE commun est maintenant
`4dd80264dbcfd11a590f831b14fe040fce6b67b2d310cc988b0ef38555aa242e` ;
l'empreinte des 2549 entrées de compilation est
`a5ba7b3c2680c88ddb075fdfa0d5bc02702138dbd35affaf29702c651252ccd5`.
Le lanceur `E:/OpenRPG_Forge/Lancer-Editeur.cmd` est inchangé et a ouvert
cet EXE. Voir [le retour du jeu sans rechargement](STUDIO_RESIDENT_GAME_RETURN_20261013.md)
pour le périmètre, les preuves et le statut de vérification. Les autres identités
ci-dessous décrivent les essais antérieurs du même produit. Gate 3 reste ouverte.

## Essais antérieurs : chargement, mémoire et jeu séparé, 13 octobre

Le seul EXE commun, accessible par `E:/OpenRPG_Forge/Lancer-Editeur.cmd`, a pour
SHA-256 `253d82dcd371535d6e40fbce924683fa9566be47790660e31c0ac0d29054a4c7`.
L'empreinte de ses 2549 entrées de compilation est
`82931937aaddff9d6a235289f1d591b0c0315d7a977b5580bac5997b57040b04`.
Voir [les changements, mesures et limites actuels](STUDIO_LOADING_NATIVE_GAME_20261012.md).
Les identités des essais précédents ci-dessous sont historiques ; elles ne
désignent pas d'autres versions actives. Gate 3 reste ouverte.

## Essais historiques : lancement direct corrigé le 12 octobre

Le même EXE commun lit désormais `forge-studio.local.json` près de lui.
Le point d'entrée à double-clic est `E:/OpenRPG_Forge/Lancer-Editeur.cmd`.
L'EXE direct, le raccourci et le wrapper sous Windows PowerShell ouvrent Tidewater
et ses assets avec le PATH Windows ordinaire, sans variables de Codex.
Identité du Studio lors de ces essais :
`8db598d1a2e3ebb16d9d8fad7b7772d3fd0a0b872f3ae8e1850e1d256699e276` ;
empreinte des 2540 entrées de compilation :
`8cbc516fdb014f78d54ee274f33309ad4e342b9f2559b13d9fa9ef183cd2a087`.
Les identités antérieures ci-dessous sont historiques. Les validations complètes
de l'ancien candidat ne sont pas transférées au Studio courant.
Voir [le correctif et ses essais](STUDIO_DIRECT_LAUNCH_20261012.md).
Gate 3 reste ouverte et le test intégré demeure retiré.

## Retrait du test intégré — décision propriétaire du 12 octobre

Le propriétaire signale le test Studio injouable, autour de 1 FPS, et autorise
sa correction ou son retrait. Son accès et ses commandes de lancement sont
retirés du Studio commun. L'édition, le départ enregistré et les données RPG
restent disponibles. La partie P05 doit être proposée dans le jeu séparé après
raccordement du gameplay ; aucune partie complète ne peut actuellement y être
revendiquée. Voir [le retrait et sa validation](STUDIO_PLAYTEST_RETIRED_20261012.md).
Les identités `019` ci-dessous décrivent le Studio antérieur, pas le Studio
modifié par ce retrait. Gate 3 reste ouverte ; aucun gain FPS n'est revendiqué.

## État historique du candidat `019` : validation locale close, parcours publics `007` incomplets

Ce candidat historique a été vérifié dans le checkout commun : sources
`9c46a4d87e9264ee2d082902e743de961d17731d30c5b7d9bc705fa6a85269e5`,
Studio `27f16ff2da18564f88fe7354e3b72abc8f6b75dc7b299771c6e1fd2e089df551`.
La validation locale complète est close : 2102 tests natifs passent, 55 sont
ignorés, les 221 suites/binaires sont épinglés, les 34 replays privés passent,
ainsi que format, diff, Clippy, deny/audit, WASM et les builds runtime/Studio/Game.
Les parcours `007` Base, Physics, NavigationAuthoring, P03, EnvironmentQuest et
ShorePush et RPG/P01 ferment normalement. Les lecteurs indépendants P03 et RPG passent.
NavigationRuntime et P04 `008` ferment normalement sur le même candidat, après
leurs refus `007` conservés. Les lecteurs indépendants de navigation et P04 `008`
passent après correction du raccord de preuve P04 au fixture parent exact.
Le raccord de navigation `008` au frontend WASM/WebView2 et au worker courant
est fermé par les sources, les artefacts et les observations du même parcours.
P02 `005` ferme normalement sur le même candidat et sa lecture indépendante passe.
Le feu/IA temporel reste à fermer : la tentative `009` atteint l'allumage,
mais le premier ACK de retrait arrive à `1336.187 ms`, au-delà de la garde
de `1000 ms` de cette acquisition. Le résultat reste `failed_closed` ; aucune
nouvelle relance n'est préparée. La mesure `019-003` du profil courant ferme
normalement et produit un diagnostic de cadence faible : `10.5116 Hz` en moyenne,
`114.5 ms` au p95, sur 367 intervalles de boucle (`34.9138 s`). Ces valeurs ne
prouvent pas les FPS présentés par le GPU et ne valident pas la fluidité.
Le scan répété de géométrie en navigation est une piste de correction partielle ;
le coût propre de l'instrumentation et les autres causes restent à distinguer.
La tentative de mesure minimale `019-001` ne collecte aucune durée : le lecteur
refuse la date d'identité convertie par PowerShell avant toute connexion CDP.
Son Studio et son navigateur direct sont ensuite fermés normalement par la reprise
de fermeture `019-001-recovery-002`, avec leurs handles retenus et exit0 observés.
La tentative `019-002` refuse ensuite l'attribution du listener CDP au renderer,
qui hérite l'argument de port du navigateur sans posséder ce listener. Aucun CDP
ni chronométrage n'est lancé. Le natif et le navigateur direct ferment normalement
dans `019-002-recovery-001` ; les résultats de refus ne sont pas réécrits.
Le contrôle suivant distingue le témoin OS du navigateur direct et l'argument
hérité des descendants, dont les identités restent vérifiées.
P05 humain et la CI distante sur la révision publiée restent requis.
Aucune qualification de Gate 3, distribution ou Linux n'est déduite de ces preuves locales.

Les lectures causales `007` EnvironmentQuest et ShorePush ferment avec succès
sur leurs capacités réellement exercées et persistées. Le premier parcours feu
`007` a été refusé : son approche atteint le point de lancement, mais le recul commence à
3246,8945 ms après l'allumage, au-delà du budget de 1000 ms, et le joueur est
vaincu. La session ferme sans arrêt forcé ; aucune qualification feu/IA n'est
déduite de la seule ignition. Les tentatives de mesure refusées ne produisent
aucun résultat de performances. La capture RPG `007`
`run1-interior_inside.png` montre les parois de la pièce désormais visibles ;
cette lecture technique ne constitue pas l'acceptation humaine P05.
Le lecteur RPG `007-shared019-rpg.reader.json` a pour SHA
`aa72077dae694f2543aca43b1466e04ed1d3fb5d4b26c9311d0f5437f2531902`.
Il ferme la quête sauvegardée et reprise, la direction initiale observée avant
capture, la visite de la pièce, les conséquences gardien/Sceau revérifiées à
leur site et les deux processus natifs/WebViews fermés normalement.
P02 `007` conserve ensuite un refus réel, avant qualification de la retouche :
la route publique `cross_interior_kcc` reste sans progression horizontale pendant
douze secondes, au point observé `(45.24, 7.39, -148.31)`, au sol et entrée active.
Son reçu est `failed_closed` ; aucun lecteur P02 qualifiant n'est exécuté pour
cette tentative. L'ouverture physique est centrée en `x=46`, de largeur `1.4 m` ;
avec le rayon joueur de `0.30 m`, le passage libre exige un centre entre `45.60`
et `46.40`. L'attente de stabilisation précédant la traversée fait dériver le
joueur jusqu'à `45.24`, hors de ce passage. Les géométries sont inchangées ;
aucune preuve ne permet d'attribuer cet arrêt à un contact avec un PNJ.
Le retry conserve la traversée publique et prépare un alignement réel avant elle.
Le lancement P02 `002` est ensuite refusé avant toute ouverture native : le reçu
de préparation conserve l'ordre des clés du retour Python direct, alors que la
CLI canonique les trie. L'observateur compare ces représentations JSON textuelles.
Le journal de refus reste conservé. La préparation `003` reproduit ensuite
exactement la représentation de la CLI et passe l'admission en lecture seule,
sans modifier la comparaison ni le produit.
P02 `003` ferme sur un nouvel échec réel : le recul puis le centrage réussissent,
mais l'approche finale oscille autour du point `(46, -148.6)` et n'atteint pas
la précision de pilotage de 10 cm en dix secondes. Les translations publiques
restent observées ; la traversée historique n'est pas encore exécutée.
Le ledger `failed_closed` est conservé au SHA
`b57840758218ec249874276015134d3ded928a0c11da72cbd223ca9ba36944ba`.
Il ne qualifie pas P02. Une correction externe du pilotage fin est ensuite préparée ;
les critères de traversée, les sources et l'exécutable commun restent inchangés.
P02 `005` ferme normalement à 01:20 UTC le 12 octobre. Le pilotage externe effectue
23 impulsions de clavier réelles et atteint le centre du passage avant la traversée :
ancre `(46.083126, -148.543671)`, puis intérieur `(46.083069, -144.858932)`.
Le critère de traversée de 1,25 m est inchangé. Sauvegarde, refus ordinaire de
reprise et reprise compatible dans un second processus passent. Le ledger a pour
SHA `d7352baefdf8f61a34b1b9ed0a5f9cf031846528577ab6e66e4f36f5f8fea4e6`,
le reçu de stage `d77c00e52576ba291c890fa6b435ca573e0641a5428571742a6e2c15e5c0303b`.
Le monitor `18b902f2c746e37b433ace391941f3881aab8d34845aac09c64c287f33d7fb79`
observe deux natifs exit0 et la fermeture de leurs 16 WebViews identifiés, sans
arrêt forcé ni prétention de drain global. Les sources, le Studio et les préimages
restent inchangés. Ce résultat ne vaut ni P05 humain ni clôture de Gate 3.
La première lecture indépendante conserve un refus d'accès sandbox à l'index
privé, sans modifier les données. La lecture du même config avec les accès requis
ferme `CURRENT_SCOPE_EVIDENCE_PASS` : `p02-007-attempt005.reader-002.json`, SHA
`7bc15aa9d1fca13ef862ac52f595fff34da7fd37209c28814bccabf36abf183d`.
Fire `003` conserve ensuite un refus mesuré à `1634.3531 ms` avant la première
touche de recul, contre le budget inchangé de `1000 ms`. Le recul est tenté,
la sauvegarde reste cohérente avec la pose publique initiale et les sources sont
préservées ; ce résultat ne qualifie pas le feu/IA temporel. Son ledger
`failed_closed` a pour SHA
`2634b2b84a540a49abd51007c67cf46168520b40315c3746825a0e9e1df81afa`.
Fire `004` conserve un nouveau refus à `1024.4004 ms` pour le même budget
de `1000 ms`, malgré la réduction des échanges souris/focus. Le ledger a pour
SHA `e0c696242583a3b013ee4d041e10fb17418d014e46928a6571060c50dbbe7384`,
le reçu `failed_closed` `90490d87ff8ff80e0b6bbc7f7f0c5e5f638ba9da196e504e586e22a7a78bc694`.
Le recul est tenté avant le refus ; aucun succès feu/IA n'est attribué à ce résultat.
Fire `005` ferme sur un refus antérieur à l'allumage : les impulsions publiques
ne placent pas le joueur à 4 cm du point `(17.05, -267.95)` dans le délai de
12 secondes. Le ledger `failed_closed` est conservé au SHA
`009fddb427f31cbcaab6fb6f3b5213b2e261d297b5aa63ea5aa68249d3464c6f`.
La promesse d'attente du focus n'est donc pas exercée par cette tentative et
aucun gain de délai d'exposition ne lui est attribué. Le contrôleur d'approche
est analysé séparément, sans modifier la cible, la tolérance, le budget ou le produit.
Fire `006` ferme sur un refus de pilotage après l'allumage. L'approche fine passe,
la première touche de recul est acquittée à `916.9506 ms`, dans le budget inchangé
de `1000 ms`, et la sauvegarde du feu reste cohérente avec la pose initiale
(écart `4.06 mm`, feu allumé). Le déplacement suivant oscille autour du point
de recul et ne l'atteint pas dans les douze secondes ; la vitalité finale est 50.
Le ledger `failed_closed` a pour SHA
`a9454215c51448b66566e64e6870b0d1fad56787c88d04f16e3522c6955b404d`.
Le natif et ses neuf WebViews identifiés sortent avec exit0. Le monitor reste
`failed_closed`, car le second parcours n'a pas été exécuté ; aucune qualification
feu/IA ou reprise n'est attribuée à ce résultat. Le même défaut de maintien des
touches pendant les lectures est corrigé seulement pour le segment de recul,
en conservant sa cible, sa tolérance et son délai.
Fire `007` conserve ensuite un nouveau refus de délai : premier ACK de recul
à `1205.0577 ms`, au-delà du budget conservé de `1000 ms`. Le recul est réellement
exécuté avant le refus de validation de sauvegarde ; les processus d'essai sont
fermés sans arrêt forcé. Le résultat reste `failed_closed`, sans lecteur qualifiant.
Son ledger est épinglé au SHA
`62f8fa060bb4e35cdc216fcaab28959b2a9b6dd8111c83037655fd0c8095b837`.
Le recul atteint réellement la cible à `0.13654 m` (tolérance inchangée `0.15 m`),
avec joueur au sol, entrée active et vitalité 21. La sauvegarde d'exposition conserve
74 points de vitalité et une pose cohérente à `4.09 mm`. Ces observations partielles
ne ferment pas le cycle complet. Le snapshot IA arrive après la fuite : ticks HUD
`7701` et navigation `7710`, PNJ déjà Idle et vitesses horizontales nulles.
Le budget de `1000 ms` est une garde de cette acquisition et de ses deux lecteurs ;
R3-B ne le chiffre pas directement. Il reste obligatoire pour cette recette.
Aucun résultat échoué n'est requalifié et aucun seuil n'est assoupli.
Le monitor `24925d88fa040325bcd70887ce47bcedad6a2c4eb2725dc765b5714b93ce77e7`
reste lui aussi `failed_closed` : un seul parcours observé sur les deux attendus,
aucune identité retenue restante, mais aucun drain complet qualifié.
Fire `008` ferme ensuite avant l'allumage : le pilotage n'atteint pas le point
nominal `(17.05, -267.95)` à `0.04 m` près en douze secondes. Le minimum observé
est `0.043971954 m`, au tick `7382`, avec vitesse horizontale `0.000936 m/s`,
joueur au sol, entrée active et simulation prête. Une impulsion suivante de
35 ms nominaux dépasse la correction nécessaire de près de `0.479 m` en Z.
Les 35 observations sont fraîches et les 28 impulsions ont leurs KeyUp et ACK
confirmés. Ces données révèlent une réponse de pilotage irrégulière et couplée
en X/Z ; elles ne prouvent ni obstacle infranchissable ni traitement tardif
du KeyUp par le moteur. L'état stable ensuite sauvegardé reste dans la portée
publique d'allumage, mais aucun Ignite, chaleur ou mouvement IA causal n'est
observé par cette tentative. Son ledger conserve `failed_closed`, SHA
`89185277f59f6c23146bfd01c685300a9636f215e33adf032fccca9cdda04e28`.
Le natif et ses huit WebViews retenus sortent avec exit0 ; le monitor
`da46111aa997d4b6066abb04d3378f6c8ae6d5b79d2c16c4a89b1abf810c0f51`
reste refusé, car un seul des deux parcours attendus a été exécuté.

Après distinction des critères, une nouvelle recette est préparée avec cadrage
du point nominal à `0.08 m`. Le cadrage nominal à 4 cm est propre au contrôleur,
sans exigence correspondante dans R3-B ou les lecteurs causaux. Cette évolution
est explicite et ne requalifie aucun refus antérieur. Les contrôles de pose réelle
Save/public à `0.08 m`, de pose d'exposition/source/frame à `0.25 m`, les ticks,
la chaleur réellement simulée, la vitalité, le budget de démarrage `1000 ms` et
les observations KCC/IA causales restent inchangés. Les sources produit et le
Studio restent ceux de `019` ; aucun résultat de cette recette n'est anticipé.

La recette `009` est ensuite exécutée une seule fois, après fermeture normale
de Unity dans l'autre chat. Elle franchit l'approche nominale et atteint Ignite,
mais refuse la séquence complète : premier ACK de retrait à `1336.187 ms`,
au-delà du budget conservé de `1000 ms`. Le repli est tenté avant le refus ;
Run2/Resume n'est pas exécuté. Le ledger reste `failed_closed`, SHA
`f3874b54dcc99587db347705625cb84fd19c8e4916908ee96b5333b14ac5b34d`.
L'ACK Ignite est observé à `408.5761 ms`, celui de Save à `412.2714 ms`,
le focus public à `427.6959 ms`. La promesse de capture attend `902.7672 ms`
avant de rendre la garde publique à `1333.596 ms`, suivie du premier key ACK.
Ce temps d'attente ne constitue ni du CPU exclusif ni une cause GPU établie.
Le retrait atteint sa cible à `6221.999 ms`, à `0.148462 m` du marker pour
une tolérance de `0.15 m`, avec joueur au sol, entrée active et simulation prête,
encore à `52 PV`. La Save d'exposition contient `92 PV`, une transaction résolue
et une pose cohérente à `5.216 mm` près. Aucun snapshot Moving précoce n'est capturé.
Le fallback brut a des ticks HUD/nav/feedback concordants à `7677`, mais le
Veilleur est `generating`, provider `None`, avec vitesses horizontales nulles.
Une cible et un état de fuite sauvegardé ne prouvent donc pas un mouvement IA réel.
Le reçu d'étape `44df1d60699fdd68712e44a8206c4189f86d5d8aa727865c575d25c76921cf04`
reste lui aussi refusé ; son contrôle canonique après parcours est absent,
et aucune lecture de qualification feu/IA complète n'est lancée.
Le natif PID `20280` et ses huit WebViews retenus sortent avec exit0, sans
arrêt forcé. Le monitor
`14600cf57ee5b00ac2aa91ff17065653dc1133f05a56a63f7f62382ccb43e50e`
conserve `failed_closed` et `drain_completed=false` : un parcours observé sur
deux attendus, aucune identité retenue restante. L'absence de Studio/Unity
est constatée ensuite par CIM. Aucun drain complet ni PASS ne sont inférés.
Aucune nouvelle relance n'est préparée ; la correction de la latence exige
une investigation différente d'un nouvel ajustement de placement.

Le retry P04 `008` démarre séparément avec le contrat console
corrigé, en épinglant les parents normaux `007` sur place ; les sources produit et
le Studio restent ceux de `019`. Les numéros distinguent les preuves et refus
conservés, sans autre version du produit.
Le retry P04 `008` ferme ensuite normalement. NavigationRuntime `008` ferme
également normalement, avec l'aide publique d'origine et les critères inchangés.
Son lecteur indépendant `navigation-008.reader.json` ferme `OBSERVED_COMPLETE`
(SHA `1b36ee7cdd1814046e850e7f54bdaf96a3b6c546b2d1e36c94965df8a90bc03c`).
Le raccord `browser-wasm-navigation-join.json` ferme ensuite le périmètre réel
navigateur de ce parcours (SHA
`8a3ff61d79d1150c67fec4a5cf021299fff8c66da9595a943b16150e12914ac4`).
Le shell Win32 héberge le frontend WASM dans WebView2 ; la simulation Bevy,
le gameplay et les branches de navigation concernées s'exécutent dans ce frontend.
La branche WASM passe exclusivement par le worker pour produire les tuiles.
Le provider recomputé sur les 28 entrées
`0bcaff877304f3228bc423b1cb2878963f070d1293f495bfb34f6ee180565320`
correspond aux deux WASM courants et aux 33 observations de navigation.
Ces observations ferment 20 cas sans chemin porte fermée, puis 8 états Moving
et 3 Reached après ouverture, avec reconstruction des tuiles et trajet extérieur
de 19,81165 m. Les trois artefacts, 29 sources du worker, le dist courant et
leur chaîne de build/embarquement restent appariés au même Studio et aux
processus WebView2 réellement observés. Il ne s'agit pas d'un nouveau lancement.
Ce raccord ne revendique ni Edge autonome, ni trace CDP directe du worker,
ni parité de plateformes, ni qualification du rendu ou acceptation humaine.
La première lecture P04 conserve son refus de chemin parent. La correction
externe `019-002` autorise seulement le fixture d'auteur exact déjà authentifié,
avec dix contrôles de données et refus spécifiques ; elle ne change pas le produit.
Son lecteur CLI réel ferme `CURRENT_SCOPE_EVIDENCE_PASS`, au SHA
`1d46e92ac4b35a9cfb38ba12ecb43e41bf63299739292720005b9a08c15c71e1`.
Il vérifie la sauvegarde Run1 et le nouveau départ Run2 ainsi que leurs sorties
natives normales sur le candidat `019`. Le verdict global de Gate 3 reste faux.
L'opérateur de dégagement `008` est refusé avant admission et avant toute touche :
son import AST perd les paramètres de certaines fonctions courtes. Son journal
est vide et son trigger reste nul. Aucune réussite ni conclusion causale de
dégagement ne lui est attribuée ; le succès appartient au parcours original.

## Historique conservé des acquisitions et de la correction commune

Reprise du 11 octobre, après la fermeture native à 22:43 UTC : le checkout et le cache communs sont
inchangés pendant les acquisitions `016`. Les numéros identifient des lots de preuves,
pas des applications propres à chaque conversation. La navigation runtime
`003` est désormais fermée normalement et son lecteur indépendant passe.
RPG/P01, P02, P03 et P04 passent leurs lecteurs indépendants. P02 ferme les
préimages de sauvegarde sur leurs copies protégées dans RPG et les fichiers
courants séparément, sans modifier les anciens reçus.
Les deux refus précédents de navigation sont conservés ci-dessous.

EnvironmentQuest et ShorePush terminent normalement avec sauvegarde et reprise
dans un nouveau processus. Les lectures causales EnvironmentQuest et ShorePush
passent. La clôture finale `public-reader-p02-preimage-candidate-016-001/closure.json`
(SHA `d44274ad52e90ffe86a0847a6bbd3dda43214e8e712e556a40552d8b07e3b418`)
rouvre le candidat et les 2539 entrées inchangées avant/après ces lectures.
EnvironmentFireQuest conserve un échec réel : le contrôleur WASD oscille autour
du waypoint `(17.05, -267.6)` demandé à 10 cm près. La session ferme sans arrêt
forcé ; aucun succès feu/IA temporel n'est déduit de cette tentative.

La capture RPG montre aussi une pièce trop sombre : ses surfaces procédurales
utilisent StandardMaterial alors que les récepteurs d'éclairage de la scène
importée résident dans VillageMaterial. Le correctif commun est appliqué après
la clôture des lectures `016`, puis soumis aux contrôles ci-dessous. Il conserve les surfaces, géométries,
identités et colliders, et raccorde la pièce aux récepteurs existants. Il doit
être vérifié visuellement avant acceptation ; sa compilation est désormais fermée, mais les preuves `016`
ne qualifient pas automatiquement un exécutable reconstruit. L'approche WASD
du feu passe à la tolérance de déplacement de 75 cm ; les impulsions finales
à 4 cm et les contrôles de portée sur la sauvegarde réelle restent inchangés.
La validation commune `017` conserve un échec de compilation E0596 avant les
tests : la référence du mesh à enrichir devait être mutable. Ce point est
corrigé avec une garde contre une pièce dérivée disparue lors du retour au
matériau habituel, couverte par la régression de projection. La reprise `018`
conserve ensuite un refus Clippy (passage par valeur inutile, must_use et
comparaison exacte des paramètres neutres), avant les tests. Les corrections
sont appliquées au même checkout. L'essai ciblé `019-001` passe la régression
de géométrie, collision, rebind et nettoyage, ainsi que deux tests de matériau.
Le test Naga refuse ensuite à tort le retour implicite ajouté à une fonction
WGSL vide ; sa correction accepte exactement ce retour sans valeur, et aucune
opération modifiant la surface. L'essai `019-002` conserve un refus de longueur
du nouveau scénario de test, corrigé par une exemption limitée à ce seul test
continu. Aucun shader de production ni assertion de comportement n'est retiré.
Clippy strict de la bibliothèque et de ses tests ferme `019-003` avec un succès
sur les sources `9c46a4d87e9264ee2d082902e743de961d17731d30c5b7d9bc705fa6a85269e5`,
inchangées pendant l'acquisition. Les quatre tests ciblés ferment réellement
avec succès à 22:14 UTC, sans modification des sources. La validation native
complète `019` ferme à 22:43 UTC : format, diff et Clippy strict workspace/all-targets
passent, ainsi que 2102 tests, avec 55 ignorés et 221 suites. Les 221 binaires
exécutés sont épinglés et les 2539 entrées de compilation restent inchangées.
Le reçu natif a pour SHA
`cfc8f9649d276f86c469ac37022b0c5d1823aa07836ab98a47d4c95e917b3664`.
Clippy strict ferme également les outils de livraison, le Hub et le launcher
avec ses fonctionnalités de production. Les contrôles deny/audit et les 34
replays privés dans 24 groupes passent sur la même empreinte. La clôture des
replays (`private-replay-closure.json`, SHA
`c590e2008bbf53826dee0d00f111758e919b15dd7253af57f9fd61f725da5223`)
rouvre les entrées, journaux, artefacts et binaires effectivement utilisés.
La politique RUSTSEC reste inchangée ; son blocage de distribution GLib n'est
pas transformé en succès de livraison. WASM `019` ferme réellement avec succès
à 23:04 UTC : contrôle WebGPU du moteur et bundle Studio/Trunk/hooks passent,
avec les 2539 entrées inchangées. Le reçu WASM a pour SHA
`7293fd9f22881bc4462d4cdf452d144138d23e3f5635bb3a7ba42097ff494300`.
La release `019` ferme à 23:26 UTC avec succès, runtime et Studio, sources inchangées.
Le Studio commun reconstruit a pour SHA
`27f16ff2da18564f88fe7354e3b72abc8f6b75dc7b299771c6e1fd2e089df551`
(183850496 octets). L'admission `studio-admission-current.json` (SHA
`dec4f223cc041e60a4680487709319e80755e5666869f62b2bfc4eb087886918`)
rouvre ces preuves et les 3023 fichiers du frontend courant. L'adoption des
sorties frontend/Studio exécute zéro nouveau build. La campagne publique `007`
démarre ses étapes fonctionnelles sur ce candidat, pendant le seul build CPU
Game vers son exécutable distinct. RPG et la mesure courante attendent la clôture
Game. La pipeline locale complète ferme réellement à 23:46 UTC :
`pipeline-closure-current.json`, SHA
`fe60f50adc4d34c348892a0354b9c091ef336ef263aa4deaa4cdfeed08ee1f17`.
Game est construit (SHA `cc60114a4c2b32a0167fdb9943a69cb669e865ebe31d99fb7e822bfee0268d78`,
137440768 octets) ; Studio/frontend et les 2539 entrées sont réouverts inchangés.
Aucun succès GPU ou Linux n'est déduit de cette fermeture de build. Les étapes `007`
Base, Physics et NavigationAuthoring ferment normalement avec succès sur ce
Studio : sauvegardes et 21 placements conservés, processus natifs exit0 et
WebViews fermés sans arrêt forcé. NavigationRuntime conserve ensuite un refus
réel : la fenêtre `opened_exit` ne valide pas sous 60 secondes les reçus KCC
du personnage stationnaire (status/support/tile au même pas). Son ledger déclare
les processus fermés, sans arrêt forcé, et les préimages intactes ; le reçu
observateur est `failed_closed`, sans reçu final de monitoring/canonique.
L'analyse des échantillons précède toute correction ou nouvelle tentative.
Elle désigne le Messager de la salle : support au sol, même pas et tuiles sont
valides, mais l'arrivée ne s'accomplit pas. Le joueur se trouve à 0,77345 m XZ
du point de blocage, à la frontière du rayon d'évitement combiné d'environ
0,77426 m ; la tentative `016` laissait environ 1,58 m. Un essai causal réel
avec déplacement public du joueur reste nécessaire avant de conclure.
P03 `007` ferme normalement. P04 conserve un refus de monitoring malgré son
ledger fonctionnel `passed` : une console Run2 déjà identifiée, même naissance
et même parent, perd son chemin d'exécutable CIM pendant la fermeture ; le
contrat interrompt alors le suivi avant de consigner l'exit/drain Run2. Ce manque
de preuve n'est pas transformé en PASS ni en crash supposé. EnvironmentQuest
`007` ferme ensuite normalement, avec sauvegarde et reprise dans un nouveau
processus ; sa lecture causale indépendante reste à exécuter.
Aucun PASS natif `017` ou `018` n'est attribué.

P05, la mesure courante à résolution déclarée et la CI sur les corrections
publiées restent distinctes et incomplètes. Gate 3 reste ouverte.
Les journaux de la CI `38123749165` sur `48a894d550c5770a5ea50bc54707113e85ab5218`
confirment deux erreurs Clippy dans `main.rs` sur Windows/Linux et un appel
`std::os::windows` non protégé dans `asset_library_native.rs` sur Linux. Ces trois
emplacements sont corrigés dans le checkout commun actuel : emprunt de la requête,
`ToString::to_string` et variantes de symlink sous `cfg(windows)`/`cfg(unix)`.
Les jobs natifs ayant sauté leurs tests et builds après ces refus, cette comparaison
des sources ne remplace pas une nouvelle CI PASS sur la révision finale publiée.

L'inventaire de publication est préparé en lecture seule, avec un index vide.
Le manifeste `019` correspond exactement au checkout : 1934 entrées suivies
inchangées, 135 suivies modifiées, 459 non suivies et 11 locks ignorés. Parmi
les dépendances nouvelles réellement compilées, l'exemple
`crates/forge_engine/examples/verify_townsmith_material_layouts.rs` inclut
`scripts/hivemind/candidates/SourceTownsmithBevyPipelineAdmissionV1.rs` : exclure
Hivemind en bloc casserait ce build. Les autres prototypes Hivemind, le diagnostic
GPU indépendant, les sorties générées Aube et les travaux hors manifeste doivent
être traités séparément. Les ressources NSIS déclarées privées et les propositions
visuelles Aube ne sont pas déclarées distribuables par ce PASS local. Aucun commit
ni push n'est déduit de l'inventaire ; la reproduction des entrées locales devra
être explicitée dans le lot proposé à publication.

Gate 3 reste ouverte ; Gate 4 n'est pas ouverte. Les lots `014` et `015`
conservent leurs résultats réels, sans les réattribuer aux nouvelles sources.
Les numéros de lots désignent des preuves datées ; tous les chats travaillent
sur les mêmes sources et le même cache Cargo. Le lot historique `016` ferme
la validation et la release communes, mais pas le parcours public complet.

La correction Studio et la recette de livraison passent la validation native
`015` : format, whitespace, Clippy strict et 2094 tests réussis, 55 ignorés,
221 suites/binaires. Les 12 nouveaux tests de collision passent. Les entrées
de build sont inchangées pendant cette validation, d'empreinte
`9b953f07e94e51a771ee97fa7204fbff5a97ae1e52f5bbbc59fcaac0a2c70e3c`.
Reçu natif SHA `e1a4e6ff8c28bd96de612011ff6a095191cf07aa1c4fc1f49a28a1da72ae5844`.
Le lecteur du jeu natif accepte maintenant la même politique optionnelle du
catalogue sans changer sa triangulation existante. La validation finale `016`
est fermée PASS : format, whitespace, Clippy strict, 2098 tests réussis,
55 ignorés et 221 suites/binaires. Les quatre nouveaux tests du lecteur passent.
L'empreinte courante des sources est
`248fe8337499bd4517d520f3c119f172fc246435a7f7cb960c9b82a82b3c8463` ;
reçu natif SHA `988ad5030112dc2cdc64bde532c1d6fc0204effe1828c0abffbd3a4f2db37882`.
Supply et les Clippy indépendants CLI/Hub/Aube passent aussi sur ces sources.
Les 34 replays privés dans 24 groupes passent sur les binaires `016`, reçu SHA
`0a3697f361118687420d979aa29f8b4c315a06272d93366596366f04d8089637`.
WASM `016` est fermé PASS avec sources inchangées, reçu SHA
`fbe2d5d438923c35afb20e7fefb7096f1488a77e6387551d16fee80493b88376`.
Release `016` est fermée PASS : runtime puis Studio ont terminé avec exit 0,
sources et frontend inchangés. Reçu `shared-016/release-result.json` SHA
`3d37010f8592df755d393d16159bfe4c322761f3ac24fc4a4c61d2fbc5012148`.
Le Studio unique `E:\OpenRPG_Forge\.gate3-ntfs-cargo-20261002\release\forge-studio.exe`
est SHA `d5a5bdae722aa7330f956ad855e135fbbb8e93e178ffd07db26fff4c46d7621b`,
183840768 octets. Le runtime est SHA
`beca6109beb7df8cb062f613a66e6e285148d0a08ee51d1f38bc9cbf682c5a92`,
88423936 octets. La vérification canonique exécutée après fermeture retourne
`VALIDATED_INPUTS`, exit 0 : `shared-016/canonical-candidate-closure-001.json`
SHA `3603fb2beec22dc800cf4c6e811bc0ad262094ab8f12d0d17526a79dcfb8b3b2` ;
reçu d'exécution `canonical-candidate-verification-001.json` SHA
`3995597b8a5ab857cd2238c308ac13afe8995d4c0a106b676728f070034bcfd1`.
Ces reçus authentifient les entrées et les sorties de construction ; aucun verdict
RPG complet, High, humain ou CI n'en est déduit. La construction Game `016`
est maintenant fermée `BUILD_ONLY` : la commande Cargo réelle a réussi,
les entrées de build sont inchangées et le journal conserve la fin du profil dev
optimisé. Le binaire commun `debug/examples/tidewater_plant_probe.exe` est SHA
`925a2f9a16ba194cb9b489019429d1f8f16f6af8566379344a3c501433676cdb`,
137444864 octets. Reçu `shared-delivery-native-016/game-result.json` SHA
`995785838b3fce900ab0b108e39dcf2ce28bd30befdf23e69050031b38ea3d41` ;
preuve de construction SHA
`deddfd110071ee99faf70964b7ca1bd03bc38628e55bddfd4f5e302dae26fca8`.
Ce résultat ne prouve ni lancement du jeu, ni RPG livré, ni profil High.

La reprise en lecture seule confirme les mêmes 2539 entrées et l'empreinte
`248fe8337499bd4517d520f3c119f172fc246435a7f7cb960c9b82a82b3c8463`,
le Studio `d5a5…`, le jeu ci-dessus et le catalogue de collisions explicites SHA
`1f6df0c360c15ebc30414ad7c2c5ab45f92da82a28579de9b9e73f456f4280da`.
Les fermetures native/WASM/release, les sorties de construction et les pins des
reçus publics ci-dessous ont été rouverts. Aucune reconstruction, copie produit
ou exécution GPU n'est effectuée pour cette reprise. Les observations sont dans
`shared-resumption-016-001/observations.json`, SHA
`eed623fdc3eab2c97f1a6ee92a000855ec9510acbfb4f9fb982ef2e5b53ad8be`.

Le parcours `public-current21-final-006` passe Base, Physics,
NavigationAuthoring, RPG/P01 et P02 sur le Studio exact `d5a5…` : les cinq
ledgers sont `passed` et leurs observateurs `public_helper_completed_normally`.
Les deux runs RPG puis les deux runs de retouche compatible sont complets ;
les sources protégées sont conservées et la fermeture n'est pas forcée.
Les reçus de stage et leurs ledgers sont épinglés dans le reçu de reprise.
RPG : reçu SHA `d3b067f90cbe0d33d88243379c73d3dfbdcef2a2cd4beed2a383b024b036d5c4`,
ledger SHA `57cdfacacc770805e82a17431ccf13865c1098ee6913a558475cbf83549d468b`.
P02 : reçu SHA `b4a2a4a6649d1bc29dd52fb853eec3d92f4dd19ef230f13eb60d0fd228a8e977`,
ledger SHA `651e1d82c4799c0f2f1ab559e117a6a625e33d60064da5f73eacacecb2d00d3c`.
Ces PASS fonctionnels locaux ne constituent pas P05, High ou un verdict humain.

NavigationRuntime `006` conserve deux refus distincts. Le premier ledger est
`observed_complete` avec 37 échantillons, mais son observateur est `failed_closed`
car les sorties natives ou le drain WebView observé n'ont pas été vérifiés.
Reçu SHA `9a681636136bd1c47357a0610205e7f5b168571ff032202b173b15b69917f45c` ;
ledger SHA `835b4e8658064a0de7c5e59578448ee759435a79aeca2cef38abdbc56fb9dff3`.
La tentative séparée `002` est `failed_closed` au ledger et à l'observateur,
avec 39 échantillons : fermeture distante WebSocket sans handshake complet,
et fermeture forcée indiquée par le helper. Sources et archive d'auteur sont
conservées ; aucun succès de clôture normale n'est inféré.
Reçu SHA `4a695d57d6f393db8cf303e1c60d42978091e87e4b76ece3463f3b1f82ae513d` ;
ledger SHA `fd9571897b4ab4815a7316bb7d5c850f547cd1ee1073d607615dd4c11d32e963`.

Le parcours public courant `public-current21-final-005` passe Base, Physics
et NavigationAuthoring sur ce même Studio. Les trois ledgers sont `passed`,
et leurs reçus de stage sont `public_helper_completed_normally` : respectivement
SHA `eb7c1f304b797a39233416603092aa90c889e95a8b69e58cabc6035863c33262`,
`b2cb3c838f586cf940012971a9652ef799ff0f1558a813a2ff2884ea448fc205`
et `437ac8cc73b70a2db9f3e903bd529567c7966714ab4bf54f01be10a61c11e5c7`.
NavigationRuntime est fermé `failed_closed` après 64 échantillons : les ticks
observés vont de 250 à 3632, la simulation est prête, des supports KCC au sol et
des tuiles `ready` sont observés. Les poses ordonnées requises des deux côtés du
raccord, avec mouvement supporté à la jonction et reçus des deux tuiles requises,
ne sont cependant pas attestées dans la fenêtre de 60 secondes.
Reçu de stage SHA
`2f21bb9764fb5b7bacdc82563a044bd790c6e43d712014047f8775a906667918` ;
ledger `04-navigation-runtime/gate3-navigation-public-playtest-ledger.json` SHA
`8331add8af979415838e5330e8d0509157e58759c3e544380ec72a882c3f3f33`.
Ce ledger conserve les sources et l'archive d'auteur, indique la fermeture
de ses processus possédés sans arrêt forcé, et reste non qualifié.
Les contrôles P03 et P04 de cette même campagne `005` sont ensuite fermés
`passed`, avec observateurs `public_helper_completed_normally` et sources
préservées. Reçus respectifs SHA
`03086bad868f0425505effea1f6c28ed5f2f4e482126418458150ea6ca245aa4`
et `8940777a8494f531bd1ead88ceb82b9ee51a6173628ff16b5d47283b43b7386f` ;
ledgers SHA `62e9d6b838982315f6b91b08f7a2842b53abe444c7b6e0b42f5a87dca76ed92b`
et `7d56aabdcaad90ccfdf6c50518604c56fda92fe807a28ddccee93634c6871598`.

Le parcours public historique `public-current21-final-004` passe Base, Physics et
NavigationAuthoring. NavigationRuntime échoue encore : les objets sont
visuellement prêts, mais la simulation reste au tick 0. Le reçu de stage est
SHA `d01f39990d15931fd58921f2b8beef2aab43408f9a74213bb658d2e50a026802`.
Le diagnostic public lit « aucune primitive UCX ». Les quatre modèles du module
ont leurs SHA attendus, mais ne contiennent aucune déclaration UCX, UTM ou
`openrpg_collision`. Les 15 maisons ordinaires ont donc des attentes physiques
vides : la readiness visuelle ne satisfait pas la readiness physique.

La correction ajoute une politique explicite de triangles statiques au catalogue
du module et la transporte jusqu'à la projection physique du Studio commun.
Les GLB et les 21 placements sont conservés. Le lancement portable de livraison
doit aussi fournir ce catalogue authentifié. Ces corrections ne valent pas
à elles seules preuve du parcours complet ni passage de gate.

Le moniteur de `final-004` a séparément refusé un nouveau descendant dont le
chemin exécutable CIM était vide ; aucun code de sortie ni drain n'est inféré
de ce refus. Les mesures natives `014` sont closes : à 1080p, moyenne 62,58 FPS,
p95 18,874 ms et 1 % low 38,19 FPS ; à 1440p, moyenne 93,62 FPS,
p95 12,4266 ms et 1 % low 68,55 FPS. Le profil High effectif n'a pas été établi ;
ces mesures ne ferment donc pas le critère de performance High.

## Résultats conservés du lot commun `014`

Le lot `014` valide les 2539 entrées de build communes d'empreinte
`9f6e9bf9be70d5803e812c21d38773eda6f5ffec1e87a2217d331f3bfab5d490`.
La validation native est fermée PASS : format, whitespace, Clippy strict puis
2082 tests réussis, 55 ignorés et 221 suites/binaires authentifiés.
Reçu natif SHA `07c8df22fc824e31a064eb476da6171ed76f2247ba5fe7b4870e23ef6325ba29` ;
réouverture indépendante des 221 binaires SHA
`1de34276aba71900e58803bc3a0bb6195a67b4e28c871b494e95477febee9f5e`.
Les replays privés passent réellement 34 tests dans 24 groupes ; ils couvrent
34 des 55 tests ignorés, sans attribuer de résultat aux 21 autres.
Reçu SHA `f781ea59d3529d2e1bd4f0126e6118746e05e69e2cb99a5ca3aab690790157a8`.
Les Clippy indépendants CLI, Hub et Aube sont fermés PASS (`shared-independent-clippy-008`).
Leur fermeture avec les replays privés est authentifiée par le SHA
`e16be2c07309592dd1a1b5c9d2de855702b7241d9fc4ec60b7ecc7e83d5bd152`.

Supply `014` est fermé PASS, avec 0 vulnérabilité connue, 10 avertissements et
le bloqueur de distribution GLib `RUSTSEC-2024-0429` conservé.
Reçu SHA `8dbe22e03dcb3cc441e11f00ada6c52f0ff5154e47cd4f3a8572d47b947a3117` ;
fermeture de politique SHA `821c059927db3f6d11a28515387ba812d8cc816e1bedcfd42e04ab1a2e68c6d7`.

Le premier assemblage WASM `014` a compilé, mais son hook avait reçu un chemin
d'assets absent dans le checkout Rust. La release a correctement refusé avant
Cargo l'absence de `forge-imports` et `forge-kit`. Les huit preuves originales,
le reçu d'assets absent et l'index de déplacement sont conservés dans
`shared-014/historical/wasm-assets-absent-001`, sans réécriture des reçus.
Le chemin réel est `E:\OpenRPG_Forge\.forge-local\studio-assets` ; son manifeste
acquis SHA `3d7f2dd80580b65dee3da1b88b3d826d0d98aac940621f85be794787dcb18ac6`
est inchangé. La nouvelle exécution WASM puis release `014`, commencée à
15:50 UTC avec ce paramètre corrigé, est fermée PASS. Les 3023 fichiers du dist
incluent les 3011 assets locaux ; leur reçu de staging SHA est
`47b087a59284910ce85497ffda1ab8a4f1412d8599ce2c020431b9e279ccaecd`.
Reçu WASM SHA `5dcabca4ffb3015a1229df9eead881ca60b574b5af0d03ce8edcf4479e4a6bf8` ;
reçu release SHA `a88dec2660ea38aedaa4f14dcbeafb0f7aafacab92b37c01f164e4bdab8baef4`.
Le Studio réel est authentifié : SHA
`37dc15d0f4805d2661c07505576ae1957412d6bfdc4b5402a802a097ed1f756a`,
183825920 octets. Le runtime natif est SHA
`beca6109beb7df8cb062f613a66e6e285148d0a08ee51d1f38bc9cbf682c5a92`,
88423936 octets. La clôture canonique `VALIDATED_INPUTS` a été exécutée après
fermeture de la construction ; SHA
`984d01d6b86288bcfb9607b3687abd48cad3a807216a1c6aff5d9884043ae3e3`.
La correction du paramètre d'exécution ne change aucune entrée de build ;
les reçus natifs, supply, indépendants et privés restent ceux de leurs exécutions réelles.

La première étape Base publique a échoué fermée : sa garde de readiness attendait
le composant d'information de zone désormais absent, alors que la carte était
affichée. Le suivi de processus a aussi refusé le `conhost.exe` Windows du Studio
avant d'observer sa sortie ; aucun code de sortie non nul n'en est déduit.
Le ledger échoué SHA
`a58e5fd037678869e12e012e35e935d1782f57f667383613a2f3998ee756b44d`
et tous ses outputs restent conservés dans `public-current21`.

L'adoption historique de livraison `012` a refusé une différence d'ordre de liste : ses 3023
chemins/SHA/tailles sont identiques, mais WindowsPath et chaînes POSIX n'avaient
pas le même tri. La correction du tri et de la garde de carte est faite dans
les sources communes. Le raccord P04 utilise maintenant l'identité et la révision
du document accepté dans le DOM ; le feedback d'action est lu dans son élément
public `aria-live`. Le suivi de console reconnaît uniquement le `conhost.exe`
Windows directement lié au processus natif observé, sans lui attribuer de sortie.
Ces corrections sont terminées ; leurs contrôles ciblés sont conservés, mais
Le parcours Base sur cette release a terminé deux fois `passed`, sans fermeture
forcée, dans `public-current21-final-001` puis `public-current21-final-002`.
Les observateurs restent `failed_closed` : le premier a figé un ExitTime WebView
sentinelle malgré une sortie native 0 ; le second a refusé une identité exécutable
CIM devenue différente ou illisible avant de fermer ses observations. Le snapshot
refusé n'étant pas enregistré par cet ancien observateur, ce second cas ne permet
pas d'identifier le PID ni de distinguer chemin vide et chemin différent.
Les relevés de sortie, métadonnées CIM et consoles déjà observées sont maintenant
corrigés. Les deux essais originaux sont conservés, sans transformer leurs refus
en PASS. Le nouvel essai réel Base dans `public-current21-final-003` est fermé
`public_helper_completed_normally` : source, dist, candidat et fixture d'origine
inchangés, sortie native 0, six WebView observés et fermeture de ces identités vérifiée.
Le drain global reste explicitement non prouvé. Reçu de stage SHA
`a9e2a4e9edd6bb454351ce637c3643c91aaf49badec7973924115fd327141b18`.
Les helpers externes figés sont Monitor SHA
`cf3330747a12ace1e15871bd8250ce2c39a100272230cbdd0ab2634dab63f673`
et contrats SHA `24da894e1efededc659051ece9d803c629f1d8e6b9a80e2e5af72414d1559f61`.
Physics et NavigationAuthoring sont également fermés
`public_helper_completed_normally`, chacun avec sortie native 0, six WebView
observés et fermeture vérifiée dans la même portée. Leurs reçus sont SHA
`604b8254881ec727b53587ad9ead20143252843d41b6d99e26ba9729ef196e05`
et `2e6721b56b212386e9bbc9835e23e10e731136f2b443421ba373cf8cc37388ad`.
Le ledger final d'édition de navigation est SHA
`4ca86b050bb46cf8147431c9f52bb4e40d6e80a3419f650301a94a78b14e7a16`.
NavigationRuntime `final-003` est fermé `failed_closed` : après 120 secondes,
le rendu avance mais la simulation reste au tick 0, objets non projetés.
Reçu de stage SHA
`afe737cad7232a0334dcc0ad80a846e59e8ab35d37ef3839d8624b81d27757e1` ;
ledger de jeu SHA
`bb0914199775fbcf3fde5d96f29c17a638d913ac93b773a7c2a839dc4a69282e`.
Le helper ferme normalement son Studio sans arrêt forcé, mais le moniteur a
refusé auparavant l'acquisition du WebView transitoire 25240 disparu avant
GetProcessById : il ne prouve donc pas les sorties ni le drain de cet essai.
Le snapshot refusé SHA est
`81017dcb1d72b8942ae5c5ae7502004369c1a084770d5f5204d7fbbdb3a0bfbf`.

Le diagnostic public séparé dans l'onglet Objets est fermé avec sortie native
0, sans arrêt forcé ni modification du candidat/source. Reçu SHA
`969258c94a1ec4717003a008cb99d85ab57dffa85bfe3689b86d0beb9e865c27`.
Il lit exactement « Placements non projetés : Commande Tauri échouée : référence
du Vault inconnue ». Les trois variables de catalogue module étaient absentes
dans le lancement initial : les quatre types de maisons Quaternius cherchent
alors le Vault au lieu de leurs modèles de module existants.
Le catalogue réel `E:\OpenRPG_Forge\.forge-local\runtime-data\data\scene\static-assets.json`
est SHA `db2a133da336bbcde7587d75e1ba2a751a25f107e0ecab5a59920d3e6cf514c5` ;
les quatre GLB locaux ont les empreintes exactes sauvegardées dans les 21 placements.
Le deuxième diagnostic s'arrêtait sur une erreur transitoire avant publication
de la bibliothèque. Le troisième attend le chargement complet avec ce triplet,
sans nouveau build ni changement de carte. Il observe réellement `ready`,
époques 3/3, 179 placements demandés et 15 objets projetés. Le compteur Rapier
agrégé de 379 ne vérifie pas les collisions des maisons ; le feedback indique
« aucune primitive UCX ». Reçu SHA
`093925d236837d959f82d02df62294091ee2ffe3bba7f87b3a967db98b041515` :
source/candidat conservés, fermeture normale et sortie native réelle 0.
Aucun résultat RPG ou navigation en jeu n'est encore attribué.
L'édition physique ne prouve pas sa simulation en jeu.
Les données de livraison `shared-delivery-data-014` sont fermées
`DATA_ONLY_READY_FOR_REVIEW`, reçu SHA
`b123ddbc0060a5a171e666197134441c3fbb6600db2fb5cb02391a83e6234ef5`.
Elles reprennent exactement la carte NavAuthoring validée et les assets communs,
sans application copiée, Save injecté ou promotion dans les données utilisateur.

Les adoptions de livraison frontend/Studio `014` et la construction réelle du
jeu sont fermées `BUILD_ONLY`, sources strictes inchangées. Le jeu commun
`debug/examples/tidewater_plant_probe.exe` est SHA
`905f8309327f2ab3f3f280738995cbe4c8dde61bfa317dd66b79d80887ba24a7`,
137462784 octets. Reçu game SHA
`c09ce63d76186ad20b9f1f2dd195d77e015b908d1406b29391614a78befe8bff` ;
fermeture des trois étapes SHA
`80d7993dd3bcc1804f7bf15dc882debbe67a7022445ac9058b4a85b6c13897c0`.
Ces builds ne sont pas des preuves d'exécution GPU, de jeu ou d'installation.
Les PASS `012` conservent leur portée antérieure et ne sont pas rebaselinés.
Ni installation principale, ni Setup, ni publication Git n'ont été modifiés.
**Gate 3 reste ouverte ; Gate 4 n'est pas ouverte.**

## Version unique

Consigne du propriétaire : reprendre le travail des autres conversations et conserver
une seule version. `E:\OpenRPG_Forge\OpenRPG_Forge-Rust` est le checkout commun, avec
ses modifications et sources non suivies. Les copies `candidate-001` et `harness`
créées pour cette reprise ont été supprimées ; leur compilation a été arrêtée sans
résultat de test. Les logs historiques restent identifiés par leur portée d'origine.

HEAD publié : `48a894d550c5770a5ea50bc54707113e85ab5218`. Les sources communes
actuelles dépassent cette révision : HEAD seul ne décrit pas le produit à valider.
L'inventaire initial découvrait dix packages et 225 targets, dont 56 targets locales nouvelles.
Launcher, Hub et livraison disposent de workspaces indépendants, à contrôler aussi
lors de la reconstruction du produit commun.

## Corrections communes

- Symlink du test de confinement des vignettes adapté à Windows/Unix ; assertion
  de refus d'un fichier extérieur conservée.
- Normalisation CRLF en mémoire avant recherche des ancrages du shader sous-marin ;
  octets WGSL authentifiés inchangés.
- Corrections Clippy déjà présentes pour la lecture des modèles conservées avec les
  nouveaux transports physiques de la bibliothèque.
- Imports absolus d'un exemple Studio remplacés par des chemins relatifs pour Linux.
- Configuration Tauri commune ouvrant `index.html?authoring=1`, comme le Studio livré.
- Liste exacte du test de permissions Studio raccordée à la lecture physique
  d'assets déjà intégrée ; refus du changement de titre Tauri conservé.
- Helpers checkpoint/environnement acceptant un WebView explicitement épinglé,
  avec contrôle de version et SHA, sans retirer d'assertion.
- Formatage des trois fichiers signalés et seules fins de ligne de treize fichiers
  Rust signalés par Git corrigés ; shaders et vendor conservés.

## Validation fraîche

`scripts/Validate-Gate3WorkingTree.py` valide directement les sources communes.
Ses manifestes couvrent les entrées Cargo suivies, modifiées, non suivies et
supprimées ; les hashes avant/après interdisent un PASS si les entrées changent.
Preuves : `E:\OpenRPG_Forge\.gate3-evidence\20261011\shared-*`.

La table conserve les contrôles du lot `011`, antérieurs à la retouche C# et au
raccord des mesures. Ils décrivent cet état historique, pas les sources finales.
Le précheck `shared-timing-precheck-001` a refusé deux erreurs de compilation du
raccord Studio : ambiguïté Bevy/Leptos et appel de hash absent. Elles sont corrigées
par l'import explicite du trait Leptos et la fonction `bytes_sha256` du projet.
Le lot `002` ferme le frontend WASM (exit 0), puis refuse un lint de documentation
du transport `BuiltTile`. Cette forme est corrigée sans exception de lint.
Le lot `003` ferme format, whitespace, engine WebGPU et frontend Studio WASM,
puis refuse la longueur de deux fonctions étendues par le timing. Les blocs
d'actuation et pause/statuts sont extraits sans changer leur ordre ni leur span.
Le lot `004` compile ces corrections, puis refuse dix appels de hash dans les
nouvelles fixtures de tests. La même API `bytes_sha256` les remplace ; les
assertions restent conservées. Le précheck `005` valide ensuite ces sources.
Les refus restent conservés, sans résultat runtime anticipé.

Le précheck `005` termine PASS le 11 octobre à 13:40 UTC : les cinq commandes
(format, whitespace, moteur Web, frontend Studio Web et Clippy natif strict)
sortent avec 0, sur 2539 entrées de build inchangées. Empreinte courante :
`c83b12fd69b2faa28afe78628786ba8382481c12bf81696183d18fc7d554f666`.
Reçu relu et logs authentifiés : SHA
`d5720fda733073778c5e7f53bdeee0e5ac1a5f8d121f4dcce9fa20d3f70e416a`.
Les tests ne sont pas exécutés par ce précheck. La validation complète du lot
`shared-012` commence ensuite sur le checkout/cache communs ; aucun résultat
du lot historique `011` n'est transféré. Budget de campagne supplémentaire
50 GiB sur E:, marge minimale 100 GiB, admission dans
`shared-012-storage-admission.json`.

### État historique du lot commun `012`

La suite native termine PASS à 14:07:56 UTC : format, whitespace et Clippy
strict exit 0, puis 2082 tests réussis, 55 ignorés et 221 suites/binaires
authentifiés. Les seize nouveaux tests timing/navigation/profil figurent
dans le log avec résultat `ok`. Reçu réouvert :
`shared-012/native-result.json`, SHA
`9be3764acec203ed0ded653bd4b644e33cc9e4bb46c65b8bc254866e024507b5`.
Manifeste natif : SHA
`d41b0b02a09888712ad3fc5198bb47f8965cb058a6fc06480b48a9308fb0908c`.
Log des tests : SHA
`d0ef8cf58d40cc4c8ab3805f7e1b0d98d56a789eb7f7c9d3e5e5d55a82fdbc01`.
Les 221 pins d'exécutables sont ensuite recalculés depuis les chemins du log,
et les totaux sont recomptés dans le texte brut. La fermeture indépendante
`shared-012/native-binary-closure.json`, SHA
`12540f15347b078bdd34cfdfd97553fa6108d06c5580fe8a59741d63183b5b1c`,
confirme leur égalité avec le reçu ; aucune qualification runtime/GPU n'en est déduite.
L'audit supply-chain, indépendant de la compilation, a terminé PASS :
deny et audit exit 0, 0 vulnérabilité, 10 avertissements et 1 bloqueur
de distribution conservé (`RUSTSEC-2024-0429`, GLib 0.18.5).
Le reçu `shared-012/supply-result.json`, SHA
`9aa9824134e9f7935d97eb34930be4278d5fdd1b64b92b8bb6851c964851f210`,
et ses deux logs ont été réouverts. Sa carte actuelle de 2539 entrées et
son environnement égalent le manifeste natif complet `012`.
La politique RustSec hors carte de build est fermée avant/après dans
`shared-012/supply-policy-observation.json`, SHA
`fe32ee79b6be8938bf6110091136e42c208c7e04263753248bd6ef81a1511ee0`.
La liaison aux reçus natif/supply complets et à la politique encore actuelle
est fermée dans `shared-012/supply-policy-closure.json`, SHA
`e938b79a9d0254bff50372c4a197e00d66504eb864700521c4cf27236fd00bd3`.
`shared-independent-clippy-007` termine également PASS à 14:08:52 UTC :
CLI, Hub et Aube production exit 0, mêmes 2539 entrées courantes, 76 fichiers
frontend inchangés, logs/Cargo/driver réouverts. Reçu SHA
`87abe6d76f6234c88980b243215cd7defa6993912e773e5009b372e96b426359`.
L'environnement indépendant retire RUSTFLAGS, comme le builder réel ;
aucune équivalence binaire avec l'environnement principal n'en est déduite.
Les replays CPU `012` terminent PASS : 34/34 tests attendus, 24 groupes,
mêmes binaires natifs, entrées/logs/artifacts CPU réouverts. Reçu
`shared-012/private-replays-current.json`, SHA
`04b4cc656f9517c03cecbd176b1a8cc7f63d8c92b593e3d565cdfa02bc526cf2`.
Aucune exécution GPU n'est ajoutée ; les 55 tests ignorés de la suite native
ne sont pas revendiqués comme tous exécutés. WASM `012` termine ensuite PASS :
contrôle moteur WebGPU et Trunk Studio release exit 0, même empreinte de sources
et même environnement principal. Reçu SHA
`4752b7971adef9b45396393f76fc8703e4267605f9ec1c6a72a2fcb86d8a1fb4` ;
manifeste WASM SHA
`73c6d0540f5be10c1e7c4ed37e8d544c2f677999902df1a2b54fb604c6db62a0`.
Les 3023 fichiers du dist primaire sont réouverts : 431245785 octets,
3011 assets locaux acquis préservés. Manifeste worker SHA
`1cc56e237f9070a17a248d65bf5cc9c5e3932f67f396d4f5c177755e10ca3675` ;
reçu de staging d'assets SHA
`384e5290f091dd5243281c9005e8375b3382b4ebc1d967124baa9655de6bd2c1`.
Ces preuves restent BUILD_ONLY, sans exécution GPU/worker revendiquée.
Release native `012` a ensuite terminé dans le même cache : runtime puis Studio
natif embarquant ce dist exact. La première Base et l'adoption Studio de livraison
ont été exécutées puis refusées comme détaillé au point courant ci-dessus.
Les parcours restants, mesures réelles et installation ne sont pas terminés.
Aucun résultat du lot `011` n'est repris.

### Historique des validations antérieures

| Contrôle | État de cette reprise |
| --- | --- |
| Format commun et `git diff --check` | `shared-011` PASS, exit 0, sources stables |
| Clippy workspace/all-targets strict | `shared-011` PASS, exit 0, `-D warnings`, sans nouvelle exception de lint |
| Clippy CLI / Hub / Aube production | `shared-independent-clippy-006` PASS : trois commandes exit 0, sources et frontend stables, sans nouvelle préparation Node |
| Tests natifs | `shared-011` PASS : 2066 tests réussis, 55 ignorés, 221 suites/binaires exacts, sources stables |
| Replays privés CPU | `shared-011/private-replays-current.json` PASS : 34 tests attendus et réussis, 24 groupes, mêmes binaires natifs, entrées et sources préservées ; aucune exécution GPU |
| WASM et worker | `shared-011` PASS : deux commandes exit 0, sources stables, worker et 3023 fichiers frontend authentifiés ; portée BUILD_ONLY, sans exécution du worker ni preuve GPU. Le refus du lot `010` est conservé |
| Build release / livraison / setup | `shared-011` FAILED de stabilité : runtime-release exit 0 le 11 octobre à 12:54 UTC, 22 entrées modifiées pendant compilation ; Studio release non exécuté. Reçu conservé, aucun candidat final ni setup qualifié |
| Parcours publics, GPU et P05 actuels | À revalider sur le build commun apparié |
| Supply-chain | `shared-011` PASS : deny et audit exit 0, sources stables ; réserve GLib de distribution conservée |

### Reçus historiques `011` réouverts

Relecture du 11 octobre 2026 à 11:59 UTC : le manifeste `shared-011` contient
2531 entrées de build, empreinte
`058a3d3178bc1caa9575405aae02cf07a397e58bc573725748b1047bb0e4709e`.
Son SHA-256 est
`c6735bf9b1d79fb073618af2f5f73ad48b5afa9e959abfb30d35bbcff714e414`.
Les reçus natif, supply-chain et privé portent ce même manifeste ;
leurs sources avant/après sont inchangées. Le Clippy indépendant `006`
retrouve la même empreinte de build.

| Reçu conservé sous `E:\OpenRPG_Forge\.gate3-evidence\20261011` | SHA-256 | Résultat relu |
| --- | --- | --- |
| `shared-011/native-result.json` | `258d3de56314ddc8ab0ff479d0cc8e843757cb0103a5b83726afcfab1be1288b` | PASS ; quatre étapes exit 0, 2066/55/221 |
| `shared-independent-clippy-006/result.json` | `1cb963b56c6fcc115ac50781dc9a12e9e413734b31101d51efb9e609bf813ef2` | PASS ; CLI, Hub et Aube production |
| `shared-011/supply-result.json` | `43ad2189babad4fdecb3eae65fac5ac79269d1dd61bd649307a05e10ae328793` | PASS ; 0 vulnérabilité, 10 avertissements, 1 bloqueur de distribution |
| `shared-011/private-replays-current.json` | `cebcb687da0352d0385a4c5c2681d8c001d08ac201229568839a162974e8dff1` | PASS ; 34/34, 24 groupes CPU |

Les SHA des quatre logs natifs, des trois logs Clippy indépendants, des deux
logs supply-chain et des 24 logs privés ont également été vérifiés après réouverture.
Les 55 tests ignorés ne sont pas revendiqués comme tous exécutés :
les 34 replays explicites ferment seulement les tests privés sélectionnés.
Les reçus conservent `runtime_verified=false` ou `new_gpu_execution=false`,
et `gate_qualified=false`.

Le reçu historique `shared-010/wasm-result.json`, SHA
`dea2faa9d626f2f32e5df7bb493e70a1ed1a9308385540652b5c3bfc00f79bd5`,
est FAILED avec `source_unchanged=false` :
`crates/forge_engine/examples/verify_native_source_scene_manifest.rs` a changé.
Engine WebGPU et bundle Studio ont chacun terminé avec exit 0 ; ces sorties
ne qualifient pas le lot final. `shared-011/wasm-result.json` est encore
IN_PROGRESS à la relecture : Engine WebGPU exit 0, Trunk release RUNNING.
Aucun PASS WASM, lancement du worker, rendu GPU ou release n'est anticipé
dans ce point historique de 11:59 UTC.

### Actualisation du 11 octobre 2026 à 12:13 UTC

`shared-011/wasm-result.json` a terminé PASS, avec
`source_unchanged=true` et le même manifeste final `c6735bf9…`.
Reçu réouvert : SHA-256
`b490152e071efc405a94a05f03c4e69d401ee071235f30b2d96b68a809a71bc3`.
Engine WebGPU et bundle Studio ont chacun terminé avec exit 0.
Les deux logs réouverts correspondent aux pins du reçu :

| Log sous `shared-011` | SHA-256 |
| --- | --- |
| `webgpu-engine.log` | `1240c1715203a13535e43f6440d1b5f84dc1a28e2e3474347397b64f21238383` |
| `studio-webgpu-bundle.log` | `5bb685f9bf99a1625ebcb6f4df6db2084e86c4db0774f2114823d1e6fc45b13d` |

Les SHA et tailles des 3023 fichiers de
`apps/forge_studio/dist` ont été recomputés : 431075094 octets,
tous conformes au reçu. Le staging local contient 3011 assets
(3000 imports et 11 fichiers de kit), avec destination vérifiée et sources
inchangées. Son manifeste frontend
`forge-studio-local-assets-manifest.json` porte le SHA
`cc4cea22d2ca54a40685c34c37e5543b9d5fbd710ea4febd458d8c8be8826ae9` ;
il ferme le manifeste source acquis
`3d7f2dd80580b65dee3da1b88b3d826d0d98aac940621f85be794787dcb18ac6`.
Le manifeste `forge-navigation-worker-manifest.json` porte le SHA
`f96400c3e19bda1a624e60f9ebbf13cbef4ea621bcde9184fc88bbfc1dbddd08`.
Les deux manifestes restent `build_only=true` et `runtime_verified=false` :
la compilation et le staging ne prouvent ni l'exécution du worker ni un rendu GPU.

Le lot release `shared-011` est en cours. `release-progress.json` expose
`runtime-release / RUNNING`, commencé à 12:08:32 UTC, pour
`cargo build --locked --offline --release -p forge_runtime`.
Aucun `release-result.json` final n'est disponible lors de cette relecture ;
le Studio natif apparié, les exécutables finaux, l'installation et les parcours
publics restent à authentifier après achèvement du lot.

### Reprise coordonnée de l'outil de capture à 12:17 UTC

L'autre conversation a terminé, sur autorisation de coordination, la retouche
de `scripts/hivemind/candidates/SourceLibraryThumbnailBatchV1.cs` dans le même
checkout. SHA final :
`1b53d15564b57b31cb8033ddf49a560243526ddcf3cad761058126c5380d1caa`.
Seuls les paramètres bornés de présentation (exposition, lumière, distance et
projection) changent ; les sources, matériaux, géométrie et refus restent intacts.
Le gel de son code est refermé ; ses acquisitions de données continuent sur F:.

Ce helper figure dans l'inventaire complet. Les PASS `011` ci-dessus restent donc
des résultats datés antérieurs à cette retouche, et ne qualifient pas automatiquement
le nouvel état commun. Le build release engagé conserve son contrôle de stabilité ;
son verdict ne sera pas anticipé. Une validation fraîche du lot commun consolidé
est requise, sans exclure ce fichier ni masquer un refus éventuel du lot `011`.

Des corrections de portabilité/configuration sont intervenues après le début de
Clippy `shared-003`. Son manifeste initial ne représente donc pas l'état final :
un nouveau reçu cohérent est requis. Le passage diagnostic global suivant a aussi
révélé les erreurs répétées des sondes graphiques locales. Les corrections couvrent
quarante sondes GLB, huit sondes sRGB/matériau, les imports du diagnostic de ressources,
les modules Studio inclus et les quatre fichiers concernés du jeu natif.
Les shaders, assertions et seuils de diagnostic sont conservés. Les conversions
entières sont vérifiées ; les exceptions Clippy sont locales et justifiées pour
la conversion vers les paquets GPU f32 et les protocoles diagnostiques longs.
Clippy strict et les 2066 tests non ignorés ont réussi dans `shared-007` après ces
corrections. La préparation du frontend Aube est ensuite venue ajouter 76 fichiers
consommés par sa compilation, de sorte que le replay a correctement refusé le
manifeste précédent avant toute exécution. L'ordre final place désormais la
préparation du frontend Aube et de ses quatre schémas Tauri générés avant le gel
et la validation commune. Les trois Clippy indépendants ont terminé avec exit 0 ;
le passage historique `shared-independent-clippy-004` ferme leur stabilité sur les
2530 entrées de ce lot. Le diagnostic GPU autonome ajouté par une autre conversation
est inclus dans cet inventaire commun. Ses modifications pendant les tentatives
indépendantes `002` et workspace `shared-008` ont fait refuser leurs reçus ;
aucun résultat partiel n'a été adopté. La tentative indépendante `003` a échoué
avant Cargo sur l'accès sandbox à l'index historique via jonction ; `004` a
ensuite effectué les trois contrôles avec accès à cet index.
Le nouveau contrat des entrées de
build conserve tous les formats Hivemind sauf les scripts Python d'orchestration
non consommés. Les deux inventaires complets et leurs changements restent dans
les reçus ; une évolution de code, shaders, données ou helpers bloque le PASS.

Les scripts publics ont également été raccordés au workspace accepté actuel :
21 placements, identités et transformations conservées exactement. Les contrôles
purs passent pour les baselines 21 et 23 ; aucun PASS runtime n'en est déduit.
La fixture de données `public-current21-source/workspace` conserve les 28 fichiers
du workspace commun, son journal et ses objets historiques. Seul son ancien cache
d'archive de zone reçoit les octets de l'objet courant accepté `8d5a9b03…`.
Le workspace utilisateur et les données communes n'ont pas été modifiés. Aucun
contenu gameplay ou fichier d'application n'est injecté dans cette fixture.

Le lot `shared-009` reposait sur 2530 entrées de build, dont les 76 fichiers du
dist Aube, quatre schémas Tauri générés et trois fichiers du diagnostic GPU
autonome, identiques dans les
validateurs Gate 3 et livraison. Les parcours publics ferment le reçu release/WASM
via `FORGE_GATE3_VALIDATED_EVIDENCE` et utilisent le dist primaire avec le même
`release/forge-studio.exe`. Aucun frontend adjacent de candidat n'est nécessaire.
Les contrôles purs d'adoption, de provenance et de refus passent ; les observations
publiques restent à exécuter sur les nouvelles sorties effectives.

Pendant le build WASM du lot `009`, l'exemple commun
`crates/forge_engine/examples/verify_native_source_scene_manifest.rs` a été ajouté.
La compilation, le worker et les 3011 fichiers d'assets ont terminé correctement,
mais le contrôle de stabilité a refusé le reçu. Ce refus est conservé. Le nouvel
exemple est repris dans le même checkout ; la validation suivante couvrira aussi
cette entrée, sans exclure son code ni adopter le lot devenu incomplet.

L'exemple a ensuite reçu sept corrections Clippy bornées, dont trois conversions
entières vérifiées ; les assertions et ses deux contrôles négatifs restent intacts.
Le lot `shared-010` ferme les contrôles natifs sur 2531 entrées et l'empreinte
`8e1df1910bbdf206f827cd1b71eb82eb3f54b8b062a80bbd3096a5648e074440`.
Ses 2066 tests ont réussi ; les 55 tests ignorés ne sont pas revendiqués comme exécutés.

La coordination avec l'autre conversation a ensuite fermé ses derniers ajouts déjà
engagés : budget `LargeDesktop` explicite, contrôle de taille avant allocation et
troisième contrôle négatif sur les modes de normales absents. Le budget `Standard`
reste le défaut ; aucune limite du moteur n'est changée. Les sources communes sont
désormais stabilisées entre les deux conversations. Les trois contrôles négatifs
ont ensuite été extraits dans une fonction sans modifier leurs assertions ;
`main` reste borné, sans nouvel `allow`. Le SHA final de l'exemple est
`c1e7bf15dab360f8c648939645925a5a32d6397b25322291f239720c7b145091`.
Ces ajouts postérieurs au lot `010` sont inclus dans le manifeste final `011` :
natif, Clippy strict, supply-chain et replays privés sont PASS. WASM était encore
en cours au point de 11:59 UTC ; son résultat final est consigné ci-dessus.

## CI et livraison observées

[CI `38123749165`](https://github.com/Karenax/OpenRPG_Forge/actions/runs/38123749165),
sur `48a894d5`, terminée : WASM et supply-chain PASS ; Windows et Linux échouent à
Clippy. Le test du shader sous-marin échouait dans la CI précédente `38054809095`,
sur `5d0dc867` ; il reste à rejouer après la correction. Les corrections nécessitent une CI sur leur
révision finale publiée. La demande de finalisation n'autorise pas de push implicite.

Installation active réouverte : `F:\instal test\Installation`, révision 7, core
`4c2dcebfad356f7586e491fc0e34259a94b5116205b7e05fc76be865647da34e`.
Le SHA de `installation.json` est
`29a622b38cc2d0dacfa2588db5dad5ccab4d0e8bc34cf8dea091e5daa5e382e8`.
Le Studio installé reste identique : 216468992 octets et SHA recomputé
`6421725dc4dbcd8d7fa79539ea0f2e34bbca2066e652d14d13cac521f0563621`.
Son reçu `provenance/studio-build-proof.json`, SHA
`df012b7145d4673e0f8e0fca25e3b325dd495dbca9c8416cebeeead806acd364`,
reste historique `BUILD_ONLY`, avec `runtime_verified=false`.
`vault-repair-provenance.v1.json`, SHA
`6096cf11aa996b866cd2fa3cbc24e9851adb4cff90bcfda72936a5ae93933af1`,
déclare `studio_and_game_rebuilt=false` et `full_source_compile_parity=false`.
Cette réparation de livraison ne qualifie pas le Studio des sources communes.

La comparaison réexécutée entre `provenance/studio-source-inputs.json`
(SHA `22b6f73b61f0aceef96ae6864a9144374627d1dc078cbd0f81b38699b899e242`),
filtré par `is_build_input` du validateur commun
(SHA `b36954c99223968a4d7e6e7783788754bd1c07d14ab2eb76413bd3626f224c7e`),
et `shared-011/native-source-manifest.json` retrouve 1835 chemins communs :
234 SHA différents, 696 chemins seulement actuels et 4 seulement historiques.
Le manifeste historique filtré a 1839 entrées ; le manifeste actuel en a 2531.
Ces portées différentes ne permettent pas de revendiquer une parité globale.
Ce calcul est une observation en lecture seule, sans reçu de comparaison autonome ;
ses deux manifestes et son classifier sont les entrées réouvrables.
Une livraison commune cohérente reste à reconstruire et à qualifier.

Le workspace utilisateur actuel est authentifié, mais sans acteurs, objets gameplay,
places, dialogues, quêtes ni liens. Les fixtures RPG historiques restent des données
de régression publique ; leurs anciens PASS ne qualifient pas le Studio actuel.

Le jeu natif livré utilise `tidewater_plant_probe` : il consomme le workspace géré
pour le terrain, la végétation et les placements reconnus, avec locomotion et
collisions. Il n'installe pas la boucle `GameplayRuntime` du playtest Studio.
Les deux relancements de `verify_game_sessions.py` prouvent seulement leur parcours
natif déclaré, sans PNJ/quête/combat/SaveResume RPG. Cette distinction est explicite
dans [ADR-0021](../../ADR/ADR-0021-detached-delivery-installation.md).
Le critère Gate 3 reste le parcours RPG authoré et joué depuis Studio ; la livraison
de l'aventure sans repo/Vault est le futur critère R4-P01, pas un nouveau critère R3.


## Mesure courante du RPG, 12 octobre 2026

La mesure `current-profile-native-observation-019-003` est acquise sur le candidat
commun `9c46…69e5` / Studio `27f16…f551`. Le natif et ses huit WebViews identifiés
ferment normalement avec exit0 ; les 66 documents d'auteur et les sources restent
inchangés. Le résultat est `OBSERVED_UNQUALIFIED`, sans acceptation humaine.

| Observation publique après préchauffage | Résultat |
| --- | --- |
| Intervalles de boucle | 367 sur `34.9138 s` |
| Durée moyenne / cadence de boucle | `95.13297 ms` / `10.5116 Hz` |
| p95 / p99 / maximum | `114.5 ms` / `129.8 ms` / `140.4 ms` |
| Intervalles supérieurs à 33,3 ms | 367 sur 367 |
| Canvas réellement observé | `994 × 617`, DPR 1 |
| Matériel OS | Ryzen 5 7600, RX 7800 XT et Radeon intégrée recensées |

Ce sont des durées de boucle Bevy, pas des FPS présentés. L'adaptateur GPU
effectivement sélectionné, la swapchain, les durées GPU et la préparation du rendu
CPU ne sont pas attestés. Le profil observé reste identique entre début et fin
(`5d48b1e69b51bece66bda6064ce93f89f7c8498f867d8fb34afe47dbe1109ae4`) :
19 498 entités de végétation rendues, descripteurs d'ombres CPU `2048² × 3`, MSAA 1,
FXAA actif et TAA inactif. Ces observations ne constituent pas un profil High complet.

La cadence est faible. Le préchauffage était déjà proche de `93 ms` par boucle ;
les trois tiers de la capture restent voisins (`96.879`, `93.903`, `94.621 ms`).
Le coût propre de l'instrumentation et du HUD n'est pas isolé. Les scopes de garde
et de rafraîchissement de navigation mesurent respectivement `1.521` et `1.469 ms`
par tick fixe en moyenne ; les deux parcourent les géométries même à l'arrêt.
Ils représentent environ `17.1 ms` écoulées par boucle, sans constituer du CPU
exclusif ni expliquer seuls les `95 ms`. Le scope physique vaut `0.429 ms` par tick.
Les scopes imbriqués ne doivent pas être additionnés. Aucun nouveau job worker
n'est dispatché pendant la capture : la mesure de jobs courants est indisponible,
et non égale à zéro. Les 21 jobs de démarrage restent un historique distinct.

Une analyse de code identifie une piste limitée à `navigation/geometry.rs`,
`runtime.rs` et `cache.rs` : query limitée aux Collider, inventaire trié réutilisé
et cache privé des reçus statiques. Toute réutilisation doit comparer les entrées
effectives ECS et Rapier, y compris la hiérarchie, les formes, poses, états et
handles ; toute différence reprend le calcul courant. Les gardes avant mouvement
et après physique, les états pending, les refus worker périmés et le feedback réel
à chaque tick doivent rester distincts et inchangés. Aucun correctif n'est encore
appliqué ; les `17.1 ms` des deux scopes ne sont pas un gain promis ni une explication
complète de la lenteur. La version validée demeure `9c46…69e5` / Studio `27f16…f551`.

Le p95 de boucle dépasse largement le repère O2 de `16.7 ms`, avec des conditions
différentes de son profil de référence. La dérogation RX 7800 XT de Gate 1 conserve
sa portée ; aucune qualification de fluidité du RPG courant n'est déduite ici.
Les cibles indicatives CPU/GPU ne sont pas transformées en mesures disponibles.

Les artefacts se trouvent dans `.gate3-evidence/20261011` :

| Artefact | SHA-256 |
| --- | --- |
| `current-profile-native-observation-019-003/result.json` | `eac5a442ffb892a76d77e83c25ab1f26a533e1083bfbbfae44f0b282ce08fe78` |
| `current-profile-native-observation-019-003/native-monitor.json` | `2aff8eeea240c3cf9b48c197828217161c543639b72067333121d7b0c27f5ad1` |
| `current-profile-native-observation-019-003/final-published-timing.json` | `8c89220ef2590aec533673c303420974ec4220dfe2e0253ee820c69fe2245713` |
| `current-profile-native-observation-019-003-analysis-001/metrics.json` | `821a7f0070db0e9de67b6a19157e7c25024d4c1bd551da5b2f2234069ba23bff` |
| `current-profile-native-observation-019-003-analysis-001/ANALYSIS.md` | `575aa2b9d736c6dbe688361622e8f9158e9768f485e1fc82ebc9769cca63b4ca` |
| `current-profile-native-observation-019-003-analysis-001/receipt.json` | `ca4a8a5d1f1a018ec5da748d0fd2c117785a0dc31d698fe155ad7af43bc7f4b8` |

## Préparation et limites de performance du RPG authoré

Le 11 octobre à 12:24 UTC, le raccord d'observation des durées dans le playtest
importé est lancé dans le checkout commun. Il entoure les opérations existantes
de gameplay et navigation, distingue le calcul des jobs de leur publication,
et expose une collecte bornée avec identités et relations d'inclusion. Les durées
écoulées, les zéros et les valeurs indisponibles doivent rester distincts ; aucun
temps CPU exclusif ni preset High complet ne sera déduit de cette instrumentation.
Le raccord est gelé à 12:54 UTC dans les sources communes : 21 fichiers du
timing/profil sont épinglés par `playtest-timing-pure-001/timing-source-freeze-002.json`,
SHA `f0ca248c143bacc870d37f40a527e90fb16bf646d1c7df6f5dfd22d5627a2ec9`.
Le HUD authoré offre Start/Stop/export JSON. Publication au plus une fois par
seconde hors transitions, échantillons bornés, pins de capture fermée conservés
et nouveau contexte adopté au Start suivant. L'inventaire ponctuel début/fin
distingue composants CPU, uniforms, descriptors et readbacks asynchrones ;
pose et compteurs sont exclus de son empreinte. Les limites de résolution
d'horloge, d'invariance entre les deux observations, High et GPU restent explicites.
Le format/parsing et dix contrôles JS synthétiques sont passés ; les seize tests
Rust réussissent dans la suite native `012`. Après les corrections des préchecks,
le gel des 21 fichiers est actualisé dans `timing-source-freeze-004.json`, SHA
`990dd36e85a639cb4b06ce68b05be86e337d56b61345628e6c3b27a55c78fea4`.
Le précheck `005` ferme leur compilation Web/natif et le Clippy strict ; la suite
native complète `012` termine PASS. Aucune observation runtime n'est acquise.

La route réelle est `authoring_viewport.rs` : une carte avec `sampled_source`
appelle `authoring_imported_terrain::launch` et utilise l'App Bevy importée
de `apps/forge_studio/src/authoring_imported_terrain.rs`.
Cette App applique `PlaytestGraphics`, mais n'installe actuellement ni
`quality_preview::install` ni `RuntimeEvidencePlugin`.
Le helper PerformanceEvidence et le profil WorldSketch High ne mesurent donc
pas le profil effectif de ce RPG. Même les statistiques globales existantes
conservent `full_preset_applied=false`.

Un raccord High honnête doit lier la politique à la carte/révision authorée,
réutiliser `VisualValleyQualityProfileDocument` et `EffectiveQualityPlan`
de `crates/forge_project/src/{quality,effective_quality}.rs`,
déclarer les capacités et pins de sources réellement disponibles, puis appliquer
et observer les sept domaines de l'App importée : terrain, végétation, ombres,
environnement, eau, pluie et post-traitement. L'identité
`QualityApplicationIdentity` de `crates/forge_world/src/quality_application.rs`
doit correspondre aux paramètres effectivement projetés. Aucun label High,
substitution ou réglage public actuel ne prouve seul cette application complète.

Une capture de présentations ciblée peut mesurer les FPS du profil courant
sans modifier le produit si elle ferme l'identité du processus WebView, la
swapchain et le canvas RPG. Elle ne ferme pas High, la durée GPU ni les temps
simulation/navigation. Aucun benchmark, capture PresentMon ou temps de navigation
exclusif n'est acquis ici. Le chronométrage intégré entoure les opérations
réelles ; dispatch-vers-publication d'un job inclut
attente et transport, et `Instant::elapsed` mesure une durée écoulée plutôt
qu'un temps CPU exclusif. Toute correction produit nécessaire sera regroupée
après les premières preuves fonctionnelles puis revalidée sur les sources communes.

## Preuves restant à fermer Gate 3

Les critères restent ceux du [contexte canonique](../../../PROJECT_CONTEXT.md),
de la [roadmap R3](../../planning/MIGRATION_ROADMAP.md) et de
[G3-09/10 et du protocole](../../planning/GATE3_PREPARATION_PLAN.md).
Les anciens PASS Source075, les tests purs et l'acceptation du correctif 005
ne remplacent pas ces observations sur la version commune actuelle.

1. Conserver l'appariement des sources, du dist et du Studio `019` authentifiés
   par les fermetures native, WASM et release ci-dessus. Toute correction produit
   ultérieure demande une nouvelle validation commune ; les reçus `011`, `014`
   et `015` gardent leurs portées historiques. Le raccord `008` ferme le périmètre
   courant de navigation WASM/WebView2 et l'attribution des tuiles au worker.
   Il ne qualifie pas Edge autonome, le rendu ou une parité globale de plateformes.
2. Conserver les PASS d'authoring et du parcours public R3-A/P01 de `007` sur
   ces sorties exactes, avec leurs limites :
   joueur/inventaire, PNJ/navigation, interaction/dialogue/quête, combat direct
   et approche environnementale, village/forêt/intérieur, loot, conséquence
   persistante, Save et Resume dans un nouveau processus. Fermer les archives,
   objets gérés et 21 placements par égalité complète avant/après ; aucun gameplay
   injecté dans les documents d'auteur.
3. Conserver P02 `005` et sa lecture indépendante (réplique et porte retouchées,
   reprise compatible sans perte), P03 `007` et P04 `008`
   (diagnostic guidant la correction, état de test choisi,
   révision de reproduction et source authorée intacte), sur le même candidat.
   Ne pas reprendre ces verdicts après une modification pertinente sans revalidation.
4. Fermer R3-B par observations causales : poussée, lancer, impact, chute/piège,
   destructible, feu, eau/extinction et réaction IA au danger. Observer aussi
   navigation porte fermée NoPath puis Open/Reached, mouvement réel/KCC et révisions.
   Les preuves GPU/worker doivent porter sur les sources et le contexte courants ;
   encodage de commandes et flags de readiness ne valent pas présentation GPU.
   Conserver captures, limites, sources et fermeture des processus d'essai.
5. Mesurer le RPG courant après préchauffage sur matériel, résolution et profil
   réellement observés ; relever fluidité, attentes, simulation et navigation,
   puis comparer aux budgets du contexte canonique. Conserver les objectifs O2
   (1080p qualité élevée, moyenne >= 60 FPS, p95 <= 16,7 ms et 1 % low >= 45 FPS),
   leurs écarts et les décisions applicables dans leurs périmètres. La
   [dérogation RX 7800 XT](../gate1/R1_PERFORMANCE_POINT2.md) clôt le point
   performance Gate 1 ; elle ne qualifie pas les mesures du RPG courant.
   Déclarer les mesures indisponibles et les limites. Ne revendiquer ni High
   effectif ni qualification O2 sans les preuves correspondantes ; le
   raccordement d'un High complet WorldSketch n'est pas ajouté comme critère R3.
   Les cibles simulation CPU <= 4 ms, préparation rendu CPU <= 4 ms et GPU
   <= 14 ms restent indicatives, avec dépassements visibles ; aucun seuil
   chiffré spécifique de navigation n'est ajouté.
   La capture `019-003` ferme les mesures de boucle et des scopes publiés du profil
   courant, avec un signal de lenteur explicite. FPS présentés, préparation CPU,
   durée GPU et coût propre de la collecte restent indisponibles. La fluidité
   courante n'est pas qualifiée.
6. Consigner P05 sur une partie complète du candidat actuel : compréhension,
   lisibilité, rythme, erreurs et reprises, puis obtenir le verdict de qualification
   explicite. L'acceptation humaine du correctif 005 conserve sa portée historique.
7. Publier seulement sur autorisation, puis obtenir les jobs CI Windows/Linux/WASM/
   supply-chain PASS sur la révision finale. ADR-0006 ne couvre plus les jobs
   effectivement démarrés ; les contrôles locaux ne prouvent pas Linux ni la CI.
   L'adoption, l'installation, l'upgrade et le Setup gardent leurs qualifications
   distinctes, requises avant leur promotion. L'installation autonome de
   l'aventure et `.orpmod` v2 relèvent de Gate 4/R4-P01 ; la qualification de
   distribution complète relève de Gate 7. La réserve GLib `RUSTSEC-2024-0429`
   reste un blocage de distribution documenté, sans devenir un critère R3
   supplémentaire. Aucun critère Gate 4 n'est présenté comme une preuve R3 acquise.

**Gate 3 reste ouverte.** Les acceptations humaines et preuves locales antérieures
gardent leurs portées distinctes. Aucun verdict GPU, humain, distant ou de clôture
n'est déduit de cette consolidation.
