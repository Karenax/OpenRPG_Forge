# Gate 1 — avancée de clôture, 10 septembre 2026

**PARTIAL ; Gate 1 non close.** Aucun commit/push, fork moteur ou dérogation nouvelle.
HEAD de travail inchangé `f786a38aca2023f08d708687f99a47d14f34e6d4`, arbre modifié conservé.

## 1. Instrumentation native du candidat complet

Défaut corrigé dans `scripts/renderdoc/capture_candidate_probe.py` : l'adapter de
capture du parcours ignorait les entrées eau/nuages du manifeste. Il les épingle
et les transmet désormais ; incohérence flag/entrée ou mauvais hash refusés.
Les commandes embarquées dans le JSON restent ignorées, commande reconstruite
par liste autorisée. **38 tests synthétiques PASS** ; aucun changement moteur.

Acquisition réelle puis replay dans deux processus distincts, RenderDoc 1.46,
Vulkan Supported/non dégradé, RX 7800 XT, candidat High clair combiné :

| Mesure | Résultat |
| --- | ---: |
| Frame capturée | 286 |
| Actions visitées / Present | 2124 / 1 |
| Draws | 1002 |
| Instances soumises, répétitions de passes comprises | 6424 |
| Triangles soumis, répétitions de passes comprises | 9325611 |
| Draw/dispatch leaves ayant un timing GPU de replay | 1139 |
| Lignes de compteurs GPU feuilles, copies comprises | 1397 |

Racine `E:/OpenRPG_Forge/.gate1-evidence-runtime/` :
`r1-final-integrated-gpu-20260910` et `r1-final-integrated-gpu-replay-20260910`.
RDC SHA-256 `0484634ec6122cd72665fc360d6ef3401d21e076e2409d4aa006d2a9267131c7`.
Analyse SHA-256 `698a36c47f17f55d767c8044baedeaa55b171e8e70afe80cb76b1a9c8cf13a31`.
Runtime SHA-256 `50991d48b8b00716847f805aa821f72fdd5ded9e9bb52b5d2dfba3f4d69e5e8f`.
Huit pins contrôlés avant/après capture (manifeste et sept entrées).

Le déclencheur attend le PNG de parcours 162 ; la frame GPU capturée est ultérieure,
**pas la même image ni un reçu atomique de préparation**. PNG 162 inspecté :
joueur/pont/forêt/eau/ciel présents. Pas de comparaison des chiffres à la vieille
capture du 8 septembre : caméra, géométrie et candidat différents.

Les durées par événement sont `PASS_REPLAY_ONLY`. Leur somme n'est ni un temps
de frame live ni un FPS. Les compteurs concernent cette frame native, pas Studio.
L'overlay garde ses valeurs indisponibles ; aucun compteur ponctuel n'y est injecté.
R1-037 reste ouvert sur sa couverture complète et les conditions de qualification.

Nettoyage : QRenderDoc acquisition PID 30184, replay PID 18732 terminés ; runtime
déjà sorti au nettoyage ciblé. Reçus hôte PASS, profils temporaires archivés.
Aucun navigateur lancé ni processus utilisateur arrêté.

## 2. Kit : géométrie réouverte, copies de travail prêtes

`scripts/blender/Prepare-Gate1HumanKit.py` exécuté avec Blender **5.2.1 LTS**,
factory startup, autoexec désactivé et timeout hôte 120 s. Six GLB courants réouverts,
transforms monde composés, bounds/origines/triangles/matériaux enregistrés, trois LOD
et UCX présents pour chacun. Six `.blend` séparés sauvegardés et réouverts :
**géométrie et affectations de matériaux identiques** avant/après ce round-trip.
Hashes sources conservés, hashes des six `.blend` revérifiés après exécution.
Blender PID 32504 terminé. Pas de rendu Blender ni qualification PBR visuelle déduite.

