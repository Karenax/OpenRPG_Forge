//! Bevy UI for normal authored commands. Range and LOS are still rechecked by simulation.
use super::{NativeGameplayCommand, NativeGameplayUi};
use crate::{PlayerInputFocus, gameplay::GameplayRuntime};
use bevy::{
    input::mouse::{MouseScrollUnit, MouseWheel},
    prelude::*,
};
use bevy_rapier3d::prelude::{QueryFilter, RapierContext, ReadRapierContext};
use forge_core::EntityId;
use forge_gameplay::{ActorRole, GameAction, GameSession, ObjectKind, QuestStatus};

#[derive(Clone, Debug, Default, PartialEq)]
pub(super) struct GameView {
    summary: String,
    dialogues: Vec<(EntityId, String)>,
    pub(super) actions: Vec<(String, GameAction)>,
}
#[derive(Component)]
pub(super) struct Hud;
#[derive(Component)]
pub(super) struct Panel;
#[derive(Component)]
pub(super) struct ActionArea;
#[derive(Component, Clone)]
pub(super) struct UiCommand(NativeGameplayCommand);
#[derive(Resource, Default)]
pub(super) struct UiFont(FontSource);

pub(super) fn spawn(mut commands: Commands, mut fonts: ResMut<Assets<Font>>) {
    let font = std::env::var_os("WINDIR")
        .and_then(|root| {
            std::fs::read(std::path::PathBuf::from(root).join("Fonts/segoeui.ttf")).ok()
        })
        .map(|bytes| FontSource::Handle(fonts.add(Font::from_bytes(bytes))))
        .unwrap_or_default();
    commands.insert_resource(UiFont(font.clone()));
    commands.spawn((
        Hud,
        Visibility::Visible,
        Text::new("G : actions/dialogue · F5 : sauvegarder · F9 : reprise au prochain lancement"),
        TextFont {
            font: font.clone(),
            font_size: FontSize::Px(17.0),
            ..default()
        },
        TextColor(Color::WHITE),
        Node {
            position_type: PositionType::Absolute,
            top: Val::Px(48.0),
            left: Val::Px(22.0),
            max_width: Val::Percent(68.0),
            padding: UiRect::all(Val::Px(10.0)),
            ..default()
        },
        BackgroundColor(Color::srgba(0.025, 0.035, 0.043, 0.92)),
    ));
    commands.spawn((Panel, Visibility::Hidden,
        Node { position_type: PositionType::Absolute, top: Val::Px(90.0), left: Val::Px(22.0),
            width: Val::Px(510.0), max_height: Val::Percent(82.0), overflow: Overflow::scroll_y(),
            flex_direction: FlexDirection::Column, row_gap: Val::Px(8.0), padding: UiRect::all(Val::Px(18.0)), ..default() },
        BackgroundColor(Color::srgba(0.025, 0.035, 0.043, 0.96)),
            ScrollPosition::default(),
    )).with_children(|panel| {
        panel.spawn((Text::new("PARTIE · G ferme ce panneau"), TextFont { font: font.clone(), font_size: FontSize::Px(19.0), ..default() }, TextColor(Color::srgb(0.95, 0.75, 0.42))));
        panel.spawn((ActionArea, Node { flex_direction: FlexDirection::Column, row_gap: Val::Px(8.0), ..default() }));
        button(panel, "Sauvegarder cette partie (F5)", NativeGameplayCommand::Save, &font);
        button(panel, "Reprendre au prochain lancement (F9)", NativeGameplayCommand::ResumeNextLaunch { allow_compatible_edits: false }, &font);
        button(panel, "Reprendre après des retouches compatibles", NativeGameplayCommand::ResumeNextLaunch { allow_compatible_edits: true }, &font);
        button(panel, "Nouvelle partie (conserver la sauvegarde)", NativeGameplayCommand::StartFresh, &font);
        button(panel, "Quitter le jeu", NativeGameplayCommand::Quit, &font);
        panel.spawn((Text::new("La reprise conserve la dernière sauvegarde. Fermez puis relancez le jeu après l’avoir demandée. Une nouvelle partie n’écrase rien avant votre sauvegarde explicite."),
            TextFont { font: font.clone(), font_size: FontSize::Px(14.0), ..default() }, TextColor(Color::srgb(0.72, 0.78, 0.82))));
    });
}

