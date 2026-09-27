# R2-016 — choix d'espèces depuis le catalogue gouverné

15 septembre 2026, Rust, préparation moteur-neutre ; critère ouvert.

## Constat

Le catalogue `VegetationPrototypeCatalog` v1/v2 d'ADR-0010 existe déjà : liaisons
par couche arborée, asset et hash GLB, pivot, hauteur source/cible, choix stable
entre variantes. Il ne constitue pas encore un catalogue éditable dans la tête
de zone. Les couches auteur ne doivent pas reprendre implicitement les IDs de
la scène d'origine ; aucun retag global du kit ou fichier utilisateur n'est permis.

## Implémenté

`prepare_selection` prépare une nouvelle valeur du même catalogue v2, depuis
des sélections explicites (couche source, asset, couche cible). Il valide le hash
source attendu et le kit, conserve exactement pivot/dimensions/hash de chaque
liaison choisie et épingle le hash cible fourni par l'appelant. La couverture de
toutes les couches arborées cibles est obligatoire. Doublons couche/asset,
sélection inconnue, trous et quota supérieur à 64 sont refusés. Tri par couche
et asset : même résultat quel que soit l'ordre de sélection.

Entrées intactes, aucun filesystem, réseau ou renderer ; aucune pose créée,
nouvelle dépendance ou format canonique introduit. Le type de sélection est un
paramètre en mémoire, pas un nouveau document persistant. La validation porte
sur les métadonnées : le résolveur existant doit encore vérifier les vrais octets
GLB et les dimensions chargées avant projection.

## Vérifié

Deux tests ciblés `forge_assets authored_selection` PASS, compilation 4,28 s,
exécution 0,00 s : affectation d'un prototype à deux couches, ordre indépendant,
normalisation inchangée, choix du prototype, round-trip JSON et refus de sources
périmées/altérées, kit périmé, cible manquante, doublon et dépassement de quota.
Clippy all-targets strict PASS (0,73 s) après suppression de clones inutiles dans
les tests ; formatage et diff contrôlés. Pas de preuve de fichier/GUI/GPU.

## Suite nécessaire pour R2-016

### Palette et aperçu — code raccordé

Palette d'arbres dans les contrôles de zone : chargement explicite des métadonnées
du kit, cases par couche/prototype, plusieurs variantes, résumé des choix, effacement
global et bouton Examiner. Les réglages sont des brouillons jusqu'à Preview/Apply,
via les commandes ordinaires ; les poses et masques ne sont pas édités. Une
sélection non vide doit couvrir toutes les couches arborées, sinon refus explicite.
Le brouillon suit les changements de tête acceptée, pas les réponses d'aperçu.

Aperçu vérifié contre hash/révision/inverse/résultat du reçu, limité au champ espèces.
Le chargeur reçoit une tête temporaire sur la révision du terrain accepté ; Abandonner
revient à la tête acceptée et invalide la livraison temporaire. Le statut de projection
attend aussi le cache réellement projeté, pas seulement les prototypes chargés.

Le retrait du mode gouverné restaure explicitement le rendu procédural, enlève les
primitives et lots gouvernés puis permet de libérer les caches. Retirer uniquement
le marqueur de mode aurait laissé des géométries gouvernées visibles : ce chemin
a été corrigé avec la restauration existante du moteur.

Ces raccords sont implémentés, non qualifiés dans l'interface. Prochain replay groupé :
chargement palette, choix/mélange, Preview/Abandon/Apply, Undo/Redo, Save/Open,
retrait global, changements de zone et absence de fichiers. Aucun verdict R2-016
avant cette preuve. Candidat graphique 8C6A960A inchangé et antérieur à ce code.
Clippy Studio Web/WASM final PASS (25,01 s), après extraction du libellé d'aperçu
hors du contrôleur commun trop long. Aucun lint désactivé ; avertissement futur
tiers inchangé. Formatage et diff PASS. Aucun lancement graphique pour ce lot.

### Livraison Studio des sélections acceptées

`authoring_species` observe maintenant la tête acceptée. Il lit le kit et son
catalogue déployé, résout les références épinglées, puis réutilise le précontrôle
GLB des placements : SHA-256, structure autonome, plafond 64 Mio par fichier et
256 Mio au total, déduplication par hash. Chaque attente vérifie son époque ;
une réponse remplacée ne peut pas publier ses assets dans la nouvelle demande.

Source mémoire dédiée `forge-species`, chemins `<sha256>.glb` : pas de réemploi
d'un handle Bevy associé à d'anciens octets sous un même nom. La projection de
végétation attend la résolution et les matériaux de vent ; elle ne prend pas
un téléchargement réussi pour une présentation prête. Absence/erreur explicite.
Le retrait des sélections enlève le mode prototypes et libère les fichiers mémoire
résidents/caches du chargeur. Une recréation du monde peut réémettre le plan conservé.

