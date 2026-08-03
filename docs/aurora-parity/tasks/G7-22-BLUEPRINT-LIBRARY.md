# G7-22 — Bibliothèque et gouvernance des palettes

Le catalogue Blueprint présente désormais une organisation déterministe dérivée du projet canonique :

- catégories hiérarchiques `origine/type` ;
- collections par origine, auteur et favoris ;
- familles de variantes au moyen du suffixe stable `blueprint--variante` ;
- impact exact d’une définition sur ses instances par UUID ;
- provenance, licence, hash et état lecture seule toujours visibles.

La mise à jour d’une définition demeure transactionnelle. Comme les instances référencent l’UUID du
blueprint, nom, type et données partagées sont propagés sans réécriture des instances ; leurs
overrides restent intacts. L’index est reconstruit depuis la source de vérité et ne crée aucun cache
canonique concurrent.

Preuves : `BlueprintLibraryIndexTests`, `BlueprintCatalogTests`, `ProjectJsonSerializerTests` et le
gate global. Les migrations versionnées de packs distants, variantes à deltas de propriétés et
collections auteur persistantes restent des extensions explicites.

La prochaine tranche unique est **G7-23 — import et export de bibliothèques**.
