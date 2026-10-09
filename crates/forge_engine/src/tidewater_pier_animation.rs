//! Motion adapter for live authored pier pivots, using acquired source phases.
use crate::{
    ManualPlacementProjection, domain_transform_to_bevy,
    tidewater_pier::{PierLanternPivot, PierSwingPivot, ResidentPier},
    tidewater_pier_motion::{LanternMotion, SignMotion, Wind},
};
use bevy::prelude::*;
use forge_assets::{tidewater_pier, tidewater_pier_motion::PierMotionPackage};
use forge_project::zone_authoring::placements::ManualPlacement;

struct Lantern {
    entity: Entity,
    pivot: Vec3,
    state: LanternMotion,
}

/// One authored instance's motion; copies own independent state and entities.
/// No placement document or immutable geometry is modified by animation.
pub struct ResidentPierMotion {
    placement: ManualPlacement,
    root: Entity,
    lanterns: Vec<Lantern>,
    sign: Entity,
    sign_pivot: Vec3,
    sign_state: SignMotion,
}

impl ResidentPierMotion {
    /// Resolve every pivot before returning a resident; refusal has no side effects.
    pub fn bind(
        world: &mut World,
        package: &PierMotionPackage,
        placement: &ManualPlacement,
    ) -> Result<Self, String> {
        placement.transform.validate().map_err(|e| e.to_string())?;
        if placement.asset_id != tidewater_pier::asset_id()
            || placement.asset_hash != tidewater_pier::package_hash()
        {
            return Err("Pier motion authored asset revision differs".into());
        }
        let roots: Vec<_> = world
            .query::<(Entity, &ManualPlacementProjection)>()
            .iter(world)
            .filter(|(_, p)| p.0.id == placement.id)
            .map(|(e, p)| (e, p.0.clone()))
            .collect();
        if roots.len() != 1 || roots[0].1 != *placement {
            return Err("Pier motion root absent or ambiguous".into());
        }
        let root = roots[0].0;
        verify_root(world, root, placement)?;
        let mut lanterns = Vec::new();
        for source in package.lanterns() {
            let matches: Vec<_> = world
                .query::<(Entity, &PierLanternPivot, &ChildOf)>()
                .iter(world)
                .filter(|(_, ordinal, parent)| {
                    parent.parent() == root && ordinal.0 == source.ordinal
                })
                .map(|(e, _, _)| e)
                .collect();
            if matches.len() != 1 {
                return Err("Pier motion lantern ordinal absent or duplicated".into());
            }
            let pivot = bevy::math::DVec3::new(
                source.pivot_world_m[0] - 55.0,
                source.pivot_world_m[1],
                source.pivot_world_m[2] + 64.0,
            )
            .as_vec3();
            verify_pivot(world, matches[0], root, pivot)?;
            let state = LanternMotion {
                t: source.t,
                ph: source.ph,
                x: source.x,
                z: source.z,
                vx: source.vx,
                vz: source.vz,
            };
            state.validate()?;
            lanterns.push(Lantern {
                entity: matches[0],
                pivot,
                state,
            });
        }
        let count = world
            .query::<(&PierLanternPivot, &ChildOf)>()
            .iter(world)
            .filter(|(_, p)| p.parent() == root)
            .count();
        if count != lanterns.len() {
            return Err("Pier motion unexpected lantern ordinal".into());
        }
        let signs: Vec<_> = world
            .query::<(Entity, &PierSwingPivot, &ChildOf)>()
            .iter(world)
            .filter(|(_, _, p)| p.parent() == root)
            .map(|(e, _, _)| e)
            .collect();
        if signs.len() != 1 {
            return Err("Pier motion sign absent or duplicated".into());
        }
        let sign_pivot = bevy::math::DVec3::from_array(package.sign_pivot_local_m()).as_vec3();
        verify_pivot(world, signs[0], root, sign_pivot)?;
        let s = package.sign();
        let sign_state = SignMotion {
            t: s.t,
            a: s.a,
            av: s.av,
            b: s.b,
            bv: s.bv,
        };
        sign_state.validate()?;
        Ok(Self {
            placement: placement.clone(),
            root,
            lanterns,
            sign: signs[0],
            sign_pivot,
            sign_state,
        })
    }

