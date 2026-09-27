# Fenêtres de l'atelier — protocole initial

La surface `main` reste propriétaire du viewport et des commandes de carte.
La surface locale `atelier` démarre une vue sans Bevy et sans carte dupliquée.
Un relais natif conserve uniquement un index de présentation borné et la dernière
intention de sélection/aperçu. Il ne conserve aucun document de terrain.

Le secondaire partage la commande applicative Tauri déjà bornée, mais le backend
refuse toute commande non `Atelier` provenant d'une autre fenêtre que `main`.
Les opérations du relais sont également contrôlées par label natif : seule la
fenêtre principale publie ; seule la secondaire soumet une intention. La création
de fenêtres est fixe : route locale et label fixes, aucune URL/commande arbitraire.

Les snapshots sont validés (quota, tailles, clés uniques, sélection existante).
La publication change la révision seulement si le contenu change et invalide
les intentions antérieures. Une intention périmée ou inconnue est rejetée. La
fenêtre principale revalide la cible dans sa session après l'aller-retour IPC.
Seules ses opérations Read/Publish consomment les intentions. Les réponses
d'ouverture ou de rapatriement ne sont pas traitées par le consommateur et
ne doivent donc pas vider sa boîte de sélection.

Le sondage de présentation est borné à deux appels par seconde et une requête en
vol par surface ; aucune boucle de rendu supplémentaire. Les contrôles d'édition
et l'historique ne sont pas synchronisés par copie de formulaire HTML.

Première passe native : ouverture secondaire, sujet Terrain synchronisé et
rapatriement vérifiés par Computer Use. Voir le rapport
`docs/evidence/gate2/STUDIO_ATELIER_FIRST_PASS_20260924.md`.

La suite ajoute un catalogue de présentation borné et sa sélection, une
mémorisation locale v1 des ancrages par sujet, et la récupération d'une fenêtre
hors des rectangles des moniteurs. Le catalogue partagé et son rapatriement ont
été observés dans la deuxième passe native (rapport `STUDIO_ATELIER_SECOND_PASS_20260924.md`).
La restauration et la récupération hors écran restent à qualifier graphiquement.
Les formulaires d'édition détachés, le débranchement réel d'un écran et
les contrôles DPI mixtes restent à compléter. Les préférences locales ne
contiennent ni document de carte, ni contenu du catalogue, ni brouillon métier.

## Inspecteur partagé — raccordement en cours de qualification

L'inspecteur publie uniquement l'identité de zone/objet, le hash source, le verrou
et neuf champs numériques de présentation. La transformation est résolue contre
la transformation acceptée, jamais cumulée à partir de l'aperçu précédent.
La demande est revalidée dans le principal puis devient une commande ordinaire
`PreviewZone`. L'application reste explicite dans le principal.

Le relais conserve jusqu'à 64 brouillons de transformation par session native,
chacun borné à neuf chaînes de 64 octets. Un compare-and-swap par brouillon
refuse l'écrasement concurrent. Un changement de sélection, de révision ou la
fermeture secondaire ne les efface pas. Une révision périmée peut être relue,
mais pas appliquée ; reprendre les valeurs courantes est un choix explicite.
Il ne s'agit pas d'une sauvegarde sur disque : quitter l'application perd ces
brouillons non appliqués, conformément au dialogue de fermeture principal.

Le client conserve les saisies en attente indépendamment du composant sélectionné,
sérialise les transmissions et garde les champs plus récents pendant un envoi.
Rapatrier depuis le principal et fermer le secondaire déclenchent un événement
de clôture dans ce dernier ; il n'autorise sa destruction qu'après accusé de
conservation de toute la file. Une erreur ou un conflit bloque la clôture et
reste visible, avec résolution explicite. Les brouillons conservés sont relus
dans le panneau partagé du principal, sans nouvelle autorité de document.

Le parcours de transformation depuis le secondaire puis rapatriement avec
conservation du brouillon a été observé par Computer Use dans le candidat
E5830A5…C81385B (rapport `STUDIO_ATELIER_INSPECTOR_20260924.md`). La fermeture
par Alt+F4 (CloseRequested natif) a ensuite été observée dans 5B0A48F avec
conservation d'une saisie incomplète `-` dans le principal. Le clic littéral
sur la croix a ensuite fermé correctement le secondaire dans B962D2D, sans
nouvelle saisie durant cet essai. Les conflits concurrents restent à qualifier
graphiquement.
Les garanties de crash/redémarrage applicatif des brouillons ne sont pas
promises par ce stockage de session.

Le snapshot transporte aussi `command_status`, résultat en lecture seule de la
commande dans l'éditeur principal. Texte limité à 1024 caractères à la publication
et 4096 octets à la validation du relais. Le secondaire l'affiche séparément des
erreurs de connexion/soumission ; un polling réussi n'efface donc pas un refus
métier. Aucun accusé de mise en file n'est assimilé à une application réussie.
Test du relais : résultat conservé entre lectures, publication trop longue
refusée sans remplacer le résultat précédent, résultat suivant reçu normalement.

Le message publié distingue aussi le traitement en cours d'un aperçu réellement
prêt, encore non appliqué. Il demande de vérifier la carte et d'utiliser Appliquer
dans le principal. Un diagnostic existant n'est pas supprimé lorsqu'un aperçu
précédent reste disponible. La publication conserve ses bornes de texte ; elle
n'ajoute aucun chemin de mutation ni application implicite depuis le secondaire.

La récupération de géométrie peut croiser la destruction du secondaire. Elle
relit alors son existence : une erreur de handle disparu n'est pas remontée
comme échec du principal ; si la fenêtre existe toujours, l'erreur est conservée.
Cela ne contourne ni l'accusé de conservation des brouillons ni les conflits.

API consultée : [WebviewWindowBuilder Tauri](https://docs.rs/tauri/latest/x86_64-pc-windows-msvc/tauri/webview/struct.WebviewWindowBuilder.html),
vérifiée contre les sources locales de Tauri 2.11.5 épinglées par le projet.
