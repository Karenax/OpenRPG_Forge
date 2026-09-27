# Profil météo candidat dans Studio — 7 septembre 2026

**Chargement, parité et boutons clair/pluie vérifiés dans le navigateur intégré.**
Gate 1 ouverte ; pas de validation Tauri, artistique ou de fluidité.

## Implémenté

- Opt-in `forge_weather_input=candidate`, unique et réservé aux scènes vallée. Aucune
  URL utilisateur arbitraire : lecture de `./forge-weather/candidate.json` seulement.
- Réutilisation du lecteur HTTP en streaming du kit : plafond 64 KiB, annulation 20 s,
  cache désactivé et redirects refusés. Les chemins kit existants restent inchangés.
- UTF-8, schéma strict et identité projet/zone/révision validés avant remplacement de la
  seule copie météo en mémoire. Aucune réassignation automatique de révision externe.
- Le manifeste sélectionné est recalculé avant Bevy. Erreur d'entrée : lancement bloqué,
  erreur de configuration visible. Pas de fallback silencieux vers le profil embarqué.
- Affichage du hash candidat et de sa portée lecture seule. Les boutons existants utilisent
  le même état météo moteur. Le diagnostic exporté ajoute `sunlight` et l'UI affiche les lux
  observés ; `applied_cpu` n'est pas requalifié en preuve GPU.
- Sans option, profil embarqué inchangé. Aucun fichier source sauvegardé, hot reload ou
  nouveau contrôle d'auteur Gate 2 ; le fichier candidat est déployé localement pour preuve.

## Tests et build

`cargo test -p forge_studio` : **39 tests unitaires + 3 intégration PASS**.
Clippy Studio all-targets/all-features natif et Clippy Web/WASM strict PASS ; check WASM
et build Trunk dev PASS. fmt/diff check PASS. Premier Clippy Web refusé pour deux
points-virgules manquants dans des closures, corrigés sans assouplissement de lint.
L'avertissement futur `proc-macro-error2 v2.0.1` demeure visible.

Deux tests ajoutés : opt-in strict/sans URL externe ; révision incompatible refusée sans
mutation, candidat accepté et hash météo distinct, données terrain/environnement intactes,
entrées JSON/UTF-8/taille invalides refusées sans perdre l'entrée précédemment validée.
Timeout/redirect du lecteur partagé non réinjectés dans le navigateur pour ce lot ; le kit
externe n'a pas été requalifié ici. Aucun nouveau contrat domaine/moteur, suite workspace
complète du lot moteur non rejouée ni annoncée comme nouvelle preuve.

## Parcours navigateur réel

Bundle servi sur `127.0.0.1:1430`, requête candidate HTTP 200 observée. Le fichier est
identique au candidat natif (facteur 0,65, transition 4 s, révision 3). URL initiale :
`/?forge_scene=world-sketch-visual-valley&forge_weather_input=candidate`.
Forêt procédurale, pas de kit externe ; viewport CSS/rendu **768 × 606**, hors Full HD.

1. Départ clair : un seul monde, six hashes alignés, 55000 lx ; hash candidat affiché.
2. Bouton « Aperçu pluie » : rain, mouillure 0,78, 35750 lx, `applied_cpu` et blend 1
   dans le diagnostic JSON réellement téléchargé.
3. Bouton « Aperçu clair » : mouillure 0, éclairage 55000 lx, blend 0, facteur 1 et
   `clear_target_restored_exactly=true`. Les six sources sélectionnées restent identiques
   au diagnostic pluie ; parité `source_manifest_matched`, monde toujours initialisé une fois.
4. Rechargement en `visual-valley` avec le même candidat : erreur explicite
   `expected Revision(1), got Revision(3)`, zéro monde lancé, contrôles désactivés.
5. Rechargement WorldSketch sans option : profil embarqué, réponse solaire `disabled`,
   hash météo historique `322ee487ea89a4709bbf101d78cffecc05084d7cfc1e8778c608c2bc7d2a2115`,
   une initialisation et parité alignée.

Hash candidat commun natif/Web :
`1e09a89de24ebc130f8b6a6481a578f199063578ef9db073723f669f8da46c27`.
Les contrôles de console après cycle et témoin embarqué ne retournent aucune erreur.
Le serveur signale un favicon absent (404), pas un échec de bundle ou de profil.

Une capture de l'interface sous pluie a été inspectée dans la conversation ; le PNG
final clair téléchargé a été ouvert et inspecté : vallée, végétation et personnage
visibles. Le premier PNG `forge-viewport-1788746610113.png` a été demandé près d'une
action de retour au clair : **conservé mais exclu d'une comparaison optique pluie stable**.
Pas de preuve pixel atomique, vidéo de transition ou validation des gouttes Web par ce lot.
La restitution solaire avant séchage reste établie par le cycle natif, pas par ces deux
exports Web espacés. Le rendu reste prototypal, aucune acceptation artistique implicite.

Les métriques navigateur indiquent 120 échantillons, p95 19,40 ms dans la première session
et 20,20 ms dans le témoin embarqué. Ce sont des diagnostics de démarrage d'un petit
viewport automatisé, **pas un benchmark GPU cible, ni une qualification de performance**.

## Preuves conservées

Racine locale : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-weather-input-20260907`.
Les exports téléchargés ont été copiés dans cette racine, originaux Downloads conservés.
Cache Cargo `terrain-water-20260906`, incrémental désactivé, `RUSTFLAGS=-C debuginfo=0`,
TEMP/TMP `terrain-water-temp-20260906`, sous `E:/OpenRPG_Forge/.gate1-build-cache`.

| Artefact | SHA-256 |
|---|---|
| web/forge_studio_bg.wasm | 95DC7515820844BA55E9BAC7436515376164BC942514C0C6E70C3EF465B53468 |
| web/forge-weather/candidate.json | 1DF4D7317B77EB02727448F72F644DA3CF410B407D8163D0CFEE9268387505AF |
| tests.log | 7379C305CED701E9ABBE295D8EFAA150E1FE08D5982C24FC8C9F1573F779D41F |
| clippy-native.log | 513D3E2441160E7EB306D9A17F33C91FA911D677B1CC915E820EC2718606F3FB |
| clippy-web-final.log | B39749C85D02171B015D9CB150357D242FEEC5D91EA8B49074D36B4468FC1F09 |
| forge-diagnostic-1788746592962.json — pluie | 8FF39A5986C2C152F56E23F9DD2C136A2E014E02B97E21CA9FF3F69C6FC7C42F |
| forge-diagnostic-1788746680782.json — clair restauré | 846D4229ABEF51CBD7B89CF32C6531BDCEF2FDA6F8B8A4B93AED4B609BD20D8F |
| forge-diagnostic-1788746746529.json — refus | 8AEF2B672EF6EB8B5736375F871E31C6B97FACA201B46E8999A557DF894FA9C2 |
| forge-diagnostic-1788746789500.json — embarqué | FEEAE2A01463D5F98D6489C5839624BB8B562CBE952000A80F43EB85586E6F85 |
| forge-viewport-1788746681095.png — clair restauré | 8EF453DF11585DEB2C3DB37DF8CA7D5435087750748CD0D7FE2EC4870D1C4F30 |

Branche Rust, HEAD de base `f786a38aca2023f08d708687f99a47d14f34e6d4`, arbre modifié
préservé. Onglet de preuve fermé, serveur temporaire arrêté ; autres onglets non touchés.
Pas de publication ou d'asset Cadle. CI distante NOT RUN. Les preuves réduisent la dette
d'accès Studio, pas les critères humains ou les exigences restantes de Gate 1.