fn button(
    parent: &mut bevy::ecs::hierarchy::ChildSpawnerCommands,
    label: &str,
    command: NativeGameplayCommand,
    font: &FontSource,
) {
    parent
        .spawn((
            Button,
            UiCommand(command),
            Node {
                padding: UiRect::all(Val::Px(10.0)),
                width: Val::Percent(100.0),
                ..default()
            },
            BackgroundColor(Color::srgb(0.09, 0.17, 0.19)),
        ))
        .with_child((
            Text::new(label),
            TextFont {
                font: font.clone(),
                font_size: FontSize::Px(16.0),
                ..default()
            },
            TextColor(Color::WHITE),
        ));
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
pub(super) fn input(
    keys: Res<ButtonInput<KeyCode>>,
    mut ui: ResMut<NativeGameplayUi>,
    mut messages: MessageWriter<NativeGameplayCommand>,
    mut focus: ResMut<PlayerInputFocus>,
    buttons: Query<(&Interaction, &UiCommand), Changed<Interaction>>,
) {
    if keys.just_pressed(KeyCode::KeyG) && ui.recovery.is_none() {
        ui.open = !ui.open;
    }
    if ui.recovery.is_some() {
        ui.open = true;
    }
    if keys.just_pressed(KeyCode::F5) {
        messages.write(NativeGameplayCommand::Save);
    }
    if keys.just_pressed(KeyCode::F9) {
        messages.write(NativeGameplayCommand::ResumeNextLaunch {
            allow_compatible_edits: false,
        });
    }
    if ui.open {
        focus.0 = false;
        for (interaction, command) in &buttons {
            if *interaction == Interaction::Pressed {
                messages.write(command.0.clone());
            }
        }
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
pub(super) fn scroll(
    mut wheel: MessageReader<MouseWheel>,
    ui: Res<NativeGameplayUi>,
    mut panels: Query<(&mut ScrollPosition, &ComputedNode), With<Panel>>,
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
        let max_physical =
            (layout.content_size().y - layout.size().y + layout.scrollbar_size.y).max(0.0);
        position.y = (layout.scroll_position.y * scale - delta).clamp(0.0, max_physical * scale);
    }
}

type HudQuery<'w, 's> =
    Query<'w, 's, (&'static mut Text, &'static mut Visibility), (With<Hud>, Without<Panel>)>;

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    reason = "One native view reads simulation and current Rapier visibility; UI never fabricates observations"
)]
pub(super) fn refresh(
    mut commands: Commands,
    runtime: Res<GameplayRuntime>,
    mut ui: ResMut<NativeGameplayUi>,
    font: Res<UiFont>,
    rapier: ReadRapierContext,
    player: Query<Entity, With<crate::ForgePlayerCharacter>>,
    mut hud: HudQuery,
    mut panels: Query<&mut Visibility, With<Panel>>,
    area: Query<(Entity, Option<&Children>), With<ActionArea>>,
    mut recovery_buttons: Query<(&UiCommand, &mut Node)>,
) {
    let mut view = game_view(runtime.session());
    let context = rapier.single().ok();
    let player = player.single().ok();
    filter_visibility(&mut view, &runtime, context.as_ref(), player);
    refresh_panels(&ui, &mut recovery_buttons, &mut panels);
    if let Ok((mut text, mut visibility)) = hud.single_mut() {
        *visibility = if ui.open {
            Visibility::Hidden
        } else {
            Visibility::Visible
        };
        let status = runtime.error().unwrap_or(runtime.feedback());
        let next = format!(
            "{}\n{}\n{}\nG : actions/dialogue · F5 : sauvegarder · F9 : reprise",
            view.summary, ui.feedback, status
        );
        if text.0 != next {
            text.0 = next;
        }
    }
    if ui.last_view.as_ref() == Some(&view) {
        return;
    }
    let Ok((area, children)) = area.single() else {
        return;
    };
    if let Some(children) = children {
        for child in children.iter() {
            commands.entity(child).despawn();
        }
    }
    render_actions(&mut commands, area, &view, &font.0);
    ui.last_view = Some(view);
}
fn filter_visibility(
    view: &mut GameView,
    runtime: &GameplayRuntime,
    context: Option<&RapierContext>,
    player: Option<Entity>,
) {
    // Only current range-qualified, visible actions are shown. The gameplay adapter
    // independently rechecks LOS/contact/water when it executes each queued request.
    let visible_target = |id| {
        runtime.is_ready()
            && context.zip(player).is_some_and(|(context, player)| {
                let state = runtime.session().state();
                let origin = state.actors[&runtime.session().document().player].position;
                let destination = state
                    .actors
                    .get(&id)
                    .map(|actor| actor.position)
                    .or_else(|| state.objects.get(&id).map(|object| object.position));
                destination.is_some_and(|destination| {
                    let origin = Vec3::new(origin.x, origin.y, origin.z);
                    let delta = Vec3::new(destination.x, destination.y, destination.z) - origin;
                    let distance = delta.length();
                    distance <= 0.001
                        || context
                            .cast_ray(
                                origin,
                                delta / distance,
                                distance,
                                true,
                                QueryFilter::default().exclude_collider(player),
                            )
                            .is_none_or(|(hit, _)| runtime.entities.get(&id).copied() == Some(hit))
                })
            })
    };
    view.actions
        .retain(|(_, action)| !runtime.has_queued_actions() && visible_target(target(action)));
    view.dialogues
        .retain(|(speaker, _)| visible_target(*speaker));
}
fn refresh_panels(
    ui: &NativeGameplayUi,
    recovery_buttons: &mut Query<(&UiCommand, &mut Node)>,
    panels: &mut Query<&mut Visibility, With<Panel>>,
) {
    for (command, mut node) in recovery_buttons.iter_mut() {
        if command.0 == NativeGameplayCommand::StartFresh {
            let display = if ui.recovery.is_some() {
                Display::Flex
            } else {
                Display::None
            };
            if node.display != display {
                node.display = display;
            }
        }
    }
    for mut visibility in panels.iter_mut() {
        visibility.set_if_neq(if ui.open {
            Visibility::Visible
        } else {
            Visibility::Hidden
        });
    }
}
fn render_actions(commands: &mut Commands, area: Entity, view: &GameView, font: &FontSource) {
    commands.entity(area).with_children(|parent| {
        parent.spawn((
            Text::new(format!(
                "{}\n{}",
                view.summary,
                view.dialogues
                    .iter()
                    .map(|(_, text)| text.as_str())
                    .collect::<Vec<_>>()
                    .join("\n")
            )),
            TextFont {
                font: font.clone(),
                font_size: FontSize::Px(16.0),
                ..default()
            },
            TextColor(Color::WHITE),
        ));
        if view.actions.is_empty() {
            parent.spawn((
                Text::new("Approchez une cible visible pour interagir."),
                TextFont {
                    font: font.clone(),
                    font_size: FontSize::Px(15.0),
                    ..default()
                },
                TextColor(Color::srgb(0.73, 0.78, 0.82)),
            ));
        }
        for (label, action) in &view.actions {
            button(
                parent,
                label,
                NativeGameplayCommand::Action(action.clone()),
                font,
            );
        }
    });
}

