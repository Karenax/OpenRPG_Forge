//! External event inputs drive the reusable source clock; native oracle values
//! remain exclusively in the parent's CPU readback checks.
use super::Step;
use bevy::{prelude::Handle, render::storage::ShaderBuffer};
use forge_assets::source_particle_emission::{
    SourceEmissionAdvance, SourceSingleBurstLoopParameters,
};
use forge_engine::source_particle_compute::{
    SourceParticleComputeQueue, SourceParticleComputeSequence, SourceParticleEmissionCommand,
    SourceParticleEmitterSequence,
};

pub(super) enum SimulationSequence {
    Legacy(Box<SourceParticleComputeSequence>),
    Source {
        entity: bevy::prelude::Entity,
        bank_hash: forge_core::ContentHash,
    },
}

pub(super) fn validate(
    steps: &[Step],
    parameters: Option<SourceSingleBurstLoopParameters>,
) -> Result<(), String> {
    if let Some(parameters) = parameters {
        parameters.validate().map_err(str::to_owned)?;
        if parameters.burst_count.to_bits() != 1.0_f32.to_bits() {
            return Err(
                "Private capacity-one draw requires the admitted Count1 source profile".into(),
            );
        }
    }
    if steps.iter().any(|step| {
        !step.delta_time.is_finite()
            || step.delta_time < 0.0
            || step.draw && step.expected.is_none()
            || if parameters.is_some() {
                step.mode.is_some()
            } else {
                step.mode
                    .is_none_or(|mode| mode > 2 || mode == 2 && step.delta_time != 0.0)
                    || !step.events.is_empty()
                    || step.reinitialize
            }
    }) || !steps.iter().any(|step| step.draw)
    {
        return Err("Invalid source draw/checkpoint schedule".into());
    }
    Ok(())
}

fn bank(mut bytes: [u8; 1024], delta: f32, mode: u16, stamp: u16, count: f32) -> [u8; 1024] {
    bytes[1008..1012].copy_from_slice(&delta.to_le_bytes());
    bytes[1012..1016].copy_from_slice(&f32::from(mode).to_le_bytes());
    bytes[1016..1020].copy_from_slice(&f32::from(stamp).to_le_bytes());
    bytes[1020..1024].copy_from_slice(&count.to_le_bytes());
    bytes
}

pub(super) fn scene_owner(
    compute: SourceParticleComputeSequence,
    parameters: SourceSingleBurstLoopParameters,
    base: &[u8; 1024],
) -> Result<forge_engine::source_particle_compute::SourceParticleSceneOwner, String> {
    let base = *base;
    let controller =
        SourceParticleEmitterSequence::new(compute, parameters).map_err(str::to_owned)?;
    let owner = forge_engine::source_particle_compute::SourceParticleSceneOwner::new(
        controller,
        move |command, stamp| {
            let stamp =
                u16::try_from(stamp).map_err(|_| "Source scene stamp exceeds admitted bank ABI")?;
            Ok(match command {
                SourceParticleEmissionCommand::Reset => bank(base, 0.0, 3, stamp, 0.0),
                SourceParticleEmissionCommand::Allocate { count } => {
                    bank(base, 0.0, 2, stamp, count)
                }
                SourceParticleEmissionCommand::Update { delta_seconds } => {
                    bank(base, delta_seconds, 1, stamp, 0.0)
                }
            })
        },
    );
    Ok(owner)
}

impl SimulationSequence {
    pub fn new(
        app: &mut bevy::prelude::App,
        compute: SourceParticleComputeSequence,
        parameters: Option<SourceSingleBurstLoopParameters>,
        base: &[u8; 1024],
    ) -> Result<Self, String> {
        match parameters {
            Some(parameters) => {
                let owner = scene_owner(compute, parameters, base)?;
                let entity = app
                    .world_mut()
                    .spawn((
                        owner,
                        bevy::prelude::Transform::IDENTITY,
                        bevy::prelude::GlobalTransform::IDENTITY,
                        bevy::prelude::Visibility::Visible,
                    ))
                    .id();
                Ok(Self::Source {
                    entity,
                    bank_hash: forge_core::bytes_sha256(base),
                })
            }
            None => Ok(Self::Legacy(Box::new(compute))),
        }
    }

    pub fn entity(&self) -> Option<bevy::prelude::Entity> {
        match self {
            Self::Legacy(_) => None,
            Self::Source { entity, .. } => Some(*entity),
        }
    }

    fn owner<'a>(
        &self,
        app: &'a bevy::prelude::App,
    ) -> Result<&'a forge_engine::source_particle_compute::SourceParticleSceneOwner, String> {
        let entity = self.entity().ok_or("Legacy sequence has no scene owner")?;
        app.world()
            .get(entity)
            .ok_or("Source scene owner entity removed".into())
    }

    pub fn submit_step(
        &mut self,
        app: &mut bevy::prelude::App,
        step: &Step,
        base: &[u8; 1024],
        stamp: u16,
    ) -> Result<bool, String> {
        match self {
            Self::Legacy(compute) => {
                let mode = step
                    .mode
                    .ok_or("Explicit source compute operation absent")?;
                compute.submit_next(
                    &mut app.world_mut().resource_mut::<SourceParticleComputeQueue>(),
                    bank(
                        *base,
                        step.delta_time,
                        mode,
                        stamp,
                        if mode == 2 { 1.0 } else { 0.0 },
                    ),
                )
            }
            Self::Source { entity, bank_hash } => {
                if forge_core::bytes_sha256(base) != *bank_hash {
                    return Err("Source scene bank changed after admission".into());
                }
                let mut owner = app
                    .world_mut()
                    .get_mut::<forge_engine::source_particle_compute::SourceParticleSceneOwner>(
                        *entity,
                    )
                    .ok_or("Source scene owner absent")?;
                if let Some(error) = owner.error() {
                    return Err(error.to_owned());
                }
                if owner.queued_frames() != 0
                    || owner.completed_frames() != owner.submitted_frames()
                {
                    return Ok(false);
                }
                if owner.completed_frames() + 1 != u64::from(stamp) {
                    return Err("Source scene timeline stamp differs".into());
                }
                owner
                    .queue_frame(step.delta_time, &step.events, step.reinitialize)
                    .map_err(str::to_owned)?;
                Ok(true)
            }
        }
    }

    pub fn poll(&mut self, app: &mut bevy::prelude::App) -> Result<bool, String> {
        match self {
            Self::Legacy(value) => value.poll(),
            Self::Source { .. } => {
                let owner = self.owner(app)?;
                if let Some(error) = owner.error() {
                    return Err(error.to_owned());
                }
                Ok(owner.queued_frames() == 0
                    && owner.completed_frames() == owner.submitted_frames())
            }
        }
    }

    pub fn current_buffer(&self, app: &bevy::prelude::App) -> Result<Handle<ShaderBuffer>, String> {
        match self {
            Self::Legacy(value) => Ok(value.current_buffer()),
            Self::Source { .. } => Ok(self.owner(app)?.current_buffer()),
        }
    }

    pub fn encoded_steps(&self, app: &bevy::prelude::App) -> Result<u64, String> {
        match self {
            Self::Legacy(value) => Ok(value.encoded_steps()),
            Self::Source { .. } => Ok(self.owner(app)?.encoded_steps()),
        }
    }

    pub fn last_frame(
        &self,
        app: &bevy::prelude::App,
    ) -> Result<Option<SourceEmissionAdvance>, String> {
        match self {
            Self::Legacy(_) => Ok(None),
            Self::Source { .. } => Ok(self.owner(app)?.last_frame()),
        }
    }
}