Limites : un seul catalogue déployé est découvert, les autres références sont
refusées (pas de substitution). Palette et aperçu temporaire des choix à relier.
Pas encore de parcours graphique, de qualification de libération GPU ni de mesure
de chargement ; seuls les chemins de code sont raccordés. Vérifier aussi les
transitions entre zones, annulation et défauts de fichier dans le replay groupé.
Clippy Studio Web/WASM PASS (25,12 s puis 24,94 s après reprise de monde),
avertissement futur tiers inchangé ; formatage et diff PASS. Aucun essai graphique
ni publication Git. Cette preuve ne couvre pas l'exécution des nouveaux chemins.

### Installation dans les zones auteur

Le Studio auteur installe maintenant `VegetationPrototypeLoaderPlugin` avec sa
végétation. Il ne dépend plus d'un catalogue de la scène de référence au Startup :
plan absent accepté, aucun faux catalogue ni chargement d'asset pour une zone vide.
`request_reload` peut accepter le premier plan après le démarrage. Le plugin
historique installe ce même service une seule fois avant son chargement initial.
Le cache de vent ne travaille pas lorsque le catalogue est absent (pas de boucle
de préparation vide à chaque frame).

Raccord de service réalisé, pas encore celui du formulaire : livraison des GLB
vérifiés, sélection, aperçu/application, restauration et disponibilité du rendu
restent à relier. Pas de qualification graphique acquise par cette installation.
Clippy Web/WASM PASS (27,71 s), après regroupement des deux gardes plan/chargements
pour respecter la limite de taille de fonction ; aucun lint désactivé. Formatage
et diff contrôlés. Aucun processus graphique lancé ni publication Git.

### Rechargement du moteur — préparation du raccord Studio

Le chargeur de prototypes était limité au Startup. `request_reload` permet
maintenant de lui soumettre un nouveau plan validé sans réinstaller le plugin :
réutilisation de l'AssetServer, déduplication du plan identique, statut Loading,
timeout et erreurs existants, conservation du cache de prototypes précédent
jusqu'à résolution complète. L'hôte doit toujours vérifier les octets GLB avant
soumission. Chargeur/horloge/AssetServer absents : erreur explicite.

Le cache de vent détecte désormais le changement de catalogue ; il reconstruit
les matériaux dérivés une fois. En échec, anciennes géométries non exposées sous
les nouveaux identifiants, erreur conservée jusqu'au prochain catalogue. Aucun
nouveau renderer ni copie de poses. Cette API n'est pas encore appelée par les
contrôles Studio : le raccord doit attendre la disponibilité des prototypes et
resoumettre la projection, y compris pour Undo/abandon d'aperçu.

Implémentation seulement à ce stade : cycle de rechargement réel, libération des
ressources dérivées et transitions visibles à qualifier dans le replay groupé.
Clippy Studio Web/WASM PASS (28,04 s), avertissement futur tiers inchangé.
Pas de lancement graphique ni de verdict de qualification du rechargement.

### Résolution des références — 15 septembre, étape suivante

`prepare_pinned_selection` vérifie le SHA-256 canonique du catalogue complet,
pas seulement celui de la végétation. Un pivot ou une hauteur modifiés avec le
même GLB ne peuvent pas remplacer silencieusement la référence sauvegardée.
Test ciblé PASS (4,19 s compilation, 0,00 s exécution) : changements de pivot et
hauteur refusés ; relecture JSON avec formatage différent acceptée.

Adapter `forge_engine::resolve_authored_species` ajouté : valide la tête,
résout chaque référence parmi au plus 64 catalogues fournis, conserve les
normalisations, dérive le hash de végétation effective et produit le catalogue
v2 existant. Catalogue absent, kit incompatible ou couverture arborée incomplète
refusés. Aucun réseau, fichier, mesh, collider ou pose modifié ; pas de remplacement
silencieux des couches sans choix explicite. Une liste de choix vide retourne
`None` et laisse l'appelant traiter l'héritage existant.

Cette API est préparée, pas encore appelée par le Studio. Sa compilation ne
prouve ni le chargement GLB ni un parcours sauvegarde/rendu ; test comportemental
de l'adapter complet, palette et raccord au chargement encore dus.

Clippy Studio Web/WASM incluant assets et moteur PASS (28,08 s), avertissement
futur tiers `proc-macro-error2` inchangé. Aucun essai graphique lancé pour ce lot.

### Persistance implémentée le 15 septembre

