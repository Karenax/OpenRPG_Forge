# G8 PLACEABLE-03 — Protection, verrouillage, pièges et trésor

La protection explicite contrôle les actions et permissions, fournit un feedback joueur et journalise
les dérogations MJ. `LockComponent` traite états, clés/credentials, challenge de compétence, code,
hacking, reverrouillage, blocage et permissions.

`TrapComponent` porte déclencheur, payload, détection, désarmement, reset, ownership, VFX et politique
amicale. `LootSpawner` lie une table au timing init/open/reset, à une seed et à un état généré persistant.
Les événements `ContainerOpened`/`ContainerClosed` conservent acteur, résultat, ancien état et autorité.

Preuves : `PlaceableSecurityServiceTests`, suite RPG Kit et solution globale.

La prochaine tranche unique est **G8 PLACEABLE-04**.
