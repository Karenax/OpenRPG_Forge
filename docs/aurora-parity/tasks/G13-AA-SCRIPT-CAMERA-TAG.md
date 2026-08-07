# G13-AA — Scripts : caméra et tags

Features : `ORF-SCR-018`, `ORF-SCR-024`.

## Livré

Une séquence caméra décrit rig, plan, durée, blend, verrouillage, skip accessible et récupération
d'abandon. Le routage par tag est explicitement activé et associe exactement tag + événement à un
script, sans recherche implicite ni accès direct au moteur.

## Preuves

- test : `G13AA_ScriptCameraTag_RoutesExplicitEventAndRestoresControl` ;
- Toolset/runtime : `G13AA_SCRIPT_CAMERA_TAG_PASS` et `G13AA_SCRIPT_CAMERA_ROUTE_PASS`.

Limite : le runtime caméra applique le plan ; le profil ne déplace aucun nœud lui-même.
