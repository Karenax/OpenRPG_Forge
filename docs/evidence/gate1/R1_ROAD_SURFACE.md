# R1 — Matériau des routes dérivé des splines

7 septembre 2026, Rust local, Gate 1 ouverte.

## Défaut confirmé et correction

`compile_world_sketch` produit `road_surface_mask_u8`, mais le matériau terrain
était exclusivement déterminé par altitude/pente. Les corridors excluaient donc
l'herbe sans rendre leur sol distinct de la prairie verte.

`RoadSurfaceProjection` lie le masque au terrain et au rapport de compilation par
hashes. Elle mélange les poids visuels grass/earth/rock vers earth suivant le masque
existant. Les sommets communs aux chunks lisent le même échantillon global.
Pas de géométrie supplémentaire, texture ou lecture shader ajoutée. Heights, normales,
indices et colliders restent identiques. Aucun document source ou format persistant
modifié ; filtres de scatter canoniques inchangés, donc pas de catalogue invalidé.

L'adapter `VisualValleyPlugin::with_road_surface` refuse un terrain différent et
transmet le cache ainsi coloré au runtime terrain, dont dérivent les LOD. Natif et
Studio WorldSketch transmettent la projection du même résultat de compilation,
sans compiler une deuxième fois. La fixture terrain autonome ne reçoit aucun masque.
Le hash supplémentaire de carving est journalisé ; le hash terrain seul ne décrit
pas cette présentation. L'export diagnostique dédié de cette identité reste à raccorder.

## Preuves et limites

112 tests monde PASS, dont trois nouveaux : poids normalisés et terre renforcée,
géométrie/colliders/sources inchangés, déterminisme, masque nul identitaire, refus
de masque périmé/malformé et de terrain différent. 2 067 sommets de la fixture
compilée changent de poids visuels. Clippy strict workspace/all-targets/all-features
PASS après correction des patterns et stockage indirect dans l'enum runtime.

