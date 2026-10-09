# Tidewater — conversion portable des accessoires de plage

Suite de l'[acquisition source](2026-10-04-beach-prop-source-acquisition.md).
`package-beach-props.py` convertit les objets vérifiés en frames locales avec
matériaux source, index, normales, UV, teintes, paramètres shader et collisions.
La licence MIT et les pins de source sont conservés.

## Résultat vérifié

Deux sorties neuves, `beach-package-20261004-v1` et `beach-package-20261004-v2`,
sont identiques : 43 prototypes, 78 parties matériau, 18 807 triangles,
424 payloads et un manifeste, soit 425 fichiers. SHA-256 du manifeste :
`0a08054a08b8da7309f00c278860cd43851341c008b8a4208f72c0b820838808`.

La bibliothèque de matériaux partagée reste celle des bâtiments :
`390950ef51469c5e906e4833272d7968127b1bb998629013d8b85ae7b8caebce`.
Les triangles avec tous leurs attributs sont consommés exactement depuis
les buffers de source-017 ; aucun matériau générique n'est substitué.

Le pivot est le centre XZ des bounds de repos, avec le minimum Y et des axes
identité. Les orientations source sont déjà portées par la géométrie ;
elles ne sont pas appliquées une seconde fois. La chaîne de cinq poteaux
et ses cordages reste une composition logique. Les rames et bouées libres
restent indépendantes.

`verify-beach-package.py` rouvre les deux paquets, contrôle toutes les références,
tailles et empreintes, bounds, indices, matériaux et conservation des normales.
Il reconstruit géométrie et collisions dans le repère source. L'erreur maximale
de retour des positions est `1.1920928955078125e-7 m` (environ 0,00012 mm).
Les critères de conversion et du vérificateur sont conservés sans élargissement.
Le contrôle des collisions reconstruites utilise le même critère float64 que
les autres paquets. Reçu `beach-package-proof-20261004-v1.json`, statut `passed`.

## Portée restante

Ce paquet n'est pas un GLB ni un asset déjà disponible dans le navigateur
Studio. Lecteur Rust, consommateur Bevy/Rapier, transport, templates et
placements communs, catalogue réutilisable, rendu et édition persistante
restent à raccorder. Il n'entre pas dans le candidat village actuellement
en compilation. Les instances FishProps demeurent un contenu distinct.
Aucune parité, performance, qualification runtime ou clôture de gate n'est
acquise par cette conversion.
