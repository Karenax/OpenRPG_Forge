# R2-023 — rivière et creusement

## Candidat A64D5ABA — écran noir corrigé et scène combinée réouverte

14 septembre 2026, exécutable local `river-forward-studio-20260914-0830` sous
`E:/OpenRPG_Forge/.gate2-evidence/`, SHA-256
`A64D5ABA9666390E1397697F8A0ABA4C0D9C1F7BAF288FC6E22C6A45D9710DFE`.
Trunk code 0 (Rust 1 min 11 s), Tauri code 0 (15,24 s). Racine isolée
`river-forward-ui-20260914-0830`, copie exacte de C58F62BC, sans modification
de données pour contourner le défaut. Même archive désormais rendue : terrain,
forêt, maison, eau et berges visibles ; capture inspectée dans la conversation.
Le correctif Forward des berges résout donc le cas d'écran noir reproduit.
Cela ne qualifie pas SSR, réfraction ou tous les profils graphiques du Studio.

Dans ce même candidat, chemin ajouté par un geste dans la vue, Examiner puis
Apply : deux points (-6,3684387 ; 11,61441) → (13,870575 ; 92,470436), largeur
3 m et accotement 1 m. ID `e7a8bec0-82a9-59d9-b938-cf650d2d9d05`.
Surface du chemin, rivière, forêt et maison visibles ensemble. Save, entrée
réelle en playtest (capsule et scène rendues), retour à l'édition puis
Close/Open : vue d'ensemble restaurée. Aucun déplacement clavier, franchissement
de porte, navigation aquatique ou mesure de performance déduit de ce parcours.

Archive finale : 31191 octets, quatre reçus, tête révision 5, hash
`ede1e7ebf06e7bcf9d16b27cc63d6436d756ce82b699959c9fa946812a653ae4`.
SHA-256 après Save et après playtest/réouverture identique :
`825ADA4AAE6B2EA98BFC01D79DFCA321539CE998E3A8387A0803BAD2A460D2A1`.
Un bâtiment, une rivière, un chemin ; bâtiment et rivière identiques à la source,
source C58F62BC inchangée. Aucun JSON manuel, pas de publication.
Candidat fermé normalement ; PID 14980 et descendants 11080, 19588, 28832,
30760, 8092, 28932, 6488 tous absents ensuite. Ce parcours complète une tranche
de R2-043 ; il ne démontre pas encore vallée sculptée, retouche porte/clairière
puis régénération, ni qualification exhaustive de R2-023 ou Gate 2.

## Candidat 85FE08E6 — historique du défaut corrigé par A64D5ABA

14 septembre 2026. Exécutable local `river-stroke-studio-20260914-0800` sous
`E:/OpenRPG_Forge/.gate2-evidence/`, SHA-256
`85FE08E6318BA14E29EA80C69B5024F05893C6ECB92EE0087875CC992832E474`.
Trunk code 0 (Rust 1 min 14 s), Tauri code 0 (19,86 s). Racine UI isolée
`river-stroke-ui-20260914-0800`, initialisée par copie exacte F92C7F86 de la
zone créée avec forêt et maison ; aucun JSON modifié manuellement.

Geste réel : armer « Tracer la rivière dans la vue », glisser sur le terrain,
quatre points obtenus ; Examiner accepté, aperçu logique de 693 échantillons.
Points X/Z : (-117,426796 ; 2,3877716), (-52,115795 ; 67,698746),
(-19,460295 ; 100,35423), (13,195206 ; 133,00972). Niveau −0,5 m, largeur 6 m,
profondeur 1 m constants. Apply puis Save/Close/Open réussissent côté session.

**Échec graphique : vue noire après Apply et après réouverture.** Console lue
sans script : panic Bevy 0.19.1 `prepass/mod.rs:1311:56`, unwrap d'une phase
opaque différée absente. L'adapter forçait l'eau en Forward mais laissait le
matériau de berge en Deferred hérité de la scène de preuve. Correction locale
ajoutée : les deux matériaux de l'adapter auteur sont Forward ; le renderer
de démonstration et les documents restent inchangés. Test de régression sur
les deux assets réellement installés : PASS (0,05 s, compilation 1 min 19 s).
Clippy engine natif strict PASS (27,41 s), diff check PASS. Correctif de rendu
observé ensuite sur A64D5ABA ci-dessus ; ce test seul ne prouve pas le rendu.
Le candidat 85FE08E6 ne contient pas cette correction et reste en échec.

