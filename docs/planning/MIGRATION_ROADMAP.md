# Roadmap de migration — OpenRPG Forge Rust/WebGPU

## État actif — 1er octobre 2026

**Gate 2 clôturée par validation propriétaire ; Gate 3 active sur Tidewater.**
Le propriétaire considère validés les points restants de Gate 2 et autorise
l'exécution jusqu'à clôture de Gate 3 sur cette carte :
[ADR-0018](../ADR/ADR-0018-gate2-owner-validation-gate3-tidewater.md).
Cette décision de poursuite ne modifie pas les verdicts machine historiques.
Les récits datés ci-dessous gardent leur portée ; aucune publication implicite.

## Chantier Tidewater — autorisation du 24 septembre 2026

L'intégration complète de Tidewater avec carte et effets visuels fidèles est
autorisée par le propriétaire. [Plan et critères TW-001 à TW-015](TIDEWATER_INTEGRATION.md),
[ADR-0017](../ADR/ADR-0017-tidewater-fidelity-integration.md).
Cette priorité lève le report maritime pour cette portée ; elle ne clôt aucune
gate et n'autorise aucune publication. Les états de gates ci-dessous restent
distincts du suivi de ce chantier. Les anciennes formulations « différé »
conservent leur valeur historique pour les autres extensions maritimes.

## État de référence — 16 septembre 2026

Gate C qualifiée, Gate 0 passée et Gate 1 clôturée localement sous dérogations.
**Gate 2 active et ouverte ; Gate 3 et suivantes non ouvertes.**
R2-020 « promotion et retouche » est terminé localement :
voir [bilan courant et réserves](../evidence/gate2/R2_GENERATED_PROMOTION.md).
CI distante : `NOT RUN — infrastructure billing`. Aucun nouveau verdict de
performance, de distribution ou de publication.

Lecture : le [contexte canonique](../../PROJECT_CONTEXT.md) résume l'état actuel ;
les cases des gates et les preuves identifiées portent la qualification de chaque
critère. Les récits successifs conservent les limites de leurs anciens candidats,
mais ne sont pas une liste de travaux à répéter. Un résultat récent remplace un
ancien résultat seulement pour le même périmètre explicitement couvert.
L'historique narratif du contexte a été déplacé sans perte dans
[l'archive du 16 septembre](../../PROJECT_HISTORY_20260916.md).

## Référence de clôture — Gate 1, 12 septembre 2026

**PASS automatisé local sous ADR-0006, avec les dérogations acceptées.**
Dérogations saccades pendant les changements de qualité et kit : ADR-0013 ;
comparaison Godot/Cadle validée explicitement. 738 tests PASS, 2 ignorés
préexistants ; builds et replays natif/Studio PASS. Révision propre qualifiée
`7386e00a1d9e8531470b57bcd8da190636a9e390`. [Rapport de clôture](../evidence/gate1/GATE1_CLOSURE_20260912.md).
CI distante NOT RUN ; aucune publication ni qualification de distribution.
Le travail Gate 2 est suivi séparément et ne fait pas partie de ce snapshot.
Les états ouverts et attentes antérieurs ci-dessous sont historiques ;
ils ne doivent pas rouvrir les décisions soldées par ce rapport.


### Historique de préparation — avant le verdict de clôture ci-dessus

ADR-0013 consigne l'instruction explicite du propriétaire : saccades pendant les
changements de qualité dérogées ; point kit humain reporté sous dérogation ;
comparaison Godot/Cadle validée. Ces points ne sont plus en attente d'accord.
La qualification finale sur révision propre est en cours ; aucun PASS de test,
droit de distribution ou travail de Gate 2 déduit de ces décisions.
Voir `docs/ADR/ADR-0013-gate1-closure-owner-decisions.md` (depuis la racine).
Les paragraphes antérieurs ci-dessous restent l'historique des acquisitions.

Date de cet état historique : 2026-09-12
Branche : `Rust`  
État global : **Gate C qualifiée sous l’exception bornée ADR-0003 ; Gate 0 passée sur preuves
automatisées locales sous ADR-0004, ADR-0005 et ADR-0006**
Gate 1 : **CLÔTURÉE localement sous dérogations ; Gate 2 suivie séparément**

**Revue de clôture du 12 septembre préparée** : 340 fichiers liés aux preuves
vérifiés, aucune acquisition répétée. Le propriétaire confirme l'absence de
retouche humaine. Revue/action minimale du kit, références comparatives et état
Git exact dans `../evidence/gate1/R1_CLOSURE_REVIEW_20260912.md`. Aucun critère
humain, réserve de stutter, droit de publication ou révision propre inventé.

**R1-040, correction temporelle commune intégrée et vérifiée sur sa portée** : contrôleur CPU
de transition/hystérésis ajouté. Raccord GPU des objets du kit, arbres et groupes
lointains implémenté, 20 tests ciblés PASS ; sondes natives/Studio PASS pour la fin
du fondu caméra arrêtée, captures inspectées et processus fermés. Ne qualifie pas
l'absence de toute saccade ni les revues humaines. Voir `../evidence/gate1/R1_TEMPORAL_LOD.md`. Pas de dérogation implicite,
modification d'asset, nouvelle gate ou campagne graphique répétée.

**Priorité de clôture après retour propriétaire : arrêter les acquisitions
répétitives du candidat inchangé.** La paire vidéo High clair/pluie est acquise
(271 images FHD chacune, entrées identiques, six tests outil PASS), sans revue
temporelle ni qualification temps réel inférées. La liste résiduelle consolidée
figure en tête de `../evidence/gate1/R1_FINAL_DOSSIER_POINT7.md` : temporalité,
kit avec véritable retouche/revues humaines, jugement comparatif, révision propre
et verdict. Ces conditions ne sont pas soldées par la multiplication des preuves
structurelles. Aucun critère ni exigence humaine supprimé, aucune gate sautée.

**R1-044/R1-055, série fixe du candidat actuel acquise** : six vues × clair/pluie
High 1080p, douze PNG inspectés, 284 sources exactes et snapshot caméra seul vérifiés.
Douze arrêts propres, aucun runtime restant. Index `index-twelve-views.json` dans
`r1-final-dossier-overlap-20260911`, 447 fichiers contrôlés ; 15 tests du collecteur PASS.
Voir `../evidence/gate1/R1_FINAL_DOSSIER_POINT7.md`. Acquisition fixe actuelle
disponible, sans qualifier la vidéo appariée, la fluidité, les revues humaines ou la gate.

**R1-055, candidat avec chevauchement consolidé** : choix explicite du collecteur
`preparation-overlap`, 408 fichiers contrôlés, 284 sources identiques, 14 tests PASS.
Cycle mobile contrôlé et rapports de parité quatre profils avec payloads épinglés ;
pas de nouvelle acquisition ni de replay des comparateurs natif/Medium/Ultra.
Index `r1-final-dossier-overlap-20260911/index.json`, statut `partial_not_gate_pass`.
Les médias historiques ne qualifient pas le candidat actuel. Voir
`../evidence/gate1/R1_FINAL_DOSSIER_POINT7.md`. Restent fluidité/LOD temporel,
dossier comparatif final, retouche/revues humaines et révision propre.

**R1-040, publication progressive implémentée** : ancien rendu conservé pendant
la préparation GPU d'un seul remplacement ; 714 tests PASS/deux ignorés,
Clippy natif/Web et build Studio PASS pour le candidat initial. Cycle mobile FAILED :
pics réduits mais retour High trop tard (10,435 s). Ajustement de débit borné
seize meshes/12 Mio : quatre tests ciblés, Clippy/build PASS, mais nouveau cycle
FAILED (High à 9,255 s, pic 103,9 ms). Acquisition et nettoyage terminés ; réduire
la latence séquentielle reste à qualifier. Chevauchement préparation/transfert Web
implémenté ensuite : 717 tests PASS/deux ignorés, Clippy natif/Web et build PASS.
Acquisition Studio du chevauchement : cycle mobile PASS, High prêt à 8,418 s,
pics Low/High 97,8/101,8 ms. Trois reçus vérifiés, capture inspectée, essais fermés.
Régression de délai résolue dans ce protocole ; fluidité globale non acquise.
Qualification native ensuite acquise : cinq étapes et reçus GPU PASS sur les mêmes
284 sources ; Medium/Ultra Studio complétés. Comparaison fonctionnelle des quatre
profils PASS, captures inspectées et processus fermés (R1_043_CURRENT_NATIVE_STUDIO).
Suite : consolidation du candidat ; fluidité, revues humaines et verdict final séparés.
Voir `../evidence/gate1/R1_040_MESH_STAGING.md`.

**R1-055, dossier du dernier diagnostic assemblé** : collecteur exécuté avec
choix `bank-reuse`, 377 fichiers contrôlés, neuf tests PASS. Anciennes captures
et vidéos non relabellisées ; nouveau PNG absent et sortie forcée explicités.
Voir `../evidence/gate1/R1_FINAL_DOSSIER_POINT7.md`. Révision propre, revues
humaines et réserves temporelles non acquises ; aucun passage Gate 2.

**R1-040, reprise berges protégées** : JSON du nouveau Studio acquis, cycle
High/Low/High et comparaison plans/sources PASS. Pics 143,5/264,5 ms contre
147,2/271 ms : pas de gain sensible démontré sur cette paire. PNG non exporté
après déplacement/réduction de fenêtre ; sortie forcée et 14 processus fermés.
Voir `../evidence/gate1/R1_040_BANK_REUSE.md`. Ne ferme ni la fluidité ni la gate ;
pas de nouvelle campagne identique sur cette optimisation marginale.

**Dossier final actualisé** : cycle Studio mobile intégré séparément au nouvel
index, 378 fichiers / 12 captures historiques / deux vidéos revérifiés ; huit
tests du collecteur PASS. Voir `../evidence/gate1/R1_FINAL_DOSSIER_POINT7.md`,
index `r1-final-dossier-motion-20260911/index-reviewed.json`. Pas de nouveau
renderer ni de promotion des anciennes captures au candidat actuel. Gate et
réserves de fluidité, comparaison finale, revue humaine et révision propre ouvertes.

**R1-040, cycle mobile Studio préchauffé acquis** : préparation bornée à huit
étapes / budget souple 4 ms par adapter. 710 tests PASS, Clippy natif/Web et builds
PASS. Trois enregistrements High/Low/High avec reçu GPU et mouvement vérifiés ;
dernier High prêt à 8,4438 s. Voir `../evidence/gate1/R1_040_PREPARATION_BUDGET.md`.
PNG inspecté, huit processus fermés. L'ancien échec de ce cycle est supersédé ;
pics 147,2/271 ms et réserves temporelles générales persistent. Ni R1-040 complet
ni Gate 1 clos. Suite : consolidation du dossier et réserves restantes, sans
répétition identique ou réouverture de l'esthétique acceptée.

**R1-040, génération végétale partagée entre profils Web** : 707 tests PASS,
Clippy natif/Web et builds PASS. Préparation CPU 176–183 ms → 24–25 ms dans la
paire préchauffée ; retour High encore tardif à 9,0811 s, cycle mobile FAILED.
JSON/PNG inspectés, huit processus fermés. Voir
`../evidence/gate1/R1_040_VEGETATION_PLAN_REUSE.md`. Gain local acquis, pas
qualification globale. Suite : préparation par budget borné pour réduire la
latence par chunks, non implémentée ici. Aucune case de gate fermée.

**R1-040, optimisation des bornes acquise** : forêt inclusive 320–321 ms →
24–25 ms dans la paire Studio préchauffée, sources constantes. 706 tests PASS,
Clippy natif/Web et builds PASS, neuf processus fermés. Pics 216,6/268,8 ms
et cycle mobile FAILED subsistent. Voir `../evidence/gate1/R1_040_FOREST_BOUNDS_REUSE.md`.
Prochaine cible : préparation végétale et latence par chunks, pas la carte.

**R1-040, cycle préchauffé réellement acquis** : Ready 5/5 en 67,6749 s,
traversée réussie mais troisième étape mobile absente ; cycle FAILED conservé.
Pics 435,6/562,1 ms, forêt inclusive ~320 ms, préparation végétale ~190 ms.
704 tests PASS, Clippy natif/Web et builds PASS, huit processus fermés.
Voir `../evidence/gate1/R1_040_STUDIO_PREWARMED_MOTION.md`. Optimisation locale
des calculs de bornes forestières identiques proposée, pas encore réalisée.

**R1-040, trace CPU Studio du 11 septembre acquise** : 704 tests PASS / deux
sondes ignorées, Clippy natif/Web et builds PASS. Commit végétal 7,7–12,5 ms,
forêt 333,7–341,1 ms aux transitions ; grandes pauses Low non expliquées par ces
phases. Cycle mobile FAILED conservé, huit processus fermés. Détails et suite
à froid/préchauffé dans `../evidence/gate1/R1_040_STUDIO_PHASE_TRACE.md`.
Aucune fermeture de gate ni optimisation de performance revendiquée.

**R1-040, terrain Web réparti par chunks le 11 septembre** : 700 tests PASS,
Clippy natif/Web PASS, candidat Studio reconstruit et réellement testé.
Ready 5/5 en 41,4071 s, High restauré, sources et reçu GPU vérifiés, PNG inspecté,
huit processus fermés. Voir `../evidence/gate1/R1_TERRAIN_STAGED_PREPARATION.md`.
Préparation fonctionnelle acquise, pas preuve d'absence de saccades ; réserves
temporelles, revues humaines et verdict final restent ouverts. Aucun passage
de gate ou publication. Les entrées suivantes décrivent les étapes antérieures.

**R1-040, smoke Studio incrémental acquis le 10 septembre à 21:49** : préparation
végétation répartie par chunks, cycle 5/5, retour High et acquittement GPU vérifiés,
JSON/PNG conservés et processus fermés. Les tentatives interrompues ne sont plus
le dernier état. Cela ne ferme pas les réserves de fluidité/tramage en mouvement.
Voir `../evidence/gate1/R1_QUALITY_TRANSITION_CPU.md`.

**Point 5 soldé localement le 10 septembre** : R1-039 acquis en natif et Studio,
en complément des capacités de mesure ci-dessous. Préparation explicite des quatre
profils vérifiée, exports clair/pluie et nettoyage acquis ; voir
`../evidence/gate1/R1_PROFILE_PREWARM.md`. R1-040, revues humaines et verdict final
restent ouverts. Les paragraphes suivants conservent la chronologie antérieure.

**Clôture locale des mesures du 10 septembre : R1-034, R1-037 et R1-038 cochés**
sur preuves natives vérifiées + dérogation GPU Studio ADR-0012. Aucun compteur
inventé, aucun verdict global. R1-039 et les réserves temporelles restent ouverts.
Voir `../evidence/gate1/R1_MEASURES_POINT5.md` pour les nouveaux relevés et pins.

**Décision propriétaire du 10 septembre : mesures GPU Studio dérogées uniquement**
([ADR-0012](../ADR/ADR-0012-gate1-studio-gpu-measurements-waiver.md)). Sous-parties
WebGPU R1-034/037/038 : `WAIVED / ACCEPTED`, non PASS mesuré ; elles ne bloquent
plus à elles seules. Mesures natives/CPU, R1-039, temporalité et revues humaines
restent exigées. Les paragraphes historiques suivants précèdent cette décision.

Complément Studio : overlay CPU observé, GPU indisponible ; limite timestamps
WebGPU du diagnostic Bevy identifiée. Vérificateur du dossier étendu et exécuté
(89 fichiers, 12 vues, 2 vidéos ; trois tests PASS), sans clôture implicite des
mesures ni des revues humaines. Instance de preuve terminée. Voir le rapport de
clôture du 10 septembre ci-dessous.

10 septembre, complément : capture/replay GPU du candidat intégré natif exécutés
après correction de l'outil (eau/nuages auparavant omis), comptages et timings de
replay acquis ; couverture Studio et préchauffage complet non acquis. Six GLB
réouverts et six copies Blender round-trip préparées, retouche humaine non effectuée.
Référence Godot historique inspectée, pas comparaison finale appariée. Ciel préparé
sans changement du rendu accepté. Voir `../evidence/gate1/R1_CLOSURE_PROGRESS_20260910.md`.
Aucun nouveau critère clos, aucune extension des exceptions, aucun push.

10 septembre, points 5 à 7 engagés : **PARTIAL**. Six GLB audités (0 erreur,
38 avertissements), douze vues clair/pluie retenues après remplacement de la
paire eau masquée (caméra seulement, géométrie identique), deux vidéos existantes
revérifiées et index de 62 fichiers hashés. Comparaison Cadle descriptive réalisée
sans copie ; Godot NOT RUN. Compteurs GPU exhaustifs/préchauffage des variantes,
retouche et revue humaines du kit, verdict sur révision propre restent requis.
Ne ferme aucun R1 restant ; aucune extension tacite des dérogations. Rapports :
`../evidence/gate1/R1_MEASURES_POINT5.md`, `R1_KIT_POINT6.md`,
`R1_FINAL_DOSSIER_POINT7.md`. Processus de test terminés, aucun push.

10 septembre, point 4 natif/Studio : **parité fonctionnelle acquise** sur le
candidat épinglé, nuages et eau intégrés dans les deux environnements. Plans
effectifs des quatre niveaux et retour High identiques, sources/kit identiques,
reçus GPU prêts. Studio vérifié en deux sessions, captures inspectées, essais
fermés. Pas de parité pixel à pixel ni nouvelle qualification temporelle ;
points 5 à 7 et réserves du point 3 inchangés. Rapport :
`../evidence/gate1/R1_NATIVE_STUDIO_POINT4.md`. Aucun passage de gate.

10 septembre, point 3 : huit parcours natifs clair/pluie Low/Medium/High/Ultra
enregistrés (271 images FHD chacun), traces de franchissement LOD vérifiées,
parcours réel High/pluie ~149 m terminé sans erreur ; essais fermés.
Complément : fondu BLEND corrigé/capturé, vent lointain isolé avec physique prête,
cycles GPU en mouvement natif et Studio prouvés. Correctif final : parcours Studio
complet PASS après attente des pipelines avant départ (délais inchangés), normales
des dérivés corrigées, continuité des ombres LOD0/LOD1 observée en natif et Studio.
Quatre parcours natifs des profils affectés réussis ; captures inspectées et essais fermés.
**Qualification temporelle complète réservée** : tramage résiduel des troncs,
pas de preuve d'imperceptibilité ou d'absence de hitch réel.
Voir `../evidence/gate1/R1_MOTION_POINT3.md` ; ne pas rejouer la même
matrice sans modification pertinente. R1-014 acquis dans la portée ci-dessous ;
R1-040 et Gate 1 restent ouverts.

10 septembre : point 2 performances accepté par dérogation explicite du propriétaire
sur sa RX 7800 XT. R1-041 clôturé administrativement **WAIVED/ACCEPTED**, pas benchmark
PASS ; benchmark cible NOT RUN, mesures historiques sous les seuils conservées.
Cette dérogation ne valide pas Gate 1 ni les blocs 3 à 7 ou la fluidité des transitions.
Voir `../evidence/gate1/R1_PERFORMANCE_POINT2.md` pour la demande et sa portée.

9 septembre, priorité demandée par le propriétaire : terminer d'abord le bloc
profils graphiques (forêt/eau, quatre niveaux, adaptation et choix manuel),
sans nouvelle finition du ciel. **Bloc fonctionnel terminé** : cycles des quatre
niveaux natif/Studio et retour High avec reçus GPU, adaptation montante native,
descendante Studio et priorité manuelle vérifiées ; sources intactes, essais fermés.
684 tests workspace PASS / un timing ignoré, 55 tests Studio finaux PASS et Clippy
strict final PASS. Suivi : `../evidence/gate1/R1_EFFECTIVE_PROFILES_INTEGRATION.md`.
R1-040 garde sa réserve de fluidité des transitions ; les six autres blocs et les
validations humaines restantes ne sont pas fermés par ce verdict fonctionnel.

9 septembre, décision humaine : vue intégrée acceptée en l'état, malgré la
réserve sur le ciel. Attente de cette revue levée ; ne pas redemander la même
validation. Portée et capture dans `R1_055_GATE1_STATUS.md`, acceptation visuelle.
Les critères techniques, les comparaisons et les autres preuves non couvertes
restent ouverts ; aucune dérogation de performance ni passage de Gate 2.

9 septembre : bois proche du conifère simplifié dans un dérivé séparé, puis
comparé en ABBA avec le même release. ~51,8→52,2 FPS forêt : gain trop faible,
candidat non retenu. Kit courant préservé, outils expérimentaux archivés hors
dépôt et retirés du code actif, processus fermés. `R1_FOREST_FOUR_TIERS.md`.
Ne pas répéter cette piste ni fermer un critère sur la seule réduction de triangles.

9 septembre, mesure release actualisée après portée lointaine : build PASS,
trajet vivant Full HD terminé, 72,47 FPS global / 51,72 FPS tiers forestier,
p95 forêt 22,28 ms / 1 % low 39,90 FPS. Seuils non satisfaits ; un seul passage,
sans nuages, pas un gain comparatif ni une qualification du GPU cible. Essai
fermé, preuves dans `R1_RELEASE_PREPARATION.md`. Poursuivre sur le coût forestier,
pas une nouvelle série de micro-réglages d'eau. Aucun critère de gate fermé.

9 septembre : fondu des casters de forêt selon la distance caméra implémenté,
replay natif terminé et processus fermés. Saut 161/162 absent dans les images
inspectées, ombre atténuée ensuite. Candidat à qualifier (coût, profils,
Studio et temporel global), pas de passage de gate. Voir
`R1_FOLIAGE_CLUSTER_ATLAS.md`, fondu caméra ; ne pas répéter les anciens essais.

9 septembre : capture GPU courante acquise et replay Vulkan PASS (1073 draws),
processus fermés. Noms de passes absents du release ; caster du saut non identifié.
Inspecter cibles/shaders dans la capture existante avant une nouvelle variante.
`R1_FOLIAGE_CLUSTER_ATLAS.md`, capture GPU courante ; aucun critère visuel fermé.

9 septembre : variante de fondu incluant les passes opaques essayée puis retirée,
saut 161/162 persistant. Deux essais sans correction visuelle : identifier le
draw/caster GPU avant une autre variante, aucun critère fermé. Preuve dans
`R1_FOLIAGE_CLUSTER_ATLAS.md`, variante opaque ; processus fermés.

9 septembre : essai de fondu d'ombre alpha-testé exécuté puis retiré, saut toujours
visible à 161/162 ; aucun correctif d'ombre retenu. Correction de portée des preuves :
transition proche gouvernée 24–32 m observée, ancien audit 64–72 m insuffisant.
Voir `R1_FOLIAGE_CLUSTER_ATLAS.md`, diagnostic du fondu. Aucun critère fermé.

9 septembre, replay du garde vidéo : départ noir corrigé à l'image, 107 pipelines
prêts avant traversée, processus terminés. Saut d'ombre encore visible plus loin
(161/162) : stabilité non qualifiée. Seuls les LOD0 projettent actuellement des
ombres ; examiner cette coupure plutôt que répéter le warmup. Preuve dans
`R1_FOLIAGE_CLUSTER_ATLAS.md`, replay du correctif. Aucun passage de gate.

9 septembre : revue vidéo supplémentaire révèle départ noir et saut d'ombre.
Garde de démarrage du recorder corrigé, tests ciblés Rust/Python PASS ; rendu
du correctif non vérifié et saut d'ombre non résolu. Voir revue temporelle dans
`R1_FOLIAGE_CLUSTER_ATLAS.md`. Aucun critère fermé par les seuls tests.

9 septembre : paire rapprochée Studio avec/sans reflets écran inspectée, effet
visible et sources/caméra constantes, essais fermés. Preuve bornée R1-023 dans
`R1_FOLIAGE_CLUSTER_ATLAS.md`, pas clôture : temporel, profils complets et revue
humaine restent requis. Ne pas rejouer cette paire figée inchangée.

9 septembre : candidat Studio combinant atlas recadré et reflets transmissifs
construit/lancé, exports inspectés et processus fermés. Base suivante de
qualification : `r1-studio-atlas-reflections-20260909`. Tests de packaging 7 cas
valides / 11 refus PASS. Aucun nouveau critère fermé ; mouvement, transitions
et eau rapprochée restent à vérifier, ne pas répéter le cadrage de départ.

9 septembre : kit atlas recadré chargé dans le Studio, PNG réel inspecté et JSON
exporté, 1662 arbres / 36 colliders, essais fermés. Smoke 1408×966 seulement,
profil High incomplet ; pas de gain Studio, transitions ou validation artistique
déduits. `R1_FOLIAGE_CLUSTER_ATLAS.md`, paquet Studio ; aucun critère fermé.

9 septembre, complément : variante atlas recadrée, ABBA natif ~53 FPS forêt
contre ~44,8 original (+18 %), ~74 FPS trajet, 1662 arbres conservés. Six tests
CPU et validation GLB PASS, capture inspectée, essais fermés. Candidat séparé
retenu pour qualification Studio, pas de critère fermé : forêt sous 60 FPS,
mips/transitions et acceptation artistique non qualifiés, GPU cible non mesuré.
Voir `R1_FOLIAGE_CLUSTER_ATLAS.md` ; le rejet ci-dessous concerne le non-recadré.

9 septembre : premier LOD atlas chargé dans un kit natif séparé puis ABBA release.
Forêt ~44,8 FPS témoin contre ~40,5 candidat, régression répétée : candidat non
retenu malgré ses triangles réduits. 1662 arbres présents, 1285 ticks communs,
eau/ombres constantes, capture inspectée et essais fermés. Conversion PBR corrigée
et testée avant mesure. `R1_FOLIAGE_CLUSTER_ATLAS.md`, verdict runtime ; aucun
kit courant remplacé ni critère promu. Examiner cartes/filtrage, ne pas rejouer inchangé.

9 septembre : deux GLB LOD1 dérivés (feuillage 7462→1626 / 5992→780 triangles),
sources et autres parties inchangées, réimports/captures DCC terminés, quatre
tests CPU et validation glTF PASS. Aspect des cartes et contrôle numérique PBR
encore insuffisants, mips et runtime non qualifiés. `R1_FOLIAGE_CLUSTER_ATLAS.md`.
Aucun kit courant remplacé, gain FPS ou passage de gate déduit ; essais fermés.

9 septembre : atlas complets albedo/alpha et normales chêne/conifère générés,
813/390 vues par carte 2048², deux rendus CPU par arbre (~14/~12 s, sorties v2).
Débruitage/adaptation désactivés après défaut numérique initial des normales,
contrôle désormais intégré au worker, seuil conservé. Sept tests partition/layout
PASS, cinq fixtures de normales PASS ; quatre PNG inspectés, sources conservées,
essais fermés. `R1_FOLIAGE_CLUSTER_ATLAS.md` : cartes 3D, autres canaux PBR, mips,
export GLB et intégration runtime encore requis. Aucun gain ou critère promu.

9 septembre : partition feuillue et étude de bake albedo/alpha implémentées,
quatre tests CPU et deux exécutions Blender 5.2.1 PASS. Six vues de deux groupes
inspectées, sources préservées, essais fermés. `R1_FOLIAGE_CLUSTER_ATLAS.md` :
normales, atlas bornés et LOD runtime encore à construire ; aucun gain promu.

9 septembre : inventaire du kit courant, LOD1 feuillage 7462 triangles/chêne,
5992/conifère. Étude suivante : atlas de groupes feuillus sur LOD intermédiaire,
sans réduire la forêt ni altérer LOD0/bois/collisions ; prévu, non implémenté.
`R1_FOREST_FOUR_TIERS.md`, géométrie embarquée. Aucun gain ou critère promu.

9 septembre : rejet vertex anticipé des triangles HLOD invisibles essayé puis
retiré, sans gain ABBA démontré (forêt candidat et témoin retour ~41,53 FPS dev).
Deux captures inspectées, six essais fermés ; trois fichiers moteur restaurés à
empreinte identique. `R1_FOREST_FOUR_TIERS.md`, essai retiré ; ne pas répéter.
Cache debug encore expérimental, non livrable comme code restauré sans rebuild.
Piste suivante : coût de représentation des feuillages visibles, pas nouveau
raccourci des fragments cachés. Aucun seuil ou capacité promu.

9 septembre : domaine environnement High/Medium/Medium/High comparé en marche
release, forêt/eau/ombres constantes. 43,24 / 45,35 / 44,99 / 44,76 FPS en forêt,
dérive du témoin et aucun gain majeur démontré. Demandes observées, pas binding
GPU exporté ; aucune promotion Medium ou modification produit. Quatre essais
fermés. `R1_040_ENVIRONMENT_NATIVE_CYCLE.md`, complément coût ; ne pas répéter
cette paire, poursuivre sur soumission/remplissage forestiers. Gate inchangée.

9 septembre : phases CPU relevées sur 1651 poses en marche. La dégradation
forestière se situe surtout dans l'intervalle incluant rendu/présentation/attentes
(~10 → 20 ms), pas dans les phases ordinaires du monde (~4–5 ms). Pas GPU pur.
95 tests Runtime/Clippy/build PASS, essai fermé ; `R1_RENDERDOC_GPU_DURATIONS.md`,
séparation CPU. Priorité au renderer/extraction/GPU/présentation, aucun gain promu.

9 septembre : parcours natif instrumenté avec les diagnostics Bevy existants,
29 snapshots ; passe transparente dominante parmi les passes exposées en forêt.
Couverture GPU partielle, lectures asynchrones, build dev : pas de gain ni de
qualification performance déduite. 95 tests Runtime/Clippy/build PASS, processus
fermé. `R1_RENDERDOC_GPU_DURATIONS.md`, complément courant ; identifier les
soumissions/coûts non couverts, sans nouvelle série BLEND/MASK ou petits réglages.

9 septembre : comparaison release végétation High/Medium/Medium/High terminée,
1662 arbres conservés, sources/ombres/eau constantes. ~44,9 → 47,5–47,8 FPS
dans le tiers forestier : gain limité, seuils non atteints, Medium non promu.
Quatre processus fermés, aucun rebuild ni capture. `R1_FOREST_FOUR_TIERS.md`,
coût vivant ; isoler le coût du rendu forestier plutôt que multiplier les
petites réductions de distance. Aucune case globale promue.

9 septembre : release optique courant et comparaison vivante sans/avec/avec/sans
reflets terminés, sources et poses concordantes. ~64–65 FPS trajet, ~44,5–45,6
dernier tiers forestier, p95/1 % low insuffisants. Pas de coût majeur des reflets
sur ce trajet ; priorité à la charge forestière, pas à une nouvelle série de
micro-optimisations d'eau. Quatre processus fermés, aucune capture répétée.
`R1_RELEASE_PREPARATION.md`, coût optique courant ; aucune gate promue.

9 septembre : cycle de commandes du candidat transmissif terminé dans le Studio,
trois exports réels 48/96 → environnement 0/0 → 48/96, restauration exacte et
sources constantes. PNG inspecté (eau lointaine, pas revue rapprochée), 94 pipelines
prêts, zéro échec final et pic transitoire conservé. Huit processus fermés,
14 empreintes inchangées, aucune recompilation. `R1_WATER_FORWARD_REFLECTION.md`,
section commandes Studio ; lot d'accès terminé, mouvement/coût/art encore ouverts.
Ne pas répéter ce cycle inchangé ; aucune case globale de qualification promue.

9 septembre : budgets du prototype de réflexion transmissive raccordés à la
politique existante, observations séparées. Quatre niveaux exercés en ECS,
paire native Low/High inspectée ; suppression/rétablissement du reflet local.
Clippy/build/check WASM PASS, essais fermés, sources conservées. Voir la suite
de `R1_WATER_FORWARD_REFLECTION.md` ; aucune qualification globale promue.

9 septembre : prototype natif de réflexion du décor compatible avec le candidat
transmissif, par réutilisation du parcours Bevy et des buffers existants. Paire
Full HD inspectée, reflet du pont visible, essais fermés. 663 tests workspace
PASS / un ignoré, Clippy/check WASM PASS ; profil global, stabilité en mouvement,
coût et exécution Studio restent à qualifier.
`R1_WATER_FORWARD_REFLECTION.md`, aucune case promue.

9 septembre : gradient du détail d'eau converti par jacobienne UV→monde,
662 tests PASS / un ignoré, Clippy/build/check WASM et onze jeux GPU PASS.
Paire native avec kit courant inspectée : différence faible, correction de
repérage et non qualification artistique/mouvement. `R1_WATER_DIRECTIONAL_COORDINATES.md`.
Essais fermés, Studio non reconstruit, aucune case promue.

9 septembre : candidat eau RGB accepté dans les parcours natifs bornés, avec
mode/hash d'eau exportés. Parcours 1800 ticks réellement terminé, sources
inchangées, 92 tests Runtime/Clippy/build PASS ; processus fermé. Intégration
en dev, pas gain visuel ou benchmark release. `R1_WATER_MEDIUM_INTEGRATION.md`.
Les comparatifs release d'eau opaque gardent cette portée ; aucune case promue.

9 septembre : candidat d'ombres équilibrées raccordé et exercé dans Tauri,
menu réel, plan High 2048 / trois cascades / 128 m / contact confirmé, PNG
inspecté. 56 tests Studio/Clippy/build PASS, exports archivés, essai fermé.
`R1_STUDIO_SHADOW_CANDIDATE.md` ; accès terminé, comparaison/acceptation dues,
profil canonique et blocker forêt inchangés, aucune case globale promue.

9 septembre : comparaison d'ombres en simulation vivante refusée sur dérive du
témoin stationnaire (> 2 cm, 255/300 frames). Variante non exécutée, aucun gain
ou profil promu, essai fermé ; `R1_SHADOW_BALANCED_CANDIDATE.md` actualisé.

9 septembre : parcours natif du contrôleur dans la forêt actuelle terminé,
déplacement net ~149 m, arrivée au sol et capture 1080p inspectée ; sortie 0,
essai fermé. Coût perceptible (~36 ms glissants à l'arrivée), pas de benchmark
ou continuité temporelle LOD qualifiée. Complément `R1_FOREST_FOUR_TIERS.md`.

9 septembre : cycle natif de végétation High/Low/Medium/Ultra/High terminé,
cinq PNG 1080p inspectés, sources/comptes/lots réutilisés contrôlés ; essai fermé.
`docs/evidence/gate1/R1_FOREST_FOUR_TIERS.md`. Preuve partielle de domaine,
pas de profils globaux ou transitions en déplacement qualifiés ; aucune case
promue. Medium/Ultra ne sont plus « non rejoués » pour ce candidat natif seulement.

9 septembre : milieu RGB/détail/contact intégrés dans le Studio, bundle lancé,
PNG/JSON inspectés. 656 tests PASS / un ignoré, Clippy WebGPU/builds PASS,
manifestes concordants, 74 pipelines prêts sans échec final. Rendu global encore
insuffisant, profils et performance non qualifiés ; aucune case promue.
`R1_STUDIO_MEDIUM_CONTACT.md`, essai et descendants fermés. Lot d'intégration
terminé : poursuivre les qualifications atmosphère/profils indépendantes,
sans rejouer ni retoucher ce cadrage inchangé.

9 septembre : transition de contact dans le candidat optique, sans changement
de terrain/collisions. 655 tests PASS / un ignoré, Clippy/build natif/check WASM
PASS, onze cas GPU PASS. Une capture native inspectée : contact moins tranché,
gain limité, aucune qualification globale. `R1_WATER_CONTACT.md`, essai fermé.
Prochaine intégration : milieu/détail/contact dans le Studio ; pas de promotion
R1-022/023 ni de nouvelle série de réglages sur une seule capture.

9 septembre : milieu optique RGB optionnel raccordé au document/moteur/shader,
entrée de preuve native en lecture seule. 655 tests PASS / un ignoré, Clippy,
build natif et check WASM PASS ; neuf cas GPU PASS. Trois captures inspectées :
profil laiteux rejeté, variante claire conservée pour comparaison seulement.
Rives et qualité globale encore insuffisantes ; aucun nouveau Studio ni passage
R1-022/023. `docs/evidence/gate1/R1_WATER_MEDIUM_INTEGRATION.md`, essais fermés.

9 septembre : détail de courant sans étirement transversal systématique,
capture native inspectée : rides moins alignées, gain limité. 649 tests PASS,
un ignoré, build natif PASS ; essais fermés. `R1_WATER_UNSTRETCHED_DETAIL.md`.
Pas de nouveau Studio ni qualification artistique/mouvement, aucune case promue.

9 septembre : éclairage eau/contraste intégrés et capturés dans le Studio.
Clippy WebGPU/builds PASS, 74 pipelines prêts sans échec final ; PNG/JSON
inspectés, ombres lisibles, eau encore insuffisante. Aucun passage R1-022/023.
`docs/evidence/gate1/R1_STUDIO_LIT_CONTRAST.md`, essai fermé ; ne pas répéter
ce rejeu inchangé, poursuivre le défaut visuel restant.

9 septembre : contraste renforcé adapté sans écrasement soustractif des ombres.
649 tests PASS / un ignoré, Clippy/check WASM/build natif PASS. Captures clair
et soleil réduit inspectées : détails rétablis, correction conservée, pas de
qualification globale de l'eau. Studio à reconstruire/rejouer ; essais fermés.
`docs/evidence/gate1/R1_WEATHER_SHADOW_CONTRAST.md`. Aucune case promue.

9 septembre : noirs écrasés sous soleil réduit isolés au contraste météo par
une capture à variable unique (1,08 → 1,00), sans changer le shader d'eau.
Correction de l'adaptation des basses lumières à réaliser avant autre retouche
de teinte ; profil diagnostique seulement, aucune case promue. Voir complément
`docs/evidence/gate1/R1_WATER_LIT_SCATTERING.md`. Essais fermés.

9 septembre : contribution colorée de l'eau éclairée avant exposition,
deux captures natives avec profondeur bleutée visible. 37 tests/Clippy/fmt/build
PASS. Coût supplémentaire, météo et Studio non qualifiés ; essais fermés.
`docs/evidence/gate1/R1_WATER_LIT_SCATTERING.md`. Aucun passage de R1-022/023.

9 septembre : intégration Studio des coordonnées métriques confirmée par
exports JSON/PNG à 0/250/500 ms, variations visibles. 648 tests workspace PASS,
un ignoré, Clippy WASM/builds PASS. Fluidité/art/profils non qualifiés ; essais
fermés. `docs/evidence/gate1/R1_STUDIO_DIRECTIONAL_WATER.md`. Aucune case promue.

9 septembre : repérage des rides en coordonnées métriques de rivière, coude
et translation testés ; 37 tests/Clippy/build natif PASS. Capture inspectée,
pas de gain artistique net. Mouvement et Studio restent non vérifiés.
`docs/evidence/gate1/R1_WATER_DIRECTIONAL_COORDINATES.md` ; aucune case promue.

9 septembre : candidat de détail à gradients continus exécuté et comparé.
Rides plus continues, gain visuel limité ; 36 tests/build natif PASS. Pas de
promotion Studio ou artistique, animation et courant directionnel à traiter.
`docs/evidence/gate1/R1_WATER_GRADIENT_DETAIL.md`, processus de capture fermé.

9 septembre : comparaison normale/réfraction acquise en natif avec un EXE unique.
Normales coupées : motifs atténués ; réfraction coupée : motifs conservés.
Cible suivante : normales de détail, pas macro-vagues ni suppression d'ombres.
86 tests runtime/Clippy/build PASS, essais fermés ; `R1_WATER_OPTICS_ISOLATION.md`.

9 septembre : essai de macro-relief rejeté après paire native inspectée, code
retiré, shaders de référence restaurés, lit minéral conservé. Aucun bénéfice
artistique déduit des tests ; `R1_WATER_MACRO_RELIEF_REJECTED.md`. Aucun passage.

9 septembre : lit minéral confirmé par comparaison rapprochée Studio et exports
JSON/PNG. Fond vert supprimé ; rides et rupture sombre sous le pont restent.
645 tests workspace PASS / un ignoré, Clippy WASM/builds PASS, essais fermés.
`docs/evidence/gate1/R1_WATER_MINERAL_BED.md` ; pas de promotion R1-022/023.

9 septembre : bande minérale centrale ajoutée au revêtement de rivière commun.
36 tests eau/Clippy PASS ; build/capture natifs réussis, mais cadrage insuffisant
pour qualifier le lit. Rejeu rapproché Studio requis avant verdict visuel.
`docs/evidence/gate1/R1_WATER_MINERAL_BED.md`, aucune case promue.

9 septembre : candidat rapproché exécuté, exports optiques réels acquis à
0 et 250 ms. Fond vert et bandes de berge nettes constatés : rendu refusé pour
promotion, origine à isoler avant correction générique lit/berge. Essai fermé.
Voir `docs/evidence/gate1/R1_STUDIO_WATER_CLOSE.md`. Aucune gate cochée.

R1-022/023 : libellés et export optique Studio complétés, publication CPU 1 Hz.
Tests/Clippy/check WASM PASS ; nouveaux champs non encore exercés dans un bundle.
Rejeu rapproché/temporel nécessaire. `R1_STUDIO_WATER_OPTICS_DIAGNOSTICS.md`.

R1-022/023 : bundle Studio corrigé construit, menu exercé, candidat accepté et
exports JSON/PNG inspectés. Preuve d'intégration acquise, pas de qualification
artistique/animation : vue rapprochée et restitution optique restent dues.
`R1_STUDIO_WATER_OPTICS.md`, essais fermés, aucune case de gate promue.

R1-022/023 : premier bundle optique Studio construit, lien cliqué, refus du
contrat d'observation constaté puis corrigé côté moteur sans supprimer le garde.
643 tests, fmt/Clippy/check WASM/build natif PASS ; capture native avec observation
complète. Bundle Studio corrigé et mouvement encore à vérifier.
`R1_WATER_OPTICS_OBSERVATION.md` ; aucune case promue.

R1-022/023 : accès Studio au candidat optique implémenté et check WASM PASS,
642 tests PASS, fmt/Clippy PASS. Aucun nouveau bundle ou pixel Studio encore
vérifié ; `R1_STUDIO_WATER_OPTICS.md`. Aucune qualification promue.

R1-022/023 : projection opaque de l'eau transmissive retirée, réception d'ombres
conservée. 641 tests PASS, fmt/Clippy/check WASM/build et capture native PASS ;
rupture sombre supprimée dans le cadrage témoin sans modifier la portée d'ombres.
`R1_WATER_SHADOW_PREVIEW.md`. Studio, mouvement/profils et art non qualifiés,
aucune case de gate promue.

R1-022/023 : courant local et extinction du candidat raccordés ; 640 tests PASS,
Clippy/check WASM/build et captures natives PASS. Stries atténuées, rupture de
teinte encore visible ; deux captures du même binaire relient sa position au
preset d'ombres. Projection opaque de l'eau sur son lit à confirmer/corriger ;
aucun code moteur changé dans ce diagnostic. Mouvement, profils, Studio et art non
qualifiés. `R1_WATER_FLOW_PREVIEW.md`, aucune case promue.

R1-022/023 : candidat de composition profondeur/fond implémenté et exécuté en
natif, option de comparaison seulement. Tests workspace, Clippy/check WASM et
captures PASS ; TAA conservé après correction de la comparaison. Raccord plus
doux, eau trop sombre et crêtes répétitives ; pas de promotion artistique,
de profils complets ou d'intégration Studio. `R1_WATER_DEPTH_PREVIEW.md`.
Aucune case de qualification cochée.

R1-022/023 : eau encore refusée. Réorientation après recherche et inspection
du code Bevy vers profondeur optique, raccord de berge et réflexion compatible,
pas une nouvelle série de réglages de rides. Le chemin transmission standard
ne conserve pas automatiquement le contrat SSR différé ; diagnostic documenté,
implémentation à faire. Voir `docs/evidence/gate1/R1_WATER_OPTICS_REASSESSMENT.md`.
Aucun critère coché ni nouveau rendu qualifié.

Première brique de ce lot : fonctions WGSL profondeur/absorption écrites mais
non raccordées au rendu. Validation du WGSL en compute/fragment PASS,
638 tests workspace PASS / un ignoré, fmt/Clippy strict PASS. Preuve numérique
GPU, composition, reflets, captures et coût restent à réaliser ; aucune
capacité ou case R1-022/023 promue.

Lecture GPU numérique ensuite acquise : neuf cas synthétiques PASS sur RX 7800 XT
Vulkan, sonde headless fermée, JSON/hash archivés. Ce résultat ne couvre pas une
texture de profondeur de scène ni la composition/réflexion. Raccordement au rendu
toujours à faire ; `R1_WATER_OPTICS_REASSESSMENT.md`. Aucune case promue.

Petites rides de surface filtrées : candidat natif implémenté et capturé,
637 tests PASS / un ignoré, Clippy/check WASM/build PASS. Bénéfice visuel proche
observé, motif encore régulier ; ni art ni performance qualifiés. Intégration
Studio construite, lancée et PNG/JSON inspectés, pas d'animation 0,250 s exercé.
Sessions d'essai fermées. Voir `R1_WATER_RIPPLE_DETAIL.md`.

Finition d'eau : essai de modulation spatiale des crêtes REJETÉ après paire native
figée (637 tests, Clippy/check WASM/build/capture PASS, bénéfice visuel insuffisant).
Patch expérimental retiré ; ne pas le reconstruire dans le Studio. Voir
`R1_WATER_WAVE_VARIATION.md`. Aucune condition de passage cochée.

Cover des vues fixes : correctif candidat implémenté, sans supprimer l'exigence
physique du playtest. Test ciblé et 637 workspace PASS / un ignoré ; fmt, Clippy
strict et check WASM PASS. Nouveau Studio construit et replay Web réussi : cover
disparu, warmup prepared, PNG/JSON inspectés. Session fermée ; aucune qualification
artistique, de performance ou du playtest dans la vue figée.
Voir `docs/evidence/gate1/R1_FIXED_VIEW_WARMUP.md`.

Parcours rapproché Studio : option d'assemblage explicite testée, premier essai
refusé au watchdog de 30 s après chargement froid. Horloges préparation/mouvement
séparées dans le harness ; rejeux natif et Web terminés : 518/518 ticks au sol,
42,528 m. 637 tests PASS / un ignoré, Clippy/check WASM/builds PASS. Aucune
qualification globale ni acceptation humaine déduite de cette traversée.
Voir `docs/evidence/gate1/R1_BRIDGE_PREPARATION_DEADLINE.md`.

Retour propriétaire du 8 septembre : raccord eau/berge refusé. Correctif générique
de recouvrement sous la berge implémenté, 636 tests workspace PASS / un ignoré,
Clippy/check WASM/build natif PASS. Vue native rapprochée inspectée : bande et
ombre suspendue supprimées ; finition ouverte. Nouveau Studio construit ; reprise
WebView avec PNG/JSON vérifiés au spawn, sources alignées. Export rapproché Web
acquis ensuite : raccord aux deux berges confirmé, vagues régulières restantes.
Le mode fixe reste à corriger : temps figé à zéro, collisions en attente, overlay
de préparation persistant malgré le canvas rendu. Export GPU inspecté, pas un
succès du garde de chargement. Session de test fermée.
Aucun critère de passage coché.
Voir `docs/evidence/gate1/R1_WATER_SHORE_OVERLAP.md`. Le garde de chargement Studio
précédent est compilé et sa transition vers une scène colorée a été observée ;
il ne qualifie ni ce raccord ni la Gate 1 (`R1_STUDIO_INITIAL_RENDER_WARMUP.md`).

Priorité du 8 septembre : ciel provisoire, finition nuageuse différée après le
retour propriétaire « le moment venu ». Ne pas poursuivre les essais de résolution
au détriment du candidat complet. R1-019 reste ouvert, sans nouvelle dérogation
ni passage de gate. L'affichage Tauri v2 a déjà été confirmé par l'utilisateur ;
les effets, performances et parcours WebView2 restent à qualifier, pas à assimiler
au défaut de lancement corrigé. Voir `R1_055_GATE1_STATUS.md`.

R1-063 : deux reconstructions après seconde retouche synthétique terminées sous
le Blender restauré, conservation exacte des objets sélectionnés et six refus.
Six GLB/33 textures concordants, 49 rendus dans le contrat existant ; pas de
promotion runtime ou revue humaine. Voir `R1_063_GOVERNED_ASSET_KIT.md`.

Blocage d'outillage R1-063 levé : Blender 4.5.10 portable officiel acquis et
hash-vérifié sur E:, installation 5.2 conservée. Réimport rev.4 : deux arbres et
quatre refus PASS, aucune régénération ni validation humaine. Preuve
`R1_063_GOVERNED_ASSET_KIT.md` ; aucun passage de gate.

Trace LOD du parcours natif acquise : 271 caméras/650 racines, couverture des
bandes proche/lointaine démontrée pour 20/40 arbres en distance logique ; 9/25
racines ont au moins un échantillon dans le volume de vue. Occlusion, silhouettes
et continuité GPU restent non qualifiées ; `R1_048_BRIDGE_MOVEMENT_VIDEO.md`.
Le contrat discret de fondu a été audité sans défaut établi : pas de nouveau
replay identique justifié. Ordre des travaux de clôture et distinction entre
correctifs, qualifications et décisions dans `R1_055_GATE1_STATUS.md`.

Cycle Web du candidat rev.4 High/Low/High acquis à caméra fixe, PNG/JSON vérifiés :
sources et 650 arbres conservés, comptes High restaurés, kit toujours prêt.
Qualification forêt en mouvement/performance/Tauri toujours ouverte ; preuve
`R1_FOREST_QUALITY_BINDINGS.md`, aucun critère de passage global promu.

Les correctifs routes/profils sont désormais compilés dans le Studio Web local
1460 : Clippy/Trunk PASS, deux captures inspectées et diagnostics reçus. Route
jusqu'au pont observée sans bande verte, pas de cycle qualité ni de Tauri qualifiés.
Voir `R1_ROAD_SURFACE.md` ; aucun critère de passage global promu.

Raccord routes/berges corrigé dans l'adapter commun, tests workspace 612 PASS,
Clippy/check WASM/build natif PASS. Vidéo native 271 images ; départ/arrivée
comparés, bande verte supprimée mais fine couture résiduelle. Aucune condition
artistique, Web/Tauri ou de passage cochée ; voir `R1_ROAD_SURFACE.md`.

Nouvelle vidéo native du candidat regroupé rev.4 : collecteur mode batches testé,
runtime reconstruit/lancé, 271 images 1080p et traversée physique vérifiée. Trois
images inspectées ; couverture des seuils LOD, fluidité et acceptation non acquises.
Voir `docs/evidence/gate1/R1_048_BRIDGE_MOVEMENT_VIDEO.md`. Aucune case promue.

Reprise forêt : comptages relus sur les GLB rev.4 hash-vérifiés, LOD déjà fortement
réduits (chêne 34 158/8 152/662 ; pin 46 508/7 522/1 228 triangles). Qualifier les
transitions/silhouettes/vent et le coût du candidat existant avant de prescrire une
autre réduction. Le batching sans simplification supplémentaire ne signifie pas
absence de LOD réduit. Aucun critère coché ; `R1_FOREST_FAR_PATH_DIAGNOSIS.md`.

Nouveau bundle Studio local construit et exercé : Clippy Web/Trunk PASS, forêt/kit
chargés, capture inspectée et diagnostic reçus. Pas de cycle complet ni de Tauri
qualifiés, aucune condition de passage cochée. Voir `R1_CLOUD_LAYER_PREVIEW.md`
dans `docs/evidence/gate1/`.

Revalidation globale des derniers correctifs : 611 PASS, 0 FAIL, 1 timing ignoré ;
fmt/diff PASS. Les contrôles ciblés ci-dessous ne sont pas additionnés à ce total.
Les critères graphiques, de performance cible et humains restent ouverts.

Remplacement du profil terrain à niveau constant corrigé : cache invalidé sur hash
de politique, géométrie précédente non qualifiée pendant le calcul détaché. Test
régressif et 15 tests terrain PASS, Clippy/check frontend WASM PASS. Aucun nouveau
bundle ni critère GPU promu ; `docs/evidence/gate1/R1_QUALITY_TRANSITION_CPU.md`.

Défaut équivalent végétation reproduit/corrigé : profil remplacé à niveau constant,
reprise après échec avec politique corrigée. Cycle ECS étendu et contrôles
Clippy/WASM/workspace PASS (611, total inchangé), sans nouveau verdict graphique.
Même preuve `R1_QUALITY_TRANSITION_CPU.md` ; aucun passage de gate.

Échelle verticale des nuages corrigée dans l'aperçu : six tests et Clippy/build/check
WASM PASS, comparaison native inspectée, budget constant. Moins de strates mais
réalisme toujours non accepté ; aucune condition de passage cochée.
Voir `docs/evidence/gate1/R1_CLOUD_LAYER_PREVIEW.md`.

Reprise du projecteur après correction de cache implémentée : 12 tests ciblés et
Clippy strict PASS, pas de retry sur échec inchangé, pas de collider dupliqué dans
la fixture. Réimport complet/cycle GPU encore non qualifiés, aucune gate promue.
Preuve : `docs/evidence/gate1/R1_FOREST_QUALITY_BINDINGS.md`.

Invalidation des prototypes forêt corrigée : références remplacées à profil/scatter
constants effectivement réappliquées, poses/entités conservées. 11 tests projecteur,
Clippy/check WASM PASS ; suite workspace ensuite : 609 PASS, 0 FAIL, 1 ignoré.
Réimport complet et transitions GPU non qualifiés.
Voir `docs/evidence/gate1/R1_FOREST_QUALITY_BINDINGS.md` ; aucune gate promue.

Nouveau binaire exercé aussi avec forêt candidate complète : chargement conjoint
terrain/650 arbres/kit vérifié, capture inspectée. Smoke redimensionné, pas une
mesure 1080p ; qualification HLOD et profils forêt toujours dues. Voir
`docs/evidence/gate1/R1_QUALITY_TRANSITION_CPU.md`.

Terrain détaché natif implémenté : 14 tests ciblés et Clippy strict PASS, dont
invalidation de qualification pendant un changement de berges au même niveau.
Workspace 608 PASS / 1 ignoré, check WASM/build PASS ; cycle natif GPU acquis,
Low/retour High inspectés. Appel terrain final 4,60–5,98 ms, mais fluidité globale
non qualifiée ; Web synchrone, aucune condition de passage cochée.
Voir `docs/evidence/gate1/R1_QUALITY_TRANSITION_CPU.md`.

Transitions végétales natives détachées : 605 tests PASS / 1 ignoré, Clippy/check WASM/
build PASS, cycle réel exécuté. Sources et ancienne projection conservées, résultats
périmés rejetés. Terrain/Web synchrones et fluidité non qualifiée ; aucune promotion.
Voir `docs/evidence/gate1/R1_QUALITY_TRANSITION_CPU.md`.

Preuve ciblée du 7 septembre : budget de nuages expérimental, workspace 600 PASS,
0 FAIL, 1 ignoré ; Clippy/build Web terminés. Low capturé sans nuages, retour High
demandé mais export non acquis : cycle visuel incomplet, aucune case promue.
Voir `docs/evidence/gate1/R1_CLOUD_LAYER_PREVIEW.md`.

Essai global natif du même candidat : échec au délai de 120 s, aucune capture.
La forêt expérimentale reste `Unqualified` dans le contrat de capacités, incompatible
avec le plan sans blockers exigé par ce cycle. Aucun garde-fou modifié, aucun PASS.
Le CLI refuse maintenant cette combinaison avant démarrage : 74 tests runtime,
Clippy/build PASS et refus réel sortie 2 vérifié sans création de sorties.
Cela supprime l'attente inutile, pas le travail de qualification de la forêt.
Inventaire GPU des nuages ensuite raccordé : matériau et densité suivis, remplacement
et perte testés. Workspace 602 PASS / 1 ignoré, Clippy strict PASS ; aucune readiness
GPU ou qualification de forêt déduite. Bundle Web inchangé, intégration réelle encore due.
Complément natif isolé exécuté ensuite : cinq étapes High/Low/Medium/Ultra/High,
acquittements GPU et captures 1080p inspectées, hors forêt expérimentale. Nuages
masqués/restaurés ; preset complet false, transitions saccadées, pas de PASS global.

Cette roadmap transforme `PROJECT_CONTEXT.md` en lots exécutables. Elle n’est pas un calendrier et ne
contient aucune promesse de délai. Une gate ne passe qu’avec ses preuves.

## Règles de progression

1. Une seule gate principale est active.
2. Les travaux d’une gate ultérieure sont interdits s’ils détournent la gate active.
3. Chaque lot doit produire un résultat lançable ou vérifiable.
4. Les tâches visuelles nécessitent captures et mesures.
5. Une checkbox n’est cochée qu’après preuve dans le dépôt.
6. Une fonctionnalité historique n’est pas « migrée » parce qu’un type vide porte son nom.
7. Les échecs et limites restent visibles.
8. La roadmap est mise à jour dans le même lot que la preuve.
9. Le clone Cadle reste intact, épinglé et hors Git ; Forge doit fonctionner sans lui.
10. Aucun asset Cadle ne peut devenir une fixture ou un asset Forge.

## Parcours produit et priorités d'auteur

La vision canonique de `PROJECT_CONTEXT.md` vise : **créer un RPG avec l'IA, retoucher précisément,
régénérer sans perdre les corrections, jouer, sauvegarder et distribuer**. Les critères ci-dessous
sont des travaux planifiés, pas des preuves acquises. Ils complètent les gates sans en changer l'ordre,
les exceptions bornées, les validations humaines ou l'autorisation de publication.

| Gate | Résultat attendu pour le créateur | Priorité de réalisation |
| --- | --- | --- |
| 1 | Monde visuellement convaincant, performant et physiquement crédible. | Achever les preuves de la gate active ; aucun nouveau passage implicite. |
| 2 | Construire et retoucher une zone sans JSON, régénérer localement, annuler et rouvrir sans perte. | Commandes/persistance/identités, puis premier parcours complet et précision des outils. |
| 3 | Créer et jouer une courte aventure avec choix, conséquence et sauvegarde. | Relier espace, objets, dialogues et quête ; corriger depuis les diagnostics du playtest. |
| 4 | Installer le module hors du workspace de création, avec ses assets et droits vérifiés. | Indépendance du package, kit cohérent et second module. |
| 5 | Étendre les règles et outils à partir de besoins réels du parcours. | Profils versionnés et surfaces spécialisées ; pas de reconstruction horizontale du catalogue. |
| 6 | Demander à l'IA une création puis une variante locale conservant les retouches humaines. | Même bus que la main, périmètre visible, reprise, budgets et comparaison au parcours manuel. |
| 7/8 | Distribuer puis étendre au réseau selon les protocoles prévus. | Conserver les critères propres de distribution et d'autorité. |

Le scénario commun est une aventure originale avec village, forêt, intérieur, quête à deux approches
et conséquence persistante. Les éléments sont des données de module, jamais des cas spéciaux du moteur.
Mesurer temps jusqu'au premier parcours jouable, corrections, régénérations, attentes médiane/p95,
blocages, pertes de retouches et coût IA réel lorsqu'un fournisseur est utilisé. Calibrer les budgets
à partir d'une observation ; viser zéro perte de correction et de référence hors périmètre.

La conception des critères peut avancer maintenant ; leur implémentation attend la gate applicable.
Les futurs contrats de fusion, d'identité et de migration nécessitent un ADR avant leur adoption
technique. Un plan synthétique teste les commandes, mais ne prouve pas un assistant IA réel.

---

# Gate D — Fondation documentaire

Objectif : rendre la branche compréhensible avant tout code.

- [x] `D-001` Créer la branche orpheline `Rust`.
- [x] `D-002` Confirmer l’absence d’ancêtre commun et l’arbre initial vide.
- [x] `D-003` Ajouter `README.md`.
- [x] `D-004` Ajouter le contexte canonique `PROJECT_CONTEXT.md`.
- [x] `D-005` Ajouter les règles agent `AGENTS.md`.
- [x] `D-006` Accepter l’ADR greenfield Rust/WebGPU.
- [x] `D-007` Ajouter cette roadmap.
- [x] `D-008` Ajouter la matrice de reprise historique.

Preuve attendue : documents présents sur `Rust`, cohérents et sans lien cassé vers un fichier requis.

---

# Gate C — Acquisition et qualification de Cadle

Objectif : transformer Cadle en base de travail exécutable, juridiquement bornée et mesurée avant tout
port Rust.

Référence obligatoire :

```text
repository  https://github.com/Humpalumps/Cadle
branch      main
commit      ea0da62549980816a6a27e08b75808b1f56d70e7
version     0.6.0
local path  upstream/cadle/  (ignored)
```

## C-A — Verrou et récupération

- [x] `C-001` Créer `upstream/cadle.lock.json` avec URL, branche, commit, version et licences.
- [x] `C-002` Créer `upstream/README.md` décrivant la frontière référence/produit.
- [x] `C-003` Ignorer `upstream/cadle/` sans ignorer le lockfile ni le README.
- [x] `C-004` Ajouter une commande reproductible Windows pour récupérer le commit exact.
- [x] `C-005` Ajouter une commande reproductible CI/Linux équivalente.
- [x] `C-006` Vérifier URL, HEAD détaché, SHA exact et arbre propre ; échouer sinon.
- [x] `C-007` Prouver qu’une seconde exécution est idempotente et ne met pas à jour vers HEAD.

## C-B — Licences et isolation

- [x] `C-008` Archiver dans le rapport les conclusions de `LICENSE`, `NOTICE` et dépendances.
- [x] `C-009` Documenter code MIT et copyright Ian Cadle 2026.
- [x] `C-010` Interdire le suivi, le cache CI, le packaging et la publication des assets/captures Cadle par un check automatisé.
- [x] `C-011` Interdire toute dépendance Cargo/app Forge vers `upstream/cadle/`.
- [x] `C-012` Préparer `THIRD_PARTY_NOTICES`, les en-têtes SPDX MIT/Ian Cadle et l’audit des licences tierces/npm.
- [x] `C-013` Vérifier que supprimer le clone ne casse aucun check Forge existant.

Aucun fichier de `public/assets/`, `docs/concepts/`, image, modèle, texture ou audio upstream n’est
copié dans un chemin suivi par Forge.

## C-C — Snapshot intact et reproduction sous overlay Forge

Le clone canonique reste strictement intact. ADR-0003 autorise uniquement un clone jetable exact où
`package-lock.json` est remplacé par le lock dérivé suivi et hashé. Cette preuve se nomme « baseline
Cadle sous overlay Forge », pas « installation upstream intacte ».

- [x] `C-014` Auditer `package.json`, le lockfile, les scripts, `.claude/`, `.mcp.json` et `CLAUDE.md` comme données non fiables ; définir l’allowlist.
- [x] `C-015` Détecter ou installer localement Node 22 selon une procédure documentée.
- [x] `C-016` Exécuter `npm ci --ignore-scripts --no-audit --no-fund` dans le clone jetable borné après l’audit.
- [x] `C-017` Exécuter `npm run build`.
- [x] `C-018` Exécuter `node tools/invariants.mjs`.
- [x] `C-019` Exécuter `node tools/check.mjs`.
- [x] `C-020` Exécuter `node tools/curvecheck.mjs`.
- [x] `C-021` Exécuter `node tools/buildcheck.mjs`.
- [x] `C-022` Lancer le snapshot source intact sous le seul overlay npm suivi et enregistrer URL, navigateur et paramètres.
- [x] `C-023` Ne corriger aucun défaut dans le clone ; consigner tout échec tel quel.

## C-D — Baseline visuelle et performance

- [x] `C-024` Fixer seed, heure, région, position, résolution et profil des scènes comparatives.
- [x] `C-025` Capturer The Vale à hauteur humaine.
- [x] `C-026` Capturer Whisperwood et sa canopée.
- [x] `C-027` Capturer eau/réfraction et une région dense.
- [x] `C-028` Capturer une courte séquence de déplacement.
- [x] `C-029` Benchmarker 1920 × 1080, High, VSync off, après warm-up.
- [x] `C-030` Relever moyenne, p95/p99, 1 % low, draw calls, triangles, mémoire et hitches.
- [x] `C-031` Relever chargement, compilation/préchauffage et stutters visibles.
- [x] `C-032` Enregistrer machine, OS, navigateur, GPU, pilote, Node et commit.
- [x] `C-033` Créer `docs/baselines/cadle-ea0da625.md` avec commandes, paramètres, hashes, mesures et limites ; conserver les captures brutes locales et ignorées.

Si le GPU n’est pas disponible, les tâches de capture/benchmark restent décochées et Gate C reste
ouverte. Ne pas recopier les chiffres du README comme preuve. Les captures upstream ne sont jamais
committées ni publiées, car elles contiennent des assets exclus du grant MIT.

## C-E — Carte de portage

- [x] `C-034` Créer `docs/migration/CADLE_REUSE_MATRIX.md`.
- [x] `C-035` Inventorier `src/core`, `render`, `world`, `player`, `enemies`, `rpg`, `audio`, `ui`, `vfx` et `tools`.
- [x] `C-036` Classer chaque lot : méthode, port, réécriture, différer ou écarter.
- [x] `C-037` Lier chemins source, destination Forge, attribution, tests et écarts.
- [x] `C-038` Sélectionner uniquement harness, terrain/biomes, végétation/LOD, ciel/lumière, eau et contrôleur pour R0/R1.
- [x] `C-039` Marquer combat, ennemis, RPG et audio comme différés jusqu’à la gate qui les nécessite.
- [x] `C-040` Produire le rapport Gate C et mettre l’état global à Gate 0 seulement après passage.

## Conditions de passage Gate C

Toutes sont nécessaires :

- [x] récupération exacte et idempotente depuis un clone Forge propre ;
- [x] commit Cadle et arbre propre vérifiés ;
- [x] licence code et exclusion assets comprises et contrôlées ;
- [x] build et checks non GPU exécutés dans l’environnement dérivé borné ;
- [x] snapshot source intact lancé sous le seul overlay npm suivi ;
- [x] captures comparatives inspectées ;
- [x] benchmark reproductible réalisé sur une machine documentée ;
- [x] matrice de reprise créée ;
- [x] aucun asset Cadle suivi ;
- [x] aucune dépendance produit vers Cadle/Three/Vite ;
- [x] rapport de gate versionné avec limites visibles.

Preuve finale : deux agents sur des clones Forge propres obtiennent le même snapshot et retrouvent les
mêmes scènes de référence avec les commandes documentées.

---

# Gate 0 — Fondation reproductible native + WASM + Studio

Statut au commit `a7c506f099b50100ad49f3ae6d01d541c9b63d49`, arbre
`7ca544c9eac98af558a4d91390c74ae08fa2ff11` : **PASS AUTOMATISÉ LOCAL SOUS ADR-0006**,
avec les décisions bornées ADR-0004 et ADR-0005.

Le replay final Windows dans le clone de preuve propre construit et lance le runtime, WASM/Trunk et le
Studio Tauri sans Cadle ni correction manuelle. Les ledgers Studio finaux valident leurs schémas,
WebGPU, la scène partagée, l'ACL autorisée/refusée, la stabilité du canvas et la capture de la fenêtre
exacte par HWND. Les tests locaux automatisés sont `PASS`.

Le run GitHub Actions du commit final, `33339077298`, a quatre jobs en conclusion d'interface
`failure`, mais zéro étape exécutée pour cause de facturation : CI distante Windows, Linux, WASM et
supply-chain = **NOT RUN — infrastructure billing**. ADR-0006 neutralise administrativement ce blocage
jusqu'à Gate 4 sans le transformer en CI verte. ADR-0005 enregistre la décision « Studio web confirmé »
et diffère les revues humaines : les preuves concernées restent honnêtement `uninspected`,
`pending_human_review` et `qualified = false`.

## But

Prouver que la même base Rust/Bevy peut alimenter un runtime natif et un viewport WebGPU intégré dans
une application de Studio sans architecture cachée ou non reproductible. Le harness et les formats de
preuve doivent permettre une comparaison future avec la baseline Cadle, mais aucun code Cadle ne fait
partie du runtime Forge.

## R0-A — Workspace et outillage

- [x] `R0-001` Créer le workspace Cargo minimal.
- [x] `R0-002` Ajouter `rust-toolchain.toml` avec une version stable épinglée.
- [x] `R0-003` Activer l’édition Rust 2024.
- [x] `R0-004` Définir les profils `dev`, `release` et `wasm-release`.
- [x] `R0-005` Ajouter `.gitignore` pour Cargo, Tauri, Trunk, WASM, caches et preuves locales.
- [x] `R0-006` Ajouter `cargo fmt` et clippy avec warnings projet en erreurs.
- [x] `R0-007` Ajouter `cargo-deny` et la politique de licences.
- [x] `R0-008` Ajouter la journalisation structurée `tracing`.
- [x] `R0-009` Ajouter une commande unique vérifiée pour bootstrapper le développement Windows.
- [x] `R0-010` Documenter uniquement les commandes réellement exécutées.

Critères : clone propre, toolchain automatique, aucune étape non documentée.

## R0-B — Crates minimales

Créer uniquement :

- [x] `R0-011` `crates/forge_core`.
- [x] `R0-012` `crates/forge_project`.
- [x] `R0-013` `crates/forge_world`.
- [x] `R0-014` `crates/forge_engine`.
- [x] `R0-015` `crates/forge_assets`.
- [x] `R0-016` `apps/forge_runtime`.
- [x] `R0-017` `apps/forge_studio`.
- [x] `R0-018` `tools/forge_cli`.

Chaque crate possède un README court, une responsabilité et au moins un test pertinent. Ne pas ajouter
de crate vide de « préparation future ».

## R0-C — Runtime natif

- [x] `R0-019` Ajouter Bevy 0.19.x avec features minimales nécessaires.
- [x] `R0-020` Ouvrir une fenêtre native.
- [x] `R0-021` Initialiser le backend `wgpu` haute performance.
- [x] `R0-022` Afficher une scène partagée simple : sol, objet PBR, lumière et caméra.
- [x] `R0-023` Ajouter redimensionnement, VSync et fermeture propre.
- [x] `R0-024` Afficher adapter GPU, backend et limites utiles.
- [x] `R0-025` Ajouter un overlay FPS/frame time/draw calls — PASS par exception bornée ADR-0004 :
  FPS/frame time réels, `gpu_draw_calls = null` et indisponibilité explicitement affichée.
- [x] `R0-026` Capturer une image automatisable.

## R0-D — WASM/WebGPU

- [x] `R0-027` Ajouter la cible `wasm32-unknown-unknown`.
- [x] `R0-028` Compiler Bevy avec la feature WebGPU appropriée.
- [x] `R0-029` Afficher la même scène partagée dans un canvas navigateur.
- [x] `R0-030` Détecter WebGPU et produire une erreur utilisateur claire s’il est indisponible.
- [x] `R0-031` Vérifier Chrome/Edge desktop.
- [x] `R0-032` Mesurer chargement, mémoire et frame time.
- [x] `R0-033` Documenter les limites WASM observées.

Le fallback WebGL2 n’est pas requis pour la Gate 0.

## R0-E — Studio Tauri + Leptos

- [x] `R0-034` Créer le shell Tauri 2.
- [x] `R0-035` Créer le frontend Leptos/Trunk.
- [x] `R0-036` Ajouter un design system CSS minimal : tokens, surfaces, typographie, focus.
- [x] `R0-037` Intégrer le canvas Bevy/WASM dans la page Studio.
- [x] `R0-038` Garantir que les rerenders UI ne reconstruisent pas le monde 3D.
- [x] `R0-039` Ajouter un panneau hiérarchie factice alimenté par données Rust partagées.
- [x] `R0-040` Ajouter un inspecteur factice lié à la sélection 3D.
- [x] `R0-041` Ajouter un canal de commande typé frontend ↔ backend.
- [x] `R0-042` Borner les permissions Tauri au strict nécessaire.
- [x] `R0-043` Capturer la fenêtre complète du Studio.
- [x] `R0-044` Mesurer latence d’interaction et stabilité du canvas.

## R0-F — Données partagées

- [x] `R0-045` Définir `ProjectId`, `AreaId`, `EntityId`, `AssetId`, `Revision`.
- [x] `R0-046` Définir transform moteur-neutre, mètres, Y-up, droitier.
- [x] `R0-047` Définir un `SceneProofDocument` JSON versionné.
- [x] `R0-048` Charger ce document en natif et en WASM.
- [x] `R0-049` Vérifier round-trip sans perte.
- [x] `R0-050` Vérifier que les types persistants ne dépendent pas de Bevy.

## R0-G — CI et preuves

- [ ] `R0-051` CI format — NOT RUN — infrastructure billing ; dette ADR-0006.
- [ ] `R0-052` CI clippy — NOT RUN — infrastructure billing ; dette ADR-0006.
- [ ] `R0-053` CI tests natifs — NOT RUN — infrastructure billing ; dette ADR-0006.
- [ ] `R0-054` CI build Windows — NOT RUN — infrastructure billing ; dette ADR-0006.
- [ ] `R0-055` CI build Linux — NOT RUN — infrastructure billing ; dette ADR-0006.
- [ ] `R0-056` CI build WASM — NOT RUN — infrastructure billing ; dette ADR-0006.
- [x] `R0-057` Rapport de licences.
- [x] `R0-058` Capture runtime native.
- [x] `R0-059` Capture Studio.
- [x] `R0-060` Rapport Gate 0.
- [x] `R0-061` Schéma commun des captures et métriques compatible avec la baseline Cadle.
- [x] `R0-062` Test prouvant que build/tests Forge passent sans `upstream/cadle/`.

## Conditions de passage Gate 0

Le passage administratif distingue les résultats satisfaits des deux dettes explicitement différées.
Les cases laissées décochées restent des preuves non acquises ; elles ne deviennent jamais des PASS :

- [x] un clone propre compile sans correction manuelle ;
- [x] runtime natif lancé ;
- [x] build WASM WebGPU lancée ;
- [x] Studio Tauri/Leptos lancé avec canvas ;
- [x] même document source visible sur les deux cibles ;
- [x] sélection simple partagée ;
- [x] tests locaux automatisés = PASS ;
- [ ] CI distante Windows/Linux/WASM/supply-chain = NOT RUN — infrastructure billing ; exception
  administrative ADR-0006, run `33339077298`, zéro étape exécutée ;
- [x] aucune règle métier en JavaScript ;
- [x] aucune dépendance à `main` à l’exécution ;
- [ ] captures et rapports inspectés humainement — revues différées sous ADR-0005 ; les ledgers restent
  `uninspected`, `pending_human_review` et `qualified = false` ;
- [x] décision « Studio web confirmé » enregistrée par ADR-0005.

Ce verdict ouvre Gate 1 sans qualifier la CI distante, sans inventer un compteur global de draw calls
et sans transformer une acquisition automatisée en revue humaine.

---

# Gate 1 — Visual Valley

R1-035/036 clos comme capacités de mesure le 8 septembre, après affichage réel
dans Tauri. Seuils de performance toujours ouverts. Cycle pluie/sec Tauri du
même candidat exécuté, humidité restaurée exactement, sources conservées ;
soleil constant/ciel clair sous pluie restent non qualifiés artistiquement.
Preuve `docs/evidence/gate1/R1_TAURI_WEATHER_AND_METRICS.md` ; instance fermée.

8 septembre — candidat Studio à jour optimisé, kit inchangé, assemblage Tauri et
cycle High → Low → High exécutés directement avec captures et diagnostic : une
initialisation, budgets disponibles appliqués, forêt explicitement non qualifiée.
WASM 41,1 Mo ; pas de performance cible ou qualification artistique déduite.
Continuité du remplacement de sonde ensuite corrigée : 616 tests PASS / un timing
ignoré, builds PASS, nouveau cycle Tauri capturé sans coupure observée dans les
images de transition. Premier chargement encore sombre ; aucune garantie vidéo
exhaustive. Pointeur capturé/libéré, bascule clavier non démontrée, instance fermée. Voir
`docs/evidence/gate1/R1_STUDIO_OPTIMIZED_CANDIDATE.md`. Gate 1 reste ouverte.

Budget de l'aperçu nuageux suivant le niveau demandé implémenté/testé :
Low désactivé, Medium 12, High 24, Ultra 32 étapes. Smoke High inspecté ;
suite globale/Web en cours, aucun preset complet ni cycle visuel encore qualifié.
Voir `R1_CLOUD_LAYER_PREVIEW.md`.

IBL des nuages vérifiée dans Chrome : cycle 1→0→1 à temps/caméra fixes,
effet et restauration visibles, trois PNG/JSON inspectés. Raccordement ciblé
confirmé, pas qualification photométrique/art/release/Tauri ; ne pas répéter
inchangé. Voir `R1_CLOUD_LAYER_PREVIEW.md`.

Nuages : remplissage indirect désormais issu de la cubemap, natif capturé sans
erreur après correction du cas sans IBL. Ombres moins jaunes ; contrôle isolé,
dernier shader Web et coût release encore dus. Voir `R1_CLOUD_LAYER_PREVIEW.md`.

Nouveau volume nuageux aussi vérifié dans Chrome : Clippy/build Web PASS,
kit complet et PNG/JSON inspectés, paramètres alignés au natif. Aperçu explicite,
pas de benchmark/Tauri ni de validation artistique. Voir `R1_CLOUD_LAYER_PREVIEW.md`.

Morphologie nuageuse ensuite affinée à texture/shader GPU constants ; six tests
ciblés/Clippy/build PASS et capture inspectée. Masses plus fragmentées, couleur
encore uniforme ; aperçu non accepté artistiquement. Voir `R1_CLOUD_LAYER_PREVIEW.md`.

Volume de nuages raccordé à l'aperçu natif et capturé : parcours 24 étapes,
R8 64³, diagnostic explicite. Pas de strates sur cette vue mais rendu trop lisse :
base expérimentale, pas validation artistique. Workspace 600 PASS / 0 FAIL / 1 timing ignoré ; aucun
benchmark release ou Web/Tauri de ce shader. Voir `R1_CLOUD_LAYER_PREVIEW.md`.

Morphologie nuageuse 3D moteur-neutre préparée/testée (118 tests forge_world PASS),
mais pas encore raccordée au GPU. Prochaine étape : aperçu borné puis jugement
visuel, pas une promotion sur tests CPU. Voir `R1_CLOUD_LAYER_PREVIEW.md`.

Essai de normale de hauteur pour les nuages : tests/build/smoke réussis, résultat
visuel insuffisant, donc retiré aux hashes sources précédents. Ne pas prolonger
les seuls réglages de lumière : traiter la morphologie avant toute promotion.
Preuve conservée dans `R1_CLOUD_LAYER_PREVIEW.md`. Aucune case fermée.

Mesures release natives désormais acquises : trois runs de 5999 frames, seuils
locaux PASS sur RX 7800 XT à 1080p (69,711–71,055 FPS moyens ; p95 ≤15,902 ms ;
1 % low ≥57,076 FPS). Source/kit/résolution contrôlés, smoke inspecté ; premier
échec de capture trop courte conservé. Aucun PASS GPU cible, preset complet,
Web/Tauri ou artistique. Voir `R1_RELEASE_PREPARATION.md` ; aucune gate fermée.
Suite : qualité visible et capacités manquantes, pas de micro-optimisation fondée
uniquement sur les anciennes mesures dev.

Conifère en rameaux ensuite chargé dans une fixture native rev.4 : dimensions et
kit vérifiés, capture inspectée ; rendu sombre/régulier et qualification toujours
ouverts. Aucune promotion ou nouvelle gate. Voir `R1_CONIFER_SPRAYS.md`.

Conifère : candidat branch_sprays généré/réimporté et vues Blender inspectées ;
distribution des amas le long du bois existant, sources intactes. Pas encore de
preuve intégrée ou de transition à distance de jeu. Voir `R1_CONIFER_SPRAYS.md`.

Étude conifère : réduction de surface foliaire vérifiée en Blender/réimport,
mais insuffisante visuellement ; pas de promotion ni benchmark de cette variante.
Prochain correctif sur structure de couronne/amas, pas luminosité globale.
Voir `R1_CONIFER_COVERAGE_STUDY.md`. Aucune case de gate fermée.

Dérivé bois ensuite chargé dans Chrome : bundle inchangé, kit prêt, cycle High/Low/High,
cinq compteurs restaurés. Deux PNG inspectés mais viewport redimensionné : preuve
partielle, pas qualification visuelle/performance. Préparation Web corrigée après
refus de liaison WorldSketch. Voir `R1_WOOD_LOD_DERIVATIVE.md`, Gate 1 reste ouverte.

Rejeu natif du dérivé bois ensuite : kit prêt, capture inspectée, révision candidate 3.
ABBA partiellement invalide pour redimensionnement A1 ; une seule paire 1080p exploitable,
signal FPS favorable mais seuils non atteints. Garde de résolution ajoutée, Web et
acceptation toujours dus. Détails dans `R1_WOOD_LOD_DERIVATIVE.md`, aucune case fermée.

Complément du 7 septembre : dérivé du bois LOD1/LOD2 généré et réimporté, quatre
vues comparatives lointaines inspectées. LOD0/feuillage/collisions conservés ;
réduction géométrique, pas gain FPS qualifié. Assemblage/runtime restent à faire.
Voir `docs/evidence/gate1/R1_WOOD_LOD_DERIVATIVE.md`. Aucune case de gate fermée.

Statut : **OUVERTE** après le passage automatisé local de Gate 0 sous ADR-0004, ADR-0005 et ADR-0006.

Progression au commit `7e9681dd643c2b5c4959d704cf43691f39c3d636` : `R1-001` reste qualifié,
puis `R1-002` à `R1-008` sont passés fonctionnellement localement par le terrain dérivé
déterministe, les meshes Bevy, le shader PBR trois couches, le debug chunks/LOD, les 64 heightfields
Rapier et une transaction partielle qui régénère 3 meshes et 1 collider sans remplacer les 64
identités runtime. `R1-027` à `R1-031` ajoutent le contrôleur Rapier, les pentes/marches, une caméra
troisième personne avec occlusion et le basculement première personne sur une seule entité caméra.
`R1-009` ajoute le document de végétation et un scatter logique déterministe borné : 8 282
placements, dont 704 arbres, 1 228 arbustes, 1 282 fougères et 5 068 patches d'herbe, pour le golden
de cache `82995caa4f17c315cdc42542b255e50945dc70b2e3c959a97b818ebbd60ceb98`.
`R1-010` à `R1-013` et `R1-015` à `R1-018` projettent ce cache en représentations procédurales
proches/moyennes, HLOD et 64 lots d'herbe, avec interaction joueur, culling observé et ombres CSM
bornées. Le vent shader `R1-014` est prouvé en natif, mais reste ouvert sans lancement WebGPU ;
`R1-032` est couvert par 150 échantillons dynamiques alignés sans retard sur la pose post-physique.
Voir
`docs/evidence/gate1/R1_001_TERRAIN_DOCUMENT.md` et
`docs/evidence/gate1/R1_002_005_008_TERRAIN_RENDERING.md`, puis
`docs/evidence/gate1/R1_006_TERRAIN_PHYSICS.md` et
`docs/evidence/gate1/R1_007_TERRAIN_PARTIAL_UPDATE.md`, enfin
`docs/evidence/gate1/R1_027_031_PLAYER_CAMERA.md` et
`docs/evidence/gate1/R1_009_VEGETATION_SCATTER.md`, puis
`docs/evidence/gate1/R1_010_018_VEGETATION_RENDERING.md`. Le lot joueur/caméra est qualifié localement
sous ADR-0006 par 135/135 tests workspace et un probe natif ECS/Rapier ; `R1-009` est qualifié par
58/58 tests ciblés et ses deux goldens ; le lot de rendu végétation est qualifié par 92/92 tests
engine/runtime, Clippy strict, compilation WASM, probes natifs, captures et métriques locales.
`R1-014`, les performances finales et la revue comparative humaine restent dus. Gate 1 reste ouverte.

`R1-019` à `R1-021` disposent maintenant d'un profil v1 strict et d'une projection native
Atmosphere/HDR/soleil/brouillard de distance. Les captures overview et hauteur humaine, le schéma
métrique v5 et le smoke RX 7800 XT passent localement. `R1-021` choisit explicitement
l'approximation de distance tant que la volumétrie n'est pas qualifiée ; les trois items restent
ouverts jusqu'au lancement réel du même profil dans le Studio WebGPU. Voir
`docs/evidence/gate1/R1_019_021_ENVIRONMENT.md`.

## But

Prouver que le nouveau socle produit un environnement plus convaincant que la base historique et au
moins aussi convaincant que la baseline Cadle sur les critères retenus, tout en tenant 60 FPS sur PC
milieu de gamme.

## Scène de référence

Créer `examples/visual_valley` avec seed et paramètres fixés :

```text
surface                 512 × 512 m
profil                   vallée forestière naturelle
terrain                  relief, chemin, rivière et berge
matériaux                herbe, terre, roche minimum
végétation               arbres, arbustes, fougères, herbe
météo                    clair + pluie de preuve
éclairage                soleil, ciel, atmosphère, brouillard
personnage               capsule ou modèle gouverné, taille humaine
caméra                    troisième personne, option première personne
physique                  sol, rochers, arbres proches, eau bornée
qualité                   Low / Medium / High / Ultra
```

## R1-A — Terrain

- [x] `R1-001` Document de terrain versionné.
- [x] `R1-002` Découpage en chunks.
- [x] `R1-003` Mesh et normales.
- [x] `R1-004` Trois matériaux PBR minimum.
- [x] `R1-005` Blend par masques.
- [x] `R1-006` collisions locales.
- [x] `R1-007` mise à jour partielle — 97/97 tests workspace et probe natif ECS/Rapier PASS local.
- [x] `R1-008` debug chunks/LOD.

## R1-B — Végétation GPU

Préparation de diversité du catalogue gouverné : v2 explicite, sélection stable par
identité sans déplacement du scatter ; cinq tests ciblés PASS. Raccord au renderer,
LOD et preuves visuelles restent à faire ; v2 refusée à l'installation jusque-là.
Voir `docs/evidence/gate1/R1_FOREST_VARIANT_SELECTION.md`. Aucun critère fermé.

Raccord effectué ensuite : clés distinctes par variante, sélection commune aux trois
LOD, conservation des poses/meshes/colliders au cycle High/Low/High testée. Clippy
workspace strict PASS, candidat natif v2 réellement lancé/capturé et inspecté ;
deux silhouettes visibles. Rendu sombre du conifère et coût comparatif non qualifiés,
Web v2 encore dû. Ancien catalogue et sources de placement conservés.

Rejeu Web v2 ensuite effectué : Clippy Web/Trunk PASS, deux captures Chrome
inspectées et cycle High/Low/High documenté avec six compteurs restaurés, sources
et temps constants, kit prêt. Pas de fluidité, coût cible ou acceptation artistique
déduits. Voir `R1_FOREST_VARIANT_SELECTION.md` ; Gate 1 reste ouverte.

Coût ensuite mesuré (A/B/B/A, même binaire/scène/High, RX 7800 XT) : catalogue
mixte -3,59 à -6,09 % de FPS moyens et +~84,2 Mio alloués. Deux paires locales,
pas de qualification cible. Ne pas promouvoir ce candidat sans travail sur les
dérivés/LOD et leur aspect ; fonctionnalité générique et ancienne v1 conservées.

- [x] `R1-009` Scatter déterministe — 58/58 tests ciblés et goldens document/cache PASS local.
- [x] `R1-010` Instances regroupées — handles partagés, éligibles au batching Bevy sans faux compteur de draws.
- [x] `R1-011` LOD arbres — near/medium distincts et plages complémentaires testées.
  Raccord physique complémentaire : 636 projections de tronc depuis les profils canoniques,
  activation par distance indépendante des LOD et conservation pendant le cycle de qualité ;
  399 tests workspace, Clippy et check WASM PASS. La protection logique des placements ne
  prouvait pas auparavant leur matérialisation Rapier. Voir `docs/evidence/gate1/R1_TREE_TRUNK_COLLISIONS.md`.
  Non-régression native avec kit : pont traversé, 518/518 ticks au sol, 435 contacts, sortie 0.
- [x] `R1-012` imposteurs ou HLOD lointain — 64 meshes HLOD de chunk observés.
  Buissons/fougères : losanges remplacés par contours bas à six points, enveloppe de
  fougère rapprochée du mesh proche ; racines/placements/distances conservés. Deux vues
  natives inspectées, silhouettes toujours prototypales. +9 064 triangles CPU préparés,
  aucune entité ajoutée ; pas d'optimisation ou de qualification artistique revendiquée.
  Voir `docs/evidence/gate1/R1_LOW_GROWTH_HLOD.md`.
  Validation : 462 tests workspace PASS / 1 timing ignoré ; test ciblé et Clippy strict
  workspace/WASM PASS après correction de forme du test, sans changement supplémentaire du rendu.
  Correctif local : silhouettes de troncs/couronnes à la place des rectangles opaques ;
  capture native et décompte géométrique dans `docs/evidence/gate1/R1_HLOD_TREE_SILHOUETTES.md`.
  Essai suivant de HLOD ramifié 3D (58 puis 90 triangles/arbre) rejeté sur captures : feuillage
  trop clairsemé malgré les tests passants. Rendu précédent rétabli ; ne pas relancer ces variantes
  comme amélioration acquise. Voir `docs/evidence/gate1/R1_HLOD_3D_REJECTED.md`.
  Les pointillés restent non résolus. Essai de sortie WGSL invariante rejeté pour erreur de
  composition, shader restauré et runtime revérifié :
  `docs/evidence/gate1/R1_VEGETATION_INVARIANT_REJECTED.md`.
  Correctif suivant : motif LOD conservé, mais prepass de normales Bevy pour rejeter les
  mêmes pixels en profondeur et en couleur. Trous noirs corrigés sur capture native ;
  coût mesuré et lancement Chrome/WebGPU observé dans `docs/evidence/gate1/R1_VEGETATION_DITHER_DEPTH_FIX.md`.
  Le relevé WebGPU (29,70 FPS moyens, 2048 × 1157) ne valide pas la cible de performance ;
  revue humaine et archivage durable de la capture Chrome restent ouverts.
  Correction suivante : fondus High 64–72/172–180 m, racine par placement utilisée par le shader
  HLOD et culling CPU de lot conservateur. Sources, poses et géométrie inchangées ; 407 tests,
  Clippy, check WASM et deux captures natives PASS. Arbres proches/moyens plus pleins, mais certains
  pointillés lointains persistent ; mouvement continu, WebGPU du lot et revue humaine non qualifiés.
  Voir `docs/evidence/gate1/R1_VEGETATION_LOD_ANCHORS.md`.
  Rejeu Chrome du build courant ensuite effectué : rendu inspecté, kit prêt et parcours au
  premier plan terminé (518 ticks au sol, 434 contacts, 42,528 m), après un échec watchdog
  conservé. Canvas 1408 × 797, capture finale non archivée à cause de l'outil ; ni benchmark
  ni revue humaine. Voir `docs/evidence/gate1/R1_VEGETATION_WEB_REPLAY.md`.
- [x] `R1-013` herbe dense sans entité par brin — 162 176 brins dans 64 lots.
  Stabilité corrigée le 7 septembre : variation liée à l'ID canonique, plus au rang du batch.
  Permutation, suppression de touffe voisine et préfixe de budget testés bit à bit ; 561 tests
  workspace PASS / 1 timing ignoré, Clippy/WASM PASS. Dimensions/densité inchangées, pas de
  qualification artistique ou d'édition Gate 2. Voir `R1_GRASS_STABLE_VARIATION.md` dans les preuves.
  Build et smoke natif High PASS, capture FHD inspectée, aucun ERROR ; brins encore larges,
  finesse/couverture à améliorer. Ni benchmark cible ni rendu Web nouveau qualifié par ce lot.
  Candidat suivant : brins 2,5–5,5 cm et rayon 0,8 m avant échelle, budgets et placements
  inchangés. Capture native FHD inspectée : finesse locale améliorée, couverture/naturel
  non qualifiés. 561 tests workspace PASS / 1 timing ignoré, Clippy/WASM/fmt PASS.
  Voir `docs/evidence/gate1/R1_GRASS_SLENDER_TUFTS.md` pour les contrôles.
- [x] `R1-014` vent shader — preuve native et lancement WebGPU acquis ; paire temporelle
  Studio du 9 septembre inspectée, variation localisée dans la végétation proche.
  La mention antérieure « WebGPU non lancé » est levée. Cette paire non contrôlée
  ne suffit pas seule au vent lointain. Complément du 10 septembre : isolation native
  caméra fixe A/B/A à 197 m, physique prête, effet/retour/témoin dans les seuils
  annoncés, images inspectées. Critère fonctionnel acquis dans cette portée ; pas
  qualification artistique ni temporelle exhaustive des transitions de la scène.
  Voir `../evidence/gate1/R1_MOTION_POINT3.md`, section vent lointain, et les preuves
  historiques `R1_FOLIAGE_CLUSTER_ATLAS.md` et `R1_FAR_WIND_ISOLATION.md`.
- [x] `R1-015` interaction locale avec personnage — champ borné aligné sur la pose post-physique.
- [x] `R1-016` culling par chunks — instantané cohérent après 241 observations caméra/range.
- [x] `R1-017` réglage des ombres par distance — CSM 128 m et règles caster/opt-out observés.
- [x] `R1-018` métriques instances visibles/cullées — schéma v4, draw calls explicitement indisponibles.

## R1-C — Atmosphère et eau

- [ ] `R1-019` ciel et soleil cohérents — projection native et aperçus Chrome exécutés ;
  volume nuageux et IBL visibles, cycle 1→0→1 observé. Cohérence artistique finale,
  raccordement aux profils et qualification globale encore dus. Voir `R1_CLOUD_LAYER_PREVIEW.md`.
  Préparation du raccord pluie/soleil (7 septembre) : contrat optionnel `sunlight`, transition
  indépendante du séchage et anciens hashes conservés ; 549 tests workspace PASS / 1 ignored.
  Aucun soleil modifié par ce contrat seul ; adapter et preuve GPU encore dus. Voir
  `docs/evidence/gate1/R1_WEATHER_SUNLIGHT_CONTRACT.md`.
  Raccord moteur suivant : état lumineux distinct, projection sur le soleil de même identité,
  restitution au retrait et refus d'acquittement en cas d'erreur. 553 tests workspace PASS /
  1 ignored, Clippy/WASM PASS. Aucun profil existant activé ; cycle GPU/visuel non qualifié.
  Voir `docs/evidence/gate1/R1_WEATHER_SUNLIGHT_ENGINE.md` ; aucune gate clôturée.
  Profil candidat externe désormais chargeable sans réassigner sa révision : 71 tests runtime,
  Clippy/build natif PASS, refus réel avant renderer et trois captures statiques inspectées.
  Atténuation visible, mais facteur synthétique 0,25 trop sombre, non promu ; cycle animé/Web
  toujours dus. Voir `docs/evidence/gate1/R1_WEATHER_INPUT_NATIVE.md`. Gate 1 reste ouverte.
  Cycle natif suivant : clair/pluie/clair dans un même processus, transitions CPU progressives,
  retour 55000 lux avant séchage puis trois readbacks inspectés ; candidat facteur 0,65 non promu.
  73 tests runtime, Clippy/build natif PASS. Vidéo/fluide, Web/Tauri et artistique non qualifiés ;
  voir `docs/evidence/gate1/R1_WEATHER_NATIVE_CYCLE.md`. Pas de passage de gate.
  Raccord Studio Web ensuite : candidat chargé avant Bevy, manifeste aligné, cycle par boutons
  55000 → 35750 → 55000 lx, source inchangée, refus de révision et retour embarqué vérifiés.
  42 tests Studio, Clippy natif/Web et build dev PASS ; Tauri, vidéo et artistique non qualifiés.
  Voir `docs/evidence/gate1/R1_STUDIO_WEATHER_INPUT.md`. Aucun profil canonique promu.
  Correctif local du 2026-09-05 : éclairage indirect du ciel Bevy raccordé, gain physique 1,
  carte 256 × 256 ; capture native inspectée et fallback testé, sans qualification WebGPU.
  Voir `docs/evidence/gate1/R1_ENVIRONMENT_WHITE_BALANCE_IBL.md`.
- [ ] `R1-020` brouillard atmosphérique — preuves native et Web/Tauri partielles acquises ;
  qualification comparative finale due, pas absence de lancement Web.
- [ ] `R1-021` brouillard volumétrique ou approximation mesurée — approximation PASS native,
  cycle Tauri du 9 septembre archivé : High actif, fallback Low (0 pas demandés),
  retour High (64/128 m), désactivation exacte. Cinq JSON/deux PNG inspectés,
  54 tests Studio/Clippy/builds PASS, essai fermé. `R1_STUDIO_ATMOSPHERE_CYCLE.md`.
  Qualité finale, mesure comparable et profils complets dus ; ne pas rejouer ce cycle.
- [ ] `R1-022` eau de rivière — raccord fonctionnel intégré, finition visuelle non qualifiée.
  État courant vérifié le 2026-09-06 : la recette embarquée révision 3 conserve le relief riverain
  autorisé et intégré en révision 2 (hauteurs explicites, coupe 12 m, remblai 40 m, raccord 120 m).
  Le test de cette recette contrôle les centres d'eau entre 0,14 et 3 m au-dessus du terrain ;
  la preuve native de promotion mesurait 0,859 à 2,8 m sur les trois voies du mesh.
  Voir `docs/evidence/gate1/R1_RIVER_VALLEY_REGRADING.md` et R1-061.
  L'enfouissement et le candidat de canyon à coupe 40 m appartiennent aux essais historiques,
  pas à la source courante : `docs/evidence/gate1/R1_WORLDSKETCH_WATER_ALIGNMENT.md`.
  Restent la finition des berges, l'hydrologie/ruisseau secondaire et la revue artistique ;
  ne pas demander à nouveau l'autorisation de la correction déjà intégrée.
- [ ] `R1-023` réflexion/réfraction selon profil.
  9 septembre : rayon réfracté raccordé au candidat forward (High 24 pas/3,2 m,
  Ultra 40/4 m, approximation conservée sur miss). 41 tests eau, build et smoke
  natif PASS ; capture éloignée, pas de qualification optique ni Studio/coût.
  Voir `docs/evidence/gate1/R1_WATER_FORWARD_REFLECTION.md` ; critère ouvert.
  Configuration initiale des caméras corrigée : l'eau ne réécrit plus SSR/transmission/MSAA des
  auxiliaires inactives présentes dès le démarrage ; ambiguïté des vues conservée en non-appliqué.
  18 tests eau et Clippy/WASM passent. Réfraction écran toujours non implémentée ; ses budgets
  de parcours ne sont pas les couches de transmission Bevy. Voir le complément de
  `docs/evidence/gate1/R1_040_WATER_ACTIVE_CAMERA.md`.
- [ ] `R1-024` pluie.
  Capture native High inspectée le 2026-09-05 : 8 192 gouttes dans un lot, pluie visible ;
  animation temporelle, Studio WebGPU et revue humaine dus. Voir
  `docs/evidence/gate1/R1_024_026_WEATHER_NATIVE.md`.
  Lancement Chrome réel clair/pluie observé sur la vallée embarquée (pas le candidat WorldSketch),
  hashes alignés ; performance dev insuffisante et archivage des captures restant dû. Voir
  `docs/evidence/gate1/R1_STUDIO_WEBGPU_LAUNCH_20260905.md`. Ne vaut pas qualification finale Studio.
  Le bandeau de résolution constant a ensuite été corrigé : tailles CSS/rendu distinguées,
  dernier relevé explicitement identifié et ledger Chrome archivé dans
  `docs/evidence/gate1/R1_STUDIO_RESOLUTION_LEDGER.json` ; voir la preuve associée
  `docs/evidence/gate1/R1_STUDIO_RESOLUTION.md`.
- [x] `R1-025` wetness réversible — capacité fonctionnelle native et Tauri démontrée.
  Clôture du 8 septembre : cycle natif du 7 septembre, trois captures et journal
  recontrôlés par SHA-256 (`R1_WEATHER_NATIVE_CYCLE.md`), puis cycle Tauri du
  8 septembre (`R1_TAURI_WEATHER_AND_METRICS.md`). Les deux restaurent exactement
  la mouillure à zéro, sans changement des sources surveillées. Tests partagés
  de cycle/conservation des meshes inclus dans les 616 PASS du dernier lot.
  Ce sont deux candidats identifiés, pas une comparaison de parité à entrées
  identiques ni une nouvelle exécution native. Qualité artistique des reflets,
  fluidité et qualification finale du candidat complet restent dues ailleurs.
  Les observations datées ci-dessous expliquent l'historique ; leurs mentions
  de cycle non exécuté ne constituent plus un motif de le rejouer à l'identique.
  Ajustement partagé du 6 septembre : facteur de rugosité défini dans l'espace alpha
  microfacette, conversion perceptuelle commune terrain/berges/végétation. Recettes,
  placements et état sec conservés ; deux vues natives comparées : reflets pluie atténués
  mais non résolus artistiquement. 461 tests workspace PASS / 1 timing ignoré, Clippy
  workspace et WASM PASS. Pas de nouveau rendu Web ni de qualification de gate.
  Preuve : `docs/evidence/gate1/R1_024_026_WEATHER_NATIVE.md`, mise à jour du 6 septembre.
  Projection native mouillée observée ; cycle pluie/sec répété dans les systèmes Bevy headless
  et conservation des meshes contrôlés. Ne vaut pas encore cycle visuel natif/Studio.
  Complément Studio : boutons d'aperçu clair/pluie raccordés sans rechargement, cycle Chrome
  observé avec wetness 0 → 0,78 → 0 exacte et post-traitement clair restauré. Hash conservé,
  un seul démarrage ; revue humaine/archivage et cycle natif final restent dus. Voir
  `docs/evidence/gate1/R1_STUDIO_WEATHER_PREVIEW.md` et son extraction JSON.
- [ ] `R1-026` bloom et color grading bornés.
  Conversion des contrôles température/teinte normalisés vers CIE xy corrigée ; 299 tests locaux
  PASS et capture claire inspectée. Pluie, Studio et revue artistique restent à qualifier.

## R1-D — Personnage et caméra

- [x] `R1-027` contrôleur personnage Rapier — profil v1 strict, commandes séquencées et probe natif.
- [x] `R1-028` pente et marches — seuils, glisse, saut et atterrissage couverts automatiquement.
- [x] `R1-029` caméra troisième personne — entité stable, pose, FOV et suivi profilés.
- [x] `R1-030` occlusion caméra — sphere cast, exclusions, rapprochement et récupération monotone.
- [x] `R1-031` option première personne — même entité, visibilité, hauteur d'yeux et retour 3P vérifiés.
  Complément Studio local : contrôles raccordés au canvas sous capture/focus, caméra première
  personne et libération Échap observées dans Chrome, annulation des commandes testée.
  Déplacement prolongé navigateur et revue humaine restent dus ; voir
  `docs/evidence/gate1/R1_STUDIO_EXPLORATION.md`.
  Essai Tauri du 8 septembre : capture du pointeur observée après clic physique,
  mais bascule clavier non démontrée avec le pilotage natif. Aucun défaut moteur
  établi, aucune promotion de la preuve Chrome en preuve Tauri ; constat et
  nettoyage consignés dans le même rapport. Ne pas rejouer cet essai inchangé.
- [x] `R1-032` interaction végétation — 150/150 directions alignées, erreur de centre maximale 0 m.
- [ ] `R1-033` animation minimale si asset disponible.

## R1-E — Performance

- [x] `R1-034` overlay CPU/GPU — natif PASS ; GPU Studio WAIVED ADR-0012.
  Clôture locale du 10 septembre : nouvel overlay natif du candidat courant
  capturé/inspecté, mesures CPU/GPU exportées ; CPU Studio observé auparavant.
  Voir `../evidence/gate1/R1_MEASURES_POINT5.md`. Historique avant clôture ci-dessous.
  État courant : partie temps GPU Studio `WAIVED / ACCEPTED` sous ADR-0012 ;
  overlay CPU et preuves natives conservés ; le critère parent a depuis été clos
  sur les preuves complémentaires du 10 septembre, pas par la seule dérogation.
  Temps CPU/GPU de la passe opaque exposés en natif, valeurs indisponibles explicites,
  quatre tests ciblés et capture Full HD vérifiés. Ni temps total de frame ni qualification
  Studio déduits ; voir `docs/evidence/gate1/R1_035_036_LIVE_FRAME_OVERLAY.md`.
- [x] `R1-035` frame histogram — calcul partagé, export natif/Web et overlay Tauri exercés.
- [x] `R1-036` moyenne, p95, 1 % low — capacités de mesure livrées ; seuils de performance non acquis.
  Clôture technique le 8 septembre : overlay Tauri 240 images réellement affiché/capturé,
  export JSON vérifié et tests du calcul inclus dans les 616 tests workspace PASS.
  Ne pas confondre mesures disponibles et performances satisfaisantes : R1-041 et les
  seuils finaux restent ouverts. Voir `R1_035_036_LIVE_FRAME_OVERLAY.md`.
  Distribution live ajoutée à l'overlay natif : fenêtre bornée à 240 images, histogramme,
  moyenne/p95/1 % low, calcul partagé avec les rapports. Deux tests et capture Full HD vérifiés ;
  défaut typographique de police encore visible, Studio non rejoué, aucun benchmark qualifiant.
  Voir `docs/evidence/gate1/R1_035_036_LIVE_FRAME_OVERLAY.md`.
  Fallback de police ensuite corrigé à la seule frontière d'affichage : 145 tests engine PASS,
  nouvelle capture Full HD lisible sans carrés. Studio et qualification globale toujours ouverts.
- [x] `R1-037` draw calls, triangles, instances — capture native PASS ; Studio WAIVED ADR-0012.
  Clôture locale du 10 septembre : capture intégrée/replay natif épinglés et
  revérifiés, 1002 draws / 6424 instances / 9325611 triangles soumis sur une frame.
  Pas un compteur global d'overlay ; voie externe ADR-0004, dérogation Studio
  explicite. Voir `../evidence/gate1/R1_MEASURES_POINT5.md`. Historique ci-dessous.
  État courant : mesures/capture GPU Studio `WAIVED / ACCEPTED` sous ADR-0012 ;
  qualification native maintenue. La couverture WebGPU exigée historiquement
  ci-dessous n'est plus un blocage autonome pour Gate 1.
  9 septembre : extraction optionnelle des durées GPU de la capture historique
  existante, replay PASS, 2042 résultats feuilles et couverture des 1780 draws/
  dispatchs. 34 tests outillage PASS, processus fermé. Voir
  `R1_RENDERDOC_GPU_DURATIONS.md` : sommes de replay, pas FPS ni mesures du
  candidat courant ; ne ferme pas l'instrumentation globale ou la gate.
  8 septembre : analyseur/replay intégrés, 28 tests synthétiques PASS et capture Vulkan
  réelle reçue du candidat courant (frame 305, 848 464 512 octets, hash vérifié).
  Relecture isolée ensuite PASS après mémoire disponible accrue : 1 641 draws,
  3 065 instances et 5 755 071 triangles soumis toutes passes, un Present, Vulkan
  non dégradé. Recalcul CLI identique et processus terminé. Échecs antérieurs
  conservés (crash puis garde-fou mémoire) ; pas de nouvelle capture nécessaire.
  Seconde acquisition/relecture indépendante PASS, frame 304, onze pins et arguments
  identiques hors sortie : mêmes draws/instances, +2 triangles uniquement en passe UI ;
  dix-sept autres passes aux comptages identiques. Répétition native acquise sur ce
  candidat, sans qualification des pixels ; aucun processus de test restant.
  Couverture de chaque backend qualifié toujours due selon ADR-0004 ;
  un reçu atomique est une solution de traçabilité envisagée, pas un critère de gate
  supplémentaire imposé par cet ADR. Pas de compteur global d'overlay ou de
  qualification implicite Web. Preuve et chemins exacts :
  `docs/evidence/gate1/R1_037_RECOVERED_TOOLING.md`.
- [x] `R1-038` VRAM estimée — allocateur natif PASS ; Studio WAIVED ADR-0012.
  Clôture locale du 10 septembre : candidat intégré courant, 1403996632 octets
  alloués / 1838633268 réservés ; estimation allocateur, pas résidence réelle.
  Voir `../evidence/gate1/R1_MEASURES_POINT5.md`. Historique avant clôture ci-dessous.
  État courant : mesure/estimation GPU Studio `WAIVED / ACCEPTED` sous ADR-0012 ;
  estimation native et distinction alloué/réservé maintenues, aucune valeur inventée.
  Rapport allocateur GPU ajouté aux diagnostics : alloué/réservé séparés, relevé toutes les
  cinq secondes, indisponibilité explicite. Vulkan natif avec kit/forêt : 847,20 Mio alloués,
  1 127,94 Mio réservés ; pas la VRAM résidente ni le total du processus. 458 tests PASS / 1
  timing ignoré, Clippy et check Studio WASM PASS. Affichage Studio et qualification cible
  restent dus ; voir `docs/evidence/gate1/R1_038_GPU_ALLOCATOR_MEMORY.md`.
  Raccord Studio ensuite vérifié dans Chrome : indisponibilité WebGPU lisible et portée explicite.
  Export JSON reçu et contrôlé, allocator null (pas zéro), aucune acceptation inventée. 33 tests
  Studio et Clippy natif/Web PASS ; affichage/export levés pour ce cas, mesure cible toujours due.
- [x] `R1-039` préchauffage pipelines importants du candidat — PASS local natif/Studio le 10 septembre.
  Service partagé explicite ajouté le 10 septembre : quatre niveaux puis retour
  au profil initial, avec attente du cache/plan GPU et annulation sur choix manuel.
  Préparation native et cycles clair/pluie exécutés ; bouton Studio utilisé,
  deux exports Ready 5/5 et captures clair/pluie acquis, sources préservées,
  processus fermés. 696 tests PASS / un timing ignoré, Clippy natif/Web PASS.
  Voir `docs/evidence/gate1/R1_PROFILE_PREWARM.md`. R1-040 reste ouvert ; aucun
  passage de gate ni préparation de toutes les futures scènes revendiqués.
  Les constats antérieurs ci-dessous précèdent ce service explicite.
  Capture High ombres noire à frame 300 malgré sortie 0, puis correcte au replay à frame 900.
  Un délai fixe ne prouve pas la disponibilité ; voir `R1_040_SHADOW_BUDGETS.md` dans les preuves.
  Garde de capture ajouté : huit observations stables du cache, kit prêt, readback propre et
  échec à l'échéance inchangée. Demande frame 1 réellement différée à 131 dans la vallée,
  capture inspectée ; deadline frame 7 refusée sans PNG. Préchauffage exhaustif toujours ouvert.
  Voir `docs/evidence/gate1/R1_039_CAPTURE_READINESS.md`.
  Complément natif figé du 8 septembre : caméra/horloges stables combinées à 30
  observations distinctes de pipelines prêts (mode ordinaire toujours huit).
  Capture frame 349 avec 113 pipelines prêts, sortie 0 ; 615 tests workspace PASS /
  un timing ignoré. Ce contrôle de disponibilité PNG ne constitue ni un préchauffage
  exhaustif ni un reçu atomique pour RenderDoc ; voir `R1_NATIVE_FIXED_PROOF_VIEW.md`.
- [ ] `R1-040` profil dynamique.
  Correctif de réutilisation des chunks de berge protégés implémenté : géométrie
  pleine résolution conservée lors d'un changement de profil, plages actualisées,
  retouche/protection invalidantes. 711 tests/deux ignorés, Clippy natif/Web PASS.
  `R1_040_BANK_REUSE.md`. Build Studio PASS et cycle PASS observé dans l'interface,
  mais contrôle UI interrompu par Échap avant export. Neuf processus fermés.
  Comparaison de timings et qualification du nouveau candidat non acquises ;
  pas de relance automatique du contrôle UI après cette interruption.
  Diagnostic suivant : cache des payloads végétaux identiques non retenu,
  seulement 4/132 et moins de 0,14 % des sommets ; un test de sonde et Clippy
  moteur PASS. `R1_040_VERTEX_PAYLOAD_PROBE.md`. Aucun gain ni changement de
  production. Ne pas confondre les morph targets terrain avec un LOD0 invariant.
  **État courant du 11 septembre : intégration fonctionnelle acquise, cycle
  Studio préchauffé High/Low/High désormais PASS en déplacement**, trois reçus
  GPU concordants, dernier High prêt à 8,4438 s. Préparation bornée 4 ms souples /
  huit étapes, 710 tests PASS ; `R1_040_PREPARATION_BUDGET.md`. Le nouvel index
  du dossier final conserve cette preuve et ses sources. Reste la qualification
  temporelle globale (pics 147,2/271 ms), pas la correction de l'ancien cycle
  manquant. Ne pas rouvrir la campagne FPS du point 2 dérogé.
  **Historique supersédé sur ces seuls points :**
  Bornes forestières partagées par appel implémentées/testées : CPU forêt
  ~320 ms → ~24 ms, sources et collisions préservées. Cycle mobile toujours
  FAILED, pics 216,6/268,8 ms ; préparation végétale ~176–183 ms à traiter.
  Voir `R1_040_FOREST_BOUNDS_REUSE.md`, 706 tests PASS / deux sondes ignorées.
  Cycle après préchauffage acquis : readiness High retour à 10,0595 s,
  après fin de marche ; FAILED. Pics persistants 435,6/562,1 ms, pas de PASS
  de fluidité. Prochaine cible : calculs répétés des bornes par mesh dans le
  projecteur forestier ; `R1_040_STUDIO_PREWARMED_MOTION.md`.
  Trace CPU Web acquise : commit végétal 7,7–12,5 ms, forêt inclusive
  333,7–341,1 ms ; pauses 5,9–7,8 s hors des phases mesurées. High retour prêt
  après la marche, cycle FAILED. Distinguer froid/préchauffé avant la correction
  suivante ; pas de nouvelle optimisation aveugle du commit végétal.
  Voir `R1_040_STUDIO_PHASE_TRACE.md`, 704 tests PASS / deux sondes ignorées.
  Studio courant acquis le 11 septembre : traversée 518/518 ticks au sol,
  High puis Low prêts en déplacement, troisième étape High absente avant
  fin du parcours ; watchdog FAILED malgré High prêt à l'arrêt. Relevé global
  max 7,35 s, pas d'attribution à une phase. JSON/PNG inspectés, huit processus
  fermés ; `R1_040_STUDIO_MOTION_20260911.md`. Isoler les phases Web avant
  correction ; ne pas transformer cet échec en PASS de mouvement.
  11 septembre : application végétale CPU isolée par une sonde explicite,
  14–17 ms contre 317–871 ms de préparation synchrone native, un cycle PASS
  sur les invariants. Ni Web ni GPU ni forêt gouvernée mesurés par cette sonde.
  Aucun refactoring de production engagé sur ce seul relevé ; qualification
  temporelle Web intégrée toujours due. Voir `R1_040_CPU_COMMIT_PROBE.md`.
  **État courant du 9 septembre : raccord fonctionnel terminé**, forêt gouvernée
  et eau forward comprises. Cycles natif/Tauri des quatre niveaux et retour High :
  sept domaines et GPU acquittés, paramètres restaurés, sources préservées.
  Remontée automatique native Low→Medium ; descente Studio High→Medium→Low,
  puis High manuel qui désactive l'automatisme, vérifiés en session réelle.
  Reste la qualification de fluidité des transitions, pas une nouvelle intégration
  des profils. Voir `../evidence/gate1/R1_EFFECTIVE_PROFILES_INTEGRATION.md`.
  Les paragraphes suivants conservent les étapes historiques, remplacées sur ces points.
  Rejeu courant Chrome avec kit : High/Low/Medium/Ultra/High, puis aperçu brume
  High/Low/High/arrêt exécutés. Une lecture Ultra expire avant stabilisation ; pas de
  qualification de fluidité. Budget de brume Low erroné dans le ledger corrigé et
  testé en ECS ; rendu Web de la brume observé, pas accepté artistiquement.
  Voir `docs/evidence/gate1/R1_STUDIO_QUALITY_FOG_CYCLE.md`. Adaptation toujours non raccordée.
  Préparation des brins accélérée sans réduction de scène : hauteur triangulée seule,
  bit-identique au chemin complet. Trois mesures CPU natives High 2,84–2,91 s →
  0,56–0,68 s ; pas une mesure de transition Web ou de frames. Voir
  `docs/evidence/gate1/R1_GRASS_HEIGHT_ONLY_PREPARATION.md`.
  Nouveau build ensuite exécuté dans Chrome avec kit : High → Ultra → High, budgets
  observés après 2,13 s puis 1,23 s ; retour High inspecté. Mesures ponctuelles,
  pas une qualification de fluidité, de FPS ou de preset complet.
  Décisions pures raccordées au document de politique : hystérésis, temporisation, bornes,
  benchmark High verrouillé et confirmation obligatoire avant de déclarer un niveau appliqué.
  337 tests workspace PASS. Les budgets des sept domaines ne sont pas encore appliqués par
  ce contrôleur dans Runtime/Studio ; R1-040 reste ouvert. Voir
  `docs/evidence/gate1/R1_040_QUALITY_DECISIONS.md`.
  Choix global initial `--quality` ensuite raccordé aux sept domaines, Low/Ultra natifs sous pluie
  inspectés sans erreur GPU, 379 tests workspace PASS. Demande globale explicitement partielle,
  pas d'acquittement adaptatif ou de contrôle Studio. Voir `docs/evidence/gate1/R1_040_GLOBAL_INITIAL_QUALITY.md`.
  Coordinateur global à chaud ensuite raccordé : cycle natif High/Low/Medium/Ultra/High sous pluie
  inspecté, code 0, zéro ERROR ; cinq domaines CPU projetés et deux partiels, jamais de confirmation
  complète. Adaptation et Studio ouverts ; voir `docs/evidence/gate1/R1_040_GLOBAL_QUALITY_CYCLE.md`.
  Studio ensuite raccordé aux demandes globales : quatre boutons, cycle navigateur réel et
  coexistence météo vérifiés sans nouveau démarrage. Deux captures inspectées ; aucun preset complet
  annoncé. Adaptation et qualification Full HD/Tauri ouvertes ; voir `docs/evidence/gate1/R1_040_STUDIO_QUALITY.md`.
  Ledger Runtime v8 : état global optionnel collecté après observation, cas Gate 0 absent et High
  partiel vérifiés en natif (addendum global). Pas d'adaptation ou passage de gate déduit.
  Volumétrie : préparation bornée et raccordement ECS opt-in testés, désactivés par défaut ; qualification
  native/WebGPU toujours ouverte (`docs/evidence/gate1/R1_040_VOLUMETRIC_PREPARATION.md`).
  CLI/soleil raccordés ensuite ; smoke natif réussi mais rendu rejeté (frontière dure et
  assombrissement). Voir `docs/evidence/gate1/R1_040_VOLUMETRIC_NATIVE.md`. Effet toujours opt-in.
  Correctif de bords ensuite capturé : ligne du ciel supprimée sur la vue, assombrissement encore
  non accepté. Texture dérivée sans asset externe ; aucune fermeture de critère volumétrique.
  Portée ensuite bornée par les cascades : comparaison au sol moins sombre, volume toujours actif
  à 128 m en High. Métriques de portée effective ajoutées ; qualification globale toujours ouverte.
  Cycle natif High/Low/Medium/Ultra/High ensuite vérifié, cinq captures inspectées, zéro ERROR,
  sources conservées ; voir `docs/evidence/gate1/R1_040_VOLUMETRIC_CYCLE.md`. WebGPU reste ouvert.
  Studio/WebGPU : activation et cycle High/Low/High/off réels vérifiés ensuite, sans reconstruction
  du monde. Preuve limitée à 768×606 ; Full HD/Tauri et mouvement ouverts. Voir
  `docs/evidence/gate1/R1_040_VOLUMETRIC_STUDIO.md`. Aucun preset complet validé.
  Mouvement : scénario natif joueur/caméra PASS et suivi CPU sans retard de schedule testé.
  Pas de qualification visuelle temporelle (`docs/evidence/gate1/R1_040_VOLUMETRIC_MOTION.md`).
  Essai interactif Web non validé : WrongDocumentError au pointer lock dans le navigateur intégré.
  Diagnostic d'entrée à exposer ; ne pas présenter cet essai comme une régression volumétrique.
  Diagnostic ensuite exposé et refus réel vérifié dans le Studio ; pas de contournement du focus
  ou du pointer lock. Voir `docs/evidence/gate1/R1_040_STUDIO_INPUT_CAPTURE.md`.
  Chrome au premier plan : capture active, bascules première/troisième personne et Échap ensuite
  vérifiés avec brume High. Marche continue et Full HD/Tauri non déduits de cet essai.
  Complément pluie : remplacement du mesh à bindings/horloge/source constants, choix natif
  `--rain-quality`, cycle ECS et quatre budgets natifs vérifiés (339 tests workspace PASS).
  Captures Low/Ultra inspectées ; aucune qualification du profil global ou du cycle GPU à chaud.
  Ombres également raccordées aux presets : cascades/résolution/distance/contacts, cycle ECS,
  budgets natifs observés et captures Low/Ultra/High tardif inspectées. 344 tests workspace PASS.
  Voir `docs/evidence/gate1/R1_040_SHADOW_BUDGETS.md`. Contrôleur global et sélection Studio ouverts.
  Fondation terrain : six topologies LOD à périmètres identiques, 4032 raccords mixtes vérifiés
  sur la vallée sculptée ; 352 tests workspace PASS pour cette fondation. Voir
  `docs/evidence/gate1/R1_040_TERRAIN_LOD_TOPOLOGY.md`.
  Raccord renderer natif optionnel réalisé ensuite : trois LOD recentrés par chunk, bindings
  canoniques conservés, retouche réelle Rapier et reconstruction de trois seuls chunks prouvées.
  High inspecté après correction d'un désaccord de distance CPU/shader. L'horizon Low a ensuite
  été corrigé : budgets de 896 m, fondu final borné, test couvrant les caméras et bords, capture native
  Low avec relief lointain conservé. Raccord global/Studio et mouvement toujours ouverts.
  Voir `docs/evidence/gate1/R1_040_TERRAIN_LOD_RENDERER.md`. R1-040 reste ouvert.
  Post-traitement raccordé au système météo existant, sans writer concurrent : bloom, grading,
  tonemapping. Cycle ECS avec source/horloge/pluie conservées et Low/High natifs inspectés.
  Voir `docs/evidence/gate1/R1_040_POST_PROCESS_BUDGETS.md`. Ne clôt pas les profils globaux.
  Sous-budget eau SSR raccordé : Low sans SSR, Medium/High/Ultra à 24/48/64 étapes ; cycle ECS
  et Low/Ultra natifs vérifiés, 366 tests workspace PASS. Preset eau toujours partiel (distance,
  réfraction et qualification environnement manquantes), sans acquittement global ni clôture.
  Voir `docs/evidence/gate1/R1_040_WATER_SSR_BUDGETS.md`.
  Environnement : sonde dérivée renouvelable, option Runtime, textures Low 64²/Ultra 512² raccordées
  et captures natives inspectées. Cycle natif High/Low/Medium/Ultra/High ensuite vérifié, après
  correction d'un cache filtré trop petit provoquant une erreur GPU. 373 tests workspace PASS.
  Volumétrie, Studio et qualification des transitions restent ouverts. Voir
  `docs/evidence/gate1/R1_040_ENVIRONMENT_NATIVE_CYCLE.md` et la preuve initiale associée.
  Végétation : sélection pure de densité implémentée, placements protégés et sélections imbriquées
  vérifiés sur WorldSketch rev3 ; raccord initial renderer/LOD/brins ensuite implémenté via CLI.
  Quatre profils testés, Low/Ultra natifs inspectés, 376 tests workspace PASS. Changement à chaud
  ensuite implémenté et testé en ECS ; cycle natif cinq niveaux exécuté sans erreur GPU et PNG
  inspectés. Coût/continuité des transitions et Studio restent ouverts.
  Voir `docs/evidence/gate1/R1_040_VEGETATION_TRANSITION_IN_PROGRESS.md`
  et `docs/evidence/gate1/R1_040_VEGETATION_RENDER_DENSITY.md`
  et `docs/evidence/gate1/R1_040_VEGETATION_INITIAL_QUALITY.md`. Aucun profil global clôturé.
- [x] `R1-041` **WAIVED/ACCEPTED le 10 septembre**, par décision explicite du
  propriétaire sur sa RX 7800 XT. Benchmark RTX 3060/RX 6600 NOT RUN, pas PASS.
  Pas de nouveau run ; seuils non atteints conservés et qualification technique
  différée. Point 2 clôturé par dérogation, autres critères inchangés.
  Historique avant dérogation :
  Point 2 repris le 9 septembre : inventaire réel RX 7800 XT + GPU intégré,
  aucun GPU cible. Benchmark cible NOT RUN. Rapport release historique rehashé
  et recalculé, seuils non satisfaits (51,72 FPS forêt / p95 22,28 ms / 1 % low
  39,90 FPS) ; ce n'est pas le candidat combiné courant. Aucun nouveau run ou
  changement de seuil. Voir `../evidence/gate1/R1_PERFORMANCE_POINT2.md`.
- [x] `R1-042` benchmark RX 7800 XT de développement : trois passages natifs 1080p High fixe,
  5 999 images chacun après préchauffage, candidat historique du 6 septembre (636 arbres), sorties 0. Moyenne
  60,93–61,58 FPS, p95 17,21–17,50 ms (seuil 16,7 ms non atteint), 1 % low 49,80–50,65 FPS.
  Mesure dev opt-level 1, arbre modifié et optiques partielles : ni qualification finale,
  ni extrapolation au GPU cible ou au Studio. Voir `R1_042_DEVELOPMENT_GPU_BENCHMARK.md`.
  Actualisation 8 septembre, candidat 1 662 arbres : A/B/B/A à deux résolutions,
  899 échantillons par essai, 40,39/40,51 FPS en Full HD contre 47,30/47,19 à
  960 × 540. Réduire les pixels ne suffit pas à atteindre 60 FPS ; aucun bottleneck
  CPU précis établi. Essais terminés, processus fermés, candidat Studio inchangé.
  9 septembre : comparaison en marche vivante ABBA sur le kit courant, quatre
  sorties 0. Ombres 4096/4 contre 2048/3 : ~49 → 57 FPS sur le parcours et
  ~32 → 40 FPS sur son dernier tiers forestier ; poses communes à moins de 8 mm.
  `R1_LIVE_WALK_SHADOW_COMPARISON.md` : preuve comparative acquise, 60 FPS non
  atteints, pas de promotion du profil ou qualification GPU cible/Studio.
  Essai suivant alpha exactement nul : quatre parcours ABBA autour de 55 FPS,
  aucun gain reproductible ; raccourci shader et test dédié retirés. Preuves dans
  `R1_FOLIAGE_ALPHA_COST.md`. Ne pas répéter cette piste inchangée ; aucun seuil
  ou critère de gate validé par cet essai.
  Release courant ensuite acquis et comparé en parcours vivant dev/release/release/dev :
  ~54,7 → 62,6 FPS global, ~39,6 → 42,6 FPS forestier ; p95 release ~25,2 ms,
  1 % low ~36 FPS. Le déficit forestier persiste en release. Voir l'actualisation
  du 9 septembre de `R1_RELEASE_PREPARATION.md` ; ombres candidates 2048/3,
  pas qualification High canonique, GPU cible ou Studio. Ne pas refaire cette paire.
- [ ] `R1-043` comparaison native vs Studio WebGPU.
  Complément Medium/Ultra acquis ensuite le 11 septembre : deux exports Studio
  avec reçus GPU et deux PNG inspectés, plans des sept domaines/sources/berges
  comparés au natif, vérificateur PASS. Les quatre niveaux de cette paire sont
  donc comparés fonctionnellement ; huit processus fermés. Snapshots épinglés,
  deux fichiers documentation/test désormais différents dans le worktree.
  `R1_043_CURRENT_NATIVE_STUDIO.md`. Restent les réserves optiques/temporelles
  et la révision finale, pas une nouvelle acquisition Medium/Ultra identique.
  Complément courant du 11 septembre : runtime reconstruit sur les mêmes 283
  sources que le Studio, cycle natif cinq étapes PASS ; plans effectifs High/Low,
  bindings et substitutions comparés aux trois enregistrements mobiles Studio.
  `R1_043_CURRENT_NATIVE_STUDIO.md`. Deux PNG inspectés, deux processus fermés.
  Cette acquisition initiale ne couvrait que High/Low ; Medium/Ultra complétés
  ci-dessus. Aucune équivalence pixel ou fluidité déduite.
  **Parité fonctionnelle acquise sur le candidat du 10 septembre** : sources,
  kit, profils et effets intégrés comparés ; `R1_NATIVE_STUDIO_POINT4.md`.
  Le cycle Studio plus récent est acquis séparément et n'est pas une nouvelle
  paire native/Studio de révision finale. Restent la portée finale et les
  réserves temporelles/comparatives, pas un transport Studio à implémenter.
  L'entrée suivante est historique :
  Complément WorldSketch verrouillé : choix Studio explicite, lancement Chrome réel et cinq
  hashes communs aux métriques natives identiques. Le défaut d'eau enfouie était alors visible et
  mesuré ; cette preuve historique ne qualifie pas la révision 2 ultérieure. Voir
  `docs/evidence/gate1/R1_STUDIO_WORLDSKETCH.md` et son extraction de parité.

## R1-F — Revue visuelle

- [x] `R1-044` acquisition des captures fixes de jour — six vues retenues, Full HD High.
- [x] `R1-045` acquisition des captures sous pluie — six vues appariées, Full HD High.
  Acquisitions du 10 septembre inspectées et revérifiées : douze captures retenues,
  deux vidéos, 89 fichiers épinglés ; aucune nouvelle campagne de rendu nécessaire.
  Voir `R1_FINAL_DOSSIER_POINT7.md`. Ces deux coches portent sur l'acquisition,
  pas sur les revues humaines de matériaux/composition ni le verdict comparatif.
- [ ] `R1-046` vue à hauteur humaine.
  Candidat LOD proche gouverné 24–32 m intégré et capturé : forêt conservée,
  workspace/Clippy/check WASM/build natif PASS, 20,639 → 31,870 FPS statiques.
  Ombres moins lointaines, arrière-plan plus clair : compromis non validé
  artistiquement. Studio ensuite reconstruit/lancé avec ce LOD et le kit corrigé,
  JSON/PNG vérifiés ; ombres High Studio inchangées, noir initial transitoire
  persistant comme défaut de démarrage, pas de performance qualifiée ni de passage de gate.
  Processus fermés ; voir `R1_GOVERNED_NEAR_LOD_CANDIDATE.md`.
  Candidat Studio aux couleurs corrigées assemblé et lancé : Trunk/Tauri PASS,
  six contrôles d'entrée du packaging PASS, kit 20/20 et 1 662 arbres chargés,
  JSON/PNG exportés et vérifiés. Noir initial disparu sans réglage ; warmup non
  qualifié, vue depuis prairie et performance hors protocole cible. Aucun passage
  de gate ; processus fermés. Voir `R1_STUDIO_COLORSPACE_INTEGRATION.md`.
  Cause du noir différenciée : mauvais encodage sRGB des textures procédurales,
  writer partagé corrigé et round-trip Blender rouge puis vert. Dérivé local de
  deux arbres, huit images couleur seulement, glTF/réimport/capture PASS : troncs
  et feuillage nettement plus lisibles, exposition inchangée. Pas de promotion
  Studio/humaine ni gain performance (~22 FPS overlay). Processus fermés ;
  `R1_PROCEDURAL_TEXTURE_COLORSPACE.md`. Ne plus compenser ce défaut par le ciel.
  Synchronisation complète de la liaison IBL intégrée au moteur partagé :
  régression reproduite puis corrigée, 630 tests PASS / un timing ignoré,
  Clippy strict/check WASM/build natif PASS. Capture réelle : sonde/caméra
  identiques, gain 1,0 ; troncs toujours trop sombres. Ne pas répéter ce contrôle
  inchangé, filtrage diffus/matériaux restent ouverts. Processus fermé, aucune
  acceptation artistique déduite. Voir `R1_SKY_BINDING_INTEGRATION.md`.
  Sous-bois sombre : cubemap brute du ciel lue sur GPU, 393 216 texels HDR
  finis et positifs. Source brute noire écartée ; filtrage/liaison diffuse et
  matériaux non encore prouvés. Aucun gain lumineux ni qualification déduits.
  Voir `R1_SKY_IBL_READBACK.md`, 84 tests natifs + 1 PASS, processus fermé.
  Candidat combiné 2048 / trois cascades / 128 m / contact conservé capturé et
  inspecté après parcours, projection effective conforme, overlay ~21,8 FPS
  non benchmark. Canopée encore trop sombre, preset non promu et Gate 1 ouverte.
  Contact off sans effet majeur ; trois cascades seules 15,716 FPS. Série close,
  pas de répétition inchangée. Voir `R1_SHADOW_BALANCED_CANDIDATE.md`.
  Paramètres isolés : atlas 4096/2048 = 13,000/18,425 FPS, quatre cascades 128 m
  et contact shadows effectivement observés. Portée 96 m seule sans gain ;
  variante 64 m refusée par validation, conservée comme échec. Pas de promotion
  visuelle ni de qualification ; cascades/contact restent à isoler. Voir
  `R1_UNDERSTORY_CPU_PHASES.md`, processus fermés, harnais natif 82 tests + 1 PASS.
  Priorité affinée : profil d'ombres High/Low, autres domaines High, mesure
  statique 13,057/29,891 FPS. Plusieurs paramètres changent, pas de promotion
  artistique ; isoler les budgets d'ombres et exporter leur projection effective.
  Compteurs GPU activés/désactivés sans gain : piste écartée pour cette charge.
  Voir le complément `R1_UNDERSTORY_CPU_PHASES.md`, quatre essais fermés.
  Localisation du coût sous-bois : 72,260 ms hors First–PostUpdate, contre
  3,559 ms dans ces phases, sur 300 frames statiques. Cibler extraction/rendu/
  présentation/attentes ; pas de cause précise ni gain produit établi.
  Voir `R1_UNDERSTORY_CPU_PHASES.md`, instrumentation native testée, essai fermé.
  Parcours natif du 8 septembre via commandes normales, sans téléportation :
  personnage visible dans le sous-bois corrigé, position (-132,50 ; 2,60 ; -203,52),
  arbres proches et cimes hors cadre. Pas de mesure exacte du rapport de hauteur
  ni d'acceptation humaine. L'overlay local tombe vers 13 FPS sous canopée : traiter
  cette charge de proximité, non caractérisée par les essais héroïques à 40 FPS.
  Captures, essais intermédiaires et nettoyage dans `R1_FOREST_REGION_SCALE.md`.
  Candidat forestier du 8 septembre : vue native basse `human_bridge` capturée
  et inspectée sans rebuild, arbres dominants et lisière dense. Coordonnées
  authorées absolues, pas mesure autonome de hauteur des yeux ou personnage
  au pied d'un arbre ; qualification reste ouverte. Voir `R1_FOREST_REGION_SCALE.md`.
- [ ] `R1-047` vue lointaine.
  Vue globale Studio actualisée le 9 septembre avec le bundle courant et 1662 arbres :
  PNG/JSON archivés, sol tacheté et limite du terrain visibles. Pas d'acceptation
  artistique ; ne pas répéter le même état. Voir `R1_065_FIXED_VIEWS.md`.
  Candidat terrain corrigé ensuite : plaques chromatiques larges retirées du shader
  partagé, grain proche conservé sur deux captures natives Full HD. Intégration
  Studio ensuite vérifiée avec exports PNG/JSON et sources inchangées ; autres
  défauts et acceptation restent dus. Voir `R1_TERRAIN_SURFACE_DETAIL.md`.
  Portée végétation ensuite testée : High 640 m au lieu de 384 m, forêt lointaine
  restaurée sur capture native sans ajouter de placements. Coût du smoke court
  66,87→61,57 FPS ; pas qualification de performance. Intégration Studio High
  ensuite construite/lancée, PNG/JSON inspectés, mêmes sources et kit, instance fermée.
  Ultra 768 m configuré, non qualifié visuellement. Voir `R1_FOREST_REGION_SCALE.md`.
- [x] `R1-048` vidéo de déplacement courte : deux MP4 natifs clair/pluie, 271 images Full HD
  chacun, 9,03 s à 30 images/s imposées hors temps réel. Vrai contrôleur et traversée Rapier
  vérifiés ; aucun verdict de performance ni validation humaine. Voir
  `docs/evidence/gate1/R1_048_BRIDGE_MOVEMENT_VIDEO.md`.
- [ ] `R1-049` revue des matériaux.
  Complément sous canopée : A/B/B/A statique après parcours réel, 300 frames/run,
  Full HD High, temps virtuel figé explicitement. BLEND 13,026/13,039 FPS,
  MASK 13,209/13,126 FPS : pas de gain justifiant une promotion, ni qualification
  du jeu vivant. Essai initial vivant refusé par contrôle de mouvement conservé.
  Isoler les coûts restant hors timings partiels ; ne pas répéter alpha/simulation
  inchangés. Preuves, limites et processus fermés dans `R1_FOLIAGE_ALPHA_COST.md`.
  8 septembre : coût transparent identifié dans les mesures du candidat dense
  (6,11 ms GPU natif). Politique d'export MASK optionnelle testée sur trois
  fixtures Blender, dérivé externe réel glTF/réimport contrôlé, 5 tests PASS.
  A/B/B/A natif ensuite exécuté : environ 40 FPS dans les deux modes,
  aucun gain établi. Capture inspectée sans avantage artistique décisif.
  Dérivé non promu, Studio inchangé ; ne pas rejouer ces essais inchangés.
  Voir `R1_FOLIAGE_ALPHA_COST.md` ; aucune qualification déduite.
  Candidat végétal ultérieur : couronnes proches/moyennes ramifiées, placements et HLOD
  préservés. Premier essai trop dégarni rejeté ; candidat densifié inspecté clair/pluie,
  331 tests PASS et compilation WASM. Coût natif sur une paire courte comparable au rendu
  antérieur, sans qualification générale ni acceptation artistique. Voir
  `docs/evidence/gate1/R1_BRANCHED_PROCEDURAL_TREES.md`.
  Normales des troncs/couronnes procéduraux corrigées après test en échec ; 330 tests
  workspace passent, géométrie et placements inchangés. Captures natives avant/après
  vérifiées, sans gain artistique net démontré avec le matériau double face. Le style
  conique reste à améliorer ; voir le complément de `docs/evidence/gate1/R1_HLOD_TREE_SILHOUETTES.md`.
  Candidat de grain de sol albédo/rugosité ajouté via un cache R8 partagé de 16 Kio.
  Captures natives clair/pluie et Chrome réel inspectés ; performance Web non qualifiée
  (dernier p95 17,6 ms à 2048 × 1101). Aucun changement des masques ni asset externe.
  Voir `docs/evidence/gate1/R1_TERRAIN_SURFACE_DETAIL.md`. Revue artistique toujours ouverte.
  Amélioration locale des berges : dégradé de sommets terre/vert, sans texture ou nouveau mesh,
  capture native inspectée. Le résultat reste prototypal et ne clôt pas la revue ; voir
  `docs/evidence/gate1/R1_BANK_MATERIAL_GRADIENT.md`.
- [ ] `R1-050` revue de composition et lisibilité.
  Revue propriétaire du 8 septembre : candidat forêt refusé (arbres trop petits,
  trop espacés, aspect de plaine). Corriger les règles de densité par région
  forestière et l'échelle adulte 5–10 fois celle du personnage (9–18 m pour
  1,80 m), sans réduire celui-ci ni écraser chemins/clairières/retouches.
  Correction intégrée ensuite : recette optionnelle, masque Forest moins
  Buildable et interpolation de densité partagée, arbres 9,04–17,80 m,
  1 662 arbres/colliders déterministes. 619 tests PASS / un timing ignoré,
  Clippy strict PASS et capture native inspectée. Mesure native dev High
  39,77 FPS : objectif non acquis. Studio optimisé construit, lancé et capturé,
  kit 20/20, 1 662 arbres, hashes alignés, PNG/JSON contrôlés, instance fermée.
  Revue artistique et qualification restent dues. Voir `R1_FOREST_REGION_SCALE.md`.
- [ ] `R1-051` comparaison avec la référence WebGPU ayant motivé le reboot.
- [ ] `R1-052` comparaison A/B avec Cadle épinglé sur même machine, résolution et profil équivalent ; séparer impact visuel et coûts par système, car Cadle couvre 2 km² contre 512 × 512 m pour Visual Valley.
  Complément : comparaison intérieure descriptive acquise, FOV Cadle contrôlé
  à 60° comme Forge. Deux fenêtres ~109 FPS/p95 41,8 et 39 ms, capture inspectée,
  huit processus fermés, upstream intact. Voir `R1_052_FOREST_OPTICAL_COMPARISON.md`.
  Budgets et modes de caméra non équivalents ; benchmark apparié et verdict
  toujours non acquis. Les tentatives suivantes sont historiques, ne pas reprendre
  l'acquisition intérieure ou la correction de dérive comme entièrement absentes.
  Dérive ensuite corrigée dans le contrôleur partagé : 701 tests PASS, quatre
  pentes au repos testées, mesure réelle native 300/300 valide et capture séparée
  inspectée. ~17,81 FPS moyens, p95 57,55 ms ; aucune fluidité revendiquée.
  `R1_IDLE_SLOPE_STABILITY.md`. Pairage Cadle et verdict restent dus ; ne pas
  reprendre la dérive au repos comme entièrement non traitée.
  Partie Forge tentée le 11 septembre : garde optique ajusté pour l'immobilité
  avec simulation vivante, cinq tests/Clippy/build PASS. Essai refusé à 167/300
  échantillons pour dérive au repos >2 cm ; pas de benchmark valide. Capture
  intérieure séparée acquise, lenteur visible à l'overlay, processus fermés.
  Voir `R1_052_FORGE_LIVE_COST_20260911.md` ; correction générique de la dérive
  requise avant reprise de cette mesure, sans changer son seuil.
  Nouvelle référence de coût du 11 septembre exécutée : deux fenêtres en forêt
  intérieure, statistiques et deltas bruts conservés, capture inspectée après
  mesure. Moyennes ~100 FPS, p95 43,5/50 ms ; pairage Forge encore dû, aucun verdict.
  Clone canonique vérifié, processus fermés ; `R1_052_CADLE_COST_20260911.md`.
  11 septembre : collecteur corrigé, six tests PASS, index de 89 fichiers exécuté.
  Les deux rapports Cadle ont zéro entrée `stats` ; High est un label, pas une
  équivalence de budgets. Vue forêt intérieure Cadle / extérieure Forge non
  appariée : acquérir les vues équivalentes avant jugement, sans retouche de carte
  induite par ce cadrage. Voir `R1_FINAL_DOSSIER_POINT7.md`. R1-052 reste ouvert.
  Comparaison descriptive exécutée le 2026-09-06 : six vues Cadle forêt/prairie et deux vues
  Forge actuelles Full HD sur RX 7800 XT, sorties sans erreur. Écart visuel important confirmé
  (couverture végétale, arbres, matières, composition) ; priorité au rendu végétal de la scène.
  Pas de benchmark équivalent ni de validation humaine ; R1-052 reste ouvert. Voir
  `docs/evidence/gate1/R1_052_CADLE_VISUAL_GAP.md`.
  Première correction : lames d'herbe courbées, touffes High à 64 brins et fondu terminal
  64–72 m au lieu de 24–72 m, sans portée supplémentaire. Capture native inspectée et 400 tests
  PASS ; amélioration proche visible, couverture globale encore trop faible. Voir le complément
  du même rapport. Ni revue artistique ni R1-052 clôturés.
  Deuxième correction : densité spatiale documentée par zones de 32 m, herbe 4 531 → 9 606
  touffes. Autres poses, exclusions, kit et plafonds conservés ; source végétation modifiée.
  404 tests, Clippy, check WASM et capture native PASS. Couverture naturelle encore insuffisante,
  rendu Chrome du lot non exécuté ; preuves dans le même rapport, gate toujours ouverte.
- [ ] `R1-053` vérification de provenance : aucun asset, nom, lore ou texte Cadle dans Visual Valley.
  Complément du 11 septembre : inventaire/revue agent des textes JSON/Markdown de
  Visual Valley acquis (16 fichiers, 357 chaînes, quatre tests PASS). Labels
  techniques/génériques et mentions documentaires d'interdiction constatés.
  `R1_053_PROVENANCE_CHAIN.md` : pas d'audit global UI, d'image ou de dérivation,
  ni de droit de publication déduit. La revue textuelle de ce sous-dossier n'est
  plus absente ; provenance finale complète et revue humaine restent dues.
  9 septembre : kit colorimétrique courant et copie Studio contrôlés, neuf fichiers
  identiques ; huit images de couleur seules modifiées dans deux arbres, quatre GLB
  inchangés. 45 images/six GLB actuels sans identité binaire avec les 139 fichiers
  Cadle exclus. PASS borné, quatre refus synthétiques PASS ; voir la nouvelle section
  de `R1_053_PROVENANCE_CHAIN.md`. Ne pas répéter à fichiers inchangés ; origine
  indépendante/textes hors GLB non requalifiés, restriction de publication conservée.
  Rev.4 contrôlée le 7 septembre : onze pins d'assemblage, six GLB runtime et six copies
  Web, deux bindings et rapports candidat/réimport concordants. Intégrité actualisée,
  pas de nouvelle preuve indépendante d'origine ni revue de droits ; R1-053 reste ouvert.
  Chaîne candidate runtime actualisée : six GLB et 45 images embarquées vérifiés contre
  leurs sources ; liens recette/scripts/candidat/réimport/catalogue cohérents. Noms techniques
  GLB lus ; aucune identité binaire avec les 139 fichiers Cadle exclus comparés en lecture
  seule. Limites dérivation/droits/revue humaine conservées, aucune promotion du candidat.
  Voir l'actualisation de `R1_053_PROVENANCE_CHAIN.md` ; R1-053 non fermé par ces seuls contrôles.
  Audit local du 2026-09-06 : 41 fichiers vérifiés (6 GLB, 33 textures, 2 références), quatre liens
  recette/générateur/source authorée intacts, aucun doublon exact possible avec les 139 fichiers
  Cadle comparés. Pas de preuve d'absence de dérivation ni de revue complète des noms/lore ;
  statuts de publication toujours restreints. Voir `docs/evidence/gate1/R1_053_PROVENANCE_CHAIN.md`.
- [x] `R1-054` indépendance du dossier upstream vérifiée localement : copie de 190 sources sans
  `upstream` ni `.git`, build natif et 397 tests hors ligne PASS, vallée avec kit et traversée
  518/518 ticks au sol. Clone canonique conservé dans le workspace original ; pas d'isolation OS,
  de packaging ou de lancement Tauri déduits. Voir `docs/evidence/gate1/R1_054_NO_UPSTREAM.md`.
- [ ] `R1-055` rapport Gate 1 avec verdict poursuivre/réorienter/arrêter.
  **Rapport de suivi actualisé le 11 septembre** dans `R1_055_GATE1_STATUS.md` :
  sept blocs distingués entre acquis, dérogé et restant. Index final partiel à
  378 fichiers, cycle mobile Studio acquis ; pas de clôture de gate, revue humaine
  du kit ou révision propre. Les paragraphes suivants restent historiques.
  Suivi actualisé le 9 septembre dans `docs/evidence/gate1/R1_055_GATE1_STATUS.md` :
  poursuivre le candidat complet ; saut d'ombre B corrigé et coût mesuré, raccord
  rapproché Studio déjà observé. Ne pas relancer ces diagnostics comme non traités.
  Profils, finition, qualification finale et conditions humaines/externes restent
  ouverts ; rapport intermédiaire, pas verdict de passage ni ouverture de Gate 2.
  Raccord Studio supplémentaire : activation/retour fixe de l'adaptation testés par clics,
  attente réelle des capacités eau Web affichée sans faux acquittement. 42 tests Studio,
  Clippy Web/WASM et bundle PASS. Voir `docs/evidence/gate1/R1_STUDIO_ADAPTATION_CONTROLS.md`.
  Ne qualifie pas les transitions automatiques Web ni le passage de gate.
  Garde-fou moteur étendu aux vues fixes Studio : adaptation refusée avant/après installation
  caméra, High conservé et retour manuel testé ; 562 tests workspace PASS / 1 timing ignoré,
  Clippy/WASM PASS. Complément dans le même rapport, pas de nouvelle qualification optique.
  Comparateur optique répétées/témoins ajouté : refuse séries courtes/doublons et distingue
  bruit/restauration en synthétique, sans promotion Web. Protocole et limites dans
  `docs/evidence/gate1/R1_WEB_OPTICAL_SAMPLE_PROTOCOL.md` ; série réelle qualifiante encore due.
  Reprise : séries répétées réellement récupérées, caméra native rapprochée chargeable dans
  le Studio sans changement de carte (`R1_STUDIO_CANDIDATE_VIEWS.md`). Isolation SSR Web
  concluante avec restauration et identités contrôlées ; support de ce chemin désormais
  séparé de l'IBL. Teinte sous le seuil absolu du comparateur, IBL non isolé, adaptation et
  readiness complètes toujours non acquises. Aucune modification de shader ou de seuil.
  Raccord vérifié : 564 tests workspace PASS / 1 timing ignoré, Clippy strict/WASM et builds
  natif/Web PASS ; smoke natif 20/36 et cycle de blockers Web 3 → 4 → 3. Candidat Tauri
  debug construit/lancé pour revue, fenêtre observée mais pixels WebView2 non inspectés.
  Aucune validation Tauri, humaine ou Gate 1 déduite de la compilation.
  Capture utilisateur suivante : premier candidat Tauri en échec ERR_FILE_NOT_FOUND.
  Overlay frontendDist corrigé (chemin relatif, pas URL Windows), test shell de régression,
  quatre tests shell PASS et candidat v2 reconstruit/relancé. Affichage Studio/vallée v2
  confirmé ensuite par l'utilisateur (« oui »), sans qualification globale WebView2 ou Gate 1.
  Premier échec conservé dans `R1_WEB_OPTICAL_SAMPLE_PROTOCOL.md`.
  Sonde IBL fixe ensuite exercée dans Chrome : 1 → 0 → 1, trois captures inspectées,
  SSR coupé, F0 et sources constants. Effet visible réversible ; répétabilité et support
  global non qualifiés. 40 tests lib Studio PASS, Clippy WASM et build PASS.
  Série répétée IBL ensuite acquise (neuf PNG) : support de chemin raccordé, 566 tests
  workspace PASS / 1 ignored, Clippy natif/Web et build PASS. Rejeu Chrome 4 → 5 → 4
  blockers avec SSR coupé : refus à IBL zéro puis restauration, sources constantes,
  readiness false. Teinte et qualification globale restent ouvertes ; protocole optique ci-dessus.
  Support technique de teinte ensuite retenu sur sa série existante (contraste conservateur
  toujours négatif), raccord/refus manuel vérifiés. 566 tests workspace/Clippy/Web PASS.
  Sans kit/forêt expérimentaux : acquittements GPU High/Low/Medium/Ultra/High contrôlés,
  full preset false et sources constantes ; pas d'adaptation, fluidité ou candidat complet
  qualifiés. Aucun passage de gate. Détails et limites dans le même protocole optique.
  Forêt : primitives survivantes désormais conservées par placement/LOD/primitive lors
  des transitions. 567 tests workspace PASS / 1 ignored, Clippy natif/Web, build et
  cycle Chrome High/Low/High vérifiés. Blocker conservé : comptages propres et arbres
  lointains encore dus. Voir `R1_FOREST_QUALITY_BINDINGS.md` dans les preuves Gate 1.
  Rapport intermédiaire consolidé le 7 septembre : critères, preuves existantes, limites de
  révision et travaux restants dans `docs/evidence/gate1/R1_055_GATE1_STATUS.md`.
  Aucune décision finale ni validation de gate ; la série de douze est revalidée sans recapture.
  Fiabilisation locale du harness : un événement ERROR ne peut plus laisser un run natif
  automatisé en succès par défaut ; 305 tests et smoke natif passent. Ne vaut pas passage
  de gate. Voir `docs/evidence/gate1/R1_RUNTIME_ERROR_VERDICT.md`.

## R1-G — Pipeline artistique image → WorldSketch → Blender → Forge

- [x] `R1-056` Contrat `forge.world-sketch` v1 strict, moteur-neutre et versionné.
- [x] `R1-057` Plan top-down et vue héroïque originaux avec prompts, dimensions, SHA-256 et provenance.
- [x] `R1-058` Validation CLI des références, chemins relatifs, hashes et dimensions PNG.
- [x] `R1-059` Compilation déterministe bornée vers les documents terrain et végétation existants et
  une entrée Blender dérivée, avec rapport séparant sorties matérialisées et données seulement
  validées.
- [x] `R1-060` Régions sémantiques converties en cinq masques éditables 257 × 257 avec feathering
  gouverné, atlas de contrôle et blending de terrain vérifié en Full HD.
- [x] `R1-061` Splines route/rivière/ruisseau matérialisées dans un cache déterministe avec carving,
  profils de berges et exclusions de végétation ; collisions et navigation restent hors de ce lot.
  Complément local : exclusions désormais appliquées au scatter runtime, avec IDs/poses des
  survivants préservés et capture native ; voir `docs/evidence/gate1/R1_WORLDSKETCH_SCATTER_CORRIDORS.md`.
  Complément altimétrique autorisé : altitudes explicites et remblai borné disponibles, 309 tests
  workspace et probe natif du pont passent. Le candidat réduit la coupe à 21,76 m mais crée
  jusqu'à 14,47 m de remblai ; berges artificielles, source non remplacée et Gate 1 toujours ouverte.
  Voir `docs/evidence/gate1/R1_WORLDSKETCH_RIVER_AUTHORED_ALTITUDES.md`.
  Correction élargie suivante intégrée localement en révision 2 : plaine riveraine bornée,
  42 022 deltas, tracés/landmarks conservés ; eau dégagée, pont à 3,90 m maximum au-dessus du
  terrain et 518/518 ticks de traversée au sol. Ce progrès technique ne clôt ni la revue
  artistique ni Gate 1. Voir `docs/evidence/gate1/R1_RIVER_VALLEY_REGRADING.md`.
- [x] `R1-062` Greybox Blender 4.5 reproductible depuis WorldSketch, avec quatre caméras fixes Full HD,
  GLB identique et pixels identiques sur deux reconstructions.
- [ ] `R1-063` Premier kit gouverné rochers/arbres/architecture : retopo, UV/PBR, LOD, collisions,
  pivots, turntables et GLB.
  État actuel : audit structurel des six GLB acquis, six copies Blender
  réouvertes sans changement prêtes pour retouche. Qualification ergonomique
  pivots/collisions/LOD, retouche humaine puis réimport et droits restent dus.
  `R1_KIT_POINT6.md`, `R1_CLOSURE_PROGRESS_20260910.md`. Le fichier de retouche
  attendu est encore absent au contrôle du 11 septembre ; ne pas remplacer ce
  geste humain par une modification de l'agent.
- [ ] `R1-064` Intégration Bevy des sorties Blender et comparaison runtime à la vue héroïque.
  Précontrôle GLB autonome ajouté avant Bevy : URI externes refusées même à hash correct,
  JSON borné, contrôle partagé compilé WASM. Tests workspace PASS et kit natif réel
  rechargé (20 instances/36 colliders). Transport Studio toujours absent ; voir
  `docs/evidence/gate1/R1_064_SELF_CONTAINED_GLB.md`.
  Transport Studio ensuite raccordé : aperçu opt-in, préflight commun et source mémoire vérifiée.
  Chrome charge réellement 20/20 instances et 36 colliders ; fichier altéré refusé avant démarrage,
  mode sans kit conservé. Tauri, import UI et revue humaine restent ouverts ; voir
  `docs/evidence/gate1/R1_064_STUDIO_GOVERNED_KIT.md`.
  Transport ensuite borné par lecture en flux et annulation : réponse bloquée et rapport
  surdimensionné refusés avant démarrage dans Chrome ; kit valide toujours prêt (20/36).
  Complément fonctionnel natif : kit chargeable en caméra joueur sans caméra fixe concurrente,
  20/20 instances et 36/36 colliders observés ; traversée du pont réussie avec kit présent.
  Ne vaut ni collisions de chaque asset vérifiées ni acceptation artistique. Voir le complément
  playtest dans `docs/evidence/gate1/R1_064_GOVERNED_ASSET_RUNTIME_INTEGRATION.md`.
  Sécurité de démarrage ajoutée : entrées natives bloquées jusqu'à la création des instances
  et l'enregistrement physique, vérifié sur 20 instances/36 colliders. Aucun passage de gate.
  Replay d'intégration ultérieur : 324 tests workspace passent ensemble, incluant les derniers
  changements de matériau, caméra et disponibilité. Preuve locale sur arbre modifié, pas
  qualification finale ADR-0006 ni validation humaine.
- [ ] `R1-065` Douze captures normalisées, parcours vidéo, revue artistique et boucle de correction.
  **Acquisition déjà réalisée** : douze captures retenues et deux vidéos
  revérifiées dans l'index du 11 septembre à 378 fichiers. Voir
  `R1_FINAL_DOSSIER_POINT7.md`. La vue intégrée a une acceptation propriétaire
  bornée ; cela ne vaut pas revue de toute la série ni comparaison finale.
  Les médias historiques ne sont pas attribués au nouvel EXE Studio.
  Les descriptions suivantes conservent les essais plus anciens.
  Dernière collecte du 6 septembre : douze PNG 1080p cohérents, six poses × clair/pluie,
  instantané externe dérivé de WorldSketch rev3 et forêt candidate, même binaire,
  High disponible partiel. Géométrie comparée intégralement, sources/placements intacts.
  Douze sorties 0 et images inspectées ; huit tests Python PASS (préparation et collecte),
  hashes des artefacts vérifiés. Premier essai eau rejeté puis remplacé par un cadrage
  exploitable dans une nouvelle série complète, sans addition de preuves anciennes.
  Sol peu naturel, reflets pluie excessifs, eau périodique et silhouettes lointaines
  restent visibles. Smokes RX 7800 XT : 59,38–83,92 FPS, p95 12,69–17,87 ms ; ne qualifient
  pas le matériel cible ni les seuils de gate. Aucune acceptation artistique ou parité Tauri.
  Preuves/hashes dans `R1_065_FIXED_VIEWS.md`, section série de douze ; R1-065 reste ouvert.
  Diagnostic des trames : trois variantes natives temporaires testées, puis retirées.
  Les grandes trames persistent sans HLOD procédural, sans ombres des arbres gouvernés et
  sans fondu du shader végétal ; cause exacte encore ouverte, aucun correctif qualifié.
  Voir `R1_DISTANT_PATTERN_ISOLATION.md`. Ces essais ne comptent pas comme vues finales.
  Essai discriminant suivant : grandes trames supprimées en clair/pluie avec trois surfaces
  terrain identiques et transitions inchangées. Contribution des géométries LOD terrain
  isolée ; neutralisation de simplification retirée, pas un correctif livré. Transition
  entre surfaces à corriger et qualifier, sans modifier les documents/colliders (même rapport).
  Préparation CPU des cibles géométriques ajoutée dans `forge_world` : interpolation parent,
  sommets communs exacts et source préservée, deux nouvelles régressions. Non raccordée au
  rendu ; compatibilité des éventails de bord et transition GPU encore à traiter. Aucun
  correctif visuel ou passage de gate revendiqué (même rapport).
  Raffinement commun des faces ajouté ensuite : intersections des deux topologies, attributs
  des deux extrémités, régression sur relief irrégulier et centres des triangles. Raccord
  GPU, coût et toutes les jonctions encore non qualifiés ; le rendu reste inchangé.
  Raccord natif réalisé ensuite : morphing Bevy positions/normales et plages LOD exclusives.
  Grandes trames supprimées sur deux vues héroïques clair/pluie inspectées. Coût mémoire et
  préparation accru (620 608 sommets alloués High), couleurs non morphées et intersections
  de berges encore ouvertes ; mouvement/Web non qualifiés. Détails dans le même rapport.
  Surcoût réduit ensuite par partage bit à bit aux deux extrémités : 122 841 sommets High
  au lieu de 620 608 (−80,2 %), mêmes 220 416 triangles. Captures clair/pluie inspectées,
  grandes trames toujours absentes ; aucune qualification FPS/VRAM globale ou des berges.
  Raccord des berges ensuite contraint géométriquement : 20/64 chunks à pleine résolution,
  les autres gardant leurs LOD ; 149 789 sommets / 274 416 triangles dérivés High.
  Deux captures héroïques clair/pluie inspectées sans les intersections dentelées précédentes.
  Six tests ciblés PASS, dont ajout/retrait, franchissement de frontière par édition du mesh
  et récupération après index invalide sans changer les handles ni le hash source.
  Qualification partielle seulement : mouvement, Web et autres vues restent ouverts.
  Régression workspace all-features : 451 PASS / 1 timing ignoré ; après extraction de
  helpers lisibles, moteur 186 PASS / 1 ignoré, Clippy strict et check Studio WASM PASS.
  Le check WASM ne constitue pas une qualification visuelle Web.
  Traversée native claire rejouée avec High et forêt candidate : 271 images Full HD,
  42,53 m et 518/518 ticks au sol, bindings conservés. Inspection début/milieu/fin seulement ;
  trait sombre au départ et stries de l'eau réservés, continuité de tous les seuils LOD
  non prouvée. Voir `R1_048_BRIDGE_MOVEMENT_VIDEO.md` pour les entrées et limites.
  Événements de cette vidéo vérifiés ensuite sans recapture : terrain High et présence de
  636 arbres gouvernés confirmés, 271 hashes PNG/journal/MP4 conservés ; vérificateur renforcé
  contre les projections manquantes ou contradictoires. Pas de preuve LOD GPU par image.
  Normales d'eau ensuite calculées par fragment : mêmes vagues/déplacement, moins d'étirement
  transversal sur les deux vues héroïques inspectées ; 26 tests eau PASS. Captures après
  préchauffage prolongé, sans qualification FPS/mouvement/Web. Voir `R1_WATER_FRAGMENT_NORMALS.md`.
  Première collecte exécutée : quatre caméras WorldSketch en clair/pluie, huit PNG Full HD
  inspectés, entrées préservées et métriques vérifiées. La vue basse révèle des éléments de
  végétation apparemment suspendus ; correction prioritaire avant qualification. Douze vues,
  vidéo et revue humaine non obtenues. Voir `docs/evidence/gate1/R1_065_FIXED_VIEWS.md`.
  Cause de l'apparente végétation suspendue identifiée : caméra enterrée de 1,687 m, racines
  d'herbe correctement ancrées. Révision 3 : caméra relevée à 1,713 m au-dessus du terrain,
  deux captures ciblées clair/pluie corrigées, terrain inchangé hors révision. Voir
  `docs/evidence/gate1/R1_065_HUMAN_CAMERA_HEIGHT.md`. Ce correctif ne clôt pas R1-065.
  Lissage temporel SSR raccordé : grain réduit dans deux captures natives clair/pluie,
  passe GPU TAA observée. Variantes motion-vector eau/végétation/pluie corrigées après
  échecs réels ; mouvement et Chrome restent à qualifier. Voir
  `docs/evidence/gate1/R1_WATER_TEMPORAL_SMOOTHING.md`.
  Chrome réel ensuite vérifié en révision 3 : hashes communs alignés, clair/pluie/clair sans
  erreur retournée et captures inspectées. Ne qualifie ni TAA GPU isolé, ni traversée Chrome,
  ni cadrage joueur initial. Voir `docs/evidence/gate1/R1_STUDIO_REV3_SHADER_SMOKE.md`.
  Départ joueur WorldSketch raccordé au landmark dans les chargeurs natif/Studio :
  hauteur triangulée, test Rapier et capture native réussis. Orientation initiale vers
  le bord sud encore à améliorer ; nouvelle exécution Chrome non obtenue pour ce raccord.
  Voir `docs/evidence/gate1/R1_AUTHORED_PLAYER_SPAWN.md`. Ne clôt pas R1-065.
  Orientation initiale ensuite corrigée vers le centre, vérifiée en capture native et dans
  Chrome réel ; traversée du pont natif toujours valide. Cadrage corrigé, mais ni qualité
  artistique ni performance qualifiées par ce smoke. Voir
  `docs/evidence/gate1/R1_PLAYER_INITIAL_HEADING.md`. R1-065 reste ouvert.
  Parcours vidéo désormais collecté en clair/pluie sur le pont, avec kit chargé et sources
  inchangées (R1-048). Douze vues normalisées et revue artistique explicite restent requises ;
  la capture hors temps réel ne qualifie pas les performances ni les artefacts temporels.
- [x] `R1-066` Relief sculpté promu en `TerrainDocument` v2, matérialisé dans les 64 heightfields
  Rapier existants et accompagné d'un audit provider-neutral des pentes, de l'eau, des routes et des
  franchissements. Le pont runtime et le fournisseur de navigation restent explicitement ouverts.
- [x] `R1-067` Placement dérivé de `bridge_south`, présentation Bevy, colliders Rapier et
  franchissement route/rivière prouvé avec le vrai contrôleur joueur. Le fournisseur de navigation
  reste `not_selected` et aucune qualification de carte jouable complète n'est revendiquée.
  Raccord Studio : le mode WorldSketch installe désormais le pont issu de sa compilation,
  sans fournir de JSON externe, et publie son hash et ses compteurs de projection séparément.
  Cette première preuve ne qualifiait pas la traversée navigateur ; voir
  `docs/evidence/gate1/R1_STUDIO_BRIDGE.md`. Le parcours automatique plus récent est décrit ci-dessous.
  Raccord natif automatique également vérifié : aucun JSON de pont requis, 518/518 ticks au sol
  avec le contrôleur réel ; priorité et erreurs des entrées explicites conservées.
  Voir `docs/evidence/gate1/R1_NATIVE_AUTO_BRIDGE.md`. Gate 1 reste ouverte.
  Scénario natif ensuite mutualisé derrière la feature moteur `qualification`, sans changer ses
  seuils : 395 tests et compilation WASM passent, traversée native avec kit rejouée (518/518 ticks
  au sol). Déclenchement explicite et verdict Studio Web ensuite raccordés et exécutés avec le kit :
  518/518 ticks au sol, 434 contacts, 42,528 m, une seule initialisation. Parcours automatique,
  pas une validation des commandes humaines ni de Tauri ; Gate 1 reste ouverte. Voir
  `docs/evidence/gate1/R1_067_SHARED_CROSSING_PROBE.md`.
- [x] `R1-068` Benchmark reproductible de Rerecast + Landmass sur la vallée, le pont, un intérieur,
  les portes, deux profils d'agent, coûts, lien hors maillage et évitement local ; compilation native
  et WASM prouvée. Verdict **NO-GO intégration** : sortie globale brute invalide, tuiles,
  reconstruction partielle et carving d'obstacle dynamique absents, backend maintenu Avian plutôt que Rapier et
  `RUSTSEC-2025-0141`. La pile reste préférée pour investigation mais le fournisseur reste
  `not_selected`.

État local non publié du 2026-09-05 : R1-064 dispose désormais d'un contrat moteur-neutre, d'un
préflight SHA-256 et d'une projection native du candidat R1-063 depuis le cache de preuve externe.
Le run RX 7800 XT charge 20/20 instances, observe 28 nœuds pour chaque LOD et matérialise 36/36
collisions UCX en colliders Rapier, avec environnement complet et capture héroïque Full HD. Cette
preuve est seulement technique : la troisième reconstruction du candidat initial est désormais vérifiée
(6 GLB, 33 textures identiques et 49 rendus dans le contrat existant). R1-063 reste ouvert faute de retouches
humaines préservées et de revue artistique explicite ; R1-064 reste ouvert faute de comparaison
humaine acceptée avec la vue héroïque. R1-065 dispose désormais de douze captures cohérentes,
sans qualification artistique finale (voir `R1_065_FIXED_VIEWS.md`). Voir
`docs/evidence/gate1/R1_064_GOVERNED_ASSET_RUNTIME_INTEGRATION.md`.

Complément local R1-063 : le worker conserve maintenant des assets corrigés depuis une source
`.blend` distincte, liée par hash à sa recette. Une retouche synthétique de sommet et rotation survit
exactement à sauvegarde/réouverture et deux reconstructions, avec cinq cas invalides refusés.
Cette preuve DCC ne qualifie pas encore la retopologie humaine ou la promotion Studio.
Le complement R1-064 charge maintenant ce kit corrige dans Bevy apres validation de sa provenance,
du hash de descripteur epingle dans l'apercu et des octets GLB : 20/20 scenes et 36/36 colliders.
L'ancien apercu non epingle est refuse avant ouverture du renderer (code 2).
Voir `docs/evidence/gate1/R1_063_GOVERNED_ASSET_KIT.md` et la preuve R1-064.

Complément R1-064 du 7 septembre : sonde native ponctuelle des points source UCX dans Rapier,
**36/36 primitives sur 20 placements PASS**, avec liaison aux six assets réels. Tests workspace
543 PASS / 1 ignored, Clippy strict et check WASM PASS. Cette preuve de requêtes ne qualifie
ni les contacts dynamiques, ni toutes les surfaces, ni l'acceptation artistique ; Gate 1 reste ouverte.
Protocole, limites et hashes dans la preuve R1-064. Aucune publication Git ni ouverture de Gate 2.

## Conditions de passage Gate 1

### Lecture actuelle des preuves — 2026-09-06

Étude de feuillage originale isolée : chêne/pin plus fournis sur les vues DCC inspectées,
six LOD sous les budgets et doubles exports identiques depuis la même scène. Moins de triangles
mais davantage de sommets ; aucun gain GPU revendiqué. Candidat non promu, silhouette et
qualification runtime restantes. Voir `docs/evidence/gate1/R1_FOLIAGE_CANDIDATE.md`.

Complément de ce candidat : réimport des deux GLB conforme aux noms/UV/triangles/collisions,
matériaux et images/samplers identiques aux GLB sources ; quatre conflits de source refusés,
dont les deux espèces déclarées retouchées. Pas de verdict visuel runtime ou de promotion.

Rejeu natif ultérieur de cette variante en fixture séparée : 20/20 scènes, 36 colliders Rapier,
94/94 pipelines prêts et capture FHD inspectée. Feuillage visible, mais écart de taille/détail
avec le scatter procédural non résolu. Run court, pas de gain de performance qualifié ;
aucun changement du kit canonique ni du Studio. Preuve et limites dans le même rapport.

Le même kit candidat passe ensuite un smoke Studio Chrome/WebGPU : 20/20 instances,
36 colliders, une initialisation et image inspectée. Viewport 1408×741, caméra joueur différente
du natif : pas de parité pixel à pixel ni de qualification visuelle finale. Kit non promu.

Aperçus du kit DCC : cadrage corrigé sur les bounds réels, 152 poses contrôlées et deux
rendus d'arbres inspectés ; sources et six GLB inchangés. Le feuillage reste à retravailler,
sans remplacement automatique de la forêt ni clôture R1-063. Voir
`docs/evidence/gate1/R1_ASSET_KIT_FRAMING.md`.

Outillage local ajouté : boutons Studio de diagnostic JSON et capture viewport PNG,
sans sauvegarde projet. 412 tests, Clippy natif/Web et build passent ; retour GPU observé,
réception des téléchargements non vérifiée. Ne clôt aucun critère de capture ou de gate.
Voir `docs/evidence/gate1/R1_STUDIO_PROOF_EXPORT.md`.

Usage de la vue : panneau de métriques masquable, masqué par défaut dans Studio Gate 1,
collecte maintenue. Aller-retour afficher/masquer exécuté et inspecté dans Chrome,
415 tests PASS. Voir `docs/evidence/gate1/R1_STUDIO_METRICS_VISIBILITY.md` ; aucun
critère artistique ou de gate fermé par ce changement d'affichage.

Les compléments datés ci-dessus sont un historique, pas autant de travaux à recommencer.
Le raccord kit Web, le pont automatique Web et le relief riverain sont implémentés et prouvés
dans leur périmètre. Les mentions antérieures « transport absent », « eau enfouie » ou
« traversée Web non obtenue » ne doivent plus piloter le prochain lot.

Les conditions ci-dessous restent ouvertes : aucun smoke ou compteur de tests ne les ferme.

Direction forêt approuvée explicitement : `docs/ADR/ADR-0010-governed-forest-projection.md`
autorise l'implémentation de prototypes gouvernés partagés, en conservant scatter, IDs, poses
et retouches. Le contrat moteur-neutre de liaison couche → prototype est implémenté dans
`forge_assets` ; son branchement au renderer reste à faire. Les smokes du kit ne remplacent
pas la forêt procédurale et aucune validation artistique ou de passage n'est inférée.

Contrat de prototypes : 418 tests workspace PASS, Clippy strict et check WASM assets PASS.
Voir `docs/evidence/gate1/R1_PROTOTYPE_CATALOG.md` ; aucun raccord renderer encore revendiqué.

Préparation adapter suivante : résolution de trois LOD avec handles partagés et contrôle de
hauteur/pivot, sans création de scènes/colliders. 421 tests workspace, Clippy strict et check
moteur WASM PASS pour cette étape de résolution synthétique.

Chargement asynchrone raccordé en option native `--prepare-vegetation-catalog` : un GLB réel
résolu pour `forge_trees`, cache prêt, 20 scènes du kit et 36 colliders conservés. Une hauteur
incorrecte produit `DimensionMismatch` et une sortie en échec explicite. 422 tests workspace
PASS, rejeu runtime après découpage du parseur, Clippy strict et check moteur WASM PASS.
Le démarrage Web, les matériaux avec vent et le remplacement scatter/HLOD restent à réaliser ;
aucune nouvelle forêt rendue ni validation humaine ne sont revendiquées. Preuves dans
`docs/evidence/gate1/R1_PROTOTYPE_CATALOG.md`.
Complément PBR : le cache réutilise les sous-assets StandardMaterial déjà créés par Bevy,
sans copie de textures. Smoke natif : 4 matériaux partagés pour le prototype chargé ;
423 tests workspace, Clippy strict et check moteur WASM PASS. Vent et remplacement du
scatter/HLOD toujours non raccordés ; aucune nouvelle qualification visuelle.

Normalisation des meshes raccordée au cache : dérivés partagés par mesh/transformation,
UV et couleurs conservées, normales/tangentes transformées par Bevy, sources intactes et
validation avant allocation. 426 tests workspace, Clippy strict et check moteur WASM PASS.
Smoke GLB natif : 12 meshes normalisés, 4 matériaux partagés, kit inchangé (20 scènes/36 colliders).
Préparation seulement : le vent animé, l'affectation au scatter et ses HLOD restent à réaliser.

Mode shader PBR/vent implémenté mais non affecté aux prototypes : poids par hauteur normalisée,
alpha/UV préservés, palette procédurale contournée explicitement. 427 tests workspace, Clippy
strict et check WASM PASS ; capture native du mode procédural inspectée sans erreur runtime.
Pas encore de preuve GPU d'un prototype animé : horloge/mouillure, scatter et HLOD à raccorder.

Cache de vent et horloge maintenant raccordés côté CPU : 4 matériaux partagés sur le chêne réel,
textures/alpha sources conservés, paramètres de hauteur/mouillure préservés à la mise à jour.
429 tests workspace, Clippy strict, build runtime et check WASM PASS. Capture du mode actuel
inspectée sans erreur runtime ; pas encore d'affectation au scatter ni de prototype animé qualifié.

Liaison au scatter préparée : vue partagée du résultat de génération actif, marqueurs avec couche
canonique, publication au démarrage et après changement de qualité. Test ECS renforcé : 636 arbres
et couches conservés sur Low/Medium/Ultra/High ; 429 tests, Clippy strict et check WASM PASS.
La réconciliation installe encore les meshes procéduraux et les anciens arbres restent dans les
HLOD : ces deux chemins doivent être raccordés ensemble. Aucun remplacement visuel revendiqué.

Aperçu natif opt-in désormais raccordé aux deux chemins : 636 arbres remplacés, 7 632 primitives
LOD0/1/2 partagées, anciens arbres exclus des lots HLOD sans retirer arbustes/fougères ni colliders.
430 tests, Clippy strict, build et check WASM PASS ; capture native réelle inspectée, mais feuillage
clairsemé **rejeté visuellement**. Lointain LOD2 individuel, HLOD optimisé et télémétrie complète
restent à faire ; compteurs procéduraux marqués invalides dans cet aperçu. Voir
`docs/evidence/gate1/R1_FOREST_PREVIEW.md`. Aucune promotion ni clôture de Gate 1.

Candidat de feuillage cohérent étudié ensuite : centres de houppier partagés entre LOD et surface
foliaire compensée sans augmenter les cartes. Génération répétée, réimport/PBR/sources et smoke
natif PASS ; capture montre une couverture lointaine améliorée, mais cartes et silhouettes encore
non qualifiées. Kit candidat révision 2, aucune promotion. Voir
`docs/evidence/gate1/R1_FOLIAGE_COHERENT_LODS.md` ; pas de nouveau verdict GPU ou humain.

Aperçu forêt : chunks lointains tree-only maintenant supportés sans dessin résiduel, en conservant
les bindings de qualité. Test ECS synthétique 64 lots/636 arbres/colliders PASS ; 431 tests workspace,
Clippy strict et check WASM PASS. Preuve ajoutée dans `R1_FOREST_PREVIEW.md`, sans nouvelle
qualification visuelle ni de hot-reload.

Reprise de l'aperçu forêt après erreur : restauration procédurale explicite via le réconciliateur,
retrait des primitives candidates après succès et erreur persistante après changement de qualité.
Test ECS avec/sans transition Low PASS (1 272 dessins rétablis, 636 colliders conservés),
432 tests workspace, Clippy strict et check moteur WASM PASS. Voir `R1_FOREST_PREVIEW.md`.
Pas de défaut GPU réel qualifié, de promotion artistique ou de fermeture de Gate 1.

Raccord Studio Web opt-in `forge_forest=preview` implémenté sur le chargement gouverné et le
projecteur partagés : check/Clippy Studio WASM, Clippy workspace et 433 tests PASS. Le lancement
WebGPU avec ce nouveau chemin reste à réaliser ; ancien bundle non probant. Voir
`docs/evidence/gate1/R1_STUDIO_FOREST_WIRING.md`. Aucune parité visuelle ou Tauri revendiquée.

Complément réel : build Trunk complet PASS, nouveau bundle lancé dans Chrome avec le candidat
révision 2. L'UI observe 636 arbres/7 632 primitives et kit 20/20/36 colliders, sans réinitialisation
du monde. Chargement/projection ECS Web prouvés ; aucune capture GPU ou mesure de performance
encore acquise pour ce bundle, donc comparaison visuelle et qualification toujours ouvertes.

Inspection suivante : captures navigateur inline High/Low/High réellement examinées, forêt
rendue et réduction/restauration de végétation observées sans nouvelle initialisation du monde.
Aspect encore non qualifié (arbustes géométriques, silhouettes répétitives, sol uniforme) ;
ni captures normalisées archivées ni performance 1080p démontrées. Détail dans
`R1_STUDIO_FOREST_WIRING.md`. Ces preuves ne ferment pas la revue artistique de Gate 1.

Correction des arbustes proches : cônes remplacés par un mesh partagé de feuilles ajourées,
sans changement de scatter/poses. 433 tests, Clippy strict, build natif et check WASM PASS ;
capture native joueur inspectée, amélioration ciblée mais structure encore régulière et aucune
qualification artistique globale. Coût 96 triangles contre 21, sans gain de performance revendiqué.
Voir `docs/evidence/gate1/R1_SHRUB_LEAF_CROWN.md` ; rejeu Web de cette géométrie encore absent.

Fougères proches ensuite découpées en tiges/folioles : disparition des grands triangles confirmée
sur capture native joueur, mais aspect encore clairsemé non qualifié. Mesh partagé 112 triangles
contre 14, poses/scatter inchangés. 433 tests, Clippy strict, build natif et check WASM PASS.
Voir `docs/evidence/gate1/R1_FERN_FRONDS.md` ; aucune qualification de performance ou artistique.

Sol : variation de litière ajoutée à la présentation herbeuse avec palettes/cache existants,
sans changer les masques peints ni ajouter de lecture texture. Captures natives clair/pluie
inspectées, sorties 0 ; 433 tests, Clippy strict et build PASS. Aspect moins uniforme mais
toujours non qualifié naturellement ; rejeu Web absent. Voir `R1_TERRAIN_SURFACE_DETAIL.md`.

Rejeu Web groupé effectué ensuite : nouveau bundle complet, captures Chrome clair/pluie
inspectées avec arbustes/fougères/litière, mêmes kit et compteurs conservés. Le manque de rejeu
Web de ces trois corrections est levé dans ce périmètre ; pas de parité normalisée, benchmark
ou Tauri qualifiés. Preuve et hash du bundle dans `R1_STUDIO_FOREST_WIRING.md`.

Parcours pont rejoué avec forêt gouvernée/corrections actuelles : natif et Studio completed,
518/518 ticks au sol et 42,528 m, kit/forêt chargés. Voir `R1_067_SHARED_CROSSING_PROBE.md`.
La vue Web d'arrivée révèle des aplats triangulaires au sol à diagnostiquer ; succès physique
distinct de la qualité du rendu et des validations humaines encore ouvertes.

Diagnostic des aplats engagé : High/Ultra ne les supprime pas ; ruban opaque de berge identifié
comme candidat. Mesure CPU sur 1 380 centres : aucun sous le terrain, surélévation maximale
1,47 m ; l'hypothèse initiale de pénétration aux centres est rejetée. Le build Web isolé
teintant uniquement les berges confirme leur responsabilité dans les aplats visibles après
traversée (518/518 ticks au sol, 434 contacts). Instrumentation retirée du code normal ;
correction du raccord géométrique encore ouverte, aucune rustine verticale appliquée.
Voir `R1_BANK_TRIANGLE_DIAGNOSTIC.md` ; aucune qualification artistique ni fermeture de gate.

Correction du raccord implémentée ensuite : découpage du ruban sur la triangulation
canonique, hauteur terrain + 1,5 cm, sans modification de l'eau ni des collisions.
435 tests PASS / 1 timing ignoré, Clippy strict et builds natif/Web PASS ; traversées
natif/Web réussies. Capture Web à l'arrivée : fragments isolés supprimés, bande continue
mais encore trop uniforme et frontière herbe trop nette. 40 369 triangles de berge contre
1 380 auparavant : coût accru documenté, pas un gain de performance. Qualité artistique,
LOD lointains et budget global restent ouverts ; preuves dans le même rapport.

Piste de partage exact des sommets mesurée puis écartée : seulement 259 doublons sur
121 107 sommets (0,214 %), attributs complets compris. Aucun code de déduplication ajouté.
Priorité suivante : matériau et transition terre/herbe, sans prolonger ce micro-audit.

Matériau/transition implémentés ensuite : shader et palette du terrain réutilisés par les
berges, poids canoniques au bord extérieur et mélange progressif vers la terre côté eau.
436 tests PASS / 1 timing ignoré, Clippy strict et builds natif/Web PASS ; capture native
inspectée et traversée Web réussie. À l'arrivée, la bande unie et sa frontière tranchée
ne sont plus observées ; de faibles facettes persistent. Aucun allègement géométrique ni
gain de performance revendiqué. Détails : `R1_BANK_TRIANGLE_DIAGNOSTIC.md`.

Le contrôle pluie du même build accentue les facettes par les reflets : matériau non qualifié
sous pluie, raccord des normales d'éclairage prioritaire. La continuité de hauteur/couleur
ne suffit pas à valider cette continuité d'éclairage.

Raccord d'éclairage corrigé ensuite : les berges utilisent les normales interpolées du
maillage canonique, distinctes des normales géométriques de collision. 437 tests PASS /
1 timing ignoré, Clippy strict et builds natif/Web PASS. Capture Web à l'arrivée sous
pluie (humidité 0,78) : facettes brillantes supprimées sur la vue de reproduction ;
traversée 518/518 ticks au sol, 434 contacts préservée. Réserve ciblée levée, pas de
qualification artistique globale ni des LOD lointains. Voir le rapport des berges.

Qualité eau : désactivation/restauration de la teinte de profondeur raccordée au matériau
dans le moteur et observée séparément des capacités optiques indisponibles. 438 tests PASS /
1 timing ignoré, Clippy strict et check Studio WASM PASS. Cycle validé en ECS seulement,
pas encore sur GPU ; aucun nouveau contrôle Studio exposé. Les presets livrés restent
inchangés et ne demandent pas Disabled. Voir `R1_040_WATER_SSR_BUDGETS.md` ; profils eau
toujours partiels, aucune fermeture de R1-040 ou de gate.

Suite Studio : commandes transitoires activer/désactiver/suivre le profil maintenant
accessibles, sans sauvegarde ni changement de preset. 439 tests PASS / 1 timing ignoré,
Clippy workspace/WASM et build Web PASS. Cycle réel Chrome confirmé par la valeur CPU
du matériau (0,180 → 0,000, maintien après Low, restauration 0,180), monde initialisé
une seule fois. Largeur des boutons corrigée et capture inspectée. Eau hors champ :
effet sur les pixels d'eau, Tauri et qualification artistique toujours non prouvés.
Voir `R1_040_WATER_SSR_BUDGETS.md` ; aucune fermeture de R1-040 ou de Gate 1.

Réflexions eau : Disabled retire désormais le SSR de la caméra unique et annule la
réflectance du seul matériau d'eau ; restauration authorée et préservation des autres
matériaux/TAA/prepasses testées. Observation du matériau distincte du budget caméra,
refus de qualification si matériau absent ou caméra ambiguë. 440 tests PASS / 1 timing
ignoré, Clippy workspace/WASM PASS. Projection CPU seulement : cycle GPU avec eau
visible et exposition Studio encore à faire, presets livrés inchangés. Réfraction écran,
distance SSR et qualification complète des profils restent ouvertes ; même rapport eau.

Exposition Studio effectuée ensuite : désactiver/suivre les reflets, indépendant de la
teinte et conservé après changement de qualité. 441 tests PASS / 1 timing ignoré,
Clippy workspace/WASM et build Web PASS. Cycle Chrome WorldSketch vérifié par les
observations matérielles 0,360 → 0,000 → 0,360, teinte indépendante, monde non reconstruit.
Interface capturée/inspectée ; eau trop distante pour qualifier ses pixels. Le cycle
GPU rapproché reste à établir, de préférence par le protocole natif de captures plutôt
que par des essais prolongés de navigation Web. Pas de qualification Tauri ou de gate.

Cycle natif réalisé ensuite avec la caméra canonique human_bridge et le kit/forêt candidat
rev2 : cinq PNG 1080p lus et inspectés, sortie 0 sans erreur, mêmes sources et caméra.
Reflets nettement supprimés puis restaurés : réserve GPU levée sur cette vue native.
La différence de teinte reste non concluante visuellement ; aucune qualification de
performance, de parité Web/Tauri ou artistique. 442 tests PASS / 1 timing ignoré, Clippy
strict et build natif PASS ; réutilisation du dossier de captures refusée sans écrasement.
Protocole `--water-cycle-proof` et hashes dans `R1_040_WATER_SSR_BUDGETS.md`.

Bilan global corrigé après ajout des commandes eau : le budget SSR seul ne suffit plus,
teinte et réflectance doivent aussi être observées appliquées. Régression synthétique
Partial/Pending/restauration vérifiée, 443 tests PASS / 1 timing ignoré, Clippy workspace
et WASM PASS. Pas de modification du rendu. L'adaptation reste non raccordée : son contrat
exige un preset réellement installé, pas les seuls budgets disponibles d'optiques partielles.
Résoudre les capacités manquantes ou approuver explicitement un contrat de fallbacks avant
d'acquitter un niveau adaptatif. Voir `R1_040_GLOBAL_QUALITY_CYCLE.md` ; Gate 1 reste ouverte.
Proposition initialement documentaire, désormais approuvée sous ADR-0011 :
`QUALITY_FALLBACK_CONTRACT_PROPOSAL.md` distingue
profil demandé, profil effectif et readiness GPU, avec substitutions à décider explicitement.
Aucun changement de contrat, de preset ou de contrôleur n'est appliqué par cette proposition.
Voie stricte indépendante identifiée : Low ne demande pas les optiques écran ni la volumétrie.
Qualifier d'abord sa réflexion environnement (liaisons/textures de la caméra et rendu visible),
puis les autres champs et exceptions ; ne pas déclarer Low complet par son seul nom. Détail
dans la section « Voie stricte » de la proposition ; aucun fallback approuvé implicitement.
Première preuve stricte ajoutée : disponibilité des deux images d'environnement de la caméra
active observée dans l'eau ; absences/forme incorrecte/ambiguïté testées. Deux captures Low
human_bridge clair/pluie passent, images disponibles true et profil complet false. 454 tests
workspace PASS / 1 timing ignoré, Clippy/WASM PASS. Contribution visuelle des reflets encore
à isoler ; voir `R1_WATER_ENVIRONMENT_IMAGE_EVIDENCE.md`.
Complément natif : le harness ne force plus High et conserve le niveau d'eau demandé.
65 tests runtime PASS, Clippy strict PASS ; cycle eau Low de cinq PNG inspectés : SSR absent,
coupure de réflectance visible et restauration confirmée, sources inchangées. Ni Low global
ni IBL séparé de l'éclairage direct ne sont qualifiés ; teinte visuelle encore non concluante.
Voir le complément du même rapport. Aucun changement de preset ou acquittement adaptatif.
Complément suivant : cycle eau étendu à sept étapes, coupure/restauration de l'intensité IBL
sans changer la réflectance. 66 tests runtime et Clippy strict PASS ; sept PNG natifs inspectés,
intensité 1/0/1 aux étapes 4/5/6, contribution IBL visible et restaurée sur `water_detail`.
L'IBL de toute la scène est affectée ; ni teinte complète, ni Low global, ni Web qualifiés.
Voir « isolation IBL native » du même rapport. R1-040 et Gate 1 restent ouverts.
Complément à temps virtuel figé : cycle natif terminé, teinte 0,18/0/0,18 isolée sur
`water_detail`, trois PNG inspectés et comparés hors HUD. Effet de couleur établi sur
cette vue native, résidu de restauration bien inférieur à la coupure ; réserve teinte
levée sur cette vue seulement. 67 tests runtime, Clippy strict et build natif PASS.
Ni benchmark (simulation figée), ni Web, ni profil complet qualifiés. Voir le complément
« teinte isolée » de `R1_WATER_ENVIRONMENT_IMAGE_EVIDENCE.md`.
Raccord suivant : mapping natif SurfaceTint déclaré disponible sous observations courantes
et sans désactivation manuelle ; le Web reste non qualifié. Smoke High natif : trois
substitutions explicites, trois blocages terrain/IBL/SSR conservés, readiness et preset
complet false. 478 tests workspace PASS / 1 timing ignoré, Clippy/WASM/build natif PASS.
Voir le complément « capacité de teinte native raccordée » de `R1_EFFECTIVE_QUALITY_PLAN.md`.
Isolation SSR suivante : réglage temporaire conservant IBL/teinte/réflectance, cycle High
natif figé de neuf captures. Étapes 6/7/8 inspectées : reflet d'arbre coupé puis restauré,
SSR 48/absent/48 observé ; contribution établie sur cette vue, reflet encore bruité.
479 tests workspace PASS / 1 timing ignoré, Clippy/WASM/build natif PASS. Aucun profil
complet, benchmark, Web ou adaptation qualifiés. Voir le complément d'isolation indépendante
de `R1_040_WATER_SSR_BUDGETS.md`.
Réserve identifiée avant raccord des capacités IBL/SSR : intensité authorée 0,72 toujours
non mappée (réflectance fixe 0,36, grandeur différente). Les captures ne prouvent pas ce
paramètre ; ADR-0011 ne couvre pas son omission. Contrat strict à implémenter ou quatrième
limite à approuver explicitement ; proposition non approuvée documentée dans
`QUALITY_FALLBACK_CONTRACT_PROPOSAL.md`. Aucune promotion de capacité ni nouveau build.
Travail indépendant ensuite : raccord pur garde-fou/contrôleur adaptatif, liaison à la
proposition et revalidation de l'identité installée. CPU seul/périmé ne suffit pas ;
échec ou perte de preuve arrête le contrôleur sans annoncer de rollback. Voie d'acquittement
historique refusée pour une demande sous garde-fou. 484 tests workspace PASS / 1 timing
ignoré, Clippy strict et check Studio WASM PASS. Observations synthétiques seulement,
renderer non raccordé et adaptation runtime désactivée. Voir `R1_EFFECTIVE_QUALITY_PLAN.md`.
Correction de ce raccord : deux tests RED reproduisent la réinitialisation de timeout ou
de readiness par remplacement du garde-fou. État désormais possédé par le contrôleur,
19 tests ciblés GREEN puis 486 tests workspace PASS / 1 timing ignoré, Clippy/WASM PASS.
Pas de changement du rendu ou des limites approuvées ; détail dans le même rapport.
Décision suivante du propriétaire, 6 septembre 2026 : quatrième limite explicitement
acceptée sous ADR-0011 (intensité des reflets conservée mais non appliquée, divergence
obligatoirement affichée). Les mentions « non approuvée » ci-dessus décrivent l'état
antérieur. Extension du plan effectif et diagnostics encore à implémenter/qualifier ;
aucune capacité promue par cet accord. Gate 1 reste ouverte, validations humaines requises,
objectif en pause non repris automatiquement et aucune publication.
Reprise de l'objectif ensuite : quatrième limite représentée par une capacité de gain
distincte et une substitution explicite du plan, déclaration d'indisponibilité dans
l'adaptateur et libellé Studio. Strict/inconnu restent bloquants ; aucune promotion IBL/SSR,
modification de shader ou de source authorée. Validation et réserves dans
`R1_EFFECTIVE_QUALITY_PLAN.md` ; Gate 1 reste ouverte.
Raccord natif suivant : support des chemins IBL/SSR conditionné aux observations courantes
du matériau, des textures, de l'intensité IBL et du budget SSR, sans ignorer les coupures
manuelles. Intensité caméra désormais exposée séparément du gain authoré. Web non qualifié,
plan candidat et readiness GPU false. 194 tests moteur PASS / 1 timing ignoré, Clippy moteur
strict et check Studio Web/WASM PASS ; smoke et réserves dans `R1_EFFECTIVE_QUALITY_PLAN.md`.
Inventaire terrain ensuite : empreinte de la source et IDs des chunks maintenus à pleine
résolution inclus dans les capacités/plan dérivé. Changement d'IDs ou de terrain invalide
l'identité du cache, même à nombre de chunks constant ; diagnostic Studio explicite.
Cette représentation ne lève pas le blocage ni n'autorise une cinquième substitution.
Validation détaillée dans `R1_EFFECTIVE_QUALITY_PLAN.md`.
Vérification native suivante PASS : inventaires projection/plan identiques, 20 chunks
protégés sur 64, sortie sans erreur, blocker terrain conservé. Proposition distincte
d'acceptation de cet écart de budget (non approuvée) dans `QUALITY_FALLBACK_CONTRACT_PROPOSAL.md`.
Pas de verdict de performance ni de Gate 1 obtenu par ce smoke.
Contrôle Web indépendant réalisé ensuite : build Trunk et affichage réel du diagnostic
PASS, 20/64 chunks explicités, trois substitutions proposées et cinq capacités encore
bloquantes conformément aux qualifications distinctes Web/natif. Readiness GPU non acquise,
accord terrain toujours attendu. Onglet temporaire et serveur fermés ; détail dans
`R1_EFFECTIVE_QUALITY_PLAN.md`, aucune nouvelle qualification artistique ou optique.
Décision ultérieure du propriétaire (« ok ») : écart de budget terrain pour protéger les
berges explicitement accepté sous ADR-0011. Les mentions précédentes « accord attendu »
décrivent l'état antérieur. Raccord au résolveur/adaptateur encore à implémenter et vérifier,
aucun passage de Gate 1, aucune qualification de performance ou publication autorisée.
Implémentation suivante : `terrain_banks_full_resolution` sous ADR-0011 uniquement,
inventaire géométrique cohérent avec hash, décompte et niveau de projection requis.
Strict/inconnu/périmé restent bloquants, budgets authorés inchangés, readiness GPU non
déduite. Validation dans `R1_EFFECTIVE_QUALITY_PLAN.md` ; pas de passage à Gate 2.
Validation du raccord : 491 tests workspace PASS / 1 timing ignoré, Clippy strict,
Web/WASM et build natif PASS. Smoke High canonique : cinq écarts, zéro blocker de
résolution, statut `resolved_plan_not_applied`, GPU/preset complet false. Installation,
acquittement et adaptation runtime restent à réaliser ; aucun verdict de Gate 1 déduit.
Prérequis d'application suivant : liaisons typées des cinq documents de domaine ajoutées
au plan et à l'invalidation du cache, cohérence terrain/eau/végétation requise. Le garde-fou
refuse les plans sans liaisons source ; changement de source invalide l'ancienne identité.
Validation détaillée dans `R1_EFFECTIVE_QUALITY_PLAN.md`, aucune readiness GPU déduite.
Raccord partiel suivant : points d'entrée `request_effective` eau/environnement et prêt
du plan par garde-fou Pending avec identité, liaisons et temps courant vérifiés. Tests ECS
des matériaux/composants, restauration et priorité des contrôles manuels eau. Le dispatch
automatique, l'invalidation des overrides installés et l'acquittement des sept domaines/GPU
restent à raccorder ; ces points d'entrée ne ferment pas R1-040 ni Gate 1.
496 tests workspace PASS / 1 timing préexistant ignoré, Clippy strict et Web/WASM PASS ;
preuves et limites dans `R1_EFFECTIVE_QUALITY_PLAN.md`, sans nouvelle preuve GPU/visuelle.
Raccord ultérieur : installation automatique des paramètres optiques après revalidation
du snapshot, invalidation sans écrasement des niveaux manuels, sept observations CPU et
garde-fou exécuté avec deadline réelle. 500 tests workspace PASS / 1 timing ignoré ;
Clippy strict, Web/WASM et build natif PASS ; smoke High natif avec sept observations CPU
et statut `plan_application_pending_gpu`. Acquittement GPU et erreurs de pipeline renderer
restent à raccorder ; adaptation désactivée et Gate 1 ouverte.
Essai natif prolongé : `TimedOut` observé sans retry du même plan, sept projections CPU
conservées et aucun succès GPU déclaré. Preuve bornée, pas qualification de la gate.
Étape GPU suivante : sonde de complétion de file liée au plan/vue, callbacks périmées
rejetées, erreur de pipeline fraîche transmise au garde-fou. 504 tests workspace PASS /
1 timing ignoré, 13 ciblés rejoués après renforcement ; Clippy/Web-WASM/build natif PASS.
Smokes natifs normal et resize : complétion réellement observée, 82 pipelines sans attente
ni erreur. Ce n'est pas encore l'acquittement GPU des ressources/effets des sept domaines ;
celui-ci, la reprise device et la qualification Web restent ouverts. Gate 1 non passée.
Inventaire GPU suivant implémenté et vérifié : 501 meshes avec allocations, 8 matériaux
avec bind groups et 5 images prêts dans le smoke High canonique ; sources inchangées et
callback reçue après contrôle. Refus des sources/familles inconnues et du renderer absent.
506 tests workspace PASS / 1 timing ignoré, Clippy/Web-WASM/build natif PASS. Contrôle des
ressources propres aux vues/effets encore requis avant acquittement complet ; Gate 1 ouverte.
Complément caméra du 7 septembre : buffer/offset uniformes, bind groups et couverture de
profondeur vérifiés en natif ; origine du viewport liée à l'identité de callback. 19 tests
ciblés PASS, Clippy/Web-WASM/build natif PASS après correction d'un conflit ECS conservé
dans les preuves. Acquittement complet et ressources des autres effets toujours ouverts ;
aucune case de Gate 1 ni acceptation humaine validée par ce complément.
Raccord GPU natif suivant : High/Low atteignent Ready pour le plan effectif préparé/soumis
après inventaire d'effets (11/11 et 6/6 groupes), sept domaines et callback liée à la vue/cible/
époque courantes. High reste Ready à 4800 frames ; resize invalide définitivement l'ancienne
application sans faux retour au succès. 516 tests workspace PASS / 1 timing ignoré,
Clippy/Web-WASM/build natif PASS. Reprise automatique, cycle quatre profils et adaptation
restent ouverts. Preset complet false, acceptations humaines inchangées ; Gate 1 non passée.
Reprise et cycle natifs suivants (7 septembre) : après resize, ancien garde-fou invalidé,
attente de ressources courantes, nouvelle séquence et nouvel acquittement sans changement
des sources. Pas de retry automatique après timeout/rejet de pipeline/choix manuel.
Cycle High/Low/Medium/Ultra/High renforcé par le GPU, cinq étapes/captures inspectées,
sources et politique constantes. 519 tests workspace PASS / 1 timing ignoré, Clippy,
Web/WASM et build natif PASS. Adaptation automatique, device-loss réel, Web/Tauri,
preuves artistiques/performance et validations humaines restent ouverts ; R1-040 et Gate 1
ne sont pas clôturés par cette preuve. Détails dans `R1_EFFECTIVE_QUALITY_PLAN.md`.
Préparation suivante du contrôleur adaptatif : démarrage depuis un plan réobservé,
historique du garde-fou et numérotation conservés, cooldown initial et benchmark fixe.
524 tests workspace PASS / 1 timing ignoré, Clippy strict et check Web/WASM PASS.
API pure non appelée par le renderer ; l'intégration automatique reste à réaliser et
qualifier. Aucun nouveau critère de R1-040 ni passage de gate validé par ce lot.
Raccord moteur suivant (7 septembre) : adaptation native opt-in réellement exécutée,
Low → Medium → High → Ultra, acquittements distincts, cinq sources constantes, priorité manuelle
et mode métriques fixes protégé. 533 tests workspace PASS / 1 timing ignoré, Clippy/Web-WASM/
build natif PASS. Délais proposition → acquittement 2,4–4,9 s : fluidité non qualifiée,
R1-040 demeure ouvert. Suite utile : réduire le coût perceptible de projection des changements,
puis qualifier l'adaptation Studio/Web et la baisse sous charge réelle ; aucun micro-audit CPU
sans lien mesuré avec ces délais. Détails et limites dans `R1_EFFECTIVE_QUALITY_PLAN.md`.
Coût dominant ensuite isolé et réduit : terrain High 3816 → 737 ms, Ultra 3638 → 744 ms,
préparation scopée sur le pool Bevy, géométrie identique. Délais complets 2,31/5,05/4,72 s →
1,11/1,99/1,85 s ; encore perceptibles. 535 tests workspace PASS / 1 timing ignoré,
Clippy/Web-WASM/build natif et quatre runs avant/après PASS ; High fixe sans régression observée.
Voir `R1_QUALITY_TRANSITION_CPU.md`. R1-040 reste ouvert ; poursuivre l'accès/qualification
Studio-Web et les critères restants, pas un audit CPU général ni une nouvelle gate.
Contrôles Studio suivants : isolation des reflets d'environnement et statut d'adaptation
implémentés ; 537 tests PASS / 1 timing ignoré, Clippy/Web-WASM/build Trunk dev PASS.
Chargement des trois modes observé dans Chrome ; cycle de clics et capture non vérifiés après
perte du contrôle navigateur. Aucune capacité Web promue, adaptation Web toujours non qualifiée.
Voir `R1_STUDIO_REFLECTION_CONTROLS.md` ; R1-040 et Gate 1 restent ouverts.
Reprise dans le navigateur intégré : cycle des trois modes PASS, réflectance CPU
0.360 → 0.000 → 0.360, cinq sources identiques à chaque étape contrôlée, état fixe et
limites Web affichés ; capture du panneau inspectée. Qualification optique Web et
performance cible toujours ouvertes, aucune promotion de capacité ni passage de gate.
Vue de preuve WorldSketch figée ensuite raccordée au Studio, sans modifier les cartes :
540 tests PASS / 1 timing ignoré, Clippy/WASM/build dev PASS, neuf readbacks Web inspectés.
Réponse à la réflectance visible ; SSR et teinte non conclusifs face au résidu de restauration,
IBL non isolée. Outil réutilisable, pas de qualification optique globale ni de benchmark.
Voir `R1_STUDIO_FIXED_PROOF_VIEW.md` ; Gate 1 reste ouverte.
Accès Studio ajouté le 7 septembre : menu des trois scènes de preuve existantes, sans saisie
d'URL, avec rechargement et reset explicite des aperçus. Parcours navigateur et retour
caméra fixe/pluie → exploration claire vérifiés ; 40 tests Studio, Clippy natif/Web et build
dev PASS. Navigation Tauri non exécutée, aucune capacité Gate 2 ni gate validée.
Voir `docs/evidence/gate1/R1_STUDIO_SCENE_NAVIGATION.md`.
Diagnostic suivant : SSR et TAA dépendent du compteur de frames, conservé malgré le temps
virtuel figé ; TAA reste partagé après coupure SSR. Renforcer le protocole par témoins
répétés/dispersion ou identité renderer, sans couper un effet pour forcer une qualification.
Reprise fonctionnelle du coordinateur : la perte d'un adaptateur invalide l'envoi précédent ;
son retour permet de redistribuer le même niveau aux sept domaines, sans envoi partiel pendant
l'absence. Test ECS de retrait/restauration eau et post-traitement, 464 tests workspace PASS /
1 timing ignoré, Clippy et check Studio WASM PASS. Aucun rechargement GPU/Web qualifié, preset
complet toujours false. Voir le complément de `R1_040_GLOBAL_QUALITY_CYCLE.md`.
Inspection optique suivante : les `steps` de transmission Bevy comptent des couches et non
les pas de ray-march Forge ; le raccord direct transmission/SSR ne satisfait donc pas le
contrat High/Ultra. Limites et sources épinglées détaillées dans
`QUALITY_FALLBACK_CONTRACT_PROPOSAL.md`, section « Limite technique vérifiée ».
Le contrat effectif dégradé est ensuite accepté explicitement par le propriétaire, sans
validation de Gate 1 : `ADR-0011-explicit-effective-quality.md`. Aucun preset authoré modifié.
Résolveur moteur-neutre implémenté dans `forge_project::effective_quality` : paramètres des
sept domaines, substitutions explicites, blocages sur capacités inconnues, hash et revalidation
du plan. Raccord diagnostique moteur/Studio ajouté ensuite ; installation, readiness GPU et adaptation restent à réaliser ; aucun profil
dégradé encore annoncé comme installé. Voir `R1_EFFECTIVE_QUALITY_PLAN.md` dans les preuves.
Cinq tests du résolveur PASS après RED initial ; 469 tests workspace PASS / 1 timing ignoré,
Clippy strict et check Studio WASM PASS. Aucune modification du rendu par ce lot de domaine.
Garde-fou d'acquittement pur ajouté ensuite : identité séquence/hash/génération, sept
observations courantes et preuve GPU séparée, timeout et invalidations terminales.
475 tests workspace PASS / 1 timing ignoré, puis cinq tests ciblés finaux PASS (dont un
ajouté après compilation du passage workspace), Clippy strict et check Studio WASM PASS.
Ce garde-fou n'est pas encore raccordé au renderer ou à l'adaptation ; aucun profil
installé ni readiness GPU réelle revendiqués. Détails dans `R1_EFFECTIVE_QUALITY_PLAN.md`.
Correction indépendante : une caméra auxiliaire inactive ne bloque plus le budget de ciel
ni sa texture ; deux vues actives restent refusées. 453 tests workspace PASS / 1 timing
ignoré, Clippy strict et check Studio WASM PASS ; preuve ECS, pas validation visuelle
multi-vue. Voir `R1_040_ENVIRONMENT_QUALITY_IN_PROGRESS.md`.

Pour progresser vers leur clôture, distinguer :

- **Travaux produit/visuels restants** : matériaux et composition (notamment végétation et berges),
  qualification eau/environnement Web des profils effectifs ; adaptation native raccordée mais
  baisse sous charge et fluidité non qualifiées, adaptation Web non qualifiée. Ne pas reprendre
  les micro-audits CPU terminés sans régression mesurée ou impact sur ces conditions.
- **Preuves finales à constituer** : la série de douze captures cohérentes existe et son intégrité
  a été revalidée ; elle reste liée à son ancien binaire. Stabiliser le candidat final avant son
  rejeu, sans relabelliser les anciennes images. Compléter ensuite comparaison
  Cadle, performance 1080p High sur le matériel cible, parité/qualification Studio-Tauri,
  provenance et indépendance dans le périmètre final. Les smokes RX 7800 XT ne suffisent pas
  à qualifier le GPU de référence ; la compilation Web n'est pas une validation Tauri.
- **Acceptations explicites restantes** : qualité artistique et comparaison visuelle, retouches
  humaines du kit, utilisation interactive et décision de passage. Le parcours Web automatique
  ne valide pas les touches maintenues ou les gestes humains.
- **Livraison** : arbre propre exigé pour la qualification finale, sans nettoyer les modifications
  présentes ; CI distante NOT RUN sous ADR-0006. Aucune publication autorisée par cette synthèse.

Cette consolidation corrige l'état du planning, sans nouveau rendu, benchmark ou passage de gate.
Les preuves techniques les plus récentes sont référencées sous R1-064/R1-067 ; les contrôles
physiques actuels de la rivière figurent dans le test
`embedded_valley_revision_preserves_authored_features_and_clears_water` de `forge_world`.

### Critères de décision

Les dégagements de végétation compilés suivent maintenant les landmarks de départ
et leur diagnostic régional distingue désormais exclusions et filtrage : le premier
plan proche a 13 centres / 400 m², sans effet supplémentaire du filtre matériau.
Priorité corrective : lisibilité des corridors et couverture adjacente, sans effacer
les chemins ni augmenter aveuglément les tentatives. Les dégagements suivent les landmarks
et les splines du WorldSketch, sans corridor ni clairière hérités de la fixture.
Test de déplacement de clairière PASS ; 581 tests workspace avant extraction du helper,
109 tests monde après, Clippy strict natif/Web et Trunk PASS. CLI et capture native
inspectées ; capture Web et diagnostic concordants, kit prêt. Pas de qualité finale
ou performance cible acquise. Voir
`docs/evidence/gate1/R1_AUTHORED_CLEARINGS.md`.

Raccordement suivant implémenté : masque de route vers poids visuels terre, sans
changement géométrique ou de collider. 584 tests workspace PASS / 1 ignored,
Clippy natif strict PASS, capture native inspectée : corridor de terre visible.
Pas de clôture artistique/performance ni de parcours d'édition déduit. Voir
`docs/evidence/gate1/R1_ROAD_SURFACE.md`.

Complément de preuve routes : Trunk PASS, capture Chrome inspectée et kit prêt,
sans erreur console observée. Le propriétaire demande aussi de reprendre le ciel
jugé peu réaliste (7 septembre). Travail artistique restant dans Gate 1 : horizon,
atmosphère, nuages et cohérence avec l'éclairage du terrain, avec règles partagées
et budgets existants. Ce retour n'est ni une acceptation visuelle ni un passage de gate.

Préparation nuages : paramètres moteur-neutres bornés et vent périodique déterministe
implémentés, 63 tests projet PASS. Génération de couverture, projection GPU et
intégration aux profils/qualités restent à faire ; aucun ciel amélioré revendiqué.
Voir `docs/evidence/gate1/R1_CLOUD_LAYER_PREPARATION.md`.

Champ de couverture ensuite implémenté et testé (trois tests ciblés, Clippy monde
strict PASS) : bruit périodique seedé, 256 Kio maximum, aucune texture externe.
Projection GPU/éclairage et intégration aux profils restent à réaliser avant capture.

Projection nuageuse native ajoutée ensuite, option d'aperçu explicite : 592 tests
PASS / 1 ignored avant correction du tri, test ciblé/Clippy moteur PASS après.
Captures natives inspectées, tri devant le feuillage corrigé. Aspect encore
plat/répétitif : **ciel non accepté**, pas de promotion aux profils ou à la qualité.
Vent GPU animé, Web, éclairage avancé et coût cible non qualifiés. Voir
`docs/evidence/gate1/R1_CLOUD_LAYER_PREVIEW.md`.

Raccordement Studio ensuite : option explicite et export diagnostique partagés,
41 tests Studio PASS, Clippy Web strict et Trunk PASS. Premier PNG Chrome inspecté,
JSON relu et paramètres identiques au natif. Rendu encore non accepté ; animation,
performance cible et profils persistants non qualifiés. Aucune gate promue.

Essai ultérieur à huit étapes : capture native rejetée pour strates visibles,
variante retirée du code actif, preuve conservée. Aucun bénéfice artistique ou
performance revendiqué ; conserver l'aperçu précédent comme base expérimentale.

Complément nuages : mips précalculés, modulation d'épaisseur et fondu distant,
deux tests ciblés et Clippy moteur strict PASS, capture native inspectée. Horizon
moins rayé, mais réalisme/animation/coût cible non qualifiés. Aperçu toujours seul,
sans promotion de gate ni d'intégration implicite au profil d'environnement.

Diagnostic natif nuages raccordé ensuite : export réel relu, paramètres/hash et
projection CPU explicites, aucune qualification GPU automatique. Deux tests ciblés
enrichis et Clippy moteur strict PASS. Pas de capture répétée pour ce contrôle JSON ;
intégration Studio/profils, réalisme et acceptation restent ouverts. Preuve dans
`docs/evidence/gate1/R1_CLOUD_LAYER_PREVIEW.md`.

Prairie : redistribution du budget High vers deux fois plus de touffes (19 138),
612 416 brins, 64 lots et plafonds inchangés. 580 tests workspace PASS / 1 ignored,
Clippy natif/Web strict et build Web PASS ; captures native/Chrome inspectées,
compteurs Web cohérents et kit prêt. Plus de couverture ponctuelle,
mais prairie naturelle et performance cible non qualifiées. Ancien catalogue refusé,
nouveau catalogue candidat validé sans changement des arbres. Voir
`docs/evidence/gate1/R1_MEADOW_COVERAGE.md` ; aucune case de gate fermée.

Microrelief du sol : normale d'éclairage dérivée du cache existant, interpolation
adoucie après rejet du premier quadrillage. Deux captures natives clear/rain inspectées,
dix tests terrain et Clippy moteur PASS. Build Web et capture Chrome inspectée,
kit 20/20 et 36 colliders prêts. Sources de carte intactes ; couverture de
prairie et acceptation artistique non résolues, R1-049 reste ouvert. Voir
`docs/evidence/gate1/R1_TERRAIN_MICRORELIEF.md`.

Garde-fou d'adaptation aux fenêtres masquées ajouté : 579 tests workspace PASS /
1 ignored, Clippy strict et build Web PASS.
Pause/reprise de focus Chrome vérifiée sur le même plan acquitté ; masquage réel,
capture du pointeur et baisse automatique non qualifiés. Voir
`docs/evidence/gate1/R1_ADAPTIVE_WINDOW_VISIBILITY.md`.

Correction indépendante des transitions : lots lointains inchangés
réutilisés, invalidation sur source modifiée ; 578 tests workspace PASS / 1 ignored,
Clippy strict et build Web PASS. Cycle Chrome High/Low/High, six compteurs restaurés,
sources/temps/kit physique constants ; deux captures inspectées. Comparaison CPU
synthétique favorable, pas de fluidité globale ni de GPU cible qualifiés. Voir
`docs/evidence/gate1/R1_FAR_BATCH_REUSE.md`.

Le contrôle du vent
(0/2/0 s) reste non concluant : filtre optique false et initialisation physique
différente au temps gelé ; ne pas répéter le même protocole sans corriger ces
conditions. Voir `docs/evidence/gate1/R1_FAR_WIND_ISOLATION.md`.

Conflit caméra reproduit puis corrigé : 575 tests workspace PASS / 1 timing ignoré
et Clippy strict natif/Web PASS.
Build et rejeu Web PASS pour deux pas sans saut de caméra ; vent lointain encore
non qualifié, aucun critère de passage coché. Voir
`docs/evidence/gate1/R1_FIXED_CAMERA_CLOCK_REGRESSION.md`.

Mesure native du regroupement ensuite : deux paires sur RX 7800 XT montrent environ
4–7 % de FPS moyens supplémentaires pour ~129 Mio alloués en plus, mais p95 regroupé
16,40/17,07 ms ; seuil non stable, GPU cible non qualifié. Capture séparée inspectée.
Voir `docs/evidence/gate1/R1_NATIVE_FAR_BATCH_COST.md`, aucun critère de gate coché.

Regroupement lointain expérimental raccordé ensuite : 573 tests workspace PASS /
1 ignored, Clippy strict natif/Web et build PASS ; deux captures Chrome des modes
individuel/regroupé du même bundle inspectées, sources et triangles constants.
Moins d'entités mais davantage de géométrie unique, aucun gain FPS/HLOD qualifié.
Mode par défaut inchangé ; réserves dans `R1_FOREST_FAR_PATH_DIAGNOSIS.md`.
Cycle High/Low/High du mode regroupé ensuite vérifié : restauration exacte des
compteurs, sources constantes, aucune erreur navigateur. Pas de mesure de fluidité
ou qualification du vent animé déduite de ce cycle.

Préparation CPU des lots lointains ajoutée ensuite : 572 tests workspace PASS /
1 ignored, Clippy strict natif/Web PASS. Données PBR et par arbre préservées ;
pas encore de raccord shader/projecteur, réduction géométrique ou HLOD qualifié.
Voir `docs/evidence/gate1/R1_FOREST_FAR_PATH_DIAGNOSIS.md`.

Complément télémétrie du 7 septembre : les bindings mesh réels de végétation et leur
visibilité CPU sont comptés, y compris la forêt candidate. Cycle High/Low/High vérifié,
570 tests workspace PASS / 1 ignored ; détails dans
`docs/evidence/gate1/R1_FOREST_LIVE_TELEMETRY.md`. Aucun critère de passage coché :
ce relevé ne qualifie ni les draw calls GPU, ni le HLOD lointain, ni la performance.

Complément du 7 septembre : le candidat kit/forêt est accessible explicitement dans le menu
Studio et son affichage a été vérifié dans Chrome. Deux vues natif clair/pluie du dernier
binaire sont inspectées ; leurs smokes ne satisfont pas p95/1 % low et ne remplacent ni
la série finale ni le matériel cible. Les PNG utilisateur sont retrouvés, mais ne forment
pas une paire A/B comparable. Voir `docs/evidence/gate1/R1_INTEGRATED_CANDIDATE_ACCESS.md`.
Aucun critère ci-dessous coché par ce lot, aucune promotion d'asset ni ouverture de Gate 2.

- [ ] qualité visuelle jugée clairement supérieure à la base Godot ;
- [ ] impact visuel jugé au moins comparable à la baseline Cadle sans en copier les assets ;
- [ ] 60 FPS moyens en 1080p High sur GPU milieu de gamme ;
- [ ] p95 ≤ 16,7 ms ;
- [ ] 1 % low ≥ 45 FPS ;
- [ ] absence de stutter récurrent ;
- [ ] profils qualité fonctionnels ;
- [ ] parité de données entre natif et Studio ;
- [ ] code sans dépendance propriétaire obligatoire ;
- [ ] rapport et captures versionnés.

---

# Gate 2 — Éditeur de monde

Lot brosses locales : cinq opérations (lever/abaisser, aplatir, lisser, bruit,
pente), picking, aperçu filaire et tracé continu raccordés aux commandes
réversibles. Un trait produit une seule commande/Undo, avec espacement métrique
et limites explicites. 7 tests ciblés PASS, Clippy world et Studio WASM PASS.
Peinture de matériaux encore absente ; geste souris, cercle, aperçu et replay
graphique non qualifiés ; R2-B reste ouvert.
Preuve : `docs/evidence/gate2/R2_TERRAIN_BRUSHES.md`.
Navigation auteur implémentée (rotation, déplacement, zoom, vue d'ensemble),
Clippy Studio WASM PASS, interaction graphique non qualifiée :
`docs/evidence/gate2/R2_EDITOR_CAMERA.md`. Aucun critère supplémentaire coché.
Essai regroupé : aperçu numérique/Apply/Save/Undo/Save et panneau observés,
archive vérifiée. Défaut restant du glisser rapide : aperçu ponctuel seulement.
Collecte du chemin à corriger avant qualification R2-B, voir
`docs/evidence/gate2/R2_AUTHORING_BRUSH_WORKSPACE.md`.
Correctif désormais implémenté (file événementielle + picking par lot), Clippy
Studio WASM PASS ; replay graphique du glisser toujours requis, R2-B non acquis.
Replay maintenant acquis pour le glisser droit rapide : bande continue de 618
samples, Apply/Save et archive d'une commande observés sur candidat 442d3065....
Les autres critères R2-B restent ouverts ; preuve détaillée ci-dessus.

Parcours Studio initial observé : création d'une zone plane, aperçu/renommage,
sauvegarde/réouverture, Undo/Redo restaurés et archive contrôlée. Aucun JSON édité
manuellement. Terrain affiché et hash moteur du renommage concordant. Correctif de
reprise 3D rejoué : altitude 8 m conservée après sauvegarde/fermeture/réouverture,
hash moteur identique à l'archive et terrain rendu. Test moteur/Rapier ciblé PASS.
Playtest et parcours complet non qualifiés. Preuve `docs/evidence/gate2/R2_AUTHORING_LIVE_PROJECTION.md`.
Les décomptes ci-dessous décrivent les validations antérieures, pas la gate entière.

Hôte R2-A/F ajouté : protocole Create/Open/Inspect/Preview/Apply/Save/Close,
97 tests core/project PASS. Raccord Tauri candidat, interface non livrée.
Suivi : `docs/evidence/gate2/R2_AUTHORING_HOST.md` depuis la racine.

R2-F, reprise du staging : choix explicite et archives des deux versions,
arrêt de processus sans destructeurs testé, 96 tests core/project PASS.
Preuve `docs/evidence/gate2/R2_INTERRUPTED_SAVE_RECOVERY.md` depuis la racine.
Parcours Studio et cas d'ouverture initiaux encore incomplets.

Complément R2-F : session sur disque, remplacement Windows, verrou de session et
conflits externes testés (92 tests core/project PASS). La reprise après kill,
le traitement explicite d'un staging interrompu et le parcours Studio restent
ouverts : `docs/evidence/gate2/R2_FILE_SESSION.md` depuis la racine.

Travail démarré le 12 septembre 2026 sur demande explicite, après clôture locale
de Gate 1 sous dérogations. Première fondation R2-F : commandes terrain,
aperçu, Undo/Redo et restauration vérifiée du journal de session (88 tests
core/project PASS). Aucun critère de parcours Studio ou sauvegarde atomique
n'est encore coché. Preuve : `docs/evidence/gate2/R2_SESSION_ARCHIVE.md`
depuis la racine du dépôt.

## But

Créer une carte complète sans modifier de JSON à la main, avec une expérience proche d’un constructeur
de monde accessible.

## R2-A — Projet et zones

État consolidé : création projet/module par formulaires Examiner/Apply, Save et
reprise acquises localement. Les entrées historiques ci-dessous retracent les
étapes ; leurs mentions « encore absent/dû » ne remplacent pas les verdicts récents.
Preuves courantes : `docs/evidence/gate2/R2_WORKSPACE_FOUNDATION.md` ; catalogue
historique distinct : `docs/evidence/gate2/R2_SAVED_ZONE_CATALOG.md`.

- [x] `R2-001` assistant nouveau projet.
  Qualification locale : formulaire de création Examiner/Apply réellement utilisé
  sur 4FB0B367 ; projet sauvegardé, relu après arrêt sur A74AEDAE et D75CB60E.
  Identité stable, module et zones attachés retrouvés. Ni assistant IA ni export
  distribuable revendiqués ; ces fonctions relèvent des gates suivantes.
  Historique de réalisation :
  Parcours Studio projet/module/nouvelle zone directe/Save/OpenManaged acquis sur
  candidat 4FB0B367, capture de terrain plat et archive contrôlées. Reprise complète
  encore due. Défaut de recouvrement des contrôles corrigé ensuite, compilation Web
  acquise mais correction non capturée (R2_WORKSPACE_FOUNDATION.md).
- [x] `R2-002` nouveau module.
  Qualification locale : choix du projet, création Examiner/Apply, ajout de zones
  et Save réalisés dans le Studio ; reprise du même ModuleId après arrêt acquise.
  Les qualifications récupération (R2-039), audit global (R2-037) et édition
  multizone (R2-005/045) restent distinctes et ouvertes, sans invalider cette création.
  Historique de réalisation :
  - Récupération workspace explicite raccordée (versions exactes et objets
    vérifiés, deux archives conservées). Test service/host PASS ; UI non qualifiée.
  - Stockage workspace : budget de lecture réel corrigé (256 Mio cumulés,
    4 Mio par objet), test ciblé PASS ; qualification intégrée toujours ouverte.
  ADR-0016 accepté le 14 septembre : socle projet/module, commandes annulables,
  sauvegarde atomique et réouverture avec audit implémentés. Trois tests ciblés
  et Clippy core/project PASS. Studio, objets immuables et adoption encore absents ;
  les clés de création de zone ne valent pas ces assistants. R2_WORKSPACE_FOUNDATION.md.
  Adapter d'objets immuables ajouté ensuite : 2 tests ciblés PASS (publication
  sans écrasement, relecture et corruption). Adoption/service workspace et garde
  historique host ajoutés ensuite. Édition gérée service ajoutée (commandes de zone,
  doubles reçus, références immuables et Undo) ; raccord et assistants Studio ouverts.
  Protocole workspace désormais exposé par le host et Tauri : test host PASS,
  Clippy natif project/Studio PASS. Formulaires et qualification graphique non acquis.
  Formulaires projet/module ajoutés ensuite (Examiner/Apply, Undo/Redo, Save,
  sélection du projet et liste) : Clippy Web PASS ; qualification graphique encore due.
  OpenManaged et commandes/playtest/sauvegarde raccordés au host ; boutons de zones
  ajoutés dans la liste Studio. Parcours host ciblé acquis, rendu réel encore à vérifier.
  Formulaire d'adoption source/module et ReviewAdoption/Adopt ajoutés ; contrôle
  service PASS, qualification graphique encore due. Aucun critère coché sur compilation seule.
- [x] `R2-003` nouvelle zone extérieure (périmètre 512 m/pas 2 m).
  Création directe dans un module ajoutée le 14 septembre : examen lié à la
  recette, publication immuable sans archive historique, Save/reprise/Undo/Redo
  testés. Formulaire Studio ajouté mais non qualifié ; preuves existantes ci-dessous
  ne couvrent pas encore ce nouveau parcours workspace (R2_WORKSPACE_FOUNDATION.md).
  Création paramétrée seed/altitude/amplitude/échelle ajoutée, taille 512 m/pas 2 m
  conservés. Résumé Studio avant Create, périmé si paramètres/clés modifiés.
  Deux tests ciblés et Clippy domaine/Web PASS ; candidat 75DA9340 : examen
  invalidé après saisie, création relief 24 m, Save/Close/Open et même hash acquis.
  Autres paramètres/abandon/clés non rejoués graphiquement ; couverture partielle.
  Création agrégée directe ensuite raccordée : CreateZone, aucun fichier ancien
  intermédiaire, premier Save agrégé. Quatre tests création/migration PASS ;
  candidat 0F315A98 : création directe, forêt puis maison via Preview/Apply,
  Save/Close/Open acquis ; archive F92C7F86 identique et rendu restauré.
  Processus fermés. Pas de qualification exhaustive de chaque saisie graphique.
  Preuve et limites : `docs/evidence/gate2/R2_ZONE_CREATION.md`.
- [x] `R2-004` métadonnées et point de départ.
  Verdict local du 19/09 : parcours Luna max sur 7D630852, puis régression des
  contrôles extraits sur 9317EE50. Métadonnées, aperçu/annulation/application,
  picking, départ Undo/Redo, Save/Open et position réelle X40/Z0/cap90 acquis.
  SHA sauvegarde 89E0DCB8 identique avant/après playtest final. Qualification
  bornée R2-004 ; GPU transversal et Gate 2 restent ouverts. Rapport final et
  captures référencés dans GATE2_RESUME_20260919.md.
  Reprise du 19/09 : annulation des aperçus Nom/Altitude après modification,
  informations canoniques de zone et cap caméra réel ajoutés. Tests ciblés et
  Clippy WASM strict PASS. Candidat 7D630852 : métadonnées, picking, Undo/Redo du
  départ, sauvegarde/réouverture et diagnostic runtime X40/Z0/cap90 acquis par
  Luna max, captures archivées. Régression du candidat final 9317EE50 acquise.
  Voir GATE2_RESUME_20260919.md.
  Parcours 284B775A acquis le 15/09 : départ 40/0/90 saisi, Preview/Apply/Save,
  redémarrage complet et champs restaurés, Jouer puis Retour à l’édition.
  Archive 9E77EF0C identique avant/après jeu, captures R2_PERSISTENT_START.md.
  Coordonnées runtime non relevées, picking et métadonnées complètes encore dus.
  Départ X/Z/cap sauvegardable ajouté le 14 septembre : Preview/Apply/Undo/Redo,
  trois codecs, host Jouer et remplacement temporaire distinct. Panneau Studio
  et choix X/Z par clic terrain raccordés, reprise du départ accepté disponible ;
  compilé ; position/orientation réelles non qualifiées et métadonnées complètes
  encore dues. Voir R2_PERSISTENT_START.md.
- [x] `R2-005` onglets de zones.
  Verdict local consolidé : renommage B→A→B/reprise D75CB60E, géométrie B=6 m
  B→A→B/Save F1C5DB66 (workspace 674A0586, archive A inchangée), tests hôte
  Undo/Redo indépendant/recréation exacte et limite16 sans éviction acquis.
  Une seule projection active ; pas de qualification de 16 rendus simultanés
  ni de l’ensemble des outils multizone. Voir R2_WORKSPACE_FOUNDATION.md.
  Géométrie multizone et plafond 16 qualifiés côté hôte par deux nouveaux tests :
  A/B indépendants, Undo/Redo A, Save depuis B/recréation hôte exacte ; 17e refusé
  sans éviction ni sauvegarde, fermeture libère une place. Geste géométrique
  Studio encore distinct. Preuve R2_WORKSPACE_FOUNDATION.md.
  Reprise après arrêt du renommage acquise ensuite sur le même D75CB60E : nom,
  hash terrain identiques et fichier AB913BF9 inchangé. Cycle B→A→B→Save→reprise
  acquis pour le nom ; géométrie multizone et limite d'onglets non qualifiées ici.
  D75CB60E : onglets corrigés visibles, renommage B conservé après B→A→B sans
  Save implicite ; Save explicite et référence A inchangée vérifiées. Reprise de
  cette retouche après arrêt et géométrie multizone encore dues (preuve workspace).
  Candidat 7A7812D0 : création B/Save/Open A/Open B/fermeture B→A acquis, hashes
  terrain et archive contrôlés. Onglets masqués par CSS de menu : correctif isolé
  ajouté, Clippy Web PASS, rendu correctif encore dû. Retouches multizones GUI dues.
  Fermeture active vers onglet voisin ajoutée : deux tests host PASS, dont retour
  vers voisin modifié sans Save implicite ; Clippy strict PASS. Geste graphique
  encore dû (R2_WORKSPACE_FOUNDATION.md).
  Onglets gérés implémentés (16 maximum, une projection, conservation des edits
  au changement et dirty par zone). Test A→B→A puis Save/reprise PASS ; qualification
  graphique et ergonomie non acquises, case conservée ouverte.
  Synchronisation des indicateurs zone/workspace après commandes ajoutée (Clippy
  Web PASS). Examen du candidat intégré interrompu par Échap ; aucune validation
  visuelle déduite de son build, processus d'essai fermés.
  Compaction des zones gérées raccordée ensuite : même tête/historique, nouvelle
  archive immuable, Save explicite et reprise compacte vérifiés ; Clippy Web PASS.
- [x] `R2-006` hiérarchie et sélection — PASS local, 20 septembre 2026.
  Candidat apparié 4DB02C62 / WASM 8FE20704 : ouverture de deux zones
  partageant les mêmes IDs, pagination exacte de 44 objets, sélections
  chemin/rivière/bâtiment/placement, reset A→B, continuité sur nouvelle
  révision du même AreaId, filtre/retouche, picking et cible occluse négative,
  annulation, suppression puis Undo qualifiés par Computer Use Luna max.
  Transport Tauri des payloads passé en chaîne JSON lossless après reproduction
  d'un arrondi de seed u64 ; test, suites Studio/projet et Clippy natif/WASM PASS.
  Preuves : `docs/evidence/gate2/R2_SCENE_INDEX.md` et rapport v4 sous
  `artifacts/forge-runtime/gate2-resume-20260919/r2-006-cu/`.
  Les réserves GPU, collisions/passage joué et revue humaine restent des limites
  transversales ; elles ne ferment pas la Gate 2 générale.
  Couches végétales ajoutées à l'index commun le 15 septembre : IDs typés,
  filtre/pagination existants et sélection partagée recette/masque. Changement
  de zone ou retrait de couche efface la sélection ; aucune instance générée
  indexée individuellement. Test de lecture/filtre PASS, Clippy WASM PASS.
  Candidat DDCC9C5F : sélection Arbres partagée entre index/recette/masque
  verrouillé observée ; aperçu de copie annulé par Échap, végétation restaurée,
  archive inchangée, processus fermés. Reset multizone et pagination >40 encore
  non qualifiés graphiquement : R2_SCENE_INDEX.md, R2_EDITOR_SHORTCUTS.md.
  Même candidat F1C5DB66 : index → inspecteur → cadrage d’une arche placée acquis
  dans une seconde fixture, sans recharger le catalogue ; archive FF89547B
  inchangée. Retouches/Undo et cas limites non couverts par ce parcours.
  Candidat F1C5DB66 : chemin sélectionné depuis l’index et cadré réellement,
  filtre BÂTIMENT + inspecteur du bâtiment acquis ; archive inchangée, processus
  fermés. Fixture sans placement ; assets et retouches via index restent à
  qualifier. Captures et limites dans R2_SCENE_INDEX.md.
  Extension du même index aux assets placés : sélection commune avec liste et
  picking 3D, ouverture de la bibliothèque, identités typées. Clippy Web/WASM
  PASS (6,86 s), interaction graphique groupée encore à qualifier. La végétation
  générée reste éditée par ses règles, pas comme des objets persistants individuels.
  Index commun chemins/rivières/bâtiments implémenté, filtre nom/type/ID et
  verrous visibles ; partage des sélections avec les inspecteurs existants et
  le picking bâtiment. Clippy Web/WASM PASS (6,87 s). Interaction réelle non
  encore qualifiée ; assets et végétation hors de cet index. Voir
  `docs/evidence/gate2/R2_SCENE_INDEX.md`.
  Ajout du cadrage du chemin accepté depuis son inspecteur (14 septembre) :
  navigation seule, altitudes dérivées du terrain, brouillon non appliqué.
  Clippy Web/WASM passe ; qualification visuelle et hiérarchie globale restent
  ouvertes. Ce bouton ne constitue pas le verdict de R2-006.
  Sélecteur de bâtiments nom/verrou/ID ajouté ; un inspecteur relu depuis la tête
  courante, sans copie canonique. Clippy Studio Web PASS ; candidat 82A6A72D :
  archive à deux bâtiments rouverte, sélection et cadrages distincts observés,
  archive inchangée et processus fermés. Preuve : R2_BUILDING_LAYOUT.md.
  Ne qualifie pas une hiérarchie universelle des autres objets de zone.
  Sélection directe sur mesh bâtiment raccordée au même inspecteur, avec
  garde de révision et occlusion. Clippy Web PASS ; candidat 4C953E54 : clic sur
  les deux bâtiments et clic vide observés, archive inchangée, processus fermés.
  Scénarios d'occlusion/annulation non qualifiés graphiquement ; R2-006 partiel.

## R2-B — Brosses de terrain

- [x] `R2-007` lever/abaisser — PASS local, 20 septembre 2026.
- [x] `R2-008` lisser — PASS local, 20 septembre 2026.
- [x] `R2-009` aplatir — PASS local, 20 septembre 2026.
- [x] `R2-010` bruit — PASS local, 20 septembre 2026.
- [x] `R2-011` pente — PASS local, 20 septembre 2026.
- [x] `R2-012` peinture matériaux — PASS local, 20 septembre 2026.
  Contrat concret proposé dans ADR-0015 : masque persistant par ID de couche,
  migration versionnée et commandes réversibles. Approbation reçue le 13/09/2026.
  Modèle v3, archive v2, commandes et poids effectifs complets/partiels codés ;
  3 tests ciblés PASS. Pinceau domaine ajouté (2 tests PASS), contrôles et
  aperçu logique coloré Studio codés, Clippy WASM PASS. Replay Studio migration,
  trait/Apply/Undo/Redo/Save/Close/Open observé, archive/moteur concordants ;
  qualification complète encore ouverte, voir `docs/evidence/gate2/R2_MATERIAL_PAINT.md`.
- [x] `R2-013` visualisation rayon/intensité — PASS local, 20 septembre 2026.
- [x] `R2-014` recalcul partiel — PASS local CPU/Rapier, 20 septembre 2026.

  Qualification groupée sur candidat 4DB02C62 / WASM 8FE20704 : cycles
  Preview/Apply/Undo/Redo/Save pour lever-abaisser, lisser, aplatir, bruit et
  pente ; réouverture finale synchronisée avec 422 deltas de relief. Le trait
  continu de 618 échantillons et la direction lever réutilisent la preuve
  historique 442d3065, toujours applicable ; la direction abaisser et les
  autres cycles ont été rejoués sur le candidat courant. Peinture, effacement,
  annulation sur changement de réglage, relief après peinture et persistance
  v3 sont couverts par UI et tests ADR-0015. Picking terrain, cercle 65/65,
  rayon/intensité et appel natif 109,9 ms observés. Tests world 28/28 et engine
  terrain_update 10/10 valident le recalcul partiel et les acquittements
  CPU/Rapier. Rapport :
  `artifacts/forge-runtime/gate2-resume-20260919/r2-007-014-cu-20260920/r2-007-014-cu-report-20260920.md`.
  Granularité GPU et revue humaine globale non déduites ; Gate 2 reste ouverte.

## R2-C — Biomes et végétation

15 septembre : réglages altitude/pente par couche raccordés au bus de
régénération, au panneau et au diff. Tests ciblés de conservation/historique et
filtrage CPU PASS, Clippy WASM PASS. Intensité du pinceau ensuite exposée et
testée. Candidat 8C6A960A : altitude Preview/Apply/Undo, restauration forêt,
point Herbe à 0,25 Preview/Apply/Save acquis, masque 193–255 et relecture native.
Processus fermés, source intacte ; pente et qualification complète encore dues,
aucune clôture du bloc. Détails : R2_VEGETATION_AUTHORING.md.

Préparation du pinceau de densité codée sur le masque existant par couche :
2 tests ciblés et Clippy world PASS. Commandes agrégées ensuite raccordées :
peinture effective, verrou/déverrouillage et retour à l'héritage. 3 tests ciblés
PASS (preview/Apply/retry, verrou, Undo et archive), Clippy project/world PASS.
Raccord générique recette/projection ensuite implémenté, sans seconde scène canonique.
Replay Studio regroupé du 13/09, candidat `975b9033…` : tracé arbres (rayon 24 m),
aperçu logique de 1871 marques, application visible, verrou, seed 43 → 44,
sauvegarde et réouverture avec clairière conservée. Archive compacte v3 explicite,
10 commandes finales, anciennes retouches conservées. 4 tests pinceau/overlay PASS.
Qualification complète, reconstruction végétale partielle et autres parcours restent
ouverts ; aucune case ni Gate 2 validée par ce seul replay.
Voir `docs/evidence/gate2/R2_VEGETATION_AUTHORING.md`.

- [x] `R2-015` pinceau densité — PASS local, 20 septembre 2026.
  Candidat 4DB02C62 : déverrouillage de couche, point puis trait, rayon/intensité,
  changement de réglage annulant l'aperçu, réarmement, Apply/Undo/Redo et
  Save/Close/Open observés par Luna max. Après réouverture, Arbres reste une
  retouche manuelle modifiable avec un échantillon nul et Herbe conserve sa
  plage 146/255–255/255. Le replay historique conserve la preuve cible 0/255,
  verrou et trait 1871 marques ; tests domaine couvrent aussi retour héritage.
  Rapport : `artifacts/forge-runtime/gate2-resume-20260919/r2-015-validation-20260920/R2-015-QUALIFICATION-20260920.md`.
- [x] `R2-016` catalogues d’espèces — PASS local, 20 septembre 2026.
  Préparation de sélection explicite dans le catalogue gouverné existant ajoutée
  le 15 septembre : couche source/asset → couche cible, pivots/dimensions/hash
  conservés, couverture et provenance metadata validées. Deux tests et Clippy
  assets PASS ; persistance transactionnelle ajoutée (tête v7, archives
  v19/20/21, deux tests ciblés PASS). Adapter de résolution épinglée préparé,
  test de refus des métadonnées altérées PASS. Livraison GLB, palette Studio,
  aperçu réversible et retrait gouverné raccordés. Candidat 42794BEB : aperçu
  chêne/abandon puis mélange chêne/pin Apply/Save/Close/Open observés sur copie.
  Conversion compacte nécessaire après refus 4 Mio ; source intacte, essai fermé.
  Undo/Redo graphique, playtest et autres familles encore dus.
  R2_SPECIES_CATALOG.md ; critère ouvert.
  Qualification complémentaire Luna max : catalogue/provenance GLB, mélange,
  retrait, propagation oak sur deux couches Tree, Preview/Apply, Undo/Redo et
  Save/Close/Open acquis sur candidat 4DB02C62. Archive finale révision 5,
  quatre reçus, deux bindings exacts et aucun doublon. Les tests domaine couvrent
  retry/idempotence ; une tentative de faute sidecar n'a pas atteint le catalogue
  embarqué dans le bundle et n'est pas revendiquée comme preuve UI. Rapport :
  `artifacts/forge-runtime/gate2-resume-20260919/r2-016-validation-20260920/R2-016-QUALIFICATION-20260920-prepared-v2.md`.
- [x] `R2-017` exclusions — PASS local, 20 septembre 2026.
  Override auteur optionnel versionné : None hérite de la recette, Some remplace
  explicitement, y compris Some(empty) couvert en domaine. Tête v9 et archives
  v28/v29/v30 préservent les anciens hashes. Commande typée réversible, retry,
  Undo/Redo, audit et persistence passent. Candidat DA859D53 / WASM 5BF46B40 :
  héritage recette, Circle, CorridorSegment, annulation sur champ, Preview/Apply,
  suppression, Undo/Redo et Save/Close/Open observés ; projection CPU/Rapier
  acquittée. Rapport :
  `artifacts/forge-runtime/gate2-resume-20260919/r2-017-validation-20260920/R2-017-CU-REPORT.md`.
  GPU non qualifié ; lint global préexistant zone_host.rs hors lot.
- [x] `R2-018` seed stable.
  Consolidé le 15 septembre : génération exacte après 42→43→42 et deux
  relectures d'archive, nouvelles révisions sans changement des instances,
  masques verrouillés conservés. Test ciblé/Clippy PASS ; archives du parcours
  Studio A64/F1 recoupées par le décodeur (42→43, corrections/terrain identiques).
  Portée et limites : R2_SEED_STABILITY.md. Ni R2-C ni Gate 2 clôturés.
  Recette : réglages densité/espacement par ID de couche ajoutés, Preview/Apply,
  corrections/voisins/seed conservés, retry et Undo après reprise testés ;
  quatre tests world PASS. Panneau et diff Studio raccordés, geste graphique
  encore dû (R2_ZONE_REGENERATION.md), sans clôture du bloc végétation.
- [x] `R2-019` aperçu temps réel — PASS local CPU/Rapier, 20 septembre 2026.
  Candidat DA859D53 : modification de recette projetée sans mutation d'archive,
  readiness observée, Abandon restaurant l'état accepté, second Preview prêt en
  9,1 ms, Apply puis Undo/Redo, Save/Close/Open au même hash final. L'absence de
  duplication est couverte par test moteur et observation visuelle ; l'UI
  n'expose pas de compteur d'instances. Rapport :
  `artifacts/forge-runtime/gate2-resume-20260919/r2-019-validation-20260920/R2-019-CU-REPORT.md`.
  Présentation GPU non qualifiée.
- [x] `R2-020` promotion et retouche — PASS local, 16 septembre 2026.
  Candidat 1303A840 : promotion/abandon/Apply/Undo/Redo réels, densité/habitat,
  refus d'origine perdue, Save/Open final et entrée/sortie du playtest acquis.
  Archive 3F539AD8 : 29 reçus, 24 commandes initiales intactes, placements initiaux
  strictement conservés, terrain inchangé ; round-trip delta sans perte.
  Chargement GLB réellement bloqué sur copie : scatter conservé, Apply et
  raccourci refusés, archive intacte. Tests des pins et des deux quotas PASS.
  Pas de garantie frame-par-frame GPU ni de nouvelle qualification artistique.
  Voir le bilan courant de R2_GENERATED_PROMOTION.md ; les notes suivantes
  décrivent les étapes historiques, pas des travaux à recommencer.
  Extension post-97DF58D4 codée : correspondances aussi pour densité/espacement
  et altitude/pente, dans les panneaux existants ; paramètres complets vérifiés
  au transport/cache. Test CPU conservation/archives/Undo/Redo et Clippy natif/WASM
  PASS. Nouveau candidat et parcours réel encore dus, aucun critère clôturé.
  Transition ajoutée : nouvel objet chargé caché jusqu'à l'acquittement du retrait
  du scatter, annulation sans révélation tardive. Deux tests CPU et Clippy WASM
  PASS ; candidat 97DF58D4 construit, coque lancée mais parcours bloqué par
  dialogue de redémarrage Windows. Copie intacte, processus fermés. Transition
  réelle et absence de trou GPU non qualifiées.
  Garde post-11415DF8 codée : Apply attend l'aperçu exact et le placement prêt,
  bouton/raccourci protégés, restauration demandée sur erreur de projection.
  Test CPU et Clippy WASM PASS ; replay du chargement réel encore dû.
  Replay 11415DF8 acquis : retouche locale de l'arbre promu, choix par proximité
  pour seed 43, Preview/Apply/Undo/Redo/Save, position conservée, lancement et
  retour du playtest sans écriture. Copie finale v27 1F51F342, source intacte,
  processus fermés. Réouverture finale, cas de chargement/chevauchement et
  qualification complète encore dus ; voir R2_GENERATED_PROMOTION.md.
  Candidat AD6DDA38 : réconciliation 44→42, Apply/Save et réouverture acquis
  sur copie DE035294 ; identité/pin de l'objet promu visibles. Retouche graphique
  post-réconciliation et playtest toujours dus, aucun verdict de bout en bout.
  Amélioration ultérieure codée : candidats natifs triés près de l'objet auteur,
  distance affichée, choix explicites uniques et révocables ; exclusions sans
  placement classées par ID. Test CPU ciblé PASS, nouveau replay encore dû.
  Les paragraphes suivants sont l'historique d'implémentation, non l'état courant.
  Bouton/raycast Studio et préparation complète raccordés en code ; Clippy WASM
  PASS. Aperçu combiné, Apply/Undo/Redo/Save/Open/régénération/playtest non qualifiés.
  Raccord ultérieur : filtrage exact et densité graphique couverts par test CPU,
  sources Studio/moteur et snapshot playtest raccordés ; Clippy WASM PASS.
  Sélection, résolution du pin, conflits avant commande et parcours graphique
  de bout en bout encore dus. Les notes suivantes retracent les étapes initiales.
  Inspection du 15 septembre : placements manuels disponibles, mais aucun lien
  persistant instance générée → suppression du scatter → placement auteur.
  Prochain lot fonctionnel prioritaire selon ADR-0014 ; contrat et preuves à
  obtenir dans `docs/evidence/gate2/R2_GENERATED_PROMOTION.md`. Non implémenté.
  Première persistance ensuite ajoutée : tête v8, archives 22/23/24, préparation
  atomique et origine conservée après retrait du placement. Test CPU et Clippy
  Projet PASS ; résolution source, filtrage scatter, conflits et UI encore dus.

## R2-D — Splines

Insertion d'un point entre deux voisins raccordée au brouillon Studio : midpoint,
repère orange et même commande Replace. 14 tests world PASS dont cycle complet
en mémoire, Clippy world/WASM PASS ; contrôle du bouton réel encore dû. Aucun
critère coché sur cette seule preuve ; détails dans R2_SPLINE_EDIT.md.

Repères de points facultatifs : replay ciblé 5BE8BF4B acquis (trois repères,
point choisi orange, désactivation sans mutation). Même candidat : chemin et
végétation affichés, Jouer/retour édition fonctionnels avec identité composite.
Copie/source intactes, processus fermés. Pas de qualification graphique 64 chemins,
de nouvelles mesures FPS ni de verdict global R2-021. Détails : R2_SPLINE_EDIT.md.

Raccord des exclusions séparées implémenté : Studio/précontrôle/playtest,
préparation native et WASM, qualité et garde de commit GPU. Treize tests world
et 16 renderer PASS (2 sondes ignorées), quatre profils et ticket d'upload simulé ;
Clippy natif/WASM PASS. Replay graphique borné acquis ensuite ci-dessus.

Exclusions de chemins séparées préparées côté CPU : nouveau constructeur du
générateur, 32 exclusions manuelles + 64 chemins testés sans changer les masques.
Cinq tests et Clippy PASS. Raccord renderer/Studio/précontrôle/playtest réalisé
ensuite ; l'adapter historique ne reste consommé que par les tests de référence.

Replay corrigé 7107B3C5 acquis : clic, trait simplifié, cyan, abandon, Apply brun
et Undo vers le tracé initial observés. Copie sauvegardée intacte, processus
fermés. Save/Redo de ce geste, interactions complexes et critères restants non
déduits ; R2-021 reste ouvert. Voir R2_SPLINE_EDIT.md pour les hashes et limites.

Replay 826A3551 : ouverture et chemin existant visibles ; placement refusé à tort.
Cause rayon curseur 1 m sous minimum moteur 2 m, corrigée dans les deux appels.
Clippy WASM web strict PASS (3,86 s), fmt PASS ; replay corrigé encore dû.
Tracé continu/aperçu cyan non qualifiés. Copie intacte, processus fermés.

Clic terrain, tracé continu simplifié et aperçu cyan avant Apply implémentés
après FD7CC2D7. Tests CPU ciblés PASS ; qualification graphique commune encore
due, sans déduire la réussite de ces outils du replay numérique précédent.

Replay FD7CC2D7 acquis : prolongement à trois points via Ajouter, brouillon
conservé après Examiner, Apply, virage visible, Save v7 et réouverture. Aucun
test de refus UI, vue rapprochée ni Undo/Redo graphique déduit. R2-021 reste
ouvert, preuve détaillée `docs/evidence/gate2/R2_SPLINE_EDIT.md`.

Raccords internes multipoints implémentés après AF3EF695 : sections/indices
communs, refus de plis et dégagement végétal extérieur. Sept tests CPU et Clippy
natif/WASM PASS ; rendu des virages non acquis. Quota d'exclusions et intersections
entre tracés restent ouverts, aucune case cochée. Voir `R2_SPLINE_EDIT.md`.

Replay `AF3EF695…` : création d'un chemin droit de 180 m par le panneau,
Apply/Save v7, playtest actif à X/Z=0, retour et réouverture visibles acquis.
Copie de preuve uniquement, original intact et processus fermés. Bug Leptos
du bouton Ajouter un point corrigé après l'essai ; reprise multipoint encore
à qualifier au prochain candidat regroupé. Pas de clôture de R2-021 ni de
Gate 2 : gestes, raccords/intersections et autres tracés restent dus.
Preuve courante : `docs/evidence/gate2/R2_SPLINE_EDIT.md` ; notes historiques ci-dessous.

Corridors végétaux dérivés et raccord playtest codés. Deux tests de génération
prouvent le dégagement sans changer les couches source ; Clippy WASM PASS.
Quota total 32 exclusions, intersections et vérification graphique encore ouverts.

Projection de rubans sur terrain raccordée après Apply et acquittement du hash
terrain, ressources possédées remplacées. Deux tests CPU et Clippy WASM PASS.
Rendu réel, jonctions/intersections, végétation et playtest non acquis ; pas de
case cochée. Limites détaillées dans `docs/evidence/gate2/R2_SPLINE_EDIT.md`.

Panneau Studio d'édition numérique des tracés raccordé à PreviewZone, un seul
inspecteur et diff précis avant/après. Clippy WASM PASS ; parcours graphique
et projection 3D non qualifiés. Prochain lot : projection puis replay regroupé.

Chemins manuels intégrés à la tête de zone et au journal commun : points,
profil largeur/accotement, verrou, Undo/Redo, retry, régénération et relecture
des archives v7/v8/v9 vérifiés en tests project. Projection et parcours Studio
encore absents ; aucune case cochée. Détails : `R2_SPLINE_EDIT.md` ci-dessous.

Préparation moteur-neutre Insert/Replace/Remove implémentée avec hash, verrous
et validation de proposition. Deux tests et Clippy PASS ; intégration à la tête
de zone, journal et Studio encore due. Aucun critère coché :
`docs/evidence/gate2/R2_SPLINE_EDIT.md`.

- [x] `R2-021` création/édition de chemin.
  Qualification locale du 20 septembre sur candidat DA859D53 : création
  multipoint, Preview sans mutation, Apply, Undo/Redo, refus d'une largeur
  invalide, Save/Close/Open et relecture exacte acquis. Reprise du même tracé
  accepté : largeur 3 → 4 m et dernier point Z 5 → 10 m, Preview/Apply,
  Undo/Redo et Save acquis ; archive B0D7B11F relue avec les valeurs attendues.
  La seconde tentative de réouverture est restée bloquée sur un sélecteur
  désactivé malgré une archive annoncée disponible ; persistance disque acquise,
  présentation GPU et revue humaine non déduites. Rapport :
  `artifacts/forge-runtime/gate2-resume-20260919/r2-021-validation-20260920/R2-021-CU-REPORT.md`.
- [x] `R2-022` route avec profil.
  15 septembre, candidat 284B775A : largeur 3 → 6 m examinée/appliquée,
  Annuler/Rétablir observés, Save et archive 5B81DDA3 relue ; points et
  accotement conservés. Essai fermé, sept processus absents. Virages,
  intersections et portée complète du profil restent à qualifier ; détail
  `docs/evidence/gate2/R2_ROAD_PROFILE_SCOPE.md`.
  Portée inspectée le 15/09 : largeur/accotement persistants et raccords de
  section, surface suivant le terrain à +2 cm ; accotement alpha, sans
  terrassement/profil vertical. Qualification réelle non acquise. Voir
  R2_ROAD_PROFILE_SCOPE.md ; ne pas confondre ces capacités dans le verdict.
  Qualification locale du 20 septembre sur candidat apparié 6C1AB803 / WASM
  FADECB49 : profil vertical optionnel rétrocompatible, altitudes par point,
  pente, dévers, limite cut/fill et collider Rapier du ruban accepté. Preview
  sans mutation, Apply, Undo/Redo, Save/Close/Open et relecture du profil
  acquis ; proposition hors cut/fill refusée avec archive DE2F8C5E inchangée.
  GPU et intersections profilées restent explicitement non qualifiés. Rapport :
  `artifacts/forge-runtime/gate2-resume-20260919/r2-022-validation-20260920/R2-022-CU-REPORT.md`.
- [x] `R2-023` rivière et carving.
  Correctif Forward observé sur A64D5ABA : même archive C58F62BC rendue sans
  écran noir. Ajout chemin, Save/playtest/retour/Close/Open combinés acquis,
  archive 825ADA4A intacte après réouverture. Profils optiques, interactions et
  qualification complète restent ouverts (R2_RIVER_CARVING.md).
  Candidat 85FE08E6 : geste de tracé, examen et Save/Close/Open acquis côté
  données ; rendu noir (panic prépasse différée des berges). Correction Forward
  de l'adapter auteur ajoutée, replay A64D5ABA acquis ci-dessus. Source intacte et
  processus fermés ; cas reproductible C58F62BC dans R2_RIVER_CARVING.md.
  Tracé continu X/Z raccordé au brouillon : simplification, minimum quatre
  points et reprise explicite du niveau/section du premier point. Trois tests
  CPU et Clippy natif/Web PASS ; geste GUI et parcours d'acceptation de cette
  nouvelle entrée encore dus. Voir R2_RIVER_CARVING.md.
  Replay 15C48C0E : panneau corrigé à quatre points, examen et Apply/Save acquis.
  Rendu refusé pour révision eau/terrain désynchronisée ; copie dérivée corrigée
  sans toucher la recette, test moteur ciblé et Clippy PASS. Replay correctif
  en préparation ; rendu/playtest rivière non qualifiés, critère toujours ouvert.
  Replay C4C14623 : zone créée/convertie/sauvegardée ; défaut de parsing view!
  du panneau révélé avant application de rivière (128 points de brouillon).
  Correction des attributs rivière/chemin codée, Clippy PASS, replay correctif dû.
  Playtest ensuite raccordé au snapshot de recettes, terrain creusé et gardes
  végétation/eau. Tests snapshot/host et Clippy natif/WASM PASS ; parcours réel
  et collider creusé non qualifiés. Pas de nage/physique de l'eau annoncée.
  Barrière d'eau CPU/ECS ajoutée (identités et ressources réelles), test ciblé
  PASS et Clippy natif/WASM PASS. Prérequis seulement : playtest rivière encore refusé.
  Picking X/Z et insertion médiane dans le brouillon ensuite codés ; Y/section
  conservés au clic, garde de péremption. Clippy WASM PASS ; gestes non observés.
  État d'eau/erreurs visible et retrait sur fin de session ensuite codés ;
  3 tests moteur ciblés PASS (1 nouveau), Clippy moteur/tests et Studio WASM
  PASS. Raccord UI non observé, candidat 133FE370 antérieur à ce lot.
  Panneau numérique rivière ensuite codé (création/édition/verrou/retrait,
  examen avant/après, aperçu relief) ; Clippy Studio WASM PASS. Rendu/panneau
  non encore observés, picking et playtest encore dus ; aucune clôture déduite.
  Brosse dérivée ensuite raccordée aux corrections canoniques : seuls les
  échantillons retouchés sont conservés, conflit de creusement refusé avant
  commande. Trois nouveaux tests ciblés PASS dont historique/régénération
  compatible/relecture des trois archives ; Clippy PASS, pas de preuve GUI.
  Raccord Studio ensuite codé : cache dérivé, terrain/eau/végétation,
  chemins et alignement des objets. Test ciblé chemin sur relief creusé PASS,
  Clippy Studio WASM PASS. Panneau, brosse avec rivière et playtest encore dus ;
  pas de qualification graphique de ce raccord, critère toujours ouvert.
  Adaptateur eau remplaçable ensuite ajouté : préparation avant échange,
  tête/terrain contrôlés, retrait des seules ressources possédées. 2 tests
  headless PASS ; installation/rendu Studio encore dus. Voir R2_AUTHORING_WATER_ADAPTER.md.
  Transmission renderer végétal ensuite ajoutée : préparations native/WASM,
  identité composite et retrait explicite via API historique. 17 tests moteur
  PASS, 2 sondes ignorées ; Clippy natif/WASM PASS. Terrain/eau et Studio encore
  à raccorder, pas de qualification graphique.
  Dégagement végétal à largeur variable ensuite ajouté au générateur (hors quota
  manuel), hash composite, sources préservées. 8 tests intégration + 1 géométrique
  et Clippy PASS ; transmission au renderer/Studio/playtest encore due.
  Projection terrain/eau depuis la tête ensuite ajoutée, sans creusement cumulatif,
  avec protection/conflits des corrections de hauteur et précontrôle de commande.
  7 tests world et Clippy PASS. Végétation, renderer et parcours Studio encore dus.
  Recette persistante ensuite ajoutée : tête v4, archives v10/11/12, commandes,
  verrou, Undo/Redo et régénération/relecture. 4 tests ciblés + 10 régressions PASS,
  1 opt-in ignoré ; Clippy project/WASM PASS. Projection et Studio encore dus,
  playtest explicitement refusé tant que terrain/eau ne concordent pas.
  Préparation pure terrain/eau explicite ajoutée : coupe bornée, lit et berge,
  sources/retouches extérieures conservées. 3 tests CPU et Clippy world PASS.
  Recette persistante ajoutée ensuite ; projection atomique, Studio, rendu et playtest encore dus ;
  aucune qualification produit déduite. Voir `docs/evidence/gate2/R2_RIVER_CARVING.md`.
  Qualification locale du 21 septembre sur candidat 6C1AB803 : nouvelle
  rivière quatre points, Preview sans mutation, Apply avec terrain/eau
  concordants et acquittement CPU/Rapier, Undo/Redo, Save/Close/Open et archive
  3EF87C85 relue avec deux rivières. Le noir immédiat à la reprise disparaît
  après le warmup WebGPU mesuré (~11 à 14,5 s) ; rendu stable confirmé après
  Close/Open, en playtest et au retour édition. GPU, optique, navigation et
  physique de l'eau restent non qualifiés. Rapports sous
  `artifacts/forge-runtime/gate2-resume-20260919/r2-023-validation-20260920/`.
- [x] `R2-024` intersections.
  Fusion des rubans ensuite codée dans la projection partagée : partition sans
  superposition, couverture maximale des accotements, départage stable, sources
  conservées. 4 tests géométriques union et 14 régressions ciblées PASS ; Clippy
  world et Studio WASM PASS. Rendu et coût sur grands tracés non qualifiés, critère ouvert.
  Ancres X/T et auto-croisements codées via une commande atomique : contrôles
  antérieurs conservés, verrous/quotas, refus des superpositions colinéaires.
  Bouton d'examen Studio raccordé au bus ; 5 tests ciblés PASS (Preview, retry,
  Undo/Redo, régénération et relecture), Clippy world/WASM PASS. Fusion des
  surfaces/accotements et parcours graphique restent dus ; pas de réseau lié
  implicitement. Voir `docs/evidence/gate2/R2_PATH_INTERSECTIONS.md`.
  Qualification locale finale du 21 septembre : profils d'ancres interpolés et
  attributs des nouveaux sommets profilés conservés. Luna max : fmt PASS ;
  `path_intersections` 8/8, `path_union` 6/6, `path_surface` 10/10 et engine
  `authoring_paths` 2/2 PASS ; Clippy strict world/engine/Studio natif/WASM
  PASS (avertissement tiers `proc-macro-error2` seulement). Candidat auteur
  `r2-024-validation-20260921-safe-anchor-authoring-final` : EXE
  `68AEA50FBB9F7A4DD6CDE27F9EFCDE7C1150780AE908DB9B87A5A7B7C3682F5A`, WASM
  `DB091055F4B3B4E74903E0DF152347D167540130427F67B4F31CDE06E013FD7D`.
  Computer Use Luna max (`@oai/sky`) : refus propre sur carte réelle avec
  archive initiale 3EF87C85 inchangée ; X synthétique UI Preview 44/91 sans
  mutation, Apply/CPU-Rapier, Undo deux points/Redo trois points, Save
  F5478051, Close/Open persistant et playtest terrain/eau/capsule sans
  sauvegarde implicite. Inspection visuelle directe, sans PNG locale. GPU,
  optique, navigation et physique de l'eau restent non qualifiés ; CI NOT RUN
  — infrastructure billing. R2-025 et Gate 2 restent ouverts.
- [x] `R2-025` clôture ou mur de preuve — PASS local borné, 22 septembre 2026.
  Répétition générique d'un placement épinglé sur chemin accepté codée : pas,
  offsets, orientation, terrain effectif, jusqu'à 256 copies dans une seule
  commande/Undo ; source intacte, copies retouchables et conservées après
  régénération. Panneau Studio raccordé ; 5 tests et 7 régressions ciblées PASS,
  Clippy world et Studio WASM PASS. Module réel, continuité, pivot/collisions et parcours GUI
  non qualifiés ; aucun mur de preuve revendiqué. Voir R2_PATH_PLACEMENT.md.
  Candidat D264A3F0 avec kit construit : six références chargées et fichier réel
  du mur contrôlé dans Studio (SHA/GLB PASS). Mode de packaging Authoring codé
  et testé ; parcours de placement encore dû avec FORGE_AUTHORING_ROOT défini.
  Suite sur carte de tracés existante : mur + 68 copies visibles, une seule
  annulation/restauration (71↔3), sauvegarde acquise. Réouverture de la rangée,
  continuité proche et collisions jouées restent dues. Champs réinitialisés
  après aperçu : correction de durée de vie du brouillon codée, Clippy WASM PASS,
  non encore qualifiée graphiquement. Détails dans R2_PATH_PLACEMENT.md.
  14/09 : sauvegarde rouverte, entrée/sortie playtest acquises, archive inchangée.
  Vue proche révèle une fente entre modules (mortier/pierres en retrait dans le
  générateur) : raccord à corriger via dérivé gouverné, pas en écrasant les pins.
  Dérivé mur D51C291D produit après correction du mortier et du lineup mono-asset ;
  rendu isolé inspecté. Réimport/raccord multi-panneaux/intégration Studio encore
  dus : R2-025 reste ouvert, ancien kit et placements préservés.
  Réimport du dérivé et raccord droit échantillonné acquis : 7 686 rayons sans
  traversée sur trois LOD, témoin historique en échec aux LOD0/1. Rendu de deux
  panneaux inspecté ; intégration Studio, angles et collisions jouées restent dus.
  Remplacement explicite de pins raccordé à l'inspecteur : lot atomique,
  transformations conservées, verrous/ancien pin contrôlés. Deux tests ciblés
  (Undo/Redo/relecture inclus) et Clippy project/Studio WASM PASS ; geste Studio
  réel avec le dérivé encore dû. Aucun remplacement implicite de kit existant.
  Catalogue additif révision 2 préparé : six pins conservés, mur corrigé ajouté
  sous un nom distinct. Candidat C53FDDEA Trunk/Tauri PASS, sept références
  embarquées ; application non lancée, remplacement de la rangée encore dû.
  Studio 182C2A03 : catalogue chargé, remplacement des 69 murs via interface
  Preview/Apply/Save acquis ; archive F432409E. Les 71 transforms/IDs/noms/verrous
  sont conservés, deux arches intactes. Réouverture, vue proche et collisions
  restent dues ; essai fermé et processus absents. Aucun verdict R2-025 complet.
  Réouverture graphique F432409E et playtest proche acquis ensuite (182C2A03) :
  raccord droit observé sans trou traversant, archive intacte après retour.
  Contact joueur non démontré, angles et Undo/Redo UI restent dus ; essai fermé.
  Marche contre collisions et vérification des réglages conservés restent dues.
  Correction angle v5 : l'ancienne preuve v4/B636 est rejetée (parement sur faces
  intérieures et lissage extérieur). GLB A1F7 byte-identique primaire/rebuild,
  relief extérieur et raccords Blender v6 PASS ; kit révision 4 conserve les
  sept pins historiques. Candidat v2 C1BD construit, navigateur Luna : 8 refs,
  A1F7 et précontrôle/taille GLB PASS. Tauri natif/Rapier/collision jouée/revue
  humaine restent non acquis ; Computer Use natif indisponible (Sky/MCP vide),
  CI NOT RUN — infrastructure billing. La revue propriétaire rejette ensuite
  A1F7/v2 pour de gros défauts de parement dans la composition ; le turntable
  isolé n'est pas une approbation artistique. Tentative v7h/v7i conservée sans
  promotion/assemblage/candidat : C858DAFE613C281CF06A9E6DB749ABE85BB6C0F354651D0A31D1D4DA1E91D6B0
  byte-identique, PBR/colliders/budgets PASS, mais projections LOD0/1 extérieure ~0,038/~0,040 m et intérieure
  ~0,747/~0,746 m en échec (normales incohérentes), puis revue Luna stricte
  FAIL sur panneaux lisses. R2-025 reste ouvert et bloque la qualification
  artistique ; correctif déplacé vers R2-026 sans clôturer Gate 2. Preuves :
  [v7h](../../artifacts/forge-runtime/gate2-resume-20260919/r2-025-composition-v7h/)
  et `R2_PATH_PLACEMENT.md`.
  Variantes B636, A1F7/v2 et v7h conservées comme rejetées. Candidat final
  construit par Astra, sans append/promotion/candidat Studio : GLB
  `73A4637E3B2E334F9DB86323D0D8B38C6F4C56E475A798D12E1060CBC75D0F79`
  (2 924 036 octets), LOD 19 456 / 4 744 / 50, `final-primary`/`final-rebuild`,
  sondes finale extérieure/intérieure et QA Luna PASS. Corrections : winding
  source, déterminant miroir et ancienne superposition de LOD du probe. Luna a
  fait la QA indépendante seulement ; Astra a construit. Instruction propriétaire
  du 22 septembre 2026 : revue visuelle **APPROUVÉE** pour ce GLB exact. Cette
  validation n'approuve ni publication, ni kit intégré, ni Studio/Rapier/collision
  jouée, qui restent non qualifiés ; R2-025 et Gate 2 restent ouverts. Kit rev5
  assemblé localement : sept assets préservés, un corner remplacé, placements
  inchangés ; attestation assemblage/runtime `human_art_review: pending` et
  `publication_approved: false`. Preuves :
  [Astra final](../../artifacts/forge-runtime/gate2-resume-20260919/r2-025-composition-astra-v1/).
  Candidat Studio build-only créé sous
  `artifacts/forge-runtime/gate2-resume-20260919/r2-025-validation-20260922-astra-approved/` :
  `Test-StudioCandidateKit` PASS avant build, 15 entrées `INPUTS_VALID`,
  `BUILD_ONLY PASS`, EXE `D7E7CECCE015CBFA4B9E231C86351FDBC24BB14E5D80CCBAEF130C60D37E24ED`,
  WASM `A09DE1B4A68E35FBC90E60EF7ECA17ED19F4B17C2BC05674A5ACDF0336BBFBFB`.
  Huit assets/pins : corner 73A463…0F79, sept autres inchangés, preview/catalogue/
  placements préservés. Revue visuelle propriétaire approuvée, mais
  `runtime_verified: false`, `gate_qualified: false` et aucun lancement
  Computer Use/Studio/Rapier/collision jouée : R2-025 restait ouvert à ce stade.
  Qualification finale sur le candidat v5 : EXE
  `6301C6244E402970C43B5423C1EBDDC8B4AC4A7D2A923868C7E89D74845C186F`,
  WASM `ADD2285E78D9EFA7D8F22397D8BC3F476FEA4D70A0349FA250B95A1AB3D1DB2C`.
  WebView2 natif piloté par CDP : zone ouverte, canvas focalisé avant pointer-lock,
  taps W reconnus, marche continue arrêtée par le mur Rapier, Échap libérant le
  pointer-lock, puis retour édition. Archive source inchangée, SHA-256
  `83E41F31FAD3EAD12E44F172C26A331C7B015AF203145975207A0D7635810D58`.
  Revue Luna max indépendante : cohérence EXE/WASM/15 pins/cinq PNG FHD et verdict
  local confirmés. Réserves conservées : automatisation CDP et non revue humaine,
  trace CDP brute absente du paquet, diagnostic visuel à 1 contact au retour alors
  que le dernier tick de mouvement en rapportait 2 (sol + mur), sans contradiction
  temporelle démontrée. `gate_qualified: false` ; CI
  `NOT RUN — infrastructure billing`. Rapport :
  `artifacts/forge-runtime/gate2-resume-20260919/r2-025-cdp-v5-20260922/R2-025-CDP-REPORT.md`.

## R2-E — Placements et bâtiments

R2-026 en cours : index du kit gouverné, recherche/catégories, fiche et contrôle
volontaire du GLB raccordés au Studio. Deux tests d'index et Clippy assets/WASM
PASS ; parcours réel du panneau encore dû. Aperçu individuel/placement non
acquis : `docs/evidence/gate2/R2_ASSET_BROWSER.md`.
Essai produit ensuite réussi pour recherche/fiche et contrôle du GLB chêne
(candidat 9ea00c8c...), sans charger de scène GPU. Aperçu individuel/placement
et tests UI négatifs encore ouverts ; mêmes preuves, kit source préservé.
Aperçu individuel ajouté puis essayé sur candidat 5473916e... : chêne/pin,
remplacement, zoom et retrait/retour carte visibles, sauvegarde inchangée.
Même moteur/canvas, un objet temporaire sans placement. Clippy/builds PASS,
processus fermés ; cadrage automatique et cas négatifs restent ouverts.
Cadrage automatique ensuite codé et rejoué sur candidat 4e24703a... : rocher et
arche visibles sans zoom manuel, retrait/retour carte, archive inchangée et
processus fermés. Aucun placement acquis ; cas négatifs encore non qualifiés.
Fixture négative durcie le 22 septembre : ancien témoin invalide de 18 octets
exclu, nouveau GLB v2 de 24 octets épinglé au SHA exact et atteignant le chunk
JSON avant refus du schéma ; paramètres/framing et arrêt loopback consignés.
Python 10/10 et préflight Rust 2/2 PASS. Luna max acquiert ensuite en IAB le
refus post-header/chunk/schema sans aperçu, placement ni stale. Le replay lent
est bloqué honnêtement avant GET GLB par l'absence du backend Tauri/projection
en standalone. Le candidat natif v7 `268B8249…04F5` acquiert ensuite cette
frontière : zone projetée, retrait pendant un flux chêne retardé puis sélection
du pin ; le flux termine sans aperçu tardif, sans Preview/Apply/Save, archive
inchangée et nettoyage complet. Les PNG ont été inspectés. Transport ralenti
synthétique sur les octets gouvernés réels, revue automatisée non humaine,
`gate_qualified=false`, CI `NOT RUN — infrastructure billing`. Avec les preuves
positives et négatives précédentes, R2-026 est clos localement par composition,
sans constituer un PASS formel de Gate 2 sous ADR-0006.

- [x] `R2-026` navigateur d’assets.
- [x] `R2-027` drag/drop.
  D2A2866F : aperçu partagé, Abandonner, puis nouvel aperçu acquis sans mutation.
  Échap essayé sans retrait de la proposition : non qualifié (réception/focus
  non établis). Archive intacte et processus fermés ; R2_CATALOG_DROP.md.
  Complément code : activation clavier après drag et annulation d'une réponse
  Preview tardive corrigées ; Échap/Abandonner unifiés. Clippy WASM PASS,
  pas de nouveau candidat graphique : cas négatifs restent non qualifiés.
  Candidat 78005EAD : parcours positif acquis après correction du rayon picker
  1→2 m. Aperçu 71→72, Apply/Undo/Redo/Save/fermeture/réouverture observés ;
  archive isolée 1E48B2F6 conserve les 71 placements précédents. Processus fermés.
  Cas négatifs et annulation du geste encore à qualifier ; critère ouvert à ce stade.
  Essai suivant 67BFB82F : aucun événement de dépôt reçu, cause native/DOM/outillage
  non tranchée. Geste souris Studio remplace ensuite DataTransfer au code ; clic
  simple conservé, même picking et Insert. Clippy WASM PASS, parcours encore dû.
  Premier essai réel 67D52876 : écran secondaire confirmé, début du glissement
  détecté mais aucun aperçu reçu. Point de dépôt et retours d'échec renforcés
  ensuite au code (Clippy WASM PASS), pas encore rejoués. Critère ouvert à ce stade.
  Glissement interne catalogue → terrain codé : point du picking existant,
  contrôle GLB et tête, préparateur Insert partagé avec le clic ; aperçu sans
  Apply/Save implicite. Fichiers externes refusés, paramètres neutres annoncés,
  routage HTML5 Windows configuré. Clippy WASM PASS ; parcours graphique,
  annulation et persistance par ce geste encore dus à ce stade. Voir R2_CATALOG_DROP.md.
  PASS local composite le 22 septembre : le candidat 78005EAD conserve le
  parcours positif Preview/Apply/Undo/Redo/Save/fermeture-réouverture ; le v6
  7CE5EE67 complète sous-seuil, hors-canvas et Échap avant/après Preview sans
  mutation, avec archive exacte et nettoyage CDP. Les deux moitiés ne sont pas
  rejouées sur un candidat unique ; revue humaine en attente,
  `gate_qualified=false`, CI `NOT RUN — infrastructure billing`. Ce cochage
  local ne constitue pas un PASS formel de Gate 2 sous ADR-0006.
  Qualification groupée suivante, 22 septembre 2026 : candidat natif exact
  `268B8249…04F5`, deux processus et copies de fixture frais. Le ledger v14
  acquiert 19/19 sous-cas : picking positif, vide, occlusion par bâtiment et
  annulation ; neuf gestes gizmo plus un verrouillage isolé avec restauration
  observée par Undo ; duplication/suppression, Undo/Redo, Save/Close/Open ; snap
  X/Z sur grille et surface ainsi que deux conflits refusés. R2-029 n'est PASS
  dans le ledger agrégé que si `placements` et `placement-lock` réussissent.
  Source et candidat exacts, audits restaurés, processus/listeners fermés ; sept
  PNG inspectés par Luna max, sans revue humaine. `gate_qualified=false`, CI
  `NOT RUN — infrastructure billing`. Rapport v14 sous
  `artifacts/forge-runtime/gate2-resume-20260919/r2-028-031-world-edit-v7-20260922-v14/`.
  Cette composition clôt localement R2-028 à R2-031, pas la Gate 2 formelle.
- [x] `R2-028` picking.
  Liste explicite des placements ajoutée ensuite, reliée à la sélection du picking :
  un seul inspecteur monté, paramètres d'ajout repliés. Clippy WASM PASS ;
  parcours UI de sélection/cadrage encore dû. Aucun défaut de ciblage déclaré résolu.
  Outil de sélection des placements manuels raccordé : rayon Bevy sur triangles
  des meshes visibles, marqueurs dérivés LOD0 et filtrage de l'inspecteur par ID.
  Clippy/builds PASS. Replay `34bca4ce…` : clic sur arche gauche et filtrage sur
  son ID acquis sans mutation. Occlusion/clic vide et qualification complète dus.
- [x] `R2-029` gizmo translation/rotation/échelle.
  Candidat regroupé `09ea75b6…` construit et ouvert : v6 relue, terrain/deux arches
  affichés et panneaux présents. Manipulations non qualifiées : ciblage incohérent
  après défilement pendant l'essai, cause non établie. Aucun Apply/Save, archive
  intacte, fenêtre fermée. Ne pas rejouer la conversion déjà acquise.
  Replay regroupé `5fc11a29…` : poignée Y visible et proposition de déplacement
  obtenue, non appliquée avant interruption Échap. Ne prouve ni Save/Undo du
  geste, ni rotation/échelle ; détails dans R2_MANUAL_PLACEMENTS.md.
  Implémenté : poignées de translation mondiales avec grille/surface, anneaux de
  rotation mondiaux, poignées d'échelle locales ; état dérivé pendant le geste,
  Preview unique au relâchement, garde de tête, verrou et annulation. Panneaux
  numériques précis et glissement terrain utilisent les mêmes commandes.
  Vérifié : tests mathématiques ciblés, contrats historiques/Undo/régénération,
  Clippy world et Studio WASM strict. Les anciens replays `ff785805…` et `35580363…`
  prouvent insertion/Save/relecture et duplication, pas ces nouveaux gizmos.
  À qualifier : rendu, picking/occlusion, annulation, commandes et Save/relecture
  du candidat regroupé. Snapping angulaire ajouté ensuite : sélecteur de pas,
  règle relative partagée aperçu/final, test ciblé et Clippy WASM PASS ; geste
  encore non qualifié. Repère local/mondial des anneaux ensuite raccordé avec
  frame initiale commune au dessin, picking et rayon ; Clippy WASM PASS,
  qualification gestuelle encore due. Translation locale ensuite raccordée :
  mètres indépendants de l'échelle, grille relative au départ, conflit surface
  refusé ; test ciblé et Clippy WASM PASS. Gestes locaux encore à qualifier ;
  collisions manuelles encore non raccordées. Détails et
  historique préservés dans R2_MANUAL_PLACEMENTS.md. Gate 2 ouverte.
- [x] `R2-030` duplication et suppression.
  Candidat D29317A0 : duplication Studio examinée/appliquée, Undo/Redo observé
  (2 → 1 → 2 bâtiments), identité stable et Save archive B4FB1904. Source intacte,
  processus fermés ; réouverture UI et suppression non acquises par ce replay.
  Duplication bâtiments ajoutée : commande typée, ID neuf déterministe côté Studio,
  nom/position explicites, copie déverrouillée conservant la porte retouchée.
  Original verrouillé intact et preview/retry/archives/Undo-Redo testés.
  Geste et rendu Studio non encore observés pour ce raccord.
  Duplication typée ajoutée : source conservée, nouvel ID, transform explicite,
  copie déverrouillée ; Preview/Undo/Redo/relecture et erreurs testés. Panneau
  X/Y/Z et duplication raccordé aux commandes. Replay ciblé `35580363…` : cadrage,
  déplacement, copie visible et Save de deux IDs distincts sur copie isolée PASS.
  Suppression et Undo/Redo UI restent à qualifier ; R2-030 reste ouvert.
- [x] `R2-031` snap surface/grille.
  Alignement des poignées de translation raccordé ensuite : quantification de
  l'axe X/Z actif seulement, surface après alignement, conflit Y/surface refusé.
  Plan terrain capturé au départ et règle commune à l'aperçu continu/final.
  Test ciblé et Clippy world PASS ; geste et rendu Studio encore dus.
  Première implémentation partagée pour déplacement/copie via inspecteur : grille
  mondiale X/Z puis surface effective/offset, valeurs absolues préservées après
  régénération. Trois tests CPU (dont relief retouché) et Clippy world/Studio WASM
  PASS. Grille à l'insertion ensuite raccordée au même calcul, paramètres validés
  avant chargement GLB. Test d'intégration Preview/Undo/Redo/relecture et conservation
  absolue après changement de relief PASS. Replay UI et raccord aux gizmos encore dus.
  Essai `34bca4ce…` refusé avant mutation par limite d'archive (3 963 115 octets,
  13 commandes). Corriger la répétition des corrections dans l'encodage compact
  avant de qualifier l'alignement sur cette zone ; ne pas contourner le garde.
  Correction v6 implémentée via conversion explicite : replay CPU sur cette archive
  3 963 115 → 2 560 237 octets, 13 reçus identiques ; retouche précédemment bloquée
  appliquée/relue en mémoire (2 561 438 octets), source intacte. Conversion UI encore due.
  Candidat v6 `a8344c0d…` construit (Trunk/Tauri PASS) ; essai UI empêché par accès
  Windows refusé, aucune édition effectuée. Copie isolée intacte et processus fermés.
  Replay suivant `5fc11a29…` : conversion UI et Save v6 acquis, 13 commandes,
  2 560 237 octets, tête conservée et hash disque confirmé après interruption.
  Réouverture UI et alignement sur cette version restent à qualifier.
  Qualification groupée suivante, 22 septembre 2026 : candidat natif exact
  `268B8249…04F5`, replay v3 isolé. R2-032 passe 3/3 : panneau complet, huit
  volumes projetés, refus exact d'une dimension invalide sans IPC, édition de
  tous les champs, verrouillage, Undo/Redo, Save/Close/Open, valeurs/verrou et
  archive exacts après réouverture. R2-033 passe 1/1 : départ devant la porte,
  W jusqu'à l'intérieur puis au mur arrière (deux contacts), S vers l'extérieur,
  pointer-lock acquis/libéré et retour édition sans mutation de l'archive.
  Coordonnées locales Z : -5,25 → -2,26 → 2,63 → -3,86. Audit start/stop
  restauré après les deux rechargements, source/candidat intacts et nettoyage
  complet. Quatre PNG 1920×1080 inspectés par Luna max ; champs/huit volumes
  restent prouvés par CDP plutôt que par un gros plan humain. `gate_qualified=false`,
  CI `NOT RUN — infrastructure billing`. Rapport sous
  `artifacts/forge-runtime/gate2-resume-20260919/r2-032-033-building-playtest-v7-20260922-v3/`.
  Clôture locale des deux critères, sans PASS formel de Gate 2 sous ADR-0006.
- [x] `R2-032` bâtiment paramétrique simple.
  Renommage Auberge Preview/Apply et duplication observés dans D29317A0 ;
  Save B4FB1904 (31 commandes), pas de réouverture UI dans ce replay.
  Renommage via SetName et inspecteur ajouté : aperçu, ID/géométrie/porte conservés,
  verrou et noms invalides contrôlés. Retry/trois archives/Undo-Redo testés ;
  geste Studio du champ de nom encore dû, sans clôture de critère.
  Candidat 21854D33 : sol intérieur visible dégagé ; largeur de porte 1,4 → 1,8 m
  saisie et Preview/Apply/Save, fermeture/réouverture puis départ playtest acquis.
  Correction X=1,5 m conservée, archive BA9B8BE5, original intact, processus fermés.
  Autres nouveaux champs et franchissement clavier non qualifiés par ce replay.
  Exclusion des racines végétales dans les emprises tournée implémentée côté
  world et testée sans mutation des masques. Raccord moteur natif/WASM/Studio
  implémenté, barrière playtest incluse ; Clippy Web PASS et test moteur
  ajout/retrait/refus invalide PASS. Rendu du nouveau raccord encore à vérifier.
  Inspecteur complété : largeur/hauteur de passage et épaisseurs murs/dalles
  raccordées au SetDimensions et au résumé d'aperçu. Conservation de ces champs
  après régénération/archives/Undo-Redo testée ; Clippy WASM PASS. Saisie réelle
  des nouveaux paramètres non encore observée ; ne ferme pas le critère.
  Candidat 168F6C25 : cadrage proche, déplacement (20,3,20), correction porte
  X=1,5 m, Preview/Apply/Save acquis dans le Studio ; archive v15 E9DFB69E,
  26 commandes. Pas de réouverture/Undo UI de ces deux commandes ; critère ouvert.
  Position/orientation modifiables via commande SetPose et inspecteur ; quatre
  tests ciblés PASS (verrou, empreinte tournée, porte conservée, Undo/Redo).
  Geste Studio de déplacement encore dû, pas de clôture du critère.
  Candidat 974CAD82 : création/aperçu cyan/Apply/Save/fermeture/réouverture UI
  acquis sur copie isolée v15 (2B718781), rendu lointain observé. Aucun passage
  joué ni retouche de porte UI qualifié. Cadrage direct ajouté ensuite au code,
  Clippy WASM PASS, geste à vérifier. Processus d'essai fermés ; critère ouvert.
  Raccord canonique initial implémenté : commandes bâtiments, correction de
  porte conservée, archives 13/14/15 ; 2 tests d'intégration et Clippy projet
  PASS. Panneau Studio raccordé au code (création/dimensions/porte/verrou/retrait,
  Preview puis Apply/Save), résumé numérique explicite. Clippy Studio WASM PASS.
  Projection acceptée raccordée au code Studio avec adapter moteur partagé :
  huit volumes, meshes/colliders de mêmes transformations, nettoyage possédé.
  Test CPU du cycle de vie PASS, Clippy natif bibliothèque et Studio WASM PASS.
  Observation UI/GPU, synchronisation physique et sauvegarde disque restent dues ;
  critère ouvert. Aperçu cyan ensuite raccordé au code : lot sans collision,
  reçu hash/révision contrôlé, rendu accepté restauré à l'abandon. Trois tests
  de domaine PASS ; observation graphique due, playtest bloqué explicitement.
  Paramètres validés/sérialisables côté projet et calcul côté world raccordés ;
  ouverture à décalage local absolu, identité stable au redimensionnement.
  6 tests ciblés et Clippy des deux crates tous targets PASS à l'étape géométrique.
  Calcul `building_layout` commencé : pièce de plain-pied, huit volumes et
  passage de porte, IDs sémantiques indépendants des dimensions. Trois tests
  ciblés PASS à cette première étape, avant le raccord canonique ci-dessus ;
  détail et limites dans R2_BUILDING_LAYOUT.md.
- [x] `R2-033` intérieur et porte franchissable.
  Franchissement CPU natif désormais acquis : 8 scénarios contrôleur/Rapier et
  volumes de production, 4 rotations, entrée/sortie et mur bloquant (0,18 s).
  Sol synthétique et commandes rejouées ; passage clavier Studio encore dû.
  Candidat 707BA511 : ouverture/encadrement désormais lisibles en éclairage
  d'inspection ; départ face à l'entrée observé en playtest, désactivation
  de la lumière vérifiée. Archive inchangée et processus fermés. Franchissement
  physique joué toujours dû, pas de clôture de ce critère.
  Départ porte désormais orienté vers l'entrée dans le code : cap temporaire
  validé par l'hôte, ancien JSON compatible, aucune mutation de recette.
  Trois tests ciblés PASS ; orientation réelle et franchissement à qualifier
  dans le prochain candidat regroupé, pas dans les anciens binaires.
  Éclairage d'inspection réversible ajouté : bascule observée dans 4EFAFB4C.
  Essai ponctuel 9F38E462 insuffisant pour distinguer l'ouverture ; intensité
  révisée dans le code, Clippy WASM PASS, dernier réglage non observé.
  Aucune qualification de lisibilité/franchissement déduite. Voir R2_BUILDING_LAYOUT.md.
  Candidat 7DD26394 : cadrages de façade/porte et départ direct observés ;
  ouverture visible depuis la caméra de playtest, aucun franchissement attesté.
  Façade ombragée et intérieur presque noirs : lisibilité à corriger.
  Bouton Studio « Jouer devant cette porte » raccordé au StartPlaytestAt existant,
  départ temporaire calculé dans le repère accepté. Test de calcul/Clippy WASM
  PASS ; geste, orientation et franchissement réel restent à qualifier.
  Candidat 168F6C25 : démarrage du mode playtest avec avatar observé après
  sauvegarde du bâtiment retouché. Franchissement de porte toujours non qualifié.
  Barrière playtest bâtiments implémentée : snapshot gelé et colliders statiques
  contrôlés dans le backend Rapier avant simulation. Tests ciblés moteur/domaine
  et contrôle partagé PASS ; ne prouve pas le franchissement réel de la porte.

## R2-F — Commandes, sauvegarde et playtest

Journal agrégé ensuite raccordé au lecteur partagé et au panneau Studio :
overrides, masques/verrous, recettes et Undo/Redo, pagination bornée et hash de tête.
5 tests ciblés et Clippy project/Studio WASM PASS ; replay UI agrégé encore dû.
Pas de second journal ni de réécriture d'archive. Voir R2_AUTHORING_HISTORY.md.
Replay de lecture ensuite acquis (`e4e75bc6…`) : dix reçus affichés, seed/verrou
et inverses lisibles ; inspecteur de densité et boutons cohérents. Archive intacte,
processus fermés. Pas de qualification globale ; playtest agrégé encore à raccorder.

Historique des retouches raccordé au Studio : pagination, résumés avant/après,
Undo/Redo lisibles, boutons reflétant les possibilités de la session. Deux tests
ciblés et Clippy natif/WASM PASS ; qualification graphique groupée encore due.
R2-037 reste ouvert : `docs/evidence/gate2/R2_AUTHORING_HISTORY.md`.
Raccourcis Save/Undo/Redo raccordés au même bus ; test de correspondance et
Clippy WASM PASS, parcours focus/commande graphique encore dû avec l'historique.
Preuve : `docs/evidence/gate2/R2_EDITOR_SHORTCUTS.md`.
Replay groupé historique/Ctrl+Z/Y/S effectué : journal actualisé, relief retiré
puis rétabli, archive sauvegardée exacte ; Ctrl+Z dans Nom reste local. Original
intact, processus fermés. Limites restantes (pages longues, autres focus/gestes)
dans `docs/evidence/gate2/R2_AUTHORING_HISTORY.md` ; pas de clôture globale R2.

Récupération raccordée au protocole et au panneau Studio, avec examen/confirmation,
revalidation des deux archives et conservation des versions. Quatre tests ciblés
catalogue/récupération PASS. Replay Studio groupé : première sauvegarde récupérée,
archive de secours exacte, ouverture depuis catalogue et terrain affiché au hash
attendu ; original intact et processus fermés. La campagne fraîche du 22/09
complète ce replay par 13/13 tests exacts de panne, conflits, staging, identité,
promotion, sauvegarde atomique et réouverture. Les cas conflictuels et le choix
sans promotion restent automatisés, non rejoués graphiquement. Voir
`docs/evidence/gate2/R2_STUDIO_RECOVERY.md` et
`docs/evidence/gate2/R2_INTERRUPTED_SAVE_RECOVERY.md`.

Qualification locale de composition — 22 septembre 2026 : les replays natifs
CDP v14 placements et v3 bâtiment/playtest utilisent le candidat exact
`268B824946C53323C899276BC918BF5348F87E1A8CC2F6B50CB7A7C99B3804F5`.
Ils complètent les preuves de code et les qualifications antérieures terrain et
workspace pour R2-034, R2-035, R2-036, R2-040 et R2-041. Les mutations passent
par le bus typé unique ; Preview reste non mutateur et lié au reçu examiné ;
Undo/Redo, Save/Close/Open exacts et Jouer/Retour sont observés par les contrôles
Studio réels. Clôture locale par composition uniquement : revue visuelle agent,
pas revue humaine, `gate_qualified=false`, CI `NOT RUN — infrastructure billing`.

- [x] `R2-034` bus de commandes.
- [x] `R2-035` dry-run/diff.
  Diff terrain lisible : altitude et nom avant/après, héritage distingué des
  retouches et couches vides explicites. Source du reçu vérifiée contre la tête
  courante ; aucune mutation. Test ciblé PASS, fmt et Clippy WASM PASS le 14/09.
  Affichage Studio encore à qualifier ; voir R2_TERRAIN_DIFF.md.
  Ctrl+Entrée applique uniquement l'aperçu examiné ; une erreur locale ne l'efface
  plus. Test de raccourcis et Clippy WASM PASS ; parcours clavier/focus encore dû.
  Actions et diff regroupés dans une barre de commande intégrée au flux, sans
  nouveau chemin de mutation ni occultation des contrôles.
  Clippy WASM PASS ; affichage pendant défilement et focus encore à qualifier.
- [x] `R2-036` undo/redo.
- [x] `R2-037` audit.
  Clôture locale par composition le 23/09 sur candidat v9b : historique auteur
  32+12 entrées, puis workspace 12→21 opérations réelles via neuf modules
  créés dans une copie, navigation GUI 1–20/21/retour sans chevauchement ;
  source exacte préservée. Pas de revue humaine ni de Gate 2 formelle déduites.
  Voir `GATE2_CLOSURE_WORK_20260923.md`.
  Historique workspace paginé ajouté : créations projet/module, rattachements,
  mises à jour de zone, Undo/Redo ; sans snapshots lourds ni objets relus. Tests
  pagination/ancien hash/reprise/host PASS ; trois opérations réellement affichées
  après reprise Studio sur A74AEDAE. Pagination multipage GUI encore due.
  Voir R2_WORKSPACE_FOUNDATION.md ; ne clôture pas l'audit global.
  Résumé d'historique complété pour chemins/rivières/bâtiments : objets changés
  par ID, nom/verrou/hash avant/après, couches vides explicites. Trois tests
  history et Clippy natif/Web PASS ; affichage réel encore dû, sans modification
  du format des reçus. Voir R2_OBJECT_HISTORY.md.
- [x] `R2-038` sauvegarde atomique.
  Qualification programmatique locale : campagne fraîche 13/13, interruption
  réelle après sync du staging, invariant ancien-ou-nouveau jamais partiel,
  verrouillage concurrent, promotion et réouverture exacte. La durabilité du
  répertoire lors d'une coupure électrique sur tout système de fichiers n'est
  pas revendiquée.
- [x] `R2-039` récupération interruption.
  Qualification locale par composition : campagne fraîche 13/13 et replay
  Studio explicite première sauvegarde -> examen/confirmation -> promotion ->
  catalogue -> ouverture exacte. Le harness frais ne rejoue pas l'UI et le
  replay Studio porte sur un candidat antérieur ; revue humaine et qualification
  Gate 2 restent non acquises.
- [x] `R2-040` réouverture exacte.
  Reprise simple workspace/module/zone plate acquise sur A74AEDAE : même hash
  terrain et archives inchangées, capture conservée. Ce parcours ne qualifie pas
  encore l'ensemble des retouches/multizones ; voir R2_WORKSPACE_FOUNDATION.md.
- [x] `R2-041` bouton Jouer.
  Départ X/Z temporaire ensuite implémenté : commande host sur hash exact,
  snapshot/Inspect, projection sur terrain, aucune écriture de zone. Commandes
  Jouer/Retour dans la barre fixe. Clippy WASM et 9 tests project/host PASS ;
  parcours graphique choisi et collisions encore dus. Voir R2_PLAYTEST_START.md.
  Raccord placements ensuite implémenté : snapshot immuable et barrière fraîche
  source/racines/hiérarchie/pose/forme/échelle Rapier avant Drive, remplaçant le
  refus systématique. Tests project/host, moteur ciblés et Clippy WASM final
  PASS. Replay Studio 77049619 : Jouer, simulation active avec placements,
  personnage au sol et retour édition acquis ; archive inchangée et nettoyage
  vérifié. Mouvement clavier et contacts/passage de l'arche encore dus ; départ
  choisi par l'auteur raccordé ensuite, sans retouche de fixture.
  Les primitives UCX réutilisent le constructeur partagé du kit ; diagnostic
  d'absence, erreur et attente. Historique des vérifications et limites :
  `docs/evidence/gate2/R2_MANUAL_COLLISIONS.md`.
  Préparation de snapshot agrégé ajoutée au domaine/host : hashes tête et paire
  effective, barrière source/terrain/végétation, lecture sans démarrage ni écriture.
  Raccord host/Studio ensuite implémenté : Start sur tête complète, snapshot,
  barrière avant Drive et retour sans écriture. Tests domaine/host et Clippy
  project/Studio WASM PASS. Replay candidat 6c932781 : Jouer, capsule au sol et
  végétation, retour édition, archive inchangée. Déplacement clavier WebView et
  attente longue non qualifiés ; aucune qualification globale déduite.
  Voir R2_AUTHORING_PLAYTEST.md.
- [x] `R2-042` retour diagnostics.
  StopPlaytest et texte du dernier runtime observés après retour édition sur
  candidat v9b, copie isolée et source inchangée ; preuve automatisée locale,
  sans qualification GPU ou humaine (`GATE2_CLOSURE_WORK_20260923.md`).
- [x] `R2-043` parcours de preuve sans JSON manuel.
  Candidat v9b : neuf étapes UI PASS sur cinq copies indépendantes v17–v21,
  vallée, forêt, chemin dégagé, rivière Z=40, maison X=12/Y=5,2,
  clairière, régénération, Save/Close/Open, Jouer/Retour. Captures d'ensemble,
  bâtiment et chemin ; source intacte. La recette antérieure enterrait la
  maison sous le terrain ; elle ne sert plus de preuve visuelle.
  R2-052, rendu GPU et revue humaine restent distincts ; voir
  `GATE2_CLOSURE_WORK_20260923.md`.
  A64D5ABA : forêt/maison/rivière/chemin combinés, entrée playtest et retour,
  Save/Close/Open acquis, archive 825ADA4A inchangée et processus fermés.
  Terrain encore plat ; vallée sculptée, retouches puis régénération et
  déplacement joué ne sont pas déduits. Preuve : R2_RIVER_CARVING.md.
  Tranche 0F315A98 acquise : nouvelle zone plate → forêt → maison → Save/Close/Open,
  sans conversion ni JSON manuel (R2_ZONE_CREATION.md). Ne couvre pas encore
  le parcours complet vallée/chemin/rivière, retouches conservées et playtest.

## Ordre de réalisation de Gate 2

Les groupes R2-A à R2-F sont des domaines de couverture, pas un ordre imposant de finir tous les
pinceaux avant les commandes et la sauvegarde. Une fois Gate 2 ouverte, commencer par une tranche
création → retouche → sauvegarde/réouverture → playtest, puis élargir les outils.

## R2-G — Conservation des retouches et précision

ADR-0014 accepté le 13 septembre puis consolidé le 22 septembre 2026. Il définit
la tête de zone unique, la séparation base/corrections, les overrides typés, les
IDs et portées de verrous, ainsi que le refus transactionnel des conflits.
ADR-0016 explicite l'autorité workspace et ses objets de zone immuables : aucune
voie historique ne reste éditable en parallèle après adoption.

Le code courant matérialise ce contrat dans `ZoneAuthoringHead`,
`ZoneEditSession`, `FileZoneSession`, `ActiveSession` et `RegenerationReview`.
Les preuves domaine/disque sont suivies dans `R2_ZONE_HEAD.md` et
`R2_ZONE_REGENERATION.md`. La conservation porte/clairière relève séparément
de R2-045 ; les conflits, inspecteurs, caches et parité geste/commande restent
suivis par leurs critères propres. Les états intermédiaires datés dans les
dossiers de preuve demeurent historiques et ne décrivent pas tous l'état courant.

- [x] `R2-044` spécifier par ADR les sources, overrides, verrous et conflits de régénération ; aucune seconde source canonique cachée.
  Clôture documentaire locale du 22 septembre 2026 : ADR-0014 consolidé avec
  l'autorité autonome et workspace, les données dérivées explicitement exclues
  de l'autorité, les overrides typés, la portée des verrous et les refus de
  régénération. Corroboration code et preuves locales ; aucun PASS ADR-0006,
  aucune revue humaine ni clôture de Gate 2 déduits.
- [x] `R2-045` conserver exactement une porte déplacée et verrouillée et une clairière peinte après régénération locale, fermeture de l'historique et réouverture.
  Verdict local consolidé : archive 5269A9B0 rouverte dans un nouveau processus
  F1C5DB66 lors du parcours index ; forêt, clairière et maison observées, capture
  path-framed.jpg, archive intacte. Comparaison avant/après recontrôlée : seed
  42 → 43, corrections égales, porte 1,5 m/bâtiment verrouillé, masque editor_trees
  verrouillé. Aucun nouveau test graphique nécessaire. Clôture de ce scénario,
  pas du rebasage du relief ni de Gate 2 ; détail R2_ZONE_REGENERATION.md.
  Scénario synthétique combiné PASS : porte corrigée/bâtiment verrouillé et
  densité corrigée/verrouillée conservés après nouvelle seed, réordre des couches,
  destruction de session et reprise DocumentStore/FileZoneSession sur disque.
  Studio A64D5ABA : porte X=1,5 m/bâtiment verrouillé et clairière peinte/verrouillée,
  seed 42 → 43, Save : corrections strictement identiques (archive 5269A9B0).
  Fermeture complète acquise ; observation après relance restante, outil Windows
  en erreur deux fois, essai arrêté sans boucle de tests. Pas d'effacement du
  journal ni de verrou fin de porte. Voir R2_ZONE_REGENERATION.md.
- [x] `R2-046` ajouter ou réordonner une feature sans changer les IDs non affectés ; diagnostiquer suppression et références orphelines.
  Replay v9b : copie/réordre/retrait de couche avec IDs stables, refus des
  références orphelines et sous-cas placement non-couche ; tests domaine déjà
  acquis. Preuve automatisée locale : `GATE2_CLOSURE_WORK_20260923.md`.
  Réordre des couches de végétation raccordé le 15 septembre : commande commune,
  boutons Monter/Descendre et diff d'ordre ; test preview/retry/archive/Undo/Redo
  PASS, Clippy WASM PASS. Copie/retrait raccordés ; test ciblé PASS pour masque
  peint verrouillé, refus des corrections orphelines et conservation exacte
  de 8 404 instances non affectées. Geste Studio et portée au-delà des couches
  de végétation restent dus : `docs/evidence/gate2/R2_LAYER_ORDER.md`.
  Aperçu 3D végétal de recette raccordé ensuite avec restauration de la version
  acceptée ; reçu contrôlé et test sans mutation PASS, Clippy WASM PASS.
  F5B45FD1 : densité 35 → 0 %, Preview/Abandon/Apply/Undo/Redo/Save observés,
  archive 56AEDC0D relue avec corrections/terrain conservés. Échap, opérations
  de couches et reprise GUI restent ouverts ; détail dans R2_LAYER_ORDER.md.
- [x] `R2-047` signaler un conflit avec une propriété verrouillée sans la déplacer ni l'écraser silencieusement.
  Replay v9b : suppression de couche à densité verrouillée refusée sans écriture,
  déverrouillage explicite, retrait et Undo exact ; voir
  `GATE2_CLOSURE_WORK_20260923.md`.
  Refus explicatifs partagés préparation/exécution : cible, verrou, cause et
  piste corrective, codes techniques conservés. 4 tests zone_regeneration PASS,
  dont même refus pour commande brute et archive inchangée ; Clippy project
  PASS. Affichage GUI et résolution auteur complète encore à qualifier.
  Régénération : conflit BuildingConstraintChanged avec ID/verrou/raison pour
  emprise incompatible, commande brute refusée sans mutation (test ciblé PASS).
- [x] `R2-048` retoucher une transformation en unités explicites, choisir pivot/repère et snap, puis annuler/rétablir sans JSON manuel.
  Candidat v9b, `transversal-v9b-20260923-v6` : grille monde au mètre,
  pivot monde externe, axe Y/180° et Undo/Redo exacts par contrôles Studio ;
  capture après orbite négative de 140 px relue par Luna max : arche cible et
  anneaux/pivot lisibles, pin non bloquant. La symétrie de l'arche interdit de
  lire 180° sur l'image seule : quaternion et reçu en font foi. Revue humaine
  et Gate 2 formelle non déduites (`GATE2_CLOSURE_WORK_20260923.md`).
  Saisie locale en mètres raccordée aux mêmes règles que les poignées, avec grille
  relative ; Clippy WASM PASS. Pivot numérique propre/point monde ensuite ajouté :
  rotation de position/orientation, deux tests géométriques et Clippy WASM PASS.
  Pivot ensuite partagé avec les anneaux (dessin/picking/rayon/résultat) :
  Clippy WASM PASS. Parcours visuel du pivot externe encore dû ; aucun verdict complet.
- [x] `R2-049` exposer dans l'inspecteur la source de la valeur, son override et son verrou ; borner la sélection aux objets ou couches visés.
  Replay v9b par composition : densité, placement, bâtiment, chemin accepté,
  brouillon de rivière sur la fixture transversale ; rivière acceptée après
  Save/Close/Open du parcours complet, sans IPC de mutation. Sources préservées,
  aucun verdict humain/GPU déduit ; `GATE2_CLOSURE_WORK_20260923.md`.
  Complément implémenté : porte recette/override/effectif et verrou bâtiment
  entier ; placements AssetId/hash/pose absolue ; chemins/rivières acceptés
  distingués des brouillons. Test origine porte PASS, Clippy Web PASS ; texte
  graphique non qualifié. Voir R2_INSPECTOR_ORIGINS.md.
  Densité par ID de couche raccordée : recette/effective, override/verrou, provenance
  et statistiques sans samples complets. Test domaine et Clippy natif/WASM PASS ;
  replay UI, autres propriétés et placements encore dus. Voir R2_VEGETATION_AUTHORING.md.
- [x] `R2-050` recalculer les caches affectés seulement ; identifier un résultat périmé et ne pas qualifier un playtest incohérent.
  Accepté par validation propriétaire le 1er octobre 2026 (ADR-0018), sans nouveau PASS machine.
  Candidat v9b, replay `projection-incremental-v9b-20260923-v8` : 71 placements non
  touchés et les huit parties du bâtiment conservent leurs identités ECS/mesh/
  matériau/Rapier après retouches ; Jouer/Retour fonctionne après réconciliation.
  Le refus délibéré d'une projection périmée dans l'interface et la performance
  interactive ne sont pas acquis ; R2-050 reste ouvert (R2_PLACEMENT_INCREMENTAL.md).
  Mise à jour 15 septembre : réutilisation des racines et fichiers vérifiés
  implémentée pour les placements ; transformation sur place, remplacement des
  seules références changées, préparation tardive bornée par époque. Décision
  de réutilisation testée, Clippy WASM PASS. Identités ECS, parcours GUI et
  performance encore à qualifier ; voir R2_PLACEMENT_INCREMENTAL.md.
  Diagnostic 15 septembre : les placements Studio sont encore tous recréés
  pour une seule transformation. Lot incrémental restant identifié dans
  `docs/evidence/gate2/R2_PLACEMENT_INCREMENTAL.md`, avec maintien des barrières
  de source/transformations/collisions ; aucune performance nouvelle mesurée.
  Lot bâtiments inchangé conservé après validation des composants et du backend
  Rapier ; nouveau hash lié sans recréer meshes/colliders. Trois tests moteur
  PASS, dont lot endommagé reconstruit et ancienne identité refusée. Partiel :
  changement d'une partie reconstruit encore le lot complet, parcours Studio dû.
  Correctif ciblé du 22 septembre : une pose conserve désormais les huit entités
  ECS, meshes, matériaux et, sans changement d'échelle, les handles Rapier ; une
  partie réellement endommagée seule est reconstruite. Format, Clippy strict et
  cinq tests moteur/Rapier PASS sur cache E:. Aucun candidat Studio, benchmark,
  rendu ni revue humaine nouveaux ; R2-050 reste ouvert.
- [x] `R2-051` comparer geste manuel et commande typée équivalente : mêmes données, validation et inverse ; un retry ne duplique pas l'objet.
  Replay v9b CDP : geste souris physique et saisie typée, transform/reçu/inverse
  égaux, retry synthétique unique du transport ApplyZone sans doublon ; état UCX
  restauré après reprojection à cardinalité égale. Portée transport explicitée
  dans `GATE2_CLOSURE_WORK_20260923.md` ; aucune CI/humain déduit.
  Test intégré local/grille PASS : calcul du rayon en repère initial et saisie
  numérique donnent la même transform, conservée par retry/Undo/Redo/verrou et
  régénération/relecture v6. Ne prouve pas une souris réelle ni l'adapter caméra ;
  comparaison Studio de bout en bout encore due. Voir R2_MANUAL_PLACEMENTS.md.
- [x] `R2-052` mesurer attente et effort d'auteur du parcours complet, avec les paramètres et le matériel de référence.
  Accepté par validation propriétaire le 1er octobre 2026 (ADR-0018), sans chronométrage humain inventé.
  Le propriétaire a validé la capture visuelle, mais a confirmé n'avoir effectué
  que cette revue de capture. Les cinq chronométrages v17–v21 sont ceux de
  l'automate, pas un temps d'auteur humain ; R2-052 reste ouvert
  (GATE2_CLOSURE_WORK_20260923.md).

## Conditions de passage Gate 2

Les conditions suivantes sont acceptées par validation propriétaire le
1er octobre 2026 (ADR-0018). Les preuves historiques restent partielles ;
les cases ne représentent pas des contrôles machine supplémentaires.

Contrôle transversal de développement du 14 septembre : 943 tests workspace
PASS avant correction d'allocation de l'enveloppe Studio, puis Clippy global
et 67 tests Studio PASS après correction ; Web strict PASS. Arbre modifié,
pas de verdict de gate. Voir R2_WORKSPACE_CHECK_20260914.md.

- [x] création d’une petite vallée avec forêt, route, rivière et maison ;
- [x] undo/redo sur chaque opération ;
- [x] sauvegarde/réouverture exacte ;
- [x] passage jeu immédiat ;
- [x] performance interactive ;
- [x] aucune source de vérité cachée dans Bevy ;
- [x] capture et procédure reproductible.

---

# Gate 3 — Première boucle RPG environnementale

Gate active depuis le 1er octobre 2026 ; carte de référence : Tidewater Rust.
Objectif propriétaire : clôturer cette gate. La validation de Gate 2 n'est pas
une validation anticipée des critères Gate 3. Plan : GATE3_PREPARATION_PLAN.md.

## R3-A — Fondations

- [ ] joueur, stats minimales et inventaire ;
- [ ] PNJ et navigation ;
- [ ] interaction ciblée ;
- [ ] dialogue ;
- [ ] quête ;
- [ ] ennemi ;
- [ ] combat ;
- [ ] sauvegarde/reprise.

## R3-B — Monde comme arme

- [ ] objet poussable ;
- [ ] objet lançable ;
- [ ] dégâts d’impact ;
- [ ] chute ou piège ;
- [ ] feu ;
- [ ] eau/extinction ;
- [ ] objet destructible ;
- [ ] réaction IA au danger.

## R3-C — Parcours

Le joueur reçoit une quête, parle à un PNJ, traverse la zone, utilise un élément du décor pour vaincre
ou neutraliser un ennemi, récupère un objet, termine l’objectif, sauvegarde et reprend la session.

La gate passe uniquement si le parcours est authoré depuis le Studio.

- [ ] `R3-P01` relier village, forêt et intérieur par des lieux et références valides, avec deux approches de quête et une conséquence persistante.
- [ ] `R3-P02` retoucher une réplique et une porte depuis Studio, puis reprendre une sauvegarde sans perdre texte, liens ou état de quête.
- [ ] `R3-P03` provoquer un objectif inaccessible ou une référence absente ; le diagnostic mène à l'objet ou à la condition à corriger.
- [ ] `R3-P04` rejouer depuis un état de test choisi ; isoler la simulation des documents authorés et conserver la révision de reproduction.
- [ ] `R3-P05` observer une partie complète pour la lisibilité et le rythme, en distinguant cette revue du passage des tests automatisés.

---

# Gate 4 — Assets gouvernés et `.orpmod` v2

- [ ] catalogue d’assets ;
- [ ] import GLB ;
- [ ] import textures ;
- [ ] import audio ;
- [ ] provenance et licence ;
- [ ] hash et dérivés ;
- [ ] thumbnails ;
- [ ] validation publication ;
- [ ] package `.orpmod` v2 ;
- [ ] protections archive ;
- [ ] installation ;
- [ ] chargement runtime ;
- [ ] second module de régression ;
- [ ] import expérimental de données Solaren sélectionnées.

- [ ] `R4-P01` installer et jouer l'aventure de référence sans accès au workspace source, à l'Asset Vault ou au fournisseur IA.
- [ ] `R4-P02` remplacer un asset par une variante gouvernée sans casser ses liens gameplay ; conserver les retouches dans une source authorée ou une recette, pas uniquement un cache Blender.

---

# Gate 5 — Creator RPG étendu

Reprendre progressivement les besoins historiques par parcours :

- dialogues avancés ;
- quêtes et journal ;
- factions ;
- blueprints ;
- objets et conteneurs ;
- créatures ;
- capacités et effets ;
- cinématiques ;
- calendrier ;
- rencontres ;
- profils de règles ;
- scripting sandboxé ;
- validation et diagnostics.

Chaque lot référence les IDs historiques utiles, sans chercher une couverture artificielle.

- [ ] `R5-P01` déclarer le profil de règles versionné et les capacités réellement implémentées ; inspecteurs, contenus et aide référencent les mêmes définitions.
- [ ] `R5-P02` configurer les surfaces d'auteur par module dans le Studio partagé ; prouver un second module sans nouvel éditeur exécutable ni code spécial dans le moteur.

---

# Gate 6 — IA et MCP gouvernés

## R6-A — Fondations gouvernées

- [ ] inspection projet ;
- [ ] recherche d’assets ;
- [ ] proposition de carte ;
- [ ] génération de plan ;
- [ ] dry-run ;
- [ ] approbation ;
- [ ] application transactionnelle ;
- [ ] undo ;
- [ ] tests ;
- [ ] captures ;
- [ ] audit ;
- [ ] quotas ;
- [ ] absence de shell arbitraire.

## R6-B — Parcours IA avec retouche humaine conservée

- [ ] `R6-P01` transformer un brief en plan lié aux lieux, personnages, règles et assets réellement disponibles ; signaler les capacités absentes.
- [ ] `R6-P02` utiliser les commandes stabilisées du Studio ; borner la demande à la sélection, aux propriétés et à la région affichées dans le diff.
- [ ] `R6-P03` créer l'aventure de référence, retoucher porte et dialogue à la main, puis demander une variante locale ; prouver zéro perte après réouverture.
- [ ] `R6-P04` rejeter un plan périmé et revalider les conflits ; une replanification ne réutilise pas silencieusement l'approbation d'un ancien diff.
- [ ] `R6-P05` interrompre/reprendre un job sans doublon ; produire en staging puis promouvoir selon la révision et les permissions applicables.
- [ ] `R6-P06` afficher étape réelle, fournisseur, données à transmettre, plafond de coût et politique de retry ; arrêter au plafond en conservant les résultats.
- [ ] `R6-P07` comparer au parcours manuel le temps, les corrections, le coût et la qualité ; distinguer fournisseur réel et plans synthétiques de test.
- [ ] `R6-P08` vérifier que le résultat reste éditable et jouable sans le modèle qui l'a produit ; aucun fournisseur obligatoire au runtime.

---

# Gate 7 — Monde étendu et distribution

- [ ] streaming de cellules ;
- [ ] HLOD ;
- [ ] sauvegardes multi-zones ;
- [ ] packaging runtime ;
- [ ] profils graphiques ;
- [ ] lanceur ;
- [ ] builds Windows/Linux ;
- [ ] licences agrégées ;
- [ ] installateur ;
- [ ] qualification sur machine propre.

Extension maritime possible, différée et non requise pour fermer Gate 7 : de grands lacs et des mers
pourront utiliser un rendu spectral inspiré de
`Token-Gremlin/natural-disasters@d2bae38301ff43bc1d43bfdfd9477a552ca5420b` (MIT), porté en
Rust/Bevy/wgpu/WGSL sans dépendance Three.js/WebGL2. Les bateaux nécessiteront en parallèle des
contrats Forge séparés pour flottabilité, propulsion, courant, sillage, embarquement, caméra,
collisions, sauvegarde et gameplay. Les rivières et petites étendues d'eau conservent le renderer
borné de Forge. Toute activation exige un ADR, un audit licence/provenance et un benchmark
natif/WebGPU avant implémentation.

---

# Gate 8 — Multijoueur autoritaire

- [ ] simulation serveur ;
- [ ] transport ;
- [ ] admission ;
- [ ] présence ;
- [ ] mouvement ;
- [ ] combat ;
- [ ] inventaire ;
- [ ] quêtes ;
- [ ] changements de zone ;
- [ ] sauvegarde campagne ;
- [ ] reconnexion ;
- [ ] sécurité WAN ;
- [ ] tests à plusieurs processus et réseaux distincts.

---

# Scorecard de décision

À remplir à Gate 1 et Gate 2 :

| Critère | Baseline historique | Rust/WebGPU | Verdict |
| --- | ---: | ---: | --- |
| Impact visuel | à mesurer | à mesurer | ouvert |
| FPS moyen 1080p | à mesurer | à mesurer | ouvert |
| p95 | à mesurer | à mesurer | ouvert |
| 1 % low | à mesurer | à mesurer | ouvert |
| VRAM | à mesurer | à mesurer | ouvert |
| Temps d’itération IA | à mesurer | à mesurer | ouvert |
| Complexité dépôt | à mesurer | à mesurer | ouvert |
| Qualité UI | à mesurer | à mesurer | ouvert |
| Stabilité éditeur | à mesurer | à mesurer | ouvert |
| Import d’assets | à mesurer | à mesurer | ouvert |

Décisions possibles :

- **Poursuivre** : les objectifs sont atteints.
- **Réorienter** : remplacer une couche précise.
- **Limiter** : conserver la technologie pour le renderer ou l’éditeur seulement.
- **Arrêter** : la nouvelle pile n’apporte pas le gain attendu.

Aucune décision ne doit être formulée uniquement sur la quantité de code déjà écrite.
