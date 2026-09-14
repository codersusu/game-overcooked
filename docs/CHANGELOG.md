# Bara Kitchen — changelog

This is a reconstructed development history from the conversation and saved artifacts. Earlier stages predate this repository's first commit; they are not invented Git revisions. Dates are calendar milestones, not continuous labor estimates.

## Trailer camera revision · 14 September 2026

- Recaptured soup cooking and fire suppression with a 45° diagonal camera so the chef does not cover the pot. Tightened the framing around the station and checked the warning, flame, spray and extinguished states.
- Kept the 47.9-second edit and matching three-second title sequences. Updated the trailer sources and the existing release video; the playable game builds are unchanged.

## 0.1.0 — review and repository handoff · 14 September 2026

- Completed real keyboard full-round playthroughs of all four kitchens: multiple deliveries, dish reuse, order expiry, natural round completion, results and retry. Levels 2–4 cover dash; 3–4 cover barrier throws. Level 2 covers warning, fire, suppression and clearing. All passed without browser runtime errors.
- Retained 536 passing native gameplay checks plus camera, layout, audio and free-placement evidence.
- Added the capybara game icon, a 47.9-second trailer from actual gameplay, identical three-second opening/closing title sequences, captured source clips and a reproducible edit script.
- Exported an Apple silicon Mac demo and packaged the tested desktop Web build. Mac received a launch smoke test; full rounds were tested in Web.
- Consolidated project prose into README, game design, implementation, changelog and prompt summary. Preserved attribution, approved references and usage counters; removed superseded prose from the tracked working tree while keeping a local archive.
- Published the source, assets, large-file tracking, downloadable ZIPs and trailer to the GitHub repository. No historical commits are fabricated.

## Playable systems and polish · 14 September 2026

- Implemented four kitchens, six chef choices, customer entry/seating/eating/exit, ticket patience, scores/streaks/stars, round timers, practice, Calm mode, local persistence, settings, pause and results.
- Added salad assembly, soup cooking/burning, finite plate return/washing, dash and throws. Recipe validation uses exact preparation states.
- Replaced webpage-like UI with café signs, recipe cards, rounded fonts and a native Unity HUD. Increased the game viewport and removed routine green instructional popups. Added short picture guides and small pot progress bars.
- Allowed raw/prepared ingredient piles on plates and counters. Added reversible portion retrieval and plate-to-pot transfer. Retained consistent plate scale through carrying and placement.
- Improved soft color shading, shadows, highlights and antialiasing. Added 19 audio clips with service/hurry/fire music, cooking foley and nonverbal guest sounds; preferred available CC0 sounds after the user's request.
- Changed chopping/washing to tap-to-start, with movement cancelling work.
- Added persistent tilt and horizontal orbit controls, camera-relative movement and automatic whole-room framing.

## Production art and motion revisions · 13–14 September 2026

- Generated the first twelve capybara/cat/dog model presets, reusable foods/tools/furniture and four Unity room scenes with adjoining cafés. Five Meshy auto-rigs succeeded; local shared rigging supports the full cast.
- Preserved the approved C1 proportions despite automatic rigging difficulty. Current clothing remains baked into separate model presets.
- Enlarged rooms/furniture relative to animals, removed the couch, added flowers/decor, then widened the dining area another 40% while preserving kitchen layout.
- Lowered worktops to paw height so ingredients and actions sit below the head.
- Reworked idle, walking, dash, carrying, throw, chopping, washing, seated idle/eating and extinguishing. Corrected vertical knife orientation, restrained side-arm idle and fire-fighting sway, and moved washing to the plate rim.
- Extended the warning phase to eight seconds and added flame/smoke/spark/spray effects. Generated all 132 character/action combinations and the fire review; sleeve/skin cleanup remains.

## Concept and scope decisions · 8–13 September 2026

- Chose a small single-player Unity demo before AI helpers or voice control. Deferred upgrades and retained gradual introduction of dash/throw.
- Selected capybara C1 after comparative silhouette/face studies: round larger head, broad short muzzle, dark animal eyes, smaller body, tiny legs and simple paws.
- Expanded each species to four readable chef/customer and male/female cosmetic looks, with contrasting accessories and varied clothing. Approved roster round 2.
- Designed ingredients, dishes and kitchen kit. Replaced carrot with cucumber in the first salad; added a fourth level combining learned skills.
- Rejected similar early room plans. Approved round-2 U, island, single-divider and zigzag layouts with ample ordinary worktops and throw-over rails.
- Unified plates/bowls and removed recipe-specific assembly stations. Established an adjoining dynamic animal café as part of every scene.

## Remaining work

Human difficulty/first-time-player testing; character weight and contact cleanup; smaller/faster builds and broader hardware coverage; native Windows/Intel distribution if desired; localization/accessibility expansion. AI assistance, voice commands, upgrades and additional content remain deferred.
