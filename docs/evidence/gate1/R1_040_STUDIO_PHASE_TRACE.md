# R1-040 — trace CPU bornée du cycle Studio

11 septembre 2026. **Instrumentation implémentée et vérifiée ; cycle mobile FAILED.**
Ce lot localise certains coûts, ne revendique ni optimisation ni fermeture de gate.

## Implémentation et validations

`QualityCpuTracePlugin`, activé seulement par `forge_quality_motion=1`, mesure
six phases du thread principal : préparation/application terrain, préparation/
application végétale, forêt gouvernée inclusive et lots forestiers lointains.
Le compteur est remis à zéro chaque frame. Une absence reste `null`, pas zéro.
La phase forêt inclut les lots lointains : leurs valeurs ne s'additionnent pas.
La sonde conserve par étape huit intervalles les plus longs, leurs mesures CPU
de la frame précédente, les maxima CPU et les premières observations de readiness.
Pas de nouvelle dépendance, seuil modifié, carte retouchée ou activation permanente.

- 704 tests workspace PASS, deux sondes CPU explicitement ignorées par défaut.
- Trois nouveaux tests : remise à zéro/activation facultative, absence et
  imbrication des mesures, conservation bornée du pic ancien et des valeurs nulles.
- Clippy strict workspace/all-targets/all-features et Studio Web PASS ; fmt PASS.
- Frontend release 2 min 24 s et bundle Tauri construits. Avertissement futur
  `proc-macro-error2` conservé ; aucune prétention de CI distante verte.
- 281 fichiers source épinglés puis revérifiés sans écart après construction.
  HEAD Rust `f786a38aca2023f08d708687f99a47d14f34e6d4`, arbre modifié, non publié.

Reproduction et journaux dans
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-phase-trace-20260911/` :
`Validate.ps1`, `Build.ps1`, `Run.ps1`, `source-inputs.json`, `tests.log`,
`clippy-web.log`, `studio/build-proof.json`, `cleanup.json`.

## Acquisition réelle

Même URL/scène/kit/parcours et même limite 120 s que
`R1_040_STUDIO_MOTION_20260911.md`. Pas de préchauffage explicite des quatre
profils avant le parcours : cette acquisition inclut démarrage et premières
transitions, elle n'est pas une mesure de transitions préchauffées récurrentes.
Contrôle Windows utilisé pour ouvrir l'instance, exporter JSON/PNG et la fermer.
Originaux : `forge-diagnostic-1789112591911.json` et
`forge-viewport-1789112602667.png`, conservés aussi sous `studio-motion.json/png`.

| Fichier | SHA-256 |
| --- | --- |
| EXE | `159ce306e1c8d29fe4edd1e7867972441ab57b3c2b976b9867a8b30c7f023d6a` |
| WASM | `b869c0890b1cd378b532ca80858320f28055238e1f09a9118f81472d8088ed07` |
| JSON | `77754d8656c5081a7ee4140a3d84fc2d6057f24456146773fde7952263361994` |
| PNG | `aa4634db219cf5a0f41593e78436f1e675fe5626776e37900b184420e8e23643` |

Traversée completed, 42,529579 m, 518/518 ticks grounded, 434 contacts, aucune
erreur du parcours. High puis Low acquittés en marche (ticks 370/651). Le retour
High est prêt à l'arrêt : seulement une frame mobile dans la troisième étape,
donc aucune troisième preuve mobile. Verdict conservé : `FAILED: délai ou parcours`.

| Étape | Première observation (s) | CPU prêt (s) | GPU prêt (s) | Frames mobiles / observées |
| --- | ---: | ---: | ---: | ---: |
| High initial | 0 | 23,5596 | 24,3982 | 72 / 86 |
| Low | 24,7221 | 42,6236 | 42,9986 | 87 / 87 |
| High retour | 43,2151 | 49,6585 | 50,1737 | 1 / 1941 |

Temps relatifs au démarrage de la sonde, pas durées de chaque phase.

| Maximum CPU d'un appel (ms) | High initial | Low | High retour |
| --- | ---: | ---: | ---: |
| Préparation terrain | 53,9 | 10,7 | 12,6 |
| Application terrain | 3,0 | 2,4 | 1,9 |
| Préparation végétale | absent | 197,2 | 188,8 |
| Application végétale | absent | 12,5 | 7,7 |
| Forêt inclusive | 2600,8 | 341,1 | 333,7 |
| Dont lots lointains | 2279,5 | 5,0 | 5,6 |

L'intervalle initial de 12036,5 ms suit 2600,8 ms de forêt, dont 2279,5 ms de
lots lointains. Les grands intervalles Low de 7758,2/5913,4 ms suivent seulement
2,3/2,5 ms de préparation terrain et 1,7/1,2 ms de préparation végétale ; aucun
commit mesuré à ces frames. En revanche, la projection forestière de 341,1 ms
est bien associée à un intervalle de 804,8 ms. Au retour High : forêt 333,7 ms
associée à 581,4 ms ; pics de 1408,1/1108,9 ms non expliqués par ces six phases.

## Conclusion utile, sans faux diagnostic

**Le commit végétal n'explique pas les pauses de plusieurs secondes.** Le
découper davantage n'est pas la prochaine correction justifiée par cette trace.
Deux coûts CPU existent réellement : création initiale des lots forestiers et
reprojection inclusive lors des changements. Mais ils n'expliquent pas la totalité
des pauses. Préparation initiale des sources, systèmes hors instrumentation,
extraction/rendu Bevy, compilation de pipelines, navigateur/driver et attente
externe ne sont pas distingués ici. Ne pas attribuer automatiquement le reste
au GPU, ni prétendre qu'un timestamp de readiness est un chronométrage GPU.

La suite pertinente est de séparer un cycle **après le préchauffage existant**
du premier cycle à froid, avec un parcours mobile borné couvrant les trois
étapes et des timings conservés, puis cibler le coût restant. Une qualification
fonctionnelle en marche ne remplacerait toujours pas celle des saccades ; le
présent échec à froid doit rester visible. Pas de répétition identique espérant
un PASS, pas de seuil assoupli ou de readiness immobile rebaptisée mobile.

PNG 1408×966 relu : route, personnage, forêt et ciel affichés ; tramage visible
sur certains troncs. Eau hors champ. Aucun nouveau verdict artistique déduit.
Les dérogations existantes, revues humaines du kit et verdict sur révision exacte
restent inchangés ; Gate 1 ouverte, Gate 2 non commencée.

Fermeture normale sortie 0. Huit processus possédés terminés, aucun survivant :
3568, 23968, 15688, 15312, 17760, 30764, 35788, 16064. Aucun navigateur utilisateur
arrêté, aucune suppression, aucun commit/push ni asset Cadle introduit.
