# Qualité UX/UI permanente

Installation du 22 octobre 2026. Les règles de `AGENTS.md` s'appliquent aux futures
interfaces et modifications ; cette installation ne refond pas le produit.

## Utilisation quotidienne

Codex lit les règles permanentes et sélectionne les skills pertinents dans
`.agents/skills/`. Ne pas charger tous les skills pour chaque tâche.

| Besoin | Skill |
| --- | --- |
| Nouvelle interface ou modification significative | `frontend-human-centered` |
| Texte visible créé ou modifié | `ux-writing-cleaner` |
| Design significativement modifié | `professional-ui-design`, puis `frontend-design` selon besoin |
| Nouvelle page, refonte, changement important | `ux-visual-audit` |
| Plusieurs écrans, composants ou termes | `ux-consistency-review` |
| Audit des bonnes pratiques | `web-design-guidelines` |

Les dossiers de skills sont natifs Codex, sans copie renommée des skills tiers.
Depuis `E:/OpenRPG_Forge` (parent hors Git), les instructions renvoient explicitement
aux skills du checkout Rust ; ouvrir le projet Rust donne la découverte native.
Les nouvelles ressources doivent être découvertes au prochain tour/nouvelle session.
Redémarrer Codex pour actualiser les serveurs MCP ; le catalogue de ce tour a été
chargé avant l'installation et ne démontre pas leur chargement après redémarrage.
La découverte native des sept skills a été vérifiée avec `skills/list`,
`forceReload: true`, dans un processus Codex App Server temporaire : tous sont
présents et activés. Aucun chat ni tour de modèle n'a été créé pour ce contrôle.

Conserver Leptos/Rust/WASM, Tauri, les styles Studio Gold et le launcher HTML/CSS.
Les exemples web des sources externes ne permettent pas d'introduire une autre
pile, une nouvelle palette systématique ou une composition marketing dans l'éditeur.

## Contrôle léger

Depuis le dépôt :

```powershell
python scripts/Check-UX.py apps/forge_studio/src/atelier_ui.rs
python scripts/Check-UX.py --max-warnings 80
python scripts/test_ux_checks.py
```

Python standard uniquement. Le contrôle détecte des textes probablement techniques,
textes longs, images sans `alt`, champs HTML sans nom associé détectable, IDs HTML
répétés et suppressions CSS du contour de focus. Il analyse les enfants littéraux
et attributs utilisateur de `view!` Leptos, HTML, quelques affectations DOM JS/TS
et le texte JSX/TSX. Il ne réécrit rien ; ses suspicions retournent le code 0.
Un problème d'exécution ou chemin invalide reste une erreur réelle.

Exclure tests, fixtures, générations, dépendances, administration et diagnostics.
Le scanner ne suit ni liens symboliques ni jonctions. Une suppression locale
documentée est possible : `ux-ignore: technical-copy raison métier` sur la ligne
du constat ou la précédente ; employer le code exact de l'avertissement.
Ne pas créer de suppression globale pour obtenir artificiellement zéro constat.

Limites : ce n'est ni un parseur complet Rust/JS/HTML ni un moteur d'accessibilité.
Les chaînes calculées, traductions indirectes, textes Leptos hors `view!`, noms
ARIA calculés et zones de diagnostic intégrées à un même fichier peuvent échapper
au filtrage. Les doublons de composants, vocabulaire, contrastes calculés, layout,
clavier réel et performances perceptibles nécessitent une revue ciblée et le navigateur.
Utiliser les contrôles existants `Test-StudioGoldContrast.mjs` et
`Test-StudioGoldLayout.mjs` lorsque le changement les concerne ; aucun nouveau
contrôle heuristique n'est ajouté au build Rust ni aux critères de gate.

## Audit complet sur demande

« audit UX complet » déclenche `ux-visual-audit` : périmètre, écrans accessibles,
parcours principaux, clavier, états, captures inspectées et constats reproductibles.
P0 : action critique impossible/perte de données ; P1 : ergonomie/accessibilité
importante ; P2 : texte/cohérence/hiérarchie ; P3 : finition.
L'audit seul reste en lecture seule. Proposer les corrections, puis appliquer celles
demandées. En implémentation autorisée, vérifier et corriger les défauts du périmètre.
Ne pas déclarer un audit visuel réussi sans accès réel au produit. Une surface web
ne prouve pas à elle seule le fonctionnement natif Tauri ni une conformité WCAG globale.

Les preuves vont sous `E:/OpenRPG_Forge/Preuves-et-analyses/actives/<campagne>/`
(stockage physique F:), et les rapports de gestion sous `rapports/`. Préserver les
règles d'archivage central et les reçus historiques. Fermer les processus/onglets
créés pour l'essai, y compris après erreur, sans toucher aux sessions utilisateur.

