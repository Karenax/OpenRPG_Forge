use super::{Action, Indicator, Panel, State};
use bevy::{prelude::*, text::FontSource};
#[derive(Component)]
pub(super) struct HourTrack;
#[derive(Component)]
pub(super) struct HourFill;
#[derive(Component)]
pub(super) struct HourLabel;
#[derive(Component)]
pub(super) struct ChoiceLabel(Indicator);
#[derive(Component)]
pub(super) struct Hud(pub Indicator);
#[derive(Resource, Default)]
pub(super) struct Metrics {
    elapsed: f64,
    frames: u32,
    pub fps: f64,
    pub frame_ms: f64,
    pub samples: u32,
}
#[allow(
    clippy::type_complexity,
    reason = "One disjoint text query handles the menu labels and three optional HUD indicators"
)]
pub(super) fn refresh(
    time: Res<Time<Real>>,
    state: Res<State>,
    mut metrics: ResMut<Metrics>,
    mut labels: Query<(
        &mut Text,
        Option<&ChoiceLabel>,
        Option<&HourLabel>,
        Option<&Hud>,
    )>,
    mut hud: Query<(&Hud, &mut Visibility)>,
    mut fills: Query<&mut Node, With<HourFill>>,
) {
    let delta = time.delta_secs_f64();
    if delta.is_finite() && delta > 0.0 {
        metrics.elapsed += delta;
        metrics.frames += 1;
        if metrics.elapsed >= 0.5 {
            metrics.fps = f64::from(metrics.frames) / metrics.elapsed;
            metrics.frame_ms = metrics.elapsed * 1000.0 / f64::from(metrics.frames);
            metrics.samples += metrics.frames;
            metrics.elapsed = 0.0;
            metrics.frames = 0;
        }
    }
    for (mut text, choice, hour, hud) in &mut labels {
        let next = if let Some(choice) = choice {
            let name = match choice.0 {
                Indicator::Fps => "Afficher les FPS",
                Indicator::Frame => "Temps d’image (ms)",
                Indicator::Clock => "Afficher l’heure du jour",
            };
            format!(
                "{}  {}",
                if state.preferences.enabled(choice.0) {
                    "[x]"
                } else {
                    "[ ]"
                },
                name
            )
        } else if hour.is_some() {
            state.preferences.hour_label()
        } else if let Some(hud) = hud {
            match hud.0 {
                Indicator::Fps => {
                    if metrics.samples > 0 {
                        format!("{:.0} FPS", metrics.fps)
                    } else {
                        "— FPS".into()
                    }
                }
                Indicator::Frame => {
                    if metrics.samples > 0 {
                        format!("{:.1} ms / image", metrics.frame_ms)
                    } else {
                        "— ms / image".into()
                    }
                }
                Indicator::Clock => state.preferences.hour_label(),
            }
        } else {
            continue;
        };
        if text.0 != next {
            text.0 = next;
        }
    }
    for (indicator, mut visibility) in &mut hud {
        visibility.set_if_neq(if state.preferences.enabled(indicator.0) {
            Visibility::Visible
        } else {
            Visibility::Hidden
        });
    }
    for mut fill in &mut fills {
        fill.width = Val::Percent(f32::from(state.preferences.minutes) / 14.4);
    }
}
fn text(value: impl Into<String>, font: &FontSource, size: f32) -> (Text, TextFont, TextColor) {
    (
        Text::new(value),
        TextFont {
            font: font.clone(),
            font_size: FontSize::Px(size),
            ..default()
        },
        TextColor(Color::srgb(0.15, 0.24, 0.28)),
    )
}
#[allow(
    clippy::too_many_lines,
    reason = "Keep the compact menu hierarchy and its HUD construction together"
)]
pub(super) fn spawn(mut commands: Commands, mut fonts: ResMut<Assets<Font>>) {
    let font = std::fs::read(r"C:\Windows\Fonts\segoeui.ttf")
        .ok()
        .map(|bytes| FontSource::Handle(fonts.add(Font::from_bytes(bytes))))
        .unwrap_or_default();
    commands
        .spawn((
            Panel,
            Visibility::Hidden,
            Node {
                position_type: PositionType::Absolute,
                width: Val::Percent(100.0),
                height: Val::Percent(100.0),
                align_items: AlignItems::Center,
                justify_content: JustifyContent::Center,
                ..default()
            },
            BackgroundColor(Color::srgba(0.04, 0.08, 0.10, 0.16)),
        ))
        .with_children(|overlay| {
            overlay
                .spawn((
                    Node {
                        width: Val::Px(352.0),
                        max_width: Val::Percent(90.0),
                        flex_direction: FlexDirection::Column,
                        row_gap: Val::Px(12.0),
                        padding: UiRect::all(Val::Px(22.0)),
                        ..default()
                    },
                    BackgroundColor(Color::srgb(0.96, 0.94, 0.88)),
                ))
                .with_children(|panel| {
                    panel.spawn(text("Affichage & lumière", &font, 20.0));
                    panel.spawn(text("Tidewater · Échap pour reprendre", &font, 12.0));
                    for indicator in [Indicator::Fps, Indicator::Frame, Indicator::Clock] {
                        panel
                            .spawn((
                                Button,
                                Action::Toggle(indicator),
                                Node {
                                    width: Val::Percent(100.0),
                                    min_height: Val::Px(38.0),
                                    align_items: AlignItems::Center,
                                    padding: UiRect::all(Val::Px(7.0)),
                                    ..default()
                                },
                                BackgroundColor(Color::srgb(0.91, 0.90, 0.85)),
                            ))
                            .with_children(|row| {
                                row.spawn((ChoiceLabel(indicator), text("", &font, 14.0)));
                            });
                    }
                    panel
                        .spawn(Node {
                            justify_content: JustifyContent::SpaceBetween,
                            ..default()
                        })
                        .with_children(|row| {
                            row.spawn(text("Régler la lumière", &font, 14.0));
                            row.spawn((HourLabel, text("16:12", &font, 14.0)));
                        });
                    panel
                        .spawn((
                            Button,
                            HourTrack,
                            bevy::ui::RelativeCursorPosition::default(),
                            Node {
                                width: Val::Percent(100.0),
                                height: Val::Px(28.0),
                                align_items: AlignItems::Center,
                                ..default()
                            },
                            BackgroundColor(Color::srgb(0.84, 0.85, 0.80)),
                        ))
                        .with_children(|track| {
                            track.spawn((
                                HourFill,
                                bevy::ui::FocusPolicy::Pass,
                                Node {
                                    width: Val::Percent(67.5),
                                    height: Val::Percent(100.0),
                                    ..default()
                                },
                                BackgroundColor(Color::srgb(0.72, 0.49, 0.23)),
                            ));
                        });
                    panel
                        .spawn(Node {
                            justify_content: JustifyContent::SpaceBetween,
                            ..default()
                        })
                        .with_children(|row| {
                            for label in ["0 h", "6 h", "12 h", "18 h", "24 h"] {
                                row.spawn(text(label, &font, 11.0));
                            }
                        });
                    panel
                        .spawn(Node {
                            column_gap: Val::Px(8.0),
                            ..default()
                        })
                        .with_children(|row| {
                            for (hour, label) in [(480, "Matin"), (720, "Midi"), (1080, "Soir")] {
                                row.spawn((
                                    Button,
                                    Action::Hour(hour),
                                    Node {
                                        flex_grow: 1.0,
                                        min_height: Val::Px(36.0),
                                        align_items: AlignItems::Center,
                                        justify_content: JustifyContent::Center,
                                        ..default()
                                    },
                                    BackgroundColor(Color::srgb(0.91, 0.90, 0.85)),
                                ))
                                .with_children(|button| {
                                    button.spawn(text(label, &font, 13.0));
                                });
                            }
                        });
                    for (action, label, color) in [
                        (
                            Action::Graphics,
                            "Réglages graphiques",
                            Color::srgb(0.22, 0.43, 0.54),
                        ),
                        (Action::Resume, "Reprendre", Color::srgb(0.72, 0.49, 0.23)),
                        (
                            Action::Quit,
                            "Quitter le jeu",
                            Color::srgb(0.52, 0.23, 0.20),
                        ),
                    ] {
                        panel
                            .spawn((
                                Button,
                                action,
                                Node {
                                    width: Val::Percent(100.0),
                                    min_height: Val::Px(42.0),
                                    align_items: AlignItems::Center,
                                    justify_content: JustifyContent::Center,
                                    ..default()
                                },
                                BackgroundColor(color),
                            ))
                            .with_children(|button| {
                                let (value, font, _) = text(label, &font, 15.0);
                                button.spawn((value, font, TextColor(Color::WHITE)));
                            });
                    }
                });
        });
    commands
        .spawn(Node {
            position_type: PositionType::Absolute,
            left: Val::Px(20.0),
            top: Val::Px(20.0),
            column_gap: Val::Px(14.0),
            ..default()
        })
        .with_children(|row| {
            for indicator in [Indicator::Fps, Indicator::Frame, Indicator::Clock] {
                let (value, font, _) = text("", &font, 15.0);
                row.spawn((
                    Hud(indicator),
                    value,
                    font,
                    TextColor(Color::WHITE),
                    Node {
                        padding: UiRect::all(Val::Px(6.0)),
                        ..default()
                    },
                    BackgroundColor(Color::srgba(0.04, 0.08, 0.10, 0.85)),
                ));
            }
        });
}
