//! Keep the acquired source deformation/mask functions; replace only bindings
//! and pass I/O. Lighting functions are not used by a depth pass.
pub fn make(raw: &str, kind: u8) -> Result<String, &'static str> {
    let raw = raw.replace("\r\n", "\n");
    let tag = format!("#ifdef PLANT_KIND_{kind}\n");
    let body = raw
        .split_once(&tag)
        .ok_or("missing plant program")?
        .1
        .split_once("\n#endif")
        .ok_or("missing plant program end")?
        .0;
    let body = body
        .split_once("struct SourceFrame")
        .ok_or("missing source frame")?
        .1;
    let body = body
        .split_once("fn sourceSurface(")
        .ok_or("missing source surface")?
        .0;
    let mut code = format!("struct SourceFrame{body}");
    let setup = code.find("fn setupSource()").ok_or("missing setup")?;
    let end = code
        .find("struct ProbeVertex")
        .ok_or("missing vertex layout")?;
    code.replace_range(setup..end, SETUP);
    code = code
        .lines()
        .filter(|l| !l.starts_with("@group("))
        .collect::<Vec<_>>()
        .join("\n");
    let mask = match kind {
        0 => "vegPlantMask(input)",
        1 => "vegCanopyMask(input,vegCanopyLeaf(input))",
        _ => return Err("unsupported shadow plant kind"),
    };
    Ok(format!(
        "{BINDINGS}\n{code}\n{}",
        FOOTER.replace("SHADOW_MASK", mask)
    ))
}

const BINDINGS: &str = r"
struct ShadowParams {clip:mat4x4f,main_camera:vec4f,shadow_camera:vec4f,lod_range:vec4f,wind_time:vec4f,gust_frame:vec4f};
@group(0) @binding(0) var<uniform> params:ShadowParams;
@group(0) @binding(1) var<storage,read> sourceInstances:array<PlantRecord>;
@group(0) @binding(2) var<storage,read> sourceModels:array<mat4x4f>;
@group(0) @binding(3) var vegLeafTex:texture_2d<f32>;
@group(0) @binding(4) var smpAnisoRepeat:sampler;
@group(0) @binding(5) var vegDetail:texture_2d<f32>;
@group(0) @binding(6) var smpLinearRepeat:sampler;
@group(0) @binding(7) var vegImpA:texture_2d<f32>;
@group(0) @binding(8) var vegImpB:texture_2d<f32>;
@group(0) @binding(9) var smpLinearClamp:sampler;
";
const SETUP: &str = r"
fn setupSource(){
 frame.cameraPos=params.shadow_camera.xyz;
 frame.time=params.wind_time.w;frame.windDir=params.wind_time.xy;frame.windSpeed=params.wind_time.z;
 frame.sunDir=vec3f(-0.886125497221313,0.45498452073767237,0.08815151197450805);
 frame.night=params.gust_frame.w;frame.frameIndex=u32(params.gust_frame.z);
 vegParams.camPos=params.main_camera.xyz;vegParams.gustOffset=params.gust_frame.xy;
 setupSourceLobes();draw.params=params.lod_range;
}
";
const FOOTER: &str = r"
@vertex fn vertex(v:ProbeVertex)->ProbeOut {
 setupSource();let record=sourceInstances[v.instance];var source:SourceVertex;
 source.position=v.position;source.normal=v.normal;source.uv=v.uv;
 source.model=sourceModels[v.instance];source.iPos=record.iPos;source.iDat=record.iDat;
 source.aVeg=v.aVeg;source.aMat=v.aMat;source.aLobe=v.aLobe;
 var o:ProbeOut;sourceVertex(&source,&o);
 o.clip=params.clip*vec4f(source.worldPos,1.0);
 o.P=source.worldPos;o.normal=source.worldNormal;o.uv=v.uv;return o;
}
@fragment fn fragment(vs:ProbeOut,@builtin(front_facing) front:bool){
 setupSource();var input:FragInput;input.vs=vs;input.P=vs.P;
 input.N=normalize(select(-vs.normal,vs.normal,front));
 input.V=normalize(params.shadow_camera.xyz-vs.P);input.uv=vs.uv;
 input.front=front;input.pixel=vs.clip.xy;
 if(!SHADOW_MASK){discard;}
}
";
