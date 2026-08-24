# Studio UX10-A — Contexte, outils et fournisseurs

Qualification locale du 23 août 2026. Les cinq routes sont accessibles dans une surface Assistant
unique depuis la page **Contexte**, après sélection explicite du module.

## ORF-AI-001 — Assistant intégré au Toolset

La surface réunit sélection, configuration humaine, aperçu sec, approbation exacte, exécution en
sandbox, preuves lisibles et diagnostics. Elle ne montre pas d’UUID et ne modifie pas le module
source pendant la qualification.

## ORF-AI-002 — Contexte structuré du projet

Le paquet borné couvre les dix surfaces canoniques et respecte le plafond d’éléments choisi par
l’auteur.

## ORF-AI-005 — Outils sémantiques

Le catalogue expose cinq commandes métier typées avec cible et permission ; aucune commande shell
arbitraire n’est construite ou exécutée.

## ORF-AI-012 — Fournisseurs interchangeables

Le profil propose le fournisseur hors ligne déterministe, un endpoint OpenAI-compatible local et
Ollama local. Réseau local et partage du contexte restent deux consentements explicites distincts.

## ORF-AI-013 — Budget et confidentialité

Bornes de contexte et de graphe, niveau de permission, classification, opt-in cloud et politique de
secrets font partie de l’aperçu puis de l’approbation liée à l’empreinte exacte du profil.

## Validation

- build Studio Debug : 0 avertissement, 0 erreur ;
- smoke global : `OPENRPG_FORGE_STUDIO_UX10_PASS`, source inchangée, interface restaurée et sandbox
  supprimée ;
- tests canoniques UX10 cumulés : 66/66 ;
- capture Forward+ Full HD inspectée :
  `artifacts/verification/studio-increment/ux10-a-context-forward-plus-fhd.png` ;
- portail incrémental : PASS en 524,12 s, résumé
  `artifacts/verification/studio-increment/ux10-a-context.summary.json` ;
- graphe Roslyn : 11 566 nœuds, 90 143 relations, 0 diagnostic, frais et déterministe.

## Limites

Le test emploie uniquement le fournisseur déterministe hors ligne et des endpoints locaux décrits
par contrat. Il ne contacte aucun service IA, ne transmet aucun projet et ne qualifie ni coût réel,
ni latence, ni disponibilité d’un fournisseur externe.
