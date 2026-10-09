# Réfraction des nouveaux matériaux de débris — 6 octobre 2026

Statut : raccord préparé, shaders acquis validés CPU ; intégration et rendu GPU
encore non qualifiés. Aucune publication ni clôture de gate.

## Contrat de référence

`RefractionPass.js` du commit figé 1438b1ab exclut les matériaux dont
`underwaterLighting` vaut `none`. Nature, PebbleField et ScannedDebris déclarent
`lite` : ils doivent contribuer à la capture. Les objets ordinaires sont filtrés
par leur boîte monde sous mer + 0,4 m ; les instances passent ce filtre et leurs
fragments sont découpés à cette même hauteur. La capture conserve l'éclairage
des objets submergés et utilise un rendu HDR à demi-résolution.

## Préparation Rust

`.tidewater-integration/debris-refraction-20261006-v1/staged` contient le raccord
des trois familles à la passe existante. Chaque matériau conserve son extension
de textures/buffers et son shader vertex. Une extension supplémentaire fournit
uniquement l'uniform de découpe en binding 200 et le shader fragment capturé.
Les miroirs copient les transformations monde et le `MeshTag` source, retirent
le tag s'il disparaît, suivent les changements de matériaux et retirent les
objets masqués/supprimés. Les instances ne sont pas filtrées par une boîte locale
qui ignorerait leurs positions générées depuis leurs buffers.

Trois compteurs Nature/Pebble/Scanned sont préparés dans l'observation existante.
Le manifeste `preparation.json` compare les deux originaux Engine au manifeste
du build natif actif. Aucun de ces originaux n'est modifié pendant sa construction.

## Preuves CPU obtenues

- `pebble-capture-shader-v1`, processus 13789 : compilation 0, tests 0,
  trois tests exécutés, zéro filtré, 0,83 s. Le programme acquis avec ses
  receveurs partagés est validé dans quatre variantes d'imports Bevy.
- `nature-scanned-capture-v1` : compilation 0, tests 0, trois tests exécutés,
  zéro filtré, 1,09 s. Deux programmes acquis et leurs adaptations de matériau
  courantes sont validés dans huit variantes d'imports Bevy.

Dans chaque cas, la suppression du seul ajout de découpe et de sa déclaration
reconstitue exactement le programme éclairé. Les essais conservent également
les tests existants de refus d'une entrée absente/dupliquée ou déjà dérivée et
de fallback vers la couleur opaque. Les variantes combinent éclairage partagé
et stockage/uniform des modèles mesh. Le contrôle Nature/Scanned vérifie aussi
le binding uniforme 200 dans le module Naga composé. Inputs inchangés.

Le contrôle `material-check-v1`, processus 95873, est lancé séparément : vrais
modules courants Nature/Pebble/Scanned et nouveau module de miroirs, avec le type
VillageMaterial d'un Engine précompilé et son Bevy exact retrouvé par fingerprint.
Il doit vérifier les types des matériaux imbriqués et des opérations ECS ; aucun
résultat terminal ne lui est encore attribué.

## Limites

Les tests de shaders utilisent le cache Bevy/Naga CPU, pas une création de
pipeline sur le GPU réel. Le contrôle de matériaux réutilise un type de base
précompilé : il ne remplace pas le check du crate Engine courant. Le raccord
reste préparé, sans adoption native ou Studio. Reste à tester la copie de tags,
les changements/retraits de matériaux et de sources, compiler le crate et ses
adapters, puis lire les captures GPU des trois familles et effectuer des
comparaisons fixes/mobiles/sous l'eau avec la référence. La carte entièrement
éditable et la parité visuelle globale ne sont pas acquises par ces contrôles.

## Contrôle de types : échec du harnais V1, reprise V2

95873 termine avec code 1, inputs inchangés. Le diagnostic E0463 ne permet pas
de résoudre le crate `forge_engine` précompilé ; aucun type du nouveau raccord
n'est donc qualifié par cet essai. V1 et son diagnostic restent conservés.

`CheckCaptureMaterialsV2.py` retire cette dépendance au crate Engine historique.
Il extrait les vraies définitions courantes de VillageMaterial, leurs attributs
de bindings, les types uniformes associés et l'implémentation Material de
spécialisation. Il conserve les vraies définitions d'extensions Nature/Pebble/
Scanned et le module de miroirs préparé. Bevy reste la bibliothèque précompilée
exacte utilisée par les tests de shaders. Le check de métadonnées V2 est actif
sous 3156, sans résultat terminal encore acquis. Les fonctions de chargement et
le crate complet restent hors de sa portée.

