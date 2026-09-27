# R2-001/002 — premier socle projet/module

## Isolation des brouillons d’inspecteurs entre zones

15 septembre : garde étendue au RoomInspector et aux PlacedObjectControls.
Le wrapper partagé capture la zone lors de création du formulaire, refuse une
source absente ou une autre zone et ne transmet alors aucune commande au bus.
Les sous-contrôles reçoivent le callback protégé ; navigation caméra inchangée.
Clippy Web/WASM PASS (7,00 s), formatage effectué. Test pur de comparaison
d’identité précédent réutilisé (fonction check inchangée) ; raccordement UI
compilé, non encore exercé graphiquement. C5C3C3B1 antérieur à ces protections.

Complément : chaque callback de commande des inspecteurs chemin/rivière
compare aussi l’AreaId propriétaire avec celui de la tête courante juste
avant transmission. Une autre zone ou session fermée produit une erreur
actionnable ; insertion/remplacement/verrou/suppression utilisent ce même
callback. Cette garde UI ne remplace pas les contrôles hash/révision du bus.
Test pur `draft_scope_rejects_other_zone_and_closed_session` : 1 PASS ; Clippy
Web/WASM PASS (7,06 s). Le test qualifie la décision d’identité, pas une course
d’événements graphique. Aucun candidat recompilé pour ce complément.

Inspection du code : les Memo de chemins/rivières comparaient uniquement
l’objet accepté, le mode insertion et le compteur de reset. Deux zones portant
le même objet pouvaient conserver le composant et ses champs de brouillon.
Ajout de l’AreaId au résultat mémoïsé pour reconstruire l’inspecteur lors du
changement de zone, sans le réinitialiser à chaque Inspect/Preview/Save de la
même zone. Ni mutation du document ni format ajouté.

Fmt check et Clippy Web/WASM strict PASS (7,03 s). Risque établi par lecture,
pas de reproduction graphique ; scénario de zones aux objets identiques encore
à qualifier. Le candidat C5C3C3B1 ne contient pas ce dernier correctif.

## R2-036/040 — sauvegarder un Undo puis rétablir après redémarrage

Test hôte ciblé `saved_undo_preserves_redo_after_reopening_managed_zone` :
PASS (1 test, 0,08 s ; compilation 1,30 s), Clippy test strict PASS (0,41 s).
Retouche altitude +8 m, Undo, Save, recréation complète de l’hôte, réouverture :
tête exacte et Redo toujours disponible. Redo restitue la correction, son retry
ne crée pas de tête supplémentaire ; disque inchangé avant Save. Save depuis
l’onglet voisin, nouvelle recréation et reprise exacte de la tête rétablie.
Fixture temporaire synthétique sur E: supprimée par le test, aucune donnée
utilisateur touchée. Ni nouveau build Studio ni geste GUI : cette preuve couvre
la persistance de la pile Redo pour ce parcours, pas tous les outils R2-036.

## Verdict consolidé R2-005 — onglets de zones

Qualifié localement pour le contrat actuel (16 onglets de métadonnées, une
projection active). Aux parcours renommage/reprise D75CB60E et tests hôte
ci-dessous s’ajoute le parcours géométrique réel F1C5DB66 : ouvrir les cartes
A puis B du même module, saisir altitude 6 m de B, Preview/Apply, passer à A,
revenir à B, Save explicite. A garde son hash terrain
7b206cb55323b16463a8607dcf0034c513f142166985bb1ea07f7aeab756d478 ; B garde
sa retouche non sauvegardée après changement d’onglet, terrain
168ccb12c2961a0108195d5cd1b3203a6b96cd9234a76851dbfe0ae2df20a120.
Archive disque AB913BF9 inchangée avant Save ; session propre après Save.

Copie isolée : E:/OpenRPG_Forge/.gate2-evidence/workspace-geometry-ui-20260914.
Workspace final SHA-256 :
674A0586C133789D43C26F586BED7C2F123BF615A1E2EB6D1EF638D596D0EC3D.
Référence A inchangée (archive 9d261155..., tête dd5d9626...). B archive
f7e356669d52ed624c5840a6a8eadb2922cd75f2b4a8fd685318773a64ae94af,
tête 85b99889f013ee38e04d58017085f2b29a4a633dd962460c436b26619545e122,
correction base_elevation_m=6 relue après fermeture. Captures
a-unchanged-b-dirty.jpg et b-geometry-saved.jpg dans cette copie.

Fenêtre fermée et PID 9180, 33904, 25340, 7896, 26112, 31728, 19068, 23416
tous absents au contrôle final. Aucun build/push ni écriture JSON manuelle.
La réouverture géométrique après recréation d’hôte et Undo/Redo indépendant
provient du test ciblé, pas d’un second redémarrage GUI de ce parcours.
Ne qualifie pas tous les outils, ni 16 vues GPU simultanées, ni Gate 2 globale.

## R2-005 — géométrie indépendante et plafond des onglets

