```text
MIDI notes -> FM synth -> track amplitudes / per-note on/off + velocity
                              |
               clear BodyProperty.acceleration
                              |
               all body pairs: local same-track gravity / near repulsion
                              |
               neighbor alignment + per-note push / pull sources
                              |
               moving track centers + circulation + kick burst / local return
                              |
               acceleration -> velocity -> position
                              |
                 invisible body anchors + luminous TrailRenderer
                              |
               per-group spread / speed / disorder / centroid
                              |
                   reverb / filter / per-instrument FM brightness / stereo pan
                              |
                           FM synth
```

Rendering uses standard Unity URP materials and Bloom. 
`EDMAfterimage` stores the camera output in a persistent texture; 
`Afterimage.shader` combines the current frame with the fading previous frame. 
`EDMReactiveCamera` exposes its toggle and decay time. `ScreenAfterimage` reuses the same pass on a fixed camera in `09_MetaBoids_Pinch`, without the EDM orbit or grading. The pass requests an intermediate camera texture so it can sample camera color with post-processing enabled or disabled. The 360-body, four-track simulation is entirely CPU C#;

Depth Priming is disabled in `EDMLightRenderer` so the shared renderer also draws the simple opaque MetaBoids shader, which has no separate depth pass.
