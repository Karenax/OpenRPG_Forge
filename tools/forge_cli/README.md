# forge_cli

Outil reproductible des documents Forge :

```text
forge_cli validate <scene-proof.json>
forge_cli hash <scene-proof.json>
forge_cli generate-proof <new-scene-proof.json>
```

`validate` parse et valide le schéma métier avant d'afficher son empreinte canonique. `hash` refuse
également un document invalide : une empreinte Forge ne certifie jamais des données non validées.
`generate-proof` produit l'exemple procédural canonique et refuse d'écraser un fichier existant.
