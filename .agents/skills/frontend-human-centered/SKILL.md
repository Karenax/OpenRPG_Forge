---
name: frontend-human-centered
description: "Concevoir pour les utilisateurs réels lors de toute création ou modification significative d’interface Forge, écran, panneau ou parcours métier."
---

# Concevoir pour les humains

1. Identifier l'utilisateur, sa tâche fréquente, les données nécessaires et le résultat attendu depuis le contexte existant. Demander seulement l'information manquante déterminante.
2. Organiser les informations par tâche ; rendre navigation et actions fréquentes prévisibles. Préserver une densité organisée adaptée à un éditeur professionnel.
3. Écrire des libellés courts orientés action. Ne conserver chaque texte visible que s'il aide une décision, une opération ou la compréhension d'un état.
4. Déplacer IDs techniques, logs, codes HTTP, API et états backend dans Diagnostics/Administration. Conserver les identifiants métier nécessaires. Ne jamais masquer un échec de sauvegarde ou une perte de données.
5. Éviter descriptions permanentes, explications évidentes, boutons sans usage et décoration gratuite. Conserver l'aide métier nécessaire et les noms accessibles des icônes.
6. Reprendre les conventions graphiques et contrôles existants ; ne pas transformer un atelier en démonstration technique ou landing page.
7. Vérifier clavier, focus, erreurs, vide, chargement et succès à proportion du changement. Utiliser ux-writing-cleaner pour les textes, professional-ui-design pour le design important, ux-visual-audit pour une nouvelle page/refonte.

Source consultée le 22 octobre 2026 : https://developers.openai.com/api/docs/guides/frontend-prompt
Synthèse adaptée au métier ; les suggestions de frameworks, palettes et effets externes ne remplacent pas le design existant.

Respecter AGENTS.md, la pile Rust/Leptos/Tauri et les conventions existantes.
Studio : apps/forge_studio/src/authoring_gold.css, authoring.css, asset_library.css,
asset_import.css et styles.css. Launcher : apps/forge_launcher/canonical-ui/.
Réutiliser les composants et tokens réels avant toute variante. Ne pas introduire
React, Tailwind, Three.js, bibliothèque d'icônes ou dépendance applicative pour
suivre un exemple externe. Préserver la gouvernance, les erreurs actionnables,
la sauvegarde et undo/redo. Une demande d'audit seule reste en lecture seule.