Choix par couche dans `ZoneCorrections.species`, référence immuable au catalogue
source par SHA-256 et couple couche source/asset. Commande `prepare_species`,
preview sans mutation, application idempotente, retrait, Undo/Redo et conservation
par Regenerate. Refus des couches absentes/non arborées, doublons et plus de 64
prototypes ; aucune vérification fictive des vrais fichiers dans le domaine.
Tête v7, archives v19/v20/v21 ; ancien champ omis, hashes historiques préservés.

Deux tests `forge_project --test zone_species` PASS : compilation 29,22 s,
exécution 0,02 s. Les trois codecs sont relus avec historique et retrait/Undo/Redo,
les versions rétrogradées sont refusées, la régénération conserve les sélections
et refuse leur disparition. Entrées invalides sans mutation. Vérification en
mémoire : ni fichier utilisateur ni GUI ouverts, aucune qualification Save/Open
Studio ou projection. Le candidat 8C6A960A ne contient pas ces changements.

Clippy a détecté le dépassement de taille des variantes de commande après ajout
du champ : `ZoneAction::Edit` contient désormais une correction allouée et le
constructeur `ZoneAction::edit` conserve les appels typés. Adaptation mécanique
des appels et déstructurations Projet/Monde/Studio, JSON inchangé. Les erreurs
de types intermédiaires ont été corrigées ; Clippy Projet/Monde all-targets
strict PASS (dernier passage 0,84 s). Aucun lint désactivé.

Contrôle final après correction : Clippy Studio Web/WASM PASS (33,24 s),
avertissement futur tiers `proc-macro-error2` inchangé. Tests `zone_species`
et `zone_start` : 4 PASS (compilation 29,87 s, exécutions 0,02/0,01 s).
Le second couvre aussi Save/Close/Open du départ via l'hôte natif ; il ne
prouve pas encore le parcours de sélection d'espèces dans le Studio.
Formatage et `git diff --check` PASS. Aucun processus graphique lancé.
Qualification workspace complète et CI distante non exécutées pour ce lot.

Raccorder maintenant l'adapter aux catalogues immuables chargés dans le Studio.
Ne pas conserver un hash de végétation figé comme seconde autorité : le catalogue
de projection se dérive de la tête effective et des références d'assets épinglées.
Puis palette Studio depuis le kit existant, examen/application, Save/Open,
projection et playtest sur les mêmes choix. Les herbes et autres familles restent
à traiter explicitement ; ce premier contrat réutilise les prototypes d'arbres.
Aucun branchement provisoire non sauvegardable annoncé comme outil terminé.
Gate 2 reste ouverte, aucune publication ni copie d'assets Cadle.

## Replay Studio du 15 septembre — espèces sauvegardées

Candidat SHA-256 `42794BEB895F2221360B48E85FA60AC6FD5F045C22D50163908546331DF37EEA`,
dans `E:/OpenRPG_Forge/.gate2-evidence/species-palette-studio-20260915`.
Frontend `species-palette-web-20260915` construit (Rust 2 min 39 s), packaging PASS.
Le rapport de build reste BUILD_ONLY ; les observations suivantes sont distinctes.

Copie isolée `E:/OpenRPG_Forge/.gate2-evidence/species-palette-ui-20260915`, zone
`81de8d42-89b9-58b5-b090-406cf4c63d06`. Catalogue du kit existant : chêne et pin,
hauteur cible 5,65 m. Aucun asset acquis ou copié depuis Cadle.

- Premier aperçu refusé : `Zone archive exceeds 4 MiB before mutation` ; hash
  de l'archive inchangé. Conversion compacte proposée : 3 843 523 → 3 089 354
  octets, 14 commandes conservées. Conversion explicite en mémoire avant nouvel essai.
- Chêne seul : silhouette large visible ; Échap restaure les arbres procéduraux.
- Chêne/pin : deux silhouettes visibles ; Ctrl+Entrée applique, Ctrl+S sauvegarde.
- Session synchronisée ; fermeture de zone puis réouverture depuis la liste :
  mélange restauré visuellement et session synchronisée.
- Archive finale : 3 090 183 octets, SHA-256
  `5717781F981031CD11D4B3DC65AFA1F282AE5BA88C57D6821F4B787FEA2FDB2B`.
- Source habitat-density-ui intacte :
  `C6343B05694C6BCEF39D527168C14DFE7A345D986D926285C7744A4B58F879F1`.

Captures locales dans le dossier de copie : `oak-preview.jpg`, `oak-abandoned.jpg`,
`mixed-preview.jpg`, `mixed-reopened.jpg`. L'éclairage de travail est très clair :
ces vues prouvent le changement de géométrie, pas une qualité artistique acquise.
Pas de replay Undo/Redo graphique ni playtest ; pas d'audit différentiel natif
supplémentaire des corrections dans cette archive. Les tests de codecs précédents
ne sont pas présentés comme ces vérifications GUI manquantes.

