using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using Forge.Application.Commands;
using Forge.Application.Build;
using Forge.Application.Content;
using Forge.Application.Assistant;
using Forge.Application.Creator;
using Forge.Application.Assets;
using Forge.Application.Areas;
using Forge.Application.Blueprints;
using Forge.Application.Dialogues;
using Forge.Application.Entities;
using Forge.Application.Factions;
using Forge.Application.Projects;
using Forge.Application.Quality;
using Forge.Application.Quests;
using Forge.Application.Time;
using Forge.Application.Validation;
using Forge.Application.WorldFeatures;
using Forge.Application.Modules;
using Forge.Application.Localization;
using Forge.Application.Navigation;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Audio;
using Forge.Domain.Assistant;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.Domain.Build;
using Forge.Domain.Creatures;
using Forge.Domain.Content;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Factions;
using Forge.Domain.Items;
using Forge.Domain.Lifecycle;
using Forge.Domain.Modules;
using Forge.Domain.Navigation;
using Forge.Domain.Localization;
using Forge.Domain.Quests;
using Forge.Domain.Quality;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Rules;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.Mcp.Abilities;
using Forge.Mcp.Assistant;
using Forge.Mcp.Build;
using Forge.Mcp.Dialogues;
using Forge.Mcp.Bosses;
using Forge.Mcp.Quests;
using Forge.Mcp.Assets;
using Forge.Mcp.Creatures;
using Forge.Mcp.Factions;
using Forge.Mcp.Items;
using Forge.Mcp.Navigation;
using Forge.Mcp.Quality;
using Forge.Mcp.Scripts;
using Forge.Mcp.WorldFeatures;
using Forge.RpgKit.Application.Abilities;
using Forge.RpgKit.Application.Dialogues;
using Forge.RpgKit.Application.Bosses;
using Forge.RpgKit.Application.Quests;
using Forge.RpgKit.Domain.Doors;
using Forge.ModuleFormat.Workspaces;
using Forge.ModuleFormat.Quality;
using Forge.Scripting;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Documentation;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Content;
using Forge.ModuleFormat.Serialization;

namespace Forge.Mcp.Projects;


public sealed partial class ForgeProjectAgentService
{
    public AssistedDialogueProposalResource ProposeAssistedDialogue(AssistedDialogueProposalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SchemaVersion != 1)
            return new(false, null,
                [new AgentDiagnostic("assistant.dialogue.request_invalid",
                    "Assisted dialogue proposal schema is invalid.", "assistant/dialogue")]);
        var snapshot = CaptureAssistedProposalSnapshot();
        try
        {
            var profile = AssistantWorkspaceProfile.CreateDefault();
            var proposal = AssistedDialogueCreationService.ProposeAsync(
                request.Brief,
                snapshot.Project,
                snapshot.LoreIndex,
                new DeterministicAssistantProvider(profile.ActiveProvider),
                profile.ActiveProvider,
                networkAuthorized: false,
                projectContentAuthorized: false).GetAwaiter().GetResult();
            return new(proposal.Succeeded, proposal,
                proposal.Lint.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
        }
        catch (ArgumentException exception)
        {
            return new(false, null,
                [new AgentDiagnostic("assistant.dialogue.brief_invalid", exception.Message, "assistant/dialogue")]);
        }
    }

