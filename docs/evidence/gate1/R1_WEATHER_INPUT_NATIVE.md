# Profil météo candidat — chargement et preuve native, 7 septembre 2026

**Implémenté et vérifié en natif ; candidat artistique non promu. Gate 1 ouverte.**
Suite de `R1_WEATHER_SUNLIGHT_ENGINE.md`, sans modifier le contrat partagé ni le moteur.

## Périmètre

`forge_runtime --weather-input <json>` accepte un fichier régulier UTF-8, au plus 64 KiB
(métadonnées et lecture bornée), dans les deux modes vallée uniquement. Le parseur refuse
l'option dupliquée ou sans valeur ; le chargeur utilise le schéma météo strict et la
validation d'identité existante projet/zone/révision avant construction du renderer.
Un profil externe n'est jamais réassigné à la révision du WorldSketch. Sans option, le
chargement embarqué et sa dérivation de révision restent identiques.

Ce chemin est un outil de qualification réutilisable, pas un éditeur Studio, une commande
de sauvegarde ou un hot reload. Aucun réglage propre au nom d'une carte ajouté au moteur.

## Tests et exécution

- `cargo test -p forge_runtime` : **70 unitaires + 1 intégration PASS**.
- `cargo clippy -p forge_runtime --all-targets --all-features -- -D warnings` : PASS.
- `cargo build -p forge_runtime`, `cargo fmt --all -- --check`, `git diff --check` : PASS.
- Deux nouveaux tests : portée/duplication CLI ; round-trip candidat, révision refusée puis
  correcte, autre zone refusée, source intacte, JSON invalide, UTF-8 invalide, taille dépassée,
  fichier absent et répertoire refusés.
- Le binaire réel refuse le profil embarqué révision 1 fourni comme fichier externe au
  WorldSketch révision 3 : `startup_failed`, phase `scene_load`, **exit 2**, avant renderer.
- Trois exécutions natives distinctes : **exit 0**, zéro événement ERROR chacune, captures
  émises frame 300 et readbacks reçus frame 303 ; pipelines prêts 98/98 puis 100/100.

Les premiers contrôles ont révélé un import de test déplacé, le dépassement de deux lignes
du parseur et une conversion numérique de test. Corrigés sans désactivation de lint ; logs
initiaux `tests.log` et `clippy.log` conservés. Aucun contrat partagé changé par ce lot :
suite workspace/WASM du lot moteur précédent non rejouée ni relabellisée comme nouvelle.

## Protocole et observation des images

