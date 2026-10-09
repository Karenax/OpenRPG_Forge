# Tidewater — catalogue commun des ressources importées

## Problème et changement local

Le candidat natif `61d6854e3f2f252439ca1aea54cfb5a4dc1c08a16b5f8c2b908b1445368eacc7`
charge l'étal mais ne l'expose pas dans le catalogue commun. Le parcours
`stand-edit-runtime-v1` reste `failed_closed` avant placement ; il attend
également un bouton dédié déjà supprimé. Ses documents sources restent intacts.

`authoring_assets/catalog.rs` fournit une façade transitoire aux objets du kit
GLB et aux fournisseurs de paquets vérifiés en mémoire. L'adapter
`imported_buildings/catalog.rs` expose bâtiments, étal, passerelles, jetée,
place et accessoires de village/plage/poissons. Les références d'asset et
empreintes sont celles des paquets existants ; aucun composite n'est annoncé
comme un GLB. Les noms de carte sont des clés d'interface transitoires,
distinctes des identités persistantes.

Insertion, dépôt et remplacement utilisent l'admission commune, puis les
commandes `ManualPlacement`/`PlacementEdit` et Preview/Apply existantes.
La disparition ou le remplacement d'un paquet invalide sa carte et une demande
de placement en attente. Cette protection compile ; son comportement dans
Studio exécuté reste à prouver. La fiche conserve provenance et pin du paquet.
Le placement sur le terrain sert d'aperçu pour ces composites ; le bouton
d'aperçu GLB temporaire reste réservé aux vrais fichiers GLB.

## Preuves et candidats

- Retrait des anciennes ombres : vrai système d'extraction Bevy testé en natif,
  1 test réussi, source intacte. Cela ne prouve pas une ombre visible dans Studio.
- Catalogue V1 : Cargo 101, module Vault courant omis par l'ancienne copie de
  validation. Échec conservé ; aucune source utilisateur supprimée.
- Catalogue V2 : 1 362 fichiers figés, avec 859 fichiers Rust/manifests courants
  repris de manière cohérente ; contrôle WASM release réussi en 1 min 24 s,
  sources intactes. Manifest SHA-256
  `98a5a6d5d9c033c9487f7b0d322679c17974606d34beec0ff902071f76204d13`.
- Les 28 pins de sources du worker de navigation correspondent au snapshot V2.
  La copie de ses artefacts et son inventaire final restent contrôlés par la
  chaîne de construction, après liaison du frontend.

La construction frontend V2 est lancée dans
`.tidewater-integration/studio-common-imported-catalog-20261005-v2`.
Une chaîne native attend précisément le prédécesseur Python PID 5764, créé le
5 octobre à `00:36:08.7713170+02:00`, avant construction et test isolé. Elle
refuse un prédécesseur d'identité différente ou un résultat frontend incomplet.
Le candidat et le test ne sont pas déclarés réussis au lancement.

## Parcours de validation

Le harness conserve les pins de la fixture de 23 placements acceptés, du bundle
et des ressources, ainsi que les contrôles Preview/Apply/Save/réouverture et
passage en jeu. Il attend la carte de l'asset épinglé
`941b533a-8d35-54ce-9fff-96d07b6b4439`, puis utilise le bouton de placement commun.

Le libellé de la bibliothèque contient un compteur dynamique ; le harness lit
ce libellé avant de l'ouvrir. Le clic physique vise le span du nom dans la carte
épinglée : le texte complet du bouton inclut aussi la catégorie. Le test
confirme `aria-pressed=true` avant le placement. Ces corrections de sélecteurs
sont conservées dans `runtime-tools-v2` et leurs empreintes dans
`runtime-tools-v2.json`, indépendamment du snapshot compilé. Les versions
précédentes des outils restent conservées. Parse UTF-8 : zéro erreur ;
`-SelfTest` sur ces helpers épinglés : code 0,
`TIDEWATER_FISH_STAND_STUDIO_SMOKE_SELF_TEST_PASS`. Ce résultat couvre le
chargement des helpers, pas les clics ou la sauvegarde dans l'application.

La sortie isolée prévue est
`.placeables-smoke-common-catalog-20261005-v1/stand-edit-runtime-v1`.
Le staging vérifie EXE et chaque fichier frontend avant exécution et après
le parcours. Les fixtures utilisateur et précédentes preuves sont préservées.

## Portée encore ouverte

Les végétaux importés se sélectionnent et se retouchent par la façade commune
existante. Leur référence actuelle dérive toutefois du projet, de la zone et
de la couche (`forge_world::imported_vegetation::editable::plant_asset_id`),
avec le hash du bloc d'instances. Cela ne constitue pas encore un catalogue
de prototypes réutilisables indépendamment de la carte. Cette tranche n'ajoute
pas de fausses références de prototypes dans le catalogue : leur admission et
projection communes restent à compléter et vérifier.

Placement et édition persistante du nouvel étal, LOD/ombres de ses poissons,
portée visible des ombres végétales, comparaison temporellement appariée avec
la référence, gameplay prévu et distribution restent ouverts. Aucun verdict
de parité visuelle ni clôture de gate n'est attribué à ces compilations.