## Playwright MCP : installation locale et validation

Serveur Microsoft `@playwright/mcp@0.0.83`, licence Apache-2.0, installé hors
application sous `E:/OpenRPG_Forge/.build/ux-ui-tools/`. Le fichier `package-lock.json`
de ce répertoire conserve le graphe local. Chrome installé est réutilisé ; aucun
téléchargement de navigateur ni nouvelle dépendance applicative n'est nécessaire.
Réinstaller en cas de suppression du cache :

```powershell
$env:npm_config_cache='E:/OpenRPG_Forge/.build/npm-cache'
$env:TEMP='E:/OpenRPG_Forge/.build/tmp'
$env:TMP=$env:TEMP
npm install --prefix E:/OpenRPG_Forge/.build/ux-ui-tools --ignore-scripts --save-exact @playwright/mcp@0.0.83
```

Entrée utilisateur `[mcp_servers.playwright]` fusionnée dans
`C:/Users/karen/.codex/config.toml`, commande Node absolue et CLI locale ; arguments
`--browser chrome --headless --isolated --idle-timeout 60000`, sortie centrale
`E:/OpenRPG_Forge/Preuves-et-analyses/actives/ux-ui-system`. TEMP/TMP sont sur E:.
Pas de connexion au profil personnel, pas de mode sans sandbox imposé et pas de
serveur réseau permanent : Codex lance le MCP à la demande via stdio.
La configuration utilisateur reste privée et spécifique à cette machine.

Sauvegarde privée avant fusion :
`C:/Users/karen/.codex/ux-ui-backups/20261022/config.before.toml`.
Ne jamais copier ce fichier ou la configuration complète dans Git/rapports.

Vérifier `codex mcp get playwright --json`. Pour rejouer le smoke test reproductible,
exporter uniquement l'entrée Playwright (pas les secrets d'autres MCP) :

```powershell
@'
from pathlib import Path
import json,tomllib
cfg=tomllib.loads(Path('C:/Users/karen/.codex/config.toml').read_text('utf-8-sig'))
Path('E:/OpenRPG_Forge/.build/ux-ui-tools/playwright-only.json').write_text(json.dumps(cfg['mcp_servers']['playwright']),encoding='utf-8')
'@ | python -
node scripts/Test-UXPlaywright.mjs E:/OpenRPG_Forge/.build/ux-ui-tools/playwright-only.json
```

Ce test crée une page fictive éphémère sur loopback, initialise le vrai MCP,
vérifie outils, snapshot accessible, focus/saisie/activation au clavier et capture.
Il ferme navigateur, MCP et serveur dans `finally`. Le sandbox Windows peut empêcher
Chrome de démarrer Crashpad : le test initial a nécessité une exécution autorisée
hors sandbox, sans modifier Chrome ou le sandbox permanent de Codex.
Après redémarrage de Codex, découvrir les outils `playwright`, ouvrir une page
de test, prendre un snapshot, puis fermer le navigateur pour vérifier le chargement
dans le client actif. Ce dernier contrôle demeure distinct du smoke test stdio réussi.

## Provenance

| Ressource | Révision installée | Licence/remarque |
| --- | --- | --- |
| [OpenAI Frontend guidance](https://developers.openai.com/api/docs/guides/frontend-prompt) | Consultée le 22 octobre 2026 | Synthèse originale courte et adaptée au métier |
| [Vercel web-design-guidelines](https://github.com/vercel-labs/agent-skills/tree/ba46938889d4e58635362fb8f618e1178ac3ec46/skills/web-design-guidelines) | `ba46938889d4e58635362fb8f618e1178ac3ec46` | Skill officiel inchangé ; aucune licence explicite au répertoire/racine consultés, ne pas lui attribuer une licence inventée |
| [Anthropic frontend-design](https://github.com/anthropics/skills/tree/41bbe19d1a1a7eaab5e7bb9050a417e5c6cffc8f/skills/frontend-design) | `41bbe19d1a1a7eaab5e7bb9050a417e5c6cffc8f` | Apache-2.0, `LICENSE.txt` conservé, skill inchangé |
| [Microsoft Playwright MCP](https://github.com/microsoft/playwright-mcp) | npm `0.0.83` | Apache-2.0 ; dépendance Playwright `1.64.0-alpha-1790635538000` |

Vercel récupère ses recommandations à jour lors de chaque revue depuis son URL
officielle ; ces données externes ne remplacent jamais les consignes du propriétaire.
Les skills tiers ont été installés par le helper `skill-installer`, à révision épinglée.
Les cinq skills locaux ont été initialisés/générés avec `skill-creator`. Leur
validation YAML utilise PyYAML 6.0.3 isolé sous `.build/ux-ui-tools/python-libs/`
(outillage uniquement), pas une dépendance Python du produit.