    /// Reattach after a common placement edit while preserving elapsed motion.
    /// Resolve the complete hierarchy before changing the resident state.
    pub fn rebind(
        &mut self,
        world: &mut World,
        package: &PierMotionPackage,
        placement: &ManualPlacement,
    ) -> Result<(), String> {
        if placement.id != self.placement.id {
            return Err("Pier motion rebind instance identity differs".into());
        }
        let mut next = Self::bind(world, package, placement)?;
        if next.lanterns.len() != self.lanterns.len() {
            return Err("Pier motion rebind source inventory differs".into());
        }
        for (new, old) in next.lanterns.iter_mut().zip(&self.lanterns) {
            old.state.validate()?;
            new.state = old.state;
        }
        self.sign_state.validate()?;
        next.sign_state = self.sign_state;
        for lantern in &next.lanterns {
            world
                .get_mut::<Transform>(lantern.entity)
                .expect("Validated rebind pivot")
                .rotation = lantern.state.rotation().as_quat();
        }
        world
            .get_mut::<Transform>(next.sign)
            .expect("Validated rebind sign")
            .rotation = next.sign_state.rotation().as_quat();
        *self = next;
        Ok(())
    }

    /// Compute all states before committing any transforms or elapsed time.
    /// A stale placement, missing pivot or invalid input retains the entire instance.
    pub fn advance(&mut self, world: &mut World, dt: f64, wind: Wind) -> Result<(), String> {
        verify_root(world, self.root, &self.placement)?;
        for l in &self.lanterns {
            verify_pivot(world, l.entity, self.root, l.pivot)?;
        }
        verify_pivot(world, self.sign, self.root, self.sign_pivot)?;
        let mut next: Vec<_> = self.lanterns.iter().map(|l| l.state).collect();
        for state in &mut next {
            state.advance(dt, wind)?;
        }
        let mut sign = self.sign_state;
        sign.advance(dt, wind)?;
        // All fallible input, hierarchy and numerical checks precede this commit.
        for (resident, state) in self.lanterns.iter_mut().zip(next) {
            world
                .get_mut::<Transform>(resident.entity)
                .expect("Validated exclusive-world pivot")
                .rotation = state.rotation().as_quat();
            resident.state = state;
        }
        world
            .get_mut::<Transform>(self.sign)
            .expect("Validated exclusive-world sign")
            .rotation = sign.rotation().as_quat();
        self.sign_state = sign;
        Ok(())
    }
}

fn verify_root(world: &World, root: Entity, placement: &ManualPlacement) -> Result<(), String> {
    if world.get::<ChildOf>(root).is_some()
        || world
            .get::<ManualPlacementProjection>(root)
            .is_none_or(|p| p.0 != *placement)
        || world.get::<Transform>(root) != Some(&domain_transform_to_bevy(placement.transform))
        || !world.get_resource::<ResidentPier>().is_some_and(|p| {
            p.authored_roots()
                .iter()
                .any(|(e, id, t)| *e == root && *id == placement.id && *t == placement.transform)
        })
    {
        return Err("Pier motion root differs from authored resident geometry".into());
    }
    Ok(())
}
fn verify_pivot(world: &World, entity: Entity, root: Entity, pivot: Vec3) -> Result<(), String> {
    let t = world
        .get::<Transform>(entity)
        .ok_or("Pier motion transform absent")?;
    if world
        .get::<ChildOf>(entity)
        .is_none_or(|p| p.parent() != root)
        || t.translation != pivot
        || t.scale != Vec3::ONE
        || !t.rotation.is_finite()
        || (t.rotation.length_squared() - 1.0).abs() > 1e-5
    {
        return Err("Pier motion live pivot differs".into());
    }
    Ok(())
}
