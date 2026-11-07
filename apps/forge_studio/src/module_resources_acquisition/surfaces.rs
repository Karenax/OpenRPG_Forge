//! Existing package codecs over the same current-scope chunk reader.
use super::{Acquired, Reply, Request, Snapshot, read};
use forge_assets::{
    source_environment_package::{EnvironmentPackage, programs::ProgramsPackage},
    source_surface_package::SurfacePackage,
};
use forge_core::ContentHash;
use forge_project::module_package::{ModuleEnvironmentSource, ModuleObjectSource};
use std::{cell::RefCell, future::Future, rc::Rc};

pub(super) async fn objects<F, Fut>(
    snapshot: &Snapshot,
    previous: Option<&Acquired>,
    transport: &RefCell<F>,
    current: &impl Fn() -> bool,
) -> Result<(Option<ContentHash>, Option<Rc<SurfacePackage>>), String>
where
    F: FnMut(Request) -> Fut,
    Fut: Future<Output = Result<Reply, String>>,
{
    let Some(binding) = &snapshot.objects else {
        return Ok((None, None));
    };
    let ModuleObjectSource::SourceSurfaces { package, assets } = &binding.source;
    let index = &package.manifest;
    let decoded = if let Some(decoded) = previous
        .filter(|old| old.objects_index == Some(index.sha256))
        .and_then(|old| old.objects.clone())
    {
        decoded
    } else {
        let raw = read(snapshot, &index.path, 2 * 1024 * 1024, transport, current).await?;
        let parent = parent(&index.path);
        Rc::new(
            SurfacePackage::load_async(&raw, index.sha256, |name, maximum| {
                let path = joined(parent, &name);
                async move { read(snapshot, &path, maximum, transport, current).await }
            })
            .await?,
        )
    };
    let mut actual: Vec<_> = decoded
        .catalog()
        .assets
        .iter()
        .map(|asset| (asset.id, asset.content_hash))
        .collect();
    actual.sort_unstable();
    let expected: Vec<_> = assets
        .iter()
        .map(|asset| (asset.asset_id, asset.sha256))
        .collect();
    if actual != expected {
        return Err("Module object palette differs from its admitted source catalog".into());
    }
    Ok((Some(index.sha256), Some(decoded)))
}

#[derive(Default)]
pub(super) struct ScenePackages {
    pub index: Option<ContentHash>,
    pub environment: Option<Rc<EnvironmentPackage>>,
    pub programs_index: Option<ContentHash>,
    pub programs: Option<Rc<ProgramsPackage>>,
}

pub(super) async fn environment<F, Fut>(
    snapshot: &Snapshot,
    previous: Option<&Acquired>,
    transport: &RefCell<F>,
    current: &impl Fn() -> bool,
) -> Result<ScenePackages, String>
where
    F: FnMut(Request) -> Fut,
    Fut: Future<Output = Result<Reply, String>>,
{
    let Some(binding) = &snapshot.environment else {
        return Ok(ScenePackages::default());
    };
    let ModuleEnvironmentSource::SourceProfile {
        environment,
        programs,
    } = &binding.source;
    let index = &environment.manifest;
    let decoded = if let Some(decoded) = previous
        .filter(|old| old.environment_index == Some(index.sha256))
        .and_then(|old| old.environment.clone())
    {
        decoded
    } else {
        let raw = read(snapshot, &index.path, 256 * 1024, transport, current).await?;
        let parent = parent(&index.path);
        Rc::new(
            EnvironmentPackage::load_async(&raw, index.sha256, |name, maximum| {
                let path = joined(parent, &name);
                async move { read(snapshot, &path, maximum, transport, current).await }
            })
            .await?,
        )
    };
    let mut result = ScenePackages {
        index: Some(index.sha256),
        environment: Some(decoded),
        ..ScenePackages::default()
    };
    if let Some(programs) = programs {
        let index = &programs.manifest;
        result.programs_index = Some(index.sha256);
        result.programs = if let Some(decoded) = previous
            .filter(|old| old.programs_index == Some(index.sha256))
            .and_then(|old| old.programs.clone())
        {
            Some(decoded)
        } else {
            let raw = read(snapshot, &index.path, 2_000_000, transport, current).await?;
            let parent = parent(&index.path);
            Some(Rc::new(
                ProgramsPackage::load_async(&raw, index.sha256, |name, maximum| {
                    let path = joined(parent, &name);
                    async move { read(snapshot, &path, maximum, transport, current).await }
                })
                .await?,
            ))
        };
    }
    Ok(result)
}

fn parent(path: &str) -> &str {
    path.rsplit_once('/').map_or("", |(parent, _)| parent)
}
fn joined(parent: &str, name: &str) -> String {
    if parent.is_empty() {
        name.to_owned()
    } else {
        format!("{parent}/{name}")
    }
}
