//! Editable game intent and readable workspace history, independent of the UI.
use forge_core::{AreaId, ModuleId, ProjectId};
use forge_project::{
    VisualQualityTier,
    authoring::workspace::WorkspaceModuleSummary,
    workspace::{
        ProjectEntry, WorkspaceError, WorkspaceHead,
        launcher::{
            DisplayMode, GAME_CONFIGURATION_VERSION, GameConfiguration, GameContent, GameLocation,
            PlayerDefaults, PresentationAsset,
        },
        session::WorkspaceAction,
    },
};

#[derive(Clone, Debug, PartialEq, Eq)]
pub(super) struct GameFields {
    pub title: String,
    pub summary: String,
    pub icon: Option<PresentationAsset>,
    pub logo: Option<PresentationAsset>,
    pub background: Option<PresentationAsset>,
    pub contents: Vec<GameContent>,
    pub entry: Option<GameLocation>,
    pub defaults: PlayerDefaults,
}

impl GameFields {
    fn from_project(project: &ProjectEntry) -> Self {
        project.game.as_ref().map_or_else(
            || Self {
                title: project.name.clone(),
                summary: String::new(),
                icon: None,
                logo: None,
                background: None,
                contents: Vec::new(),
                entry: None,
                // Match the existing launcher's initial settings. These do not
                // overwrite the player's later choices.
                defaults: PlayerDefaults {
                    quality: VisualQualityTier::Ultra,
                    display_mode: DisplayMode::Borderless,
                    master_volume: 82,
                    music_volume: 64,
                    reduce_motion: false,
                    subtitles: true,
                },
            },
            |game| Self {
                title: game.title.clone(),
                summary: game.summary.clone(),
                icon: game.icon,
                logo: game.logo,
                background: game.background,
                contents: game.contents.clone(),
                entry: Some(game.entry),
                defaults: game.defaults.clone(),
            },
        )
    }
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub(super) struct GameForm {
    pub project_id: ProjectId,
    pub before: Option<GameConfiguration>,
    original: GameFields,
    pub fields: GameFields,
}

impl GameForm {
    pub fn from_project(project: &ProjectEntry) -> Self {
        let fields = GameFields::from_project(project);
        Self {
            project_id: project.id,
            before: project.game.clone(),
            original: fields.clone(),
            fields,
        }
    }

    pub fn is_dirty(&self) -> bool {
        self.fields != self.original
    }

    pub fn includes(&self, module: ModuleId, area: AreaId) -> bool {
        self.fields
            .contents
            .iter()
            .any(|content| content.module_id == module && content.areas.contains(&area))
    }

    pub fn entry_key(entry: GameLocation) -> String {
        format!("{}/{}", entry.module_id, entry.area_id)
    }

    pub fn resolve_entry(&self, value: &str) -> Option<GameLocation> {
        let (module, area) = value.split_once('/')?;
        let entry = GameLocation {
            module_id: module.parse().ok()?,
            area_id: area.parse().ok()?,
        };
        self.includes(entry.module_id, entry.area_id)
            .then_some(entry)
    }

    pub fn set_area(&mut self, module: ModuleId, area: AreaId, selected: bool) {
        if selected {
            if let Some(content) = self
                .fields
                .contents
                .iter_mut()
                .find(|c| c.module_id == module)
            {
                if !content.areas.contains(&area) {
                    content.areas.push(area);
                }
            } else {
                self.fields.contents.push(GameContent {
                    module_id: module,
                    areas: vec![area],
                });
            }
        } else {
            if let Some(content) = self
                .fields
                .contents
                .iter_mut()
                .find(|c| c.module_id == module)
            {
                content.areas.retain(|candidate| *candidate != area);
            }
            self.fields
                .contents
                .retain(|content| !content.areas.is_empty());
            if self.fields.entry
                == Some(GameLocation {
                    module_id: module,
                    area_id: area,
                })
            {
                self.fields.entry = None;
            }
        }
    }

    pub fn configuration(&self) -> Result<GameConfiguration, String> {
        if self.fields.contents.is_empty() {
            return Err("Sélectionnez au moins une carte pour le jeu.".into());
        }
        let entry = self.fields.entry.ok_or("Choisissez la carte de départ.")?;
        let game = GameConfiguration {
            version: GAME_CONFIGURATION_VERSION,
            title: self.fields.title.clone(),
            summary: self.fields.summary.clone(),
            icon: self.fields.icon,
            logo: self.fields.logo,
            background: self.fields.background,
            contents: self.fields.contents.clone(),
            entry,
            defaults: self.fields.defaults.clone(),
        };
        game.validate_shape().map_err(|error| match error {
            WorkspaceError::Name => "Donnez un titre non vide, sans espaces superflus.".into(),
            WorkspaceError::Game("invalid presentation summary") => {
                "Le résumé est trop long ou contient des caractères non autorisés.".into()
            }
            WorkspaceError::Game("entry is not included in the game") => {
                "La carte de départ doit faire partie du jeu.".into()
            }
            WorkspaceError::Game("volume out of range") => {
                "Les volumes doivent être compris entre 0 et 100.".into()
            }
            other => format!("Configuration du jeu invalide : {other}"),
        })?;
        Ok(game)
    }