14 septembre : deux tests supplémentaires dans workspace_tabs.rs, exécutés
séparément pour ne pas rejouer les scénarios déjà acquis.

- independent_geometry_and_undo_survive_switch_save_and_host_recreation : PASS
  (compilation 1,24 s, exécution 0,09 s). A à +3 m, B à -4 m ; géométrie effective
  changée, archives inchangées avant Save ; A conserve sa correction après B,
  Undo/Redo A n’altère pas B ; Save depuis B puis destruction/recréation de l’hôte
  restitue les deux têtes exactes et propres.
- tab_limit_refuses_seventeenth_without_eviction_and_releases_after_close : PASS
  (compilation 1,23 s, exécution 0,16 s). 17 zones créées par revue/commande du
  workspace ; 16 ouvertes, 17e refusée sans changement de tête/onglets/archive,
  onglet existant toujours accessible, fermeture libère une place ; ouverture
  de la 17e réussit ensuite, sans sauvegarde implicite.

Clippy ciblé --test workspace_tabs strict PASS (0,39 s). Fixtures synthétiques
sur E: via TEMP, supprimées par les tests après fermeture de l’hôte. Aucun
processus graphique lancé. Ce n’est pas un parcours souris Studio ni une
qualification de performance de 16 projections : une seule projection active.
R2-005 reste ouvert pour le parcours géométrique visible et son verdict consolidé.

## Verdict consolidé R2-001/002

Création projet et module qualifiées localement : les gestes Examiner/Apply et
Save ont été effectués dans 4FB0B367, puis leurs identités et références relues
après arrêt dans A74AEDAE/D75CB60E. Le parcours n'est donc plus seulement une
implémentation ou une compilation. Manifestes de ces trois candidats et captures
création/historique/reprise présents au contrôle documentaire final.
R2-001 et R2-002 cochés sur cette preuve, sans passage de Gate 2 ni revue humaine
inventée. L'assistant consiste en formulaires explicites, sans IA produit.
R2-005 reste ouvert pour sa qualification restante ; récupération, audit global
et conservation des retouches géométriques gardent leurs propres critères.
Les paragraphes suivants sont un journal chronologique : une ancienne mention
« encore dû » décrit la date du lot, pas une nouvelle obligation de refaire un
parcours déjà acquis. Aucun test/build lancé pour cette consolidation.

## Reprise après arrêt de la retouche multizone

Même candidat D75CB60E, même sauvegarde AB913BF9 vérifiés avant lancement.
Après nouveau processus, Open workspace puis Open zone 2 : nom « Zone B retouchée »
visible dans l'onglet, session synchronisée, terrain effectivement affiché et hash
`390b81339ec4017cb3675c3f885060912f8b8d7cecc42dde04a1b105420503ba` identique
au terrain avant arrêt. Capture `restarted-retouch.jpg` dans
`E:/OpenRPG_Forge/.gate2-evidence/workspace-tabs-fixed-ui-20260914`.
Après fermeture, archive toujours
`AB913BF9176997E7E7DC7B6740A1CFB5242D53A208628DA3A2F781B9F9C13579`.
Racine 640 et descendants 30360/10072/33816/18684/16288/18252/20812 absents.
Computer Use utilisé ; aucun build, benchmark ou test Cargo supplémentaire.
Le parcours renommage B→A→B→Save→arrêt→Open est désormais acquis. Cette preuve
ne qualifie pas les changements de géométrie multizone, récupération interrompue,
limite d'onglets ou les autres critères d'édition de Gate 2.

## Preuve des onglets corrigés et retouche conservée

Candidat `D75CB60EF59B7BDF4DE0C34E84C38DFBB7333E919B6E9757C73E576277B99032`
construit dans `.gate2-evidence/workspace-tabs-fixed-studio-20260914` (Trunk
58,42 s puis Tauri PASS). Copie vérifiée du workspace précédent dans
`.gate2-evidence/workspace-tabs-fixed-ui-20260914`, tous ces chemins sous E:/OpenRPG_Forge.
Onglets A/B réellement visibles, actif contrasté. B renommée « Zone B retouchée »
via Preview puis bouton Apply. B→A→B conserve nom, astérisque dirty et terrain
hash `390b81339ec4017cb3675c3f885060912f8b8d7cecc42dde04a1b105420503ba`.
Pendant les bascules, fichier workspace toujours 09B118AA : aucune Save implicite.
Save explicite retire l'astérisque ; archive finale
`AB913BF9176997E7E7DC7B6740A1CFB5242D53A208628DA3A2F781B9F9C13579`.
Référence A inchangée ; B pointe sur objet
`9678523221504a59a7decfe8bd584379a4637b33e245a29b03c00b4c3dd040dc`.
Captures `dirty-neighbour.jpg` et `saved-tabs.jpg` conservées dans la copie.

Aucun faux refus de révision dans les observations de bascule (échantillons,
pas trace exhaustive). Ctrl+Entrée tenté n'a pas appliqué la proposition ; usage
du bouton réussi, raccourci non qualifié ici. Reprise après arrêt de cette nouvelle
retouche, géométrie multizone et limite 16 onglets encore dues : R2-005 ouvert.
Skill Computer Use utilisé. Candidat fermé et huit PID suivis absents :
14256/34584/29164/3560/13836/32880/32348/34704. Aucune publication.