Archive sauvegardée : 22958 octets, trois reçus, tête révision 4,
hash du dernier reçu `7193cd46286ebae0ea26edf0e592d683b7353d4b41f0ad1b4ea8d68b44582fb1`.
SHA-256 avant/après réouverture :
`C58F62BCEA2F097B1C424B8BB5E64B850C9745A45BDDA6134AFF847C7245FB31`.
ID rivière `198884e3-d8ce-5237-b072-04d889ff2a79`. Bâtiment et recette de
végétation identiques à la source F92C7F86, source restée intacte.
Fermeture normale du candidat et de sa console : PID 13820 et descendants
28824, 11472, 31804, 27448, 25428, 28212, 22124, 15684 tous absents ensuite.
Pas de playtest ni de qualification visuelle réussie déduits.

## Tracé continu dans la vue — 14 septembre 2026, implémenté

`forge_world::river_stroke::prepare` transforme les intersections X/Z du geste
en brouillon de 4–128 points. Réutilise la simplification de chemins (tolérance
1 m aux échantillons) ; subdivise les plus longs segments si moins de quatre
points subsistent, sans déplacer les extrémités. Niveau Y absolu, largeur et
profondeur du premier point conservés sur toute la nouvelle ligne. Aucune
simulation hydraulique, adaptation automatique au relief ou sauvegarde implicite.
Le formulaire explique que le geste remplace le brouillon et garde ce profil
constant ; chaque point reste ensuite retouchable.

Bouton « Tracer la rivière dans la vue » raccordé au pointeur partagé. Garde
de tête et de brouillon avant consommation du trait, refus pendant playtest
ou projection indisponible ; annulation par Échap/sortie via le pointeur existant.
La commande Insert/Replace, le précontrôle du creusement et Preview/Apply restent
les seuls chemins d'acceptation. Aucune nouvelle donnée persistante ou dépendance.

Trois tests ciblés PASS (0,00 s ; compilation finale 0,62 s) : ligne droite
subdivisée, extrémités/profil exacts, déterminisme, virages, round-trip JSON,
entrées non finies/trop courtes/hors quota refusées. Clippy world/test strict
PASS (0,30 s), Studio Web strict PASS (6,07 s) après correction des comparaisons
float du test et de copied/cloned. Aucun warning projet désactivé.
Ces tests ne qualifient pas le geste GUI, l'application, le creusement ni le
Save/Open de cette nouvelle entrée. Prochain candidat graphique requis ; pas de
rejeu des précédentes qualifications inchangées, R2-023 demeure ouvert.

13 septembre 2026, branche Rust modifiée, aucune publication. **Critère ouvert.**

Adaptateur d'eau remplaçable ajouté ensuite, encore non appelé par le Studio :
`R2_AUTHORING_WATER_ADAPTER.md`. Deux tests headless de remplacement/retrait et
refus des sources périmées acquis ; aucune qualification GPU.

## Transmission au renderer végétal — contrôles natif/WASM acquis

API `update_vegetation_sources_with_waterways` ajoutée : chemins/rivières immuables,
validation avant remplacement, annulation du staging et invalidation des caches.
Le constructeur world expose son hash de population pour éviter de recalculer une
identité différente dans l'adapter. L'ancien appel sans rivière reste compatible
et retire explicitement les rivières d'une projection précédente.

Préparation synchrone, worker natif, reprise procédurale et cache incrémental WASM
consomment tous le constructeur waterways. Les gardes worker/staging utilisent
la même identité composite déjà contrôlée avant commit. Aucun nouveau mécanisme
de mutation des sources, aucune suppression de masque. Studio ne transmet pas
encore ces recettes ; terrain creusé/eau, projection graphique et playtest restent
à raccorder. Contrôles ciblés : `cargo test -p forge_engine --lib
vegetation::quality:: --locked` : 17 PASS, 2 sondes CPU préexistantes ignorées,
323 filtrés, compilation 1 min 54 s et exécution 5,26 s. Nouveau cas : rivière
et 64 chemins avec 32 exclusions manuelles, quatre qualités, identité/statistiques
conformes à la préparation directe, retry inchangé, recette NaN refusée sans
remplacement et appel historique retirant les rivières en conservant les chemins.
Tests headless, pas une preuve d'affichage ni d'upload physique GPU.

