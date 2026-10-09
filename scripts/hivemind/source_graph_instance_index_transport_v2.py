"""Shared repair recipe for the two admitted vertex output aliases."""
import re
from source_graph_instance_index_transport_v1 import repair_vertex_instance_transport as repair_v1
def repair_vertex_instance_transport(text):
    pattern=r'fn source_graph_displaced_forward_frame\(vertex:SourceGraphVertex(?:,source_instance_index:u32)?\)->(SourceGraphForwardOutput|VertexOutput) \{'
    matches=list(re.finditer(pattern,text));assert len(matches)==1
    alias=matches[0].group(1)
    if alias=='SourceGraphForwardOutput':return repair_v1(text)
    signature=matches[0].group(0)
    normalized=text.replace(signature,signature.replace('->VertexOutput','->SourceGraphForwardOutput'),1)
    result=repair_v1(normalized)
    marker='fn source_graph_displaced_forward_frame(vertex:SourceGraphVertex,source_instance_index:u32)->SourceGraphForwardOutput {'
    assert result.count(marker)==1
    return result.replace(marker,marker.replace('->SourceGraphForwardOutput','->VertexOutput'),1)