pub(super) fn target(action: &GameAction) -> EntityId {
    match *action {
        GameAction::ChooseDialogue { speaker, .. } => speaker,
        GameAction::Attack { target } => target,
        GameAction::Pickup { object }
        | GameAction::SetDoor { object, .. }
        | GameAction::Push { object, .. }
        | GameAction::Throw { object, .. }
        | GameAction::Ignite { object }
        | GameAction::Extinguish { object } => object,
    }
}

pub(super) fn game_view(session: &GameSession) -> GameView {
    let document = session.document();
    let state = session.state();
    let player = &state.actors[&document.player];
    let inventory = player
        .inventory
        .iter()
        .map(|(id, quantity)| {
            format!(
                "{} × {quantity}",
                document
                    .item(*id)
                    .map_or("Objet", |item| item.name.as_str())
            )
        })
        .collect::<Vec<_>>()
        .join(" · ");
    let quests = document
        .quests
        .iter()
        .map(|quest| {
            format!(
                "{} : {}",
                quest.name,
                quest_status_label(state.quests[&quest.id])
            )
        })
        .collect::<Vec<_>>()
        .join(" · ");
    let mut view = GameView {
        summary: format!(
            "Vitalité {} · Endurance {}\nInventaire : {}\nQuêtes : {}",
            player.vitality,
            player.stamina,
            if inventory.is_empty() {
                "vide"
            } else {
                &inventory
            },
            if quests.is_empty() { "aucune" } else { &quests }
        ),
        ..default()
    };
    actor_actions(session, &mut view);
    object_actions(session, &mut view);
    view
}
fn actor_actions(session: &GameSession, view: &mut GameView) {
    let document = session.document();
    let state = session.state();
    let player = &state.actors[&document.player];
    for actor in &document.actors {
        if actor.id == document.player || state.actors[&actor.id].vitality == 0 {
            continue;
        }
        if let Ok(dialogue) = session.dialogue_view(document.player, actor.id) {
            view.dialogues
                .push((actor.id, format!("{} : {}", actor.name, dialogue.text)));
            view.actions
                .extend(dialogue.choices.into_iter().map(|choice| {
                    (
                        choice.text,
                        GameAction::ChooseDialogue {
                            speaker: actor.id,
                            node: dialogue.node,
                            choice: choice.id,
                        },
                    )
                }));
        }
        if actor.role == ActorRole::Enemy
            && distance_squared(player.position, state.actors[&actor.id].position)
                <= document.profile.attack_range_m.powi(2)
        {
            view.actions.push((
                format!("Frapper {}", actor.name),
                GameAction::Attack { target: actor.id },
            ));
        }
    }
}
fn object_actions(session: &GameSession, view: &mut GameView) {
    let document = session.document();
    let state = session.state();
    let player = &state.actors[&document.player];
    for object in &document.objects {
        let current = &state.objects[&object.id];
        if current.consumed
            || current.destroyed
            || distance_squared(player.position, current.position)
                > document.profile.interaction_range_m.powi(2)
        {
            continue;
        }
        match object.kind {
            ObjectKind::Pickup { .. } => view.actions.push((
                format!("Ramasser {}", object.name),
                GameAction::Pickup { object: object.id },
            )),
            ObjectKind::Door { .. } => view.actions.push((
                format!(
                    "{} {}",
                    if current.open { "Fermer" } else { "Ouvrir" },
                    object.name
                ),
                GameAction::SetDoor {
                    object: object.id,
                    open: !current.open,
                },
            )),
            ObjectKind::Prop => (),
        }
        let delta = Vec3::new(
            current.position.x - player.position.x,
            0.0,
            current.position.z - player.position.z,
        )
        .normalize_or_zero();
        let impulse = |speed: f32, up: f32| {
            forge_core::Vec3::new(
                delta.x * object.physics.mass_kg * speed,
                object.physics.mass_kg * up,
                delta.z * object.physics.mass_kg * speed,
            )
        };
        if object.physics.pushable {
            view.actions.push((
                format!("Pousser {}", object.name),
                GameAction::Push {
                    object: object.id,
                    impulse: impulse(2.0, 0.0),
                },
            ));
        }
        if object.physics.throwable {
            view.actions.push((
                format!("Lancer {}", object.name),
                GameAction::Throw {
                    object: object.id,
                    impulse: impulse(5.0, 3.0),
                },
            ));
        }
        if object.physics.flammable {
            view.actions.push((
                format!(
                    "{} {}",
                    if current.burning {
                        "Éteindre"
                    } else {
                        "Allumer"
                    },
                    object.name
                ),
                if current.burning {
                    GameAction::Extinguish { object: object.id }
                } else {
                    GameAction::Ignite { object: object.id }
                },
            ));
        }
    }
}
fn distance_squared(a: forge_core::Vec3, b: forge_core::Vec3) -> f32 {
    (a.x - b.x).powi(2) + (a.y - b.y).powi(2) + (a.z - b.z).powi(2)
}
fn quest_status_label(status: QuestStatus) -> &'static str {
    match status {
        QuestStatus::Available => "À découvrir",
        QuestStatus::Active => "En cours",
        QuestStatus::Completed => "Terminée",
    }
}
