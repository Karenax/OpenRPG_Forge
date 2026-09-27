# ADR-0014 — Retouches persistantes et régénération locale

Date : 12 septembre 2026  
Statut : **accepté pour implémentation par le propriétaire le 13 septembre 2026 ; contrat R2-044 consolidé localement le 22 septembre 2026 ; qualification technique globale non acquise**  
Portée normative : Gate 2, R2-044 à R2-051, branche Rust  
Complète ADR-0001 et ADR-0008 ; s'articule avec l'autorité workspace d'ADR-0016 ;
ne modifie pas la clôture de Gate 1.

Accord explicite « j'autorise » en réponse à la demande conjointe ADR-0014/0015.
Ne vaut ni passage de Gate 2 ni autorisation de publication Git.

## Décision consolidée — 22 septembre 2026

`R2-044` est une exigence documentaire d'architecture. Son contrat normatif est
porté par les sections « Autorité et couches », « Identités et verrous » et
« Régénération transactionnelle ». Il ne dépend pas de l'achèvement des preuves
produit distinctes R2-045 à R2-052.

### Autorité persistante et sources

Pour une zone autonome, `ZoneAuthoringHead` est l'unique tête métier mutable.
`ZoneBase` conserve les sorties acceptées de la recette et leur empreinte de
générateur ; `ZoneCorrections` conserve les décisions authorées. La vue effective,
les aperçus, documents projetés, entités ECS, meshes, collisions, navmesh, caches
GPU et snapshots de playtest sont dérivés et ne deviennent jamais une autre tête.
WorldSketch reste une source de propositions.

La persistance autonome remplace atomiquement le même `authoring.json` via
`DocumentStore`. Un éventuel `authoring.pending` est uniquement un candidat de
récupération : sa présence bloque l'ouverture ou la migration et ne le promeut
jamais implicitement.

Pour une zone gérée, ADR-0016 rend la tête workspace seule autorité mutable de
persistance. Elle référence par hash des archives de zone immuables ; l'ancienne
archive autonome reste lisible mais devient non éditable après adoption. Cette
hiérarchie explicite tête workspace → objet immutable → tête métier n'est pas
une seconde source canonique cachée ou concurrente.

### Corrections, identités et verrous

Les overrides sont des champs typés de `ZoneCorrections` : terrain, densité,
exclusions, placements, tracés, rivières, bâtiments, départ, espèces et promotions.
Ils ciblent des IDs persistants et portent des valeurs métriques absolues lorsque
le domaine l'exige. Aucun chemin JSON libre, index de parcours, objet Bevy ou nom
affiché ne sert d'identité.

Chaque verrou expose sa portée réelle : couche de densité, placement entier,
tracé, rivière ou bâtiment entier lorsque le modèle ne possède pas de verrou plus
fin. Déverrouiller autorise une future commande mais ne supprime jamais l'override.
Une suppression persistante ou une origine promue utilise une exclusion typée ;
elle n'est pas déduite de l'absence d'un cache.

### Régénération et conflits

`RegenerationReview` lie toute proposition au hash et à la révision exacts de la
tête, transporte l'ancienne base, la proposition et une copie exacte des
corrections, puis calcule une tête effective uniquement sans conflit. Les ruptures
d'identité ou de grille, références corrigées disparues, changements incompatibles
de relief ou de bâtiment et document effectif invalide sont des refus explicites.
La session revalide la proposition avant Apply ; un plan périmé, un retry ou une
commande brute conflictuelle ne publie aucune mutation partielle.

La matérialisation courante de ce contrat est décrite dans
`docs/evidence/gate2/R2_ZONE_HEAD.md`,
`docs/evidence/gate2/R2_ZONE_REGENERATION.md`,
`docs/evidence/gate2/R2_BUILDING_LAYOUT.md` et
`docs/evidence/gate2/R2_WORKSPACE_FOUNDATION.md`.

### Lecture des extensions datées

Les sections datées conservées dans ce document sont le journal historique des
incréments. Leurs mentions « encore à raccorder », « reste à implémenter » ou
« playtest refusé » décrivent uniquement l'état au jour indiqué. Elles ne
remplacent ni la décision consolidée ci-dessus ni l'état courant de la roadmap
et des dossiers de preuve.

## Contexte initial à la date de la décision

Le Studio commençait à éditer `TerrainDocument` au travers de commandes révisées,
avec archive et Undo/Redo. `WorldSketchDocument.locked_feature_ids` valide des
identifiants de features ; cela ne prouve pas la fusion d'une nouvelle génération
avec une porte déplacée ou une clairière peinte. Un journal Undo borné ne peut
pas être l'unique mécanisme de conservation durable des retouches.

## Proposition de contrat

