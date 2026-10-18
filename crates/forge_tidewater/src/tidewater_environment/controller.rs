// SPDX-License-Identifier: MIT
// Adapted from Tidewater Environment.js at 1438b1ab. Copyright 2026 DRG Software
// Solutions LLC; see third_party/tidewater/LICENSE.
//! Source refresh cadence with commit only after ordered command encoding.
use std::sync::Arc;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Step {
    Face(u8),
    Mips,
    Filter { target: u8, level: u8 },
    Sh { target: u8 },
    Publish { target: u8 },
}
#[derive(Clone, Debug)]
pub struct Controller {
    generation: Arc<()>,
    revision: u64,
    front: u8,
    primed: bool,
    timer: f64,
    last_sun: [f64; 3],
    step: Option<u8>,
}
impl Default for Controller {
    fn default() -> Self {
        Self {
            generation: Arc::new(()),
            revision: 0,
            front: 0,
            primed: false,
            timer: 0.0,
            last_sun: [0.0, -2.0, 0.0],
            step: None,
        }
    }
}
pub struct Planned {
    generation: Arc<()>,
    revision: u64,
    next: Controller,
    steps: Vec<Step>,
}
impl Planned {
    #[must_use]
    pub fn steps(&self) -> &[Step] {
        &self.steps
    }
}
fn step(index: u8, target: u8) -> Step {
    match index {
        0..=5 => Step::Face(index),
        6 => Step::Mips,
        7..=12 => Step::Filter {
            target,
            level: index - 7,
        },
        13 => Step::Sh { target },
        _ => Step::Publish { target },
    }
}
fn angle(a: [f64; 3], b: [f64; 3]) -> f64 {
    let length_a = a.iter().map(|v| v * v).sum::<f64>();
    let length_b = b.iter().map(|v| v * v).sum::<f64>();
    let dot = a.into_iter().zip(b).map(|(a, b)| a * b).sum::<f64>();
    (dot / (length_a * length_b).sqrt()).clamp(-1.0, 1.0).acos()
}
impl Controller {
    #[must_use]
    pub const fn front(&self) -> u8 {
        self.front
    }

    /// The source performs all fifteen logical steps on first use/force, then
    /// one step per frame. Mips contains two dispatches; publication is separate.
    /// Planning does not mutate the live controller or acknowledge GPU completion.
    pub fn plan(&self, dt: f64, sun: [f64; 3], force: bool) -> Result<Planned, String> {
        if !dt.is_finite()
            || dt < 0.0
            || sun.iter().any(|v| !v.is_finite())
            || (sun.iter().map(|v| v * v).sum::<f64>() - 1.0).abs() > 0.001
        {
            return Err("Invalid environment refresh input".into());
        }
        let mut next = self.clone();
        next.revision = self
            .revision
            .checked_add(1)
            .ok_or("Environment revision overflow")?;
        next.timer -= dt;
        if !next.timer.is_finite() {
            return Err("Environment timer overflow".into());
        }
        let mut steps = Vec::new();
        let back = 1 - self.front;
        if force || !self.primed {
            next.primed = true;
            next.timer = 3.0;
            next.last_sun = sun;
            next.step = None;
            steps = (0..15).map(|index| step(index, back)).collect();
            next.front = back;
        } else {
            if next.step.is_none() && (angle(sun, self.last_sun) > 0.004 || next.timer <= 0.0) {
                next.timer = 3.0;
                next.last_sun = sun;
                next.step = Some(0);
            }
            if let Some(index) = next.step {
                steps.push(step(index, back));
                if index == 14 {
                    next.front = back;
                    next.step = None;
                } else {
                    next.step = Some(index + 1);
                }
            }
        }
        Ok(Planned {
            generation: self.generation.clone(),
            revision: self.revision,
            next,
            steps,
        })
    }

    /// Commit only after every planned step was encoded in order. An idle plan
    /// can be committed immediately. GPU completion/readback is a separate gate.
    /// Stale plans and plans belonging to a replaced generation leave state intact.
    pub fn commit(&mut self, plan: Planned) -> Result<(), String> {
        self.validate(&plan)?;
        *self = plan.next;
        Ok(())
    }

    /// Refuse a stale plan before it can write any resident GPU resource.
    pub fn validate(&self, plan: &Planned) -> Result<(), String> {
        if !Arc::ptr_eq(&self.generation, &plan.generation) || self.revision != plan.revision {
            return Err("Stale environment refresh plan".into());
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    const SUN: [f64; 3] = [0.0, 1.0, 0.0];
    #[test]
    fn first_force_and_warm_refresh_follow_source_back_buffer_order() {
        let mut controller = Controller::default();
        let cold = controller.plan(1.0 / 60.0, SUN, false).unwrap();
        assert_eq!(
            cold.steps(),
            &(0..15).map(|i| step(i, 1)).collect::<Vec<_>>()
        );
        assert_eq!(controller.front(), 0);
        controller.commit(cold).unwrap();
        assert_eq!(controller.front(), 1);
        let idle = controller.plan(2.9, SUN, false).unwrap();
        assert!(idle.steps().is_empty());
        controller.commit(idle).unwrap();
        for index in 0..15 {
            let plan = controller
                .plan(if index == 0 { 0.11 } else { 0.01 }, SUN, false)
                .unwrap();
            assert_eq!(plan.steps(), &[step(index, 0)]);
            assert_eq!(controller.front(), 1);
            controller.commit(plan).unwrap();
        }
        assert_eq!(controller.front(), 0);
        let forced = controller.plan(0.0, SUN, true).unwrap();
        assert_eq!(forced.steps().len(), 15);
        controller.commit(forced).unwrap();
        assert_eq!(controller.front(), 1);
    }
    #[test]
    fn angular_threshold_invalid_inputs_and_stale_plans_preserve_state() {
        let mut controller = Controller::default();
        let cold = controller.plan(0.0, SUN, false).unwrap();
        controller.commit(cold).unwrap();
        let near = [0.003_f64.sin(), 0.003_f64.cos(), 0.0];
        assert!(
            controller
                .plan(0.0, near, false)
                .unwrap()
                .steps()
                .is_empty()
        );
        let far = [0.005_f64.sin(), 0.005_f64.cos(), 0.0];
        assert_eq!(
            controller.plan(0.0, far, false).unwrap().steps(),
            &[Step::Face(0)]
        );
        for (dt, sun) in [(f64::NAN, SUN), (-1.0, SUN), (0.0, [0.0; 3])] {
            assert!(controller.plan(dt, sun, false).is_err());
        }
        let stale = controller.plan(0.0, SUN, false).unwrap();
        let accepted = controller.plan(0.0, SUN, false).unwrap();
        controller.commit(accepted).unwrap();
        assert!(controller.commit(stale).is_err());
        let mut other = Controller::default();
        let foreign = controller.plan(0.0, SUN, false).unwrap();
        assert!(other.commit(foreign).is_err());
        assert_eq!(other.front(), 0);
    }
}
