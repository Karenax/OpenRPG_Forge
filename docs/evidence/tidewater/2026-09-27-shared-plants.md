# Vegetation dans le moteur partage — 27 septembre 2026

## Renderer

`tidewater_plants` porte maintenant le materiau, ses buffers d'instances, ses
attributs aVeg/aMat/aLobe et son shader charge en memoire depuis des octets
verifies par l'appelant. `scene` prepare les six groupes et leur selection
near/far avec l'ordre source ; `canonical` reconstruit les streams depuis les
documents Forge et applique les corrections de la zone. Les adapters natifs
reexportent/utilisent ces implementations au lieu de les dupliquer.

La creation de scene accepte une camera et une visibilite explicites et renvoie
les entites derivees a retirer lors d'un remplacement. Elle refuse une camera
non finie avant modification. Le branchement au cycle de navigation Studio,
les identites de picking des instances, la reutilisation dynamique des buffers
et la qualification des gestes d'edition ne sont pas encore acquis.

Les cellules d'herbe, leur selection, les textures et le chargement de la
population restent a raccorder dans Studio. Le deplacement des fonctions dans
le moteur ne prouve pas l'accessibilite depuis l'interface.

## Paquet local

`scripts/tidewater/package-plant-assets.py` produit la fermeture des dependances
geometrie/texture/shader/herbe du fixture epingle. Les placements importes restent
dans les documents du workspace : les anciens buffers d'instances ne sont pas
copies dans le paquet. Les descriptions source intactes conservent leurs noms
de streams ; le chargeur devra fournir la population canonique avant projection.

Source : Tidewater 1438b1abfcaee3267092b75573014f4d9b4a983c, MIT, licence incluse.
Fixture 0b4437a7fa404d47ef22b430f7fa4eff573b86c5fc5f62742d25a25be5561027.
Paquet E:/OpenRPG_Forge/.tidewater-integration/plant-package-001,
83 payloads, 46 590 597 octets, manifeste
1fec8a063214ad05ceac19f277dbd17e131e04d2714378bae9239efd12224f83.
Ce paquet n'est pas un GLB aplati : les attributs et la deformation shader sont
necessaires au rendu source selon ADR-0017. Il ne constitue pas encore un import
complet dans le Vault ou le catalogue produit.

Deux constructions deterministes PASS. Refus verifies : pins fixture/manifeste
incorrects, chemin sortant, payload corrompu, dossier de sortie deja existant.
Copie de test restauree et reverifiee ; aucune source originale modifiee.
Preuves : plant-package-proof-001.json dans le dossier d'integration.
Le lecteur Rust du paquet et le transport Studio ne sont PAS IMPLEMENTES.

## Verification du renderer

Clippy moteur/probe strict --no-deps PASS. Build natif PASS (7 min 17 s).
Compilation Studio WASM PASS (7 min 46 s, attente du build natif comprise).
Execution native PASS avec le workspace canonique et la bibliotheque de batiments.
Capture inspectee : rendu conserve. Les trois sorties sont identiques octet par
octet a native-shadow-cache-001 :

- native-shared-plants-001.png :
  243ddf9659f95afea4bbd978b2b47006983d052fe3d841958c6f45182a3c4e78.
- native-shared-plants-001.shadow.bin :
  a8077ba841b789d6acfc7c7bc7da39496920a3b294068eab4b5ad25cb7ee8288.
- native-shared-plants-001.object-depth.bin :
  0c38d6ff937fbf5fe6248bbf5a272781804876bbe081222545f0334b611553a1.

Preuve locale : shared-plants-proof-001.json, log native-shared-plants-001.log.
Cette comparaison qualifie la non-regression de la scene native fixe Forge,
pas la parite complete upstream, ni le canvas Studio, ni le chargement du
nouveau paquet (la scene de preuve emploie encore son fixture complet).
Tous les processus d'essai ont termine.
Aucune publication, distribution Studio ni cloture de l'objectif global.

Suite du meme jour : lecteur Rust et acquisition optionnelle maintenant
implementes, verifies et compiles ; voir
[Lecteur du paquet vegetal](2026-09-27-plant-package-reader.md). Le canvas et les
gestes d'edition restent non qualifies.