### Origines promues — première représentation du 15 septembre 2026

Extension de commande : `ReconcilePromotions` porte la nouvelle base et la liste
complète des correspondances explicitement choisies. L'ensemble des identités
de placements promus doit rester identique ; aucun placement, verrou ou autre
correctif n'est modifiable par cette commande. Recette et correspondances ont
un reçu et un inverse communs. L'adapter Monde valide l'existence des nouvelles
origines, sans rapprochement automatique par distance. Les contraintes de
régénération existantes restent applicables (notamment conservation des couches).
Archives contenant cette action : intégrale 25, compacte 26, delta 27 ; tête v8
inchangée et anciens encodages inchangés en l'absence de cette action.
Cette extension ne constitue pas la preuve de son accessibilité dans le Studio.

`ZoneCorrections.promotions` relie couche/ID d'instance générée, hash de génération
au moment de la promotion et ID du placement auteur. Ce journal d'origines reste
présent après suppression du placement pour empêcher sa réapparition future.
Il n'est pas une copie de mesh ni une nouvelle autorité de carte. Première portée :
arbres, 4096 origines maximum, clés uniques triées et placements distincts.
Une couche source supprimée rend la tête invalide ; la résolution des instances
et des pins réels relève de l'adapter Monde, encore à raccorder.

Champ omis : encodages historiques conservés. Champ présent : tête v8, archives
intégrale v22, compacte v23 et delta v24, y compris après Undo. La commande
prépare origine et placement ensemble ; elle ne prétend pas vérifier seule
l'existence de l'instance générée ou ses octets d'asset. Filtrage du scatter,
conflits de nouvelle génération et interface restent à implémenter.

### Espèces authorées — extension du 15 septembre 2026

`ZoneCorrections.species` conserve les choix par couche arborée dans la tête
unique : hash du catalogue source immuable, couche source et asset sélectionné.
Le catalogue épinglé porte déjà normalisation et provenance ; pas de hash figé
de végétation effective ni de nouveau catalogue mutable. Le domaine vérifie
IDs, quotas, doublons et présence des couches ; la résolution réelle des octets
du catalogue/GLB demeure une responsabilité des adapters, pas encore raccordée.

Champ absent : anciens hashes inchangés. Présent : tête v7 et archives intégrale
v19, compacte v20, delta v21 dès qu'un reçu contient ce domaine, même après Undo.
Edit/Preview/Apply, inverse, audit et codecs existants sont réutilisés. Regenerate
conserve ces retouches et refuse une couche arborée disparue. `None` à la commande
retire le choix d'une couche ; un tableau explicite vide conserve la version v7.
Cette extension ne qualifie ni palette Studio ni rendu des espèces choisies.

### Point de départ authoré — extension du 14 septembre 2026

Pour R2-004, `ZoneCorrections.start` porte un `ZoneStart` moteur-neutre : X/Z
en mètres et cap optionnel en degrés, projetés sur le terrain lors du lancement.
Absent conserve les hashes historiques ; présent exige tête v6. Le point passe
par Edit/Preview/Apply, inverse et audit, et reste conservé par Regenerate.
La validation refuse les coordonnées non finies et la bande extérieure de 1 m ;
elle ne promet pas l'absence d'obstacle ou un départ sur un étage.

Archives intégrale v16, compacte v17 et delta v18 dès qu'un reçu contient un
départ avant ou après, même après son retrait/Undo. Lecteurs historiques conservés,
refus de réétiquetage vers une ancienne version. Le delta distingue Keep de
Set(None) ; aucun fichier séparé ni migration automatique. L'allocation du petit
point optionnel évite de grossir toutes les variantes de commandes.

« Jouer » utilise le point authoré s'il existe. StartPlaytestAt le remplace
uniquement dans le snapshot de simulation : ni tête ni archive modifiées.
L'ancien type PlaytestStart devient un alias du type neutre, sans changer son
encodage. Le départ n'est pas une sauvegarde de partie Gate 3. Qualification
transversale et parcours graphique encore requis ; aucune gate clôturée ici.

### Autorité et couches

Une zone possède une seule tête canonique, versionnée et adressée par hash.
Elle référence la recette génératrice, la base générée acceptée et les corrections
authorées. La base est liée à la version du générateur, aux entrées et à la seed.
Elle n'est jamais une deuxième tête modifiable indépendamment. WorldSketch reste
une source de propositions au sens d'ADR-0008, pas une voie d'écriture concurrente.

La vue effective est calculée depuis cette base et les corrections. Les meshes,
instances, collisions, navmesh et caches GPU restent entièrement dérivés.
Fermer ou compacter l'historique Undo conserve la tête, les corrections, leurs
provenances et les verrous. Le format actuel d'archive terrain n'est pas déclaré
compatible par simple changement d'étiquette : une migration explicite sera requise.

