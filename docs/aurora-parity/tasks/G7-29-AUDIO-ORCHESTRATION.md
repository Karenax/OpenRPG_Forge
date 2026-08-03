# G7-29 — Orchestration et accessibilité audio

L’orchestration audio canonique contient cinq états musicaux, pistes, stems, transitions et
cooldowns ; des bus hiérarchiques avec snapshot, plateforme, volume, dynamique et renforcement des
sons importants ; et des captions localisées liées aux ressources avec importance et direction.

`AreaMusicStateMachine` refuse une transition pendant son cooldown et résout les captions par
langue puis fallback. Le Toolset génère une structure Master/Music et permet d’ajouter un son
important sous-titré dans le même profil transactionnel.

Preuves : `AreaMusicStateMachineTests`, tests environnement/sérialisation et suite globale.

La prochaine tranche unique est **G7-30 — règles et états de jeu**.
