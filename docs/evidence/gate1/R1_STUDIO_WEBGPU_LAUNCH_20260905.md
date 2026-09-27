# Gate 1 — Lancement réel du Studio WebGPU

Date : 2026-09-05. Branche `Rust`, local non publié. Gate 1 ouverte.

## Bundle vérifié

Construction exécutée depuis `apps/forge_studio` :

```powershell
$env:CARGO_TARGET_DIR='C:\Users\karen\AppData\Local\Temp\openrpg-forge-r1-064-target'
$env:CARGO_INCREMENTAL='0'
$env:RUSTFLAGS='-C debuginfo=0'
$env:NO_COLOR='true'
trunk build --config Trunk.toml --dist E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-webgpu-20260905/dist
```

PASS, sortie 0, première compilation complète en 8 min 26 s. Profil dev optimisé,
pas un build release de qualification. Avertissement futur Rust `proc-macro-error2 2.0.1`
conservé. Le `dist` précédent du workspace n'a pas été remplacé.

SHA-256 de `forge_studio_bg.wasm` dans ce dossier externe :
`7d1604ceb92d69e003bd3291056944445da3642f31aa7c08c598a9f7e886ef0e`.

Serveur local de preuve : `python -m http.server 1420 --bind 127.0.0.1 --directory <dist>`.
Chrome connecté par le contrôle navigateur, deux navigations réelles :

- `http://127.0.0.1:1420/?forge_scene=visual-valley&forge_weather=rain` ;
- `http://127.0.0.1:1420/?forge_scene=visual-valley&forge_weather=clear`.

## Preuves observées, pas seulement compilées

Deux captures navigateur affichées et inspectées dans la conversation : terrain, végétation,
ciel, ombres et capsule joueur sont réellement présentés ; traînées de pluie visibles dans
le premier état, absentes dans le second. Les captures n'ont pas été exportées en fichiers
durables ni hashées : archivage visuel complet encore dû. Aucune acceptation humaine inférée.

Les deux ledgers `#forge-scene-telemetry-ledger` donnent
`parity_status=source_manifest_matched`, `failure_reason=null`, caméra 1/1,
eau `observed_main_world_components`, météo `observed_cpu_projection`, état attendu rain/clear.
Le champ `gpu_presentation=not_observed` reste inchangé : c'est le statut du probe CPU/ECS,
pas un verdict sur les captures inspectées séparément. Aucun warning/erreur retourné par la
lecture des logs du navigateur ; cette API ne constitue pas une garantie d'exhaustivité des logs.

Les six hashes sélectionnés sont égaux aux six hashes observés dans le runtime :

| Document | SHA-256 canonique |
| --- | --- |
| Terrain | `9f16780a1de1e9dd4b65931624a8b45281713829434292bada320a578420b2ef` |
| Végétation | `5c7619a6ee04533accbf998e3c12077363e4e3b06c86d28e9a62b76bae80a5b8` |
| Environnement | `4b252f036887ec6dbe6ae0942862d15bdba589e7deab92e62a9091e21b6cdd6f` |
| Contrôleur | `941eceefb29835c0949a9801f83f421839bcb5b0cd79583899c22d6832a74f90` |
| Eau | `c581e9ef65ca1bc34d5f94908d5f43df77794da4ad5b97e1b3c4f8c0d025ebce` |
| Météo | `b0d8e12bd49d3510361d587463a3002e98741b68052b283fa3f8ca432c87dd9e` |

Ce sont les documents embarqués historiques de la vallée, PAS le WorldSketch candidat avec
kit Blender et pont. Aucune parité de ce candidat n'est revendiquée. La rivière n'est pas
visible dans ce cadrage joueur ; ses effets visuels ne sont pas qualifiés par ces captures.

Une sélection du bouton Weather profile a produit une observation de canvas stable,
zéro instabilité et un seul démarrage du monde. Latence handler/post-paint : 51,4 ms,
un seul échantillon ; sélection locale du document, pas une mutation de profil.

## Performance et défauts conservés

Pluie : 60 frames de chauffe puis 120 mesures CPU Bevy/WASM ; moyenne 33,3075 ms,
p95 37 ms, 1 % low 25,094 FPS, moyenne 30,023 FPS. Toutes les frames dépassent 16,7 ms.
Mémoire WASM observée : 196 280 320 octets. Ce run ne satisfait pas la cible 60 FPS.
Pas de timings GPU ni de comparaison native équivalente dans ce lot.

Canvas réellement observé : 2048 × 1157, alors que le bandeau UI affiche en dur
« 1920 × 1080 » (`apps/forge_studio/src/lib.rs`, barre du viewport). Ce libellé est inexact
et doit être remplacé par une mesure réelle. Ne pas présenter ce run comme un benchmark 1080p.
La mesure initiale claire affichait également environ 33 ms malgré un FPS instantané ultérieur
plus élevé : ne pas utiliser cet instantané pour qualifier la performance.

Les pointillés des meshes végétaux, l'herbe peu naturelle et les formes procédurales restent
visibles. La météo change par rechargement de page, pas par un cycle interactif Studio.
WebView2/Tauri non lancé ; ACL et distribution non testées. Le skill de contrôle navigateur
a guidé la lecture séparée des ledgers et des captures ; aucune qualification humaine déduite.

## Suite nécessaire

Corriger le libellé de résolution, conserver des captures exportées et qualifier un run release
aux dimensions prévues. Lier ensuite la preuve au candidat artistique gouverné approuvé ;
ne pas annoncer une parité absente. Gate 1, revues humaines et CI distante restent ouvertes
(`NOT RUN` pour la CI). Aucun changement de source artistique, commit ou push.
