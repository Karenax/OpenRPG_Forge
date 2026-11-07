---
name: ux-writing-cleaner
description: "Auditer ou rédiger tout texte visible créé ou modifié dans le frontend, notamment libellés, boutons, aide, erreurs, états vides et notifications."
---

# Nettoyer les textes d'interface

Inspecter chaînes rendues dans view! Leptos, HTML et textes affectés au DOM, y compris messages dynamiques et traductions du périmètre. Ne pas assimiler logs, fixtures et codes internes à des textes visibles.

Pour chaque constat, donner fichier:ligne, texte, catégorie, proposition et raison. Si un label, nom accessible ou message nécessaire manque, le signaler comme essentiel manquant et proposer sa création, sans inventer son sens métier. Classer les textes existants :
- Essentiel : conserver.
- Utile mais trop long : simplifier.
- Technique : déplacer vers Diagnostics ou Administration.
- Redondant ou inutile : supprimer.

Nommer les actions par leur résultat et garder le même terme dans le bouton, la confirmation et l'aide. Employer la langue du projet et préserver les clés de traduction, variables et pluriels.
Exemples : « Cliquez ici pour créer un nouveau projet dans votre espace de travail » → « Nouveau projet » ; « Le processus de sauvegarde des données s'est terminé avec succès » → « Enregistré » ; « HTTP 200 / SYNC_COMPLETED » → « À jour » seulement si cet état est utile.
Expliquer une erreur par son impact et une action possible ; conserver avertissements, conséquences irréversibles et erreurs de sauvegarde. Ne pas retirer labels, noms accessibles ou aide nécessaire à la compréhension d'un champ. Ne pas raccourcir une instruction métier indispensable pour respecter un quota de mots.
Lancer python scripts/Check-UX.py sur les fichiers concernés ; traiter ses avertissements comme des pistes à vérifier, jamais des suppressions automatiques. En audit, présenter les corrections sans éditer ; en tâche d'implémentation autorisée, corriger puis relire dans l'écran.

Respecter AGENTS.md, la pile Rust/Leptos/Tauri et les conventions existantes.
Studio : apps/forge_studio/src/authoring_gold.css, authoring.css, asset_library.css,
asset_import.css et styles.css. Launcher : apps/forge_launcher/canonical-ui/.
Réutiliser les composants et tokens réels avant toute variante. Ne pas introduire
React, Tailwind, Three.js, bibliothèque d'icônes ou dépendance applicative pour
suivre un exemple externe. Préserver la gouvernance, les erreurs actionnables,
la sauvegarde et undo/redo. Une demande d'audit seule reste en lecture seule.
