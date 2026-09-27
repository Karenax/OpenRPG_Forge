"""Deterministic spatial grouping of connected foliage faces; no Blender dependency."""
import math


def partition_faces(positions, faces, cell_size):
    """Keep each connected leaf/card whole, even across exported split vertices.

    Connectivity uses exact position edges, never a distance-based weld that could
    merge neighboring leaves. Returned face indices refer to the unmodified input.
    """
    if not math.isfinite(cell_size) or cell_size <= 0:
        raise ValueError('cell_size must be finite and positive')
    if not faces or len(faces) > 200_000 or len(positions) > 600_000:
        raise ValueError('empty or oversized input')
    points = [tuple(p) for p in positions]
    if any(len(p) != 3 or not all(math.isfinite(v) for v in p) for p in points):
        raise ValueError('invalid position')
    parents = list(range(len(faces)))

    def root(i):
        while parents[i] != i:
            parents[i] = parents[parents[i]]
            i = parents[i]
        return i

    edges = {}
    for i, face in enumerate(faces):
        if len(face) < 3 or len(set(face)) != len(face) or any(
            not isinstance(v, int) or v < 0 or v >= len(points) for v in face
        ):
            raise ValueError('invalid face')
        for a, b in zip(face, (*face[1:], face[0])):
            edge = tuple(sorted((points[a], points[b])))
            if edge[0] == edge[1]:
                raise ValueError('degenerate edge')
            if edge in edges:
                left, right = root(i), root(edges[edge])
                parents[max(left, right)] = min(left, right)
            else:
                edges[edge] = i
    connected = {}
    for i in range(len(faces)):
        connected.setdefault(root(i), []).append(i)
    groups = {}
    for component in connected.values():
        unique = sorted({points[v] for i in component for v in faces[i]})
        center = [math.fsum(p[axis] for p in unique) / len(unique) for axis in range(3)]
        cell = tuple(math.floor(v / cell_size) for v in center)
        groups.setdefault(cell, []).extend(component)
    return [{'cell': cell, 'faces': sorted(indices)} for cell, indices in sorted(groups.items())]
