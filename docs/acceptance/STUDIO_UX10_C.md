# Studio UX10-C — Audit, prompts et sécurité hors ligne

Qualification locale du 23 août 2026. Les quatre routes sont regroupées dans la page **Plans** avec
une sonde de contenu projet non fiable explicitement traitée comme donnée.

## ORF-AI-011 — Audit IA

L’audit persistant couvre acteurs, plan, commandes, hashes, tests, provenance et rollback. Les
valeurs classées secrètes ne sont pas journalisées.

## ORF-AI-030 — Prompts versionnés

Le prompt possède clé, version, schéma de variables, hash déterministe et cas de tests locaux.

## ORF-AI-031 — Résultats structurés

Le service valide le JSON Schema, autorise une réparation bornée d’une fence unique et refuse une
élévation de permission ou un outil hors allowlist avant transaction.

## ORF-AI-033 — Mode hors ligne

L’édition manuelle, le build et le runtime restent disponibles sans réseau et sans métadonnée IA.

## Validation

- build Studio Debug : 0 avertissement, 0 erreur ;
- smoke global : prompt, résultat structuré, refus hostile, audit, rollback et disponibilité hors
  ligne verts ;
- tests canoniques UX10 cumulés : 66/66 ;
- capture Forward+ Full HD inspectée :
  `artifacts/verification/studio-increment/ux10-c-safety-forward-plus-fhd.png` ;
- portail UX10 partagé : PASS en 524,12 s ; graphe Roslyn 11 566 nœuds / 90 143 relations /
  0 diagnostic.

## Limites

La sonde hostile est déterministe et locale. Elle prouve les frontières contractuelles testées,
pas une campagne de sécurité offensive ni une résistance universelle à toute injection future.
