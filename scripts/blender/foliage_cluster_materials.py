"""Owned Cycles study materials; source glTF graphs are never modified."""


def bake_material(source, channel='albedo', basis=None):
    if channel not in ('albedo', 'normal', 'orm') or (channel == 'normal' and basis is None):
        raise ValueError('supported channel and normal view basis required')
    material = source.copy()
    nodes, links = material.node_tree.nodes, material.node_tree.links
    principled = [n for n in nodes if n.type == 'BSDF_PRINCIPLED']
    outputs = [n for n in nodes if n.type == 'OUTPUT_MATERIAL' and n.is_active_output]
    if len(principled) != 1 or len(outputs) != 1:
        raise ValueError('one standard glTF material surface required')
    bsdf, output = principled[0], outputs[0]
    emission = nodes.new('ShaderNodeEmission')
    transparent = nodes.new('ShaderNodeBsdfTransparent')
    mix = nodes.new('ShaderNodeMixShader')

    def copy_input(source_socket, target):
        if source_socket.is_linked:
            links.new(source_socket.links[0].from_socket, target)
        else:
            target.default_value = source_socket.default_value

    copy_input(bsdf.inputs['Alpha'], mix.inputs[0])
    if channel == 'albedo':
        copy_input(bsdf.inputs['Base Color'], emission.inputs['Color'])
    elif channel == 'orm':
        # The caller rejects glTF occlusion textures; AO is explicitly neutral.
        encoded = nodes.new('ShaderNodeCombineXYZ')
        encoded.inputs[0].default_value = 1
        copy_input(bsdf.inputs['Roughness'], encoded.inputs[1])
        copy_input(bsdf.inputs['Metallic'], encoded.inputs[2])
        links.new(encoded.outputs[0], emission.inputs['Color'])
    else:
        # The source NormalMap graph resolves its tangent normal into world space.
        # Otherwise use the source mesh's shading normal (including custom normals).
        normal_input = bsdf.inputs['Normal']
        normal = normal_input.links[0].from_socket if normal_input.is_linked else nodes.new('ShaderNodeNewGeometry').outputs['Normal']
        normalize = nodes.new('ShaderNodeVectorMath')
        normalize.operation = 'NORMALIZE'
        links.new(normal, normalize.inputs[0])
        facing = nodes.new('ShaderNodeVectorMath')
        facing.operation = 'DOT_PRODUCT'
        facing.inputs[1].default_value = basis[2]
        links.new(normalize.outputs[0], facing.inputs[0])
        negative = nodes.new('ShaderNodeMath')
        negative.operation = 'LESS_THAN'
        negative.inputs[1].default_value = 0
        links.new(facing.outputs['Value'], negative.inputs[0])
        sign = nodes.new('ShaderNodeMath')
        sign.operation = 'MULTIPLY_ADD'
        sign.inputs[1].default_value = -2
        sign.inputs[2].default_value = 1
        links.new(negative.outputs[0], sign.inputs[0])
        face_normal = nodes.new('ShaderNodeVectorMath')
        face_normal.operation = 'SCALE'
        links.new(normalize.outputs[0], face_normal.inputs[0])
        links.new(sign.outputs[0], face_normal.inputs['Scale'])
        encoded = nodes.new('ShaderNodeCombineXYZ')
        for i, axis in enumerate(basis):
            dot = nodes.new('ShaderNodeVectorMath')
            dot.operation = 'DOT_PRODUCT'
            dot.inputs[1].default_value = axis
            links.new(face_normal.outputs[0], dot.inputs[0])
            pack = nodes.new('ShaderNodeMath')
            pack.operation = 'MULTIPLY_ADD'
            pack.inputs[1].default_value = .5
            pack.inputs[2].default_value = .5
            links.new(dot.outputs['Value'], pack.inputs[0])
            links.new(pack.outputs[0], encoded.inputs[i])
        links.new(encoded.outputs[0], emission.inputs['Color'])
    links.new(transparent.outputs[0], mix.inputs[1])
    links.new(emission.outputs[0], mix.inputs[2])
    for link in list(output.inputs['Surface'].links):
        links.remove(link)
    links.new(mix.outputs[0], output.inputs['Surface'])
    return material
