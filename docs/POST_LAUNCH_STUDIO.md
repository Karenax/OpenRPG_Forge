# Studio post-lancement

Le Studio post-lancement regroupe les 27 dernières capacités G13 dans six onglets : Mobilité,
Contenu, Live Director, Réseau, Plateforme et Production. Il édite le profil canonique du module,
affiche un résumé sémantique, prévisualise le changement et applique une commande annulable.

La configuration reste sûre par défaut : génération média locale et optionnelle, extensions serveur
signées et isolées, protocole négocié par famille/version, hot reload en staging avec rollback,
enregistrements expurgés et budgets de soak bornés. La Crypte active un profil de démonstration
déterministe uniquement lorsqu'elle charge la migration par défaut ; un profil auteur gagne toujours.

MCP fournit `forge_get_post_launch_capabilities` et `forge_preview_post_launch_capability`. Les
prévisualisations `mobility`, `protocol`, `media` et `soak` n'écrivent rien. Une application passe
par le plan transactionnel existant des extensions runtime.

Les marqueurs `OPENRPG_FORGE_TOOLSET_G13AC_AL_POST_LAUNCH_PASS` et
`OPENRPG_FORGE_RUNTIME_G13AC_AL_POST_LAUNCH_PASS` prouvent respectivement l'intégration auteur et
runtime. Les tests unitaires restent l'autorité pour chaque décision détaillée.