    public AssistedDialogueApplyPlanResult PlanAssistedDialogue(AssistedDialogueApplyPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1 || request.BaseRevision < 0)
                return AssistedDialogueFailure(_history.State.Revision.Value,
                    "assistant.dialogue.request_invalid", "Assisted dialogue request schema or revision is invalid.");
            var transactionId = Guid.NewGuid();
            var executionPlanId = Guid.NewGuid();
            var command = new ApplyAssistedDialogueCommand(
                CommandId.From(Guid.NewGuid()), transactionId, executionPlanId,
                new ProjectRevision(request.BaseRevision), request.Proposal, request.Permission, DryRun: true);
            var preview = _assistedDialogueHandler.Handle(_history.State, command);
            if (!preview.Succeeded || preview.Command.Patch is null || preview.Command.Inverse is null)
                return new(false, null, transactionId.ToString("D"), request.Proposal.Dialogue.Id.ToString(),
                    request.BaseRevision, null, null, "no-change", request.Proposal.Citations,
                    request.Proposal.LocalizationPlaceholders, request.Proposal.Lint.Select(item => item.Code).ToArray(),
                    request.Proposal.EstimatedCostUsd, preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            var key = executionPlanId.ToString("D");
            var stored = new StoredAssistedDialoguePlan(
                key,
                command,
                preview.Command.Patch,
                preview.Command.Inverse,
                [new PlanAuditEntry(key, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _assistedDialoguePlans.Add(key, stored);
            return ToResult(stored);
        }
    }

    public AssistedDialogueApplyPlanResult ApplyAssistedDialoguePlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_assistedDialoguePlans.TryGetValue(planId, out var plan))
                return AssistedDialogueFailure(_history.State.Revision.Value,
                    "assistant.dialogue.plan_missing", $"Assisted dialogue plan '{planId}' does not exist.");
            if (plan.Applied)
                return AssistedDialogueFailure(plan.Command.BaseRevision.Value,
                    "assistant.dialogue.plan_already_applied", $"Assisted dialogue plan '{planId}' is already applied.");
            var result = _assistedDialogueHandler.Handle(
                _history.State,
                plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(plan.PlanId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return WithFailure(plan, result.Diagnostics.Select(Map).ToArray());
            }
            var saved = _session.Save(result.Command.State.Project);
            if (!saved.Succeeded)
            {
                plan.Audit.Add(new(plan.PlanId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return WithFailure(plan, saved.Diagnostics.Select(item =>
                    new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }
            _history.Record(result.Command);
            plan.Applied = true;
            plan.AppliedRevision = _history.State.Revision.Value;
            plan.Audit.Add(new(plan.PlanId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AssistedDialogueApplyPlanResult RevertAssistedDialoguePlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_assistedDialoguePlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return AssistedDialogueFailure(_history.State.Revision.Value,
                    "assistant.dialogue.plan_not_applied", $"Assisted dialogue plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return AssistedDialogueFailure(plan.Command.BaseRevision.Value,
                    "assistant.dialogue.revert_conflict", "Only the latest project mutation can be reverted safely.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return AssistedDialogueFailure(plan.Command.BaseRevision.Value,
                    "assistant.dialogue.inverse_unavailable", "The assisted dialogue inverse is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return WithFailure(plan, saved.Diagnostics.Select(item =>
                    new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }
            plan.Reverted = true;
            plan.Audit.Add(new(plan.PlanId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AssistedQuestProposalResource ProposeAssistedQuest(AssistedQuestProposalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SchemaVersion != 1)
            return new(false, null,
                [new AgentDiagnostic("assistant.quest.request_invalid",
                    "Assisted quest proposal schema is invalid.", "assistant/quest")]);
        var snapshot = CaptureAssistedProposalSnapshot();
        try
        {
            var profile = AssistantWorkspaceProfile.CreateDefault();
            var proposal = AssistedQuestCreationService.ProposeAsync(
                request.Brief,
                snapshot.Project,
                snapshot.LoreIndex,
                new DeterministicAssistantProvider(profile.ActiveProvider),
                profile.ActiveProvider,
                networkAuthorized: false,
                projectContentAuthorized: false).GetAwaiter().GetResult();
            return new(proposal.Succeeded, proposal,
                proposal.Lint.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
        }
        catch (ArgumentException exception)
        {
            return new(false, null,
                [new AgentDiagnostic("assistant.quest.brief_invalid", exception.Message, "assistant/quest")]);
        }
    }

    public AssistedQuestApplyPlanResult PlanAssistedQuestDomain(AssistedQuestDomainApplyPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1 || request.BaseRevision < 0)
                return AssistedQuestFailure(_history.State.Revision.Value,
                    "assistant.quest.request_invalid", "Assisted quest request schema or revision is invalid.");
            var transactionId = Guid.NewGuid();
            var executionPlanId = Guid.NewGuid();
            var command = new ApplyAssistedQuestCommand(
                CommandId.From(Guid.NewGuid()), transactionId, executionPlanId,
                new ProjectRevision(request.BaseRevision), request.Proposal, request.Permission, DryRun: true);
            var preview = _assistedQuestHandler.Handle(_history.State, command);
            if (!preview.Succeeded || preview.Command.Patch is null || preview.Command.Inverse is null)
                return new(false, null, transactionId.ToString("D"), request.Proposal.Quest.Id.ToString(),
                    request.BaseRevision, null, null, "no-change", request.Proposal.Citations,
                    request.Proposal.Lint.Select(item => item.Code).ToArray(),
                    request.Proposal.TestScenarios.Select(item => item.Kind.ToString()).ToArray(),
                    request.Proposal.EstimatedCostUsd, preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            var key = executionPlanId.ToString("D");
            var stored = new StoredAssistedQuestPlan(
                key,
                command,
                preview.Command.Patch,
                preview.Command.Inverse,
                [new PlanAuditEntry(key, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _assistedQuestPlans.Add(key, stored);
            return ToResult(stored);
        }
    }

    public AssistedQuestApplyPlanResult PlanAssistedQuest(AssistedQuestApplyPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (!QuestJsonBoundaryMapper.TryMapAssisted(
                    _history.State.Project, request, out var mapped, out var diagnostic))
                return new(false, null, null, null, request.BaseRevision, null, null, "no-change",
                    [], [], [], 0, [diagnostic!], false, false, []);
            return PlanAssistedQuestDomain(mapped!);
        }
    }

    public AssistedQuestApplyPlanResult ApplyAssistedQuestPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_assistedQuestPlans.TryGetValue(planId, out var plan))
                return AssistedQuestFailure(_history.State.Revision.Value,
                    "assistant.quest.plan_missing", $"Assisted quest plan '{planId}' does not exist.");
            if (plan.Applied)
                return AssistedQuestFailure(plan.Command.BaseRevision.Value,
                    "assistant.quest.plan_already_applied", $"Assisted quest plan '{planId}' is already applied.");
            var result = _assistedQuestHandler.Handle(
                _history.State,
                plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(plan.PlanId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return WithFailure(plan, result.Diagnostics.Select(Map).ToArray());
            }
            var saved = _session.Save(result.Command.State.Project);
            if (!saved.Succeeded)
            {
                plan.Audit.Add(new(plan.PlanId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return WithFailure(plan, saved.Diagnostics.Select(item =>
                    new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }
            _history.Record(result.Command);
            plan.Applied = true;
            plan.AppliedRevision = _history.State.Revision.Value;
            plan.Audit.Add(new(plan.PlanId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AssistedQuestApplyPlanResult RevertAssistedQuestPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_assistedQuestPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return AssistedQuestFailure(_history.State.Revision.Value,
                    "assistant.quest.plan_not_applied", $"Assisted quest plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return AssistedQuestFailure(plan.Command.BaseRevision.Value,
                    "assistant.quest.revert_conflict", "Only the latest project mutation can be reverted safely.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return AssistedQuestFailure(plan.Command.BaseRevision.Value,
                    "assistant.quest.inverse_unavailable", "The assisted quest inverse is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return WithFailure(plan, saved.Diagnostics.Select(item =>
                    new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }
            plan.Reverted = true;
            plan.Audit.Add(new(plan.PlanId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AssistedBossProposalResource ProposeAssistedBoss(AssistedBossProposalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SchemaVersion != 1)
            return new(false, null,
                [new AgentDiagnostic("assistant.boss.request_invalid",
                    "Assisted boss proposal schema is invalid.", "assistant/boss")]);
        var snapshot = CaptureAssistedProposalSnapshot();
        try
        {
            var profile = AssistantWorkspaceProfile.CreateDefault();
            var proposal = AssistedBossCreationService.ProposeAsync(
                request.Brief,
                snapshot.Project,
                snapshot.LoreIndex,
                AvailableForgeScripts(snapshot.RootDirectory),
                new DeterministicAssistantProvider(profile.ActiveProvider),
                profile.ActiveProvider,
                networkAuthorized: false,
                projectContentAuthorized: false).GetAwaiter().GetResult();
            return new(proposal.Succeeded, proposal,
                proposal.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
        }
        catch (ArgumentException exception)
        {
            return new(false, null,
                [new AgentDiagnostic("assistant.boss.brief_invalid", exception.Message, "assistant/boss")]);
        }
    }

    public AssistedBossApplyPlanResult PlanAssistedBoss(AssistedBossApplyPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1 || request.BaseRevision < 0)
                return AssistedBossFailure(_history.State.Revision.Value,
                    "assistant.boss.request_invalid", "Assisted boss request schema or revision is invalid.");
            var transactionId = Guid.NewGuid();
            var executionPlanId = Guid.NewGuid();
            var command = new ApplyAssistedBossCommand(
                CommandId.From(Guid.NewGuid()), transactionId, executionPlanId,
                new ProjectRevision(request.BaseRevision), request.Proposal, request.Permission, DryRun: true);
            var preview = _assistedBossHandler.Handle(_history.State, command);
            if (!preview.Succeeded || preview.Command.Patch is null || preview.Command.Inverse is null)
                return new(false, null, transactionId.ToString("D"), request.Proposal.Boss.Id.ToString("D"),
                    request.BaseRevision, null, null, "no-change", request.Proposal.Citations,
                    request.Proposal.Diagnostics.Select(item => item.Code).ToArray(),
                    request.Proposal.Balance.Runs, request.Proposal.Balance.Passed,
                    request.Proposal.EstimatedCostUsd, preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            var key = executionPlanId.ToString("D");
            var stored = new StoredAssistedBossPlan(key, command, preview.Command.Patch, preview.Command.Inverse,
                [new PlanAuditEntry(key, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _assistedBossPlans.Add(key, stored);
            return ToResult(stored);
        }
    }

    public AssistedBossApplyPlanResult ApplyAssistedBossPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_assistedBossPlans.TryGetValue(planId, out var plan))
                return AssistedBossFailure(_history.State.Revision.Value,
                    "assistant.boss.plan_missing", $"Assisted boss plan '{planId}' does not exist.");
            if (plan.Applied)
                return AssistedBossFailure(plan.Command.BaseRevision.Value,
                    "assistant.boss.plan_already_applied", $"Assisted boss plan '{planId}' is already applied.");
            var result = _assistedBossHandler.Handle(_history.State,
                plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(plan.PlanId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return WithFailure(plan, result.Diagnostics.Select(Map).ToArray());
            }
            var saved = _session.Save(result.Command.State.Project);
            if (!saved.Succeeded)
            {
                plan.Audit.Add(new(plan.PlanId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return WithFailure(plan, saved.Diagnostics.Select(item =>
                    new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }
            _history.Record(result.Command);
            plan.Applied = true;
            plan.AppliedRevision = _history.State.Revision.Value;
            plan.Audit.Add(new(plan.PlanId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AssistedBossApplyPlanResult RevertAssistedBossPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_assistedBossPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return AssistedBossFailure(_history.State.Revision.Value,
                    "assistant.boss.plan_not_applied", $"Assisted boss plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return AssistedBossFailure(plan.Command.BaseRevision.Value,
                    "assistant.boss.revert_conflict", "Only the latest project mutation can be reverted safely.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return AssistedBossFailure(plan.Command.BaseRevision.Value,
                    "assistant.boss.inverse_unavailable", "The assisted boss inverse is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return WithFailure(plan, saved.Diagnostics.Select(item =>
                    new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }
            plan.Reverted = true;
            plan.Audit.Add(new(plan.PlanId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AssistedAbilityProposalResource ProposeAssistedAbility(AssistedAbilityProposalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SchemaVersion != 1)
            return new(false, null,
                [new AgentDiagnostic("assistant.ability.request_invalid",
                    "Assisted ability proposal schema is invalid.", "assistant/ability")]);
        var snapshot = CaptureAssistedProposalSnapshot();
        try
        {
            var profile = AssistantWorkspaceProfile.CreateDefault();
            var proposal = AssistedAbilityCreationService.ProposeAsync(
                request.Brief,
                snapshot.Project,
                snapshot.LoreIndex,
                AvailableForgeScripts(snapshot.RootDirectory),
                new DeterministicAssistantProvider(profile.ActiveProvider),
                profile.ActiveProvider,
                networkAuthorized: false,
                projectContentAuthorized: false).GetAwaiter().GetResult();
            return new(proposal.Succeeded, proposal,
                proposal.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
        }
        catch (ArgumentException exception)
        {
            return new(false, null,
                [new AgentDiagnostic("assistant.ability.brief_invalid", exception.Message, "assistant/ability")]);
        }
    }

    public AssistedAbilityApplyPlanResult PlanAssistedAbility(AssistedAbilityApplyPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1 || request.BaseRevision < 0)
                return AssistedAbilityFailure(_history.State.Revision.Value,
                    "assistant.ability.request_invalid", "Assisted ability request schema or revision is invalid.");
            var transactionId = Guid.NewGuid();
            var executionPlanId = Guid.NewGuid();
            var command = new ApplyAssistedAbilityCommand(
                CommandId.From(Guid.NewGuid()), transactionId, executionPlanId,
                new ProjectRevision(request.BaseRevision), request.Proposal, request.Permission, DryRun: true);
            var preview = _assistedAbilityHandler.Handle(_history.State, command);
            if (!preview.Succeeded || preview.Command.Patch is null || preview.Command.Inverse is null)
                return new(false, null, transactionId.ToString("D"),
                    request.Proposal.Orchestration.Id.ToString("D"),
                    request.Proposal.Orchestration.OwnerBlueprintId.ToString(), request.BaseRevision,
                    null, null, "no-change", request.Proposal.Citations,
                    request.Proposal.Diagnostics.Select(item => item.Code).ToArray(),
                    request.Proposal.Tests.Select(item => item.Kind.ToString()).ToArray(),
                    request.Proposal.Tests.Count(item => item.Passed), request.Proposal.Balance.Passed,
                    request.Proposal.Balance.PowerScore, request.Proposal.EstimatedCostUsd,
                    preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            var key = executionPlanId.ToString("D");
            var stored = new StoredAssistedAbilityPlan(key, command, preview.Command.Patch, preview.Command.Inverse,
                [new PlanAuditEntry(key, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _assistedAbilityPlans.Add(key, stored);
            return ToResult(stored);
        }
    }

    public AssistedAbilityApplyPlanResult ApplyAssistedAbilityPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_assistedAbilityPlans.TryGetValue(planId, out var plan))
                return AssistedAbilityFailure(_history.State.Revision.Value,
                    "assistant.ability.plan_missing", $"Assisted ability plan '{planId}' does not exist.");
            if (plan.Applied)
                return AssistedAbilityFailure(plan.Command.BaseRevision.Value,
                    "assistant.ability.plan_already_applied", $"Assisted ability plan '{planId}' is already applied.");
            var result = _assistedAbilityHandler.Handle(_history.State,
                plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(plan.PlanId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return WithFailure(plan, result.Diagnostics.Select(Map).ToArray());
            }
            var saved = _session.Save(result.Command.State.Project);
            if (!saved.Succeeded)
            {
                plan.Audit.Add(new(plan.PlanId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return WithFailure(plan, saved.Diagnostics.Select(item =>
                    new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }
            _history.Record(result.Command);
            plan.Applied = true;
            plan.AppliedRevision = _history.State.Revision.Value;
            plan.Audit.Add(new(plan.PlanId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AssistedAbilityApplyPlanResult RevertAssistedAbilityPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_assistedAbilityPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return AssistedAbilityFailure(_history.State.Revision.Value,
                    "assistant.ability.plan_not_applied", $"Assisted ability plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return AssistedAbilityFailure(plan.Command.BaseRevision.Value,
                    "assistant.ability.revert_conflict", "Only the latest project mutation can be reverted safely.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return AssistedAbilityFailure(plan.Command.BaseRevision.Value,
                    "assistant.ability.inverse_unavailable", "The assisted ability inverse is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return WithFailure(plan, saved.Diagnostics.Select(item =>
                    new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }
            plan.Reverted = true;
            plan.Audit.Add(new(plan.PlanId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AssistedBlenderCreationProposalResource ProposeAssistedBlenderCreation(
        AssistedBlenderCreationProposalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var snapshot = CaptureAssistedProposalSnapshot();
        if (request.SchemaVersion != 1)
            return new(false, snapshot.AssetRevision, null,
                [new AgentDiagnostic("assistant.blender.request_invalid",
                    "Assisted Blender proposal schema is invalid.", "assistant/blender")]);
        try
        {
            var profile = AssistantWorkspaceProfile.CreateDefault();
            var proposal = AssistedBlenderCreationService.ProposeAsync(
                request.Brief, snapshot.RootDirectory, snapshot.Project,
                snapshot.LoreIndex, new DeterministicAssistantProvider(profile.ActiveProvider),
                profile.ActiveProvider, networkAuthorized: false,
                projectContentAuthorized: false).GetAwaiter().GetResult();
            return new(proposal.Succeeded, snapshot.AssetRevision, proposal,
                proposal.Diagnostics.Select(code =>
                    new AgentDiagnostic(code, code, $"assets/assisted-3d/{proposal.Definition.Id:D}")).ToArray());
        }
        catch (ArgumentException exception)
        {
            return new(false, snapshot.AssetRevision, null,
                [new AgentDiagnostic("assistant.blender.brief_invalid", exception.Message, "assistant/blender")]);
        }
    }

    public AssistedBlenderCreationApplyPlanResult PlanAssistedBlenderCreation(
        AssistedBlenderCreationApplyPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1 || request.BaseRevision < 0)
                return AssistedBlenderFailure(_assistedBlenderState.Revision.Value,
                    "assistant.blender.request_invalid", "Assisted Blender request schema or revision is invalid.");
            var transactionId = Guid.NewGuid();
            var executionPlanId = Guid.NewGuid();
            var command = new ApplyAssistedBlenderCreationCommand(
                CommandId.From(Guid.NewGuid()), transactionId, executionPlanId,
                new AssetExternalEditRevision(request.BaseRevision), request.Proposal, request.Permission, DryRun: true);
            var preview = _assistedBlenderCreationHandler.Handle(_session.RootDirectory, _assistedBlenderState, command);
            if (!preview.Succeeded)
                return new(false, null, transactionId.ToString("D"), request.Proposal.Definition.Id.ToString("D"),
                    request.Proposal.Configuration.AssetId.ToString(), request.BaseRevision, preview.Patches,
                    preview.SemanticDiff, request.Proposal.RecipeRelativePath,
                    request.Proposal.Definition.OutputRelativePath, request.Proposal.Citations,
                    request.Proposal.Diagnostics, request.Proposal.Tests.Select(item => item.Id).ToArray(),
                    request.Proposal.Tests.Count(item => item.Passed), request.Proposal.EstimatedCostUsd,
                    preview.Diagnostics.Select(code => new AgentDiagnostic(code, code, "assistant/blender")).ToArray(),
                    false, false, []);
            var key = executionPlanId.ToString("D");
            var stored = new StoredAssistedBlenderCreationPlan(key, command, preview.Patches,
                _assistedBlenderState.Configurations,
                [new PlanAuditEntry(key, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _assistedBlenderCreationPlans.Add(key, stored);
            return ToResult(stored);
        }
    }

    public AssistedBlenderCreationApplyPlanResult ApplyAssistedBlenderCreationPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_assistedBlenderCreationPlans.TryGetValue(planId, out var plan))
                return AssistedBlenderFailure(_assistedBlenderState.Revision.Value,
                    "assistant.blender.plan_missing", $"Assisted Blender plan '{planId}' does not exist.");
            if (plan.Applied)
                return AssistedBlenderFailure(plan.Command.BaseRevision.Value,
                    "assistant.blender.plan_already_applied", $"Assisted Blender plan '{planId}' is already applied.");
            var result = _assistedBlenderCreationHandler.Handle(_session.RootDirectory, _assistedBlenderState,
                plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(plan.PlanId, "apply", _assistedBlenderState.Revision.Value, _clock.UtcNow, false));
                return WithFailure(plan,
                    result.Diagnostics.Select(code => new AgentDiagnostic(code, code, "assistant/blender")).ToArray());
            }
            _assistedBlenderState = result.State;
            plan.FileDiff = result.Patches;
            plan.Applied = true;
            plan.AppliedRevision = _assistedBlenderState.Revision.Value;
            plan.Audit.Add(new(plan.PlanId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AssistedBlenderCreationApplyPlanResult RevertAssistedBlenderCreationPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_assistedBlenderCreationPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return AssistedBlenderFailure(_assistedBlenderState.Revision.Value,
                    "assistant.blender.plan_not_applied", $"Assisted Blender plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _assistedBlenderState.Revision.Value)
                return AssistedBlenderFailure(plan.Command.BaseRevision.Value,
                    "assistant.blender.revert_conflict", "Only the latest assisted Blender mutation can be reverted safely.");
            try
            {
                ApplyAssistedBlenderCreationHandler.Restore(_session.RootDirectory, plan.FileDiff, useAfter: false);
                _assistedBlenderState = new AssetExternalEditState(
                    plan.BeforeConfigurations, _assistedBlenderState.Revision.Next());
                plan.Reverted = true;
                plan.Audit.Add(new(plan.PlanId, "revert", _assistedBlenderState.Revision.Value, _clock.UtcNow, true));
                return ToResult(plan);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                plan.Audit.Add(new(plan.PlanId, "revert", _assistedBlenderState.Revision.Value, _clock.UtcNow, false));
                return WithFailure(plan,
                    [new AgentDiagnostic("assistant.blender.revert_failed", exception.Message, "assistant/blender")]);
            }
        }
    }

    public AssistedModel3DGenerationProposalResource ProposeAssistedModel3DGeneration(
        AssistedModel3DGenerationProposalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var snapshot = CaptureAssistedProposalSnapshot();
        if (request.SchemaVersion != 1)
            return new(false, snapshot.AssetRevision, null,
                [new AgentDiagnostic("assistant.model3d.request_invalid",
                    "Assisted model 3D proposal schema is invalid.", "assistant/model3d")]);
        try
        {
            IModel3DGenerationProvider provider = request.Brief.Provider.Kind == Model3DProviderKind.DeterministicOffline
                ? new DeterministicModel3DGenerationProvider()
                : new LoopbackModel3DGenerationProvider(request.Brief.Provider, _model3DHttpClient,
                    Environment.GetEnvironmentVariable);
            var proposal = AssistedModel3DGenerationService.ProposeAsync(
                request.Brief, snapshot.RootDirectory, provider, request.NetworkAuthorized)
                .GetAwaiter().GetResult();
            return new(proposal.Succeeded, snapshot.AssetRevision, proposal,
                proposal.Diagnostics.Select(code => new AgentDiagnostic(code, code,
                    $"assets/assisted-provider-3d/{proposal.Receipt.Id:D}")).ToArray());
        }
        catch (Model3DProviderException exception)
        {
            return new(false, snapshot.AssetRevision, null,
                [new AgentDiagnostic(exception.Code, exception.Message, "assistant/model3d")]);
        }
        catch (ArgumentException exception)
        {
            return new(false, snapshot.AssetRevision, null,
                [new AgentDiagnostic("assistant.model3d.brief_invalid", exception.Message, "assistant/model3d")]);
        }
    }

    public AssistedModel3DGenerationApplyPlanResult PlanAssistedModel3DGeneration(
        AssistedModel3DGenerationApplyPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1 || request.BaseRevision < 0)
                return AssistedModel3DFailure(_assistedBlenderState.Revision.Value,
                    "assistant.model3d.request_invalid", "Assisted model 3D request schema or revision is invalid.");
            var transactionId = Guid.NewGuid();
            var executionPlanId = Guid.NewGuid();
            var command = new ApplyAssistedModel3DGenerationCommand(
                CommandId.From(Guid.NewGuid()), transactionId, executionPlanId,
                new AssetExternalEditRevision(request.BaseRevision), request.Proposal, request.Permission, DryRun: true);
            var preview = _assistedModel3DGenerationHandler.Handle(
                _session.RootDirectory, _assistedBlenderState, command);
            if (!preview.Succeeded)
                return new(false, null, transactionId.ToString("D"), request.Proposal.Receipt.Id.ToString("D"),
                    request.Proposal.Configuration.AssetId.ToString(), request.BaseRevision, preview.Patches,
                    preview.SemanticDiff, request.Proposal.Receipt.SourceRelativePath,
                    request.Proposal.ReceiptRelativePath, request.Proposal.Receipt.OutputRelativePath,
                    request.Proposal.Citations, request.Proposal.Diagnostics,
                    request.Proposal.Tests.Select(item => item.Id).ToArray(),
                    request.Proposal.Tests.Count(item => item.Passed), request.Proposal.EstimatedCostUsd,
                    request.Proposal.Receipt.Provider.Key, request.Proposal.Receipt.LicenseSpdx,
                    request.Proposal.Receipt.ProviderProvenance,
                    preview.Diagnostics.Select(code => new AgentDiagnostic(code, code, "assistant/model3d")).ToArray(),
                    false, false, []);
            var key = executionPlanId.ToString("D");
            var stored = new StoredAssistedModel3DGenerationPlan(key, command, preview.Patches,
                _assistedBlenderState.Configurations,
                [new PlanAuditEntry(key, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _assistedModel3DGenerationPlans.Add(key, stored);
            return ToResult(stored);
        }
    }

    public AssistedModel3DGenerationApplyPlanResult ApplyAssistedModel3DGenerationPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_assistedModel3DGenerationPlans.TryGetValue(planId, out var plan))
                return AssistedModel3DFailure(_assistedBlenderState.Revision.Value,
                    "assistant.model3d.plan_missing", $"Assisted model 3D plan '{planId}' does not exist.");
            if (plan.Applied)
                return AssistedModel3DFailure(plan.Command.BaseRevision.Value,
                    "assistant.model3d.plan_already_applied", $"Assisted model 3D plan '{planId}' is already applied.");
            var result = _assistedModel3DGenerationHandler.Handle(_session.RootDirectory, _assistedBlenderState,
                plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(plan.PlanId, "apply", _assistedBlenderState.Revision.Value, _clock.UtcNow, false));
                return WithFailure(plan,
                    result.Diagnostics.Select(code => new AgentDiagnostic(code, code, "assistant/model3d")).ToArray());
            }
            _assistedBlenderState = result.State;
            plan.FileDiff = result.Patches;
            plan.Applied = true;
            plan.AppliedRevision = _assistedBlenderState.Revision.Value;
            plan.Audit.Add(new(plan.PlanId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AssistedModel3DGenerationApplyPlanResult RevertAssistedModel3DGenerationPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_assistedModel3DGenerationPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return AssistedModel3DFailure(_assistedBlenderState.Revision.Value,
                    "assistant.model3d.plan_not_applied", $"Assisted model 3D plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _assistedBlenderState.Revision.Value)
                return AssistedModel3DFailure(plan.Command.BaseRevision.Value,
                    "assistant.model3d.revert_conflict", "Only the latest assisted model 3D mutation can be reverted safely.");
            try
            {
                ApplyAssistedModel3DGenerationHandler.Restore(_session.RootDirectory, plan.FileDiff, useAfter: false);
                _assistedBlenderState = new AssetExternalEditState(
                    plan.BeforeConfigurations, _assistedBlenderState.Revision.Next());
                plan.Reverted = true;
                plan.Audit.Add(new(plan.PlanId, "revert", _assistedBlenderState.Revision.Value, _clock.UtcNow, true));
                return ToResult(plan);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                plan.Audit.Add(new(plan.PlanId, "revert", _assistedBlenderState.Revision.Value, _clock.UtcNow, false));
                return WithFailure(plan,
                    [new AgentDiagnostic("assistant.model3d.revert_failed", exception.Message, "assistant/model3d")]);
            }
        }
    }

    public ExternalWorkerSecurityResource GetExternalWorkerSecurity()
    {
        lock (_gate)
        {
            return new(
                ExternalWorkerSecurityPolicy.CurrentSchemaVersion,
                _externalWorkerSecurityHistory.State.Revision.Value,
                _externalWorkerSecurityHistory.State.Policy,
                _externalWorkerSecurityLoadDiagnostics.ToArray());
        }
    }

    public ExternalWorkerSecurityPlanResult PlanExternalWorkerSecurity(ExternalWorkerSecurityPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1 || request.BaseRevision < 0)
                return ExternalWorkerSecurityFailure(_externalWorkerSecurityHistory.State.Revision.Value,
                    "asset.worker_security.request_invalid", "Worker security request schema or revision is invalid.");
            var transactionId = Guid.NewGuid();
            var planId = Guid.NewGuid();
            var command = new SetExternalWorkerSecurityPolicyCommand(
                CommandId.From(transactionId),
                planId,
                new ExternalWorkerSecurityPolicyRevision(request.BaseRevision),
                request.Policy,
                request.Permission,
                DryRun: true);
            var preview = _externalWorkerSecurityHandler.Handle(_externalWorkerSecurityHistory.State, command);
            if (!preview.Succeeded)
                return new(false, null, transactionId.ToString("D"), request.BaseRevision, null,
                    preview.Patch, preview.Inverse, 0,
                    preview.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, "assets/worker-security")).ToArray(),
                    false, false, []);
            var key = planId.ToString("D");
            var stored = new StoredExternalWorkerSecurityPlan(
                key,
                transactionId.ToString("D"),
                command,
                preview.Patch!,
                preview.Inverse!,
                [new PlanAuditEntry(key, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _externalWorkerSecurityPlans.Add(key, stored);
            return ToResult(stored);
        }
    }

    public ExternalWorkerSecurityPlanResult ApplyExternalWorkerSecurityPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_externalWorkerSecurityPlans.TryGetValue(planId, out var plan))
                return ExternalWorkerSecurityFailure(_externalWorkerSecurityHistory.State.Revision.Value,
                    "asset.worker_security.plan_missing", $"Worker security plan '{planId}' does not exist.");
            if (plan.Applied)
                return ExternalWorkerSecurityFailure(plan.Command.BaseRevision.Value,
                    "asset.worker_security.plan_already_applied", $"Worker security plan '{planId}' is already applied.");
            var result = _externalWorkerSecurityHandler.Handle(
                _externalWorkerSecurityHistory.State,
                plan.Command with { DryRun = false });
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(plan.PlanId, "apply", _externalWorkerSecurityHistory.State.Revision.Value,
                    _clock.UtcNow, false));
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = result.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, "assets/worker-security")).ToArray(),
                };
            }
            var saved = ExternalWorkerSecurityPolicyStore.Save(_session.RootDirectory, result.State.Policy);
            if (!saved.Succeeded)
            {
                plan.Audit.Add(new(plan.PlanId, "apply", _externalWorkerSecurityHistory.State.Revision.Value,
                    _clock.UtcNow, false));
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            }
            _externalWorkerSecurityHistory.Record(result);
            _externalWorkerSecurityLoadDiagnostics.Clear();
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new(plan.PlanId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public ExternalWorkerSecurityPlanResult RevertExternalWorkerSecurityPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_externalWorkerSecurityPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return ExternalWorkerSecurityFailure(_externalWorkerSecurityHistory.State.Revision.Value,
                    "asset.worker_security.plan_not_applied", $"Worker security plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _externalWorkerSecurityHistory.State.Revision.Value)
                return ExternalWorkerSecurityFailure(plan.Command.BaseRevision.Value,
                    "asset.worker_security.revert_conflict", "Only the latest worker security mutation can be reverted safely.");
            if (!_externalWorkerSecurityHistory.Undo())
                return ExternalWorkerSecurityFailure(plan.Command.BaseRevision.Value,
                    "asset.worker_security.inverse_unavailable", "Worker security inverse operation is unavailable.");
            var saved = ExternalWorkerSecurityPolicyStore.Save(
                _session.RootDirectory, _externalWorkerSecurityHistory.State.Policy);
            if (!saved.Succeeded)
            {
                _externalWorkerSecurityHistory.Redo();
                return ExternalWorkerSecurityFailure(plan.Command.BaseRevision.Value,
                    "asset.worker_security.save_failed", "Reverted worker security policy could not be saved.");
            }
            plan.Reverted = true;
            plan.Audit.Add(new(plan.PlanId, "revert", _externalWorkerSecurityHistory.State.Revision.Value,
                _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AssistedForgeScriptProposalResource ProposeAssistedForgeScript(AssistedForgeScriptProposalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SchemaVersion != 1)
            return new(false, null,
                [new AgentDiagnostic("assistant.script.request_invalid",
                    "Assisted ForgeScript proposal schema is invalid.", "assistant/script")]);
        var snapshot = CaptureAssistedProposalSnapshot();
        try
        {
            var profile = AssistantWorkspaceProfile.CreateDefault();
            var proposal = AssistedForgeScriptCreationService.ProposeAsync(
                request.Brief,
                snapshot.RootDirectory,
                snapshot.Project,
                snapshot.LoreIndex,
                new DeterministicAssistantProvider(profile.ActiveProvider),
                profile.ActiveProvider,
                _forgeScriptRuntime,
                networkAuthorized: false,
                projectContentAuthorized: false).GetAwaiter().GetResult();
            return new(proposal.Succeeded, proposal, proposal.Diagnostics.Select(MapScript).ToArray());
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return new(false, null,
                [new AgentDiagnostic("assistant.script.brief_invalid", exception.Message, "assistant/script")]);
        }
    }

    public AssistedForgeScriptApplyPlanResult PlanAssistedForgeScript(AssistedForgeScriptApplyPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1 || request.BaseRevision < 0)
                return AssistedForgeScriptFailure(_history.State.Revision.Value,
                    "assistant.script.request_invalid", "Assisted ForgeScript request schema or revision is invalid.");
            var transactionId = Guid.NewGuid();
            var executionPlanId = Guid.NewGuid();
            var command = new ApplyAssistedForgeScriptCommand(
                Guid.NewGuid(), transactionId, executionPlanId, request.BaseRevision,
                request.Proposal, request.Permission, DryRun: true);
            var preview = _assistedForgeScriptHandler.Handle(_session.RootDirectory, _history.State.Revision.Value, command);
            if (!preview.Succeeded || preview.Patch is null)
                return new(false, null, transactionId.ToString("D"), request.Proposal.ScriptId.ToString("D"),
                    request.Proposal.RelativePath, request.BaseRevision, null, "no-change",
                    request.Proposal.ForgeScriptApiVersion, request.Proposal.EventBinding,
                    request.Proposal.Tests.Select(item => item.Id).ToArray(),
                    preview.Diagnostics.Select(code => new AgentDiagnostic(code, code, request.Proposal.RelativePath)).ToArray(),
                    request.Proposal.EstimatedCostUsd, false, false, []);
            var key = executionPlanId.ToString("D");
            var stored = new StoredAssistedForgeScriptPlan(
                key,
                command,
                preview.Patch,
                [new PlanAuditEntry(key, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _assistedForgeScriptPlans.Add(key, stored);
            return ToResult(stored);
        }
    }

    public AssistedForgeScriptApplyPlanResult ApplyAssistedForgeScriptPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_assistedForgeScriptPlans.TryGetValue(planId, out var plan))
                return AssistedForgeScriptFailure(_history.State.Revision.Value,
                    "assistant.script.plan_missing", $"Assisted ForgeScript plan '{planId}' does not exist.");
            if (plan.Applied)
                return AssistedForgeScriptFailure(plan.Command.BaseRevision,
                    "assistant.script.plan_already_applied", $"Assisted ForgeScript plan '{planId}' is already applied.");
            var result = _assistedForgeScriptHandler.Handle(
                _session.RootDirectory,
                _history.State.Revision.Value,
                plan.Command with { CommandId = Guid.NewGuid(), DryRun = false });
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(plan.PlanId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return WithFailure(plan, result.Diagnostics.Select(code =>
                    new AgentDiagnostic(code, code, plan.Command.Proposal.RelativePath)).ToArray());
            }
            plan.Applied = true;
            plan.Audit.Add(new(plan.PlanId, "apply", _history.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AssistedForgeScriptApplyPlanResult RevertAssistedForgeScriptPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_assistedForgeScriptPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return AssistedForgeScriptFailure(_history.State.Revision.Value,
                    "assistant.script.plan_not_applied", $"Assisted ForgeScript plan '{planId}' is not currently applied.");
            var path = AssistedForgeScriptCreationService.ResolvePath(_session.RootDirectory, plan.Patch.RelativePath);
            var current = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
            if (!string.Equals(AssistedForgeScriptCreationService.Sha256(current),
                    plan.Command.Proposal.SourceSha256, StringComparison.Ordinal))
                return AssistedForgeScriptFailure(_history.State.Revision.Value,
                    "assistant.script.revert_conflict", "ForgeScript source changed after the plan was applied.");
            ApplyAssistedForgeScriptHandler.Restore(_session.RootDirectory, plan.Patch, useAfter: false);
            plan.Reverted = true;
            plan.Audit.Add(new(plan.PlanId, "revert", _history.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

}
