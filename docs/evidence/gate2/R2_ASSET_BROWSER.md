# Navigateur du kit gouverné — 12 septembre 2026

## Implémenté

R2-026 : index de recherche en lecture seule dans forge_assets et panneau
Bibliothèque d'assets dans l'espace auteur. Réutilise le rapport existant
forge.blender-asset-kit-build v1, pas un nouveau catalogue canonique ou un
importeur privé/acheté. Le rapport est validé avant indexation, jusqu'à la limite
existante de 1024 assets. Provenance, licence, hashes, IDs et chemins conservent
les validations de Gate 1 ; aucun contournement pour admettre du contenu Cadle.

Recherche par mots dans ID/type/catégorie française, insensible à la casse et
aux accents français usuels, tirets/underscores séparateurs. Filtre arbres,
rochers ou architecture, tri stable par ID, requête limitée à 256 octets.
Fiche : kit/révision, provenance et licence déclarées, chemin GLB relatif, hash
attendu, état de revue humaine de la source retouchée. Aucun faux nom d'usage,
pivot, dimension, collision ou droit de redistribution déduit des identifiants.

Chargement volontaire de ./forge-kit/build.json via le transport local existant,
limite 1 Mio et délai 20 s, aucune redirection admise. Kit absent ou invalide :
message explicite, aucune ancienne fiche conservée après échec de rechargement.
Le chargement d'un index n'est pas un chargement de tous les modèles.

Bouton Vérifier le fichier GLB sélectionné : un seul fichier à la demande,
64 Mio maximum, SHA-256 attendu, en-tête/longueur JSON et absence d'URI selon
le précontrôle partagé. Octets relâchés ensuite ; aucun moteur/mesh/collision
créé. Changement de sélection : ne pas afficher le résultat de l'autre asset.
La fiche distingue manifeste déclaré et fichier contrôlé. Ce contrôle n'est
ni le validateur glTF complet, ni une qualification visuelle/placement/runtime.

## Vérifié

