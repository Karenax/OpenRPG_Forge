"""Expose pipeline failures and last GPU pixels; retain original pass criteria."""
from pathlib import Path
import subprocess
import sys

S=Path(__file__).parent;R=S.parents[1]
example=R/'crates/forge_engine/examples/verify_source_glb_lighting_diagnostic_v43.rs'
queue=S/'queue_townsmith_lighting_diagnostic_v43.py'
assert not example.exists() and not queue.exists()
q=(R/'crates/forge_engine/examples/verify_source_glb_lighting_components_v42.rs').read_text(encoding='utf-8')
a='let mut previous: Option<Vec<u8>> = None;';assert q.count(a)==1
q=q.replace(a,a+'\n    let mut last_observed: Option<Vec<u8>> = None;')
a='let pending = cache.waiting_pipelines().count();';assert q.count(a)==1
patch='''
        let failed: Vec<String> = cache.pipelines().enumerate().filter_map(|(index,p)| {
            if let bevy::render::render_resource::CachedPipelineState::Err(error)=&p.state {
                Some(format!("pipeline {index}: {error:?}"))
            } else { None }
        }).collect();
        if !failed.is_empty() {
            std::fs::write(root.join("PIPELINE-DIAGNOSTIC.v1.json"),serde_json::to_vec_pretty(&serde_json::json!({"status":"actual_pipeline_errors_observed","frames":frames,"pending":pending,"errors":failed,"gpu_validation_errors":errors.lock().unwrap().clone(),"thresholds_unchanged":true,"full_import":false}))?)?;
            return Err("Actual cached pipeline failure retained in diagnostic receipt".into());
        }
'''
q=q.replace(a,a+'\n'+patch)
a='let varied = bytes.chunks_exact(8).filter(|p| *p != &bytes[..8]).count();';assert q.count(a)==1
q=q.replace(a,a+'\n                last_observed=Some(bytes.clone());')
a='return Err("No stable nonblank source-material camera draw in 90 seconds".into());';assert q.count(a)==1
patch='''
            let observed_bytes=last_observed.as_ref().map(|b|b.len()).unwrap_or(0);
            let varied_pixels=last_observed.as_ref().map(|b|b.chunks_exact(8).filter(|p|*p!=&b[..8]).count()).unwrap_or(0);
            if let Some(bytes)=&last_observed {std::fs::write(root.join("LAST-OBSERVED.rgba16f"),bytes)?;}
            std::fs::write(root.join("DRAW-DIAGNOSTIC.v1.json"),serde_json::to_vec_pretty(&serde_json::json!({"status":"draw_stability_refusal_with_last_observation","frames":frames,"pending":pending,"stable":stable,"observed_bytes":observed_bytes,"varied_pixels":varied_pixels,"gpu_validation_errors":errors.lock().unwrap().clone(),"thresholds_unchanged":true,"full_import":false}))?)?;
'''
q=q.replace(a,patch+'\n            '+a)
example.write_text(q,encoding='utf-8')
q=(S/'queue_source_glb_lighting_components_v42.py').read_text(encoding='utf-8')
q=q.replace("os.environ['OPENRPG_PRIVATE_LIGHT_LAYER']","os.environ.get('OPENRPG_PRIVATE_LIGHT_LAYER','0')")
q=q.replace("ROOT=W/f'townsmith-native-lighting-layer-{LAYER}-v42'","ROOT=W/'townsmith-lighting-diagnostic-v43'")
q=q.replace('verify_source_glb_lighting_components_v42','verify_source_glb_lighting_diagnostic_v43')
compile(q,str(queue),'exec');queue.write_text(q,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(queue)],check=True)