`lifecycle-tests.rs` et `VerifyCaptureLifecycle.py` préparent trois tests du
véritable algorithme générique de miroirs avec des fixtures StandardMaterial :
suivi des poses/tags/matériaux sans mutation de la source, retrait des sources
masquées/supprimées et différence du filtre de boîte entre objets ordinaires et
instances. Le script exige d'abord un résultat V2 PASS et ses hashes inchangés.
Ces tests ne sont pas encore exécutés ; leurs fixtures CPU ne remplacent pas
les programmes acquis ni la validation GPU des matériaux.

V2 produit ensuite deux diagnostics E0425 : `MeshTag` n'est pas exporté par le
prelude Bevy utilisé. Le handle 3156 reste encore actif lors de cette observation ;
aucun code de sortie n'est inventé. Pour préserver ses pins, la correction est
faite dans une nouvelle copie `staged-v2` : import explicite `bevy::mesh::MeshTag`.
`preparation-v2.json` épingle cette copie et vérifie à nouveau les originaux.
Le check de types V3 est lancé sous 58133 sur cette copie, avec les définitions
courantes et sans Engine précompilé. Le verifier de lifecycle exige désormais
ce V3 PASS avant exécution ; les tests ne sont toujours pas attribués comme faits.

58133 termine avec code 0, inputs inchangés. V3 vérifie les vrais bindings et
types extraits du matériau Village courant, sa spécialisation Material, les
définitions des trois extensions et le module corrigé de miroirs. Il compile
en métadonnées les types `ExtendedMaterial` imbriqués et leurs opérations ECS.
Cela ne compile toujours pas les loaders, le crate complet ou un pipeline GPU.

Le verifier de lifecycle satisfait désormais ce prérequis et lance sous 70620
les trois fixtures de suivi/retrait, dans un nouvel output `lifecycle-v1`.
Il reprend le module corrigé sans modification fonctionnelle, à côté des tests
CPU ; la délimitation du commentaire de module est seulement adaptée pour son
inclusion dans le harnais. Aucun succès d'exécution de ces tests n'est encore
attribué. Les fonctions réelles de shaders gardent leurs preuves indépendantes.

70620 termine : compilation 0, tests 101, inputs inchangés. Les deux tests de
shaders passent, mais les trois fixtures de miroirs échouent sans miroir créé.
Leurs objets insèrent `Visibility::Visible`, dont le composant requis
`InheritedVisibility` reste initialement masqué dans un World sans propagation
de visibilité. Le filtre réel les exclut donc avant toute création : cet essai
ne démontre pas un défaut du filtre de production et reste conservé.

La préparation de fixtures V2 initialise explicitement
`InheritedVisibility::VISIBLE`, avec assertion de cette visibilité effective.
Le filtre de production est conservé. Sous 87684, compilation 0, tests 0,
inputs inchangés : cinq tests effectivement exécutés, zéro filtré, dont trois
nouveaux cas de suivi/retrait et les deux cas de shaders existants. Le suivi
préserve les identités de miroir et de matériau, actualise pose/tag/couleur,
retire un tag disparu sans muter le matériau auteur, retire les matériaux
dérivés et entités des sources masquées/supprimées et applique la différence
de filtre de boîte pour les instances. Ces assertions restent CPU, avec
StandardMaterial comme fixture du même algorithme générique.

3156 termine aussi à code 1, inputs inchangés, avec les diagnostics E0425
précédemment observés. Sa copie d'entrée n'est pas remplacée par la correction.
Le V3 PASS reste la preuve de types de la copie corrigée `staged-v2`.

## Intégration préparée et protégée

`AdoptPreparedRender20261006.py` prépare l'adoption commune du raccord de capture
et de la famille temporelle Scanned. Son mode par défaut effectue seulement un
aperçu : sept fichiers, cinq existants et deux nouveaux. Le preview réel termine
à code 0, `builds_released:false`, `production_modified:false`. Les hashes des
sources, des fichiers préparés et des inputs des cinq validations PASS sont
vérifiés, pas seulement les statuts des reçus.

L'application exige les deux résultats terminaux PASS du build natif Pebble et
du check Studio web, leurs pins de sources encore intacts et la branche Rust.
Les cinq originaux sont sauvegardés avant remplacement ; un chevauchement
inattendu est refusé. Le nouveau module de capture reçoit une attribution MIT
Tidewater, sans modifier son corps fonctionnel validé. Toute adoption reste
`APPLIED_PENDING_FULL_VALIDATION` : le check du crate, les adapters et les essais
GPU sont requis ensuite. L'aperçu n'applique pas ces changements et n'autorise
aucune publication implicite.
