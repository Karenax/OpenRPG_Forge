# G13-S — Triggers : automatisation, narration et GM

Features : `ORF-TRG-009`, `ORF-TRG-010`, `ORF-TRG-015`, `ORF-TRG-016`, `ORF-TRG-018`.

## Livré

Heartbeat occupé/budgété, événement custom namespacé, piste avec trace et quête, interjection avec
participants/priorité/cooldown et déclenchement manuel GM utilisent un contrat unique. L'action GM
est explicitement autorisée et auditée ; elle ne contourne pas l'autorité.

## Preuves

- test : `G13S_TriggerAutomationNarrativeDirector_ProducesAuditedEffects` ;
- Toolset/runtime : `G13S_TRIGGER_AUTOMATION_NARRATIVE_GM_PASS` et
  `G13S_TRIGGER_DIRECTOR_PASS`.

Limite : le Live Director transporte la commande et conserve son journal séparé.
