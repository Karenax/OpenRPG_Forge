use super::{
    State,
    policy::{TIERS, label},
};
use bevy::ecs::{change_detection::Tick, system::SystemParam};
use bevy::input::mouse::{MouseScrollUnit, MouseWheel};
use bevy::prelude::*;
use forge_engine::{
    PendingPlayerControlCommand, PlayerInputFocus,
    cell_streaming::{
        CellStreamingObserver, CellStreamingStats, PlayerCellStreamingStatus,
        TerrainRetryAvailability, TerrainRetryUiStatus, retry_failed_terrain,
        terrain_retry_availability,
    },
    native_gameplay::{NativeGameplayUi, NativeGraphicsCommand},
    source_graphics::{Control, Settings},
};
use forge_project::VisualQualityTier;

const CONTROLS_PER_PAGE: usize = 4;
const CONTROL_PAGES: usize = Control::ALL.len() / CONTROLS_PER_PAGE;

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub(super) enum TerrainNotice {
    #[default]
    None,
    Retryable,
    Unavailable,
    Waiting,
}

#[allow(
    clippy::struct_excessive_bools,
    reason = "The menu, input and terrain retry track independent UI state"
)]
#[derive(Resource)]
pub struct GraphicsUi {
    pub open: bool,
    pub consumed_pointer: bool,
    selected: VisualQualityTier,
    draft: Option<Settings>,
    advanced: bool,
    page: usize,
    pub(super) focus: usize,
    pub(super) terrain: TerrainNotice,
    seen_terrain_error: Option<(String, u64)>,
    saw_stream_error: bool,
    awaiting_player_tick: Option<Tick>,
    pub(super) retrying: bool,
    retry_grace: bool,
}
impl GraphicsUi {
    pub fn new(selected: VisualQualityTier, open: bool) -> Self {
        Self {
            open,
            consumed_pointer: false,
            selected,
            draft: None,
            advanced: false,
            page: 0,
            focus: 0,
            terrain: TerrainNotice::None,
            seen_terrain_error: None,
            saw_stream_error: false,
            awaiting_player_tick: None,
            retrying: false,
            retry_grace: false,
        }
    }
}
#[derive(SystemSet, Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub(crate) struct GraphicsUiInput;
#[derive(Component)]
pub(super) struct Root;
#[derive(Component)]
pub(super) struct Status;
#[derive(Component)]
pub(super) struct Title;
#[derive(Component)]
pub(super) struct TerrainStatus;
#[derive(Component)]
pub(super) struct ScrollPanel;
#[derive(Component)]
pub(super) enum Section {
    Profiles,
    Advanced,
    ControlRow(usize),
}
#[derive(Component)]
pub(super) struct PageLabel;
#[derive(Component)]
pub(super) struct ControlValue(usize);
#[derive(Component, Clone, Copy)]
pub(super) enum Action {
    Choose(usize),
    Apply,
    Close,
    RetryTerrain,
    Advanced,
    PreviousPage,
    NextPage,
    Adjust(usize, bool),
}
impl Action {
    fn index(self) -> usize {
        match self {
            Self::Choose(index) => index,
            Self::Apply => 4,
            Self::Close => 5,
            Self::RetryTerrain => 6,
            Self::Advanced => 7,
            Self::PreviousPage => 8,
            Self::NextPage => 9,
            Self::Adjust(index, increase) => {
                10 + (index % CONTROLS_PER_PAGE) * 2 + usize::from(increase)
            }
        }
    }
}

