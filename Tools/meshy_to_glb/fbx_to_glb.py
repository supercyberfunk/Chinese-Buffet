"""Convert a Meshy binary FBX (+ PBR PNGs) into a decimated, game-ready .glb.

usage: fbx_to_glb.py <model_dir> <out.glb> [target_faces] [texture_size]
"""
import io
import os
import re
import struct
import sys
import time
import zlib

import numpy as np
import pygltflib
import pymeshlab
from PIL import Image


# ---------------------------------------------------------------- FBX parsing
def read_tree(buf, pos, end, version, out):
    while pos < end:
        if version >= 7500:
            nend, nprops, plen = struct.unpack_from("<QQQ", buf, pos)
            hdr = 24
        else:
            nend, nprops, plen = struct.unpack_from("<III", buf, pos)
            hdr = 12
        if nend == 0:
            break
        p = pos + hdr
        nlen = buf[p]
        p += 1
        name = buf[p:p + nlen].decode("ascii", "replace")
        p += nlen
        props = []
        pend = p + plen
        while p < pend:
            t = chr(buf[p])
            p += 1
            if t in "YCIFDL":
                fmt = {"Y": "<h", "C": "<?", "I": "<i", "F": "<f", "D": "<d", "L": "<q"}[t]
                (v,) = struct.unpack_from(fmt, buf, p)
                p += struct.calcsize(fmt)
                props.append(v)
            elif t in "fdlib":
                n, enc, clen = struct.unpack_from("<III", buf, p)
                p += 12
                raw = buf[p:p + clen]
                p += clen
                if enc == 1:
                    raw = zlib.decompress(raw)
                dt = {"f": "<f4", "d": "<f8", "l": "<i8", "i": "<i4", "b": "<?"}[t]
                props.append(np.frombuffer(raw, dtype=dt, count=n))
            elif t in "SR":
                (n,) = struct.unpack_from("<I", buf, p)
                p += 4
                data = buf[p:p + n]
                p += n
                props.append(data.decode("utf-8", "replace") if t == "S" else data)
            else:
                raise ValueError(f"unknown prop type {t!r}")
        children = []
        read_tree(buf, p, nend, version, children)
        out.append((name, props, children))
        pos = nend


def find_all(nodes, name):
    for n in nodes:
        if n[0] == name:
            yield n
        yield from find_all(n[2], name)


def child(node, name):
    for c in node[2]:
        if c[0] == name:
            return c
    return None



def apply_model_transform(root, verts):
    """Bake the Model node's Lcl Rotation/Scaling and the file's unit scale into the vertices (-> metres, Y-up)."""
    unit = 1.0
    for p in find_all(root, "P"):
        if p[1] and p[1][0] == "UnitScaleFactor":
            unit = float(p[1][-1])
    rotation = np.zeros(3)
    scaling = np.ones(3)
    model = next(find_all(root, "Model"), None)
    if model is not None:
        for p in find_all(model[2], "P"):
            if p[1][0] == "Lcl Rotation":
                rotation = np.asarray(p[1][4:7], dtype=np.float64)
            elif p[1][0] == "Lcl Scaling":
                scaling = np.asarray(p[1][4:7], dtype=np.float64)
    rx, ry, rz = np.radians(rotation)
    cx, sx = np.cos(rx), np.sin(rx)
    cy, sy = np.cos(ry), np.sin(ry)
    cz, sz = np.cos(rz), np.sin(rz)
    mx = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]])
    my = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    mz = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]])
    m = mz @ my @ mx  # FBX Euler XYZ order
    out = (verts * scaling) @ m.T
    out *= unit / 100.0  # FBX units are centimetres at UnitScaleFactor 1
    print(f"  model transform: rotation={rotation.tolist()} scaling={scaling.tolist()} unit={unit}")
    return out

