//! Adapter and ID boundary checks against the acquired shader; not GPU proof.
use forge_engine::tidewater_plants::picking::{MAX_ID, PickingExtension, decode, shader_source};
fn main() -> Result<(), Box<dyn std::error::Error>> {
    let path = std::env::args()
        .nth(1)
        .ok_or("Expected plant shader path")?;
    let acquired = std::fs::read_to_string(path)?;
    let derived = shader_source(&acquired)?;
    let source = acquired.replace("\r\n", "\n");
    assert_eq!(derived.matches("o.pickId=pickingIds.x+tag;").count(), 4);
    assert_eq!(derived.matches("sourceSurface(input,&surface);").count(), 4);
    assert_eq!(derived.matches("sourceVertex(&source,&o);").count(), 4);
    // Every upstream discard remains present, including impostor alpha/LOD.
    assert_eq!(
        derived.matches("discard").count(),
        source.matches("discard").count()
    );
    for index in 0..4 {
        let marker = format!("#ifdef PLANT_KIND_{index}");
        let original = source.split_once(&marker).ok_or("source kind")?.1;
        let adapted = derived.split_once(&marker).ok_or("derived kind")?.1;
        // Preserve every source deformation/surface function byte for byte.
        let start = "struct SourceFrame";
        let end = "@vertex\nfn vertex";
        let original = original.split_once(start).ok_or("source frame")?.1;
        let adapted = adapted.split_once(start).ok_or("derived frame")?.1;
        let original = original.split_once(end).ok_or("source vertex")?.0;
        let adapted = adapted.split_once(end).ok_or("derived vertex")?.0;
        // Only the varying declaration differs before the adapter entrypoint.
        let adapted = adapted
            .replace(
                "@group(#{MATERIAL_BIND_GROUP}) @binding(42) var<uniform> pickingIds:vec4u;\n",
                "",
            )
            .replace("\n    @location(15) @interpolate(flat) pickId:u32,", "");
        assert_eq!(original, adapted);
    }
    for id in [1, 255, 256, 65_383, 65_535, 65_536, MAX_ID] {
        let bytes = id.to_le_bytes();
        assert_eq!(decode([bytes[0], bytes[1], bytes[2], 255]), Some(id));
    }
    assert_eq!(decode([0, 0, 0, 255]), None);
    assert_eq!(decode([1, 0, 0, 0]), None);
    assert!(PickingExtension::new(1, 65_383).is_ok());
    assert!(PickingExtension::new(MAX_ID, 1).is_ok());
    for (first, count) in [(0, 1), (1, 0), (MAX_ID, 2), (u32::MAX, 1), (1, u32::MAX)] {
        assert!(PickingExtension::new(first, count).is_err());
    }
    assert!(shader_source(&derived).is_err());
    assert!(shader_source(&source.replacen("struct ProbeOut {", "struct Changed {", 1)).is_err());
    assert!(shader_source(&source.replacen("sourceSurface(input,&surface);", "", 1)).is_err());
    println!(
        "PLANT_PICKING_ADAPTER_PASS kinds=4 source_functions=unchanged id_boundaries=pass GPU=NOT_RUN"
    );
    if let Some(path) = std::env::args().nth(2) {
        check_occluder(&std::fs::read_to_string(path)?)?;
    }
    Ok(())
}

fn check_occluder(source: &str) -> Result<(), String> {
    use forge_engine::tidewater_plants::picking::occluders::village_shader;
    let result = village_shader(source)?;
    let source = source.replace("\r\n", "\n");
    let anchor = "    if s.alpha < mat.alphaTest { discard; }";
    let (before, _) = source.split_once(anchor).ok_or("alpha")?;
    assert_eq!(
        result.split_once(anchor).expect("preserved alpha").0,
        before
    );
    assert_eq!(
        source.matches("discard").count(),
        result.matches("discard").count()
    );
    assert!(result.contains("return vec4f(0.0,0.0,0.0,1.0);"));
    assert!(village_shader(&source.replace(anchor, "")).is_err());
    println!("VILLAGE_OCCLUDER_ADAPTER_PASS deformation_and_alpha=unchanged GPU=NOT_RUN");
    Ok(())
}
