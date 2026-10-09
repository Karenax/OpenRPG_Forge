# Tidewater — acquisition des accessoires de cour

Suite de la [comparaison native Village](2026-10-04-village-native-comparison.md).
Le nouveau `scripts/tidewater/export-village-props.mjs` observe les émissions
de `Village::_villageProps` à la révision source
`1438b1abfcaee3267092b75573014f4d9b4a983c`, sans modifier le clone source.
Les marqueurs sont insérés dans un module en mémoire ; les appels générateurs,
les arguments et la consommation aléatoire restent ceux de la source.

Deux acquisitions indépendantes, `village-parts-20261004-v1` et
`village-parts-20261004-v2`, produisent 422 fichiers identiques octet pour
octet. Le digest de scène `e2f4799b...805f1` reste identique au village
non instrumenté et aux extractions précédentes. Les collisions et le terrain
participent à cette comparaison. La licence MIT est conservée dans chaque lot.

| Objets logiques | Nombre |
|---|---:|
| Clôtures | 4 |
| Cordes à linge avec leurs poteaux | 3 |
| Séchoir à poissons / à filets | 1 / 1 |
| Tonneaux / caisses / casiers | 5 / 6 / 5 |
| Barque renversée sur tréteaux | 1 |
| Seau | 1 |
| Lampes de chemin | 3 |

Les trente objets ont soixante parties de matériau et 12 261 triangles dans
les batches village. Les poteaux du linge appartiennent à leur corde à linge ;
les supports de la barque appartiennent à sa composition. Les colis
conservent géométrie en coordonnées source, normales, UV, tint, données
matériau float32 et données originales float64, identités, collisions et
lumières émises à l'intérieur de chaque groupe observé.

`verify-village-props.py` vérifie les deux lots et compare chaque plage
d'indices et chaque attribut au maillage original acquis `source-017`.
Il contrôle les identités, les trente types attendus, la fermeture des
références binaires avec tailles/SHA-256, les bornes/valeurs finies et
l'absence de chevauchement entre objets ou avec les bâtiments acquis.
Le reçu `village-parts-proof-20261004-v1.json` est PASS.

Le premier lancement d'extraction s'arrête avant écriture car les marqueurs
multilignes ne prenaient pas en compte CRLF ; seule la représentation en
mémoire est normalisée, le hash des octets source reste conservé. Le premier
contrôle d'attributs détecte ensuite la transformation `rope → hard`.
Le vérificateur reproduit maintenant explicitement les folds originaux
`glass`, `rope`, `cloth`, `net`, `flag` dans la précision float64 originale
avant conversion float32 ; les contrôles restent exacts, sans tolérance ajoutée.

## Travail encore requis

Ces lots sont des acquisitions, pas des assets disponibles dans Studio.
Il reste le pliage portable des matériaux, les pivots et bounds locaux,
les lecteurs Rust contrôlés, le transport/catalogue et l'intégration par
les placements communs `ManualPlacement`/`PlacementEdit`. Les trois lampes
doivent réutiliser leur acquisition et leurs identités existantes pour éviter
un double placement. Les instances `FishProps` ajoutées au séchoir sont
distinctes des batches géométriques et restent à acquérir ; elles ne sont
pas comptées comme acquises par ce reçu. Les accessoires de plage gardent
également leur lot à réaliser. Le rendu, l'édition persistante de ce nouveau
contenu et la parité complète restent ouverts.
