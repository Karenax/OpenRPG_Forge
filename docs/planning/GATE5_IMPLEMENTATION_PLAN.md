# Gate 5 — Creator RPG étendu

Clôturée localement le 22 octobre 2026 selon le
[rapport final](../evidence/gate5/GATE5_CLOSURE_20261022.md) et ADR-0034.
Ouverte le 19 octobre 2026 sur demande propriétaire : « lance la gate 5 avec
pour objectif d'aller jusqu'à sa clôture ». Gate 4 reste clôturée localement.
Objectif : réaliser et qualifier les parcours de la roadmap, sans ouverture
de Gate 6 ni publication Git implicite. Le checkout commun reste l'autorité.

**Périmètre précisé par le propriétaire le 21 octobre 2026 : construire et
valider les fonctionnalités de base permettant de créer des quêtes par la
suite.** La Gate 5 ne constitue pas une campagne de création de quêtes ou
d'aventure. Les données narratives existantes restent des jeux de test isolés ;
leur contenu n'est pas à enrichir. Chaque parcours utilise uniquement les
données minimales nécessaires pour vérifier les mécanismes génériques :
édition, validation, exécution, refus atomiques, sauvegarde/reprise et
réutilisation entre modules. Les critères fonctionnels ont été démontrés,
sans logique moteur propre à un scénario.

**Stockage — instruction propriétaire actualisée le 20 octobre : E: porte le
développement, les caches, outils, téléchargements et temporaires ; F: conserve
les preuves, l'historique et la bibliothèque d'assets convertis.** Le checkout
reste sur E:. Le cache Cargo partagé est `E:/OpenRPG_Forge/.build/cargo-target`,
le registre sous `.build/cargo-home` et les temporaires sous `.build/tmp` sur E:.
Cette répartition remplace celle du 19 octobre. Le rapatriement des fichiers
temporairement placés sur C: est suivi sous
`F:/OpenRPG_Forge/.gate5-recovery-20261019-001`, avec inventaires et empreintes.
Les prochaines compilations doivent aussi définir `TEMP` et `TMP` sous
`E:/OpenRPG_Forge/.build/tmp` pour éviter les temporaires du compilateur sur C:.

## Lots et critères de sortie

Au 21 octobre, le [rapport de qualification locale](../evidence/gate5/GATE5_LOCAL_QUALIFICATION_20261021.md)
rapproche les treize domaines et R5-P01/P02. Les lots A–F passent localement.
La décision propriétaire du 22 octobre retire les contrôles GitHub des critères
des gates ; ADR-0034 et le rapport final consignent la clôture locale.

| Lot | Portée | Preuve nécessaire | État |
| --- | --- | --- | --- |
| A | Profil versionné, capacités réellement disponibles et surfaces par module | Même définition/unité/valeur dans validation, calcul et inspecteur/aide ; refus d'une capacité absente ; historique et réouverture | PASS local |
| B | Blueprints, créatures, objets/conteneurs | Héritage et overrides persistants ; transferts atomiques et refus sans perte ; deux acteurs configurés | PASS local |
| C | Dialogues avancés, quêtes/journal et factions | Branches conditionnelles, deux solutions, conséquences locales, récompense unique après reprise | PASS local ; données de test préexistantes |
| D | Capacités/effets, calendrier et rencontres | Coûts atomiques, expiration unique, temps injecté, récurrence et reprise sans doublon | PASS local |
| E | Cinématiques et scripting sandboxé | Interruption sans blocage ; quotas et permissions refusés sans mutation partielle | PASS local |
| F | Diagnostics et deux modules dans le même Studio/runtime | Erreur vers donnée corrigible, correction puis replay ; Save/Open et isolation des deux modules | PASS local |
| G | Qualification finale et rapport de clôture | Matrice des treize domaines et R5-P01/P02, source exacte, non-régressions, builds et parcours réels | PASS local ; clôture documentée le 22 octobre, ADR-0034 |

Les cas P07/P08 de la [préparation](preparation-parallele-20260926/GATE5_REGLES_ET_CREATOR.md)
servent d'oracles détaillés. Le ruleset de référence et le manuel ne sont pas
annoncés intégralement implémentés par adoption d'un profil partiel. Les domaines
ne sont pas supprimés pour réduire artificiellement la gate.

## Qualification

Conserver un manifeste des entrées suivies, modifiées, supprimées et non suivies
avant modification et pour chaque candidat qualifié. Tester le checkout commun,
sans copie de code par conversation. Tests ciblés puis workspace/Clippy/fmt,
builds natif et WASM pertinents, lancements et captures du Studio/runtime.
Le succès d'un test de domaine ne prouve pas un parcours UI ou GPU.

Les réserves de distribution de Gate 4 restent séparées. ADR-0034 consigne la
décision du 22 octobre : arrêt des contrôles GitHub, sauvegarde distante et
validation locale des gates, sans transformer les résultats distants historiques.
Toute revue humaine explicitement exigée demeure distincte des preuves machine.

## Reprise historique

Sources : roadmap Gate 5, préparation P07/P08 du 26 septembre et
[matrice de reprise](../migration/LEGACY_REUSE_MATRIX.md), référence
`archive/main@a7f4a3f5dd0eeff1dc060ea1237b1d7d650b7ca0`.
Pas de copie C#/Godot, ni de reconstruction des 717 routes ou des anciennes fenêtres.

## État initial

Branche `Rust`, HEAD `e310d9b324b80b9d5df6e2be213c815c99459444`.
Le checkout possède des changements préexistants ; ce HEAD seul ne représente
pas les sources courantes. `forge_gameplay` fournit déjà le profil minimal,
dialogues conditionnels, quête de récupération, inventaire et interactions
physiques. Le Studio dispose de commandes de preview/apply/history pour ces
définitions. Aucun lot Gate 5 n'est qualifié par cet inventaire.

## Avancement du 19 octobre — fondations du lot A

Implémentés : descripteurs communs des sept règles numériques, validation et
édition atomique d'un candidat, registre des capacités, configuration des
surfaces conservant le contenu masqué, inspecteur et aide partagés dans Studio.
[ADR-0027](../ADR/ADR-0027-creator-capability-contract.md) documente le contrat
v1/v2 et la compatibilité fonctionnelle des sauvegardes.

Preuves dans `E:/OpenRPG_Forge/.gate5-evidence/20261019/` :

- `opening/source-inputs.json` : 6 065 entrées inventoriées, dont les suppressions
  et les données historiques inaccessibles signalées comme telles, jamais
  rebaptisées absentes. Les sources compilables doivent rester lisibles.
- `gameplay-tests-001.log` : 38 tests réussis, dont cinq nouveaux contrats Creator.
- `project-tests-001.log` : trois tests réussis, dont configuration/règle,
  preview sans écriture, Undo/Redo, réouverture et refus sans mutation.
- `gameplay-clippy-001.log` : Clippy strict du domaine réussi.
- `studio-wasm-check-001.log`, `studio-wasm-clippy-002.log` : vérification et
  Clippy strict WASM réussis. Le premier Clippy est conservé avec ses deux
  erreurs corrigées ; aucun échec n'est renommé PASS.
- `lot-a/source-inputs.json` : 6 070 entrées de la version en validation.
- `workspace-tests-001.log` : tentative interrompue par disque plein, sans
  verdict de test workspace. Les sessions incrémentales générées par cette
  tentative ont été identifiées dans le log puis retirées, avec contrôle des
  chemins et dates. La compression des PDB a été refusée par Windows ; les
  symboles générés ont d'abord été placés sur C:, puis rapatriés sur F: à la
  demande propriétaire. Les reçus `generated-pdb-relocation*.json` retracent
  cette étape historique. Sources et applications installées conservées.
- `workspace-tests-002.log` : reprise avec deux compilations simultanées,
  arrêtée à la création des archives moteur/carte par manque d'espace, code 101.
  Aucun verdict de tests workspace n'est acquis. Les deux tentatives restent
  conservées. La reprise doit employer le cache partagé sur F:.
- `C_CLEANUP_VERIFIED.json` : aucun fichier ou dossier `gate5-*` ne subsiste dans
  le répertoire C: utilisé par ce chantier, contrôle du 19 octobre à 17:32 UTC.
  Les deux archives de symboles sur F: ont été vérifiées entrée par entrée
  avant suppression des sources C:. Le cache incrémental est sur F:. Les deux derniers doublons
  C: ont été comparés aux empreintes originales et aux copies F: avant retrait.
  Destination : `F:/OpenRPG_Forge/.gate5-recovery-20261019-001/`.
- `FINAL_STORAGE_RELOCATION.json` : audit terminé à 17:49 UTC, 2 065 fichiers
  du cache vérifiés par empreinte, deux archives de symboles (75 + 1 fichiers)
  vérifiées, aucun élément Gate 5 restant sur C:. L'ancien chemin incrémental
  moteur sur E: est un lien vers le cache préservé sur F:.

`creator-direct-replay-001.log` : les six tests Creator passent sur le binaire
fraîchement compilé du lot A, dont celui reliant les valeurs de l'inspecteur
aux dégâts, à la protection, au coût et à la compatibilité de sauvegarde.
Le binaire est identifié dans `creator-direct-binary-001.json`. Ce replay ciblé
ne remplace pas les tests workspace interrompus.
Les avertissements de cache incrémental (hard links refusés, copie de repli)
et celui de compatibilité future de `proc-macro-error2` sont conservés.

Restent à acquérir pour le lot A : lancement et inspection UI, configuration
cohérente entre zones d'un module, export/admission des configurations et
parcours des deux modules. **R5-P01 et R5-P02 restent ouverts.**

## Lot B — blueprints en cours de validation

Les définitions de personnages sont versionnées, avec liens et overrides
explicites par champ. Les révisions matérialisent les valeurs dans les acteurs
canoniques ; nom, position, patrouille et dialogue restent locaux. Une définition
utilisée ne peut pas être supprimée ; le détachement conserve les statistiques.
Les anciens documents sans bibliothèque restent lisibles sans ajouter de champ.

Le panneau Studio fournit création depuis un acteur, édition des valeurs
héritées, révision, liaison, rétablissement par champ et détachement via le
preview/apply/historique commun. Les statistiques retouchées dans l'inspecteur
deviennent des overrides seulement pour les champs modifiés. La suppression
d'un acteur lié renvoie vers le détachement préalable.

La capture `F:/OpenRPG_Forge/.gate5-evidence/20261019/lot-b-blueprints/source-inputs.json`
identifie les sources avant les premiers tests du domaine (6 073 entrées).
Un test supplémentaire du bus de zone a été ajouté ensuite ; la capture
`lot-b-blueprints-002/source-inputs.json` sur F: inclut ce test (6 073 entrées).
Le replay ciblé `blueprints-creator-direct-001.log` passe **13/13 tests** sur le
binaire compilé dans le cache F:, avec empreinte et manifeste dans le reçu
associé. Il couvre les sept nouveaux scénarios de blueprints : révision sans
écraser les overrides, réouverture et état réel de session, retour à l'héritage,
refus atomiques, capture des retouches, compatibilité fonctionnelle des données
éditoriales et détachement/suppression. La capture
`lot-b-blueprints-003/source-inputs.json` identifie 6 075 entrées avant ce build.
La validation `forge_gameplay`/`forge_project` du candidat blueprints est terminée :
**326 tests réussis**, code 0, journal `blueprints-domain-project-tests-002.log`
sur F:. Le scénario de révision/override/Undo/Redo/réouverture passe aussi dans
le bus de zone. Ces résultats précèdent les extensions conteneurs ci-dessous ;
ils ne qualifient pas automatiquement la version suivante.
Aucun parcours UI du lot B n'est acquis. Les conteneurs et la qualification de deux
créatures dans le runtime restent à réaliser.