def load_fbx_mesh(path):
    with open(path, "rb") as f:
        buf = f.read()
    assert buf.startswith(b"Kaydara FBX Binary"), "not a binary FBX"
    version = struct.unpack_from("<I", buf, 23)[0]
    root = []
    read_tree(buf, 27, len(buf), version, root)
    geo = next(find_all(root, "Geometry"))
    verts = np.asarray(child(geo, "Vertices")[1][0], dtype=np.float64).reshape(-1, 3)
    verts = apply_model_transform(root, verts)
    idx = np.asarray(child(geo, "PolygonVertexIndex")[1][0], dtype=np.int64)

    # Polygons -> triangles (fan). Negative index marks the last corner of a polygon.
    ends = np.flatnonzero(idx < 0)
    starts = np.concatenate(([0], ends[:-1] + 1))
    sizes = ends - starts + 1
    corners = idx.copy()
    corners[ends] = -corners[ends] - 1
    if np.all(sizes == 3):
        tri_corner = np.arange(len(idx)).reshape(-1, 3)
    else:
        tri_corner = []
        for s, n in zip(starts, sizes):
            for k in range(1, n - 1):
                tri_corner.append((s, s + k, s + k + 1))
        tri_corner = np.asarray(tri_corner, dtype=np.int64)
    tri_pos = corners[tri_corner]  # (T,3) position indices

    uv_layer = child(geo, "LayerElementUV")
    uvs = None
    if uv_layer is not None:
        uv_data = np.asarray(child(uv_layer, "UV")[1][0], dtype=np.float64).reshape(-1, 2)
        mapping = child(uv_layer, "MappingInformationType")[1][0]
        ref = child(uv_layer, "ReferenceInformationType")[1][0]
        if mapping == "ByPolygonVertex":
            if ref == "IndexToDirect":
                uv_index = np.asarray(child(uv_layer, "UVIndex")[1][0], dtype=np.int64)
                corner_uv = uv_data[uv_index]
            else:
                corner_uv = uv_data
            uvs = corner_uv[tri_corner]  # (T,3,2)
        elif mapping == "ByVertice" or mapping == "ByVertex":
            uvs = uv_data[tri_pos]
        else:
            raise ValueError(f"unsupported UV mapping {mapping}")
    return verts, tri_pos, uvs


# ---------------------------------------------------------------- decimation
def decimate(verts, tri_pos, uvs, target_faces):
    t0 = time.time()
    # Split vertices per (position, uv) so texture seams become mesh boundaries pymeshlab can preserve.
    T = len(tri_pos)
    if uvs is not None:
        key_uv = np.round(uvs.reshape(-1, 2), 5)
        keys = np.concatenate([tri_pos.reshape(-1, 1).astype(np.float64), key_uv], axis=1)
    else:
        keys = tri_pos.reshape(-1, 1).astype(np.float64)
    uniq, inverse = np.unique(keys, axis=0, return_inverse=True)
    inverse = inverse.reshape(-1)
    v_pos = verts[uniq[:, 0].astype(np.int64)]
    faces = inverse.reshape(T, 3).astype(np.int32)
    v_uv = uniq[:, 1:3] if uvs is not None else None
    print(f"  split mesh: {len(v_pos)} verts, {T} faces ({time.time() - t0:.1f}s)")

    ms = pymeshlab.MeshSet()
    if v_uv is not None:
        ms.add_mesh(pymeshlab.Mesh(vertex_matrix=v_pos, face_matrix=faces, v_tex_coords_matrix=v_uv))
    else:
        ms.add_mesh(pymeshlab.Mesh(vertex_matrix=v_pos, face_matrix=faces))
    ms.meshing_remove_duplicate_faces()
    ms.meshing_remove_unreferenced_vertices()
    ms.meshing_decimation_quadric_edge_collapse(
        targetfacenum=int(target_faces),
        preserveboundary=True,
        preservenormal=True,
        preservetopology=False,
        planarquadric=True,
        qualitythr=0.3,
    )
    ms.meshing_remove_unreferenced_vertices()
    ms.compute_normal_per_vertex()
    m = ms.current_mesh()
    out_v = np.asarray(m.vertex_matrix(), dtype=np.float32)
    out_f = np.asarray(m.face_matrix(), dtype=np.uint32)
    out_n = np.asarray(m.vertex_normal_matrix(), dtype=np.float32)
    out_uv = np.asarray(m.vertex_tex_coord_matrix(), dtype=np.float32) if v_uv is not None else None
    print(f"  decimated: {len(out_v)} verts, {len(out_f)} faces ({time.time() - t0:.1f}s)")
    return out_v, out_f, out_n, out_uv