Clippy engine lib/tests final PASS (2,82 s) et Studio WASM strict PASS (16,69 s).
Après les tests, seule extraction mécanique de la fixture rivière en helper pour
la limite Clippy 100 lignes (test initial 112) ; pas de nouveau replay runtime
après cette extraction. Avertissement amont proc-macro-error2 conservé.
Formatage/diff contrôlés ; aucun processus graphique lancé ni fichier de zone
modifié. R2-023/Gate 2 restent ouverts.

## Dégagement végétal — générateur raccordé, renderer restant

`VegetationGenerationPlan::from_documents_with_waterways` combine exclusions
manuelles, chemins et rivières sans écrire leurs corridors dans le document
végétal ni consommer son quota de 32 exclusions. Chaque segment interpole la
demi-largeur de l'eau et ajoute shoulder + blend ; extrémités arrondies comme le
calcul de creusement. Pas de dégagement systématique à la largeur maximale.
Le filtre passe avant acceptation d'espacement, par la même fonction utilisée
pour les candidats de densité de rendu. Masques peints et placements manuels ne
sont pas modifiés. Aucun champ canonique ajouté pour cette géométrie dérivée.

Sans rivière, hash/cache identique à l'ancien constructeur de chemins. Avec
rivières, hash composite versionné (source végétale/chemins + recettes rivière)
afin qu'un changement de rivière ne réutilise pas une ancienne population.

Contrôles exécutés : 8 tests intégration rivière/carving PASS (0,06 s, compilation
5,26 s) et 1 test géométrique de largeur variable/extrémités PASS (compilation
lib-test 8,21 s). Présence initiale puis absence d'instances dans le corridor,
32 exclusions manuelles conservées, source inchangée, équivalence sans rivière
et hash distinct avec rivière. Clippy world lib/test strict PASS (1,31 s).
Première compilation corrigée pour la conversion explicite d'erreur de hash.
Pas de mesure GPU, de rendu ni d'essai Studio ; workers, cache incrémental,
staging d'upload et barrière playtest doivent encore transmettre les rivières.
Le refus playtest est conservé. Aucun verdict de Gate 2.

## Projection depuis la tête — calcul et précontrôle ajoutés

`river_projection::project` dérive terrain et liste d'eaux identifiées à partir
du hash complet de la tête. Chaque rivière est calculée sur le même terrain
effectif non creusé ; minimum des coupes fusionné, jamais creusement cumulatif.
Répéter le calcul est identique ; retirer les recettes retrouve le terrain source.
Un lit naturel assez profond conserve l'eau sans exiger une fausse retouche.
Les sorties ne sont pas une nouvelle tête et ne sont pas persistées.

Les échantillons de hauteur dont la correction diffère de la base sont protégés,
y compris une suppression explicite d'un delta de base. Une coupe qui les change
refuse toute la projection, avec ID rivière et coordonnées d'échantillon ; les
retouches extérieures ou déjà plus profondes sont conservées. Limite du modèle
actuel : il reconnaît les différences numériques, pas une intention manuelle de
réaffirmer une valeur identique à la base. Résolution des conflits encore à exposer.

`river_projection::prepare_edit` réutilise ZoneRequest et précontrôle Insert/Replace.
Remove/Unlock restent disponibles pour réparer une recette non projetable.
Pas de mutation ni de readiness GPU impliquée ; host/UI devront consommer cette
préparation et revalider la révision, comme les chemins.

7 tests carving/projection PASS (compilation finale 4,49 s ; exécution 0,02 s),
Clippy world lib/test strict PASS (1,13 s), formatage effectué. Quatre tests nouveaux :
recalcul/retrait, recouvrement sans double coupe, conflits/effacement, lit naturel,
précontrôle conforme à la commande et réparation. Aucun essai GPU dans ce lot.
Raccord végétation, affichage terrain/eau et playtest restent à réaliser ; le refus
playtest demeure. Aucun critère R2-023 coché.

## Recette persistante — domaine et archives raccordés ensuite

`ZoneCorrections.rivers` ajouté : au plus 16 recettes ID-triées, chacune conserve
WaterDocument, budget et verrou. Insert/Replace/Remove/SetLocked partagent le
journal. Verrou revalidé sur commande brute ; Regenerate conserve les recettes
et vérifie leur identité/bornes sur la base proposée. Aucun creusement automatique
dans `effective()` à ce stade : recette sauvegardée ne signifie pas rendu raccordé.
Playtest refuse explicitement les rivières présentes plutôt que les omettre.

Tête v4, archives intégrale v10 / compacte v11 / delta v12. Absence du champ omise
préserve les hashes historiques ; présence vide exige v4. Historique après Undo
conserve la version d'archive requise. Les deux adaptateurs path_surface utilisent
maintenant la version calculée des corrections au lieu de forcer v3.

