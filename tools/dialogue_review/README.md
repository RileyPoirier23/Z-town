# Dialogue review

Every line in the game and every parody name (brands, TV shows) needs your OK before a
release can go out. This is where you give it.

## Review in the browser

```
pip install -r tools/dialogue_review/requirements.txt
python tools/dialogue_review/serve.py
```

It opens a page listing everything waiting for review, grouped by file, with the context
for each line. Edit the words, then **Approve**, **Reject** or leave it as **draft**.
Changes are saved straight into `game/dialogue/*.yaml` and the name files.

Lines in `[BRACKETS]` are placeholders: they need memere's real words from you.

## Review in a spreadsheet

```
python tools/dialogue_review/export.py            # writes dialogue_review.xlsx and .csv
python tools/dialogue_review/export.py --import dialogue_review.xlsx   # applies your edits
```

## What the statuses do

| Status | Dev build | Release build |
|---|---|---|
| draft | shows with a **[DRAFT]** tag | **blocks the release** |
| approved | shows normally | ships (unless it's still a `[placeholder]`) |
| rejected | never shown | cut |

Check what's still blocking a release: `dotnet run --project tools/ZTown.Tools -- release-check`
