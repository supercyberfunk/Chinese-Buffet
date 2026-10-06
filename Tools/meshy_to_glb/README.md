# Meshy FBX to game-ready GLB

Meshy's "image-to-3d-texture" FBX exports are about 1.9 million triangles with 4K textures
(~130 MB per model). That is far too heavy to instance around a level, so this script turns one
export into a `.glb` the demo can load:

- bakes the FBX node transform (Meshy exports Z-up geometry with a -90° X rotation and ×100 scale)
  into Y-up metres,
- decimates to ~60k triangles with quadric edge collapse (texture seams are preserved),
- re-pivots the mesh to bottom-centre so it stands on the floor at y = 0,
- resizes textures to 2048 (base colour as JPEG, normal as PNG) and packs roughness + metallic into
  one glTF metallicRoughness texture,
- writes a single self-contained `.glb` (6–10 MB).

## Usage

```bash
pip install numpy pillow pymeshlab pygltflib
# unzip the Meshy export somewhere, then:
python fbx_to_glb.py <folder containing the .fbx and .png files> Assets/Resources/Models/MyProp.glb [target_faces=60000] [texture_size=2048]
```

Drop the result in `Assets/Resources/Models/`; `PropLibrary.Place("MyProp", ...)` in the scene
builder will pick it up (com.unity.cloud.gltfast imports `.glb` files in the Editor).

If `pymeshlab` fails to load its filter plugins on a headless Linux box with
`libOpenGL.so.0: cannot open shared object file`, install `libopengl0` (apt) or point
`LD_LIBRARY_PATH` at a stub.