Copies et `inspection.json` :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-kit-human-ready-20260910`.
Inspection SHA-256 `56b6be98b503e5e82f8017819e13f46b3cff0b86f94b4f76c43dcb012d0cf98d`.
Coordonnées des mesures : monde Blender, mètres, **Z vertical**, pas coordonnées
Forge Y vertical. Les collisions sont recensées, pas jugées correctes par leur seul nombre.
LOD1/2 et UCX sont masqués initialement pour lecture, toujours présents dans l'Outliner.

### Parcours humain restant, prêt à effectuer

1. Ouvrir `vv-rock-moss-a.blend` dans ce dossier, examiner base/origine et matériaux.
2. Effectuer une retouche intentionnelle identifiable sur LOD0, puis enregistrer
   sous un **nouveau nom** `vv-rock-moss-a-human.blend` dans le même dossier.
   Noter en une phrase l'intention ; ne pas modifier les GLB du kit actif.
3. Reprendre ensuite l'export/réimport gouverné : contrôler l'intention conservée,
   dériver les LOD/collision affectés, valider les hashes et le rendu dans Forge.
   Cette dernière étape reste à exécuter sur le véritable fichier retouché.
4. Examiner aussi les autres pivots/collisions/LOD et statuer sur provenance/droits.

Une action de l'agent ne peut pas être nommée « retouche humaine ». Les copies sont
des dérivés d'inspection, pas les fichiers source authorés historiques ni des assets
publiables. Statuts `human_retouch_performed=false`, `publication_approved=false`.

## 3. Comparaison Godot historique retrouvée

Source consultée sur place, sans import :
`E:/OpenRPG_Forge/OpenRPG_Forge/docs/assets/screenshots/forge-forest-map-demo.png`.
Commit ayant introduit le fichier : `3f9c8425dadd1dc02331cc087b4b899c6180041b`.
Blob Git courant et à ce commit identiques : `b8354234848f5d82bed75a60cbd95e4326924664`.
SHA-256 `5cdfc6a717d40fcf2be89447dc082c0e071184cb53f298a33b725c58041034bc`.
Provenance lue : `docs/assets/GODOT_FOREST_DEMO_INTEGRATION.md`, daté du 2 août 2026,
prototype Godot plat 128×128 m ; ce document ne décrit pas toute la qualité du
dernier logiciel historique. Capture 1280×720 effectivement inspectée.

Observation comparative : cette référence montre un sol uniforme plat, un ciel
uniforme et une forêt dispersée. Le candidat Forge courant présente relief,
matériaux de sol, eau/berges, forêt dense et ciel nuageux. **Enrichissement visuel
constaté face à cette référence précise**, pas supériorité générale sur Godot.
Scènes, résolution, cadrage et date différents ; pas de benchmark ni comparaison
appariée. La comparaison finale au niveau exigé et le jugement humain restent dus.
Le précédent statut Godot NOT RUN est donc affiné : consultation comparative
historique réalisée, qualification comparative complète non acquise.

## 4. Ciel préparé, pas modifié

Voir `docs/design/SKY_IMPROVEMENTS_PREPARATION.md` : formes, éclairage et horizon,
sur le renderer existant et avec budgets conservés. Références primaires consultées,
protocole A/B borné ; aucun asset externe, nouveau shader ou réglage courant appliqué.

## Ce qui empêche encore le passage

**Mise à jour de décision après ce lot :** le propriétaire a explicitement choisi
la dérogation limitée aux mesures GPU du Studio, formalisée par
[ADR-0012](../../ADR/ADR-0012-gate1-studio-gpu-measurements-waiver.md).
Les sous-parties WebGPU R1-034/037/038 sont `WAIVED / ACCEPTED`, pas PASS technique.
La couverture GPU Studio ne constitue donc plus un blocage autonome ; les mentions
historiques de décision encore nécessaire ci-dessous sont remplacées sur cette
seule portée. Préchauffage, temporalité, preuves natives, kit et verdict final
restent hors dérogation. Aucun renderer adapté, aucune nouvelle acquisition.

### Complément de vérification du même jour

- Studio épinglé `eb75ecef416944c4ec8ead6d88d450a71603280e9ea06679f05223f1eabceffd`
  ouvert seul ; activation réelle de l'overlay observée par Computer Use.
  Passe opaque CPU 0,100 puis 0,200 ms ; GPU, statistiques GPU et mémoire backend
  indisponibles. Ce relevé visuel n'est pas un benchmark. Export PNG non obtenu :
  commande initialement hors écran, puis interruption des interactions et échec
  de lecture de fenêtre. Ne pas annoncer de nouvelle capture PNG conservée.
  PID possédé 1140 et enfants directs absents au contrôle final ; aucun processus
  utilisateur arrêté et aucune nouvelle instance lancée pour répéter ce résultat.
- Cause de l'absence du temps GPU confirmée dans les sources locales Bevy 0.19.1 :
  `bevy_render/src/diagnostic/internal.rs`, `write_timestamp`, exige
  `TIMESTAMP_QUERY_INSIDE_ENCODERS` et retourne sans mesure si absent ; commentaire
  upstream explicite « unsupported on WebGPU ». Activer seulement `TIMESTAMP_QUERY`
  ne corrige donc pas ce chemin. Pas de patch du registre Cargo ni de fork moteur.
- `Verify-Gate1FinalEvidence.py` vérifie maintenant aussi la capture RDC native,
  l'analyse de replay épinglée, les huit entrées intégrées, les six sources GLB et
  six copies `.blend`, et les reçus de fin de processus GPU. Aucune commande issue
  des documents n'est exécutée. Le résultat reste `partial_not_gate_pass`.
- Exécution réelle : **89 fichiers hashés**, 12 captures sélectionnées, 2 vidéos
  revérifiées. Index local :
  `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-final-dossier-supplement-20260910/index.json`,
  SHA-256 `ad3e8efdc2401620826334251d5e91bb5ca454f484ce80d4f76ef0c406518a0e`.
  `Test-Gate1FinalEvidence.py` : **3 tests PASS**, dont sept corruptions de contrat
  rejetées et un hash modifié rejeté. Ces tests ne prouvent pas une retouche humaine.

### Limites restant bloquantes

- Mouvement : tramage des troncs et absence de hitch non qualifiés. Le code utilise
  encore volontairement le dither pour le bois opaque écrivant la profondeur ;
  supprimer ce rejet ou passer tout le bois en transparence sans preuve n'est pas
  un correctif valide. Aucune amélioration temporelle revendiquée dans ce lot.
- Mesures : couverture native ponctuelle avancée, pas couverture exhaustive Studio
  ni préchauffage anticipé de toutes les variantes. R1-034 est un overlay CPU/GPU
  et R1-038 une estimation mémoire : ne pas ajouter l'exhaustivité des temps CPU,
  la VRAM résidente ou un reçu atomique comme conditions nouvelles ; conserver
  simplement les limites de portée des chiffres et les qualifications manquantes.
- Kit : retouche/revue humaines et qualification finale de provenance non réalisées.
- Dossier : références historiques disponibles, mais comparaison finale approuvée et
  acquisition depuis une révision source propre exacte encore manquantes.

La demande de finir autorise les travaux techniques, pas à inventer une revue
humaine, forker Bevy ou étendre l'exception draw calls. Pour la voie GPU non couverte,
une décision spécifique reste nécessaire si l'on retient un fork/instrumentation
bas niveau ou une dérogation ; aucune de ces voies n'est implicitement adoptée.
La performance déjà dérogée et le ciel accepté ne sont pas rouverts.
