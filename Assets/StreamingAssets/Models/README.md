# Models

Drop `.glb` files here and `GlbModelLoader` picks them up at runtime via com.unity.cloud.gltfast:

- `player.glb` replaces the player's placeholder capsule
- `customer.glb` replaces each customer's placeholder capsule

Models should be authored with +Y up and the feet at the origin, roughly 1.8 m tall.
Large binaries belong in Git LFS (`git lfs track "*.glb"`).