Les corrections sont typées par domaine, jamais des chemins JSON arbitraires :
transformations métriques, points de spline, échantillons de hauteur, masques de
matériaux/densité, présence ou suppression d'un placement. Une transformation
absolue conserve exactement la valeur authorée ; elle ne devient pas implicitement
un décalage relatif lors d'une régénération. Le mode relatif, s'il est proposé,
est un choix distinct visible dans l'inspecteur.

### Identités et verrous

Chaque feature authorée possède un ID persistant indépendant de son nom et de sa
position dans une liste. Les enfants générés utilisent l'ID parent et une clé
sémantique stable propre au générateur, pas un indice de parcours. Un générateur
incapable de maintenir cette correspondance doit déclarer une rupture d'identité,
pas réattribuer silencieusement les corrections au voisin le plus proche.

Les objets manuels possèdent leurs propres IDs ; les suppressions authorées sont
conservées comme exclusions explicites pour empêcher leur réapparition. Les verrous
portent sur les propriétés visées. Ils n'interdisent pas toute évolution de la zone,
mais toute incompatibilité spatiale ou référence orpheline doit être diagnostiquée.

### Régénération transactionnelle

1. Préparer un plan sur la révision et le hash exacts de la tête courante.
2. Générer une proposition hors des sources actives, avec empreintes d'entrées.
3. Comparer ancienne base, proposition et corrections, par ID/propriété.
4. Conserver les corrections existantes, même non verrouillées. Présenter toute
   suppression de leur cible, rupture d'identité ou contrainte incompatible.
5. Si un conflit subsiste, refuser l'application sans mutation. Le plan expose
   ancienne valeur, valeur proposée, valeur effective, origine et verrou.
6. Une résolution est une commande explicite sur ce plan : conserver, modifier
   la proposition ou retirer une correction. Déverrouiller ne signifie pas effacer.
7. Revalider les dépendances et la révision avant publication atomique de la tête.
   Produire inverse, audit et nouvelle révision ; un retry ne réapplique rien.
8. Reconstruire seulement les caches dépendants des propriétés modifiées. Un reçu
   tardif portant un autre hash ne peut rendre la vue ou le playtest « prêt ».

Le même protocole s'applique aux gestes Studio et aux commandes programmatiques.
Le playtest utilise un snapshot effectif identifié ; il ne réécrit pas les sources.
Une reconstruction échouée conserve les sources et indique une projection périmée.

## Preuves requises avant acceptation technique

- Déplacer/verrouiller une porte et peindre une clairière ; régénérer localement,
  fermer l'historique, sauvegarder et rouvrir : corrections identiques.
- Ajouter et réordonner une feature : IDs et références non affectés identiques.
- Supprimer une cible corrigée : conflit explicite, aucune perte de données.
- Proposer une valeur incompatible avec un verrou : aucune mutation silencieuse.
- Geste et commande équivalents : mêmes valeurs, validation, inverse et retry.
- Ancien plan, résultat hors ordre, cache absent ou échec de publication : aucun
  faux acquittement, aucune tête partiellement enregistrée.
- Parcours Studio sans JSON manuel, capture et mesures d'effort/attente auteur.

## Limites de qualification restantes

Les schémas concrets, bornes de taille, migration explicite et audit acteur/temps
sont désormais matérialisés dans le domaine et ses archives. Leur couverture
produit, l'affichage de toutes les provenances, les résolutions graphiques de
conflits et la granularité des caches restent suivis séparément par R2-037 et
R2-046 à R2-052.

Aucun stockage multi-fichier naïf n'est accepté : l'atomicité porte sur la tête
mutable et ses références vérifiées. Aucun nouveau moteur ou writer concurrent
n'est autorisé.

Cette décision permet de cocher localement le seul critère documentaire R2-044.
Elle ne coche aucun autre critère, ne constitue ni un PASS ADR-0006, ni une
qualification humaine, ni une clôture de Gate 2, et n'autorise aucune publication.

## Tracés authorés — représentation du 13 septembre

### Recettes de rivière — extension du 13 septembre

`ZoneCorrections.rivers` conserve les recettes d'eau déjà typées (`WaterDocument`),
leur EntityId, budget de coupe et verrou, au plus 16 recettes triées par ID.
Absent conserve les hashes historiques ; présence, même vide, exige tête v4.
Le journal utilise archive intégrale v10, compacte v11 ou delta v12 et conserve
cette exigence après Undo. Les lecteurs historiques restent disponibles sans
écriture implicite ; un réétiquetage vers une ancienne version est refusé.
Insert/Replace/SetLocked/Remove passent par ZoneRequest, verrou revalidé sur Edit
brut et recettes conservées à Regenerate. La géométrie creusée ne doit pas être
réappliquée cumulativement à sa propre sortie : raccord des caches dérivés encore
à réaliser. Le playtest refuse explicitement une tête avec rivière tant que ce
raccord n'est pas fait. Ce modèle ne constitue pas une qualification R2-023.

