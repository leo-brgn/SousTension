# Radio and sonar art — issue 120

`kit_radio.build_all()` returns eleven modular `Asset` instances. Build/export using the existing `lib.py` API. Blender 5.2.2 generation is verified; source is `blender/sources/RadioSonar.blend`, FBX/manifest are in `Assets/_Project/Art/Models/Radio`, 24 front/three-quarter/open previews in `scratch_out/preview/Radio`.

| Model | Moving part or integration socket | Blender axis |
|---|---|---|
| SonarConsole | GainKnob, RangeKnob; ServiceCover; ScreenRenderTarget with UV0 | knobs Y, door Z |
| MagneticTapeRecorder | TransportButton00–03; LoadedTapePath toggled when tape absent; Mount_LeftReel/RightReel | buttons Y translation |
| TapeReelBlank / TapeReelRecorded | Reel | Y spindle rotation |
| SonarHeadphones | LeftEarcup, RightEarcup; Mount_Head, Mount_Cable | X earcup rotation |
| HeadphoneCableCoiled | Mount_Headphones / Mount_RadioJack | modular static cable |
| HydrophoneCrankStation | OrientationCrank; CrankGrip parented under crank | Y rotation |
| VLFRadioStation | FrequencyNeedle, RadioKnob0–2; Mount_AntennaCable | Y rotation |
| VLFAntennaDeployable | ExtendableMast, DeploymentCrank | mast Z translation 0–0.6m, crank Y rotation |
| MechanicalCipherMachine | Key_00_00–Key_02_09, SpaceBar; PaperCarriage | keys Z translation, carriage X translation |
| CipherMessagePad | TopMessageSheet with UV0 | X page rotation |

Imported coordinate convention verified by the project is Blender `(x,y,z)` to Unity `(-x,z,-y)`. Animation rotations map Blender X/Y/Z to Unity X/Z/Y with signs +/+/−. Each manifest lists exact Blender pivot and socket positions. Roots are at the origin. Source contains preview reels parented to the recorder but the recorder FBX retains separate reusable sockets; mount TapeReelRecorded or TapeReelBlank there during assembly.

Placement suggestion for compartment 03_Radio (Blender world coordinates): replace RadioConsole_Blockout at `(-2.13,2,0)` / Z rotation 90° with SonarConsole. Put a .85m-high worktop at the left wall and install VLFRadioStation at `(-2.08,2.9,.85)` and MagneticTapeRecorder at `(-2.08,1.0,.85)`, each Z rotation90°. Cipher machine fits a separate worktop at `(1.9,2.8,.85)` rotated−90°. HydrophoneCrankStation mounts near the sonar at `(-2.25,2.8,.82)` rotated90°. VLFAntennaDeployable attaches to the aft radio bulkhead/ceiling with clearance for the .6m deployment travel. Check actual room extents, wall thickness and interaction route in the global builder before accepting these placements; the suggestions do not modify boat layout.

SonarScreen is a circular UV0 mesh prepared for a render-target material. It currently uses the common vertex-color shader, so the preview has a blank dark face. Frequency legends, key labels and message content are deliberately blank localization supports. Cipher keyboard has thirty independent keys and a space bar. It models art geometry, not a specified historical cipher mechanism.

No gameplay, physical cable simulation, audio, collider setup or embedded animation clips are supplied. The headphone coil is static modular geometry with end sockets; use a spline/joint implementation for dynamic cable behavior. Sonar capot, gain controls, recorder transport, reel pivots, radio needle, antenna and cipher key/carriage pivots provide the integration structure. Portable mass assumptions for integration: headphones .45kg, reel .22kg, message pad .09kg; these values are prototype choices, not GDD requirements.

Validation performed in Blender: finite vertices, Col color attributes, nonzero triangle areas, successful FBX export and visual inspection. Unity import/render-target assignment and in-scene interaction clearance remain to verify in the shared integration pass. P3 lamp electronics rack and ActIII Signal emitter are outside this P2 issue lot.
