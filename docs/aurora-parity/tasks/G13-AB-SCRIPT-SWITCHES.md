# G13-AB — Scripts : flags module et réglages IA

Features : `ORF-SCR-025`, `ORF-SCR-026`.

## Livré

Les flags ont nom, type, valeur par défaut, portée, documentation et version de migration. Les
réglages IA ont nom, type, valeurs autorisées, preview et autorité. Le résolveur normalise une
valeur invalide vers le défaut et retourne des dictionnaires en lecture seule.

## Preuves

- test : `G13AB_ScriptSwitches_UseTypedNamedDefaultsAndBoundedAiValues` ;
- Toolset/runtime : `G13AB_SCRIPT_SWITCHES_PASS` et `G13AB_SCRIPT_AUTOMATION_PASS`.

Limite : ces réglages configurent des comportements déclaratifs ; ils n'introduisent pas un nouveau
moteur IA ni une dépendance LLM.