Suite workspace complète : **584 PASS / 0 FAIL / 1 timing ignoré**. Capture native
terminée, ouverte et inspectée. Répertoire local :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-road-surface-20260907/`.

`native/forge_runtime.exe` conservé séparément, SHA-256
`f6896c16c3c0f02a962741a316f3c34823c8dd283324dbf34528697ca29fa83c`.
Capture `native/clear.png`, 1920×1080, SHA-256
`45348603bbfd0642899349f897ee45b21385d71560726f4f3a164a25a74b355e`.
Entrées inchangées du précédent candidat, pins vérifiés après exécution, sortie 0.
116/116 pipelines prêts, zéro ERROR runtime, kit 20/20 et 36 colliders. Capture
émise à 408 après huit observations stables, callback 411 (300 était le minimum).

Observation : le corridor de terre rejoint maintenant le pont ; il se distingue
de la prairie verte. La palette terre reste orangée, la couverture herbeuse encore
clairsemée. Les LOD projetés sont High, 192 meshes ; cette vue ne qualifie pas un
cycle de changement de qualité. Le journal INFO du hash n'est pas visible avec le
filtre WARN de cette exécution : ne pas prétendre l'y avoir observé.

Smoke RX 7800 XT, High sans VSync, 239 échantillons : 66,23 FPS moyens,
p95 17,9094 ms, 1 % low 40,42 FPS. Capture incluse, pas de benchmark comparatif ni
de performance cible acquise. Les avertissements de profils partiels et invariant
préexistants restent présents. Le compteur instantané de l'overlay n'est pas cette
fenêtre de mesure.

Ce lot ne prouve ni édition de route dans le Studio, ni sauvegarde/régénération
avec retouches, ni mise à jour incrémentale du masque pendant une édition terrain.
Clippy strict Studio WASM PASS sur le raccordement final ; avertissement futur
préexistant `proc-macro-error2 2.0.1`. Pas de nouveau bundle Trunk ni d'exécution
Chrome/Tauri de cette correction sur ce lot : compilation Web seulement.
Ces parcours ne sont pas implémentés ici. Pas de performance cible, d'acceptation
artistique, de passage de gate, de CI distante ou de publication Git déduits.

## Complément Web exécuté

Trunk PASS après correction de l'environnement `NO_COLOR=1` en `NO_COLOR=true`
(premier appel refusé avant compilation). Bundle local `web/`, WASM SHA-256
`b84b35bb5153c8e0c69f21839f7d58d00524fb2f924dd003698d45b7e8733eed`.
Kit et vues copiés du candidat précédent sans modification ; serveur loopback 1451.

Chrome, vue `human_bridge`, 1408×797, un pas virtuel de 250 ms : kit 20/20,
36 colliders prêts, 650 arbres ; console error vide. Capture
`forge-viewport-1788791485439.png` ouverte et inspectée : corridor terre visible
jusqu'au pont. Diagnostic `forge-diagnostic-1788791486558.json` archivé avec la
capture sous le répertoire de preuve ; copies SHA-256 égales aux téléchargements.
`gate_passed=false`, `human_acceptance=false`. Pas de parcours interactif, pluie,
cycle LOD ou Tauri qualifié ; dimensions différentes du natif, pas de parité pixel.

Retour propriétaire pendant ce lot : ciel jugé peu réaliste. Le ciel reste donc
un écart artistique explicite à traiter, indépendamment du raccordement des routes.

## Raccord des routes sur les berges — 7 septembre 2026

La vidéo `r1-batched-bridge-video-20260907` montre une bande verte traversant
la route à l'entrée du pont. Les berges utilisaient les poids du terrain canonique
sans le masque visuel de route appliqué au terrain rendu.

Correction générique : `RoadSurfaceProjection::coverage_at` interpole le masque
sur les triangles de la grille supportée et refuse les coordonnées invalides.
L'adapter eau accepte cette projection validée contre son terrain ; natif et
Studio WorldSketch transmettent la même source aux deux adapters. Seuls les
poids de matériaux des berges changent, sans modification des documents, positions,
normales, indices ou colliders. Le journal `bank_road_surface_projected` expose
l'identité de cette couche. Ce n'est pas une retouche enregistrée de la carte.

Tests : un nouveau test monde couvre les deux triangles, frontières et coordonnées
invalides ; le scénario eau existant est étendu pour les invariants géométriques,
poids normalisés, masque nul identitaire et couverture complète terre. Le premier
rejeu Clippy a refusé trois comparaisons flottantes de test ; elles sont remplacées
par des comparaisons de bits, sans désactiver la règle. Nouveau Clippy strict des
trois packages monde/moteur/runtime et check Studio frontend WASM PASS. Suite
workspace complète : **612 PASS, 0 FAIL, 1 timing ignoré**. L'avertissement futur
préexistant `proc-macro-error2 2.0.1` reste signalé par le check WASM.

Build natif puis collecte terminés avec sortie 0, le 7 septembre à 20:52 UTC.
Répertoire : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-bank-road-video-20260907/`.
Le collecteur non modifié produit 271 images 1920×1080 et un MP4 de 9,033 s,
30 images/s imposées hors temps réel. Les images 0000 et 0270 sont ouvertes et
comparées aux mêmes images de `r1-batched-bridge-video-20260907` : la bande verte
sur la route disparaît aux deux extrémités. Une fine couture sombre persiste à
l'entrée du pont ; le raccord artistique complet n'est pas qualifié. L'eau striée,
la forêt candidate et le ciel provisoire restent des écarts distincts.

Le journal confirme `bank_road_surface_projected`, 121 107 sommets traités et masque
`64d206d4b5b0170157e0e74aab9ee68cc7ca60d595abe850e567dd5516925c16`.
Traversée 42,5275 m, 518/518 ticks au sol, 434 contacts tablier, bindings terrain,
joueur et pont conservés. Aucune erreur runtime ; entrées épinglées inchangées
pendant la collecte. Les 271 images ne sont pas toutes inspectées visuellement.

SHA-256 :

- runtime : `222c72fd6093d2a4e974d0eccdaf8de5f4dd08f5f53993e9f022813d4fd67b19` ;
- vidéo : `ea2804ff9b93d7516e46beb33cdd86bb30c65c669174a5c739c7f51dbea744dc` ;
- journal : `7d325742ac31e3fedbf14c7471e0aa994025fb8fd51c7531c1ffbd5e1564a02a` ;
- manifeste : `4ea170602cd946879c079510d909aec41ac362b827da7dbf77ed2d47b6973225`.

L'interpolation
du masque sur des poids déjà interpolés ne garantit pas l'égalité pixel par pixel
de tous les raccords partiels. Aucun résultat Web/Tauri, performance, acceptation
artistique ou passage de gate n'est déduit de ces tests.

### Isolation de l'ombre des berges — essai rejeté