Depuis la répartition du 20 octobre, `scripts/gate5/invoke-cargo.ps1` force la cible,
le registre Cargo, les téléchargements, `TEMP` et `TMP` sur E: ; les logs restent
dans `F:/OpenRPG_Forge/Preuves-et-analyses/actives/`. Les échecs d'extraction exFAT
décrits ci-dessous sont historiques et ne prescrivent plus l'usage de F: pour Cargo.
La tentative `blueprints-gameplay-tests-001.log` s'est arrêtée avant compilation :
le volume F: est exFAT et refuse un horodatage lors de l'extraction Cargo de
`cfg-if`. `prepare_registry.py` prépare les sources depuis les archives originales
épinglées par `Cargo.lock`, sans appliquer les horodatages tar : SHA-256 des
archives intactes, comparaison de chaque fichier extrait avec l'archive,
vérification du nom/version et marqueur Cargo écrit en dernier. Les extractions
interrompues sont conservées. Le cache C: préexistant sert seulement de source
en lecture seule ; les archives manquantes sont téléchargées directement sur F:.
Le marqueur suit le [contrat d'extraction Cargo](https://doc.rust-lang.org/stable/nightly-rustc/src/cargo/sources/registry/mod.rs.html).
Les deux tests du préparateur passent sur F: : archive à horodatage Unix zéro
et contenu inchangé, refus d'une somme incorrecte, d'un fichier source altéré
ou d'un chemin sortant du paquet. Ils ne qualifient pas les blueprints.

## Lot B — candidat conteneurs et catalogue

[ADR-0028](../ADR/ADR-0028-container-inventory-contract.md) fixe les unités,
transferts atomiques, filtres, clé non consommée et conservation à la reprise.
Les nouveaux états sont portés par les objets canoniques. Les sauvegardes avec
conteneur portent la version 2 ; sans conteneur, le format historique demeure.
Le registre dérivé des reçus vérifie les inventaires, verrouillages, ramassages
et consommations/récompenses de quête ; un événement d'items dans une frame
physique est refusé. Les observations physiques restent celles de l'adaptateur.

Le Studio propose catalogue d'items, limites de pile et préparation du contenu,
filtre, capacité et clé d'un conteneur. Les références et piles empêchent une
suppression/retouche invalide. Les actions de jeu, leurs quantités et leurs
libellés communs sont raccordés aux présentations native et Web. Le compteur
structurel de sources d'items tient compte des conteneurs et des clés ; ce
compteur n'est pas une preuve de navigation ou de solvabilité complète.

La capture `lot-b-containers-001/source-inputs.json` sur F: identifie 6 082
entrées avant la validation du domaine. Douze nouveaux tests couvrent les
transferts, refus, filtre, pile, capacité, clé, quota, reprise, falsification,
catalogue et sources structurelles. Un scénario du bus de zone vérifie
preview/Undo/Redo et les trois codecs. `containers-gameplay-tests-001.log` porte
**58 tests réussis**, dont les douze nouveaux contrats. Clippy a demandé deux
simplifications booléennes et l'extraction d'un contrôle de sauvegarde ; les
corrections sont appliquées. Le second passage ne signale plus qu'une longueur
de test de zone, corrigée en retirant des assertions redondantes avec l'égalité
du head complet. Le troisième passage demande un point-virgule dans un test,
également corrigé. Les échecs sont conservés dans `containers-clippy-00[123].log`.
`containers-clippy-005.log` passe strictement sur gameplay/projet et tous leurs
targets ; `containers-fmt-001.log` passe sur tout le workspace.

La capture `lot-b-containers-002/source-inputs.json` (6 082 entrées) inclut les
corrections et le nouveau test de stockage natif. Le replay
`containers-gameplay-tests-002.log` passe **58/58 tests**. Les deux suites de
`containers-project-tests-001.log` passent **10/10 tests** : contenu initial
preview/Undo/Redo/trois codecs, sauvegarde v2 de partie, fermeture de l'hôte,
réouverture et reprise exacte sans modifier les sources authorées. Ce sont des
preuves de domaine et de stockage ; les parcours GUI restent à qualifier.

La première compilation utilise le cache incrémental, avec avertissements de
copie de repli sur F: exFAT. Depuis le replay corrigé, `invoke-cargo.ps1`
désactive l'incrémental pour éviter les copies de sessions. Le cache partagé
existant est conservé ; cible, registre, téléchargements et temporaires restent
sur F:. Cette option de build est consignée dans chaque nouveau reçu.

La préparation `registry-prepare-studio-001.log` est conservée comme interrompue,
sans reçu final. Après vérification de l'absence du processus, le passage
`registry-prepare-studio-002.log` se termine avec code zéro ; son reçu JSON du
19 octobre à 20:58 UTC inventorie les 925 dépendances verrouillées du Studio.
Les sources déjà présentes sont revérifiées contre leurs archives originales,
les autres sont extraites sur F: sans restaurer les dates tar incompatibles avec
exFAT. Cette préparation ne constitue ni un build ni une qualification d'interface.

## Lot A — autorité commune du Creator en cours

[ADR-0029](../ADR/ADR-0029-module-creator-authority.md) définit un profil commun
de règles, capacités et surfaces par module. Les bibliothèques de blueprints
restent locales aux zones. Une seule commande workspace référence la définition
immuable et toutes les zones matérialisées : Preview n'écrit rien, Apply publie
les objets avant d'avancer le head, Save reste explicite et Undo/Redo porte sur
l'ensemble. Les lectures, sauvegardes et réouvertures vérifient aussi les profils
retenus dans l'historique. Une retouche isolée de règles incompatible est refusée.

Les captures `lot-a-module-creator-00[123]/source-inputs.json` sur F: inventorient
6 087 entrées par candidat. `module-creator-tests-001.log` porte **17/17 tests de
contrat réussis** ; ses trois tests natifs s'arrêtent sur une clé de fixture
invalide, corrigée. Le passage 002 détecte la lecture interdite du verrou Windows
par l'inventaire de test ; ce fichier de coordination est exclu de la comparaison
des données persistées, sans changer le contrat de stockage. Les deux échecs sont
conservés. `module-creator-tests-003.log` passe **3/3 scénarios natifs** à 21:06 UTC :
deux zones, non-écriture du preview, Save/Open, Undo/Redo, propositions périmées ou
falsifiées, refus d'une règle locale incohérente et corruption d'une définition
historique. Clippy 001 puis 002 refusent des fonctions trop longues ; les
opérations d'attachement, de mise à jour, de rejeu et d'application sont extraites
sans lever la règle. Le candidat `lot-a-module-creator-005/source-inputs.json`
porte le passage strict **réussi** de `module-creator-clippy-003.log` sur
gameplay/projet et tous leurs targets, à 21:10 UTC. Le replay complet de ces
deux composants est lancé sous `module-creator-domain-project-tests-001.log` ;
il se termine à 21:21 UTC avec **347 tests réussis**, aucun échec, sur le
candidat backend antérieur au nouveau protocole Studio. Les sources compilées
de gameplay/projet sont restées inchangées durant ce replay ; les retouches
concomitantes des contrôles Studio n'étaient pas dans cette cible de compilation.
`module-creator-fmt-001.log` passe sur tout le
workspace à 21:11 UTC. Le raccordement au protocole et aux contrôles Studio est
développé dans le candidat suivant ; ses parcours, l'initialisation des futures
zones et le transport des modules restent à qualifier. **R5-P01/P02 ne sont pas clôturés**.

## Lot A — protocole et contrôles Studio en cours

Le contexte natif expose le module propriétaire et le hash du workspace. Les
contrôles préparent maintenant une configuration commune (règles, surfaces et
capacités) et montrent les noms des cartes affectées. Le backend conserve les
exigences des données cachées de toutes les cartes, refuse un contexte périmé et
Apply utilise la proposition exacte avec l'identité d'audit de son preview.
Les contrôles des cartes isolées gardent leur commande locale. L'initialisation
des règles d'une nouvelle carte utilise la définition commune lorsqu'elle existe.

L'historique dédié au module utilise les snapshots workspace, rafraîchit la carte
active et refuse une opération touchant un autre module ou supprimant la carte
ouverte. Le playtest conserve sa garde d'édition. Les captures
`lot-a-module-host-00[1234]/source-inputs.json` sur F: inventorient 6 092 entrées.
Les diagnostics initiaux (forme, syntaxe d'un test, longueur du scénario) sont
conservés dans `module-host-clippy-00[123].log`. Le contrôle strict
`module-host-clippy-004.log` passe sur gameplay/projet et tous leurs targets.
Le replay ciblé `module-host-tests-001.log` passe à 21:30 UTC : **17/17 tests**,
dont six scénarios de configuration/historique commun, six de gestion des onglets
et cinq de sauvegarde native. Le test hôte prouve aussi la conservation du stamp
humain du preview lorsque Apply arrive avec un stamp plus récent. Le contrôle
WASM `module-host-studio-wasm-check-001.log` est interrompu volontairement à
21:47 UTC (code -1) après avancement des sources de transport : le contrôle natif
des nouvelles interfaces précède un prochain replay WASM stabilisé. Le reçu
`.interrupted.json` identifie exactement les processus arrêtés ; les dépendances
compilées et les journaux sont conservés. Ce passage n'a aucun verdict WASM.
Aucun parcours GUI n'est encore qualifié.

La capture `lot-a-module-host-005/source-inputs.json` ajoute un oracle natif
supplémentaire de refus sans écriture : Apply sans preview, contexte périmé après
retouche locale et configuration/historique pendant un playtest. Ce test n'entre
pas dans la cible WASM ; il reste à exécuter. Il est inclus dans le Clippy strict
réussi du transport ci-dessous. Les 17 tests ci-dessus portent sur le candidat 004.

## Lot A — transport commun en cours de qualification

Le manifeste de paquet v3 inclut la définition commune ; les paquets v2 sans
profil gardent leur format. L'admission vérifie chaque document contre le profil,
même si ses hashes ont été recalculés. L'export natif capture toutes les cartes
du module, ses références d'assets réunies et le profil. La réouverture par le
producteur de workspace réadopte toutes les cartes et vérifie leurs archives exactes.
Une matérialisation déjà équivalente n'est pas réécrite. Les droits et les preuves
du contenu original restent conservés par le chemin d'origine existant.

La révision du module reste indépendante des retouches des autres modules et de
l'ordre des cartes ; la capture initiale reste protégée par le CAS global. Les
sources raster/instances non transportées restent refusées explicitement avant
publication. Ces changements ne constituent pas une nouvelle prise en charge
de ces sources, ni une preuve d'import GUI ou de cohabitation des deux modules.

Les captures `lot-a-module-transport-00[1234]/source-inputs.json` inventorient
6 095 entrées. `module-transport-clippy-001.log` passe sur gameplay/projet et tous
leurs targets, avant la normalisation de l'ordre des cartes et les derniers tests
de producteur. Les nouveaux oracles couvrent versions v2/v3, règles/surfaces
incohérentes, archives déjà matérialisées conservées et isolation des révisions.
Ils restent à exécuter. Le test de bout en bout du producteur exporte deux cartes
avec origine conservée, puis les réouvre sans changer leurs archives ; il reste
également à exécuter.

`module-transport-delivery-check-001.log` s'arrête avant compilation : le verrou
propre de `forge_delivery` requiert `bitflags 2.13.2`, absent du cache F:. Le
préparateur vérifie et prépare **78 dépendances** sous
`registry-prepare-delivery-001.json`, verrou
`ef9b2f77680b98c1f1667a303dde29c9c8fb94115f83b6d6729de4d18cf42377`,
sans modifier ce verrou ni le verrou Studio. Le contrôle
`module-transport-delivery-check-002.log` passe à 21:59 UTC sur l'exporteur et
tous ses targets. C'est une vérification de compilation, distincte de ses tests
de paquets et des parcours du Studio. Le replay
`module-transport-project-tests-001.log` est lancé sur les scénarios natifs de
configuration, paquets, onglets et sauvegardes ; il passe à 22:05 UTC avec
**47/47 tests** (9 Creator, 27 paquets, 6 onglets, 5 sauvegardes). Cela inclut
le refus des changements pendant le playtest, l'isolation des révisions et la
conservation des archives déjà matérialisées. Le passage
`module-transport-delivery-tests-001.log` est lancé pour les paquets construits,
leur chargement et leur admission ; il passe à 22:14 UTC avec **29/29 tests**
(12 constructeur, 4 runtime, 13 admission). Le nouveau cas transporte trois
cartes, dont une sans RPG, et vérifie le profil commun et les archives exactes.
Ces tests restent distincts du producteur de workspace et du Studio.

La capture `lot-a-module-transport-005/source-inputs.json` inclut la sélection
d'assets par module : références explicites, catalogue d'origine encore présent
et fermeture de leurs lignages ; les assets sans rapport d'un autre module ne
sont pas sélectionnés. L'ancienne API d'export du catalogue complet reste
inchangée pour ses appelants. Les nouveaux tests de sélection couvrent aussi les
droits et le maintien des pins historiques ; ils ne sont pas encore exécutés. Le contrôle
`module-transport-studio-native-check-001.log` est lancé sur la bibliothèque
native du Studio et tous ses targets, sans desktop. Il termine à 22:32 UTC avec
code zéro. Les entrées ont avancé pendant la préparation des dépendances ; le
candidat stabilisé est donc rejoué sous Clippy strict avant les tests. Le binaire
desktop et le frontend WASM restent à compiler et qualifier.

## Lot A/F — deux modules dans un workspace, en cours

Le même producteur accepte une requête `--module-set REQUEST.json` : paquets
épinglés, choix de carte de départ par module, destination neuve unique. Il
valide toutes les admissions avant préparation, peuple une seule autorité de
workspace, conserve chaque origine et n'active la destination qu'une fois le lot
complet vérifié. Le profil du producteur est borné à 16 modules, 128 identités
d'assets réunies et 256 MiB de paquets ; les sources non prises en charge restent
refusées. Les identités répétées et les versions d'assets incompatibles sont
refusées. Un asset partagé déjà normalisé n'est réutilisé que si ses métadonnées
sont identiques, sans écraser celles du premier module.

Les captures `lot-a-module-set-00[12]/source-inputs.json` comportent 6 096 entrées.
Les nouveaux tests du producteur vérifient deux modules et leurs origines dans un
workspace, la déduplication des assets partagés et le refus sans activation d'un
conflit de métadonnées. Ils restent à exécuter. Le producteur est un outil de
qualification des données ; il ne constitue ni un nouvel éditeur ni un parcours
d'import GUI. L'ouverture alternée et la configuration des deux modules dans le
Studio restent à qualifier, ainsi que les autres domaines de Gate 5.
Les contrôles `module-set-studio-native-clippy-001.log` et `002.log` conservent
leurs refus (closure redondante, puis import local placé après des instructions).
Après correction, `module-set-studio-native-clippy-003.log` passe à 22:38 UTC,
avec `-D warnings`, tous les targets natifs de la bibliothèque sans desktop.
`lot-a-module-set-004/source-inputs.json` inventorie le candidat avant lancement
de `module-set-studio-tests-001.log` : résultat **24/24 tests réussis**, code zéro
à 23:02 UTC le 19 octobre (9 exports d'assets, 15 producteur/workspace).
Cela inclut deux modules dans un seul workspace, déduplication, conflits refusés,
export puis réouverture de toutes les zones avec profil commun et origine, et
sélection des assets par module avec pins et droits conservés. Les sources
narratives ont été raccordées après la compilation de ces binaires ; elles ne
sont donc pas couvertes par ce verdict. Le binaire desktop et le frontend WASM
restent non qualifiés.

## Lot C — transitions narratives raccordées, validation en cours

[ADR-0030](../ADR/ADR-0030-bounded-narrative-transactions.md) fixe les variables
typées, conditions composées bornées, flux à solutions alternatives, échec et
journal projeté. Préparés séparément pendant la compilation du candidat précédent,
les fichiers sont ensuite raccordés : bloc narratif facultatif exigeant Creator
v2, sauvegarde v3 uniquement en sa présence, coûts/récompense/solution dans la
transaction de dialogue, ledger de conservation étendu et vérification des
valeurs persistées à partir des effets retenus. Les textes restent retouchables
sans changer le hash fonctionnel ; les conditions et valeurs initiales le changent.

Le Studio possède les formulaires de variables typées, graphe nœuds/choix et flux
de quête/journal, via l'aperçu commun. La retouche d'un choix conserve ses
conditions et effets si l'auteur choisit de les garder. L'adoption d'un flux peut
convertir explicitement les anciens effets de restitution vers une solution
choisie, dans le même aperçu, sans changer identités, textes ni liens.
Les projections de journal native et Web utilisent la même API de domaine.

`lot-c-narrative-001/source-inputs.json` inventorie 6 105 entrées.
`narrative-domain-tests-001.log` passe à 23:05 UTC le 19 octobre : **71/71 tests**,
dont neuf nouveaux scénarios narratifs (deux solutions, récompense unique,
reprise, échec, refus atomiques, falsification isolée de variable, cycles,
adoption explicite et retouches de texte). Le premier Clippy strict conserve
cinq diagnostics de structure/style ; les corrections sont rejouées sous
`narrative-domain-project-clippy-002.log`, **réussi à 23:10 UTC** pour tous les
targets gameplay/projet avec `-D warnings`.
`narrative-project-tests-001.log` passe à 23:13 UTC : **20/20 tests**, dont le
nouvel oracle de preview/Apply/retry, Undo/Redo et réouverture des trois codecs
d'archive. `narrative-save-host-tests-001.log` passe à 23:15 UTC : **6/6 tests**,
incluant la sauvegarde v3 sur disque via l'hôte natif, sa réouverture avec la
variable modifiée en partie et la valeur initiale d'auteur conservée.

Les captures `lot-c-narrative-00[2345]` suivent les corrections et le raccordement
des formulaires. Les coûts et conditions des solutions sont conservés lors
d'une retouche de leur nom ; les choix permettent de garder les effets ou d'y
ajouter un effet typé. L'ordre des prérequis et les conditions d'échec avancées
existantes sont préservés lors d'une simple retouche de journal.
`narrative-studio-wasm-clippy-001.log` conserve un échec de longueur de fonction
dans `CreatorControls`. Après extraction du composant de règles, le replay
`narrative-studio-wasm-clippy-002.log` passe le 20 octobre à 00:20 UTC, avec
warnings traités comme erreurs. Ce verdict précède les retouches UX suivantes ;
elles exigent un nouveau passage. Les compilations des surfaces Studio et moteur, leurs
parcours et les factions à témoins locaux restent à qualifier ou réaliser.
Aucun domaine ni critère R5 n'est clôturé ici.

La préparation de lancement suit également la consigne F: : le packager accepte
une destination neuve sous `F:/OpenRPG_Forge/`, sa syntaxe PowerShell est vérifiée,
et les outils existants sont identifiés dans `frontend-toolchain-001.json`
(Trunk 0.21.14, wasm-bindgen 0.2.127 correspondant au verrou). Le build Trunk
doit être hors ligne et sa destination explicite sur F:, avec les mêmes caches
et temporaires. Les sources Tauri 2.11.5 révèlent que son gestionnaire crée le
dossier de données par défaut avant WebView2 ; le nouveau raccordement natif
`FORGE_WEBVIEW_DATA_ROOT` fixe le chemin avant création des fenêtres principales
et Atelier. Il reste à compiler, tester et exercer. Aucun lancement GUI n'a été
effectué pour cette préparation.

[ADR-0031](../ADR/ADR-0031-local-faction-witnesses.md) prépare le sous-lot factions
restant : groupes et relations initiales, variations propres aux PNJ témoins
d'une agression directe physiquement observée, seuils dans les dialogues et
persistance des causes. Le scénario oppose un témoin visible et un PNJ du même
groupe masqué par un obstacle. L'implémentation reste à réaliser ; la capacité
`Factions` n'est pas encore déclarée disponible. La perspective bonus des mondes
vivants conserve son statut non ouvert.

Les fichiers `factions.rs`, `faction_observation.rs` et `faction_ledger.rs`
préparent les définitions, snapshots de témoins, projection pure des variations
et reconstruction liée à un reçu de contact accepté. Ils étaient non déclarés
lors du passage narratif WASM 004. Le candidat suivant les raccorde au document,
aux attaques atomiques, conditions de relation et sauvegarde v4 ; dix tests de
contrat sont ajoutés. `factions-domain-tests-001.log` passe à 01:31 UTC le
20 octobre : **83/83**, dont les dix contrats factions, sur
`lot-c-factions-001/source-inputs.json` (6 113 entrées). La capacité runtime du
candidat est déclarée. Le candidat suivant raccorde le collecteur physique borné,
les formulaires et deux tests de persistance/historique supplémentaires ; il
partage la même borne géométrique de perception et reste à valider. Aucun
parcours GUI n'est qualifié.
Le contrôle `factions-domain-project-clippy-001.log` conserve un échec de style
dans un test (point-virgule manquant) à 01:37 UTC. Il est corrigé sans modifier
le comportement ; les nouveaux tests de host v4 et d'historique/archives sont
présents mais attendent encore leur exécution.
`factions-domain-project-clippy-002.log` passe à 01:38 UTC sur
`lot-c-factions-003/source-inputs.json`, tous targets gameplay/projet et
warnings traités comme erreurs. Les tests ciblés de host, historique et
configuration commune sont ensuite lancés avec les contrats factions ; leur
résultat doit encore être consigné.
`factions-project-contract-tests-001.log` passe à 01:47 UTC sur
`lot-c-factions-004/source-inputs.json` : **33/33** (10 contrats factions,
7 tests du host de sauvegarde, 9 de configuration commune et 7 d'édition RPG).
Les nouveaux cas couvrent Save/Open v4 sans mutation des bases authorées et
aperçu sans écriture, retry, Undo/Redo et trois codecs d'archive. Le scénario
headless Rapier ajouté oppose un témoin visible à un témoin derrière un obstacle,
puis retire celui-ci ; son exécution reste à acquérir. La compilation stricte
WASM du moteur et des formulaires est le contrôle suivant.
L'adaptateur physique commun à utiliser
est `forge_engine/src/gameplay/simulation.rs` (`execute_pending_actions`,
`action_observation` et le raycast `visible`) ; aucune branche par module n'est
nécessaire.

Le défaut de saturation des dégâts est reproduit dans
`overkill-reproduction-tests-001.log` : deux échecs, avec vitalité maximale 7 et
dégâts 20, puis vitalité 25 et protection 3 sur deux coups. `apply_attack` borne
désormais le reçu à la vitalité réellement perdue. Le replay complet
`overkill-domain-tests-001.log` passe **73/73 tests** le 20 octobre à 00:24 UTC,
dont ces deux régressions, coût unique, retry, Save/Resume et source inchangée.
Les inventaires d'entrées sont `lot-b-overkill-001` (reproduction) et
`lot-b-overkill-002` (correction), sous le dossier de preuves F: commun.

Les retouches suivantes sont présentes dans les sources des formulaires :
destinations de graphe libellées par texte, réinitialisation des sélections au
changement de zone, champs adaptés au type de variable, commandes de suppression
de choix/nœud et de changement du nœud initial avec validation des liens. Les
effets de synchronisation conservent les brouillons lors d'un aperçu sur une
source inchangée. Ces retouches attendent compilation WASM puis parcours GUI ;
les tests de domaine seuls ne les qualifient pas.

`narrative-studio-desktop-clippy-001.log` passe à 01:01 UTC le 20 octobre,
desktop et tous targets, warnings traités comme erreurs. Le raccordement
`FORGE_WEBVIEW_DATA_ROOT` et ses deux tests unitaires compilent ; ces tests
ne sont pas encore exécutés. La retouche suivante du point d'entrée retourne
une erreur de démarrage, sans panique, pour une configuration invalide ; elle
reste à revalider. Aucun lancement graphique n'est qualifié par Clippy.

Le packager accepte le kit déjà copié par le hook Trunk seulement lorsque sa
liste fermée de fichiers et toutes ses empreintes correspondent au kit gouverné
explicitement fourni ; il ne duplique pas ses pins et refuse toute substitution
ou fichier ajouté. `candidate-kit-input-tests-001.log` et son reçu JSON passent
sur F: avec fixtures de worker et de démarrage Web, routes d'auteur, tailles de
fenêtre, erreurs de provenance/empreinte et ces trois cas de kit déjà présent.
Les fixtures sont expressément des textes de test, sans WASM/GLB exécutables :
ce verdict contrôle les entrées du packager, sans build ni qualification GUI.
Le replay `candidate-kit-input-tests-002` passe également avec un fichier caché
supplémentaire : l'inventaire fermé emploie `-Force` et refuse ce fichier. Son
reçu JSON épingle les scripts de ce candidat ; le premier reçu reste historique.

`qualification-module-inputs-001/module-set-request.json`, sur F:, prépare
l'admission des archives historiques privées Cairns 1.0.1 et Relais 1.0.0 dans
une nouvelle autorité commune. Taille et SHA-256 de chaque archive ont été
vérifiés contre l'index des contenus 019 ; les archives restent inchangées sur
E:. Le reçu `preparation.json` distingue cette préparation de l'admission, du
producteur et du parcours Studio, qui ne sont pas encore exécutés sur ces entrées.

Le prérequis `data-wasm-opt="z"` est relevé dans l'entrée Trunk. L'outil existant
Binaryen 131 est recopié avec sa licence Apache 2.0 dans
`F:/OpenRPG_Forge/.build/tools/binaryen-131-001/`, SHA-256 identique et version
contrôlée par exécution. `wasm-opt-tool-001.json` conserve les chemins et pins ;
aucun téléchargement ni build frontend n'est assimilé à cette préparation.

`lot-c-narrative-008/source-inputs.json` épingle le candidat des retouches UX.
`narrative-studio-wasm-clippy-003.log` conserve son échec à 01:12 UTC : un
`collapsible_if` dans le champ de valeur, sans erreur de typage des formulaires.
La condition est regroupée sans changement de comportement ; un replay strict
est requis avant tout verdict sur ce candidat retouché.
`narrative-studio-wasm-clippy-004.log` passe à 01:21 UTC, sur
`lot-c-narrative-009/source-inputs.json`, warnings projet traités comme erreurs.
L'avertissement de compatibilité future `proc-macro-error2 2.0.1` reste visible.
Le champ typé, graphe, commandes de suppression, règles et gardes de brouillon
compilent ; leur parcours GUI reste à qualifier. Les fichiers préparatoires des
factions demeurent hors des modules compilés de ce candidat.

## Préparation des lots D et E — 20 octobre

[ADR-0032](../ADR/ADR-0032-simulation-clock-abilities-and-encounters.md) prépare
une horloge injectée commune aux capacités/effets, calendrier et rencontres,
avec transactions de coûts et activations bornées.
[ADR-0033](../ADR/ADR-0033-bounded-scripts-and-skippable-cinematics.md) documente
la sélection d'un AST JSON typé pour le premier profil de scripting et le
contrat des cinématiques interrompables. Ces documents sont des conceptions à
implémenter ; les capacités D/E restent indisponibles et aucun de leurs critères
n'est clôturé.

`factions-studio-engine-wasm-clippy-001.log` conserve un échec à 02:00 UTC :
paramètre de callback à annoter en `bool` dans les relations du Creator. Il est
corrigé. L'inspection `studio-build-watch-before-001.json` montre également la
surveillance Cargo globale du package dans la branche de build sans desktop,
alors qu'elle ne produit aucun fichier. Cette branche émet désormais une
directive explicite pour `build.rs`, conformément à la
[documentation Cargo](https://doc.rust-lang.org/cargo/reference/build-scripts.html#change-detection) ;
la branche native Tauri conserve ses entrées de configuration/ACL. Le gain
éventuel reste à mesurer ; le replay garde le même périmètre et les mêmes lints.

`temporal.rs`, `temporal_validation.rs`, `temporal_schedule.rs`,
`ability_runtime.rs` et `temporal_clock.rs` préparent les données, validation,
échéances et transactions du lot D. Ils restent non déclarés dans `lib.rs`,
avec des raccordements de document/état/actions encore à réaliser. Ces fichiers
restent hors des modules compilés et ne rendent aucune capacité D disponible.

Le replay `factions-studio-engine-wasm-clippy-002.log` échoue à 02:11 UTC sur
`default_trait_access` dans un constructeur vide du formulaire, après résolution
du typage. Il est corrigé par `BTreeMap::new()`. L'empreinte du script de build
WASM contient maintenant `RerunIfChanged` sur `build.rs` au lieu de la date
globale `Precalculated`. Le verdict intégré reste à acquérir.

`factions-studio-engine-wasm-clippy-003.log` passe à 02:22 UTC sur
`lot-c-factions-007/source-inputs.json`, moteur et Studio sélectionnés explicitement,
warnings projet traités comme erreurs. Le contrôle prend 8 min 51 s : aucun gain
global de durée n'est établi par la correction de surveillance. Celle-ci est
confirmée par l'empreinte Cargo ; l'avertissement de compatibilité future
`proc-macro-error2 2.0.1` demeure visible. Le scénario physique natif et les
parcours GUI restent à exécuter.

`factions-rapier-tests-001.log`, sur `lot-c-factions-008/source-inputs.json`,
compile actuellement le témoin physique natif avec `-C debuginfo=0`, caches et
temporaires sur F:. Ce profil sera conservé pour le candidat natif. Les sources
qu'il compile restent figées pendant cette préparation des modules D.

Un point de cohérence est relevé dans `execute_pending_actions` : les événements
d'un reçu déjà appliqué semblent promus de nouveau lorsqu'un retry exact est
déjà en attente. Le test `gameplay_physics/retry.rs` est préparé mais non déclaré
dans le target actif ; le candidat natif actuel reste figé. Reproduire sur les
mêmes fixtures après sa terminaison, puis corriger et rejouer si confirmé.

La préparation non enregistrée du lot D ajoute `temporal_edit.rs` et
`temporal_integrity.rs`, ainsi que les oracles dans `tests/temporal/contract.rs`.
Les contrôles source refusent maintenant les dates, périodes et délais dont la
conversion en nanosecondes déborde avant leur exécution. Les oracles couvrent
également l'exclusion symétrique entre effets et l'absence de coûts sur refus.
Ces fichiers ne sont toujours pas des cibles compilées : aucune réussite de
test D, disponibilité dans le Studio ou qualification n'en est déduite.

Le lot E dispose maintenant de sources préparatoires non déclarées :
`scripts.rs`, `script_validation.rs`, `scripts/tests.rs`, `cinematics.rs` et
`cinematic_validation.rs`. Le langage propose les opérations de l'ADR-0033,
des diagnostics avec identité d'instruction, et une API de partie explicite.
L'interpréteur travaille sur un candidat cloné et ne le promeut qu'après succès
complet. Six oracles visent les permissions, branches, quota et erreurs après
écriture. Deux autres visent les frontières temporelles et le codec de lecture
cinématique. La présentation est dérivée de l'horloge, sans curseur indépendant.
Seul leur formatage a été contrôlé à ce stade : ces tests ne sont pas encore
compilés/exécutés, et les actions, le registre, le Studio, le moteur et la reprise
de partie restent à raccorder. Les capacités du lot E restent indisponibles.

À 03:11 UTC, `lot-e-unit-probe-002/tests.log` apporte une première preuve
exécutée : **8/8 tests isolés réussis** sur l'interpréteur de script et la
présentation cinématique. Le harnais sur F: référence directement les sources
communes sur E:, avec les bibliothèques métier déjà compilées. Il ne copie pas
le produit et ne lance pas un autre build Cargo. La tentative `001`, conservée,
avait utilisé le compilateur global 1.99 incompatible avec ces bibliothèques ;
`002` emploie explicitement le compilateur 1.98 épinglé. Les neuf avertissements
de code encore non raccordé restent dans le journal : ce contrôle ne constitue
ni un Clippy strict ni un build du produit. Les validations source préparées
dans `script_validation.rs` et `cinematic_validation.rs` ne sont pas couvertes
par ce harnais. Les raccordements et qualifications du lot E restent à faire.

## Préparation des formulaires du lot D — 20 octobre

Les nouveaux fichiers `editor/temporal_controls.rs` et ses sous-modules Studio
préparent l’édition structurée de l’horloge, des échéances, des rencontres,
des effets et capacités, ainsi que leur attribution aux acteurs. Les coûts et
effets se composent d’abord dans un brouillon local ; les mutations de source
passent par `Edit` et le preview/apply/historique commun. Les suppressions
utilisées sont refusées et les conditions non représentées par le formulaire
restent conservées tant qu’un remplacement n’est pas demandé.

Ces modules ne sont pas déclarés dans `editor.rs` : aucun nouveau panneau n’est
donc annoncé disponible. Le formatage Rust est contrôlé ; typage Leptos,
Clippy WASM, raccordement au domaine et parcours GUI restent à vérifier.
Le candidat physique natif `factions-rapier-tests-001` reste inchangé et poursuit
sa première compilation de dépendances sur F:.

## Préparation du raccordement de partie du lot E — 20 octobre

`script_runtime.rs`, `script_ledger.rs`, `cinematic_runtime.rs` et
`presentation_edit.rs` préparent les transactions, la relecture des scripts et
les candidats de document. La porte utilise le helper métier ordinaire ; sa
visibilité doit être élargie au crate au raccordement, sans copier ses règles.
Le diagnostic typé, les actions/événements et les champs de partie restent à
ajouter. La relecture prévue des variables doit appeler le même interpréteur
pour chaque reçu de script avant comparaison de l’état et des portes.

`tests/presentation/contract.rs` prépare onze tests de partie : API réelle,
permissions, budgets CPU/événements, portée, reprise et retry, saut/interruption,
quête active terminable et récompense unique après reprise, puis altérations
rehashées de variables, porte, compteur, progression et terminaison de séquence. Ce fichier
reste sous un répertoire non découvert comme cible Cargo, en attendant le
raccordement. Les nouveaux modules et ces tests ne sont pas compilés : les
**huit tests isolés précédents** demeurent la seule preuve exécutée du lot E.

`native-input-stability-001.json`, à 03:29 UTC sur F:, compare 302 fichiers
existants des sources core/gameplay/moteur/projet, du target physique et des
entrées Cargo avec `lot-c-factions-008/source-inputs.json` : **aucune différence**
sur ce périmètre. Les modules préparatoires non déclarés sont exclus ; cette
comparaison ciblée n’affirme pas l’absence de nouveaux fichiers. Le handle de
compilation `11851` reste vivant ; un timeout d’observation ne provoque aucune
relance. Aucun verdict de test physique n’est encore acquis.

`cinematic_ledger.rs` prépare la vérification du départ, des arrêts, de la
progression sauvegardée et de la terminaison à la frontière observée. Il reste
non déclaré et non compilé, avec les autres raccordements de partie du lot E.

`script_edit.rs` ajoute des opérations de brouillon par identité stable :
insertion, retouche, retrait et déplacement d’instructions, avec contrôle de
forme avant copie et avant résultat. Déplacer un parent dans son sous-arbre est
refusé. Le contrôle complet des références et du programme reste celui de la
promotion du document, notamment pour un corps de répétition encore vide.
À 03:37 UTC, `lot-e-unit-probe-003/tests.log` passe **13/13 tests isolés** : les
huit précédents et cinq nouveaux cas d’édition. Les sources communes sont
référencées directement par le harnais sur F: ; le compilateur épinglé 1.98 et
les mêmes bibliothèques métier sont utilisés. Huit avertissements de code non
raccordé restent visibles. Ce résultat ne compile ni les ledgers/adapters de
partie ni les formulaires ; il ne rend aucune capacité E disponible.

À 03:41 UTC, `lot-d-calendar-probe-001/tests.log` apporte **8/8 tests isolés
réussis** pour `temporal.rs` et `temporal_schedule.rs` : changement de jour,
précision entière, échéance initiale et bornes exclusives, récurrence sans
doublon, ordre déterministe et refus de surcharge/débordement. Le harnais sur
F: référence les sources communes et le compilateur 1.98 épinglé ; vingt
avertissements de types encore non raccordés sont conservés. Cette preuve ne
compile pas les validations de document ni les transactions de partie ; elle
ne qualifie ni les rencontres, ni les sauvegardes de partie, ni le Studio.

`editor/cinematic_controls.rs` prépare maintenant un formulaire de séquence :
segments identifiés, répliques/personnages, durées, ordre et caméra en mètres et
degrés. Les segments restent un brouillon avant le preview/apply commun.
Le fichier reste non déclaré dans `editor.rs`, formaté mais non compilé ; aucun
parcours de lecture, de caméra ou de contrôle joueur n’est encore qualifié.

`editor/script_controls.rs` et `script_controls/instruction_fields.rs` préparent
les permissions de lecture/écriture/portes et un formulaire d’instructions
imbriquées. Les opérations de brouillon utilisent `script_edit.rs`, avec
identités conservées et refus de changement de type qui supprimerait des
sous-instructions. Les valeurs suivent le type de la variable et les mutations
de document passent par le preview/apply commun. Ces fichiers restent non
déclarés, formatés mais non compilés ; aucun panneau ni appel de partie n’est
annoncé disponible par cette préparation.

## Preuve physique des factions — 20 octobre, 04:04 UTC

`factions-rapier-tests-001.log` et son reçu terminent avec le code **0** après
95 min 04 s de première compilation native, profil conservé `-C debuginfo=0`
et tous caches/temporaires sur F:. Le test
`local_faction_witnesses_follow_real_occlusion_and_preserve_their_distinct_causes`
passe : vraie occlusion Rapier d'un témoin, retrait du mur, seconde conséquence
avec cause distincte, Source inchangée et Save/Resume v4. Le scénario est
headless, sans verdict de rendu ni de formulaire.

Le target `gameplay_physics/retry.rs` est maintenant déclaré dans le fichier
de test commun. `lot-c-factions-retry-001/source-inputs.json` épingle 6 148
entrées avant `factions-retry-red-001.log`, qui vise le replay d'événements sur
retry exact. Le code de promotion reste inchangé pour acquérir la reproduction.

À 04:09 UTC, `factions-retry-red-001.log` reproduit le défaut : les assertions
de séquence, coût, vitalité et relation passent, mais deux événements de dégâts
sont promus au lieu d'un. Le reçu métier est idempotent ; l'adaptateur rejoue sa
projection. La promotion exige désormais une séquence de reçu supérieure à
celle de la session avant l'appel. Un reçu antérieur reste consultable et son
feedback indique qu'il est déjà appliqué. Le replay physique ciblé doit encore
confirmer cette correction et conserver le scénario d'occlusion.

`factions-retry-green-001.log` passe à 04:15 UTC sur
`lot-c-factions-retry-002/source-inputs.json` : **2/2 tests physiques réussis**,
retry exact sans nouvelle promotion et occlusion à causes distinctes conservée.

## Raccordement du lot D — 20 octobre

Les modules temporels sont déclarés dans le domaine : document facultatif,
présence d'acteur par défaut compatible, horloge injectée par frame, capacités,
effets et rencontres, conservation des coûts et reconstruction à la reprise.
La sauvegarde v5 est utilisée seulement si la simulation est configurée.
`temporal-domain-tests-001.log` passe à 04:22 UTC sur
`lot-d-domain-001/source-inputs.json` : **10/10 tests de partie réussis**.
L'import inutilisé `GameState`, signalé par ce premier build, est retiré ensuite.
Un cas supplémentaire refuse le réarmement récurrent sans échéances récurrentes.

Le moteur fournit le pas `Time<Fixed>`, filtre les acteurs absents et projette
l'activation sur les mêmes identités ECS. Le menu natif raccorde capacités,
annulation d'effets et calendrier. Les formulaires temporels sont maintenant
déclarés dans le Studio. Ces raccordements restent à compiler et à qualifier ;
le registre décrit le contrat implémenté du candidat, sans verdict de parcours.

`temporal-domain-project-clippy-001.log` conserve les refus de qualité : imports
globaux, `self` inutilisé et validations trop longues. Les imports sont précisés,
l'annulation devient une fonction associée et les validations d'événements et de
calendrier sont séparées. Le replay strict `temporal-domain-project-clippy-002.log`
passe à 04:35 UTC sur `lot-d-domain-003/source-inputs.json`, tous targets
gameplay/projet, warnings traités comme erreurs.

`temporal-gameplay-tests-002.log` passe à 04:42 UTC sur
`lot-d-gameplay-004/source-inputs.json` : **102 tests gameplay réussis**, incluant
les formats historiques et onze contrats de partie temporels. Le test ajouté
ensuite au host v5 est épinglé dans `lot-d-host-005/source-inputs.json` ;
`temporal-host-save-tests-001.log` passe à 04:46 UTC : **8/8 tests host réussis**,
dont coûts d'objet/endurance, effet sauvegardé puis expiré après Load/Resume,
retry sans nouvelle consommation et source authorée inchangée. Deux nouveaux
scénarios Rapier attendent leur compilation :
protection/expiration après reprise et apparition d'un participant sur sa même
identité, contact réel puis reprise sans doublon. Les tests graphiques restent
distincts de ces contrôles.

## Validation physique temporelle et raccordement E — 20 octobre

`temporal-rapier-tests-001.log` termine à **04:59 UTC**, code **0**, après
11 min 14 s de build : **2/2 tests physiques réussis**. Manifeste associé :
`lot-d-physics-006/source-inputs.json` (6 153 entrées), dans le répertoire
de preuves F: indiqué ci-dessus. La protection modifie les dégâts réels,
expire sur les pas fixes après reprise et ne consomme pas une seconde fois
les coûts. Le participant d'une rencontre utilise son même corps, devient
visible et joignable au contact, puis conserve une seule activation après
reprise. Ces tests sont headless ; ils ne qualifient pas le rendu ou le Studio.

Les modules E sont maintenant déclarés dans `forge_gameplay` : champs source
facultatifs, actions de script/début/passage/interruption, interprétation sur
clone, relecture des événements et du contrôle joueur à la reprise, sauvegarde
v6 seulement pour les sources E. Les sauvegardes sans E conservent leur
version historique. Les scripts exposent seulement variables, portes et messages,
avec permissions distinctes et budgets bornés ; aucune autorité système ambiante.
Les séquences dérivent leur progression de l'horloge commune, avec caméra neutre.
Les adapters de caméra, arrêt d'entrée joueur, actions natives et formulaires
sont déclarés ; leur compilation et leur qualification restent à acquérir.

`lot-e-domain-001/source-inputs.json` épingle 6 156 entrées avant le premier
build `presentation-domain-tests-001.log` visant onze scénarios de vraie session.
Les treize tests isolés antérieurs restent une preuve distincte. L'ancienne
référence de test à « scripts non implémentés » est remplacée par le refus
d'une capacité future inconnue et d'une version future du profil partagé,
puisque le candidat E implémente désormais le contrat borné de script.

`presentation-domain-tests-001.log` passe à **05:02 UTC**, code **0** :
**11/11 tests de session E**. Les refus de permissions après écriture et les
budgets CPU/événements annulent le candidat entier. Le script conserve variables,
porte et reçu unique après reprise/retry. Les séquences reprennent au milieu
d'un plan, se terminent une fois, peuvent être sautées/interrompues, et conservent
une quête active puis sa récompense unique après reprise. Les altérations
d'état et de reçus, même réempreintées, sont refusées.

Les trois premiers journaux `presentation-domain-project-clippy-001/002/003.log`
conservent leurs échecs : import de test absent, validateur trop long, point-virgule
de test et longueur du scénario host temporel. Ils sont corrigés sans désactiver
de lint ni retirer d'assertion. **`presentation-domain-project-clippy-004.log`
passe à 05:14 UTC**, tous targets gameplay/projet. Son manifeste est
`lot-e-domain-005/source-inputs.json`, 6 158 entrées. Les nouveaux scénarios
host v6 sont compilés par ce contrôle mais ne sont pas encore exécutés.
Le replay complet gameplay utilise `lot-e-gameplay-006/source-inputs.json`.

Deux scénarios physiques E sont déclarés, sans verdict : caméra et entrées
après sauvegarde/reprise/fin, puis restitution après saut et interruption.
Le formulaire de script conserve maintenant le diagnostic typé avec un lien
vers l'instruction retouchable. Une vérification de brouillon utilise une
nouvelle partie en mémoire, les permissions et budgets réels du domaine ; elle
ne modifie pas les sources ni une partie jouée et ne fournit pas de preuve
physique de visibilité. Le contrôle WASM et le parcours diagnostic/correction/
replay restent à acquérir avant qualification de cette surface.

Le replay complet `presentation-gameplay-tests-001.log` termine à **05:18 UTC**,
code **0**, avec **126 tests réussis** et aucun échec. Le contrôle WASM strict
`temporal-presentation-studio-engine-wasm-clippy-001.log` conserve son refus
à 05:24 UTC : deux branches de résolution de cible ont un corps identique.
Les branches capacité/attaque sont regroupées dans le moteur et la présentation
native, sans changement de comportement ni suppression de contrôle.
Le replay `temporal-presentation-studio-engine-wasm-clippy-002.log` utilise
`lot-de-studio-008/source-inputs.json`, **6 160 entrées** ; son lancement
ne constitue pas un verdict. La [matrice de qualification](../evidence/gate5/GATE5_QUALIFICATION_STATUS_20261020.md)
rapproche les treize domaines et R5-P01/P02, avec leurs preuves et réserves.

`temporal-presentation-studio-engine-wasm-clippy-002.log` termine à 05:27 UTC :
le moteur passe et le Studio conserve deux refus de lint (point-virgule et
argument d'erreur non consommé). Les corrections empruntent l'erreur au lieu de
la passer par valeur et ajoutent le point-virgule. Le replay strict
`temporal-presentation-studio-engine-wasm-clippy-003.log` utilise
`lot-de-studio-010/source-inputs.json`, 6 160 entrées.

**`presentation-host-save-tests-001.log` passe à 05:29 UTC : 9/9 tests natifs**,
formats v1 à v6, manifeste `lot-e-host-009/source-inputs.json`. Le nouveau scénario
v6 configure script et séquence par la commande normale, sauvegarde en cours de
plan, ferme/réouvre le host, vérifie le même hash stocké, reprend le script sans
nouvelle mutation sur retry puis saute la séquence. État narratif et octets de
source authorée sont conservés. Cette preuve de persistance n'est pas un parcours
graphique du Studio ni un test de caméra du moteur.

**`temporal-presentation-studio-engine-wasm-clippy-003.log` passe à 05:36 UTC**,
moteur et bibliothèque Studio WASM/WebGPU, warnings projet traités comme erreurs.
Les formulaires D/E et le diagnostic vers l'instruction compilent dans le même
Studio. L'avertissement de compatibilité Rust future de `proc-macro-error2`
reste visible ; il ne constitue pas un échec du contrôle strict du projet.
Le replay physique complet `presentation-rapier-tests-001.log` est ensuite
lancé sur `lot-e-physics-011/source-inputs.json`, 6 160 entrées : 18 scénarios
natifs, dont reprise/fin et saut/interruption avec restitution de caméra et
commandes. Les captures et parcours graphiques restent à qualifier séparément.

`presentation-engine-boundaries-001.log` passe les **six contrôles Python** des
frontières moteur/contenus et de propriété des entrées compilées. Son reçu sur
F: conserve les empreintes du test et du journal ; le manifeste de référence est
`lot-e-physics-011/source-inputs.json`. Ces contrôles n'exécutent ni Cargo ni GPU
et ne constituent pas une preuve de runtime générique.

Après le contrôle WASM, une relecture des sources repère des libellés français
mal encodés dans trois fichiers du panneau de scripts. La conversion accidentelle
UTF-8/CP1252 est inversée exactement, sans copie de code, avec empreintes avant/
après dans `script-label-encoding-repair-001.json` sur F:. Les noms, aides et le
lien « Retoucher l’instruction signalée » retrouvent leurs caractères corrects.
Le scan du panneau gameplay et de la crate métier ne trouve plus ces marqueurs.
Le prochain candidat graphique doit inclure ces octets corrigés ; le résultat
WASM003 reste celui du candidat antérieur, et ne qualifie pas leur affichage.

Trois anciens tests projet sont adaptés au contrat E maintenant disponible :
le constructeur commun accepte la capacité de script borné, tandis que le codec
refuse une capacité future inconnue ; package et édition de zone continuent à
refuser une version future du profil Creator, sans mutation. Leur nouveau replay
reste à acquérir. Les contrats métier et les sources du build physique en cours
ne sont pas modifiés par ces retouches de tests et de libellés.

`presentation-rapier-tests-001.log` termine à **05:48 UTC**, code **101** :
**17/18 tests réussis**. Saut et interruption restituent caméra et commandes,
mais le scénario reprise/fin s'arrête avant ces assertions sur un mauvais oracle
de retry. `last_events` conserve intentionnellement le dernier lot non vide ;
il ne doit pas devenir vide après un retry qui ne produit aucun événement.
Le test envoie maintenant deux requêtes identiques dans le même lot et exige
une seule séquence, un seul reçu, une seule écriture et une seule promotion du
message. Le contrat moteur reste inchangé. Le reçu de l'échec est conservé.
Le replay complet `presentation-rapier-tests-002.log` utilise
`lot-e-physics-012/source-inputs.json`, 6 160 entrées ; son verdict reste à acquérir.

**`presentation-rapier-tests-002.log` passe à 05:55 UTC : 18/18 tests physiques**,
code **0**, après 5 min 15 s de build. Caméra et contrôle joueur sont restitués
après reprise/fin, saut et interruption. Les deux commandes de script identiques
ne produisent qu'une séquence, un reçu, une écriture et une promotion du message.
Les anciens scénarios de contact, sauvegarde, factions et temps restent verts.
Le contrôle est headless ; il ne remplace pas les captures et parcours graphiques.

Le contrôle natif complet `gate5-native-workspace-clippy-001.log` s'arrête avant
compilation, faute de `anstream 1.0.0` dans le cache hors ligne. La fermeture
verrouillée du CLI est préparée sur F: par le préparateur existant : **62 paquets**,
dont **13 extraits et vérifiés** et 49 sources existantes revérifiées. Le reçu
`registry-prepare-cli-001.json` conserve les SHA-256 ; le lockfile reste identique
à `lot-g-native-013/source-inputs.json`, empreinte
`c9da3d205f49b5e7755d508ca2b76191f0a00a216f9e03efc3986ea8ea689081`.
Le cache C: existant est seulement lu ; archives, sources extraites et temporaires
sont sur F:. La préparation se termine avant toute relance de Cargo.
Le replay strict natif `gate5-native-workspace-clippy-002.log` utilise
`lot-g-native-014/source-inputs.json` (6 160 entrées), workspace et tous targets.
Son lancement n'acquiert aucun verdict de qualité ou de tests.

La préparation du build graphique est consignée dans
`frontend-build-preflight-001.json` sur F: : versions exécutées et empreintes
de Trunk **0.21.14**, wasm-bindgen **0.2.127** et wasm-opt **version_131**, plus
pins de `Cargo.lock`, de la configuration Trunk et du manifeste d'assets statiques.
Les outils déjà installés sur C: sont seulement exécutés/lus ; wasm-opt réside
sur F:. Le prochain build doit fixer `TRUNK_TOOLS_WASM_BINDGEN=0.2.127`,
`TRUNK_TOOLS_WASM_OPT=version_131`, `TRUNK_OFFLINE=true` et
`TRUNK_SKIP_VERSION_CHECK=true`, avec le dossier wasm-opt F: en tête de PATH,
un dist F: neuf et les racines Cargo/temporaires F: habituelles.
La [résolution d'outils de Trunk v0.21.14](https://github.com/trunk-rs/trunk/blob/v0.21.14/src/tools.rs#L273-L283)
retourne le binaire système quand sa version correspond, avant le recours à son
cache de téléchargement. Cette préparation ne constitue ni un build ni une
preuve d'absence de nouvelles écritures lors d'un lancement graphique futur.

Le [protocole graphique](GATE5_UI_QUALIFICATION_PROTOCOL.md) rapproche les treize
domaines, les deux modules, les opérations réelles de sauvegarde/reprise et
les oracles de configuration commune/isolation. Il s'appuie sur les libellés
des formulaires présents. Son état reste **à exécuter** : aucune rédaction de
parcours ne devient une capture ni un verdict. Les cas de budgets CPU et
d'événements y sont séparés pour attribuer correctement un refus.

`gate5-native-workspace-clippy-002.log` termine à **06:30 UTC**, code **101**,
après 29 min 37 s : quatre refus `format_push_string` dans les nouveaux résumés
natifs du calendrier et des cinématiques. Les écritures formatées vont désormais
directement dans la chaîne de résumé, sans allocation temporaire ni exception de
lint. Le comportement de simulation n'est pas modifié.

Le wrapper de validation écrit et flush chaque ligne Cargo dans son journal
UTF-8 sur F:, pour rendre les longs contrôles observables pendant l'exécution.
`cargo-wrapper-streaming-smoke-001.log` confirme le succès normal ;
`cargo-wrapper-streaming-negative-001.log` conserve le diagnostic et le code 1
d'une option volontairement invalide. Ces deux essais ne compilent aucun produit.
Le replay strict natif courant est `gate5-native-workspace-clippy-003.log`,
manifeste `lot-g-native-015/source-inputs.json`, **6 161 entrées**.

Ce replay termine le 20 octobre à **07:02 UTC**, code **0**, après **28 min 52 s** :
Clippy strict natif `--workspace --all-targets --locked --offline -j2 -- -D warnings`
est **PASS**. Le reçu conserve les chemins Cargo, target et temporaires sur F:
et `RUSTFLAGS=-C debuginfo=0`. Cette vérification n'exécute pas les tests et ne
qualifie pas les parcours graphiques. Les refus 001/002 restent historiques.

Les tests natifs complets sont ensuite lancés dans
`gate5-native-workspace-tests-001.log`, manifeste
`lot-g-workspace-tests-016/source-inputs.json` (**6 161 entrées**), avec
`test --workspace --all-targets --locked --offline -j2 -- --test-threads=1`.
`clippy003-to-workspace-tests001-inputs.json` confirme des entrées compilées
inchangées : seuls les trois documents de résultat ont changé. Un lancement
ne constitue pas un verdict de test ; le reçu terminal reste à acquérir.

Le superviseur local de preuve `sequenced-frontend-001.ps1`, conservé uniquement
sur F: (SHA-256 `061e3b2ceda474b66929615ac97214befd91ca4e47066620a1fd0c6ffb16320e`),
attend le processus exact de cette suite. Son état initial vérifié est
`WAITING_FOR_WORKSPACE_TESTS` dans `sequenced-frontend-001.state.json`.
Il ne lance le frontend release qu'après reçu terminal réussi, contrôle de la
commande de test, des outils/static inputs épinglés et des sources compilées.
Le futur inventaire est `lot-g-frontend-017/source-inputs.json`, la destination
fraîche `frontend-release-001/`, le journal `frontend-release-001.log` et le reçu
`frontend-release-001.receipt.json`. Un échec ou une variation des entrées arrête
la séquence pour inspection ; aucun verdict de build n'est acquis par l'attente.
Ce script de qualification n'est ni une copie des sources ni une autre version
du Studio. Les sorties, caches et temporaires du build restent sur F:.

`gate5-workspace-fmt-final-001.log` et son reçu constatent **PASS**, code **0**,
le 20 octobre à **07:25 UTC** pour `fmt --all -- --check`. Le contrôle lit les
sources du candidat sans les modifier ni compiler une autre cible pendant la
suite native. La suite workspace reste une exécution distincte, sans verdict
terminal acquis à cette heure.

La préparation du lancement confirme le rôle distinct des exécutables : le jeu
séparé configuré près du Studio doit provenir de
`forge_tidewater --example tidewater_plant_probe`, avec son entrée générique
`--module`. Le validateur `forge_runtime` n'accepte pas ce contrat et ne le
remplace pas. La recette locale d'optimisation du jeu est documentée dans
`scripts/delivery/README.md` et `scripts/delivery/complete_rebuild.py` ; elle peut
être exécutée dans le checkout commun avec manifeste, sans copie de sources.
Le lanceur `native_game_module.rs` fournit explicitement cache, sauvegardes,
rapport et répertoire courant à partir de la racine de session configurée sur F:.
Cette inspection prépare le futur lancement ; elle ne constitue pas son succès.

L'audit du lockfile est lancé dans `gate5-rustsec-audit-001.log` avec
`audit --db F:/OpenRPG_Forge/.build/cargo-home/advisory-db --json`.
Cette opération ne compile aucune cible et ne modifie pas les dépendances.
La base est explicite sur F:, comme les caches et rapports. Le lancement et
le processus actif ne constituent pas un verdict : le reçu terminal, le rapport
JSON et la comparaison exacte avec `docs/evidence/gate0/RUSTSEC_POLICY.json`
restent à acquérir. Aucune exception ni nouvelle entrée de politique n'est ajoutée.

L'audit termine à **08:39 UTC le 20 octobre**, code **0**, après 16 min 41 s.
Le reçu `gate5-rustsec-policy-001.json` constate **PASS** pour la comparaison
exacte : **954 dépendances**, `vulnerabilities=0` et **10 avertissements** identiques
à la politique existante, sans filtre de plateforme ni avis ignoré. Le lockfile
et la politique ont les mêmes empreintes que le manifeste de la suite workspace.
La base contient 1 338 avis au commit
`999ec89d5d3f10f20be14750ec8c2d8b44e0ac9b`, dernière mise à jour déclarée le
17 octobre. La réserve de distribution `RUSTSEC-2024-0429` sur `glib 0.18.5`
reste bloquante selon la politique ; ce résultat ne qualifie ni la distribution
ni Linux. Licences, bans et sources restent un contrôle distinct à acquérir.

Le replay d'architecture Python termine à **09:09 UTC**, code **0** :
`gate5-engine-boundaries-final-001.log` et son reçu constatent **6/6 PASS**.
Le manifeste `lot-g-engine-boundaries-final-001/source-inputs.json` contient
6 161 entrées ; son rapprochement avec le manifeste des tests workspace ne
trouve aucun changement des sources compilées, uniquement trois documents.
Les contrôles couvrent les frontières moteur/contenu, les implémentations
d'exemples partagées et l'exclusion des candidats d'orchestration des entrées
de compilation. Ils ne remplacent pas les parcours graphiques à deux modules.

`supply-chain-preflight-001/preflight.json` prépare le contrôle cargo-deny
**0.20.2**, sans l'exécuter. Sa configuration `deny-f-storage.toml`, uniquement
sur F:, dérive du `deny.toml` commun avec un seul ajout :
`advisories.db-path = "F:/OpenRPG_Forge/.build/cargo-home/advisory-dbs"`.
La comparaison TOML interprétée est strictement identique après retrait de ce
chemin ; aucune licence, exception, règle de ban ou source n'est changée.
Les trois cibles Windows/Linux/WASM et `all-features = true` sont conservés.
Le [paramètre officiel](https://embarkstudios.github.io/cargo-deny/checks/advisories/cfg.html#db-path)
désigne la racine des bases ; les autres caches et temporaires restent bornés
par l'environnement sur F:. Les empreintes de l'outil, du lockfile et des deux
configurations sont dans le reçu. Il s'agit d'une préparation, pas d'un PASS
des contrôles licences/bans/sources/advisories.

La suite `gate5-native-workspace-tests-001.log` termine à **09:57 UTC** avec le
code Cargo **101**. Le groupe `forge_project --lib` compte **101 réussites et
1 échec** : `separate_game_exports_current_unsaved_zone_without_saving_or_freezing_it`.
Son export réussit ; l'inspection suivante échoue car la fixture injectait une
session privée `Managed` sans workspace ni module propriétaire. Le frontend
conditionnel s'arrête comme prévu, avant tout build ; son reçu
`frontend-release-001.receipt.json` conserve `build_started_utc = null`.

Le test est déplacé vers `tests/module_creator.rs` et utilise la fixture publique
à module réel : ouverture gérée, aperçu/application d'une retouche non sauvegardée,
archive attendue préparée indépendamment par la session de domaine, refus du hash
périmé, état modifiable sans playtest et comparaison des fichiers avant/après
export. L'exigence de propriétaire et les règles produit ne sont pas changées.
Ce correctif de fixture reste à revalider ; la suite globale conserve son FAIL.

Le replay ciblé est lancé dans `gate5-managed-export-fixture-test-001.log`, sur
`lot-g-export-fixture-019/source-inputs.json` (6 161 entrées), pour l'intégralité
du target `module_creator`, en série. Aucun verdict n'est acquis par son lancement.
Le formatage du candidat corrigé passe à **10:05 UTC**, code **0**, dans
`gate5-workspace-fmt-final-002.log` ; il n'a modifié aucune source.

Le replay d'intégration termine à **10:10 UTC**, code **0** : **10/10 PASS**,
dont le scénario d'export corrigé sur module réel, le contrôle des capacités
cachées/futures, Save/Open/Undo/Redo et l'isolation inter-modules. Le test déplacé
conserve l'export exact de la retouche non sauvegardée, le refus du hash périmé,
la possibilité d'Undo et l'absence d'écriture pendant l'export/l'inspection.
Ce résultat ne remplace pas le replay global du workspace, encore requis.

Clippy strict natif du workspace, tous targets, est ensuite lancé dans
`gate5-native-workspace-clippy-004.log`, manifeste
`lot-g-native-020/source-inputs.json` (6 161 entrées), sur le candidat corrigé.
Le nouveau passage global des tests attend ce verdict ; le frontend reste non
construit et les refus historiques sont conservés.

La prochaine séquence frontend est préparée, **non lancée**, dans
`sequenced-frontend-002.ps1`, SHA-256
`5c2a758892dfabb21726be58859866fd49dc51eac730a575dd0917ff788110bb`.
`frontend-sequence-002-preflight.json` conserve sa vérification syntaxique.
Elle reçoit le PID et l'heure de création du futur processus de tests, vérifie
son identité et le reçu `gate5-native-workspace-tests-002.log.receipt.json`, puis
contrôle les entrées avant le build. Les emplacements réservés sont
`lot-g-workspace-tests-021/`, `lot-g-frontend-022/` et `frontend-release-002/`.
Un garde préalable préserve tout reçu déjà présent ; les résultats 001 ne sont
pas réécrits. Caches et temporaires sont définis sur F: dès l'entrée du script.

`gate5-native-workspace-clippy-004.log` termine à **10:37 UTC**, code **0**,
après **25 min 44 s** : **PASS** strict natif du workspace et de tous ses targets
sur la fixture corrigée, sans désactivation de lint. Les tests globaux suivants
utilisent `gate5-native-workspace-tests-002.log` et le manifeste
`lot-g-workspace-tests-021/source-inputs.json`. Le fichier de processus associé
épingle le PID et l'heure de création du wrapper pour le frontend conditionnel.
Le verdict global des tests reste à acquérir.

La séquence frontend 002 est désormais lancée : elle attend le wrapper des
tests 002 (PID 14856, création `2026-10-20T10:39:57.8375164Z`). Le séquenceur
`sequenced-native-candidates-001.ps1` est lui aussi lancé sur F:, en attente
du frontend 002 (PID 15220, création `2026-10-20T10:43:14.7553707Z`). Son
empreinte est `bfb3f2aac33e8e40d59de6f651beb2025643139dc059ea2a3508cef428b19458`.
Après reçu frontend réussi et contrôle des sources compilées, il construit
séquentiellement le jeu séparé, le candidat Studio puis le producteur de
workspace. L'inventaire prévu est `lot-g-native-candidates-023/` ; les reçus
et états restent sous le dossier de preuves F:. Aucun lancement graphique,
import des deux packages ni verdict de qualification n'en est déduit.

### Reprise après maintenance du stockage — 20 octobre 2026

Les processus des tests 002 et des deux séquenceurs sont absents, sans reçus
terminaux. Ils sont classés interrompus, sans PASS ni FAIL fonctionnel inventé ;
ne pas réutiliser leurs scripts, dont les builds visent l'ancien cache F:.
Le fetch RustSec 001 a un reçu d'échec : Git refuse la propriété du dépôt exFAT.
Il ne prouve aucun défaut du code ni aucun contrôle supply-chain réussi.

La consigne actuelle utilise exclusivement le cache partagé
`E:/OpenRPG_Forge/.build/cargo-target`, Cargo home et temporaires voisins sur E:.
Les nouvelles preuves sont centralisées dans
`../Preuves-et-analyses/actives/gate5/20261019/`, physiquement sur F:.
`source-inputs-reprise-20261020.json` et `reprise-stockage-20261020.json`
épinglent 6 161 entrées et les différences : aucun fichier métier ni lock changé,
mais profil de compilation, configuration, lanceurs et documentation actualisés.
La reprise complète, avec le profil actuel et sans RUSTFLAGS hérité, utilise
`gate5-native-workspace-tests-e-001.log`. Son verdict reste à acquérir.
Le constructeur Studio respecte désormais les flags de l'appelant et le profil
commun au lieu d'imposer une seconde variante `debuginfo=0` ; syntaxe et diff
contrôlés. Les reçus historiques ne sont pas réécrits.

Estimation de la reprise : preuves < 2 Gio et 5 000 fichiers, sous le budget
conseillé de 20 Gio / 50 000 fichiers. La reconstruction du cache partagé neuf
sur E: réserve jusqu'à 200 Gio, sans cache par essai ; 686 Gio sont libres au
démarrage. Cette estimation ne supprime aucune donnée. Les parcours graphiques
et le verdict final restent à acquérir.

Les cinq outils de qualification (Trunk, wasm-bindgen, Tauri, cargo-deny et
cargo-audit) sont épinglés et vérifiés dans `.build/tools/bin` sur E: ; Binaryen
131 y utilise son emplacement partagé déjà vérifié. Leurs hashes et versions
sont dans `shared-tools-e-001.json`. Les deux archives privées restent identiques,
avec une nouvelle requête `module-set-request-e-001.json` vers un workspace de
données isolées sur E:, sans édition des anciens épinglages.

Le fetch RustSec sur E: passe à 21:28 UTC, puis l'audit et son rapprochement
exact avec la politique passent à 21:29 : 954 dépendances, 1 348 avis dans la
base propre, zéro entrée de vulnérabilité et les dix avertissements attendus.
Le commit de base est `e6f94f3760d5e6c54c1b05f204002263bc025524` ; la réserve de
distribution GLib reste conservée. Enfin `gate5-supply-chain-check-e-001.log`
passe à 21:30 UTC, code 0 : advisories, bans, licenses et sources OK, sans
modification de politique hors chemin de stockage. Ces contrôles de graphe locaux
ne prouvent ni Linux exécuté, ni distribution, ni qualification graphique.

L'enchaînement courant est `E:/OpenRPG_Forge/.build/tools/gate5-candidate-sequence.ps1`,
SHA-256 `c8c3ced3853392fd5e48d9b6a7f9348f6453b952ceb0457cc5064cd9028da8b5`.
Sa copie de preuve et son contrôle syntaxique sont dans la campagne centrale.
Il attend l'identité exacte du wrapper de tests E: (PID 35000, création
`2026-10-20T21:22:38.6301177Z`) et exige son reçu de succès avant les builds.
Il refuse un changement des entrées compilées ; l'unique adaptation de lanceur
Studio depuis l'inventaire des tests est explicitement épinglée. Frontend et
candidat utilisent `.build/frontend/studio` et `.build/frontend/studio-native`,
le même cache, puis le producteur prépare les deux modules dans les données
isolées E:. Aucun parcours graphique n'est déduit de cet enchaînement.

La suite globale E: termine à **22:19 UTC**, code **0**, après 56 min 24 s :
**2 489 tests réussis, zéro échec, 56 ignorés, 262 cibles**. Le résumé
`native-workspace-tests-e-001.summary.json` épingle le journal et inventorie
chaque ignoré ; les anciennes sondes CPU et fixtures externes/privées restent
distinctes des succès. Les contrats Creator passent à 10/10, temporels à 11/11,
factions à 10/10 et packages à 27/27 dans ce total global.

Le frontend E: 001 échoue immédiatement avec le code 2 : `NO_COLOR=1`, hérité
de la session, est refusé par le parseur booléen de Trunk. Aucun frontend ni
candidat n'est produit ; les tests réussis ne sont pas rejoués pour ce réglage.
Le refus et le séquenceur 001 restent conservés. La tentative 002 utilise
`NO_COLOR=true`, neutralise explicitement les anciens flags et commence par
Clippy natif strict puis Studio/moteur WASM strict avant les mêmes builds.
Son script commun est épinglé à
`7940351cefe94f873dcf0fafd18a0e23dc71600ba05eeb76df10b84c657cd1c6` ;
sa copie de preuve et `candidate-sequence-e-002-preflight.json` sont centraux.
Les sources métier et le cache unique ne sont pas dupliqués.

### Reproduction Windows et replay courant — 21 octobre 2026

Le refus de racine observé dans la bibliothèque d'assets distante est reproduit
localement avec une vraie jonction. Le correctif distingue l'orthographe absolue
configurée de la racine physique canonique, rebase les composants validés et
conserve les contrôles physiques de confinement. Les références vers un voisin
de même préfixe et les composants parents restent refusés. Le premier replay
vert révèle une fixture de trois octets, volontairement invalide pour le transport
GLB ; le nouveau scénario réutilise ensuite la fixture GLB valide du transport.
Les refus intermédiaires ne sont ni effacés ni remplacés.

La bibliothèque Studio passe à 198 succès, zéro échec et quatre ignorés dans
`asset-library-root-alias-green-e-002.log`. Le replay global suivant
`gate5-native-workspace-tests-e-002.log` termine le 20 octobre à 23:10:16 UTC,
code 0 : **2 490 succès, zéro échec, 56 ignorés, 262 cibles**. Clippy natif
strict `native-workspace-clippy-e-003.log` termine à 23:10:28 UTC, code 0.
Le séquenceur 003 vérifie les entrées compilées sans exception temporaire et
enchaîne Clippy WASM, frontend, jeu, Studio, producteur de modules, transport
`.orpmod` hors workspace et frontières moteur. Sa copie de preuve
`candidate-sequence-e-003.ps1` est épinglée à
`f07a5926f7f44f634e01a3a3ffd4d487004a9d9d77e35d965e8d612edf15c541`.

La mitigation CI limite le parallélisme natif et les symboles sur les runners,
puis nettoie leurs seuls intermédiaires release d'outils installés après garde
GitHub Actions et vérification du chemin. La syntaxe YAML et PowerShell est
validée dans `ci-workflow-validation-e-001.json` ; aucun workflow distant n'est
déclenché par cette validation locale. Les preuves restent physiques sur F:,
les compilations et temporaires dans le cache commun E:, sans nouveau cache C:.
Gate 5 reste ouverte jusqu'aux parcours graphiques et critères de clôture.

### Premier parcours graphique et correction d'historique — 21 octobre

Le candidat 003 termine ses builds et contrôles à 00:28:16 UTC, code 0,
avec verdict `BUILD_PASS_RUNTIME_NOT_VERIFIED`. Après résolution d'une racine
vide dans l'environnement du lanceur de qualification, les deux modules sont
listés et Les Trois Cairns s'ouvre dans le Studio avec rendu GPU. Le profil
WebView2 est effectivement sous `.build/qualification-data/gate5/webview` sur E:.

UI01 prépare puis applique une portée d'interaction commune de 3 à 4 mètres.
L'aperçu laisse les sources enregistrées inchangées. Le bouton global Annuler
révèle une annulation limitée à la zone, refusée par la cohérence avec le module.
La reproduction rouge est capturée sur F: ; la règle à 4 mètres est sauvegardée
par l'UI avant fermeture du candidat. Aucun contrat de confinement ou de profil
n'est relâché pour contourner le refus.

Le préparateur d'historique du Studio choisit désormais l'autorité du module
pour une carte gérée. Il réutilise PreviewModuleHistory et ApplyModuleHistory,
leurs épinglages et leurs refus d'effets inter-modules. Les boutons et raccourcis
utilisent la disponibilité de cette autorité ; Appliquer confirme le résultat
examiné. Les historiques autonomes de zone et de terrain conservent leurs routes.
Les 16 tests d'intégration des modules passent à 06:54:05 UTC, y compris le
nouveau cas réel avec deux modules, puis Clippy WASM strict passe à 06:55:40 UTC.

Le séquenceur de correction `history-candidate-e-001` revalide le workspace
et construit la nouvelle sortie `.build/frontend/studio-native-history`, sans
dupliquer le checkout ni le cache Cargo. L'ancien candidat fermé de 653 Mo et
3 030 fichiers est conservé pendant ce replay ; la paire reste bornée à environ
1,3 Go et 6 100 fichiers. Les preuves nouvelles restent centrales sur F:.
La clôture attend toujours le parcours graphique du correctif et tous les
autres critères de la matrice.

Le replay global du correctif passe à 07:25:08 UTC : 2 491 succès, aucun échec,
56 ignorés et 262 cibles. Clippy refuse ensuite uniquement les 121 lignes du
nouveau test d'intégration. Les accès répétés au profil sont factorisés dans
un helper de test. La tentative `history-candidate-e-002` vérifie que toutes
les autres entrées compilées sont identiques au replay global, épingle les deux
versions de ce fichier de test et rejoue les 16 tests des modules avant Clippy
et les builds. Aucun deuxième replay global n'est exigé pour ce seul refactor
du test ; aucun code produit n'a changé depuis les 2 491 succès.
La tentative 002 passe les 16 tests à 07:30:17 UTC, Clippy natif à 07:30:20
et Clippy WASM à 07:30:25, puis entre dans la construction du frontend.

Pendant la reconstruction, le jeu natif inchangé est exercé depuis le Studio
précédent. Cinq parties prouvent l'acceptation de quête, F5, F9 et la reprise
de A, le lancement et la sauvegarde distincte de B, puis la reprise de A après B.
Les rapports `game-smoke-resume-e-001.receipt.json` et
`game-smoke-isolation-e-001.receipt.json` conservent identités, hashes,
séquences, captures et rapports de projection. Les sources auteur sont
inchangées et la sauvegarde A n'est écrasée ni par B ni par le lancement frais
intermédiaire nécessaire à sa demande de reprise. Les cinq projections ont
leurs trois modèles prêts et aucun pipeline en échec. Ces preuves fonctionnelles
ne qualifient pas le frontend d'historique en cours de reconstruction, les
surfaces configurables ni le réexport ; R5-P02 et Gate 5 restent ouverts.

La tentative 002 termine à 07:47:37 UTC. Une seconde zone vide est préparée
par les commandes natives dans les données isolées, puis son gameplay est
activé et sauvegardé dans le Studio. Le profil hérite de 4 m ; l'aperçu de
retouche à 5 m nomme les deux cartes et s'applique. L'annulation commune restaure
4 m, mais la projection terrain refuse la révision historique : elle exige
la révision suivante. Les deux captures utiles sont conservées sur F: dans
`gate5-ui01-propagation-e-001*` et `gate5-ui01-projection-red-e-001*`.

Le test moteur Rapier reproduit ce refus à 08:11:13 UTC. Le correctif sépare
explicitement projection d'un snapshot accepté et mutation ordinaire : les
bases, hashes, identités et transactions de caches restent contrôlés ; seule
la projection explicite conserve une révision historique. Les trois tests
terrain passent à 08:14:29 UTC, incluant base périmée, autre zone, préparation
sans écriture, fidélité aux données régénérées et maintien du contrat strict
des mutations. Le séquenceur `projection-history-candidate-e-001` démarre à
08:15:55 UTC : tests moteur, Clippy natif/WASM, replay global, format et builds
du frontend, du jeu et du Studio. L'ancien jeu doit aussi être reconstruit,
puisque le moteur a changé. Sa nouvelle sortie est
`.build/frontend/studio-native-projection-history` ; les trois candidats
restent estimés à moins de 2 Go et 10 000 fichiers, sans autre checkout/cache.
**UI01 reste incomplet** jusqu'au replay graphique et au calcul observé en jeu.

Le replay global de ce correctif termine à 08:46:59 UTC : 2 493 succès, zéro
échec, 56 ignorés et 262 cibles. Le format passe à 08:47:06 UTC ; le frontend
entre ensuite en construction. Pendant ce replay, les formulaires du Studio
précédent créent et sauvegardent le blueprint « G5 — Sentinelle des cairns »
avec deux instances : sa révision v2 propage 50 au pillard et préserve la
vitalité locale 60 et la fuite de la seconde. Les preuves
`ui02-blueprint-authoring-e-001*` rapprochent UI, archives et invariants préservés.
UI02/UI03 restent incomplets jusqu'à réouverture et observation dans le candidat
corrigé ; aucune qualification finale n'est déduite de cette préparation.

Le frontend corrigé termine à 09:02:28 UTC ; le séquenceur construit ensuite
le jeu séparé. Dans le Studio précédent, UI04 prépare puis sauvegarde un item
« G5 — Jeton de garde » (pile 20) et le coffre `a5f192cd-c7f6-5b33-8f1c-748ab5db8478`
à (1,5 ; 0,5 ; 1), capacité 3, deux jetons initiaux et filtre limité à cet item.
Le workspace enregistré est à la révision 19. Les données et captures vérifiées
sont sur F: dans `ui04-container-authoring-e-001*` ; UI04 reste incomplet
jusqu'à réouverture, transferts acceptés/refusés sans perte et reprise en jeu.

Le candidat de projection termine à 09:14 UTC. Son rejeu confirme Undo/Redo
4 → 5 → 4 → 5 m et le calcul en jeu : à départ identique, la gardienne située
à 4,5 m est absente des actions à 4 m, accessible à 5 m. Le dialogue booléen
et les transferts réels sont sauvegardés puis repris (séquence 3, un jeton
dans chaque inventaire). Les preuves détaillées restent dans la matrice et
dans `ui01-range-*` / `ui04-ui05-resume-e-001*` sur F:.
Le refus de dépôt n'est cependant pas accessible : les menus le masquent.
Un préparateur commun des demandes de transfert et le retour visible dans
le panneau natif corrigent ce manque sans changer les validations métier.
13 contrats de conteneurs passent à 09:53 UTC ; le premier échec d'oracle
textuel est conservé. Le séquenceur `container-feedback-candidate-e-001`
reprend contrôles et builds. Les quatre candidats conservés restent estimés
à moins de 4 Go et 14 000 fichiers ; aucune qualification finale n'est déduite.

Le lot passe ses 12 tests natifs et Clippy natif/WASM, puis entre dans le replay
global à 10:04 UTC. Pendant cette validation, la réouverture du blueprint
retrouve v2, héritage 50 et valeurs locales 60/fuite ; les preuves
`ui02-blueprint-reopen-*` sont conservées sur F:. Trois positions sont ensuite
préparées uniquement par le Studio : pillard (1,5 ; 0,9 ; 2), sentinelle
(−1,5 ; 0,9 ; 2), sceau (0 ; 0,3 ; 1). Le reçu
`ui03-ui04-nearby-authoring-e-001` vérifie que ces poses sont les seules données
gameplay changées, sans mutation des statistiques, blueprints, noms ou dialogues.
Le comportement des acteurs et le refus du filtre restent à observer dans le
nouveau candidat ; les sauvegardes précédentes restent conservées sur F:.

### Reprise du 21 octobre — solutions UI06 et candidat conteneur

Le formulaire du Studio sauvegarde deux solutions de la quête : restitution
consommant un sceau, ou accord conditionné par « Passage reconnu », consommant
deux jetons. Les deux choix du dialogue référencent leurs solutions exactes ;
les quatre textes du journal sont enregistrés. Le choix de restitution conserve
son identité et sa condition de défaite du pillard. Le reçu
`ui06-two-solutions-authoring-e-001.receipt.json` et son archive source sont dans
la campagne centrale F:. Les acteurs, objets, items, règles et récompense de base
sont inchangés par cette retouche. **Les deux parties fraîches, la reprise et
la récompense unique restent NON EXÉCUTÉES sur ces sources.**

La séquence `container-feedback-candidate-e-001` passe les tests natifs de
présentation (12), Clippy strict natif et WASM, puis la suite workspace :
**2 495 PASS, 0 FAIL, 56 ignorés, 262 cibles**. Le contrôle de formatage passe ;
la compilation du frontend est en cours. Ces contrôles ne qualifient pas encore
le refus graphique du dépôt, ni les parcours UI03 à UI13 restants.

La préparation graphique UI08 enregistre l’effet « G5 — Garde des cairns »
(10 points, 5 000 ms, cumul refusé sans coût), la capacité « G5 — Protection
ciblée » (personnage vivant à 5 m, coût de 5 points d’endurance) et son acquisition
par le Voyageur. Sources et capture vérifiées sur F: dans
`ui08-ability-authoring-e-001.receipt.json` et `ui08-ability-acquired-e-001.png`.
Le calendrier UI09 est également sauvegardé : jour 1 à 08 h 00, 1 000 ms/minute,
événement « G5 — Relève des gardes » après 20 minutes puis toutes les 20 minutes
(`ui09-calendar-authoring-e-001.receipt.json`). Ces préparations ne valident pas
les coûts, l’expiration ou le franchissement de borne avec reprise en jeu.

UI11 : la séquence « G5 — Les voix des cairns » contient deux plans de 10 000 ms,
avec caméras (0,2,-4) et (4,2,-4) regardant (0,0,0), champs 60° et 65° ;
la gardienne parle dans le plan des cairns. L’ordre est ensuite retouché par
Descendre, examiné, appliqué et sauvegardé : le plan du Voyageur précède celui
de la gardienne. Le reçu `ui11-cinematic-reorder-e-001.receipt.json` confirme
les mêmes identités et données de segments, avec aperçu sans écriture des
sources. Les captures et archives sont centralisées sur F:.
Fin, saut, interruption et reprise en cours de plan restent NON EXÉCUTÉS.
