# Gate 1 — revue de clôture à décider

## État courant — Gate 1 clôturée le 12 septembre 2026

**PASS automatisé local sous ADR-0006, avec les dérogations acceptées.**
Dérogations saccades pendant les changements de qualité et kit : ADR-0013 ;
comparaison Godot/Cadle validée explicitement. 738 tests PASS, 2 ignorés
préexistants ; builds et replays natif/Studio PASS. Révision propre qualifiée
`7386e00a1d9e8531470b57bcd8da190636a9e390`. [Rapport de clôture](GATE1_CLOSURE_20260912.md).
CI distante NOT RUN ; aucune publication ni qualification de distribution.
Le travail Gate 2 est suivi séparément et ne fait pas partie de ce snapshot.
Les états ouverts et attentes antérieurs ci-dessous sont historiques ;
ils ne doivent pas rouvrir les décisions soldées par ce rapport.


### Décisions de clôture — 12 septembre 2026

ADR-0013 consigne l'instruction explicite du propriétaire : saccades pendant les
changements de qualité dérogées ; point kit humain reporté sous dérogation ;
comparaison Godot/Cadle validée. Ces points ne sont plus en attente d'accord.
La qualification finale sur révision propre est en cours ; aucun PASS de test,
droit de distribution ou travail de Gate 2 déduit de ces décisions.
Voir `docs/ADR/ADR-0013-gate1-closure-owner-decisions.md` (depuis la racine).
Les paragraphes antérieurs ci-dessous restent l'historique des acquisitions.

12 septembre 2026. **Préparation vérifiée, gate NON VALIDÉE.**
La demande « fait tout ça » autorise la préparation et les contrôles techniques,
pas une acceptation humaine fictive. Le propriétaire a confirmé aujourd'hui
qu'il n'a pas effectué de retouche manuelle du kit.

## Candidat et preuves disponibles

- Moteur natif : `r1-temporal-lod-20260911/attempt-5`.
- Studio : `r1-temporal-lod-20260911/attempt-7`, même moteur ; correction du seul
  ordre d'installation de la source mémoire et des plugins du kit.
- Contrôle de liaison du 12 septembre : **340 fichiers vérifiés**, dont les 288
  sources épinglées, exécutables/frontend, données natives, six GLB déployés,
  six copies Blender et références. Aucun changement des sources moteur depuis
  ces preuves. Ce contrôle d'intégrité ne réexécute pas les tests/runtime.
- Index : [index.json](E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-closure-review-20260912/index.json).
  Script : `E:/OpenRPG_Forge/.gate1-evidence-runtime/Prepare-ClosureReview-20260912.ps1`.
- Aucun build, nouveau benchmark, runtime ou navigateur lancé pour cette revue.

## 1. Temporalité : défaut corrigé, réserve distincte

Le tramage persistant est corrigé et contrôlé : voir [preuve temporelle](R1_TEMPORAL_LOD.md).
La vidéo native [parcours avec arrêt](E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-temporal-lod-20260911/attempt-5/native/bridge.mp4)
est disponible, mais son horloge imposée à 30 images/s ne démontre pas la fluidité
temps réel. Les pics historiques du changement High/Low/High ne sont pas annulés
par la correction LOD. **Absence de stutter récurrent : non qualifiée.**

Décision requise : maintenir cette réserve pour correction/mesure ciblée ou
l'accepter explicitement dans une dérogation bornée. Les dérogations de performance
et de mesures GPU Studio existantes ne sont pas étendues automatiquement.

## 2. Kit : six fichiers immédiatement accessibles

| Fichier de revue | Points à examiner |
|---|---|
| [Rocher A](E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-kit-human-ready-20260910/vv-rock-moss-a.blend) | Base, pivot, mousse, collision ; candidat simple pour la retouche |
| [Rocher B](E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-kit-human-ready-20260910/vv-rock-moss-b.blend) | Base et silhouette aux trois LOD |
| [Chêne](E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-kit-human-ready-20260910/vv-tree-oak-a.blend) | Pied, tronc, feuillage, silhouette LOD2 plus haute |
| [Pin](E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-kit-human-ready-20260910/vv-tree-pine-a.blend) | Pied, silhouette LOD2 plus haute, collision du tronc |
| [Mur](E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-kit-human-ready-20260910/vv-architecture-wall-a.blend) | Base, raccords, matériau, collision |
| [Arche](E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-kit-human-ready-20260910/vv-architecture-arch-a.blend) | Passage libre, appuis, collisions et silhouette |