Fenêtre fermée ; absence vérifiée des PID propres 28684, 7632, 8820, 17056,
13764, 19396, 23208, 32380. Aucune compilation relancée pour le replay.
R2-016 reste ouvert (autres familles notamment), Gate 2 ouverte, aucun push.

### Reprise après indisponibilité — code postérieur au replay

Deux corrections bornées dans le Studio :

- La déduplication de livraison ignorait le hash demandé même après un échec
  de `prepare`. Une nouvelle demande de session/aperçu réinitialise désormais
  cet échec et incrémente l'époque ; les requêtes en cours et réussies restent
  dédupliquées. Aucun retry par frame, aucune levée du contrôle SHA-256.
- Le retour à une sélection vide exigeait à tort un catalogue chargé. Le bouton
  d'examen reste maintenant disponible pour cette sélection sans catalogue ;
  la préparation valide la tête, puis emprunte la commande transactionnelle
  existante. Toute sélection non vide exige toujours le catalogue et sa résolution.

Clippy Studio Web/WASM strict PASS (dernier passage 7,19 s), fmt --check PASS.
Avertissement futur tiers proc-macro-error2 inchangé. Aucun essai graphique,
aucun nouveau binaire ni archive modifiée par ce correctif. Reprise après
échec réseau et retrait sans catalogue : implémentés, parcours réel encore dû.

### Mélange multi-couches — implémentation

La palette propose de reprendre les espèces d'une couche sur toutes les couches
arborées de la zone. L'action ne fait que remplacer le brouillon ; elle conserve
les références épinglées et trie les couches par ID. Le domaine valide la tête
candidate, dont le quota global de 64 prototypes, avant remplacement du brouillon.
Couche sans sélection : erreur explicite. Les couches herbe ne sont pas ciblées.
Les masques, poses, paramètres de génération et fichiers ne sont pas modifiés.
L'auteur doit encore examiner, appliquer puis sauvegarder par le parcours normal.

Clippy Studio Web/WASM strict PASS (7,32 s). Fonction implémentée, non qualifiée
dans la fenêtre ; ne figure pas dans le binaire 42794BEB. Aucun nouveau test GPU
ni modification de l'archive de preuve. R2-016 reste ouvert.

Règle multi-couches extraite dans `forge_project::zone_authoring::species` :
`spread_to_tree_layers` prépare les choix sans moteur ni interface et valide
aussi le brouillon source (dont les références orphelines). Le Studio délègue
à cette règle ; pas de seconde implémentation métier.

Test ajouté : deux couches, tri stable, références identiques, préparation sans
mutation, source absente/orpheline refusée, 33 espèces × 2 couches refusées,
Preview/Apply, conservation de la base et des autres corrections, archive relue
et Undo. Premier essai : identifiant de fixture invalide `second-trees`, corrigé
en `second_trees`. Passage final : 3 tests zone_species PASS (2,13 s compilation,
0,02 s exécution), Clippy Studio Web/WASM strict PASS (36,50 s), fmt/diff PASS.
Cette preuve CPU ne remplace pas le parcours du bouton ni le playtest.

## Candidat regroupé prêt — BUILD_ONLY

`E:/OpenRPG_Forge/.gate2-evidence/draft-species-studio-20260915/forge-studio-candidate.exe`
SHA-256 `C86B50EDFFC21EE7298EED6AC3CCD969625C99B76FCBE69B4CD4BA268E4C7CBE`.
Frontend `draft-species-web-20260915`, Rust 3 min, packaging réussi. Un premier
appel Trunk a refusé NO_COLOR=1 avant compilation ; relancé avec la valeur booléenne
true. Session de compilation 64422 terminée, packaging 18942 terminé.
Kit existant inchangé ; fenêtre configurée à -1840/90, 1600×900. Aucun lancement.
Rejouer les fonctions nouvelles sur copie de l'archive 5717781F : mélange partagé,
retrait et retour, puis invalidation de brouillons. Ne pas reconstruire le même
candidat sans changement pertinent. Réussite graphique et Gate 2 non déduites.

Replay ciblé C86B50ED : copie `draft-species-ui-20260915` de l'archive 5717781F.
Sans charger le catalogue dans la palette, Effacer tous les choix rend Examiner
disponible ; aperçu procédural visible, Apply activé. Échap restaure le mélange
chêne/pin et désactive Apply. Captures `remove-preview.jpg`, `remove-abandoned.jpg`.
Ni Apply ni Save ; hash archive inchangé. Le kit est présent : cela prouve le
retrait sans chargement préalable du catalogue dans la palette, pas un kit absent.
Mélange multi-couches et retry après erreur réseau non testés ici.
Fenêtre fermée et PID propres absents : 33228,10688,19784,27828,19716,29436,5928,5312.
