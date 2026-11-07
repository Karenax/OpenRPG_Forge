"""Resolve original Shader Graph UV inputs without substituting slot defaults."""

def checked_channel(value):
    if type(value) is not int or not 0 <= value <= 7:
        raise ValueError(f'Original UV channel must be an integer in 0..7: {value!r}')
    return value


def channel_expression(channel):
    channel = checked_channel(channel)
    return 'source_uv' if channel == 0 else f'source_uv{channel}'


def implicit_uv_input(value):
    # Connected inputs remain references, even when their producer is constant.
    if 'ref' in value:
        return None
    slot = value.get('source_slot', {})
    if not slot.get('m_Type', '').endswith('.UVMaterialSlot'):
        return None
    if 'm_Channel' not in slot:
        raise ValueError('Implicit source UV slot has no declared channel')
    channel = checked_channel(slot['m_Channel'])
    return channel_expression(channel) + '.xy', 2, channel
