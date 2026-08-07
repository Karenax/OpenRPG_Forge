# G13-V — Routes de véhicules

Feature : `ORF-WPT-012`.

## Livré

`VehicleRouteProfile` décrit points 3D, largeur, inclinaison, voies, sens, limite de vitesse, arrêts,
attente, dépassement et mode réseau. L'échantillonnage déterministe interpole la route et retourne la
limite et l'arrêt actifs.

## Preuves

- test : `G13V_VehicleRoutes_InterpolatesSplineLaneAndStopPolicy` ;
- Toolset/runtime : `G13V_VEHICLE_ROUTE_PASS`.

Limite : ce lot ne fournit pas un système de conduite complet ni une physique de véhicule.