/// Observe the shared failure before publishing the graphics menu state.
/// The raw diagnostic is kept only for deduplication, never rendered.
pub(super) fn sync_terrain(world: &mut World) {
    let retry = terrain_retry_availability(world);
    let stream_error = world
        .get_resource::<CellStreamingStats>()
        .and_then(|stats| stats.last_error.clone());
    let player = world.get_resource_ref::<PlayerCellStreamingStatus>();
    let player_ready = player.as_ref().map(|status| status.ready);
    let player_tick = player.as_ref().map(DetectChanges::last_changed);
    let error = stream_error
        .clone()
        .or_else(|| player.and_then(|status| status.error.clone()));
    let sequence = world
        .get_resource::<CellStreamingObserver>()
        .map_or(0, |observer| observer.retry_sequence);
    let recovering = world
        .get_resource::<NativeGameplayUi>()
        .is_some_and(NativeGameplayUi::is_recovering);
    let queued = world
        .get_resource::<TerrainRetryUiStatus>()
        .is_some_and(|status| status.request_queued);
    let mut ui = world.resource_mut::<GraphicsUi>();
    if stream_error.is_some() {
        ui.saw_stream_error = true;
        ui.awaiting_player_tick = None;
    } else if ui.saw_stream_error {
        // Update can admit a retry after the previous FixedUpdate.  A stale player
        // error is not a fresh refusal until the guard has run again.
        ui.saw_stream_error = false;
        ui.awaiting_player_tick = player_tick;
    }
    let awaiting_player = ui
        .awaiting_player_tick
        .is_some_and(|old| Some(old) == player_tick);
    if ui.awaiting_player_tick.is_some() && !awaiting_player {
        ui.awaiting_player_tick = None;
    }
    if queued {
        ui.retrying = true;
        ui.retry_grace = true;
    }
    let mut notice = match retry {
        TerrainRetryAvailability::NoFailure if error.is_none() => {
            if ui.terrain == TerrainNotice::None || player_ready == Some(true) {
                TerrainNotice::None
            } else if player_ready == Some(false) {
                // Admission is not enough: the fixed-step physics guard has not yet resumed.
                TerrainNotice::Waiting
            } else {
                TerrainNotice::Unavailable
            }
        }
        TerrainRetryAvailability::NoFailure | TerrainRetryAvailability::Unavailable => {
            TerrainNotice::Unavailable
        }
        TerrainRetryAvailability::Retryable => TerrainNotice::Retryable,
        TerrainRetryAvailability::Waiting => TerrainNotice::Waiting,
    };
    if ui.retrying {
        if error.is_none() {
            ui.retrying = false;
            ui.retry_grace = false;
        } else if ui.retry_grace {
            ui.retry_grace = false;
            notice = TerrainNotice::Waiting;
        } else if retry == TerrainRetryAvailability::Waiting {
            notice = TerrainNotice::Waiting;
        } else {
            ui.retrying = false;
        }
    }
    if awaiting_player {
        notice = TerrainNotice::Waiting;
    }
    ui.terrain = notice;
    if notice == TerrainNotice::None || recovering {
        // Show a still-active issue again after the higher-priority recovery choice.
        ui.seen_terrain_error = None;
    } else {
        let identity = (
            error.unwrap_or_else(|| "terrain verification".into()),
            sequence,
        );
        if ui.seen_terrain_error.as_ref() != Some(&identity) {
            ui.seen_terrain_error = Some(identity);
            ui.open = true;
            ui.focus = if notice == TerrainNotice::Retryable {
                6
            } else {
                5
            };
        }
    }
    world.insert_resource(TerrainRetryUiStatus {
        failed: notice != TerrainNotice::None,
        retryable: notice == TerrainNotice::Retryable,
        waiting: notice == TerrainNotice::Waiting,
        request_queued: false,
    });
}

