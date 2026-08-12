# G8 SCRIPT-04 — Qualité et état ForgeScript

`R100 / FC-SCRIPT-P1-04` est clos. `ForgeScriptQualityService` fournit un `TestWorld` déterministe,
des fixtures et services mockés, des assertions, des suites sous quota et un rapport CI. Le
profileur agrège CPU, instructions, allocations et chemins d’appel par handler, classe les hotspots
et produit des alertes de seuil localisées.

La référence API est générée depuis le registre versionné avec exemples, disponibilité par kit et
côté d’exécution, plus ressources MCP. L’analyse statique applique cinq familles de règles
configurables. L’état n’accepte que des composants explicitement enregistrés, validés et migrables,
sous quota ; aucun pointeur ni état brut de VM n’est sérialisé.

Le même profil canonique alimente l’éditeur, MCP et un panneau runtime accessible par un adaptateur
unique. La preuve de clôture est `P1-R100-SCRIPT-QUALITY-ACCEPTANCE.md`. La prochaine tranche
canonique est `R101 / FC-SCRIPT-P1-05`.