    pub fn action(&self) -> Result<WorkspaceAction, String> {
        let game = self.configuration()?;
        if self.before.as_ref() == Some(&game) {
            return Err("La configuration du jeu est inchangée.".into());
        }
        Ok(WorkspaceAction::ConfigureGame {
            project_id: self.project_id,
            before: self.before.clone().map(Box::new),
            after: Some(Box::new(game)),
        })
    }
}

/// Describe the actual reviewed delta, including projects other than the one
/// currently selected. Return no description for an unrecognized change so
/// that the UI cannot offer an ambiguous confirmation.
pub(super) fn describe_workspace_change(
    before: &WorkspaceHead,
    after: &WorkspaceHead,
    labels: &[WorkspaceModuleSummary],
) -> Vec<String> {
    let mut changes = Vec::new();
    for project in &before.projects {
        let Some(next) = after.projects.iter().find(|next| next.id == project.id) else {
            changes.push(format!("Projet « {} » retiré", project.name));
            continue;
        };
        if project.name != next.name {
            changes.push(format!(
                "Projet « {} » renommé « {} »",
                project.name, next.name
            ));
        }
        if project.game != next.game {
            changes.push(format!(
                "Projet « {} » — configuration du jeu : {}",
                next.name,
                game_change_fields(project.game.as_ref(), next.game.as_ref())
            ));
        }
        describe_modules(project, next, labels, &mut changes);
    }
    for project in &after.projects {
        if !before
            .projects
            .iter()
            .any(|candidate| candidate.id == project.id)
        {
            changes.push(format!("Projet « {} » ajouté", project.name));
        }
    }
    changes
}

fn describe_modules(
    project: &ProjectEntry,
    next: &ProjectEntry,
    labels: &[WorkspaceModuleSummary],
    changes: &mut Vec<String>,
) {
    for module in &project.modules {
        let Some(new_module) = next
            .modules
            .iter()
            .find(|candidate| candidate.id == module.id)
        else {
            changes.push(format!(
                "Projet « {} » — module « {} » retiré",
                next.name, module.name
            ));
            continue;
        };
        if module.name != new_module.name {
            changes.push(format!(
                "Projet « {} » — module « {} » renommé « {} »",
                next.name, module.name, new_module.name
            ));
        }
        if module.origin != new_module.origin {
            changes.push(format!(
                "Projet « {} » — origine du module « {} »",
                next.name, new_module.name
            ));
        }
        if module.creator != new_module.creator {
            changes.push(format!(
                "Projet « {} » — profil du module « {} »",
                next.name, new_module.name
            ));
        }
        for zone in &module.zones {
            let label = area_label(labels, module.id, zone.area_id);
            match new_module
                .zones
                .iter()
                .find(|candidate| candidate.area_id == zone.area_id)
            {
                None => changes.push(format!(
                    "Projet « {} » — module « {} » — {label} retirée",
                    next.name, new_module.name
                )),
                Some(new_zone) if new_zone != zone => changes.push(format!(
                    "Projet « {} » — module « {} » — {label} modifiée",
                    next.name, new_module.name
                )),
                _ => (),
            }
        }
        for zone in &new_module.zones {
            if !module
                .zones
                .iter()
                .any(|candidate| candidate.area_id == zone.area_id)
            {
                let label = area_label(labels, module.id, zone.area_id);
                changes.push(format!(
                    "Projet « {} » — module « {} » — {label} ajoutée",
                    next.name, new_module.name
                ));
            }
        }
    }
    for module in &next.modules {
        if !project
            .modules
            .iter()
            .any(|candidate| candidate.id == module.id)
        {
            changes.push(format!(
                "Projet « {} » — module « {} » ajouté",
                next.name, module.name
            ));
        }
    }
}

fn area_label(labels: &[WorkspaceModuleSummary], module: ModuleId, area: AreaId) -> String {
    labels
        .iter()
        .find(|item| item.id == module)
        .and_then(|item| item.maps.iter().find(|item| item.area_id == area))
        .map_or_else(
            || format!("carte {area}"),
            |map| format!("carte « {} »", map.name),
        )
}

fn game_change_fields(
    before: Option<&GameConfiguration>,
    after: Option<&GameConfiguration>,
) -> String {
    let (Some(before), Some(after)) = (before, after) else {
        return if let Some(after) = after {
            let mut fields = vec![
                "titre",
                "résumé",
                "cartes incluses",
                "carte de départ",
                "réglages initiaux",
            ];
            if after.icon.is_some() {
                fields.push("icône");
            }
            if after.logo.is_some() {
                fields.push("logo");
            }
            if after.background.is_some() {
                fields.push("fond");
            }
            format!("ajoutée — {}", fields.join(", "))
        } else {
            "retirée".into()
        };
    };
    let mut fields = Vec::new();
    if before.title != after.title {
        fields.push("titre");
    }
    if before.summary != after.summary {
        fields.push("résumé");
    }
    if before.icon != after.icon {
        fields.push("icône");
    }
    if before.logo != after.logo {
        fields.push("logo");
    }
    if before.background != after.background {
        fields.push("fond");
    }
    if before.contents != after.contents {
        fields.push("cartes incluses");
    }
    if before.entry != after.entry {
        fields.push("carte de départ");
    }
    if before.defaults != after.defaults {
        fields.push("réglages initiaux");
    }
    if fields.is_empty() {
        "version".into()
    } else {
        fields.join(", ")
    }
}
