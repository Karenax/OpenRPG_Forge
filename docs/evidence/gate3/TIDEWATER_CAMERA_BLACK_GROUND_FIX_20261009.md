# Correctif008 — sol noir pendant la rotation (9 octobre2026)

Défaut reproduit puis corrigé dans le candidat natif. Le verdict reste local à ce correctif ; la Gate3 reste ouverte, et la validation humaine antérieure ne qualifie pas ce nouveau binaire.

## Cause et correction

Le walk remplaçait les handles Mesh3d du terrain CDLOD et de l'océan en PostUpdate, après propagation des transformations, sans ordre face à AssetEventSystems. Bevy0.19.1 extrait les données Mesh à partir des événements Added/Modified. Un handle changé après cette barrière pouvait donc être extrait immédiatement avec ses données GPU retardées d'une image. La spécialisation du matériau pouvait alors être retirée sans pipeline utilisable. La répétition pendant la rotation supprimait le terrain/l'océan du rendu ; le fond apparaissait noir.

Le système `walk::update` conserve PostUpdate et l'ordre après TransformSystems::Propagate, et passe explicitement **avant AssetEventSystems et VisibilitySystems::VisibilityPropagate**. Terrain et océan reçoivent leurs handles, données et visibilité dans la même image. Aucun changement de matériau, d'heure, d'ombres, de plein écran ou de paramètres utilisateur n'est requis.

## Preuves natives

AMD Radeon RX7800XT, Vulkan,2560x1440 ; profil utilisateur copié en isolation,09:30. La sonde produit des messages Bevy MouseMotion avant InputSystems, consommés par le sampler natif et la simulation réelle. Aucun déplacement clavier ni entrée OS automatisée. Rotations alternées sur place,82 captures par replay. L'observation porte sur le handle réellement extrait, les RenderAssets GPU et le pipeline spécialisé compilé, après QueueMeshes.

| Replay | Mesh terrain absent | Pipeline absent | Océan absent | Captures |
|---|---:|---:|---:|---:|
| Avant,001 | 213 / 325 | 213 / 325 | Non instrumenté | 82 |
| Corrigé final,003 | 0 / 315 | 0 / 315 | 0 / 315 | 82 |

Le premier replay corrigé002 avait déjà zéro observation Mesh/pipeline manquant et des captures correctes, mais son verdict échouait car la mesure de déplacement incluait0.179m de stabilisation verticale initiale avant l'entrée de rotation. Le test final conserve cette mesure et mesure aussi la position à partir du début de l'entrée souris (frame25). Déplacement maximal pendant ce témoin : 0.000000m, seuil0.05m. Cette correction du protocole ne change pas le correctif de production.

La fraction de pixels presque noirs dans le tiers inférieur passe de 78.87% au maximum à 0.27%. Cette statistique seule ne prouve pas le terrain : elle accompagne les captures et les observations GPU. Captures inspectées et planches disponibles localement.

Rejeu Escape/Quit final : PASS, fermeture par Interaction::Pressed sur le bouton visible. Compilation du binaire final et Clippy ciblé : PASS, aucun diagnostic dans walk.rs/terrain_proof.rs ;23 diagnostics préexistants dans d'autres fichiers, sans prétention Clippy workspace. Dépendances et sources isolées sont fingerprintées. Contrôles de format/diff et installation consignés séparément.

Pics du replay final : privé 3.619GiB ; GPU dédié 1.856GiB ; GPU partagé 0.072GiB. Garde borné aux processus de l'essai, aucun seuil minimum de RAM disponible, aucun arrêt forcé ni descendant restant. Readback WARN seulement lors de la fermeture, sans erreur runtime.

## Fichiers locaux

- `example-001/` : sources exactes de la sonde avant correctif ; inputs et binaire conservés.
- `example-003/` : binaire final et fingerprints.
- `terrain-proof-001/` et `terrain-proof-003/` : captures avant/après et reçus.
- `menu-proof-001/` : fermeture réelle Escape/Quit.
- `comparison.json`, `deployment.json`, `backups/` : mesures, installation et retour arrière007.

Le replay exerce la rotation native sur place au point de départ. Les autres emplacements/effets ne constituent pas une nouvelle qualification exhaustive. Les captures ralentissent la cadence ; aucune performance interactive générale n'est déduite. La validation visuelle humaine du binaire008 reste à recueillir. Aucun commit/push ni clôture de Gate.

Dossier local : `E:\OpenRPG_Forge\.tidewater-integration\launcher-repair-20261009-008`.
Binaire installé SHA256 : `81e97d81ac73e196d5bb3facb638892c8e9a5d73ac014f13ef5adc00141657f4`.
