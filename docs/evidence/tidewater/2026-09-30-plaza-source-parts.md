# Tidewater — extraction vérifiée des objets de la place

Les scripts `scripts/tidewater/export-plaza-props.mjs` et
`scripts/tidewater/verify-plaza-props.py` prélèvent séparément, à partir de
`Village._plaza` dans la révision source épinglée, les deux bancs, le
lampadaire et les deux tonneaux. Le luminaire conserve aussi sa lumière source.
La source suivie reste inchangée ; l'unique fichier non suivi déjà présent dans
le clone de référence est conservé.

Les sorties indépendantes `E:/OpenRPG_Forge/.tidewater-integration/plaza-parts-003/`
et `plaza-parts-004/` contiennent **64 fichiers identiques octet pour octet**.
Le [rapport de vérification](../../../../.tidewater-integration/plaza-parts-proof-002.json)
confirme 1 207 triangles répartis en cinq objets et neuf parties de matériau,
avec une collision par objet. Les plages d'indices correspondent exactement au
maillage village acquis `source-017` et ne chevauchent aucune plage attribuée
aux 23 bâtiments, aux trois passerelles ou au quai. Le digest de la scène
instrumentée est identique à celui de l'extraction des passerelles,
`e2f4799bb6f425afb4b9b800fb547528a037da15ad95f8c9d2182889c1e805f1` ;
le terrain et les collisions sont inchangés par l'observation.

`scripts/tidewater/package-plaza-props.py` replie les trois matières sur les
matériaux village vérifiés, puis crée des pivots locaux avec une erreur maximale
de retour aux coordonnées source de `2,39 × 10⁻⁸ m`. Il vérifie les 1 207
triangles, attributs inclus, contre le maillage acquis. Les paquets
`plaza-package-001/` et `plaza-package-002/` sont identiques (60 fichiers,
dont 59 charges utiles et le manifeste). Le SHA-256 du manifeste est
`c0b3ce259e9de735204e5ea03e2cce5b6bdf0a332c3ffaca9fab59052712a23a` ;
la licence MIT source et l'empreinte de la bibliothèque de matériaux village
sont conservées.

Le lecteur Rust `forge_assets::tidewater_plaza::PlazaPackage` contrôle
l'empreinte du manifeste, chaque taille/SHA-256, la fermeture des références,
les cinq identités source, neuf parties de matériau, collisions et lumière.
Le test ciblé `cargo test -p forge_assets
pinned_plaza_package_rejects_changed_payload --offline` sur le paquet réel
passe (1 test), y compris le refus d'un tampon ou d'un manifeste altéré.
`cargo check -p forge_assets --target wasm32-unknown-unknown --offline` passe.
Il reste à raccorder le transport, le catalogue, les placements persistants,
les collisions et la lumière dans Studio, puis vérifier une capture et un
parcours jouable. Les autres accessoires de plage et du village restent à
extraire. Aucune capture de ces cinq objets dans le moteur Forge n'est
revendiquée à ce stade.