Les chemins manuels résident dans `ZoneCorrections.paths`, non dans un fichier
WorldSketch concurrent. ID stable, nom, points X/Z métriques, largeur, accotement
et verrou sont persistants ; les futures courbes/meshes restent dérivés.
Ce premier profil de chemin n'est pas un profil hydraulique de rivière.
WorldSketch demeure une source de propositions : son préparateur de retouches
ne vaut pas acceptation dans une zone.

`paths: None`, omis, préserve les hashes historiques ; Some, même vide, exige
une tête v3. L'archive est v8 intégrale, v9 compacte ou v7 delta dès qu'un reçu
contient ce domaine, y compris après Undo. L'encodage delta v6 est conservé pour
les anciens journaux ; l'ajout du champ ne réécrit aucune archive à l'ouverture.
Les anciennes versions ne doivent pas accepter un journal avec des chemins
par simple changement d'étiquette. Limites : 64 tracés et 128 points par tracé,
toujours 4 Mio par archive et 64 Mio de requêtes dépliées.

Insert/Replace/Remove/SetLocked préparent le même ZoneRequest ; verrou revalidé
dans la session pour les commandes brutes aussi. Regenerate conserve les chemins
et revalide leurs bornes sur le terrain proposé. Pas de projection encore :
playtest refusé explicitement lorsque des chemins sont présents. Cette limite
doit disparaître avec le raccord des caches identifiés, pas avec une omission
silencieuse des tracés. Aucun passage de Gate 2 acquis.

## Première représentation concrète des placements manuels — 13 septembre 2026

Implémentation partielle de l'ADR accepté : placements manuels dans les corrections
de la même tête, avec EntityId, AssetId/hash et Transform moteur-neutre existant.
Le verrou vise pour cette première étape l'ensemble du placement ; les verrous
plus fins et les overrides de features générées restent à traiter.

`placements: None` est omis et conserve les hashes historiques. La première commande
d'insertion explicite prépare la tête v2 ; un domaine présent mais vide reste v2.
Undo peut rétablir v1. Le journal de ces opérations exige archive intégrale v4 ou
compacte v5, sans oublier cette exigence après Undo. Les anciennes archives v1–v3
restent acceptées à l'identique ; ouverture sans migration ni écriture implicite.

Les commandes, inverses, retry et Save existants sont réutilisés. Aucun second
fichier canonique, aucune dépendance ajoutée. Référence d'asset non assimilée à une
approbation de licence. Projection et édition Studio encore dues : le playtest
refuse une tête contenant des placements non projetés. Preuves et limites dans
`docs/evidence/gate2/R2_MANUAL_PLACEMENTS.md` ; aucune qualification de Gate 2.

### Encodage du journal v6 — correction du 13 septembre

Le replay réel a atteint 4 Mio après 13 commandes : v5 répète toutes les corrections
dans chaque Edit. V6 encode les six champs typés des corrections en Keep/Set depuis
le précédent **Edit enregistré**, initialisé aux corrections vides. Undo/Redo et
Regenerate ne changent pas cette base d'encodage ; la relecture reconstruit les
requêtes originales avant les validations métier et les hashes des reçus complets.
Set(None) reste distinct de Keep ; comparaison canonique des champs, pas égalité
flottante approximative. Aucun changement des documents, hashes ou règles d'édition.

Conversion explicite par Examiner/Convertir puis Save, sans effacer le journal.
Les lecteurs v1–v5 et leur sens sont conservés ; aucune conversion à l'ouverture.
La limite fichier reste 4 Mio et celle des commandes 1 024. Requêtes dépliées bornées
à 64 Mio à l'encodage comme à la relecture, reçus dérivés non comptés dans cette
borne ; coût mémoire/temps complet à qualifier avant clôture. Aucun nouveau moteur,
stockage canonique parallèle ou dépendance. Preuve de reprise réelle en mémoire,
conversion graphique encore due : cela ne vaut pas passage de Gate 2.

Raccord au kit gouverné : AssetId déterministe à partir du couple kit/identifiant
local, encodé par longueurs UTF-8 sous `governed-kit/v1:`. Le hash épingle séparément
le contenu ; changement de chemin ou de révision ne change pas l'identité, changement
de contenu exige une décision explicite. Pas de substitution par nom ressemblant,
de résolution réseau implicite, ni de nouvelle source canonique d'assets.