`cargo test -p forge_assets asset_browser --locked --offline` : 2 PASS, 0 échec,
0 ignoré, 21 filtrés (0,00 s hors compilation). Tri, recherche multiterme,
accents/casse/séparateurs, catégorie, borne et préservation des entrées ;
rejets Cadle, licence inadaptée au contrat generated et traversée de chemin.
Clippy forge_assets/all-targets strict PASS (0,75 s), Studio WASM strict PASS
(3,74 s après correction d'un masquage de variable du raccord GLB).
Formatage/diff contrôlés ; avertissement amont proc-macro-error2 inchangé.

## Non vérifié / restant

Panneau codé/compilé, pas encore lancé avec un kit déployé dans ce candidat.
Transport GLB choisi et messages d'erreur non qualifiés graphiquement. Les
tests d'index n'attestent pas des octets d'un vrai GLB. Aperçu 3D individuel,
drag/drop, placement, gizmaux et sauvegarde des placements restent à construire.
Le binaire c999bcfa... précédent ne contient pas ce navigateur.

Compétence openrpg-import-assets utilisée pour séparer les statuts/provenance
et ne pas transformer une référence de catalogue en asset qualifié. Aucun
import, conversion Blender, mutation du Vault ou helper d'import nécessaire
pour ce raccord au contrat Rust existant. Aucun asset copié, aucun réseau
externe lancé par ce lot, aucune publication Git. R2-026 et Gate 2 restent ouverts.

## Essai produit ciblé

Candidat natif SHA-256
`9ea00c8cff4792f02f6a6f07e29f73f164003989e560f2bdbf0810345c928b82`,
Trunk/WASM 2 min 16 s et Tauri debug sans bundle 40,71 s, PASS.
Configuration locale `.gate2-evidence/tauri-authoring-assets.conf.json`, écran
secondaire x=-1920/y=52, 1920×1080. Aucune zone ou scène GPU ouverte.

Kit source en lecture seule :
`.gate1-evidence-runtime/r1-terrain-staged-20260911/studio/web/forge-kit`.
Rapport SHA-256 `06a4ecb34e03aca33ba410a4378b36b38c272d78610364024dbaa2bf27566c1a`,
six GLB copiés vers le candidat après contrôle chemins/provenance/hashes.
Source procedural_original_with_authored_edits, licence generated existante,
publication non approuvée et revue humaine pending conservées ; aucun Cadle.

Studio observé : six références triées ; recherche « vegetation » sans accent
→ deux arbres ; fiche vv-tree-oak-a avec kit/révision, provenance, licence,
chemin et hash. Bouton de contrôle : 11 739 160 octets, SHA-256 conforme et
précontrôle GLB autonome réussi. Hash du chêne :
`b8d4e241a4995c1574bdd4e2df0e413e6c5504d8875bfa8c19bacf560747d422`.
Captures affichées et inspectées dans la conversation, aucun PNG local prétendu.
Le résultat inférieur concerne seulement le fichier choisi ; clarifier ensuite
le message général « fichiers non vérifiés » pour éviter une ambiguïté.

Fermeture Alt-F4 : PID 4544 et descendants 4356, 17428, 30848, 23620, 11812,
18668, 19036 absents au contrôle final. Rapport source inchangé. Ancien binaire
conservé : `.gate2-evidence/studio-authoring-historykeys-tested.exe` (c999bcfa...).
Consultation/recherche/fiche/contrôle d'un vrai GLB prouvés sur ce candidat.
Autres fichiers, erreurs HTTP/hash/timeout non rejoués par l'UI ; aperçu 3D et
placement non acquis. Gate 2 ouverte, ADR-0014/0015 toujours proposés.

## Aperçu individuel — candidat suivant

Raccord codé dans le même App Bevy et le même canvas auteur : GLB contrôlé avant
insertion dans une source mémoire, une seule racine temporaire, aucun placement
persistant ni collider. Requiert une zone ouverte hors playtest. Le modèle est
présenté au-dessus de la carte (Y=1024 m), sans changer son échelle source ;
caméra rapprochée, rotation/zoom et retour à la caméra précédente disponibles.
Seules les primitives sous un nœud du kit ID_LOD0 sont montrées ; LOD1/2,
helpers UCX et primitives non classées sont cachés. Aucun LOD0 identifié : refus.

Sélection, retrait, rechargement de catalogue, changement de source et playtest
invalident la demande asynchrone ; remplacement/délai/erreur suppriment la
racine d'aperçu et relâchent ses handles et octets mémoire. Le chargement Bevy
est limité à 30 secondes après soumission. Aucun second moteur ou format de
sauvegarde ajouté. Les messages de vérification du catalogue ont été clarifiés.

Clippy Studio WASM strict PASS (3,89 s), formatage effectué. Premier contrôle
avait refusé deux types Option imbriqués ; remplacés par des enums explicites,
sans désactivation de lint. Build et essai graphique du candidat encore en cours ;
ne pas considérer ce raccord comme un aperçu visuellement qualifié.
Cadrage initial fixe (40 m), sans mesure automatique des dimensions ; un asset
hors gabarit peut demander un zoom. La bibliothèque reste limitée au kit gouverné
déployé, pas un importeur général. Placement et R2-027+ restent ouverts.

### Essai regroupé de l'aperçu

Build Trunk/WASM et Tauri debug sans bundle PASS, code de sortie 0 ; compilations
Rust affichées 40,26 s WASM et 41,16 s natives (hors assemblage Trunk).
Binaire SHA-256 `5473916e45f8a2881f15039f314e1f39f801797e62f93be34b5371590c3c7626`.
Config `.gate2-evidence/tauri-authoring-preview.conf.json`, frontend
`.gate2-evidence/studio-authoring-web-preview`, même kit dont les six hashes
ont été vérifiés après copie. Ancien binaire bibliothèque sauvegardé dans
`.gate2-evidence/studio-authoring-assets-tested.exe`.

Un seul lancement, écran secondaire : ouverture de la copie de zone révision 6
au hash `01f205f9000b6d3c9b80541e0c7cd6672662ee057d6686d31fe9403aa7c1e3d1` ;
aperçu du chêne visible, sélection du pin retirant le chêne et restaurant la
carte, aperçu du pin visible, bouton Rapprocher agrandissant le pin, retrait
ramenant à la vue d'ensemble initiale. Les deux arbres sont isolés sur fond
bleu, sans volumes UCX visibles. Captures affichées et inspectées dans la
conversation, pas de fichier PNG exporté. Aucun verdict esthétique humain.

Archive `.gate2-evidence/authoring-preview-ui-projects/81de8d42-89b9-58b5-b090-406cf4c63d06/authoring.json`
inchangée avant/après : `5e84f542a314e4a59517fa9fd54ac89197f602f487fd82947a81ecaefbd11757`.
Alt-F4 puis contrôle : PID 22848 et descendants 29480, 27432, 13408, 28848,
5592, 17820, 5984 tous absents. Aucun navigateur utilisateur fermé.

Parcours positif aperçu/remplacement/zoom/retrait vérifié. Rotation, erreurs de
chargement, annulation pendant téléchargement, retrait pendant playtest et
libération GPU chiffrée non qualifiés par cet essai. Aucun benchmark FPS ajouté,
aucun gain de performance revendiqué. Le cadrage automatique des petits assets
reste à faire. R2-026 global et Gate 2 demeurent ouverts.

### Cadrage automatique — code suivant, non encore rejoué

Le candidat source suivant remplace le cadrage fixe par l'union des huit coins
des AABB des seules primitives LOD0, après composition des GlobalTransform.
Ordonnancement PostUpdate après propagation des transformations et calcul des
bounds ; la racine reste cachée tant que les bounds ne sont pas disponibles.
Le pivot est le centre de l'union ; distance initiale trois fois le rayon de
sa sphère englobante, minimum 4 m. Dimensions non finies ou rayon supérieur à
300 m refusés explicitement. Aucun changement de l'échelle de l'asset ou du
document ; ces bounds de cadrage ne qualifient pas ses dimensions fonctionnelles.

Les erreurs de téléchargement tardives sont désormais également conditionnées
au ticket courant : un retrait ou un changement de sélection invalide aussi leur
message, pas seulement la soumission du modèle. Clippy Studio WASM strict PASS
(4,09 s), formatage effectué. Le binaire 5473916e... et ses captures prouvent
encore le cadrage fixe précédent, pas ce nouveau code. Build/replay rocher et
architecture regroupé requis avant qualification du cadrage automatique.

### Replay du cadrage automatique

Candidat `4e24703a73212ad4059618027695015ce34b5ffc4e7e53ee2fbc0c2db5c5a847`,
build Trunk/Tauri terminé code 0 (compilation WASM 38,75 s, native 40,06 s ;
durées hors assemblage Trunk). Configuration locale
`.gate2-evidence/tauri-authoring-framing.conf.json`, frontend
`.gate2-evidence/studio-authoring-web-framing`. Ancien candidat 5473916e...
préservé dans `.gate2-evidence/studio-authoring-preview-tested.exe`.

Un lancement, écran secondaire : zone isolée ouverte au même hash révision 6,
vv-rock-moss-a automatiquement centré et visible à taille lisible ; remplacement
par vv-architecture-arch-a également centré, arche entièrement dans le cadre.
Aucune adaptation manuelle du zoom entre ces deux gabarits. Retrait ramenant
à la carte observé. Captures affichées/inspectées dans la conversation, pas de
PNG exporté. Le rendu des matériaux n'est pas une nouvelle qualification
artistique du kit et aucune collision ou échelle fonctionnelle n'est acquise.

Archive inchangée : `5e84f542a314e4a59517fa9fd54ac89197f602f487fd82947a81ecaefbd11757`.
Fermeture puis vérification : PID 18712 et descendants 1768, 28176, 17460,
18244, 29952, 2708, 4664 absents. Aucun autre navigateur fermé.
Sources liées au candidat : preview.rs SHA-256
`8ad7bf2d78e712c50773bb1e017ad0158d749ca2b09aa242d68aa23048254ee6` ;
authoring_assets.rs `edd6a89612a0ad0791a97fdd912c5d641c5d531e33aeab1ebf462f52b79e6f27` ;
authoring_camera.rs `4cf31f92871892b09bc496112e98bf28c2f8bf33aa2c991e480d57c37f110755`.

Le cadrage de ces deux gabarits est prouvé sur ce candidat. Les cas négatifs,
annulations en cours de téléchargement, dimensions extrêmes et mémoire GPU
chiffrée restent non rejoués. Aucune répétition des audits de performance Gate 1.
R2-026 complet, placement et Gate 2 ne sont pas déclarés acquis.

### Fixture locale GLB — implémentée, non qualifiée graphiquement

`scripts/Serve-StudioKitFault.py` peut désormais rejouer sur loopback les
défaillances du **seul** GLB ciblé, sans écrire dans le candidat ni dans le kit
gouverné : un autre GLB valide au hash différent (`glb-alternate`), un corps
non-GLB (`glb-invalid`), une longueur déclarée suivie d'un corps tronqué
(`glb-truncated`), une fermeture de flux prématurée (`glb-interrupted`) et un
GLB intact envoyé par segments ralentis (`glb-slow`). Les anciens modes
`timeout` et `oversize` restent réservés à `forge-kit/build.json`.

Le serveur écoute exclusivement `127.0.0.1`, refuse les chemins relatifs
sortants, symlinks et fichiers dépassant 64 Mio ; son flux lent est borné à
60 s. `--evidence-dir` est obligatoire, doit être nouveau et rester sur E: ;
il reçoit les rapports de démarrage et d'arrêt. Il ne constitue pas une preuve
Studio, GPU ou esthétique.

Procédure Luna max après construction d'un candidat auteur isolé (exemple,
adapter le nom réel de GLB et choisir un dossier de preuve neuf) :

