# Tidewater importé : édition et Playtest dans Studio

Le Playtest Studio de la zone Tidewater importée charge désormais sa bibliothèque
vérifiée après le rechargement de page, projette le terrain échantillonné et les
plantes, synchronise les collisions et démarre un joueur sur la plage. Le démarrage
utilise la première personne, face au village, sans capsule visible. La vue à la
troisième personne reste accessible par V. Le chemin procédural existant garde
son comportement initial.

Le garde-fou Playtest distingue les sources procédurales des sources importées.
Pour ces dernières, il vérifie les hashes de terrain et de végétation, leur
résidence et les placements acceptés avant de déclarer la simulation prête.
Cette tranche concerne la marche sur terre. Les tracés, rivières et bâtiments
paramétriques ajoutés sur ce type de source sont refusés tant que leur projection
n'est pas raccordée. L'eau et la nage Tidewater ne sont pas validées ici.

## Vérifications locales

- `cargo check --locked --offline --target wasm32-unknown-unknown -p forge_studio --no-default-features --features web -j1` : passe.
- `cargo test --locked --offline -p forge_engine first_person_start_hides_capsule_and_sets_eye_projection --lib -j1` : 1 test passe.
- `scripts/tidewater/Invoke-FishStandStudioSmoke.ps1 -SelfTest` : passe.
- Smoke CDP du candidat 009 : `E:/OpenRPG_Forge/.tidewater-integration/studio-imported-playtest-smoke-011/fish-stand-studio-smoke-ledger.json`, statut `passed`. L'échoppe est placée, acceptée, sauvegardée et retrouvée après réouverture ; le mode Jouer démarre sans rechargement manuel de la bibliothèque ; la capture du pointeur et W déplacent le joueur de `(53.60, 3.40, -77.00)` à `(53.61, 3.65, -80.22)` avec contact au sol ; le retour à l'édition préserve les archives. La fixture source est inchangée.
- Candidat local `studio-imported-playtest-candidate-009/forge-studio-candidate.exe` : SHA-256 `4511D1DD8CC5C49F8C7D2D8CC366E7F03B3F9C051041EC9CDF2BB3FE4FEC9DDA`. WASM emballé : SHA-256 `9FEB37BB63C0A4603665B68AF14DD362EAC9F766DAF27FEA016D2ECDEA72173D`.
- Capture après déplacement : `E:/OpenRPG_Forge/.tidewater-integration/studio-imported-playtest-smoke-011/fish-stand-playtest.png`.

La capture montre le village et le relief, mais aussi un écart visuel important
avec la référence Tidewater : ciel peu détaillé, lumière et contraste plus plats,
eau non rendue comme dans la scène native. Le statut de simulation prouve la
cohérence des sources CPU et de la physique, pas la parité GPU. Ce candidat n'est
ni une qualification de distribution ni une publication.

## Continuation visuelle du 29 septembre

Le candidat 012 ajoute à la caméra du Playtest Studio le grade HDR/ACES,
l'exposition/saturation/contraste, FXAA, netteté, vignette et bloom du profil
jouable natif. La courbe et le masque de mesure d'exposition sont maintenant
partagés dans `forge_engine::tidewater_exposure` ; le jeu natif relit ces
mêmes fonctions. Le suivi des nuages reprend le contrôleur après les 120 étapes
vérifiées du paquet et publie les paramètres courants au ciel, au relief, aux
plantes et aux bâtiments. Le joueur regarde le village avec un angle de 12°.

`cargo check --locked --offline -p forge_engine --example tidewater_plant_probe -j1`
et le check Studio WebAssembly passent. Le smoke isolé 015 passe : placement,
sauvegarde, réouverture, Playtest, 56 images de nuages dynamiques observées
pendant le déplacement, collision au sol, retour à l'édition et source intacte.
Reçu : `E:/OpenRPG_Forge/.tidewater-integration/studio-imported-playtest-smoke-015/fish-stand-studio-smoke-ledger.json`.
Exécutable candidat SHA-256
`8E4FF359BCC643042B31BFC6DA12A9789B3D8875570EC967F5538E9623CCC441` ;
WASM SHA-256 `A725EB33C0794B6C2123FEF78A61BA0FC9EA41E79206447865A56B630C03E331`.

