//! Read-only structural migrations. Existing IDs, revisions, metadata and authored terrain are
//! preserved. Older areas gain deterministic Forge-owned empty documents, so reopening a snapshot
//! does not add visible authored content.
use crate::{
    PathDocument, RiverDocument, RoadDocument, TerrainDocument, VegetationDensityDocument,
    VegetationDocument, WORKSPACE_SCHEMA_VERSION, WallDocument, WorkspaceArea, WorkspaceAreaKind,
    WorkspaceDocument, WorkspaceError, WorkspaceModule, WorkspaceProject,
};
use forge_core::{AreaId, ModuleId, ProjectId, Revision, Vec3};
use serde::Deserialize;

#[derive(Deserialize)]
struct Version {
    schema_version: u32,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct V1 {
    schema: String,
    schema_version: u32,
    revision: Revision,
    project: ProjectV1,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct ProjectV1 {
    id: ProjectId,
    name: String,
    module: ModuleV1,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct ModuleV1 {
    id: ModuleId,
    name: String,
    area: AreaV1V2,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct V2 {
    schema: String,
    schema_version: u32,
    revision: Revision,
    project: ProjectV2,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct ProjectV2 {
    id: ProjectId,
    name: String,
    modules: Vec<ModuleV2>,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct ModuleV2 {
    id: ModuleId,
    name: String,
    areas: Vec<AreaV1V2>,
}

/// Exact pre-v3 area shape. Do not type legacy snapshots as the current `WorkspaceArea`.
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct AreaV1V2 {
    id: AreaId,
    name: String,
    kind: WorkspaceAreaKind,
    spawn: Vec3,
    terrain: TerrainDocument,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct V3 {
    schema: String,
    schema_version: u32,
    revision: Revision,
    project: ProjectV3,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct ProjectV3 {
    id: ProjectId,
    name: String,
    modules: Vec<ModuleV3>,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct ModuleV3 {
    id: ModuleId,
    name: String,
    areas: Vec<AreaV3>,
}

/// Exact pre-v4 area shape. It intentionally cannot accept the new `paths` member.
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct AreaV3 {
    id: AreaId,
    name: String,
    kind: WorkspaceAreaKind,
    spawn: Vec3,
    terrain: TerrainDocument,
    vegetation: VegetationDocument,
    vegetation_density: VegetationDensityDocument,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct V4 {
    schema: String,
    schema_version: u32,
    revision: Revision,
    project: ProjectV4,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct ProjectV4 {
    id: ProjectId,
    name: String,
    modules: Vec<ModuleV4>,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct ModuleV4 {
    id: ModuleId,
    name: String,
    areas: Vec<AreaV4>,
}

/// Exact pre-v5 area shape. It intentionally cannot accept the new `roads` member.
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct AreaV4 {
    id: AreaId,
    name: String,
    kind: WorkspaceAreaKind,
    spawn: Vec3,
    terrain: TerrainDocument,
    vegetation: VegetationDocument,
    vegetation_density: VegetationDensityDocument,
    paths: PathDocument,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct V5 {
    schema: String,
    schema_version: u32,
    revision: Revision,
    project: ProjectV5,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct ProjectV5 {
    id: ProjectId,
    name: String,
    modules: Vec<ModuleV5>,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct ModuleV5 {
    id: ModuleId,
    name: String,
    areas: Vec<AreaV5>,
}

/// Exact pre-v6 area shape. It intentionally cannot accept the new `rivers` member.
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct AreaV5 {
    id: AreaId,
    name: String,
    kind: WorkspaceAreaKind,
    spawn: Vec3,
    terrain: TerrainDocument,
    vegetation: VegetationDocument,
    vegetation_density: VegetationDensityDocument,
    paths: PathDocument,
    roads: RoadDocument,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct V6 {
    schema: String,
    schema_version: u32,
    revision: Revision,
    project: ProjectV6,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct ProjectV6 {
    id: ProjectId,
    name: String,
    modules: Vec<ModuleV6>,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct ModuleV6 {
    id: ModuleId,
    name: String,
    areas: Vec<AreaV6>,
}

/// Exact pre-v7 area shape. It intentionally cannot accept the new `walls` member.
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct AreaV6 {
    id: AreaId,
    name: String,
    kind: WorkspaceAreaKind,
    spawn: Vec3,
    terrain: TerrainDocument,
    vegetation: VegetationDocument,
    vegetation_density: VegetationDensityDocument,
    paths: PathDocument,
    roads: RoadDocument,
    rivers: RiverDocument,
}

pub(super) fn read(json: &str) -> Result<WorkspaceDocument, WorkspaceError> {
    // Inspect only the version, then deserialize the original bytes strictly. Parsing through
    // Value would discard duplicate keys before the actual schema had a chance to reject them.
    match serde_json::from_str::<Version>(json)?.schema_version {
        1 => migrate_v1(serde_json::from_str(json)?),
        2 => migrate_v2(serde_json::from_str(json)?),
        3 => migrate_v3(serde_json::from_str(json)?),
        4 => migrate_v4(serde_json::from_str(json)?),
        5 => migrate_v5(serde_json::from_str(json)?),
        6 => migrate_v6(serde_json::from_str(json)?),
        WORKSPACE_SCHEMA_VERSION => migrate_embedded_documents(serde_json::from_str(json)?),
        version => Err(WorkspaceError::UnsupportedVersion(version)),
    }
}

fn migrate_v1(old: V1) -> Result<WorkspaceDocument, WorkspaceError> {
    debug_assert_eq!(old.schema_version, 1);
    let project_id = old.project.id;
    Ok(WorkspaceDocument {
        schema: old.schema,
        schema_version: WORKSPACE_SCHEMA_VERSION,
        revision: old.revision,
        project: WorkspaceProject {
            id: project_id,
            name: old.project.name,
            modules: vec![WorkspaceModule {
                id: old.project.module.id,
                name: old.project.module.name,
                areas: vec![migrate_area(project_id, old.project.module.area)?],
            }],
        },
    })
}

fn migrate_v2(old: V2) -> Result<WorkspaceDocument, WorkspaceError> {
    debug_assert_eq!(old.schema_version, 2);
    let project_id = old.project.id;
    let modules = old
        .project
        .modules
        .into_iter()
        .map(|module| {
            Ok(WorkspaceModule {
                id: module.id,
                name: module.name,
                areas: module
                    .areas
                    .into_iter()
                    .map(|area| migrate_area(project_id, area))
                    .collect::<Result<Vec<_>, WorkspaceError>>()?,
            })
        })
        .collect::<Result<Vec<_>, WorkspaceError>>()?;
    Ok(WorkspaceDocument {
        schema: old.schema,
        schema_version: WORKSPACE_SCHEMA_VERSION,
        revision: old.revision,
        project: WorkspaceProject {
            id: project_id,
            name: old.project.name,
            modules,
        },
    })
}

fn migrate_v3(old: V3) -> Result<WorkspaceDocument, WorkspaceError> {
    debug_assert_eq!(old.schema_version, 3);
    let project_id = old.project.id;
    let modules = old
        .project
        .modules
        .into_iter()
        .map(|module| {
            Ok(WorkspaceModule {
                id: module.id,
                name: module.name,
                areas: module
                    .areas
                    .into_iter()
                    .map(|area| migrate_area_v3(project_id, area))
                    .collect::<Result<Vec<_>, WorkspaceError>>()?,
            })
        })
        .collect::<Result<Vec<_>, WorkspaceError>>()?;
    Ok(WorkspaceDocument {
        schema: old.schema,
        schema_version: WORKSPACE_SCHEMA_VERSION,
        revision: old.revision,
        project: WorkspaceProject {
            id: project_id,
            name: old.project.name,
            modules,
        },
    })
}

fn migrate_v4(old: V4) -> Result<WorkspaceDocument, WorkspaceError> {
    debug_assert_eq!(old.schema_version, 4);
    let project_id = old.project.id;
    let modules = old
        .project
        .modules
        .into_iter()
        .map(|module| {
            Ok(WorkspaceModule {
                id: module.id,
                name: module.name,
                areas: module
                    .areas
                    .into_iter()
                    .map(|area| migrate_area_v4(project_id, area))
                    .collect::<Result<Vec<_>, WorkspaceError>>()?,
            })
        })
        .collect::<Result<Vec<_>, WorkspaceError>>()?;
    Ok(WorkspaceDocument {
        schema: old.schema,
        schema_version: WORKSPACE_SCHEMA_VERSION,
        revision: old.revision,
        project: WorkspaceProject {
            id: project_id,
            name: old.project.name,
            modules,
        },
    })
}

fn migrate_v5(old: V5) -> Result<WorkspaceDocument, WorkspaceError> {
    debug_assert_eq!(old.schema_version, 5);
    let project_id = old.project.id;
    let modules = old
        .project
        .modules
        .into_iter()
        .map(|module| {
            Ok(WorkspaceModule {
                id: module.id,
                name: module.name,
                areas: module
                    .areas
                    .into_iter()
                    .map(|area| migrate_area_v5(project_id, area))
                    .collect::<Result<Vec<_>, WorkspaceError>>()?,
            })
        })
        .collect::<Result<Vec<_>, WorkspaceError>>()?;
    Ok(WorkspaceDocument {
        schema: old.schema,
        schema_version: WORKSPACE_SCHEMA_VERSION,
        revision: old.revision,
        project: WorkspaceProject {
            id: project_id,
            name: old.project.name,
            modules,
        },
    })
}

fn migrate_v6(old: V6) -> Result<WorkspaceDocument, WorkspaceError> {
    debug_assert_eq!(old.schema_version, 6);
    let project_id = old.project.id;
    let modules = old
        .project
        .modules
        .into_iter()
        .map(|module| {
            Ok(WorkspaceModule {
                id: module.id,
                name: module.name,
                areas: module
                    .areas
                    .into_iter()
                    .map(|area| migrate_area_v6(project_id, area))
                    .collect::<Result<Vec<_>, WorkspaceError>>()?,
            })
        })
        .collect::<Result<Vec<_>, WorkspaceError>>()?;
    Ok(WorkspaceDocument {
        schema: old.schema,
        schema_version: WORKSPACE_SCHEMA_VERSION,
        revision: old.revision,
        project: WorkspaceProject {
            id: project_id,
            name: old.project.name,
            modules,
        },
    })
}

fn migrate_area(project_id: ProjectId, old: AreaV1V2) -> Result<WorkspaceArea, WorkspaceError> {
    let terrain = old.terrain.migrate_to_latest()?;
    let mut area = super::workspace::new_exterior_area(
        project_id,
        old.id,
        terrain.revision,
        old.name,
        terrain.seed,
        old.spawn,
    );
    area.kind = old.kind;
    area.terrain = terrain;
    // Legacy terrain material IDs were author-defined. Keep the synthesized Forge recipe bound to
    // the preserved semantic role instead of assuming the current default `meadow_grass` ID.
    for layer in &mut area.vegetation.layers {
        if let Some(filter) = &mut layer.material_filter {
            filter.material_layer_id = area.terrain.material_layers[filter.material_role.index()]
                .id
                .clone();
        }
    }
    Ok(area)
}

fn migrate_area_v3(project_id: ProjectId, old: AreaV3) -> Result<WorkspaceArea, WorkspaceError> {
    let terrain = old.terrain.migrate_to_latest()?;
    let vegetation = old.vegetation.migrate_to_latest()?;
    let paths_revision = terrain
        .revision
        .max(vegetation.revision)
        .max(old.vegetation_density.revision);
    let paths = PathDocument::new(project_id, old.id, paths_revision, old.name.clone(), vec![]);
    let roads = RoadDocument::new(project_id, old.id, paths_revision, old.name.clone(), vec![]);
    let rivers = RiverDocument::new(project_id, old.id, paths_revision, old.name.clone(), vec![]);
    let walls = WallDocument::new(project_id, old.id, paths_revision, old.name.clone(), vec![]);
    Ok(WorkspaceArea {
        id: old.id,
        name: old.name,
        kind: old.kind,
        spawn: old.spawn,
        terrain,
        vegetation,
        vegetation_density: old.vegetation_density,
        paths,
        roads,
        rivers,
        walls,
    })
}

fn migrate_area_v4(project_id: ProjectId, old: AreaV4) -> Result<WorkspaceArea, WorkspaceError> {
    let terrain = old.terrain.migrate_to_latest()?;
    let vegetation = old.vegetation.migrate_to_latest()?;
    let road_revision = terrain
        .revision
        .max(vegetation.revision)
        .max(old.vegetation_density.revision)
        .max(old.paths.revision);
    let roads = RoadDocument::new(project_id, old.id, road_revision, old.name.clone(), vec![]);
    let rivers = RiverDocument::new(project_id, old.id, road_revision, old.name.clone(), vec![]);
    let walls = WallDocument::new(project_id, old.id, road_revision, old.name.clone(), vec![]);
    Ok(WorkspaceArea {
        id: old.id,
        name: old.name,
        kind: old.kind,
        spawn: old.spawn,
        terrain,
        vegetation,
        vegetation_density: old.vegetation_density,
        paths: old.paths,
        roads,
        rivers,
        walls,
    })
}

fn migrate_area_v5(project_id: ProjectId, old: AreaV5) -> Result<WorkspaceArea, WorkspaceError> {
    let terrain = old.terrain.migrate_to_latest()?;
    let vegetation = old.vegetation.migrate_to_latest()?;
    let river_revision = terrain
        .revision
        .max(vegetation.revision)
        .max(old.vegetation_density.revision)
        .max(old.paths.revision)
        .max(old.roads.revision);
    let rivers = RiverDocument::new(project_id, old.id, river_revision, old.name.clone(), vec![]);
    let walls = WallDocument::new(project_id, old.id, river_revision, old.name.clone(), vec![]);
    Ok(WorkspaceArea {
        id: old.id,
        name: old.name,
        kind: old.kind,
        spawn: old.spawn,
        terrain,
        vegetation,
        vegetation_density: old.vegetation_density,
        paths: old.paths,
        roads: old.roads,
        rivers,
        walls,
    })
}

fn migrate_area_v6(project_id: ProjectId, old: AreaV6) -> Result<WorkspaceArea, WorkspaceError> {
    let terrain = old.terrain.migrate_to_latest()?;
    let vegetation = old.vegetation.migrate_to_latest()?;
    let wall_revision = terrain
        .revision
        .max(vegetation.revision)
        .max(old.vegetation_density.revision)
        .max(old.paths.revision)
        .max(old.roads.revision)
        .max(old.rivers.revision);
    let walls = WallDocument::new(project_id, old.id, wall_revision, old.name.clone(), vec![]);
    Ok(WorkspaceArea {
        id: old.id,
        name: old.name,
        kind: old.kind,
        spawn: old.spawn,
        terrain,
        vegetation,
        vegetation_density: old.vegetation_density,
        paths: old.paths,
        roads: old.roads,
        rivers: old.rivers,
        walls,
    })
}

fn migrate_embedded_documents(
    mut workspace: WorkspaceDocument,
) -> Result<WorkspaceDocument, WorkspaceError> {
    for module in &mut workspace.project.modules {
        for area in &mut module.areas {
            area.terrain = area.terrain.migrate_to_latest()?;
            area.vegetation = area.vegetation.migrate_to_latest()?;
        }
    }
    Ok(workspace)
}
