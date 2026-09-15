# Bara Kitchen — implementation and handoff

Version 0.2.0 · Unity 6000.6.0f1 · 15 September 2026

## Open and run

Clone with Git LFS; instructions and playable downloads are in the [README](../README.md). Unity project: `Unity/BaraKitchen`. Entry scene: `Assets/BaraKitchen/Scenes/BaraKitchen_Game.unity`. The project uses Unity's built-in renderer, C#, native UI Toolkit, local assets and local saves. The base game requires no paid Unity plugin or runtime AI service. Optional GPT-Live chat uses OpenAI through a local Python gateway.

Open the entry scene and press Play. `Level_01`–`Level_04` are art review scenes; the entry scene loads generated playable kitchen prefabs. `Character_Studio` and `Animation_Studio` support asset review.

Browser build and review galleries are served from `art`:

```sh
python3 -m http.server 54114 --directory art
```

Open `http://localhost:54114/production/gameplay-round-01/`. The page is a full-window Unity canvas, not a separate JavaScript gameplay implementation. Browser data/wasm files are large; first loading can take time.

## Runtime responsibilities

All paths below are relative to `Unity/BaraKitchen/Assets/BaraKitchen`.

| Component | Responsibility |
|---|---|
| `Gameplay/KitchenGame.cs` | Session phases, room loading, stations, customer/dish lifecycle, pause, settlement and save orchestration |
| `Gameplay/ChefController.cs` | Camera-relative movement, collisions, facing/reach, context interactions, work cancellation, dash/throw and tool/animation coordination |
| `Gameplay/KitchenItem.cs` | Persistent food/vessel identity, preparation progress, portion children, exact recipes and visual refresh |
| `Gameplay/KitchenStation.cs` | Station type, slot contents, work progress, pot state and highlighted interaction point |
| `Gameplay/OrderBook.cs` | Tickets, patience, matching deliveries, streaks, score and expiry events |
| `Gameplay/CafeGuest.cs` | Waypoint movement, seat reservation, idle/eat/exit state, meal presentation |
| `Gameplay/KitchenHUD.cs` / `UI/Kitchen.uss` | Menus, selection, picture guides, tickets, score/time, settings, results and viewport allocation |
| `Gameplay/KitchenCameraController.cs` | Tilt/yaw input, smoothing, static-bound framing and saved preferences |
| `Gameplay/KitchenAudio.cs` | Music priorities/crossfades, one-shots, work loops, pause and gain controls |
| `Gameplay/StationHighlight.cs` | Animated worktop/service outlines |
| `Gameplay/GameCatalog.cs` | Level parameters, prefab/icon/audio references and save/settings definitions |
| `Gameplay/KitchenVoiceGuide.cs` | Single chat button, live context heartbeat, idle tracking, transport lifecycle and temporary station markers |
| `Gameplay/NativeLiveVoice.cs` | Desktop microphone/PCM streaming, bounded playback queue and immediate microphone shutdown |
| `Gameplay/KitchenHelpContext.cs` | Read-only live state, legal immediate actions and collider-checked camera-relative walking routes |
| `Gameplay/GameReviewTelemetry.cs` | Read-only test state exported to `window.baraState` in Web development builds |

The session owns authoritative transitions. Orders emit delivery/expiry events; the game connects these to the relevant customer, score and plate lifecycle. Eating completion returns a dirty dish to a finite queue. Avoid separate subscribers independently creating plates or scoring a delivery twice. Round settlement runs once; a stopped session rejects further work. Pause suspends gameplay clocks and active audio.

Items retain their root identity through preparation and transfer. Appearance is a view of state; replacing a mesh does not require replacing the logical ingredient. Plate portions are actual child items. Their references/progress survive adding, taking back and recipe invalidation. Reparenting restores a canonical world scale, avoiding the earlier pickup/drop size jump. A pot uses explicit loading/cooking/ready/warning/burning/extinguished states. The same visual root also owns a `KitchenItem` of kind `Pot` and retains its source station. Extinguished pots can be carried/staged; only the bin resets burnt contents. `PotDocked` prevents a missing pot from accepting ingredients, and redocking a burnt pot preserves the extinguished state. The bin retains the reusable pot; it must be returned before cooking. Recipe and timing parameters are documented in [Game design](GAME_DESIGN.md).

