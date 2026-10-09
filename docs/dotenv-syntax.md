# Environment file syntax

Save the file as UTF-8. Each assignment occupies one line:

```dotenv
# A comment
DOTENV_SAMPLE=loaded-from-file
EMPTY=
export FEATURE=enabled
TOOLS="C:\tools\bin;C:\another directory"
LABEL='text with # inside'
NAME=value # trailing comment
```

- Blank lines and lines beginning with `#` are ignored.
- `export ` is optional. Names cannot contain whitespace, NUL, or `=`.
- Leading/trailing whitespace around unquoted values is removed. Matching single or double quotes preserve enclosed spaces.
- A trailing comment follows a quoted value, or a whitespace-separated `#` after an unquoted value.
- Empty values are supported. Environment names are case-insensitive; the last duplicate wins.
- Backslashes, dollar signs, percent signs, and equals signs in values remain literal.
- No shell execution, variable interpolation, escape processing, multiline values, or file includes.
- An unmatched quote, unexpected text after a quoted value, or NUL is an error. Diagnostics identify the line without printing its contents.

The file is reread at each launch. File values are intended to override matching debugger environment variables while preserving unrelated variables; real debugger acceptance testing is required before release.