Comparaison interne à même caméra et même chemin Studio, captures prises avant
la prise de souris : 013 avant exposition automatique, SHA-256
`6398F5FF249B00DB51FFDFDF53C80FBF8D542ABC370B6B6845AD8C51C0E5EFB4` ;
015 après, SHA-256
`650E228A4AFDA847E6387E36681EB794528437A16C2B86A9668E2D12F76CF4BD`.
Luminance sRGB approximative `0,2126 R + 0,7152 G + 0,0722 B`, un pixel sur 8
sur chaque axe :

| Région dans la capture 1920×1080 | Avant | Après |
|---|---:|---:|
| Ciel, x300–1200 / y200–350 | 116,31 | 192,12 |
| Reliefs, x400–1200 / y430–550 | 25,49 | 53,28 |
| Maisons, x950–1350 / y555–720 | 45,31 | 93,06 |
| Plage, x520–1300 / y800–960 | 142,32 | 199,96 |

La lisibilité du village progresse, mais la plage est très claire et le ciel
reste presque uniforme dans cette vue malgré les soumissions des nuages.
Cette paire A/B interne n'est pas une comparaison appariée avec le site source :
sa caméra, sa météo, sa résolution et sa chaîne temporelle doivent encore être
reproduites. Eau, effets fins et parité visuelle complète restent ouverts.

## Réglages graphiques du Playtest et candidat 014

Le panneau **Tester & suivi → Paramètres graphiques Tidewater (Playtest)**
commande maintenant, pendant la marche, l'exposition EV, la saturation, le
contraste, la netteté, le bloom et la vignette de la caméra Bevy, avec remise
à zéro. Ces choix sont temporaires et ne touchent pas le document de zone.
Les autres commandes de l'onglet Effects de la référence restent dans le menu
de la promenade native `H`/`F10` ; le Playtest Studio n'expose que les six
effets effectivement raccordés à sa caméra.

Le check WebAssembly, `rustfmt --check` ciblé, `git diff --check` ciblé et le
self-test du smoke passent. Le build release WASM et l'exécutable Tauri local
`studio-imported-playtest-candidate-014/forge-studio-candidate.exe` ont été
reconstruits. SHA-256 du WASM :
`5920420E2B67581CFDC632BB13A2DF21D2122A698CBF41E44F8A300F9EF8C42D` ;
SHA-256 de l'exécutable :
`71DF943A75504B7DEA006C7861A815FB8A1F5738DB21646C887F8CB7C8BF8141`.
L'exécutable copié est bit à bit identique au binaire Cargo fraîchement bâti.
Le test unitaire natif du démarrage en première personne a été relancé après
ces changements, mais sa compilation moteur est restée en attente dans le
cache partagé pendant plusieurs minutes ; cette relance a été interrompue,
sans verdict de test. Le check natif de l'exemple Tidewater et le parcours
réel du candidat 014 ont passé.
`Build-StudioCandidate.ps1` refuse désormais un nom de binaire différent du
`[[bin]]` Cargo : l'ancien chemin permettait de copier par erreur un exécutable
périmé depuis le cache. Le candidat 013 n'est pas retenu pour cette raison.

Le smoke isolé
`studio-imported-playtest-smoke-017/fish-stand-studio-smoke-ledger.json`
est `passed` : échoppe placée, acceptée, sauvegardée et retrouvée après
réouverture ; Playtest, exposition réglée à −1,00 EV puis rétablie à +0,40 EV
et observée sur la caméra ; capture du pointeur, déplacement de
`(53,60 ; 3,40 ; −77,00)` à `(53,61 ; 3,69 ; −80,97)`, contact au sol ; retour
à l'édition ; source préservée. Le premier essai 016 a échoué sur le premier
clic de reprise de focus après le panneau, puis le test 017 a validé la
reprise avec un second clic utilisateur CDP si nécessaire.

À caméra fixe, les captures 017 avant et après le changement d'exposition
montrent un effet mesurable dans le renderer (luminance sRGB, un pixel sur 8) :

| Région | +0,40 EV | −1,00 EV |
|---|---:|---:|
| Ciel | 191,98 | 124,03 |
| Reliefs | 53,21 | 27,65 |
| Maisons | 92,94 | 49,07 |

Ce test prouve que le curseur agit sur l'image, pas une équivalence des EV
avec la démo. Les captures utilisateur de la source montrent toujours une
présence de nuages et un niveau de finition supérieurs. Le Playtest Studio
ne projette toujours pas l'océan et les détails côtiers du runtime Tidewater
complet ; les captures ne sont pas appariées à pose, météo et temps égaux.
