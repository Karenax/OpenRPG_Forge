---
name: ux-consistency-review
description: "Vérifier la cohérence UX entre écrans, parcours ou composants Forge, notamment lors de changements transversaux de vocabulaire, navigation, formulaires ou styles."
---

# Cohérence entre écrans

Choisir les écrans concernés et un écran existant de référence ; relever les composants et tokens partagés. Comparer terminologie métier, libellés et résultats des boutons, navigation, modales, notifications, icônes, dimensions, tables, formulaires, raccourcis clavier et emplacement des fonctions avancées.
Documenter chaque divergence avec les deux emplacements, comportement attendu, impact et priorité P0–P3. Différencier incohérence réelle et variation justifiée par le métier ou l'espace disponible.
Favoriser le composant existant et corriger à sa source si l'impact est maîtrisé. Ne pas créer une variante presque identique, ni refondre des écrans hors périmètre.
Appliquer ux-writing-cleaner aux termes divergents et professional-ui-design aux variations significatives. Vérifier les parcours partagés après correction autorisée ; un audit seul produit des propositions en lecture seule.

Respecter AGENTS.md, la pile Rust/Leptos/Tauri et les conventions existantes.
Studio : apps/forge_studio/src/authoring_gold.css, authoring.css, asset_library.css,
asset_import.css et styles.css. Launcher : apps/forge_launcher/canonical-ui/.
Réutiliser les composants et tokens réels avant toute variante. Ne pas introduire
React, Tailwind, Three.js, bibliothèque d'icônes ou dépendance applicative pour
suivre un exemple externe. Préserver la gouvernance, les erreurs actionnables,
la sauvegarde et undo/redo. Une demande d'audit seule reste en lecture seule.