#[expect(
    clippy::too_many_lines,
    reason = "One Bevy hierarchy builds the existing graphics dialog and its controls."
)]
pub(super) fn spawn(mut commands: Commands, mut fonts: ResMut<Assets<Font>>) {
    let font = std::env::var_os("WINDIR")
        .and_then(|root| {
            std::fs::read(std::path::PathBuf::from(root).join("Fonts/segoeui.ttf")).ok()
        })
        .map(|bytes| FontSource::Handle(fonts.add(Font::from_bytes(bytes))))
        .unwrap_or_default();
    commands
        .spawn((
            Root,
            GlobalZIndex(100),
            Visibility::Hidden,
            Node {
                display: Display::None,
                position_type: PositionType::Absolute,
                width: Val::Percent(100.0),
                height: Val::Percent(100.0),
                align_items: AlignItems::Center,
                justify_content: JustifyContent::Center,
                ..default()
            },
            BackgroundColor(Color::srgba(0.0, 0.0, 0.0, 0.45)),
        ))
        .with_children(|root| {
            root.spawn((
                ScrollPanel,
                ScrollPosition::default(),
                Node {
                    width: Val::Px(480.0),
                    max_width: Val::Percent(94.0),
                    max_height: Val::Percent(94.0),
                    overflow: Overflow::scroll_y(),
                    flex_direction: FlexDirection::Column,
                    row_gap: Val::Px(10.0),
                    padding: UiRect::all(Val::Px(20.0)),
                    ..default()
                },
                BackgroundColor(Color::srgb(0.025, 0.035, 0.043)),
            ))
            .with_children(|panel| {
                panel.spawn((
                    Title,
                    Text::new("Réglages graphiques"),
                    TextFont {
                        font: font.clone(),
                        font_size: FontSize::Px(23.0),
                        ..default()
                    },
                    TextColor(Color::srgb(0.95, 0.75, 0.42)),
                ));
                panel.spawn((
                    TerrainStatus,
                    Text::new(""),
                    TextFont {
                        font: font.clone(),
                        font_size: FontSize::Px(16.0),
                        ..default()
                    },
                    TextColor(Color::WHITE),
                    Node {
                        display: Display::None,
                        ..default()
                    },
                ));
                button(panel, "Réessayer le terrain", Action::RetryTerrain, &font);
                panel
                    .spawn((
                        Section::Profiles,
                        Node {
                            flex_direction: FlexDirection::Column,
                            row_gap: Val::Px(10.0),
                            ..default()
                        },
                        Visibility::Visible,
                    ))
                    .with_children(|profiles| {
                        for (index, tier) in TIERS.into_iter().enumerate() {
                            button(profiles, label(tier), Action::Choose(index), &font);
                        }
                        profiles.spawn((
                            Text::new(
                                "Choisir un profil réinitialise les réglages avancés en attente.",
                            ),
                            TextFont {
                                font: font.clone(),
                                font_size: FontSize::Px(13.0),
                                ..default()
                            },
                            TextColor(Color::srgb(0.72, 0.78, 0.82)),
                        ));
                    });
                button(panel, "Réglages avancés", Action::Advanced, &font);
                panel
                    .spawn((
                        Section::Advanced,
                        Node {
                            display: Display::None,
                            flex_direction: FlexDirection::Column,
                            row_gap: Val::Px(6.0),
                            ..default()
                        },
                        Visibility::Hidden,
                    ))
                    .with_children(|advanced| {
                        advanced.spawn((
                            PageLabel,
                            Text::new("Réglages 1/5"),
                            TextFont {
                                font: font.clone(),
                                font_size: FontSize::Px(15.0),
                                ..default()
                            },
                            TextColor(Color::WHITE),
                        ));
                        for (index, control) in Control::ALL.into_iter().enumerate() {
                            advanced
                                .spawn((
                                    Section::ControlRow(index),
                                    Visibility::Hidden,
                                    Node {
                                        display: Display::None,
                                        align_items: AlignItems::Center,
                                        justify_content: JustifyContent::SpaceBetween,
                                        column_gap: Val::Px(8.0),
                                        ..default()
                                    },
                                ))
                                .with_children(|row| {
                                    row.spawn((
                                        Text::new(control.label()),
                                        TextFont {
                                            font: font.clone(),
                                            font_size: FontSize::Px(15.0),
                                            ..default()
                                        },
                                        TextColor(Color::WHITE),
                                        Node {
                                            flex_grow: 1.0,
                                            ..default()
                                        },
                                    ));
                                    control_button(row, control, index, false, &font);
                                    row.spawn((
                                        ControlValue(index),
                                        Text::new(""),
                                        TextFont {
                                            font: font.clone(),
                                            font_size: FontSize::Px(15.0),
                                            ..default()
                                        },
                                        TextColor(Color::WHITE),
                                    ));
                                    control_button(row, control, index, true, &font);
                                });
                        }
                        advanced
                            .spawn(Node {
                                flex_direction: FlexDirection::Row,
                                column_gap: Val::Px(8.0),
                                ..default()
                            })
                            .with_children(|pages| {
                                button(pages, "Page précédente", Action::PreviousPage, &font);
                                button(pages, "Page suivante", Action::NextPage, &font);
                            });
                    });
                panel.spawn((
                    Status,
                    Text::new("Préparation du rendu…"),
                    TextFont {
                        font: font.clone(),
                        font_size: FontSize::Px(16.0),
                        ..default()
                    },
                    TextColor(Color::WHITE),
                ));
                button(panel, "Appliquer", Action::Apply, &font);
                button(panel, "Fermer", Action::Close, &font);
                panel.spawn((
                    Text::new("Tab ou flèches : naviguer · Entrée : choisir · Échap : fermer"),
                    TextFont {
                        font: font.clone(),
                        font_size: FontSize::Px(13.0),
                        ..default()
                    },
                    TextColor(Color::srgb(0.72, 0.78, 0.82)),
                ));
            });
        });
}