## Correction du changement de projection

Le diagnostic transitoire de révision vu sur A→B venait de l'ordre Studio :
observe_result envoyait B à la file de retouches de A avant location.reload.
Les changements de session acceptés passent désormais par une branche dédiée :
invalidation de l'ancienne projection, état natif retenu, rechargement ; le montage
suivant fait Inspect et construit la nouvelle projection. Aucune insertion du
document B dans la file d'updates de A. Refus host : aucune invalidation ; échec
de reload : erreur visible et ancienne projection explicitement invalidée.
Les retouches ordinaires conservent observe_result et les validations moteur.

Clippy Studio Web strict PASS (6,74 s) après extraction pour respecter la limite
de taille de fonction, sans suppression de lint. fmt/diff check PASS. Aucun
nouveau build/capture : disparition réelle du diagnostic encore à vérifier avec
le correctif de visibilité des onglets. Aucun changement du format ou service.

## Parcours multizone réel et correctif de visibilité

Candidat 7A7812D0 lancé sur copie vérifiée `workspace-tabs-ui-20260914` (racine
locale `.gate2-evidence`). Création Studio de Zone B, clé zone-2, dans le module
existant par Examiner/Create, puis Save. AreaId `5078218e-f3f2-5bc9-b174-f7e5e767876d`.
Archive workspace SHA256 `09B118AAEA1D7327087B9096CD29FEF6E535D586F2B25DD94C2EC20EE8D12D35`.
Open A puis Open B : terrain B affiché, hash
`ff71d406a891ae9dd5e4e58ee133d80613a4eac2705f6bef4a115e2286da0952`.
Fermer B revient effectivement à A, terrain visible et hash initial 7b206cb5.
Capture `returned-neighbour.jpg` conservée dans la copie ; archive inchangée
après navigation/fermeture. Aucun test de retouche non sauvegardée GUI revendiqué.

Défaut observé : onglets présents dans l'accessibilité mais absents de leur
emplacement visuel. Cause CSS : `.scene-navigation nav` positionne absolument
tous les nav descendants comme menu de scènes. Correctif isolé ajouté : classe
managed-zone-tabs dans le flux, boutons repliables et actif contrasté. Clippy
Studio Web strict PASS (7,40 s), avertissement futur tiers proc-macro-error2 connu.
Correctif non encore capturé : R2-005 reste ouvert, prochain candidat requis.
Un diagnostic de révision transitoire apparaît lors de la bascule, puis disparaît
au rechargement normal ; aucune corruption persistante observée, à améliorer.

Skill Computer Use a permis ce constat et le parcours réel. Candidat fermé ; PID
4752 et descendants 32008/11872/32096/32312/6892/5248/34324 tous absents ensuite.
Source précédente intacte, aucune session utilisateur fermée, aucun push.

## Candidat intégré de fermeture multizone

Build Tauri natif PASS (1 min 13 s), candidat SHA256
`7A7812D06DF07D02A2066B3022B678FB1F2B765A64375D621330E4483D626C8B`
dans `E:/OpenRPG_Forge/.gate2-evidence/workspace-tabs-studio-20260914`.
Frontend history-web réutilisé : ses quatre hashes vérifiés contre le manifeste
A74AEDAE avant le build, puis fichiers source/copied et kit vérifiés par le script.
Pas de recompilation WASM : modifications limitées au host natif et tests.
Le candidat incorpore fermeture vers voisin et refus contextualisé sur corruption.
Statut BUILD_ONLY : aucun lancement ou nouveau verdict visuel déduit du build.
Les six processus de compilation suivis sont terminés. Prochain parcours : deux
zones, retouche A, A→B→A, Save, fermeture vers voisin, puis reprise exacte.

## Dernier correctif : fermeture et onglet voisin

Fermer une zone gérée sauvegardée sélectionne désormais la voisine (droite,
puis gauche), au lieu de laisser une projection vide avec des onglets ouverts.
La lecture est effectuée avant retrait de l'onglet : une erreur conserve l'état
actif. Dernier onglet : fermeture habituelle ; zone active modifiée : refus
inchangé. Aucune sauvegarde implicite, format ou nouvelle commande.

