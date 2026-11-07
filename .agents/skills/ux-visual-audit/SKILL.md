---
name: ux-visual-audit
description: "Auditer visuellement une interface réelle avec Playwright MCP lors de nouvelles pages, refontes ou changements importants ; utiliser aussi sur demande « audit UX complet ». Un audit seul reste en lecture seule."
---

# Audit visuel réel

1. Définir écrans, scénarios, tailles pertinentes et données fictives. Pour un audit seul, utiliser une instance accessible ; ne démarrer un serveur ou lancer un build que si la demande l'autorise. Pour une implémentation autorisée, démarrer seulement le serveur de test requis, sans remplacer un processus utilisateur ni réutiliser des données réelles pour des mutations. Utiliser les lanceurs et caches partagés prescrits si un build est nécessaire et autorisé.
2. Découvrir les outils du serveur MCP playwright. Vérifier une navigation et un snapshot accessible. Si indisponible après installation, demander un redémarrage de Codex puis réessayer ; ne pas déclarer une validation visuelle à partir d'un linter ou du seul test d'installation.
3. Ouvrir l'application réelle, inspecter rôles, noms et interactions ; capturer bureau et tailles/zoom supportés. Une fixture de test valide le mécanisme, pas le produit. Pour le shell Tauri inaccessible au navigateur, tester sa surface web représentative et nommer les fonctions natives non couvertes.
4. Exécuter les parcours principaux : formulaires, validations, menus, boutons, Tab/Shift+Tab, Entrée, Espace, Échap, focus des modales et restauration du focus. Vérifier chargement, vide, succès, erreurs et récupération avec des fixtures ou fautes contrôlées.
5. Inspecter chaque capture : débordements, chevauchement, alignement, densité, lisibilité et hiérarchie. Mesurer contrastes et tailles si nécessaire. Appliquer ux-writing-cleaner et web-design-guidelines au périmètre.
6. Classer les constats P0 (action critique impossible/perte de données), P1 (ergonomie/accessibilité importante), P2 (texte/cohérence/hiérarchie), P3 (finition). Rapporter écran, fichier:ligne si établi, reproduction, impact, preuve et correction proposée ; distinguer observation et hypothèse.
7. Pour « audit UX complet », parcourir tous les écrans accessibles du périmètre et signaler ceux inaccessibles. Ne corriger qu'une fois les corrections demandées ou autorisées. Pour une implémentation déjà autorisée, corriger ses défauts puis rejouer les scénarios et captures affectés.
8. Fermer uniquement les onglets et processus créés pour l'essai dans un finally, y compris sur échec ; vérifier leur arrêt. Sauver les preuves sous E:/OpenRPG_Forge/Preuves-et-analyses/actives/<campagne-stable>/ et le rapport sous E:/OpenRPG_Forge/Preuves-et-analyses/rapports/, conformément aux consignes de stockage. N'écrire aucune donnée sensible dans les captures.

Installation/connexion : voir docs/tooling/UX_UI_SYSTEM.md. Le test scripts/Test-UXPlaywright.mjs utilise le vrai protocole MCP sur une fixture éphémère pour vérifier navigateur, clavier, snapshot et capture ; il ne qualifie jamais le Studio.

Respecter AGENTS.md, la pile Rust/Leptos/Tauri et les conventions existantes.
Studio : apps/forge_studio/src/authoring_gold.css, authoring.css, asset_library.css,
asset_import.css et styles.css. Launcher : apps/forge_launcher/canonical-ui/.
Réutiliser les composants et tokens réels avant toute variante. Ne pas introduire
React, Tailwind, Three.js, bibliothèque d'icônes ou dépendance applicative pour
suivre un exemple externe. Préserver la gouvernance, les erreurs actionnables,
la sauvegarde et undo/redo. Une demande d'audit seule reste en lecture seule.
