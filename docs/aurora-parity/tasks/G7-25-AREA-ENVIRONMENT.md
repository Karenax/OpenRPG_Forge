# G7-25 — Environnements de zone

`AreaEnvironmentProfile` est désormais une partie versionnée de chaque document de zone. Il réunit
classification extensible et tags, cycle horloge/jour/nuit, éclairage HDR, température, intensités,
ombres, brouillard distance ou volumétrique, et profil audio adaptatif jour/nuit/combat/ambiance/
réverbération.

L’écran Environnement de zone édite ce contrat puis appelle `SetAreaEnvironmentHandler`. La mutation
offre contrôle de révision, dry-run, patch inverse, audit et undo. Le sérialiseur canonique conserve
les profils et migre les anciens documents vers un défaut cohérent avec intérieur/extérieur.

Les courbes physiques, probes, volumes locaux et lecture audio runtime seront branchés sur ces
contrats dans les lots spécialisés ; aucun paramètre moteur n’est stocké dans le domaine.

Preuves : `SetAreaEnvironmentHandlerTests`, tests de sérialisation et suite globale.

La prochaine tranche unique est **G7-26 — streaming, minimap et terrains**.