Deux tests workspace_tabs PASS (compilation 28,91 s ; exécution 0,06 s) :
bascule A/B, fermeture vers B après Save et réouverture exacte ; fermeture de B
vers A modifiée conservant tête/dirty et fichier sauvegardé inchangé. Clippy
project all-targets strict PASS (5,63 s), fmt et diff check PASS. Le refus sur
objet voisin corrompu est prévu par l'ordre du code mais pas injecté dans ce test.
Complément : erreur de lecture contextualisée avec AreaId voisin et conservation
de l'onglet courant. Test d'injection dans une fixture synthétique ajouté : objet
corrompu après ouverture A/B, Close refusé, tête et deux onglets inchangés, archive
workspace inchangée, objet restauré puis Close réussi vers A. Trois tests ciblés
PASS (compilation 28,97 s, exécution 0,04 s) et Clippy strict PASS (5,75 s).
Le refus sur corruption n'est donc plus seulement une propriété lue dans le code.
Inspection du raccord Studio : la réponse acceptée passe par observe_result puis
rechargement de projection ; une réponse refusée conserve l'état courant et affiche
la raison. Ce constat de code ne vaut pas capture d'exécution.
Pas de nouveau candidat graphique : ergonomie réelle de cette fermeture encore
à qualifier, R2-005 ouvert. Aucun processus graphique lancé ni publication.

## Dernière preuve : reprise Studio et historique — 14 septembre 2026

Candidat SHA256 `A74AEDAE6B2F413359A890AE9ADDF17406819DC21809345B80059A86A01A3BC9`,
build Trunk (2 min 54 s) puis Tauri PASS, dans
`E:/OpenRPG_Forge/.gate2-evidence/workspace-history-studio-20260914`.
Copie exacte des cinq fichiers du workspace précédent vers
`E:/OpenRPG_Forge/.gate2-evidence/workspace-history-ui-20260914`, source préservée.

Parcours réel : Ouvrir workspace, lire l'historique, puis ouvrir la zone gérée.
Trois opérations visibles dans l'ordre récent : ajout zone (révision 4), création
module (3), création projet (2). Pagination désactivée pour cette page unique.
Terrain plat réellement affiché après reprise, hash moteur
`7b206cb55323b16463a8607dcf0034c513f142166985bb1ea07f7aeab756d478`.
Captures locales : `history-reopened.png` et `zone-reopened.jpg` dans cette copie.
La barre reste dans le flux au défilement ; création/adoption sont repliées.
L'application adjacente aux formulaires n'a pas été exercée dans ce replay.

Après fermeture : hashes workspace `8C290C3FC56480799E0532CFC19716A8431600555AE45CB6E944AA63D0C1A60C`
et objet `9D261155F3BF50230B5F2B823DAC763B9935F1EC2FFA0934FA10E2326FC0E1E6`
identiques dans source et copie. Fenêtre fermée ; racine 27084 et sept descendants
17104/10380/13420/33536/28352/34284/31772 tous absents au contrôle final.
Skill Computer Use employé pour ce parcours réel. Aucun processus utilisateur arrêté.
Preuve limitée à cette reprise simple : multizone, récupération et qualification
globale R2-037/R2-040 restent ouvertes. Aucun push ni nouvelle campagne globale.

## Lot précédent : historique du workspace

ReadHistory ajouté au protocole/service et au panneau Studio : vingt opérations
acceptées par page, plus récentes d'abord, commande/révision/action et contexte
projet/module/ordinal de zone issus du reçu. Ni snapshots avant/après ni archives
de zones transportés. Hash de tête exigé, offsets périmés/hors plage refusés.
Lecture sans mutation ni Save, autorisée même avec onglet géré/playtest ; ancien
affichage retiré à la requête suivante. Pas de changement de format canonique.

Trois tests ciblés PASS (compilation 29,49 s) : pagination 23 opérations, ordre,
payload borné, relecture identique, Undo/Redo, refus ancien hash et offset extrême,
archive inchangée, host après reprise et lecture avec zone gérée ouverte/contextes.
Clippy project all-targets strict PASS (0,96 s) après extraction de helper de test
et emprunt du paramètre de récupération. Parcours GUI non qualifié ; aucun nouveau
build candidat ni processus graphique. Clippy Studio Web strict PASS (32,82 s),
warning futur tiers proc-macro-error2 inchangé. R2-037 et Gate 2 restent ouverts.

## Lot précédent : parcours Studio intégré et correction ergonomique

Candidat `4FB0B36718D13225F90A23822E0A9C020841C79129FC76E857648AF15A371F56`
construit dans `E:/OpenRPG_Forge/.gate2-evidence/workspace-authoring-studio-20260914`
(Trunk 3 min 05 s puis Tauri, build-proof.json). Kit déjà qualifié inchangé.
Workspace d'essai neuf `E:/OpenRPG_Forge/.gate2-evidence/workspace-authoring-ui-20260914`.

Geste réel acquis : Create workspace, Examiner/Apply « Mon projet », sélection
du projet, Examiner/Apply « Nouveau module », choix du module, Examiner/Create
zone par défaut, Save workspace, OpenManaged et terrain visible. La saisie UIA
de l'amplitude a échoué : recette restée plate, aucune qualification du champ
24 m revendiquée. Projet/module/zone réellement sauvegardés, archive workspace
SHA256 `8C290C3FC56480799E0532CFC19716A8431600555AE45CB6E944AA63D0C1A60C`.
Zone `b975be3b-e136-5d68-9ebb-213af8cdde54`, objet 5121 octets
`9d261155f3bf50230b5f2b823dac763b9935f1ec2ffa0934fa10e2326fc0e1e6` ;
absence d'authoring.json historique vérifiée. Capture locale `created-zone.png`
dans le workspace d'essai. Pas de fermeture/réouverture du workspace dans ce replay,
ni de parcours multizone/récupération graphique ; ces critères restent ouverts.

