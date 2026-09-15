# Bara Kitchen — prompt and feedback summary

Condensed from the user's requests in this development conversation. This is a thematic summary, not a verbatim transcript. Unity/Blender learning questions and credentials are intentionally omitted. Later accepted feedback takes precedence over earlier explorations.

## Original direction

Build a cute animal kitchen game, initially called Beara Kitchen and then Bara Kitchen because the default chef is a capybara. Study Overcooked's cooking interactions, visual readability, animations and levels as a reference. Test a small single-player game first; perhaps add an autonomous support chef and voice task assignment later. Prefer a limited demo with four levels before expansion. Choose Unity for development and available assets. Work in reviewable stages: art/proportions, models and animation, game systems, then assembled levels and playtests.

## Scope and mechanics

Set upgrades aside. Introduce actions such as dash and ingredient throw gradually; remote oven shutdown is a parked idea. The first dish should use chopped tomato and chopped cucumber instead of carrot. Add a fourth level combining learned mechanics, with a new dish only if necessary.

Do not require a special salad station. A salad is created by combining the proper chopped ingredients on a plate. Provide several empty worktops. Do not distinguish plates and bowls. Make each level spatially different; add barriers that block walking but allow ingredients to be thrown over them. The user accepted the redesigned second-round plans as the starting point, open to playtest improvements.

Later feedback asked for greater freedom: raw ingredients and chopped soup ingredients may sit on any worktop or plate, even when that does not produce a valid recipe. Wrong combinations should look like natural piles and invite experimentation. Allow ingredients to be removed again. Chopping/washing should start with one key press and continue until done, with movement cancelling.

## Animal art

Keep a recognizable animal face and connected upright limbs. Use an oversized rounded head, shorter muzzle, tiny legs, a smaller body, soft surfaces and simple paws/feet without human fingers. Reject long horse-like snouts, human-looking eyes and excessively broad bodies. Compare several directions, then preserve the chosen C1 face/proportions as the reusable standard.

Include cat and dog; explore red panda, rabbit and otter. Raccoon is optional if it looks too similar to red panda. Animals can be chefs or customers. Each species needs male/female versions in both roles, with equivalent abilities. Both presets need visible decoration, stronger color contrast and clearer silhouettes. Tiny flowers matching the fur are not readable. Everyday clothing should vary by character rather than repeating one outfit. The second wardrobe round was accepted.

Use existing or generated 3D assets where useful; test image-to-3D and rigging with Meshy. Keep source assets and prompts for future reuse. Credits/API access were authorized for development, but secrets must stay outside the shared project.

## Rooms and rendering

Include the customer area beside every kitchen: animals arrive, sit, eat and leave. Use tables/chairs, remove the couch, add flowers and decoration. Increase room scale relative to animals. The later dining-area request was to widen it 30–50% while keeping the kitchen layout; the implemented adjustment was 40%.

Keep the original colorful models when rendering in Unity. Add depth through light/shadow and better surfaces; avoid gray, flat-looking food, sinks, bins and pots. Investigate rough edges/jagged silhouettes rather than assuming more polygons alone solve them. Standardize plate size across all states and improve interactive/serving highlights with attractive motion.

## Animation feedback

Watch the clips rather than relying on names: the first pass looked nearly static. Idle should gently raise/lower arms along the sides; halve the excessive lift. Walk and carry-walk must visibly move feet. Dash should lean into fast movement and show an afterimage. Throwing needs a visible arm/paw action.

Chopping should keep the head upright with arm motion and a vertical knife, blade down. Lower the tables to hand height below the head. Simplify washing to a small up/down motion at the plate rim, preserving the handoff to the other hand. Check dog arm/sleeve self-intersection, not just hand-to-plate contact, and inspect all animals. Fire-fighting sway should be subtle with the extinguisher following the body. Customers need visible seated idle and eating. Fire should have a readable small-flame warning long enough to react before full burning. Generate the revised motion set across the current models.

## Systems, UI, sound and camera

Implement recipe tickets with patience bars, delivery/expiry feedback, animated serving guidance, score, round countdown, dynamic customers, menus, kitchen selection and settings. Make the UI feel like a game in this café, not a website.

Give the kitchen/café more screen space. Keep large text notifications out of the scene, then remove most routine instructional notifications entirely. Replace written cooking countdowns with tiny progress bars above pots. Use minimal static picture tutorials per level and leave exploration to the player.

Add everyday cooking music, urgent/time-low/fire music and action sounds. Customers should make cute nonverbal reactions rather than intelligible dialogue. Search existing sound libraries first; use ElevenLabs only where a suitable existing asset is unavailable.

Allow the player to choose an angled or nearly overhead view by scrolling, and to turn the camera left/right. Keep a near-overhead default if free control is unavailable. The implemented camera supports both tilt and horizontal rotation with saved preferences.

## Final review and handoff

Playtest every level. Create a roughly 30–60-second Steam-style trailer using key aspects of the game. Start with three seconds of dark background, enlarged title/icon framing, and end with the same scene. Create a matching cute game icon.

Push code, assets and trailers to `codersusu/game-overcooked` so development can continue there. Clean up the documents into an overview/README, game design, implementation, changelog and this prompt summary. Include screenshots, asset/trailer/download links, development duration and measured input/output/cache token usage; distinguish recorded usage from unavailable billing totals.

## Trailer review feedback

The overall trailer was accepted. In the soup-cooking and fire-fighting shots, the chef obscured the stove from the straight-on camera. Recapture those two scenes at a roughly 45° diagonal angle so the pot and the action remain visible.

## Burnt-food cleanup feedback

Submit all completed work to Git. After the fire is extinguished, burnt food must remain in the pot and be taken to the trash before the pot can cook again. The implementation carries the original pot to the bin, discards its contents, and returns the empty pot to the stove.

## Development time request

Check the total elapsed creation period and actual active development time, and update the repository with both figures and their measurement basis.

## Contextual voice help request

Use the available OpenAI API to let the player ask the chef how to play at any time. The program should supply the live kitchen context so the answer can give concrete directions, such as collecting vegetables on one side and using a chopping board on the other. Enable the feature in the game. This request is for spoken gameplay support; it does not ask for autonomous cooking or voice task execution.

## Live conversation refinement

Use GPT-Live for a more natural ongoing conversation. Remove writing because typing is impractical during gameplay; provide a simple chat button. Beyond how-to questions, the chef should discuss whether the player is doing well, offer hints when the player is idle, and remind them about important unattended tasks such as a burning pot. The game should continue while the player talks. This remains companionship and advice, not an autonomous cooking assistant.

## Cuter voice and second trailer

Make the assistant voice cuter to fit the animal café. Create a second trailer emphasizing in-game voice support: show different player questions and the chef’s replies, plus casual conversation. End by asking “What is the cutest animal in this kitchen?” with a playful answer along the lines of “Me, Bara.” Keep the original gameplay trailer.

The closing “hee-hee” sounded strange/scary; remove it and keep “Me! Bara, the capybara.” Never distribute the developer’s key. Let players supply their own key through a popup.

## Developer showcase contribution

Prepare an extra submission page inspired by the OpenAI developer showcase's Void Explorer entry. Select representative screenshots and pair them with short prompts that guide the major build stages, rather than a comprehensive specification. Include reusable submission material based on this game's actual development and feedback. The resulting [showcase guide](SHOWCASE_SUBMISSION.md) labels the prompts as condensed reconstructions.
