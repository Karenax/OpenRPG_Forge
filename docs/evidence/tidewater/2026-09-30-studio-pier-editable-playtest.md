# Quai Tidewater — édition et parcours WebGPU isolés

Le candidat Studio local `studio-tidewater-pier-candidate-093` charge le paquet
source épinglé `f3d89e2a8b92d7f99044b40500d28525a223955b28c1deffe0e9f1deeb8c5e73`.
Son exécutable a le SHA-256
`e12d220bde50c45c59ae59e909f14f093491c604f6bf6f69645bcab300b4bd4f`.
Il s'agit d'un candidat `BUILD_ONLY`, pas d'une distribution qualifiée ou publiée.

Dans une copie isolée de la zone, le parcours automatisé a rendu le quai source
éditable par `Preview` puis `Appliquer`, au repère source `(55, 0, −64)` m.
Le placement, son identifiant et son hash de paquet ont été retrouvés dans
l'archive sauvegardée puis après fermeture/réouverture de la zone. La zone
source et son archive d'objets d'origine ont gardé leurs SHA-256. Le Studio
projette 23 bâtiments, 3 passerelles et 1 quai avec surfaces Rapier
synchronisées. [Capture en édition](../../../../.tidewater-integration/studio-tidewater-pier-smoke-093/pier-authored.png).

Le [parcours WebGPU](../../../../.tidewater-integration/studio-tidewater-pier-smoke-093/boardwalk-studio-smoke-ledger.json)
part temporairement de `(55, −66)` m, **sans saut**, puis avance
sur son platelage. Le joueur atteint d'abord `(55,00, 3,21, −44,51)` m,
puis `(54,94, 3,21, +32,15)` m près de la tête côté mer. Il reste `au sol true`
avec 1 contact Rapier et une translation résolue de `0,0833` m sur
le dernier tick. Son pied est à environ `2,31` m, sur le platelage source
à `2,30` m. Les [captures sur le quai](../../../../.tidewater-integration/studio-tidewater-pier-smoke-093/pier-traversal.png)
et [près de sa tête](../../../../.tidewater-integration/studio-tidewater-pier-smoke-093/pier-head.png)
montrent le bois, les garde-corps, l'arche, la mer et le village depuis la
caméra joueur. Le contrôle ne franchit pas l'extrémité du quai à `Z = +40` m.

Deux essais précédents sans saut se sont bloqués sur les marches d'entrée :
`studio-tidewater-pier-smoke-087` avec le profil d'origine vers
`Z = −64,64` m, puis `studio-tidewater-pier-smoke-089` avec une capsule plus
étroite vers `Z = −65,23` m. La seconde tentative de profil a été retirée du
code. Le candidat 093 garde le profil d'origine et ajoute, dans l'adapter
Bevy/Rapier du quai, une surface de marche invisible au-dessus des trois marches.
Les 207 solides et 8 capteurs source restent intacts ; cet appui dérivé est
un 208e solide, enfant du placement éditable et inclus dans son contrôle de
synchronisation. La géométrie visible du paquet reste inchangée. Le test moteur
ciblé vérifie les comptes et le rattachement de l'appui au placement déplacé.
L'animation physique de l'enseigne et des lanternes, les
accessoires finaux, une comparaison à caméra et éclairage appariés avec la
source, et la parité visuelle de la carte complète restent à qualifier.

Validation locale : test ciblé `pinned_pier_prepares_source_and_authored_collisions`
PASS, build WebGPU release du candidat 093 PASS, `cargo check` WASM du code
courant PASS, parcours Studio 093 PASS, formatage Rust et `git diff --check`
PASS. `cargo clippy -p forge_engine --lib` termine avec avertissements ; son
mode strict `-D warnings` échoue auparavant dans `forge_assets` et
`forge_project` sur des avertissements hors de ce lot. La CI distante et la
revue visuelle comparative ne sont pas qualifiées par ce contrôle local.
