---
name: professional-ui-design
description: "Composer ou modifier significativement le design d’un écran métier Forge, sa typographie, ses couleurs, ses composants ou sa hiérarchie visuelle."
---

# Interface professionnelle

Lire frontend-design pour la composition et web-design-guidelines pour les bonnes pratiques si elles sont pertinentes ; ouvrir uniquement ces ressources nécessaires.
Appliquer frontend-human-centered comme contrainte produit : les conseils créatifs externes ne justifient ni hero marketing, nouvelle palette ou police systématique, ni nouvelle pile technique.

Relever les tokens, composants, états et dimensions en place ; proposer un changement limité au besoin. Garder navigation stable, hiérarchie claire, typographie cohérente, espacements réguliers et icônes nommées. Réutiliser contrôles et modales plutôt que créer des variantes.
Adapter tables et formulaires au volume réel. Rendre l'action principale identifiable et préserver les actions secondaires nécessaires. Prévoir chargement, vide, succès et erreur, avec récupération et maintien des saisies.
Ne pas ajouter automatiquement cartes, badges, dégradés, ombres, animation ou illustration. Une information métier utile prime sur le minimalisme.
Vérifier les critères WCAG 2.2 AA pertinents : contrastes mesurés, focus visible non masqué, ordre clavier, noms accessibles, erreurs associées aux champs, cibles, zoom et reflow selon les surfaces supportées. Respecter reduced-motion. Ne pas déclarer une conformité complète après quelques tests heuristiques.
Pour un changement important, utiliser ux-visual-audit puis ux-consistency-review si plusieurs écrans sont touchés.

Respecter AGENTS.md, la pile Rust/Leptos/Tauri et les conventions existantes.
Studio : apps/forge_studio/src/authoring_gold.css, authoring.css, asset_library.css,
asset_import.css et styles.css. Launcher : apps/forge_launcher/canonical-ui/.
Réutiliser les composants et tokens réels avant toute variante. Ne pas introduire
React, Tailwind, Three.js, bibliothèque d'icônes ou dépendance applicative pour
suivre un exemple externe. Préserver la gouvernance, les erreurs actionnables,
la sauvegarde et undo/redo. Une demande d'audit seule reste en lecture seule.