## Optional GPT-Live conversation

Follow the [README setup](../README.md#chat-with-bara-while-playing). Python 3.9+ and `aiohttp` from `scripts/voice/requirements.txt` are required. The environment or a private root `.env` supplies `OPENAI_API_KEY`. Key-free helper copies and instructions are included in both downloads.

| Component | Responsibility |
|---|---|
| `scripts/voice/live_chef_server.py` | Local session gateway, client delegation, compact Live context, hint policy and session cleanup |
| `scripts/voice/chef_brain.py` | Detailed read-only gameplay reasoning via Responses / `gpt-5.4-mini`, strict result schema and burnt-pot guard |
| `art/production/gameplay-round-01/live-chat.js` | Browser WebRTC audio, data channel, permission and immediate track shutdown |
| `Assets/Plugins/WebGL/BaraVoice.jslib` | Unity-to-browser start/context/stop bridge |
| `Gameplay/NativeLiveVoice.cs` | Native 24 kHz mono PCM16 microphone stream through the gateway WebSocket and streaming AudioClip playback |

**GPT-Live (`gpt-live-1`, Marin)** supplies continuous, interruptible conversation. The browser creates a WebRTC offer; the private server exchanges it at `/v1/live/sessions`, then attaches a server WebSocket to the opaque session ID. Only negotiated SDP and a random local session token go to the browser. Browser media flows directly to OpenAI. Native Unity uses the loopback WebSocket, which relays PCM to the primary Live WebSocket. Live manages speech timing; there is no record/submit/transcribe/synthesize turn loop.

Gameplay questions delegate to the server. Input transcript fragments are accumulated in memory, and the backend receives the recent question, four recent question/answer pairs and a fresh full kitchen snapshot. Its answer returns through `session.commentary.append` with the matching delegation ID. Casual conversation stays with Live. Large station/action/route tables stay in the reasoning backend; only compact state changes enter Live through `session.thinking.append`. All sessions and Responses requests use `store: false`.

`KitchenHelpContext` exports orders, inventory/portions, worktop and pot contents, heat/warning time, dish supply, unlocks, score/deliveries/misses/streak, idle duration and current phase. Capsule casts and station approach checks produce walkable routes; direction words follow the camera basis. Only reachable station IDs can become markers. Inventory, focus, camera-direction or kitchen changes can invalidate a pending directional answer; a discarded answer cannot trigger actions. Burnt/missing-pot advice uses an explicit recovery guard. Generated general advice can still be mistaken.

Chat never changes phase/time scale, captures game controls or cancels work. Kitchen audio ducks to 32% while connected. The browser requests acoustic echo cancellation; native PCM has no dedicated echo canceller, so headphones are preferable for the native prototype. The client sends context every two seconds. General hints require quiet conversation and a 45-second cooldown; warning/fire reminders are deduplicated per cooking cycle. Idle hints require 18 seconds of inactivity with an unfinished order. Progress encouragement follows three more actual deliveries, at least 90 seconds apart. Voice requests can disable/enable reminders or end chat.

The microphone starts only after Chat and permission. Ending chat stops capture/playback immediately, sends `session.close` and awaits `session.closed` for final usage; hangup is a fallback. Focus loss/page hiding stops chat. The server ends orphaned sessions after 25 seconds without a game heartbeat, and caps each chat at 15 minutes. Native generation checks reject late transport callbacks. Service remains playable if permission, helper, network, quota or API access fails.

The gateway binds **127.0.0.1:54115 only**, checks Host/Origin and a custom client header, permits one chat at a time, limits request sizes and sanitizes upstream failures. Browser origins are localhost/127.0.0.1 on ports 54114, 54115 and 8000. It serves no static files. Unity's HTTP setting enables the fixed loopback endpoint; upstream API traffic uses TLS. A public release would need an authenticated hosted gateway, per-player quotas and abuse controls, with no shared key embedded in clients.

The helper writes no audio, transcript or context logs. OpenAI receives continuous audio only during an explicitly enabled chat. `store: false` is not a promise of zero provider retention. The button/status identify AI voice and an active microphone. GPT-Live is billed by connected duration, including silence; reasoning usage is separate. Current API tests are outside the historical development token checkpoint.

Validation:

```sh
.local/voice-venv/bin/python scripts/voice/test_live.py
# Explicit paid smoke tests using only synthetic speech/state fixtures:
.local/voice-venv/bin/python scripts/voice/check_native.py --live
node scripts/voice/check_browser.cjs --live
```

The four-kitchen native suite passes **896 assertions**, including voice context/routes and unchanged service/pause state. Offline tests cover hints, cooldowns, burnt-pot recovery, stale replies, session close and localhost boundaries. [Voice QA](../art/production/gameplay-round-01/qa/voice/) records synthetic Live transport checks and real Unity browser interaction. Synthetic test transcripts may be saved as QA evidence; runtime conversations are not. Hardware microphone quality, acoustic echo and wider device/browser coverage remain user playtest work.

Official contracts: [GPT-Live model and duration pricing](https://developers.openai.com/api/docs/models/gpt-live-1), [WebRTC](https://developers.openai.com/api/docs/guides/voice-webrtc?api=live), [WebSockets](https://developers.openai.com/api/docs/guides/voice-websockets?api=live), [client delegation](https://developers.openai.com/api/docs/guides/live-delegation), [context and lifecycle](https://developers.openai.com/api/docs/guides/live-conversations).

## Content and regeneration

| Source | Output / workflow |
|---|---|
| `art/concepts/levels-round-02/levels.json` | Approved discrete floor plans |
| `scripts/build_kitchen_models.py` | Editable mesh JSON, GLB/OBJ/MTL prop kit and room placement manifests |
| `art/production/round-01/characters/` | Original model files, textures, references, prompts and generation/rig records |
| `Editor/ArtProductionBuilder.cs` | Unity meshes/materials, prefabs, scene art and renders |
| `Editor/ArtAnimationBuilder.cs` | Shared motion rig, Animator/AnimationClips and studio reviews |
| `Editor/GameplayBuilder.cs` | Catalog, room collision boundaries, stations, icons, UI setup and playable entry scene |
| `scripts/compose_room_glbs.py`, `scripts/build_art_review.py` | Portable populated room GLBs and browser review pages |

`ArtProductionBuilder.BuildPotCleanupIcons` refreshes only the empty/burnt inventory icons. `python3 scripts/package_demo.py` packages existing Web/Mac exports into versioned ZIPs and verifies their CRCs/SHA256 hashes.

Normal local rebuilding uses downloaded sources; it makes no generation API calls. Use Unity's **Bara Kitchen** menu. **Build playable game** refreshes gameplay assets. **Rebuild rooms and materials** refreshes environment data while keeping character prefabs. **Build first art batch** is a broader regeneration and can overwrite generated prefabs; save manual variants separately first. `BuildPolishedGame` rebuilds environment and gameplay before exporting Web.

Python tooling dependencies: `python3 -m pip install -r requirements.txt`. Browser tooling: `npm install`, then `npx playwright install chromium`. Tests default to Playwright's browser; `BARA_CHROME` may select an installed executable. `BARA_NODE_MODULES` optionally selects an external dependency tree. Run scripts from the repository root. Raw captures/build caches go under ignored `.local`.

Paid asset experiments are separate scripts: `scripts/meshy_test.py`, `scripts/meshy_production.py`, and `scripts/audio/generate_cafe.py`. They require a private root `.env` and service credit. Never commit it or call those scripts for an ordinary rebuild. Generation scripts record task IDs and avoid blindly resubmitting uncertain paid requests. Existing generation JSON retains asset provenance; credentials are excluded.

## Character and rendering details

The twelve current Meshy models use textured source GLB/FBX and 2K base-color textures. Five Meshy auto-rigs succeeded; seven failed pose estimation. Successful source rigs/walk/run downloads remain separate. The playable cast uses a local approximate eleven-bone rig and shared native clips, preserving the approved tiny-legged proportions. Eleven shared clips × twelve looks give 132 review combinations, plus the fire review. Animation sockets attach tools. These are not final artist-authored production skin weights; sleeves, armpits and some prop contacts need further cleanup.

The reusable kit has 60 mesh assets, including one retired bench retained as source; active rooms use tables/chairs. Four ingredient state prefabs, a universal dish and cooking pot preserve root state. Current clothes are baked costume presets rather than modular garment meshes.

Linear color space, the local Soft Color shader, warm minimum illumination, wrapped diffuse shading and cast shadows preserve color and volume. Four-sample MSAA, high-resolution shadows, texture mipmaps/trilinear filtering and appropriate normal/surface handling reduce jagged edges. Transparent highlight and dash shaders provide animated feedback. The camera caches static room bounds rather than fitting moving particles/food every frame, preventing view pumping.

## Build and validate

Set `UNITY` to the editor executable for the version in `ProjectSettings/ProjectVersion.txt`. Example shell commands, from the repository root:

```sh
"$UNITY" -batchmode -projectPath "$PWD/Unity/BaraKitchen" \
  -executeMethod GameplayChecks.Begin -logFile "$PWD/.local/checks.log"

"$UNITY" -batchmode -projectPath "$PWD/Unity/BaraKitchen" \
  -executeMethod ArtProductionBuilder.BuildWebOnly -quit \
  -logFile "$PWD/.local/web-build.log"

"$UNITY" -batchmode -projectPath "$PWD/Unity/BaraKitchen" \
  -executeMethod ArtProductionBuilder.BuildMacDemo -quit \
  -logFile "$PWD/.local/mac-build.log"
```

Create `.local` before invoking these. **Do not add `-quit` to GameplayChecks.Begin**: the play-mode checks terminate the editor themselves. Web export requires the Web support module. Do not run two editor builds against the same project simultaneously. `BuildWebOnly` reuses the generated catalog; use `BuildGameWeb` if changing catalog/layout sources. Web output is currently a development build, without download-size optimization. The Mac exporter installs the new icon and writes `.local/release/Bara Kitchen.app`; the shipped executable is arm64. The ZIP is not notarized.

With the local server running:

```sh
node scripts/gameplay/play_full_level.cjs 1
node scripts/gameplay/play_full_level.cjs 2
node scripts/gameplay/play_full_level.cjs 3
node scripts/gameplay/play_full_level.cjs 4
```

These use real UI selection, keyboard input, route finding and read-only telemetry. They cook/serve multiple meals, wash and reuse dishes, exercise dash/rail throws, pause, wait out natural order/round timers, check results and retry. Level 2 also lets a batch warn/burn, extinguishes it and carries the pot to the bin for cleanup. `BARA_BURNT_POT_CHECK=1 BARA_CAPTURE=0 node scripts/gameplay/play_full_level.cjs 2` runs the focused trash-trip/reuse keyboard test, saving separate evidence in `qa/burnt-pot`. Optional `BARA_CAPTURE=0` skips recording. Test scripts do not set the score or clock. Native checks may advance simulation directly to cover boundary conditions.

Version 0.1.1 has **785 passing native assertions**, plus a passing [keyboard cleanup/reuse test](../art/production/gameplay-round-01/qa/burnt-pot/level-2.json) with 53 checks. The original 0.1.0 handoff includes full-round browser evidence for all four kitchens in [QA](../art/production/gameplay-round-01/qa/full-playthrough). Separate existing camera, layout, exploration and audio checks cover saved angles, viewport limits, plate freedom, real audio signal and pause silence. The Mac export received a launch smoke check; complete rounds were exercised in the Web build. Automated success is not evidence that every star target or route feels enjoyable; human balance and broader hardware testing remain.

## Trailer and icon

[Trailer source and final MP4](../art/trailers/round-01/) contain the title composition, recorded Unity canvas clips and `edit.json`. The new [icon](../art/brand/round-01/) was generated with the built-in image tool, then used in the title, browser favicon and Mac application icon. Its prompt/method record is retained.

`node scripts/trailer/capture_title.cjs` renders the HTML title. `python3 scripts/trailer/render_trailer.py` renders the edit with FFmpeg; set `FFMPEG_BIN` or install FFmpeg on PATH. The opening and closing reuse the identical three-second title clip with a gentle push-in. Soup and fire shots use a 45° azimuth at roughly 43° elevation, with closer crops to expose the pot beside the chef. Recapture them with `BARA_TRAILER_POT_RETAKE=1 node scripts/gameplay/play_full_level.cjs 2`; the separate retake report is under the trailer QA folder. Gameplay footage is real input-driven capture, edited with cuts/crops; game music and foley are mixed in post. Export: 1920×1080, 30 fps, H.264 MP4, AAC stereo at 48 kHz. The poster is a video frame. This follows the relevant [Steam trailer guidance](https://partner.steamgames.com/doc/store/trailer); it is a review trailer, not a published Steam store listing.

## Audio and third-party materials

Nineteen local audio clips ship. Existing recordings were preferred; no further audio generation was requested after that preference. The first ElevenLabs batch already existed, and six retained clips supply service/hurry/fire music, two nonverbal guest reactions and extinguisher spray. Their prompts and per-asset records are in [generation.json](../art/audio/round-01/generation.json). These are separate from the CC0 collection.

| Use | Source / attribution | Processing |
|---|---|---|
| Chop | Joseph SARDIN, BigSoundBank [Zucchini #3 / 2070](https://bigsoundbank.com/cutting-board-zucchini-3-s2070.html), CC0 | One 0.48s strike; gain/fades |
| Wash | Joseph SARDIN, BigSoundBank [Stir in water #2 / 0651](https://bigsoundbank.com/stir-in-water-2-s0651.html), CC0 | Gentle compressed 3.88s loop |
| Fire | Joseph SARDIN, BigSoundBank [Big branching fire #2 / 0988](https://bigsoundbank.com/big-branching-fire-2-s0988.html), CC0 | Quiet 5.88s loop |
| Dish | Kenney [Impact Sounds](https://kenney.nl/assets/impact-sounds), `impactPlate_light_000.ogg`, CC0 | Mono/gain |
| Footsteps | Same pack, `footstep_wood_000.ogg`, CC0 | Quiet, varied pitch |
| Pickup | Same pack, `impactSoft_heavy_000.ogg`, CC0 | Reduced gain |

`success`, `ready`, `new-order`, `warning`, `expired`, `ui` and `whoosh` are original locally synthesized cues. `scripts/audio/prepare_cafe.py` prepares the pack; run `scripts/audio/use_existing.py` afterward to apply the CC0 replacements. Original downloads and license text stay in [audio sources](../art/audio/sources/). The extra researched cutlery recording is not an active sound.

Lilita One and Varela Round font license files are retained beside the fonts in `Gameplay/UI/Fonts`. The local model-viewer 4.1.0 library is Apache 2.0; its license is in the production viewer's vendor directory. Meshy model provenance and image-generation prompts remain with the assets. Do not label the entire asset collection CC0: that license applies to the identified recordings only.

## Handoff limits and next work

The demo has English UI, desktop keyboard/mouse controls, local saves, approximate shared character skinning and a large Web download. There is no installer, notarization, Windows native build, Steam integration, public hosted backend or multiplayer. The optional live voice helper is a local development service, not autonomous chef control. Keep the approved art direction and floor plans as baselines while tuning; see [prompt summary](PROMPT_SUMMARY.md) for the decisions behind them. Save-format migrations and separate Calm scores should be preserved when extending settings. Re-run a full service loop after changes to item ownership, customer return or action cancellation.

## Development time accounting

`docs/development-metrics.json` separates elapsed project time from recorded active assistant time. Its `developmentTime` section covers the 37 completed turns through v0.1.1, ending at `2026-09-14T13:07:00.339Z`; the timing audit itself is excluded. The token checkpoint is separate and remains at its previously documented timestamp.

Active time sums the runtime's `task_complete.duration_ms` once per unique completed turn ID. Elapsed time uses the first matching `task_started` timestamp and final completion timestamp. The sanitized [timing ledger](development-time-turns.json) publishes only timestamps and durations. This totals 26,616,969 ms active over 526,614,242 ms elapsed. The sum of raw turn start/end intervals is 270,162 ms longer than the reported active durations; the records do not explain this discrepancy, so no cause is invented. Reported durations, not a heuristic that caps gaps between messages, define the active total.

Recompute from the private local session log with `python3 scripts/update_development_time.py /path/to/session.jsonl --through 2026-09-14T13:07:00.339Z --commit 949aea3`. An explicit completed-turn cutoff prevents the current audit from counting itself. The script checks matching boundaries, duplicate records, overlap and daily attribution. Raw session logs, prompts, tool outputs and credentials are not published.
