# P2 R142 — Météo runtime distribuée

`ORF-GAME-018` conserve la machine autoritaire déterministe et sa sauvegarde. Le palier ferme les
deux câblages manquants : publication réseau signée par empreinte de contenu, application seulement
des révisions serveur plus récentes, et dispatch ForgeScript `weather.changed` typé avec état,
intensité, humidité, mouillage, foudre, effet gameplay et révision.

Tests ciblés : `WeatherReplicationServiceTests` (2/2) et
`WeatherForgeScriptDispatchServiceTests` (1/1). Preuve Toolset :
`OPENRPG_FORGE_TOOLSET_R142_WEATHER_RUNTIME_PASS`.

P3/P4, AV7–AV10, commit et push sont exclus.