Un essai borné ajoute `NotShadowCaster` uniquement à la couche de berge surélevée
de 0,015 m, sans retirer la réception des ombres. Clippy/build natif PASS ; même
parcours, sources, profil High, météo claire et kit que la vidéo précédente.
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-bank-shadow-video-20260907/` contient
271 images Full HD, vidéo contrôlée et traversée 518/518 ticks au sol.

L'image 0000 ouverte montre toujours la couture sombre devant le pont. Cet essai
ne démontre donc pas que la supprimer en tant que caster corrige le défaut. La
modification expérimentale et son assertion sont retirées ; `water.rs` retrouve
exactement le SHA-256 `287a0c8b5fd06e72af9afb7817c24a85cdc3df4442d7b801c33e8e391d64bf84`
du correctif routes validé précédemment. Aucun changement des assets ou de la carte.
La suite workspace de l'essai a été interrompue volontairement après le verdict
visuel, avant résultat global ; le check WASM suivant n'a pas été exécuté. Les
612 PASS précédents concernent la source restaurée, pas une validation de l'essai.

Pins de l'essai rejeté :

- runtime : `c970bf9c59576ba00f78bfe617b893e11a3d82e693b7af2ce36e3ecd73cadaaf` ;
- vidéo : `c251ae1fe1f0b52de10cecd29c9fed29186f32cbf179174847e927427139490d` ;
- journal : `8a2ebba8a24cfa51ddd22faa58c937b39c3d97aa80173fbab297b87820dff139` ;
- manifeste : `7a5b4836eee97edeb7643432b2c4076e84ca8a032211f4dcbec0789f36e623ee`.

La couture reste ouverte ; ne pas relancer cette même hypothèse inchangée. Aucun
critère de gate promu et aucune mesure de performance déduite de la vidéo imposée.

Après retrait, build runtime et fmt/diff PASS. Le binaire de travail reconstruit
porte le hash `fd58d754e427d6ec935a2114b294834e12d88c87c55a0f6e6ed319faa0a3d82e` ;
il n'a pas été relancé sur ce lot. Le hash diffère du binaire vidéo précédent :
ne pas lui attribuer une capture propre ou une reproductibilité binaire démontrée.

### Nouveau Studio Web exécuté — 7 septembre, 21:04–21:06 UTC

Bundle construit après restauration : Clippy Studio WASM et Trunk dev offline
PASS, sources épinglées inchangées pendant le build et après inspection. Assemblage
kit rev.4 PASS, GLB copiés/hash vérifiés sans transformation ni nouvelle acquisition.
Répertoire `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-bank-road-studio-20260907/`,
serveur loopback 1460. WASM :
`10726ed3e97520e788fd4f1fbe3ae00f1d584d210d320b68d97c4827d5e9a2e5`.

Chrome réel : scène WorldSketch, forêt batches, kit 20/20 et 36 colliders prêts,
650 arbres. Deux PNG 2048×1157 et deux diagnostics effectivement téléchargés,
copiés et comparés par SHA-256 aux originaux. Vue générale avec nuages puis vue
candidate `human_bridge`, sans nuages expérimentaux, temps fixe avancé à 0,250 s.
Les deux PNG sont ouverts et inspectés. La seconde montre la route en terre
jusqu'au pont sans bande verte superposée. Angle différent de la vidéo native :
pas de comparaison pixel ni de disparition de toute couture déduite.

Fichiers locaux / SHA-256 :

- `viewport.png` : `a58c6bd06e7ebd0c986f2f3170de0ff35595116a570db7f4654407a41e03175d` ;
- `diagnostic.json` : `61eed230241a00ebea98cf26368643f2692d83081d2210908df7520471bdcaec` ;
- `bridge.png` : `175bf51adc1675193c273610e42127f64ad2f7f471338960d65547744057de01` ;
- `bridge.json` : `3c1673db698d03905765c346c6fac0c37e8ca391711973795f2f798294ec5a1c`.

Les diagnostics conservent `gate_passed=false`, `human_acceptance=false`, paire
GPU/JSON non atomique et forêt non qualifiée. Le démarrage fixe avait 0 collider
avant l'avancement de l'horloge ; le diagnostic final en indique 36, sans prouver
une traversée physique. Ce lot ne rejoue pas le cycle complet de qualité, la
performance ou Tauri. Les derniers correctifs de profil sont dans le bundle,
mais pas qualifiés par un nouveau remplacement de profil interactif. Nuages
toujours flous/non acceptés. Aucun changement de code ou nouveau total de tests.
