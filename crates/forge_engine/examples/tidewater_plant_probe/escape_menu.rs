//! In-game presentation settings; engine input is released while either menu is open.
use super::graphics;
use bevy::prelude::*;
use forge_engine::{PlayerInputFocus, tidewater_sky::solar};
#[path = "display_preferences.rs"]
mod config;
#[path = "display_daylight.rs"]
mod daylight;
#[path = "display_proof.rs"]
mod display_proof;
#[path = "logs_proof.rs"]
mod logs_proof;
#[path = "escape_menu_proof.rs"]
mod proof;
#[path = "terrain_proof.rs"]
mod terrain_proof;
pub(super) fn terrain_inland_requested() -> bool {
    terrain_proof::requested()
        && std::env::var("FORGE_TIDEWATER_TERRAIN_INLAND").as_deref() == Ok("1")
}
#[path = "display_ui.rs"]
mod ui;
pub(super) fn proof_requested() -> bool {
    logs_proof::requested()
        || proof::proof_requested()
        || display_proof::requested()
        || terrain_proof::requested()
}
pub(super) fn install_proof(app: &mut App) {
    if logs_proof::requested() {
        logs_proof::install(app);
    } else if terrain_proof::requested() {
        terrain_proof::install(app);
    } else if display_proof::requested() {
        display_proof::install(app);
    } else {
        proof::install_proof(app);
    }
}
pub(crate) fn solar_frame(world: &World) -> Option<solar::Frame> {
    world
        .get_resource::<daylight::Daylight>()
        .map(|clock| clock.frame)
}
#[derive(Resource)]
pub(super) struct State {
    pub open: bool,
    preferences: config::Preferences,
}
impl Default for State {
    fn default() -> Self {
        Self {
            open: false,
            preferences: config::load(),
        }
    }
}
#[derive(Component)]
pub(super) struct Panel;
#[derive(Clone, Copy, PartialEq, Eq)]
pub(super) enum Indicator {
    Fps,
    Frame,
    Clock,
}
#[derive(Component, Clone, Copy)]
pub(super) enum Action {
    Graphics,
    Resume,
    Quit,
    Toggle(Indicator),
    Hour(u16),
}
pub(super) fn install(app: &mut App) -> Result<(), String> {
    app.init_resource::<State>().init_resource::<ui::Metrics>();
    let hours = f64::from(app.world().resource::<State>().preferences.minutes) / 60.0;
    app.insert_resource(daylight::Daylight::new(hours)?)
        .add_systems(PostStartup, ui::spawn)
        .add_systems(
            Update,
            (update.after(graphics::toggle), ui::refresh.after(update)),
        )
        .add_systems(
            PostUpdate,
            daylight::update.before(bevy::transform::TransformSystems::Propagate),
        );
    Ok(())
}
pub(super) fn update(world: &mut World) {
    let keys = world.resource::<ButtonInput<KeyCode>>();
    let escape = keys.just_pressed(KeyCode::Escape);
    let shortcut = keys.just_pressed(KeyCode::KeyH) || keys.just_pressed(KeyCode::F10);
    let mut actions = world.query_filtered::<(&Interaction, &Action), Changed<Interaction>>();
    let pressed: Vec<_> = actions
        .iter(world)
        .filter_map(|(i, a)| (*i == Interaction::Pressed).then_some(*a))
        .collect();
    world.resource_scope(|world, mut state: Mut<State>| {
        let mut save = false;
        if escape {
            if world.resource::<graphics::GraphicsState>().open {
                world.resource_mut::<graphics::GraphicsState>().open = false; state.open = true;
            } else { state.open = !state.open; }
        }
        if shortcut && world.resource::<graphics::GraphicsState>().open { state.open = false; }
        for action in pressed {
            if !state.open { continue; }
            match action {
                Action::Graphics => { world.resource_mut::<graphics::GraphicsState>().open = true; state.open = false; }
                Action::Resume => state.open = false,
                Action::Quit => { world.write_message(bevy::app::AppExit::Success); }
                Action::Toggle(indicator) => { let field = state.preferences.field_mut(indicator); *field = !*field; save = true; }
                Action::Hour(minutes) => { if state.preferences.minutes != minutes { state.preferences.minutes = minutes; save = true; } }
            }
        }
        if state.open {
            let mut sliders = world.query_filtered::<(&Interaction, &bevy::ui::RelativeCursorPosition), With<ui::HourTrack>>();
            let position = sliders.iter(world).find_map(|(interaction, cursor)| (*interaction == Interaction::Pressed).then_some(cursor.normalized).flatten());
            if let Some(position) = position.filter(|position| position.is_finite()) {
                #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
                // Bevy normalizes nodes around their centre: -0.5..=0.5.
                let minutes = ((position.x + 0.5).clamp(0.0,1.0) * 96.0).round() as u16 * 15;
                if state.preferences.minutes != minutes { state.preferences.minutes = minutes; save = true; }
            }
        }
        if save && let Err(error) = config::save(&state.preferences) { warn!(%error, "display preferences could not be saved"); }
        let graphics_open = world.resource::<graphics::GraphicsState>().open;
        if state.open || graphics_open { world.resource_mut::<PlayerInputFocus>().0 = false; }
        let mut panels = world.query_filtered::<&mut Visibility, With<Panel>>();
        for mut visibility in panels.iter_mut(world) { visibility.set_if_neq(if state.open {Visibility::Visible} else {Visibility::Hidden}); }
        let mut panels = world.query_filtered::<&mut Visibility, With<graphics::Panel>>();
        for mut visibility in panels.iter_mut(world) { visibility.set_if_neq(if graphics_open {Visibility::Visible} else {Visibility::Hidden}); }
    });
}
