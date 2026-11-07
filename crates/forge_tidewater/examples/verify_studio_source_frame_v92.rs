//! Compile the shared source-scene interfaces from a downstream consumer.
fn main() {
    let _ = forge_engine::source_graph_frame::update;
    let _ = forge_engine::source_graph_resources::register_with_sampling;
    let _ = forge_engine::source_graph_scene_attachment::attach;
}