```powershell
python scripts/Serve-StudioKitFault.py `
  --directory E:/OpenRPG_Forge/OpenRPG_Forge-Rust/artifacts/.../candidate/web `
  --port 0 --mode glb-slow `
  --glb-path forge-kit/assets/vv-tree-oak-a.glb `
  --chunk-bytes 65536 --delay-ms 250 `
  --evidence-dir E:/OpenRPG_Forge/OpenRPG_Forge-Rust/artifacts/.../fault-glb-slow
```

Le port effectif est imprimé et consigné dans `fixture-start.json`. Pour le
hash différent, passer aussi `--mode glb-alternate --alternate-glb
forge-kit/assets/<autre-glb>.glb`. Luna utilise ensuite cette URL loopback pour
tester annulation, retrait ou message d'erreur dans le Studio, puis arrête le
processus identifié et vérifie `fixture-stop.json`. Les contrôles Python locaux
ont validé les réponses HTTP, headers, corps, interruption et délai ; aucun
Computer Use, build ou preuve visuelle n'a été lancé par ce lot.

### Durcissement de la fixture — 22 septembre 2026

Le premier replay `glb-invalid` v6 est conservé mais explicitement exclu : son
corps de 18 octets atteignait seulement le garde `GLB tronqué`, pas le parseur
annoncé. Son dossier contient `SUPERSEDED.md` et ne participe plus au verdict.

Le témoin corrigé est un conteneur GLB v2 déterministe de 24 octets, avec header,
longueur et chunk `JSON` valides ; le document `{}` est syntaxiquement valide
mais ne fournit pas `asset.version = 2.0`. Le même payload sert à dériver en
mémoire le SHA du manifeste et à répondre pour le GLB. SHA-256 du payload :
`AB7CB3261B3327ECA0EED861FD7D0F73B784DBF7F1B4D839B48661A2878D6E63`.
Il doit donc franchir transport, SHA, header et bornes du chunk avant le refus
du document gouverné.

Les ledgers RUNNING/STOPPED consignent maintenant mode, chemins ciblé/alternatif,
taille des chunks, délai, troncature, framing et sémantique de faute, tout en
conservant le `stop_path` aléatoire requis pour un arrêt loopback explicite.
`py_compile` PASS, 10/10 tests HTTP/ledger PASS et 2/2 tests Rust
`glb_preflight` PASS, sur caches et temporaires E:. Aucun replay Studio n'est
déduit de ces tests ; le nouveau témoin et l'annulation utilisateur lente
restent à qualifier par Luna max.

### Replays Luna max — témoin corrigé et frontière IAB

Le témoin `glb-invalid` corrigé est acquis en IAB automatisé sur le frontend v6.
Recherche oak, sélection et bouton de vérification réels ; le manifeste épingle
le SHA `AB7CB326…D6E63`, puis l'UI affiche le refus du document glTF 2.0 gouverné,
et non `GLB tronqué`. Aucun aperçu ni placement, fiche oak conservée, fixture
RUNNING→STOPPED et aucun listener restant. Preuve :
`artifacts/forge-runtime/gate2-resume-20260919/r2-026-negative-r20260922-glb-invalid-oak-v1/`.

Le replay lent a établi une limite distincte, sans la masquer : le frontend IAB
standalone affiche `Mode navigateur autonome : backend Tauri absent` puis refuse
l'aperçu avant tout GET GLB avec `Ouvrir une zone en mode édition`. Le retrait
pendant téléchargement et le changement oak→pine n'ont donc pas été simulés.
La fixture a néanmoins été arrêtée proprement et le blocage est conservé dans :
`artifacts/forge-runtime/gate2-resume-20260919/r2-026-negative-r20260922-glb-slow-oak-pine-v1/`.

Méthode : IAB/CUA Luna max automatisé, non natif et non humain.
`gate_qualified=false`. CI : **NOT RUN — infrastructure billing**. R2-026 reste
ouvert jusqu'au replay avec backend Tauri natif et projection réellement ouverte.

### Annulation native pendant un chargement tardif — candidat v7

Le replay automatisé CDP du 22 septembre sur le candidat natif exact
`268B824946C53323C899276BC918BF5348F87E1A8CC2F6B50CB7A7C99B3804F5`
a acquis la frontière manquante. Une zone réellement projetée est ouverte par
le backend Tauri ; le chêne gouverné réel de 11 739 160 octets, épinglé au SHA-256
`B8D4E241A4995C1574BDD4E2DF0E413E6C5504D8875BFA8C19BACF560747D422`,
est fourni par un `ReadableStream` retardé synthétique. Ce transport permet de
figer la course de manière déterministe ; il ne constitue pas une fixture de
faute HTTP réelle.

Pendant le flux, l'action utilisateur « Retirer l'aperçu / revenir à la carte »
est suivie de la sélection du pin. Le flux chêne termine ensuite ses 90 segments,
mais aucun aperçu chêne tardif ne devient visible : la fiche finale reste
`vv-tree-pine-a`, l'UI indique « Aucun aperçu actif. La carte n'a pas été
modifiée », et les compteurs IPC restent à zéro pour Preview/Apply/Save. Les
options de placement et l'archive isolée restent inchangées. Le wrapper `fetch`
est restauré à l'identique ; le PID 22252 est fermé gracieusement, sans processus
forcé, et le listener CDP a disparu.

Les deux PNG 1920×1080 ont été inspectés : panneau et terrain lisibles, pin
sélectionné, aucun aperçu chêne ni artefact visuel évident. Ledger principal :
`artifacts/forge-runtime/gate2-resume-20260919/r2-026-world-edit-v7-cancel-20260922-retry4/GATE2-WORLD-EDITING-CDP-ledger.json`
(SHA-256 `9DA3BDEC80AD953895A583E6EC1E773CDC5D2337958044CC19147A83DB3FD986`).
Ledger scénario : `asset-preview-cancel/asset-preview-cancel-ledger.json`
(SHA-256 `293476236C48A3131908EAFA138B90982B7A5D8992E33D4A56EE39FFB1F70DA8`).

Le verdict R2-026 devient **PASS local composite** : les replays antérieurs
couvrent recherche, fiche, contrôle GLB, aperçu positif, remplacement, cadrage
et refus du témoin invalide ; ce replay natif couvre l'annulation tardive sans
mutation. La revue est automatisée et non humaine, le transport lent reste
synthétique, `gate_qualified=false`, CI : **NOT RUN — infrastructure billing**.
Ce verdict clôt le critère local R2-026, pas Gate 2 ni le PASS formel ADR-0006.
