from pathlib import Path
S=Path(__file__).parent
code=(S/'candidates/SourceFilteredNativeTextureV1.rs').read_text(encoding='utf-8')
assert code.count('wgpu::MipmapFilterMode::Nearest')==1
code=code.replace('wgpu::MipmapFilterMode::Nearest','wgpu::MipmapFilterMode::Linear')
(S/'candidates/SourceFilteredNativeTextureV2.rs').write_text(code,encoding='utf-8')
queue=(S/'queue_townsmith_filtered_gpu_v1.py').read_text(encoding='utf-8').replace('native-filtered-gpu-control-v1','native-filtered-gpu-control-v2').replace('SourceFilteredNativeTextureV1.rs','SourceFilteredNativeTextureV2.rs')
queue=queue.replace("for p in [code,target,shader,program,inputpath,Path(__file__),", "for p in [code,target,shader,program,inputpath,Path(__file__),W/'townsmith-s-paint-filtered-mip-blend-diagnostic-v1/RESULT.v1.json',")
(S/'queue_townsmith_filtered_gpu_v2.py').write_text(queue,encoding='utf-8')
compare=(S/'reopen_townsmith_filtered_gpu_v1.py').read_text(encoding='utf-8').replace('native-filtered-gpu-control-v1','native-filtered-gpu-control-v2').replace('native-filtered-gpu-reopen-v1','native-filtered-gpu-reopen-v2')
compare=compare.replace('Bilinear repeat sampling at nine UVs','Native-observed adjacent mip interpolation with bilinear repeat sampling at nine UVs')
compare=compare.replace("'implicit_derivative_sampling_qualified':False", "'nominal_filter_metadata_changed':False,'full_sampler_policy_qualified':False,'implicit_derivative_sampling_qualified':False")
(S/'reopen_townsmith_filtered_gpu_v2.py').write_text(compare,encoding='utf-8')
