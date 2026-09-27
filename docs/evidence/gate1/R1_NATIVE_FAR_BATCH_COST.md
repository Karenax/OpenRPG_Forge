# Coût natif du regroupement lointain — 7 septembre 2026

**Résultat local favorable mais non qualifié Gate 1.** Deux paires dans les ordres
individuel/lots puis lots/individuel, même binaire et mêmes entrées. Le mode regroupé
reste expérimental, sans simplification géométrique ni promotion automatique.

## Protocole exécuté

`--prototype-forest-batches` sélectionne maintenant le même moteur que le mode Web
`forge_forest=batches`. Catalogue/kit vérifiés obligatoires ; il implique le mode
forêt et reste exclu de l'adaptation automatique non qualifiée.
Le test CLI existant est étendu : refus sans catalogue, activation des deux ressources
logiques, mode par défaut inchangé. Test ciblé et Clippy runtime all-targets/all-features
strict PASS. Le parseur a été réduit par extraction de la sélection forêt après
un premier échec Clippy sur sa longueur. Un premier build a demandé à tort la feature
`qualification` au runtime ; le build corrigé utilise sa feature native par défaut,
qui active déjà `forge_engine/qualification`. Pas de nouvelle suite workspace revendiquée
pour ce seul raccord CLI ; le passage moteur précédent reste 573 PASS / 1 ignored.

Binaire debug optimisé SHA-256 :
`8429CAF73B5210C41CAA86E7E85A4B33C233BFC2C64C483A320ED7B31D5939ED`.
RX 7800 XT, Vulkan, pilote 26.8.1, High, clair, caméra `human_bridge`, 1920×1080,
VSync demandée désactivée. Sortie à 1500 frames, chauffe de 300 frames, **1199
échantillons par essai**, aucune capture pendant ces quatre collectes.
Compilation terminée et nos trois vues Web de preuve fermées avant mesure.
Les autres applications de la machine ne constituent pas un environnement de laboratoire.
Pas de PresentMon ni de mesure complète du temps GPU : statistiques de frames du runtime.

## Mesures, sans sélection du meilleur passage

| Ordre | Mode | FPS moyens | p95 ms | 1 % low FPS | Allocateur GPU, octets alloués |
|---|---|---:|---:|---:|---:|
| 1 | Individuel | 62,91 | 17,2796 | 51,19 | 888533332 |
| 2 | Lots | 67,04 | 16,4011 | 53,34 | 1024017204 |
| 3 | Lots | 65,91 | 17,0687 | 53,16 | 1023944524 |
| 4 | Individuel | 63,18 | 17,3468 | 52,15 | 888714076 |

Avantage local observé d'environ 4–7 % en FPS moyens, avec environ **129 Mio alloués
supplémentaires**. L'allocateur backend n'est ni la VRAM résidente totale ni la mémoire
du processus. Le second p95 regroupé dépasse 16,7 ms : pas de stabilité au seuil démontrée.
Le GPU demandé pour Gate 1 (RTX 3060/RX 6600) n'est pas qualifié par cette machine.
Ni cette paire ni la mémoire mesurée ne prouvent absence de fuite ou fluidité des changements
de profil. Les lots sont encore reconstruits lors de ces changements.

## Contrôles et rendu

Quatre sorties 0, aucun événement ERROR dans les logs. Les cinq sources du plan effectif,
la caméra, les dimensions, le backend, le nombre d'échantillons et les 36 colliders du kit
sont contrôlés par `Validate-Far-Native-Pair.ps1`, sortie PASS. Blocker végétation et
readiness false conservés. Les logs gardent notamment l'avertissement wgpu sur une sortie
vertex sans `@invariant` utilisée avec comparaison de profondeur Equal ; ne pas les
présenter comme sans avertissement. Aucun correctif à cet avertissement dans ce lot.

Diagnostic borné suivant : le même avertissement est déjà présent à la ligne 69 du
log historique `r1-integrated-candidate-20260907/human_bridge-clear/runtime.jsonl`
(06:10 UTC), avant le regroupement. Les `VertexOutput` importés de Bevy 0.19.1,
`render/forward_io.wgsl` et `prepass/prepass_io.wgsl`, déclarent effectivement la
position sans `@invariant`. Cela ne localise pas chaque warning à un shader précis,
mais contredit l'hypothèse d'une régression créée uniquement par les lots. Aucun
fork de Bevy ni changement de comparaison de profondeur n'est entrepris sur ce
seul signal. L'avertissement reste visible, sans prétendre démontrer son innocuité.

Une cinquième exécution séparée capture le mode regroupé à la frame 300 (1920×1080).
PNG ouvert et inspecté : terrain, pont, eau et arbres visibles, pas d'écran vide ni de
disparition globale de la forêt. Sol régulier et feuillage artificiel restent visibles.
Les métriques de cette exécution avec capture sont exclues du tableau.
Une image ne valide pas le vent animé, les transitions ou l'acceptation artistique.

## Artefacts locaux

Racine `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-web-optics-20260907` :

- `Run-Far-Native.ps1`, exécutions par défaut, `-Reverse`, puis `-Capture` ;
- `far-native-pair/` et `far-native-pair-reverse/` : `preview.json/log`, `batches.json/log` ;
- `far-native-pair-capture/batches.png`, métriques et log de capture séparés ;
- `far-native-clippy.log`, `far-native-tests.log`, `far-native-build.log` (échec de feature),
  `far-native-build-v2.log` (sortie 0).

Les entrées et leurs hashes sont imprimés et vérifiés avant chaque exécution par le script.
Sources, anciens artefacts et worktree conservés. Aucun push ni passage de Gate 1.