## Compléments du parcours natif préparé

Le parcours exige maintenant `RequireGraphicsPersistence` et
`RequireLiveShadows`. Il vérifie le réglage d'exposition enregistré,
restauré après rechargement puis réinitialisé, ainsi que l'avancement des
compteurs des trois cascades et du vent avant/après une marche. Il exige
la même génération résidente. Ces compteurs portent sur l'encodage des
passes ; ils ne remplacent pas une lecture GPU de profondeur ni une
comparaison des pixels reçus sur le sol. Aucun PASS anticipé.

Le reçu `runtime-scope.json` épingle le runner et conserve son prédécesseur.
Les contrôles de fixtures et de référence ne sont pas assouplis. Le contrôle
`-SelfTest` du harness `runtime-tools-v2` termine code 0 après les corrections
de sélecteurs. La liaison frontend est encore active lors de cette préparation.

## Frontend et candidat natif construits ; inventaire UI observé

Frontend V2 lié : code 0, sources intactes ; WASM brut 126 014 801 octets,
SHA-256 `24120faf840e70f1ea93fa88c847322c5316d9ddd09531c4fe48ef95d9332a03`.
Assemblage et réutilisation du worker contrôlés, 3 020 fichiers. Construction
native code 0, sources et frontend intacts : EXE SHA-256
`d150553ddee0d53c9f61cc91525a44a7bb05877339b349bba5b1c8d1a5dad136`.

Le premier parcours échoue avant insertion sur le comptage UI. Son appel
avait déjà commencé avant les derniers ajouts de switches : il n'exigeait
ni GraphicsPersistence ni LiveShadows. Son reçu original reste inchangé.
Le second parcours inclut ces switches et conserve la vérification stricte.
Ses diagnostics montrent 187 cartes de catalogue, dont l'étal avec son
identifiant exact, mais zéro objet dans le sélecteur au moment observé.
Le statut viewport est alors « Chargement du relief importé en cours ».
Aucune perte de placement n'est déduite de cette lecture pendant chargement ;
le terrain n'est pas qualifié par sa capture de grille.

Le harness suivant attend explicitement les 23 placements attendus après
ouverture du workspace, avec le délai existant de 120 s. Il corrige aussi
le nom attendu de l'insertion commune : « Échoppe de Joe », libellé exact du
catalogue, avec identité et bundle pin inchangés. Les deux anciens échecs et
leurs profils indépendants restent conservés. Save/réouverture et ombres
visibles ne sont toujours pas déclarés réussis.

## Inventaire des sources : 181 identités exactes, pas 23 options

Après le chargement, le troisième parcours observe 181 objets : les 23
placements manuels sont tous présents, plus 158 sources héritées (3
passerelles, 1 jetée, 5 accessoires de place, 27 de village, 43 de plage,
78 poissons et 1 étal). L'attente stricte de 23 options échoue ; ce résultat
reste conservé. L'absence initiale du sélecteur pendant le chargement ne
constitue pas une perte des placements.

`prepare-stand-smoke-inventory.py` produit un inventaire indépendant depuis
la fixture épinglée et les véritables descripteurs des six paquets. Il
vérifie manifestes, descripteurs, commit et cardinalités, puis applique le
contrat UUID Forge et les clés source documentées. Il ne lit pas le DOM
pour construire les valeurs attendues. Les 181 paires identifiant/libellé
concordent exactement avec l'observation UI conservée, sans aucune différence.
L'inventaire a SHA-256
`f0bcb881d00259514e42de6a6f1abebb0fa7bf165400d96bd91aaae65e3d7fb1`.
Une modification du descripteur boardwalk dans une copie minimale est
refusée avant production d'inventaire ; ressources originales inchangées.
Le reçu `inventory-verification.json` limite ce PASS aux identités et pins.

Le harness accepte cet inventaire uniquement avec son hash, la même
fixture/index et les pins des ressources. Il conserve les 23 placements
manuels attendus et exige désormais l'égalité exacte des 181 identifiants
et noms dans le sélecteur. Après insertion et réouverture, il exige ces
mêmes 181 objets plus la nouvelle copie. Les assertions sur les 24
placements manuels sauvegardés ne changent pas. Parcours v4 lancé ; aucun
Save/réouverture ou rendu d'ombres encore attribué à cette exécution.

## Parcours v4 terminé : insertion commune, réouverture et marche PASS

Le reçu `studio-common-imported-catalog-20261005-v2/rerun-v4-result.json`
termine code 0 : candidat et frontend
inchangés. Le ledger réel est
`E:/OpenRPG_Forge/.placeables-smoke-common-catalog-20261005-v1/stand-edit-runtime-v4/fish-stand-studio-smoke-ledger.json`.
Il déclare `status=passed`, `authoring_pass=true`, `reopened=true` et
`source_preserved=true`, avec qualification de gate et revue humaine à false.