# ---------------------------------------------------------------- textures
def find_texture(model_dir, suffix):
    for name in os.listdir(model_dir):
        low = name.lower()
        if not low.endswith(".png"):
            continue
        stem = low[:-4]
        if suffix == "" and not re.search(r"_(metallic|roughness|normal)$", stem):
            return os.path.join(model_dir, name)
        if suffix and stem.endswith("_" + suffix):
            return os.path.join(model_dir, name)
    return None


def encode_image(img, fmt, size):
    img = img.resize((size, size), Image.LANCZOS) if img.size != (size, size) else img
    bio = io.BytesIO()
    if fmt == "jpeg":
        img.convert("RGB").save(bio, format="JPEG", quality=90, optimize=True)
        return bio.getvalue(), "image/jpeg"
    img.save(bio, format="PNG", optimize=True)
    return bio.getvalue(), "image/png"


# ---------------------------------------------------------------- GLB
def write_glb(out_path, name, v, f, n, uv, model_dir, tex_size):
    # Pivot at bottom-centre so the model sits on the floor when placed at y=0.
    lo, hi = v.min(axis=0), v.max(axis=0)
    v = v - np.array([(lo[0] + hi[0]) * 0.5, lo[1], (lo[2] + hi[2]) * 0.5], dtype=np.float32)
    if uv is not None:
        uv = uv.copy()
        uv[:, 1] = 1.0 - uv[:, 1]  # FBX bottom-left origin -> glTF top-left origin

    blobs = []
    views = []

    def add_blob(data, target=None):
        offset = sum(len(b) for b in blobs)
        pad = (-len(data)) % 4
        blobs.append(data + b"\0" * pad)
        views.append(pygltflib.BufferView(buffer=0, byteOffset=offset, byteLength=len(data), target=target))
        return len(views) - 1

    gltf = pygltflib.GLTF2(asset=pygltflib.Asset(version="2.0", generator="chinese-buffet fbx_to_glb"))
    gltf.bufferViews = views

    idx_view = add_blob(f.astype(np.uint32).tobytes(), pygltflib.ELEMENT_ARRAY_BUFFER)
    pos_view = add_blob(v.astype(np.float32).tobytes(), pygltflib.ARRAY_BUFFER)
    nrm_view = add_blob(n.astype(np.float32).tobytes(), pygltflib.ARRAY_BUFFER)
    accessors = [
        pygltflib.Accessor(bufferView=idx_view, componentType=pygltflib.UNSIGNED_INT, count=f.size, type=pygltflib.SCALAR, max=[int(f.max())], min=[int(f.min())]),
        pygltflib.Accessor(bufferView=pos_view, componentType=pygltflib.FLOAT, count=len(v), type=pygltflib.VEC3, max=v.max(axis=0).tolist(), min=v.min(axis=0).tolist()),
        pygltflib.Accessor(bufferView=nrm_view, componentType=pygltflib.FLOAT, count=len(n), type=pygltflib.VEC3),
    ]
    attributes = pygltflib.Attributes(POSITION=1, NORMAL=2)
    if uv is not None:
        uv_view = add_blob(uv.astype(np.float32).tobytes(), pygltflib.ARRAY_BUFFER)
        accessors.append(pygltflib.Accessor(bufferView=uv_view, componentType=pygltflib.FLOAT, count=len(uv), type=pygltflib.VEC2))
        attributes.TEXCOORD_0 = 3
    gltf.accessors = accessors

    images, textures = [], []

    def add_texture(img, fmt, size):
        data, mime = encode_image(img, fmt, size)
        view = add_blob(data)
        images.append(pygltflib.Image(bufferView=view, mimeType=mime))
        textures.append(pygltflib.Texture(sampler=0, source=len(images) - 1))
        return len(textures) - 1

    gltf.samplers = [pygltflib.Sampler(magFilter=pygltflib.LINEAR, minFilter=pygltflib.LINEAR_MIPMAP_LINEAR, wrapS=pygltflib.REPEAT, wrapT=pygltflib.REPEAT)]
    pbr = pygltflib.PbrMetallicRoughness(baseColorFactor=[1, 1, 1, 1], metallicFactor=1.0, roughnessFactor=1.0)
    material = pygltflib.Material(name=name, pbrMetallicRoughness=pbr, doubleSided=False)

    base = find_texture(model_dir, "")
    if base:
        pbr.baseColorTexture = pygltflib.TextureInfo(index=add_texture(Image.open(base), "jpeg", tex_size))
    metal = find_texture(model_dir, "metallic")
    rough = find_texture(model_dir, "roughness")
    if metal or rough:
        mr_size = max(256, tex_size // 2)
        g = Image.open(rough).convert("L").resize((mr_size, mr_size), Image.LANCZOS) if rough else Image.new("L", (mr_size, mr_size), 255)
        b = Image.open(metal).convert("L").resize((mr_size, mr_size), Image.LANCZOS) if metal else Image.new("L", (mr_size, mr_size), 0)
        r = Image.new("L", (mr_size, mr_size), 255)
        mr = Image.merge("RGB", (r, g, b))  # glTF: roughness in G, metallic in B
        pbr.metallicRoughnessTexture = pygltflib.TextureInfo(index=add_texture(mr, "png", mr_size))
    else:
        pbr.metallicFactor = 0.0
        pbr.roughnessFactor = 0.8
    normal = find_texture(model_dir, "normal")
    if normal:
        material.normalTexture = pygltflib.NormalMaterialTexture(index=add_texture(Image.open(normal).convert("RGB"), "png", tex_size))

    gltf.images = images
    gltf.textures = textures
    gltf.materials = [material]
    gltf.meshes = [pygltflib.Mesh(name=name, primitives=[pygltflib.Primitive(attributes=attributes, indices=0, material=0)])]
    gltf.nodes = [pygltflib.Node(name=name, mesh=0)]
    gltf.scenes = [pygltflib.Scene(name=name, nodes=[0])]
    gltf.scene = 0

    binary = b"".join(blobs)
    gltf.buffers = [pygltflib.Buffer(byteLength=len(binary))]
    gltf.set_binary_blob(binary)
    gltf.save_binary(out_path)
    size = hi - lo
    print(f"  wrote {out_path} ({os.path.getsize(out_path) / 1e6:.1f} MB), size x={size[0]:.2f} y={size[1]:.2f} z={size[2]:.2f}")


def main():
    model_dir, out_path = sys.argv[1], sys.argv[2]
    target_faces = int(sys.argv[3]) if len(sys.argv) > 3 else 60000
    tex_size = int(sys.argv[4]) if len(sys.argv) > 4 else 2048
    fbx = next(os.path.join(model_dir, f) for f in os.listdir(model_dir) if f.lower().endswith(".fbx"))
    name = os.path.splitext(os.path.basename(out_path))[0]
    print(f"{name}: reading {os.path.basename(fbx)}")
    verts, tri_pos, uvs = load_fbx_mesh(fbx)
    print(f"  source: {len(verts)} verts, {len(tri_pos)} tris, uv={'yes' if uvs is not None else 'no'}")
    v, f, n, uv = decimate(verts, tri_pos, uvs, target_faces)
    write_glb(out_path, name, v, f, n, uv, model_dir, tex_size)


if __name__ == "__main__":
    main()