Racine locale : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-weather-input-20260907`.
`Replay.ps1` conserve les arguments exacts et refuse de remplacer une collecte existante.
Le candidat est dérivé du profil météo embarqué : révision 3, ajout de
`sunlight = { rain_illuminance_multiplier: 0.25, transition_seconds: 4.0 }`, rien d'autre.
Ces valeurs restent synthétiques, non promues. Windows, RX 7800 XT, Vulkan, pilote 26.8.1,
1920 × 1080, High demandé, VSync par défaut, 480 frames, métriques après 120 frames.
WorldSketch externe du dossier `r1-camera-set-20260906-c`, kit/catalogue du dossier
`r1-foliage-coherent-runtime-20260906-b`, preview `keep_landmark-clear` de la série
`r1-twelve-views-20260906`, aperçu forêt à prototypes activé.

| Run | Réponse solaire | Lux | Mouillure | FPS moyen / p95 ms / 1 % low |
|---|---|---:|---:|---|
| candidate-clear | applied_cpu, facteur 1 | 55000 | 0 | 74,59 / 14,86 / 43,49 |
| reference-rain, sans option | disabled | 55000 | 0,78 | 77,20 / 13,48 / 53,28 |
| candidate-rain | applied_cpu, facteur 0,25 | 13750 | 0,78 | 76,99 / 13,65 / 53,15 |

359 échantillons par run, incluant le coût du readback : **smokes, pas benchmark ni gain
de performance**. Le 1 % low clair n'atteint pas 45 FPS ; aucune qualification cible.

Les trois PNG ont été ouverts et inspectés. Cadrage identique, architecture et végétation
présentes. Le candidat pluie assombrit nettement ciel, terrain et objets ; l'effet n'est
donc pas seulement un champ diagnostique. Les surfaces mouillées restent brillantes et
les ombres très dures, plusieurs silhouettes deviennent presque noires. **Le facteur
0,25 n'est pas retenu comme amélioration artistique globale.** L'atténuation ne simule
pas la couverture nuageuse ou la diffusion du ciel.

Même terrain `18bd933d37f06a6a08a34b19938a78cb8f6fe8d9667b34bd2dcd6355956fd208`,
mêmes hashes environnement/eau dans les trois métriques, même caméra `keep_landmark`.
Météo candidate clair/pluie : `b4307aa8ea4fa17f4613013360b61d5439b656c51a30d5a198106f1bbd6ec585`.
Référence pluie : ancien hash `322ee487ea89a4709bbf101d78cffecc05084d7cfc1e8778c608c2bc7d2a2115`.
Fichier météo canonique inchangé : SHA-256
`B2C69CEC24AF5AE398D084F20E14E3659A633E60D87806498208D9813E2D4284`.

Les simulations ont leur propre temps réel, pluie/vent et bruit temporel ; ce n'est pas
une comparaison pixel à pixel synchronisée. Le JSON final n'est pas un snapshot atomique
de la frame PNG. Aucun cycle animé clair/pluie/clair, restauration GPU à chaud, Web ou
Tauri qualifié ici. Les tests ECS précédents restent la preuve de transition/restauration.

## Intégrité des preuves

Base Git `f786a38aca2023f08d708687f99a47d14f34e6d4`, branche Rust modifiée, tracking local
behind 103 sans fetch. Qualification intermédiaire du binaire local, pas d'un arbre propre.
Cache Cargo `E:/OpenRPG_Forge/.gate1-build-cache/terrain-water-20260906`, incrémental désactivé,
`RUSTFLAGS=-C debuginfo=0`, TEMP/TMP `terrain-water-temp-20260906` dans le même cache parent.

| Artefact | SHA-256 |
|---|---|
| forge_runtime.exe | A13255F268EF121B8D34662F5B055D68B1D9E7CA6F35266FC6936E4CB5982D54 |
| candidate.json | 8F79CA8AE9166D5046AF11380E8EC942E540772532824618A55B9BB15C74D7C9 |
| tests-verified.log | 0CDF12108311143344E2A984855B7EF18A1DCBB00FA68CF6C0C06BFD70E8C355 |
| clippy-verified.log | 469CB4CB6937A76D2EF5A94E53BFA0BF43B6570929D32DEFF4C7A3E5D62ADC3E |
| candidate-clear/capture.png | A7CFF2C53C949E1870B72F7D3E6239611EEB5255C97DE86B21307FBB1675F618 |
| candidate-clear/metrics.json | BBDB052404CACE24FC2864554E20826DFAFF2ADDEDA0A8A9DCFD45D02A91A402 |
| reference-rain/capture.png | 2E607DF880BEEED300F2729FDD95BD293A0FCB05770B78790746A834863A0038 |
| reference-rain/metrics.json | C66233705AA543D9D29484763B29490893A0631BF3297E9E9ABDDF3103A5D4E3 |
| candidate-rain/capture.png | E19608065A96F97D031430F6CCE058105AA230FBCF10DA8F03451EC60D249392 |
| candidate-rain/metrics.json | D1E95CCEF83E08FD388F64B93BE3272AAFDD7941160E4D3FE0E72B1ECDE5F7CA |

Suite compatible Gate 1 : réutiliser ce chargement pour une transition réelle bornée et
pour comparer un éclairage équilibré, sans multiplier les captures du même réglage rejeté.
Pas de nouvelle gate, de publication, de nettoyage ni d'asset Cadle. CI distante NOT RUN ;
acceptations humaines explicites toujours requises.

Suite exécutée : `R1_WEATHER_NATIVE_CYCLE.md` documente maintenant un cycle natif unique
clair/pluie/clair et trois captures, avec un candidat 0,65 non promu. La vidéo/qualification
de fluidité et le Web restent dus ; le présent rapport conserve ses trois runs statiques.