Défauts observés : barre fixe recouvrant les contrôles amenés dans la vue,
application projet/module éloignée des formulaires. Correctif : barre dans le flux,
création/adoption repliables, résumé et Apply adjacents à la section concernée.
Clippy Web strict PASS (7,36 s), pas encore de capture du correctif ; le candidat
ci-dessus ne l'inclut pas. Skill Computer Use utilisé pour découvrir ces défauts.
Candidat fermé par son bouton de fermeture : fenêtre et processus racine 23128
absents, aucun WebView portant le chemin de ce candidat restant. Aucune session
utilisateur fermée, aucune publication. Gate 2 reste ouverte.

## Lot précédent : nouvelle zone directement dans le module

ReviewNewZone/CreateNewZone et formulaire Studio ajoutés. Préparation réutilisant
les bornes de création terrain (512 m/pas 2 m, seed/altitude/amplitude/échelle),
ProjectId existant conservé, AreaId stable par module/clé. L'examen ne réserve ni
répertoire ni objet. Apply compare les hashes exacts et publie l'objet immuable,
puis attache la zone ; Save reste explicite. Aucun authoring.json historique
intermédiaire. Un répertoire de verrou réserve l'identité avant Save pour empêcher
la création concurrente par la voie historique. Abandon/interruption peut laisser
clé réservée et objet orphelin ; aucune suppression implicite.

Test `workspace_creation` PASS (0,02 s, compilation 28,70 s) : examen sans écriture,
recette changée refusée, retry, absence d'archive historique, création concurrente
refusée, ouverture gérée, fermeture sale refusée, Save/reprise, Undo/Redo.
Clippy project strict PASS (5,59 s) après extraction du garde host et de la fixture.
Clippy Studio Web strict PASS (6,83 s), après renommage du signal terrain_seed ;
fmt check PASS. Warning futur tiers proc-macro-error2 inchangé.
Formulaire raccordé, qualification graphique non acquise ; pas de nouveau candidat.

## Lot précédent : récupération explicite du workspace

ReviewRecovery/Recover raccordés au service, host et panneaux Studio. L'examen
rejoue les deux archives et vérifie leurs objets ; une candidate invalide ou avec
objets absents ne peut pas être promue. Choix lié aux deux hashes exacts, sessions
fermées et verrou workspace acquis. La promotion prend aussi les verrous des
sources historiques encore présentes. DocumentStore archive les deux versions
avant promotion ou abandon du staging ; ouverture ultérieure explicite.

Test `workspace_recovery` PASS : ouverture bloquée sur pending, candidate invalide
refusée, abandon avec archives conservées, ancienne décision refusée, objet absent
refusé puis publié, source ouverte refusée, promotion et réouverture exactes,
source inchangée. 1 test en 0,04 s, compilation 29,27 s. Clippy project all-targets
strict PASS après extraction du helper de fixture (0,33 s).
Clippy Studio Web strict PASS (6,77 s) après correction des imports explicites.
Warning futur tiers proc-macro-error2 inchangé ; aucun warning Forge ignoré.
Qualification graphique non réalisée ; aucun nouveau candidat ni processus GUI.
La conservation d'une sauvegarde absente/corrompue ne la répare pas : avertissement
explicite dans le panneau. Pas de récupération automatique des archives de secours.

## Lot précédent : budget de lecture réel

La vérification des objets compte désormais les octets réellement lus, au lieu
de réserver 4 Mio pour chaque archive unique. Le plafond cumulé reste 256 Mio,
avec 4 Mio maximum par objet. Contrôle de taille avant lecture puis lecture
bornée au budget restant + un octet sentinelle avant validation/rejeu.
Cela retire la limite artificielle de 64 petites archives ; aucune source ni
archive existante n'est modifiée. Ce budget porte sur les octets lus, pas sur
une mesure de consommation mémoire du rejeu.

Test ciblé `object_read_charges_actual_bytes_and_rejects_insufficient_budget` :
PASS (1 test, 0,01 s ; compilation 42,31 s). Budget nul et insuffisant refusés,
budget exact accepté. Pas de campagne graphique ni de nouveau candidat.
Clippy project all-targets strict PASS (5,42 s), après déplacement du module
de test en fin de fichier demandé par la règle de style.
La qualification intégrée reste en attente ; Gate 2 reste ouverte.

## Lot précédent : compaction gérée

ReviewZoneCompaction/CompactZoneArchive utilisent maintenant l'adapter workspace
pour une zone gérée. Le plan conserve les références d'archive exactes et la
révision workspace ; la conversion réutilise le codec delta et sa vérification
tête/reçus. Apply publie un nouvel objet puis change la référence ; Save explicite.
Ancien objet/source conservés. Changer d'onglet ferme la revue de compaction.
La notification Studio inclut la compaction pour actualiser les indicateurs.

