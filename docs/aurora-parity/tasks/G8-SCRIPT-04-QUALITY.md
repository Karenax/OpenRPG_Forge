# G8 SCRIPT-04 — Qualité et état ForgeScript

`ForgeScriptQualityService` apporte un `TestWorld` seedé, des fixtures et services mockés, des
assertions et un rapport CI. Le profiler agrège CPU, instructions, allocations et chemins d'appel,
classe les hotspots et produit des alertes de seuil.

La référence API est générée depuis le registre versionné avec exemples, disponibilité par kit/côté
d'exécution et ressources MCP. L'analyse statique localise cinq familles de risques. La sauvegarde
n'accepte que des composants explicitement enregistrés, validés et migrables ; aucun pointeur ou état
brut de VM n'est sérialisé.

La prochaine tranche unique est **G8 SCRIPT-05**.
