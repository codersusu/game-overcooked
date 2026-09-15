#!/usr/bin/env python3
"""Build the showcase page, submission prose and portable image/prompt kit.

The JSON is the editable content source. Images are byte-for-byte copies of
existing project artifacts; this script does not generate or retouch visuals.
"""
from pathlib import Path
import hashlib
import html
import json
import zipfile

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'art/showcase/round-01'
DATA = json.loads((OUT / 'submission.json').read_text())
MEDIA = {item['id']: item for item in DATA['media']}
REPO = DATA['repository']


def esc(value):
    return html.escape(str(value), quote=True)


def repo_link(path):
    route = 'tree' if (ROOT / path).is_dir() else 'blob'
    return f'{REPO}/{route}/main/{path}'


def build():
    for field in DATA['fields']:
        if 'from' in field:
            field['value'] = DATA[field['from']]
        if 'limit' in field:
            assert len(field['value']) <= field['limit'], field['id']
    assert len(DATA['steps']) == 8
    evidence = []
    for item in DATA['media']:
        target, source = OUT / item['file'], ROOT / item['source']
        assert target.read_bytes() == source.read_bytes(), item['id']
        evidence.append({'file': item['file'], 'source': item['source'],
                         'sha256': hashlib.sha256(target.read_bytes()).hexdigest(),
                         'modifications': 'None; exact copy of existing artifact'})
    for step in DATA['steps']:
        assert all(key in MEDIA for key in step['media'])
        assert all((ROOT / path).exists() for path in step['references'])
    md = [f"# {DATA['title']} — showcase submission", '', DATA['tagline'], '',
          DATA['description'], '',
          f"Format reference: [Void Explorer]({DATA['reference']}). "
          f"Prepared for the [official submission form]({DATA['submissionForm']}) "
          f"as checked on {DATA['preparedDate']}. {DATA['status']}", '',
          '## Suggested cover', '',
          f"![{MEDIA['cover']['alt']}](images/cover.png)", '',
          MEDIA['cover']['caption'], '', '## Build process', '', DATA['promptNote'], '']
    for i, step in enumerate(DATA['steps'], 1):
        m = MEDIA[step['media'][0]]
        md += [f"### {i}. {step['title']}", '', step['summary'], '',
               f"![{m['alt']}]({m['file']})", '', f"*{m['kind']} — {m['caption']}*", '',
               '> ' + step['prompt'], '', f"Result: {step['result']}", '',
               'Supporting artifacts: ' + ' · '.join(
                   f'[{Path(p.rstrip("/")).name}]({repo_link(p)})' for p in step['references']), '']
    md += ['## Submission copy', '',
           'Short answers below fit the limits shown in the official form. '
           'Review items are marked; contact details are intentionally left for the creator.', '']
    for f in DATA['fields']:
        suffix = f" ({len(f['value'])}/{f['limit']} characters)" if 'limit' in f else ''
        md += [f"### {f['label']}{suffix}", '', ('**Review before sending.** ' if f.get('review') else '') + f['value'], '']
        if f.get('note'):
            md += [f['note'], '']
    md += ['## Media selection and provenance', '',
           '| File | Type | Suggested use |', '|---|---|---|']
    for m in DATA['media']:
        md.append(f"| [{Path(m['file']).name}]({m['file']}) | {m['kind']} | {m['caption']} |")
    md += ['', 'All selected files are unchanged copies of the sources recorded in '
           '[media-manifest.json](media-manifest.json). The concept sheets and icon are generated art; '
           'gameplay screenshots show the implemented build. The cooking game and most captures predate '
           'the voice addition. No new game content or audio was generated for this submission package.', '',
           '## Trailers', '',
           f"- [Gameplay trailer · 48 seconds]({repo_link('art/trailers/round-01/Bara-Kitchen-Trailer.mp4')})",
           f"- [Live voice trailer · 57 seconds]({repo_link('art/trailers/round-02/Bara-Kitchen-Voice-Trailer.mp4')})", '',
           '## Notes for the submitter', '']
    md += ['- ' + note for note in DATA['editorNotes']]
    md += ['', 'For asset sources and licenses, see '
           f"[Implementation]({repo_link('docs/IMPLEMENTATION.md')}). "
           'For development time and token usage, use the scoped historical checkpoint in the '
           f"[README]({repo_link('README.md')}); it is not a total for all later voice and showcase work.", '']
    markdown = '\n'.join(md)
    (OUT / 'submission.md').write_text(markdown)
    docs_markdown = markdown.replace('](images/', '](../art/showcase/round-01/images/').replace(
        '](media-manifest.json)', '](../art/showcase/round-01/media-manifest.json)')
    (ROOT / 'docs/SHOWCASE_SUBMISSION.md').write_text(docs_markdown)
    (OUT / 'media-manifest.json').write_text(json.dumps({
        'preparedDate': DATA['preparedDate'], 'selection': evidence,
        'fieldLengths': {f['id']: {'count':len(f['value']), 'limit':f['limit']} for f in DATA['fields'] if 'limit' in f}
    }, indent=2) + '\n')
    cards = []
    for i, s in enumerate(DATA['steps'], 1):
        refs = ' · '.join(f'<a href="{esc(repo_link(p))}" target="_blank" rel="noreferrer">{esc(Path(p.rstrip("/")).name)}</a>' for p in s['references'])
        cards.append(f'''<details class="step" id="{s['id']}" data-step="{i-1}" {'open' if i == 1 else ''}>
<summary><span class="number">{i:02}</span><span><strong>{esc(s['title'])}</strong><small>{esc(s['summary'])}</small></span><span class="plus" aria-hidden="true">+</span></summary>
<div class="step-body"><p class="prompt" id="prompt-{i}">{esc(s['prompt'])}</p>
<button class="copy" data-copy="prompt-{i}">Copy prompt <span aria-hidden="true">↗</span></button>
<p class="result">{esc(s['result'])}</p><p class="refs">{refs}</p></div></details>''')
    fields = []
    for f in DATA['fields']:
        count = f"{len(f['value'])} / {f['limit']}" if 'limit' in f else ''
        fields.append(f'''<article class="field"><div class="field-top"><h3>{esc(f['label'])}</h3><small>{count}</small></div>
{'<span class="review">Review before sending</span>' if f.get('review') else ''}<p id="field-{f['id']}">{esc(f['value'])}</p>
{f'<small>{esc(f["note"])}</small>' if f.get('note') else ''}
{f'<button class="copy" data-copy="field-{f["id"]}">Copy answer ↗</button>' if not f.get('review') else ''}</article>''')
    template = (ROOT / 'scripts/showcase/page.html').read_text()
    replacements = {
        '__TITLE__':esc(DATA['title']), '__TAGLINE__':esc(DATA['tagline']),
        '__TAGS__':''.join(f'<span>{esc(t)}</span>' for t in DATA['tags']),
        '__DESCRIPTION__':''.join(f'<p>{esc(p)}</p>' for p in DATA['description'].split('\n\n')),
        '__PROMPT_NOTE__':esc(DATA['promptNote']), '__STEPS__':'\n'.join(cards),
        '__FIELDS__':'\n'.join(fields),
        '__NOTES__':''.join(f'<li>{esc(n)}</li>' for n in DATA['editorNotes']),
        '__DATA__':json.dumps(DATA, ensure_ascii=False).replace('</', '<\\/'),
    }
    for key, value in replacements.items():
        template = template.replace(key, value)
    (OUT / 'index.html').write_text(template)
    archive = OUT / 'Bara-Kitchen-Showcase-Kit.zip'
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as z:
        for name in ['index.html', 'submission.json', 'submission.md', 'media-manifest.json']:
            z.write(OUT / name, 'Bara-Kitchen-Showcase/' + name)
        for m in DATA['media']:
            z.write(OUT / m['file'], 'Bara-Kitchen-Showcase/' + m['file'])
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None
    print(json.dumps({'steps':len(cards), 'images':len(evidence), 'fields':len(fields),
                      'allFieldLimitsPass':True, 'exactImageCopies':True,
                      'portableKitBytes':archive.stat().st_size}, indent=2))


if __name__ == '__main__':
    build()