pub(super) fn scroll(
    mut wheel: MessageReader<MouseWheel>,
    ui: Res<GraphicsUi>,
    mut panels: Query<(&mut ScrollPosition, &ComputedNode), With<ScrollPanel>>,
) {
    let delta: f32 = wheel
        .read()
        .map(|event| {
            event.y
                * match event.unit {
                    MouseScrollUnit::Line => 42.0,
                    MouseScrollUnit::Pixel => 1.0,
                }
        })
        .sum();
    if !ui.open || delta == 0.0 {
        return;
    }
    for (mut position, layout) in &mut panels {
        let scale = layout.inverse_scale_factor();
        let maximum =
            (layout.content_size().y - layout.size().y + layout.scrollbar_size.y).max(0.0);
        let current = layout.scroll_position.y * scale;
        position.y = (current - delta).clamp(0.0, maximum * scale);
    }
}

fn action_is_active(action: Action, ui: &GraphicsUi) -> bool {
    match action {
        Action::RetryTerrain => ui.terrain == TerrainNotice::Retryable,
        Action::Choose(_) => !ui.advanced,
        Action::PreviousPage | Action::NextPage => ui.advanced,
        Action::Adjust(index, _) => ui.advanced && index / CONTROLS_PER_PAGE == ui.page,
        _ => true,
    }
}

/// Keep keyboard focus visible in a small window without overriding wheel scrolling.
pub(super) fn keep_focus_visible(
    ui: Res<GraphicsUi>,
    buttons: Query<(&Action, &ComputedNode, &bevy::ui::UiGlobalTransform), Without<ScrollPanel>>,
    mut panels: Query<
        (
            &ComputedNode,
            &bevy::ui::UiGlobalTransform,
            &mut ScrollPosition,
        ),
        With<ScrollPanel>,
    >,
    mut last_focus: Local<Option<(usize, usize, bool)>>,
) {
    if !ui.open {
        *last_focus = None;
        return;
    }
    let key = (ui.focus, ui.page, ui.advanced);
    if *last_focus == Some(key) {
        return;
    }
    let Some((_, button, button_transform)) = buttons
        .iter()
        .find(|(action, _, _)| action.index() == ui.focus && action_is_active(**action, &ui))
    else {
        return;
    };
    let Ok((panel, panel_transform, mut position)) = panels.single_mut() else {
        return;
    };
    let top = panel_transform.translation.y - panel.size().y * 0.5;
    let bottom = panel_transform.translation.y + panel.size().y * 0.5;
    let button_top = button_transform.translation.y - button.size().y * 0.5;
    let button_bottom = button_transform.translation.y + button.size().y * 0.5;
    let offset = if button_top < top {
        button_top - top
    } else if button_bottom > bottom {
        button_bottom - bottom
    } else {
        0.0
    };
    let scale = panel.inverse_scale_factor();
    let maximum = (panel.content_size().y - panel.size().y + panel.scrollbar_size.y).max(0.0);
    position.y = (panel.scroll_position.y * scale + offset * scale).clamp(0.0, maximum * scale);
    *last_focus = Some(key);
}

fn control_button(
    parent: &mut bevy::ecs::hierarchy::ChildSpawnerCommands,
    control: Control,
    index: usize,
    increase: bool,
    font: &FontSource,
) {
    let action = if increase { "Augmenter" } else { "Diminuer" };
    parent
        .spawn((
            Button,
            bevy::ui::prelude::AccessibleLabel::new(format!("{action} {}", control.label())),
            Action::Adjust(index, increase),
            Node {
                padding: UiRect::axes(Val::Px(12.0), Val::Px(6.0)),
                ..default()
            },
            BackgroundColor(Color::srgb(0.09, 0.17, 0.19)),
        ))
        .with_child((
            Text::new(if increase { "+" } else { "−" }),
            TextFont {
                font: font.clone(),
                font_size: FontSize::Px(17.0),
                ..default()
            },
            TextColor(Color::WHITE),
        ));
}

fn button(
    parent: &mut bevy::ecs::hierarchy::ChildSpawnerCommands,
    text: &str,
    action: Action,
    font: &FontSource,
) {
    let page_button = matches!(action, Action::PreviousPage | Action::NextPage);
    parent
        .spawn((
            Button,
            bevy::ui::prelude::AccessibleLabel::new(text),
            action,
            if matches!(action, Action::RetryTerrain) {
                Visibility::Hidden
            } else {
                Visibility::Visible
            },
            Node {
                display: if matches!(action, Action::RetryTerrain) {
                    Display::None
                } else {
                    Display::Flex
                },
                width: if page_button {
                    Val::Auto
                } else {
                    Val::Percent(100.0)
                },
                flex_grow: if page_button { 1.0 } else { 0.0 },
                padding: UiRect::all(Val::Px(10.0)),
                ..default()
            },
            BackgroundColor(Color::srgb(0.09, 0.17, 0.19)),
        ))
        .with_child((
            Text::new(text),
            TextFont {
                font: font.clone(),
                font_size: FontSize::Px(17.0),
                ..default()
            },
            TextColor(Color::WHITE),
        ));
}

