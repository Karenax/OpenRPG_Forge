# G8 TRIGGER-01 — Volumes, filtres et détection

`TriggerShape` couvre polygone 2D extrudé, box, sphere, capsule et convex hull, avec sommets, trous,
hauteur, snap et aperçu runtime. L’édition d’un sommet produit patch et inverse et accepte le dry-run.

`TriggerVolume` compose filtres, conditions, cooldown, politique de tir et actions typées. Les portails
réutilisent le même `PortalComponent` que les portes. `VolumeEntered` conserve entité, point,
direction, layers et state ; `VolumeExited` ajoute durée passée et raison.

Preuves : `TriggerFoundationTests`, suite RPG Kit et solution globale.

La prochaine tranche unique est **G8 TRIGGER-02**.