Parcours workspace_adoption étendu : revue cible falsifiée refusée, tête complète
et page d'historique identiques après compaction, retry accepté, archive compacte
retrouvée après sauvegarde/réouverture, puis nouvelle édition de zone. PASS final
(0,10 s, compilation 22,80 s). Clippy project strict PASS (0,35 s après tests modifiés).
Clippy Studio Web strict PASS (31,83 s ; warning tiers proc-macro-error2 inchangé).
Pas de build candidat ni de qualification graphique sur ce lot.

## Dernier lot : cohérence des indicateurs et candidat intégré

Le pilotage Windows a pu lire/capturer/fermer l'ancien candidat de préflight.
Frontend intégré compilé (1 min 28 s), puis candidat natif construit dans
E:/OpenRPG_Forge/.gate2-evidence/workspace-integrated-studio-20260914.
SHA-256 EXE : 8A39ED7BF8D9BCD98CBCFBAACC3948880549E19B87EF5B7BF02101FBCEDF568E.
Au lancement du nouveau candidat, Computer Use a signalé un arrêt utilisateur
par Échap. Aucun geste ni capture acquis pour ce nouveau candidat. PID 628 et
sept descendants fermés, absence vérifiée. Données d'essai conservées ; aucun push.

Après cet arrêt, travail limité au code : relire le panneau auteur après Save
workspace, et le panneau workspace après Save/ApplyZone/OpenManaged/Close auteur.
Un compteur de changement, consommé après la fin des requêtes, évite de perdre
une actualisation pendant busy. Inspect ne redéclenche pas de boucle ; aucune
mutation ou sauvegarde automatique. Les contrôles workspace sont désactivés
pendant une requête auteur. Le garde hash natif reste l'autorité.

Clippy Web strict PASS (6,16 s), après extraction des helpers d'observation et de
notification. Pas de nouveau build de candidat ni de contrôle graphique ; le
candidat ci-dessus est donc antérieur à ce correctif. Qualification UI encore due.
Le warning tiers de compatibilité future proc-macro-error2 reste signalé.

## Dernier lot : onglets gérés R2-005