#[derive(SystemParam)]
pub(super) struct Input<'w, 's> {
    keys: Res<'w, ButtonInput<KeyCode>>,
    ui: ResMut<'w, GraphicsUi>,
    state: Res<'w, State>,
    game: Option<ResMut<'w, NativeGameplayUi>>,
    focus: ResMut<'w, PlayerInputFocus>,
    pending: ResMut<'w, PendingPlayerControlCommand>,
    requests: MessageWriter<'w, NativeGraphicsCommand>,
    commands: Commands<'w, 's>,
    buttons: Query<'w, 's, (&'static Interaction, &'static Action), Changed<Interaction>>,
}

#[derive(SystemParam)]
pub(super) struct OptionalEffects<'w> {
    materials: Option<Res<'w, Assets<forge_engine::source_surface::SourceSurfaceMaterial>>>,
    underwater: Option<Res<'w, forge_engine::source_underwater::SourceUnderwaterControl>>,
    lighting: Option<Res<'w, forge_engine::source_underwater_lighting::Control>>,
    refraction: Option<Res<'w, forge_engine::source_refraction_capture::Control>>,
}

#[expect(
    clippy::too_many_lines,
    reason = "The existing dialog actions are handled against one coherent UI and game state."
)]
pub(super) fn input(mut input: Input) {
    input.ui.consumed_pointer = false;
    // Recovery owns input and stays visible even if this panel was already open.
    if input.game.as_ref().is_some_and(|game| game.is_recovering()) {
        return;
    }
    if input.keys.just_pressed(KeyCode::F2) {
        if !input.ui.open {
            if let Some(game) = &mut input.game {
                game.close();
                if game.is_open() {
                    return;
                }
            }
            input.ui.selected = input.state.requested;
            input.ui.draft = input
                .state
                .policy
                .resolve(input.state.requested, input.state.requested_custom)
                .ok()
                .map(|effective| effective.settings);
            input.ui.advanced = false;
            input.ui.page = 0;
            input.ui.focus = TIERS
                .iter()
                .position(|tier| *tier == input.ui.selected)
                .unwrap_or(0);
        }
        input.ui.open = !input.ui.open;
        input.ui.consumed_pointer = true;
    }
    if !input.ui.open {
        return;
    }
    if input.ui.draft.is_none() {
        let custom = (input.ui.selected == input.state.requested)
            .then_some(input.state.requested_custom)
            .flatten();
        input.ui.draft = input
            .state
            .policy
            .resolve(input.ui.selected, custom)
            .ok()
            .map(|effective| effective.settings);
    }
    input.focus.0 = false;
    input.pending.cancel_input();
    if input.keys.just_pressed(KeyCode::Escape) || input.keys.just_pressed(KeyCode::KeyG) {
        input.ui.open = false;
        input.ui.consumed_pointer = true;
        return;
    }
    let mut focus_order = if input.ui.advanced {
        let mut order = vec![7, 8];
        order.extend(10..18);
        order.extend([9, 4, 5]);
        order
    } else {
        vec![0, 1, 2, 3, 4, 5, 7]
    };
    if input.ui.terrain == TerrainNotice::Retryable {
        focus_order.push(6);
    }
    if !focus_order.contains(&input.ui.focus) {
        input.ui.focus = 5;
    }
    let index = focus_order
        .iter()
        .position(|focus| *focus == input.ui.focus)
        .unwrap_or(0);
    let backwards =
        input.keys.pressed(KeyCode::ShiftLeft) || input.keys.pressed(KeyCode::ShiftRight);
    if input.keys.just_pressed(KeyCode::ArrowDown)
        || input.keys.just_pressed(KeyCode::ArrowRight)
        || input.keys.just_pressed(KeyCode::Tab) && !backwards
    {
        input.ui.focus = focus_order[(index + 1) % focus_order.len()];
    }
    if input.keys.just_pressed(KeyCode::ArrowUp)
        || input.keys.just_pressed(KeyCode::ArrowLeft)
        || input.keys.just_pressed(KeyCode::Tab) && backwards
    {
        input.ui.focus = focus_order[(index + focus_order.len() - 1) % focus_order.len()];
    }
    let mut action = None;
    if input.keys.just_pressed(KeyCode::Enter) || input.keys.just_pressed(KeyCode::Space) {
        action = Some(match input.ui.focus {
            0..=3 => Action::Choose(input.ui.focus),
            4 => Action::Apply,
            5 => Action::Close,
            6 => Action::RetryTerrain,
            7 => Action::Advanced,
            8 => Action::PreviousPage,
            9 => Action::NextPage,
            index => Action::Adjust(
                input.ui.page * CONTROLS_PER_PAGE + (index - 10) / 2,
                (index - 10) % 2 == 1,
            ),
        });
    }
    for (interaction, button) in &input.buttons {
        let active = action_is_active(*button, &input.ui);
        if active && *interaction == Interaction::Hovered {
            input.ui.focus = button.index();
        }
        if active && *interaction == Interaction::Pressed {
            action = Some(*button);
        }
    }
    if let Some(action) = action {
        dispatch_action(&mut input, action);
    }
}

