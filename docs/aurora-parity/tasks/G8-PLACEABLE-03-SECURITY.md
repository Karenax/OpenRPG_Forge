# G8 PLACEABLE-03 — Protection, verrouillage, pièges et trésor

La protection explicite contrôle les actions et permissions, fournit un feedback joueur et journalise
les dérogations MJ. `LockComponent` traite états, clés/credentials, challenge de compétence, code,
hacking, reverrouillage, blocage et permissions.

`TrapComponent` porte déclencheur, payload, détection, désarmement, reset, ownership, VFX et politique
amicale. `LootSpawner` lie une table au timing init/open/reset, à une seed et à un état généré persistant.
Les événements `ContainerOpened`/`ContainerClosed` conservent acteur, résultat, ancien état et autorité.

R093 relie désormais protection et verrouillage au même profil projet canonique, avec diagnostic
stable, override MJ journalisé par `PlaceableProtectionOverride`, résolution credential/skill/code/
hacking, reverrouillage, jam, Studio transactionnel, ForgeScript/MCP et contrôles runtime focusables.

R094 relie le piège, le trésor et les événements de conteneur à `PlaceableSecurityEventsProfile`.
La seed, le timing et l'état généré restent déterministes et persistants ; le runtime réutilise les
services RPG Kit et expose des listes de loot et d'événements avec commandes focusables.

Preuves : `PlaceableSecurityEventsRuntimeAdapterTests`,
`P1-R094-PLACEABLE-SECURITY-EVENTS-ACCEPTANCE.md` et
`r094-placeable-security-events-ui.png`.
