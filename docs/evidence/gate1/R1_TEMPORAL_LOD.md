# Gate 1 — fondu LOD temporel commun

Date : 11 septembre 2026. Branche `Rust`, modifications locales conservées, sans publication.

## Périmètre autorisé

Remplacer le tramage spatial persistant du kit et de la forêt gouvernée par un fondu
de 200 ms, avec hystérésis de 2 m. L'arrêt de la caméra ne doit pas arrêter un fondu
déjà engagé. Terrain et herbe gardent leurs mécanismes distincts. Aucun asset ni
placement canonique n'est retouché pour obtenir une capture.

## Implémentation

- Contrôleur moteur commun, sans allocation, jusqu'à quatre représentations ; la
  quatrième est l'absence de dessin au-delà de la distance de culling.
- Un état par placement, partagé par toutes ses primitives. Kit : seuils 44/128/1500 m.
  Forêt : centres des anciennes bandes, issus du profil, et sa distance de culling.
- Buffer GPU partagé ; index porté par `MeshTag` pour les primitives et par un
  attribut dérivé réservé pour les lots lointains. Tags de variation conservés.
- Quantification unique : codes signés complémentaires, y compris aux demi-paliers.
  Couleur, prépass et ombres utilisent le même état ; transparence PBR conservée.
- Matériau étendu partagé par matériau source du kit : aucun clone par objet,
  aucun vent appliqué aux objets statiques, sources conservées.
- Trois LOD présents et reçu GPU récent correspondant à l'application requis pour
  activer le transport. Remplacement géométrique : invalidation du reçu précédent.
  Le reçu couvre la préparation des ressources, pas une preuve pixel par pixel.
- Un fondu déjà acquis finit pendant le renouvellement d'un reçu global, seulement
  si l'inventaire GPU reste prêt et l'époque du renderer identique. Une nouvelle
  transition attend toujours le reçu complet. Un remplacement invalide cet acquis.
- Deux plages CPU constantes évitent de faire croître la table interne des plages
  Bevy à chaque image. Les dessins sans couverture sont écartés ; les lots restent
  regroupés. Slots libérés recyclables, capacité bornée à 32768 groupes.
- Plusieurs caméras indépendantes : fallback spatial explicite. Le mode temporel
  courant concerne un viewport actif ; il ne prétend pas résoudre une sélection
  LOD temporelle différente pour plusieurs vues simultanées.

## Preuves ciblées

Les anciens binaires/images ne contiennent pas ce changement et restent historiques.
Racine des nouveaux essais :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-temporal-lod-20260911/`.

Premier contrôle ciblé : 12 tests PASS, dont projection forêt individuelle/lots
et partage des matériaux du kit. Après ajout des contrôles complémentaires, le
premier lancement workspace a échoué à compiler des assertions de test (type non
Debug et chemin du type de reçu) ; erreurs conservées dans `tests.log`, corrigées.
Sept tests Python du collecteur vidéo PASS. Workspace corrigé (`attempt-2`) :
735 tests PASS, deux ignorés explicitement. Après corrections ciblées ultérieures,
`attempt-5` : Clippy workspace/all-targets/all-features strict PASS, 20 tests temporels
PASS, builds natif et WASM/Studio PASS, 288 fichiers source épinglés inchangés.
Le résultat workspace antérieur n'est pas présenté comme un nouveau passage intégral.

Échecs conservés et corrigés : `attempt-3`, ambiguïté de parsing WGSL dans le masque
d'ombre (test Naga ajouté) ; `attempt-4`, fondu CPU suspendu pendant le renouvellement
du reçu global (test de cycle de vie étendu, critères de la sonde inchangés).

### Natif actuel — attempt-5

`native/manifest.json` : sortie normale, sonde
`bounded_cpu_temporal_transport_verified`, première stabilisation **0,200 s**,
cinq groupes suivis, caméra arrêtée 600 ms. 291 PNG FHD, vidéo 9,7 s à 30 images/s
**offline**, pas un benchmark. Images `frame-0026.png` et `frame-0043.png` inspectées
par l'agent : scène rendue, continuité du pont et de la forêt ; pas de tramage
persistant évident sur les surfaces opaques visibles. Ce constat borné ne mesure
pas la couverture de chaque pixel et ne constitue pas une revue humaine du film.

Runtime SHA-256 : `28c4ee706bf10ff674b8b276e7e785038090ad421ba9c76acf379adaad8489fc`.
Vidéo SHA-256 : `7bfdec52e5393b548ef2f5882ceb11a07aa4230401efd46c533e8799ced9d633`.
Les hashes d'entrées inchangées figurent dans le manifeste ; aucun asset retouché.

### Studio — contrôle distinct

`attempt-5` a révélé au lancement une initialisation de ShaderBuffer avant AssetServer.
Diagnostic conservé dans `studio-diagnostic.txt`, capture de la fenêtre inspectée,
aucune qualification de rendu revendiquée pour cet essai. Huit processus possédés
fermés, `studio-cleanup.json` confirme aucun restant. Correction du démarrage :
enregistrement de la source mémoire avant DefaultPlugins, installation du kit après.
`attempt-6` n'a pas compilé le frontend (variable NO_COLOR mal formatée, corrigée
dans le script d'essai).

`attempt-7` : fmt et Clippy Studio PASS, build WASM/paquet PASS ; 288 pins source
vérifiés. Seuls `apps/forge_studio/src/lib.rs` et `governed_kit.rs` diffèrent de
`attempt-5` (ordre d'installation), code moteur et assets identiques : preuve
native réutilisée sans relancer la même acquisition.

`studio-temporal.json` : phase completed, aucun échec, 15 observations, un groupe
suivi, `cpu_settled_while_stopped=true`. Premier état final observé 0,1771 s après
la détection d'un fondu déjà engagé (ce n'est pas sa durée totale). Pas d'exception
ou d'erreur console dans `studio-diagnostic-final.txt`. Capture de la vraie fenêtre
via Computer Use : `studio-inspected.png`, SHA-256
`58474b1ad5c90b007cdaab18d8f7a6fb20eeda7c52830ba3caecbb844a67411e`.
Scène, pont, joueur et forêt visibles ; inspection agent après le parcours, pas
preuve pixel par pixel pendant chaque transition. Viewport 1408×966, donc pas un
comparatif FHD natif/Studio ou benchmark. Huit processus possédés fermés,
`studio-cleanup.json` confirme aucun restant.

Sonde optionnelle : `--temporal-lod-probe` en natif avec enregistrement bridge ;
`forge_probe=bridge&forge_temporal_lod=1` dans le Studio. Elle interrompt le déplacement
600 ms au premier fondu observé, puis reprend. Au plus huit groupes et 64 observations,
cadence au plus 50 Hz. Les observations natives référencent leur PNG exact.
Le rapport vérifie la stabilisation CPU pendant l'arrêt ; l'inspection des images
et les mesures temps réel restent des preuves distinctes. Les anciens protocoles
ne changent pas lorsque cette option est absente.

## Verdict

Implémentation raccordée et contrôle ciblé natif/Studio PASS. Le fondu n'est plus
lié au maintien de la caméra dans une bande de distance ; il se termine à l'arrêt.
Règle réutilisable par les placements gouvernés, pas une correction ponctuelle de
la carte. Terrain/herbe et fallback multi-vues explicitement hors de ce changement.
Les 30 images/s offline ne mesurent pas le temps réel ; aucune absence globale de
saccades, revue humaine, dérogation supplémentaire ou clôture de Gate 1 inférée.
Pas de dépendance ni asset ajouté, aucun commit/push. Les autres modifications
locales restent conservées.
