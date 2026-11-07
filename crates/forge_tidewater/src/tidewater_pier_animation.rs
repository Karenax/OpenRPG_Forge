//! Map-owned resolution of acquired pier data for the shared motion host.
use bevy::prelude::*;
use forge_project::zone_authoring::placements::ManualPlacement;
use forge_tidewater_assets::tidewater_pier;
use forge_tidewater_assets::tidewater_pier_motion::PierMotionPackage;
use {
    crate::tidewater_pier::{PierLanternPivot, PierSwingPivot, ResidentPier},
    forge_engine::pendulum_motion::{
        LanternMotion, SignMotion, Wind,
        host::{
            Binding, Motion, Resident,
            resolve::{self, Target},
        },
    },
};

/// One authored pier instance, animated by the common engine host.
pub struct ResidentPierMotion {
    motion: Resident,
}

impl ResidentPierMotion {
    /// Resolve the pinned dataset and hand its pivots to the shared host.
    pub fn bind(
        world: &mut World,
        package: &PierMotionPackage,
        placement: &ManualPlacement,
    ) -> Result<Self, String> {
        let (root, bindings) = resolve(world, package, placement)?;
        Ok(Self {
            motion: Resident::bind(world, placement, root, bindings)?,
        })
    }

    /// Reattach after an authoring edit or geometry reacquisition.
    pub fn rebind(
        &mut self,
        world: &mut World,
        package: &PierMotionPackage,
        placement: &ManualPlacement,
    ) -> Result<(), String> {
        let (root, bindings) = resolve(world, package, placement)?;
        self.motion.rebind(world, placement, root, bindings)
    }

    /// Preserve the map-owned geometry admission check before the shared frame.
    pub fn advance(&mut self, world: &mut World, dt: f64, wind: Wind) -> Result<(), String> {
        verify_resident(world, self.motion.root(), self.motion.placement())?;
        self.motion.advance(world, dt, wind)
    }
}

impl forge_engine::pendulum_motion::host::collection::Projection for ResidentPierMotion {
    type Source = PierMotionPackage;

    fn bind(
        world: &mut World,
        source: &Self::Source,
        placement: &ManualPlacement,
    ) -> Result<Self, String> {
        ResidentPierMotion::bind(world, source, placement)
    }

    fn rebind(
        &mut self,
        world: &mut World,
        source: &Self::Source,
        placement: &ManualPlacement,
    ) -> Result<(), String> {
        ResidentPierMotion::rebind(self, world, source, placement)
    }

    fn advance(&mut self, world: &mut World, dt: f64, wind: Wind) -> Result<(), String> {
        ResidentPierMotion::advance(self, world, dt, wind)
    }

    fn release(self, world: &mut World) {
        self.motion.release(world);
    }
}

fn resolve(
    world: &mut World,
    package: &PierMotionPackage,
    placement: &ManualPlacement,
) -> Result<(Entity, Vec<Binding>), String> {
    placement
        .transform
        .validate()
        .map_err(|error| error.to_string())?;
    if placement.asset_id != tidewater_pier::asset_id()
        || placement.asset_hash != tidewater_pier::package_hash()
    {
        return Err("Pier motion authored asset revision differs".into());
    }
    let roots: Vec<_> = world
        .query::<(Entity, &forge_engine::ManualPlacementProjection)>()
        .iter(world)
        .filter(|(_, projection)| projection.0.id == placement.id)
        .map(|(entity, projection)| (entity, projection.0.clone()))
        .collect();
    if roots.len() != 1 || roots[0].1 != *placement {
        return Err("Pier motion root absent or ambiguous".into());
    }
    let root = roots[0].0;
    verify_resident(world, root, placement)?;

    let mut targets = Vec::with_capacity(package.lanterns().len() + 1);
    for source in package.lanterns() {
        let key = u64::try_from(source.ordinal).map_err(|_| "Pier motion ordinal is too large")?;
        if key == u64::MAX {
            return Err("Pier motion ordinal conflicts with its sign".into());
        }
        let pivot = bevy::math::DVec3::from_array(
            package
                .lantern_pivot_local_m(source.ordinal)
                .ok_or("Pier motion source pivot is absent")?,
        )
        .as_vec3();
        targets.push(Target {
            key,
            pivot,
            rest_rotation: Quat::IDENTITY,
            rest_scale: Vec3::ONE,
            motion: Motion::Suspended(LanternMotion {
                t: source.t,
                ph: source.ph,
                x: source.x,
                z: source.z,
                vx: source.vx,
                vz: source.vz,
            }),
        });
    }
    let source = package.sign();
    targets.push(Target {
        key: u64::MAX,
        pivot: bevy::math::DVec3::from_array(package.sign_pivot_local_m()).as_vec3(),
        rest_rotation: Quat::IDENTITY,
        rest_scale: Vec3::ONE,
        motion: Motion::Hinged(SignMotion {
            t: source.t,
            a: source.a,
            av: source.av,
            b: source.b,
            bv: source.bv,
        }),
    });
    let bindings = resolve::bindings(world, root, &targets, |world, entity| {
        let lantern = world.get::<PierLanternPivot>(entity);
        let sign = world.get::<PierSwingPivot>(entity).is_some();
        match (lantern, sign) {
            (Some(_), true) => Err("Pier motion pivot has conflicting source identities".into()),
            (Some(lantern), false) => {
                let key =
                    u64::try_from(lantern.0).map_err(|_| "Pier motion ordinal is too large")?;
                if key == u64::MAX {
                    return Err("Pier motion ordinal conflicts with its sign".into());
                }
                Ok(Some(key))
            }
            (None, true) => Ok(Some(u64::MAX)),
            (None, false) => Ok(None),
        }
    })?;
    // This acquired geometry specifies direct children of its root. Generic
    // assets may use the same resolver with nested pivots.
    if bindings.iter().any(|binding| {
        world
            .get::<ChildOf>(binding.entity)
            .is_none_or(|parent| parent.parent() != root)
    }) {
        return Err("Pier motion pivot differs from its source geometry".into());
    }
    Ok((root, bindings))
}

fn verify_resident(world: &World, root: Entity, placement: &ManualPlacement) -> Result<(), String> {
    if !world
        .get_resource::<ResidentPier>()
        .is_some_and(|resident| {
            resident
                .authored_roots()
                .iter()
                .any(|(entity, id, transform)| {
                    *entity == root && *id == placement.id && *transform == placement.transform
                })
        })
    {
        return Err("Pier motion root differs from authored resident geometry".into());
    }
    Ok(())
}
