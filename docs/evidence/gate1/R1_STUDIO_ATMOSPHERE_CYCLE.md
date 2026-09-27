# R1 — qualification Studio atmosphère/profils : préparation

9 septembre 2026, Rust local, Gate 1 ouverte, aucune publication.

## Éviter les répétitions

`R1_STUDIO_QUALITY_FOG_CYCLE.md` et `R1_040_VOLUMETRIC_STUDIO.md` prouvent
déjà des interactions Web avec la brume. Ils ne sont pas des qualifications
Tauri, n'archivent pas de PNG local et précèdent la correction du budget
observé en Low. La mention générale « lancement WebGPU dû » de R1-020/021
ne signifie donc pas qu'aucun lancement n'a jamais eu lieu.

L'export JSON courant omettait `studio-volumetric-ledger`, pourtant publié par
le moteur et affiché dans le DOM. Raccord ajouté à `observations.volumetric`,
sans nouvelle collecte, calcul, cadence, mutation de source ou changement GPU.
Absence = null ; la borne effective absente reste absente. Le budget de pas
demandé n'est pas une mesure des pas exécutés par le GPU.

## Vérifié et restant

Six tests d'export PASS, dont états actif / fallback Low / absent ; aucun
passage de gate ou atomicité PNG/JSON inventé. Clippy WebGPU strict PASS.
Le premier contrôle de format a refusé la disposition du test ; formatage
corrigé avant tests. Aucun échec masqué. Build complet Studio en cours dans
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-atmosphere-cycle-20260909/`.
Validation bornée au Studio : aucun contrat partagé ni shader modifié.

Suite : **54 tests Studio PASS** (50 bibliothèque, 4 shell), Clippy WebGPU
PASS et compilation Rust release terminée en 21,39 s. Optimisation/assemblage
encore en cours, session 22176 vivante au dernier relevé ; ne pas redémarrer
sur une simple absence de sortie. Aucun nouveau rendu Tauri encore observé.

Protocole restant : candidat Tauri sans optique expérimentale épinglant les
profils, scène WorldSketch avec kit/forêt ; états High sans brume, High avec
brume, Low demandé puis retour High et désactivation. Exporter les observations
et pixels utiles, vérifier une initialisation et la restitution, puis fermer
les processus possédés. Ne pas appeler cela un benchmark ou une acceptation
artistique ; les captures non Full HD gardent cette limite explicitement.

## Cycle Tauri exécuté — 9 septembre, 08:12–08:19 locale

La préparation ci-dessus est historique : session 22176 terminée code 0.
EXE `BBDEE9099D117C4D18C9250336F2D38DA73250B998F6EFAAB6ED0B92ED391E80` ;
WASM `986855193BF56EAD2E4EA81BDA4F3B316419F00444643819C9E44244734C6425`.
Entrée `index.html?forge_scene=world-sketch-visual-valley&forge_kit=governed&forge_forest=batches`.
Une session, clics réels, aucune injection DOM/ECS, aucun déplacement demandé.
Un clic PNG hors fenêtre a été refusé : défilement et nouvelle observation
avant reprise, pas de faux export.

| État | Appliquée | Pas demandés | Borne | Suffixe JSON |
| --- | --- | --- | --- | --- |
| High initial | non | 64 | absente | 1788934438461 |
| High activation | oui | 64 | 128 m | 1788934557237 |
| Low, demande maintenue | non | 0 | absente | 1788934636265 |
| Retour High | oui | 64 | 128 m | 1788934691135 |
| Désactivation High | non | 64 | absente | 1788934735546 |

Cinq `forge-diagnostic-<suffixe>.json` archivés dans le dossier local ci-dessus.
Assertions PASS : états/budgets exacts, une initialisation, mêmes six hashes
runtime, gate false et preset complet false. Le manifeste n'est pas un rehash
continu des fichiers. Kit prêt 20/20 et 36 colliders, 1662 arbres au départ.
Préparation initiale 35 441,4 ms, 98 pipelines, zéro attente/échec final,
pic transitoire 19 erreurs conservé. Ce ledger de démarrage ne qualifie pas
chaque pipeline ultérieur ; application effective acquittée = null.

Deux PNG GPU inspectés, 1408 × 966 : sans brume `1788934497866`, avec brume
`1788934569433`. Voile chaud/lointain moins contrasté visible ; retour sans
voile inspecté dans la fenêtre. Pas de qualification artistique, de mouvement
caméra ou de fluidité. Temps animé non figé, végétation non identique entre
captures. JSON/PNG non atomiques. Cadence initiale 28,025 ms moyenne, p95
31,3 ms, 1 % low 30,44 FPS : pas un benchmark du cycle ni du GPU cible.

SHA-256 des cinq JSON dans l'ordre du tableau :

```text
47B36CE0547564F45366D0E1F27E0433B978BA2046F7271B60753F73B11B8EC0
5DC780DDDA1E634E574B08B946F5DF37391599CC60CE23D2B67C0D6563A1848B
BCDE7D69EBD1CDBF0D43371A0D92FEF96DFA01F987C5781CBFA299594F37CFFF
B32E472A2CA692BB21DAD73AF3BA83B6C05E14C606487760F864CF8EB27CF6A6
01B2773B5DAC5BFEBB30051DB5EE1E37A0C20C9E071F00AF7961F4BA7B1C305E
```

PNG sans : `E3BA524C399EE182B5B883D9142F1B65D46DBB5A09FD0CA2A2EBFD7BF1A203E4`.
PNG avec : `0A3F23018804073D8796F809D8971B0E134984D49D4D40C48E6BA5C43EDE6A6E`.
Processus possédés 31912, 6920, 17008, 19848, 30560, 32288, 20800, 27872
fermés, absence vérifiée. Aucun navigateur utilisateur fermé.
Export et cycle fonctionnel Tauri vérifiés ; R1-019/020/021 et profils complets
restent ouverts. Ne pas relancer ce cycle inchangé.