Host : jusqu'à 16 zones gérées ouvertes, une projection active. OpenManaged peut
remplacer cette projection sans fermer/perdre la précédente : les modifications
acceptées restent dans les références du workspace. Dirty compare la référence
de chaque zone au dernier checkpoint sauvé. Close refuse une zone dirty, Save
sauve le workspace ; mutations structurelles refusées tant qu'un onglet subsiste.
Studio : bandeau noms/*, état actif et boutons de sélection, désactivés en playtest.
Pas de restauration des onglets après redémarrage dans ce lot.

Tests workspace_tabs et workspace_adoption : 2 PASS (0,05 s chacun, compilation
21,26 s). Preuve multi-zone : retouche A, switch B (propre), retour A même hash et
dirty, Close refusé, Save nettoie les deux indicateurs, fermeture/reprise hash A
identique. Clippy project natif PASS (4,76 s), Studio Web PASS (29,00 s) ; warning
de compatibilité future proc-macro-error2 tiers inchangé. Aucun rendu 3D lancé.

Limites : bandeau et transitions visuelles non observés. Qualification à faire
sur un candidat intégré ; la compilation seule ne clôture pas R2-005/Gate 2.

## Dernier lot : adoption depuis Studio

Formulaire AdoptionControls : catalogue des sources sauvegardées sans pending,
choix zone/module, examen explicite et Apply routé vers Adopt. Source historique
conservée annoncée dans le formulaire. ReviewAdoption du host exige une session
fermée, lit le hash exact sous verrou, rejoue l'archive, contrôle l'identité et
le projet, puis produit une WorkspaceRequest liée à la tête actuelle. Pas de
publication d'objet/tête pendant cette revue ; Apply reprend tous les contrôles.

Test workspace_adoption étendu à la revue : état inchangé et hash périmé refusé,
puis parcours existant adoption/édition/playtest/reprise. PASS (0,07 s, compilation
21,22 s). Clippy project/Studio natif strict PASS (2,09 s) et Web strict PASS
(5,96 s), après extraction d'un helper de test et imports explicites. Avertissement
Cargo de compatibilité future proc-macro-error2 tiers toujours présent.

Aucune observation graphique acquise, aucune nouvelle campagne de tests globale.
Le parcours visible complet doit encore être qualifié ; onglets multi-zones et
compaction gérée restent ouverts. Ni Gate 2 ni R2-001/002 déclarés terminés.

## Dernier lot : host de zone gérée et ouverture Studio

OpenManaged raccorde la zone du workspace à ActiveSession sous forme de projection
en lecture seule. Preview/Apply utilisent le service workspace, puis relisent
l'archive acceptée. Save passe par la tête workspace. Historique, can_undo/redo
et playtest reprennent les mêmes contrats de zone ; pas de second éditeur.
Le workspace ne peut être fermé/modifié structurellement tant que sa zone est
ouverte. Sauvegarde interdite en playtest, fermeture dirty refusée.

Boutons de zones par module ajoutés à WorkspaceControls et callback vers
OpenManaged ; remplacement du viewport classé comme pour Open. Clippy Web PASS
(28,69 s ; avertissement tiers proc-macro-error2 de compatibilité future).
Aucun candidat visuel lancé ; rendu, gestes et adoption UI non qualifiés.

Parcours workspace_adoption étendu : ouvrir la zone gérée depuis le host, renommer
via Preview/Apply, reçu identique, dirty/fermeture protégés, playtest hash exact,
Save/Close/Open même hash, puis Undo direct comme le raccourci Studio. PASS final
(0,08 s, compilation 21,21 s). Clippy project strict PASS après adaptation Undo/Redo
(4,66 s). Contrôle natif project/Studio PASS plus tôt dans le lot (6,72 s).
La source historique reste vérifiée inchangée. Répertoire synthétique E: supprimé.

Limites : compaction gérée refusée explicitement pour l'instant ; pas encore
d'adoption accessible dans les formulaires, de vrais onglets multi-zones ni de
qualification graphique. Gate 2 reste ouverte ; aucune publication Git.

## Dernier lot : formulaires Studio

WorkspaceControls ajouté au panneau auteur : Create/Open/Inspect, formulaires
projet/module (clés stables indépendantes des noms), choix du projet destinataire,
Examiner/Apply, Undo/Redo examinés, Save/Close, liste et indicateur dirty.
Inspect relit la session native au montage ; pas de nouvelle autorité côté UI.
Saisie/change efface le preview ; fieldset désactivé pendant la requête ; la garde
hash/révision reste native. Résumé de proposition en français, sans JSON technique.

Clippy Studio Web wasm32-unknown-unknown, no-default-features/features web, offline
locked, warnings projet en erreurs : PASS (5,76 s), après correction de syntaxe
d'un callback Leptos. Avertissement de compatibilité future proc-macro-error2 2.0.1
signalé par Cargo (tiers), aucun lint projet désactivé. Fmt/diff contrôlés.

Non acquis : aucun nouveau candidat compilé/lancé, aucun rendu ni geste réel
inspecté sur ces formulaires. R2-001/002 non cochés. Adoption et édition gérée
dans le viewport restent à raccorder ; le protocole de ce panneau n'expose pas
ces flux encore incomplets. Les paragraphes suivants décrivent les lots antérieurs.

## Dernier lot : protocole host et Tauri

WorkspaceHostRequest/State moteur-neutres ajoutés : Create/Open/Inspect/Preview/
Apply/Adopt/Save/Close. Le même AuthoringHost possède la session workspace ; Tauri
réutilise son unique OnceLock/Mutex pour les deux familles de commandes. Aucun
second host divergent. Pas de fermeture sale ni de mutation workspace en playtest.
L'adoption exige la fermeture de la session de zone active.

Test workspace_host : PASS (0,01 s, compilation 21,08 s), création, round-trip du
protocole, aperçu sans mutation, reçu identique Apply, refus Close sale/Open déjà
ouvert, Save/Close/Open et tête identique. Clippy project/Studio natif all-targets
strict PASS (1,70 s), après ajout du nouveau cas de réponse au validateur de sélection.
Pas de formulaire visible, pas de test WebView, pas de nouvelle qualification Web.
Le protocole d'édition gérée et les assistants visuels restent le prochain raccord.

## Dernier lot : édition de zone gérée

Le service propose read_zone, preview_zone_edit et apply_zone_edit. Il réutilise
ZoneEditSession et son archive, puis porte le nouveau hash via UpdateZone.
L'aperçu ne publie rien ; Apply vérifie le résultat exact du couple commande de
zone/requête workspace. L'objet est publié avant le changement en mémoire ; Save
reste explicite. Execute générique refuse UpdateZone comme AttachZone.

Parcours adoption étendu : départ de joueur ajouté à une zone adoptée, double
aperçu sans mutation, Apply/retry identiques, nouveau contenu lisible avant Save,
Save/reprise avec départ, Undo de l'édition puis de l'adoption ; source historique
toujours identique. Test ciblé PASS, 0,04 s, compilation 21,41 s. Clippy project
all-targets strict PASS après extraction d'un helper de test et retrait d'un mut
inutile. Aucun contrôle désactivé, aucune nouvelle dépendance ni exécution graphique.

Limite : service natif seulement ; raccord host/Studio aux gestes, onglets et état
non sauvegardé encore à réaliser. Le parcours ne qualifie pas toutes les commandes
de zone ni le playtest. Gate 2 reste ouverte, CI NOT RUN, aucune publication.

## Dernier lot : adoption et autorité unique

AttachZone passe par un aperçu avec hash/révision, puis par `FileWorkspaceSession::adopt`,
jamais par execute générique. Le service exige le verrou de l'archive historique,
ses octets sauvegardés exacts et l'absence de récupération pendante. Projet, zone
et tête sont validés avant publication de l'objet. Verrou conservé jusqu'à Save.
Open/Save vérifient les références courantes et historiques dédupliquées (budget
conservateur 256 Mio, réservation 4 Mio par objet). Le host contrôle l'autorité
après verrouillage pour Open/Recover, ainsi qu'avant nouvelle création.
Une appartenance annulée reste protégée par l'audit, sans migration inverse implicite.

Six tests des suites workspace/adoption/objects PASS sur le premier raccord.
Après ajout des gardes Create et durcissement des erreurs de lecture du pending,
test adoption rejoué : 1 PASS, 0 échec, 0,02 s (compilation 20,86 s), avec scénario
supplémentaire : abandon après publication objet mais avant tête, ancien Open encore
possible, tête inchangée et retry d'adoption ensuite réussi. Ce drop contrôlé ne
prouve pas une coupure électrique. Clippy project all-targets strict PASS, 4,75 s.

Autres assertions du parcours : source ouverte refusée, hash périmé refusé,
execute direct interdit, retry identique, source verrouillée avant Save, blocage
Open historique après Save et après Undo, réouverture exacte et source inchangée.
Répertoire synthétique E: supprimé ; aucun fichier utilisateur ni candidat graphique.
Pas encore d'assistant Studio ni d'édition des zones gérées ; aucune validation Gate 2.

Les sections suivantes conservent la chronologie des premiers lots.

## Suite : objets immuables de zone

Adapter `workspace/objects` implémenté, pas encore raccordé aux commandes
d'adoption. Revue des octets avec SHA-256 puis rejeu du codec de zone existant,
validation ProjectId/AreaId/hash de tête, lecture maximale 4 Mio. Écriture dans
un fichier temporaire create_new, sync_all puis publication par hard link sans
remplacer un objet existant. Seul le staging créé par l'appel est supprimé.
Une interruption peut laisser du staging ou un objet orphelin ; aucun GC implicite.
Les archives sources et la tête du workspace ne sont jamais écrites par cet adapter.

`cargo test -p forge_project --test workspace_objects --offline --locked -j 2` :
2 PASS, 0 échec ; compilation 20,63 s, tests 0,01 s. Vérifications : publication
et retry identiques, relecture exacte, source intacte, référence absente, mauvais
projet/hash de tête, corruption non écrasée, octets et format invalides, taille
excessive. Hard links réellement exercés sur E:. Répertoire synthétique supprimé.
Clippy core/project all-targets strict PASS (4,57 s) après correction d'un commentaire.
Pas de simulation de coupure électrique, de concurrence hostile ou de preuve Studio.

Le host doit encore interdire la double édition avant d'activer l'adoption.
Le stockage de l'objet seul ne transfère pas l'autorité d'une zone.

14 septembre 2026 — branche Rust, arbre de développement modifié, sans publication.
ADR-0016 autorisé explicitement par le propriétaire. Gate 2 ouverte.

## Implémenté

- ModuleId distinct des ProjectId et AreaId ; noms Unicode indépendants des IDs.
- Tête versionnée, unicité des IDs/rattachements, noms et tailles bornés.
- Créations projet/module, aperçu non mutateur, Apply, audit, Undo/Redo,
  garde hash/révision, retry idempotent et refus de réutilisation d'un command_id.
- Archive versionnée avec rejeu des commandes et comparaison exacte de la tête.
- Sauvegarde dans workspace-v1 via DocumentStore : verrou, hash attendu et
  remplacement atomique existants. Les archives historiques ne sont pas touchées.
- Adapter provisoire sans zones : aucune référence d'archive n'est acceptée sans
  futur contrôle des objets immuables. Pas de migration implicite.

## Réellement vérifié

`cargo test -p forge_project --test workspace --offline --locked -j 2` :
3 PASS, 0 échec (compilation finale 21,76 s ; exécution 0,01 s).
Création Unicode, aperçu, retry, plan périmé, Undo/Redo, round-trip, reprise avec
historique, tête falsifiée, doublons et appartenance multiple, version inconnue,
entrée trop grande ; cycle filesystem Save/Close/Open/Undo/Save/Open, exclusion
du second éditeur et octets historiques inchangés. Répertoire synthétique E:
supprimé après essai ; aucun fichier utilisateur supprimé.

Clippy core/project all-targets avec warnings en erreurs : PASS, 5,22 s.
Premier passage domaine seul : 2 PASS avant l'ajout de l'adapter filesystem.
Pas de nouveau test graphique ni de processus graphique lancé.

## À poursuivre, non acquis

Stockage des archives de zones immuables, adoption et interdiction du double
éditeur historique, recovery workspace explicite, raccord des assistants Studio
et onglets, parcours humain et qualification workspace regroupée ADR-0006.
Les contrôles existants DocumentStore sont réutilisés, pas une nouvelle preuve
d'injection de crash ou d'interruption physique sur ce lot. La seule validation
du schéma ne prouve jamais l'existence d'une archive de zone. CI NOT RUN.