Contrôles : 2 tests rivière + 2 chemins PASS (0,06 s + 0,02 s, compilation 19,12 s).
Preview sans mutation, Apply/retry, Undo/Redo, régénération, relecture des trois
archives et réétiquetage ancien refusé ; verrou/suppression brute invalide sans
mutation, budget NaN rejeté. Régressions migration/session/delta/host/régénération :
10 PASS, 1 replay local opt-in ignoré préexistant (compilation 7,06 s).
Clippy project lib/nouveaux tests PASS (3,08 s), Studio WASM strict PASS (18,48 s).
Premier check corrigé pour initialisation `rivers: None` en migration et ownership
du Replace ; Clippy corrigé pour must_use sur la version publique, sans lint affaibli.
Pas de test de sauvegarde utilisateur ni de lancement graphique dans ce lot.
Régression world après extension de tête : 9 tests surface + 3 carving PASS
(compilation 6,10 s ; exécution 0,14 s), sans nouveau replay graphique.

## Implémenté : préparation pure du terrassement

`forge_world::river_carving::prepare` prend le terrain accepté, un `WaterDocument`
existant et un budget de coupe. Il ne possède aucune session ni écriture fichier.
La proposition expose les deux hashes sources, un `TerrainEdit::SetHeightDeltas`,
le nombre de sommets modifiés et la coupe maximale. Aucune nouvelle représentation
canonique d'eau et aucune dépendance ajoutée.

Profil requis : PiecewiseLinearV1 + AuthoredSurfaceV1, identités de zone identiques,
au plus 128 contrôles, séparation horizontale ≥ 0,5 m, largeur/profondeur ≤ 64 m,
transition de berge ≤ 64 m, budget de coupe 0,001–64 m. Grille de terrain courante
512 m / pas 2 m validée par TerrainGenerationPlan. Le lit suit l'altitude et la
profondeur interpolées, remonte vers le bord mouillé (un quart de profondeur),
puis rejoint le terrain par une transition smoothstep. Les cibles sont calculées
sur la source immuable ; minimum des couloirs au recouvrement. Aucune coupe limitée
silencieusement : budget dépassé => erreur, pas de proposition partielle.
Les corrections hors corridor restent exactes ; un fond existant plus profond
n'est pas comblé. Une opération sans sommet modifié est refusée explicitement.

## Vérifié

- `cargo test -p forge_world --test river_carving --locked` : 3 PASS, 0,01 s
  (compilation finale 0,87 s).
- Géométrie aux échantillons du lit, bord mouillé, transition et extérieur ;
  déterminisme, hashes et sources intacts, correction extérieure, fond profond,
  budget invalide/dépassé, identité différente, profil d'altitude non supporté.
- `cargo clippy -p forge_world --lib --test river_carving -- -D warnings` : PASS
  (1,39 s). Formatage effectué.
- Premier essai refusé car la fixture conservait une profondeur optique supérieure
  à son nouveau lit de 2 m : fixture rendue cohérente, validation produit conservée.
  Premier Clippy corrigé par noms explicites, sans désactiver de lint.

## Intégration restante, sans verdict prématuré

La zone doit conserver la recette rivière et dériver ce résultat depuis la base
adéquate, non réappliquer cumulativement le terrassement à son propre résultat.
Recette/remplacement/verrou/suppression et archives sont ajoutés ci-dessus ;
il reste à préparer atomiquement terrain/eau/
dégagement végétal. Préserver les retouches manuelles et diagnostiquer les conflits.
Le hash terrain seul ne suffit pas à prouver la synchronisation de l'eau.

Ensuite : inspecteur et points Studio, aperçu terrain/eau commun, Apply/Undo/Redo,
réouverture, régénération, collisions et playtest sur les mêmes sources. Rien de
cela n'est déduit des trois tests purs. Pas de montage graphique lancé pour ce lot.
Recette persistante acquise côté domaine, mais aucune qualification Studio ni
Gate 2 clôturée.

Limites : pas de remblai ; pente/érosion de berge du profil optique non reproduites,
pas de qualification des confluences ni de continuité eau/terrain entre sommets,
pas de mesure de latence maximale à 128 contrôles. La coupe seule ne garantit pas
que des berges naturellement trop basses contiennent l'eau : diagnostic ou remblai
explicite encore nécessaire avant de qualifier le résultat graphique.
