"""Preserve entrypoint instance index explicitly through the shared vertex helper.

Bevy's pinned mesh.wgsl uses a separate index argument to work around the DX12
wgpu/naga builtin-index transport bug. This changes no asset geometry or ABI.
"""
def repair_vertex_instance_transport(text):
    signature='fn source_graph_displaced_forward_frame(vertex:SourceGraphVertex)->SourceGraphForwardOutput {'
    repaired='fn source_graph_displaced_forward_frame(vertex:SourceGraphVertex,source_instance_index:u32)->SourceGraphForwardOutput {'
    if repaired in text:
        assert signature not in text
        return text
    assert text.count(signature)==1
    start=text.index(signature);brace=text.index('{',start);depth=1;end=brace+1
    while depth:
        depth+=(text[end]=='{')-(text[end]=='}');end+=1
    block=text[start:end]
    assert block.count('vertex.instance_index')>=2
    block=block.replace(signature,repaired).replace('vertex.instance_index','source_instance_index')
    text=text[:start]+block+text[end:]
    call='source_graph_displaced_forward_frame(vertex);'
    assert 1<=text.count(call)<=2
    text=text.replace(call,'source_graph_displaced_forward_frame(vertex,vertex.instance_index);')
    return text