fn dispatch_action(input: &mut Input, action: Action) {
    input.ui.consumed_pointer = true;
    match action {
        Action::Choose(index) => {
            input.ui.selected = TIERS[index];
            input.ui.focus = index;
            input.ui.draft = input
                .state
                .policy
                .resolve(input.ui.selected, None)
                .ok()
                .map(|effective| effective.settings);
        }
        Action::Apply => {
            let tier = input.ui.selected;
            let baseline = input.state.policy.resolve(tier, None).ok();
            if let (Some(settings), Some(baseline)) = (input.ui.draft, baseline)
                && settings != baseline.settings
            {
                input.commands.queue(move |world: &mut World| {
                    world.write_message(super::CustomSettingsCommand { tier, settings });
                });
            } else {
                input.requests.write(NativeGraphicsCommand(tier));
            }
        }
        Action::Close => input.ui.open = false,
        Action::Advanced => {
            input.ui.advanced = !input.ui.advanced;
            input.ui.focus = 7;
        }
        Action::PreviousPage if input.ui.advanced => {
            input.ui.page = (input.ui.page + CONTROL_PAGES - 1) % CONTROL_PAGES;
        }
        Action::NextPage if input.ui.advanced => {
            input.ui.page = (input.ui.page + 1) % CONTROL_PAGES;
        }
        Action::Adjust(index, increase) if input.ui.advanced => {
            if let (Some(control), Some(settings)) = (Control::ALL.get(index), &mut input.ui.draft)
            {
                let direction = if increase { 1.0 } else { -1.0 };
                if let Err(error) = control.step(settings, direction) {
                    tracing::warn!(%error, "Graphics control rejected");
                }
            }
        }
        Action::RetryTerrain if input.ui.terrain == TerrainNotice::Retryable => {
            input.commands.queue(|world: &mut World| {
                let accepted = retry_failed_terrain(world);
                if accepted {
                    let mut ui = world.resource_mut::<GraphicsUi>();
                    ui.retrying = true;
                    ui.retry_grace = true;
                    ui.terrain = TerrainNotice::Waiting;
                }
            });
        }
        Action::PreviousPage | Action::NextPage | Action::Adjust(_, _) | Action::RetryTerrain => {}
    }
}