Les LOD1/2 et UCX sont masqués initialement dans Blender, pas supprimés.
Les mesures et avertissements sont dans [point 6](R1_KIT_POINT6.md).
Six fichiers inchangés depuis la préparation : aucun réimport humain à prétendre réussi.

**Action humaine minimale :** ouvrir le rocher A, effectuer une modification
géométrique intentionnelle visible, conserver les noms des objets, puis enregistrer
sous `vv-rock-moss-a-human.blend` dans ce même dossier. Fournir une phrase décrivant
l'intention. Ne pas écraser les GLB actifs.

**Après réception, travail agent restant :** épingler la source retouchée, exporter
dans un nouveau candidat, contrôler les changements de LOD/collision nécessaires,
réimporter et vérifier la conservation de l'intention dans Forge. Une sauvegarde
ou une retouche automatique ne remplace pas cette participation humaine.

Provenance : le kit déclare `internal_gate_proof_only`, une licence projet et une
revue de publication requise. L'intégrité du kit déployé et du kit audité concorde ;
ceci ne prouve pas des droits nouveaux. Aucune promotion en bibliothèque distribuable.
La revue artistique/provenance humaine reste à renseigner, sans avis juridique inventé.

## 3. Comparatif : avis agent et décision humaine séparés

Captures consultées sur place le 12 septembre, sans copie de média Cadle :

- [Forge Studio actuel](E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-temporal-lod-20260911/attempt-7/studio-inspected.png).
- [Prototype Godot historique](E:/OpenRPG_Forge/OpenRPG_Forge/docs/assets/screenshots/forge-forest-map-demo.png).
- [Cadle forêt, FOV vertical 60°](E:/OpenRPG_Forge/OpenRPG_Forge-Rust/artifacts/cadle-gate-c/qualification-script-c/tools/out/r1-forest-vfov60-20260911/shot-forest-vfov60-after-measurement.png).

| Axe | Avis de l'agent, non validation humaine |
|---|---|
| Face au prototype Godot présenté | Progrès visuel net : scène habitée à hauteur du personnage, matériaux, forêt dense, ombres et ciel nuageux contre sol/ciel uniformes. Ne compare pas toutes les capacités de Godot. |
| Face à Cadle | Cadle présente un couvert bas plus continu, des troncs plus détaillés et une lumière volumétrique plus structurée. L'équivalence visuelle exigée n'est pas démontrée. |
| Limites | Godot 1280×720 aérien ; Cadle FHD intérieur ; Studio viewport 1408×966 en sortie de pont. Différences de cadrage/scène/budgets : aucun ratio de performance ou A/B apparié déduit. La comparaison intérieure antérieure est documentée dans R1_052_FOREST_OPTICAL_COMPARISON.md. |

**Décision humaine attendue :** jugement sur la supériorité face à la référence
Godot et sur l'impact comparable à Cadle. Si le niveau Cadle n'est pas atteint,
choisir explicitement entre poursuite artistique ciblée et dérogation à ce critère.
L'acceptation antérieure du ciel et de la vue intégrée reste acquise, sans être
requalifiée en acceptation comparative. Aucun nouveau réglage de carte imposé ici.

## 4. Révision finale : état réel, pas nettoyage implicite

HEAD local `f786a38aca2023f08d708687f99a47d14f34e6d4`, arbre HEAD
`e055dc0b1567b1a4f29286e3f9c1e917ab507612` ; **ce ne sont pas les octets modifiés
qualifiés par les manifests**. 404 entrées modifiées/non suivies avant ce document.
Référence distante locale en avance de 103 commits (`0 / 103`), sans fetch dans
ce lot : ce nombre décrit le cache local, pas l'état distant garanti actuel.

Ne pas résoudre cette situation par reset, stash, merge, commit global ou push.
Après les décisions précédentes : préparer le périmètre exact du commit local et
la révision de preuve propre sous l'autorité Git appropriée ; exécuter alors les
contrôles ADR-0006 affectés. Un index de hashes ne remplace pas cette révision.

## Verdict de cette reprise

**Dossier de revue et intégrité : préparés/vérifiés. Gate 1 : ouverte.**
Bloquants externes : retouche et revues humaines absentes, décision comparative
et réserve de stutter non soldées, révision propre non constituée. Aucun processus
de test n'attend en arrière-plan ; aucune validation, publication ou Gate 2 lancée.
