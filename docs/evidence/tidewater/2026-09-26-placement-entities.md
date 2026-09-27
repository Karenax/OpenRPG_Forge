# Entités de placement et sélection partagée

`ManualPlacementPart` porte l'identité canonique sur chaque morceau visible.
Studio utilise maintenant ce composant moteur dans sa sélection par raycast,
à la place du type local équivalent. Les filtres de visibilité, de proposition
et de révision de la sélection restent ceux du parcours existant.

`tidewater_assets::placements::spawn` résout la géométrie par identité et hash,
vérifie la transformation et la présence de tous les handles de matériau avant
de créer des entités. La racine conserve `ManualPlacementProjection` et la pose
du document ; ses enfants partagent les maillages résidents et portent l'identité
de cette racine. La visibilité initiale est cachée : le contrôleur du viewport
doit qualifier ses ressources avant affichage. Aucune collision n'est inventée.

Cette API prépare le raccordement au contrôleur Studio ; celui-ci charge encore
les placements depuis le catalogue GLB. Il reste à brancher la résolution du
paquet, ses matériaux et ressources d'environnement, la qualification visuelle,
les gizmos et le cycle de réconciliation. L'existence d'entités sélectionnables
ne prouve donc pas encore l'édition Tidewater accessible dans l'interface.

Validation : `tidewater_package_probe` exécuté sur `building-package-002`
réussit. Deux instances ont des racines et identités distinctes, les mêmes
handles de maillage et la pose attendue. La suppression de la première détruit
ses enfants et conserve ceux de la seconde. Un matériau manquant laisse le
nombre d'entités inchangé. Le premier essai supposait à tort un World vide
(quatre entités internes présentes) ; le contrôle compare désormais l'état
avant/après. Les handles de matériau factices de ce contrôle ne qualifient pas
le rendu GPU de cette nouvelle route.

Le check Studio `--lib --no-default-features --features web --target
wasm32-unknown-unknown -j 1` réussit après retrait de l'import de macro devenu
inutile. L'avertissement de compatibilité future `proc-macro-error2` subsiste.
Clippy strict ciblé de l'exemple (`--no-deps -D warnings`) réussit également.