L'insertion par le catalogue commun crée « Échoppe de Joe », identité
`988ac5f0-18b1-54ec-8f85-6743d95b85e3`, asset
`941b533a-8d35-54ce-9fff-96d07b6b4439`, bundle
`c739c737914261991fd9aba21c4372e7e9cafa31527f7a4139001a4db8e9b335`.
Preview et Apply ont le même request hash
`2c6384187356f4cd8540bc7518612f80eb477520792b17961b9b649baf089392`.
Le checkpoint sauvegardé contient 24 placements manuels ; le sélecteur
restaure les 181 objets d'origine plus cette copie, soit 182 identités.
L'objet immuable sauvegardé a SHA-256
`bce8bd8644a77f9818c0d05e098480bdf76a274a9ed6d4d6adea0287e89896ec`.
Les archives originales restent intactes.

Le passage joué observe une simulation prête et une marche de Z -77,00 à
-79,70 m, au sol, avec déplacement demandé/résolu concordant au tick 873.
Les options `RequireGraphicsPersistence` et `RequireLiveShadows` sont actives.
Les compteurs des trois couches passent de [12,8,5] à [40,22,12], puis de
[79,44,25] à [107,59,33]. Cela prouve l'avancement des passes encodées dans
ce parcours, pas la couverture des pixels d'ombre sur chaque receveur.
La capture `fish-stand-playtest.png` a été examinée : terrain, bâtiments,
végétation et ombres au sol sont visibles. Cette vue n'est pas appariée à la
référence Three.js en caméra et horloge ; aucune parité visuelle n'en est déduite.

Le cycle complet de transformation/duplication/verrouillage/suppression et
annuler/rétablir est lancé séparément sur une nouvelle copie du checkpoint
v4. Il couvre l'étal source, un poisson source, la jetée source et l'étal
ajouté via le catalogue. Son reçu reste indépendant de ce PASS d'insertion.

### Parcours complet : délai de communication corrigé, essai v2 actif

L'essai `common-lifecycle-runtime-v1` termine en échec fermé, avant tout
cas édité : annulation d'une requête CDP. Source, candidat et frontend
inchangés, listener dédié fermé. Le runner reste conservé avec son ledger.
Le helper commun utilisait le délai CDP de 15 secondes, malgré un budget
d'interaction de 120 secondes. Le harness d'édition borne désormais chaque
évaluation à ce budget explicite et conserve la pile d'erreur sur disque.
Son SelfTest passe ; aucune assertion sur les documents n'est supprimée.

Le runner v2 épingle les trois helpers et réutilise exactement le même
candidat. Une connexion d'observation indépendante lit 14 Preview et
14 Apply terminés, 184 options et « Retouches non sauvegardées » ; aucune
sauvegarde n'est encore attribuée à cette observation. L'essai reste actif
à cet instant. Le GPU picking des plantes n'est pas demandé dans ce parcours.

### Parcours complet v2 terminé : quatre cycles communs PASS

`common-lifecycle-v2-result.json` termine code 0, candidat, frontend et
helpers inchangés. Le ledger est
`E:/OpenRPG_Forge/.placeables-smoke-common-catalog-20261005-v1/common-lifecycle-runtime-v2/common-placeable-studio-ledger.json`.
Il déclare quatre cas édités et quatre cycles de suppression, source et
frontend préservés, listener dédié fermé. Le scénario a sélectionné les
objets par leurs options publiques, sans injection d'identité.

Pour l'étal source, le poisson source `fish-prop:0`, la jetée source
`pier:0` et l'étal manuel ajouté par le catalogue, il vérifie : déplacement
X +1,25 m, rotation relative Y 15°, échelle (1,2 ; 0,9 ; 1,1), verrouillage,
absence de mutation par clic sur les commandes verrouillées, duplication
conservant référence et transformée sans recopier le verrou, déverrouillage.
Le cas manuel vérifie aussi l'annulation de l'aperçu quand son brouillon
change. Tous les Preview/Apply ont des reçus concordants.

La première sauvegarde est relue depuis l'objet immuable épinglé
`00273091be672bbc98a378802baf54393cf81e05b46f3e47dc32888eab310a2a`.
Les huit objets (quatre originaux et quatre copies) sont retrouvés après
réouverture avec leurs transformées et références exactes.
Puis chaque original et sa dernière copie sont supprimés. Undo restaure
uniquement la dernière copie ; Redo la retire sans ressusciter la source.
Les trois sources héritées conservent leur tombstone `Removed` et zéro copie.
La seconde sauvegarde est épinglée par
`c40b7a1d38a7a657a24e8612e814e9282ee698e5c5b72717b6c850a77c0ba533`.
La dernière réouverture vérifie la conservation des autres identités et
l'absence des huit objets supprimés, sans réécriture du checkpoint.

Le PASS porte sur le parcours d'édition et sa persistance. Il ne couvre
ni le picking GPU des plantes (NOT RUN), ni la retraite des pixels d'ombre
de ces objets, ni un passage joué après suppression, ni la parité Three.js
ou les performances. Qualification de gate et revue humaine restent false.