#[allow(
    clippy::too_many_arguments,
    clippy::type_complexity,
    reason = "Bevy UI state and disjoint text queries"
)]
#[expect(
    clippy::too_many_lines,
    reason = "Existing dialog text and controls observe the same current graphics transaction."
)]
pub(super) fn update(
    mut ui: ResMut<GraphicsUi>,
    state: Res<State>,
    display: Option<Res<forge_engine::native_display::InitialDisplayStatus>>,
    game: Option<Res<NativeGameplayUi>>,
    mut display_reported: Local<bool>,
    mut commands: Commands,
    mut root: Query<
        (&mut Node, &mut Visibility),
        (With<Root>, Without<Action>, Without<TerrainStatus>),
    >,
    mut buttons: Query<
        (
            &Action,
            Entity,
            &mut BackgroundColor,
            &Children,
            &mut Node,
            &mut Visibility,
            Option<&bevy::ui::prelude::AccessibleLabel>,
        ),
        (Without<Root>, Without<TerrainStatus>),
    >,
    mut texts: Query<&mut Text>,
    statuses: Query<Entity, With<Status>>,
    titles: Query<Entity, With<Title>>,
    mut terrain_statuses: Query<
        (Entity, &mut Node),
        (With<TerrainStatus>, Without<Root>, Without<Action>),
    >,
    mut sections: Query<
        (&Section, &mut Node, &mut Visibility),
        (Without<Root>, Without<Action>, Without<TerrainStatus>),
    >,
    values: Query<(Entity, &ControlValue)>,
    pages: Query<Entity, With<PageLabel>>,
    effects: OptionalEffects,
) {
    let display_warning = display
        .as_deref()
        .and_then(forge_engine::native_display::InitialDisplayStatus::fallback_message);
    if display_warning.is_some() && !*display_reported {
        ui.open = true;
        *display_reported = true;
    }
    let visible_panel = ui.open && !game.as_ref().is_some_and(|game| game.is_recovering());
    for (mut node, mut visible) in &mut root {
        let display = if visible_panel {
            Display::Flex
        } else {
            Display::None
        };
        if node.display != display {
            node.display = display;
        }
        let visibility = if visible_panel {
            Visibility::Visible
        } else {
            Visibility::Hidden
        };
        if *visible != visibility {
            *visible = visibility;
        }
    }
    if !visible_panel {
        return;
    }
    for (section, mut node, mut visible) in &mut sections {
        let shown = match section {
            Section::Profiles => !ui.advanced,
            Section::Advanced => ui.advanced,
            Section::ControlRow(index) => ui.advanced && *index / CONTROLS_PER_PAGE == ui.page,
        };
        let display = if shown { Display::Flex } else { Display::None };
        if node.display != display {
            node.display = display;
        }
        let visibility = if shown {
            Visibility::Inherited
        } else {
            Visibility::Hidden
        };
        if *visible != visibility {
            *visible = visibility;
        }
    }
    for (action, entity, mut color, children, mut node, mut visibility, accessible) in &mut buttons
    {
        if matches!(action, Action::RetryTerrain) {
            let visible = ui.terrain == TerrainNotice::Retryable;
            let display = if visible {
                Display::Flex
            } else {
                Display::None
            };
            if node.display != display {
                node.display = display;
            }
            let wanted = if visible {
                Visibility::Inherited
            } else {
                Visibility::Hidden
            };
            if *visibility != wanted {
                *visibility = wanted;
            }
        }
        if !action_is_active(*action, &ui) {
            continue;
        }
        let selected = matches!(action, Action::Choose(index) if TIERS[*index] == ui.selected);
        let wanted = if action.index() == ui.focus {
            Color::srgb(0.18, 0.32, 0.35)
        } else if selected {
            Color::srgb(0.14, 0.25, 0.27)
        } else {
            Color::srgb(0.09, 0.17, 0.19)
        };
        if color.0 != wanted {
            color.0 = wanted;
        }
        for child in children {
            if let Ok(mut text) = texts.get_mut(*child) {
                match action {
                    Action::Choose(index) => {
                        let value = format!(
                            "{}{}",
                            label(TIERS[*index]),
                            if selected { " · sélectionné" } else { "" }
                        );
                        if text.0 != value {
                            text.0 = value;
                        }
                        if accessible.is_some_and(|accessible| accessible.0 != text.0) {
                            commands
                                .entity(entity)
                                .insert(bevy::ui::prelude::AccessibleLabel::new(text.0.clone()));
                        }
                    }
                    Action::Advanced => {
                        let value = if ui.advanced {
                            "Retour aux profils"
                        } else {
                            "Réglages avancés"
                        };
                        if text.0 != value {
                            text.0 = value.into();
                        }
                        if accessible.is_some_and(|accessible| accessible.0 != text.0) {
                            commands
                                .entity(entity)
                                .insert(bevy::ui::prelude::AccessibleLabel::new(text.0.clone()));
                        }
                    }
                    _ => {}
                }
            }
        }
    }
    if ui.open && ui.advanced {
        for entity in &pages {
            if let Ok(mut text) = texts.get_mut(entity) {
                let value = format!("Réglages {}/{}", ui.page + 1, CONTROL_PAGES);
                if text.0 != value {
                    text.0 = value;
                }
            }
        }
    }
    let bounce_page = Control::ALL
        .iter()
        .position(|control| *control == Control::BounceLight)
        .is_some_and(|index| index / CONTROLS_PER_PAGE == ui.page);
    let has_bounce = ui.open
        && ui.advanced
        && bounce_page
        && effects.materials.as_ref().is_some_and(|materials| {
            materials
                .iter()
                .any(|(_, material)| material.ground_bounce.is_some())
        });
    for (entity, value) in &values {
        if !ui.open || !ui.advanced || value.0 / CONTROLS_PER_PAGE != ui.page {
            continue;
        }
        let Some(settings) = ui.draft else { continue };
        let control = Control::ALL[value.0];
        let unavailable = match control {
            Control::UnderwaterShafts => effects.underwater.is_none(),
            Control::UnderwaterLighting
            | Control::UnderwaterCaustics
            | Control::UnderwaterShadowRefraction => effects.lighting.is_none(),
            Control::WaterRefractionCapture => effects.refraction.is_none(),
            Control::BounceLight => !has_bounce,
            _ => false,
        };
        if let Ok(mut text) = texts.get_mut(entity) {
            let mut value = control.display(settings);
            if unavailable {
                value.push_str(" · sans effet ici");
            }
            if text.0 != value {
                text.0 = value;
            }
        }
    }
    let mut messages = Vec::new();
    if state.error.is_some() {
        messages.push("Impossible d'appliquer ce profil. Réessayez.".to_owned());
    }
    if state.preference_error.is_some() {
        if state
            .preference_error
            .as_deref()
            .is_some_and(|error| error.contains("Legacy graphics preferences changed"))
        {
            messages.push(
                "Une autre version a modifié les réglages. Choisissez et appliquez un profil pour confirmer."
                    .to_owned(),
            );
        } else {
            messages
                .push("Réglages non enregistrés. Réappliquez un profil pour réessayer.".to_owned());
        }
    }
    if messages.is_empty() {
        let name = if state.requested_custom.is_some() {
            format!("Personnalisé ({})", label(state.requested))
        } else {
            label(state.requested).to_owned()
        };
        let pending = ui.draft.is_some_and(|settings| {
            state
                .policy
                .resolve(state.requested, state.requested_custom)
                .is_ok_and(|current| ui.selected != state.requested || settings != current.settings)
        });
        messages.push(if pending {
            "Modifications en attente. Appliquez pour les enregistrer.".into()
        } else if !state.ready {
            format!("Application de {name}…")
        } else if state.saved == Some(state.requested)
            && state.saved_custom == state.requested_custom
        {
            if state.requested_custom.is_some() {
                format!("{name} · enregistré")
            } else {
                format!("{name} · appliqué et enregistré")
            }
        } else {
            format!("{name} · appliqué")
        });
    }
    if ui.draft.is_some_and(|settings| {
        settings.temporal_aa <= 0.5
            && state
                .policy
                .preset(ui.selected)
                .shadows
                .contact_shadows_enabled
    }) {
        messages.push("Sans TAA, les ombres de contact sont désactivées.".into());
    }
    let mut message = messages.join("\n");
    if let Some(warning) = display_warning {
        message.push('\n');
        message.push_str(warning);
    }
    for entity in &statuses {
        if let Ok(mut text) = texts.get_mut(entity)
            && text.0 != message
        {
            text.0.clone_from(&message);
        }
    }
    update_terrain_presentation(&ui, &titles, &mut terrain_statuses, &mut texts);
}

#[allow(
    clippy::type_complexity,
    reason = "Disjoint Bevy terrain presentation queries"
)]
fn update_terrain_presentation(
    ui: &GraphicsUi,
    titles: &Query<Entity, With<Title>>,
    terrain_statuses: &mut Query<
        (Entity, &mut Node),
        (With<TerrainStatus>, Without<Root>, Without<Action>),
    >,
    texts: &mut Query<&mut Text>,
) {
    for entity in titles.iter() {
        if let Ok(mut text) = texts.get_mut(entity) {
            let title = if ui.terrain == TerrainNotice::None {
                "Réglages graphiques"
            } else {
                "Chargement du terrain"
            };
            if text.0 != title {
                text.0 = title.into();
            }
        }
    }
    let terrain_message = match ui.terrain {
        TerrainNotice::None => "",
        TerrainNotice::Waiting => {
            "Vérification du terrain en cours. Si la partie reste bloquée, fermez puis relancez le jeu. La progression non sauvegardée peut être perdue."
        }
        TerrainNotice::Retryable => "La partie est en pause. Le terrain n’a pas pu être chargé.",
        TerrainNotice::Unavailable => {
            "La partie est en pause. Le terrain ne peut pas être repris ici. Fermez puis relancez le jeu. La progression non sauvegardée peut être perdue."
        }
    };
    for (entity, mut node) in terrain_statuses.iter_mut() {
        let display = if terrain_message.is_empty() {
            Display::None
        } else {
            Display::Flex
        };
        if node.display != display {
            node.display = display;
        }
        if let Ok(mut text) = texts.get_mut(entity)
            && text.0 != terrain_message
        {
            text.0 = terrain_message.into();
        }
    }
}
